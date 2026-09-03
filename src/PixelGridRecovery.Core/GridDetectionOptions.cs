namespace PixelGridRecovery.Core;

public sealed record GridDetectionOptions
{
    public int MinCellSize { get; init; } = 2;
    public int MaxCellSize { get; init; } = 64;
}
