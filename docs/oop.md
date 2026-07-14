# Object Orientation

A running reference on OOP fundamentals. Each topic gets its own section, documented as
we work through it — general principles, not tied to any one language.

> **🚧 WORK IN PROGRESS.** Sections 1–4 are written. Still to come: abstraction,
> inheritance vs. composition, polymorphism, the Liskov Substitution Principle,
> interfaces vs. abstract classes, SOLID, coupling & cohesion, immutability, the Law of
> Demeter, encapsulate-what-varies, god objects, value objects vs. entities, aggregates,
> and — as the capstone that names the pattern language sections 3–4 already use —
> Domain-Driven Design and how it fits into OOP. Don't treat this doc as complete until
> this banner is gone.

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

---

## 3. Behavior, ports and adapters

A train is not a bag of speeds and brake pressures. It is a live apparatus with hundreds
of states, and it **owns the rules that keep those states valid** — every transition runs
through its invariants, and nobody sets a value without passing the rules first. That part
is genuinely complex, and it belongs to the train.

What the train must *not* know is that this particular motor makes 4000 horsepower. Swap
the diesel for an electric and the train's rule set does not change — therefore the motor
cannot be part of the train.

> **The motor-brand test.** Would swapping the mechanism change this line? If **yes**,
> it's *mechanism* — it belongs outside the domain. If **no**, it's *policy* — it belongs
> inside. This one question resolves almost every "where does this go" argument.

The domain object is a **digital twin**: abstract where the hardware is concrete. It is
not simple in *logic* — it may be the most complex thing you own — it is simple in
*coupling*. It depends on nothing concrete. The hardware gets plugged in later.

### The rules (overview)

1. The object owns its own functionality
2. The object acts through ports, never through mechanism
3. Adapters own mechanism, never policy
4. Persistence is never the object's functionality
5. Exactly one thing persists domain state
6. Tell, don't ask

### Rule 1 — The object owns its own functionality

