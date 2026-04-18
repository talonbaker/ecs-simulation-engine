# Unit Testing — A Field Guide for Engineers

> **Project:** ECS Simulation Engine  
> **Author:** Talon Baker  
> **Version:** v0.7.2  
> **Purpose:** Understand unit testing from first principles, using this codebase as the working example throughout.

---

## 1. What Is a Unit Test, Actually?

A **unit test** is a short, self-contained program that calls one small piece of your code with a specific input and checks that the output is what you expected.

That's it. There is no mystery. The word "unit" just means a small, isolated piece — typically a single function, method, or class.

Here is the smallest possible example:

```csharp
[Fact]
public void TwoAndTwo_IsFour()
{
    int result = 2 + 2;
    Assert.Equal(4, result);
}
```

The `[Fact]` attribute tells xUnit "this method is a test." The `Assert.Equal` call either passes silently or throws with a useful message if the values don't match. The test runner finds every `[Fact]`-marked method in the project, runs them all, and reports which ones passed and which ones failed.

The value of tests like these is not testing `2 + 2`. The value is that when you write one for *your* code and it passes, you have a permanent, machine-executable proof that your code did the right thing at that moment in time. When it later fails because someone changed the code, you find out immediately — not six weeks later when a user reports a bug.

---

## 2. The Three A's — Arrange, Act, Assert

Every well-written test follows this structure:

```
Arrange  — set up the inputs and preconditions
Act      — call the code under test
Assert   — check that the result is what you expected
```

Here is a real test from this codebase, annotated:

```csharp
[Fact]
public void AwakeEntity_DrainsSatiation_ByRate_Times_DeltaTime()
{
    // ARRANGE: build an entity with known starting values
    var (em, entity) = BuildWithMetab(satiation: 80f, satiationDrain: 2f);

    // ACT: run the system for 3 game-seconds
    Sys.Update(em, deltaTime: 3f);

    // ASSERT: 80 - (2 * 1.0 * 3) = 74
    Assert.Equal(74f, entity.Get<MetabolismComponent>().Satiation, precision: 3);
}
```

The structure is visible just from reading it. Arrange sets up one entity with 80 satiation and a drain rate of 2. Act runs the metabolism system. Assert checks the arithmetic. If the system ever produces a different number — because someone changed the drain formula, or introduced a multiplier they forgot was there — this test fails with:

```
Expected: 74
Actual:   ???
```

---

## 3. Why ECS Systems Are Perfect for Testing

Most codebases have code that is hard to test: code that reads from a database, calls a web API, writes to a file, depends on the current time, or spawns threads. Testing that code requires setting up fake servers, mock databases, and time-injection frameworks.

ECS systems have none of those problems. Look at the signature every system in this engine implements:

```csharp
public void Update(EntityManager em, float deltaTime)
```

A system takes in an `EntityManager` (pure in-memory data) and a `float` (a number). It reads components, writes components, and returns. No file I/O. No network. No randomness. No shared state between ticks.

This is as close to a **pure function** as object-oriented code gets. Pure functions are trivially testable:

1. Create an `EntityManager` with known state.
2. Call `Update`.
3. Read the components back out and check them.

The test setup in `MetabolismSystemTests.cs` is three lines:

```csharp
var (em, entity) = BuildWithMetab(satiation: 80f, satiationDrain: 2f);
Sys.Update(em, deltaTime: 3f);
Assert.Equal(74f, entity.Get<MetabolismComponent>().Satiation, precision: 3);
```

You can read and understand this test in under five seconds. That clarity is the goal.

---

## 4. What Makes a Test Good vs Useless

Not all tests are valuable. Here is how to tell the difference.

### A good test:

**Tests one specific behaviour.** The name `AwakeEntity_DrainsSatiation_ByRate_Times_DeltaTime` tells you exactly what the test checks. If it fails, you know exactly what broke.

**Fails for exactly one reason.** If the test fails, it is because the specific behaviour it describes broke — not because some unrelated helper method changed.

**Is readable without context.** A new engineer should be able to read the test, understand what it tests, and understand why it failed — without reading any other file.

**Uses explicit values, not magic numbers.** Comparing against `74f` is better than comparing against `expected` where `expected` was computed with the same formula as the production code. If both are wrong in the same way, the test passes and lies to you.

### A useless test:

**Tests implementation details instead of behaviour.** A test that asserts `_components.Count == 1` is testing how the internals of `Entity` are structured, not what it does. Refactoring internals will break it even if behaviour is unchanged.

**Has complex setup that's hard to follow.** If the Arrange section is 40 lines long, the test is probably testing too many things at once.

