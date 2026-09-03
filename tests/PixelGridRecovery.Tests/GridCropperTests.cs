using PixelGridRecovery.Core;

namespace PixelGridRecovery.Tests;

public sealed class GridCropperTests
{
    [Fact]
    public void CropsOnlyCompleteCellsAtTheOffset()
    {
        var image = new PixelImage(88, 92);
        image[3, 7] = new Rgba32(10, 20, 30, 40);
        image[82, 86] = new Rgba32(90, 80, 70, 60);
        var grid = new GridInfo(10, 10, 3, 7, 1);
        var cropper = new GridCropper();
        Assert.Equal(new GridBounds(3, 7, 80, 80), cropper.GetBounds(image, grid));
        var result = cropper.Crop(image, grid);
        Assert.Equal(0, result.Width % grid.CellWidth);
        Assert.Equal(0, result.Height % grid.CellHeight);
        Assert.Equal(image[3, 7], result[0, 0]);
        Assert.Equal(image[82, 86], result[79, 79]);
        result[0, 0] = default;
        Assert.NotEqual(default, image[3, 7]);
    }

    [Fact]
    public void RectangularCellsAndExactFitAreSupported()
    {
        var bounds = new GridCropper().GetBounds(new PixelImage(30, 24), new GridInfo(10, 6, 0, 0, 0));
        Assert.Equal(new GridBounds(0, 0, 30, 24), bounds);
    }

    [Theory]
    [InlineData(0, 10, 0, 0)]
    [InlineData(10, -1, 0, 0)]
    [InlineData(10, 10, -1, 0)]
    [InlineData(10, 10, 0, -1)]
    [InlineData(10, 10, 10, 0)]
    [InlineData(10, 10, 0, 10)]
    [InlineData(100, 10, 0, 0)]
    [InlineData(10, 100, 0, 0)]
    [InlineData(60, 60, 40, 40)]
    public void RejectsInvalidOrEmptyGrid(int width, int height, int x, int y) =>
        Assert.ThrowsAny<ArgumentException>(() => new GridCropper().Crop(new PixelImage(80, 80), new GridInfo(width, height, x, y, 0)));
}
