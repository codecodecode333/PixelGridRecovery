using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using PixelGridRecovery.Core;

namespace PixelGridRecovery.App;

public static class BitmapCodec
{
    public const long MaxImagePixels = 16_777_216;

    public static PixelImage Load(string path)
    {
        using var bitmap = new Bitmap(path);
        if (bitmap.RawFormat.Guid != ImageFormat.Png.Guid && bitmap.RawFormat.Guid != ImageFormat.Jpeg.Guid)
            throw new ArgumentException("PNG 또는 JPG 이미지를 선택해 주세요.", nameof(path));
        if ((long)bitmap.Width * bitmap.Height > MaxImagePixels)
            throw new ArgumentException($"최대 {MaxImagePixels:N0} 픽셀 이미지를 지원합니다.", nameof(path));
        return FromBitmap(bitmap);
    }

    public static PixelImage FromBitmap(Bitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        using var normalized = bitmap.Clone(new Rectangle(0, 0, bitmap.Width, bitmap.Height), PixelFormat.Format32bppArgb);
        var image = new PixelImage(normalized.Width, normalized.Height);
        var data = normalized.LockBits(new Rectangle(0, 0, normalized.Width, normalized.Height),
            ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var row = new byte[checked(image.Width * 4)];
            for (int y = 0; y < image.Height; y++)
            {
                Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), row, 0, row.Length);
                for (int x = 0; x < image.Width; x++)
                    image[x, y] = new Rgba32(row[x * 4 + 2], row[x * 4 + 1], row[x * 4], row[x * 4 + 3]);
            }
        }
        finally { normalized.UnlockBits(data); }
        return image;
    }

    public static Bitmap ToBitmap(PixelImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        var bitmap = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppArgb);
        try
        {
            var data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                var row = new byte[checked(image.Width * 4)];
                for (int y = 0; y < image.Height; y++)
                {
                    for (int x = 0; x < image.Width; x++)
                    {
                        var pixel = image[x, y];
                        row[x * 4] = pixel.B;
                        row[x * 4 + 1] = pixel.G;
                        row[x * 4 + 2] = pixel.R;
                        row[x * 4 + 3] = pixel.A;
                    }
                    Marshal.Copy(row, 0, IntPtr.Add(data.Scan0, y * data.Stride), row.Length);
                }
            }
            finally { bitmap.UnlockBits(data); }
            return bitmap;
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }

    public static void SavePng(PixelImage image, string path)
    {
        using var bitmap = ToBitmap(image);
        string destination = Path.GetFullPath(path);
        string temporary = Path.Combine(Path.GetDirectoryName(destination)!, $".pixelgrid-{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                bitmap.Save(stream, ImageFormat.Png);
            File.Move(temporary, destination, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }
}
