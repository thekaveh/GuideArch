using GuideArch.Models;
using VMx.Components;
using VMx.Services;

namespace GuideArch.ViewModels;

/// <summary>
/// Wraps HelloVmx.Run() as a ComponentVM&lt;Greeting&gt; built via the standard VMx builder.
/// </summary>
/// <remarks>
/// <c>ComponentVM&lt;M&gt;</c> is sealed (subclassing is not supported by the VMx-C# API),
/// so a static factory is the correct pattern for configuring a named, pre-constructed VM
/// that a view can bind to as its DataContext.
/// </remarks>
public static class HelloVmxVMFactory
{
    public static ComponentVM<HelloVmx.Greeting> Create()
    {
        var vm = ComponentVM<HelloVmx.Greeting>.Builder()
            .Name("hello")
            .Model(new HelloVmx.Greeting(HelloVmx.Run()))
            .Services(NullMessageHub.Instance, NullDispatcher.Instance)
            .Build();
        vm.Construct();
        return vm;
    }
}
