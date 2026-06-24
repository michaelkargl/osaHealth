---
name: code-review
description: >
  Review the current branch (or a specific PR) against osaHealth's documented
  guidelines. Produces a grouped terminal summary and auto-posts deduped inline
  comments to the open PR via one batched GitHub reviews API call.
allowed-tools: Bash(gh issue view:*), Bash(gh search:*), Bash(gh issue list:*), Bash(gh pr comment:*), Bash(gh pr diff:*), Bash(gh pr view:*), Bash(gh pr list:*), Bash(gh api:*), Bash(grep:*), Bash(awk:*), Bash(find:*), Bash(sort:*), Bash(sed:*), Bash(wc:*), Bash(head:*), Bash(tail:*), Bash(comm:*), Bash(git worktree:*), Bash(git log:*), Bash(git show:*), Bash(git merge-base:*), Bash(git diff:*), Bash(git fetch:*), Bash(git branch:*), Bash(git remote:*), Bash(rm -rf /tmp/cr-worktree-*), Bash(rm -rf /tmp/cr-review-*), Bash(mkdir -p /tmp/cr-*)
---

# Code Review

## Input

```
/code-review [<PR number>]
```

- **No argument** — reviews the current local branch against `origin/main`.
- **PR number** — fetches the PR, checks out its head ref into an isolated worktree,
  and reviews that instead.

---

## Review checklist

Each item is a one-line rule in our phrasing with a pointer to the doc section
that owns it; the reviewer opens the doc only when it needs the detail.

> The checklist is a starting set applied with judgement, not an exhaustive linter.
> Some items require context; mark your confidence honestly and explain low-confidence
> calls so the author can verify.

### F# files (`*.fs`)

Source: `docs/coding-guidelines-fsharp.md` and `docs/onion-architecture.md`.

