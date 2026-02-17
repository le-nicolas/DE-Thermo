using System.Drawing.Drawing2D;
using System.Globalization;

namespace DEThermo.Gui;

public partial class Form1 : Form
{
    private readonly Dictionary<string, TextBox> _inputs = new(StringComparer.OrdinalIgnoreCase);
    private Label _zeroLabel = null!;
    private Label _freezeLabel = null!;
    private Label _targetLabel = null!;
    private Label _noteLabel = null!;
    private Label _animPhaseLabel = null!;
    private Label _animTimeLabel = null!;
    private Label _animTempLabel = null!;
    private Label _animSpeedLabel = null!;
    private Panel _plotPanel = null!;
    private Panel _animationPanel = null!;
    private TrackBar _speedTrack = null!;
    private System.Windows.Forms.Timer _animationTimer = null!;
    private SimulationOutput? _lastOutput;
    private int _animationIndex;
    private int _animationFrame;

    public Form1()
    {
        InitializeComponent();
        BuildUi();
        _animationTimer = new System.Windows.Forms.Timer { Interval = 33 };
        _animationTimer.Tick += (_, _) => AdvanceAnimation();
    }

    private void BuildUi()
    {
        Text = "DE-Thermo GUI";
        Width = 1360;
        Height = 820;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1160, 720);

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            SplitterDistance = 380,
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
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(10)
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 48));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 52));

        panel.Controls.Add(BuildAnimationSection(), 0, 0);
        panel.Controls.Add(BuildPlotSection(), 0, 1);

        return panel;
    }

    private Control BuildAnimationSection()
    {
        var box = new GroupBox
        {
            Dock = DockStyle.Fill,
            Text = "Water Animation"
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        box.Controls.Add(layout);

        var controls = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 42,
            AutoSize = true,
            Padding = new Padding(4, 2, 4, 2)
        };
        layout.Controls.Add(controls, 0, 0);

        var playButton = new Button
        {
            Text = "Play",
            Width = 70,
            Height = 27
        };
        playButton.Click += (_, _) => StartAnimation();
        controls.Controls.Add(playButton);

        var pauseButton = new Button
        {
            Text = "Pause",
            Width = 70,
            Height = 27
        };
        pauseButton.Click += (_, _) => PauseAnimation();
        controls.Controls.Add(pauseButton);

        var resetButton = new Button
        {
            Text = "Reset",
            Width = 70,
            Height = 27
        };
        resetButton.Click += (_, _) => ResetAnimation();
        controls.Controls.Add(resetButton);

        controls.Controls.Add(new Label
        {
            Text = "Speed",
            Width = 42,
            Height = 25,
            TextAlign = ContentAlignment.MiddleCenter,
            Margin = new Padding(12, 4, 0, 0)
        });

        _speedTrack = new TrackBar
        {
            Minimum = 1,
            Maximum = 10,
            Value = 3,
            TickStyle = TickStyle.None,
            Width = 130
        };
        _speedTrack.ValueChanged += (_, _) =>
        {
            _animSpeedLabel.Text = $"x{_speedTrack.Value}";
        };
        controls.Controls.Add(_speedTrack);

        _animSpeedLabel = new Label
        {
            Text = "x3",
            Width = 36,
            Height = 25,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 4, 8, 0)
        };
        controls.Controls.Add(_animSpeedLabel);

        _animPhaseLabel = BuildPillLabel("phase: idle");
        _animTimeLabel = BuildPillLabel("time: -");
        _animTempLabel = BuildPillLabel("temp: -");
        controls.Controls.Add(_animPhaseLabel);
        controls.Controls.Add(_animTimeLabel);
        controls.Controls.Add(_animTempLabel);

        _animationPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(241, 245, 249),
            Margin = new Padding(4, 4, 4, 6)
        };
        _animationPanel.Paint += (_, e) => DrawAnimation(e.Graphics, _animationPanel.ClientRectangle);
        layout.Controls.Add(_animationPanel, 0, 1);

        return box;
    }

    private static Label BuildPillLabel(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            Font = new Font("Segoe UI", 9f, FontStyle.Regular),
            BackColor = Color.FromArgb(226, 232, 240),
            Padding = new Padding(8, 4, 8, 4),
            Margin = new Padding(6, 3, 0, 0)
        };
    }

    private Control BuildPlotSection()
    {
        var box = new GroupBox
        {
            Dock = DockStyle.Fill,
            Text = "Temperature Trajectory"
        };

        _plotPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(248, 250, 252)
        };
        _plotPanel.Paint += (_, e) => DrawPlot(e.Graphics, _plotPanel.ClientRectangle);
        box.Controls.Add(_plotPanel);

        return box;
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
            Width = 150,
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
            _animationIndex = 0;
            _animationFrame = 0;
            RenderOutput(output);
            StartAnimation();
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
        UpdateAnimationReadout();
        _animationPanel.Invalidate();
        _plotPanel.Invalidate();
    }

    private void StartAnimation()
    {
        if (_lastOutput is null || _lastOutput.Points.Count < 2)
        {
            return;
        }

        if (_animationIndex >= _lastOutput.Points.Count - 1)
        {
            _animationIndex = 0;
        }

        _animationTimer.Start();
    }

    private void PauseAnimation()
    {
        _animationTimer.Stop();
    }

    private void ResetAnimation()
    {
        if (_lastOutput is null)
        {
            return;
        }

        _animationTimer.Stop();
        _animationIndex = 0;
        _animationFrame = 0;
        UpdateAnimationReadout();
        _animationPanel.Invalidate();
        _plotPanel.Invalidate();
    }

    private void AdvanceAnimation()
    {
        if (_lastOutput is null || _lastOutput.Points.Count < 2)
        {
            _animationTimer.Stop();
            return;
        }

        _animationIndex += Math.Max(1, _speedTrack.Value);
        _animationFrame++;

        if (_animationIndex >= _lastOutput.Points.Count - 1)
        {
            _animationIndex = _lastOutput.Points.Count - 1;
            _animationTimer.Stop();
        }

        UpdateAnimationReadout();
        _animationPanel.Invalidate();
        _plotPanel.Invalidate();
    }

    private void UpdateAnimationReadout()
    {
        if (_lastOutput is null || _lastOutput.Points.Count == 0)
        {
            _animPhaseLabel.Text = "phase: idle";
            _animTimeLabel.Text = "time: -";
            _animTempLabel.Text = "temp: -";
            return;
        }

        var idx = Math.Clamp(_animationIndex, 0, _lastOutput.Points.Count - 1);
        var p = _lastOutput.Points[idx];
        var phase = GetPhaseName(_lastOutput, p.TS, p.TempC);
        _animPhaseLabel.Text = $"phase: {phase}";
        _animTimeLabel.Text = $"time: {p.TS:F0} s";
        _animTempLabel.Text = $"temp: {p.TempC:F1} C";
    }

    private static string GetPhaseName(SimulationOutput output, double tS, double tempC)
    {
        var zero = output.Milestones.ReachesZeroS;
        var freeze = output.Milestones.FreezeCompleteS;

        if (tempC > 0.0)
        {
            return "liquid cooling";
        }

        if (zero.HasValue && freeze.HasValue && tS >= zero.Value && tS < freeze.Value)
        {
            return "freezing";
        }

        if (freeze.HasValue && tS >= freeze.Value)
        {
            return "solid cooling";
        }

        return "sub-zero";
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

    private void DrawAnimation(Graphics g, Rectangle bounds)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var bg = new LinearGradientBrush(
                   bounds,
                   Color.FromArgb(224, 242, 254),
                   Color.FromArgb(226, 232, 240),
                   LinearGradientMode.Vertical))
        {
            g.FillRectangle(bg, bounds);
        }

        if (_lastOutput is null || _lastOutput.Points.Count == 0)
        {
            using var promptFont = new Font("Segoe UI", 10.5f, FontStyle.Regular);
            g.DrawString("Run a simulation to animate water cooling and freezing.", promptFont, Brushes.SlateGray, 18, 18);
            return;
        }

        var idx = Math.Clamp(_animationIndex, 0, _lastOutput.Points.Count - 1);
        var point = _lastOutput.Points[idx];
        var scenario = _lastOutput.Scenario;
        var freezeFraction = ComputeFreezeFraction(_lastOutput, point.TS, point.TempC);

        var containerAreaFactor = Clamp((scenario.AreaM2 - 0.01) / 0.10, 0.0, 1.0);
        var fillRatio = Clamp(0.30 + scenario.MassKg / 1.20, 0.35, 0.90);
        var cupWidth = (float)(180.0 + containerAreaFactor * 90.0);
        var cupHeight = 250f;
        var cupX = bounds.Width * 0.50f - cupWidth * 0.5f;
        var cupY = bounds.Height * 0.52f - cupHeight * 0.5f;
        var wall = 8f;

        var outerRect = new RectangleF(cupX, cupY, cupWidth, cupHeight);
        var innerRect = RectangleF.Inflate(outerRect, -wall, -wall);
        var fluidHeight = innerRect.Height * (float)fillRatio;
        var fluidTop = innerRect.Bottom - fluidHeight;
        var iceHeight = fluidHeight * (float)freezeFraction;
        var liquidTop = fluidTop + iceHeight;

        var tempNorm = Normalize(point.TempC, -20, Math.Max(90, scenario.InitialTempC));
        var hot = Color.FromArgb(250, 125, 55);
        var cool = Color.FromArgb(37, 99, 235);
        var waterColor = Blend(cool, hot, tempNorm);
        var waterTopColor = Blend(Color.White, waterColor, 0.45);
        var waterBottomColor = Blend(waterColor, Color.FromArgb(15, 23, 42), 0.25);

        var agitation = Clamp(0.20 + scenario.HtcWm2K / 26.0 + Math.Abs(scenario.InitialTempC - scenario.AmbientTempC) / 180.0, 0.2, 1.8);
        var waveAmplitude = (float)(2.5 + 7.0 * agitation * (1.0 - freezeFraction));
        var wavePhase = (float)(_animationFrame * 0.30 + scenario.AreaM2 * 20.0);
        var waveCycles = 2.0 + containerAreaFactor * 1.4;

        if (liquidTop < innerRect.Bottom - 2f)
        {
            using var liquidPath = new GraphicsPath();
            var wave = BuildWave(innerRect.Left, innerRect.Right, liquidTop, waveAmplitude, wavePhase, waveCycles, 48);
            liquidPath.StartFigure();
            liquidPath.AddLine(innerRect.Left, innerRect.Bottom, innerRect.Left, wave[0].Y);
            liquidPath.AddLines(wave);
            liquidPath.AddLine(innerRect.Right, wave[^1].Y, innerRect.Right, innerRect.Bottom);
            liquidPath.CloseFigure();

            using var waterBrush = new LinearGradientBrush(
                new PointF(innerRect.Left, fluidTop),
                new PointF(innerRect.Left, innerRect.Bottom),
                waterTopColor,
                waterBottomColor);
            g.FillPath(waterBrush, liquidPath);
        }

        if (freezeFraction > 0.0)
        {
            var iceRect = new RectangleF(innerRect.Left, fluidTop, innerRect.Width, iceHeight);
            using var iceBrush = new SolidBrush(Color.FromArgb(175, 230, 245, 255));
            g.FillRectangle(iceBrush, iceRect);

            using var crackPen = new Pen(Color.FromArgb(130, 148, 163, 184), 1.0f);
            var crackCount = 3 + (int)(freezeFraction * 8.0);
            for (var i = 0; i < crackCount; i++)
            {
                var x = iceRect.Left + (i + 1) * iceRect.Width / (crackCount + 1);
                var drift = (float)(Math.Sin(i * 1.7 + _animationFrame * 0.05) * 6.0);
                g.DrawLine(crackPen, x, iceRect.Top + 2, x + drift, Math.Max(iceRect.Top + 4, iceRect.Bottom - 2));
            }
        }

        if (point.TempC > 45 && freezeFraction < 0.1)
        {
            using var steamPen = new Pen(Color.FromArgb(90, 203, 213, 225), 2f);
            for (var i = 0; i < 3; i++)
            {
                var x = outerRect.Left + outerRect.Width * (0.25f + i * 0.22f);
                var yTop = outerRect.Top - 58;
                var yBottom = outerRect.Top + 8;
                var x1 = x + (float)Math.Sin((_animationFrame + i * 12) * 0.10) * 8f;
                var x2 = x + (float)Math.Sin((_animationFrame + i * 17) * 0.09) * 11f;
                g.DrawBezier(
                    steamPen,
                    x,
                    yBottom,
                    x1 - 10f,
                    yBottom - 20f,
                    x2 + 10f,
                    yTop + 20f,
                    x2,
                    yTop);
            }
        }

        if (freezeFraction > 0.25)
        {
            using var frostBrush = new SolidBrush(Color.FromArgb(90, 226, 232, 240));
            g.FillEllipse(frostBrush, outerRect.Left - 6, outerRect.Top + 6, 18, 18);
            g.FillEllipse(frostBrush, outerRect.Right - 12, outerRect.Top + 20, 16, 16);
            g.FillEllipse(frostBrush, outerRect.Left - 4, outerRect.Bottom - 26, 14, 14);
        }

        using var glassPen = new Pen(Color.FromArgb(84, 100, 116, 139), 3f);
        using var rimPen = new Pen(Color.FromArgb(140, 148, 163, 184), 2f);
        g.DrawRoundedRectangle(glassPen, Rectangle.Round(outerRect), 16);
        g.DrawRoundedRectangle(rimPen, Rectangle.Round(innerRect), 12);

        DrawThermometer(g, bounds, scenario, point.TempC);

        using var textFont = new Font("Segoe UI", 10f, FontStyle.Bold);
        using var textBrush = new SolidBrush(Color.FromArgb(30, 41, 59));
        g.DrawString($"Scenario: {scenario.Name}", textFont, textBrush, 16, bounds.Height - 72);
        g.DrawString($"t = {point.TS:F0} s | T = {point.TempC:F1} C | freeze = {freezeFraction * 100:F0}%", textFont, textBrush, 16, bounds.Height - 46);
    }

    private void DrawPlot(Graphics g, Rectangle bounds)
    {
        g.Clear(Color.FromArgb(248, 250, 252));
        g.SmoothingMode = SmoothingMode.AntiAlias;

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
        using var fullPen = new Pen(Color.FromArgb(191, 219, 254), 1.8f);
        using var linePen = new Pen(Color.FromArgb(16, 185, 129), 2.8f);
        using var zeroPen = new Pen(Color.Goldenrod, 1.6f) { DashStyle = DashStyle.Dash };
        using var targetPen = new Pen(Color.Firebrick, 1.6f) { DashStyle = DashStyle.Dash };
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

        var fullLine = points.Select(p => new PointF(MapX(p.TS), MapY(p.TempC))).ToArray();
        g.DrawLines(fullPen, fullLine);

        var idx = Math.Clamp(_animationIndex, 0, points.Count - 1);
        if (idx >= 1)
        {
            var activeLine = points.Take(idx + 1).Select(p => new PointF(MapX(p.TS), MapY(p.TempC))).ToArray();
            g.DrawLines(linePen, activeLine);
            var marker = activeLine[^1];
            g.FillEllipse(Brushes.White, marker.X - 5, marker.Y - 5, 10, 10);
            g.DrawEllipse(new Pen(Color.FromArgb(22, 163, 74), 2f), marker.X - 5, marker.Y - 5, 10, 10);
            g.DrawLine(new Pen(Color.FromArgb(34, 197, 94), 1f), marker.X, plotRect.Top, marker.X, plotRect.Bottom);
        }

        g.DrawString("time (s)", labelFont, textBrush, plotRect.Right - 70, plotRect.Bottom + 30);
        g.DrawString("temp (C)", labelFont, textBrush, 8, plotRect.Top - 6);
        g.DrawString("0 C", labelFont, Brushes.DarkGoldenrod, plotRect.Left + 8, zeroY - 16);
        g.DrawString($"{_lastOutput.TargetTempC:F0} C", labelFont, Brushes.Firebrick, plotRect.Left + 8, targetY - 16);
    }

    private static PointF[] BuildWave(
        float left,
        float right,
        float centerY,
        float amplitude,
        float phase,
        double cycles,
        int segments)
    {
        var points = new PointF[segments + 1];
        for (var i = 0; i <= segments; i++)
        {
            var x = left + i * (right - left) / segments;
            var t = i / (double)segments;
            var wave = Math.Sin(t * cycles * Math.PI * 2.0 + phase);
            var y = centerY + (float)(wave * amplitude);
            points[i] = new PointF(x, y);
        }

        return points;
    }

    private void DrawThermometer(Graphics g, Rectangle bounds, ScenarioInput scenario, double tempC)
    {
        var tube = new RectangleF(bounds.Width - 82, 64, 24, bounds.Height - 128);
        using var tubePen = new Pen(Color.FromArgb(100, 116, 139), 2f);
        using var tubeBrush = new SolidBrush(Color.FromArgb(241, 245, 249));
        g.FillRoundedRectangle(tubeBrush, Rectangle.Round(tube), 8);
        g.DrawRoundedRectangle(tubePen, Rectangle.Round(tube), 8);

        var norm = Normalize(tempC, -20, Math.Max(90, scenario.InitialTempC));
        var fillHeight = (float)(tube.Height * norm);
        var fillRect = new RectangleF(tube.Left + 3, tube.Bottom - fillHeight + 1, tube.Width - 6, Math.Max(2, fillHeight - 2));
        var fillColor = Blend(Color.FromArgb(59, 130, 246), Color.FromArgb(239, 68, 68), norm);
        using var fillBrush = new SolidBrush(fillColor);
        g.FillRectangle(fillBrush, fillRect);

        using var font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
        g.DrawString($"{tempC:F1} C", font, Brushes.SlateGray, tube.Left - 18, tube.Bottom + 6);
    }

    private static double ComputeFreezeFraction(SimulationOutput output, double timeS, double tempC)
    {
        var zero = output.Milestones.ReachesZeroS;
        var freeze = output.Milestones.FreezeCompleteS;

        if (tempC > 0.0)
        {
            return 0.0;
        }

        if (zero.HasValue && freeze.HasValue && freeze.Value > zero.Value + 1e-9)
        {
            if (timeS <= zero.Value)
            {
                return 0.0;
            }
            if (timeS >= freeze.Value)
            {
                return 1.0;
            }
            return Clamp((timeS - zero.Value) / (freeze.Value - zero.Value), 0.0, 1.0);
        }

        return tempC <= 0.0 ? 1.0 : 0.0;
    }

    private static double Normalize(double value, double min, double max)
    {
        if (Math.Abs(max - min) < 1e-9)
        {
            return 0.0;
        }

        return Clamp((value - min) / (max - min), 0.0, 1.0);
    }

    private static double Clamp(double value, double min, double max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }

    private static Color Blend(Color a, Color b, double t)
    {
        var x = Clamp(t, 0.0, 1.0);
        return Color.FromArgb(
            (int)(a.A + (b.A - a.A) * x),
            (int)(a.R + (b.R - a.R) * x),
            (int)(a.G + (b.G - a.G) * x),
            (int)(a.B + (b.B - a.B) * x));
    }
}

internal static class GraphicsExtensions
{
    public static void DrawRoundedRectangle(this Graphics graphics, Pen pen, Rectangle bounds, int radius)
    {
        using var path = RoundedRectPath(bounds, radius);
        graphics.DrawPath(pen, path);
    }

    public static void FillRoundedRectangle(this Graphics graphics, Brush brush, Rectangle bounds, int radius)
    {
        using var path = RoundedRectPath(bounds, radius);
        graphics.FillPath(brush, path);
    }

    private static GraphicsPath RoundedRectPath(Rectangle bounds, int radius)
    {
        var diameter = radius * 2;
        var arc = new Rectangle(bounds.Location, new Size(diameter, diameter));
        var path = new GraphicsPath();

        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }
}
