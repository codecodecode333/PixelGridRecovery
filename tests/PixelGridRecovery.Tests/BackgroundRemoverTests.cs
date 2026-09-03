using PixelGridRecovery.Core;

namespace PixelGridRecovery.Tests;

public sealed class BackgroundRemoverTests
{
    private static readonly Rgba32 Background = new(200, 200, 200);
    private static readonly Rgba32 Foreground = new(30, 70, 120);
    private readonly BackgroundRemover remover = new();

    [Fact]
    public void SolidBorderBackgroundBecomesTransparent()
    {
        var image = Filled(5, 4, Background);
        for (int y = 1; y < 3; y++)
        for (int x = 1; x < 4; x++) image[x, y] = Foreground;

        var result = remover.Remove(image, Pick(Background, 0));

        Assert.Equal(14, result.RemovedPixelCount);
        Assert.Equal(new Rgba32(0, 0, 0, 0), result.Output[0, 0]);
        Assert.Equal(Foreground, result.Output[2, 2]);
        Assert.Equal(Background, image[0, 0]);
    }

    [Fact]
    public void BorderConnectedModePreservesEnclosedMatchingColor()
    {
        var image = Filled(7, 5, Background);
        for (int y = 1; y < 4; y++)
        for (int x = 1; x < 6; x++) image[x, y] = Foreground;
        image[3, 2] = Background;

        var result = remover.Remove(image, Pick(Background, 0));

        Assert.Equal(Background, result.Output[3, 2]);
        Assert.Equal(default, result.Output[0, 0]);
    }

    [Fact]
    public void BorderConnectionUsesFourNeighborsOnly()
    {
        var image = Filled(3, 3, Foreground);
        image[0, 0] = Background;
        image[1, 1] = Background;

        var result = remover.Remove(image, Pick(Background, 0));

        Assert.Equal(default, result.Output[0, 0]);
        Assert.Equal(Background, result.Output[1, 1]);
    }

    [Fact]
    public void AggressiveModeRemovesEnclosedMatchingColor()
    {
        var image = Filled(7, 5, Background);
        for (int y = 1; y < 4; y++)
        for (int x = 1; x < 6; x++) image[x, y] = Foreground;
        image[3, 2] = Background;

        var result = remover.Remove(image, Pick(Background, 0) with { BorderConnectedOnly = false });

        Assert.Equal(default, result.Output[3, 2]);
        Assert.Equal(21, result.RemovedPixelCount);
    }

    [Fact]
    public void ToleranceUsesNormalizedRgbEuclideanDistance()
    {
        var image = new PixelImage(3, 1);
        image[0, 0] = new Rgba32(202, 201, 199);
        image[1, 0] = new Rgba32(210, 210, 210);
        image[2, 0] = new Rgba32(240, 240, 240);

        var low = remover.Remove(image, Pick(Background, 1) with { BorderConnectedOnly = false }).Output;
        var medium = remover.Remove(image, Pick(Background, 5) with { BorderConnectedOnly = false }).Output;

        Assert.Equal(default, low[0, 0]);
        Assert.Equal(image[1, 0], low[1, 0]);
        Assert.Equal(default, medium[0, 0]);
        Assert.Equal(default, medium[1, 0]);
        Assert.Equal(image[2, 0], medium[2, 0]);
    }

    [Fact]
    public void AutoBorderUsesDominantQuantizedColorDespiteForegroundTouchingEdge()
    {
        var image = Filled(7, 6, new Rgba32(200, 201, 199));
        image[1, 0] = new Rgba32(202, 200, 201);
        image[3, 0] = image[3, 1] = Foreground;

        var color = BackgroundRemover.DetectBorderColor(image);
        var result = remover.Remove(image, new BackgroundRemovalOptions
        {
            Mode = BackgroundRemovalMode.AutoBorder,
            Tolerance = 1
        });

        Assert.NotNull(color);
        Assert.InRange(color.Value.R, (byte)200, (byte)202);
        Assert.InRange(color.Value.G, (byte)200, (byte)201);
        Assert.InRange(color.Value.B, (byte)199, (byte)201);
        Assert.Equal(Foreground, result.Output[3, 0]);
        Assert.Equal(default, result.Output[0, 0]);
    }

    [Fact]
    public void TransparentPixelsAreCanonicalAndForegroundAlphaIsPreserved()
    {
        var image = Filled(3, 1, Foreground);
        image[0, 0] = new Rgba32(99, 88, 77, 0);
        image[1, 0] = new Rgba32(25, 50, 75, 128);

        var result = remover.Remove(image, Pick(Background, 0));

        Assert.Equal(default, result.Output[0, 0]);
        Assert.Equal(new Rgba32(25, 50, 75, 128), result.Output[1, 0]);
    }

