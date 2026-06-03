using GuideArch.Models;
using VMx.Components;
using VMx.Services;

namespace GuideArch.ViewModels;

/// <summary>
/// Factory for CriticalConstraintVM — read-only result wrapper for a
/// <see cref="CriticalConstraintM"/>. No commands. No mutation paths.
/// Per spec/viewmodels.md §5.6.
/// </summary>
public static class CriticalConstraintVMFactory
{
    public static ComponentVM<CriticalConstraintM> Create(CriticalConstraintM model)
    {
        var vm = ComponentVM<CriticalConstraintM>.Builder()
            .Name($"critical-constraint-vm-{model.ConstraintIndex}")
            .Model(model)
            .Services(NullMessageHub.Instance, NullDispatcher.Instance)
            .Build();
        vm.Construct();
        return vm;
    }
}
