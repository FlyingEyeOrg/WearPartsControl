using System.Globalization;
using System.Text;
using WearPartsControl.ApplicationServices.ClientAppInfo;
using WearPartsControl.ApplicationServices.Localization;
using WearPartsControl.Domain.Entities;

namespace WearPartsControl.ApplicationServices.ComNotification;

public sealed record ComNotificationMessage(string Title, string Markdown);
public sealed record ComNotificationPreview(ComNotificationMessage Warning, ComNotificationMessage Shutdown);

public static class ComNotificationMessageFactory
{
    private const string PlaceholderValue = "###";
    private const string TemplatePrefix = "ViewModels.ComNotificationTemplate.";
    private static readonly IReadOnlyDictionary<string, string> LifetimeTypeAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Meter"] = "记米",
        ["Count"] = "计次",
        ["Time"] = "计时",
        ["记米"] = "记米",
        ["计次"] = "计次",
        ["计时"] = "计时"
    };

    public static ComNotificationMessage CreateTestMessage(
        ClientAppInfoModel? clientAppInfo,
        string? meResponsibleName,
        string? meResponsibleWorkId,
        string? prdResponsibleName,
        string? prdResponsibleWorkId,
        string? replacementOperatorName)
    {
        return CreateTestNotificationMessage(
            clientAppInfo,
            meResponsibleName,
            meResponsibleWorkId,
            prdResponsibleName,
            prdResponsibleWorkId,
            replacementOperatorName,
            isShutdown: false);
    }

    public static ComNotificationPreview CreateTestPreview(
        ClientAppInfoModel? clientAppInfo,
        string? meResponsibleName,
        string? meResponsibleWorkId,
        string? prdResponsibleName,
        string? prdResponsibleWorkId,
        string? replacementOperatorName)
    {
        return new ComNotificationPreview(
            CreateTestNotificationMessage(clientAppInfo, meResponsibleName, meResponsibleWorkId, prdResponsibleName, prdResponsibleWorkId, replacementOperatorName, isShutdown: false),
            CreateTestNotificationMessage(clientAppInfo, meResponsibleName, meResponsibleWorkId, prdResponsibleName, prdResponsibleWorkId, replacementOperatorName, isShutdown: true));
    }

    private static ComNotificationMessage CreateTestNotificationMessage(
        ClientAppInfoModel? clientAppInfo,
        string? meResponsibleName,
        string? meResponsibleWorkId,
        string? prdResponsibleName,
        string? prdResponsibleWorkId,
        string? replacementOperatorName,
        bool isShutdown)
    {
        var title = LocalizedText.Get("ViewModels.UserConfigVm.TestNotificationTitle");
        var markdown = BuildMarkdown(
            Template(isShutdown ? "ShutdownHeading" : "WarningHeading"),
            CreateEnvironment(clientAppInfo, usePlaceholder: true),
            CreatePeople(
                meResponsibleName,
                meResponsibleWorkId,
                prdResponsibleName,
                prdResponsibleWorkId,
                replacementOperatorName,
                PlaceholderValue),
            Template("WearPartInfoHeading"),
            [
                new NotificationItem(Template("PartNameLabel"), PlaceholderValue),
                new NotificationItem(Template("BarcodeLabel"), PlaceholderValue),
                new NotificationItem(Template("LifetimeTypeLabel"), PlaceholderValue),
                new NotificationItem(Template("CurrentValueLabel"), PlaceholderValue),
                new NotificationItem(Template("WarningValueLabel"), PlaceholderValue),
                new NotificationItem(Template("ShutdownValueLabel"), PlaceholderValue)
            ],
            Template("NotificationInfoHeading"),
            Template(isShutdown ? "ShutdownActionMessage" : "WarningActionMessage"),
            DateTime.Now);

        return new ComNotificationMessage(title, markdown);
    }

    public static ComNotificationMessage CreateWearPartAlertMessage(
        ClientAppConfigurationEntity clientAppConfiguration,
        string? partBarcode,
        string? lifetimeType,
        string severity,
        string partName,
        double currentValue,
        double warningValue,
        double shutdownValue,
        string? meResponsibleName,
        string? meResponsibleWorkId,
        string? prdResponsibleName,
        string? prdResponsibleWorkId,
        string? replacementOperatorName,
        string? replacementOperatorWorkId,
        DateTime occurredAt)
    {
        var isShutdown = string.Equals(severity, "Shutdown", StringComparison.OrdinalIgnoreCase);
        var title = LocalizedText.Get(isShutdown
            ? "Services.WearPartMonitor.ShutdownNotificationTitle"
            : "Services.WearPartMonitor.WarningNotificationTitle");
        var markdown = BuildMarkdown(
            Template(isShutdown
                ? "ShutdownHeading"
                : "WarningHeading"),
            CreateEnvironment(clientAppConfiguration),
            CreatePeople(
                meResponsibleName,
                meResponsibleWorkId,
                prdResponsibleName,
                prdResponsibleWorkId,
                replacementOperatorName,
                replacementOperatorWorkId),
            Template("WearPartInfoHeading"),
            [
                new NotificationItem(Template("PartNameLabel"), ResolveDisplayValue(partName)),
                new NotificationItem(Template("BarcodeLabel"), ResolveDisplayValue(partBarcode)),
                new NotificationItem(Template("LifetimeTypeLabel"), NormalizeLifetimeType(lifetimeType)),
                new NotificationItem(Template("CurrentValueLabel"), FormatNumber(currentValue)),
                new NotificationItem(Template("WarningValueLabel"), FormatNumber(warningValue)),
                new NotificationItem(Template("ShutdownValueLabel"), FormatNumber(shutdownValue))
            ],
            Template("NotificationInfoHeading"),
            Template(isShutdown
                ? "ShutdownActionMessage"
                : "WarningActionMessage"),
            occurredAt.ToLocalTime());

        return new ComNotificationMessage(title, markdown);
    }

    private static string BuildMarkdown(
        string heading,
        NotificationEnvironment environment,
        NotificationPeople people,
        string wearPartHeading,
        IReadOnlyList<NotificationItem> wearPartItems,
        string notificationHeading,
        string notificationBody,
        DateTime occurredAt)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# {heading}");
        builder.AppendLine();
        AppendSummaryLine(builder, Template("TimeLabel"), occurredAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        AppendSummaryLine(builder, Template("SiteLabel"), environment.SiteCode);
        AppendSummaryLine(builder, Template("FactoryLabel"), environment.FactoryCode);
        AppendSummaryLine(builder, Template("AreaLabel"), environment.AreaCode);
        AppendSummaryLine(builder, Template("ProcedureLabel"), environment.ProcedureCode);
        AppendSummaryLine(builder, Template("EquipmentCodeLabel"), environment.EquipmentCode);
        AppendSummaryLine(builder, Template("ResourceNumberLabel"), environment.ResourceNumber);
        AppendSummaryLine(builder, Template("MeResponsibleLabel"), people.MeResponsible);
        AppendSummaryLine(builder, Template("PrdResponsibleLabel"), people.PrdResponsible);
        AppendSummaryLine(builder, Template("ReplacementPersonLabel"), people.ReplacementOperator);
        builder.AppendLine();
        AppendSection(builder, wearPartHeading, wearPartItems);
        builder.AppendLine();
        AppendNotificationSection(builder, notificationHeading, notificationBody);
        builder.AppendLine();
        builder.AppendLine("---");
        builder.Append(Template("Footer"));
        return builder.ToString();
    }

    private static void AppendSummaryLine(StringBuilder builder, string label, string? value)
    {
        builder.AppendLine($"**{label}**：{ResolveDisplayValue(value)}  ");
    }

    private static void AppendSection(StringBuilder builder, string heading, IReadOnlyList<NotificationItem> items)
    {
        builder.AppendLine($"## {heading}");
        builder.AppendLine();
        foreach (var item in items)
        {
            builder.AppendLine($"- **{item.Label}**：{item.Value}");
        }
    }

    private static void AppendNotificationSection(StringBuilder builder, string heading, string body)
    {
        builder.AppendLine($"## {heading}");
        builder.AppendLine();
        builder.AppendLine(body);
    }

    private static NotificationEnvironment CreateEnvironment(ClientAppInfoModel? clientAppInfo, bool usePlaceholder = false)
    {
        return new NotificationEnvironment(
            usePlaceholder ? PlaceholderValue : clientAppInfo?.SiteCode,
            usePlaceholder ? PlaceholderValue : clientAppInfo?.FactoryCode,
            usePlaceholder ? PlaceholderValue : clientAppInfo?.AreaCode,
            usePlaceholder ? PlaceholderValue : clientAppInfo?.ProcedureCode,
            usePlaceholder ? PlaceholderValue : clientAppInfo?.EquipmentCode,
            usePlaceholder ? PlaceholderValue : clientAppInfo?.ResourceNumber);
    }

    private static NotificationEnvironment CreateEnvironment(ClientAppConfigurationEntity clientAppConfiguration)
    {
        return new NotificationEnvironment(
            clientAppConfiguration.SiteCode,
            clientAppConfiguration.FactoryCode,
            clientAppConfiguration.AreaCode,
            clientAppConfiguration.ProcedureCode,
            clientAppConfiguration.EquipmentCode,
            clientAppConfiguration.ResourceNumber);
    }

    private static NotificationPeople CreatePeople(
        string? meResponsibleName,
        string? meResponsibleWorkId,
        string? prdResponsibleName,
        string? prdResponsibleWorkId,
        string? replacementOperatorName,
        string? replacementOperatorWorkId,
        bool usePlaceholder = false)
    {
        return new NotificationPeople(
            FormatPersonDisplay(meResponsibleName, meResponsibleWorkId, usePlaceholder),
            FormatPersonDisplay(prdResponsibleName, prdResponsibleWorkId, usePlaceholder),
            FormatPersonDisplay(replacementOperatorName, replacementOperatorWorkId, usePlaceholder));
    }

    private static string ResolveDisplayValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? Template("NotConfigured")
            : value.Trim();
    }

    private static string NormalizeLifetimeType(string? value)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return Template("NotConfigured");
        }

        return LifetimeTypeAliases.TryGetValue(normalized, out var alias)
            ? alias
            : normalized;
    }

    private static string FormatPersonDisplay(string? name, string? workId, bool usePlaceholder)
    {
        if (usePlaceholder)
        {
            return $"{PlaceholderValue}({PlaceholderValue})";
        }

        var normalizedName = string.IsNullOrWhiteSpace(name)
            ? Template("NotConfigured")
            : name.Trim();
        var normalizedWorkId = string.IsNullOrWhiteSpace(workId)
            ? Template("NotConfigured")
            : workId.Trim();
        return $"{normalizedName}({normalizedWorkId})";
    }

    private static string Template(string name)
    {
        return LocalizedText.Get($"{TemplatePrefix}{name}");
    }

    private static string FormatNumber(double value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private sealed record NotificationEnvironment(
        string? SiteCode,
        string? FactoryCode,
        string? AreaCode,
        string? ProcedureCode,
        string? EquipmentCode,
        string? ResourceNumber);

    private sealed record NotificationPeople(
        string MeResponsible,
        string PrdResponsible,
        string ReplacementOperator);

    private sealed record NotificationItem(string Label, string Value);
}