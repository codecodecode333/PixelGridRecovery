using System.Runtime.InteropServices;
using PixelGridRecovery.Core;

namespace PixelGridRecovery.App;

public sealed partial class MainForm : Form
{
    private readonly ImageProcessingService service = new();
    private PixelImage? original;
    private GridRecoveryResult? recoveredResult;
    private BackgroundRemovalResult? backgroundRemovedResult;
    private Bitmap? originalBitmap;
    private Bitmap? resultBitmap;
    private string? sourcePath;
    private bool updating;
    private double confidence;
    private GridDetectionMethod detectionMethod = GridDetectionMethod.Unknown;
    private Rgba32? pickedBackgroundColor;
    private bool pickingBackgroundColor;

    public MainForm()
    {
        BuildLayout();
        modeInput.DataSource = Enum.GetValues<BlockReductionMode>();
        modeInput.SelectedItem = BlockReductionMode.DominantColor;
        backgroundModeInput.DataSource = Enum.GetValues<BackgroundRemovalMode>();
        backgroundModeInput.SelectedItem = BackgroundRemovalMode.None;
        loadButton.Click += (_, _) => LoadImage();
        detectButton.Click += (_, _) => RunOperation(AutoDetect);
        previewButton.Click += (_, _) => RunOperation(PreviewResult);
        exportButton.Click += (_, _) => ExportPng();
        foreach (var input in new[] { cellWidthInput, cellHeightInput, offsetXInput, offsetYInput })
            input.ValueChanged += (_, _) => GridChanged();
        modeInput.SelectedValueChanged += (_, _) => InvalidateResult();
        backgroundModeInput.SelectedValueChanged += (_, _) => BackgroundSettingsChanged();
        toleranceInput.ValueChanged += (_, _) =>
        {
            toleranceLabel.Text = $"Tolerance: {toleranceInput.Value}";
            BackgroundSettingsChanged();
        };
        borderConnectedInput.CheckedChanged += (_, _) => BackgroundSettingsChanged();
        autoBackgroundButton.Click += (_, _) => RunOperation(AutoDetectBackground);
        pickBackgroundButton.Click += (_, _) => BeginPickBackgroundColor();
        resultPreview.MouseClick += (_, args) => PickBackgroundColor(args.Location);
        overlayInput.CheckedChanged += (_, _) => originalPreview.ShowGrid = overlayInput.Checked;
        settings.Enabled = detectButton.Enabled = previewButton.Enabled = exportButton.Enabled = false;
        backgroundSettings.Enabled = false;
    }

    private GridGeometry CurrentGrid => new((double)cellWidthInput.Value, (double)cellHeightInput.Value,
        (double)offsetXInput.Value, (double)offsetYInput.Value, confidence, detectionMethod);

    private void LoadImage()
    {
        using var dialog = new OpenFileDialog { Filter = "PNG / JPG images|*.png;*.jpg;*.jpeg", Title = "이미지 불러오기" };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;
        RunOperation(() =>
        {
            var loaded = BitmapCodec.Load(dialog.FileName);
            var bitmap = BitmapCodec.ToBitmap(loaded);
            originalPreview.PreviewImage = null;
            originalBitmap?.Dispose();
            original = loaded;
            originalBitmap = bitmap;
            originalPreview.PreviewImage = bitmap;
            sourcePath = dialog.FileName;
            fileLabel.Text = Path.GetFileName(sourcePath);
            Text = $"PixelGridRecovery · {fileLabel.Text}";
            originalSizeLabel.Text = $"Original: {original.Width} × {original.Height}";
            updating = true;
            cellWidthInput.Maximum = original.Width;
            cellHeightInput.Maximum = original.Height;
            cellWidthInput.Value = Math.Min(10, original.Width);
            cellHeightInput.Value = Math.Min(10, original.Height);
            offsetXInput.Value = offsetYInput.Value = 0;
            UpdateOffsetLimits();
            updating = false;
            settings.Enabled = detectButton.Enabled = true;
            confidence = 0;
            detectionMethod = GridDetectionMethod.Unknown;
            methodLabel.Text = "Method: —";
            confidenceLabel.Text = "Confidence: —";
            ResetBackgroundRemovalControls();
            InvalidateResult();
            UpdateGridDisplay();
            statusLabel.Text = "Auto Detect를 누르거나 격자 값을 직접 입력하세요.";
        });
    }

