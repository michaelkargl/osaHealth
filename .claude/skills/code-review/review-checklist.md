# osaHealth Review Checklist

Single source of truth for the `code-review-osa` skill.
Edit here — never duplicate rule text in `SKILL.md` or any agent prompt.

The lane globs in the headings below are labels for humans; the matching patterns are
implemented in `Prep-Pr.ps1`. This file owns review *policy*; the script owns file *matching*.

---

## F# files (`*.fs`, `*.fsx`)

Apply every rule that a changed file triggers. Skip only if the rule is provably N/A
for that file (e.g. rule 11 for a file that is not `DependencyInjection.fs`).

1. **Functional only** — no classes, no inheritance, no interfaces. Every dependency
   arrives as a function parameter.
   → [Functional only](../../../docs/coding-guidelines-fsharp.md#functional-only)

2. **Curried parameters** — injected or partially-applied functions use curried args,
   not tuples. Tuples are fine only for genuinely grouped return values.
   → [Curried parameters over tuples](../../../docs/coding-guidelines-fsharp.md#curried-parameters-over-tuples)

3. **Pure domain** — domain functions have no I/O and do not call `DateTime.UtcNow`.
   Time must be injected as `now: unit -> DateTime`.
   → [Domain layer — pure functions only](../../../docs/coding-guidelines-fsharp.md#domain-layer--pure-functions-only)
   → [Domain Model Layer](../../../docs/onion-architecture.md#domain-model-layer)

4. **Plain primitives in Commands/Queries** — no UMX-branded types as fields.
   "Plain" means leaf types; nested record types in the same assembly are fine.
   → [Layer boundaries](../../../docs/coding-guidelines-fsharp.md#layer-boundaries)
   → [Application Services Layer](../../../docs/onion-architecture.md#application-services-layer)

5. **Single tagging point** — UMX tagging (unbranded → branded) happens once,
   in the handler at entity construction, not in the HTTP layer or query/command type.
   → [Mapping and data transformation](../../../docs/coding-guidelines-fsharp.md#mapping-and-data-transformation)

6. **One mapping function per boundary** — no transformation logic leaking into
   the layer it serves. Each crossing has exactly one function that owns it.
   → [Mapping and data transformation](../../../docs/coding-guidelines-fsharp.md#mapping-and-data-transformation)

7. **Error handling discipline** — `Result<'T,'E>` for expected failures; never
   exceptions for control flow. Exceptions caught only at the infrastructure boundary.
   User input that fails validation **must yield a 4xx, never a 5xx**.
   → [Error handling](../../../docs/coding-guidelines-fsharp.md#error-handling)

8. **Computation expression hygiene** — name the `Ok` value, not the wrapper.
   Use `do!` for `Result<unit,_>` / `Task<unit>`. Never block on `.Result` inside
   a `task {}` block.
   → [Anti-patterns](../../../docs/coding-guidelines-fsharp.md#anti-patterns)

9. **`open` ordering** — three tiers (BCL/third-party, framework, project-specific),
   alphabetical within each tier, no blank lines between tiers.
   → [Open statement ordering](../../../docs/coding-guidelines-fsharp.md#open-statement-ordering)

10. **No nulls** — no `null`, no `Option.Value`, no `unbox`. Pattern-match `option`
    and `Result` explicitly.
    → [Null safety](../../../docs/coding-guidelines-fsharp.md#null-safety)

11. **`DependencyInjection.fs` is wiring only** — no validation, no mapping,
    no business rules. It partially applies infrastructure dependencies and nothing more.
    → [Dependency wiring](../../../docs/coding-guidelines-fsharp.md#dependency-wiring--dependencyinjectionfs)

12. **Compiler confidence** — don't flag what the F# compiler already catches
    (exhaustive match, unused bindings, missing opens). Focus on:
    architectural fit, domain-rule correctness, error-path coverage, layer-boundary discipline.
    → [Compiler confidence](../../../docs/coding-guidelines-fsharp.md#compiler-confidence)

13. **Security basics** — no string-built MongoDB filters (use typed
    `FilterDefinitionBuilder`), no PII or secrets in logs or responses, derive user
    identity from the authenticated principal not the request body, and only bind
    fields the caller is authorised to set.
    → [Query injection](../../../docs/security.md#query-injection)
    → [Data exposure](../../../docs/security.md#data-exposure)
    → [Access control](../../../docs/security.md#access-control)

14. **Database performance** — no DB calls inside loops (use batch operations),
    project only the fields the handler needs, and every new query path must have
    an index to back it.
    → [Database calls inside loops](../../../docs/performance.md#database-calls-inside-loops)
    → [Read projection](../../../docs/performance.md#read-projection)
    → [Indexes](../../../docs/performance.md#indexes)

---

## Docs files (`docs/**/*.md`)

1. **No function names as doc anchors** — use a neutral label ("Example") rather than
   a specific function name. Function names change; the doc label should not.

2. **New terms belong in the glossary** — introduce domain terms in `docs/glossary.md`
   and cross-link from where they appear; don't redefine inline in a different file.

3. **Cross-references point to the owning section** — link to the doc/section that
   owns the rule, not to a duplicate. Avoid copying the full rule text into two places.

---

## Flutter/Dart files (`src/frontend/lib/**/*.dart`)

**Dormant lane.** When Dart files change, note them as "unchecked" in the summary.
Do not review Flutter code; guidelines for it are not yet formalised.

---

## Confidence bands

| Band    | Meaning |
|---------|---------|
| 90–100% | Definite — demonstrably breaks a rule |
| 70–89%  | Strong — almost certainly a violation |
| 50–69%  | Uncertain — may lack full context |
| 30–49%  | Speculative — could be intentional; frame as a question |
| <30%    | Discard — do not raise |

- Below **50%**: include a Clarifying Question in the report entry and list it in the
  "Questions Requiring Clarification" section of the terminal summary.

---

## Severity rules

| Severity    | When to use |
|-------------|-------------|
| MUST FIX    | Bugs, security vulnerabilities, data loss risk, breaking changes, any failed mandatory check, any user-input path that returns 5xx instead of 4xx |
| NICE TO FIX | Style improvements, minor optimisations, documentation suggestions, non-critical guideline deviations |
