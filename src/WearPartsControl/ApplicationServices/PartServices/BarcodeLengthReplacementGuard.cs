using WearPartsControl.Exceptions;

namespace WearPartsControl.ApplicationServices.PartServices;

public sealed class BarcodeLengthReplacementGuard : IWearPartReplacementGuard
{
    public int Order => 100;

    public Task ValidateAsync(WearPartReplacementGuardContext context, CancellationToken cancellationToken = default)
    {
        if (context.NormalizedBarcode.Length < context.Definition.CodeMinLength)
        {
            throw new UserFriendlyException($"条码长度不能小于 {context.Definition.CodeMinLength}。", code: "WearPartReplacement:BarcodeTooShort");
        }

        if (context.NormalizedBarcode.Length > context.Definition.CodeMaxLength)
        {
            throw new UserFriendlyException($"条码长度不能大于 {context.Definition.CodeMaxLength}。", code: "WearPartReplacement:BarcodeTooLong");
        }

        return Task.CompletedTask;
    }
}