using System.Collections;
using System.Management.Automation;
using Dadstart.Labs.MediaForge.Models;
using Microsoft.Extensions.Logging;

namespace Dadstart.Labs.MediaForge.Cmdlets;

/// <summary>
/// Creates a new VideoEncodingSettings object with specified encoding parameters.
/// </summary>
/// <remarks>
/// This cmdlet creates a VideoEncodingSettings object that encapsulates video encoding parameters
/// including codec, CRF (Constant Rate Factor) or bitrate, preset, codec profile, and tune settings.
/// This object can be used with video encoding operations to ensure consistent parameter application.
/// </remarks>
[Cmdlet(VerbsCommon.New, nameof(VideoEncodingSettings), DefaultParameterSetName = CrfParameterSet)]
[OutputType(typeof(VideoEncodingSettings))]
public class NewVideoEncodingSettingsCommand : CmdletBase
{
    private static class HelpMessages
    {
        public const string Codec = "The video codec to use for encoding (e.g., 'h264', 'h265', 'vp9')";
        public const string CRF = "The Constant Rate Factor value for quality control. Lower values indicate higher quality. Typical ranges: 18-28 for H.264, 20-30 for H.265";
        public const string Bitrate = "The bitrate for variable bitrate encoding in kbps";
        public const string Preset = "The encoding preset that balances speed vs. compression efficiency (e.g., 'ultrafast', 'superfast', 'veryfast', 'faster', 'fast', 'medium', 'slow', 'slower', 'veryslow')";
        public const string CodecProfile = "The codec profile to use (e.g., 'high', 'main', 'baseline' for H.264)";
        public const string Tune = "The tuning option for the codec (e.g., 'film', 'animation', 'grain', 'stillimage', 'fastdecode', 'zerolatency')";
    }
    private const string CrfParameterSet = "CRF";
    private const string VbrParameterSet = "VBR";

    /// <summary>
    /// The video codec to use for encoding (e.g., 'h264', 'h265', 'vp9').
    /// </summary>
    [Parameter(
        Mandatory = true,
        ParameterSetName = CrfParameterSet,
        HelpMessage = HelpMessages.Codec)]
    [Parameter(
        Mandatory = true,
        ParameterSetName = VbrParameterSet,
        HelpMessage = HelpMessages.Codec)]
    [ValidateNotNullOrEmpty]
    public string Codec { get; set; } = string.Empty;

    /// <summary>
    /// The Constant Rate Factor value for quality control. Lower values indicate higher quality.
    /// Typical ranges: 18-28 for H.264, 20-30 for H.265.
    /// </summary>
    [Parameter(
        Mandatory = true,
        ParameterSetName = CrfParameterSet,
        HelpMessage = HelpMessages.CRF)]
    [ValidateRange(0, 51)]
    public int CRF { get; set; }

    /// <summary>
    /// The bitrate for variable bitrate encoding in kbps.
    /// </summary>
    [Parameter(
        Mandatory = true,
        ParameterSetName = VbrParameterSet,
        HelpMessage = HelpMessages.Bitrate)]
    [ValidateRange(0, int.MaxValue)]
    public int Bitrate { get; set; }

    /// <summary>
    /// The encoding preset that balances speed vs. compression efficiency
    /// (e.g., 'ultrafast', 'superfast', 'veryfast', 'faster', 'fast', 'medium',
    /// 'slow', 'slower', 'veryslow').
    /// </summary>
    [Parameter(
        ParameterSetName = CrfParameterSet,
        HelpMessage = HelpMessages.Preset)]
    [Parameter(
        ParameterSetName = VbrParameterSet,
        HelpMessage = HelpMessages.Preset)]
    public string Preset { get; set; } = "slow";

    /// <summary>
    /// The codec profile to use (e.g., 'high', 'main', 'baseline' for H.264).
    /// </summary>
    [Parameter(
        ParameterSetName = CrfParameterSet,
        HelpMessage = HelpMessages.CodecProfile)]
    public string CodecProfile { get; set; } = "high";

    /// <summary>
    /// The tuning option for the codec (e.g., 'film', 'animation', 'grain',
    /// 'stillimage', 'fastdecode', 'zerolatency').
    /// </summary>
    [Parameter(
        ParameterSetName = CrfParameterSet,
        HelpMessage = HelpMessages.Tune)]
    public string Tune { get; set; } = "film";

    /// <summary>
    /// Processes the video encoding settings creation request.
    /// </summary>
    protected override void Process()
    {
        Logger.LogInformation("Creating video encoding settings: ParameterSet={ParameterSetName}, Codec={Codec}",
            ParameterSetName, Codec);

        VideoEncodingSettings settings;

        if (ParameterSetName == CrfParameterSet)
        {
            settings = new ConstantRateVideoEncodingSettings(Codec, Preset, CodecProfile, Tune, CRF);
            Logger.LogDebug("Created ConstantRateVideoEncodingSettings with CRF={CRF}, Preset={Preset}, CodecProfile={CodecProfile}, Tune={Tune}",
                CRF, Preset, CodecProfile, Tune);
        }
        else if (ParameterSetName == VbrParameterSet)
        {
            settings = new VariableRateVideoEncodingSettings(Codec, Preset, CodecProfile, Tune, Bitrate);
            Logger.LogDebug("Created VariableRateVideoEncodingSettings with Bitrate={Bitrate}, Preset={Preset}",
                Bitrate, Preset);
        }
        else
        {
            throw new InvalidOperationException($"Unexpected or missing parameter set {ParameterSetName}");
        }

        Logger.LogInformation("Successfully created video encoding settings");
        WriteObject(settings);
    }

    private static IList<string> ConvertHashtableToArgs(Hashtable? hashtable)
    {
        if (hashtable == null || hashtable.Count == 0)
            return Array.Empty<string>();

        List<string> args = new();
        foreach (DictionaryEntry entry in hashtable)
        {
            string key = entry.Key?.ToString() ?? string.Empty;
            string value = entry.Value?.ToString() ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(key))
            {
                args.Add($"-{key}");
                if (!string.IsNullOrWhiteSpace(value))
                    args.Add(value);
            }
        }

        return args;
    }
}

