using GuideArch.Models;
using VMx.Components;
using VMx.Services;

namespace GuideArch.ViewModels;

/// <summary>
/// Factory for DecisionVM — wraps a <see cref="DecisionM"/>.
/// Observable: <c>Id</c> (read-only), <c>Name</c> (read-write).
/// Per spec/viewmodels.md §4.1.
/// </summary>
public static class DecisionVMFactory
{
    public static ComponentVM<DecisionM> Create(DecisionM model)
    {
        var vm = ComponentVM<DecisionM>.Builder()
            .Name($"decision-vm-{model.Id}")
            .Model(model)
            .Services(NullMessageHub.Instance, NullDispatcher.Instance)
            .Build();
        vm.Construct();
        return vm;
    }
}
