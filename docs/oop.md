# Object Orientation

A running reference on OOP fundamentals. Each topic gets its own section, documented as
we work through it — general principles, not tied to any one language.

---

## 1. Encapsulation

Encapsulation isn't "make fields private and write getters/setters for everything."
That's the cargo-cult version, and it protects nothing — a getter/setter pair around a
public field is just a public field with extra steps. Real encapsulation is about
**protecting invariants**: the object controls its own internal state so that it can
never be observed in a state that violates its own rules.

### The five rules (overview)

1. Hide state, expose behavior
2. A logic-free getter/setter is not encapsulation
3. Don't leak mutable internals
4. Encapsulation is about invariants, not visibility keywords
5. Tell, don't ask

### The critical rules

**Rule 1 — Hide state, expose behavior.**
The question isn't "is this field private," it's "can the outside world put this object
into an invalid state." A `BankAccount` with a public (or naively get/set) `balance`
field lets anyone set it to `-500`. A `BankAccount` with a private balance and a
`withdraw(amount)` method that enforces `amount <= balance` protects the invariant *at
the only door that exists*.

**Rule 2 — If you have a getter and setter with no logic in either, you haven't
encapsulated anything.**
That's a public field wearing a disguise. It still means any code, anywhere, can mutate
that state at any time, with zero rule enforcement. The tell: if you can delete the
getter/setter and replace every call site with direct field access and *nothing about
correctness changes*, you never had encapsulation — you had ceremony.

**Rule 3 — Don't leak mutable internals.**
This is the one that actually causes production bugs. Returning a reference to your
internal `List` or `Map` means the caller can mutate your object's guts without going
through any of your logic. You didn't encapsulate the collection, you handed out a spare
key. Fix: return a copy, an unmodifiable view, or better — don't expose the collection at
all, expose *operations* on it (`addItem`, `removeItem`, `contains`).

**Rule 4 — Encapsulation is about invariants, not visibility keywords.**
`private` is a tool, not the goal. You can have a fully `public` class that's still
well-encapsulated if every mutation path enforces the object's rules, and you can have an
all-`private`-fields class that's a lie because it has a `getInternalStateForTesting()`
method that everyone quietly uses in production. Judge encapsulation by "can this object
ever be invalid," not by keyword count.

**Rule 5 — Tell, don't ask.**
The behavioral flip side of hiding state. Instead of asking an object for its data and
then making a decision on the outside (`if (account.getBalance() < amount) { ... }`
scattered across five call sites), tell the object what you want done
(`account.withdraw(amount)`) and let it decide whether that's legal. Every place that
"asks then acts" on another object's internals is a spot where the invariant enforcement
leaked out of the class that owns it.

### The failure mode most often seen in practice

Not "forgot to make it private." It's **the anemic object** — a class that's technically
encapsulated (private fields, generated getters/setters) but has no behavior of its own.
All the logic that should live *inside* the object lives in some `AccountService` or
`AccountManager` class that pokes at the getters and setters from outside. That's
encapsulation in name, procedural code in practice. It's the most common way people fail
this rule while genuinely believing they're doing OOP correctly.
