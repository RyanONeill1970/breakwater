namespace Breakwater;

/// <summary>
/// Every colour the game draws with, in one place, so a visual restyle is a one-file change.
/// Team colours are shared by the 3D view, the HUD overlays and the minimap.
/// </summary>
public static class Palette
{
    /// <summary>The harbour art study: warm limestone, olive foliage and cool water.</summary>
    public static class Art
    {
        public static readonly Color Sand = new("c6b58e");
        public static readonly Color Grass = new("4f542d");
        public static readonly Color Earth = new("827252");
        public static readonly Color Road = new("655f51");
        public static readonly Color Markings = new("a99f7c");
        public static readonly Color Sea = new("123b4a");
        public static readonly Color Lagoon = new("2a8c80");
        public static readonly Color Rock = new("6e6b5e");
        public static readonly Color ScanTint = new("b2b9b0");
        public static readonly Color Armour = new("777965");
        public static readonly Color Foam = new("d5e3d5");
        public static readonly Color Sun = new("ffe3b1");
        public static readonly Color Haze = new("8aa6ab");
        public static readonly Color SkyFill = new("91a8bf");
        public static readonly Color Leaf = new("4a5624");
        public static readonly Color LeafTip = new("788443");
        public static readonly Color Bark = new("77705b");
        public static readonly Color Wall = new("c9c1a9");
        public static readonly Color Concrete = new("969383");
        public static readonly Color Coping = new("b2ac91");
        public static readonly Color Crane = new("916946");
        public static readonly Color Rubber = new("22292a");
        public static readonly Color Grime = new("333d31");
        public static readonly Color Roof = new("626c67");
        public static readonly Color Metal = new("424c4a");
        public static readonly Color Glass = new("243b3f");
        public static readonly Color Timber = new("74664d");
        public static readonly Color Dust = new(.56f, .5f, .38f, .16f);
        public static readonly Color Ink = new("eef0dc");
        public static readonly Color Caption = new("bdd0c7");
        public static readonly Color Panel = new(.035f, .07f, .075f, .88f);

        public static Color Ground(Terrain terrain) => terrain switch
        {
            Terrain.Road => Road,
            Terrain.Track => Earth.Lightened(.12f),
            Terrain.JungleTrack => Earth,
            Terrain.JungleEdge => Grass,
            Terrain.JungleFringe => Grass.Darkened(.12f),
            Terrain.DeepJungle => Grass.Darkened(.22f),
            Terrain.River => Lagoon.Darkened(.15f),
            _ => Grass,
        };
    }

    public static readonly Color[] Groups = [new("72dfc3"), new("7bbfff"), new("efce84"), new("c4a3ec")];
    public static readonly Color Enemy = new("ed9679");

    /// <summary>Classic escort battles' supply trucks.</summary>
    public static readonly Color Convoy = new("f2f0e6");

    /// <summary>The colour that marks a vehicle's side: enemy, convoy, or its group.</summary>
    public static Color Team(Vehicle vehicle) => vehicle.Enemy ? Enemy : vehicle.Convoy ? Convoy : Groups[vehicle.Group];
    public static readonly Color Ghost = new("d2aa86");
    public static readonly Color Fog = new(.025f, .065f, .08f);

    /// <summary>Fog alpha over ground never seen, and over ground seen before but not now.</summary>
    public const float FogUnexplored = .66f, FogExplored = .3f, MapFogUnexplored = .72f;

    public static class Ui
    {
        // The interface is dressed as vehicle equipment: olive-drab plates, stencilled amber legends,
        // and green phosphor readouts in recessed display windows.
        public static readonly Color Ink = new("e6e2cf");
        public static readonly Color Muted = new("969c86");
        public static readonly Color Accent = new("8fe0a0");

        /// <summary>Stencilled panel headings and the brackets on panel corners.</summary>
        public static readonly Color Legend = new("d8b25c");
        public static readonly Color Brackets = Legend with { A = .5f };

        /// <summary>Live readouts: the clock, the view mode and the gunner sight's figures.</summary>
        public static readonly Color Readout = new("9dffb8");

