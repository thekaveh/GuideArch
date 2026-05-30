using GuideArch.Models;
using VMx.Components;
using VMx.Services;

namespace GuideArch.ViewModels;

/// <summary>
/// Factory for AlternativeVM — wraps an <see cref="AlternativeM"/>.
/// Observable: <c>Id</c> (read-only), <c>DecisionId</c> (read-write),
/// <c>Name</c> (read-write).
/// Changing <c>DecisionId</c> triggers a solve (spec §4.2).
/// Per spec/viewmodels.md §4.2.
/// </summary>
public static class AlternativeVMFactory
{
    public static ComponentVM<AlternativeM> Create(AlternativeM model)
    {
        var vm = ComponentVM<AlternativeM>.Builder()
            .Name($"alternative-vm-{model.Id}")
            .Model(model)
            .Services(NullMessageHub.Instance, NullDispatcher.Instance)
            .Build();
        vm.Construct();
        return vm;
    }
}
