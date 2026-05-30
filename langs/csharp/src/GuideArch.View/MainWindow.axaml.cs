using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using GuideArch.Models;
using GuideArch.ViewModels;
using VMx.Components;

namespace GuideArch.View;

/// <summary>
/// Code-behind for the M3 app shell.
/// All mutations go through ScenarioMutator (in the VM layer).
/// Fatal ScenarioMutationException is caught here and shown as a dialog.
/// </summary>
public partial class MainWindow : Window
{
    private readonly ComponentVM<ScenarioState> _vm;
    private readonly ScenarioCommands _cmds;
    private ScenarioMutator Mutator => _cmds.Mutator;

    public MainWindow()
    {
        InitializeComponent();
        _vm = ScenarioVMFactory.Create();
        _cmds = ScenarioVMFactory.GetCommands(_vm);
        DataContext = _vm;
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
