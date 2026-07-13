using System;
using System.IO;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Reflection;
using System.Threading;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Module;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dadstart.Labs.MediaForge.Cmdlets;

/// <summary>
/// Base class for MediaForge PowerShell cmdlets providing logging, path resolution, and progress helpers.
/// </summary>
public abstract class CmdletBase : PSCmdlet
{
    /// <summary>
    /// Shared error identifiers used by cmdlets in this module.
    /// </summary>
    protected static class ErrorIds
    {
        public const string FileNotFound = "FileNotFound";
        public const string OutputPathResolutionFailed = "OutputPathResolutionFailed";
        public const string MediaFileReadFailed = "MediaFileReadFailed";
        public const string ConversionFailed = "ConversionFailed";
        public const string SubtitleExportFailed = "SubtitleExportFailed";
        public const string SplitChaptersFailed = "SplitChaptersFailed";
        public const string InvalidChapterRanges = "InvalidChapterRanges";
        public const string OutputFileExists = "OutputFileExists";
        public const string FfmpegExecutionFailed = "FfmpegExecutionFailed";
        public const string FfmpegExecutionException = "FfmpegExecutionException";
    }

    /// <summary>
    /// Activity ID for the main operation in progress records (e.g. batch or top-level task).
    /// </summary>
    protected static int MainActivityId => ProgressActivityIds.Main;

    /// <summary>
    /// Activity ID for the current item in progress records (e.g. current file or stream).
    /// </summary>
    protected static int CurrentItemActivityId => ProgressActivityIds.CurrentItem;

    private IDebuggerService? _debugger;
    private ILogger? _logger;
    private IDisposable? _commandTitleScope;
    private string? _powerShellCommandName;

    /// <summary>
    /// Logger instance for the derived cmdlet type.
    /// </summary>
    protected ILogger Logger => _logger ??= ModuleServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger(GetType());

    protected IDebuggerService Debugger => _debugger ??= ModuleServices.GetRequiredService<IDebuggerService>();

    public string CmdletName => GetType().Name;
    protected string PowerShellCommandName => _powerShellCommandName ??= ResolvePowerShellCommandName();
    protected virtual bool ShouldSetCommandTerminalTitle => false;

    protected CmdletBase()
    {
        ModuleServices.EnsureInitialized();
        CmdletContext.Current = this;

    }

    /// <summary>
    /// Sets up the PowerShell command context for logging before processing begins.
    /// </summary>
    protected sealed override void BeginProcessing()
    {
        CmdletContext.Current = this;
        Debugger.BreakIfDebugging(Debugger.PowerShellBreakOnBeginProcessing);

        if (ShouldSetCommandTerminalTitle)
            _commandTitleScope = TrySetTerminalTitle(BuildTerminalTitle(PowerShellCommandName));

        try
        {
            Logger.LogDebug("Begin processing {CmdletName} command", CmdletName);
            Begin();
        }
        catch
        {
            _commandTitleScope?.Dispose();
            _commandTitleScope = null;
            throw;
        }
    }

    /// <summary>
    /// Processes each record in the pipeline.
    /// Handles common behavior and then calls child Process to do the actual record process
    /// </summary>
    protected sealed override void ProcessRecord()
    {
        Debugger.BreakIfDebugging(Debugger.PowerShellBreakOnProcessRecord);

        Logger.LogDebug("Processing {CmdletName} command", CmdletName);
        Process();
    }

    /// <summary>
    /// Cleans up the PowerShell command context after processing completes.
    /// </summary>
    protected sealed override void EndProcessing()
    {
        try
        {
            Debugger.BreakIfDebugging(Debugger.PowerShellBreakOnEndProcessing);

            Logger.LogDebug("End processing {CmdletName} command", CmdletName);
            End();
        }
        finally
        {
            TryAlertOnCompletion();
            _commandTitleScope?.Dispose();
            _commandTitleScope = null;
            CmdletContext.Current = null;
        }
    }

