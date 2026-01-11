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

        // Add additional arguments if provided
        if (additionalArguments != null)
        {
            args.AddRange(additionalArguments);
        }

        return args;
    }

    /// <inheritdoc />
    public void ExecuteConversion(
        string resolvedInputPath,
        string resolvedOutputPath,
        VideoEncodingSettings videoSettings,
        AudioTrackMapping[] audioMappings,
        string[]? additionalArguments = null)
    {
        if (videoSettings.IsSinglePass)
        {
            _ffmpegService.ConvertAsync(
                resolvedInputPath,
                resolvedOutputPath,
                BuildFfmpegArguments(videoSettings, audioMappings, null, additionalArguments),
                CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
        }
        else
        {
            // First pass
            _ffmpegService.ConvertAsync(
                resolvedInputPath,
                resolvedOutputPath,
                BuildFfmpegArguments(videoSettings, audioMappings, 1, additionalArguments),
                CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();

            // Second pass
            _ffmpegService.ConvertAsync(
                resolvedInputPath,
                resolvedOutputPath,
                BuildFfmpegArguments(videoSettings, audioMappings, 2, additionalArguments),
                CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
        }
    }
}
