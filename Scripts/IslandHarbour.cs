namespace Breakwater;

public static class IslandHarbour
{


    public static void Build(Node3D parent, Vector3 origin, float seaLevel = -.06f)
    {
        var harbour = new Node3D { Name = "DetailedHarbour", Position = origin };
        parent.AddChild(harbour);
        var batch = new ArtStudyBatch();
        var concrete = HarbourMaterial(Palette.Art.Concrete, .12f, seaLevel);
        var coping = HarbourMaterial(Palette.Art.Coping, .3f, seaLevel);
        var wall = HarbourMaterial(Palette.Art.Wall, .18f, seaLevel);
        var steel = HarbourMaterial(Palette.Art.Metal, .6f, seaLevel);
        var rust = HarbourMaterial(Palette.Art.Crane, .5f, seaLevel);
        var roofing = HarbourMaterial(Palette.Art.Roof, .65f, seaLevel);
        var wood = SurfaceTextures.Material(SurfaceTextures.Bark, 3, Palette.Art.Timber);
        var rubber = Finish(Palette.Art.Rubber);
        var glass = Finish(Palette.Art.Glass, .25f);
        var paint = Finish(Palette.Art.Markings);

        // Massive quay wall, narrow expansion joints, and individual coping stones.
        batch.Box(new(0, -1.25f, 0), new(34, 2.5f, 20), concrete);
        batch.Box(new(0, -3.1f, 0), new(34.5f, 1.2f, 20.5f), steel);
        for (float x = -17; x < 17; x += 4.25f)
        {
            for (int z = -10; z < 10; z += 4)
            {
                batch.Box(new(x + 2.125f, .08f, z + 2), new(4.21f, .16f, 3.96f), concrete);
            }
            batch.Box(new(x + 2.125f, .25f, 10.3f), new(4.21f, .4f, .65f), coping);
            batch.Box(new(x + 2.125f, .25f, -10.3f), new(4.21f, .4f, .65f), coping);
        }
        for (int z = -9; z <= 9; z += 3)
        {
            batch.Box(new(-17.3f, .25f, z), new(.65f, .4f, 2.95f), coping);
            // Timber fenders and hung tyres at the seaward edge.
            batch.Box(new(-17.45f, -.6f, z), new(.25f, 2.7f, .3f), wood);
            batch.Ring(new(-17.68f, -.6f, z), .34f, .62f, rubber, new(0, 0, Mathf.Pi / 2));
            batch.Beam(new(-17.6f, .3f, z), new(-17.7f, -.4f, z), .025f, steel);
        }
        for (int x = -14; x <= 14; x += 7)
        {
            foreach (int side in (int[])[-1, 1])
            {
                var p = new Vector3(x, .18f, side * 9.3f);
                batch.Box(p, new(1, .18f, .75f), steel);
                batch.Beam(p, p + Vector3.Up * .75f, .18f, steel);
                batch.Beam(p + new Vector3(-.45f, .63f, 0), p + new Vector3(.45f, .63f, 0), .13f, steel);
                batch.Box(new(x, .18f, side * 8.6f), new(2.5f, .02f, .18f), paint);
            }
        }
        // Two crane tracks and inset drainage gratings give the large slab a believable scale.
        foreach (float z in (float[])[-2.8f, 2.8f])
        {
            batch.Box(new(-6, .17f, z), new(19, .09f, .13f), steel);
            for (int x = -15; x <= 3; x += 2)
            {
                batch.Box(new(x, .16f, z), new(.35f, .1f, .5f), steel);
            }
        }
        for (int i = 0; i < 14; i++)
        {
            batch.Box(new(15.5f, .2f, -6 + i * .35f), new(1.1f, .05f, .12f), steel);
        }
        BuildHarbourCrane(batch, new(-8, .25f, -2), rust, steel, glass, concrete);
        BuildHarbourShed(batch, new(9, .2f, -5), wall, steel, roofing, wood, glass);

        // Cargo is grouped around the store, leaving the vehicle approach clear.
        for (int i = 0; i < 11; i++)
        {
            var p = new Vector3(5 + i % 4 * 2.1f, .2f + i / 8 * 1.3f, 3.7f + i / 4 % 2 * 2.2f);
            CargoCrate(batch, p, wood, steel);
        }
        for (int i = 0; i < 5; i++)
        {
            var p = new Vector3(14 + i % 2 * 1.1f, .22f, -.5f + i / 2 * 1.1f);
            batch.Beam(p, p + Vector3.Up * 1.3f, .42f, i % 2 == 0 ? rust : roofing, 20);
            foreach (float h in (float[])[.12f, .4f, .95f, 1.23f])
            {
                batch.Ring(p + Vector3.Up * h, .41f, .44f, steel, Vector3.Zero);
            }
        }
        // Coiled hawser and dock ladders.
        for (int ring = 0; ring < 5; ring++)
        {
            batch.Ring(new(-13, .25f, 7), .35f + ring * .11f, .41f + ring * .11f, wood, Vector3.Zero);
        }
        foreach (float z in (float[])[-6, 6])
        {
            batch.Beam(new(-17.8f, -2, z - .4f), new(-17.8f, 1, z - .4f), .065f, steel);
            batch.Beam(new(-17.8f, -2, z + .4f), new(-17.8f, 1, z + .4f), .065f, steel);
            for (float y = -2; y <= .8f; y += .35f)
            {
                batch.Beam(new(-17.85f, y, z - .4f), new(-17.85f, y, z + .4f), .05f, steel);
            }
        }
        batch.Commit(harbour);
        harbour.AddChild(new Label3D
        {
            Text = "BREAKWATER\nHARBOUR STORES",
            Position = new(8, 4.2f, -1.25f),
            FontSize = 42,
            PixelSize = .012f,
            Modulate = Palette.Art.Markings,
            OutlineSize = 0,
            Shaded = true,
            NoDepthTest = false,
        });

    }

