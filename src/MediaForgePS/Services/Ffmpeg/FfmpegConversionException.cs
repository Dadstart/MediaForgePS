using System;
using System.Runtime.Serialization;
using System.Text;

namespace Dadstart.Labs.MediaForge.Services.Ffmpeg;

/// <summary>
/// Exception thrown when FFmpeg conversion fails.
/// </summary>
[Serializable]
public class FfmpegConversionException : Exception
{
    /// <summary>
    /// Initializes a new instance of the FfmpegConversionException class.
    /// </summary>
    public FfmpegConversionException()
    {
        InputPath = string.Empty;
        OutputPath = string.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the FfmpegConversionException class with a specified error message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public FfmpegConversionException(string message)
        : base(message)
    {
        InputPath = string.Empty;
        OutputPath = string.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the FfmpegConversionException class with a specified error message and inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public FfmpegConversionException(string message, Exception innerException)
        : base(message, innerException)
    {
        InputPath = string.Empty;
        OutputPath = string.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the FfmpegConversionException class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="inputPath">Path to the input file.</param>
    /// <param name="outputPath">Path to the output file.</param>
    /// <param name="exitCode">The exit code from FFmpeg.</param>
    /// <param name="errorOutput">The error output from FFmpeg.</param>
    public FfmpegConversionException(string message, string inputPath, string outputPath, int? exitCode, string? errorOutput)
        : base(message)
    {
        InputPath = inputPath;
        OutputPath = outputPath;
        ExitCode = exitCode;
        ErrorOutput = errorOutput;
    }

    /// <summary>
    /// Initializes a new instance of the FfmpegConversionException class with an inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="inputPath">Path to the input file.</param>
    /// <param name="outputPath">Path to the output file.</param>
    /// <param name="exitCode">The exit code from FFmpeg.</param>
    /// <param name="errorOutput">The error output from FFmpeg.</param>
    /// <param name="innerException">The inner exception.</param>
    public FfmpegConversionException(string message, string inputPath, string outputPath, int? exitCode, string? errorOutput, Exception innerException)
        : base(message, innerException)
    {
        InputPath = inputPath;
        OutputPath = outputPath;
        ExitCode = exitCode;
        ErrorOutput = errorOutput;
    }

    /// <summary>
    /// Path to the input file that was being converted.
    /// </summary>
    public string InputPath { get; }

    /// <summary>
    /// Path to the output file that was being created.
    /// </summary>
    public string OutputPath { get; }

    /// <summary>
    /// The exit code from FFmpeg, if available.
    /// </summary>
    public int? ExitCode { get; }

    /// <summary>
    /// The error output from FFmpeg, if available.
    /// </summary>
    public string? ErrorOutput { get; }

    /// <inheritdoc />
    public override string ToString()
    {
        var baseString = base.ToString();
        if (string.IsNullOrEmpty(InputPath) && string.IsNullOrEmpty(OutputPath) && ExitCode == null && string.IsNullOrEmpty(ErrorOutput))
            return baseString;

        var details = new StringBuilder();
        details.AppendLine(baseString);
        details.AppendLine("Additional Information:");
        if (!string.IsNullOrEmpty(InputPath))
            details.AppendLine($"  InputPath: {InputPath}");
        if (!string.IsNullOrEmpty(OutputPath))
            details.AppendLine($"  OutputPath: {OutputPath}");
        if (ExitCode.HasValue)
            details.AppendLine($"  ExitCode: {ExitCode.Value}");
        if (!string.IsNullOrEmpty(ErrorOutput))
            details.AppendLine($"  ErrorOutput: {ErrorOutput}");

        return details.ToString();
    }
}
