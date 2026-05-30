using GuideArch.Models;
using VMx.Components;
using VMx.Services;

namespace GuideArch.ViewModels;

/// <summary>
/// Factory for ConstraintVM — wraps a <see cref="ConstraintM"/> (any flavor).
/// Edits trigger a solve (spec §4.5).
/// Per spec/viewmodels.md §4.5.
/// </summary>
public static class ConstraintVMFactory
{
    public static ComponentVM<ConstraintM> Create(ConstraintM model, int index)
    {
        var vm = ComponentVM<ConstraintM>.Builder()
            .Name($"constraint-vm-{index}")
            .Model(model)
            .Services(NullMessageHub.Instance, NullDispatcher.Instance)
            .Build();
        vm.Construct();
        return vm;
    }
}
