namespace WearPartsControl.ApplicationServices.ClientAppInfo;

public sealed class ClientAppInfoModel
{
    public Guid? Id { get; set; }

    public string SiteCode { get; set; } = string.Empty;

    public string FactoryCode { get; set; } = string.Empty;

    public string AreaCode { get; set; } = string.Empty;

    public string ProcedureCode { get; set; } = string.Empty;

    public string EquipmentCode { get; set; } = string.Empty;

    public string ResourceNumber { get; set; } = string.Empty;

    public string PlcProtocolType { get; set; } = string.Empty;

    public string PlcIpAddress { get; set; } = string.Empty;

    public int PlcPort { get; set; } = 102;

    public string ShutdownPointAddress { get; set; } = string.Empty;

    public bool EnableCutterMesValidation { get; set; }

    public string CutterMesWsdl { get; set; } = string.Empty;

    public string CutterMesUser { get; set; } = string.Empty;

    public string CutterMesPassword { get; set; } = string.Empty;

    public string CutterMesSite { get; set; } = string.Empty;

    public int SiemensRack { get; set; }

    public int SiemensSlot { get; set; }

    public bool IsStringReverse { get; set; } = true;
}