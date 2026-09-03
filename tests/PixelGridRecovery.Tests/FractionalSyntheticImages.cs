using PixelGridRecovery.Core;

namespace PixelGridRecovery.Tests;

public enum Rasterization { Nearest, Area }

internal static class FractionalSyntheticImages
{
    private static readonly Rgba32 Background = new(22, 27, 35);

    public static PixelImage Sprite(int size = 64)
    {
        var image = new PixelImage(size, size);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            double nx = (x + 0.5) / size, ny = (y + 0.5) / size;
            var color = Background;
            if (Math.Pow((nx - 0.49) / 0.36, 2) + Math.Pow((ny - 0.47) / 0.39, 2) <= 1)
                color = new Rgba32(40, 65, 78);
            if (Math.Pow((nx - 0.49) / 0.30, 2) + Math.Pow((ny - 0.46) / 0.33, 2) <= 1)
                color = nx < 0.52 ? new Rgba32(73, 177, 159) : new Rgba32(48, 139, 145);
            if (ny is > 0.35 and < 0.45 && (nx is > 0.29 and < 0.38 || nx is > 0.59 and < 0.68))
                color = new Rgba32(238, 218, 143);
            if (ny is > 0.57 and < 0.63 && nx is > 0.37 and < 0.62)
                color = new Rgba32(35, 49, 65);
            if (y == size / 4 && x is >= 2 and <= 4 || x == size - 6 && y >= size / 3 && y <= size / 3 + 2)
                color = new Rgba32(243, 179, 88);
            image[x, y] = color;
        }
        return image;
    }

    public static PixelImage OnLargeBackground()
    {
        var sprite = Sprite(32);
        var result = new PixelImage(128, 128);
        for (int y = 0; y < result.Height; y++)
        for (int x = 0; x < result.Width; x++)
            result[x, y] = x >= 53 && x < 85 && y >= 41 && y < 73 ? sprite[x - 53, y - 41] : Background;
        return result;
    }

    public static PixelImage Rasterize(PixelImage logical, double scaleX, double scaleY,
        double offsetX = 0, double offsetY = 0, Rasterization mode = Rasterization.Area, int noise = 0, bool blur = false)
    {
        var result = new PixelImage((int)Math.Ceiling(offsetX + logical.Width * scaleX),
            (int)Math.Ceiling(offsetY + logical.Height * scaleY));
        Rgba32 Read(int x, int y) => x < 0 || y < 0 || x >= logical.Width || y >= logical.Height ? Background : logical[x, y];
        for (int y = 0; y < result.Height; y++)
        for (int x = 0; x < result.Width; x++)
        {
            if (mode == Rasterization.Nearest)
            {
                result[x, y] = Read((int)Math.Floor((x + 0.5 - offsetX) / scaleX), (int)Math.Floor((y + 0.5 - offsetY) / scaleY));
                continue;
            }
            // Integrate in logical coordinates; this helper never calls production geometry or sampling.
            double u0 = (x - offsetX) / scaleX, u1 = (x + 1 - offsetX) / scaleX;
            double v0 = (y - offsetY) / scaleY, v1 = (y + 1 - offsetY) / scaleY;
            double r = 0, g = 0, b = 0, a = 0;
            for (int v = (int)Math.Floor(v0); v < Math.Ceiling(v1); v++)
            for (int u = (int)Math.Floor(u0); u < Math.Ceiling(u1); u++)
            {
                double coverage = Math.Max(0, Math.Min(u1, u + 1) - Math.Max(u0, u))
                    * Math.Max(0, Math.Min(v1, v + 1) - Math.Max(v0, v)) / ((u1 - u0) * (v1 - v0));
                var c = Read(u, v);
                r += coverage * c.R; g += coverage * c.G; b += coverage * c.B; a += coverage * c.A;
            }
            result[x, y] = new Rgba32(Byte(r), Byte(g), Byte(b), Byte(a));
        }
        if (blur) result = Blur(result);
        if (noise > 0)
        {
            var random = new Random(704);
            for (int y = 0; y < result.Height; y++)
            for (int x = 0; x < result.Width; x++)
            {
                var c = result[x, y];
                result[x, y] = new Rgba32(Byte(c.R + random.Next(-noise, noise + 1)),
                    Byte(c.G + random.Next(-noise, noise + 1)), Byte(c.B + random.Next(-noise, noise + 1)), c.A);
            }
        }
        return result;
    }

    private static PixelImage Blur(PixelImage input)
    {
        var output = new PixelImage(input.Width, input.Height);
        int[] kernel = [1, 2, 1];
        for (int y = 0; y < input.Height; y++)
        for (int x = 0; x < input.Width; x++)
        {
            double r = 0, g = 0, b = 0, a = 0;
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                var c = input[Math.Clamp(x + dx, 0, input.Width - 1), Math.Clamp(y + dy, 0, input.Height - 1)];
                double weight = kernel[dx + 1] * kernel[dy + 1] / 16.0;
                r += c.R * weight; g += c.G * weight; b += c.B * weight; a += c.A * weight;
            }
            output[x, y] = new Rgba32(Byte(r), Byte(g), Byte(b), Byte(a));
        }
        return output;
    }

    private static byte Byte(double value) => (byte)Math.Clamp(Math.Floor(value + 0.5), 0, 255);
}
