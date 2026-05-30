using Avalonia.Controls;
using GuideArch.ViewModels;

namespace GuideArch.View;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = HelloVmxVMFactory.Create();
    }
}
