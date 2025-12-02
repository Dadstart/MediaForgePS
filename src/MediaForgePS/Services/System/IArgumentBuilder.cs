namespace Dadstart.Labs.MediaForge.Services.System;

/// <summary>
/// Builder for constructing command-line arguments.
/// Supports flags, key-value pairs, and multiple occurrences of the same key.
/// </summary>
public interface IArgumentBuilder
{
    /// <summary>
    /// Adds a flag argument (e.g., "-y").
    /// </summary>
    /// <param name="flag">The flag to add (e.g., "-y").</param>
    /// <returns>The builder instance for method chaining.</returns>
    public IArgumentBuilder AddFlag(string flag);

    /// <summary>
    /// Adds a key-value pair argument (e.g., "-c:v", "libx264").
    /// </summary>
    /// <param name="key">The argument key (e.g., "-c:v").</param>
    /// <param name="value">The argument value (e.g., "libx264").</param>
    /// <returns>The builder instance for method chaining.</returns>
    public IArgumentBuilder AddOption(string key, string value);

    /// <summary>
    /// Adds a key-value pair argument with optional value (e.g., "-metadata:s:a:0", "title=\"Track 1\"").
    /// If value is null or whitespace, the argument is not added.
    /// </summary>
    /// <param name="key">The argument key.</param>
    /// <param name="value">The argument value, or null/whitespace to skip.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public IArgumentBuilder AddOptionIfNotNull(string key, string? value);

    /// <summary>
    /// Converts the builder's arguments to an enumerable of strings suitable for Ffmpeg command-line execution.
    /// </summary>
    /// <returns>An enumerable of argument strings.</returns>
    public IEnumerable<string> ToArguments();

    /// <summary>
    /// Converts the builder's arguments to a dictionary representation.
    /// Flags are represented with empty string values.
    /// Multiple occurrences of the same key will only keep the last value.
    /// </summary>
    /// <returns>A dictionary of argument keys to values.</returns>
    public IDictionary<string, string> ToDictionary();

    /// <summary>
    /// Clears all arguments from the builder.
    /// </summary>
    /// <returns>The builder instance for method chaining.</returns>
    public IArgumentBuilder Clear();
}
