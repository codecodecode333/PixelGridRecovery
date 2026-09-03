using PixelGridRecovery.Core;

namespace PixelGridRecovery.Tests;

public sealed class GridDetectorTests
{
    [Fact]
    public void DetectsTenPixelCellsIn80By80Image()
    {
        var grid = new GridDetector().Detect(SyntheticImages.Blocks());
        Assert.Equal(10, grid.CellWidth);
        Assert.Equal(10, grid.CellHeight);
        Assert.Equal(0, grid.OffsetX);
        Assert.Equal(0, grid.OffsetY);
        Assert.InRange(grid.Confidence, 0.7, 1);
    }

    [Theory]
    [InlineData(3, 7, 0)]
    [InlineData(3, 7, 4)]
    [InlineData(0, 0, 5)]
    public void FindsOffsetsDespitePaddingAndNoise(int x, int y, int noise)
    {
        var image = SyntheticImages.Blocks(offsetX: x, offsetY: y, noise: noise, trailingX: 3, trailingY: 2);
        var detector = new GridDetector();
        var grid = detector.Detect(image);
        Assert.Equal(10, grid.CellWidth);
        Assert.Equal(10, grid.CellHeight);
        Assert.Equal(x, grid.OffsetX);
        Assert.Equal(y, grid.OffsetY);
        Assert.Equal(grid, detector.Detect(image));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(19)]
    [InlineData(64)]
    public void FindsFundamentalRatherThanDivisorsOrMultiples(int size)
    {
        var grid = new GridDetector().Detect(SyntheticImages.Blocks(cellWidth: size, cellHeight: size));
        Assert.Equal(size, grid.CellWidth);
        Assert.Equal(size, grid.CellHeight);
    }

    [Fact]
    public void Detects1254By1254Example()
    {
        var grid = new GridDetector().Detect(SyntheticImages.Blocks(66, 66, 19, 19));
        Assert.Equal(19, grid.CellWidth);
        Assert.Equal(19, grid.CellHeight);
    }

    [Fact]
    public void AxesAreIndependent()
    {
        var grid = new GridDetector().Detect(SyntheticImages.Blocks(cellWidth: 9, cellHeight: 13, offsetX: 4, offsetY: 6));
        Assert.Equal(new GridInfo(9, 13, 4, 6, grid.Confidence), grid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void AlmostUniformImageHasLowConfidence(int noise)
    {
        var image = new PixelImage(80, 80);
        var random = new Random(43);
        for (int y = 0; y < image.Height; y++)
        for (int x = 0; x < image.Width; x++)
        {
            byte value = (byte)(120 + random.Next(-noise, noise + 1));
            image[x, y] = new Rgba32(value, value, value);
        }
        Assert.InRange(new GridDetector().Detect(image).Confidence, 0, 0.15);
    }

    [Fact]
    public void SingleStrongBoundaryDoesNotMakeAGrid()
    {
        var image = new PixelImage(80, 80);
        for (int y = 0; y < 80; y++)
        for (int x = 0; x < 80; x++)
            image[x, y] = x >= 31 || y >= 27 ? new Rgba32(255, 255, 255) : new Rgba32(0, 0, 0);
        Assert.Equal(0, new GridDetector().Detect(image).Confidence);
    }

    [Fact]
    public void HiddenRgbDoesNotProduceEdges()
    {
        var image = SyntheticImages.Blocks();
        for (int y = 0; y < image.Height; y++)
        for (int x = 0; x < image.Width; x++)
            image[x, y] = image[x, y] with { A = 0 };
        Assert.Equal(0, new GridDetector().Detect(image).Confidence);
    }

    [Fact]
    public void DetectsEdgesDefinedOnlyByAlpha()
    {
        var image = new PixelImage(80, 80);
        for (int y = 0; y < 80; y++)
        for (int x = 0; x < 80; x++)
            image[x, y] = new Rgba32(0, 0, 0, (byte)((x / 10 + y / 10) % 2 * 255));
        var grid = new GridDetector().Detect(image);
        Assert.Equal(10, grid.CellWidth);
        Assert.Equal(10, grid.CellHeight);
    }

    [Fact]
    public void IsolatedOffGridStripeDoesNotHideRepeatedLowContrastEdges()
    {
        var image = SyntheticImages.Blocks();
        for (int y = 0; y < image.Height; y++)
        for (int x = 0; x < image.Width; x++)
        {
            var pixel = image[x, y];
            image[x, y] = x == 37 ? new Rgba32(255, 255, 255)
                : new Rgba32((byte)(pixel.R / 8), (byte)(pixel.G / 8), (byte)(pixel.B / 8));
        }
        var grid = new GridDetector().Detect(image);
        Assert.Equal(10, grid.CellWidth);
        Assert.Equal(10, grid.CellHeight);
        Assert.Equal(0, grid.OffsetX);
        Assert.Equal(0, grid.OffsetY);
    }

    [Fact]
    public void LargeUniformBackgroundStillAllowsSpriteGridDetection()
    {
        var sprite = SyntheticImages.Blocks();
        var image = new PixelImage(483, 487);
        for (int y = 0; y < sprite.Height; y++)
        for (int x = 0; x < sprite.Width; x++)
            image[203 + x, 207 + y] = sprite[x, y];
        var grid = new GridDetector().Detect(image);
        Assert.Equal(10, grid.CellWidth);
        Assert.Equal(10, grid.CellHeight);
        Assert.Equal(3, grid.OffsetX);
        Assert.Equal(7, grid.OffsetY);
    }

    [Fact]
    public void TinyImageReturnsSafeFallback() =>
        Assert.Equal(new GridInfo(1, 1, 0, 0, 0), new GridDetector().Detect(new PixelImage(1, 1)));

    [Theory]
    [InlineData(1, 64)]
    [InlineData(10, 9)]
    public void InvalidSearchRangeIsRejected(int min, int max) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new GridDetector(new GridDetectionOptions { MinCellSize = min, MaxCellSize = max }));
}
