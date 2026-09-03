namespace PixelGridRecovery.Core;

public sealed class ImageProcessingService
{
    private readonly GridDetector detector;
    private readonly GridCropper cropper = new();
    private readonly BlockReducer reducer = new();
    private readonly GridSampler sampler = new();
    private readonly BackgroundRemover backgroundRemover = new();

    public ImageProcessingService(GridDetectionOptions? options = null) => detector = new GridDetector(options);

    public GridInfo Detect(PixelImage image) => detector.Detect(image);

    public GridGeometry DetectGeometry(PixelImage image) => detector.DetectGeometry(image);
    public GridDetectionResult DetectDetailed(PixelImage image) => detector.DetectDetailed(image);
    public GridRegion GetCropBounds(PixelImage image, GridGeometry grid) => GridSampler.GetRegion(image, grid);
    public GridRecoveryResult Process(PixelImage image, GridGeometry grid,
        BlockReductionMode mode = BlockReductionMode.DominantColor) => sampler.Recover(image, grid, mode);
    public BackgroundRemovalResult RemoveBackground(PixelImage recovered,
        BackgroundRemovalOptions? options = null) => backgroundRemover.Remove(recovered, options);
    public Rgba32? DetectBackgroundColor(PixelImage recovered) => BackgroundRemover.DetectBorderColor(recovered);

    public GridBounds GetCropBounds(PixelImage image, GridInfo grid) => cropper.GetBounds(image, grid);

    public ProcessingResult Process(PixelImage image, GridInfo grid,
        BlockReductionMode mode = BlockReductionMode.Median)
    {
        var bounds = cropper.GetBounds(image, grid);
        var cropped = cropper.Crop(image, grid);
        return new ProcessingResult(bounds, reducer.Reduce(cropped, grid.CellWidth, grid.CellHeight, mode));
    }
}

public sealed record ProcessingResult(GridBounds CropBounds, PixelImage Output);
