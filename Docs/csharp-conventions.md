# C# conventions — Breakwater

The house style for all C# in this repository. `.editorconfig` and `Directory.Build.props`
enforce the mechanical rules (braces, namespaces, collection expressions, unused usings, zero
warnings), so a slip fails the build. The rest are for review.

## 1. File-scoped namespaces, one type per file

```csharp
namespace Breakwater.Core;

public sealed class CaptureSite(string name, Vector2 position)
{
    // ...
}
```

Large classes are split into partials by concern, named `Type.Concern.cs`, for example
`Simulation.Combat.cs`, `Simulation.Ai.cs` and `CommandHud.Modal.cs`.

## 2. Usings live in `Usings.cs`

Each project has a `Usings.cs` of `global using` directives, and `ImplicitUsings` is on, so most
files have no using block at all. Where a file does need its own, it goes *below* the namespace.

The one exception is `System.Numerics`: its `Vector2` collides with `Godot.Vector2`. Core files
import it below their namespace, which takes precedence over the global Godot import. Godot-side
files alias it instead:

```csharp
namespace Breakwater;

using V2 = System.Numerics.Vector2;
```

## 3. Primary constructors

Use a primary constructor for classes that take collaborators or simple state, and use the
parameter directly rather than copying it into a field:

```csharp
public sealed class Navigation(IReadOnlyList<Obstacle> obstacles)
{
    public IReadOnlyList<Obstacle> Obstacles { get; } = obstacles;
}
```

Godot nodes are the exception. The engine creates them through a parameterless constructor, so
they take collaborators through a `Start(...)` method or settable properties (see `QaDriver`).

## 4. Static when there's no instance state

A class whose members are all pure functions of their arguments is `static` (`Geometry`,
`Mission`, `SaveGame`).

## 5. Collection expressions for initialisers

```csharp
public List<Vector2> Orders { get; set; } = [];
private static readonly float[] RoadNorthing = [-37, -20, -4];
```

Don't use them to end a query: `.Where(...).ToList()` reads better than `[.. query]`.

## 6. Properties in alphabetical order, where practical

Declare properties and fill object initialisers alphabetically so a member is easy to find. The
identity comes first (`Id`), and a group of members that shares one explanatory comment stays
together.

## 7. Always brace; one statement per line

```csharp
if (tank.Route.Count == 0)
{
    tank.Speed = 0;
}
```

Never `if (x) y;` and never two statements on one line.

## 8. Nullable reference types are on

Express intent with `?` and handle the null. The null-forgiving `!` is reserved for Godot node
fields assigned in `_Ready` (`private Label clock = null!;`), and the field says so in a comment.

## 9. No `async void`

To act after a frame has drawn, connect a one-shot signal rather than awaiting in an `async void`
method:

```csharp
RenderingServer.Singleton.Connect(RenderingServer.SignalName.FramePostDraw,
    Callable.From(SaveScreenshot), (uint)ConnectFlags.OneShot);
```

## 10. Exceptions: narrow, and never swallowed

Catch only what you can handle, with an exception filter, and tell the player what happened:

```csharp
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException or JsonException)
{
    hud.Notify("Load unavailable: " + ex.Message);
}
```

Rethrow with `throw;`, never `throw ex;`.

## 11. Logging

Use `GD.Print` for information and `GD.PushError` for failures, so both show up in Godot's
output and in the headless check runs.

## 12. No magic numbers

A number whose meaning isn't obvious is a named constant with a comment, on the type that owns
it: `CaptureSite.HoldSeconds`, `Mission.Edge`, `Vehicle.MaxOrders`, `Shell.Speed`, and tuning
constants at the top of each `Simulation` partial. Build player-facing text from those constants
so that it can't drift out of date:

```csharp
$"Hold each station for {CaptureSite.HoldSeconds:0} seconds."
```

Prefer a framework constant to redeclaring one (`byte.MaxValue`, `MathF.Tau`,
`TimeSpan.FromDays(...)`). Colours live in `Palette`. UI layout pixel sizes may stay inline next
to the control they size.

## 13. No stringly-typed values

Use enums for closed sets: `CombatEventKind`, `ObstacleKind`, `Loadout`, `VehicleStatus`,
`DeployAction`. Key-binding ids are persisted in `settings.json` as strings, so they stay strings,
but code refers to them only through the `CommandIds` constants.

## 14. Saves go through DTOs

`SaveGame` serialises private DTOs, never live simulation objects. To persist new state, add it to
the DTO and its validation, keep older save versions loading, and add a test in `SaveGameTests`.

## 15. Test names read as a sentence

xUnit v3, `snake_case`, first letter capitalised, describing the behaviour:

```csharp
[Fact]
public void Capture_is_contested_reversible_and_leads_to_victory()
```
