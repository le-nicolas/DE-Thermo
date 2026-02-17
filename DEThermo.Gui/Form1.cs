using System.Globalization;

namespace DEThermo.Gui;

public partial class Form1 : Form
{
    private readonly Dictionary<string, TextBox> _inputs = new(StringComparer.OrdinalIgnoreCase);
    private Label _zeroLabel = null!;
    private Label _freezeLabel = null!;
    private Label _targetLabel = null!;
    private Label _noteLabel = null!;
    private Panel _plotPanel = null!;
    private SimulationOutput? _lastOutput;

    public Form1()
    {
        InitializeComponent();
        BuildUi();
    }

    private void BuildUi()
    {
        Text = "DE-Thermo GUI";
        Width = 1320;
        Height = 780;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1100, 680);

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            SplitterDistance = 360,
            FixedPanel = FixedPanel.Panel1
        };
        Controls.Add(split);

        var leftPanel = BuildLeftPanel();
        split.Panel1.Controls.Add(leftPanel);

        var rightPanel = BuildRightPanel();
        split.Panel2.Controls.Add(rightPanel);
    }

    private Control BuildLeftPanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(14)
        };

        var title = new Label
        {
            Dock = DockStyle.Top,
            Height = 42,
            Text = "Scenario Inputs",
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };
        panel.Controls.Add(title);

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48));
        panel.Controls.Add(table);

        AddInputRow(table, "Name", "name", "Ceramic Mug");
        AddInputRow(table, "Mass (kg)", "mass_kg", "0.35");
        AddInputRow(table, "Initial Temp (C)", "initial_temp_c", "90");
        AddInputRow(table, "Ambient Temp (C)", "ambient_temp_c", "-18");
        AddInputRow(table, "Area (m2)", "area_m2", "0.03");
        AddInputRow(table, "HTC (W/m2K)", "htc_w_m2k", "8");
        AddInputRow(table, "Target Temp (C)", "target_temp_c", "-12");
        AddInputRow(table, "Duration (s)", "duration_s", "21600");
        AddInputRow(table, "Step (s)", "step_s", "30");

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 46,
            Padding = new Padding(0, 12, 0, 0)
        };
        panel.Controls.Add(actions);

        var runButton = new Button
        {
            Text = "Run Simulation",
            Width = 150,
            Height = 30
        };
        runButton.Click += (_, _) => RunSimulation();
        actions.Controls.Add(runButton);

        var exportButton = new Button
        {
            Text = "Export CSV",
            Width = 110,
            Height = 30
        };
        exportButton.Click += (_, _) => ExportLastRun();
        actions.Controls.Add(exportButton);

        var summary = BuildSummaryGroup();
        summary.Dock = DockStyle.Top;
        summary.Height = 190;
        summary.Padding = new Padding(12, 8, 12, 8);
        summary.Margin = new Padding(0, 12, 0, 0);
        panel.Controls.Add(summary);

        return panel;
    }

    private GroupBox BuildSummaryGroup()
    {
        var box = new GroupBox
        {
            Text = "Milestones"
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4
        };

        _zeroLabel = MakeSummaryLabel("t(0C): -");
        _freezeLabel = MakeSummaryLabel("t(freeze complete): -");
        _targetLabel = MakeSummaryLabel("t(target): -");
        _noteLabel = MakeSummaryLabel("note: -");
        _noteLabel.ForeColor = Color.Firebrick;

        layout.Controls.Add(_zeroLabel, 0, 0);
        layout.Controls.Add(_freezeLabel, 0, 1);
        layout.Controls.Add(_targetLabel, 0, 2);
        layout.Controls.Add(_noteLabel, 0, 3);
        box.Controls.Add(layout);

        return box;
    }

    private static Label MakeSummaryLabel(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            Font = new Font("Segoe UI", 9.75f, FontStyle.Regular),
            Margin = new Padding(2, 10, 2, 2)
        };
    }

    private Control BuildRightPanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10)
        };

        _plotPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(248, 250, 252)
        };
        _plotPanel.Paint += (_, e) => DrawPlot(e.Graphics, _plotPanel.ClientRectangle);
        panel.Controls.Add(_plotPanel);

        return panel;
    }

    private void AddInputRow(TableLayoutPanel table, string labelText, string key, string defaultValue)
    {
        var row = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var label = new Label
        {
            Text = labelText,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(3, 7, 3, 7)
        };

        var box = new TextBox
        {
            Text = defaultValue,
            Width = 140,
            Anchor = AnchorStyles.Left
        };
        _inputs[key] = box;

        table.Controls.Add(label, 0, row);
        table.Controls.Add(box, 1, row);
    }

    private void RunSimulation()
    {
        try
        {
            var culture = CultureInfo.InvariantCulture;
            var scenario = new ScenarioInput
            {
                Name = _inputs["name"].Text.Trim(),
                MassKg = double.Parse(_inputs["mass_kg"].Text, culture),
                InitialTempC = double.Parse(_inputs["initial_temp_c"].Text, culture),
                AmbientTempC = double.Parse(_inputs["ambient_temp_c"].Text, culture),
                AreaM2 = double.Parse(_inputs["area_m2"].Text, culture),
                HtcWm2K = double.Parse(_inputs["htc_w_m2k"].Text, culture)
            };
            var targetTempC = double.Parse(_inputs["target_temp_c"].Text, culture);
            var durationS = double.Parse(_inputs["duration_s"].Text, culture);
            var stepS = double.Parse(_inputs["step_s"].Text, culture);

            var output = ThermoEngine.SimulateTrajectory(scenario, targetTempC, durationS, stepS);
            _lastOutput = output;
            RenderOutput(output);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Invalid input or simulation error.\n\n{ex.Message}",
                "DE-Thermo",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void RenderOutput(SimulationOutput output)
    {
        _zeroLabel.Text = $"t(0C): {Fmt(output.Milestones.ReachesZeroS)}";
        _freezeLabel.Text = $"t(freeze complete): {Fmt(output.Milestones.FreezeCompleteS)}";
        _targetLabel.Text = $"t(target): {Fmt(output.Milestones.ReachesTargetS)}";
        _noteLabel.Text = $"note: {output.Milestones.ReasonUnreachable ?? "ok"}";
        _plotPanel.Invalidate();
    }

    private void ExportLastRun()
    {
        if (_lastOutput is null)
        {
            MessageBox.Show("Run a simulation first.", "DE-Thermo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            FileName = "de_thermo_gui_output.csv"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var culture = CultureInfo.InvariantCulture;
        using var writer = new StreamWriter(dialog.FileName);
        writer.WriteLine("t_s,temp_c");
        foreach (var point in _lastOutput.Points)
        {
            writer.WriteLine($"{point.TS.ToString(culture)},{point.TempC.ToString(culture)}");
        }

        MessageBox.Show($"Saved: {dialog.FileName}", "DE-Thermo", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private static string Fmt(double? seconds)
    {
        return seconds.HasValue ? $"{seconds.Value:F1} s ({seconds.Value / 60.0:F1} min)" : "N/A";
    }

    private void DrawPlot(Graphics g, Rectangle bounds)
    {
        g.Clear(Color.FromArgb(248, 250, 252));
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        var marginLeft = 70f;
        var marginTop = 36f;
        var marginRight = 20f;
        var marginBottom = 56f;
        var plotRect = RectangleF.FromLTRB(
            marginLeft,
            marginTop,
            bounds.Width - marginRight,
            bounds.Height - marginBottom);

        using var axisPen = new Pen(Color.FromArgb(148, 163, 184), 1.2f);
        using var gridPen = new Pen(Color.FromArgb(226, 232, 240), 1f);
        using var linePen = new Pen(Color.FromArgb(16, 185, 129), 2.8f);
        using var zeroPen = new Pen(Color.Goldenrod, 1.8f) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
        using var targetPen = new Pen(Color.Firebrick, 1.8f) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
        using var textBrush = new SolidBrush(Color.FromArgb(30, 41, 59));
        using var titleFont = new Font("Segoe UI", 11.5f, FontStyle.Bold);
        using var labelFont = new Font("Segoe UI", 9f, FontStyle.Regular);

        g.DrawString("DE-Thermo Temperature Trajectory", titleFont, textBrush, marginLeft, 10f);

        if (_lastOutput is null || _lastOutput.Points.Count < 2)
        {
            g.DrawRectangle(axisPen, plotRect.X, plotRect.Y, plotRect.Width, plotRect.Height);
            g.DrawString("Run a simulation to see the trajectory.", labelFont, textBrush, plotRect.X + 12, plotRect.Y + 20);
            return;
        }

        var points = _lastOutput.Points;
        var minX = 0d;
        var maxX = points.Last().TS;
        var minY = Math.Floor(points.Min(p => p.TempC) - 4);
        var maxY = Math.Ceiling(points.Max(p => p.TempC) + 4);

        float MapX(double x) => (float)(plotRect.Left + ((x - minX) / (maxX - minX + 1e-9)) * plotRect.Width);
        float MapY(double y) => (float)(plotRect.Top + ((maxY - y) / (maxY - minY + 1e-9)) * plotRect.Height);

        g.DrawRectangle(axisPen, plotRect.X, plotRect.Y, plotRect.Width, plotRect.Height);

        for (var i = 0; i <= 6; i++)
        {
            var xVal = minX + i * (maxX - minX) / 6.0;
            var x = MapX(xVal);
            g.DrawLine(gridPen, x, plotRect.Top, x, plotRect.Bottom);
            g.DrawString($"{xVal:F0}", labelFont, textBrush, x - 12, plotRect.Bottom + 8);
        }

        for (var i = 0; i <= 6; i++)
        {
            var yVal = minY + i * (maxY - minY) / 6.0;
            var y = MapY(yVal);
            g.DrawLine(gridPen, plotRect.Left, y, plotRect.Right, y);
            g.DrawString($"{yVal:F0}", labelFont, textBrush, plotRect.Left - 40, y - 8);
        }

        var zeroY = MapY(0);
        g.DrawLine(zeroPen, plotRect.Left, zeroY, plotRect.Right, zeroY);
        var targetY = MapY(_lastOutput.TargetTempC);
        g.DrawLine(targetPen, plotRect.Left, targetY, plotRect.Right, targetY);

        var linePoints = points.Select(p => new PointF(MapX(p.TS), MapY(p.TempC))).ToArray();
        g.DrawLines(linePen, linePoints);

        g.DrawString("time (s)", labelFont, textBrush, plotRect.Right - 70, plotRect.Bottom + 30);
        g.DrawString("temp (C)", labelFont, textBrush, 8, plotRect.Top - 6);
        g.DrawString("0 C", labelFont, Brushes.DarkGoldenrod, plotRect.Left + 8, zeroY - 16);
        g.DrawString($"{_lastOutput.TargetTempC:F0} C", labelFont, Brushes.Firebrick, plotRect.Left + 8, targetY - 16);
    }
}
