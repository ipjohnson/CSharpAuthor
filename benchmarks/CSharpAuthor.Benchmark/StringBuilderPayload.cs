using System.Text;

namespace Bench;

/// <summary>
/// The §10 reference point: the same file, emitted by hand with a <see cref="StringBuilder"/>
/// and no library at all.
/// </summary>
/// <remarks>
/// It is written to be byte-identical to <see cref="TreePayload.Generate"/> against V1 - run the
/// harness with <c>--verify</c> to check. It is a floor, not a competitor: it hard-codes every
/// name, indent and using directive, so it cannot derive a using directive, cannot restyle, and
/// cannot vary. §1 of the handoff has the measured bookkeeping cost of doing this for real.
/// </remarks>
internal static class StringBuilderPayload
{
    private static readonly string[] Types =
    {
        "string", "string", "string", "string", "string",
        "int", "int", "int",
        "bool", "bool", "bool",
        "Guid", "Guid",
        "DateTime", "DateTime",
        "decimal", "decimal",
        "double", "double",
        "long", "long",
        "TimeSpan", "TimeSpan",
        "IReadOnlyList<string>", "IReadOnlyDictionary<string,int>",
    };

    private static readonly string[] Names =
    {
        "Id", "Name", "Category", "Description", "ScopeName",
        "Order", "Version", "RetryLimit",
        "IsEnabled", "IsTransient", "AllowsNull",
        "Key", "CorrelationId",
        "CreatedAt", "ModifiedAt",
        "Amount", "Discount",
        "Ratio", "Weight",
        "Ticks", "Sequence",
        "Duration", "Timeout",
        "Tags", "Counters",
    };

    private static readonly string[] Parameters =
    {
        "id", "name", "category", "description", "scopeName",
        "order", "version", "retryLimit",
        "isEnabled", "isTransient", "allowsNull",
        "key", "correlationId",
        "createdAt", "modifiedAt",
        "amount", "discount",
        "ratio", "weight",
        "ticks", "sequence",
        "duration", "timeout",
        "tags", "counters",
    };

    public static string Generate()
    {
        var builder = new StringBuilder();

        builder.Append("using System;\n");
        builder.Append("using System.Collections.Generic;\n");
        builder.Append("using System.Globalization;\n");
        builder.Append("using System.Text;\n");
        builder.Append('\n');
        builder.Append("namespace CSharpAuthor.Benchmark.Generated\n");
        builder.Append("{\n");
        builder.Append("    public class BenchmarkPayload\n");
        builder.Append("    {\n");
        builder.Append('\n');

        builder.Append("        public BenchmarkPayload(");

        for (var i = 0; i < Names.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            builder.Append(Types[i]);
            builder.Append(' ');
            builder.Append(Parameters[i]);
        }

        builder.Append(")\n");
        builder.Append("        {\n");

        for (var i = 0; i < Names.Length; i++)
        {
            builder.Append("            ");
            builder.Append(Names[i]);
            builder.Append(" = ");
            builder.Append(Parameters[i]);
            builder.Append(";\n");
        }

        builder.Append("        }\n");

        for (var i = 0; i < Names.Length; i++)
        {
            builder.Append('\n');
            builder.Append("        public ");
            builder.Append(Types[i]);
            builder.Append(' ');
            builder.Append(Names[i]);
            builder.Append(" { get; init; }\n");
        }

        builder.Append('\n');
        builder.Append("        public string Execute(int retryCount, bool verbose)\n");
        builder.Append("        {\n");
        builder.Append("            var builder = new StringBuilder();\n");
        builder.Append("            var timestamp = DateTime.UtcNow;\n");
        builder.Append("            var attempts = 0;\n");
        builder.Append("            var completed = false;\n");
        builder.Append("            var identifier = Key.ToString();\n");
        builder.Append("            var separator = \";\";\n");
        builder.Append("            builder.Append(\"Id=\");\n");
        builder.Append("            builder.Append(Id);\n");
        builder.Append("            builder.Append(separator);\n");
        builder.Append("            builder.Append(\"Name=\");\n");
        builder.Append("            builder.Append(Name);\n");
        builder.Append("            builder.Append(separator);\n");
        builder.Append("            if (IsEnabled && verbose)\n");
        builder.Append("            {\n");
        builder.Append("                builder.Append(\"enabled\");\n");
        builder.Append("            }\n");
        builder.Append("            else\n");
        builder.Append("            {\n");
        builder.Append("                builder.Append(\"disabled\");\n");
        builder.Append("            }\n");
        builder.Append("            foreach(var tag in Tags)\n");
        builder.Append("            {\n");
        builder.Append("                builder.Append(tag);\n");
        builder.Append("                builder.Append(separator);\n");
        builder.Append("            }\n");
        builder.Append("            while(attempts < retryCount)\n");
        builder.Append("            {\n");
        builder.Append("                attempts = (attempts + 1);\n");
        builder.Append("            }\n");
        builder.Append("            var total = (Order * Version);\n");
        builder.Append("            var ratioText = Ratio.ToString(CultureInfo.InvariantCulture);\n");
        builder.Append("            var amountText = Amount.ToString(CultureInfo.InvariantCulture);\n");
        builder.Append("            builder.Append(ratioText);\n");
        builder.Append("            builder.Append(amountText);\n");
        builder.Append("            try\n");
        builder.Append("            {\n");
        builder.Append("                builder.Append(Counters.Count);\n");
        builder.Append("            }\n");
        builder.Append("            catch (Exception exception)\n");
        builder.Append("            {\n");
        builder.Append("                builder.Append(exception.Message);\n");
        builder.Append("            }\n");
        builder.Append("            if (total > 100)\n");
        builder.Append("            {\n");
        builder.Append("                completed = true;\n");
        builder.Append("            }\n");
        builder.Append("            builder.Append(timestamp.ToString(\"O\"));\n");
        builder.Append("            builder.Append(identifier);\n");
        builder.Append("            builder.Append(completed);\n");
        builder.Append("            builder.Append((Description ?? \"none\"));\n");
        builder.Append("            return builder.ToString();\n");
        builder.Append("        }\n");
        builder.Append("    }\n");
        builder.Append("}\n");

        return builder.ToString();
    }
}
