using System.Drawing.Drawing2D;
using PixelGridRecovery.Core;

namespace PixelGridRecovery.App;

public sealed class ImagePreviewControl : Control
{
    private Bitmap? previewImage;
    private GridInfo? grid;
    private bool showGrid;

    public Bitmap? PreviewImage
    {
        get => previewImage;
        set { previewImage = value; Invalidate(); }
    }

    public GridInfo? Grid
    {
        get => grid;
        set { grid = value; Invalidate(); }
    }

    public bool ShowGrid
    {
        get => showGrid;
        set { showGrid = value; Invalidate(); }
    }

    public ImagePreviewControl()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Color.FromArgb(38, 41, 46);
        Dock = DockStyle.Fill;
        TabStop = false;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (previewImage is null)
        {
            TextRenderer.DrawText(e.Graphics, "미리보기 없음", Font, ClientRectangle,
                Color.Silver, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return;
        }

        float scale = Math.Min((ClientSize.Width - 24f) / previewImage.Width,
            (ClientSize.Height - 24f) / previewImage.Height);
        if (scale <= 0)
            return;
        if (scale >= 1)
            scale = MathF.Floor(scale);
        var rect = new RectangleF((ClientSize.Width - previewImage.Width * scale) / 2,
            (ClientSize.Height - previewImage.Height * scale) / 2,
            previewImage.Width * scale, previewImage.Height * scale);

        var saved = e.Graphics.Save();
        try
        {
            e.Graphics.SetClip(rect);
            DrawCheckerboard(e.Graphics, rect);
            e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
            e.Graphics.DrawImage(previewImage, rect, new RectangleF(0, 0, previewImage.Width, previewImage.Height), GraphicsUnit.Pixel);
            e.Graphics.PixelOffsetMode = PixelOffsetMode.Default;
            if (showGrid && grid is { CellWidth: > 0, CellHeight: > 0 })
                DrawGrid(e.Graphics, rect, scale, grid);
        }
        finally { e.Graphics.Restore(saved); }
        using var border = new Pen(Color.FromArgb(110, 116, 125));
        e.Graphics.DrawRectangle(border, rect.X, rect.Y, rect.Width, rect.Height);
    }

    private static void DrawCheckerboard(Graphics graphics, RectangleF rect)
    {
        const int square = 12;
        using var light = new SolidBrush(Color.FromArgb(222, 222, 222));
        using var dark = new SolidBrush(Color.FromArgb(189, 189, 189));
        graphics.FillRectangle(light, rect);
        for (int row = 0; row * square < rect.Height; row++)
        for (int column = 0; column * square < rect.Width; column++)
            if ((row + column) % 2 == 0)
                graphics.FillRectangle(dark, rect.X + column * square, rect.Y + row * square, square, square);
    }

    private static void DrawGrid(Graphics graphics, RectangleF rect, float scale, GridInfo grid)
    {
        using var pen = new Pen(Color.FromArgb(155, 0, 235, 255));
        float stepX = grid.CellWidth * scale;
        float stepY = grid.CellHeight * scale;
        // Avoid painting thousands of indistinguishable lines when zoomed out.
        stepX *= Math.Max(1, MathF.Ceiling(4 / stepX));
        stepY *= Math.Max(1, MathF.Ceiling(4 / stepY));
        for (float x = rect.Left + grid.OffsetX * scale; x <= rect.Right; x += stepX)
            graphics.DrawLine(pen, x, rect.Top, x, rect.Bottom);
        for (float y = rect.Top + grid.OffsetY * scale; y <= rect.Bottom; y += stepY)
            graphics.DrawLine(pen, rect.Left, y, rect.Right, y);
    }
}
