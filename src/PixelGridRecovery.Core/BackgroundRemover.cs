namespace PixelGridRecovery.Core;

public sealed class BackgroundRemover
{
    private const int ColorShift = 3;
    private const double MaximumRgbDistance = 255 * 1.7320508075688772;

    public BackgroundRemovalResult Remove(PixelImage image, BackgroundRemovalOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        options ??= new BackgroundRemovalOptions();
        Validate(options);

        var output = CopyCanonical(image);
        if (options.Mode == BackgroundRemovalMode.None)
            return new BackgroundRemovalResult(output, null, 0);

        Rgba32? background = options.Mode == BackgroundRemovalMode.AutoBorder
            ? DetectBorderColor(image)
            : options.BackgroundColor;
        if (background is null)
            return new BackgroundRemovalResult(output, null, 0);

        double threshold = options.Tolerance / 100 * MaximumRgbDistance;
        double thresholdSquared = threshold * threshold;
        var candidates = new bool[image.Pixels.Length];
        for (int index = 0; index < candidates.Length; index++)
        {
            var pixel = image.Pixels[index];
            candidates[index] = pixel.A == 0 || DistanceSquared(pixel, background.Value) <= thresholdSquared;
        }

        bool[] removalMask = options.BorderConnectedOnly
            ? ConnectedToBorder(candidates, image.Width, image.Height)
            : candidates;
        int removed = 0;
        for (int index = 0; index < removalMask.Length; index++)
        {
            if (!removalMask[index]) continue;
            if (image.Pixels[index].A != 0) removed++;
            output.Pixels[index] = default;
        }
        return new BackgroundRemovalResult(output,
            new Rgba32(background.Value.R, background.Value.G, background.Value.B), removed);
    }

    public static Rgba32? DetectBorderColor(PixelImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        var bins = new Dictionary<int, BorderBin>();
        foreach (var pixel in BorderPixels(image))
        {
            if (pixel.A == 0) continue;
            int key = BinKey(pixel);
            var bin = bins.GetValueOrDefault(key);
            bins[key] = new BorderBin(bin.Count + 1, bin.R + pixel.R, bin.G + pixel.G, bin.B + pixel.B);
        }
        if (bins.Count == 0) return null;
        var winner = bins.OrderByDescending(pair => pair.Value.Count).ThenBy(pair => pair.Key).First().Value;
        return new Rgba32(Round(winner.R / winner.Count), Round(winner.G / winner.Count),
            Round(winner.B / winner.Count));
    }

    private static void Validate(BackgroundRemovalOptions options)
    {
        if (!Enum.IsDefined(options.Mode))
            throw new ArgumentOutOfRangeException(nameof(options), "Unknown background removal mode.");
        if (!double.IsFinite(options.Tolerance) || options.Tolerance is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(options), "Tolerance must be finite and between 0 and 100.");
        if (options.Mode == BackgroundRemovalMode.PickColor && options.BackgroundColor is null)
            throw new ArgumentException("PickColor mode requires a background color.", nameof(options));
    }

    private static bool[] ConnectedToBorder(bool[] candidates, int width, int height)
    {
        var visited = new bool[candidates.Length];
        var queue = new Queue<int>();
        void Enqueue(int index)
        {
            if (!candidates[index] || visited[index]) return;
            visited[index] = true;
            queue.Enqueue(index);
        }

        for (int x = 0; x < width; x++)
        {
            Enqueue(x);
            if (height > 1) Enqueue((height - 1) * width + x);
        }
        for (int y = 1; y < height - 1; y++)
        {
            Enqueue(y * width);
            if (width > 1) Enqueue(y * width + width - 1);
        }

        while (queue.Count > 0)
        {
            int index = queue.Dequeue();
            int x = index % width;
            if (x > 0) Enqueue(index - 1);
            if (x + 1 < width) Enqueue(index + 1);
            if (index >= width) Enqueue(index - width);
            if (index + width < candidates.Length) Enqueue(index + width);
        }
        return visited;
    }

    private static PixelImage CopyCanonical(PixelImage image)
    {
        var copy = new PixelImage(image.Width, image.Height);
        for (int index = 0; index < image.Pixels.Length; index++)
            copy.Pixels[index] = image.Pixels[index].A == 0 ? default : image.Pixels[index];
        return copy;
    }

    private static IEnumerable<Rgba32> BorderPixels(PixelImage image)
    {
        for (int x = 0; x < image.Width; x++) yield return image[x, 0];
        if (image.Height > 1)
            for (int x = 0; x < image.Width; x++) yield return image[x, image.Height - 1];
        for (int y = 1; y < image.Height - 1; y++)
        {
            yield return image[0, y];
            if (image.Width > 1) yield return image[image.Width - 1, y];
        }
    }

    private static double DistanceSquared(Rgba32 first, Rgba32 second)
    {
        double red = first.R - second.R, green = first.G - second.G, blue = first.B - second.B;
        return red * red + green * green + blue * blue;
    }

    private static int BinKey(Rgba32 color) =>
        ((color.R >> ColorShift) << 10) | ((color.G >> ColorShift) << 5) | (color.B >> ColorShift);
    private static byte Round(double value) => (byte)Math.Clamp(Math.Floor(value + 0.5), 0, 255);
    private readonly record struct BorderBin(int Count = 0, double R = 0, double G = 0, double B = 0);
}
