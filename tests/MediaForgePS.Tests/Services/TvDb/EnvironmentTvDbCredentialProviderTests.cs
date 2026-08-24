using System;
using Dadstart.Labs.MediaForge.Services.TvDb;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Services.TvDb;

public sealed class EnvironmentTvDbCredentialProviderTests : IDisposable
{
    private readonly string? _originalApiKey;
    private readonly string? _originalPin;

    public EnvironmentTvDbCredentialProviderTests()
    {
        _originalApiKey = Environment.GetEnvironmentVariable(EnvironmentTvDbCredentialProvider.ApiKeyVariableName);
        _originalPin = Environment.GetEnvironmentVariable(EnvironmentTvDbCredentialProvider.PinVariableName);
    }

    public void Dispose()
    {
        RestoreEnvironmentVariable(EnvironmentTvDbCredentialProvider.ApiKeyVariableName, _originalApiKey);
        RestoreEnvironmentVariable(EnvironmentTvDbCredentialProvider.PinVariableName, _originalPin);
    }

    [Fact]
    public void ApiKey_WhenSet_ReturnsTrimmedValue()
    {
        Environment.SetEnvironmentVariable(EnvironmentTvDbCredentialProvider.ApiKeyVariableName, "  test-key  ");

        var provider = new EnvironmentTvDbCredentialProvider();

        Assert.Equal("test-key", provider.ApiKey);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ApiKey_WhenMissingOrWhitespace_ReturnsNull(string? value)
    {
        Environment.SetEnvironmentVariable(EnvironmentTvDbCredentialProvider.ApiKeyVariableName, value);

        var provider = new EnvironmentTvDbCredentialProvider();

        Assert.Null(provider.ApiKey);
    }

    [Fact]
    public void Pin_WhenSet_ReturnsTrimmedValue()
    {
        Environment.SetEnvironmentVariable(EnvironmentTvDbCredentialProvider.PinVariableName, " 1234 ");

        var provider = new EnvironmentTvDbCredentialProvider();

        Assert.Equal("1234", provider.Pin);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("\t")]
    public void Pin_WhenMissingOrWhitespace_ReturnsNull(string? value)
    {
        Environment.SetEnvironmentVariable(EnvironmentTvDbCredentialProvider.PinVariableName, value);

        var provider = new EnvironmentTvDbCredentialProvider();

        Assert.Null(provider.Pin);
    }

    [Fact]
    public void VariableNames_MatchExpectedEnvironmentKeys()
    {
        Assert.Equal("TVDB_API_KEY", EnvironmentTvDbCredentialProvider.ApiKeyVariableName);
        Assert.Equal("TVDB_PIN", EnvironmentTvDbCredentialProvider.PinVariableName);
    }

    private static void RestoreEnvironmentVariable(string name, string? value) =>
        Environment.SetEnvironmentVariable(name, value);
}
