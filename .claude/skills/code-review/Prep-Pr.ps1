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

Every state-changing operation is gated behind ShouldProcess, so the script can be
previewed with -WhatIf without touching git or the filesystem.

The orchestrator reads the review-context.json path printed on stdout after this runs.
The table printed to stdout is for the human watching; the JSON file is the contract.

.PARAMETER PrNumber
The pull request number to prepare.

.PARAMETER Repo
GitHub repository slug to verify against and query. Default: 'michaelkargl/osaHealth'.

.EXAMPLE
.\Prep-Pr.ps1 -PrNumber 22

.EXAMPLE
.\Prep-Pr.ps1 -PrNumber 22 -WhatIf
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)][int]$PrNumber,
    [string]$Repo = 'michaelkargl/osaHealth'
)

<#
.SYNOPSIS
Throws unless the git remote 'origin' points at the expected repository.

.PARAMETER PrNumber
PR number, used only to phrase the error message.

.PARAMETER Repo
Repository slug the origin URL must contain.
#>
function Confirm-OriginRemote {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$PrNumber,
        [Parameter(Mandatory)][string]$Repo
    )

    $origin = git remote get-url origin
    if ($LASTEXITCODE -ne 0 -or $origin -notmatch [regex]::Escape($Repo)) {
        throw "Cannot review PR $PrNumber -- the git remote ('$origin') is not $Repo. Run from a local clone of that repo."
    }
}

<#
.SYNOPSIS
Fetches PR metadata from GitHub.

.DESCRIPTION
Queries 'gh pr view' and returns the fields the review needs: base ref, head SHA,
title, and the PR body truncated to 500 characters (empty string when the PR has
no body).

.PARAMETER PrNumber
The pull request number to query.

.PARAMETER Repo
Repository slug to query.
#>
function Get-PrMetadata {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$PrNumber,
        [Parameter(Mandatory)][string]$Repo
    )

    $metaJson = gh pr view $PrNumber --repo $Repo --json title,body,headRefName,baseRefName,headRefOid
    if ($LASTEXITCODE -ne 0) { throw "gh pr view failed for PR $PrNumber (the PR may not exist)." }
    $meta = $metaJson | ConvertFrom-Json

    [pscustomobject]@{
        BaseRef     = $meta.baseRefName
        HeadSha     = $meta.headRefOid
        Title       = $meta.title
        BodySummary = if ($meta.body) { $meta.body.Substring(0, [Math]::Min(500, $meta.body.Length)) } else { '' }
    }
}

<#
.SYNOPSIS
Derives every path and name the review uses from the PR number.

.DESCRIPTION
Pure computation, touches nothing. Temp locations live under $env:TEMP; the skill
file paths are derived from the script's own location, so the skill keeps working
if the directory moves or the repo is cloned elsewhere.

.PARAMETER PrNumber
The pull request number the layout is for.
#>
function Get-ReviewLayout {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$PrNumber
    )

    $tempDir   = $env:TEMP
    $reviewDir = Join-Path $tempDir "cr-review-$PrNumber"
    $diffsDir  = Join-Path $reviewDir 'diffs'
    $skillDir  = $PSScriptRoot

    [pscustomobject]@{
        PrNumber           = $PrNumber
        Branch             = "cr-pr-$PrNumber"
        TempDir            = $tempDir
        WorktreePath       = Join-Path $tempDir "cr-worktree-$PrNumber"
        ReviewDir          = $reviewDir
        DiffsDir           = $diffsDir
        DiffPath           = Join-Path $diffsDir 'fsharp-docs.diff'
        ContextPath        = Join-Path $reviewDir 'review-context.json'
        SkillDir           = $skillDir
        ChecklistPath      = Join-Path $skillDir 'review-checklist.md'
        AgentPromptPath    = Join-Path $skillDir 'agent-prompt.md'
        ReportTemplatePath = Join-Path $skillDir 'report-template.md'
    }
}

<#
.SYNOPSIS
Removes worktree and branch leftovers from a previously aborted run.

.DESCRIPTION
Best-effort pre-clean so a re-run never trips over stale state: removes the PR's
worktree if present, prunes stale worktree registrations, and deletes the PR's
tracking branch if present.

.PARAMETER Layout
Review layout object from Get-ReviewLayout.
#>
function Remove-StaleReviewState {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory)][object]$Layout
    )

    if (-not $PSCmdlet.ShouldProcess("PR $($Layout.PrNumber)", 'Remove stale review state from previous runs')) { return }

    if (Test-Path $Layout.WorktreePath) { git worktree remove $Layout.WorktreePath --force }
    git worktree prune
    if (git branch --list $Layout.Branch) { git branch -D $Layout.Branch }
}

