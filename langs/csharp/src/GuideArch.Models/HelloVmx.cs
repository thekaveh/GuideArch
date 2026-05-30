using VMx.Components;
using VMx.Services;

namespace GuideArch.Models;

/// <summary>
/// Smoke-test VMx wiring by instantiating a trivial ComponentVM and
/// returning a descriptive string for the UI.
/// </summary>
public static class HelloVmx
{
    public sealed record Greeting(string Message);

    public static string Run()
    {
        var vm = ComponentVM<Greeting>.Builder()
            .Name("hello")
            .Model(new Greeting("hello from VMx"))
            .Services(NullMessageHub.Instance, NullDispatcher.Instance)
            .Build();

        vm.Construct();
        return $"VMx loaded — model.Message = \"{vm.Model.Message}\", status = {vm.Status}";
    }
}
