namespace Breakwater;

public partial class ArtStudy
{
    private const float VehicleScale = 1.5f, RoadTravelPerSecond = .004f;
    private readonly List<(Node3D Hull, Node3D Turret, float Start, CpuParticles3D Dust)> artVehicles = [];
    private readonly BattleView vehicleBuilder = new();

    private void BuildVehicles()
    {
        var dust = DustMaterial();
        foreach (float start in (float[])[0, .028f, .056f, .46f, .49f])
        {
            var model = vehicleBuilder.Body(start < .3f ? VehicleKind.Abrams : VehicleKind.Bradley, false, false);
            model.Hull.Scale = Vector3.One * VehicleScale;
            AddChild(model.Hull);
            var trail = new CpuParticles3D
            {
                Amount = 30,
                Lifetime = 2.8f,
                LocalCoords = false,
                EmissionShape = CpuParticles3D.EmissionShapeEnum.Box,
                EmissionBoxExtents = new(1.1f, .12f, .4f),
                Direction = Vector3.Back,
                Spread = 45,
                InitialVelocityMin = .2f,
                InitialVelocityMax = .6f,
                Gravity = new(0, .22f, 0),
                ScaleAmountMin = .8f,
                ScaleAmountMax = 1.7f,
                Mesh = new QuadMesh { Size = new(2, 2) },
                MaterialOverride = dust,
                Position = new(0, .35f, 2),
                ColorRamp = new Gradient
                {
                    Colors = [Palette.Art.Dust with { A = 0 }, Palette.Art.Dust, Palette.Art.Dust with { A = 0 }],
                    Offsets = [0, .18f, 1],
                },
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            model.Hull.AddChild(trail);
            artVehicles.Add((model.Hull, model.Turret, start, trail));
        }
        foreach (var (position, angle) in (ValueTuple<Vector2, float>[])[(new(26, -37), 145), (new(36, -28), 168)])
        {
            var model = vehicleBuilder.Body(VehicleKind.T72, true, false);
            model.Hull.Position = Ground(position, .05f);
            model.Hull.Scale = Vector3.One * VehicleScale;
            model.Hull.RotationDegrees = new(0, angle, 0);
            model.Turret.RotationDegrees = new(0, -18, 0);
            AddChild(model.Hull);
        }
        AnimateVehicles();
        vehicleBuilder.Free();
    }

    private Vector2 RoadPoint(float t)
    {
        float index = Mathf.PosMod(t, 1) * road.Count;
        int segment = (int)index;
        return road[segment].Lerp(road[(segment + 1) % road.Count], index - segment);
    }

    private void AnimateVehicles()
    {
        foreach (var (hull, turret, start, _) in artVehicles)
        {
            float t = start + clock * RoadTravelPerSecond;
            var p = RoadPoint(t);
            var ahead = RoadPoint(t + .002f);
            var direction = (ahead - p).Normalized();
            hull.Position = Ground(p, .06f);
            hull.Rotation = new(0, Mathf.Atan2(-direction.X, -direction.Y), 0);
            turret.Rotation = new(0, Mathf.Sin(clock * .13f + start * 5) * .12f, 0);
        }
    }

    private static StandardMaterial3D DustMaterial()
    {
        const int size = 64;
        var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float radius = (new Vector2(x, y) / (size - 1) * 2 - Vector2.One).Length();
                float alpha = Mathf.Pow(Math.Clamp(1 - radius, 0, 1), 1.5f);
                image.SetPixel(x, y, Colors.White with { A = alpha });
            }
        }
        return new StandardMaterial3D
        {
            AlbedoTexture = ImageTexture.CreateFromImage(image),
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            VertexColorUseAsAlbedo = true,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
    }
}
