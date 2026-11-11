namespace Breakwater.Core;

/// <summary>Classic-mode ground types. See <see cref="TerrainMap"/> for how each affects speed and cover.</summary>
public enum Terrain
{
    Open,
    Road,
    Track,

    /// <summary>A road or track where it runs through jungle.</summary>
    JungleTrack,

    /// <summary>The outer band of a jungle: the best firing position on the island.</summary>
    JungleEdge,

    JungleFringe,
    DeepJungle,
    River,
}
