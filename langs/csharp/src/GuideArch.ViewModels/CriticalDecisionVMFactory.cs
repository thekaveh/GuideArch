using GuideArch.Models;
using VMx.Components;
using VMx.Services;

namespace GuideArch.ViewModels;

/// <summary>
/// Factory for CriticalDecisionVM — read-only result wrapper for a
/// <see cref="CriticalDecisionM"/>. No commands. No mutation paths.
/// Per spec/viewmodels.md §4.6.
/// </summary>
public static class CriticalDecisionVMFactory
{
    public static ComponentVM<CriticalDecisionM> Create(CriticalDecisionM model)
    {
        var vm = ComponentVM<CriticalDecisionM>.Builder()
            .Name($"critical-decision-vm-rank{model.Rank}")
            .Model(model)
            .Services(NullMessageHub.Instance, NullDispatcher.Instance)
            .Build();
        vm.Construct();
        return vm;
    }
}
