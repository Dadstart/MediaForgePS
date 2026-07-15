using System;

namespace Dadstart.Labs.MediaForge.Models;

/// <summary>
/// Result of converting a single media file.
/// </summary>
/// <param name="InputPath">Original source file path.</param>
/// <param name="OutputPath">Path to the converted output file when known.</param>
/// <param name="Status">
/// Human-readable status or error message. Equals <see cref="CompletedStatus"/> when conversion succeeded.
/// </param>
/// <param name="InputSizeBytes">Size of the input file in bytes.</param>
/// <param name="OutputSizeBytes">Size of the output file in bytes when conversion succeeded.</param>
/// <param name="SizeReductionPercent">
/// Percent of input size saved by conversion (positive means smaller output). Null when conversion failed or input size is unavailable.
/// </param>
/// <param name="ProcessingTime">Wall-clock time spent converting this file.</param>
public sealed record MediaConversionResult(
    string InputPath,
    string OutputPath,
    string Status,
    long InputSizeBytes,
    long OutputSizeBytes,
    double? SizeReductionPercent,
    TimeSpan ProcessingTime)
{
    /// <summary>
    /// Status value used when conversion completed successfully.
    /// </summary>
    public const string CompletedStatus = "Success";

    /// <summary>
    /// Status value used when conversion was skipped because -WhatIf was specified (or ShouldProcess declined).
    /// </summary>
    public const string WhatIfStatus = "WhatIf";

    /// <summary>
    /// Alias for <see cref="InputPath"/> for callers that used the legacy <c>ConversionResult.FilePath</c> name.
    /// </summary>
    public string FilePath => InputPath;
}
