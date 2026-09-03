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
                Assert.Equal(4, buttons.Length);
                Assert.All(buttons, button => Assert.True(button.Parent!.ClientRectangle.Contains(button.Bounds), $"Clipped button: {button.Text}"));
                Assert.Equal(4, controls.OfType<NumericUpDown>().Count());
                Assert.All(controls.OfType<NumericUpDown>(), input =>
                {
                    Assert.Equal(3, input.DecimalPlaces);
                    Assert.Equal(0.05m, input.Increment);
                    input.Value = 18.55m;
                    Assert.Equal(18.55m, input.Value);
                });
                Assert.Equal(BlockReductionMode.DominantColor, controls.OfType<ComboBox>().Single().SelectedItem);
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
