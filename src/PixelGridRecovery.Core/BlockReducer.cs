namespace PixelGridRecovery.Core;

public sealed class BlockReducer
{
    public PixelImage Reduce(PixelImage image, int cellWidth, int cellHeight,
        BlockReductionMode mode = BlockReductionMode.Median)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cellWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cellHeight);
        if (image.Width % cellWidth != 0 || image.Height % cellHeight != 0)
            throw new ArgumentException("Input must contain only complete grid cells.", nameof(image));
        if (!Enum.IsDefined(mode))
            throw new ArgumentOutOfRangeException(nameof(mode));
        if (mode is BlockReductionMode.AreaWeightedAverage or BlockReductionMode.DominantColor)
            return new GridSampler().Recover(image, new GridGeometry(cellWidth, cellHeight, 0, 0), mode).Output;

        var result = new PixelImage(image.Width / cellWidth, image.Height / cellHeight);
        var histogram = mode == BlockReductionMode.Median ? new int[4 * 256] : [];
        for (int y = 0; y < result.Height; y++)
        for (int x = 0; x < result.Width; x++)
        {
            int startX = x * cellWidth;
            int startY = y * cellHeight;
            Rgba32 color;
            if (mode == BlockReductionMode.Center || (cellWidth == 1 && cellHeight == 1))
                color = image.Pixels[(startY + cellHeight / 2) * image.Width + startX + cellWidth / 2];
            else if (mode == BlockReductionMode.Average)
                color = Average(image, startX, startY, cellWidth, cellHeight);
            else
                color = Median(image, startX, startY, cellWidth, cellHeight, histogram);
            result.Pixels[y * result.Width + x] = color.A == 0 ? default : color;
        }
        return result;
    }

    private static Rgba32 Average(PixelImage image, int x, int y, int width, int height)
    {
        long red = 0, green = 0, blue = 0, alpha = 0;
        for (int row = y; row < y + height; row++)
        for (int column = x; column < x + width; column++)
        {
            var color = image.Pixels[row * image.Width + column];
            red += color.R * color.A;
            green += color.G * color.A;
            blue += color.B * color.A;
            alpha += color.A;
        }
        if (alpha == 0)
            return default;
        return new Rgba32(RoundDivide(red, alpha), RoundDivide(green, alpha), RoundDivide(blue, alpha),
            RoundDivide(alpha, (long)width * height));
    }

    private static Rgba32 Median(PixelImage image, int x, int y, int width, int height, int[] histogram)
    {
        Array.Clear(histogram);
        int visible = 0;
        for (int row = y; row < y + height; row++)
        for (int column = x; column < x + width; column++)
        {
            var color = image.Pixels[row * image.Width + column];
            histogram[768 + color.A]++;
            if (color.A == 0)
                continue;
            visible++;
            histogram[color.R]++;
            histogram[256 + color.G]++;
            histogram[512 + color.B]++;
        }
        byte alpha = MedianChannel(histogram, 768, width * height);
        if (visible == 0 || alpha == 0)
            return default;
        return new Rgba32(MedianChannel(histogram, 0, visible), MedianChannel(histogram, 256, visible),
            MedianChannel(histogram, 512, visible), alpha);
    }

    private static byte MedianChannel(int[] histogram, int start, int count)
    {
        int lowerRank = (count - 1) / 2;
        int upperRank = count / 2;
        int cumulative = 0;
        int lower = -1;
        for (int value = 0; value < 256; value++)
        {
            cumulative += histogram[start + value];
            if (lower < 0 && cumulative > lowerRank)
                lower = value;
            if (cumulative > upperRank)
                return (byte)((lower + value + 1) / 2);
        }
        throw new InvalidOperationException("The channel histogram is empty.");
    }

    private static byte RoundDivide(long value, long count) => (byte)((value + count / 2) / count);
}
