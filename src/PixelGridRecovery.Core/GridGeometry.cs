namespace PixelGridRecovery.Core;

public enum GridDetectionMethod
{
    Unknown,
    EdgePeriodicity,
    FractionalRefinement,
    Manual
}

public sealed record GridGeometry(double CellWidth, double CellHeight, double OffsetX, double OffsetY,
    double Confidence = 0, GridDetectionMethod Method = GridDetectionMethod.Manual)
{
    public double BoundaryX(int column) => OffsetX + column * CellWidth;
    public double BoundaryY(int row) => OffsetY + row * CellHeight;

    public static GridGeometry FromInteger(GridInfo grid) => new(grid.CellWidth, grid.CellHeight,
        grid.OffsetX, grid.OffsetY, grid.Confidence, GridDetectionMethod.EdgePeriodicity);
}

public sealed record GridRegion(double X, double Y, double Width, double Height, int Columns, int Rows);

public sealed record AxisDetectionDiagnostics(double CoarsePeriod, double CoarseOffset, double IntegerScore,
    double RefinedPeriod, double RefinedOffset, double RefinedScore, int SupportingBoundaries,
    double SpatialCoverage, double PhaseError, bool ImageBoundaryAnchored = false);

public sealed record GridDetectionResult(GridGeometry Geometry, AxisDetectionDiagnostics X, AxisDetectionDiagnostics Y);
public sealed record GridRecoveryResult(GridRegion Region, PixelImage Output);
