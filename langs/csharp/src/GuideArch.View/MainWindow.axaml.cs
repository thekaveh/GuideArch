using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using GuideArch.Models;
using GuideArch.ViewModels;
using ScottPlot;
using VMx.Components;

namespace GuideArch.View;

/// <summary>
/// Code-behind for the M4 app shell.
/// All mutations go through ScenarioMutator (in the VM layer).
/// Fatal ScenarioMutationException is caught here and shown as a dialog.
/// Charts are re-plotted in the View layer whenever Candidates or
/// SelectedCandidateIndex change (spec charts.md §2, §3).
/// </summary>
public partial class MainWindow : Window
{
    private readonly ComponentVM<ScenarioState> _vm;
    private readonly ScenarioCommands _cmds;
    private ScenarioMutator Mutator => _cmds.Mutator;

    // Track last-rendered state so we only refresh when data actually changed.
    private int _lastCandidateCount = -1;
    private int? _lastSelectedIndex = -2; // sentinel "not yet rendered"

    public MainWindow()
    {
        InitializeComponent();
        _vm = ScenarioVMFactory.Create();
        _cmds = ScenarioVMFactory.GetCommands(_vm);
        DataContext = _vm;

        // Subscribe to VM state changes to refresh charts.
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ComponentVM<ScenarioState>.Model))
                OnStateChanged();
        };

        InitCharts();
    }

    // -----------------------------------------------------------------------
    // Chart initialization
    // -----------------------------------------------------------------------

    private void InitCharts()
    {
        // Chart A — horizontal bar chart, dark background
        var plotA = ChartA.Plot;
        plotA.FigureBackground.Color = Color.FromHex("#1E1E2E");
        plotA.DataBackground.Color = Color.FromHex("#181825");
        plotA.Axes.Color(Color.FromHex("#CDD6F4"));
        plotA.Grid.MajorLineColor = Color.FromHex("#313244");
        plotA.Title("Top candidates by score");
        plotA.XLabel("Score");

        // Chart B — triangle series, dark background
        var plotB = ChartB.Plot;
        plotB.FigureBackground.Color = Color.FromHex("#1E1E2E");
        plotB.DataBackground.Color = Color.FromHex("#181825");
        plotB.Axes.Color(Color.FromHex("#CDD6F4"));
        plotB.Grid.MajorLineColor = Color.FromHex("#313244");
        plotB.XLabel("Value");
        plotB.YLabel("Membership");
    }

    // -----------------------------------------------------------------------
    // State-change observer — re-plots charts when relevant state changes
    // -----------------------------------------------------------------------

    private void OnStateChanged()
    {
        var state = _vm.Model;
        bool candidatesChanged = state.Candidates.Length != _lastCandidateCount;
        bool selectionChanged = state.SelectedCandidateIndex != _lastSelectedIndex;

        if (!candidatesChanged && !selectionChanged) return;

        _lastCandidateCount = state.Candidates.Length;
        _lastSelectedIndex = state.SelectedCandidateIndex;

        RenderChartA(state);
        RenderChartB(state);

        // Sync the candidates DataGrid selection row.
        SyncResultsGridSelection(state);
    }

    // -----------------------------------------------------------------------
    // Chart A — horizontal bar chart of top 30 candidates (spec §2)
    // -----------------------------------------------------------------------

    private void RenderChartA(ScenarioState state)
    {
        var plotA = ChartA.Plot;
        plotA.Clear();

        if (state.Candidates.IsEmpty)
        {
            ChartA.Refresh();
            return;
        }

        var bars = ChartData.PrepRankedCandidates(state.Candidates, topN: 30);
        int n = bars.Length;

        // Build ScottPlot Bar objects — horizontal orientation.
        // Position = rank (0 = top), Value = score.
        var scottBars = new List<ScottPlot.Bar>(n);
        int selectedIdx = state.SelectedCandidateIndex ?? 0;

        for (int i = 0; i < n; i++)
        {
            var b = bars[i];
            byte alpha = (byte)(255 * b.OpacityFactor);
            bool isSelected = (i == selectedIdx);

            // Accent color: #89B4FA (Catppuccin blue), faded for non-selected.
            // Selected bar is highlighted in #CBA6F7 (mauve).
            var fillColor = isSelected
                ? new Color(0xCB, 0xA6, 0xF7, 255)  // mauve
                : new Color(0x89, 0xB4, 0xFA, alpha); // blue with opacity fade

            scottBars.Add(new ScottPlot.Bar
            {
                Position = i,           // Y position (rank index)
                Value = b.Score,        // X value (score)
                FillColor = fillColor,
                LineColor = new Color(0xFF, 0xFF, 0xFF, 80),
                LineWidth = 0.5f,
                Orientation = ScottPlot.Orientation.Horizontal,
                Label = b.Label
            });
        }

        var barPlot = plotA.Add.Bars(scottBars.ToArray());
        barPlot.Horizontal = true;

        // Y-axis: rank labels, inverted so rank 0 is at top.
        double[] yPositions = Enumerable.Range(0, n).Select(i => (double)i).ToArray();
        string[] yLabels = bars.Select(b => $"#{b.Rank}").ToArray();
        plotA.Axes.Left.TickGenerator = new ScottPlot.TickGenerators.NumericManual(yPositions, yLabels);
        plotA.Axes.InvertY();

        // X-axis: score from 0 to max.
        double maxScore = bars.Max(b => b.Score);
        plotA.Axes.SetLimitsX(0, maxScore * 1.05);
        plotA.Axes.AutoScaleY();

        ChartA.Refresh();
    }

    // -----------------------------------------------------------------------
    // Chart B — triangle series for selected candidate (spec §3)
    // -----------------------------------------------------------------------

    private void RenderChartB(ScenarioState state)
    {
        var plotB = ChartB.Plot;
        plotB.Clear();

        if (state.Candidates.IsEmpty || state.Scenario is null || !state.SelectedCandidateIndex.HasValue)
        {
            plotB.Title("No candidate selected");
            ChartB.Refresh();
            return;
        }

        int idx = state.SelectedCandidateIndex.Value;
        if (idx < 0 || idx >= state.Candidates.Length)
        {
            ChartB.Refresh();
            return;
        }

        var candidate = state.Candidates[idx];
        plotB.Title($"Rank #{candidate.Rank} — Score {candidate.Score:G6}");

        var triangles = ChartData.PrepTriangleSeries(candidate, state.Scenario);

        if (triangles.IsEmpty)
        {
            ChartB.Refresh();
            return;
        }

        // Property color palette (Catppuccin Mocha accent colors).
        ScottPlot.Color[] palette =
        {
            Color.FromHex("#89B4FA"), // blue
            Color.FromHex("#A6E3A1"), // green
            Color.FromHex("#F38BA8"), // red
            Color.FromHex("#FAB387"), // peach
            Color.FromHex("#CBA6F7"), // mauve
            Color.FromHex("#89DCEB"), // sky
            Color.FromHex("#F9E2AF"), // yellow
            Color.FromHex("#74C7EC"), // sapphire
        };

        for (int i = 0; i < triangles.Length; i++)
        {
            var t = triangles[i];
            var color = palette[i % palette.Length];

            var line = plotB.Add.ScatterLine(t.Xs, t.Ys, color);
            line.LineWidth = 2;
            line.LegendText = t.PropertyName;
        }

        plotB.ShowLegend();
        plotB.Axes.AutoScale();
        ChartB.Refresh();
    }

    // -----------------------------------------------------------------------
    // Chart A click handler — selects candidate (spec §2 Click behavior)
    // -----------------------------------------------------------------------

    private void OnChartAPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var state = _vm.Model;
        if (state.Candidates.IsEmpty) return;

        // Convert pointer position to plot coordinate.
        var pixel = e.GetPosition(ChartA);
        var coord = ChartA.Plot.GetCoordinates(
            (float)pixel.X, (float)pixel.Y,
            ChartA.Plot.Axes.Bottom, ChartA.Plot.Axes.Left);

        // In horizontal bar chart: Y = rank index (inverted axis).
        // coord.Y gives the rank; we round to nearest integer.
        int hitIdx = (int)Math.Round(coord.Y);
        int n = Math.Min(state.Candidates.Length, 30);

        if (hitIdx < 0 || hitIdx >= n) return;

        try
        {
            Mutator.SelectCandidate(hitIdx);
        }
        catch (ScenarioMutationException)
        {
            // Ignore out-of-range clicks silently.
        }
    }

    // -----------------------------------------------------------------------
    // Sync DataGrid selection with SelectedCandidateIndex
    // -----------------------------------------------------------------------

    private void SyncResultsGridSelection(ScenarioState state)
    {
        if (!state.SelectedCandidateIndex.HasValue) return;
        int idx = state.SelectedCandidateIndex.Value;
        if (idx < 0 || idx >= state.Candidates.Length) return;

        var candidate = state.Candidates[idx];
        if (!ReferenceEquals(ResultsGrid.SelectedItem, candidate))
            ResultsGrid.SelectedItem = candidate;
    }

    // -----------------------------------------------------------------------
    // ResultsGrid selection changed — drives SelectedCandidateIndex (spec §6)
    // -----------------------------------------------------------------------

    private void OnResultsGridSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ResultsGrid.SelectedItem is not CandidateM selected) return;
        var state = _vm.Model;
        int idx = state.Candidates.IndexOf(selected);
        if (idx < 0) return;
        if (state.SelectedCandidateIndex == idx) return;

        try
        {
            Mutator.SelectCandidate(idx);
        }
        catch (ScenarioMutationException)
        {
            // Ignore.
        }
    }

    // -----------------------------------------------------------------------
    // Toolbar handlers
    // -----------------------------------------------------------------------

    private void OnNewClicked(object? sender, RoutedEventArgs e)
        => _cmds.NewCmd.Execute(null);

    private async void OnOpenClicked(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open scenario JSON",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Scenario JSON") { Patterns = new[] { "*.json" } }
            }
        });

        if (files.Count >= 1)
        {
            var path = files[0].TryGetLocalPath();
            if (path is not null)
                _cmds.OpenCmd.Execute(path);
        }
    }

    private void OnSaveClicked(object? sender, RoutedEventArgs e)
        => _cmds.SaveCmd.Execute(null);

    private async void OnSaveAsClicked(object? sender, RoutedEventArgs e)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save scenario JSON",
            SuggestedFileName = "scenario.json",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("Scenario JSON") { Patterns = new[] { "*.json" } }
            }
        });

        if (file is not null)
        {
            var path = file.TryGetLocalPath();
            if (path is not null)
                _cmds.SaveAsCmd.Execute(path);
        }
    }

    private void OnSolveClicked(object? sender, RoutedEventArgs e)
        => _cmds.SolveCmd.Execute(null);

    // -----------------------------------------------------------------------
    // Decisions tab
    // -----------------------------------------------------------------------

    private async void OnAddDecisionClicked(object? sender, RoutedEventArgs e)
    {
        await SafeMutateAsync(() => Mutator.AddDecision());
    }

    // -----------------------------------------------------------------------
    // Alternatives tab
    // -----------------------------------------------------------------------

    private async void OnAddAlternativeClicked(object? sender, RoutedEventArgs e)
    {
        // Pick the first decision as the default; user can change via rename.
        var decisions = _vm.Model.DecisionsView;
        if (decisions.IsEmpty)
        {
            await ShowErrorAsync("No decisions exist. Add a decision first.");
            return;
        }

        // Use the selected row in DecisionsGrid if available; otherwise use first.
        string decisionId = decisions[0].Id;
        if (DecisionsGrid.SelectedItem is DecisionM selected)
            decisionId = selected.Id;

        await SafeMutateAsync(() => Mutator.AddAlternative(decisionId));
    }

    // -----------------------------------------------------------------------
    // Properties tab
    // -----------------------------------------------------------------------

    private async void OnAddPropertyClicked(object? sender, RoutedEventArgs e)
    {
        await SafeMutateAsync(() => Mutator.AddProperty());
    }

    // -----------------------------------------------------------------------
    // Constraints tab — Threshold
    // -----------------------------------------------------------------------

    private async void OnAddThresholdClicked(object? sender, RoutedEventArgs e)
    {
        var props = _vm.Model.PropertiesView;
        if (props.IsEmpty)
        {
            await ShowErrorAsync("No properties exist. Add a property first.");
            return;
        }
        // Add a threshold with min = 0 and no max (spec: at least one must be set).
        await SafeMutateAsync(() => Mutator.AddThresholdConstraint(props[0].Id, 0.0, null));
    }

    // -----------------------------------------------------------------------
    // Constraints tab — Dependency
    // -----------------------------------------------------------------------

    private async void OnAddDependencyClicked(object? sender, RoutedEventArgs e)
    {
        var alts = _vm.Model.AlternativesView;
        if (alts.Length < 2)
        {
            await ShowErrorAsync("Need at least 2 alternatives to create a dependency constraint.");
            return;
        }
        await SafeMutateAsync(() => Mutator.AddDependencyConstraint(alts[0].Id, alts[1].Id));
    }

    // -----------------------------------------------------------------------
    // Constraints tab — Conflict
    // -----------------------------------------------------------------------

    private async void OnAddConflictClicked(object? sender, RoutedEventArgs e)
    {
        var alts = _vm.Model.AlternativesView;
        if (alts.Length < 2)
        {
            await ShowErrorAsync("Need at least 2 alternatives to create a conflict constraint.");
            return;
        }
        await SafeMutateAsync(() => Mutator.AddConflictConstraint(alts[0].Id, alts[1].Id));
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Executes a mutation; catches <see cref="ScenarioMutationException"/> and
    /// shows an error dialog (spec editors.md §5 fatal validation feedback).
    /// </summary>
    private async Task SafeMutateAsync(Action mutation)
    {
        try
        {
            mutation();
        }
        catch (ScenarioMutationException ex)
        {
            await ShowErrorAsync(ex.Message);
        }
    }

    /// <summary>Shows a simple error dialog using an Avalonia window.</summary>
    private async Task ShowErrorAsync(string message)
    {
        var dialog = new Window
        {
            Title = "GuideArch — Error",
            Width = 420,
            Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(20),
                Spacing = 16,
                Children =
                {
                    new TextBlock
                    {
                        Text = message,
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap
                    },
                    new Button
                    {
                        Content = "OK",
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right
                    }
                }
            }
        };

        // Wire the OK button to close the dialog.
        if (dialog.Content is StackPanel sp &&
            sp.Children[1] is Button okBtn)
        {
            okBtn.Click += (_, _) => dialog.Close();
        }

        await dialog.ShowDialog(this);
    }
}