<#
.SYNOPSIS
Fetches the PR head and materialises it as an isolated worktree.

.DESCRIPTION
Fetches 'pull/<N>/head' into the PR's tracking branch and adds a git worktree for
it under $env:TEMP, leaving the main working directory untouched.

.PARAMETER Layout
Review layout object from Get-ReviewLayout.
#>
function New-PrWorktree {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory)][object]$Layout
    )

    if (-not $PSCmdlet.ShouldProcess("PR $($Layout.PrNumber)", "Fetch the PR head and add a worktree at $($Layout.WorktreePath)")) { return }

    git fetch origin "pull/$($Layout.PrNumber)/head:$($Layout.Branch)"
    if ($LASTEXITCODE -ne 0) { throw "git fetch failed for PR $($Layout.PrNumber) (the PR may have been deleted)." }

    git worktree add $Layout.WorktreePath $Layout.Branch
    if ($LASTEXITCODE -ne 0) { throw "git worktree add failed for PR $($Layout.PrNumber)." }
}

<#
.SYNOPSIS
Computes the review scope: merge-base plus the changed files partitioned into lanes.

.DESCRIPTION
Read-only; requires the PR branch to exist (see New-PrWorktree). Finds where the PR
branched off its base, lists the changed files, and partitions them deterministically:
F# ('*.fs', '*.fsx'), Docs ('docs/**/*.md'), Dart ('src/frontend/lib/**/*.dart'),
and Other (everything else). Git emits forward slashes, so the patterns match on '/'.

.PARAMETER BaseRef
Name of the PR's base branch (without the 'origin/' prefix).

.PARAMETER Layout
Review layout object from Get-ReviewLayout.
#>
function Get-ReviewScope {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$BaseRef,
        [Parameter(Mandatory)][object]$Layout
    )

    # Capture before trimming: if git fails its output is $null, and $null.Trim() would
    # mask the intended error below with a null-method exception.
    $mergeBaseRaw = git merge-base "origin/$BaseRef" $Layout.Branch
    if ($LASTEXITCODE -ne 0 -or -not $mergeBaseRaw) { throw "git merge-base failed for PR $($Layout.PrNumber)." }
    $mergeBase = "$mergeBaseRaw".Trim()

    $changed = git diff --name-only "$mergeBase..$($Layout.Branch)"
    if ($LASTEXITCODE -ne 0) { throw "git diff --name-only failed for PR $($Layout.PrNumber)." }
    # A PR with no changes emits nothing, leaving $changed = $null; drop that so it
    # cannot pass the OtherFiles filter as a phantom entry.
    $changed = @($changed | Where-Object { $_ })

    $fsFiles   = @($changed | Where-Object { $_ -match '\.fsx?$' })
    $docsFiles = @($changed | Where-Object { $_ -match '^docs/.*\.md$' })
    $dartFiles = @($changed | Where-Object { $_ -match '^src/frontend/lib/.*\.dart$' })
    $claimed   = @($fsFiles + $docsFiles + $dartFiles)

    [pscustomobject]@{
        MergeBase  = $mergeBase
        FsFiles    = $fsFiles
        DocsFiles  = $docsFiles
        DartFiles  = $dartFiles
        OtherFiles = @($changed | Where-Object { $claimed -notcontains $_ })
    }
}

<#
.SYNOPSIS
Writes the unified diff of the reviewed lanes (F# + Docs) to the layout's diff path.

.DESCRIPTION
Creates the diffs directory and writes the line-level diff for the F# and Docs lanes.
When neither lane has files, writes an empty diff file so downstream reads never miss.

.PARAMETER Scope
Review scope object from Get-ReviewScope.

.PARAMETER Layout
Review layout object from Get-ReviewLayout.
#>
function Write-ReviewDiff {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory)][object]$Scope,
        [Parameter(Mandatory)][object]$Layout
    )

    if (-not $PSCmdlet.ShouldProcess($Layout.DiffPath, 'Write the F# + Docs diff')) { return }

    New-Item -ItemType Directory -Force $Layout.DiffsDir | Out-Null

    $reviewFiles = @($Scope.FsFiles + $Scope.DocsFiles)
    if ($reviewFiles.Count -gt 0) {
        git diff "--output=$($Layout.DiffPath)" "$($Scope.MergeBase)..$($Layout.Branch)" -- $reviewFiles
        if ($LASTEXITCODE -ne 0) { throw "git diff failed for PR $($Layout.PrNumber)." }
    }
    else {
        Set-Content -Path $Layout.DiffPath -Value '' -Encoding utf8
    }
}

