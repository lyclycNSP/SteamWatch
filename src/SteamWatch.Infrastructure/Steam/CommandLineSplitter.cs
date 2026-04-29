namespace SteamWatch.Infrastructure.Steam;

public static class CommandLineSplitter
{
    public static IReadOnlyList<string> Split(string commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return [];
        }

        var result = new List<string>();
        var current = new List<char>();
        var inQuotes = false;

        foreach (var character in commandLine)
        {
            if (character == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (char.IsWhiteSpace(character) && !inQuotes)
            {
                AddCurrent(result, current);
                continue;
            }

            current.Add(character);
        }

        AddCurrent(result, current);
        return result;
    }

    private static void AddCurrent(ICollection<string> result, ICollection<char> current)
    {
        if (current.Count == 0)
        {
            return;
        }

        result.Add(new string(current.ToArray()));
        current.Clear();
    }
}
