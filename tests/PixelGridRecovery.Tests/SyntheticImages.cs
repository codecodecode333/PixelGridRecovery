using PixelGridRecovery.Core;

namespace PixelGridRecovery.Tests;

internal static class SyntheticImages
{
    public static PixelImage Blocks(int columns = 8, int rows = 8, int cellWidth = 10,
        int cellHeight = 10, int offsetX = 0, int offsetY = 0, int noise = 0,
        int trailingX = 0, int trailingY = 0)
    {
        var random = new Random(1979);
        var palette = new Rgba32[columns * rows];
        for (int i = 0; i < palette.Length; i++)
            palette[i] = new Rgba32((byte)random.Next(30, 231), (byte)random.Next(30, 231), (byte)random.Next(30, 231));
        var image = new PixelImage(columns * cellWidth + offsetX + trailingX,
            rows * cellHeight + offsetY + trailingY);
        for (int y = 0; y < image.Height; y++)
        for (int x = 0; x < image.Width; x++)
        {
            bool inside = x >= offsetX && y >= offsetY && x < offsetX + columns * cellWidth && y < offsetY + rows * cellHeight;
            var color = inside ? palette[(y - offsetY) / cellHeight * columns + (x - offsetX) / cellWidth] : new Rgba32(17, 23, 29);
            byte Perturb(byte channel) => (byte)Math.Clamp(channel + random.Next(-noise, noise + 1), 0, 255);
            image[x, y] = new Rgba32(Perturb(color.R), Perturb(color.G), Perturb(color.B));
        }
        return image;
    }
}
