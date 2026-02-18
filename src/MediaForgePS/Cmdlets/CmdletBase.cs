using System;
using System.IO;
using System.Management.Automation;
using System.Threading;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Module;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dadstart.Labs.MediaForge.Cmdlets;

/// <summary>
/// Base class for MediaForge PowerShell cmdlets that supports async and
/// provides common functionality for logging and other.
/// </summary>
public abstract class CmdletBase : PSCmdlet
{
    /// <summary>
    /// Activity ID for the main operation in progress records (e.g. batch or top-level task).
    /// </summary>
    protected const int MainActivityId = 0;

    /// <summary>
    /// Activity ID for the current item in progress records (e.g. current file or stream).
    /// </summary>
    protected const int CurrentItemActivityId = 1;

    private IDebuggerService? _debugger;
    private ILogger? _logger;

    /// <summary>
    /// Logger instance for the derived cmdlet type.
    /// </summary>
    protected ILogger Logger => _logger ??= ModuleServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger(GetType());

    protected IDebuggerService Debugger => _debugger ??= ModuleServices.GetRequiredService<IDebuggerService>();

    public string CmdletName => GetType().Name;

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

        Logger.LogDebug("Begin processing {CmdletName} command", CmdletName);
        Begin();
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
        Debugger.BreakIfDebugging(Debugger.PowerShellBreakOnEndProcessing);

        Logger.LogDebug("End processing {CmdletName} command", CmdletName);
        End();

        CmdletContext.Current = null;
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
            "FileNotFound",
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
            "OutputPathResolutionFailed",
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
                    "MediaFileReadFailed",
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
                "MediaFileReadFailed",
                ErrorCategory.ReadError,
                resolvedPath));
            mediaFile = null!;
            return false;
        }
    }
}
