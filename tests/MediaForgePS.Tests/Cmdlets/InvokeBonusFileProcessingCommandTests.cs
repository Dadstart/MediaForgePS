using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Dadstart.Labs.MediaForge.Cmdlets;
using Dadstart.Labs.MediaForge.Models;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Cmdlets;

public class InvokeBonusFileProcessingCommandTests
{
    [Fact]
    public void Defaults_AreInitializedCorrectly()
    {
        var cmdlet = new InvokeBonusFileProcessingCommand();

        Assert.NotNull(cmdlet);
        Assert.Equal("nvenc", cmdlet.DefaultVideoEncoder);
        Assert.Equal(string.Empty, cmdlet.InputPath);
        Assert.Equal(string.Empty, cmdlet.OutputPath);
    }

    [Fact]
    public void CreateDefaultVideoEncodingSettings_UsesNvencSettings_WhenEncoderIsNvenc()
    {
        var cmdlet = new InvokeBonusFileProcessingCommand
        {
            DefaultVideoEncoder = "nvenc"
        };

        var settings = InvokeCreateDefaultVideoEncodingSettings(cmdlet);

        var nvencSettings = Assert.IsType<NvencVideoEncodingSettings>(settings);
        Assert.Equal("hevc_nvenc", nvencSettings.Codec);
    }

    [Fact]
    public void CreateDefaultVideoEncodingSettings_UsesLibx264_WhenEncoderIsX264()
    {
        var cmdlet = new InvokeBonusFileProcessingCommand
        {
            DefaultVideoEncoder = "x264"
        };

        var settings = InvokeCreateDefaultVideoEncodingSettings(cmdlet);

        var crfSettings = Assert.IsType<ConstantRateVideoEncodingSettings>(settings);
        Assert.Equal("libx264", crfSettings.Codec);
    }

    [Fact]
    public void CreateDefaultVideoEncodingSettings_DefaultsToLibx265_WhenEncoderNotSpecified()
    {
        var cmdlet = new InvokeBonusFileProcessingCommand
        {
            DefaultVideoEncoder = null!
        };

        var settings = InvokeCreateDefaultVideoEncodingSettings(cmdlet);

        var crfSettings = Assert.IsType<ConstantRateVideoEncodingSettings>(settings);
        Assert.Equal("libx265", crfSettings.Codec);
    }

