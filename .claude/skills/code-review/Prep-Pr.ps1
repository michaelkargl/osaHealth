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

Import-Module (Join-Path $PSScriptRoot 'Cli.psm1') -Force

<#
.SYNOPSIS
Throws unless the git remote 'origin' points at the expected repository.

.PARAMETER PrNumber
PR number, used only to phrase the error message.

.PARAMETER Repo
Repository slug the origin URL must contain.
#>
function Assert-OriginRemote {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$PrNumber,
        [Parameter(Mandatory)][string]$Repo
    )

    $origin = Read-Cli git remote get-url origin
    if ($origin -notmatch [regex]::Escape($Repo)) {
        throw "Cannot review PR $PrNumber -- the git remote ('$origin') is not $Repo. Run from a local clone of that repo."
    }
}

<#
.SYNOPSIS
Fetches PR metadata from GitHub.

.DESCRIPTION
Queries 'gh pr view' and returns the fields the review needs: base ref, head SHA,
title, and the PR description truncated to 500 characters (empty string when the
PR has no description).

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

    $metaJson = Read-Cli gh pr view $PrNumber --repo $Repo --json 'title,body,headRefName,baseRefName,headRefOid' -ErrorCodes @{ 1 = 'the PR may not exist' }
    $meta = $metaJson | ConvertFrom-Json

    [pscustomobject]@{
        BaseRef     = $meta.baseRefName
        HeadSha     = $meta.headRefOid
        Title       = $meta.title
        Description = if ($meta.body) { $meta.body.Substring(0, [Math]::Min(500, $meta.body.Length)) } else { '' }
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
function Resolve-ReviewPaths {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$PrNumber
    )

    $tempDir   = $env:TEMP
    $reviewDir = Join-Path $tempDir "cr-review-$PrNumber"
    $diffsDir  = Join-Path $reviewDir 'diffs'
    $skillDir  = $PSScriptRoot

    [pscustomobject] @{
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

.PARAMETER PrNumber
PR number, used only to phrase the confirmation prompt.

.PARAMETER WorktreePath
Path of the worktree to remove if present.

.PARAMETER Branch
Name of the tracking branch to delete if present.
#>
function Remove-StaleReviewState {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory)][string]$PrNumber,
        [Parameter(Mandatory)][string]$WorktreePath,
        [Parameter(Mandatory)][string]$Branch
    )

    if (-not $PSCmdlet.ShouldProcess("PR $PrNumber", 'Remove stale review state from previous runs')) { return }

    if (Test-Path $WorktreePath) { git worktree remove $WorktreePath --force }
    git worktree prune
    if (git branch --list $Branch) { git branch -D $Branch }
}

<#
.SYNOPSIS
Fetches the PR head and materialises it as an isolated worktree.

.DESCRIPTION
Fetches 'pull/<N>/head' into the PR's tracking branch and adds a git worktree for
it under $env:TEMP, leaving the main working directory untouched.

.PARAMETER PrNumber
The pull request number to fetch.

.PARAMETER Branch
Name of the tracking branch to fetch the PR head into.

