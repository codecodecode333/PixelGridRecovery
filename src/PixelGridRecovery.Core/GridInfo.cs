namespace PixelGridRecovery.Core;

public sealed record GridInfo(
    int CellWidth,
    int CellHeight,
    int OffsetX,
    int OffsetY,
    double Confidence);