<#
.SYNOPSIS
Writes the review-context.json contract file.

.DESCRIPTION
Serialises the context to JSON at the given path. This file is the machine contract
the orchestrator reads; its field names are part of the SKILL.md placeholder table.

.PARAMETER Context
Ordered hashtable with the full review context.

.PARAMETER Path
Destination path of the JSON file.
#>
function Write-ReviewContext {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory)][object]$Context,
        [Parameter(Mandatory)][string]$Path
    )

    if (-not $PSCmdlet.ShouldProcess($Path, 'Write review-context.json')) { return }

    $Context | ConvertTo-Json -Depth 5 | Out-File -FilePath $Path -Encoding utf8
}

<#
.SYNOPSIS
Prints the human-readable run summary.

.DESCRIPTION
Prints the context file path (the line the orchestrator reads) followed by a summary
table. Stdout only; the JSON file remains the machine contract.

.PARAMETER Context
Ordered hashtable with the full review context.

.PARAMETER Path
Path of the written review-context.json.
#>
function Write-ReviewSummary {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][object]$Context,
        [Parameter(Mandatory)][string]$Path
    )

    Write-Host ''
    Write-Host "Review context for PR #$($Context.prNumber) written to:"
    Write-Host "  $Path"
    Write-Host ''

    $summary = [ordered]@{
        'PR'          = "#$($Context.prNumber) - $($Context.title)"
        'Base ref'    = $Context.baseRefName
        'Head SHA'    = $Context.headSha
        'Merge base'  = $Context.mergeBase
        'Worktree'    = $Context.worktreePath
        'Diff'        = $Context.diffPath
        'F# files'    = $Context.fsFiles.Count
        'Docs files'  = $Context.docsFiles.Count
        'Dart files'  = "$($Context.dartFiles.Count) (unchecked)"
        'Other files' = "$($Context.otherFiles.Count) (unchecked)"
    }
    $summary.GetEnumerator() | ForEach-Object { '{0,-12} {1}' -f $_.Key, $_.Value } | Write-Host
}

<#
.SYNOPSIS
Orchestrates the PR preparation end to end.
#>
function Invoke-Main {
    [CmdletBinding(SupportsShouldProcess)]
    param()

    Set-StrictMode -Version Latest
    $ErrorActionPreference = 'Stop'

    Confirm-OriginRemote -PrNumber $PrNumber -Repo $Repo
    $meta   = Get-PrMetadata -PrNumber $PrNumber -Repo $Repo
    $layout = Get-ReviewLayout -PrNumber $PrNumber

    Remove-StaleReviewState -Layout $layout
    New-PrWorktree -Layout $layout

    # The scope computation needs the fetched PR branch, which -WhatIf skips.
    if ($WhatIfPreference) {
        Write-Host 'What if: Would compute the merge-base, partition changed files into lanes, and write the diff and review-context.json.'
        return
    }

    $scope = Get-ReviewScope -BaseRef $meta.BaseRef -Layout $layout
    Write-ReviewDiff -Scope $scope -Layout $layout

    $context = [ordered]@{
        prNumber           = $PrNumber
        repo               = $Repo
        branch             = $layout.Branch
        tempDir            = $layout.TempDir
        worktreePath       = $layout.WorktreePath
        reviewDir          = $layout.ReviewDir
        diffPath           = $layout.DiffPath
        skillDir           = $layout.SkillDir
        checklistPath      = $layout.ChecklistPath
        agentPromptPath    = $layout.AgentPromptPath
        reportTemplatePath = $layout.ReportTemplatePath
        mergeBase          = $scope.MergeBase
        headSha            = $meta.HeadSha
        baseRefName        = $meta.BaseRef
        title              = $meta.Title
        bodySummary        = $meta.BodySummary
        fsFiles            = $scope.FsFiles
        docsFiles          = $scope.DocsFiles
        dartFiles          = $scope.DartFiles
        otherFiles         = $scope.OtherFiles
    }
    Write-ReviewContext -Context $context -Path $layout.ContextPath
    Write-ReviewSummary -Context $context -Path $layout.ContextPath
}

# Run only when executed directly; stay inert when dot-sourced by the tests.
if ($MyInvocation.InvocationName -ne '.') {
    Invoke-Main
}
