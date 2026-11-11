namespace Breakwater;

/// <summary>
/// Vehicle models built from a <see cref="Silhouette"/>: a hull with sloped glacis plates, fenders
/// and either tracks run round road wheels, sprocket and idler or a row of road wheels; and a turret
/// (flat-sided and angular, or a low Soviet dome) with mantlet, a gun with bore evacuator and muzzle
/// collar, cupola, machine gun, sight, smoke dischargers and antenna. Hull and turret are one mesh
/// each, coloured per vertex and drawn with <c>Shaders/Paint.gdshader</c>.
/// </summary>
/// <remarks>
/// Metres, standing on the origin with the gun towards −Z. The turret's origin is
/// <see cref="TurretHeight"/> (plus the deck) above the ground; the muzzle ends where
/// <c>BattleView.Effects</c> expects the flash, 0.5 m plus the barrel length ahead of it.
/// </remarks>
public static class VehicleModel
{
    private const string PaintShaderPath = "res://Shaders/Paint.gdshader";

    /// <summary>The turret ring's height above the ground, before a carrier's extra deck.</summary>
    public const float TurretHeight = 1.45f;

    // Dirt each part takes (vertex alpha; see the paint shader).
    private const float PaintDirt = 1, MetalDirt = .6f, RubberDirt = .8f;

    // Hull: half its width inside the tracks, and the tracks' inner and outer faces.
    private const float HullHalfWidth = 1.02f, TrackInner = 1.04f, TrackOuter = 1.52f;

    // Tracks run round a sprocket and idler this far fore and aft, this high, with this radius.
    private const float TrackEnd = 1.72f, TrackAxle = .4f, TrackRadius = .4f;
    private const int RoadWheels = 6, TrackEndPoints = 8, WheelSides = 14;

    /// <summary>Where the gun's barrel starts, inside the mantlet, relative to the turret.</summary>
    private const float GunRoot = -1.1f;

    /// <summary>How far ahead of the turret's origin the barrel is measured from: the muzzle is this plus the barrel length ahead.</summary>
    private const float BarrelDatum = .5f;

    private const float GunHeight = .05f;

    /// <summary>The top of the turret roof above the turret's origin.</summary>
    public const float TurretTop = .3f;

    /// <summary>The gun's tip for <paramref name="shape"/>, relative to the turret's origin.</summary>
    public static Vector3 Muzzle(Silhouette shape) => new(0, GunHeight, -BarrelDatum - shape.Barrel);

    /// <summary>How brightly hulls glow at night, as a multiple of their paint.</summary>
    private const float HeatGlow = .9f;

    private static ShaderMaterial? day, night;

    /// <summary>The paint material, glowing faintly with engine heat at <paramref name="atNight"/>.</summary>
    public static ShaderMaterial Paint(bool atNight)
    {
        ref var material = ref atNight ? ref night : ref day;
        if (material == null)
        {
            material = new ShaderMaterial { Shader = GD.Load<Shader>(PaintShaderPath) };
            material.SetShaderParameter("mud_color", Palette.World.Mud);
            material.SetShaderParameter("heat", atNight ? HeatGlow : 0);
        }

        return material;
    }

    public static (ArrayMesh Hull, ArrayMesh Turret) Build(Silhouette shape, Color paint)
    {
        var hull = new MeshBuilder();
        var turret = new MeshBuilder();
        var armour = paint with { A = PaintDirt };
        if (shape.Wheels > 0 && shape.Barrel == 0)
        {
            Truck(hull, turret, shape, armour);
        }
        else
        {
            Hull(hull, shape, armour);
            if (shape.Wheels > 0)
            {
                Wheels(hull, shape.Wheels, .42f, .36f, 1.3f);
            }
            else
            {
                Tracks(hull, shape, armour);
            }

            Turret(turret, shape, armour);
        }

        return (hull.Commit(), turret.Commit());
    }

