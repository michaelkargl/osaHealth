# Code Review (osaHealth)

Review an osaHealth pull request against `review-checklist.md`. Prepares an isolated
worktree, runs a blocking Opus review agent over the F# + docs diff, and prints a
structured report to the console. See `SKILL.md` for the operational steps Claude
follows when the skill runs.

```
o Step 1 prep (Prep-Pr.ps1)
→ Step 2 review agent
→ Step 3 report
→ Step 4 cleanup
```

## Usage

```pwsh
/code-review-osa <PR number or URL>

# Examples
# /code-review-osa 22
# /code-review-osa https://github.com/michaelkargl/osaHealth/pull/22
```

The repo is always `michaelkargl/osaHealth`.

## PowerShell conventions

Functions take explicit, typed, primitive (or array) parameters for exactly the
data they use — never an opaque object/hashtable bundling more than the function
needs. An object parameter hides the real dependency surface: the signature no
longer discloses what the function actually depends on, and the function can't be
tested or reasoned about independently of the whole object's shape.

Exception: a parameter whose entire purpose is to carry a data payload through
(e.g. serializing or printing the full thing, like `Write-ReviewContext`'s
`$Context`) is fine — every field is genuinely used by the callee, so there's no
hidden coupling.