    /// <summary>
    /// Optionally plays a completion alert after the cmdlet finishes.
    /// Overridden by <see cref="ProgressCmdletBase"/> when <c>-Alert</c> is specified.
    /// </summary>
    protected virtual void TryAlertOnCompletion()
    {
    }

    protected IDisposable PushOperationTerminalTitle(string operationName)
    {
        if (!ShouldSetCommandTerminalTitle || string.IsNullOrWhiteSpace(operationName))
            return NoOpTerminalTitleScope.Instance;

        return TrySetTerminalTitle(BuildTerminalTitle(PowerShellCommandName, operationName));
    }

    /// <summary>
    /// Override this method to perform custom initialization logic when processing begins.
    /// This method is called by BeginProcessing after any necessary setup
    /// </summary>
    protected virtual void Begin()
    {
    }

    /// <summary>
    /// Override this method with processing logic.
    /// This method is called by ProcessRecord after any necessary setup
    /// </summary>
    protected virtual void Process()
    {
    }

    /// <summary>
    /// Override this method to perform custom cleanup logic when processing ends.
    /// This method is called by EndProcessing after any necessary setup
    /// </summary>
    protected virtual void End()
    {
    }

    /// <summary>
    /// Writes a message to the host (information stream) with optional foreground color.
    /// Use for user-facing status and milestone messages that should appear in the console.
    /// </summary>
    /// <param name="message">Message to display.</param>
    /// <param name="foregroundColor">Optional console color for the message.</param>
    protected void WriteHostMessage(string message, ConsoleColor? foregroundColor = null)
    {
        var hostMsg = new HostInformationMessage { Message = message };
        if (foregroundColor.HasValue)
            hostMsg.ForegroundColor = foregroundColor.Value;
        WriteInformation(new InformationRecord(hostMsg, "PSHOST"));
    }

    /// <summary>
    /// Creates a standard error record.
    /// </summary>
    /// <param name="exception">Underlying exception.</param>
    /// <param name="errorId">Stable error identifier.</param>
    /// <param name="errorCategory">PowerShell error category.</param>
    /// <param name="targetObject">Target object that caused the error.</param>
    protected static ErrorRecord CreateErrorRecord(
        Exception exception,
        string errorId,
        ErrorCategory errorCategory,
        object? targetObject)
    {
        return new ErrorRecord(
            exception,
            errorId,
            errorCategory,
            targetObject);
    }

    /// <summary>
    /// Writes a standardized error record.
    /// </summary>
    protected void WriteStandardError(
        Exception exception,
        string errorId,
        ErrorCategory errorCategory,
        object? targetObject)
    {
        WriteError(CreateErrorRecord(exception, errorId, errorCategory, targetObject));
    }

    /// <summary>
    /// Resolves an input path and writes a standardized not-found error when resolution fails.
    /// </summary>
    /// <param name="pathResolver">Path resolver service.</param>
    /// <param name="inputPath">Input path from cmdlet parameter.</param>
    /// <param name="resolvedInputPath">Resolved file system path.</param>
    /// <returns>True when resolved successfully; otherwise false.</returns>
    protected bool TryResolveInputPath(IPathResolver pathResolver, string inputPath, out string resolvedInputPath)
    {
        if (pathResolver.TryResolveInputPath(inputPath, out resolvedInputPath))
            return true;

        WriteError(CreateErrorRecord(
            new FileNotFoundException($"Media file not found: {inputPath}"),
            ErrorIds.FileNotFound,
            ErrorCategory.ObjectNotFound,
            inputPath));

        return false;
    }

    /// <summary>
    /// Resolves an output path and writes a standardized path error when resolution fails.
    /// </summary>
    /// <param name="pathResolver">Path resolver service.</param>
    /// <param name="outputPath">Output path from cmdlet parameter.</param>
    /// <param name="resolvedOutputPath">Resolved file system path.</param>
    /// <returns>True when resolved successfully; otherwise false.</returns>
    protected bool TryResolveOutputPath(IPathResolver pathResolver, string outputPath, out string resolvedOutputPath)
    {
        if (pathResolver.TryResolveOutputPath(outputPath, out resolvedOutputPath))
            return true;

        WriteError(CreateErrorRecord(
            new InvalidOperationException($"Failed to resolve output path: {outputPath}"),
            ErrorIds.OutputPathResolutionFailed,
            ErrorCategory.InvalidArgument,
            outputPath));

        return false;
    }

