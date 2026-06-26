function Get-CrTempDir {
    <#
    .Synopsis
    Returns the native Windows temporary directory path ($env:TEMP).
    Call once in Step 1c and store the result as {TEMP_DIR}; use it in Read tool paths and the agent prompt.
    The module functions derive the temp dir internally, so {TEMP_DIR} is only needed for orchestrator-side Read calls.
    #>
    [CmdletBinding()]
    param()
    $env:TEMP
}

function Test-CrRepo {
    <#
    .Synopsis
    Verifies that the current directory's git remote matches the expected repository.
    Throws a descriptive error if it does not; the review must stop.
    .Parameter Repo
    GitHub repository slug to verify against. Default: 'michaelkargl/osaHealth'.
    #>
    [CmdletBinding()]
    param(
        [string]$Repo = 'michaelkargl/osaHealth'
    )
    $origin = git remote get-url origin 2>&1
    if ($LASTEXITCODE -ne 0 -or $origin -notmatch [regex]::Escape($Repo)) {
        throw "Cannot review this PR -- the git remote ('$origin') is not $Repo. Run from a local clone of that repo."
    }
    Write-Output "Origin verified: $origin"
}

function Get-CrPrMetadata {
    <#
    .Synopsis
    Fetches PR metadata from GitHub and returns the raw JSON string.
    Extract: title, body (truncate to 500 chars as {BODY_SUMMARY}), headRefName, baseRefName, headRefOid (={HEAD_SHA}).
    .Parameter PrNumber
    The pull request number to fetch.
    .Parameter Repo
    GitHub repository slug. Default: 'michaelkargl/osaHealth'.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$PrNumber,
        [string]$Repo = 'michaelkargl/osaHealth'
    )
    gh pr view $PrNumber --repo $Repo --json title,body,additions,deletions,headRefName,baseRefName,number,url,headRefOid
}

function New-CrWorktree {
    <#
    .Synopsis
    Fetches the PR branch and creates a git worktree at {TEMP_DIR}\cr-worktree-{PrNumber}.
    Runs git fetch then git worktree add sequentially inside this function; both must succeed.
    .Parameter PrNumber
    The pull request number.
    #>
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory)][string]$PrNumber
    )
    $tempDir = Get-CrTempDir
    $worktreePath = "$tempDir\cr-worktree-$PrNumber"
    if ($PSCmdlet.ShouldProcess("pull/$PrNumber/head:cr-pr-$PrNumber", 'git fetch origin')) {
        git fetch origin "pull/$PrNumber/head:cr-pr-$PrNumber"
        if ($LASTEXITCODE -ne 0) { throw "git fetch failed for PR $PrNumber" }
    }
    if ($PSCmdlet.ShouldProcess($worktreePath, 'git worktree add')) {
        git worktree add $worktreePath "cr-pr-$PrNumber"
        if ($LASTEXITCODE -ne 0) { throw "git worktree add failed for PR $PrNumber" }
        Write-Output "Worktree created at $worktreePath"
    }
}

function Get-CrMergeBase {
    <#
    .Synopsis
    Returns the merge-base SHA between origin/{BaseRef} and the PR branch.
    Store the output SHA as {MERGE_BASE} for use in subsequent steps.
    .Parameter PrNumber
    The pull request number.
    .Parameter BaseRef
    The base branch name (e.g. 'main'), taken from PR metadata baseRefName field.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$PrNumber,
        [Parameter(Mandatory)][string]$BaseRef
    )
    git merge-base "origin/$BaseRef" "cr-pr-$PrNumber"
}

function Write-CrFileList {
    <#
    .Synopsis
    Creates the review directory and writes the changed-file list to files.txt.
    Emits the absolute path to files.txt; read it with the Read tool immediately after.
    .Parameter PrNumber
    The pull request number.
    .Parameter MergeBase
    The merge-base SHA from Get-CrMergeBase.
    #>
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory)][string]$PrNumber,
        [Parameter(Mandatory)][string]$MergeBase
    )
    $tempDir = Get-CrTempDir
    $reviewDir = "$tempDir\cr-review-$PrNumber"
    $outFile = "$reviewDir\files.txt"
    if ($PSCmdlet.ShouldProcess($reviewDir, 'New-Item Directory')) {
        New-Item -ItemType Directory -Force $reviewDir | Out-Null
    }
    if ($PSCmdlet.ShouldProcess($outFile, 'git diff --name-only')) {
        git diff --name-only "--output=$outFile" "${MergeBase}..cr-pr-${PrNumber}"
        if ($LASTEXITCODE -ne 0) { throw "git diff --name-only failed for PR $PrNumber" }
    }
    Write-Output $outFile
}

