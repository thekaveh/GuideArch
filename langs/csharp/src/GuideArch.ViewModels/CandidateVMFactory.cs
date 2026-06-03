using GuideArch.Models;
using VMx.Components;
using VMx.Services;

namespace GuideArch.ViewModels;

/// <summary>
/// Factory for CandidateVM — read-only result wrapper for a <see cref="CandidateM"/>.
/// No commands. No mutation paths.
/// Per spec/viewmodels.md §5.6.
/// </summary>
public static class CandidateVMFactory
{
    public static ComponentVM<CandidateM> Create(CandidateM model)
    {
        var vm = ComponentVM<CandidateM>.Builder()
            .Name($"candidate-vm-rank{model.Rank}")
            .Model(model)
            .Services(NullMessageHub.Instance, NullDispatcher.Instance)
            .Build();
        vm.Construct();
        return vm;
    }
}
