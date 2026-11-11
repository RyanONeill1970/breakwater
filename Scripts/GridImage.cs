namespace Breakwater;

/// <summary>
/// Paints the battlefield's cell grids (exploration fog, Classic ground) into smooth textures, so
/// the 3D view and the minimaps show soft fog and roads with clean curved edges rather than a
/// staircase of squares. Each cell becomes a block of pixels, the blocks are blurred together,
/// and the ground's edges are then sharpened back up along the blurred outline.
/// </summary>
public static class GridImage
{
    private const int Cells = Reconnaissance.GridSize;

    /// <summary>Fog pixels per cell, and the blur radius in pixels: about a cell, so fog fades in over a few metres.</summary>
    private const int FogScale = 4, FogBlur = 4;

    /// <summary>Ground pixels per cell, and the blur radius that rounds off each cell's corners before the edge is redrawn.</summary>
    private const int GroundScale = 8, GroundBlur = 4;

    /// <summary>The blurred ground's coverage is redrawn as a sharp edge between these levels.</summary>
    private const float EdgeLow = .4f, EdgeHigh = .6f;

    /// <summary>Box blurs applied per axis; two approximate a Gaussian.</summary>
    private const int BlurPasses = 2;

    private const int Channels = 4;

    /// <summary>The fog over ground not in sight now: darker where it has never been seen.</summary>
    public static Image Fog(Reconnaissance recon, float unexplored, float explored)
    {
        var cover = new float[Reconnaissance.CellCount];
        for (int i = 0; i < cover.Length; i++)
        {
            cover[i] = recon.Observed[i] ? 0 : recon.Explored[i] ? explored : unexplored;
        }

        int size = Cells * FogScale;
        var alpha = Blur(Enlarge(cover, FogScale), size, FogBlur);
        var pixels = new byte[size * size * Channels];
        for (int p = 0; p < alpha.Length; p++)
        {
            Write(pixels, p, Palette.Fog with { A = alpha[p] });
        }

        return Finish(size, pixels);
    }

    /// <summary>Classic ground: roads, tracks, rivers and jungle, clear over open grass.</summary>
    public static Image Ground(TerrainMap terrain, Func<Terrain, Color> color)
    {
        // Colours are blurred premultiplied by coverage, so open ground doesn't tint the edges.
        var layers = new float[Channels][];
        for (int c = 0; c < Channels; c++)
        {
            layers[c] = new float[Reconnaissance.CellCount];
        }

        for (int i = 0; i < Reconnaissance.CellCount; i++)
        {
            float cover = terrain[i] == Terrain.Open ? 0 : 1;
            var shade = color(terrain[i]);
            layers[0][i] = shade.R * cover;
            layers[1][i] = shade.G * cover;
            layers[2][i] = shade.B * cover;
            layers[3][i] = cover;
        }

        int size = Cells * GroundScale;
        var blurred = layers.Select(layer => Blur(Enlarge(layer, GroundScale), size, GroundBlur)).ToArray();
        var pixels = new byte[size * size * Channels];
        for (int p = 0; p < size * size; p++)
        {
            float cover = blurred[3][p];
            float unmix = cover > 0 ? 1 / cover : 0;
            float edge = Mathf.SmoothStep(EdgeLow, EdgeHigh, cover);
            Write(pixels, p, new Color(blurred[0][p] * unmix, blurred[1][p] * unmix, blurred[2][p] * unmix, edge));
        }

        return Finish(size, pixels);
    }

    /// <summary>Shows <paramref name="image"/> in <paramref name="texture"/>, creating it the first time.</summary>
    public static ImageTexture Show(ImageTexture? texture, Image image)
    {
        if (texture == null)
        {
            return ImageTexture.CreateFromImage(image);
        }

        texture.Update(image);
        return texture;
    }

    /// <summary>Each cell's value repeated over a <paramref name="scale"/>-pixel square block.</summary>
    private static float[] Enlarge(float[] cells, int scale)
    {
        int size = Cells * scale;
        var pixels = new float[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                pixels[y * size + x] = cells[y / scale * Cells + x / scale];
            }
        }

        return pixels;
    }

    private static float[] Blur(float[] pixels, int size, int radius)
    {
        var scratch = new float[pixels.Length];
        for (int pass = 0; pass < BlurPasses; pass++)
        {
            BoxBlur(pixels, scratch, size, radius, 1, size);
            BoxBlur(scratch, pixels, size, radius, size, 1);
        }

        return pixels;
    }

    /// <summary>
    /// A running-sum box blur along one axis: <paramref name="step"/> moves along a line, <paramref name="stride"/>
    /// to the next line. Samples beyond the edge repeat the edge pixel.
    /// </summary>
    private static void BoxBlur(float[] from, float[] to, int size, int radius, int step, int stride)
    {
        float width = 2 * radius + 1;
        for (int line = 0; line < size; line++)
        {
            int start = line * stride;
            float sum = 0;
            for (int k = -radius; k <= radius; k++)
            {
                sum += from[start + Math.Clamp(k, 0, size - 1) * step];
            }

            for (int i = 0; i < size; i++)
            {
                to[start + i * step] = sum / width;
                sum += from[start + Math.Min(i + radius + 1, size - 1) * step] - from[start + Math.Max(i - radius, 0) * step];
            }
        }
    }

    private static void Write(byte[] pixels, int index, Color color)
    {
        int at = index * Channels;
        pixels[at] = (byte)color.R8;
        pixels[at + 1] = (byte)color.G8;
        pixels[at + 2] = (byte)color.B8;
        pixels[at + 3] = (byte)color.A8;
    }

    private static Image Finish(int size, byte[] pixels)
    {
        var image = Image.CreateFromData(size, size, false, Image.Format.Rgba8, pixels);
        image.GenerateMipmaps();
        return image;
    }
}
