using System.Text.Json;

namespace SteamWatch.Infrastructure.Storage;

public sealed class DiagnosticLog(string dataDirectory)
{
    private readonly object _gate = new();
    public void Record(string category, Exception exception)
    {
        try
        {
            lock (_gate)
            {
                var path = Path.Combine(dataDirectory, "diagnostics.log");
                if (File.Exists(path) && new FileInfo(path).Length > 1_048_576) File.Move(path, path + ".previous", overwrite: true);
                File.AppendAllText(path, JsonSerializer.Serialize(new { at = DateTimeOffset.UtcNow,
                    category, error = exception.GetType().Name }) + Environment.NewLine);
            }
        }
        catch (IOException) { /* A failed diagnostic must not mask the original operation. */ }
        catch (UnauthorizedAccessException) { }
    }
}
