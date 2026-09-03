namespace PixelGridRecovery.Core;

public sealed class GridCropper
{
    public GridBounds GetBounds(PixelImage image, GridInfo grid)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(grid);
        if (grid.CellWidth <= 0 || grid.CellHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(grid), "Cell dimensions must be positive.");
        if (grid.OffsetX < 0 || grid.OffsetX >= grid.CellWidth || grid.OffsetY < 0 || grid.OffsetY >= grid.CellHeight)
            throw new ArgumentOutOfRangeException(nameof(grid), "Each offset must be between zero and its cell size minus one.");
        int columns = Math.Max(0, image.Width - grid.OffsetX) / grid.CellWidth;
        int rows = Math.Max(0, image.Height - grid.OffsetY) / grid.CellHeight;
        if (columns == 0 || rows == 0)
            throw new ArgumentException("The grid leaves no complete cells in this image.", nameof(grid));
        return new GridBounds(grid.OffsetX, grid.OffsetY, columns * grid.CellWidth, rows * grid.CellHeight);
    }

    public PixelImage Crop(PixelImage image, GridInfo grid)
    {
        var bounds = GetBounds(image, grid);
        var result = new PixelImage(bounds.Width, bounds.Height);
        for (int y = 0; y < bounds.Height; y++)
            Array.Copy(image.Pixels, (y + bounds.Y) * image.Width + bounds.X,
                result.Pixels, y * bounds.Width, bounds.Width);
        return result;
    }
}
