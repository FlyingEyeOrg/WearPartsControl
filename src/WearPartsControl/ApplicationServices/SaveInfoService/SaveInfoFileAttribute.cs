using System;

namespace WearPartsControl.ApplicationServices.SaveInfoService;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class SaveInfoFileAttribute : Attribute
{
    public SaveInfoFileAttribute(string fileName)
    {
        FileName = fileName;
    }

    public string FileName { get; }

    public string? BaseDirectory { get; init; }
}