    private static void Hull(MeshBuilder mesh, Silhouette shape, Color armour)
    {
        float deck = shape.Deck;
        float top = 1.14f + deck;
        // A wheeled hull is boat-shaped and fills the width the tracks would take.
        bool wheeled = shape.Wheels > 0;
        float half = wheeled ? TrackOuter - .22f : HullHalfWidth;
        float floor = wheeled ? .55f : .32f;
        float glacis = shape.Light ? -1.55f : -1.25f;
        Vector2[] side = [new(-1.72f, floor), new(1.8f, floor), new(1.98f, .62f), new(1.95f, top), new(glacis, top + .04f), new(-2.04f, .8f)];
        mesh.Prism(side, -half, half, armour);

        // Engine deck grille, and the driver's hatch and periscopes ahead of the turret.
        mesh.Box(new(0, top + .015f, 1.3f), new(half * 1.3f, .03f, .9f), Palette.World.Track with { A = MetalDirt });
        mesh.Box(new(-.35f, top + .06f, glacis + .35f), new(.5f, .08f, .45f), armour.Darkened(.06f));
        mesh.Box(new(-.35f, top + .12f, glacis + .18f), new(.34f, .06f, .06f), Palette.World.Optics with { A = 0 });

        if (!wheeled)
        {
            // Fenders over the tracks, with headlights at their front corners.
            foreach (int s in (int[])[-1, 1])
            {
                Vector2[] fender = [new(-2.08f, 1.0f), new(2.02f, 1.0f), new(2.02f, 1.07f), new(-1.98f, 1.07f)];
                mesh.Prism(fender, s * HullHalfWidth, s * (TrackOuter + .03f), armour);
                mesh.Box(new(s * 1.3f, 1.13f, -1.96f), new(.16f, .12f, .1f), Palette.World.Optics with { A = MetalDirt });
            }
        }

        if (shape.Skirts)
        {
            foreach (int s in (int[])[-1, 1])
            {
                Vector2[] skirt = [new(-1.98f, .78f), new(-1.8f, .5f), new(1.78f, .5f), new(1.78f, 1.02f), new(-1.98f, 1.02f)];
                mesh.Prism(skirt, s * (TrackOuter + .02f), s * (TrackOuter + .08f), armour.Darkened(.04f));
            }
        }
    }

    private static void Tracks(MeshBuilder mesh, Silhouette shape, Color armour)
    {
        var rubber = Palette.World.Track with { A = RubberDirt };
        var hub = Palette.World.Wheel with { A = RubberDirt };
        // The track's run: round the sprocket at the back from bottom to top, then round the idler at the front.
        List<Vector2> loop = [];
        foreach (var (end, start) in (ValueTuple<float, float>[])[(TrackEnd, -Mathf.Pi / 2), (-TrackEnd, Mathf.Pi / 2)])
        {
            for (int i = 0; i <= TrackEndPoints; i++)
            {
                float angle = start + Mathf.Pi * i / TrackEndPoints;
                loop.Add(new(end + Mathf.Cos(angle) * TrackRadius, TrackAxle + Mathf.Sin(angle) * TrackRadius));
            }
        }

        foreach (int s in (int[])[-1, 1])
        {
            float middle = s * (TrackInner + TrackOuter) / 2;
            float width = TrackOuter - TrackInner;
            mesh.Band(loop, s * TrackInner, s * TrackOuter, rubber);
            // Road wheels carry the hull; sprocket at the back, idler at the front, return rollers above.
            for (int i = 0; i < RoadWheels; i++)
            {
                float z = Mathf.Lerp(-1.32f, 1.32f, (float)i / (RoadWheels - 1));
                mesh.Wheel(new(middle, .34f, z), .31f, width * .9f, WheelSides, rubber, hub, s);
            }

            mesh.Wheel(new(middle, TrackAxle + .04f, TrackEnd), .3f, width * .8f, WheelSides, hub, rubber, s);
            mesh.Wheel(new(middle, TrackAxle, -TrackEnd), .28f, width * .8f, WheelSides, hub, rubber, s);
            if (!shape.Skirts)
            {
                foreach (float z in (float[])[-.8f, 0, .8f])
                {
                    mesh.Wheel(new(middle, .72f, z), .08f, width * .6f, 8, hub, rubber, s);
                }
            }
        }
    }