Heating, spinning, braking — that's what the apparatus *is*. If a `*Service` owns "how to
wash" or "how to brake," it has stolen the object's identity and left behind an
[anemic object](#1-encapsulation): a DTO with a parasite attached.

```
// ❌ ANEMIC — the train is a parameter bag; the service owns the train's rules and state
class Train {
    public int MaxSpeed { get; set; }
    public TrainState State { get; set; }
}

class TrainService {
    public void Accelerate(Train t, int target) {
        if (target > t.MaxSpeed) throw new InvalidOperationException("Too fast");  // train's rule
        t.State = TrainState.Accelerating;                                         // train's state
    }
}

// ✅ The train owns its rules, its state transitions, and its behavior
class Train {
    public void Accelerate(int target) {
        if (target > MaxSpeed)               throw new ArgumentOutOfRangeException(nameof(target));
        if (_state == TrainState.EmergencyStopped) throw new InvalidOperationException("Brake engaged");

        _state = TrainState.Accelerating;
        // ...
    }
}
```

### Rule 2 — The object acts through ports, never through mechanism

The object *does* things. It simply doesn't know how they're wired. A **port** is an
interface the *domain* defines: a socket it plugs a request into. The train knows *where
to enter the request*; it never learns what's on the other side.

```
// ✅ PORT — declared BY the domain, FOR the domain. Speaks the train's language.
interface ITraction { void SetOutput(double fraction); }

class Train {
    private readonly ITraction _traction;

    public void Accelerate(int target) {
        if (target > MaxSpeed) throw new ArgumentOutOfRangeException(nameof(target));

        _state = TrainState.Accelerating;
        _traction.SetOutput(target / (double)MaxSpeed);   // the train ACTS — through a socket
    }
}

// ❌ MECHANISM inside the domain — the train now can't move without a real motor
public void Accelerate(int target) {
    _injector.SetDutyCycle(0.7);                // diesel-specific
    _serialPort.Write("THROTTLE 2800HP\r\n");   // wire protocol
}
```

The dependency points **inward**: the port lives with the domain, infrastructure
implements it. The train never references the adapter.

### Rule 3 — Adapters own mechanism, never policy

An **adapter** is the infrastructure-side implementation of a port — the thing that knows
*how*. And it can be enormous. `ITraction.SetOutput(0.7)` is one method; behind it a
`DieselTraction` juggles injection timing, turbo lag and ramp limits, while an
`ElectricTraction` juggles inverter frequency and battery state-of-charge.

**Thin in interface, arbitrarily thick in implementation.** The adapter is the designated
container for mess — the domain stays clean *because* the adapter absorbs the ugliness.

An adapter's size is proportional to the *impedance mismatch* between the domain's
vocabulary and the mechanism's (plainly: the further apart the two languages are, the more
translating it has to do). In a CRUD system the domain already speaks the mechanism's
language — rows and fields on both sides — so the adapter shrinks to a
[shim](glossary.md#shim). That's why most people have only ever met thin ones.

```
// Same one-method port. Wildly different guts. Neither is visible to the train.
class DieselTraction : ITraction {
    public void SetOutput(double fraction) {
        var hp = fraction * 4000;
        _turbo.Spool(hp);
        _injector.RampTo(hp, over: TimeSpan.FromSeconds(4));   // a diesel can't be slammed
    }
}

class ElectricTraction : ITraction {
    public void SetOutput(double fraction) {
        if (_battery.StateOfCharge < 0.1) fraction *= 0.5;     // ✅ mechanism: derating
        _inverter.SetFrequency(fraction * MaxHz);
    }
}
```

**The hard constraint:** an adapter may be arbitrarily complex with **mechanism**. It may
never hold **policy**.

```
// ❌ POLICY leaking into an adapter — a safety rule that VANISHES the day someone
//    fits an electric motor, because it only exists in the diesel implementation
class DieselTraction : ITraction {
    public void SetOutput(double fraction) {
        if (_train.OnCurve && fraction > 0.7) fraction = 0.7;   // ❌ that's the TRAIN's rule
        ...
    }
}
```

Apply the motor-brand test in reverse to catch leaks going outward: *would swapping diesel
for electric change this line?* No — so it's policy, and it belongs back in the train.

### Rule 4 — Persistence is never the object's functionality

This is the rule that finally answers "may the object hold a repository." Ask: *is this
the object's functionality?* Braking — yes. Accelerating — yes. **Saving itself to a
database — no.** A train does not persist itself; that was never part of being a train.
It's the application's job.

Three different things masquerade as "persistence," and only one of them is the
repository's:

| What | What it is | Who owns it |
|------|-----------|-------------|
| **Domain state** | "this train is Accelerating, at 80km/h" | the repository, at the boundary |
| **Telemetry** | `100, 110, 120…` — a stream of observations | cross-cutting infrastructure; *nothing in the domain* |
| **Domain events** | "ExceededSpeedLimit" — something the domain deemed significant | the object *emits a value*; the shell persists it |

```
// ❌ The object reaches for persistence. It now needs a database in order to brake.
class Train {
    private readonly ITrainRepository _repository;   // ❌ not a port — a lifecycle concern

    public void Accelerate(int target) {
        var since = _repository.CountKmSinceService(Id);   // IO, inside the domain
        if (since > ServiceInterval) throw new MaintenanceDueException();
    }
}

// ✅ Pass the data in. The orchestrator fetches it.
class Train {
    public void Accelerate(int target, int kmSinceService) {
        if (kmSinceService > ServiceInterval) throw new MaintenanceDueException();
        ...
    }
}
```

`ITraction` is fine and `ITrainRepository` is not — even though both are interfaces. The
difference isn't the shape, it's *whose job it is*.

If a reading is domain-*significant*, the object doesn't persist it and doesn't page
anyone. It emits a **value** and lets the shell decide:

```
class Train {
    public void Observe(int speedKmh) {
        CurrentSpeed = speedKmh;                              // invariant-guarded (section 2)
        if (speedKmh > SpeedLimit)
            _events.Add(new SpeedLimitExceeded(speedKmh));    // ✅ a value. It doesn't call anyone.
    }
}

// orchestrator
foreach (var e in train.DrainEvents())
    _eventStore.Append(e);                                    // ✅ persistence, at the boundary
```

### Rule 5 — Exactly one thing persists domain state

The tempting mistake is to let each part save itself — the traction saves, the brakes
save, the train saves. Don't.

If three components persist independently you have three writers, three transactions, and
no coherent answer to *"what was the train's state at time T?"* You cannot roll back a
half-written train, because there is no single write to roll back.

```
// ❌ Everyone persists. No atomicity, no consistency boundary, no rollback.
class Train    { void Accelerate() { ...; _repo.Save(this); } }
class Traction { void SetOutput()  { ...; _repo.SaveTraction(this); } }

// ✅ One writer, at the boundary, once per operation. THAT is the consistency boundary.
class TrainControlService {
    public void Accelerate(Guid id, int target) {
        var train = _repo.Load(id);                              // IO:     load
        train.Accelerate(target, _repo.CountKmSinceService(id)); // DOMAIN: behave
        _repo.Save(train);                                       // IO:     persist — once
    }
}
```

That single write is what makes the word "consistent" mean anything. Parts never persist
themselves.

### Rule 6 — Tell, don't ask

Notice what the orchestrator does *not* do: reach into the train, pull its limits out, and
re-derive the train's own rules on its behalf.

```
// ❌ ASKING — the train's rules now live in the caller, ready to be duplicated by the
//    next caller who needs them
if (target > train.MaxSpeed)                    throw new InvalidOperationException("Too fast");
if (train.State == TrainState.EmergencyStopped) throw new InvalidOperationException("Braked");
train.State = TrainState.Accelerating;

// ✅ TELLING — the train owns the rules and enforces them itself
train.Accelerate(target, kmSinceService);
```

Same principle as [Encapsulation Rule 5](#1-encapsulation), applied to a whole operation
rather than a single field.

### Variant — when the domain must be side-effect free

Some architectures require a **pure** domain: no side effects at all, not even through
ports (this codebase is one — see *Pure function* in the glossary). There the object
doesn't act; it returns a **description** of what should happen, and the shell executes it.
This is the *functional core, imperative shell* pattern.

```
// The train decides — purely. Same input, same output, always. No mocks needed to test it.
class Train {
    public TractionCommand PlanAcceleration(int target) {
        if (target > MaxSpeed) throw new ArgumentOutOfRangeException(nameof(target));
        return new TractionCommand(target / (double)MaxSpeed);   // a value, not an action
    }
}

// The shell does — every side effect lives here
var command = train.PlanAcceleration(120);
traction.SetOutput(command.Fraction);
```

The line doesn't move: the train still owns *what* happens and never the mechanism. What
changes is whether it *initiates* the effect or merely *describes* it. Ports are the
natural OOP answer; the pure core is what you reach for when the domain must be provably
free of side effects.

### The litmus test

Everything above collapses into one question:

> **Can you instantiate the object and run a full scenario with no hardware and no
> database** — plugging in fake adapters and running it in a simulator?

If yes, your boundaries are right. If constructing a `Train` requires a live motor driver
or a database connection, they are not. This is why "no repository in the entity" isn't
dogma: a repository in the constructor means the twin cannot run without a database, and
the simulator dies.

It also tells you how to test each layer. The domain gets **unit tests with no mocks** —
it has no dependencies to mock. Adapters get **integration tests** against the real
mechanism or a simulator of it. If you find yourself needing a mocking framework to test
your *domain*, that's the alarm telling you a mechanism got in.

---

## 4. Dependency injection into objects

Injection isn't banned from domain objects — it's **filtered**. A train may hold an
`ITraction`. It may not hold an `ITrainRepository`. Both are interfaces; only one is the
train's business.

### The rules (overview)

1. Inject ports — the abstractions the domain owns
2. Never inject persistence, transactions, or "which object" concerns
3. A `*Service` collaborator is often a value object wearing a uniform
4. Pass *transient* collaborators to the method, not the constructor
5. Wire it all up at the composition root

### Rule 1 — Inject ports, the abstractions the domain owns

A port is defined *by* the domain, *for* the domain, and describes a capability the object
genuinely has. Constructor-injecting one is correct — the train has traction for its whole
life.

```
// ✅ Declared in the domain, next to the object that needs it
interface ITraction { void SetOutput(double fraction); }

class Train {
    private readonly ITraction _traction;
    public Train(ITraction traction, int maxSpeed) { _traction = traction; MaxSpeed = maxSpeed; }
}

// Implemented in infrastructure. The domain never sees this file.
class DieselTraction : ITraction { public void SetOutput(double f) { /* injectors, turbo */ } }
```

### Rule 2 — Never inject persistence, transactions, or "which object" concerns

The test isn't "is it an interface." It's *whose functionality is this?*

```
// ❌ A repository is not a capability of a train. It's how the APPLICATION manages trains.
class Train {
    public Train(ITraction traction, ITrainRepository repo) { ... }   // ❌
}

// ✅ The repository belongs to the orchestrator, which is allowed to know about databases
class TrainControlService {
    public TrainControlService(ITrainRepository repo) { _repo = repo; }   // ✅
}
```

### Rule 3 — A `*Service` collaborator is often a value object wearing a uniform

"The train needs an `IRouteService` injected so `Follow` can call it" deserves a hard
look at what that service actually *does*. If a route is just data and rules — stops,
gradients, speed limits, no IO — it isn't a service at all.

```
// ❌ A "service" with no IO and no state — injected for no reason
interface IRouteService { Stop[] GetStops(string routeName); }

class Train {
    public Train(IRouteService routes) { _routes = routes; }
}

// ✅ It was a value object all along
record Route(string Name, Stop[] Stops, int SpeedLimit);

train.Follow(northernLine);   // handed in as an argument, not injected
```

A name ending in `Service` *invites* you to inject it. Ask what it does before you believe
the name.

### Rule 4 — Pass transient collaborators to the method, not the constructor

A port is permanent — the train always has traction. A **clock**, a pricing policy, an
exchange rate is *situational*: it belongs to the operation, not the object's whole life.
Pass it to the method (**method injection** — sometimes called *double dispatch* in DDD
circles, though the term classically means something narrower).

```
// ❌ CONSTRUCTOR — every Train now depends on a clock forever, including the ninety
//    percent of operations that never look at the time
class Train {
    public Train(ITraction traction, IClock clock) { ... }
}

// ✅ METHOD — the collaborator arrives with the call that actually needs it
class Train {
    public void Depart(Route route, IClock clock) {
        _departedAt = clock.Now();
        ...
    }
}

train.Depart(northernLine, systemClock);
```

### Rule 5 — Wire it all up at the composition root

The "virtual USB cable" gets plugged in at exactly **one** place — `main()`, startup, the
composition root. That's where real adapters meet the ports. Nowhere else in the system
knows which implementation it got.

```
// composition root — the ONLY place that knows a diesel exists
var traction = new DieselTraction(gpio);          // real mechanism
var train    = new Train(traction, maxSpeed: 200);
var service  = new TrainControlService(new MongoTrainRepository(db));

// ...and in a test, the same domain object, no hardware, no database:
var fakeTraction = new FakeTraction();
var train = new Train(fakeTraction, maxSpeed: 200);
train.Accelerate(120, kmSinceService: 0);
Assert.Equal(0.6, fakeTraction.LastOutput);       // no mocking framework in sight
```

That the *same* `Train` runs against a real locomotive and a simulator, unchanged, is not
a nice side benefit. It **is** the payoff. If it isn't true, the boundaries are wrong.
