using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dadstart.Labs.MediaForge.Cmdlets;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Module;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Cmdlets;

public class ConvertMediaFileCommandTests
{
    [Fact]
    public void BuildFfmpegArguments_ForwardsPassValueToVideoSettings()
    {
        var command = new ConvertMediaFileCommand
        {
            VideoEncodingSettings = new RecordingVideoEncodingSettings(),
            AudioTrackMappings = Array.Empty<AudioTrackMapping>()
        };

        try
        {
            var firstPassArgs = InvokeBuildArguments(command, 1).ToArray();
            var secondPassArgs = InvokeBuildArguments(command, 2).ToArray();
            var singlePassArgs = InvokeBuildArguments(command, null).ToArray();

            Assert.Contains("pass-1", firstPassArgs);
            Assert.Contains("pass-2", secondPassArgs);
            Assert.Contains("pass-null", singlePassArgs);
        }
        finally
        {
            CmdletContext.Current = null;
        }
    }

    private static IEnumerable<string> InvokeBuildArguments(ConvertMediaFileCommand command, int? pass)
    {
        var method = typeof(ConvertMediaFileCommand).GetMethod("BuildFfmpegArguments", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Failed to locate BuildFfmpegArguments.");

        var result = method.Invoke(command, new object?[] { pass }) as IEnumerable<string>;
        return result ?? Array.Empty<string>();
    }

    private sealed record RecordingVideoEncodingSettings()
        : VideoEncodingSettings("codec", "preset", "profile", "tune", Array.Empty<string>())
    {
        public override bool IsSinglePass => false;

        public override string ToString() => "recording";

        public override IList<string> ToFfmpegArgs(int? pass)
        {
            var indicator = pass.HasValue ? $"pass-{pass.Value}" : "pass-null";
            return new[] { indicator };
        }
    }
}
