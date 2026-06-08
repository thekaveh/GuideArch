using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using GuideArch.View;
using GuideArch.ViewModels;
using VMx.Components;

// ──────────────────────────────────────────────────────────────────────────────
// Strategy: run Avalonia's main loop on a dedicated thread with
// UseHeadless + SingleViewLifetime (no real window system). All UI work is
// dispatched to Dispatcher.UIThread.  We wait for the captures to complete via
// a TaskCompletionSource before shutting down.
// ──────────────────────────────────────────────────────────────────────────────

string outputDir = FindOutputDir();
Directory.CreateDirectory(outputDir);
Console.WriteLine($"[ss] Output dir: {outputDir}");

var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

// Avalonia's STA thread — owns the event loop.
var avaloniaThread = new Thread(() =>
{
    try
    {
        AppBuilder
            .Configure<GuideArch.View.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .WithInterFont()
            .LogToTrace()
            .SetupWithoutStarting();          // <-- initialises the platform but does NOT block

        // Post the capture work to the UI thread; it will run once the dispatcher
        // starts pumping below.
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                Console.WriteLine("[ss] UI thread running, capturing...");
                CaptureAll(outputDir);
                tcs.TrySetResult(0);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[ss] FATAL in capture: " + ex);
                tcs.TrySetResult(1);
            }
        });

        // Pump the dispatcher until the TCS is resolved.
        // MainLoop exits when we call Shutdown().
        Dispatcher.UIThread.MainLoop(CancellationToken.None);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine("[ss] FATAL in Avalonia setup: " + ex);
        tcs.TrySetResult(1);
    }
});

avaloniaThread.IsBackground = false;
if (OperatingSystem.IsWindows())
    avaloniaThread.SetApartmentState(ApartmentState.STA);
avaloniaThread.Start();

int exitCode = await tcs.Task;

// Ask the dispatcher to stop after our work is done.
Dispatcher.UIThread.Post(() => Dispatcher.UIThread.BeginInvokeShutdown(DispatcherPriority.Background));
avaloniaThread.Join(TimeSpan.FromSeconds(10));

return exitCode;

// ────────────────────────────────────────────────────────────────────────────
// Capture helpers — called on the Avalonia UI thread
// ────────────────────────────────────────────────────────────────────────────

void CaptureAll(string dir)
{
    var tabNames = new[]
    {
        "Decisions", "Alternatives", "Properties", "Coefficients",
        "Constraints", "Results", "Critical Decisions", "Critical Constraints",
    };

    Console.WriteLine("[ss] Capturing empty state...");
    CaptureEmptyState(tabNames, dir);

    Console.WriteLine("[ss] Capturing SAS state...");
    CaptureSasState(tabNames, dir);
}

void CaptureEmptyState(string[] tabNames, string dir)
{
    var window = new MainWindow { Width = 1200, Height = 800 };
    window.Show();
    Tick(5);

    var tabs = window.FindControl<TabControl>("MainTabs");

    // ScottPlot charts render black in headless mode — hide them for Results tab.
    var chartA = window.FindControl<Control>("ChartA");
    var chartB = window.FindControl<Control>("ChartB");

    for (int i = 0; i < tabNames.Length; i++)
    {
        if (tabs is not null && i < tabs.ItemCount)
        {
            tabs.SelectedIndex = i;
            Dispatcher.UIThread.RunJobs();
            Tick(4);
        }

        // Results tab (index 5): hide charts so tab body renders correctly.
        bool hideCharts = (i == 5 && chartA is not null && chartB is not null);
        if (hideCharts)
        {
            chartA!.IsVisible = false;
            chartB!.IsVisible = false;
            Dispatcher.UIThread.RunJobs();
            Tick(4);
        }

        var frame = window.CaptureRenderedFrame();
        SaveOrWarn(frame, Path.Combine(dir, $"empty-{ToSafe(tabNames[i])}.png"));

        if (hideCharts)
        {
            chartA!.IsVisible = true;
            chartB!.IsVisible = true;
        }
    }
    window.Close();
}

