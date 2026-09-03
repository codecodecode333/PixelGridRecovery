namespace PixelGridRecovery.Core;

public sealed class ImageProcessingService
{
    private readonly GridDetector detector;
    private readonly GridCropper cropper = new();
    private readonly BlockReducer reducer = new();

    public ImageProcessingService(GridDetectionOptions? options = null) => detector = new GridDetector(options);

    public GridInfo Detect(PixelImage image) => detector.Detect(image);

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
