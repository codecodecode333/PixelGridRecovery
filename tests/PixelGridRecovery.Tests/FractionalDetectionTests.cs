using System.Diagnostics;
using PixelGridRecovery.Core;
using Xunit.Abstractions;

namespace PixelGridRecovery.Tests;

public sealed class FractionalDetectionTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(18)]
    [InlineData(19)]
    public void IntegerRegressionUsesExactGeometryAndRestoresEveryPixel(int scale)
    {
        var logical = FractionalSyntheticImages.Sprite();
        var input = FractionalSyntheticImages.Rasterize(logical, scale, scale, mode: Rasterization.Nearest);
        var grid = Detect(input);
        Assert.Equal(scale, grid.CellWidth);
        Assert.Equal(scale, grid.CellHeight);
        Assert.Equal(0, grid.OffsetX);
        Assert.Equal(0, grid.OffsetY);
        AssertRestored(logical, new ImageProcessingService().Process(input, grid).Output);
    }

    [Theory]
    [InlineData(2.5, Rasterization.Nearest)]
    [InlineData(4.25, Rasterization.Nearest)]
    [InlineData(7.5, Rasterization.Nearest)]
    [InlineData(18.4, Rasterization.Nearest)]
    [InlineData(18.6, Rasterization.Nearest)]
    [InlineData(18.75, Rasterization.Nearest)]
    [InlineData(19.2, Rasterization.Nearest)]
    [InlineData(2.5, Rasterization.Area)]
    [InlineData(4.25, Rasterization.Area)]
    [InlineData(7.5, Rasterization.Area)]
    [InlineData(18.4, Rasterization.Area)]
    [InlineData(18.6, Rasterization.Area)]
    [InlineData(18.75, Rasterization.Area)]
    [InlineData(19.2, Rasterization.Area)]
    public void FindsFractionalScaleWithSameColorRuns(double scale, Rasterization rasterization)
    {
        var input = FractionalSyntheticImages.Rasterize(FractionalSyntheticImages.Sprite(), scale, scale, mode: rasterization);
        var grid = Detect(input);
        Assert.InRange(grid.CellWidth, scale - 0.05, scale + 0.05);
        Assert.InRange(grid.CellHeight, scale - 0.05, scale + 0.05);
    }

    [Theory]
    [InlineData(2.5, Rasterization.Nearest)]
    [InlineData(4.25, Rasterization.Nearest)]
    [InlineData(7.5, Rasterization.Nearest)]
    [InlineData(18.6, Rasterization.Nearest)]
    [InlineData(18.75, Rasterization.Nearest)]
    [InlineData(19.2, Rasterization.Nearest)]
    [InlineData(2.5, Rasterization.Area)]
    [InlineData(4.25, Rasterization.Area)]
    [InlineData(7.5, Rasterization.Area)]
    [InlineData(18.6, Rasterization.Area)]
    [InlineData(18.75, Rasterization.Area)]
    [InlineData(19.2, Rasterization.Area)]
    public void NearOriginPhaseDoesNotDiscardAWholeRowOrColumn(double scale, Rasterization mode)
    {
        var logical = FractionalSyntheticImages.Sprite();
        var input = FractionalSyntheticImages.Rasterize(logical, scale, scale, mode: mode);
        var grid = Detect(input);
        foreach (int boundary in new[] { 0, 32, 64 })
        {
            Assert.InRange(Math.Abs(grid.BoundaryX(boundary) - boundary * scale), 0, 0.6);
            Assert.InRange(Math.Abs(grid.BoundaryY(boundary) - boundary * scale), 0, 0.6);
        }
        var recovered = new ImageProcessingService().Process(input, grid).Output;
        Assert.Equal(64, recovered.Width);
        Assert.Equal(64, recovered.Height);
        if (scale >= 7.5) AssertRestored(logical, recovered);
    }

    [Theory]
    [InlineData(Rasterization.Nearest)]
    [InlineData(Rasterization.Area)]
    public void FractionalOffsetsAlignFirstMiddleAndLastBoundariesAndRestoreSprite(Rasterization mode)
    {
        var logical = FractionalSyntheticImages.Sprite();
        var input = FractionalSyntheticImages.Rasterize(logical, 18.6, 18.6, 7.35, 12.7, mode);
        var grid = Detect(input);
        foreach (int index in new[] { 0, 32, 64 })
        {
            double errorX = Math.Abs(grid.BoundaryX(index) - (7.35 + index * 18.6));
            double errorY = Math.Abs(grid.BoundaryY(index) - (12.7 + index * 18.6));
            output.WriteLine($"boundary {index}: X={errorX:F5}px Y={errorY:F5}px");
            Assert.InRange(errorX, 0, 0.6);
            Assert.InRange(errorY, 0, 0.6);
        }
        Assert.True(Math.Abs(7.35 + 64 * 19 - (7.35 + 64 * 18.6)) > 25);
        AssertRestored(logical, new ImageProcessingService().Process(input, grid).Output);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(2, false)]
    [InlineData(4, false)]
    [InlineData(0, true)]
    [InlineData(4, true)]
    public void HandlesNoiseAntialiasingAndIndependentAxes(int noise, bool blur)
    {
        var input = FractionalSyntheticImages.Rasterize(FractionalSyntheticImages.Sprite(), 18.6, 17.4, 7.35, 12.7, noise: noise, blur: blur);
        var grid = Detect(input);
        Assert.InRange(grid.CellWidth, 18.58, 18.62);
        Assert.InRange(grid.CellHeight, 17.38, 17.42);
        Assert.InRange(grid.OffsetX, 6.85, 7.85);
        Assert.InRange(grid.OffsetY, 12.2, 13.2);
    }

    [Fact]
    public void FindsSmallSpriteWithinLargeBackground()
    {
        var input = FractionalSyntheticImages.Rasterize(FractionalSyntheticImages.OnLargeBackground(), 7.5, 7.5, 2.2, 4.3);
        var grid = Detect(input);
        Assert.InRange(grid.CellWidth, 7.45, 7.55);
        Assert.InRange(grid.CellHeight, 7.45, 7.55);
    }

    [Fact]
    public void OneBlurredBoundaryDoesNotBecomeAHighConfidenceGrid()
    {
        var logical = new PixelImage(2, 2);
        logical[0, 0] = new Rgba32(0, 0, 0);
        logical[1, 0] = logical[0, 1] = logical[1, 1] = new Rgba32(255, 255, 255);
        var image = FractionalSyntheticImages.Rasterize(logical, 40, 40, blur: true);
        Assert.Equal(0, Detect(image).Confidence);
    }

    [Fact]
    public void InvalidRefinementSettingsAreRejected()
    {
        foreach (var options in new[]
        {
            new GridDetectionOptions { FinePeriodStep = 0 },
            new GridDetectionOptions { FineOffsetStep = double.NaN },
            new GridDetectionOptions { RefinementRange = double.PositiveInfinity },
            new GridDetectionOptions { AlignmentTolerance = -1 },
            new GridDetectionOptions { FinePeriodStep = 1 },
            new GridDetectionOptions { MaxCoarseCandidates = 0 }
        })
            Assert.Throws<ArgumentOutOfRangeException>(() => new GridDetector(options).DetectGeometry(new PixelImage(8, 8)));
    }

    [Fact]
    public void DetectionIsDeterministic()
    {
        var input = FractionalSyntheticImages.Rasterize(FractionalSyntheticImages.Sprite(32), 18.6, 17.4, 7.35, 12.7);
        var detector = new GridDetector();
        Assert.Equal(detector.DetectDetailed(input), detector.DetectDetailed(input));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(80)]
    public void UniformImageDoesNotInventConfidence(int size)
    {
        var grid = Detect(new PixelImage(size, size));
        Assert.Equal(0, grid.Confidence);
        Assert.Equal(GridDetectionMethod.Unknown, grid.Method);
    }

    private GridGeometry Detect(PixelImage image)
    {
        var watch = Stopwatch.StartNew();
        var result = new GridDetector().DetectDetailed(image);
        output.WriteLine($"{image.Width}x{image.Height}: {result.Geometry}; {watch.ElapsedMilliseconds} ms");
        output.WriteLine($"X {result.X}\nY {result.Y}");
        return result.Geometry;
    }

    internal static void AssertRestored(PixelImage expected, PixelImage actual)
    {
        Assert.Equal(expected.Width, actual.Width);
        Assert.Equal(expected.Height, actual.Height);
        for (int y = 0; y < expected.Height; y++)
        for (int x = 0; x < expected.Width; x++)
            Assert.True(expected[x, y] == actual[x, y], $"Pixel ({x},{y}): expected {expected[x,y]}, got {actual[x,y]}");
    }
}