    private static void Wheels(MeshBuilder mesh, int perSide, float radius, float width, float spread)
    {
        var tyre = Palette.World.Track with { A = RubberDirt };
        var hub = Palette.World.Wheel with { A = RubberDirt };
        foreach (int s in (int[])[-1, 1])
        {
            for (int i = 0; i < perSide; i++)
            {
                float z = Mathf.Lerp(-spread, spread, (float)i / (perSide - 1));
                mesh.Wheel(new(s * (TrackOuter - width / 2), radius, z), radius, width, WheelSides, tyre, hub, s);
            }
        }
    }

    private static void Turret(MeshBuilder mesh, Silhouette shape, Color armour)
    {
        float w = shape.Turret / 2;
        var steel = armour.Darkened(.18f) with { A = MetalDirt };
        bool carrier = shape.Barrel < 1;
        if (carrier)
        {
            // A small cupola with a heavy machine gun.
            Ring(mesh, 8, w, w, -.3f, .1f, .82f, armour);
        }
        else if (shape.Round)
        {
            // A cast dome: wide and low, rounding over to a small top.
            Ring(mesh, 14, w, 1.15f, -.3f, .08f, .84f, armour, zOffset: .05f);
            Ring(mesh, 14, w * .84f, 1.15f * .84f, .08f, .3f, .6f, armour, zOffset: .05f);
        }
        else
        {
            // Flat armour plates, narrowing to a wedge at the front.
            Vector3[] outline = [new(-w * .55f, 0, -1.2f), new(w * .55f, 0, -1.2f), new(w, 0, -.6f), new(w, 0, .95f), new(w * .8f, 0, 1.15f), new(-w * .8f, 0, 1.15f), new(-w, 0, .95f), new(-w, 0, -.6f)];
            mesh.Loft(outline.Select(p => p + Vector3.Up * -.3f).ToArray(), outline.Select(p => new Vector3(p.X * .9f, .3f, p.Z * .94f)).ToArray(), armour);
            if (!shape.Light)
            {
                // The stowage basket on the turret bustle.
                mesh.Box(new(0, -.05f, 1.36f), new(w * 1.5f, .28f, .42f), Palette.World.Stowage with { A = PaintDirt });
            }
        }

        // The gun: mantlet, a tapering barrel, the bore evacuator part-way along and a muzzle collar.
        float tip = -BarrelDatum - shape.Barrel;
        float radius = carrier ? .04f : .085f;
        if (!carrier)
        {
            mesh.Box(new(0, GunHeight, -1.2f), new(.56f, .42f, .3f), armour.Darkened(.05f));
        }

        var root = new Vector3(0, GunHeight, carrier ? -.3f : GunRoot);
        var muzzle = new Vector3(0, GunHeight, tip);
        mesh.Cylinder(root, muzzle, radius, radius * .82f, 10, steel);
        if (!carrier && shape.Barrel > 2)
        {
            var evacuator = root.Lerp(muzzle, .48f);
            mesh.Cylinder(evacuator, evacuator + Vector3.Forward * .45f, radius * 1.45f, radius * 1.45f, 10, steel);
            mesh.Cylinder(muzzle + Vector3.Back * .14f, muzzle, radius * 1.15f, radius * 1.15f, 10, steel);
        }

        if (!carrier)
        {
            // Commander's cupola with its machine gun, the gunner's sight, and a loader's hatch.
            var cupola = new Vector3(w * .45f, .3f, .35f);
            mesh.Cylinder(cupola, cupola + Vector3.Up * .18f, .27f, .24f, 12, armour.Darkened(.04f));
            mesh.Cylinder(cupola + new Vector3(0, .24f, 0), cupola + new Vector3(0, .24f, -.8f), .025f, .025f, 6, steel);
            mesh.Box(new(-w * .42f, .4f, -.55f), new(.3f, .2f, .34f), armour.Darkened(.08f));
            mesh.Box(new(-w * .42f, .42f, -.73f), new(.24f, .12f, .02f), Palette.World.Optics with { A = 0 });
            mesh.Cylinder(new(-w * .45f, .3f, .4f), new(-w * .45f, .35f, .4f), .25f, .25f, 10, armour.Darkened(.06f));

            // Smoke dischargers, a cluster each side of the turret front, angled up and out.
            foreach (int s in (int[])[-1, 1])
            {
                for (int i = 0; i < 3; i++)
                {
                    var at = new Vector3(s * w * .92f, -.05f + i * .1f, -.55f);
                    mesh.Cylinder(at, at + new Vector3(s * .08f, .12f, -.2f), .045f, .045f, 6, steel);
                }
            }
        }

        if (shape.Launcher)
        {
            mesh.Box(new(w + .12f, .45f, -.2f), new(.5f, .42f, 1.5f), armour.Darkened(.1f));
            foreach (float x in (float[])[-.11f, .11f])
            {
                mesh.Cylinder(new(w + .12f + x, .45f, -.94f), new(w + .12f + x, .45f, -.97f), .1f, .1f, 10, Palette.World.Track with { A = MetalDirt });
            }
        }

        mesh.Cylinder(new(-w * .6f, .3f, .9f), new(-w * .6f, 2.3f, .9f), .02f, .01f, 5, Palette.World.Antenna with { A = MetalDirt });
    }