function Write-CrDiff {
    <#
    .Synopsis
    Creates the diffs directory and writes the unified diff for F# and Docs files.
    Emits the absolute path to fsharp-docs.diff; verify it is non-empty with the Read tool after.
    .Parameter PrNumber
    The pull request number.
    .Parameter MergeBase
    The merge-base SHA from Get-CrMergeBase.
    .Parameter Files
    Array of file paths (relative to repo root) to include in the diff. Pass all F# and Docs files from Write-CrFileList.
    #>
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory)][string]$PrNumber,
        [Parameter(Mandatory)][string]$MergeBase,
        [Parameter(Mandatory)][string[]]$Files
    )
    $tempDir = Get-CrTempDir
    $diffsDir = "$tempDir\cr-review-$PrNumber\diffs"
    $outFile = "$diffsDir\fsharp-docs.diff"
    if ($PSCmdlet.ShouldProcess($diffsDir, 'New-Item Directory')) {
        New-Item -ItemType Directory -Force $diffsDir | Out-Null
    }
    if ($PSCmdlet.ShouldProcess($outFile, 'git diff')) {
        git diff "--output=$outFile" "${MergeBase}..cr-pr-${PrNumber}" -- $Files
        if ($LASTEXITCODE -ne 0) { throw "git diff failed for PR $PrNumber" }
    }
    Write-Output $outFile
}

function Get-CrExistingComments {
    <#
    .Synopsis
    Fetches all existing inline review comments on the PR and writes them to existing-comments.json.
    Emits the absolute file path; parse the JSON in-context with the Read tool.
    .Parameter PrNumber
    The pull request number.
    .Parameter Repo
    GitHub repository slug. Default: 'michaelkargl/osaHealth'.
    #>
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory)][string]$PrNumber,
        [string]$Repo = 'michaelkargl/osaHealth'
    )
    $tempDir = Get-CrTempDir
    $outFile = "$tempDir\cr-review-$PrNumber\existing-comments.json"
    if ($PSCmdlet.ShouldProcess($outFile, "gh api repos/$Repo/pulls/$PrNumber/comments --paginate")) {
        gh api "repos/$Repo/pulls/$PrNumber/comments" --paginate | Out-File -FilePath $outFile -Encoding utf8
    }
    Write-Output $outFile
}

function Publish-CrReview {
    <#
    .Synopsis
    Posts the review payload as a single batched GitHub Review (event=COMMENT).
    The JSON file at -InputPath must already exist, written by the Write tool in the previous step.
    Never loops individual calls; all comments go in one POST.
    .Parameter PrNumber
    The pull request number.
    .Parameter InputPath
    Absolute path to the new-comments.json file prepared by the orchestrator.
    .Parameter Repo
    GitHub repository slug. Default: 'michaelkargl/osaHealth'.
    #>
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory)][string]$PrNumber,
        [Parameter(Mandatory)][string]$InputPath,
        [string]$Repo = 'michaelkargl/osaHealth'
    )
    if ($PSCmdlet.ShouldProcess("repos/$Repo/pulls/$PrNumber/reviews", 'gh api POST')) {
        gh api "repos/$Repo/pulls/$PrNumber/reviews" --method POST --input $InputPath
    }
}

function Remove-CrWorktree {
    <#
    .Synopsis
    Cleanup: removes the worktree, deletes the tracking branch, and removes the review temp dir.
    All three destructive operations run sequentially inside this function.
    .Parameter PrNumber
    The pull request number.
    #>
    [CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'High')]
    param(
        [Parameter(Mandatory)][string]$PrNumber
    )
    $tempDir = Get-CrTempDir
    $worktreePath = "$tempDir\cr-worktree-$PrNumber"
    $reviewDir = "$tempDir\cr-review-$PrNumber"
    if ($PSCmdlet.ShouldProcess($worktreePath, 'git worktree remove --force')) {
        git worktree remove $worktreePath --force
    }
    if ($PSCmdlet.ShouldProcess("cr-pr-$PrNumber", 'git branch -D')) {
        git branch -D "cr-pr-$PrNumber"
    }
    if ($PSCmdlet.ShouldProcess($reviewDir, 'Remove-Item -Recurse -Force')) {
        Remove-Item -Recurse -Force $reviewDir
    }
    Write-Output "Cleanup complete for PR $PrNumber"
}

Export-ModuleMember -Function Get-CrTempDir, Test-CrRepo, Get-CrPrMetadata, New-CrWorktree, Get-CrMergeBase, Write-CrFileList, Write-CrDiff, Get-CrExistingComments, Publish-CrReview, Remove-CrWorktree
