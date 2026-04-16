using WearPartsControl.Domain.Exceptions;

namespace WearPartsControl.Domain.ValueObjects;

public readonly record struct ResourceNumber
{
    public ResourceNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainValidationException("ResourceNumber 不能为空。");
        }

        Value = value.Trim();
    }

    public string Value { get; }

    public override string ToString() => Value;
}