        // Plates: shaded from the top colour to the bottom one.
        public static readonly Color PanelTop = new(.125f, .145f, .115f, .96f);
        public static readonly Color PanelBottom = new(.075f, .09f, .07f, .96f);
        public static readonly Color PanelBorder = new("3d4637");
        public static readonly Color ButtonTop = new("39422f");
        public static readonly Color ButtonBottom = new("262d21");
        public static readonly Color ButtonBorder = new("515b46");
        public static readonly Color ButtonHoverTop = new("46513a");
        public static readonly Color ButtonHoverBottom = new("2f3829");
        public static readonly Color ButtonPressedTop = new("171c14");
        public static readonly Color ButtonPressedBottom = new("222a1e");

        /// <summary>A recessed display window, behind readouts and gauges.</summary>
        public static readonly Color Display = new("0b0f0b");

        public static readonly Color BevelLight = new(1, 1, .9f, .1f);
        public static readonly Color BevelDark = new(0, 0, 0, .5f);
        public static readonly Color Screw = new("5b6250");
        public static readonly Color Track = Display;
        public static readonly Color HealthTrack = new(.04f, .05f, .04f, .85f);
        public static readonly Color HitRing = new(1, .38f, .25f);
        public static readonly Color Ricochet = new("b9d3dc");
        public static readonly Color FlankHit = new("ffc766");
        public static readonly Color RearHit = new("ff7a52");
        public static readonly Color KnockedOut = new("ff5a3c");

        /// <summary>Enemy counter-attacks, landings and lost sites: callouts, labels and minimap pings.</summary>
        public static readonly Color Alert = new("ff6a4a");
        public static readonly Color CalloutOutline = new(.03f, .04f, .03f);
        public static readonly Color ModalShade = new(.02f, .03f, .02f, .82f);
        public static readonly Color SightReticle = new("9dffc9");
        public static readonly Color SightShade = new(.01f, .02f, .02f, .93f);

        public static readonly Color SettingsShade = new(.02f, .03f, .02f, .94f);
    }

    public static class Map
    {
        public static readonly Color Sea = new("245460");
        public static readonly Color Land = new("566e59");
        public static readonly Color Grid = new(1, 1, 1, .08f);
        public static readonly Color Obstacle = new("b1b398");
        public static readonly Color Site = new("d8c990");
        public static readonly Color Wreck = new("253635");

        // Classic zone map: zones won, and zones the enemy still holds.
        public static readonly Color HeldZone = new("2f6f9a");
        public static readonly Color EnemyZone = new("8a3d33");

        /// <summary>Classic ground on the minimap: the world colour, a little brighter to read at small size.</summary>
        public static Color Ground(Terrain terrain) => World.Ground(terrain).Lightened(.12f);
    }

    /// <summary>Classic night battles, and the thermal sight that sees through them.</summary>
    public static class Night
    {
        public static readonly Color Haze = new("1f2c3d");
        public static readonly Color Moon = new("b8c8ff");
        public static readonly Color ThermalCold = new("031006");
        public static readonly Color ThermalHot = new("b8ffb0");
    }

    public static class World
    {
        public static readonly Color Haze = new("b9d5d6");
        public static readonly Color Sun = new("fff1d6");
        public static readonly Color SeaDeep = new("1a4d66");
        public static readonly Color SeaShallow = new("3b8a8a");
        public static readonly Color Foam = new("eef6f0");
        public static readonly Color Sand = new("cfbd88");
        public static readonly Color[] Grass = [new("798966"), new("8b8b72"), new("6b826b")];

        /// <summary>Each island's turf under the photographed grass texture: lush, dry, and damp.</summary>
        public static readonly Color[] Turf = [new("5a763a"), new("75773f"), new("4d6c3e")];

        public static readonly Color Road = new("b1aa87");
        public static readonly Color SupplyLine = new("91bba0");
        public static readonly Color PalmTrunk = new("796948");
        public static readonly Color PalmLeaf = new("426d51");

        /// <summary>Old palm fronds, yellowing before they fall.</summary>
        public static readonly Color PalmLeafOld = new("8a8a4a");
        public static readonly Color Coconut = new("5d5230");

