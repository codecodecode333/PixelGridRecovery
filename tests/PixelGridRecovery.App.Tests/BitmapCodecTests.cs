using System.Drawing.Imaging;
using PixelGridRecovery.App;
using PixelGridRecovery.Core;
using PixelGridRecovery.Tests;

namespace PixelGridRecovery.App.Tests;

public sealed class BitmapCodecTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "PixelGridRecovery.Tests", Guid.NewGuid().ToString("N"));

    public BitmapCodecTests() => Directory.CreateDirectory(directory);

    [Fact]
    public void PngRoundTripPreservesRgbaAndReleasesFileLock()
    {
        var image = new PixelImage(3, 1);
        image[0, 0] = new Rgba32(31, 127, 249, 128);
        image[1, 0] = new Rgba32(4, 5, 6, 0);
        image[2, 0] = new Rgba32(100, 150, 200);
        string path = Path.Combine(directory, "alpha.png");
        BitmapCodec.SavePng(image, path);
        var loaded = BitmapCodec.Load(path);
        for (int x = 0; x < 3; x++)
            Assert.Equal(image[x, 0], loaded[x, 0]);
        using var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.True(exclusive.Length > 0);
    }

    [Fact]
    public void LoadsJpegAsOpaquePixels()
    {
        using var bitmap = new Bitmap(11, 7, PixelFormat.Format24bppRgb);
        using (var graphics = Graphics.FromImage(bitmap)) graphics.Clear(Color.CornflowerBlue);
        string path = Path.Combine(directory, "input.jpg");
        bitmap.Save(path, ImageFormat.Jpeg);
        var image = BitmapCodec.Load(path);
        Assert.Equal(11, image.Width);
        Assert.Equal(7, image.Height);
        Assert.Equal(255, image[5, 3].A);
        Assert.InRange(image[5, 3].B, 230, 240);
    }

    [Fact]
    public void CompletePngWorkflowRestores66By66()
    {
        var input = SyntheticImages.Blocks(66, 66, 19, 19);
        string sourcePath = Path.Combine(directory, "input.png");
        string outputPath = Path.Combine(directory, "output.png");
        BitmapCodec.SavePng(input, sourcePath);
        var loaded = BitmapCodec.Load(sourcePath);
        var service = new ImageProcessingService();
        var result = service.Process(loaded, service.Detect(loaded));
        BitmapCodec.SavePng(result.Output, outputPath);
        var exported = BitmapCodec.Load(outputPath);
        Assert.Equal(66, exported.Width);
        Assert.Equal(66, exported.Height);
        for (int y = 0; y < 66; y++)
        for (int x = 0; x < 66; x++)
            Assert.Equal(input[x * 19, y * 19], exported[x, y]);
    }

    [Fact]
    public void FailedExportDoesNotLeaveTemporaryFiles()
    {
        string destination = Path.Combine(directory, "existing-directory");
        Directory.CreateDirectory(destination);
        var error = Record.Exception(() => BitmapCodec.SavePng(new PixelImage(2, 2), destination));
        Assert.True(error is IOException or UnauthorizedAccessException);
        Assert.True(Directory.Exists(destination));
        Assert.Empty(Directory.GetFiles(directory));
    }

    [Fact]
    public void ExportCanReplaceAnExistingPng()
    {
        string path = Path.Combine(directory, "replaced.png");
        BitmapCodec.SavePng(new PixelImage(2, 2), path);
        BitmapCodec.SavePng(new PixelImage(3, 4), path);
        var loaded = BitmapCodec.Load(path);
        Assert.Equal(3, loaded.Width);
        Assert.Equal(4, loaded.Height);
        Assert.Single(Directory.GetFiles(directory));
    }

    [Fact]
    public void MalformedPngIsRejected()
    {
        string path = Path.Combine(directory, "broken.png");
        File.WriteAllText(path, "This is not an image.");
        Assert.Throws<ArgumentException>(() => BitmapCodec.Load(path));
    }

    [Fact]
    public void RejectsOtherImageFormats()
    {
        string path = Path.Combine(directory, "input.gif");
        using var bitmap = new Bitmap(2, 2);
        bitmap.Save(path, ImageFormat.Gif);
        Assert.Throws<ArgumentException>(() => BitmapCodec.Load(path));
    }

    public void Dispose() => Directory.Delete(directory, recursive: true);
}
