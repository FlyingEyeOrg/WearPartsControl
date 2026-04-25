namespace WearPartsControl.ApplicationServices.PartServices;

public static class CutterReplacementValidationPolicy
{
    public static bool RequiresCutterValidation(string? procedureCode, string? wearPartTypeCode)
    {
        var normalizedProcedure = procedureCode?.Trim() ?? string.Empty;
        var normalizedWearPartType = wearPartTypeCode?.Trim() ?? string.Empty;
        return (string.Equals(normalizedProcedure, "模切分条", StringComparison.Ordinal)
                || string.Equals(normalizedProcedure, "DieCutSlitting", StringComparison.OrdinalIgnoreCase))
            && string.Equals(normalizedWearPartType, WearPartTypeCodes.Cutter, StringComparison.OrdinalIgnoreCase);
    }

    public static string? ResolveMesParameter(string? partName, string? toolCode)
    {
        if (ContainsUpperPosition(partName) || ContainsUpperPosition(toolCode))
        {
            return "QDBH-UP";
        }

        if (ContainsLowerPosition(partName) || ContainsLowerPosition(toolCode))
        {
            return "QDBH-DOWN";
        }

        return null;
    }

    private static bool ContainsUpperPosition(string? value)
    {
        var normalized = value?.Trim() ?? string.Empty;
        return normalized.Contains("上刀", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("UP", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsLowerPosition(string? value)
    {
        var normalized = value?.Trim() ?? string.Empty;
        return normalized.Contains("下刀", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("DOWN", StringComparison.OrdinalIgnoreCase);
    }
}