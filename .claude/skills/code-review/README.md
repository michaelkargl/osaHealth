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