    /// <summary>
    /// Reads media metadata for a resolved path and writes standardized errors when reading fails.
    /// </summary>
    /// <param name="mediaReaderService">Media reader service.</param>
    /// <param name="resolvedPath">Resolved media file path.</param>
    /// <param name="mediaFile">Resolved media file metadata.</param>
    /// <returns>True when metadata is available; otherwise false.</returns>
    protected bool TryGetMediaFile(IMediaReaderService mediaReaderService, string resolvedPath, out MediaFile mediaFile)
    {
        try
        {
            var result = mediaReaderService.GetMediaFileAsync(resolvedPath, CancellationToken.None)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();

            if (result == null)
            {
                Logger.LogWarning("Media file information is null for: {ResolvedPath}", resolvedPath);
                WriteError(CreateErrorRecord(
                    new InvalidOperationException($"Failed to get media file information: {resolvedPath}"),
                    ErrorIds.MediaFileReadFailed,
                    ErrorCategory.ReadError,
                    resolvedPath));
                mediaFile = null!;
                return false;
            }

            mediaFile = result;
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to read media file information for: {ResolvedPath}", resolvedPath);
            WriteError(CreateErrorRecord(
                ex,
                ErrorIds.MediaFileReadFailed,
                ErrorCategory.ReadError,
                resolvedPath));
            mediaFile = null!;
            return false;
        }
    }

    protected static string BuildTerminalTitle(string commandName, string? operationName = null)
    {
        if (string.IsNullOrWhiteSpace(commandName))
            return "MF: Other";

        if (string.IsNullOrWhiteSpace(operationName))
            return $"MF: {commandName}";

        return $"MF: {commandName}: {operationName}";
    }

    private string ResolvePowerShellCommandName()
    {
        var cmdletAttribute = GetType().GetCustomAttribute<CmdletAttribute>(inherit: true);
        if (cmdletAttribute == null)
            return CmdletName;

        return $"{cmdletAttribute.VerbName}-{cmdletAttribute.NounName}";
    }

    private IDisposable TrySetTerminalTitle(string title)
    {
        var rawUi = TryGetRawUi();
        if (rawUi == null)
            return NoOpTerminalTitleScope.Instance;

        string previousTitle;
        try
        {
            previousTitle = rawUi.WindowTitle;
            rawUi.WindowTitle = title;
        }
        catch (Exception ex) when (ex is HostException or NotImplementedException or InvalidOperationException)
        {
            Logger.LogDebug(ex, "Terminal title updates are unavailable for {CmdletName}", CmdletName);
            return NoOpTerminalTitleScope.Instance;
        }

        return new TerminalTitleScope(rawUi, previousTitle);
    }

    private PSHostRawUserInterface? TryGetRawUi()
    {
        try
        {
            return Host?.UI?.RawUI;
        }
        catch (Exception ex) when (ex is HostException or NotImplementedException or InvalidOperationException)
        {
            Logger.LogDebug(ex, "Raw host UI is unavailable for {CmdletName}", CmdletName);
            return null;
        }
    }

    private sealed class TerminalTitleScope : IDisposable
    {
        private readonly PSHostRawUserInterface _rawUi;
        private readonly string _previousTitle;
        private bool _isDisposed;

        public TerminalTitleScope(PSHostRawUserInterface rawUi, string previousTitle)
        {
            _rawUi = rawUi;
            _previousTitle = previousTitle;
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            try
            {
                _rawUi.WindowTitle = _previousTitle;
            }
            catch (Exception ex) when (ex is HostException or NotImplementedException or InvalidOperationException)
            {
                // Best effort restore; ignore hosts that do not support title updates.
            }
        }
    }

    private sealed class NoOpTerminalTitleScope : IDisposable
    {
        public static readonly NoOpTerminalTitleScope Instance = new();

        public void Dispose()
        {
        }
    }
}
