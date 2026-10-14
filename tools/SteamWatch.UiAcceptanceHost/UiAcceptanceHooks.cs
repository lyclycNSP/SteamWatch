using System.IO.Pipes;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.UI.Xaml;
using SteamWatch.Core.Notifications;

namespace SteamWatch.App;

// Compiled only with UiAcceptanceHost=true, with a test-only informational version.
internal static class UiAcceptanceHooks
{
    internal static void Start(MainWindow window)
    {
        var marker = Path.Combine(AppContext.BaseDirectory, "ui-acceptance.host");
        if (!File.Exists(marker) || !string.Equals(File.ReadAllText(marker).Trim(),
                Path.GetFullPath(App.Service.DataDirectory), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Acceptance host requires an explicit isolated data-directory marker.");
        var lifetime = new CancellationTokenSource();
        window.Closed += (_, _) => lifetime.Cancel();
        _ = Task.Run(() => ServeAsync(window, lifetime.Token));
    }

    private static async Task ServeAsync(MainWindow window, CancellationToken cancellationToken)
    {
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            Path.GetFullPath(App.Service.DataDirectory).ToUpperInvariant())));
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await using var pipe = new NamedPipeServerStream("SteamWatch-UI-Acceptance-" + key,
                    PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(cancellationToken);
                using var reader = new StreamReader(pipe, Encoding.UTF8, false, 1024, true);
                using var writer = new StreamWriter(pipe, new UTF8Encoding(false), 1024, true) { AutoFlush = true };
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                deadline.CancelAfter(TimeSpan.FromSeconds(10));
                var command = await reader.ReadLineAsync(deadline.Token);
                var result = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
                if (!window.DispatcherQueue.TryEnqueue(() =>
                    {
                        try { result.SetResult(window.AcceptanceCommand(command)); }
                        catch (Exception ex) { result.SetResult(new { error = ex.Message }); }
                    })) throw new InvalidOperationException("Acceptance dispatcher unavailable.");
                await writer.WriteLineAsync(JsonSerializer.Serialize(await result.Task.WaitAsync(deadline.Token)));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception ex) { App.Service.RecordDiagnostic("ui-acceptance-host", ex); }
    }
}

public sealed partial class MainWindow
{
    private MainPage? _acceptancePage;
    private EventHandler<NotificationMessage>? _acceptanceOldNotification;

    internal object AcceptanceCommand(string? command)
    {
        _acceptancePage ??= RootFrame.Content as MainPage
            ?? throw new InvalidOperationException("MainPage missing.");
        switch (command)
        {
            case "light": ((FrameworkElement)Content).RequestedTheme = ElementTheme.Light; break;
            case "dark": ((FrameworkElement)Content).RequestedTheme = ElementTheme.Dark; break;
            case "detach":
                _acceptanceOldNotification = _acceptancePage.AcceptanceNotification;
                RootFrame.Content = null;
                break;
            case "attach": RootFrame.Content = _acceptancePage; break;
            case "stale-notification":
                (_acceptanceOldNotification ?? throw new InvalidOperationException("No captured handler."))(
                    this, new NotificationMessage("STALE ACCEPTANCE NOTIFICATION", "Must not display after remount.", PlaySound: false));
                break;
            case "snapshot": break;
            case "activate": Activate(); break;
            case "notification-burst": _acceptancePage.AcceptanceNotificationBurst(); break;
            case "notification-update": _acceptancePage.AcceptanceNotificationUpdate(); break;
            case "blocking-dialog": _acceptancePage.AcceptanceBlockingDialog(); break;
            default: throw new InvalidOperationException("Unknown acceptance command.");
        }
        return _acceptancePage.AcceptanceSnapshot();
    }
}

public sealed partial class MainPage
{
    internal void AcceptanceNotificationBurst()
    {
        QueueNotification(new NotificationMessage("QA 早期提醒", "QA 70%", PlaySound: false), _lifetime.Generation);
        QueueNotification(new NotificationMessage("QA 后期提醒", "QA 95%", PlaySound: false), _lifetime.Generation);
        QueueNotification(new NotificationMessage("QA 最新超限", "QA 100% latest", NotificationSeverity.Critical, false), _lifetime.Generation);
    }

    internal void AcceptanceNotificationUpdate() => QueueNotification(
        new NotificationMessage("QA 最新强退", "QA latest countdown", NotificationSeverity.Critical, false), _lifetime.Generation);

    internal void AcceptanceBlockingDialog() => _ = ShowDialogAsync(new Microsoft.UI.Xaml.Controls.ContentDialog
    { XamlRoot = XamlRoot, Title = "QA 编辑占用", Content = "QA modal gate", CloseButtonText = "关闭" });

    internal EventHandler<NotificationMessage>? AcceptanceNotification => _notificationHandler;

    internal object AcceptanceSnapshot()
    {
        static object? Field(object target, string name) => target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(target);
        var monitor = Field(_appService, "_monitor");
        var task = monitor is null ? null : Field(monitor, "_task") as Task;
        var subscribers = Field(_appService, "UserNotificationRaised") as Delegate;
        return new
        {
            attached = _lifetime.IsAttached,
            generation = _lifetime.Generation,
            initialized = _initializationTask?.IsCompletedSuccessfully == true,
            initializationIdentity = _initializationTask is null ? 0 : RuntimeHelpers.GetHashCode(_initializationTask),
            runtimeLoaded = _hasLoadedRuntime,
            timerEnabled = _monitorTimer.IsEnabled,
            notificationSubscribers = subscribers?.GetInvocationList().Length ?? 0,
            monitorIdentity = task is null ? 0 : RuntimeHelpers.GetHashCode(task),
            monitorStatus = task?.Status.ToString(),
            theme = ActualTheme.ToString(),
            rasterizationScale = XamlRoot?.RasterizationScale,
            page = PageTitle.Text,
            windowHandle = App.WindowHandle.ToInt64(), isLoaded = IsLoaded,
            width = ActualWidth, height = ActualHeight
        };
    }
}
