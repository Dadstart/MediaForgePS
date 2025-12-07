using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dadstart.Labs.MediaForge.Models;

namespace Dadstart.Labs.MediaForge.Parsers;

/// <summary>
/// Interface for parsing media file information from JSON.
/// </summary>
public interface IMediaModelParser
{
    /// <summary>
    /// Parses a complete media file from JSON.
    /// </summary>
    /// <param name="path">The path to the media file.</param>
    /// <param name="raw">The raw JSON string to parse.</param>
    /// <returns>The parsed media file.</returns>
    MediaFile ParseFile(string path, string raw);

    /// <summary>
    /// Parses a media chapter from JSON.
    /// </summary>
    /// <param name="raw">The raw JSON string to parse.</param>
    /// <returns>The parsed media chapter.</returns>
    MediaChapter ParseChapter(string raw);

    /// <summary>
    /// Parses a media format from JSON.
    /// </summary>
    /// <param name="raw">The raw JSON string to parse.</param>
    /// <returns>The parsed media format.</returns>
    MediaFormat ParseFormat(string raw);

    /// <summary>
    /// Parses a media stream from JSON.
    /// </summary>
    /// <param name="raw">The raw JSON string to parse.</param>
    /// <returns>The parsed media stream.</returns>
    MediaStream ParseStream(string raw);
}
