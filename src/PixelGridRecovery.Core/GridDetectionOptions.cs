namespace PixelGridRecovery.Core;

public sealed record GridDetectionOptions
{
    public int MinCellSize { get; init; } = 2;
    public int MaxCellSize { get; init; } = 64;
    public double RefinementRange { get; init; } = 1.5;
    public double CoarsePeriodStep { get; init; } = 0.1;
    public double FinePeriodStep { get; init; } = 0.01;
    public double CoarseOffsetStep { get; init; } = 0.5;
    public double FineOffsetStep { get; init; } = 0.05;
    public double AlignmentTolerance { get; init; } = 0.75;
    public int MaxCoarseCandidates { get; init; } = 6;
}