    [Fact]
    public void PlexLayout_DefinesExpectedBonusFoldersAndSuffixes()
    {
        var layoutField = typeof(InvokeBonusFileProcessingCommand)
            .GetField("_plexLayout", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(layoutField);

        var layout = (ValueTuple<string, string>[])layoutField!.GetValue(null)!;

        Assert.Equal(8, layout.Length);

        Assert.Contains(layout, p => p.Item1 == "Behind The Scenes" && p.Item2 == "behindthescenes");
        Assert.Contains(layout, p => p.Item1 == "Deleted Scenes" && p.Item2 == "deleted");
        Assert.Contains(layout, p => p.Item1 == "Featurettes" && p.Item2 == "featurette");
        Assert.Contains(layout, p => p.Item1 == "Interviews" && p.Item2 == "interview");
        Assert.Contains(layout, p => p.Item1 == "Scenes" && p.Item2 == "scene");
        Assert.Contains(layout, p => p.Item1 == "Shorts" && p.Item2 == "short");
        Assert.Contains(layout, p => p.Item1 == "Trailers" && p.Item2 == "trailer");
        Assert.Contains(layout, p => p.Item1 == "Other" && p.Item2 == "other");
    }

    [Fact]
    public void CreateAudioTrackMappings_CreatesCopyMapping_ForMultiChannelDts()
    {
        var streams = new List<MediaStream>
        {
            CreateAudioStream(1, "dts", "eng", 6, "DTS 5.1")
        };

        var mappings = InvokeCreateAudioTrackMappings(streams);

        Assert.Single(mappings);
        var copy = Assert.IsType<CopyAudioTrackMapping>(mappings[0]);
        Assert.Equal(0, copy.SourceIndex);
        Assert.Equal(0, copy.DestinationIndex);
    }

    [Fact]
    public void CreateAudioTrackMappings_CreatesEncodeMapping_ForAac()
    {
        var streams = new List<MediaStream>
        {
            CreateAudioStream(1, "aac", "eng", 2, "AAC 2.0")
        };

        var mappings = InvokeCreateAudioTrackMappings(streams);

        Assert.Single(mappings);
        var encode = Assert.IsType<EncodeAudioTrackMapping>(mappings[0]);
        Assert.Equal(0, encode.SourceIndex);
        Assert.Equal(0, encode.DestinationIndex);
        Assert.Equal("aac", encode.DestinationCodec);
        Assert.Equal(2, encode.DestinationChannels);
    }

    [Fact]
    public void CreateAudioTrackMappings_SwapsDtsAndMultiChannelAacOrder()
    {
        var streams = new List<MediaStream>
        {
            CreateAudioStream(1, "dts", "eng", 6, "DTS 5.1"),
            CreateAudioStream(2, "aac", "eng", 6, "AAC 5.1")
        };

        var mappings = InvokeCreateAudioTrackMappings(streams);

        Assert.Equal(2, mappings.Length);

        var first = mappings[0];
        var second = mappings[1];

        var firstEncode = Assert.IsType<EncodeAudioTrackMapping>(first);
        var secondCopy = Assert.IsType<CopyAudioTrackMapping>(second);

        Assert.Equal(0, firstEncode.DestinationIndex);
        Assert.Equal(1, secondCopy.DestinationIndex);
        Assert.Equal(6, firstEncode.DestinationChannels);
        Assert.Equal("aac", firstEncode.DestinationCodec);
    }

    [Fact]
    public void GetFileSizeOrZero_ReturnsExpectedSize_WhenFileExists()
    {
        var tempPath = Path.GetTempFileName();
        try
        {
            var content = new byte[] { 1, 2, 3, 4, 5, 6, 7 };
            File.WriteAllBytes(tempPath, content);

            var size = InvokeGetFileSizeOrZero(tempPath);

            Assert.Equal(content.Length, size);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    [Fact]
    public void GetFileSizeOrZero_ReturnsZero_WhenFileDoesNotExist()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.tmp");

        var size = InvokeGetFileSizeOrZero(path);

        Assert.Equal(0, size);
    }

    private static VideoEncodingSettings InvokeCreateDefaultVideoEncodingSettings(InvokeBonusFileProcessingCommand cmdlet)
    {
        var method = typeof(InvokeBonusFileProcessingCommand)
            .GetMethod("CreateDefaultVideoEncodingSettings", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var result = method!.Invoke(cmdlet, Array.Empty<object>());
        Assert.NotNull(result);
        return (VideoEncodingSettings)result!;
    }

    private static AudioTrackMapping[] InvokeCreateAudioTrackMappings(List<MediaStream> streams)
    {
        var method = typeof(InvokeBonusFileProcessingCommand)
            .GetMethod("CreateAudioTrackMappings", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var result = method!.Invoke(null, new object[] { streams });
        Assert.NotNull(result);
        return (AudioTrackMapping[])result!;
    }

    private static long InvokeGetFileSizeOrZero(string path)
    {
        var method = typeof(InvokeBonusFileProcessingCommand)
            .GetMethod("GetFileSizeOrZero", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var result = method!.Invoke(null, new object[] { path });
        Assert.NotNull(result);
        return (long)result!;
    }

    private static MediaStream CreateAudioStream(int index, string codec, string language, int channels, string? title = null)
    {
        var tags = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(language))
            tags["language"] = language;
        if (!string.IsNullOrEmpty(title))
            tags["title"] = title;

        var rawJson = $@"{{
            ""index"": {index},
            ""codec_name"": ""{codec}"",
            ""codec_type"": ""audio"",
            ""channels"": {channels},
            ""tags"": {{
                {(language != null ? $@"""language"": ""{language}""," : "")}
                {(title != null ? $@"""title"": ""{title}""," : "")}
                ""DURATION-{language}"": ""00:43:29.500000""
            }}
        }}";

        return new MediaStream(
            "audio",
            index,
            codec,
            string.Empty,
            string.Empty,
            tags,
            TimeSpan.Zero,
            language,
            rawJson);
    }
}

