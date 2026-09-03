using PixelGridRecovery.Core;

namespace PixelGridRecovery.Tests;

public sealed class GridSamplerTests
{
    [Fact]
    public void AverageUsesExactOverlapAreaAndAlpha()
    {
        var image = new PixelImage(3, 2);
        for (int y = 0; y < 2; y++)
        {
            image[0, y] = new Rgba32(240, 0, 0, 255);
            image[1, y] = new Rgba32(0, 0, 240, 85);
            image[2, y] = new Rgba32(0, 255, 0, 0);
        }
        var result = new GridSampler().Recover(image, new GridGeometry(2, 1.5, 0.5, 0.25), BlockReductionMode.AreaWeightedAverage);
        // Width weights: red=.5, blue=1, hidden green=.5; alpha=(.5*255+85)/2.
        Assert.Equal(new Rgba32(144, 0, 96, 106), result.Output[0, 0]);
    }

    [Fact]
    public void DominantSelectsAnObservedColorInsteadOfInventedChannelMedian()
    {
        var image = new PixelImage(3, 1);
        image[0, 0] = new Rgba32(200, 10, 10);
        image[1, 0] = new Rgba32(10, 200, 10);
        image[2, 0] = new Rgba32(10, 10, 200);
        var result = new GridSampler().Recover(image, new GridGeometry(3, 1, 0, 0)).Output;
        Assert.Contains(result[0, 0], new[] { image[0, 0], image[1, 0], image[2, 0] });
    }

    [Theory]
    [InlineData(BlockReductionMode.Center)]
    [InlineData(BlockReductionMode.Average)]
    [InlineData(BlockReductionMode.Median)]
    [InlineData(BlockReductionMode.AreaWeightedAverage)]
    [InlineData(BlockReductionMode.DominantColor)]
    public void IntegerSamplingMatchesLegacyReducer(BlockReductionMode mode)
    {
        var image = SyntheticImages.Blocks(cellWidth: 10, cellHeight: 8, noise: 4);
        for (int y = 0; y < image.Height; y++)
        for (int x = 0; x < image.Width; x++)
            image[x, y] = image[x, y] with { A = (byte)((x + y) % 7 == 0 ? 0 : (x + y) % 3 == 0 ? 128 : 255) };
        var expected = new BlockReducer().Reduce(image, 10, 8, mode);
        var actual = new GridSampler().Recover(image, new GridGeometry(10, 8, 0, 0), mode).Output;
        FractionalDetectionTests.AssertRestored(expected, actual);
    }

    [Fact]
    public void FloatingPointRoundoffDoesNotDiscardTheLastCell()
    {
        var region = GridSampler.GetRegion(new PixelImage(186, 186), new GridGeometry(18.6000000003, 18.6, 0, 0));
        Assert.Equal(10, region.Columns);
        Assert.Equal(10, region.Rows);
        Assert.Equal(9, GridSampler.GetRegion(new PixelImage(186, 186), new GridGeometry(18.61, 18.6, 0, 0)).Columns);
    }

    [Theory]
    [InlineData(BlockReductionMode.AreaWeightedAverage)]
    [InlineData(BlockReductionMode.DominantColor)]
    [InlineData(BlockReductionMode.Median)]
    public void TransparentHiddenColorsDoNotLeak(BlockReductionMode mode)
    {
        var image = new PixelImage(4, 4);
        for (int y = 0; y < 4; y++)
        for (int x = 0; x < 4; x++) image[x, y] = new Rgba32(255, 10, 80, 0);
        Assert.Equal(default, new GridSampler().Recover(image, new GridGeometry(2.5, 2.5, 0.3, 0.4), mode).Output[0, 0]);
    }

    [Fact]
    public void ManualFractionalGridRestoresSourceWithoutDetectionOrIntermediateResize()
    {
        var logical = FractionalSyntheticImages.Sprite();
        var input = FractionalSyntheticImages.Rasterize(logical, 18.55, 18.70, 7.35, 12.7);
        var result = new ImageProcessingService().Process(input, new GridGeometry(18.55, 18.70, 7.35, 12.7));
        Assert.Equal(new GridRegion(7.35, 12.7, 64 * 18.55, 64 * 18.70, 64, 64), result.Region);
        FractionalDetectionTests.AssertRestored(logical, result.Output);
    }

    [Theory]
    [InlineData(double.NaN, 2, 0, 0)]
    [InlineData(double.PositiveInfinity, 2, 0, 0)]
    [InlineData(0, 2, 0, 0)]
    [InlineData(2, -1, 0, 0)]
    [InlineData(2, 2, double.NaN, 0)]
    [InlineData(2, 2, -0.1, 0)]
    [InlineData(2, 2, 2, 0)]
    [InlineData(100, 100, 0, 0)]
    public void InvalidGeometryIsRejected(double width, double height, double x, double y) =>
        Assert.ThrowsAny<ArgumentException>(() => new GridSampler().Recover(new PixelImage(10, 10), new GridGeometry(width, height, x, y)));
}
