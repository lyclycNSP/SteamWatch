using System.Text;

namespace SteamWatch.Infrastructure.Steam;

internal static class SteamAppInfoIconReader
{
    private const uint AppInfoMagicV29 = 0x07564429;
    private const int V29HeaderLength = 16;
    private const byte BinaryKeyValuesStringType = 1;
    private const string ClientIconKey = "clienticon";

    public static IReadOnlyDictionary<int, string> ReadClientIconHashes(string appInfoPath)
    {
        if (!File.Exists(appInfoPath))
        {
            return new Dictionary<int, string>();
        }

        var bytes = File.ReadAllBytes(appInfoPath);
        if (bytes.Length < V29HeaderLength || ReadUInt32(bytes, 0) != AppInfoMagicV29)
        {
            return new Dictionary<int, string>();
        }

        var stringTableOffset = checked((int)ReadUInt32(bytes, 8));
        if (stringTableOffset < V29HeaderLength || stringTableOffset >= bytes.Length - sizeof(uint))
        {
            return new Dictionary<int, string>();
        }

        var stringTable = ReadStringTable(bytes, stringTableOffset);
        if (!stringTable.TryGetValue(ClientIconKey, out var clientIconKeyIndex))
        {
            return new Dictionary<int, string>();
        }

        return ReadV29Records(bytes, stringTableOffset, clientIconKeyIndex);
    }

    private static IReadOnlyDictionary<int, string> ReadV29Records(
        byte[] bytes,
        int stringTableOffset,
        uint clientIconKeyIndex)
    {
        var hashes = new Dictionary<int, string>();
        var offset = V29HeaderLength;
        while (offset + (sizeof(uint) * 2) <= stringTableOffset)
        {
            var appId = checked((int)ReadUInt32(bytes, offset));
            if (appId == 0)
            {
                break;
            }

            var recordSize = checked((int)ReadUInt32(bytes, offset + sizeof(uint)));
            var recordStart = offset + (sizeof(uint) * 2);
            var recordEnd = recordStart + recordSize;
            if (recordSize <= 0 || recordEnd > stringTableOffset)
            {
                break;
            }

            var hash = FindStringValue(bytes, recordStart, recordEnd, clientIconKeyIndex);
            if (hash is not null && IsSha1Hash(hash))
            {
                hashes[appId] = hash;
            }

            offset = recordEnd;
        }

        return hashes;
    }

    private static Dictionary<string, uint> ReadStringTable(byte[] bytes, int stringTableOffset)
    {
        var count = ReadUInt32(bytes, stringTableOffset);
        if (count > 100_000)
        {
            return new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
        }

        var strings = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
        var offset = stringTableOffset + sizeof(uint);
        for (uint index = 0; index < count && offset < bytes.Length; index++)
        {
            var end = Array.IndexOf(bytes, (byte)0, offset);
            if (end < 0)
            {
                break;
            }

            var key = Encoding.UTF8.GetString(bytes, offset, end - offset);
            if (!strings.ContainsKey(key))
            {
                strings[key] = index;
            }

            offset = end + 1;
        }

        return strings;
    }

    private static string? FindStringValue(byte[] bytes, int start, int end, uint keyIndex)
    {
        for (var offset = start; offset + 5 < end; offset++)
        {
            if (bytes[offset] != BinaryKeyValuesStringType || ReadUInt32(bytes, offset + 1) != keyIndex)
            {
                continue;
            }

            var valueStart = offset + 5;
            var valueEnd = Array.IndexOf(bytes, (byte)0, valueStart);
            if (valueEnd < valueStart || valueEnd > end)
            {
                continue;
            }

            return Encoding.UTF8.GetString(bytes, valueStart, valueEnd - valueStart);
        }

        return null;
    }

    private static bool IsSha1Hash(string? value)
    {
        return value is { Length: 40 }
            && value.All(character =>
                character is >= '0' and <= '9'
                    or >= 'a' and <= 'f'
                    or >= 'A' and <= 'F');
    }

    private static uint ReadUInt32(byte[] bytes, int offset)
    {
        return BitConverter.ToUInt32(bytes, offset);
    }
}
