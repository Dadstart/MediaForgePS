using System;
using System.IO;
using System.Linq;
using System.Management.Automation;
using Dadstart.Labs.MediaForge.Cmdlets;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.System;
using Dadstart.Labs.MediaForge.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Cmdlets;

public class TestSubtitleSpellingCommandTests : IDisposable
{
    private readonly Mock<ILoggerFactory> _loggerFactoryMock;
    private readonly Mock<IDebuggerService> _debuggerServiceMock;
    private readonly Mock<ISubtitleSpellCheckService> _spellCheckServiceMock;
    private readonly ServiceProvider _serviceProvider;
    private readonly ModuleServicesTestScope _moduleServicesScope;

    public TestSubtitleSpellingCommandTests()
    {
        _loggerFactoryMock = new Mock<ILoggerFactory>();
        _debuggerServiceMock = new Mock<IDebuggerService>();
        _spellCheckServiceMock = new Mock<ISubtitleSpellCheckService>();

        var loggerMock = new Mock<ILogger<TestSubtitleSpellingCommand>>();
        var pathResolverLoggerMock = new Mock<ILogger<PathResolver>>();
        _loggerFactoryMock.Setup(f => f.CreateLogger(It.IsAny<string>()))
            .Returns((string name) =>
            {
                if (name?.Contains("PathResolver", StringComparison.Ordinal) == true)
                    return pathResolverLoggerMock.Object;
                return loggerMock.Object;
            });
        _debuggerServiceMock.Setup(d => d.BreakIfDebugging(It.IsAny<bool>()));

        var services = new ServiceCollection();
        services.AddSingleton(_loggerFactoryMock.Object);
        services.AddSingleton(_debuggerServiceMock.Object);
        services.AddSingleton(_spellCheckServiceMock.Object);
        services.AddSingleton<ILogger<PathResolver>>(pathResolverLoggerMock.Object);
        services.AddSingleton<IPathResolver, PathResolver>();
        _serviceProvider = services.BuildServiceProvider();
        _moduleServicesScope = new ModuleServicesTestScope(_serviceProvider);
    }

    public void Dispose()
    {
        _moduleServicesScope.Dispose();
        _serviceProvider.Dispose();
    }

    [Fact]
    public void TestSubtitleSpelling_WhenIssuesExist_WritesIssues()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "MediaForgePS_SpellCmdlet_" + Guid.NewGuid().ToString("N"));
        var srtPath = Path.Combine(tempDir, "test.srt");
        try
        {
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(srtPath, "1\n00:00:01,000 --> 00:00:02,000\nhello\n\n");

            var issue = new SubtitleSpellingIssue(
                srtPath,
                1,
                "00:00:01,000 --> 00:00:02,000",
                1,
                "misspeled",
                "hello misspeled",
                SubtitleSpellingIssue.EmptySuggestions);
            _spellCheckServiceMock
                .Setup(s => s.CheckFile(srtPath, It.IsAny<SubtitleSpellCheckOptions>()))
                .Returns([issue]);

            using var ps = CreatePowerShell();
            ps.AddCommand("Test-SubtitleSpelling")
                .AddParameter("InputPath", srtPath);

            var results = ps.Invoke().Select(r => r.BaseObject).OfType<SubtitleSpellingIssue>().ToList();
            var errors = ps.Streams.Error.ReadAll();

            Assert.Empty(errors);
            Assert.Single(results);
            Assert.Equal("misspeled", results[0].Word);
            _spellCheckServiceMock.Verify(
                s => s.CheckFile(srtPath, It.IsAny<SubtitleSpellCheckOptions>()),
                Times.Once);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void TestSubtitleSpelling_IgnoreWord_PassesThroughOptions()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "MediaForgePS_SpellCmdlet_" + Guid.NewGuid().ToString("N"));
        var srtPath = Path.Combine(tempDir, "test.srt");
        try
        {
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(srtPath, "1\n00:00:01,000 --> 00:00:02,000\nhello\n\n");

            SubtitleSpellCheckOptions? captured = null;
            _spellCheckServiceMock
                .Setup(s => s.CheckFile(srtPath, It.IsAny<SubtitleSpellCheckOptions>()))
                .Callback<string, SubtitleSpellCheckOptions?>((_, options) => captured = options)
                .Returns(Array.Empty<SubtitleSpellingIssue>());

            using var ps = CreatePowerShell();
            ps.AddCommand("Test-SubtitleSpelling")
                .AddParameter("InputPath", srtPath)
                .AddParameter("IgnoreWord", new[] { "Cartman" })
                .AddParameter("IncludeSuggestions", true);

            ps.Invoke();
            var errors = ps.Streams.Error.ReadAll();

            Assert.Empty(errors);
            Assert.NotNull(captured);
            Assert.Contains("Cartman", captured!.IgnoreWords!);
            Assert.True(captured.IncludeSuggestions);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void TestSubtitleSpelling_NonSrtFile_WritesWarningAndSkips()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "MediaForgePS_SpellCmdlet_" + Guid.NewGuid().ToString("N"));
        var txtPath = Path.Combine(tempDir, "notes.txt");
        try
        {
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(txtPath, "not an srt");

            using var ps = CreatePowerShell();
            ps.AddCommand("Test-SubtitleSpelling")
                .AddParameter("InputPath", txtPath);

            var results = ps.Invoke();
            var warnings = ps.Streams.Warning.ReadAll();

            Assert.Empty(results);
            Assert.Contains(warnings, w => w.Message.Contains("non-SRT", StringComparison.OrdinalIgnoreCase)
                || w.Message.Contains("No SRT", StringComparison.OrdinalIgnoreCase));
            _spellCheckServiceMock.Verify(
                s => s.CheckFile(It.IsAny<string>(), It.IsAny<SubtitleSpellCheckOptions>()),
                Times.Never);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    private static PowerShell CreatePowerShell() =>
        PowerShellCmdletTestHost.Create<TestSubtitleSpellingCommand>("Test-SubtitleSpelling");
}
