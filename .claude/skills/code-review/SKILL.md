---
name: code-review-osa
description: >
  Review an osaHealth pull request against review-checklist.md. Prepares an isolated
  worktree, runs a blocking Opus review agent over the F# + docs diff, and prints a
  structured report to the console. Use when asked to review a PR of this repo.
user-invocable: true
allowed-tools: PowerShell(*Prep-Pr.ps1*), PowerShell(git *), PowerShell(Remove-Item*), Read, Glob, Agent
---

# Code Review (osaHealth)

See `README.md` for usage and an overview.

## Process

### Step 1 — Prepare the PR

Extract the PR number from the input and run the prep script

```ps1
& .\.claude\skills\code-review\Prep-Pr.ps1 -PrNumber {prNumber}
```

Read the `review-context.json` whose path the script prints. It is the **contract**:
a flat JSON object whose every top-level field is available as `{fieldName}` wherever
a step below references it.

### Step 2 — Run the review agent

Sanity check: `Read("{diffPath}", limit: 5)`. If it is empty although
`fsFiles`/`docsFiles` list files, stop and diagnose.

Read `{agentPromptPath}`, substitute every contract placeholder, and launch a
**blocking** agent with the substituted text:

```
Agent(model: "opus", run_in_background: false, prompt: "<substituted template>")
```

The agent's tool result is the structured report. Do not re-read the diff or any
guidelines in the main context — the agent handled that.

### Step 3 — Present the summary

Read `{reportTemplatePath}` and fill it from the agent's report plus the contract's
file lists. All sections are required, even when empty. `dartFiles` and `otherFiles`
have no review lane — list every entry in the "Unchecked Files" section rather than
silently dropping them.

### Step 4 — Clean up

Remove the worktree, the local PR branch, and the scratch dir. Run this even after
a failed run.

```ps1
git worktree remove "{worktreePath}" --force
git branch -D "{branch}"
Remove-Item -Recurse -Force "{reviewDir}"
```

## When a step fails

- **Step 1 throws** (wrong remote, deleted PR): stop and surface the script's
  message — do not work around it.
- **A later step fails or a run was aborted**: re-run from Step 1 — the script
  removes stale worktrees and branches before starting.
- **The agent's report is missing sections or unparseable**: re-launch the Step 2
  agent once with the same prompt; if it happens again, stop and show the raw report.
- **After any failure**, still run Step 4.

## Hard rules

| Forbidden                                                     | Reason                                                         |
|----------------------------------------------------------------|-----------------------------------------------------------------|
| Bash tool for any command                                     | This skill uses the **PowerShell** tool only                   |
| `cygpath`, `node`, `python3`, `python`                        | Not needed — use the Read tool + in-context parsing            |
| `git checkout`, `git switch`, `git stash` in main working dir | Worktree only                                                  |
| Reading `~/.claude/skills/*`                                  | Read `review-checklist.md` from the repo working tree instead  |
| `run_in_background: true` on Agent                            | The agent must be blocking so its report is the tool result    |
| `git add`, `git commit`, `git push`                           | Never mutate the repo during review                            |
