namespace PixelGridRecovery.Core;

public sealed class GridSampler
{
    private const double GeometryTolerance = 1e-9;
    private const int ColorShift = 3;
    private const int AlphaShift = 4;

    public static GridRegion GetRegion(PixelImage image, GridGeometry grid)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(grid);
        if (!double.IsFinite(grid.CellWidth) || !double.IsFinite(grid.CellHeight) || grid.CellWidth < 1 || grid.CellHeight < 1)
            throw new ArgumentOutOfRangeException(nameof(grid), "Cell dimensions must be finite and at least one source pixel.");
        if (!double.IsFinite(grid.OffsetX) || !double.IsFinite(grid.OffsetY)
            || grid.OffsetX < 0 || grid.OffsetX >= grid.CellWidth || grid.OffsetY < 0 || grid.OffsetY >= grid.CellHeight)
            throw new ArgumentOutOfRangeException(nameof(grid), "Each offset must be finite and in [0, cell size).");
        int columns = CountCells(image.Width, grid.OffsetX, grid.CellWidth);
        int rows = CountCells(image.Height, grid.OffsetY, grid.CellHeight);
        if (columns < 1 || rows < 1)
            throw new ArgumentException("The grid leaves no complete cells in this image.", nameof(grid));
        return new GridRegion(grid.OffsetX, grid.OffsetY,
            Math.Min(image.Width - grid.OffsetX, columns * grid.CellWidth),
            Math.Min(image.Height - grid.OffsetY, rows * grid.CellHeight), columns, rows);
    }

    public GridRecoveryResult Recover(PixelImage image, GridGeometry grid,
        BlockReductionMode mode = BlockReductionMode.DominantColor)
    {
        var region = GetRegion(image, grid);
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        var output = new PixelImage(region.Columns, region.Rows);
        var histogram = mode == BlockReductionMode.Median ? new double[1024] : [];
        var colors = new HashSet<Rgba32>();
        var bins = new Dictionary<int, ColorBin>();
        for (int y = 0; y < region.Rows; y++)
        for (int x = 0; x < region.Columns; x++)
        {
            double left = grid.BoundaryX(x), top = grid.BoundaryY(y);
            double right = Math.Min(image.Width, grid.BoundaryX(x + 1));
            double bottom = Math.Min(image.Height, grid.BoundaryY(y + 1));
            if (mode == BlockReductionMode.Center ||
                (grid.CellWidth == 1 && grid.CellHeight == 1 && grid.OffsetX == 0 && grid.OffsetY == 0))
            {
                var center = image[Math.Min(image.Width - 1, (int)Math.Floor((left + right) / 2)),
                    Math.Min(image.Height - 1, (int)Math.Floor((top + bottom) / 2))];
                output[x, y] = center.A == 0 ? default : center;
                continue;
            }
            double red = 0, green = 0, blue = 0, alpha = 0, area = 0, visibleArea = 0;
            Array.Clear(histogram);
            colors.Clear();
            bins.Clear();
            for (int sy = (int)Math.Floor(top); sy < Math.Ceiling(bottom); sy++)
            for (int sx = (int)Math.Floor(left); sx < Math.Ceiling(right); sx++)
            {
                double overlap = (Math.Min(sx + 1, right) - Math.Max(sx, left))
                    * (Math.Min(sy + 1, bottom) - Math.Max(sy, top));
                if (overlap <= 0) continue;
                var pixel = image.Pixels[sy * image.Width + sx];
                if (pixel.A == 0) pixel = default;
                area += overlap;
                if (mode == BlockReductionMode.DominantColor)
                {
                    colors.Add(pixel);
                    int key = BinKey(pixel);
                    var bin = bins.GetValueOrDefault(key);
                    bins[key] = new ColorBin(bin.Weight + overlap, bin.R + overlap * pixel.R,
                        bin.G + overlap * pixel.G, bin.B + overlap * pixel.B, bin.A + overlap * pixel.A);
                }
                else if (mode == BlockReductionMode.Median)
                {
                    histogram[768 + pixel.A] += overlap;
                    if (pixel.A == 0) continue;
                    visibleArea += overlap;
                    histogram[pixel.R] += overlap;
                    histogram[256 + pixel.G] += overlap;
                    histogram[512 + pixel.B] += overlap;
                }
                else
                {
                    double opacity = overlap * pixel.A;
                    red += opacity * pixel.R;
                    green += opacity * pixel.G;
                    blue += opacity * pixel.B;
                    alpha += opacity;
                }
            }
            Rgba32 selected;
            if (mode == BlockReductionMode.DominantColor)
                selected = Dominant(colors, bins);
            else if (mode == BlockReductionMode.Median)
            {
                byte a = Median(histogram, 768, area);
                selected = a == 0 || visibleArea == 0 ? default : new Rgba32(Median(histogram, 0, visibleArea),
                    Median(histogram, 256, visibleArea), Median(histogram, 512, visibleArea), a);
            }
            else
                selected = alpha == 0 ? default : new Rgba32(Round(red / alpha), Round(green / alpha), Round(blue / alpha), Round(alpha / area));
            output[x, y] = selected.A == 0 ? default : selected;
        }
        return new GridRecoveryResult(region, output);
    }

    private static int CountCells(int extent, double offset, double size) =>
        (int)Math.Max(0, Math.Floor((extent - offset + GeometryTolerance * Math.Max(1, extent)) / size));

    private static Rgba32 Dominant(HashSet<Rgba32> colors, Dictionary<int, ColorBin> bins)
    {
        int winningKey = 0;
        var winner = new ColorBin();
        foreach (var (key, bin) in bins)
            if (bin.Weight > winner.Weight || (bin.Weight == winner.Weight && key < winningKey))
            { winningKey = key; winner = bin; }
        Rgba32 selected = default;
        double bestDistance = double.PositiveInfinity;
        foreach (var color in colors)
        {
            if (BinKey(color) != winningKey) continue;
            double distance = Square(color.R - winner.R / winner.Weight) + Square(color.G - winner.G / winner.Weight)
                + Square(color.B - winner.B / winner.Weight) + Square(color.A - winner.A / winner.Weight);
            if (distance < bestDistance || (distance == bestDistance && Packed(color) < Packed(selected)))
            { selected = color; bestDistance = distance; }
        }
        return selected;
    }

    private static byte Median(double[] histogram, int start, double total)
    {
        double cumulative = 0;
        for (int value = 0; value < 256; value++)
        {
            cumulative += histogram[start + value];
            if (histogram[start + value] == 0 || cumulative < total / 2 - 1e-10) continue;
            if (Math.Abs(cumulative - total / 2) <= 1e-10)
                for (int next = value + 1; next < 256; next++)
                    if (histogram[start + next] > 0) return Round((value + next) / 2.0);
            return (byte)value;
        }
        return 0;
    }

    private static int BinKey(Rgba32 c) => ((c.A >> AlphaShift) << 15) | ((c.R >> ColorShift) << 10) | ((c.G >> ColorShift) << 5) | (c.B >> ColorShift);
    private static uint Packed(Rgba32 c) => ((uint)c.A << 24) | ((uint)c.R << 16) | ((uint)c.G << 8) | c.B;
    private static double Square(double value) => value * value;
    private static byte Round(double value) => (byte)Math.Clamp(Math.Floor(value + 0.5), 0, 255);
    private readonly record struct ColorBin(double Weight = 0, double R = 0, double G = 0, double B = 0, double A = 0);
}