void CaptureSasState(string[] tabNames, string dir)
{
    var window = new MainWindow { Width = 1200, Height = 800 };
    window.Show();

    var vm = (ComponentVM<ScenarioState>)window.DataContext!;
    var cmds = ScenarioVMFactory.GetCommands(vm);
    string sasPath = ExtractSas();
    cmds.OpenCmd.Execute(sasPath);

    Dispatcher.UIThread.RunJobs();
    Tick(10);
    Dispatcher.UIThread.RunJobs();

    Console.WriteLine($"[ss] Status: {vm.Model.Status}");

    var tabs = window.FindControl<TabControl>("MainTabs");
    if (tabs is null) { Console.Error.WriteLine("[ss] MainTabs not found"); window.Close(); return; }

    // ScottPlot's AvaPlot controls paint black in headless mode (no GPU surface).
    // Find the chart controls by name and hide them before capturing the Results tab
    // so the DataGrid (left column) renders correctly.
    var chartA = window.FindControl<Control>("ChartA");
    var chartB = window.FindControl<Control>("ChartB");

    int tabCount = tabs.ItemCount;
    for (int i = 0; i < tabNames.Length; i++)
    {
        if (i < tabCount)
        {
            tabs.SelectedIndex = i;
            Dispatcher.UIThread.RunJobs();
            Tick(4);
            Dispatcher.UIThread.RunJobs();
        }

        // Results tab (index 5): hide charts so DataGrid renders instead of black.
        bool hideCharts = (i == 5 && chartA is not null && chartB is not null);
        if (hideCharts)
        {
            chartA!.IsVisible = false;
            chartB!.IsVisible = false;
            Dispatcher.UIThread.RunJobs();
            Tick(4);
        }

        var frame = window.CaptureRenderedFrame();
        SaveOrWarn(frame, Path.Combine(dir, $"sas-{ToSafe(tabNames[i])}.png"));

        if (hideCharts)
        {
            chartA!.IsVisible = true;
            chartB!.IsVisible = true;
        }
    }

    window.Close();
    if (sasPath.StartsWith(Path.GetTempPath())) File.Delete(sasPath);
}

// ────────────────────────────────────────────────────────────────────────────
// Utilities
// ────────────────────────────────────────────────────────────────────────────

static void Tick(int n) => AvaloniaHeadlessPlatform.ForceRenderTimerTick(n);
static string ToSafe(string s) => s.Replace(" ", "-").ToLowerInvariant();

static void SaveOrWarn(Avalonia.Media.Imaging.WriteableBitmap? bmp, string path)
{
    if (bmp is null) { Console.Error.WriteLine($"[ss] null frame: {Path.GetFileName(path)}"); return; }
    using var s = File.OpenWrite(path);
    bmp.Save(s);
    Console.WriteLine($"[ss]   -> {path}");
}

static string FindOutputDir()
{
    var d = new DirectoryInfo(AppContext.BaseDirectory);
    for (int i = 0; i < 15; i++)
    {
        if (File.Exists(Path.Combine(d.FullName, "GuideArch.sln")))
            return Path.Combine(d.FullName, "tests", "visual", "snapshots");
        if (d.Parent is null) break;
        d = d.Parent;
    }
    return Path.Combine(Directory.GetCurrentDirectory(), "tests", "visual", "snapshots");
}

static string ExtractSas()
{
    // First look for the file on disk (dev mode).
    var d = new DirectoryInfo(AppContext.BaseDirectory);
    for (int i = 0; i < 15; i++)
    {
        var c = Path.Combine(d.FullName, "spec", "conformance", "scenarios", "sas.json");
        if (File.Exists(c)) return c;
        if (d.Parent is null) break;
        d = d.Parent;
    }

    // Fall back to embedded resource in GuideArch.View.
    var tmp = Path.Combine(Path.GetTempPath(), "guidearch-sas.json");
    var asm = typeof(SampleScenarios).Assembly;
    var res = asm.GetManifestResourceNames()
        .FirstOrDefault(n => n.EndsWith("sas.json", StringComparison.OrdinalIgnoreCase));
    if (res is null) throw new FileNotFoundException("sas.json not found as resource or on disk");
    using var src = asm.GetManifestResourceStream(res)!;
    using var dst = File.Create(tmp);
    src.CopyTo(dst);
    return tmp;
}

