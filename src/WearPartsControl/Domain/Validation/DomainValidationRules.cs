using WearPartsControl.Domain.Exceptions;

namespace WearPartsControl.Domain.Validation;

public static class DomainValidationRules
{
    public static void NotWhiteSpace(string value, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainValidationException($"字段 {propertyName} 不能为空。")
                .WithData("PropertyName", propertyName);
        }
    }

    public static void MaxLength(string value, int maxLength, string propertyName)
    {
        if (value.Length > maxLength)
        {
            throw new DomainValidationException($"字段 {propertyName} 长度不能超过 {maxLength}。")
                .WithData("PropertyName", propertyName)
                .WithData("MaxLength", maxLength)
                .WithData("ActualLength", value.Length);
        }
    }
}
