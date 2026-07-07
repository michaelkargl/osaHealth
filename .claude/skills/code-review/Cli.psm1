<#
.SYNOPSIS
Runs a native command and throws with full context if it exits non-zero.

.DESCRIPTION
Shared by Read-Cli and Write-Cli. Not exported: callers pick one of those two
based on whether the command reads or mutates state.
#>
function Invoke-Cli {
    param(
        [Parameter(Mandatory)][string]$Command,
        [Parameter(Mandatory)][string[]]$Arguments,
        [hashtable]$ErrorCodes
    )

    $commandLine = "$Command $($Arguments -join ' ')"
    $output = & $Command @Arguments
    if ($LASTEXITCODE -ne 0) {
        $reason = $ErrorCodes[$LASTEXITCODE]
        if (-not $reason) { $reason = 'unrecognized error' }
        throw "Command failed: $commandLine (status code ${LASTEXITCODE}: $reason)"
    }
    $output
}

<#
.SYNOPSIS
Runs a read-only native command and throws with full context if it exits non-zero.

.DESCRIPTION
Use for commands that don't mutate state (e.g. 'git remote get-url', 'git
merge-base', 'gh pr view') and must always actually execute -- there's no
-WhatIf parameter here at all, so a read call site can never be accidentally
skipped by a stray -WhatIf. For commands that mutate state, use Write-Cli instead.

Accepts arguments either as an explicit array or positionally, so both of these
work:
    Read-Cli -Command 'git' -Arguments @('remote', 'get-url', 'origin')
    Read-Cli git remote get-url origin

Caveat: in the positional form, an *unquoted* argument containing a comma is
silently corrupted. PowerShell parses a bare 'a,b,c' token as an array literal,
and folding that into the [string[]] Arguments slot coerces it back to a single
string by joining with spaces, not commas -- e.g. 'title,body,headRefName'
typed unquoted would arrive as "title body headRefName". Quote any
comma-containing argument explicitly to avoid this.

.PARAMETER Command
The native executable to run, e.g. 'git' or 'gh'.

.PARAMETER Arguments
Arguments to pass to Command, e.g. @('remote', 'get-url', 'origin') or, when
called positionally, remote get-url origin.

.PARAMETER ErrorCodes
Optional map of exit code to a human-readable reason, e.g. @{ 1 = 'the PR may
not exist' }. Exit codes not present in this map fall back to the reason
'unrecognized error'. Omit entirely when the command and exit code are already
self-explanatory.

.EXAMPLE
Read-Cli git remote get-url origin

.EXAMPLE
Read-Cli gh pr view $PrNumber --repo $Repo --json 'title,body,headRefName' -ErrorCodes @{ 1 = 'the PR may not exist' }
#>
function Read-Cli {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, Position = 0)][string]$Command,
        [Parameter(Position = 1, ValueFromRemainingArguments)][string[]]$Arguments,
        [hashtable]$ErrorCodes = @{}
    )

    Invoke-Cli -Command $Command -Arguments $Arguments -ErrorCodes $ErrorCodes
}

<#
.SYNOPSIS
Runs a state-mutating native command, honoring -WhatIf/-Confirm, and throws
with full context if it exits non-zero.

.DESCRIPTION
Use for commands that mutate state (e.g. 'git fetch', 'git worktree add', 'git
diff --output='). Supports ShouldProcess: under -WhatIf it prints exactly the
command that would have run and skips execution entirely, without needing to
know that command's semantics.

Because this function lives in a module, $WhatIfPreference does NOT
automatically cross that boundary the way it would between two functions in
the same script. Callers MUST forward it explicitly:
    Write-Cli git fetch origin $refSpec -WhatIf:$WhatIfPreference
Omitting the forward means this always executes for real, even when the
calling script is running under -WhatIf.

Accepts arguments either as an explicit array or positionally -- see Read-Cli's
help for both forms and the comma-quoting caveat, which applies here too.

.PARAMETER Command
The native executable to run, e.g. 'git'.

.PARAMETER Arguments
Arguments to pass to Command, e.g. @('fetch', 'origin', 'main').

.PARAMETER ErrorCodes
Optional map of exit code to a human-readable reason. See Read-Cli's help.

.EXAMPLE
Write-Cli git fetch origin "pull/$PrNumber/head:$Branch" -ErrorCodes @{ 128 = 'the PR may have been deleted' } -WhatIf:$WhatIfPreference
#>
function Write-Cli {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory, Position = 0)][string]$Command,
        [Parameter(Position = 1, ValueFromRemainingArguments)][string[]]$Arguments,
        [hashtable]$ErrorCodes = @{}
    )

    $commandLine = "$Command $($Arguments -join ' ')"
    if (-not $PSCmdlet.ShouldProcess($commandLine, 'Execute')) { return }

    Invoke-Cli -Command $Command -Arguments $Arguments -ErrorCodes $ErrorCodes
}

Export-ModuleMember -Function Read-Cli, Write-Cli
