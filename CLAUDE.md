# Breakwater: working notes

Godot 4.7.2 .NET / .NET 10 / C# prototype. Read `README.md` for how to play and verify, and
`Docs/DESIGN.md` for the mechanics.

## Layout

- `Scripts/Core`: the engine-independent simulation. It has no Godot references, so it can be
  unit-tested; keep it that way.
- `Scripts`: Godot presentation, input and UI (namespace `Breakwater`).
- `Tests`: xUnit v3. It compiles `Scripts/Core/*.cs` directly, so it needs no Godot runtime.

## Verify

```powershell
dotnet build Breakwater.csproj
dotnet test --project Tests/Breakwater.Tests.csproj
./Tools/Check.ps1 -Godot '<path to Godot 4.7.2 .NET console exe>'   # adds the in-engine checks
```

The build treats warnings as errors and enforces `.editorconfig`, so a style slip fails the build.

## C# conventions

The full rules, with examples, are in [`Docs/csharp-conventions.md`](Docs/csharp-conventions.md).
In short:

1. File-scoped namespaces, one type per file (partials split by concern: `Simulation.Combat.cs`).
2. Usings go in `Usings.cs` as global usings. `System.Numerics` is the exception: it collides with
   `Godot.Vector2`, so Core files import it below their namespace, and Godot files alias it as `V2`.
3. Primary constructors for plain classes. Godot nodes can't use them, because the engine needs a
   parameterless constructor, so they take collaborators through a `Start(...)` method or properties.
4. `static` when there's no instance state.
5. Collection expressions (`[]`) for initialisers; keep `.ToList()`/`.ToArray()` for queries.
6. Properties in alphabetical order where practical, with the id first.
7. Always brace control-flow bodies. One statement per line.
8. Nullable is on. `= null!` is only for Godot node fields assigned in `_Ready`, and says so in a comment.
9. Never `async void`. For "after this frame draws", connect a one-shot signal.
10. Catch the narrowest exceptions and surface them to the player; never swallow them.
11. No magic numbers. Gameplay values are named constants on the type that owns them, and UI
    text is built from those constants. Colours live in `Palette`. UI layout pixel sizes may stay
    inline next to the control they size.
12. No stringly-typed values: use enums (`CombatEventKind`, `ObstacleKind`, `Loadout`, ...) and
    `CommandIds` for key-binding ids, which are persisted strings.
13. Test names are sentences: `Capture_is_contested_reversible_and_leads_to_victory`.
14. Log with `GD.Print` for information and `GD.PushError` for failures.

## Saves

`SaveGame` writes private DTOs, not live objects. When adding persistent state, add it to the DTO and
its validation, keep older versions loading, and add a test in `SaveGameTests`.
