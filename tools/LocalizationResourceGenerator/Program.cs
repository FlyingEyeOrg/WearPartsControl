using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace LocalizationResourceGenerator
{

internal static class Program
{
    public static int Main(string[] args)
    {
        return LocalizationGeneratorCli.Run(args, Console.Error);
    }
}

public static class LocalizationGeneratorCli
{
    public static int Run(string[] args, TextWriter errorWriter)
    {
        if (args.Length != 3)
        {
            errorWriter.WriteLine("Usage: LocalizationResourceGenerator <sourceDir> <resxOutputDir> <codeOutputFile>");
            return 1;
        }

        try
        {
            LocalizationArtifactGenerator.Generate(args[0], args[1], args[2]);
            return 0;
        }
        catch (DirectoryNotFoundException exception)
        {
            errorWriter.WriteLine(exception.Message);
            return 2;
        }
        catch (FileNotFoundException exception)
        {
            errorWriter.WriteLine(exception.Message);
            return 3;
        }
        catch (Exception exception)
        {
            errorWriter.WriteLine(exception.Message);
            return 4;
        }
    }
}

public static class LocalizationArtifactGenerator
{
    public static void Generate(string sourceDirectory, string resxOutputDirectory, string codeOutputPath)
    {
        var fullSourceDirectory = Path.GetFullPath(sourceDirectory);
        var fullResxOutputDirectory = Path.GetFullPath(resxOutputDirectory);
        var fullCodeOutputPath = Path.GetFullPath(codeOutputPath);

        if (!Directory.Exists(fullSourceDirectory))
        {
            throw new DirectoryNotFoundException($"Localization source directory not found: {fullSourceDirectory}");
        }

        Directory.CreateDirectory(fullResxOutputDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(fullCodeOutputPath)!);

        var jsonFiles = Directory.GetFiles(fullSourceDirectory, "LocalizationResource*.json", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (jsonFiles.Length == 0)
        {
            throw new FileNotFoundException("No JSON localization files were found.");
        }

        var baseFile = jsonFiles.FirstOrDefault(path => string.Equals(Path.GetFileName(path), "LocalizationResource.json", StringComparison.OrdinalIgnoreCase));
        if (baseFile is null)
        {
            throw new FileNotFoundException("LocalizationResource.json is required as the schema source.");
        }

        var documents = jsonFiles.ToDictionary(
            path => Path.GetFileName(path),
            path => JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8)),
            StringComparer.OrdinalIgnoreCase);

        try
        {
            var schema = LocalizationNode.CreateRoot(documents[Path.GetFileName(baseFile)].RootElement);

            DeleteStaleGeneratedResxFiles(fullResxOutputDirectory);

            foreach (var (fileName, document) in documents)
            {
                schema.Validate(document.RootElement, fileName, "$", isRoot: true);
                var values = new SortedDictionary<string, string>(StringComparer.Ordinal);
                schema.Flatten(document.RootElement, values, prefix: null);
                WriteResx(Path.Combine(fullResxOutputDirectory, Path.GetFileNameWithoutExtension(fileName) + ".resx"), values);
            }

            WriteFileIfChanged(fullCodeOutputPath, LocalizationCodeWriter.Write(schema));
        }
        finally
        {
            foreach (var document in documents.Values)
            {
                document.Dispose();
            }
        }
    }

    private static void DeleteStaleGeneratedResxFiles(string resxOutputDirectory)
    {
        foreach (var filePath in Directory.GetFiles(resxOutputDirectory, "LocalizationResource*.resx", SearchOption.TopDirectoryOnly))
        {
            File.Delete(filePath);
        }
    }

    private static void WriteResx(string outputPath, IReadOnlyDictionary<string, string> values)
    {
        var root = new XElement("root",
            new XElement("resheader", new XAttribute("name", "resmimetype"), new XElement("value", "text/microsoft-resx")),
            new XElement("resheader", new XAttribute("name", "version"), new XElement("value", "2.0")),
            new XElement("resheader", new XAttribute("name", "reader"), new XElement("value", "System.Resources.ResXResourceReader, System.Windows.Forms, ...")),
            new XElement("resheader", new XAttribute("name", "writer"), new XElement("value", "System.Resources.ResXResourceWriter, System.Windows.Forms, ...")));

        foreach (var item in values)
        {
            root.Add(new XElement("data",
                new XAttribute("name", item.Key),
                new XAttribute(XNamespace.Xml + "space", "preserve"),
                new XElement("value", item.Value)));
        }

        var document = new XDocument(new XDeclaration("1.0", "utf-8", null), root);
        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Indent = true,
            NewLineChars = Environment.NewLine,
            NewLineHandling = NewLineHandling.Replace,
            OmitXmlDeclaration = false
        };

        using var stream = new MemoryStream();
        using (var writer = XmlWriter.Create(stream, settings))
        {
            document.Save(writer);
        }

        WriteBytesIfChanged(outputPath, stream.ToArray());
    }

    private static void WriteFileIfChanged(string filePath, string content)
    {
        if (File.Exists(filePath))
        {
            var existing = File.ReadAllText(filePath, Encoding.UTF8);
            if (string.Equals(existing, content, StringComparison.Ordinal))
            {
                return;
            }
        }

        File.WriteAllText(filePath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static void WriteBytesIfChanged(string filePath, byte[] content)
    {
        if (File.Exists(filePath))
        {
            var existing = File.ReadAllBytes(filePath);
            if (existing.AsSpan().SequenceEqual(content))
            {
                return;
            }
        }

        File.WriteAllBytes(filePath, content);
    }
}

public sealed class LocalizationNode
{
    private LocalizationNode(string name, string typeName, NodeKind kind)
    {
        Name = name;
        TypeName = typeName;
        Kind = kind;
    }

    public string Name { get; }
    public string TypeName { get; }
    public NodeKind Kind { get; }
    public List<LocalizationNode> Children { get; } = new();
    public LocalizationNode? ItemTemplate { get; private set; }
    public int ArrayLength { get; private set; }

    public static LocalizationNode CreateRoot(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("The root localization JSON must be an object.");
        }

        return Create(element, name: string.Empty, typeName: "LocalizationCatalog", isRoot: true);
    }

    public void Validate(JsonElement element, string fileName, string path, bool isRoot = false)
    {
        switch (Kind)
        {
            case NodeKind.Object:
                if (element.ValueKind != JsonValueKind.Object)
                {
                    throw new InvalidOperationException($"{fileName}: {path} is expected to be an object.");
                }

                var properties = element.EnumerateObject().ToDictionary(property => property.Name, property => property.Value, StringComparer.Ordinal);
                foreach (var child in Children)
                {
                    if (!properties.TryGetValue(child.Name, out var childElement))
                    {
                        throw new InvalidOperationException($"{fileName}: missing property {BuildPath(path, child.Name, isRoot)}.");
                    }

                    child.Validate(childElement, fileName, BuildPath(path, child.Name, isRoot));
                }
                break;

            case NodeKind.Value:
                if (!IsScalar(element))
                {
                    throw new InvalidOperationException($"{fileName}: {path} is expected to be a scalar value.");
                }
                break;

            case NodeKind.ValueArray:
                if (element.ValueKind != JsonValueKind.Array)
                {
                    throw new InvalidOperationException($"{fileName}: {path} is expected to be an array.");
                }

                var primitiveItems = element.EnumerateArray().ToArray();
                if (primitiveItems.Length != ArrayLength)
                {
                    throw new InvalidOperationException($"{fileName}: {path} length mismatch. Expected {ArrayLength}, actual {primitiveItems.Length}.");
                }

                for (var i = 0; i < primitiveItems.Length; i++)
                {
                    if (!IsScalar(primitiveItems[i]))
                    {
                        throw new InvalidOperationException($"{fileName}: {path}[{i}] is expected to be a scalar value.");
                    }
                }
                break;

            case NodeKind.ObjectArray:
                if (element.ValueKind != JsonValueKind.Array)
                {
                    throw new InvalidOperationException($"{fileName}: {path} is expected to be an array.");
                }

                var objectItems = element.EnumerateArray().ToArray();
                if (objectItems.Length != ArrayLength)
                {
                    throw new InvalidOperationException($"{fileName}: {path} length mismatch. Expected {ArrayLength}, actual {objectItems.Length}.");
                }

                for (var i = 0; i < objectItems.Length; i++)
                {
                    ItemTemplate!.Validate(objectItems[i], fileName, $"{path}[{i}]", isRoot: false);
                }
                break;
        }
    }

    public void Flatten(JsonElement element, IDictionary<string, string> values, string? prefix)
    {
        switch (Kind)
        {
            case NodeKind.Object:
                foreach (var child in Children)
                {
                    child.Flatten(element.GetProperty(child.Name), values, BuildPrefix(prefix, child.Name));
                }
                break;

            case NodeKind.Value:
                values[prefix!] = ReadScalar(element);
                break;

            case NodeKind.ValueArray:
                var primitiveItems = element.EnumerateArray().ToArray();
                for (var i = 0; i < primitiveItems.Length; i++)
                {
                    values[$"{prefix}.{i}"] = ReadScalar(primitiveItems[i]);
                }
                break;

            case NodeKind.ObjectArray:
                var objectItems = element.EnumerateArray().ToArray();
                for (var i = 0; i < objectItems.Length; i++)
                {
                    ItemTemplate!.Flatten(objectItems[i], values, $"{prefix}.{i}");
                }
                break;
        }
    }

    private static LocalizationNode Create(JsonElement element, string name, string typeName, bool isRoot)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => CreateObject(element, name, typeName),
            JsonValueKind.Array => CreateArray(element, name, typeName),
            _ when IsScalar(element) => new LocalizationNode(name, typeName, NodeKind.Value),
            _ => throw new InvalidOperationException($"Unsupported localization node type at {(isRoot ? "$" : name)}.")
        };
    }

    private static LocalizationNode CreateObject(JsonElement element, string name, string typeName)
    {
        var node = new LocalizationNode(name, typeName, NodeKind.Object);
        foreach (var property in element.EnumerateObject())
        {
            node.Children.Add(Create(property.Value, property.Name, ToPascalCase(property.Name) + "Section", isRoot: false));
        }

        return node;
    }

    private static LocalizationNode CreateArray(JsonElement element, string name, string typeName)
    {
        var items = element.EnumerateArray().ToArray();
        if (items.Length == 0)
        {
            return new LocalizationNode(name, typeName, NodeKind.ValueArray) { ArrayLength = 0 };
        }

        if (items[0].ValueKind == JsonValueKind.Object)
        {
            var node = new LocalizationNode(name, typeName, NodeKind.ObjectArray) { ArrayLength = items.Length };
            node.ItemTemplate = CreateObject(items[0], name + "Item", ToPascalCase(name) + "ItemSection");
            return node;
        }

        if (items[0].ValueKind == JsonValueKind.Array)
        {
            throw new InvalidOperationException($"Nested arrays are not supported for localization node '{name}'.");
        }

        var primitiveNode = new LocalizationNode(name, typeName, NodeKind.ValueArray) { ArrayLength = items.Length };
        return primitiveNode;
    }

    private static bool IsScalar(JsonElement element)
    {
        return element.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null;
    }

    private static string ReadScalar(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            JsonValueKind.Null => string.Empty,
            _ => element.GetRawText()
        };
    }

    private static string BuildPath(string path, string name, bool isRoot)
    {
        return isRoot || string.IsNullOrEmpty(path) || path == "$" ? name : path + "." + name;
    }

    private static string BuildPrefix(string? prefix, string name)
    {
        return string.IsNullOrEmpty(prefix) ? name : prefix + "." + name;
    }

    public static string ToPascalCase(string value)
    {
        var parts = value.Split(new[] { '.', '-', '_', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var builder = new StringBuilder();
        foreach (var part in parts)
        {
            var sanitized = new string(part.Where(char.IsLetterOrDigit).ToArray());
            if (sanitized.Length == 0)
            {
                continue;
            }

            builder.Append(char.ToUpperInvariant(sanitized[0]));
            if (sanitized.Length > 1)
            {
                builder.Append(sanitized[1..]);
            }
        }

        return builder.Length == 0 ? "Item" : builder.ToString();
    }

    public enum NodeKind
    {
        Value,
        Object,
        ValueArray,
        ObjectArray
    }
}

public static class LocalizationCodeWriter
{
    public static string Write(LocalizationNode schema)
    {
        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated />");
        builder.AppendLine("namespace WearPartsControl.ApplicationServices.Localization.Generated;");
        builder.AppendLine();
        builder.AppendLine("public abstract class LocalizationSectionBase");
        builder.AppendLine("{");
        builder.AppendLine("    protected LocalizationSectionBase(global::System.Func<string, string> getter, string prefix)");
        builder.AppendLine("    {");
        builder.AppendLine("        Getter = getter;");
        builder.AppendLine("        Prefix = prefix;");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    protected global::System.Func<string, string> Getter { get; }");
        builder.AppendLine();
        builder.AppendLine("    protected string Prefix { get; }");
        builder.AppendLine();
        builder.AppendLine("    protected string Key(string localName) => string.IsNullOrEmpty(Prefix) ? localName : Prefix + \".\" + localName;");
        builder.AppendLine();
        builder.AppendLine("    protected string GetString(string localName) => Getter(Key(localName));");
        builder.AppendLine("}");
        builder.AppendLine();

        WriteObject(builder, schema, isRoot: true);
        return builder.ToString();
    }

    private static void WriteObject(StringBuilder builder, LocalizationNode node, bool isRoot)
    {
        var typeName = isRoot ? "LocalizationCatalog" : node.TypeName;
        builder.AppendLine($"public sealed class {typeName} : LocalizationSectionBase");
        builder.AppendLine("{");
        builder.AppendLine(isRoot
            ? $"    public {typeName}(global::System.Func<string, string> getter) : base(getter, string.Empty) {{ }}"
            : $"    public {typeName}(global::System.Func<string, string> getter, string prefix) : base(getter, prefix) {{ }}");
        builder.AppendLine();

        foreach (var child in node.Children)
        {
            switch (child.Kind)
            {
                case LocalizationNode.NodeKind.Value:
                    builder.AppendLine($"    public string {LocalizationNode.ToPascalCase(child.Name)} => GetString(\"{EscapeString(child.Name)}\");");
                    builder.AppendLine();
                    break;

                case LocalizationNode.NodeKind.Object:
                    builder.AppendLine($"    public {child.TypeName} {LocalizationNode.ToPascalCase(child.Name)} => new(Getter, Key(\"{EscapeString(child.Name)}\"));");
                    builder.AppendLine();
                    break;

                case LocalizationNode.NodeKind.ValueArray:
                    builder.AppendLine($"    public global::System.Collections.Generic.IReadOnlyList<string> {LocalizationNode.ToPascalCase(child.Name)} => new string[]");
                    builder.AppendLine("    {");
                    for (var i = 0; i < child.ArrayLength; i++)
                    {
                        builder.AppendLine($"        GetString(\"{EscapeString(child.Name)}.{i}\"),");
                    }
                    builder.AppendLine("    };");
                    builder.AppendLine();
                    break;

                case LocalizationNode.NodeKind.ObjectArray:
                    builder.AppendLine($"    public global::System.Collections.Generic.IReadOnlyList<{child.ItemTemplate!.TypeName}> {LocalizationNode.ToPascalCase(child.Name)} => new {child.ItemTemplate.TypeName}[]");
                    builder.AppendLine("    {");
                    for (var i = 0; i < child.ArrayLength; i++)
                    {
                        builder.AppendLine($"        new(Getter, Key(\"{EscapeString(child.Name)}.{i}\")),");
                    }
                    builder.AppendLine("    };");
                    builder.AppendLine();
                    break;
            }
        }

        builder.AppendLine("}");
        builder.AppendLine();

        foreach (var child in node.Children.Where(child => child.Kind == LocalizationNode.NodeKind.Object))
        {
            WriteObject(builder, child, isRoot: false);
        }

        foreach (var child in node.Children.Where(child => child.Kind == LocalizationNode.NodeKind.ObjectArray))
        {
            WriteObject(builder, child.ItemTemplate!, isRoot: false);
        }
    }

    private static string EscapeString(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}

}