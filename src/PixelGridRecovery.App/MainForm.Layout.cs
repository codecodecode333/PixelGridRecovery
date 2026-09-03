namespace PixelGridRecovery.App;

public sealed partial class MainForm
{
    private readonly Button loadButton = MakeButton("Load Image");
    private readonly Button detectButton = MakeButton("Auto Detect");
    private readonly Button previewButton = MakeButton("Preview Result");
    private readonly Button exportButton = MakeButton("Export PNG");
    private readonly NumericUpDown cellWidthInput = MakeNumber("Cell Width", 1, 10);
    private readonly NumericUpDown cellHeightInput = MakeNumber("Cell Height", 1, 10);
    private readonly NumericUpDown offsetXInput = MakeNumber("Offset X", 0, 0);
    private readonly NumericUpDown offsetYInput = MakeNumber("Offset Y", 0, 0);
    private readonly ComboBox modeInput = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Top, AccessibleName = "Reduction Mode" };
    private readonly CheckBox overlayInput = new() { Text = "Grid Overlay 표시", Checked = true, AutoSize = true };
    private readonly Label confidenceLabel = MakeLabel("Confidence: —");
    private readonly Label methodLabel = new() { Text = "Method: —", AutoSize = true, MaximumSize = new Size(210, 0), AccessibleName = "Detection Method", Margin = new Padding(3, 7, 3, 7) };
    private readonly Label originalSizeLabel = MakeLabel("Original: —");
    private readonly Label croppedSizeLabel = MakeLabel("Cropped: —");
    private readonly Label outputSizeLabel = MakeLabel("Output: —");
    private readonly Label fileLabel = new() { AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true, Text = "PNG 또는 JPG 이미지를 불러오세요." };
    private readonly Label statusLabel = new() { Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft, Text = "준비" };
    private readonly ImagePreviewControl originalPreview = new() { ShowGrid = true, AccessibleName = "Original Preview" };
    private readonly ImagePreviewControl resultPreview = new() { AccessibleName = "Result Preview" };
    private readonly TableLayoutPanel settings = new() { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, Padding = new Padding(12) };

    private void BuildLayout()
    {
        Text = "PixelGridRecovery · V0.2";
        Font = new Font("Segoe UI", 10);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(1220, 740);
        MinimumSize = new Size(980, 650);
        StartPosition = FormStartPosition.CenterScreen;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(12) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

        var toolbar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 1 };
        foreach (var button in new[] { loadButton, detectButton, previewButton, exportButton })
        {
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            toolbar.Controls.Add(button);
        }
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        toolbar.Controls.Add(fileLabel);
        root.Controls.Add(toolbar, 0, 0);

        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 244));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        var settingsHost = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.FromArgb(242, 244, 247) };
        settings.Controls.Add(MakeLabel("격자 설정"));
        AddNumber("Cell Width", cellWidthInput);
        AddNumber("Cell Height", cellHeightInput);
        AddNumber("Offset X", offsetXInput);
        AddNumber("Offset Y", offsetYInput);
        settings.Controls.Add(confidenceLabel);
        settings.Controls.Add(methodLabel);
        settings.Controls.Add(overlayInput);
        settings.Controls.Add(MakeLabel("Reduction Mode"));
        settings.Controls.Add(modeInput);
        settings.Controls.Add(MakeLabel("이미지 크기"));
        settings.Controls.Add(originalSizeLabel);
        settings.Controls.Add(croppedSizeLabel);
        settings.Controls.Add(outputSizeLabel);
        var help = MakeLabel("숫자 수정 → 격자 확인 →\nPreview Result → Export PNG\n\n미리보기는 창 크기에 맞춰 표시됩니다.");
        help.MaximumSize = new Size(210, 0);
        help.ForeColor = Color.DimGray;
        settings.Controls.Add(help);
        settingsHost.Controls.Add(settings);
        body.Controls.Add(settingsHost, 0, 0);
        body.Controls.Add(PreviewPanel("Original Preview · 원본 + 격자", originalPreview), 1, 0);
        body.Controls.Add(PreviewPanel("Result Preview · 복원 결과", resultPreview), 2, 0);
        root.Controls.Add(body, 0, 1);
        root.Controls.Add(statusLabel, 0, 2);
        Controls.Add(root);
    }

    private void AddNumber(string label, NumericUpDown input)
    {
        var row = new TableLayoutPanel { Dock = DockStyle.Top, Height = 34, ColumnCount = 2 };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        row.Controls.Add(MakeLabel(label));
        row.Controls.Add(input);
        settings.Controls.Add(row);
    }

    private static Control PreviewPanel(string title, Control preview)
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = new Padding(8, 0, 0, 0) };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.Controls.Add(MakeLabel(title), 0, 0);
        panel.Controls.Add(preview, 0, 1);
        return panel;
    }

    private static Label MakeLabel(string text) => new() { Text = text, AutoSize = true, Margin = new Padding(3, 7, 3, 7) };
    private static Button MakeButton(string text) => new() { Text = text, AutoSize = true, Height = 36, Padding = new Padding(8, 3, 8, 3), Margin = new Padding(0, 3, 8, 3), AccessibleName = text };
    private static NumericUpDown MakeNumber(string name, int minimum, int value) => new() { Minimum = minimum, Maximum = 1_000_000, Value = value, DecimalPlaces = 3, Increment = 0.05m, Dock = DockStyle.Fill, AccessibleName = name };
}
