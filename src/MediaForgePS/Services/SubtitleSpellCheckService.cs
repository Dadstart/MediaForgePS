using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Dadstart.Labs.MediaForge.Models;
using Microsoft.Extensions.Logging;
using WeCantSpell.Hunspell;

namespace Dadstart.Labs.MediaForge.Services;

/// <summary>
/// Spell-checks SRT cue text with WeCantSpell.Hunspell and an embedded en_US dictionary.
/// </summary>
public sealed class SubtitleSpellCheckService : ISubtitleSpellCheckService
{
    private const string EmbeddedDicResourceName = "Dadstart.Labs.MediaForge.Dictionaries.en_US.dic";
    private const string EmbeddedAffResourceName = "Dadstart.Labs.MediaForge.Dictionaries.en_US.aff";

    private readonly ILogger<SubtitleSpellCheckService> _logger;
    private readonly object _embeddedLock = new();
    private readonly ConcurrentDictionary<string, WordList> _fileDictionaries =
        new(StringComparer.OrdinalIgnoreCase);
    private WordList? _embeddedDictionary;

    /// <summary>
    /// Creates a spell-check service.
    /// </summary>
    public SubtitleSpellCheckService(ILogger<SubtitleSpellCheckService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public IReadOnlyList<SubtitleSpellingIssue> CheckFile(string path, SubtitleSpellCheckOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var content = File.ReadAllText(path);
        return CheckContent(path, content, options);
    }

    /// <inheritdoc />
    public IReadOnlyList<SubtitleSpellingIssue> CheckContent(
        string path,
        string content,
        SubtitleSpellCheckOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(content);

        options ??= SubtitleSpellCheckOptions.Default;
        var dictionary = ResolveDictionary(options.DictionaryPath);
        var ignoreWords = BuildIgnoreSet(options.IgnoreWords);
        var blocks = SrtDocumentHelper.ParseSrtBlocks(content);
        var issues = new List<SubtitleSpellingIssue>();

        for (var blockIndex = 0; blockIndex < blocks.Count; blockIndex++)
        {
            var block = blocks[blockIndex];
            var cueNumber = ResolveCueNumber(block.SequenceLine, blockIndex);
            for (var lineIndex = 0; lineIndex < block.TextLines.Count; lineIndex++)
            {
                var lineText = block.TextLines[lineIndex];
                var occurrenceByWord = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var word in SubtitleSpellTokenizer.Tokenize(lineText))
                {
                    if (ignoreWords.Contains(word))
                        continue;

                    if (dictionary.Check(word))
                        continue;

                    occurrenceByWord.TryGetValue(word, out var priorCount);
                    var occurrence = priorCount + 1;
                    occurrenceByWord[word] = occurrence;

                    var suggestions = options.IncludeSuggestions
                        ? GetSuggestions(word, options)
                        : SubtitleSpellingIssue.EmptySuggestions;

                    issues.Add(new SubtitleSpellingIssue(
                        path,
                        cueNumber,
                        block.TimestampLine,
                        lineIndex + 1,
                        [word],
                        lineText,
                        suggestions,
                        occurrence));
                }
            }
        }

        _logger.LogDebug("Spell-checked {Path}: {IssueCount} issue(s)", path, issues.Count);
        return issues;
    }

    /// <inheritdoc />
    public bool IsCorrectlySpelled(string word, SubtitleSpellCheckOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(word);
        options ??= SubtitleSpellCheckOptions.Default;
        return ResolveDictionary(options.DictionaryPath).Check(word);
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetSuggestions(string word, SubtitleSpellCheckOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(word);
        options ??= SubtitleSpellCheckOptions.Default;
        var dictionary = ResolveDictionary(options.DictionaryPath);
        var queryOptions = new QueryOptions
        {
            MaxSuggestions = 5,
            TimeLimitSuggestGlobal = TimeSpan.FromSeconds(2),
            TimeLimitSuggestStep = TimeSpan.FromMilliseconds(250)
        };

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try
        {
            return dictionary.Suggest(word, queryOptions, cts.Token).Take(5).ToArray();
        }
        catch (OperationCanceledException)
        {
            return Array.Empty<string>();
        }
    }

    private WordList ResolveDictionary(string? dictionaryPath)
    {
        if (string.IsNullOrWhiteSpace(dictionaryPath))
            return GetEmbeddedDictionary();

        var dicPath = Path.GetFullPath(dictionaryPath);
        return _fileDictionaries.GetOrAdd(dicPath, LoadDictionaryFromFiles);
    }

    private WordList GetEmbeddedDictionary()
    {
        if (_embeddedDictionary is not null)
            return _embeddedDictionary;

        lock (_embeddedLock)
        {
            if (_embeddedDictionary is not null)
                return _embeddedDictionary;

            _embeddedDictionary = LoadEmbeddedDictionary();
            return _embeddedDictionary;
        }
    }

    private static WordList LoadEmbeddedDictionary()
    {
        var assembly = typeof(SubtitleSpellCheckService).Assembly;
        using var dicStream = OpenRequiredResource(assembly, EmbeddedDicResourceName);
        using var affStream = OpenRequiredResource(assembly, EmbeddedAffResourceName);
        return WordList.CreateFromStreams(dicStream, affStream);
    }

    private static Stream OpenRequiredResource(Assembly assembly, string resourceName)
    {
        var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
            throw new InvalidOperationException($"Embedded dictionary resource '{resourceName}' was not found.");

        return stream;
    }

    private static WordList LoadDictionaryFromFiles(string dictionaryFilePath)
    {
        if (!File.Exists(dictionaryFilePath))
            throw new FileNotFoundException($"Hunspell dictionary file was not found: {dictionaryFilePath}", dictionaryFilePath);

        var affixFilePath = Path.ChangeExtension(dictionaryFilePath, ".aff");
        if (!File.Exists(affixFilePath))
            throw new FileNotFoundException($"Hunspell affix file was not found: {affixFilePath}", affixFilePath);

        return WordList.CreateFromFiles(dictionaryFilePath, affixFilePath);
    }

    private static HashSet<string> BuildIgnoreSet(IReadOnlyList<string>? ignoreWords)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (ignoreWords is null)
            return set;

        foreach (var word in ignoreWords)
        {
            if (!string.IsNullOrWhiteSpace(word))
                set.Add(word.Trim());
        }

        return set;
    }

    private static int ResolveCueNumber(string sequenceLine, int blockIndex)
    {
        if (int.TryParse(sequenceLine.Trim(), out var cueNumber) && cueNumber > 0)
            return cueNumber;

        return blockIndex + 1;
    }
}
