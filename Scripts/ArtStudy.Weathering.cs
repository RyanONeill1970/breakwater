namespace Breakwater;

public partial class ArtStudy
{
    private void BuildHarbourWeathering()
    {
        const int resolution = 128;
        var random = new Random(1207);
        var noise = new FastNoiseLite { Seed = 1608, Frequency = .08f };
        var image = Image.CreateEmpty(resolution, resolution, false, Image.Format.Rgba8);
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                var p = new Vector2(x, y) / (resolution - 1) * 2 - Vector2.One;
                float edge = Math.Clamp(1 - p.Length(), 0, 1);
                float grain = Math.Clamp(noise.GetNoise2D(x, y) + .45f, 0, 1);
                image.SetPixel(x, y, Palette.Art.Grime with { A = edge * grain * .5f });
            }
        }
        image.GenerateMipmaps();
        var stain = ImageTexture.CreateFromImage(image);
        for (int i = 0; i < 26; i++)
        {
            var p = QuayOrigin + new Vector3(random.NextSingle() * 29 - 14.5f, .4f, random.NextSingle() * 17 - 8.5f);
            AddChild(new Decal
            {
                Position = p,
                Size = new(2 + random.NextSingle() * 4, 1.1f, 1 + random.NextSingle() * 2),
                RotationDegrees = new(0, random.NextSingle() * 360, 0),
                TextureAlbedo = stain,
                DistanceFadeEnabled = true,
                DistanceFadeBegin = 120,
                DistanceFadeLength = 30,
            });
        }
        // Small debris is modelled in batches; large routes remain uncluttered.
        var batch = new ArtStudyBatch();
        var stone = Finish(Palette.Art.Rock);
        var timber = SurfaceTextures.Material(SurfaceTextures.Bark, 2, Palette.Art.Timber);
        for (int i = 0; i < 70; i++)
        {
            var p = new Vector2(-68 + random.NextSingle() * 38, 25 + random.NextSingle() * 17);
            if (RoadDistance(p) < 4 || Shore(p) < 4)
            {
                continue;
            }
            float size = .12f + random.NextSingle() * .3f;
            batch.Add(new SphereMesh { Radius = size, Height = size * 1.2f, RadialSegments = 8, Rings = 4 },
                new(new Basis(Vector3.Up, random.NextSingle() * Mathf.Tau), Ground(p, size * .3f)), stone);
        }
        for (int i = 0; i < 8; i++)
        {
            var p = new Vector2(-69 + random.NextSingle() * 5, 26 + random.NextSingle() * 5);
            batch.Box(Ground(p, .12f), new(.18f, .12f, 1.3f + random.NextSingle()), timber, random.NextSingle() * Mathf.Tau);
        }
        batch.Commit(this);
    }
}
