using GuideArch.ViewModels;
using Xunit;

namespace GuideArch.ViewModels.Tests;

/// <summary>
/// The 5 mandatory AppVM cross-impl checks (mirrors langs/typescript and langs/python):
///
///   1. Default theme = "dark" when persistence is empty.
///   2. Theme round-trips through the persistence layer.
///   3. Unknown theme is non-fatal (warning appended, state unchanged).
///   4. PropertyChanged fires when theme changes.
///   5. Mode is set at construction and immutable thereafter.
///
/// Plus a sixth probe: AppVM.scenario is a stable child reference.
/// </summary>
public class AppVMTests
{
    private sealed class StubPersistence
    {
        private string? _stored;
        public string Load() => _stored ?? AppVMFactory.DefaultTheme;
        public void Save(string v) => _stored = v;
        public string? Peek() => _stored;
    }

    [Fact]
    public void DefaultsToDark_WhenNothingPersisted()
    {
        var stub = new StubPersistence();
        var vm = AppVMFactory.Create(
            mode: "native",
            loadTheme: stub.Load,
            persistTheme: stub.Save);
        Assert.Equal(AppVMFactory.DefaultTheme, vm.Model.Theme);
        Assert.Equal("dark", vm.Model.Theme);
    }

    [Fact]
    public void RoundTripsThemeThroughPersistence()
    {
        var stub = new StubPersistence();
        var vm1 = AppVMFactory.Create("native", stub.Load, stub.Save);
        var cmds1 = AppVMFactory.GetCommands(vm1);
        cmds1.SetTheme("light");
        Assert.Equal("light", vm1.Model.Theme);
        Assert.Equal("light", stub.Peek());

        // Fresh AppVM reading the same persistence restores 'light'.
        var vm2 = AppVMFactory.Create("native", stub.Load, stub.Save);
        Assert.Equal("light", vm2.Model.Theme);
    }

    [Fact]
    public void UnknownThemeIsNonFatal_AppendsWarning_LeavesState()
    {
        var stub = new StubPersistence();
        var vm = AppVMFactory.Create("native", stub.Load, stub.Save);
        var cmds = AppVMFactory.GetCommands(vm);

        var before = vm.Model.Theme;
        var ex = Record.Exception(() => cmds.SetTheme("chartreuse"));
        Assert.Null(ex);
        Assert.Equal(before, vm.Model.Theme);
        Assert.Single(vm.Model.Warnings);
        Assert.Contains("chartreuse", vm.Model.Warnings[0]);
        // Persistence must NOT have been touched.
        Assert.Null(stub.Peek());
    }

    [Fact]
    public void PropertyChanged_FiresOnThemeChange()
    {
        var stub = new StubPersistence();
        var vm = AppVMFactory.Create("native", stub.Load, stub.Save);
        var cmds = AppVMFactory.GetCommands(vm);

        var seen = new List<string>();
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is not null) seen.Add(e.PropertyName);
        };

        cmds.SetTheme("light");

        Assert.Contains(nameof(VMx.Components.ComponentVM<AppState>.Model), seen);
    }

    [Fact]
    public void ModeIsSetAtConstruction_AndImmutable()
    {
        var stub = new StubPersistence();
        var vm = AppVMFactory.Create("tauri", stub.Load, stub.Save);

        Assert.Equal("tauri", vm.Model.Mode);

        // There is no SetMode on the public surface.
        var cmds = AppVMFactory.GetCommands(vm);
        Assert.Null(typeof(AppCommands).GetProperty("SetMode"));
        Assert.Null(typeof(AppCommands).GetMethod("SetMode"));

        // Re-assigning Model with a different mode is possible via the
        // ComponentVM.Model setter, but no API path the AppVM exposes does
        // it — mode must remain tauri after any setTheme.
        cmds.SetTheme("light");
        Assert.Equal("tauri", vm.Model.Mode);
    }

    [Fact]
    public void Scenario_IsStableChildReference()
    {
        var stub = new StubPersistence();
        var vm = AppVMFactory.Create("native", stub.Load, stub.Save);
        var cmds = AppVMFactory.GetCommands(vm);

        Assert.NotNull(cmds.Scenario);
        var before = cmds.Scenario;
        cmds.SetTheme("light");
        // setTheme must not replace the scenario reference.
        Assert.Same(before, cmds.Scenario);
    }
}
