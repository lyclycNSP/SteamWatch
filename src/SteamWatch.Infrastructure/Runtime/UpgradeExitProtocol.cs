using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;

namespace SteamWatch.Infrastructure.Runtime;

public enum UpgradeExitResult : byte { Declined = 0, Prepared = 1, Failed = 2 }

public static class UpgradeExitClient
{
    internal static string PipeName(string dataDirectory) => "SteamWatch-Upgrade-" +
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(dataDirectory).ToUpperInvariant())));

    public static async Task<UpgradeExitResult> RequestAsync(string dataDirectory, TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        await using var pipe = new NamedPipeClientStream(".", PipeName(dataDirectory), PipeDirection.InOut,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await pipe.ConnectAsync(3000, deadline.Token).ConfigureAwait(false);
        await pipe.WriteAsync(new byte[] { 1 }, deadline.Token).ConfigureAwait(false);
        await pipe.FlushAsync(deadline.Token).ConfigureAwait(false);
        var response = new byte[1];
        await pipe.ReadExactlyAsync(response, deadline.Token).ConfigureAwait(false);
        return Enum.IsDefined((UpgradeExitResult)response[0]) ? (UpgradeExitResult)response[0]
            : throw new InvalidDataException("升级退出响应无效。");
    }
}

/// <summary>Current-user IPC only requests normal shutdown; the UI owns authentication and persistence.</summary>
public sealed class UpgradeExitServer : IDisposable
{
    private readonly CancellationTokenSource _stopping = new();
    private readonly Task _loop;
    private int _disposed;

    public UpgradeExitServer(string dataDirectory, Func<CancellationToken, Task<bool>> prepare,
        Action commit, Action abort, Action<Exception>? reportError = null)
    {
        var token = _stopping.Token;
        _loop = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                var prepared = false;
                try
                {
                    await using var pipe = new NamedPipeServerStream(UpgradeExitClient.PipeName(dataDirectory),
                        PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                    await pipe.WaitForConnectionAsync(token).ConfigureAwait(false);
                    using var request = CancellationTokenSource.CreateLinkedTokenSource(token);
                    request.CancelAfter(TimeSpan.FromSeconds(90));
                    var command = new byte[1];
                    await pipe.ReadExactlyAsync(command, request.Token).ConfigureAwait(false);
                    if (command[0] != 1) continue;
                    UpgradeExitResult result;
                    try
                    {
                        prepared = await prepare(request.Token).ConfigureAwait(false);
                        request.Token.ThrowIfCancellationRequested();
                        result = prepared ? UpgradeExitResult.Prepared : UpgradeExitResult.Declined;
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        reportError?.Invoke(ex);
                        result = UpgradeExitResult.Failed;
                    }
                    await pipe.WriteAsync(new byte[] { (byte)result }, request.Token).ConfigureAwait(false);
                    await pipe.FlushAsync(request.Token).ConfigureAwait(false);
                    if (prepared)
                    {
                        commit();
                        prepared = false;
                    }
                }
                catch (OperationCanceledException) { if (token.IsCancellationRequested) break; }
                catch (IOException ex) { reportError?.Invoke(ex); }
                catch (UnauthorizedAccessException ex) { reportError?.Invoke(ex); break; }
                catch (Exception ex) { reportError?.Invoke(ex); } // Keep the IPC boundary alive after a failed UI callback.
                finally
                {
                    if (prepared)
                    {
                        // Recovery belongs to the request; its failure must not stop the listener.
                        try { abort(); }
                        catch (Exception ex) { reportError?.Invoke(ex); }
                    }
                }
            }
        }, token);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _stopping.Cancel();
        _ = _loop.ContinueWith(_ => _stopping.Dispose(), TaskScheduler.Default);
    }
}
