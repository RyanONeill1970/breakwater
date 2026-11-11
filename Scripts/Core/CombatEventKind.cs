namespace Breakwater.Core;

public enum CombatEventKind
{
    /// <summary>A movement or regroup order was acknowledged.</summary>
    Order,

    /// <summary>A capture site became secured.</summary>
    Capture,

    /// <summary>A new enemy contact was reported.</summary>
    Contact,

    Shot,
    Impact,
    Destroy,

    /// <summary>The enemy command sent defenders to relieve or retake a site (an intercepted radio call).</summary>
    Assault,

    /// <summary>Enemy reinforcements landed on the north shore.</summary>
    Reinforcement,

    /// <summary>A secured site came back under enemy pressure and is no longer held.</summary>
    SiteLost,

    /// <summary>Classic: a vehicle fired smoke, or a smoke shell burst.</summary>
    Smoke,

    /// <summary>Classic: the player called an artillery mission.</summary>
    ArtilleryCalled,

    /// <summary>Classic: an enemy artillery mission is on its way (an intercepted warning).</summary>
    IncomingArtillery,

    /// <summary>Classic: an artillery round landed.</summary>
    Barrage,

    /// <summary>Classic: a mine went off.</summary>
    MineBlast,

    /// <summary>Classic: a building was knocked down.</summary>
    BuildingDestroyed,
}
