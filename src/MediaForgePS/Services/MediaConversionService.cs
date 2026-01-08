using System;
using System.Collections.Generic;
using System.Management.Automation;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services.Ffmpeg;
using Dadstart.Labs.MediaForge.Services.System;

namespace Dadstart.Labs.MediaForge.Services;

/// <summary>
/// Service for performing media file conversions with progress reporting.
/// </summary>
public class MediaConversionService : IMediaConversionService
{
    private readonly IFfmpegService _ffmpegService;
    private readonly IMediaReaderService _mediaReaderService;
    private readonly IPlatformService _platformService;

    /// <summary>
    /// Initializes a new instance of the MediaConversionService class.
    /// </summary>
    /// <param name="ffmpegService">Ffmpeg service for conversion.</param>
    /// <param name="mediaReaderService">Media reader service for getting file duration.</param>
    /// <param name="platformService">Platform service for argument building.</param>
    public MediaConversionService(
        IFfmpegService ffmpegService,
        IMediaReaderService mediaReaderService,
        IPlatformService platformService)
    {
        _ffmpegService = ffmpegService ?? throw new ArgumentNullException(nameof(ffmpegService));
        _mediaReaderService = mediaReaderService ?? throw new ArgumentNullException(nameof(mediaReaderService));
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

        // Add additional arguments if provided
        if (additionalArguments != null)
        {
            args.AddRange(additionalArguments);
        }

        return args;
    }

    /// <inheritdoc />
    public bool ExecuteConversion(
        string resolvedInputPath,
        string resolvedOutputPath,
        VideoEncodingSettings videoSettings,
        AudioTrackMapping[] audioMappings,
        Action<FfmpegProgress, long?, string> progressCallback,
        string[]? additionalArguments = null)
    {
        // Get input file duration for progress percentage calculation
        var inputFile = _mediaReaderService.GetMediaFileAsync(resolvedInputPath, CancellationToken.None)
            .ConfigureAwait(false).GetAwaiter().GetResult();
        long? totalDurationMs = inputFile?.Format?.Duration != null ? (long)(inputFile.Format.Duration * 1000) : null;

        if (videoSettings.IsSinglePass)
        {
            _ffmpegService.ConvertAsync(
                resolvedInputPath,
                resolvedOutputPath,
                BuildFfmpegArguments(videoSettings, audioMappings, null, additionalArguments),
                progress => progressCallback(progress, totalDurationMs, "Converting"),
                CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
        }
        else
        {
            // First pass
            _ffmpegService.ConvertAsync(
                resolvedInputPath,
                resolvedOutputPath,
                BuildFfmpegArguments(videoSettings, audioMappings, 1, additionalArguments),
                progress => progressCallback(progress, totalDurationMs, "Pass 1 of 2"),
                CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();

            // Second pass
            _ffmpegService.ConvertAsync(
                resolvedInputPath,
                resolvedOutputPath,
                BuildFfmpegArguments(videoSettings, audioMappings, 2, additionalArguments),
                progress => progressCallback(progress, totalDurationMs, "Pass 2 of 2"),
                CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        return true;
    }
}
