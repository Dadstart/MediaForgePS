using System.Management.Automation;
using Dadstart.Labs.MediaForge.Services;

namespace Dadstart.Labs.MediaForge.Cmdlets;

/// <summary>
/// Builds the terminal title with the active progress percentage as a prefix.
/// Current-item progress takes precedence over the main activity so the title tracks the live bar.
/// </summary>
internal sealed class ProgressTerminalTitle
{
    private readonly string _baseTitle;
    private int? _mainPercent;
    private int? _currentItemPercent;

    public ProgressTerminalTitle(string baseTitle)
    {
        _baseTitle = baseTitle;
    }

    private string CurrentTitle => Format(_baseTitle, _currentItemPercent ?? _mainPercent);

    public string Apply(ProgressRecord record)
    {
        var percent = ReadActivePercent(record);
        if (record.ActivityId == ProgressActivityIds.CurrentItem)
            _currentItemPercent = percent;
        else if (record.ActivityId == ProgressActivityIds.Main)
        {
            _mainPercent = percent;
            if (record.RecordType == ProgressRecordType.Completed)
                _currentItemPercent = null;
        }

        return CurrentTitle;
    }

    public static bool HasActivePercent(ProgressRecord record) =>
        ReadActivePercent(record).HasValue;

    public static string Format(string baseTitle, int? percent)
    {
        if (percent is null or < 0 or > 100)
            return baseTitle;

        return $"{percent.Value}% {baseTitle}";
    }

    private static int? ReadActivePercent(ProgressRecord record)
    {
        if (record.RecordType == ProgressRecordType.Completed)
            return null;

        if (record.PercentComplete is < 0 or > 100)
            return null;

        return record.PercentComplete;
    }
}
