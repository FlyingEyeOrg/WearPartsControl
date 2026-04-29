namespace WearPartsControl.ApplicationServices.PartServices;

/// <summary>
/// 数据值类型枚举。
/// </summary>
public enum PartDataType
{
    /// <summary>
    /// JSON。
    /// </summary>
    Json = 0,

    /// <summary>
    /// 字符串。
    /// </summary>
    String = 1,

    /// <summary>
    /// 16 位整型。
    /// </summary>
    Int16 = 5,

    /// <summary>
    /// 整型。
    /// </summary>
    Int = 2,

    /// <summary>
    /// 无符号 32 位整型。
    /// </summary>
    UInt32 = 6,

    /// <summary>
    /// 浮点型。
    /// </summary>
    Float = 3,

    /// <summary>
    /// 双精度浮点型。
    /// </summary>
    Double = 4,

    /// <summary>
    /// 布尔型。
    /// </summary>
    Bool = 7
}
