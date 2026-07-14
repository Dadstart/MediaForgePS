namespace Dadstart.Labs.MediaForge.Providers;

/// <summary>
/// Classification of a path resolved by the Media PSProvider.
/// </summary>
public enum MediaPathKind
{
    /// <summary>
    /// A filesystem directory under the drive root.
    /// </summary>
    FileSystemDirectory,

    /// <summary>
    /// A filesystem file that is not treated as a media container.
    /// </summary>
    FileSystemFile,

    /// <summary>
    /// A media file that exposes virtual children (format, chapters, streams).
    /// </summary>
    MediaFile,

    /// <summary>
    /// The media file's format node.
    /// </summary>
    Format,

    /// <summary>
    /// The chapters container.
    /// </summary>
    Chapters,

    /// <summary>
    /// A single chapter by zero-based order index.
    /// </summary>
    Chapter,

    /// <summary>
    /// The streams container.
    /// </summary>
    Streams,

    /// <summary>
    /// A stream type container (video, audio, subtitle, data, attachment, or all).
    /// </summary>
    StreamType,

    /// <summary>
    /// A single stream (type-relative index, or absolute when type is all).
    /// </summary>
    Stream,
}