        /// <summary>Ridge rock: the photographed stone, turned from red sandstone to weathered grey.</summary>
        public static readonly Color Rock = new("7e796b");
        public static readonly Color Bark = new("5a4b3b");
        public static readonly Color Building = new("b4ad92");
        public static readonly Color Roof = new("596e71");

        // Buildings: rendered walls in a few shades, concrete trim and roofs, glass and wooden shutters.
        public static readonly Color[] BuildingWalls = [new("d2c6a4"), new("c2bcaa"), new("ddd6c4"), new("bba98b")];
        public static readonly Color BuildingTrim = new("8e8876");
        public static readonly Color RoofSlab = new("7d7e76");
        public static readonly Color RoofPlant = new("a2a49d");
        public static readonly Color Glass = new("1e2a31");
        public static readonly Color Shutter = new("5d6f5a");
        public static readonly Color Door = new("4b3a2c");
        public static readonly Color WindowLamp = new("ffc978");

        public static readonly Color SiteRing = new("d8c990");
        public static readonly Color FlagPole = new("d0d5c7");
        public static readonly Color Flag = new("e08c6d");
        public static readonly Color CaptureBand = new(.45f, .87f, .76f, .85f);
        public static readonly Color ContestedBand = new(.93f, .59f, .47f, .9f);
        public static readonly Color DepotCrate = new("576e64");
        public static readonly Color DepotDrum = new("9a9874");
        public static readonly Color EnemyHull = new("a17a62");
        public static readonly Color ScoutHull = new("73866c");
        public static readonly Color TankHull = new("536e5b");
        public static readonly Color Track = new("2a3634");
        public static readonly Color Wheel = new("606c59");
        public static readonly Color Optics = new("36453e");
        public static readonly Color Antenna = new("263d38");
        public static readonly Color Wreck = new("343d38");

        // Vehicle dressing: mud and dust, the stowage basket's kit, and a truck's canvas tilt.
        public static readonly Color Mud = new("6e5f45");
        public static readonly Color Stowage = new("5b5a44");
        public static readonly Color Canvas = new("6d6a4c");
        public static readonly Color Shell = new("ffe7a3");

        // Classic ground and jungle.
        public static readonly Color DirtTrack = new("a08d66");
        public static readonly Color JungleFloor = new("3f5a3c");
        public static readonly Color DeepJungleFloor = new("2d4630");
        public static readonly Color River = new("3f7f86");
        public static readonly Color[] Canopy = [new("35603d"), new("2f5634"), new("3e6b40"), new("466f45")];
        public static readonly Color CarrierHull = new("64785f");

        // Classic support weapons.
        public static readonly Color SmokeScreen = new(.78f, .8f, .78f, .8f);
        public static readonly Color Mine = new("d9b44a");
        public static readonly Color FireMission = new("ffb35c");

        public static Color Ground(Terrain terrain) => terrain switch
        {
            Terrain.Road => Road,
            Terrain.Track => DirtTrack,
            Terrain.JungleTrack => DirtTrack.Darkened(.25f),
            Terrain.JungleEdge => JungleFloor.Lightened(.12f),
            Terrain.JungleFringe => JungleFloor,
            Terrain.DeepJungle => DeepJungleFloor,
            Terrain.River => River,
            _ => Grass[0],
        };

        // Combat effects.
        public static readonly Color FlashCore = new("fff6d8");
        public static readonly Color Flash = new("ffd19a");
        public static readonly Color Fire = new("ffa23a");
        public static readonly Color FireDeep = new("c9401c");
        public static readonly Color Spark = new("ffd66b");
        public static readonly Color SmokeDark = new(.09f, .1f, .1f, .82f);
        public static readonly Color SmokeLight = new(.55f, .57f, .55f, .5f);
        public static readonly Color Dust = new(.78f, .7f, .52f, .7f);
        public static readonly Color Shockwave = new(1, .88f, .7f, .8f);
        public static readonly Color Debris = new("24292a");
        public static readonly Color Scorch = new(.06f, .06f, .05f, .6f);
    }
}
