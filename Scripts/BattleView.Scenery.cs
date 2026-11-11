namespace Breakwater;

/// <summary>
/// Buildings retain their destructible roots, facade details and roof plant. Scanned rock formations
/// sit on low connecting terrain, fitted to the ridges' simulation footprints and heights.
/// </summary>
public partial class BattleView
{
    private const string FacadeShaderPath = "res://Shaders/Facade.gdshader";
    private const string RockShaderPath = "res://Shaders/ArtRock.gdshader";

    // Buildings: storey height and the widest bay (one window each), in metres, shared with the facade shader.
    private const float Storey = 2.4f, Bay = 3;

    // Fractions of windows shuttered, and lit at night.
    private const float Shuttered = .2f, NightLit = .35f;

    // Building trim in metres: the plinth round the foot, the parapet round the roof, and the door.
    private const float PlinthHeight = .45f, PlinthOverhang = .15f;
    private const float ParapetHeight = .55f, ParapetThickness = .25f, CorniceHeight = .16f, CorniceOverhang = .18f;
    private const float DoorHeight = 2.2f, DoorDepth = .12f, DoorCanopyDepth = .9f;

    /// <summary>A door is this fraction of its bay's width, enough to cover the ground-floor window behind it.</summary>
    private const float DoorWidth = .52f;

    /// <summary>Roof plant: air-conditioning units per building, at most, and the smallest roof that takes a water tank or stair hut.</summary>
    private const int MaxRoofUnits = 3;
    private const float RoofForPlant = 40;
    private const int BuildingSeed = 23;

    // Ridges: heightfield spacing, how far in from the foot the slope reaches full height, and how much
    // noise roughens the top, all in metres except the roughness, a fraction of the height.
    private const float RidgeStep = 1, RidgeSlope = 3.5f, RidgeRoughness = .4f;
    private const float RidgeNoiseFrequency = .12f;

    private void BuildObstacle(Obstacle obstacle)
    {
        var node = new Node3D();
        AddChild(node);
        obstacles[obstacle] = node;
        if (obstacle.Kind == ObstacleKind.Building)
        {
            BuildBuilding(node, obstacle);
        }
        else
        {
            BuildRidge(node, obstacle);
        }
    }

    private void BuildBuilding(Node3D node, Obstacle obstacle)
    {
        var random = new Random(BuildingSeed + (int)obstacle.Center.X * 31 + (int)obstacle.Center.Y);
        var foot = World(obstacle.Center);
        var size = new Vector3(obstacle.HalfSize.X * 2, obstacle.Height, obstacle.HalfSize.Y * 2);
        var wall = Palette.Art.Wall.Darkened(random.NextSingle() * .12f);

        node.AddChild(new MeshInstance3D
        {
            MaterialOverride = Facade(obstacle, wall, random.NextSingle()),
            Mesh = new BoxMesh { Size = size },
            Position = foot + Vector3.Up * (size.Y / 2),
        });

        var trim = Palette.Art.Coping;
        Box(node, foot + Vector3.Up * (PlinthHeight / 2), new(size.X + PlinthOverhang * 2, PlinthHeight, size.Z + PlinthOverhang * 2), trim.Darkened(.25f));
        Box(node, foot + Vector3.Up * (size.Y - CorniceHeight / 2), new(size.X + CorniceOverhang * 2, CorniceHeight, size.Z + CorniceOverhang * 2), trim);

        // The parapet: a low wall round the edge of the flat roof.
        float parapetY = size.Y + ParapetHeight / 2;
        foreach (int side in (int[])[-1, 1])
        {
            Box(node, foot + new Vector3(0, parapetY, side * (size.Z - ParapetThickness) / 2), new(size.X, ParapetHeight, ParapetThickness), wall.Darkened(.08f));
            Box(node, foot + new Vector3(side * (size.X - ParapetThickness) / 2, parapetY, 0), new(ParapetThickness, ParapetHeight, size.Z), wall.Darkened(.08f));
        }

        // A door in the middle bay of the south face, with a small canopy over it.
        int bays = Math.Max(1, (int)(size.X / Bay));
        float bayWidth = size.X / bays;
        float doorX = -size.X / 2 + (bays / 2 + .5f) * bayWidth;
        Box(node, foot + new Vector3(doorX, DoorHeight / 2, size.Z / 2 + DoorDepth / 2), new(bayWidth * DoorWidth, DoorHeight, DoorDepth), Palette.Art.Timber);
        Box(node, foot + new Vector3(doorX, DoorHeight + CorniceHeight, size.Z / 2 + DoorCanopyDepth / 2), new(bayWidth * DoorWidth * 1.5f, CorniceHeight, DoorCanopyDepth), trim);

        // Roof plant: air-conditioning units, and on a big enough roof a water tank or a stair hut.
        var roof = foot + Vector3.Up * size.Y;
        var room = new Vector2(size.X, size.Z) / 2 - Vector2.One * (ParapetThickness + 1);
        int units = 1 + random.Next(MaxRoofUnits);
        for (int i = 0; i < units && room.X > 0 && room.Y > 0; i++)
        {
            var at = new Vector3((random.NextSingle() * 2 - 1) * room.X, .35f, (random.NextSingle() * 2 - 1) * room.Y);
            Box(node, roof + at, new(1, .7f, .8f), Palette.Art.Metal);
        }

        if (size.X * size.Z >= RoofForPlant)
        {
            var corner = roof + new Vector3(-room.X * .6f, 0, -room.Y * .6f);
            if (random.Next(2) == 0)
            {
                Mesh(node, corner + Vector3.Up * 1.3f, new CylinderMesh { BottomRadius = .75f, Height = 1.4f, RadialSegments = 14, TopRadius = .75f }, Palette.Art.Metal.Darkened(.1f));
                foreach (var leg in (Vector3[])[new(-.5f, 0, -.5f), new(.5f, 0, -.5f), new(.5f, 0, .5f), new(-.5f, 0, .5f)])
                {
                    Box(node, corner + leg + Vector3.Up * .3f, new(.1f, .6f, .1f), Palette.Art.Timber);
                }
            }
            else
            {
                Box(node, corner + Vector3.Up * 1.1f, new(2.2f, 2.2f, 2), wall.Darkened(.05f));
                Box(node, corner + Vector3.Up * (2.2f + CorniceHeight / 2), new(2.4f, CorniceHeight, 2.2f), trim);
            }
        }
    }

