namespace PixelGridRecovery.Core;

public sealed record BackgroundRemovalOptions
{
    public BackgroundRemovalMode Mode { get; init; } = BackgroundRemovalMode.None;
    public Rgba32? BackgroundColor { get; init; }
    public double Tolerance { get; init; } = 20;
    public bool BorderConnectedOnly { get; init; } = true;
}