    /// <summary>The canvas-topped supply truck: frame, cab and wheels on the hull; the cargo on the "turret".</summary>
    private static void Truck(MeshBuilder hull, MeshBuilder cargo, Silhouette shape, Color armour)
    {
        hull.Box(new(0, .75f, 0), new(1.6f, .25f, 4), Palette.World.Track with { A = MetalDirt });
        Vector3[] cab = [new(-1.05f, .85f, -2.05f), new(1.05f, .85f, -2.05f), new(1.05f, .85f, -.9f), new(-1.05f, .85f, -.9f)];
        hull.Loft(cab, cab.Select(p => new Vector3(p.X * .92f, 2.1f, p.Z == -2.05f ? -1.8f : p.Z)).ToArray(), armour);
        hull.Box(new(0, 1.75f, -1.86f), new(1.7f, .45f, .02f), Palette.World.Optics with { A = 0 });
        Wheels(hull, shape.Wheels, .45f, .34f, 1.45f);

        // Cargo bed and canvas tilt, relative to the "turret" origin above the deck.
        float floor = 1.0f - (TurretHeight + shape.Deck);
        var canvas = Palette.World.Canvas with { A = PaintDirt };
        cargo.Box(new(0, floor + .2f, .75f), new(2.1f, .4f, 2.6f), armour.Darkened(.1f));
        cargo.Box(new(0, floor + .8f, .75f), new(2f, .8f, 2.5f), canvas);
        cargo.Cylinder(new(0, floor + 1.2f, -.5f), new(0, floor + 1.2f, 2), 1, 1, 16, canvas);
    }

    /// <summary>
    /// A ring-shaped loft: an ellipse <paramref name="halfX"/> by <paramref name="halfZ"/> at <paramref name="from"/>
    /// shrinking by <paramref name="taper"/> up to <paramref name="to"/>.
    /// </summary>
    private static void Ring(MeshBuilder mesh, int points, float halfX, float halfZ, float from, float to, float taper, Color color, float zOffset = 0)
    {
        Vector3 Point(int i, float y, float scale)
        {
            float a = Mathf.Tau * i / points;
            return new(Mathf.Cos(a) * halfX * scale, y, Mathf.Sin(a) * halfZ * scale + zOffset);
        }

        mesh.Loft([.. Enumerable.Range(0, points).Select(i => Point(i, from, 1))], [.. Enumerable.Range(0, points).Select(i => Point(i, to, taper))], color);
    }
}
