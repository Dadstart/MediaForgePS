using System;
using System.Collections.Generic;

namespace Dadstart.Labs.MediaForge.Services.Ffmpeg;

/// <summary>
/// Validates caller-supplied FFmpeg argument tokens before they are appended to a built command line.
/// </summary>
/// <remarks>
/// Additional FFmpeg arguments are trusted-input-only: they are passed through to FFmpeg without a full
/// option allowlist. This guard blocks a few high-risk patterns that can open extra inputs
/// (extra <c>-i</c>) or force local file protocol URLs (<c>file:</c>).
/// </remarks>
public static class FfmpegAdditionalArgumentsGuard
{
    /// <summary>
    /// Ensures <paramref name="additionalArguments"/> do not contain disallowed high-risk tokens.
    /// </summary>
    /// <param name="additionalArguments">Optional additional FFmpeg argument tokens from a trusted caller.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when an argument is an extra <c>-i</c> input flag or a <c>file:</c> protocol URL.
    /// </exception>
    public static void EnsureSafeForTrustedInput(IEnumerable<string>? additionalArguments)
    {
        if (additionalArguments is null)
            return;

        var index = 0;
        foreach (var argument in additionalArguments)
        {
            ArgumentNullException.ThrowIfNull(argument);

            if (IsInputFlag(argument))
            {
                throw new ArgumentException(
                    "Additional FFmpeg arguments must not include '-i'. Input paths are supplied by the conversion API; additional arguments are trusted-input-only codec/filter options.",
                    nameof(additionalArguments));
            }

            if (IsFileProtocolUrl(argument))
            {
                throw new ArgumentException(
                    $"Additional FFmpeg arguments must not include file: protocol URLs (argument at index {index}: '{argument}'). Use resolved filesystem paths via the conversion API instead.",
                    nameof(additionalArguments));
            }

            index++;
        }
    }

    private static bool IsInputFlag(string argument) =>
        string.Equals(argument, "-i", StringComparison.Ordinal);

    private static bool IsFileProtocolUrl(string argument) =>
        argument.StartsWith("file:", StringComparison.OrdinalIgnoreCase);
}
