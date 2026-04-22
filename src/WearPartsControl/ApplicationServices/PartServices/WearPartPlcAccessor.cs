using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.ApplicationServices.PlcService;
using WearPartsControl.Domain.Entities;
using WearPartsControl.Exceptions;

namespace WearPartsControl.ApplicationServices.PartServices;

internal static class WearPartPlcAccessor
{
    private const string SkipAddress = "######";

    public static PlcConnectionOptions BuildConnectionOptions(ClientAppConfigurationEntity configuration)
    {
        return PlcConnectionOptionsFactory.Create(configuration);
    }

    public static async Task<string> ReadAsStringAsync(IPlcOperationPipeline pipeline, string operationName, string address, string dataType, CancellationToken cancellationToken = default)
    {
        if (ShouldSkip(address))
        {
            return string.Empty;
        }

        return NormalizeDataType(dataType) switch
        {
            "INT" or "INT32" => (await pipeline.ReadAsync<int>(operationName, address, cancellationToken: cancellationToken).ConfigureAwait(false)).ToString(),
            "FLOAT" or "SINGLE" => (await pipeline.ReadAsync<float>(operationName, address, cancellationToken: cancellationToken).ConfigureAwait(false)).ToString(System.Globalization.CultureInfo.InvariantCulture),
            "DOUBLE" or "DECIMAL" => (await pipeline.ReadAsync<double>(operationName, address, cancellationToken: cancellationToken).ConfigureAwait(false)).ToString(System.Globalization.CultureInfo.InvariantCulture),
            "BOOL" or "BOOLEAN" => await pipeline.ReadAsync<bool>(operationName, address, cancellationToken: cancellationToken).ConfigureAwait(false) ? "1" : "0",
            _ => await pipeline.ReadAsync<string>(operationName, address, cancellationToken: cancellationToken).ConfigureAwait(false)
        };
    }

    public static async Task<double> ReadAsDoubleAsync(IPlcOperationPipeline pipeline, string operationName, string address, string dataType, CancellationToken cancellationToken = default)
    {
        if (ShouldSkip(address))
        {
            return 0d;
        }

        return NormalizeDataType(dataType) switch
        {
            "INT" or "INT32" => await pipeline.ReadAsync<int>(operationName, address, cancellationToken: cancellationToken).ConfigureAwait(false),
            "FLOAT" or "SINGLE" => await pipeline.ReadAsync<float>(operationName, address, cancellationToken: cancellationToken).ConfigureAwait(false),
            "DOUBLE" or "DECIMAL" => await pipeline.ReadAsync<double>(operationName, address, cancellationToken: cancellationToken).ConfigureAwait(false),
            "BOOL" or "BOOLEAN" => await pipeline.ReadAsync<bool>(operationName, address, cancellationToken: cancellationToken).ConfigureAwait(false) ? 1d : 0d,
            _ => ParseDouble(await pipeline.ReadAsync<string>(operationName, address, cancellationToken: cancellationToken).ConfigureAwait(false), address)
        };
    }

    public static Task ClearCounterAsync(IPlcOperationPipeline pipeline, string operationName, string address, CancellationToken cancellationToken = default)
    {
        if (ShouldSkip(address))
        {
            return Task.CompletedTask;
        }

        return pipeline.WriteAsync(operationName, address, 0, cancellationToken: cancellationToken);
    }

    public static async Task PulseZeroClearSignalAsync(IPlcOperationPipeline pipeline, string operationName, string address, CancellationToken cancellationToken = default)
    {
        if (ShouldSkip(address))
        {
            return;
        }

        await pipeline.WriteAsync(operationName, address, true, cancellationToken: cancellationToken).ConfigureAwait(false);
        await pipeline.WriteAsync(operationName, address, false, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public static Task WriteCurrentValueAsync(IPlcOperationPipeline pipeline, string operationName, string address, string dataType, double value, CancellationToken cancellationToken = default)
    {
        if (ShouldSkip(address))
        {
            return Task.CompletedTask;
        }

        switch (NormalizeDataType(dataType))
        {
            case "INT":
            case "INT32":
                return pipeline.WriteAsync(address: address, operationName: operationName, value: Convert.ToInt32(Math.Round(value, MidpointRounding.AwayFromZero), System.Globalization.CultureInfo.InvariantCulture), cancellationToken: cancellationToken);
            case "FLOAT":
            case "SINGLE":
                return pipeline.WriteAsync(address: address, operationName: operationName, value: Convert.ToSingle(value, System.Globalization.CultureInfo.InvariantCulture), cancellationToken: cancellationToken);
            case "DOUBLE":
            case "DECIMAL":
                return pipeline.WriteAsync(address: address, operationName: operationName, value: value, cancellationToken: cancellationToken);
            case "BOOL":
            case "BOOLEAN":
                return pipeline.WriteAsync(address: address, operationName: operationName, value: value > 0d, cancellationToken: cancellationToken);
            default:
                return pipeline.WriteAsync(address: address, operationName: operationName, value: value.ToString(System.Globalization.CultureInfo.InvariantCulture), cancellationToken: cancellationToken);
        }
    }

    public static Task WriteBarcodeAsync(IPlcOperationPipeline pipeline, string operationName, string address, string barcode, CancellationToken cancellationToken = default)
    {
        if (ShouldSkip(address))
        {
            return Task.CompletedTask;
        }

        return pipeline.WriteAsync(operationName, address, barcode, cancellationToken: cancellationToken);
    }

    public static Task WriteShutdownSignalAsync(IPlcOperationPipeline pipeline, string operationName, string address, bool shutdown, CancellationToken cancellationToken = default)
    {
        if (ShouldSkip(address))
        {
            return Task.CompletedTask;
        }

        var trimmed = address.Trim();
        var invert = trimmed.StartsWith('!');
        var actualAddress = invert ? trimmed[1..] : trimmed;
        return pipeline.WriteAsync(operationName, actualAddress, invert ? !shutdown : shutdown, cancellationToken: cancellationToken);
    }

    public static PartDataType? ResolvePartDataType(string? dataType)
    {
        return NormalizeDataType(dataType) switch
        {
            "JSON" => PartDataType.Json,
            "STRING" => PartDataType.String,
            "INT" or "INT32" => PartDataType.Int,
            "FLOAT" or "SINGLE" => PartDataType.Float,
            "DOUBLE" or "DECIMAL" => PartDataType.Double,
            _ => null
        };
    }

    private static bool ShouldSkip(string address)
    {
        return string.IsNullOrWhiteSpace(address) || string.Equals(address.Trim(), SkipAddress, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeDataType(string? dataType)
    {
        return string.IsNullOrWhiteSpace(dataType) ? string.Empty : dataType.Trim().ToUpperInvariant();
    }

    private static double ParseDouble(string rawValue, string address)
    {
        if (double.TryParse(rawValue, System.Globalization.NumberStyles.Float | System.Globalization.NumberStyles.AllowThousands, System.Globalization.CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        if (double.TryParse(rawValue, out value))
        {
            return value;
        }

        throw new UserFriendlyException(LocalizedText.Format("Services.WearPartReplacement.ValueCannotConvert", address, rawValue));
    }
}