    [Fact]
    public void NoneModeKeepsOpaquePixelsAndColorDistanceIgnoresAlpha()
    {
        var image = new PixelImage(1, 1);
        image[0, 0] = new Rgba32(200, 200, 200, 128);

        Assert.Equal(image[0, 0], remover.Remove(image).Output[0, 0]);
        Assert.Equal(default, remover.Remove(image, Pick(Background, 0)).Output[0, 0]);
    }

    [Fact]
    public void RemovalIsDeterministic()
    {
        var image = Filled(9, 8, Background);
        for (int y = 2; y < 6; y++)
        for (int x = 2; x < 7; x++) image[x, y] = Foreground;
        var first = remover.Remove(image, Pick(Background, 3));
        var second = remover.Remove(image, Pick(Background, 3));

        Assert.Equal(first.BackgroundColor, second.BackgroundColor);
        Assert.Equal(first.RemovedPixelCount, second.RemovedPixelCount);
        AssertImagesEqual(first.Output, second.Output);
    }

    [Fact]
    public void TinyAndFullyTransparentImagesAreSafe()
    {
        var opaque = new PixelImage(1, 1);
        opaque[0, 0] = Background;
        Assert.Equal(default, remover.Remove(opaque, new BackgroundRemovalOptions
        {
            Mode = BackgroundRemovalMode.AutoBorder,
            Tolerance = 0
        }).Output[0, 0]);

        var transparent = new PixelImage(1, 1);
        transparent[0, 0] = new Rgba32(1, 2, 3, 0);
        var result = remover.Remove(transparent, new BackgroundRemovalOptions
        {
            Mode = BackgroundRemovalMode.AutoBorder
        });
        Assert.Null(result.BackgroundColor);
        Assert.Equal(default, result.Output[0, 0]);
    }

    [Fact]
    public void InvalidOptionsAreRejected()
    {
        var image = new PixelImage(1, 1);
        foreach (var options in new[]
        {
            new BackgroundRemovalOptions { Mode = (BackgroundRemovalMode)99 },
            new BackgroundRemovalOptions { Tolerance = double.NaN },
            new BackgroundRemovalOptions { Tolerance = double.PositiveInfinity },
            new BackgroundRemovalOptions { Tolerance = -1 },
            new BackgroundRemovalOptions { Tolerance = 101 }
        })
            Assert.Throws<ArgumentOutOfRangeException>(() => remover.Remove(image, options));
        Assert.Throws<ArgumentException>(() => remover.Remove(image,
            new BackgroundRemovalOptions { Mode = BackgroundRemovalMode.PickColor }));
    }

    [Fact]
    public void FractionalRecoveryThenRemovalPreservesLogicalSpriteAndClearsBackground()
    {
        var logical = FractionalSyntheticImages.Sprite();
        var raster = FractionalSyntheticImages.Rasterize(logical, 18.6, 17.4, 7.35, 12.7);
        var service = new ImageProcessingService();
        var recovered = service.Process(raster, service.DetectGeometry(raster));
        var removed = service.RemoveBackground(recovered.Output, new BackgroundRemovalOptions
        {
            Mode = BackgroundRemovalMode.AutoBorder,
            Tolerance = 0,
            BorderConnectedOnly = true
        });
        var background = logical[0, 0];

        Assert.Equal(logical.Width, removed.Output.Width);
        Assert.Equal(logical.Height, removed.Output.Height);
        for (int y = 0; y < logical.Height; y++)
        for (int x = 0; x < logical.Width; x++)
        {
            if (logical[x, y] == background)
                Assert.Equal(default, removed.Output[x, y]);
            else
                Assert.Equal(logical[x, y], removed.Output[x, y]);
        }
    }

    private static BackgroundRemovalOptions Pick(Rgba32 color, double tolerance) => new()
    {
        Mode = BackgroundRemovalMode.PickColor,
        BackgroundColor = color,
        Tolerance = tolerance,
        BorderConnectedOnly = true
    };

    private static PixelImage Filled(int width, int height, Rgba32 color)
    {
        var image = new PixelImage(width, height);
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++) image[x, y] = color;
        return image;
    }

    private static void AssertImagesEqual(PixelImage expected, PixelImage actual)
    {
        Assert.Equal(expected.Width, actual.Width);
        Assert.Equal(expected.Height, actual.Height);
        for (int y = 0; y < expected.Height; y++)
        for (int x = 0; x < expected.Width; x++) Assert.Equal(expected[x, y], actual[x, y]);
    }
}
