---
name: code-review-osa
description: >
  Review a PR in the osaHealth repo against documented guidelines. Creates a git worktree,
  runs a blocking Opus review agent, compiles a structured report, and auto-posts deduped
  inline comments via one batched GitHub API call. Native PowerShell — no /tmp, no cygpath,
  no node, no python.
user-invocable: true
allowed-tools: PowerShell(*Prep-Pr.ps1*), PowerShell(git *), PowerShell(gh *), PowerShell(Remove-Item*), Read, Grep, Glob, Write, Agent
---

<!--
  FLOW  (Prep-Pr.ps1 owns the git plumbing; the skill orchestrates + posts)

  /code-review-osa <PR>
     │
  STEP 1  Prep-Pr.ps1 -PrNumber {PR}
            ├─ verify remote ─(not osaHealth)─► STOP
            ├─ gh pr view ─► title / base / head SHA
            ├─ git fetch + worktree add ─► {TEMP_DIR}\cr-worktree-{PR}
            ├─ git merge-base ─► {MERGE_BASE}
            ├─ git diff --name-only ─► lanes:  *.fs/*.fsx → F#      ┐ reviewed
            │                                  docs/**/*.md → Docs   ┘
            │                                  src/frontend/lib/**/*.dart → Dart (unchecked)
            │                                  everything else → "unchecked"
            └─ git diff (F#+Docs) ─► fsharp-docs.diff
          ─► writes ONE review-context.json ─► skill reads it for every placeholder
     ▼
  STEP 2  Read agent-prompt.md ─► substitute placeholders ─► launch BLOCKING Opus Agent
            agent reads: review-checklist.md + the diff + docs/ guidelines
            agent returns: structured report (checks · MUST/NICE FIX · security · perf)
     ▼
  STEP 3  Compile + print terminal summary (all sections required)
     ▼
  STEP 4  gh api GET comments ─► dedup index (path, title) ─► drop already-posted issues
            └─ all duplicates? ─► print "no new comments" ─► STEP 5
          verify line numbers in hunks (Grep) ─► Write new-comments.json ─► gh api POST review
     ▼
  STEP 5  git worktree remove · git branch -D · Remove-Item review dir
-->

# Code Review (osaHealth)

## What this does (read this first)

At heart this skill is **four moves**. Everything below is in service of them:

1. **Get an isolated copy of the PR's code** — so the review reads changed files
   without disturbing your working directory.
2. **Work out exactly what changed** — which files, and which lines within them.
3. **Hand that diff to an Opus agent** with `review-checklist.md`, and get back a
   structured report (must-fix · nice-to-fix · security · perf).
4. **Post the findings to the PR as inline comments** — deduped, so re-runs don't
   repeat what was already said.

### Why so many git commands?

They're **three jobs, not seven separate ideas**:

| Job | Commands | Why |
|-----|----------|-----|
| Get the code | `git fetch` + `git worktree add` | A worktree is a second checkout in a temp dir — that's what keeps your working directory untouched. |
| Scope the review | `git merge-base` + `git diff --name-only` + `git diff` | merge-base finds where the PR branched off base; the file list drives the F#/Docs/Dart lanes; the line-level diff lets the agent judge only `+` lines. |
| Clean up | `git worktree remove` + `git branch -D` | Undo the first job so temp dirs don't accumulate. |

**Adding checks (e.g. OWASP) touches none of this.** Security lives in
`review-checklist.md` (rule 13) and the "Security Analysis" section of
`agent-prompt.md`. Extend those; leave the git plumbing alone.

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

### Step 1: Prepare the PR (one call)

**1a.** Extract `{PR}` (the PR number) from the input. Repo is always `michaelkargl/osaHealth`.

**1b.** Run the prep script. It owns *all* the git plumbing — verify remote, fetch PR
metadata, fetch + worktree the PR head, compute the merge-base, partition the changed
files into lanes, and write the F#+Docs diff — and emits one `review-context.json`:

```ps1
& .\.claude\skills\code-review\Prep-Pr.ps1 -PrNumber {PR}
<#
Single source of truth for the "get the code" + "scope the review" plumbing.
If it throws (wrong remote, deleted PR), STOP and surface the message — do not work around it.
Lane partitioning is done in the script; you do NOT classify files by hand.
#>
```

**1c.** Read the context contract the script just wrote. This is the single source for
every placeholder used in the rest of the review:

```
Read("{TEMP_DIR}\cr-review-{PR}\review-context.json")
```

`{TEMP_DIR}` is `$env:TEMP`. The JSON supplies:

| `review-context.json` field | Used as |
|---|---|
| `mergeBase` | `{MERGE_BASE}` |
| `headSha` | `{HEAD_SHA}` |
| `baseRefName` | base branch |
| `title` | `{TITLE}` |
| `bodySummary` | `{BODY_SUMMARY}` (already truncated to 500 chars) |
| `worktreePath` | root for all worktree file reads |
| `diffPath` | F#+Docs diff, used for line-number checks in Step 4 |
| `fsFiles` / `docsFiles` | `{FS_FILE_LIST}` / `{DOCS_FILE_LIST}` |
| `dartFiles` | carry to the summary as "unchecked" (Dormant lane) |
| `otherFiles` | carry to the summary as "unchecked" — never silently skip |

**1d.** Sanity-check the diff exists (non-empty whenever `fsFiles`/`docsFiles` is non-empty):

```
Read("<diffPath from review-context.json>", limit: 5)
```

If it is empty but `fsFiles`/`docsFiles` listed files, stop and diagnose.

---

### Step 2: Launch F# + Docs review agent

Read the agent prompt template:

```
Read("C:\Users\kami\workspace\github-space\osaHealth\.claude\skills\code-review\agent-prompt.md")
```

Substitute these placeholders in the template text — every value comes from the
`review-context.json` read in Step 1c:

| Placeholder | `review-context.json` field |
|-------------|-----------------------------|
| `{PR}` | `prNumber` |
| `{TITLE}` | `title` |
| `{BODY_SUMMARY}` | `bodySummary` |
| `{MERGE_BASE}` | `mergeBase` |
| `{HEAD_SHA}` | `headSha` |
| `{TEMP_DIR}` | `tempDir` |
| `{FS_FILE_LIST}` | `fsFiles` |
| `{DOCS_FILE_LIST}` | `docsFiles` |

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

### Step 3: Compile and present review summary

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

### Step 4: Dedup and post inline comments

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

…and proceed directly to Step 5.

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

### Step 5: Cleanup

```ps1
# 6. Remove worktree, branch, and temp dir
git worktree remove "{TEMP_DIR}\cr-worktree-{PR}" --force; git branch -D "cr-pr-{PR}"; Remove-Item -Recurse -Force "{TEMP_DIR}\cr-review-{PR}"
```