**Always passes regardless of whether the code is correct.** This is the most dangerous kind. The test `Assert.True(true)` will never fail but tells you nothing.

---

## 5. The Test File Structure in This Project

```
APIFramework.Tests/
├── Core/
│   ├── EntityTests.cs           ← Entity: Add, Has, Get, Remove, onChange
│   └── EntityManagerTests.cs   ← EntityManager: Query, Create, Destroy, index
├── Systems/
│   ├── MetabolismSystemTests.cs         ← drain rates, sleep/anger multipliers
│   ├── EnergySystemTests.cs             ← awake/sleep cycling, state tags
│   ├── BiologicalConditionSystemTests.cs ← tag thresholds
│   ├── DigestionSystemTests.cs          ← pipeline contract, nutrient release
│   ├── BrainSystemTests.cs              ← drive scoring, mood modifiers
│   └── FeedingDrinkingSystemTests.cs    ← guard conditions, spawn behaviour
└── Integration/
    └── SmokeTests.cs            ← full 24h run, invariant assertions
```

**Core tests** check the ECS machinery itself. If `Query<T>()` returns the wrong entity or `Add<T>()` doesn't fire the onChange callback, everything above it breaks — so we test these thoroughly first.

**System tests** check each system in isolation. Each test creates only the components that system reads; everything else is absent. The goal is to verify that the system's contract ("if you give me this input, I will produce this output") holds exactly.

**Integration tests** run the whole simulation end-to-end. They are intentionally less precise — checking "nutrient stores accumulated something" rather than exact values. Their job is to catch failures in the *handoffs* between systems: does food actually move from FeedingSystem through EsophagusSystem through DigestionSystem and end up in MetabolismComponent? The unit tests above verify each step; the integration test verifies the whole chain.

---

## 6. Anatomy of a Specific Test: The Regression Case

The most important test in the entire suite is this one:

```csharp
[Fact]
public void ZeroNutrients_StomachEmpties_ButNoSatiationOrHydration()
{
    var (em, entity) = Build(
        volumeMl:   100f,
        digestionRate: 10f,
        nutrients:  new NutrientProfile(), // ← all zeros
        satiation:  50f,
        hydration:  30f);

    Sys.Update(em, deltaTime: 1f);

    Assert.Equal(90f, entity.Get<StomachComponent>().CurrentVolumeMl, precision: 2);
    Assert.Equal(50f, entity.Get<MetabolismComponent>().Satiation);
    Assert.Equal(30f, entity.Get<MetabolismComponent>().Hydration);
}
```

This test describes the **exact bug that was found in the v0.7.2 simulation run**. When `NutrientProfile` fields were not deserialized from JSON (because `IncludeFields = true` was missing), every nutrient value was zero. The stomach filled with volume but the entity was not fed. Billy starved.

The test above pins that bug scenario. It says: "if someone passes a zero-nutrient profile, the stomach still drains volume (correct), but Satiation and Hydration do not change (also correct — there was nothing nutritious to absorb)."

The companion test, `CorrectNutrients_ProduceSatiation_AndHydration`, pins the passing case — a real banana profile produces real satiation.

Together these two tests form a **regression guard**: if someone accidentally reverts the `IncludeFields = true` fix or introduces a zero-initialization somewhere in the pipeline, one of these tests immediately fails and tells them exactly why.

This is the core purpose of regression tests. They are written *after* you fix a bug to ensure that bug cannot silently return.

---

## 7. What Assert Methods Are Available?

xUnit provides a rich set of assertion helpers. The most common:

| Method | What it checks |
|:---|:---|
| `Assert.Equal(expected, actual)` | Values are equal |
| `Assert.Equal(expected, actual, precision: 3)` | Floats equal to 3 decimal places |
| `Assert.True(condition)` | Condition is true |
| `Assert.False(condition)` | Condition is false |
| `Assert.Null(value)` | Value is null |
| `Assert.NotNull(value)` | Value is not null |
| `Assert.Empty(collection)` | Collection has zero items |
| `Assert.Single(collection)` | Collection has exactly one item |
| `Assert.Contains(item, collection)` | Collection contains item |
| `Assert.DoesNotContain(item, collection)` | Collection does not contain item |
| `Assert.InRange(value, low, high)` | Value is between low and high (inclusive) |
| `Assert.Throws<T>(action)` | Action throws exception of type T |
| `Record.Exception(action)` | Runs action, returns exception (or null if no exception) |

The float precision variant is essential when comparing floating-point arithmetic. Direct equality (`Assert.Equal(74f, result)`) can fail due to floating-point rounding where values like `73.999997` don't compare equal to `74.0`. Using `precision: 3` means "correct to 3 decimal places."

