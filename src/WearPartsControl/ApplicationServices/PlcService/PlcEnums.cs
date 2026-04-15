namespace WearPartsControl.ApplicationServices.PlcService;

public enum PlcProtocolType
{
    OmronCip,
    OmronFins,
    Mitsubishi,
    SiemensS1500,
    SiemensS1200,
    AllenBradley,
    InovanceAm,
    InovanceH3U,
    InovanceH5U,
    InovanceEip,
    Beckhoff,
    Keyence,
    ModbusTcp
}

public enum PlcDataType
{
    Int,
    DInt,
    UDInt,
    Bool,
    Real,
    LReal,
    String
}
