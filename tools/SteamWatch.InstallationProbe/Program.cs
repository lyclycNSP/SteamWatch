using System.Diagnostics;
using System.Text.Json;
using SteamWatch.Infrastructure.Runtime;

// Controlled process fixture for DELIVERY-004, never a production installer helper.
internal static class Program
{
    private sealed record Target(int ProcessId, long StartedUtcTicks, string Executable);

    private static async Task<int> Main(string[] args)
    {
        try
        {
            if (args.Length is < 3 or > 7) throw new ArgumentException("Use serve|coordinate|transaction|bundle <artifact-root> <sandbox> [mode].");
            var root = Path.GetFullPath(args[1]);
            var sandbox = Path.GetFullPath(args[2]);
            if (!sandbox.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Sandbox must be below the explicitly supplied artifact root.");
            if (args[0] == "bundle" && args.Length is 5 or 6)
                return await BundleFixture.RunAsync(root, sandbox, args[3], args[4], args.ElementAtOrDefault(5));
            if (args.Length > 4) throw new ArgumentException("Unexpected fixture arguments.");
            if (args[0] == "transaction" && args.Length == 4)
                return TransactionFixture.Run(sandbox, args[3]);
            var descriptor = Path.Combine(sandbox, "target.json");
            var data = Path.Combine(sandbox, "data");
            if (args[0] == "coordinate")
            {
                if (!File.Exists(descriptor)) return 0;
                var target = JsonSerializer.Deserialize<Target>(await File.ReadAllTextAsync(descriptor))
                    ?? throw new InvalidDataException("Missing target identity.");
                using var process = Process.GetProcessById(target.ProcessId);
                // No arbitrary process selection: only this probe executable can be coordinated.
                if (!string.Equals(target.Executable, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(process.MainModule?.FileName, target.Executable, StringComparison.OrdinalIgnoreCase) ||
                    process.StartTime.ToUniversalTime().Ticks != target.StartedUtcTicks)
                    throw new InvalidOperationException("Target process identity changed.");
                var result = await UpgradeExitClient.RequestAsync(data, TimeSpan.FromSeconds(2));
                if (result != UpgradeExitResult.Prepared) return result == UpgradeExitResult.Declined ? 10 : 11;
                // A successful pipe reply is insufficient: wait for actual process termination.
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await process.WaitForExitAsync(deadline.Token);
                return 0;
            }
            if (args[0] != "serve" || args.Length != 4) throw new ArgumentException("Invalid fixture command.");
            var mode = args[3];
            if (mode is not ("accept" or "decline" or "fail" or "timeout" or "prepared-stays"))
                throw new ArgumentException("Unknown fixture mode.");
            Directory.CreateDirectory(data);
            var committed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var server = new UpgradeExitServer(data, async token =>
            {
                if (mode == "timeout") await Task.Delay(TimeSpan.FromSeconds(8), token);
                if (mode == "fail") throw new IOException("Controlled save failure.");
                return mode != "decline";
            }, () => committed.TrySetResult(), () => { });
            using var self = Process.GetCurrentProcess();
            await File.WriteAllTextAsync(descriptor + ".new", JsonSerializer.Serialize(new Target(self.Id,
                self.StartTime.ToUniversalTime().Ticks, Environment.ProcessPath!)));
            File.Move(descriptor + ".new", descriptor);
            if (mode == "accept")
            {
                await committed.Task.WaitAsync(TimeSpan.FromSeconds(15));
                await Task.Delay(600); // Demonstrate response-before-process-exit ordering.
            }
            else await Task.Delay(TimeSpan.FromSeconds(8)); // Always exits naturally; never killed by the test.
            await File.WriteAllTextAsync(Path.Combine(sandbox, "fixture-exited.txt"), mode);
            return 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or
            InvalidOperationException or System.ComponentModel.Win32Exception or OperationCanceledException or TimeoutException or JsonException or System.Security.SecurityException or System.Runtime.InteropServices.COMException)
        {
            Console.Error.WriteLine(ex.Message);
            return 12;
        }
    }
}
