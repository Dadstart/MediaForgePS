namespace Dadstart.Labs.MediaForge.Services.System;

/// <summary>
/// Builder for constructing command-line arguments.
/// Supports flags, key-value pairs, and multiple occurrences of the same key.
/// </summary>
public class ArgumentBuilder(IPlatformService platformService) : IArgumentBuilder
{
    private readonly List<Argument> _arguments = new();
    private readonly IPlatformService _platformService = platformService;

    /// <summary>
    /// Adds a flag argument (e.g., "-y").
    /// </summary>
    /// <param name="flag">The flag to add (e.g., "-y").</param>
    /// <returns>The builder instance for method chaining.</returns>
    public IArgumentBuilder AddFlag(string flag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(flag);
        _arguments.Add(new Argument(flag, null));
        return this;
    }

    /// <summary>
    /// Adds a key-value pair argument (e.g., "-c:v", "libx264").
    /// </summary>
    /// <param name="key">The argument key (e.g., "-c:v").</param>
    /// <param name="value">The argument value (e.g., "libx264").</param>
    /// <returns>The builder instance for method chaining.</returns>
    public IArgumentBuilder AddOption(string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        _arguments.Add(new Argument(key, value));
        return this;
    }

    /// <summary>
    /// Adds a key-value pair argument with optional value (e.g., "-metadata:s:a:0", "title=\"Track 1\"").
    /// If value is null or whitespace, the argument is not added.
    /// </summary>
    /// <param name="key">The argument key.</param>
    /// <param name="value">The argument value, or null/whitespace to skip.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public IArgumentBuilder AddOptionIfNotNull(string key, string? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (!string.IsNullOrWhiteSpace(value))
            _arguments.Add(new Argument(key, value));
        return this;
    }

    /// <summary>
    /// Converts the builder's arguments to an enumerable of strings suitable command-line execution.
    /// </summary>
    /// <returns>An enumerable of argument strings.</returns>
    public IEnumerable<string> ToArguments()
    {
        foreach (var arg in _arguments)
        {
            yield return arg.Key;
            if (arg.Value != null)
                yield return QuoteArgumentValue(arg.Value);
        }
    }

    /// <summary>
    /// Quotes and escapes an argument value according to platform-specific rules.
    /// </summary>
    /// <param name="value">The argument value to quote.</param>
    /// <returns>A properly quoted and escaped argument value.</returns>
    private string QuoteArgumentValue(string value)
    {
        return _platformService.QuoteArgument(value);
    }

    /// <summary>
    /// Converts the builder's arguments to a dictionary representation.
    /// Flags are represented with empty string values.
    /// Multiple occurrences of the same key will only keep the last value.
    /// </summary>
    /// <returns>A dictionary of argument keys to values.</returns>
    public IDictionary<string, string> ToDictionary()
    {
        var dict = new Dictionary<string, string>();
        foreach (var arg in _arguments)
        {
            dict[arg.Key] = arg.Value ?? string.Empty;
        }
        return dict;
    }

    /// <summary>
    /// Clears all arguments from the builder.
    /// </summary>
    /// <returns>The builder instance for method chaining.</returns>
    public IArgumentBuilder Clear()
    {
        _arguments.Clear();
        return this;
    }

    private sealed record Argument(string Key, string? Value);
}

