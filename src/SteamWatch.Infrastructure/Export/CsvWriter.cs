using System.Globalization;
using System.Text;

namespace SteamWatch.Infrastructure.Export;

public sealed class CsvWriter
{
    public string Write(IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<object?>> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(",", headers.Select(Escape)));

        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(",", row.Select(value => Escape(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty))));
        }

        return builder.ToString();
    }

    private static string Escape(string value)
    {
        if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\n') && !value.Contains('\r'))
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
