using System.Runtime.ExceptionServices;
using PixelGridRecovery.App;
using PixelGridRecovery.Core;

namespace PixelGridRecovery.App.Tests;

public sealed class MainFormLayoutTests
{
    [Theory]
    [InlineData(980, 650)]
    [InlineData(1220, 740)]
    public void FormConstructsAndRendersAtSupportedWindowSizes(int width, int height)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var form = new MainForm { ClientSize = new Size(width, height) };
                var controls = Descendants(form).ToArray();
                _ = form.Handle;
                foreach (var control in controls)
                {
                    _ = control.Handle;
                    control.PerformLayout();
                }
                form.PerformLayout();
                using var rendered = new Bitmap(form.Width, form.Height);
                form.DrawToBitmap(rendered, new Rectangle(Point.Empty, rendered.Size));
                var colors = new HashSet<int>();
                for (int y = 100; y < height - 100; y += 5)
                for (int x = 50; x < width - 50; x += 5)
                    colors.Add(rendered.GetPixel(x, y).ToArgb());
                Assert.True(colors.Count > 5, "Child controls did not render.");
                var buttons = controls.OfType<Button>().ToArray();
                Assert.Equal(6, buttons.Length);
                Assert.All(buttons, button => Assert.True(button.Parent!.ClientRectangle.Contains(button.Bounds), $"Clipped button: {button.Text}"));
                Assert.Equal(4, controls.OfType<NumericUpDown>().Count());
                Assert.All(controls.OfType<NumericUpDown>(), input =>
                {
                    Assert.Equal(3, input.DecimalPlaces);
                    Assert.Equal(0.05m, input.Increment);
                    input.Value = 18.55m;
                    Assert.Equal(18.55m, input.Value);
                });
                var reductionMode = controls.OfType<ComboBox>().Single(control => control.AccessibleName == "Reduction Mode");
                var backgroundMode = controls.OfType<ComboBox>().Single(control => control.AccessibleName == "Background Removal Mode");
                Assert.Equal(BlockReductionMode.DominantColor, reductionMode.SelectedItem);
                Assert.Equal(BackgroundRemovalMode.None, backgroundMode.SelectedItem);
                var tolerance = controls.OfType<TrackBar>().Single(control => control.AccessibleName == "Background Tolerance");
                Assert.Equal(0, tolerance.Minimum);
                Assert.Equal(100, tolerance.Maximum);
                Assert.Equal(20, tolerance.Value);
                Assert.True(controls.OfType<CheckBox>().Single(control => control.AccessibleName == "Border-connected only").Checked);
                Assert.Contains(buttons, button => button.AccessibleName == "Pick Color");
                Assert.Contains(buttons, button => button.AccessibleName == "Auto Detect Background");
                Assert.Contains(controls, control => control.AccessibleName == "Background Removal");
                Assert.Contains(controls, control => control.AccessibleName == "Detection Method");
                string? output = Environment.GetEnvironmentVariable("PIXELGRID_LAYOUT_PATH");
                if (width == 1220 && !string.IsNullOrWhiteSpace(output))
                    rendered.Save(output, System.Drawing.Imaging.ImageFormat.Png);
            }
            catch (Exception ex) { failure = ex; }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "Form rendering timed out.");
        if (failure is not null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }

    [Fact]
    public void PreviewMapsLetterboxedClientCoordinatesToImagePixels()
    {
        using var bitmap = new Bitmap(1000, 500);
        using var preview = new ImagePreviewControl { ClientSize = new Size(200, 200), PreviewImage = bitmap };

        Assert.False(preview.TryGetImagePixel(new Point(100, 20), out _));
        Assert.True(preview.TryGetImagePixel(new Point(100, 100), out var center));
        Assert.Equal(new Point(500, 250), center);
        Assert.False(preview.TryGetImagePixel(new Point(100, 190), out _));
    }

    [Fact]
    public void PreviewMapsNearestNeighborUpscaleWithoutOffByOne()
    {
        using var bitmap = new Bitmap(10, 10);
        using var preview = new ImagePreviewControl { ClientSize = new Size(200, 200), PreviewImage = bitmap };

        Assert.True(preview.TryGetImagePixel(new Point(31, 31), out var first));
        Assert.Equal(new Point(0, 0), first);
        Assert.True(preview.TryGetImagePixel(new Point(32, 32), out var second));
        Assert.Equal(new Point(1, 1), second);
        Assert.False(preview.TryGetImagePixel(new Point(14, 100), out _));
    }

    private static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (var descendant in Descendants(child))
                yield return descendant;
        }
    }
}
