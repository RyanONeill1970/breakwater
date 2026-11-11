namespace Breakwater.Core;

/// <summary>
/// Extra terms a Classic battle must be won on; breaking one fails the mission. Each is optional:
/// the most vehicles that may be lost, the time allowed, and whether civilian buildings must be spared.
/// </summary>
public sealed record BattleConditions(int? MaxLosses = null, float? TimeLimitSeconds = null, bool SpareCivilians = false);