    private static Vector3 Rgb(Color color) => new(color.R, color.G, color.B);

    private ShaderMaterial Facade(Obstacle obstacle, Color wall, float seed)
    {
        var material = new ShaderMaterial { Shader = GD.Load<Shader>(FacadeShaderPath) };
        material.SetShaderParameter("wall_color", wall);
        material.SetShaderParameter("wall_texture", SurfaceTextures.Albedo(SurfaceTextures.Stucco));
        material.SetShaderParameter("wall_mean", Rgb(SurfaceTextures.Mean(SurfaceTextures.Stucco)));
        material.SetShaderParameter("roof_texture", SurfaceTextures.Albedo(SurfaceTextures.Concrete));
        material.SetShaderParameter("roof_mean", Rgb(SurfaceTextures.Mean(SurfaceTextures.Concrete)));
        material.SetShaderParameter("trim_color", Palette.Art.Coping);
        material.SetShaderParameter("roof_color", Palette.Art.Roof);
        material.SetShaderParameter("glass_color", Palette.Art.Glass);
        material.SetShaderParameter("sky_color", night ? Palette.Night.Haze : Palette.World.Haze);
        material.SetShaderParameter("shutter_color", Palette.Art.Metal);
        material.SetShaderParameter("lamp_color", Palette.World.WindowLamp);
        material.SetShaderParameter("center", World(obstacle.Center));
        material.SetShaderParameter("half_size", new Vector2(obstacle.HalfSize.X, obstacle.HalfSize.Y));
        material.SetShaderParameter("height", obstacle.Height);
        material.SetShaderParameter("storey", Storey);
        material.SetShaderParameter("bay", Bay);
        material.SetShaderParameter("shuttered", Shuttered);
        material.SetShaderParameter("lit", night ? NightLit : 0);
        material.SetShaderParameter("seed", seed * 100);
        return material;
    }

    /// <summary>
    /// A ridge as a heightfield over its footprint: steep rocky sides rising to a rough crest, highest
    /// along its long axis. It never rises above the obstacle's height.
    /// </summary>
    private void BuildRidge(Node3D node, Obstacle obstacle)
    {
        var half = obstacle.HalfSize;
        int columns = Math.Max(2, (int)MathF.Ceiling(half.X * 2 / RidgeStep));
        int rows = Math.Max(2, (int)MathF.Ceiling(half.Y * 2 / RidgeStep));
        var noise = new FastNoiseLite { Frequency = RidgeNoiseFrequency, Seed = (int)(obstacle.Center.X * 7 + obstacle.Center.Y) };
        float slope = Math.Min(RidgeSlope, Math.Min(half.X, half.Y) * .8f);
        bool alongX = half.X >= half.Y;

        var local = new Vector3[columns + 1, rows + 1];
        for (int i = 0; i <= columns; i++)
        {
            for (int j = 0; j <= rows; j++)
            {
                float x = -half.X + i * half.X * 2 / columns;
                float z = -half.Y + j * half.Y * 2 / rows;
                float edge = Math.Min(half.X - Math.Abs(x), half.Y - Math.Abs(z));
                float rise = Mathf.SmoothStep(0, slope, edge);
                // Highest along the long axis, falling off towards the long sides.
                float across = alongX ? z / half.Y : x / half.X;
                float crest = 1 - .25f * across * across;
                float rough = 1 - RidgeRoughness / 2 + RidgeRoughness * (noise.GetNoise2D(obstacle.Center.X + x, obstacle.Center.Y + z) * .5f + .5f);
                local[i, j] = new(x, obstacle.Height * .35f * Math.Min(1, rise * crest * rough), z);
            }
        }

        var tool = new SurfaceTool();
        tool.Begin(Godot.Mesh.PrimitiveType.Triangles);
        for (int i = 0; i < columns; i++)
        {
            for (int j = 0; j < rows; j++)
            {
                foreach (var (a, b) in (ValueTuple<int, int>[])[(i, j), (i + 1, j), (i + 1, j + 1), (i, j), (i + 1, j + 1), (i, j + 1)])
                {
                    // Normal from the neighbouring heights, so the surface shades smoothly.
                    var dx = local[Math.Min(a + 1, columns), b] - local[Math.Max(a - 1, 0), b];
                    var dz = local[a, Math.Min(b + 1, rows)] - local[a, Math.Max(b - 1, 0)];
                    tool.SetNormal(dz.Cross(dx).Normalized());
                    tool.AddVertex(local[a, b]);
                }
            }
        }

        var rock = new ShaderMaterial { Shader = GD.Load<Shader>(RockShaderPath) };
        rock.SetShaderParameter("rock_tex", SurfaceTextures.Albedo(SurfaceTextures.Rock));
        rock.SetShaderParameter("stone_color", Palette.Art.Rock);
        rock.SetShaderParameter("moss_color", Palette.Art.Grass);
        node.AddChild(new MeshInstance3D { MaterialOverride = rock, Mesh = tool.Commit(), Position = World(obstacle.Center) });
        BuildRidgeScans(node, obstacle);
    }
}
