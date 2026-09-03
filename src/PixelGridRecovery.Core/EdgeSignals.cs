namespace PixelGridRecovery.Core;

internal sealed record EdgeSignals(double[] X, double[] Y)
{
    public static EdgeSignals Build(PixelImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        var horizontal = new double[image.Width];
        var vertical = new double[image.Height];
        for (int y = 0; y < image.Height; y++)
        for (int x = 0; x < image.Width; x++)
        {
            int index = y * image.Width + x;
            var pixel = image.Pixels[index];
            if (x > 0)
                horizontal[x] += Difference(pixel, image.Pixels[index - 1]) / image.Height;
            if (y > 0)
                vertical[y] += Difference(pixel, image.Pixels[index - image.Width]) / image.Width;
        }
        return new EdgeSignals(horizontal, vertical);
    }

    private static double Difference(Rgba32 a, Rgba32 b) =>
        (Math.Abs(a.R * a.A - b.R * b.A) / 255.0
         + Math.Abs(a.G * a.A - b.G * b.A) / 255.0
         + Math.Abs(a.B * a.A - b.B * b.A) / 255.0
         + Math.Abs(a.A - b.A)) / 4;
}
