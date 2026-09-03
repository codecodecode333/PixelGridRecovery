namespace PixelGridRecovery.Core;

internal sealed class FractionalPeriodRefiner
{
    private const int MinimumSupport = 3;
    private const int FineCandidates = 5;
    private const double FineRange = 0.15;
    private const double MinimumPeakContrast = 0.5;
    private const double MaximumBorderUncertainty = 0.5;
    private const double EquivalentScoreTolerance = 0.002;
    private const double SharpEdgeEquivalentScoreTolerance = 0.04;
    private const double SharpEdgePhasePenalty = 0.1;
    private readonly GridDetectionOptions options;

    public FractionalPeriodRefiner(GridDetectionOptions options)
    {
        this.options = options;
        foreach (double value in new[] { options.RefinementRange, options.CoarsePeriodStep, options.FinePeriodStep,
                     options.CoarseOffsetStep, options.FineOffsetStep, options.AlignmentTolerance })
            if (!double.IsFinite(value) || value <= 0)
                throw new ArgumentOutOfRangeException(nameof(options), "Refinement steps and ranges must be finite and positive.");
        if (options.FinePeriodStep > options.CoarsePeriodStep || options.CoarsePeriodStep > options.RefinementRange
            || options.FineOffsetStep > options.CoarseOffsetStep || options.MaxCoarseCandidates is < 1 or > 16
            || options.FinePeriodStep < 0.001 || options.FineOffsetStep < 0.001)
            throw new ArgumentOutOfRangeException(nameof(options), "Invalid or excessively dense refinement search.");
    }

    public AxisRefinement Refine(double[] raw, int coarsePeriod, int coarseOffset)
    {
        var data = Prepare(raw);
        var empty = new AxisDetectionDiagnostics(coarsePeriod, coarseOffset, 0, coarsePeriod, coarseOffset, 0, 0, 0, 0);
        if (data.Peaks.Length < MinimumSupport)
            return new AxisRefinement(coarsePeriod, coarseOffset, 0, empty);

        var integer = Score(data, coarsePeriod, coarseOffset);
        var seeds = GenerateSeeds(data, coarsePeriod);
        var candidates = new List<Candidate> { integer };
        var scanned = new HashSet<long>();
        foreach (int seed in seeds)
        {
            double lower = Math.Max(options.MinCellSize, seed - options.RefinementRange);
            double upper = Math.Min(options.MaxCellSize, seed + options.RefinementRange);
            for (long index = (long)Math.Ceiling(lower / options.CoarsePeriodStep); index * options.CoarsePeriodStep <= upper + 1e-9; index++)
            {
                if (!scanned.Add(index)) continue;
                double period = index * options.CoarsePeriodStep;
                var best = new Candidate(period, 0);
                for (int phase = 0; phase * options.CoarseOffsetStep < period; phase++)
                    best = Better(best, Score(data, period, phase * options.CoarseOffsetStep));
                candidates.Add(best);
            }
        }

        // Keep several distinct local optima: a single integer seed can be a harmonic.
        var finalists = new List<Candidate>();
        foreach (var candidate in candidates.OrderByDescending(c => c.Value))
        {
            if (finalists.Any(c => Math.Abs(c.Period - candidate.Period) < FineRange)) continue;
            finalists.Add(candidate);
            if (finalists.Count == FineCandidates) break;
        }
        var refined = new List<Candidate>();
        foreach (var candidate in finalists)
        {
            var best = candidate;
            double middleIndex = Math.Round((data.Middle - candidate.Offset) / candidate.Period);
            int steps = (int)Math.Ceiling(FineRange / options.FinePeriodStep);
            for (int step = -steps; step <= steps; step++)
            {
                double period = candidate.Period + step * options.FinePeriodStep;
                if (period < options.MinCellSize || period > options.MaxCellSize) continue;
                double predictedPhase = candidate.Offset + middleIndex * (candidate.Period - period);
                int phaseSteps = (int)Math.Ceiling(options.CoarseOffsetStep * 1.5 / options.FineOffsetStep);
                for (int phase = -phaseSteps; phase <= phaseSteps; phase++)
                    best = Better(best, Score(data, period, Normalize(predictedPhase + phase * options.FineOffsetStep, period)));
            }
            for (int iteration = 0; iteration < 3; iteration++)
            {
                var fitted = FitLattice(data, best);
                if (fitted.Value <= best.Value) break;
                best = fitted;
            }
            refined.Add(best);
        }
        var winner = refined.Aggregate(integer, Better);
        if (winner.Support < MinimumSupport)
            return new AxisRefinement(coarsePeriod, coarseOffset, 0, empty);
        // Compare exact integers with the same objective; there is no integer bonus.
        winner = Better(winner, Score(data, Math.Round(winner.Period), Math.Round(winner.Offset)));
        var anchored = AnchorImageBoundaries(data, winner);
        bool boundaryAnchored = anchored.Period != winner.Period || anchored.Offset != winner.Offset;
        winner = anchored;
        double rival = refined.Where(c => Math.Abs(c.Period - winner.Period) > FineRange)
            .Select(c => c.Value).DefaultIfEmpty(0).Max();
        double separation = Math.Clamp((winner.Value - rival) / 0.15, 0, 1);
        double confidence = winner.Value * Math.Min(1, winner.Support / 8.0)
            * Math.Sqrt(winner.Coverage) * (0.7 + 0.3 * separation) * Math.Min(1, data.Contrast / 5);
        var diagnostics = new AxisDetectionDiagnostics(coarsePeriod, coarseOffset, integer.Value,
            winner.Period, winner.Offset, winner.Value, winner.Support, winner.Coverage, winner.PhaseError, boundaryAnchored);
        return new AxisRefinement(winner.Period, winner.Offset, Math.Clamp(confidence, 0, 1), diagnostics);
    }