    private static StandardMaterial3D Finish(Color color, float roughness = .85f) => new()
    {
        AlbedoColor = color,
        Roughness = roughness,
        MetallicSpecular = .2f,
    };
    public static Material HarbourMaterial(Color color, float grainScale, float seaLevel = -.06f)
    {
        var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://Shaders/ArtHarbour.gdshader") };
        material.SetShaderParameter("base_color", color);
        material.SetShaderParameter("grime_color", Palette.Art.Grime);
        material.SetShaderParameter("grain_tex", SurfaceTextures.Albedo(SurfaceTextures.Concrete));
        material.SetShaderParameter("grain_scale", grainScale);
        material.SetShaderParameter("sea_level", seaLevel);
        return material;
    }

    public static void CargoCrate(ArtStudyBatch batch, Vector3 p, Material wood, Material metal)
    {
        batch.Box(p + new Vector3(0, .13f, 0), new(1.8f, .24f, 1.45f), wood);
        batch.Box(p + new Vector3(0, .8f, 0), new(1.6f, 1.1f, 1.3f), wood);
        foreach (float x in (float[])[-.66f, .66f])
        {
            batch.Box(p + new Vector3(x, .85f, .68f), new(.16f, 1.2f, .12f), wood);
            batch.Box(p + new Vector3(x, .85f, -.68f), new(.16f, 1.2f, .12f), wood);
            batch.Box(p + new Vector3(x, 1.42f, 0), new(.16f, .12f, 1.4f), wood);
        }
        foreach (float z in (float[])[-.5f, .5f])
        {
            batch.Box(p + new Vector3(0, 1.43f, z), new(1.65f, .035f, .04f), metal);
        }
    }

