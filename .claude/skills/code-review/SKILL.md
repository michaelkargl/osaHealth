---
name: code-review-osa
description: >
  Review a PR in the osaHealth repo against documented guidelines. Creates a git worktree,
  runs a blocking Opus review agent, compiles a structured report, and auto-posts deduped
  inline comments via one batched GitHub API call. Native PowerShell — no /tmp, no cygpath,
  no node, no python.
user-invocable: true
allowed-tools: PowerShell(Get-Command *), PowerShell(Get-CrTempDir*), PowerShell(Test-CrRepo*), PowerShell(Get-CrPrMetadata*), PowerShell(New-CrWorktree*), PowerShell(Get-CrMergeBase*), PowerShell(Write-CrFileList*), PowerShell(Write-CrDiff*), PowerShell(Get-CrExistingComments*), PowerShell(Publish-CrReview*), PowerShell(Remove-CrWorktree*), Read, Grep, Glob, Write, Agent
---

<!--
  FLOW  (every step is a self-contained PowerShell call — no module dependency)

  /code-review-osa <PR>
     │
  STEP 1  git remote check ─(not osaHealth)──────────────────────────────────────► STOP
          $env:TEMP ─► {TEMP_DIR}
          gh pr view ─► {TITLE} {baseRefName} {HEAD_SHA}
          git fetch + git worktree add ─► worktree at {TEMP_DIR}\cr-worktree-{PR}
          git merge-base ─► {MERGE_BASE}
          git diff --name-only ─► files.txt ─► lanes:
                                 *.fs / *.fsx      → F#    ┐ reviewed
                                 docs/**/*.md       → Docs  ┘
                                 src/frontend/**    → Dart   (DORMANT → "unchecked")
                                 everything else    → "unchecked"
     ▼
  STEP 2  git diff (filtered to F#+Docs) ─► fsharp-docs.diff
     ▼
  STEP 3  Read agent-prompt.md ─► substitute placeholders ─► launch BLOCKING Opus Agent
            agent reads: review-checklist.md + the diff + docs/ guidelines
            agent returns: structured report (checks · MUST/NICE FIX · security · perf)
     ▼
  STEP 4  Compile + print terminal summary (all sections required)
     ▼
  STEP 5  gh api GET comments ─► dedup index (path, title) ─► drop already-posted issues
            └─ all duplicates? ─► print "no new comments" ─► STEP 6
          verify line numbers in hunks (Grep) ─► Write new-comments.json ─► gh api POST review
     ▼
  STEP 6  git worktree remove · git branch -D · Remove-Item review dir
-->

# Code Review (osaHealth)

## Input

```
/code-review-osa <PR number or URL>
```

Examples:
- `/code-review-osa 22`
- `/code-review-osa https://github.com/michaelkargl/osaHealth/pull/22`

Repo is always `michaelkargl/osaHealth`.

---

## Review Criteria

All review criteria live in `review-checklist.md` beside this file.
That is the single source of truth. Edit there; never duplicate rule text here.

---

## Hard Rules — READ BEFORE EVERY STEP

These are **FORBIDDEN** during a review:

| Forbidden | Reason |
|-----------|--------|
| Bash tool for any command | This skill uses the **PowerShell** tool only |
| `cygpath`, `node`, `python3`, `python` | Not needed — use the Read tool + in-context parsing |
| Heredocs (`<<'EOF'`) | Not supported in PowerShell; use the **Write tool** for JSON payloads |
| `git checkout`, `git switch`, `git stash` in main working dir | Worktree only |
| Reading `~/.claude/skills/*` | Read `review-checklist.md` from the repo working tree instead |
| `run_in_background: true` on Agent | Agents must be blocking so their report is the tool result |
| `git add`, `git commit`, `git push` | Never mutate the repo during review |

---

## Review Process

---

### Step 1: PR information and worktree

**1a.** Extract `{PR}` (the PR number) from the input. Repo is always `michaelkargl/osaHealth`.

```ps1
# 1b. Verify repo
$origin = git remote get-url origin 2>&1; if ($LASTEXITCODE -ne 0 -or $origin -notmatch 'michaelkargl/osaHealth') { throw "Cannot review this PR -- the git remote ('$origin') is not michaelkargl/osaHealth." }; Write-Output "Origin verified: $origin"
<#
If it throws, stop and surface the error message to the user.
Do not attempt to work around the mismatch.
#>
```

```ps1
# 1c. Resolve temp dir
$env:TEMP
<#
Store the output as {TEMP_DIR} — used for Read tool paths, agent prompt, and subsequent commands.
Example output: C:\Users\kami\AppData\Local\Temp
#>
```

```ps1
# 1d. Fetch PR metadata
gh pr view {PR} --repo michaelkargl/osaHealth --json title,body,headRefName,baseRefName,headRefOid
<#
Extract and store: title, body (truncate to first 500 chars -> {BODY_SUMMARY}),
headRefName, baseRefName, headRefOid (= {HEAD_SHA}).
#>
```

```ps1
# 1e. Fetch PR branch
git fetch origin "pull/{PR}/head:cr-pr-{PR}"
<#
If it fails, the PR may have been deleted — stop and report.
#>
```

```ps1
# 1e-2. Create worktree
git worktree add "{TEMP_DIR}\cr-worktree-{PR}" "cr-pr-{PR}"
<#
All file reads during the review use {TEMP_DIR}\cr-worktree-{PR}\ as the root.
#>
```

```ps1
# 1f. Get merge base
git merge-base "origin/{baseRefName}" "cr-pr-{PR}"
<#
Store the printed SHA as {MERGE_BASE}.
#>
```

```ps1
# 1g. Write changed-file list
New-Item -ItemType Directory -Force "{TEMP_DIR}\cr-review-{PR}" | Out-Null; git diff --name-only "{MERGE_BASE}..cr-pr-{PR}" | Out-File -FilePath "{TEMP_DIR}\cr-review-{PR}\files.txt" -Encoding utf8
<#
Read the output of this command — it's the path to files.txt. Read it with the Read tool immediately after.
Do NOT use gh pr view --json files — it paginates silently at 100 files.
git diff --name-only has no pagination limit.
#>
```

```
Read("{TEMP_DIR}\cr-review-{PR}\files.txt")
```

Partition the file list into lanes:
- **F# lane**: any file matching `*.fs` or `*.fsx`
- **Docs lane**: any file matching `docs/**/*.md`
- **Dart lane**: any file matching `src/frontend/lib/**/*.dart` — note as "unchecked", skip review
- **Other**: everything else — note in summary, never silently skip

---

### Step 2: Write diffs

```ps1
# 2. Write F# + Docs diff
New-Item -ItemType Directory -Force "{TEMP_DIR}\cr-review-{PR}\diffs" | Out-Null; git diff "{MERGE_BASE}..cr-pr-{PR}" -- {fs_file_1} {fs_file_2} {docs_file_1} | Out-File -FilePath "{TEMP_DIR}\cr-review-{PR}\diffs\fsharp-docs.diff" -Encoding utf8
<#
Replace {fs_file_1} {fs_file_2} {docs_file_1} with every F# and Docs file identified in step 1g.
Sanity check: every file from 1g must be in a lane.
Files outside F#/Docs/Dart must be noted in the summary as "unchecked".
#>
```

Verify the diff was written (non-empty):

```
Read("{TEMP_DIR}\cr-review-{PR}\diffs\fsharp-docs.diff", limit: 5)
```

If the output is empty but there were F#/docs files in the change list, stop and diagnose.

---

### Step 3: Launch F# + Docs review agent

Read the agent prompt template:

```
Read("C:\Users\kami\workspace\github-space\osaHealth\.claude\skills\code-review\agent-prompt.md")
```

Substitute these placeholders in the template text:

| Placeholder | Value |
|-------------|-------|
| `{PR}` | PR number |
| `{TITLE}` | PR title from step 1d |
| `{BODY_SUMMARY}` | First 500 chars of PR body |
| `{MERGE_BASE}` | SHA from step 1f |
| `{HEAD_SHA}` | `headRefOid` from step 1d |
| `{TEMP_DIR}` | Native path from step 1c |
| `{FS_FILE_LIST}` | F# files from step 1g |
| `{DOCS_FILE_LIST}` | Docs files from step 1g |

Launch a **blocking** Agent with the substituted text as the prompt:

```
Agent(
  model: "opus",
  run_in_background: false,
  prompt: "<substituted template>"
)
```

The agent's tool result (~10–15 KB) is the structured report. Do NOT re-read the diff or any guidelines in the main context — the agent handled that.

---

### Step 4: Compile and present review summary

Compile the agent's report into the format below.
**ALL sections are REQUIRED** — include every section even when it has no findings.

```markdown
# Code Review Summary: PR #{PR}

