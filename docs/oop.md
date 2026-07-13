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

---

## 2. Object construction & invariants

Validation for an invariant lives in exactly **one place**. The constructor does not
re-implement a rule the setter already enforces — it routes through the same door. Two
enforcement points for one rule isn't defense in depth, it's a drift bug waiting for a
diff that only touches one of them.

### The rules (overview)

1. One invariant, one enforcement point
2. Mutable state: the constructor delegates to the private setter
3. Immutable config: validate, then assign to a readonly field
4. Real objects have both — fixed config and moving parts
5. Multi-field invariants create an assignment-order dependency

### Rule 1 — One invariant, one enforcement point

A constructor and a setter that each independently check "is this below freezing" isn't
defense in depth — it's two copies of the same rule that will inevitably drift. Someone
"fixes" the freezing threshold in the setter six months from now, forgets the constructor
has its own copy, and now the object can be *constructed* in one invalid state and *set*
into a different invalid state. One rule, one place that knows it.

### Rule 2 — Mutable state: the constructor delegates to the private setter

The constructor doesn't need to know what "invalid" means — it hands the value to the
thing that does:

```
class WashingMachine {
    private double _waterTemperature;

    public WashingMachine(double waterTemperature) {
        WaterTemperature = waterTemperature; // routes through the setter
    }

    public double WaterTemperature {
        get => _waterTemperature;
        private set {
            if (value < FreezingPoint)
                throw new ArgumentOutOfRangeException(nameof(value), "Water can't be colder than freezing.");
            _waterTemperature = value;
        }
    }
}
```

Now there is one place that knows the rule. If the freezing threshold ever changes, or a
second rule gets added ("also can't exceed boiling"), every path that mutates the
field — the constructor, and any future method like `Heat(degrees)` — inherits the fix
automatically, because they all go through the same setter. This is
[Encapsulation Rule 1](#1-encapsulation) (state hidden behind behavior) and
[Rule 4](#1-encapsulation) (the invariant lives with the data, not with however many call
sites touch it) applied to construction specifically.

### Rule 3 — Immutable config: validate, then assign to a readonly field

If a value can't change after construction — a machine's *rated* max temperature, not
its *current* temperature — it shouldn't get a private setter at all. A private setter
advertises mutability that doesn't exist; that's a small lie for the next person reading
the class. Instead, validate inline (or via a `static` validation function) and assign to
a `readonly` field once. There is no door to walk through after construction because
there's nothing left to guard.

### Rule 4 — Real objects have both

A washing machine has fixed config — `MinWaterTemperature`, `MaxWaterTemperature`, no
setters, validated once and assigned `readonly` — *and* moving parts —
`CurrentWaterTemperature`, `Rpm` — that genuinely change during a cycle and need the
private-setter pattern from Rule 2. The moving part's invariant is often defined in terms
of the config's invariant, not a standalone constant: "within the range *this machine*
was built with," not "above freezing" as a global rule.

### Rule 5 — Multi-field invariants create an assignment-order dependency

```
class WashingMachine {
    public double MinWaterTemperature { get; }
    public double MaxWaterTemperature { get; }

    private double _currentWaterTemperature;
    public double CurrentWaterTemperature {
        get => _currentWaterTemperature;
        private set {
            if (value < MinWaterTemperature || value > MaxWaterTemperature)
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    $"Current temperature must be within [{MinWaterTemperature}, {MaxWaterTemperature}].");
            _currentWaterTemperature = value;
        }
    }

    public WashingMachine(double minTemp, double maxTemp, double startingTemp) {
        if (minTemp < AbsoluteFreezingPoint)
            throw new ArgumentOutOfRangeException(nameof(minTemp), "Can't configure below freezing.");
        if (maxTemp < minTemp)
            throw new ArgumentOutOfRangeException(nameof(maxTemp), "Max must be >= min.");

        MinWaterTemperature = minTemp;
        MaxWaterTemperature = maxTemp;
        CurrentWaterTemperature = startingTemp; // now validated against the config we just set
    }
}
```

`CurrentWaterTemperature`'s setter reads `MinWaterTemperature`/`MaxWaterTemperature`, so
those fields **must** be assigned before it runs. Get the order backwards and the setter
validates against whatever the fields default to (`0`/`0`), and the bug won't surface
until someone builds a machine with a non-default range. The ordering isn't incidental —
it's the invariant made visible in code.

> The `readonly`-vs-private-setter distinction used above as a signal of mutability gets
> its own full treatment in a later **Immutability** section.
