# ADR-0011: Authentication — Identity Provider and Token Validation

- **Status:** Proposed (spike required — no decision made)
- **Date:** 2026-05-30
- **Deciders:** Michael Kargl, Remy Okafor (Architecture review)
- **Tags:** backend, security, auth, on-prem, flutter

---

## Context

osaHealth handles health data. Every API request from the Flutter client must be authenticated and scoped to the requesting user. The sync loop security model depends on the server deriving `userId` from a validated token — never from the request body or URL (see [docs/offline-sync.md](../offline-sync.md#security-invariant--uuid-is-an-identifier-not-proof-of-ownership)).

**Constraints:**

- **On-premises deployment.** The full infrastructure is customer-owned. No external services are connected — no cloud identity providers, no SaaS, no callbacks to external networks.
- **Offline-first client.** The Flutter app must function without a network connection. Authentication happens on login; subsequent API calls use the issued JWT. Token refresh requires network access to the IdP.
- **Dapr is already in the stack** for pub/sub, service invocation, and secrets. The Dapr [Bearer middleware](https://docs.dapr.io/reference/components-reference/supported-middleware/middleware-bearer/) handles JWT validation at the sidecar level — no auth code required in application services. This part is not under evaluation; it is the chosen validation mechanism regardless of which IdP is selected.

**The open question** is solely the Identity Provider: what issues JWTs to the Flutter client on login, manages user sessions, and exposes the OIDC discovery endpoint that the Dapr Bearer middleware points to.

---

## Decision

**Pending.** A prototype is required before committing. The unknowns are too operational to resolve from documentation alone — see [Spike scope](#spike-scope) below.

---

## What is already decided

| Concern | Decision |
|---------|----------|
| Token format | JWT (JSON Web Token) |
| Validation mechanism | Dapr Bearer middleware at the sidecar — zero auth code in application services |
| `userId` source | `sub` claim from the validated JWT — never from the request body or URL |
| Protocol | OIDC / OAuth2 (Authorization Code flow with PKCE for the Flutter client) |

---

## Options considered

### Option A — Keycloak *(candidate)*

Open-source, self-hosted Java/Quarkus application. The most widely deployed on-premises IdP. Supports OIDC, SAML, LDAP federation, and fine-grained authorization policies.

- **Pros:** Longest production track record in air-gapped environments. Deep LDAP/AD federation if future deployments require it. Large operator community — issues in isolated environments are well-documented.
- **Cons:** Operationally heavy. Java runtime, complex configuration surface, more moving parts to maintain. Initial setup takes real time.
- **Reversal condition:** If operational burden becomes the dominant complaint across the first two production deployments, Authentik is evaluated as a replacement.

### Option B — Authentik *(candidate)*

Open-source, self-hosted Python application. OIDC, SAML, LDAP federation. Growing fast; the UI is significantly better than Keycloak's.

- **Pros:** Lighter operational footprint. Better developer and administrator UX. Faster to configure for straightforward OIDC setups.
- **Cons:** Shorter track record in fully isolated, air-gapped healthcare environments. At sufficient scale the operational differences from Keycloak are less studied. Fewer community answers for edge cases specific to isolated deployments.
- **Reversal condition:** If a production incident surfaces a gap in the air-gapped operating model that the community cannot help resolve, Keycloak is evaluated as a replacement.

### Option C — Zitadel *(considered, not shortlisted)*

Modern Go-based IdP, self-hostable. Good OIDC implementation and developer experience. Excluded from the prototype shortlist because its operational track record in fully isolated deployments is too thin to evaluate from documentation alone. Revisit if neither Keycloak nor Authentik closes cleanly.

---

## Spike scope

The prototype must answer the following before a decision is made:

1. **Flutter PKCE flow** — does the OAuth2 Authorization Code + PKCE flow work cleanly with the `flutter_appauth` package against both candidates? What does the login UX look like?
2. **Offline token expiry** — the Flutter app can be disconnected for hours or days. When the JWT expires and the IdP is unreachable, what does the sync loop do? Queue silently? Surface a notification? Prompt re-authentication? The IdP choice does not answer this, but the prototype surfaces the exact failure mode so a product decision can be made.
3. **Token refresh** — does refresh work reliably when the device reconnects after an extended offline period? Are there session lifetime constraints that conflict with realistic usage patterns?
4. **Dapr Bearer middleware integration** — configure the middleware against each IdP's OIDC discovery endpoint and JWKS URL. Verify that a JWT issued by the IdP passes sidecar validation end-to-end.
5. **Operational footprint** — how long does cold-start configuration take? What does a minimal production-ready deployment look like (containers, persistence, backup)?

The prototype does not need to implement the full sync loop. A minimal Flutter login screen hitting a stub backend behind the Dapr Bearer middleware is sufficient to answer all five questions.

---

## Consequences

*To be completed when the decision is recorded.*

---

## Reversal conditions

*To be completed when the decision is recorded. Candidate-specific reversal conditions are recorded under each option above.*

---

## References

- [docs/offline-sync.md](../offline-sync.md) — Sync loop design; UUID security invariant that depends on server-side token validation.
- [ADR-0010](0010-backend-database.md) — Backend query layer; establishes Dapr as the service runtime.
- [Dapr Bearer middleware](https://docs.dapr.io/reference/components-reference/supported-middleware/middleware-bearer/) — JWT validation at the sidecar level.
- [Keycloak](https://www.keycloak.org/) — Option A.
- [Authentik](https://goauthentik.io/) — Option B.
