using System.IO;
using System.Text;
using LocalizationResourceGenerator;
using Xunit;

namespace WearPartsControl.Tests;

public sealed class LocalizationResourceGeneratorTests : IDisposable
{
    private readonly string _rootDirectory;
    private readonly string _sourceDirectory;
    private readonly string _outputDirectory;
    private readonly string _codeOutputPath;

    public LocalizationResourceGeneratorTests()
    {
        _rootDirectory = Path.Combine(Path.GetTempPath(), "WearPartsControl.LocalizationGenerator.Tests", Guid.NewGuid().ToString("N"));
        _sourceDirectory = Path.Combine(_rootDirectory, "Localization");
        _outputDirectory = Path.Combine(_rootDirectory, "Generated");
        _codeOutputPath = Path.Combine(_rootDirectory, "Generated", "LocalizationCatalog.g.cs");
        Directory.CreateDirectory(_sourceDirectory);
    }

    [Fact]
    public void Generate_ShouldCreateResxAndCode_ForValidJson()
    {
        WriteJson("LocalizationResource.json", """
        {
          "FriendlyErrorTitle": "Notice",
          "UnexpectedError": "An unexpected error occurred.",
          "MainWindow": {
            "Title": "WearPartsControl",
            "Tabs": ["Tab A", "Tab B"]
          }
        }
        """);
        WriteJson("LocalizationResource.zh-CN.json", """
        {
          "FriendlyErrorTitle": "提示",
          "UnexpectedError": "出现异常。",
          "MainWindow": {
            "Title": "系统",
            "Tabs": ["标签A", "标签B"]
          }
        }
        """);

        LocalizationArtifactGenerator.Generate(_sourceDirectory, _outputDirectory, _codeOutputPath);

        var neutralResxPath = Path.Combine(_outputDirectory, "LocalizationResource.resx");
        var chineseResxPath = Path.Combine(_outputDirectory, "LocalizationResource.zh-CN.resx");

        Assert.True(File.Exists(neutralResxPath));
        Assert.True(File.Exists(chineseResxPath));
        Assert.True(File.Exists(_codeOutputPath));

        var neutralResx = File.ReadAllText(neutralResxPath, Encoding.UTF8);
        var generatedCode = File.ReadAllText(_codeOutputPath, Encoding.UTF8);

        Assert.Contains("MainWindow.Tabs.1", neutralResx, StringComparison.Ordinal);
        Assert.Contains("public sealed class LocalizationCatalog", generatedCode, StringComparison.Ordinal);
        Assert.Contains("public MainWindowSection MainWindow => new(Getter, Key(\"MainWindow\"));", generatedCode, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_ShouldDeleteStaleResxOutputs()
    {
        WriteValidJsonSet();
        Directory.CreateDirectory(_outputDirectory);
        File.WriteAllText(Path.Combine(_outputDirectory, "LocalizationResource.fr-FR.resx"), "stale", Encoding.UTF8);

        LocalizationArtifactGenerator.Generate(_sourceDirectory, _outputDirectory, _codeOutputPath);

        Assert.False(File.Exists(Path.Combine(_outputDirectory, "LocalizationResource.fr-FR.resx")));
    }

    [Fact]
    public void Generate_ShouldThrow_WhenBaseFileMissing()
    {
        WriteJson("LocalizationResource.zh-CN.json", "{}\n");

        var exception = Assert.Throws<FileNotFoundException>(() => LocalizationArtifactGenerator.Generate(_sourceDirectory, _outputDirectory, _codeOutputPath));

        Assert.Contains("LocalizationResource.json is required as the schema source.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_ShouldThrow_WhenPropertyMissingInCultureFile()
    {
        WriteJson("LocalizationResource.json", """
        {
          "FriendlyErrorTitle": "Notice",
          "MainWindow": {
            "Title": "WearPartsControl"
          }
        }
        """);
        WriteJson("LocalizationResource.zh-CN.json", """
        {
          "MainWindow": {
            "Title": "系统"
          }
        }
        """);

        var exception = Assert.Throws<InvalidOperationException>(() => LocalizationArtifactGenerator.Generate(_sourceDirectory, _outputDirectory, _codeOutputPath));

        Assert.Contains("missing property FriendlyErrorTitle", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_ShouldThrow_WhenPropertyTypeMismatched()
    {
        WriteJson("LocalizationResource.json", """
        {
          "MainWindow": {
            "Title": "WearPartsControl"
          }
        }
        """);
        WriteJson("LocalizationResource.zh-CN.json", """
        {
          "MainWindow": "系统"
        }
        """);

        var exception = Assert.Throws<InvalidOperationException>(() => LocalizationArtifactGenerator.Generate(_sourceDirectory, _outputDirectory, _codeOutputPath));

        Assert.Contains("MainWindow is expected to be an object.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_ShouldThrow_WhenArrayLengthMismatched()
    {
        WriteJson("LocalizationResource.json", """
        {
          "MainWindow": {
            "Tabs": ["A", "B"]
          }
        }
        """);
        WriteJson("LocalizationResource.zh-CN.json", """
        {
          "MainWindow": {
            "Tabs": ["甲"]
          }
        }
        """);

        var exception = Assert.Throws<InvalidOperationException>(() => LocalizationArtifactGenerator.Generate(_sourceDirectory, _outputDirectory, _codeOutputPath));

        Assert.Contains("MainWindow.Tabs length mismatch", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_ShouldThrow_WhenNestedArraysEncountered()
    {
        WriteJson("LocalizationResource.json", """
        {
          "Tabs": [["A"], ["B"]]
        }
        """);

        var exception = Assert.Throws<InvalidOperationException>(() => LocalizationArtifactGenerator.Generate(_sourceDirectory, _outputDirectory, _codeOutputPath));

        Assert.Contains("Nested arrays are not supported", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_ShouldEscapeSpecialCharactersInResx()
    {
        WriteJson("LocalizationResource.json", """
        {
          "FriendlyErrorTitle": "Notice & <alert>",
          "UnexpectedError": "A \"quoted\" value"
        }
        """);

        LocalizationArtifactGenerator.Generate(_sourceDirectory, _outputDirectory, _codeOutputPath);

        var neutralResx = File.ReadAllText(Path.Combine(_outputDirectory, "LocalizationResource.resx"), Encoding.UTF8);

        Assert.Contains("Notice &amp; &lt;alert&gt;", neutralResx, StringComparison.Ordinal);
        Assert.Contains("A \"quoted\" value", neutralResx, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_ShouldReturnUsageError_WhenArgumentsMissing()
    {
        using var writer = new StringWriter();

        var exitCode = LocalizationGeneratorCli.Run([], writer);

        Assert.Equal(1, exitCode);
        Assert.Contains("Usage: LocalizationResourceGenerator", writer.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Run_ShouldReturnDirectoryError_WhenSourceDirectoryMissing()
    {
        using var writer = new StringWriter();

        var exitCode = LocalizationGeneratorCli.Run([Path.Combine(_rootDirectory, "Missing"), _outputDirectory, _codeOutputPath], writer);

        Assert.Equal(2, exitCode);
        Assert.Contains("Localization source directory not found", writer.ToString(), StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
    }

    private void WriteValidJsonSet()
    {
        WriteJson("LocalizationResource.json", """
        {
          "FriendlyErrorTitle": "Notice",
          "UnexpectedError": "An unexpected error occurred.",
          "MainWindow": {
            "Title": "WearPartsControl",
            "Tabs": ["Tab A", "Tab B"]
          }
        }
        """);
        WriteJson("LocalizationResource.en-US.json", """
        {
          "FriendlyErrorTitle": "Notice",
          "UnexpectedError": "An unexpected error occurred.",
          "MainWindow": {
            "Title": "WearPartsControl",
            "Tabs": ["Tab A", "Tab B"]
          }
        }
        """);
    }

    private void WriteJson(string fileName, string content)
    {
        File.WriteAllText(Path.Combine(_sourceDirectory, fileName), content.Replace("\n", Environment.NewLine, StringComparison.Ordinal), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }
}