    private IEnumerable<int> GenerateSeeds(SignalData data, int coarsePeriod)
    {
        var votes = new Dictionary<int, double>();
        for (int i = 1; i < data.Peaks.Length; i++)
        for (int distance = 1; distance <= Math.Min(3, i); distance++)
        {
            double gap = data.Peaks[i].Position - data.Peaks[i - distance].Position;
            for (int divisor = 1; divisor <= 4; divisor++)
            {
                int period = (int)Math.Round(gap / divisor);
                if (period < options.MinCellSize || period > options.MaxCellSize) continue;
                votes[period] = votes.GetValueOrDefault(period) + 1.0 / (distance * divisor);
            }
        }
        return new[] { coarsePeriod }.Concat(votes.OrderByDescending(v => v.Value).ThenBy(v => v.Key)
            .Take(options.MaxCoarseCandidates).Select(v => v.Key)).Distinct();
    }

    private Candidate Score(SignalData data, double period, double offset)
    {
        if (period < options.MinCellSize || period > options.MaxCellSize || offset < 0 || offset >= period)
            return new Candidate(period, offset);
        double tolerance = Math.Min(options.AlignmentTolerance, period * 0.2);
        double sum = 0, error = 0, weight = 0;
        int support = 0;
        double previousSupportedIndex = double.NegativeInfinity;
        double first = double.PositiveInfinity, last = 0;
        Span<double> aligned = stackalloc double[4];
        Span<double> totals = stackalloc double[4];
        aligned.Clear();
        totals.Clear();
        foreach (var peak in data.Peaks)
        {
            double index = Math.Round((peak.Position - offset) / period);
            double residual = peak.Position - (offset + index * period);
            // A sharp raster edge locates a physical boundary only within half a source pixel.
            double distance = Math.Max(0, Math.Abs(residual) - peak.Uncertainty);
            double phasePenalty = peak.Uncertainty > 0 ? SharpEdgePhasePenalty * residual * residual : 0;
            double closeness = Math.Exp(-0.5 * (distance * distance + phasePenalty) / (tolerance * tolerance));
            int section = Math.Clamp((int)(4 * (peak.Position - data.First) / (data.Last - data.First + 1)), 0, 3);
            totals[section] += peak.Weight;
            aligned[section] += peak.Weight * closeness;
            sum += peak.Weight * closeness;
            if (distance > tolerance) continue;
            if (index != previousSupportedIndex)
            {
                support++;
                previousSupportedIndex = index;
            }
            first = Math.Min(first, peak.Position);
            last = Math.Max(last, peak.Position);
            error += peak.Weight * residual * residual;
            weight += peak.Weight;
        }
        if (support < MinimumSupport) return new Candidate(period, offset);
        double recall = sum / data.TotalWeight;
        double regularity = 1;
        for (int section = 0; section < 4; section++)
            if (totals[section] > 0) regularity = Math.Min(regularity, aligned[section] / totals[section]);
        double boundaryEnergy = 0, interiorEnergy = 0;
        int count = 0;
        int firstIndex = (int)Math.Ceiling((data.First - offset - tolerance) / period);
        int lastIndex = (int)Math.Floor((data.Last - offset + tolerance) / period);
        for (int k = firstIndex; k <= lastIndex; k++)
        {
            double position = offset + k * period;
            if (position <= 0 || position >= data.Values.Length) continue;
            boundaryEnergy += Sample(data.Values, position);
            interiorEnergy += Sample(data.Values, position + period * 0.5);
            count++;
        }
        double density = Math.Min(1, support / (double)Math.Max(1, count));
        double alignment = Math.Min(1, boundaryEnergy / Math.Max(1, support));
        double interiorPenalty = Math.Clamp(interiorEnergy / Math.Max(1e-9, boundaryEnergy), 0, 1);
        double value = recall * recall * (0.65 + 0.35 * density) * (0.9 + 0.1 * alignment)
            * (1 - 0.25 * interiorPenalty) * (0.8 + 0.2 * regularity);
        return new Candidate(period, offset, value, support, Math.Clamp((last - first) / (data.Values.Length - 1), 0, 1), Math.Sqrt(error / weight));
    }

