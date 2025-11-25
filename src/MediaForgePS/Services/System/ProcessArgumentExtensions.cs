using System.Text;

namespace Dadstart.Labs.MediaForge.Services.System;

/// <summary>
/// Extension methods for process argument handling.
/// </summary>
public static class ProcessArgumentExtensions
{
    /// <summary>
    /// Converts a collection of arguments to a properly quoted command-line argument string.
    /// </summary>
    /// <param name="arguments">The arguments to quote.</param>
    /// <param name="platformService">The platform service to use.</param>
    /// <returns>A properly quoted argument string suitable for ProcessStartInfo.Arguments.</returns>
    public static string ToQuotedArgumentString(this IEnumerable<string> arguments, IPlatformService platformService)
    {
        return string.Join(" ", arguments.Select(arg => QuoteArgument(arg, platformService)));
    }

    /// <summary>
    /// Quotes a single argument according to platform-specific rules.
    /// </summary>
    /// <param name="argument">The argument to quote.</param>
    /// <param name="platformService">The platform service to use.</param>
    /// <returns>A properly quoted argument string suitable for ProcessStartInfo.Arguments.</returns>
    public static string QuoteArgument(string argument, IPlatformService platformService)
    {
        return platformService.IsWindows() ? QuoteWindowsArgument(argument) : QuoteUnixArgument(argument);
    }

    /// <summary>
    /// Quotes a single argument for Windows.
    /// </summary>
    /// <param name="argument">The argument to quote.</param>
    /// <returns>A properly quoted argument string suitable for ProcessStartInfo.Arguments.</returns>
    public static string QuoteWindowsArgument(string argument)
    {
        if (string.IsNullOrEmpty(argument))
            return "\"\"";

        // Check if argument needs quoting
        bool needsQuoting = argument.Contains(' ') || argument.Contains('"') || argument.Contains('\t') || argument.Contains('\\');

        // If argument contains no spaces, quotes, tabs, or backslashes, return as-is
        if (!needsQuoting)
            return argument;

        // Build quoted argument with proper Windows escaping
        var result = new StringBuilder();
        result.Append('"');

        int backslashCount = 0;
        for (int i = 0; i < argument.Length; i++)
        {
            char c = argument[i];

            if (c == '\\')
            {
                backslashCount++;
            }
            else if (c == '"')
            {
                // Backslashes before a quote: double them, then escape the quote
                result.Append('\\', backslashCount * 2);
                result.Append("\\\"");
                backslashCount = 0;
            }
            else
            {
                // Append any accumulated backslashes, then the character
                result.Append('\\', backslashCount);
                result.Append(c);
                backslashCount = 0;
            }
        }

        // Append any trailing backslashes (doubled before closing quote)
        result.Append('\\', backslashCount * 2);
        result.Append('"');
        return result.ToString();
    }

    private static readonly char[] UnixSpecialCharacters = [' ', '\t', '\n', '\v', '\'', '"', '\\', '$', '`', '*', '?', '[', ']', '(', ')', '{', '}', '|', '&', ';', '<', '>', '!'];

    /// <summary>
    /// Quotes a single argument for Unix.
    /// </summary>
    /// <param name="argument">The argument to quote.</param>
    /// <returns>A properly quoted argument string suitable for ProcessStartInfo.Arguments.</returns>
    public static string QuoteUnixArgument(string argument)
    {
        if (string.IsNullOrEmpty(argument))
            return "''";

        // Characters that require quoting on Unix-like systems
        if (argument.IndexOfAny(UnixSpecialCharacters) == -1)
            return argument;

        // Use single quotes (simpler and safer for most cases)
        // To include a single quote, we end the quoted string, add an escaped quote, and start a new quoted string
        var result = new StringBuilder();
        result.Append('\'');

        foreach (char c in argument)
        {
            if (c == '\'')
            {
                // End current quote, add escaped quote, start new quote
                result.Append("'\\''");
            }
            else
            {
                result.Append(c);
            }
        }

        result.Append('\'');
        return result.ToString();
    }
}

