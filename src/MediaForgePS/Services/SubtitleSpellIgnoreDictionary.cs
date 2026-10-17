using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Dadstart.Labs.MediaForge.Services;

/// <summary>
/// File-backed forever-ignore word list for subtitle spell checking.
/// Default path: <c>%LocalApplicationData%/MediaForgePS/spelling-ignore.txt</c>.
/// </summary>
public sealed class SubtitleSpellIgnoreDictionary : ISubtitleSpellIgnoreDictionary
{
    private readonly object _sync = new();
    private readonly ILogger<SubtitleSpellIgnoreDictionary> _logger;
    private readonly string _dictionaryPath;
    private HashSet<string>? _cachedWords;

    /// <summary>
    /// Creates an ignore dictionary using the default MediaForgePS app-data path.
    /// </summary>
    public SubtitleSpellIgnoreDictionary(ILogger<SubtitleSpellIgnoreDictionary> logger)
        : this(logger, GetDefaultDictionaryPath())
    {
    }

    /// <summary>
    /// Creates an ignore dictionary at a specific path (tests / custom locations).
    /// </summary>
    public SubtitleSpellIgnoreDictionary(ILogger<SubtitleSpellIgnoreDictionary> logger, string dictionaryPath)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        ArgumentException.ThrowIfNullOrWhiteSpace(dictionaryPath);
        _dictionaryPath = Path.GetFullPath(dictionaryPath);
    }

    /// <inheritdoc />
    public string DictionaryPath => _dictionaryPath;

    /// <summary>
    /// Default forever-ignore dictionary path under local application data.
    /// </summary>
    public static string GetDefaultDictionaryPath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MediaForgePS",
            "spelling-ignore.txt");

    /// <inheritdoc />
    public IReadOnlyCollection<string> LoadWords()
    {
        lock (_sync)
            return LoadWordsUnlocked().ToArray();
    }

    /// <inheritdoc />
    public bool AddWord(string word)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(word);
        var trimmed = word.Trim();
        if (trimmed.Length == 0)
            return false;

        lock (_sync)
        {
            var words = LoadWordsUnlocked();
            if (!words.Add(trimmed))
                return false;

            var directory = Path.GetDirectoryName(_dictionaryPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            // Rewrite the full sorted set so the file stays tidy and durable.
            var ordered = words.OrderBy(w => w, StringComparer.OrdinalIgnoreCase).ToArray();
            AtomicFileHelper.WriteTextAtomically(
                _dictionaryPath,
                string.Join(Environment.NewLine, ordered) + Environment.NewLine,
                Encoding.UTF8,
                overwrite: true);

            _logger.LogInformation(
                "Added '{Word}' to persistent spelling ignore dictionary at {Path}",
                trimmed,
                _dictionaryPath);
            return true;
        }
    }

    private HashSet<string> LoadWordsUnlocked()
    {
        if (_cachedWords is not null)
            return _cachedWords;

        var words = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(_dictionaryPath))
        {
            _cachedWords = words;
            return words;
        }

        foreach (var line in File.ReadLines(_dictionaryPath))
        {
            var trimmed = line.Trim();
            if (trimmed.Length > 0 && !trimmed.StartsWith("#", StringComparison.Ordinal))
                words.Add(trimmed);
        }

        _cachedWords = words;
        return words;
    }
}
