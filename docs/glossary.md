# Glossary

---

## I

### Invariant

A rule that must always be true, no matter what. If it is ever false, the system's correctness or security model breaks.

*Example from this codebase:* the server always derives `userId` from the auth token — never from the request body or URL. This must hold for every request without exception.

---

## 4

### 403 Forbidden

The HTTP 403 Forbidden client error response status code indicates that the server understood the request but refused to process it.

<https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Status/403>

### 409 Conflict

The HTTP 409 Conflict client error response status code indicates a request conflict with the current state of the target resource.

<https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Status/409>