---

## 8. Private Test Helper Methods

Every test file in this project has a helper like `BuildWithMetab` or `Build`. These are not tests themselves — they are factory methods that reduce repetition in the Arrange step.

```csharp
private static (EntityManager em, Entity entity) BuildWithMetab(
    float satiation      = 80f,
    float satiationDrain = 1f,
    ...)
{
    var em     = new EntityManager();
    var entity = em.CreateEntity();
    entity.Add(new MetabolismComponent
    {
        Satiation          = satiation,
        SatiationDrainRate = satiationDrain,
        ...
    });
    return (em, entity);
}
```

Without this helper, every single test would need to manually construct `EntityManager`, call `CreateEntity`, and `Add` every required component. That's four to eight lines of setup per test, which quickly obscures what each test is actually about.

The key design principle: **helpers set up data; they do not make assertions.** Assertions always live in the `[Fact]` method, never in a helper. If a helper asserted something, a failing helper would produce a confusing error message that doesn't point to the test that triggered it.

---

## 9. Running the Tests

From a terminal in the solution root:

```bash
dotnet test
```

To run only a specific test file:

```bash
dotnet test --filter "FullyQualifiedName~MetabolismSystemTests"
```

To run only a specific test:

```bash
dotnet test --filter "FullyQualifiedName~AwakeEntity_DrainsSatiation"
```

To see output from tests even when they pass (useful for debugging):

```bash
dotnet test --logger "console;verbosity=detailed"
```

In Visual Studio or Rider, the Test Explorer window discovers and runs all tests. Click any test to run just that one, or right-click a folder to run all tests in it.

---

## 10. What to Test (and What to Skip)

**Test:**
- Every non-trivial formula (drain rates, multiplier interactions, ratios)
- Every guard condition (the "if this isn't true, skip" gates in action systems)
- Every callback or event that changes external state (onChange fires, tags set/cleared)
- Every boundary case (zero, max value, exactly at threshold)
- Every bug you have ever fixed (regression tests)

**Skip:**
- Trivial getters and setters that are literally one line with no logic
- `ToString()` overrides
- Log/debug output
- Code that would require a real database, network, or filesystem just to set up (integration-test that instead)

The rule of thumb: if a method contains a conditional (`if`, `switch`, `?:`), it probably deserves a test for each branch. If a method is just `return _field`, you can skip it.

---

## 11. How Tests Protect Future Work

As of v0.7.2, you are about to add intestines — SmallIntestineComponent, LargeIntestineComponent, and the systems that move chyme through them. Here is what the current tests protect:

When you modify `DigestionSystem` to hand off partially-digested chyme to `SmallIntestineSystem` instead of depositing directly into `MetabolismComponent`, the existing `DigestionSystemTests.cs` will immediately tell you if you accidentally broke the stomach-drain logic, the ratio-proportional nutrient release, or the Satiation/Hydration conversion factors. You will not need to run the full simulation to discover a broken pipeline — the test suite tells you within two seconds of hitting save.

When you add the new intestine systems, you write their tests first (or alongside the code). Those tests then form the permanent specification for how the intestine pipeline must behave. Future contributors — or future you, six months from now — can change the implementation freely as long as the tests still pass.

This is the full value proposition of a test suite: **it makes large refactors safe**. Without tests, changing a core system like `EntityManager` or `DigestionSystem` means carefully re-reading every system that uses it and hoping you caught everything. With tests, you change the code, run `dotnet test`, and the suite either confirms everything still works or pinpoints exactly what broke.

---

## 12. The Test Pyramid

A useful mental model for how many tests of each kind to write:

```
         /\
        /  \
       / E2E\       ← End-to-end (fewest, slowest, most fragile)
      /──────\
     / Integr \     ← Integration (some, moderate speed)
    /──────────\
   / Unit Tests \   ← Unit (most, fast, precise)
  /──────────────\
```

This project follows the pyramid:

- **Unit tests (most):** 80+ tests in `Core/` and `Systems/`, each running in under 1ms, testing exactly one thing.
- **Integration tests (some):** `SmokeTests.cs`, running a full sim day, checking the pipeline handoffs.
- **End-to-end tests (zero, for now):** Would require the CLI or GUI to run. Not needed at this scale.

The pyramid is upside-down in many legacy codebases — lots of slow, brittle integration tests and almost no unit tests. Flipping it is one of the most valuable refactors a codebase can undergo.

---

*End of UNIT-TESTING.md — v0.7.2*
