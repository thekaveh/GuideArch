using GuideArch.Models;
using VMx.Components;
using VMx.Services;

namespace GuideArch.ViewModels;

/// <summary>
/// Factory for CoefficientVM — wraps a <see cref="CoefficientM"/>.
/// Exposes <c>Lower</c>, <c>Modal</c>, <c>Upper</c> as read-write doubles.
/// Editing any cell triggers a solve (spec §5.4).
/// Per spec/viewmodels.md §5.4.
/// </summary>
public static class CoefficientVMFactory
{
    public static ComponentVM<CoefficientM> Create(CoefficientM model)
    {
        var vm = ComponentVM<CoefficientM>.Builder()
            .Name($"coeff-vm-{model.AlternativeId}-{model.PropertyId}")
            .Model(model)
            .Services(NullMessageHub.Instance, NullDispatcher.Instance)
            .Build();
        vm.Construct();
        return vm;
    }
}
