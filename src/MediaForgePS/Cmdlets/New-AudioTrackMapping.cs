using System;
using System.Management.Automation;
using Dadstart.Labs.MediaForge.Models;
using Microsoft.Extensions.Logging;

namespace Dadstart.Labs.MediaForge.Cmdlets;

/// <summary>
/// Creates a new audio track mapping configuration for media file conversion.
/// </summary>
/// <remarks>
/// This cmdlet creates either a copy mapping (to copy audio streams without re-encoding) or
/// an encode mapping (to encode audio streams with specific codec, bitrate, and channel settings).
/// </remarks>
[Cmdlet(VerbsCommon.New, nameof(AudioTrackMapping), DefaultParameterSetName = CopyParameterSet)]
[OutputType(typeof(AudioTrackMapping))]
public class NewAudioTrackMappingCommand : CmdletBase
{
    private const string CopyParameterSet = "Copy";
    private const string EncodeParameterSet = "Encode";
    /// <summary>
    /// Optional title metadata for the audio track.
    /// </summary>
    [Parameter(
        Mandatory = false,
        HelpMessage = "Title metadata for the audio track")]
    public string? Title { get; set; }

    /// <summary>
    /// Source stream index (typically 0 for the input file).
    /// </summary>
    [Parameter(
        Mandatory = true,
        Position = 0,
        HelpMessage = "Source stream index (typically 0 for the input file)")]
    [ValidateRange(0, int.MaxValue)]
    public int SourceStream { get; set; }

    /// <summary>
    /// Source audio stream index within the source stream.
    /// </summary>
    [Parameter(
        Mandatory = true,
        Position = 1,
        HelpMessage = "Source audio stream index within the source stream")]
    [ValidateRange(0, int.MaxValue)]
    public int SourceIndex { get; set; }

    /// <summary>
    /// Destination audio stream index in the output file.
    /// </summary>
    [Parameter(
        Mandatory = true,
        Position = 2,
        HelpMessage = "Destination audio stream index in the output file")]
    [ValidateRange(0, int.MaxValue)]
    public int DestinationIndex { get; set; }

    /// <summary>
    /// Creates a copy mapping that copies the audio stream without re-encoding.
    /// </summary>
    [Parameter(
        ParameterSetName = CopyParameterSet,
        Mandatory = true,
        HelpMessage = "Creates a copy mapping that copies the audio stream without re-encoding")]
    public SwitchParameter Copy { get; set; }

    /// <summary>
    /// Creates an encode mapping that encodes the audio stream with specified settings.
    /// </summary>
    [Parameter(
        ParameterSetName = EncodeParameterSet,
        Mandatory = true,
        HelpMessage = "Creates an encode mapping that encodes the audio stream with specified settings")]
    public SwitchParameter Encode { get; set; }

    /// <summary>
    /// Destination codec for encoding (e.g., "aac", "mp3", "opus").
    /// </summary>
    [Parameter(
        ParameterSetName = EncodeParameterSet,
        Mandatory = true,
        HelpMessage = "Destination codec for encoding (e.g., 'aac', 'mp3', 'opus')")]
    [ValidateNotNullOrEmpty]
    public string Codec { get; set; } = string.Empty;

    /// <summary>
    /// Destination bitrate in kbps. If not specified, defaults are used based on channel count.
    /// </summary>
    [Parameter(
        ParameterSetName = EncodeParameterSet,
        Mandatory = false,
        HelpMessage = "Destination bitrate in kbps. If not specified, defaults are used based on channel count")]
    [ValidateRange(0, int.MaxValue)]
    public int Bitrate { get; set; }

    /// <summary>
    /// Number of audio channels for the encoded output (e.g., 1 for mono, 2 for stereo, 6 for 5.1, 8 for 7.1).
    /// </summary>
    [Parameter(
        ParameterSetName = EncodeParameterSet,
        Mandatory = true,
        HelpMessage = "Number of audio channels for the encoded output (e.g., 1 for mono, 2 for stereo, 6 for 5.1, 8 for 7.1)")]
    [ValidateRange(1, int.MaxValue)]
    public int Channels { get; set; }

    /// <summary>
    /// Processes the audio track mapping creation request.
    /// </summary>
    protected override void Process()
    {
        Logger.LogInformation("Creating audio track mapping: SourceStream={SourceStream}, SourceIndex={SourceIndex}, DestinationIndex={DestinationIndex}, ParameterSet={ParameterSetName}",
            SourceStream, SourceIndex, DestinationIndex, ParameterSetName);

        if (!string.Equals(ParameterSetName, CopyParameterSet) && !string.Equals(ParameterSetName, EncodeParameterSet))
            throw new InvalidOperationException($"Unexpected or missing parameter set {ParameterSetName}");

        AudioTrackMapping mapping;

        if (ParameterSetName == CopyParameterSet)
        {
            mapping = new CopyAudioTrackMapping(
                Title,
                SourceStream,
                SourceIndex,
                DestinationIndex);
            Logger.LogDebug("Created CopyAudioTrackMapping");
        }
        else
        {
            mapping = new EncodeAudioTrackMapping(
                Title,
                SourceStream,
                SourceIndex,
                DestinationIndex,
                Codec,
                Bitrate,
                Channels);
            Logger.LogDebug("Created EncodeAudioTrackMapping with Codec={Codec}, Bitrate={Bitrate}, Channels={Channels}",
                Codec, Bitrate, Channels);
        }

        Logger.LogInformation("Successfully created audio track mapping");
        WriteObject(mapping);
    }
}
