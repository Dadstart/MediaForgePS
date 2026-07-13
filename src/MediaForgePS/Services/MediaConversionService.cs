using System;
using System.Collections.Generic;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services.Ffmpeg;
using Dadstart.Labs.MediaForge.Services.System;

namespace Dadstart.Labs.MediaForge.Services;

/// <summary>
/// Service for performing media file conversions.
/// </summary>
public class MediaConversionService : IMediaConversionService
{
    private readonly IFfmpegService _ffmpegService;
    private readonly IPlatformService _platformService;

    /// <summary>
    /// Initializes a new instance of the MediaConversionService class.
    /// </summary>
    /// <param name="ffmpegService">Ffmpeg service for conversion.</param>
    /// <param name="platformService">Platform service for argument building.</param>
    public MediaConversionService(
        IFfmpegService ffmpegService,
        IPlatformService platformService)
    {
        _ffmpegService = ffmpegService ?? throw new ArgumentNullException(nameof(ffmpegService));
        _platformService = platformService ?? throw new ArgumentNullException(nameof(platformService));
    }

    /// <inheritdoc />
    public IEnumerable<string> BuildFfmpegArguments(
        VideoEncodingSettings videoSettings,
        AudioTrackMapping[] audioMappings,
        int? pass = null,
        string[]? additionalArguments = null)
    {
        var args = new List<string>();

        // Add video encoding arguments
        args.AddRange(videoSettings.ToFfmpegArgs(_platformService, pass));

        // Add audio track mapping arguments
        foreach (var audioMapping in audioMappings)
        {
            args.AddRange(audioMapping.ToFfmpegArgs(_platformService));
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
        IProgress<FfmpegProgress>? progress = null)
    {
        if (videoSettings.IsSinglePass)
        {
            _ffmpegService.ConvertAsync(
                resolvedInputPath,
                resolvedOutputPath,
                BuildFfmpegArguments(videoSettings, audioMappings, null, additionalArguments),
                progress,
                CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
        }
        else
        {
            // First pass maps to 0-50%; second pass maps to 50-100%.
            var firstPassProgress = CreatePassProgress(progress, passOffsetPercent: 0, passWeightPercent: 50);
            _ffmpegService.ConvertAsync(
                resolvedInputPath,
                resolvedOutputPath,
                BuildFfmpegArguments(videoSettings, audioMappings, 1, additionalArguments),
                firstPassProgress,
                CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();

            var secondPassProgress = CreatePassProgress(progress, passOffsetPercent: 50, passWeightPercent: 50);
            _ffmpegService.ConvertAsync(
                resolvedInputPath,
                resolvedOutputPath,
                BuildFfmpegArguments(videoSettings, audioMappings, 2, additionalArguments),
                secondPassProgress,
                CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
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
