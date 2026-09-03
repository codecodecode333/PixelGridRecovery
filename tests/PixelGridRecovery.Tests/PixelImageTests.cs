using PixelGridRecovery.Core;

namespace PixelGridRecovery.Tests;

public sealed class PixelImageTests
{
    [Fact]
    public void PixelsRetainAllFourChannels()
    {
        var image = new PixelImage(3, 2);
        var color = new Rgba32(20, 40, 60, 80);
        image[2, 1] = color;
        Assert.Equal(color, image[2, 1]);
        Assert.Equal(default, image[0, 0]);
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(3, -1)]
    public void RejectsInvalidDimensions(int width, int height) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new PixelImage(width, height));

    [Theory]
    [InlineData(3, 0)]
    [InlineData(-1, 0)]
    [InlineData(0, 2)]
    public void CoordinatesCannotWrapIntoAnotherRow(int x, int y)
    {
        var image = new PixelImage(3, 2);
        Assert.Throws<ArgumentOutOfRangeException>(() => image[x, y]);
    }
}
