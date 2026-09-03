namespace PixelGridRecovery.Core;

public sealed record BackgroundRemovalResult(PixelImage Output, Rgba32? BackgroundColor, int RemovedPixelCount);
