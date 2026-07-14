using System;

namespace Dadstart.Labs.MediaForge.Services.TvDb;

/// <summary>
/// Error while calling TheTVDB API.
/// </summary>
public sealed class TvDbApiException : Exception
{
    public TvDbApiException(string errorId, string message)
        : base(message)
    {
        ErrorId = errorId;
    }

    public TvDbApiException(string errorId, string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorId = errorId;
    }

    /// <summary>
    /// Stable error identifier used when writing PowerShell <c>ErrorRecord</c>s.
    /// </summary>
    public string ErrorId { get; }
}
