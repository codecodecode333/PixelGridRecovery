namespace PixelGridRecovery.Core;

public sealed class GridDetector
{
    private const int MinimumRepeatedEdges = 3;
    private const double NoiseMultiplier = 3;
    private const double MinimumContrast = 1;
    private const double FullConfidenceContrast = 8;
    private const double StrongEdgeThreshold = 0.2;
    private const double ClearScoreMargin = 0.2;
    private readonly GridDetectionOptions options;

    public GridDetector(GridDetectionOptions? options = null)
    {
        this.options = options ?? new GridDetectionOptions();
        if (this.options.MinCellSize < 2 || this.options.MaxCellSize < this.options.MinCellSize)
            throw new ArgumentOutOfRangeException(nameof(options), "Cell size range must start at 2 or higher.");
    }

    public GridInfo Detect(PixelImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        var horizontal = new double[image.Width];
        var vertical = new double[image.Height];
        for (int y = 0; y < image.Height; y++)
        for (int x = 0; x < image.Width; x++)
        {
            int index = y * image.Width + x;
            var pixel = image.Pixels[index];
            if (x > 0)
                horizontal[x] += Difference(pixel, image.Pixels[index - 1]) / image.Height;
            if (y > 0)
                vertical[y] += Difference(pixel, image.Pixels[index - image.Width]) / image.Width;
        }

        var axisX = DetectAxis(horizontal);
        var axisY = DetectAxis(vertical);
        return new GridInfo(axisX.Period, axisY.Period, axisX.Offset, axisY.Offset,
            Math.Min(axisX.Confidence, axisY.Confidence));
    }

    private static double Difference(Rgba32 a, Rgba32 b)
    {
        // Premultiplied color ignores hidden RGB; alpha edges still contribute.
        return (Math.Abs(a.R * a.A - b.R * b.A) / 255.0
              + Math.Abs(a.G * a.A - b.G * b.A) / 255.0
              + Math.Abs(a.B * a.A - b.B * b.A) / 255.0
              + Math.Abs(a.A - b.A)) / 4;
    }

    private AxisResult DetectAxis(double[] raw)
    {
        var fallback = new AxisResult(Math.Min(options.MinCellSize, raw.Length), 0, 0);
        if (raw.Length < MinimumRepeatedEdges + 1)
            return fallback;

        var sorted = raw.Skip(1).Order().ToArray();
        // Lower quantiles estimate the within-cell noise floor even for 2px cells.
        double lower = Quantile(sorted, 0.2);
        double floor = lower + NoiseMultiplier * (Quantile(sorted, 0.4) - lower);
        var positive = sorted.Select(value => Math.Max(0, value - floor)).Where(value => value >= MinimumContrast).ToArray();
        if (positive.Length < MinimumRepeatedEdges)
            return fallback;

        double cap = Quantile(positive, 0.75);
        if (cap < MinimumContrast)
            return fallback;
        var signal = raw.Select(value => value - floor < MinimumContrast ? 0 : Math.Clamp((value - floor) / cap, 0, 1)).ToArray();
        signal[0] = 0; // There is no observable edge before the first pixel.
        int firstEdge = Array.FindIndex(signal, value => value > 0);
        int lastEdge = Array.FindLastIndex(signal, value => value > 0);
        double total = signal.Sum();
        var best = new Candidate(fallback.Period, 0, 0);
        double runnerUp = 0;
        int maxPeriod = Math.Min(options.MaxCellSize, (raw.Length - 1) / (MinimumRepeatedEdges - 1));

        for (int period = options.MinCellSize; period <= maxPeriod; period++)
        for (int offset = 0; offset < period; offset++)
        {
            int count = 0;
            int hits = 0;
            double captured = 0;
            for (int position = offset == 0 ? period : offset; position < signal.Length; position += period)
            {
                // Empty margins should not favor a larger multiple of a small sprite's grid.
                if (position >= firstEdge && position <= lastEdge)
                    count++;
                captured += signal[position];
                if (signal[position] >= StrongEdgeThreshold)
                    hits++;
            }
            if (hits < MinimumRepeatedEdges)
                continue;

            // Strength penalizes sub-periods; captured energy penalizes skipped boundaries.
            double strength = captured / count;
            double recall = captured / total;
            double score = 2 * strength * recall / (strength + recall);
            var candidate = new Candidate(period, offset, score);
            if (candidate.Score > best.Score)
            {
                runnerUp = best.Score;
                best = candidate;
            }
            else
                runnerUp = Math.Max(runnerUp, candidate.Score);
        }

        if (best.Score == 0)
            return fallback;
        double separation = Math.Clamp((best.Score - runnerUp) / (ClearScoreMargin * best.Score), 0, 1);
        double contrast = Math.Min(1, cap / FullConfidenceContrast);
        double confidence = Math.Clamp(best.Score * (0.5 + 0.5 * separation) * contrast, 0, 1);
        return new AxisResult(best.Period, best.Offset, confidence);
    }

    private static double Quantile(double[] sorted, double fraction) =>
        sorted[(int)((sorted.Length - 1) * fraction)];

    private readonly record struct Candidate(int Period, int Offset, double Score);
    private readonly record struct AxisResult(int Period, int Offset, double Confidence);
}
