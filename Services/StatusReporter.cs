using System;
using System.Threading.Tasks;
using Avalonia.Controls.Notifications;
using Avalonia.Media;
using Avalonia.Threading;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;

namespace systembuilderGUI.Services;

/// <summary>
/// Shows status and error messages of the plugin in OneWare Studio:
/// a notification (toast), a line in the output panel and, for long running work,
/// a process entry in the status bar.
/// </summary>
public static class StatusReporter
{
    private const string Title = "SystemBuilder";

    public static void Info(string message) =>
        Report(NotificationType.Information, message, null);

    public static void Success(string message) =>
        Report(NotificationType.Success, message, Brushes.LimeGreen);

    public static void Warning(string message) =>
        Report(NotificationType.Warning, message, Brushes.Orange);

    public static void Error(string message, Exception? exception = null)
    {
        var shortText = exception is null ? message : $"{message} {exception.Message}";
        var outputText = exception is null ? message : $"{message}{Environment.NewLine}{exception}";
        Report(NotificationType.Error, shortText, Brushes.Red, outputText);
    }

    /// <summary>
    /// Shows a spinner with <paramref name="status"/> in the status bar until the returned scope is disposed.
    /// </summary>
    public static ProcessScope BeginProcess(string status) => new(status);

    /// <summary>
    /// Runs <paramref name="action"/> with a status bar entry and reports an error instead of throwing.
    /// </summary>
    /// <returns>true if the action finished without an exception.</returns>
    public static async Task<bool> RunAsync(string status, Func<Task> action, string? successMessage = null,
        string errorMessage = "Operation failed.")
    {
        using var process = BeginProcess(status);
        try
        {
            await action();
            if (successMessage is not null) Success(successMessage);
            return true;
        }
        catch (Exception ex)
        {
            process.FinishMessage = "Failed";
            Error(errorMessage, ex);
            return false;
        }
    }

    private static void Report(NotificationType type, string message, IBrush? brush, string? outputText = null)
    {
        OnUiThread(() =>
        {
            ContainerLocator.Current.Resolve<IWindowService>().ShowNotification(Title, message, type);

            var project = ContainerLocator.Current.Resolve<IProjectExplorerService>().ActiveProject;
            ContainerLocator.Current.Resolve<IOutputService>().WriteLine($"[{Title}] {outputText ?? message}", brush, project);
        });
    }

    private static void OnUiThread(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess()) action();
        else Dispatcher.UIThread.Post(action);
    }

    public sealed class ProcessScope : IDisposable
    {
        private ApplicationProcess? _process;
        private bool _disposed;

        internal ProcessScope(string status)
        {
            OnUiThread(() =>
                _process = ContainerLocator.Current.Resolve<IApplicationStateService>()
                    .AddState(status, AppState.Loading));
        }

        /// <summary>Message shown when the process entry is removed from the status bar.</summary>
        public string FinishMessage { get; set; } = "Done";

        public void Update(string status)
        {
            OnUiThread(() =>
            {
                if (_process is not null) _process.StatusMessage = status;
            });
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            OnUiThread(() =>
            {
                if (_process is not null)
                    ContainerLocator.Current.Resolve<IApplicationStateService>().RemoveState(_process, FinishMessage);
            });
        }
    }
}
