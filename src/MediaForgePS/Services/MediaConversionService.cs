using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services.Ffmpeg;

namespace Dadstart.Labs.MediaForge.Services;

/// <summary>
/// Service for performing media file conversions.
/// </summary>
public class MediaConversionService : IMediaConversionService
{
    private readonly IFfmpegService _ffmpegService;

    /// <summary>
    /// Initializes a new instance of the MediaConversionService class.
    /// </summary>
    /// <param name="ffmpegService">Ffmpeg service for conversion.</param>
    public MediaConversionService(IFfmpegService ffmpegService)
    {
        _ffmpegService = ffmpegService ?? throw new ArgumentNullException(nameof(ffmpegService));
    }

    /// <inheritdoc />
    public IEnumerable<string> BuildFfmpegArguments(
        VideoEncodingSettings videoSettings,
        AudioTrackMapping[] audioMappings,
        int? pass = null,
        string[]? additionalArguments = null)
    {
        return BuildFfmpegArguments(videoSettings, audioMappings, pass, passLogFile: null, additionalArguments);
    }

    /// <summary>
    /// Builds FFmpeg arguments, optionally including two-pass logfile routing.
    /// </summary>
    public IEnumerable<string> BuildFfmpegArguments(
        VideoEncodingSettings videoSettings,
        AudioTrackMapping[] audioMappings,
        int? pass,
        string? passLogFile,
        string[]? additionalArguments = null)
    {
        var args = new List<string>();

        if (videoSettings is VariableRateVideoEncodingSettings variableRate && pass is int passNumber)
            args.AddRange(variableRate.ToFfmpegArgs(passNumber, passLogFile));
        else
            args.AddRange(videoSettings.ToFfmpegArgs(pass));

        // Pass 1 for VBR is video analysis only; omit audio mappings.
        if (pass != 1)
        {
            foreach (var audioMapping in audioMappings)
                args.AddRange(audioMapping.ToFfmpegArgs());
        }

        // Enable experimental TrueHD-in-MP4 muxing when copying TrueHD/Atmos tracks.
        if (!ContainsStrictExperimental(additionalArguments))
        {
            args.Add("-strict");
            args.Add("-2");
        }

        // Add additional arguments if provided
        if (additionalArguments != null)
            args.AddRange(additionalArguments);

        return args;
    }

    private static bool ContainsStrictExperimental(string[]? additionalArguments)
    {
        if (additionalArguments is null || additionalArguments.Length < 2)
            return false;

        for (var i = 0; i < additionalArguments.Length - 1; i++)
        {
            if (!string.Equals(additionalArguments[i], "-strict", StringComparison.OrdinalIgnoreCase))
                continue;

            var value = additionalArguments[i + 1];
            if (string.Equals(value, "-2", StringComparison.Ordinal) ||
                string.Equals(value, "experimental", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <inheritdoc />
    public void ExecuteConversion(
        string resolvedInputPath,
        string resolvedOutputPath,
        VideoEncodingSettings videoSettings,
        AudioTrackMapping[] audioMappings,
        string[]? additionalArguments = null,
        IProgress<FfmpegProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (videoSettings.IsSinglePass)
        {
            _ffmpegService.ConvertAsync(
                resolvedInputPath,
                resolvedOutputPath,
                BuildFfmpegArguments(videoSettings, audioMappings, null, additionalArguments),
                progress,
                cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
            return;
        }

        var workDirectory = AtomicFileHelper.CreateTempDirectory();
        try
        {
            var passLogFile = Path.Combine(workDirectory, "passlog");

            // First pass maps to 0-50%; second pass maps to 50-100%.
            var firstPassProgress = CreatePassProgress(progress, passOffsetPercent: 0, passWeightPercent: 50);
            var pass1Args = new List<string>(BuildFfmpegArguments(videoSettings, audioMappings, 1, passLogFile, additionalArguments))
            {
                "-f",
                "null"
            };

            _ffmpegService.ConvertAsync(
                resolvedInputPath,
                AtomicFileHelper.PlatformNullDevice,
                pass1Args,
                firstPassProgress,
                cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

            cancellationToken.ThrowIfCancellationRequested();

            var secondPassProgress = CreatePassProgress(progress, passOffsetPercent: 50, passWeightPercent: 50);
            _ffmpegService.ConvertAsync(
                resolvedInputPath,
                resolvedOutputPath,
                BuildFfmpegArguments(videoSettings, audioMappings, 2, passLogFile, additionalArguments),
                secondPassProgress,
                cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        }
        finally
        {
            AtomicFileHelper.TryDeleteDirectory(workDirectory);
        }
    }

    private static IProgress<FfmpegProgress>? CreatePassProgress(
        IProgress<FfmpegProgress>? progress,
        int passOffsetPercent,
        int passWeightPercent)
    {
        if (progress is null)
            return null;

        return new SynchronousProgress<FfmpegProgress>(update =>
        {
            var mappedPercent = passOffsetPercent + (update.PercentComplete * passWeightPercent / 100);
            progress.Report(update with { PercentComplete = mappedPercent });
        });
    }
}
