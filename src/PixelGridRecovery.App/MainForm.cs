using System.Runtime.InteropServices;
using PixelGridRecovery.Core;

namespace PixelGridRecovery.App;

public sealed partial class MainForm : Form
{
    private readonly ImageProcessingService service = new();
    private PixelImage? original;
    private ProcessingResult? result;
    private Bitmap? originalBitmap;
    private Bitmap? resultBitmap;
    private string? sourcePath;
    private bool updating;
    private double confidence;

    public MainForm()
    {
        BuildLayout();
        modeInput.DataSource = Enum.GetValues<BlockReductionMode>();
        modeInput.SelectedItem = BlockReductionMode.Median;
        loadButton.Click += (_, _) => LoadImage();
        detectButton.Click += (_, _) => RunOperation(AutoDetect);
        previewButton.Click += (_, _) => RunOperation(PreviewResult);
        exportButton.Click += (_, _) => ExportPng();
        foreach (var input in new[] { cellWidthInput, cellHeightInput, offsetXInput, offsetYInput })
            input.ValueChanged += (_, _) => GridChanged();
        modeInput.SelectedValueChanged += (_, _) => InvalidateResult();
        overlayInput.CheckedChanged += (_, _) => originalPreview.ShowGrid = overlayInput.Checked;
        settings.Enabled = detectButton.Enabled = previewButton.Enabled = exportButton.Enabled = false;
    }

    private GridInfo CurrentGrid => new((int)cellWidthInput.Value, (int)cellHeightInput.Value,
        (int)offsetXInput.Value, (int)offsetYInput.Value, confidence);

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
            confidenceLabel.Text = "Confidence: —";
            InvalidateResult();
            UpdateGridDisplay();
            statusLabel.Text = "Auto Detect를 누르거나 격자 값을 직접 입력하세요.";
        });
    }

    private void AutoDetect()
    {
        if (original is null)
            return;
        var detected = service.Detect(original);
        updating = true;
        cellWidthInput.Value = detected.CellWidth;
        cellHeightInput.Value = detected.CellHeight;
        UpdateOffsetLimits();
        offsetXInput.Value = detected.OffsetX;
        offsetYInput.Value = detected.OffsetY;
        updating = false;
        confidence = detected.Confidence;
        confidenceLabel.Text = $"Confidence: {confidence:P0}";
        InvalidateResult();
        UpdateGridDisplay();
        statusLabel.Text = confidence < 0.5
            ? "검출 신뢰도가 낮습니다. 격자 값을 확인하고 직접 수정할 수 있습니다."
            : $"Grid: {detected.CellWidth} × {detected.CellHeight} · Offset: {detected.OffsetX}, {detected.OffsetY}";
    }

    private void GridChanged()
    {
        if (updating || original is null)
            return;
        updating = true;
        UpdateOffsetLimits();
        updating = false;
        confidence = 0;
        confidenceLabel.Text = "Confidence: 수동 설정";
        InvalidateResult();
        UpdateGridDisplay();
    }

    private void UpdateOffsetLimits()
    {
        offsetXInput.Maximum = cellWidthInput.Value - 1;
        offsetYInput.Maximum = cellHeightInput.Value - 1;
    }

    private void UpdateGridDisplay()
    {
        if (original is null)
            return;
        originalPreview.Grid = CurrentGrid;
        try
        {
            var bounds = service.GetCropBounds(original, CurrentGrid);
            croppedSizeLabel.Text = $"Cropped: {bounds.Width} × {bounds.Height}";
            outputSizeLabel.Text = $"Output: {bounds.Width / CurrentGrid.CellWidth} × {bounds.Height / CurrentGrid.CellHeight}";
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
        result = null;
        resultPreview.PreviewImage = null;
        resultBitmap?.Dispose();
        resultBitmap = null;
        exportButton.Enabled = false;
        if (original is not null)
            statusLabel.Text = "설정이 변경되었습니다. Preview Result를 눌러 결과를 갱신하세요.";
    }

    private void PreviewResult()
    {
        if (original is null)
            return;
        var processed = service.Process(original, CurrentGrid, (BlockReductionMode)modeInput.SelectedItem!);
        var bitmap = BitmapCodec.ToBitmap(processed.Output);
        resultPreview.PreviewImage = null;
        resultBitmap?.Dispose();
        result = processed;
        resultBitmap = bitmap;
        resultPreview.PreviewImage = bitmap;
        exportButton.Enabled = true;
        statusLabel.Text = $"복원 완료: {processed.Output.Width} × {processed.Output.Height} px · PNG로 저장할 수 있습니다.";
    }

    private void ExportPng()
    {
        if (result is null)
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
            BitmapCodec.SavePng(result.Output, dialog.FileName);
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