    private Candidate FitLattice(SignalData data, Candidate candidate)
    {
        double w = 0, sx = 0, sy = 0, sxx = 0, sxy = 0;
        foreach (var peak in data.Peaks)
        {
            double index = Math.Round((peak.Position - candidate.Offset) / candidate.Period);
            if (Math.Abs(peak.Position - (candidate.Offset + index * candidate.Period)) > options.AlignmentTolerance) continue;
            w += peak.Weight;
            sx += peak.Weight * index;
            sy += peak.Weight * peak.Position;
            sxx += peak.Weight * index * index;
            sxy += peak.Weight * index * peak.Position;
        }
        double denominator = w * sxx - sx * sx;
        if (denominator <= 0) return candidate;
        double period = (w * sxy - sx * sy) / denominator;
        double offset = Normalize((sy - period * sx) / w, period);
        return Score(data, period, offset);
    }

    private Candidate AnchorImageBoundaries(SignalData data, Candidate candidate)
    {
        bool sharp = data.Peaks.Count(p => p.Uncertainty > 0) >= data.Peaks.Length * 0.8;
        double uncertainty = Math.Min(MaximumBorderUncertainty,
            Math.Max(sharp ? MaximumBorderUncertainty : 0, candidate.PhaseError * 4));
        double scoreTolerance = sharp ? SharpEdgeEquivalentScoreTolerance : EquivalentScoreTolerance;
        bool Equivalent(Candidate proposed) => proposed.Support >= candidate.Support
            && proposed.Value >= candidate.Value * (1 - scoreTolerance);
        // Preserve boundary cells only when source localization and matching scores support an image border.
        if (Math.Min(candidate.Offset, candidate.Period - candidate.Offset) <= uncertainty)
        {
            var origin = Score(data, candidate.Period, 0);
            if (Equivalent(origin)) candidate = origin;
        }
        double cells = Math.Round((data.Values.Length - candidate.Offset) / candidate.Period);
        if (cells > 0 && Math.Abs(candidate.Offset + cells * candidate.Period - data.Values.Length) <= uncertainty)
        {
            var end = Score(data, (data.Values.Length - candidate.Offset) / cells, candidate.Offset);
            if (Equivalent(end)) candidate = end;
        }
        return candidate;
    }

