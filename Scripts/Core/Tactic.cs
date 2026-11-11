namespace Breakwater.Core;

/// <summary>What a defender's AI is currently trying to do; see <see cref="Simulation"/>'s AI partial.</summary>
public enum Tactic
{
    Guard,
    Patrol,
    Engage,
    Flank,
    Investigate,
    Retreat,

    /// <summary>Sent by the enemy command to relieve or retake a capture site.</summary>
    Assault,
}
