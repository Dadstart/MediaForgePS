using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.Ffmpeg;
using Dadstart.Labs.MediaForge.Services.System;
using Microsoft.Extensions.DependencyInjection;

namespace Dadstart.Labs.MediaForge.Cmdlets;

/// <summary>
/// Converts all MKV files in a directory using module conversion services.
/// </summary>
[Cmdlet(VerbsData.Convert, "MkvDirectory")]
[OutputType(typeof(MkvDirectoryConversionResult))]
public class ConvertMkvDirectoryCommand : CmdletBase
{
    private IPathResolver? _pathResolver;
    private IMediaReaderService? _mediaReaderService;
    private IAudioTrackMappingService? _audioTrackMappingService;
    private IMediaConversionService? _mediaConversionService;
    private readonly List<MkvDirectoryConversionResult> _results = new();

    /// <summary>
    /// Directory containing MKV files to convert.
    /// </summary>
    [Parameter(
        Mandatory = true,
        Position = 0,
        HelpMessage = "Directory containing MKV files to convert")]
    [ValidateNotNullOrEmpty]
    public string InputDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Directory where converted files are written. Defaults to InputDirectory.
    /// </summary>
    [Parameter(
        Mandatory = false,
        Position = 1,
        HelpMessage = "Directory where converted files are written. Defaults to InputDirectory.")]
    public string? OutputDirectory { get; set; }

    /// <summary>
    /// Includes MKV files in child directories.
    /// </summary>
    [Parameter(HelpMessage = "Include MKV files in subdirectories.")]
    public SwitchParameter Recurse { get; set; }

    /// <summary>
    /// Default encoder to use: x264, x265, or nvenc.
    /// </summary>
    [Parameter(
        Mandatory = false,
        HelpMessage = "Default encoder to use: 'x264', 'x265', or 'nvenc'")]
    [ValidateSet("x264", "x265", "nvenc", IgnoreCase = true)]
    public string DefaultVideoEncoder { get; set; } = "nvenc";

    /// <summary>
    /// Additional x265 params passed to Ffmpeg with -x265-params.
    /// </summary>
    [Parameter(Mandatory = false, HelpMessage = "Additional x265 params (passed to ffmpeg via -x265-params).")]
    public string? X265Params { get; set; }

    private IPathResolver PathResolver => _pathResolver ??= ModuleServices.GetRequiredService<IPathResolver>();

    private IMediaReaderService MediaReaderService => _mediaReaderService ??= ModuleServices.GetRequiredService<IMediaReaderService>();

    private IAudioTrackMappingService AudioTrackMappingService => _audioTrackMappingService ??= ModuleServices.GetRequiredService<IAudioTrackMappingService>();

    private IMediaConversionService MediaConversionService => _mediaConversionService ??= ModuleServices.GetRequiredService<IMediaConversionService>();

    protected override void Process()
    {
        _results.Clear();

        if (!TryResolveDirectoryPath(InputDirectory, requireExists: true, out var resolvedInputDirectory))
        {
            WriteError(CreateErrorRecord(
                new DirectoryNotFoundException($"Input directory does not exist: {InputDirectory}"),
                "InputDirectoryNotFound",
                ErrorCategory.ObjectNotFound,
                InputDirectory));
            return;
        }

        var outputDirectory = string.IsNullOrWhiteSpace(OutputDirectory) ? resolvedInputDirectory : OutputDirectory!;
        if (!TryResolveDirectoryPath(outputDirectory, requireExists: false, out var resolvedOutputDirectory))
        {
            WriteError(CreateErrorRecord(
                new InvalidOperationException($"Failed to resolve output directory: {outputDirectory}"),
                ErrorIds.OutputPathResolutionFailed,
                ErrorCategory.InvalidArgument,
                outputDirectory));
            return;
        }

        Directory.CreateDirectory(resolvedOutputDirectory);

        var searchOption = Recurse.IsPresent ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var mkvFiles = Directory.EnumerateFiles(resolvedInputDirectory, "*.mkv", searchOption)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (mkvFiles.Length == 0)
        {
            WriteWarning($"No MKV files found in: {resolvedInputDirectory}");
            return;
        }

        var videoSettings = MediaConversionHelper.CreateDefaultVideoEncodingSettings(DefaultVideoEncoder);
        var additionalArguments = MediaConversionHelper.BuildX265Arguments(X265Params, videoSettings.Codec);

        foreach (var inputPath in mkvFiles)
        {
            var result = ConvertSingleFile(
                resolvedInputDirectory,
                resolvedOutputDirectory,
                inputPath,
                videoSettings,
                additionalArguments);

            _results.Add(result);
            WriteObject(result);
        }
    }

    private MkvDirectoryConversionResult ConvertSingleFile(
        string resolvedInputDirectory,
        string resolvedOutputDirectory,
        string inputPath,
        VideoEncodingSettings videoSettings,
        string[]? additionalArguments)
    {
        try
        {
            if (!TryGetMediaFile(MediaReaderService, inputPath, out var mediaFile))
                return new MkvDirectoryConversionResult(inputPath, inputPath, false, "Failed to read media metadata.");

            var audioMappings = AudioTrackMappingService.CreateDirectoryEncodeMappings(mediaFile);
            var outputPath = BuildOutputPath(resolvedInputDirectory, resolvedOutputDirectory, inputPath);

            var outputDirectory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(outputDirectory))
                Directory.CreateDirectory(outputDirectory);

            if (!PathResolver.TryResolveOutputPath(outputPath, out var resolvedOutputPath))
            {
                return new MkvDirectoryConversionResult(
                    inputPath,
                    outputPath,
                    false,
                    "Failed to resolve output path.");
            }

            MediaConversionService.ExecuteConversion(
                inputPath,
                resolvedOutputPath,
                videoSettings,
                audioMappings,
                additionalArguments);

            return new MkvDirectoryConversionResult(inputPath, resolvedOutputPath, true, "Success");
        }
        catch (FfmpegConversionException ex)
        {
            var statusMessage = MediaConversionHelper.BuildConversionFailureStatusMessage(ex);
            return new MkvDirectoryConversionResult(inputPath, inputPath, false, statusMessage);
        }
        catch (Exception ex)
        {
            return new MkvDirectoryConversionResult(inputPath, inputPath, false, ex.Message);
        }
    }

    private static string BuildOutputPath(string inputRoot, string outputRoot, string inputPath)
    {
        var relativePath = Path.GetRelativePath(inputRoot, inputPath);
        var outputRelativePath = Path.ChangeExtension(relativePath, ".mp4");
        return Path.Combine(outputRoot, outputRelativePath);
    }

    private bool TryResolveDirectoryPath(string path, bool requireExists, out string resolvedPath)
    {
        resolvedPath = string.Empty;

        if (Dadstart.Labs.MediaForge.Services.System.PathResolver.TryResolveProviderPath(this, path, out var fromProvider))
        {
            resolvedPath = fromProvider!;
            return !requireExists || Directory.Exists(resolvedPath);
        }

        if (Dadstart.Labs.MediaForge.Services.System.PathResolver.TryGetUnresolvedProviderPath(this, path, out var unresolved))
        {
            resolvedPath = unresolved!;
            return !requireExists || Directory.Exists(resolvedPath);
        }

        return false;
    }
}

/// <summary>
/// Result of converting a single MKV file from a directory batch.
/// </summary>
public record MkvDirectoryConversionResult(string InputPath, string OutputPath, bool Success, string Status);