.PARAMETER WorktreePath
Path at which to add the git worktree.
#>
function New-PrWorktree {
    [CmdletBinding(SupportsShouldProcess)]  
    param(
        [Parameter(Mandatory)][string]$PrNumber,
        [Parameter(Mandatory)][string]$Branch,
        [Parameter(Mandatory)][string]$WorktreePath
    )

    # Explicit forward required: $WhatIfPreference doesn't cross the module boundary.
    Write-Cli git fetch origin "pull/$PrNumber/head:$Branch" `
        -ErrorCodes @{ 128 = 'the PR may have been deleted' } -WhatIf:$WhatIfPreference

    # Explicit forward required: $WhatIfPreference doesn't cross the module boundary.
    Write-Cli git worktree add $WorktreePath $Branch -WhatIf:$WhatIfPreference
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

.PARAMETER PrNumber
PR number, used only to phrase error messages.

.PARAMETER Branch
Name of the PR's tracking branch.
#>
function Get-ReviewScope {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$BaseRef,
        [Parameter(Mandatory)][string]$PrNumber,
        [Parameter(Mandatory)][string]$Branch
    )

    # Capture before trimming: if git fails its output is $null, and $null.Trim() would
    # mask the intended error below with a null-method exception.
    $mergeBaseRaw = Read-Cli git merge-base "origin/$BaseRef" $Branch
    if (-not $mergeBaseRaw) { throw "git merge-base failed for PR $PrNumber." }
    $mergeBase = "$mergeBaseRaw".Trim()

    $changed = Read-Cli git diff --name-only "$mergeBase..$Branch"
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

.PARAMETER FsFiles
F# files in scope for the diff (from Get-ReviewScope).

.PARAMETER DocsFiles
Docs files in scope for the diff (from Get-ReviewScope).

.PARAMETER MergeBase
Merge-base commit the diff is computed from (from Get-ReviewScope).

.PARAMETER DiffPath
Destination path of the unified diff file.

.PARAMETER DiffsDir
Directory containing DiffPath; created if missing.

.PARAMETER Branch
Name of the PR's tracking branch, the diff's end ref.
#>
function Write-ReviewDiff {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory)][string[]]$FsFiles,
        [Parameter(Mandatory)][string[]]$DocsFiles,
        [Parameter(Mandatory)][string]$MergeBase,
        [Parameter(Mandatory)][string]$DiffPath,
        [Parameter(Mandatory)][string]$DiffsDir,
        [Parameter(Mandatory)][string]$Branch
    )

    New-Item -ItemType Directory -Force $DiffsDir | Out-Null

    $reviewFiles = @($FsFiles + $DocsFiles)
    if ($reviewFiles.Count -gt 0) {
        $diffArgs = @('diff', "--output=$DiffPath", "$MergeBase..$Branch", '--') + $reviewFiles
        # Explicit forward required: $WhatIfPreference doesn't cross the module boundary.
        Write-Cli -Command 'git' -Arguments $diffArgs -WhatIf:$WhatIfPreference
    }
    else {
        Set-Content -Path $DiffPath -Value '' -Encoding utf8
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
    
    $width = ($summary.Keys | Measure-Object -Property Length -Maximum).Maximum
    $summary.GetEnumerator() | ForEach-Object { "{0,-$width} {1}" -f $_.Key, $_.Value } | Write-Host
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

    Assert-OriginRemote -PrNumber $PrNumber -Repo $Repo
    $meta   = Get-PrMetadata -PrNumber $PrNumber -Repo $Repo
    $layout = Resolve-ReviewPaths -PrNumber $PrNumber

    Remove-StaleReviewState `
        -PrNumber $PrNumber `
        -WorktreePath $layout.WorktreePath `
        -Branch $layout.Branch

    New-PrWorktree `
        -PrNumber $PrNumber `
        -Branch $layout.Branch `
        -WorktreePath $layout.WorktreePath

    # Get-ReviewScope is read-only and always executes -- it can't itself skip
    # under -WhatIf. But it depends on the PR branch New-PrWorktree just (not
    # actually) fetched, so calling it here would either error on a nonexistent
    # ref or, if gated, throw the merge-base null-check below. Stop here instead.
    if ($WhatIfPreference) {
        Write-Host 'What if: Would compute the review scope and write the diff and review-context.json.'
        return
    }

    $scope = Get-ReviewScope `
                -BaseRef $meta.BaseRef `
                -PrNumber $PrNumber `
                -Branch $layout.Branch
    
    Write-ReviewDiff `
        -FsFiles $scope.FsFiles `
        -DocsFiles $scope.DocsFiles `
        -MergeBase $scope.MergeBase `
        -DiffPath $layout.DiffPath `
        -DiffsDir $layout.DiffsDir `
        -Branch $layout.Branch

    $context = [ordered] @{
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
        description        = $meta.Description
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
