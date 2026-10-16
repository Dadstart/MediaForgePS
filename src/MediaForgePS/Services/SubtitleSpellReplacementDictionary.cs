using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Dadstart.Labs.MediaForge.Services;

/// <summary>
/// File-backed misspelling → replacement map for subtitle spell fixing.
/// Default path: <c>%LocalApplicationData%/MediaForgePS/spelling-replacements.txt</c>.
/// Format: one <c>misspelling=replacement</c> pair per line.
/// </summary>
public sealed class SubtitleSpellReplacementDictionary : ISubtitleSpellReplacementDictionary
{
    private readonly object _sync = new();
    private readonly ILogger<SubtitleSpellReplacementDictionary> _logger;
    private readonly string _dictionaryPath;
    private Dictionary<string, string>? _cachedReplacements;

    /// <summary>
    /// Creates a replacement dictionary using the default MediaForgePS app-data path.
    /// </summary>
    public SubtitleSpellReplacementDictionary(ILogger<SubtitleSpellReplacementDictionary> logger)
        : this(logger, GetDefaultDictionaryPath())
    {
    }

    /// <summary>
    /// Creates a replacement dictionary at a specific path (tests / custom locations).
    /// </summary>
    public SubtitleSpellReplacementDictionary(ILogger<SubtitleSpellReplacementDictionary> logger, string dictionaryPath)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        ArgumentException.ThrowIfNullOrWhiteSpace(dictionaryPath);
        _dictionaryPath = Path.GetFullPath(dictionaryPath);
    }

    /// <inheritdoc />
    public string DictionaryPath => _dictionaryPath;

    /// <summary>
    /// Default replacement dictionary path under local application data.
    /// </summary>
    public static string GetDefaultDictionaryPath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MediaForgePS",
            "spelling-replacements.txt");

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> LoadReplacements()
    {
        lock (_sync)
            return new Dictionary<string, string>(LoadReplacementsUnlocked(), StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public bool AddReplacement(string misspelling, string replacement)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(misspelling);
        ArgumentException.ThrowIfNullOrWhiteSpace(replacement);

        var from = misspelling.Trim();
        var to = replacement.Trim();
        if (from.Length == 0 || to.Length == 0)
            return false;

        if (from.Contains('=', StringComparison.Ordinal))
            throw new ArgumentException("Misspelling cannot contain '='.", nameof(misspelling));
        if (to.Contains('=', StringComparison.Ordinal))
            throw new ArgumentException("Replacement cannot contain '='.", nameof(replacement));

        lock (_sync)
        {
            var map = LoadReplacementsUnlocked();
            if (map.TryGetValue(from, out var existing) &&
                string.Equals(existing, to, StringComparison.Ordinal))
                return false;

            map[from] = to;

            var directory = Path.GetDirectoryName(_dictionaryPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var ordered = map
                .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(pair => $"{pair.Key}={pair.Value}");
            AtomicFileHelper.WriteTextAtomically(
                _dictionaryPath,
                string.Join(Environment.NewLine, ordered) + Environment.NewLine,
                Encoding.UTF8,
                overwrite: true);

            _logger.LogInformation(
                "Saved spelling replacement '{From}' → '{To}' at {Path}",
                from,
                to,
                _dictionaryPath);
            return true;
        }
    }

    private Dictionary<string, string> LoadReplacementsUnlocked()
    {
        if (_cachedReplacements is not null)
            return _cachedReplacements;

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(_dictionaryPath))
        {
            _cachedReplacements = map;
            return map;
        }

        foreach (var line in File.ReadLines(_dictionaryPath))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith("#", StringComparison.Ordinal))
                continue;

            var separator = trimmed.IndexOf('=');
            if (separator <= 0 || separator >= trimmed.Length - 1)
            {
                _logger.LogWarning(
                    "Skipping invalid spelling replacement line in {Path}: {Line}",
                    _dictionaryPath,
                    trimmed);
                continue;
            }

            var from = trimmed[..separator].Trim();
            var to = trimmed[(separator + 1)..].Trim();
            if (from.Length == 0 || to.Length == 0)
                continue;

            map[from] = to;
        }

        _cachedReplacements = map;
        return map;
    }
}
