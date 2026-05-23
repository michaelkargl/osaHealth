# ADR-NNNN: <Short, decision-shaped title>

- **Status:** Proposed | Accepted | Superseded by [ADR-XXXX](XXXX-...md) | Deprecated
- **Date:** YYYY-MM-DD
- **Deciders:** <names / roles>
- **Tags:** <e.g. backend, frontend, infra, security>

## Context

What is the problem we are solving? What forces are at play (technical, organisational, regulatory)? What constraints box us in? Link the issue or discussion that prompted this decision.

Keep this section narrow — one decision per ADR. If two forces deserve their own argument, split them.

## Decision

State the decision in one or two sentences, in the active voice. "We will use X for Y."

Then expand: what exactly does adopting this look like in the codebase? Which components are affected? What changes on day one?

## Options considered

For each serious option, write one paragraph:

### Option A — <name>
- Pros: …
- Cons: …
- Why it lost (or won): …

### Option B — <name>
- Pros: …
- Cons: …
- Why it lost (or won): …

A losing option without a recorded reason is a future re-litigation waiting to happen. Spend the words.

## Consequences

What becomes easier? What becomes harder? What new failure modes does this introduce, and how would we notice them at 3am?

Include any follow-up work this ADR creates (link issues).

## Reversal conditions

What would have to be true for us to revisit and overturn this decision? Be concrete — "if the library is unmaintained for 12 months", "if we hit >N tenants and the per-tenant overhead becomes load-bearing", "if a security audit flags the pattern".

An ADR without reversal conditions calcifies into folklore. Write the conditions even if you think they are unlikely.

## References

- Links to issues, PRs, prior art, external documentation.