    private void AutoDetect()
    {
        if (original is null)
            return;
        var detection = service.DetectDetailed(original);
        var detected = detection.Geometry;
        System.Diagnostics.Debug.WriteLine($"Grid detection: X={detection.X}; Y={detection.Y}");
        updating = true;
        cellWidthInput.Value = (decimal)detected.CellWidth;
        cellHeightInput.Value = (decimal)detected.CellHeight;
        UpdateOffsetLimits();
        offsetXInput.Value = Math.Min(offsetXInput.Maximum, (decimal)detected.OffsetX);
        offsetYInput.Value = Math.Min(offsetYInput.Maximum, (decimal)detected.OffsetY);
        updating = false;
        confidence = detected.Confidence;
        detectionMethod = detected.Method;
        methodLabel.Text = $"Method: {detected.Method}";
        confidenceLabel.Text = $"Confidence: {confidence:P0}";
        InvalidateResult();
        UpdateGridDisplay();
        statusLabel.Text = confidence < 0.5
            ? "검출 신뢰도가 낮습니다. 격자 값을 확인하고 직접 수정할 수 있습니다."
            : $"Grid: {detected.CellWidth:F3} × {detected.CellHeight:F3} · Offset: {detected.OffsetX:F3}, {detected.OffsetY:F3}";
    }

    private void GridChanged()
    {
        if (updating || original is null)
            return;
        updating = true;
        UpdateOffsetLimits();
        updating = false;
        confidence = 0;
        detectionMethod = GridDetectionMethod.Manual;
        methodLabel.Text = "Method: Manual";
        confidenceLabel.Text = "Confidence: 수동 설정";
        InvalidateResult();
        UpdateGridDisplay();
    }

    private void UpdateOffsetLimits()
    {
        offsetXInput.Maximum = cellWidthInput.Value - 0.001m;
        offsetYInput.Maximum = cellHeightInput.Value - 0.001m;
    }

    private void UpdateGridDisplay()
    {
        if (original is null)
            return;
        originalPreview.Grid = CurrentGrid;
        try
        {
            var bounds = service.GetCropBounds(original, CurrentGrid);
            croppedSizeLabel.Text = $"Cropped: {bounds.Width:F3} × {bounds.Height:F3}";
            outputSizeLabel.Text = $"Output: {bounds.Columns} × {bounds.Rows}";
            previewButton.Enabled = true;
        }
        catch (ArgumentException)
        {
            croppedSizeLabel.Text = "Cropped: —";
            outputSizeLabel.Text = "Output: —";
            previewButton.Enabled = false;
            statusLabel.Text = "완전한 셀이 남지 않습니다. 셀 크기나 오프셋을 줄여 주세요.";
        }
    }

    private void InvalidateResult()
    {
        if (updating)
            return;
        recoveredResult = null;
        backgroundRemovedResult = null;
        pickingBackgroundColor = false;
        resultPreview.Cursor = Cursors.Default;
        resultPreview.PreviewImage = null;
        resultBitmap?.Dispose();
        resultBitmap = null;
        exportButton.Enabled = false;
        backgroundSettings.Enabled = false;
        if (original is not null)
            statusLabel.Text = "설정이 변경되었습니다. Preview Result를 눌러 결과를 갱신하세요.";
    }

    private void PreviewResult()
    {
        if (original is null)
            return;
        var processed = service.Process(original, CurrentGrid, (BlockReductionMode)modeInput.SelectedItem!);
        recoveredResult = processed;
        backgroundSettings.Enabled = true;
        RefreshBackgroundRemoval();
    }

    private BackgroundRemovalMode CurrentBackgroundMode =>
        (BackgroundRemovalMode)backgroundModeInput.SelectedItem!;

    private BackgroundRemovalOptions CurrentBackgroundOptions => new()
    {
        Mode = CurrentBackgroundMode,
        BackgroundColor = pickedBackgroundColor,
        Tolerance = toleranceInput.Value,
        BorderConnectedOnly = borderConnectedInput.Checked
    };

    private void BackgroundSettingsChanged()
    {
        if (updating) return;
        RunOperation(RefreshBackgroundRemoval);
    }

    private void RefreshBackgroundRemoval()
    {
        if (recoveredResult is null) return;
        pickingBackgroundColor = false;
        resultPreview.Cursor = Cursors.Default;
        if (CurrentBackgroundMode == BackgroundRemovalMode.None)
        {
            backgroundRemovedResult = null;
            ShowResult(recoveredResult.Output);
            exportButton.Enabled = true;
            statusLabel.Text = $"복원 완료: {recoveredResult.Output.Width} × {recoveredResult.Output.Height} px · 배경 제거 안 함";
            return;
        }
        if (CurrentBackgroundMode == BackgroundRemovalMode.PickColor && pickedBackgroundColor is null)
        {
            backgroundRemovedResult = null;
            ShowResult(recoveredResult.Output);
            exportButton.Enabled = false;
            statusLabel.Text = "Pick Color를 누른 뒤 결과 이미지에서 배경색을 선택하세요.";
            return;
        }
        backgroundRemovedResult = service.RemoveBackground(recoveredResult.Output, CurrentBackgroundOptions);
        if (backgroundRemovedResult.BackgroundColor is { } color)
        {
            pickedBackgroundColor = color;
            ShowBackgroundColor(color);
        }
        ShowResult(backgroundRemovedResult.Output);
        exportButton.Enabled = true;
        statusLabel.Text = $"배경 제거 완료: {backgroundRemovedResult.RemovedPixelCount} px 투명화 · PNG alpha 내보내기 가능";
    }