    private static void BuildHarbourCrane(ArtStudyBatch b, Vector3 p, Material paint, Material steel, Material glass, Material concrete)
    {
        b.Box(p, new(4.5f, .5f, 4.5f), concrete);
        for (int side = 0; side < 4; side++)
        {
            float angle = side * Mathf.Pi / 2;
            var a = new Vector3(Mathf.Cos(angle) * 1.8f, 0, Mathf.Sin(angle) * 1.8f);
            var c = new Vector3(Mathf.Cos(angle + Mathf.Pi / 2) * 1.8f, 0, Mathf.Sin(angle + Mathf.Pi / 2) * 1.8f);
            b.Beam(p + a, p + a * .45f + Vector3.Up * 9, .14f, paint);
            for (int level = 0; level < 4; level++)
            {
                float y = level * 2.2f;
                float taper = 1 - y * .06f;
                b.Beam(p + a * taper + Vector3.Up * y, p + c * (taper - .13f) + Vector3.Up * (y + 2.2f), .07f, paint);
                b.Beam(p + a * taper + Vector3.Up * y, p + c * taper + Vector3.Up * y, .09f, steel);
            }
        }
        b.Ring(p + Vector3.Up * 9, 1.2f, 1.65f, steel, Vector3.Zero);
        b.Box(p + new Vector3(.8f, 9.9f, 0), new(3.4f, 1.4f, 2.8f), paint);
        b.Box(p + new Vector3(2.5f, 10.9f, .3f), new(1.8f, 2.1f, 2.3f), paint);
        b.Box(p + new Vector3(2.51f, 11.25f, 1.47f), new(1.45f, .95f, .06f), glass);
        b.Box(p + new Vector3(3.42f, 11.25f, .3f), new(.06f, .95f, 1.9f), glass);
        b.Box(p + new Vector3(2.5f, 12, .3f), new(2.1f, .14f, 2.55f), steel);
        b.Box(p + new Vector3(4.5f, 10.1f, -1), new(2.7f, 1.9f, 2.4f), concrete);
        // Paired boom chords, cross bracing, sheaves and actual load cable.
        for (int side = -1; side <= 1; side += 2)
        {
            var start = p + new Vector3(-.5f, 10.4f, side * .8f);
            var end = p + new Vector3(-12, 16.3f, side * .35f);
            b.Beam(start, end, .105f, paint);
            b.Beam(start + Vector3.Up * 1.7f, end + Vector3.Up * .55f, .1f, paint);
            for (int i = 0; i < 8; i++)
            {
                var a = start.Lerp(end, i / 8f);
                var c = start.Lerp(end, (i + 1) / 8f);
                b.Beam(a, c + Vector3.Up * Mathf.Lerp(1.7f, .55f, (i + 1) / 8f), .055f, paint);
            }
            b.Beam(p + new Vector3(2, 14, side * .3f), end + Vector3.Up * .55f, .028f, steel);
        }
        b.Beam(p + new Vector3(2, 10, 0), p + new Vector3(2, 14, 0), .14f, paint);
        var tip = p + new Vector3(-12, 16.3f, 0);
        b.Beam(tip, tip + Vector3.Down * 10, .025f, steel);
        b.Box(tip + Vector3.Down * 10, new(.35f, .55f, .22f), steel);
        b.Ring(tip + Vector3.Down * 10.5f, .16f, .24f, steel, new(Mathf.Pi / 2, 0, 0));
        for (int i = 0; i < 24; i++)
        {
            float y = i * .35f;
            b.Beam(p + new Vector3(.6f, y, 1.35f), p + new Vector3(1.2f, y, 1.35f), .035f, steel);
        }
        b.Beam(p + new Vector3(.6f, 0, 1.35f), p + new Vector3(.6f, 8.5f, 1.35f), .04f, steel);
        b.Beam(p + new Vector3(1.2f, 0, 1.35f), p + new Vector3(1.2f, 8.5f, 1.35f), .04f, steel);
    }

    private static void BuildHarbourShed(ArtStudyBatch b, Vector3 p, Material wall, Material metal, Material roof, Material wood, Material glass)
    {
        b.Box(p + new Vector3(0, 2.25f, 0), new(9.5f, 4.5f, 6.8f), wall);
        b.Add(new PrismMesh { Size = new(6.8f, 1.05f, 9.5f) }, new(new Basis(Vector3.Up, Mathf.Pi / 2), p + Vector3.Up * 5), wall);
        b.Box(p + new Vector3(0, .2f, 0), new(9.8f, .4f, 7), metal);
        // Corrugated roof pitches, end fascia, gutter and downpipe.
        for (int side = -1; side <= 1; side += 2)
        {
            for (int strip = 0; strip < 27; strip++)
            {
                float x = -5.1f + strip * .39f;
                var mesh = new BoxMesh { Size = new(.43f, .09f, 4.1f) };
                b.Add(mesh, new(Basis.FromEuler(new(side * .28f, 0, 0)), p + new Vector3(x, 4.95f + strip % 2 * .035f, side * 1.9f)), roof);
            }
            b.Beam(p + new Vector3(-5.2f, 4.45f, side * 3.9f), p + new Vector3(5.2f, 4.45f, side * 3.9f), .095f, metal);
        }
        b.Beam(p + new Vector3(4.85f, .1f, 3.8f), p + new Vector3(4.85f, 4.45f, 3.8f), .09f, metal);
        // Door recess, individual shutters, hinges and canopy supports.
        b.Box(p + new Vector3(-1, 1.8f, 3.43f), new(4, 3.6f, .12f), metal);
        for (int plank = 0; plank < 12; plank++)
        {
            b.Box(p + new Vector3(-2.85f + plank * .34f, 1.75f, 3.52f), new(.32f, 3.3f, .1f), wood);
        }
        foreach (float y in (float[])[.5f, 2.9f])
        {
            b.Box(p + new Vector3(-1, y, 3.6f), new(3.9f, .13f, .08f), metal);
        }
        b.Box(p + new Vector3(3.3f, 2.55f, 3.46f), new(1.65f, 1.4f, .1f), metal);
        b.Box(p + new Vector3(3.3f, 2.55f, 3.54f), new(1.4f, 1.14f, .08f), glass);
        b.Box(p + new Vector3(3.3f, 2.55f, 3.6f), new(.06f, 1.2f, .06f), metal);
        b.Box(p + new Vector3(-1, 3.95f, 4), new(5.1f, .15f, 1.6f), roof);
        foreach (float x in (float[])[-3.4f, 1.4f])
        {
            b.Beam(p + new Vector3(x, 0, 4.6f), p + new Vector3(x, 3.9f, 4.6f), .09f, metal);
        }
    }

}
