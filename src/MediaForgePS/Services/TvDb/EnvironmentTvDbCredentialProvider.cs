using System;

namespace Dadstart.Labs.MediaForge.Services.TvDb;

/// <summary>
/// Reads TheTVDB credentials from process environment variables.
/// </summary>
public sealed class EnvironmentTvDbCredentialProvider : ITvDbCredentialProvider
{
    public const string ApiKeyVariableName = "TVDB_API_KEY";
    public const string PinVariableName = "TVDB_PIN";

    public string? ApiKey
    {
        get
        {
            var value = Environment.GetEnvironmentVariable(ApiKeyVariableName);
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }

    public string? Pin
    {
        get
        {
            var value = Environment.GetEnvironmentVariable(PinVariableName);
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