    private void AutoDetectBackground()
    {
        if (recoveredResult is null) return;
        var color = service.DetectBackgroundColor(recoveredResult.Output);
        if (color is null)
        {
            statusLabel.Text = "불투명한 테두리 픽셀이 없어 배경색을 찾지 못했습니다.";
            return;
        }
        pickedBackgroundColor = color;
        ShowBackgroundColor(color.Value);
        updating = true;
        backgroundModeInput.SelectedItem = BackgroundRemovalMode.AutoBorder;
        updating = false;
        RefreshBackgroundRemoval();
    }

    private void BeginPickBackgroundColor()
    {
        if (recoveredResult is null) return;
        pickingBackgroundColor = true;
        ShowResult(recoveredResult.Output);
        exportButton.Enabled = false;
        resultPreview.Cursor = Cursors.Cross;
        statusLabel.Text = "결과 이미지에서 배경으로 사용할 픽셀을 클릭하세요.";
    }

    private void PickBackgroundColor(Point clientPoint)
    {
        if (!pickingBackgroundColor || recoveredResult is null
            || !resultPreview.TryGetImagePixel(clientPoint, out var pixel)) return;
        var color = recoveredResult.Output[pixel.X, pixel.Y];
        if (color.A == 0)
        {
            statusLabel.Text = "이미 투명한 픽셀입니다. 불투명한 배경 픽셀을 선택하세요.";
            return;
        }
        pickedBackgroundColor = new Rgba32(color.R, color.G, color.B);
        ShowBackgroundColor(pickedBackgroundColor.Value);
        pickingBackgroundColor = false;
        resultPreview.Cursor = Cursors.Default;
        updating = true;
        backgroundModeInput.SelectedItem = BackgroundRemovalMode.PickColor;
        updating = false;
        RunOperation(RefreshBackgroundRemoval);
    }

    private void ShowResult(PixelImage image)
    {
        var bitmap = BitmapCodec.ToBitmap(image);
        resultPreview.PreviewImage = null;
        resultBitmap?.Dispose();
        resultBitmap = bitmap;
        resultPreview.PreviewImage = bitmap;
    }

    private void ShowBackgroundColor(Rgba32 color) =>
        backgroundColorLabel.Text = $"Background Color: #{color.R:X2}{color.G:X2}{color.B:X2}";

    private void ResetBackgroundRemovalControls()
    {
        bool wasUpdating = updating;
        updating = true;
        pickedBackgroundColor = null;
        backgroundColorLabel.Text = "Background Color: —";
        backgroundModeInput.SelectedItem = BackgroundRemovalMode.None;
        toleranceInput.Value = 20;
        borderConnectedInput.Checked = true;
        updating = wasUpdating;
    }

    private void ExportPng()
    {
        PixelImage? exportImage = CurrentBackgroundMode == BackgroundRemovalMode.None
            ? recoveredResult?.Output : backgroundRemovedResult?.Output;
        if (exportImage is null)
            return;
        using var dialog = new SaveFileDialog
        {
            Filter = "PNG image|*.png", DefaultExt = "png", AddExtension = true, OverwritePrompt = true,
            FileName = Path.GetFileNameWithoutExtension(sourcePath) + "-recovered.png", Title = "복원 PNG 저장"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;
        RunOperation(() =>
        {
            if (!string.Equals(Path.GetExtension(dialog.FileName), ".png", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("저장 파일의 확장자는 .png여야 합니다.");
            BitmapCodec.SavePng(exportImage, dialog.FileName);
            statusLabel.Text = $"저장 완료: {dialog.FileName}";
        });
    }

    private void RunOperation(Action operation)
    {
        try
        {
            UseWaitCursor = true;
            operation();
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException
            or ExternalException or OutOfMemoryException or NotSupportedException)
        {
            string message = ex is OutOfMemoryException
                ? "이미지를 읽거나 처리할 수 없습니다. 파일 형식과 이미지 크기를 확인해 주세요."
                : ex.Message;
            statusLabel.Text = "처리 실패: " + message;
            MessageBox.Show(this, message, "PixelGridRecovery", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally { updating = false; UseWaitCursor = false; }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            originalPreview.PreviewImage = resultPreview.PreviewImage = null;
            originalBitmap?.Dispose();
            resultBitmap?.Dispose();
        }
        base.Dispose(disposing);
    }
}
