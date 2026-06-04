using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using GuideArch.Models;
using GuideArch.ViewModels;
using ScottPlot;
using VMx.Components;
using AvaColor = Avalonia.Media.Color;
using AvaBrush = Avalonia.Media.IBrush;
using AvaSolidBrush = Avalonia.Media.SolidColorBrush;
using AvaFontFamily = Avalonia.Media.FontFamily;
using AvaFontWeight = Avalonia.Media.FontWeight;
using AvaTextWrapping = Avalonia.Media.TextWrapping;
using AvaTextTrimming = Avalonia.Media.TextTrimming;
using AvaOrientation = Avalonia.Layout.Orientation;
using AvaVertAlign = Avalonia.Layout.VerticalAlignment;
using AvaHorizAlign = Avalonia.Layout.HorizontalAlignment;

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
    // Root VM owns the theme observable + the child ScenarioVM. AXAML
    // bindings continue to target the inner ScenarioVM via DataContext so
    // every existing `{ReflectionBinding Model.X}` stays valid — the
    // reference graph is still rooted at AppVM, navigating down to scenario.
    private readonly ComponentVM<AppState> _appVm;
    private readonly AppCommands _appCmds;
    private readonly ComponentVM<ScenarioState> _vm;
    private readonly ScenarioCommands _cmds;
    private ScenarioMutator Mutator => _cmds.Mutator;

    // Track last-rendered state so we only refresh when data actually changed.
    // _lastScenarioRef captures the ScenarioM instance the coefficients matrix
    // was last built from. ScenarioM is a sealed record and every mutation
    // (UpdateCoefficient, AddAlternative, …) produces a new instance via `with`,
    // so reference equality is a reliable "did the matrix shape or values
    // change?" test. Pure selection clicks in the Results grid leave Scenario
    // alone — without this gate they'd rebuild the entire coefficients UI tree
    // on every click and the chart canvases on top of it, which is what made
    // the Results tab flicker.
    private ScenarioM? _lastScenarioRef = null;
    private int _lastCandidateCount = -1;
    private int? _lastSelectedIndex = -2; // sentinel "not yet rendered"

    // Coalesces rapid selection clicks into a single chart redraw cycle.
    // Charts B and C both Clear() + add new line series + Refresh() on every
    // selection change; without coalescing, holding the arrow key in the
    // ResultsGrid or click-storming the rows queues a render per click and
    // the visible canvases strobe. The 30ms window is fast enough to feel
    // immediate but lossy enough that the user never sees an in-between
    // frame on a fast scroll.
    private DispatcherTimer? _chartRefreshTimer;

    public MainWindow()
    {
        InitializeComponent();
        _appVm = AppVMFactory.Create(mode: "native");
        _appCmds = AppVMFactory.GetCommands(_appVm);
        _vm = _appCmds.Scenario;
        _cmds = ScenarioVMFactory.GetCommands(_vm);
        DataContext = _vm;

        // Apply the initial theme + re-apply on every theme change. The hub
        // route is not used here because Avalonia's RequestedThemeVariant
        // setter must run on the UI thread anyway.
        ApplyTheme(_appVm.Model.Theme);
        _appVm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ComponentVM<AppState>.Model))
                ApplyTheme(_appVm.Model.Theme);
        };

        // Subscribe to VM state changes to refresh charts.
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ComponentVM<ScenarioState>.Model))
            {
                OnStateChanged();
                UpdateStatusFilePath();
            }
        };
        UpdateStatusFilePath();

        InitCharts();
    }

    private void ApplyTheme(string theme)
    {
        var app = Avalonia.Application.Current;
        if (app is null) return;
        app.RequestedThemeVariant = theme switch
        {
            "light" => Avalonia.Styling.ThemeVariant.Light,
            "dark" => Avalonia.Styling.ThemeVariant.Dark,
            _ => Avalonia.Styling.ThemeVariant.Default,
        };
        // Update the toolbar's theme-toggle icon to reflect what the click
        // will switch *to* next (sun while in dark mode, moon while in light).
        var icon = this.FindControl<TextBlock>("ThemeIcon");
        if (icon is not null) icon.Text = theme == "dark" ? "☀" : "🌙";
    }

    private void OnThemeToggleClicked(object? sender, RoutedEventArgs e)
    {
        var current = _appVm.Model.Theme;
        _appCmds.SetTheme(current == "dark" ? "light" : "dark");
    }

    private void UpdateStatusFilePath()
    {
        var label = this.FindControl<TextBlock>("StatusFilePath");
        if (label is null) return;
        var path = _vm.Model.FilePath;
        if (string.IsNullOrEmpty(path))
        {
            label.IsVisible = false;
            return;
        }
        label.Text = Path.GetFileName(path);
        ToolTip.SetTip(label, path);
        label.IsVisible = true;
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

        // Chart C — top-10 comparison polylines, dark background
        var plotC = ChartC.Plot;
        plotC.FigureBackground.Color = Color.FromHex("#1E1E2E");
        plotC.DataBackground.Color = Color.FromHex("#181825");
        plotC.Axes.Color(Color.FromHex("#CDD6F4"));
        plotC.Grid.MajorLineColor = Color.FromHex("#313244");
        plotC.Title("Top 10 candidates — modal per property");
        plotC.XLabel("Property");
        plotC.YLabel("Modal sum");
    }

    // ScottPlot v5 has no native qualitative palette helper for these specific
    // hex codes, so we keep our own. Same order as the TS COMPARISON_PALETTE
    // and the Python COMPARISON_PALETTE so visual identity is stable across
    // impls and screenshots line up in cross-impl review.
    private static readonly string[] ComparisonPalette =
    {
        "#4e79a7", // blue
        "#f28e2b", // orange
        "#e15759", // red
        "#76b7b2", // teal
        "#59a14f", // green
        "#edc948", // yellow
        "#b07aa1", // purple
        "#ff9da7", // pink
        "#9c755f", // brown
        "#bab0ac", // grey
    };

    // -----------------------------------------------------------------------
    // State-change observer — re-plots charts when relevant state changes
    // -----------------------------------------------------------------------

    private void OnStateChanged()
    {
        var state = _vm.Model;
        bool scenarioChanged = !ReferenceEquals(_lastScenarioRef, state.Scenario);
        bool candidatesChanged = state.Candidates.Length != _lastCandidateCount;
        bool selectionChanged = state.SelectedCandidateIndex != _lastSelectedIndex;

        // Coefficients matrix is a UI mirror of state.Scenario.Coefficients —
        // only rebuild it when the scenario instance itself has changed (load /
        // new / any structural or value mutation). Selection-only changes
        // never touch Scenario, so we skip the rebuild on Results-tab clicks.
        if (scenarioChanged)
        {
            RebuildCoefficientsMatrix(state);
            _lastScenarioRef = state.Scenario;
        }

        if (!candidatesChanged && !selectionChanged) return;

        _lastCandidateCount = state.Candidates.Length;
        _lastSelectedIndex = state.SelectedCandidateIndex;

        // spec/viewmodels.md §6 caps the Results table at the top 50 rows
        // — Python (main.py:1042) and TS (ResultsTab.svelte:14) do the same.
        // Project here so the DataGrid never tries to render thousands of
        // rows of TOPSIS candidates on a large scenario. Only refresh
        // ItemsSource when the candidates set itself changed; a selection-
        // only delta would otherwise hand the DataGrid a brand-new array
        // (.ToArray() on Span) every click and trigger a re-bind that fed
        // back into OnResultsGridSelectionChanged.
        if (candidatesChanged)
        {
            var resultsGrid = this.FindControl<DataGrid>("ResultsGrid");
            if (resultsGrid is not null)
            {
                resultsGrid.ItemsSource = state.Candidates.Length > 50
                    ? state.Candidates.AsSpan(0, 50).ToArray()
                    : (System.Collections.IEnumerable)state.Candidates;
            }
        }

        // Chart A (ranked bar) only depends on the candidates set; selection
        // doesn't change its bars. Charts B (fuzzy triangle of selected) and
        // C (top-10 polyline with selected highlighted) do depend on
        // selection — but coalesce rapid clicks via _chartRefreshTimer rather
        // than re-rendering both canvases per event.
        if (candidatesChanged) RenderChartA(state);
        ScheduleChartBcRefresh();

        // Sync the candidates DataGrid selection row.
        SyncResultsGridSelection(state);
    }

    private void ScheduleChartBcRefresh()
    {
        if (_chartRefreshTimer is null)
        {
            _chartRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
            _chartRefreshTimer.Tick += (_, _) =>
            {
                _chartRefreshTimer.Stop();
                var s = _vm.Model;
                RenderChartB(s);
                RenderChartC(s);
            };
        }
        if (!_chartRefreshTimer.IsEnabled)
            _chartRefreshTimer.Start();
    }

    // -----------------------------------------------------------------------
    // Coefficients 2-D matrix builder (spec editors.md §2.4)
    // -----------------------------------------------------------------------

    // Column widths (px).
    // AltNameColWidth: fixed (alt names are short; we don't want them to grow).
    // MinPropColWidth: floor for star-sized property columns so 4 props don't
    // shrink below readability when the window is narrow. Above that floor
    // each property column shares the remaining canvas width equally.
    private const double AltNameColWidth = 160.0;
    private const double MinPropColWidth = 140.0;
    private const double RowHeight = 28.0;
    private const double GroupHeaderHeight = 26.0;

    private void RebuildCoefficientsMatrix(ScenarioState state)
    {
        var matrix = state.CoefficientsMatrix;

        if (matrix.Groups.IsEmpty)
        {
            CoefficientsContent.Content = null;
            return;
        }

        // Lookup brushes from app resources.
        var bgSurface = TryGetBrush("BgSurfaceBrush") ?? new AvaSolidBrush(AvaColor.Parse("#13161d"));
        var borderSubtle = TryGetBrush("BorderSubtleBrush") ?? new AvaSolidBrush(AvaColor.Parse("#262b36"));
        var textPrimary = TryGetBrush("TextPrimaryBrush") ?? new AvaSolidBrush(AvaColor.Parse("#e6e7ed"));
        var textSecondary = TryGetBrush("TextSecondaryBrush") ?? new AvaSolidBrush(AvaColor.Parse("#9298a8"));
        var textMuted = TryGetBrush("TextMutedBrush") ?? new AvaSolidBrush(AvaColor.Parse("#5e6478"));
        var accentMuted = TryGetBrush("AccentMutedBrush") ?? new AvaSolidBrush(AvaColor.Parse("#3d2a6b"));
        var accentBrush = TryGetBrush("AccentBrush") ?? new AvaSolidBrush(AvaColor.Parse("#8b5cf6"));
        var monoFont = new AvaFontFamily("Cascadia Code,Consolas,monospace");

        int propCount = matrix.Properties.Length;
        // Minimum total width — once the canvas is wider than this, columns
        // share the extra. The host ScrollViewer kicks in below it.
        double minTotalWidth = AltNameColWidth + propCount * MinPropColWidth;

        // ── Build the root StackPanel ──────────────────────────────────
        var root = new StackPanel { Orientation = AvaOrientation.Vertical };
        root.MinWidth = minTotalWidth;

        // ── Property column header row ─────────────────────────────────
        var headerRow = new Grid { Height = GroupHeaderHeight + 4 };
        headerRow.Background = bgSurface;
        var headerCols = new ColumnDefinitions();
        headerCols.Add(new ColumnDefinition(AltNameColWidth, GridUnitType.Pixel));
        for (int p = 0; p < propCount; p++)
            headerCols.Add(new ColumnDefinition(1, GridUnitType.Star) { MinWidth = MinPropColWidth });
        headerRow.ColumnDefinitions = headerCols;

        // "Alternative" label in the first col
        var altHeader = new TextBlock
        {
            Text = "Alternative",
            FontSize = 11,
            FontWeight = AvaFontWeight.SemiBold,
            Foreground = textSecondary,
            VerticalAlignment = AvaVertAlign.Center,
            Margin = new Avalonia.Thickness(8, 0, 4, 0)
        };
        Grid.SetColumn(altHeader, 0);
        headerRow.Children.Add(altHeader);

        // Property column headers
        for (int p = 0; p < propCount; p++)
        {
            var prop = matrix.Properties[p];
            var kindText = prop.Kind == PropertyKind.Min ? "↓" : "↑";
            var headerCell = new Border
            {
                BorderBrush = borderSubtle,
                BorderThickness = new Avalonia.Thickness(1, 0, 0, 1),
                Padding = new Avalonia.Thickness(4, 2, 4, 2)
            };
            var headerContent = new StackPanel { Orientation = AvaOrientation.Vertical, Spacing = 1 };
            headerContent.Children.Add(new TextBlock
            {
                Text = prop.Name,
                FontSize = 11,
                FontWeight = AvaFontWeight.Medium,
                Foreground = textPrimary,
                TextWrapping = AvaTextWrapping.NoWrap,
                TextTrimming = AvaTextTrimming.CharacterEllipsis
            });
            // Kind + weight badge
            var badge = new StackPanel { Orientation = AvaOrientation.Horizontal, Spacing = 4 };
            badge.Children.Add(new TextBlock
            {
                Text = kindText,
                FontSize = 10,
                Foreground = accentBrush,
                VerticalAlignment = AvaVertAlign.Center
            });
            badge.Children.Add(new TextBlock
            {
                Text = $"{prop.Weight:G4}",
                FontSize = 10,
                FontFamily = monoFont,
                Foreground = textMuted,
                VerticalAlignment = AvaVertAlign.Center
            });
            headerContent.Children.Add(badge);
            headerCell.Child = headerContent;
            Grid.SetColumn(headerCell, p + 1);
            headerRow.Children.Add(headerCell);
        }
        root.Children.Add(headerRow);

        // ── Decision groups ────────────────────────────────────────────
        foreach (var group in matrix.Groups)
        {
            // Group header
            var groupHeader = new Border
            {
                Background = accentMuted,
                Padding = new Avalonia.Thickness(8, 4, 8, 4),
                MinWidth = minTotalWidth
            };
            groupHeader.Child = new TextBlock
            {
                Text = group.DecisionName,
                FontSize = 12,
                FontWeight = AvaFontWeight.SemiBold,
                Foreground = textPrimary
            };
            root.Children.Add(groupHeader);

            // Alternative rows
            foreach (var row in group.Rows)
            {
                var rowGrid = new Grid { Height = RowHeight };

                var rowCols = new ColumnDefinitions();
                rowCols.Add(new ColumnDefinition(AltNameColWidth, GridUnitType.Pixel));
                for (int p = 0; p < propCount; p++)
                    rowCols.Add(new ColumnDefinition(1, GridUnitType.Star) { MinWidth = MinPropColWidth });
                rowGrid.ColumnDefinitions = rowCols;
                rowGrid.Background = bgSurface;

                // Alternative name cell
                var nameCell = new Border
                {
                    BorderBrush = borderSubtle,
                    BorderThickness = new Avalonia.Thickness(0, 0, 1, 1),
                    Padding = new Avalonia.Thickness(8, 0, 4, 0)
                };
                nameCell.Child = new TextBlock
                {
                    Text = row.AlternativeName,
                    FontSize = 12,
                    Foreground = textPrimary,
                    VerticalAlignment = AvaVertAlign.Center,
                    TextTrimming = AvaTextTrimming.CharacterEllipsis
                };
                Grid.SetColumn(nameCell, 0);
                rowGrid.Children.Add(nameCell);

                // Coefficient cells
                for (int p = 0; p < row.Cells.Length; p++)
                {
                    var cell = row.Cells[p];
                    var cellBorder = new Border
                    {
                        BorderBrush = borderSubtle,
                        BorderThickness = new Avalonia.Thickness(1, 0, 0, 1),
                        Padding = new Avalonia.Thickness(4, 0, 4, 0)
                    };

                    // L · M · U — three click-to-edit slots. Visible state is
                    // a TextBlock (so the cell reads identically to before the
                    // editing landed); clicking the slot swaps it for a TextBox
                    // that's focused and selected, ready to type. Enter or blur
                    // commits, Escape cancels. Each slot only ever edits its
                    // own vertex of the fuzzy triple — the other two are taken
                    // from the captured `cell` snapshot, which is fine because
                    // any commit causes a matrix rebuild that re-snapshots all
                    // three values into the next pass of editors.
                    //
                    // The commit itself is deferred via Dispatcher.UIThread.Post.
                    // Mutator.UpdateCoefficient triggers a synchronous re-solve
                    // and an OnStateChanged callback that tears down this very
                    // Border tree; running that destruction inside the TextBox's
                    // own LostFocus / KeyDown handler is what was crashing on
                    // the user's machine. Posting it makes Avalonia finish
                    // unwinding the focus / key-event call stack first.
                    var cellContent = new StackPanel
                    {
                        Orientation = AvaOrientation.Horizontal,
                        Spacing = 3,
                        VerticalAlignment = AvaVertAlign.Center,
                        HorizontalAlignment = AvaHorizAlign.Center,
                    };

                    var capturedCell = cell;
                    cellContent.Children.Add(MakeEditableValueSlot(
                        cell.Lower, textMuted, monoFont,
                        newL => CommitCoefficient(
                            capturedCell.AlternativeId, capturedCell.PropertyId,
                            newL, capturedCell.Modal, capturedCell.Upper)));
                    cellContent.Children.Add(MakeDotSeparator(textMuted));
                    cellContent.Children.Add(MakeEditableValueSlot(
                        cell.Modal, textPrimary, monoFont,
                        newM => CommitCoefficient(
                            capturedCell.AlternativeId, capturedCell.PropertyId,
                            capturedCell.Lower, newM, capturedCell.Upper)));
                    cellContent.Children.Add(MakeDotSeparator(textMuted));
                    cellContent.Children.Add(MakeEditableValueSlot(
                        cell.Upper, textMuted, monoFont,
                        newU => CommitCoefficient(
                            capturedCell.AlternativeId, capturedCell.PropertyId,
                            capturedCell.Lower, capturedCell.Modal, newU)));

                    cellBorder.Child = cellContent;
                    Grid.SetColumn(cellBorder, p + 1);
                    rowGrid.Children.Add(cellBorder);
                }

                root.Children.Add(rowGrid);
            }
        }

        CoefficientsContent.Content = root;
    }

    private static TextBlock MakeDotSeparator(AvaBrush fg) =>
        new TextBlock
        {
            Text = "·",
            FontSize = 10,
            Foreground = fg,
            VerticalAlignment = AvaVertAlign.Center
        };

    /// <summary>
    /// Click-to-edit value slot. The visible state is a plain TextBlock so the
    /// coefficient row reads identically to the read-only layout that shipped
    /// before. PointerPressed swaps in a focused TextBox; LostFocus or Enter
    /// parses the text and invokes <paramref name="onCommit"/>; Escape reverts.
    ///
    /// onCommit is invoked only when the parsed value actually differs from
    /// <paramref name="initial"/>, and only via Dispatcher.UIThread.Post so the
    /// matrix rebuild that UpdateCoefficient triggers happens after this
    /// control's event handlers have fully unwound (otherwise Avalonia tears
    /// the control down inside its own callback stack and crashes).
    /// </summary>
    private static Border MakeEditableValueSlot(
        double initial,
        AvaBrush fg,
        AvaFontFamily ff,
        Action<double> onCommit)
    {
        var slot = new Border
        {
            Padding = new Avalonia.Thickness(3, 0, 3, 0),
            Background = Avalonia.Media.Brushes.Transparent,
            VerticalAlignment = AvaVertAlign.Center,
            Cursor = new Avalonia.Input.Cursor(StandardCursorType.Ibeam),
            // A faint hover surface lifts the slot as "interactive" without
            // committing to the TextBox chrome until the user actually clicks.
            Classes = { "coeff-slot" },
        };

        TextBlock MakeDisplay() => new TextBlock
        {
            Text = $"{initial:F3}",
            FontSize = 11,
            FontFamily = ff,
            Foreground = fg,
            VerticalAlignment = AvaVertAlign.Center,
            HorizontalAlignment = AvaHorizAlign.Center,
        };

        slot.Child = MakeDisplay();

        void BeginEdit()
        {
            var box = new TextBox
            {
                Text = $"{initial:F3}",
                FontSize = 11,
                FontFamily = ff,
                Foreground = fg,
                Background = Avalonia.Media.Brushes.Transparent,
                BorderThickness = new Avalonia.Thickness(0),
                Padding = new Avalonia.Thickness(0),
                MinHeight = 0,
                VerticalAlignment = AvaVertAlign.Center,
                HorizontalAlignment = AvaHorizAlign.Stretch,
                // Inline editor is sized to comfortably hold "0.999" in 11pt
                // mono with no chrome — narrower than the FluentTheme default
                // so the L · M · U triple fits inside the cell column.
                Width = 44,
                TextAlignment = Avalonia.Media.TextAlignment.Center,
            };

            bool done = false;
            void Done(bool commit)
            {
                if (done) return;
                done = true;

                if (commit
                    && double.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                    && parsed != initial)
                {
                    // Defer the actual mutation so the focus / key-event call
                    // stack unwinds before OnStateChanged tears this slot down.
                    var value = parsed;
                    Dispatcher.UIThread.Post(() => onCommit(value));
                }
                else
                {
                    // Restore the display TextBlock. Either nothing changed,
                    // parsing failed, or the user pressed Escape.
                    slot.Child = MakeDisplay();
                }
            }

            box.LostFocus += (_, _) => Done(commit: true);
            box.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    e.Handled = true;
                    Done(commit: true);
                }
                else if (e.Key == Key.Escape)
                {
                    e.Handled = true;
                    Done(commit: false);
                }
            };

            slot.Child = box;
            Dispatcher.UIThread.Post(() =>
            {
                box.Focus();
                box.SelectAll();
            });
        }

        slot.PointerPressed += (_, e) =>
        {
            // Only swap on a primary-button click that hit the TextBlock itself,
            // not a re-entrant event from the TextBox we're about to install.
            if (slot.Child is TextBox) return;
            if (e.GetCurrentPoint(slot).Properties.IsLeftButtonPressed)
                BeginEdit();
        };

        return slot;
    }

    /// <summary>
    /// Defers the actual <see cref="ScenarioMutator.UpdateCoefficient"/> call
    /// onto the UI dispatcher and surfaces any <see cref="ScenarioMutationException"/>
    /// through the status-bar warning. Called from MakeEditableValueSlot's
    /// onCommit callbacks, which are themselves already posted, so this is
    /// effectively the catch-all for domain-rule failures.
    /// </summary>
    private void CommitCoefficient(
        string alternativeId, string propertyId,
        double lower, double modal, double upper)
    {
        try
        {
            Mutator.UpdateCoefficient(alternativeId, propertyId, lower, modal, upper);
        }
        catch (ScenarioMutationException ex)
        {
            StampDiscardWarning(ex.Message);
        }
    }

    private AvaBrush? TryGetBrush(string key)
    {
        if (Avalonia.Application.Current?.Resources.TryGetResource(key, null, out var res) == true
            && res is AvaBrush brush)
            return brush;
        return null;
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
        // Use -1 (no highlight) when nothing is selected, instead of defaulting
        // to 0 — otherwise an "unselected" state renders row 0 in the mauve
        // 'selected' color and confuses the user. Solve()'s preserve-if-in-range
        // policy seeds 0 when candidates first appear; null here means the
        // user explicitly deselected.
        int selectedIdx = state.SelectedCandidateIndex ?? -1;

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
    // Chart C — top-10 candidate comparison polylines (legacy view)
    // One polyline per candidate; x = property index, y = modal sum per
    // property. Click on any line (or its legend) selects that candidate.
    // -----------------------------------------------------------------------

    private void RenderChartC(ScenarioState state)
    {
        var plotC = ChartC.Plot;
        plotC.Clear();

        if (state.Candidates.IsEmpty || state.Scenario is null)
        {
            ChartC.Refresh();
            return;
        }

        var series = ChartData.PrepComparisonSeries(state.Candidates, state.Scenario);
        if (series.IsEmpty)
        {
            ChartC.Refresh();
            return;
        }

        int selectedRank = state.SelectedCandidateIndex ?? -1;

        for (int i = 0; i < series.Length; i++)
        {
            var s = series[i];
            var hex = ComparisonPalette[s.PaletteIndex % ComparisonPalette.Length];
            bool isSelected = (s.Rank == selectedRank);
            // De-emphasise non-selected lines when something is selected, so
            // the chosen line reads against a faded background.
            byte alpha = (byte)(selectedRank < 0 ? 230 : (isSelected ? 255 : 70));
            var baseColor = Color.FromHex(hex);
            var color = new Color(baseColor.R, baseColor.G, baseColor.B, alpha);

            var line = plotC.Add.ScatterLine(s.Xs, s.Ys, color);
            line.LineWidth = isSelected ? 2.5f : 1.4f;
            line.LegendText = s.Label;
        }

        // X-axis ticks = property names.
        var props = state.Scenario.Properties;
        double[] xs = Enumerable.Range(0, props.Length).Select(i => (double)i).ToArray();
        string[] labels = props.Select(p => p.Name).ToArray();
        plotC.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericManual(xs, labels);

        plotC.ShowLegend();
        plotC.Axes.AutoScale();
        ChartC.Refresh();
    }

    private void OnChartCPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var state = _vm.Model;
        if (state.Candidates.IsEmpty || state.Scenario is null) return;

        // Pointer in plot coordinates.
        var pixel = e.GetPosition(ChartC);
        var coord = ChartC.Plot.GetCoordinates(
            (float)pixel.X, (float)pixel.Y,
            ChartC.Plot.Axes.Bottom, ChartC.Plot.Axes.Left);

        // Pick the nearest polyline at the nearest property-x index.
        var series = ChartData.PrepComparisonSeries(state.Candidates, state.Scenario);
        if (series.IsEmpty) return;

        int nearestX = (int)Math.Round(coord.X);
        if (nearestX < 0 || nearestX >= state.Scenario.Properties.Length) return;

        int hitRank = -1;
        double bestDy = double.MaxValue;
        foreach (var s in series)
        {
            double y = s.Ys[nearestX];
            double dy = Math.Abs(y - coord.Y);
            if (dy < bestDy) { bestDy = dy; hitRank = s.Rank; }
        }

        if (hitRank < 0) return;

        try
        {
            Mutator.SelectCandidate(hitRank);
        }
        catch (ScenarioMutationException)
        {
            // Out-of-range click — ignore.
        }
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
    {
        bool wasDirty = _vm.Model.IsDirty;
        _cmds.NewCmd.Execute(null);
        if (wasDirty) StampDiscardWarning("Create a new scenario");
    }

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
            {
                bool wasDirty = _vm.Model.IsDirty;
                _cmds.OpenCmd.Execute(path);
                // Only stamp the discard warning when the Open actually
                // succeeded — OpenCmd's catch path leaves the model
                // unchanged (IsDirty stays true), and a corrupt-file load
                // shouldn't claim "replaced unsaved changes" alongside
                // the legitimate "Open failed: …" message.
                if (wasDirty && !_vm.Model.IsDirty) StampDiscardWarning("Open a scenario");
            }
        }
    }

    /// <summary>
    /// Records a warning that the just-completed action discarded unsaved
    /// changes. Mirrors the TS Toolbar._confirmDiscardIfDirty + Python
    /// _confirm_discard_if_dirty user-facing UX (a real modal-confirm is on
    /// the v1.1 backlog for all three impls). Call AFTER the action
    /// completes so a cancelled file-picker doesn't leave a phantom
    /// "discarded changes" warning.
    /// </summary>
    private void StampDiscardWarning(string action)
    {
        var state = _vm.Model;
        _vm.Model = state with
        {
            Warnings = state.Warnings.Add(
                $"{action} replaced unsaved changes — last revision discarded.")
        };
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

    private void OnOpenSampleSasClicked(object? sender, RoutedEventArgs e)
    {
        bool wasDirty = _vm.Model.IsDirty;
        OpenSample(SampleScenarios.All[0]);
        if (wasDirty && !_vm.Model.IsDirty) StampDiscardWarning("Open Sample SAS");
    }

    private void OnOpenSampleEdsClicked(object? sender, RoutedEventArgs e)
    {
        bool wasDirty = _vm.Model.IsDirty;
        OpenSample(SampleScenarios.All[1]);
        if (wasDirty && !_vm.Model.IsDirty) StampDiscardWarning("Open Sample EDS");
    }

    /// <summary>
    /// Writes the embedded sample resource to a temp file, then opens it via
    /// OpenCmd so the existing ScenarioLoader path is used unchanged.
    /// </summary>
    private void OpenSample(SampleScenarios.Sample sample)
    {
        try
        {
            var tempPath = Path.Combine(Path.GetTempPath(), $"guidearch-{sample.Id}.json");
            using (var src = SampleScenarios.Open(sample))
            using (var dst = File.Create(tempPath))
                src.CopyTo(dst);
            _cmds.OpenCmd.Execute(tempPath);
        }
        catch (Exception ex)
        {
            // Use fire-and-forget; ShowErrorAsync is async but this is a sync handler.
            _ = ShowErrorAsync($"Could not open sample '{sample.Label}': {ex.Message}");
        }
    }

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
