<#
.SYNOPSIS
Prepares a PR for review: verifies the repo, materialises an isolated worktree of the
PR head, computes the review scope, and writes a single review-context.json the skill consumes.

.DESCRIPTION
This is the "get the code" + "scope the review" plumbing for the code-review-osa skill,
collapsed into one call so SKILL.md does not inline six git commands. It:
  1. verifies the git remote is the expected repo,
  2. fetches PR metadata (title, body, base ref, head SHA),
  3. fetches the PR head into a tracking branch and adds a git worktree in $env:TEMP,
  4. computes the merge-base and the changed-file list,
  5. partitions changed files into lanes (F#, Docs, Dart, Other) deterministically,
  6. writes the F#+Docs unified diff, and
  7. writes review-context.json with every variable the orchestrator needs.

The orchestrator reads {TEMP}\cr-review-{PrNumber}\review-context.json after this runs.
The table printed to stdout is for the human watching; the JSON file is the contract.

.PARAMETER PrNumber
The pull request number to prepare.

.PARAMETER Repo
GitHub repository slug to verify against and query. Default: 'michaelkargl/osaHealth'.

.EXAMPLE
.\Prep-Pr.ps1 -PrNumber 22
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)][string]$PrNumber,
    [string]$Repo = 'michaelkargl/osaHealth'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# --- Read-only: verify remote --------------------------------------------------
$origin = git remote get-url origin
if ($LASTEXITCODE -ne 0 -or $origin -notmatch [regex]::Escape($Repo)) {
    throw "Cannot review PR $PrNumber -- the git remote ('$origin') is not $Repo. Run from a local clone of that repo."
}

# --- Derived paths -------------------------------------------------------------
$tempDir      = $env:TEMP
$branch       = "cr-pr-$PrNumber"
$worktreePath = Join-Path $tempDir "cr-worktree-$PrNumber"
$reviewDir    = Join-Path $tempDir "cr-review-$PrNumber"
$diffsDir     = Join-Path $reviewDir 'diffs'
$diffPath     = Join-Path $diffsDir 'fsharp-docs.diff'
$contextPath  = Join-Path $reviewDir 'review-context.json'

# --- Read-only: PR metadata ----------------------------------------------------
$metaJson = gh pr view $PrNumber --repo $Repo --json title,body,headRefName,baseRefName,headRefOid
if ($LASTEXITCODE -ne 0) { throw "gh pr view failed for PR $PrNumber (the PR may not exist)." }
$meta = $metaJson | ConvertFrom-Json

$baseRef     = $meta.baseRefName
$headSha     = $meta.headRefOid
$title       = $meta.title
$bodySummary = if ($meta.body) { $meta.body.Substring(0, [Math]::Min(500, $meta.body.Length)) } else { '' }

# --- Dry-run gate: everything below mutates disk/git ---------------------------
$plan = @(
    "git fetch origin pull/$PrNumber/head:$branch"
    "git worktree add $worktreePath $branch"
    "git merge-base origin/$baseRef $branch"
    "git diff --name-only <merge-base>..$branch"
    "git diff --output=$diffPath <merge-base>..$branch -- <F# + Docs files>"
    "write $contextPath"
)
if (-not $PSCmdlet.ShouldProcess("PR $PrNumber", 'prepare review worktree and context')) {
    Write-Output 'WhatIf -- would run, mutating nothing:'
    $plan | ForEach-Object { Write-Output "  $_" }
    return
}

# --- Pre-clean stale state from a previously aborted run -----------------------
if (Test-Path $worktreePath) { git worktree remove $worktreePath --force }
git worktree prune
if (git branch --list $branch) { git branch -D $branch }

# --- Fetch + worktree ----------------------------------------------------------
git fetch origin "pull/$PrNumber/head:$branch"
if ($LASTEXITCODE -ne 0) { throw "git fetch failed for PR $PrNumber (the PR may have been deleted)." }

git worktree add $worktreePath $branch
if ($LASTEXITCODE -ne 0) { throw "git worktree add failed for PR $PrNumber." }

# --- Scope: merge-base + changed files -----------------------------------------
$mergeBase = (git merge-base "origin/$baseRef" $branch).Trim()
if ($LASTEXITCODE -ne 0 -or -not $mergeBase) { throw "git merge-base failed for PR $PrNumber." }

$range   = "$mergeBase..$branch"
$changed = git diff --name-only $range
if ($LASTEXITCODE -ne 0) { throw "git diff --name-only failed for PR $PrNumber." }

# --- Partition into lanes (deterministic; git emits forward slashes) -----------
$fsFiles    = @($changed | Where-Object { $_ -match '\.fsx?$' })
$docsFiles  = @($changed | Where-Object { $_ -match '^docs/.*\.md$' })
$dartFiles  = @($changed | Where-Object { $_ -match '^src/frontend/lib/.*\.dart$' })
$claimed    = @($fsFiles + $docsFiles + $dartFiles)
$otherFiles = @($changed | Where-Object { $claimed -notcontains $_ })

# --- Scope: F# + Docs diff -----------------------------------------------------
New-Item -ItemType Directory -Force $diffsDir | Out-Null
$reviewFiles = @($fsFiles + $docsFiles)
if ($reviewFiles.Count -gt 0) {
    git diff "--output=$diffPath" $range -- $reviewFiles
    if ($LASTEXITCODE -ne 0) { throw "git diff failed for PR $PrNumber." }
}
else {
    Set-Content -Path $diffPath -Value '' -Encoding utf8
}

# --- Write the context contract ------------------------------------------------
$context = [ordered]@{
    prNumber     = $PrNumber
    repo         = $Repo
    tempDir      = $tempDir
    worktreePath = $worktreePath
    reviewDir    = $reviewDir
    diffPath     = $diffPath
    mergeBase    = $mergeBase
    headSha      = $headSha
    baseRefName  = $baseRef
    title        = $title
    bodySummary  = $bodySummary
    fsFiles      = $fsFiles
    docsFiles    = $docsFiles
    dartFiles    = $dartFiles
    otherFiles   = $otherFiles
}
$context | ConvertTo-Json -Depth 5 | Out-File -FilePath $contextPath -Encoding utf8

# --- Human-readable summary (stdout only; JSON is the machine contract) ---------
Write-Output ''
Write-Output "Review context for PR #$PrNumber written to:"
Write-Output "  $contextPath"
Write-Output ''
$summary = [ordered]@{
    'PR'          = "#$PrNumber - $title"
    'Base ref'    = $baseRef
    'Head SHA'    = $headSha
    'Merge base'  = $mergeBase
    'Worktree'    = $worktreePath
    'Diff'        = $diffPath
    'F# files'    = $fsFiles.Count
    'Docs files'  = $docsFiles.Count
    'Dart files'  = "$($dartFiles.Count) (unchecked)"
    'Other files' = "$($otherFiles.Count) (unchecked)"
}
$summary.GetEnumerator() | ForEach-Object { '{0,-12} {1}' -f $_.Key, $_.Value } | Write-Output
