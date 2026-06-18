using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace FixedWrapDiff;

public sealed partial class Form1 : Form
{
    private readonly TextBox leftPathTextBox = new();
    private readonly TextBox rightPathTextBox = new();
    private readonly NumericUpDown wrapColumnInput = new();
    private readonly NumericUpDown fontSizeInput = new();
    private readonly ComboBox encodingComboBox = new();
    private readonly ComboBox viewModeComboBox = new();
    private readonly SplitContainer resultSplitContainer = new();
    private readonly SyncRichTextBox leftRulerBox = new();
    private readonly SyncRichTextBox rightRulerBox = new();
    private readonly SyncRichTextBox leftTextBox = new();
    private readonly SyncRichTextBox rightTextBox = new();
    private readonly Color differenceBackColor = Color.FromArgb(255, 244, 190);
    private readonly Color characterDifferenceBackColor = Color.FromArgb(255, 205, 205);
    private readonly string[] startupArgs;

    private List<CompareLine> currentCompareLines = [];
    private bool hasCurrentComparison;
    private bool isSynchronizingScroll;

    public Form1(string[]? args = null)
    {
        startupArgs = args ?? [];
        InitializeComponent();
        BuildLayout();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        ApplyStartupArgs();
    }

    private void BuildLayout()
    {
        Text = "FixedWrapDiff";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(800, 400);
        ClientSize = new Size(1100, 650);

        var topPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(8),
            ColumnCount = 7,
            RowCount = 3,
        };
        topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 0));
        topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 0));
        topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 0));
        topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        topPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        topPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        topPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var leftButton = CreateButton("左選択");
        var rightButton = CreateButton("右選択");
        var compareButton = CreateButton("比較");

        leftButton.Click += (_, _) => SelectFile(leftPathTextBox);
        rightButton.Click += (_, _) => SelectFile(rightPathTextBox);
        compareButton.Click += (_, _) => CompareFiles();

        ConfigurePathTextBox(leftPathTextBox);
        ConfigurePathTextBox(rightPathTextBox);

        wrapColumnInput.Minimum = 1;
        wrapColumnInput.Maximum = 100000;
        wrapColumnInput.Value = 100;
        wrapColumnInput.Width = 90;
        wrapColumnInput.ValueChanged += (_, _) => UpdateRulers((int)wrapColumnInput.Value);

        fontSizeInput.Minimum = 8;
        fontSizeInput.Maximum = 24;
        fontSizeInput.Value = 10;
        fontSizeInput.Width = 70;
        fontSizeInput.ValueChanged += (_, _) => ApplyEditorFontSize();

        encodingComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        encodingComboBox.Items.AddRange(["Shift_JIS", "UTF-8"]);
        encodingComboBox.SelectedIndex = 0;
        encodingComboBox.Width = 110;

        viewModeComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        viewModeComboBox.Items.AddRange(["左右表示", "上下表示"]);
        viewModeComboBox.SelectedIndex = 0;
        viewModeComboBox.Width = 110;
        viewModeComboBox.SelectedIndexChanged += (_, _) => ApplyViewModeAndRender();

        AddFilePathRow(topPanel, 0, "左ファイル", leftPathTextBox, leftButton);
        AddFilePathRow(topPanel, 1, "右ファイル", rightPathTextBox, rightButton);

        var optionPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 6, 0, 0),
        };
        AddLabeledControl(optionPanel, "折り返し桁数", wrapColumnInput);
        AddLabeledControl(optionPanel, "フォントサイズ", fontSizeInput);
        AddLabeledControl(optionPanel, "文字コード", encodingComboBox);
        AddLabeledControl(optionPanel, "表示モード", viewModeComboBox);
        optionPanel.Controls.Add(compareButton);
        topPanel.Controls.Add(optionPanel, 0, 2);
        topPanel.SetColumnSpan(optionPanel, 7);

        ConfigureRulerTextBox(leftRulerBox);
        ConfigureRulerTextBox(rightRulerBox);
        ConfigureResultTextBox(leftTextBox);
        ConfigureResultTextBox(rightTextBox);
        leftTextBox.ScrollChanged += (_, _) => SynchronizeScroll(leftTextBox, rightTextBox);
        rightTextBox.ScrollChanged += (_, _) => SynchronizeScroll(rightTextBox, leftTextBox);

        resultSplitContainer.Dock = DockStyle.Fill;
        resultSplitContainer.Orientation = Orientation.Vertical;
        resultSplitContainer.Panel1.Controls.Add(CreateResultPane(leftRulerBox, leftTextBox));
        resultSplitContainer.Panel2.Controls.Add(CreateResultPane(rightRulerBox, rightTextBox));
        ApplyEditorFontSize();
        UpdateRulers((int)wrapColumnInput.Value);

        Controls.Add(resultSplitContainer);
        Controls.Add(topPanel);
    }

    private static void AddFilePathRow(TableLayoutPanel panel, int row, string labelText, TextBox textBox, Button button)
    {
        panel.Controls.Add(new Label
        {
            Text = labelText,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(4, 7, 8, 4),
        }, 0, row);

        panel.Controls.Add(textBox, 1, row);
        panel.SetColumnSpan(textBox, 5);
        panel.Controls.Add(button, 6, row);
    }

    private static void AddLabeledControl(FlowLayoutPanel panel, string labelText, Control control)
    {
        var container = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(4, 0, 12, 4),
        };
        container.Controls.Add(new Label { Text = labelText, AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(0, 6, 0, 0) });
        container.Controls.Add(control);
        panel.Controls.Add(container);
    }

    private static Button CreateButton(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Margin = new Padding(4),
    };

    private static void ConfigurePathTextBox(TextBox textBox)
    {
        textBox.Dock = DockStyle.Fill;
        textBox.ReadOnly = true;
        textBox.MinimumSize = new Size(300, 0);
        textBox.Margin = new Padding(4);
    }

    private static void ConfigureResultTextBox(RichTextBox textBox)
    {
        textBox.Dock = DockStyle.Fill;
        textBox.Font = new Font("Consolas", 10);
        textBox.ReadOnly = true;
        textBox.WordWrap = false;
        textBox.ScrollBars = RichTextBoxScrollBars.Both;
        textBox.HideSelection = false;
        textBox.DetectUrls = false;
    }

    private static void ConfigureRulerTextBox(RichTextBox textBox)
    {
        textBox.Dock = DockStyle.Fill;
        textBox.Font = new Font("Consolas", 10);
        textBox.ReadOnly = true;
        textBox.WordWrap = false;
        textBox.ScrollBars = RichTextBoxScrollBars.None;
        textBox.HideSelection = true;
        textBox.DetectUrls = false;
        textBox.BackColor = Color.FromArgb(245, 245, 245);
        textBox.BorderStyle = BorderStyle.FixedSingle;
        textBox.TabStop = false;
    }

    private static Control CreateResultPane(RichTextBox rulerBox, RichTextBox resultBox)
    {
        var pane = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
        };
        pane.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        pane.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        pane.Controls.Add(rulerBox, 0, 0);
        pane.Controls.Add(resultBox, 0, 1);
        return pane;
    }

    private void SelectFile(TextBox target)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "比較するファイルを選択",
            Filter = "Text files (*.*)|*.*",
            CheckFileExists = true,
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            target.Text = dialog.FileName;
        }
    }

    private void ApplyStartupArgs()
    {
        if (startupArgs.Length < 2)
        {
            return;
        }

        leftPathTextBox.Text = startupArgs[0];
        rightPathTextBox.Text = startupArgs[1];

        if (startupArgs.Length >= 3 && int.TryParse(startupArgs[2], out var wrapColumns))
        {
            wrapColumnInput.Value = Math.Clamp(wrapColumns, (int)wrapColumnInput.Minimum, (int)wrapColumnInput.Maximum);
        }

        CompareFiles();
    }

    private void CompareFiles()
    {
        var leftPath = leftPathTextBox.Text.Trim();
        var rightPath = rightPathTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(leftPath))
        {
            ShowWarning("左ファイルを指定してください。");
            return;
        }

        if (string.IsNullOrWhiteSpace(rightPath))
        {
            ShowWarning("右ファイルを指定してください。");
            return;
        }

        if (!File.Exists(leftPath))
        {
            ShowWarning("左ファイルが存在しません。");
            return;
        }

        if (!File.Exists(rightPath))
        {
            ShowWarning("右ファイルが存在しません。");
            return;
        }

        var wrapColumns = (int)wrapColumnInput.Value;
        if (wrapColumns < 1)
        {
            ShowWarning("折り返し桁数は1以上の整数を指定してください。");
            return;
        }

        try
        {
            var encoding = GetSelectedEncoding();
            var leftLines = BuildVirtualLines(leftPath, wrapColumns, encoding);
            var rightLines = BuildVirtualLines(rightPath, wrapColumns, encoding);
            currentCompareLines = CompareVirtualLines(leftLines, rightLines);
            hasCurrentComparison = true;
            UpdateRulers(wrapColumns);
            ApplyViewModeAndRender();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"読み込みに失敗しました。\n\n{ex.Message}", "FixedWrapDiff", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private Encoding GetSelectedEncoding() =>
        encodingComboBox.SelectedItem?.ToString() == "UTF-8"
            ? new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false)
            : Encoding.GetEncoding("shift_jis");

    private static List<VirtualLine> BuildVirtualLines(string path, int wrapColumns, Encoding encoding)
    {
        var virtualLines = new List<VirtualLine>();
        var originalLineNumber = 0;

        foreach (var line in File.ReadLines(path, encoding))
        {
            originalLineNumber++;

            if (line.Length == 0)
            {
                virtualLines.Add(new VirtualLine(originalLineNumber, 1, 1, 0, string.Empty));
                continue;
            }

            var wrapIndex = 1;
            for (var index = 0; index < line.Length; index += wrapColumns)
            {
                var length = Math.Min(wrapColumns, line.Length - index);
                virtualLines.Add(new VirtualLine(
                    originalLineNumber,
                    wrapIndex,
                    index + 1,
                    index + length,
                    line.Substring(index, length)));
                wrapIndex++;
            }
        }

        return virtualLines;
    }

    private static List<CompareLine> CompareVirtualLines(IReadOnlyList<VirtualLine> leftLines, IReadOnlyList<VirtualLine> rightLines)
    {
        var compareLines = new List<CompareLine>(Math.Max(leftLines.Count, rightLines.Count));
        var max = Math.Max(leftLines.Count, rightLines.Count);

        for (var i = 0; i < max; i++)
        {
            var left = i < leftLines.Count ? leftLines[i] : null;
            var right = i < rightLines.Count ? rightLines[i] : null;
            var existsOnlyLeft = left is not null && right is null;
            var existsOnlyRight = left is null && right is not null;
            var isDifferent = existsOnlyLeft || existsOnlyRight || left?.Text != right?.Text;
            compareLines.Add(new CompareLine(left, right, isDifferent, existsOnlyLeft, existsOnlyRight));
        }

        return compareLines;
    }

    private void ApplyViewModeAndRender()
    {
        resultSplitContainer.Orientation = viewModeComboBox.SelectedItem?.ToString() == "上下表示"
            ? Orientation.Horizontal
            : Orientation.Vertical;

        if (hasCurrentComparison)
        {
            RenderCompareLines();
        }
    }

    private void RenderCompareLines()
    {
        leftTextBox.SuspendLayout();
        rightTextBox.SuspendLayout();
        leftTextBox.Clear();
        rightTextBox.Clear();

        for (var i = 0; i < currentCompareLines.Count; i++)
        {
            var compareLine = currentCompareLines[i];
            AppendCompareLine(leftTextBox, compareLine.Left, compareLine, compareLine.Right, i == currentCompareLines.Count - 1);
            AppendCompareLine(rightTextBox, compareLine.Right, compareLine, compareLine.Left, i == currentCompareLines.Count - 1);
        }

        leftTextBox.SelectionStart = 0;
        rightTextBox.SelectionStart = 0;
        leftTextBox.ResumeLayout();
        rightTextBox.ResumeLayout();
    }

    private void UpdateRulers(int wrapColumns)
    {
        var rulerText = BuildRulerText(wrapColumns);
        SetRulerText(leftRulerBox, rulerText);
        SetRulerText(rightRulerBox, rulerText);
    }

    private static void SetRulerText(RichTextBox target, string text)
    {
        target.Clear();
        target.AppendText(text);
        target.SelectionStart = 0;
    }

    private void ApplyEditorFontSize()
    {
        var fontSize = (float)fontSizeInput.Value;
        var font = new Font("Consolas", fontSize);

        leftTextBox.Font = font;
        rightTextBox.Font = font;
        leftRulerBox.Font = font;
        rightRulerBox.Font = font;

        SetRulerHeight(leftRulerBox);
        SetRulerHeight(rightRulerBox);
    }

    private static void SetRulerHeight(Control rulerBox)
    {
        if (rulerBox.Parent is not TableLayoutPanel pane)
        {
            return;
        }

        var lineHeight = TextRenderer.MeasureText("0", rulerBox.Font).Height;
        pane.RowStyles[0].Height = (lineHeight * 2) + 8;
    }

    private static string BuildRulerText(int wrapColumns)
    {
        var numberLine = new char[MetadataLength + wrapColumns];
        Array.Fill(numberLine, ' ');
        "Column".CopyTo(numberLine);

        for (var column = 10; column <= wrapColumns; column += 10)
        {
            var number = column.ToString();
            var start = MetadataLength + column - number.Length;
            for (var i = 0; i < number.Length && start + i < numberLine.Length; i++)
            {
                numberLine[start + i] = number[i];
            }
        }

        var scaleLine = new StringBuilder(MetadataLength + wrapColumns);
        scaleLine.Append(' ', MetadataLength);
        for (var column = 1; column <= wrapColumns; column++)
        {
            scaleLine.Append(column % 10 == 0 ? '0' : (char)('0' + column % 10));
        }

        return new string(numberLine) + Environment.NewLine + scaleLine;
    }

    private void AppendCompareLine(RichTextBox target, VirtualLine? line, CompareLine compareLine, VirtualLine? otherLine, bool isLast)
    {
        var text = FormatVirtualLine(line);
        var lineStart = target.TextLength;
        target.AppendText(text);
        if (!isLast)
        {
            target.AppendText(Environment.NewLine);
        }

        var lineLength = text.Length;
        if (compareLine.IsDifferent)
        {
            target.Select(lineStart, lineLength);
            target.SelectionBackColor = differenceBackColor;
        }

        if (line is not null && otherLine is not null && line.Text != otherLine.Text)
        {
            HighlightCharacterDifferences(target, lineStart + MetadataLength, line.Text, otherLine.Text);
        }

        target.Select(target.TextLength, 0);
        target.SelectionBackColor = target.BackColor;
    }

    private void HighlightCharacterDifferences(RichTextBox target, int textStart, string text, string otherText)
    {
        var max = Math.Max(text.Length, otherText.Length);
        for (var i = 0; i < max; i++)
        {
            var current = i < text.Length ? text[i] : '\0';
            var other = i < otherText.Length ? otherText[i] : '\0';
            if (current == other || i >= text.Length)
            {
                continue;
            }

            target.Select(textStart + i, 1);
            target.SelectionBackColor = characterDifferenceBackColor;
        }
    }

    private const int MetadataLength = 26;

    private static string FormatVirtualLine(VirtualLine? line)
    {
        if (line is null)
        {
            return "[L------ W--- C---- ----] ";
        }

        return $"[L{line.OriginalLineNumber:000000} W{line.WrapIndex:000} C{line.StartColumn:0000}-{line.EndColumn:0000}] {line.Text}";
    }

    private void SynchronizeScroll(SyncRichTextBox source, SyncRichTextBox target)
    {
        if (isSynchronizingScroll)
        {
            return;
        }

        try
        {
            isSynchronizingScroll = true;
            target.SetScrollPosition(source.GetScrollPosition());
            SyncRulerScroll(source);
            SyncRulerScroll(target);
        }
        finally
        {
            isSynchronizingScroll = false;
        }
    }

    private void SyncRulerScroll(SyncRichTextBox textBox)
    {
        var position = textBox.GetScrollPosition();
        position.Y = 0;

        if (ReferenceEquals(textBox, leftTextBox))
        {
            leftRulerBox.SetScrollPosition(position);
        }
        else if (ReferenceEquals(textBox, rightTextBox))
        {
            rightRulerBox.SetScrollPosition(position);
        }
    }

    private void ShowWarning(string message) =>
        MessageBox.Show(this, message, "FixedWrapDiff", MessageBoxButtons.OK, MessageBoxIcon.Warning);
}

public sealed record VirtualLine(
    int OriginalLineNumber,
    int WrapIndex,
    int StartColumn,
    int EndColumn,
    string Text);

public sealed record CompareLine(
    VirtualLine? Left,
    VirtualLine? Right,
    bool IsDifferent,
    bool ExistsOnlyLeft,
    bool ExistsOnlyRight);

public sealed class SyncRichTextBox : RichTextBox
{
    private const int WmVScroll = 0x0115;
    private const int WmHScroll = 0x0114;
    private const int WmMouseWheel = 0x020A;
    private const int EmGetScrollPos = 0x04DD;
    private const int EmSetScrollPos = 0x04DE;

    public event EventHandler? ScrollChanged;

    public Point GetScrollPosition()
    {
        var point = new Point();
        SendMessage(Handle, EmGetScrollPos, IntPtr.Zero, ref point);
        return point;
    }

    public void SetScrollPosition(Point point)
    {
        SendMessage(Handle, EmSetScrollPos, IntPtr.Zero, ref point);
    }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);

        if (m.Msg is WmVScroll or WmHScroll or WmMouseWheel)
        {
            ScrollChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        ScrollChanged?.Invoke(this, EventArgs.Empty);
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, ref Point lParam);
}