    private static SignalData Prepare(double[] raw)
    {
        var sorted = raw.Skip(1).Order().ToArray();
        if (sorted.Length == 0) return new SignalData(new double[raw.Length], [], 0);
        double floor = sorted[(int)((sorted.Length - 1) * 0.1)];
        var signal = raw.Select(value => Math.Max(0, value - floor)).ToArray();
        signal[0] = 0;
        double threshold = Math.Max(MinimumPeakContrast, (sorted[(int)((sorted.Length - 1) * 0.95)] - floor) * 0.07);
        var peaks = new List<Peak>();
        for (int i = 1; i < signal.Length; i++)
        {
            if (signal[i] < threshold || signal[i] <= signal[i - 1]
                || (i + 1 < signal.Length && signal[i] < signal[i + 1])) continue;
            int left = i, right = i;
            while (left > Math.Max(1, i - 2) && signal[left - 1] < signal[left]) left--;
            while (right < Math.Min(signal.Length - 1, i + 2) && signal[right + 1] <= signal[right]) right++;
            double energy = 0, moment = 0;
            for (int j = left; j <= right; j++) { energy += signal[j]; moment += j * signal[j]; }
            peaks.Add(new Peak(moment / energy, energy, signal[i] / energy >= 0.97 ? 0.5 : 0));
        }
        if (peaks.Count == 0) return new SignalData(signal, [], 0);
        var weights = peaks.Select(p => p.Weight).Order().ToArray();
        double cap = weights[(int)((weights.Length - 1) * 0.75)];
        var smooth = new double[signal.Length];
        for (int i = 1; i < signal.Length; i++)
            smooth[i] = Math.Min(1, (signal[i - 1] + 2 * signal[i] + (i + 1 < signal.Length ? signal[i + 1] : 0)) / (2 * cap));
        return new SignalData(smooth, peaks.Select(p => p with { Weight = Math.Min(p.Weight, cap) }).ToArray(), cap);
    }

    private static double Sample(double[] signal, double position)
    {
        if (position < 0 || position > signal.Length - 1) return 0;
        int left = (int)Math.Floor(position);
        double fraction = position - left;
        return signal[left] * (1 - fraction) + signal[Math.Min(left + 1, signal.Length - 1)] * fraction;
    }

    private static double Normalize(double offset, double period)
    {
        double phase = offset - Math.Floor(offset / period) * period;
        return phase < 1e-8 || period - phase < 1e-8 ? 0 : phase;
    }

    private static Candidate Better(Candidate first, Candidate second) => second.Value > first.Value ? second : first;
    private sealed record SignalData(double[] Values, Peak[] Peaks, double Contrast)
    {
        public double TotalWeight { get; } = Peaks.Sum(p => p.Weight);
        public double First { get; } = Peaks.Length > 0 ? Peaks[0].Position : 0;
        public double Last { get; } = Peaks.Length > 0 ? Peaks[^1].Position : 0;
        public double Middle => (First + Last) / 2;
    }
    private readonly record struct Peak(double Position, double Weight, double Uncertainty);
    private readonly record struct Candidate(double Period, double Offset, double Value = 0, int Support = 0, double Coverage = 0, double PhaseError = 0);
}

internal sealed record AxisRefinement(double Period, double Offset, double Confidence, AxisDetectionDiagnostics Diagnostics);