**PR Title**: {title}
**Files Changed**: {count} ({fs_count} F#, {docs_count} docs, {other_count} other/unchecked)
**Skills Applied**: fsharp-programming, docs-guidelines

---

## Mandatory Checks Executed

| Rule | File | Triggered | Result |
|------|------|-----------|--------|
| {rule} | {file} | Yes/No | ✅ PASS / ❌ FAIL / ⏭️ N/A |

---

## Cross-Domain Checks Executed

| Trigger | Cross-Check | Result |
|---|---|---|
| {file} | {description} | ✅ PASS / ❌ FAIL / ⏭️ N/A |

---

## MUST FIX ({count})

### Issue 1: {brief title}
- **File**: `{file_path}:{line_number}`
- **Confidence**: {X}%
- **Category**: {Security/Bug/Guideline Violation/Mandatory Check Failed}
- **Description**: {detailed explanation}
{If confidence < 50%}
- **Clarifying Question**: {question}
{/If}

---

## NICE TO FIX ({count})

### Issue 1: {brief title}
- **File**: `{file_path}:{line_number}`
- **Confidence**: {X}%
- **Category**: {Style/Optimization/Documentation}
- **Description**: {detailed explanation}
{If confidence < 50%}
- **Clarifying Question**: {question}
{/If}

---

## Security Analysis (OWASP Top 10)

No security vulnerabilities detected. [or table]

---

## Performance Analysis

No performance issues detected. [or table]

---

## Questions Requiring Clarification

{Numbered list of questions from low-confidence findings, or "None."}

---

## Unchecked Files

{List any files not in F#/Docs/Dart lanes — note file path and why skipped, or "None."}
```

---

### Step 5: Dedup and post inline comments

```ps1
# 5a. Fetch existing PR review comments
gh api "repos/michaelkargl/osaHealth/pulls/{PR}/comments" --paginate | Out-File -FilePath "{TEMP_DIR}\cr-review-{PR}\existing-comments.json" -Encoding utf8
<#
Read {TEMP_DIR}\cr-review-{PR}\existing-comments.json in the next step.
#>
```

**5b. Build dedup index** — Read tool, parse in context:

```
Read("{TEMP_DIR}\cr-review-{PR}\existing-comments.json")
```

Keep only objects whose `body` contains `🤖 Generated with [Claude Code]`. For each, extract:
- `path` — the file path
- The first `### ` line of `body` — strip `### ` and trim → the title

Build a set of `(path, title)` pairs. This is the dedup index.

> **Why `(path, title)` not `(path, line, title)`**: line numbers shift when new commits are pushed; the title stays stable.

**5c. Filter candidates:** for each MUST FIX and NICE TO FIX issue, compute its `(path, title)` key and drop it if the key already exists in the dedup index.

If every candidate is a duplicate, print:
> "No new review comments to post on PR #{PR} — all {N} issues from this run are already present as Claude-generated comments."

…and proceed directly to Step 6.

**5d. Verify line numbers:** for each remaining candidate, confirm the reported line is inside a diff hunk by checking `{TEMP_DIR}\cr-review-{PR}\diffs\fsharp-docs.diff` with Grep. If the line is not inside a hunk, snap to the nearest line that IS in the diff context. The GitHub API rejects comments on lines outside the diff.

**5e.** Construct valid JSON for the GitHub Reviews API. Each comment body must:
- Have `### {Issue Title}` as the first line (the dedup key for future runs)
- Include severity, confidence, category, and description
- End with `🤖 Generated with [Claude Code](https://claude.ai/code)`

```
Write(
  file_path: "{TEMP_DIR}\cr-review-{PR}\new-comments.json",
  content: "{valid JSON string — see format below}"
)
```

```json
{
  "event": "COMMENT",
  "comments": [
    {
      "path": "src/osaHealth.Api/QueryHandlers.fs",
      "line": 47,
      "body": "### Issue Title\n\n**Severity**: MUST FIX · **Confidence**: 95% · **Category**: Guideline Violation\n\nDescription here.\n\n🤖 Generated with [Claude Code](https://claude.ai/code)"
    }
  ]
}
```

```ps1
# 5e. Post all new comments in one batched review
gh api "repos/michaelkargl/osaHealth/pulls/{PR}/reviews" --method POST --input "{TEMP_DIR}\cr-review-{PR}\new-comments.json"
<#
All non-duplicate comments go into one POST — never loop individual calls.
Use "event": "COMMENT" — never REQUEST_CHANGES or APPROVE.
After the call returns, print: "Posted {N} new review comments on PR #{PR} ({M} duplicate(s) skipped)."
#>
```

---

### Step 6: Cleanup

```ps1
# 6. Remove worktree, branch, and temp dir
git worktree remove "{TEMP_DIR}\cr-worktree-{PR}" --force; git branch -D "cr-pr-{PR}"; Remove-Item -Recurse -Force "{TEMP_DIR}\cr-review-{PR}"
```
