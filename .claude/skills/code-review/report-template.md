# Review Summary Template

The terminal report for Step 3 of `SKILL.md`. Fill every `{placeholder}` from the
agent's report and `review-context.json`.

**ALL sections are REQUIRED** — include every section even when it has no findings.

---

```markdown
# Code Review Summary: PR #{prNumber}

**PR Title**: {title}
**Files Changed**: {count} ({fs_count} F#, {docs_count} docs, {dart_count} Dart (unchecked), {other_count} other (unchecked))
**Checklist**: `review-checklist.md` (all rules triggered by the changed files)

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

{Numbered list of questions from findings below 50% confidence, or "None."}

---

## Unchecked Files

{One line per unchecked file, or "None.":
 - every `dartFiles` entry — reason: Dart is a dormant lane, guidelines not yet formalised
 - every `otherFiles` entry — reason: no review lane matches this path}
```
