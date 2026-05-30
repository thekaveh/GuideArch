using GuideArch.Models;
using VMx.Components;
using VMx.Services;

namespace GuideArch.ViewModels;

/// <summary>
/// Factory for PropertyVM — wraps a <see cref="PropertyM"/>.
/// Observable: <c>Id</c> (read-only), <c>Name</c> (read-write),
/// <c>Kind</c> (read-write), <c>Weight</c> (read-write).
/// Changing <c>Kind</c> or <c>Weight</c> triggers a solve (spec §4.3).
/// Per spec/viewmodels.md §4.3.
/// </summary>
public static class PropertyVMFactory
{
    public static ComponentVM<PropertyM> Create(PropertyM model)
    {
        var vm = ComponentVM<PropertyM>.Builder()
            .Name($"property-vm-{model.Id}")
            .Model(model)
            .Services(NullMessageHub.Instance, NullDispatcher.Instance)
            .Build();
        vm.Construct();
        return vm;
    }
}
