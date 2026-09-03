namespace PixelGridRecovery.Core;

public sealed class PixelImage
{
    public int Width { get; }
    public int Height { get; }
    internal Rgba32[] Pixels { get; }

    public PixelImage(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        Width = width;
        Height = height;
        Pixels = new Rgba32[checked(width * height)];
    }

    public Rgba32 this[int x, int y]
    {
        get => Pixels[GetIndex(x, y)];
        set => Pixels[GetIndex(x, y)] = value;
    }

    private int GetIndex(int x, int y)
    {
        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height)
            throw new ArgumentOutOfRangeException(nameof(x), "Pixel coordinates are outside the image.");
        return y * Width + x;
    }
}
