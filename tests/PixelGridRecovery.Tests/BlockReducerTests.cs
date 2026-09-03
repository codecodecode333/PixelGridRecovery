using PixelGridRecovery.Core;

namespace PixelGridRecovery.Tests;

public sealed class BlockReducerTests
{
    private readonly BlockReducer reducer = new();

    [Fact]
    public void CenterUsesLowerRightOfFourCentralPixels()
    {
        var image = new PixelImage(4, 2);
        image[1, 1] = new Rgba32(10, 20, 30, 40);
        image[3, 1] = new Rgba32(50, 60, 70, 80);
        var result = reducer.Reduce(image, 2, 2, BlockReductionMode.Center);
        Assert.Equal(2, result.Width);
        Assert.Equal(1, result.Height);
        Assert.Equal(image[1, 1], result[0, 0]);
        Assert.Equal(image[3, 1], result[1, 0]);
    }

    [Fact]
    public void AverageComputesChannelsAndRoundsHalfUp()
    {
        var image = Row(new(10, 20, 30), new(21, 41, 61));
        Assert.Equal(new Rgba32(16, 31, 46), reducer.Reduce(image, 2, 1, BlockReductionMode.Average)[0, 0]);
    }

    [Fact]
    public void AverageWeightsColorByAlphaAndAveragesAlphaOverAllPixels()
    {
        var image = Row(new(255, 0, 0, 255), new(0, 0, 255, 85), new(0, 255, 0, 0), new(0, 255, 0, 0));
        Assert.Equal(new Rgba32(191, 0, 64, 85), reducer.Reduce(image, 4, 1, BlockReductionMode.Average)[0, 0]);
    }

    [Fact]
    public void MedianIsPerChannelAndIgnoresOutliers()
    {
        var image = Row(new(10, 90, 50, 20), new(20, 10, 70, 60), new(255, 30, 10, 100));
        Assert.Equal(new Rgba32(20, 30, 50, 60), reducer.Reduce(image, 3, 1)[0, 0]);
    }

    [Fact]
    public void EvenMedianAveragesTwoMiddleSamples()
    {
        var image = Row(new(10, 20, 30, 10), new(21, 41, 61, 21));
        Assert.Equal(new Rgba32(16, 31, 46, 16), reducer.Reduce(image, 2, 1)[0, 0]);
    }

    [Fact]
    public void MedianExcludesHiddenRgbButIncludesTransparentAlpha()
    {
        var image = Row(new(255, 0, 255, 0), new(20, 40, 60, 128), new(20, 40, 60, 255));
        Assert.Equal(new Rgba32(20, 40, 60, 128), reducer.Reduce(image, 3, 1)[0, 0]);
    }

    [Theory]
    [InlineData(BlockReductionMode.Center)]
    [InlineData(BlockReductionMode.Average)]
    [InlineData(BlockReductionMode.Median)]
    public void FullyTransparentOutputHasCanonicalZeroRgb(BlockReductionMode mode)
    {
        var image = Row(new(200, 90, 20, 0), new(10, 220, 30, 0));
        Assert.Equal(default, reducer.Reduce(image, 2, 1, mode)[0, 0]);
    }

    [Fact]
    public void MostlyTransparentMedianRemainsTransparent()
    {
        var image = Row(new(0, 255, 0, 0), new(0, 255, 0, 0), new(255, 0, 0, 255));
        Assert.Equal(default, reducer.Reduce(image, 3, 1)[0, 0]);
    }

    [Theory]
    [InlineData(BlockReductionMode.Center)]
    [InlineData(BlockReductionMode.Average)]
    [InlineData(BlockReductionMode.Median)]
    public void UniformBlocksRestoreOriginalColorsAndResolution(BlockReductionMode mode)
    {
        var image = SyntheticImages.Blocks(cellWidth: 7, cellHeight: 9);
        var result = reducer.Reduce(image, 7, 9, mode);
        Assert.Equal(8, result.Width);
        Assert.Equal(8, result.Height);
        for (int y = 0; y < 8; y++)
        for (int x = 0; x < 8; x++)
            Assert.Equal(image[x * 7, y * 9], result[x, y]);
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(2, -1)]
    [InlineData(3, 2)]
    [InlineData(2, 3)]
    [InlineData(10, 10)]
    public void RejectsInvalidCellsOrUnalignedInput(int width, int height) =>
        Assert.ThrowsAny<ArgumentException>(() => reducer.Reduce(new PixelImage(4, 4), width, height));

    [Fact]
    public void RejectsUnknownMode() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => reducer.Reduce(new PixelImage(4, 4), 2, 2, (BlockReductionMode)999));

    [Fact]
    public void ServiceRestoresOffsetImageWithoutChangingInput()
    {
        var image = SyntheticImages.Blocks(offsetX: 3, offsetY: 7, trailingX: 4, trailingY: 2);
        var service = new ImageProcessingService();
        var grid = service.Detect(image);
        var result = service.Process(image, grid);
        Assert.Equal(new GridBounds(3, 7, 80, 80), result.CropBounds);
        Assert.Equal(8, result.Output.Width);
        Assert.Equal(8, result.Output.Height);
        for (int y = 0; y < 8; y++)
        for (int x = 0; x < 8; x++)
            Assert.Equal(image[3 + x * 10, 7 + y * 10], result.Output[x, y]);
        Assert.Equal(87, image.Width);
        Assert.Equal(89, image.Height);
    }

    private static PixelImage Row(params Rgba32[] colors)
    {
        var image = new PixelImage(colors.Length, 1);
        for (int i = 0; i < colors.Length; i++)
            image[i, 0] = colors[i];
        return image;
    }
}
