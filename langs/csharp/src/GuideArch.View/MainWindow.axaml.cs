using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using GuideArch.ViewModels;
using VMx.Components;

namespace GuideArch.View;

public partial class MainWindow : Window
{
    private readonly ComponentVM<ScenarioState> _vm;
    private readonly ScenarioCommands _cmds;

    public MainWindow()
    {
        InitializeComponent();
        _vm = ScenarioVMFactory.Create();
        _cmds = ScenarioVMFactory.GetCommands(_vm);
        DataContext = _vm;
    }

    private async void OnOpenClicked(object sender, RoutedEventArgs e)
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
}
