using System.Text;

namespace SteamWatch.Infrastructure.Steam;

internal sealed class ValveKeyValues
{
    public Dictionary<string, ValveKeyValues> Children { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> Values { get; } = new(StringComparer.OrdinalIgnoreCase);
    public string? Get(string key) => Values.GetValueOrDefault(key);
    public ValveKeyValues? Find(string key) => Children.GetValueOrDefault(key)
        ?? Children.Values.Select(child => child.Find(key)).FirstOrDefault(child => child is not null);

    public static ValveKeyValues Parse(string content)
    {
        var index = 0;
        return ReadObject(0, false);

        ValveKeyValues ReadObject(int depth, bool nested)
        {
            if (depth > 64) throw new InvalidDataException("Steam 配置嵌套过深。");
            var node = new ValveKeyValues();
            while (Token() is { } key)
            {
                if (key == "}")
                {
                    if (!nested) throw new InvalidDataException("Steam 配置有多余右括号。");
                    return node;
                }
                var value = Token() ?? throw new InvalidDataException("Steam 配置缺少值。");
                if (value == "{") node.Children[key] = ReadObject(depth + 1, true);
                else if (value == "}") throw new InvalidDataException("Steam 配置缺少值。");
                else node.Values[key] = value;
            }
            if (nested) throw new InvalidDataException("Steam 配置写入尚未完成，请稍后刷新。");
            return node;
        }

        string? Token()
        {
            while (index < content.Length)
            {
                if (char.IsWhiteSpace(content[index])) { index++; continue; }
                if (content[index] == '/' && index + 1 < content.Length && content[index + 1] == '/')
                {
                    while (index < content.Length && content[index] != '\n') index++;
                    continue;
                }
                break;
            }
            if (index == content.Length) return null;
            var character = content[index++];
            if (character is '{' or '}') return character.ToString();
            if (character != '"')
            {
                var start = index - 1;
                while (index < content.Length && !char.IsWhiteSpace(content[index]) && content[index] is not '{' and not '}') index++;
                return content[start..index];
            }
            var builder = new StringBuilder();
            while (index < content.Length)
            {
                character = content[index++];
                if (character == '"') return builder.ToString();
                if (character == '\\' && index < content.Length && content[index] is '\\' or '"') character = content[index++];
                builder.Append(character);
            }
            throw new InvalidDataException("Steam 配置包含未完成的字符串。");
        }
    }
}
