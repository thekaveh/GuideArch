using VMx.Components;
using VMx.Services;

namespace GuideArch.ViewModels;

/// <summary>
/// Placeholder VM factory for M1.
/// Returns a ComponentVM carrying a simple status string.
/// M2 will replace this with a richer GuideArch domain VM.
/// </summary>
public static class HelloVmxVMFactory
{
    public sealed record StatusModel(string Message);

    public static ComponentVM<StatusModel> Create()
    {
        var vm = ComponentVM<StatusModel>.Builder()
            .Name("guidearch-status")
            .Model(new StatusModel("M1: domain ready; UI in M2"))
            .Services(NullMessageHub.Instance, NullDispatcher.Instance)
            .Build();
        vm.Construct();
        return vm;
    }
}
