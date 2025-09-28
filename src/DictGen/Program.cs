using YamlDotNet.Serialization;

class Program
{
    static void Main(string[] args)
    {
        string yamlPath = args.Length > 0 ? args[0] : "Offsets.yaml";
        string csPath = args.Length > 1 ? args[1] : "Generated/Offsets.cs";

        if (!File.Exists(yamlPath))
        {
            Console.WriteLine($"YAML file not found: {yamlPath}");
            return;
        }

        string yaml = File.ReadAllText(yamlPath);
        var deserializer = new DeserializerBuilder().Build();
        object yamlObject = deserializer.Deserialize<object>(yaml);

        Directory.CreateDirectory(Path.GetDirectoryName(csPath)!);

        using var writer = new StreamWriter(csPath);
        writer.WriteLine("using System.Collections.Generic;");
        writer.WriteLine();
        writer.WriteLine("namespace Generated");
        writer.WriteLine("{");
        writer.WriteLine("    public static class Offsets");
        writer.WriteLine("    {");
        writer.WriteLine("        public static readonly Dictionary<string, object> Data = " +
                         ToCSharpLiteral(yamlObject, 2) + ";");
        writer.WriteLine("    }");
        writer.WriteLine("}");

        Console.WriteLine($"Offsets class generated at: {csPath}");
    }

    static string ToCSharpLiteral(object obj, int indentLevel)
    {
        string indent = new string(' ', indentLevel * 4);

        if (obj is Dictionary<object, object> dict)
        {
            var entries = new List<string>();
            foreach (var kvp in dict)
            {
                string key = kvp.Key.ToString()!;
                string value = ToCSharpLiteral(kvp.Value, indentLevel + 1);
                entries.Add($"{indent}{{ \"{key}\", {value} }}");
            }
            return $"new Dictionary<string, object>\n{indent}{{\n{string.Join(",\n", entries)}\n{indent}}}";
        }
        else if (obj is string s)
        {
            return $"\"{s}\"";
        }
        else if (obj is int or long or double or bool)
        {
            return obj.ToString()!.ToLowerInvariant();
        }
        else if (obj == null)
        {
            return "null";
        }
        else
        {
            return $"\"{obj}\"";
        }
    }
}