1. **Functional only** — no classes, no inheritance, no interfaces. Every dependency
   arrives as a function parameter.
   → [Functional only](../../../docs/coding-guidelines-fsharp.md#functional-only)

2. **Curried parameters** — functions that are injected or partially applied must use
   curried args, not tuples. Tuples are fine only for genuinely grouped return values.
   → [Curried parameters over tuples](../../../docs/coding-guidelines-fsharp.md#curried-parameters-over-tuples)

3. **Pure domain** — domain functions have no I/O and do not call `DateTime.UtcNow`.
   Time must be injected as `now: unit -> DateTime`.
   → [Domain layer — pure functions only](../../../docs/coding-guidelines-fsharp.md#domain-layer--pure-functions-only)
   → [Domain Model Layer](../../../docs/onion-architecture.md#domain-model-layer) ("Not allowed" table)

4. **Plain primitives in Commands/Queries** — no UMX-branded types as fields.
   "Plain" means leaf types; nested record types defined in the same assembly are fine.
   → [Layer boundaries](../../../docs/coding-guidelines-fsharp.md#layer-boundaries)
   → [Layer rule](../../../docs/coding-guidelines-fsharp.md#layer-rule)
   → [Application Services Layer](../../../docs/onion-architecture.md#application-services-layer) ("Not allowed" table)

5. **Single tagging point** — UMX tagging from unbranded → branded happens once,
   in the handler at entity construction, not in the HTTP layer or the query/command type.
   → [Mapping and data transformation](../../../docs/coding-guidelines-fsharp.md#mapping-and-data-transformation)
   → [Layer rule](../../../docs/coding-guidelines-fsharp.md#layer-rule)

6. **One mapping function per boundary** — no transformation logic leaking into the
   layer it serves. Each crossing has exactly one function that owns it.
   → [Mapping and data transformation](../../../docs/coding-guidelines-fsharp.md#mapping-and-data-transformation)

7. **Error handling discipline** — `Result<'T,'E>` for expected failures, never
   exceptions for control flow. `failwith` only for unreachable branches or
   unrecoverable programmer errors. Exceptions are caught only at the infrastructure
   boundary.
   → [Error handling](../../../docs/coding-guidelines-fsharp.md#error-handling)

8. **Computation expression hygiene** — name the `Ok` value, not the wrapper.
   Use `do!` for `Result<unit,_>` / `Task<unit>`. Never block on `.Result` inside
   a `task { }` block.
   → [Anti-patterns](../../../docs/coding-guidelines-fsharp.md#anti-patterns)

9. **`open` ordering** — three tiers (BCL/third-party, framework, project-specific),
   alphabetical within each tier, no blank lines between tiers.
   → [Open statement ordering](../../../docs/coding-guidelines-fsharp.md#open-statement-ordering)

10. **No nulls** — no `null`, no `Option.Value`, no `unbox`. Pattern-match `option`
    and `Result` explicitly.
    → [Null safety](../../../docs/coding-guidelines-fsharp.md#null-safety)

11. **`DependencyInjection.fs` is wiring only** — no validation logic, no mapping,
    no business rules. It partially applies infrastructure dependencies into handlers
    and nothing more.
    → [Dependency wiring](../../../docs/coding-guidelines-fsharp.md#dependency-wiring--dependencyinjectionfs)

12. **Compiler confidence** — F# has a strong compiler. Exhaustive pattern-match
    warnings, unused-binding errors, and missing-open diagnostics are caught before
    the code reaches review. A review comment that duplicates a compiler warning
    wastes the author's attention. Let the compiler carry what it can; focus review
    attention on what it cannot see: architectural fit, domain-rule correctness,
    error-path coverage, and layer-boundary discipline.
    → [Compiler confidence](../../../docs/coding-guidelines-fsharp.md#compiler-confidence)

13. **Security basics** — no string-built MongoDB filters (use typed
    `FilterDefinitionBuilder`), no PII or secrets in logs or responses, derive user
    identity from the authenticated principal not the request body, and only bind
    fields the caller is authorised to set.
    → [Security](../../../docs/security.md)

14. **Database performance** — no DB calls inside loops (use batch operations),
    project only the fields the handler needs, and every new query path must have
    an index to back it.
    → [Performance](../../../docs/performance.md)

---

### Docs files (`docs/**/*.md`)

Source: established conventions in this project.

1. **No function names as doc anchors** — use a neutral label ("Example") rather than
   a specific function name for code examples. Function names change; the label in the
   doc should not need to keep up.

2. **New terms belong in the glossary** — if the PR introduces a domain term, it should
   be defined in `docs/glossary.md` and cross-linked from where it appears; not
   redefined inline in a different file.

3. **Cross-references point to the owning section** — link to the doc/section that owns
   the rule, not to a duplicate. Avoid copying the full rule text into two places.

---

## CRITICAL Tool Usage Rule

**Never inspect files through Bash.** Every Bash invocation prompts the user for
approval. Read, Grep, and Glob are purpose-built for file work and run without
prompts — they cover reading, pattern search, and file discovery respectively.
Reserve Bash for the git/GitHub operations listed in `allowed-tools` above;
those operations have no dedicated tool equivalent.

---

## Review Process

### Step 1: Fetch the diff

**No PR number** (local branch review):
```bash
git fetch origin main
git diff origin/main...HEAD
```
The diff output is the authoritative change record — scan `diff --git` headers
for the file list. No separate `--name-only` call is needed.

**PR number given:**
```bash
gh pr view <PR> --repo michaelkargl/osaHealth --json title,body,headRefOid
gh pr diff <PR> --repo michaelkargl/osaHealth
```
`gh pr diff` returns the full diff with no pagination cap — use it as the single
source of truth for both the file list and the change content.

> **Why not `gh pr view --json files`?** That field paginates silently at 100
> files. `gh pr diff` has no such limit.

### Step 2: Partition changed files into review lanes

Extract file paths from `diff --git` headers in the diff (Grep for `^diff --git`).
Split by pattern:

| Lane | Pattern | Status |
|---|---|---|
| F# | `*.fs` | Active — apply F# checklist |
| Docs | `docs/**/*.md` | Active — apply Docs checklist |
| Flutter | `src/frontend/**/*.dart` | **Dormant** — no guideline doc yet; note it as out-of-scope in the summary |

Any file matching no lane goes into the summary as "unchecked" — never silently
skip it.

### Step 3: Apply the checklist

Work through the diff (already in context from Step 1). Review **added and
context lines only** — pre-existing code outside the diff is not in scope.

If surrounding context is needed beyond the diff (e.g. to trace a symbol's
definition), read the full file from the working directory (local mode) or
fetch it via `gh api repos/michaelkargl/osaHealth/contents/<path>?ref=<headRefOid>`
(PR mode).

For each finding record:
- **File path** (relative to repo root)
- **Line number** (from the `+` side of a diff hunk — must fall within the hunk)
- **Title** — short label (5–10 words); this doubles as the dedup key
- **Severity** — `BLOCKER` or `SUGGESTION`
- **Confidence** — numeric, 0–100%:

| Band | Meaning |
|---|---|
| 90–100% | Definite — the change demonstrably breaks a rule, with a concrete example |
| 70–89% | Strong — almost certainly violates the guideline; an edge case might save it |
| 50–69% | Uncertain — reads like a violation but the reviewer may lack full context |
| 30–49% | Speculative — could be intentional; frame it as a question, not a finding |
| <30% | Discard — not confident enough to raise |

- **Body** — explanation and fix; link to the owning guideline section

Threshold behaviour:
- Below **75%**: embed a clarifying question in the inline comment body.
- Below **50%**: also surface the question in the terminal summary.

Prefer [Serena](../../../docs/serena.md) over Grep for code exploration. Serena
provides LSP-powered `find_declaration`, `find_references`, `get_symbols_overview`,
and `get_diagnostics` — faster and more precise than text searches for tracing
symbols, checking change impact, or surfacing warnings the compiler already owns.
Fall back to Grep if Serena is unreachable.

### Step 4: Deliver findings

#### 4a — Terminal summary

Print findings grouped by file:

```
── src/osaHealth.Api/QueryHandlers.fs ─────────────────────
  BLOCKER   (95%)  Line 47: UserId branded in query type
  ...
── docs/coding-guidelines-fsharp.md ───────────────────────
  SUGGESTION (65%) Line 12: function name used as doc anchor
  ...
```

If no findings: print `✓ No findings — everything looks good.` and stop.

#### 4b — Post inline comments to the PR

PR number required. Without one, skip this step and note it in the summary.

**Dedup check.** Fetch existing Claude-generated comments so re-runs don't
double-post:

```bash
gh api repos/michaelkargl/osaHealth/pulls/<PR>/comments --paginate
```

Keep only comments whose body contains `🤖 Generated with [Claude Code]`.
From each, extract `path` and the first `### ` heading to build a set of
`(path, title)` lookup keys. Keying on title rather than line number keeps
dedup robust when new commits shift lines around.

Drop any finding whose `(path, title)` key is already in the set. If every
candidate is a duplicate, print:

> "No new review comments to post on PR #<N> — all <M> issues from this run
> are already present as Claude-generated comments."

…and stop.

**Verify line numbers.** For each remaining candidate, confirm the reported
line sits inside a diff hunk. The API rejects comments placed on lines outside
the diff. A `+` line in the diff output is always valid; a context line
(no prefix) in the same hunk also works.

**Post in a single batched call:**

```bash
gh api repos/michaelkargl/osaHealth/pulls/<PR>/reviews \
  --method POST \
  --input - <<'EOF'
{
  "event": "COMMENT",
  "comments": [
    {
      "path": "src/osaHealth.Api/QueryHandlers.fs",
      "line": 47,
      "body": "### Issue Title\n\n**Severity**: BLOCKER · **Confidence**: 95% · **Category**: Guideline Violation\n\nDescription here.\n\n🤖 Generated with [Claude Code](https://claude.ai/code)"
    }
  ]
}
EOF
```

One POST for all comments — never loop individual `gh pr comment` calls
(those create non-resolvable issue-level comments). Use `"event": "COMMENT"`
for an informational review; never `REQUEST_CHANGES` or `APPROVE`.

**Comment body format** (markdown string in the JSON `body` field):

```markdown
### {Issue Title}

**Severity**: {BLOCKER | SUGGESTION} · **Confidence**: {N}% · **Category**: {category}

{Description}

{If applicable}
**Guideline**: {skill name and specific rule}
{/If}

{If confidence < 75%}
**Question**: {clarifying question}
{/If}

🤖 Generated with [Claude Code](https://claude.ai/code)
```

After posting, print a one-line summary:

> "Posted <N> new review comments on PR #<number> (<M> duplicate(s) skipped)."

> **Line numbers must be inside diff hunks.** If the API returns
> "Line could not be resolved," check the diff output for the nearest `+`
> or context line and retry.

---

## Reviewer guidance

- A `SUGGESTION` is a real improvement, not a blocker. Use your judgement
  about raising it given the PR's scope.
- Low-confidence findings should state *why* confidence is low so the author
  can verify. A low-confidence flag is still useful signal — don't suppress it.
- Pre-existing issues in untouched code are out of scope. If something is
  severe enough to warrant a follow-up, mention it as a separate concern.
- The Flutter lane is intentionally dormant. When `src/frontend/` files
  changed, call that out in the summary so the team knows those were skipped.
- Prefer Serena over Grep for code exploration. Use `get_diagnostics` on
  changed files before reviewing — don't flag what the compiler already
  catches. Use `find_references` to cross-check change impact. Fall back
  to Grep if the Serena MCP server is unreachable.
  → [Serena docs](../../../docs/serena.md)
