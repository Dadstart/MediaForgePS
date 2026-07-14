using System.Collections.ObjectModel;
using System.IO;
using System.Management.Automation;
using System.Management.Automation.Provider;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Services;

namespace Dadstart.Labs.MediaForge.Providers;

/// <summary>
/// PowerShell provider for browsing filesystem media files and their internal format, chapters, and streams.
/// </summary>
/// <remarks>
/// Mount a folder or media file as a drive:
/// <code>
/// New-PSDrive -Name clips -PSProvider Media -Root 'C:\Videos'
/// Get-ChildItem clips:
/// Get-ChildItem clips:\movie.mkv\streams\audio
/// </code>
/// Stream indices under a type folder (e.g. audio\0) are type-relative, matching Export-MediaStream.
/// Indices under streams\all are absolute ffprobe stream indices.
/// </remarks>
[CmdletProvider("Media", ProviderCapabilities.None)]
public sealed class MediaCmdletProvider : NavigationCmdletProvider
{
    private static readonly string[] _virtualMediaChildren =
    [
        MediaPathParser.FormatNode,
        MediaPathParser.ChaptersNode,
        MediaPathParser.StreamsNode,
    ];

    private static readonly string[] _streamTypeChildren =
    [
        "video", "audio", "subtitle", "data", "attachment", MediaPathParser.AllStreamsNode,
    ];

    /// <inheritdoc />
    protected override Collection<PSDriveInfo> InitializeDefaultDrives() => [];

    /// <inheritdoc />
    protected override PSDriveInfo NewDrive(PSDriveInfo drive)
    {
        ArgumentNullException.ThrowIfNull(drive);

        if (string.IsNullOrWhiteSpace(drive.Root))
            throw new ArgumentException("Media drive Root must be a file or directory path.", nameof(drive));

        var root = Path.GetFullPath(drive.Root);
        if (!File.Exists(root) && !Directory.Exists(root))
            throw new ArgumentException($"Media drive Root does not exist: {root}", nameof(drive));

        if (File.Exists(root) && !MediaPathParser.IsMediaFilePath(root))
            throw new ArgumentException($"Media drive Root file must be a known media type: {root}", nameof(drive));

        return new MediaDriveInfo(new PSDriveInfo(drive.Name, drive.Provider, root, drive.Description, drive.Credential));
    }

    /// <inheritdoc />
    protected override PSDriveInfo RemoveDrive(PSDriveInfo drive)
    {
        if (drive is MediaDriveInfo mediaDrive)
            mediaDrive.ClearCache();

        return drive;
    }

    /// <inheritdoc />
    protected override bool IsValidPath(string path) => path is not null;

    /// <inheritdoc />
    protected override bool ItemExists(string path)
    {
        var info = TryResolve(path);
        if (info is null)
            return false;

        return info.Kind switch
        {
            MediaPathKind.FileSystemDirectory => Directory.Exists(info.PhysicalPath),
            MediaPathKind.FileSystemFile or MediaPathKind.MediaFile => File.Exists(info.PhysicalPath),
            MediaPathKind.Format or MediaPathKind.Chapters or MediaPathKind.Streams
                or MediaPathKind.StreamType => File.Exists(info.PhysicalPath),
            MediaPathKind.Chapter => TryGetChapter(info, out _),
            MediaPathKind.Stream => TryGetStream(info, out _),
            _ => false,
        };
    }

    /// <inheritdoc />
    protected override bool IsItemContainer(string path)
    {
        var info = TryResolve(path);
        if (info is null)
            return false;

        return info.Kind is MediaPathKind.FileSystemDirectory
            or MediaPathKind.MediaFile
            or MediaPathKind.Chapters
            or MediaPathKind.Streams
            or MediaPathKind.StreamType;
    }

    /// <inheritdoc />
    protected override void GetChildItems(string path, bool recurse)
    {
        var info = TryResolve(path);
        if (info is null)
        {
            WriteError(new ErrorRecord(
                new ItemNotFoundException($"Cannot find path '{path}' because it does not exist."),
                "PathNotFound",
                ErrorCategory.ObjectNotFound,
                path));
            return;
        }

        WriteChildren(info, recurse);
    }

    /// <inheritdoc />
    protected override void GetChildNames(string path, ReturnContainers returnContainers)
    {
        var info = TryResolve(path);
        if (info is null)
            return;

        foreach (var (name, childPath, isContainer) in EnumerateChildDescriptors(info))
        {
            if (returnContainers == ReturnContainers.ReturnAllContainers && !isContainer)
                continue;

            WriteItemObject(name, childPath, isContainer);
        }
    }

    /// <inheritdoc />
    protected override void GetItem(string path)
    {
        var info = TryResolve(path);
        if (info is null)
        {
            WriteError(new ErrorRecord(
                new ItemNotFoundException($"Cannot find path '{path}' because it does not exist."),
                "PathNotFound",
                ErrorCategory.ObjectNotFound,
                path));
            return;
        }

        if (!TryWriteItem(info))
        {
            WriteError(new ErrorRecord(
                new ItemNotFoundException($"Cannot find path '{path}' because it does not exist."),
                "PathNotFound",
                ErrorCategory.ObjectNotFound,
                path));
        }
    }

    /// <inheritdoc />
    protected override string GetChildName(string path)
    {
        if (string.IsNullOrEmpty(path))
            return string.Empty;

        // Absolute filesystem paths must use OS APIs so Unix leading '/' is preserved.
        var osPath = ToOsPath(path);
        if (Path.IsPathRooted(osPath))
            return Path.GetFileName(Path.TrimEndingDirectorySeparator(osPath));

        var normalized = MediaPathParser.NormalizeProviderPath(path);
        if (normalized.Length == 0)
            return string.Empty;

        var segments = MediaPathParser.SplitSegments(normalized);
        return segments[^1];
    }

    /// <inheritdoc />
    protected override string GetParentPath(string path, string root)
    {
        if (string.IsNullOrEmpty(path))
            return string.Empty;

        var osPath = ToOsPath(path);
        if (IsDriveRootPath(osPath, root) || IsDriveRootPath(osPath, PSDriveInfo?.Root))
            return string.Empty;

        // Absolute filesystem paths must use OS APIs so Unix leading '/' is preserved.
        // NormalizeProviderPath Trim('/') otherwise turns "/tmp/a" into "tmp/a" and breaks
        // subsequent MakePath / .. resolution on non-Windows hosts.
        if (Path.IsPathRooted(osPath))
            return Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(osPath)) ?? string.Empty;

        var normalized = MediaPathParser.NormalizeProviderPath(path);
        if (normalized.Length == 0)
            return string.Empty;

        var segments = MediaPathParser.SplitSegments(normalized);
        if (segments.Length <= 1)
            return string.Empty;

        return MediaPathParser.JoinSegments(segments[..^1]);
    }

    /// <inheritdoc />
    protected override string MakePath(string parent, string child)
    {
        if (string.IsNullOrEmpty(child))
            return parent ?? string.Empty;

        if (string.IsNullOrEmpty(parent))
        {
            var rootedChild = ToOsPath(child);
            if (Path.IsPathRooted(rootedChild))
                return rootedChild;

            return MediaPathParser.NormalizeProviderPath(child);
        }

        var osParent = ToOsPath(parent);
        var osChild = ToOsPath(child);
        if (Path.IsPathRooted(osParent) || Path.IsPathRooted(osChild))
            return Path.Combine(osParent, osChild.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        var parentNormalized = MediaPathParser.NormalizeProviderPath(parent);
        var childNormalized = MediaPathParser.NormalizeProviderPath(child);
        if (parentNormalized.Length == 0)
            return childNormalized;
        if (childNormalized.Length == 0)
            return parentNormalized;

        return MediaPathParser.JoinSegments(parentNormalized, childNormalized);
    }

    /// <inheritdoc />
    protected override string NormalizeRelativePath(string path, string basePath)
    {
        if (string.IsNullOrEmpty(path))
            return string.Empty;

        var osPath = ToOsPath(path);
        var osBase = ToOsPath(basePath);
        if (Path.IsPathRooted(osPath) && !string.IsNullOrEmpty(osBase) && Path.IsPathRooted(osBase))
        {
            string fullPath;
            string fullBase;
            try
            {
                fullPath = Path.GetFullPath(osPath);
                fullBase = Path.TrimEndingDirectorySeparator(Path.GetFullPath(osBase));
            }
            catch (Exception)
            {
                return MediaPathParser.NormalizeProviderPath(path);
            }

            if (string.Equals(fullPath, fullBase, StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            var prefix = fullBase + Path.DirectorySeparatorChar;
            if (fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return MediaPathParser.NormalizeProviderPath(fullPath[prefix.Length..]);
        }

        var normalized = MediaPathParser.NormalizeProviderPath(path);
        var baseNormalized = MediaPathParser.NormalizeProviderPath(basePath);
        if (baseNormalized.Length == 0)
            return normalized;

        if (normalized.Equals(baseNormalized, StringComparison.OrdinalIgnoreCase))
            return string.Empty;

        var relativePrefix = baseNormalized + "/";
        if (normalized.StartsWith(relativePrefix, StringComparison.OrdinalIgnoreCase))
            return normalized[relativePrefix.Length..];

        return normalized;
    }

    private void WriteChildren(MediaPathInfo info, bool recurse)
    {
        foreach (var (name, childPath, isContainer) in EnumerateChildDescriptors(info))
        {
            var childInfo = TryResolve(childPath);
            if (childInfo is null)
                continue;

            if (!TryWriteItem(childInfo))
                continue;

            if (recurse && isContainer)
                WriteChildren(childInfo, recurse: true);
        }
    }

    private IEnumerable<(string Name, string ChildPath, bool IsContainer)> EnumerateChildDescriptors(MediaPathInfo info)
    {
        switch (info.Kind)
        {
            case MediaPathKind.FileSystemDirectory:
                foreach (var directory in Directory.EnumerateDirectories(info.PhysicalPath))
                {
                    var name = Path.GetFileName(directory);
                    yield return (name, MakeChildProviderPath(info.ProviderPath, name), true);
                }

                foreach (var file in Directory.EnumerateFiles(info.PhysicalPath))
                {
                    var name = Path.GetFileName(file);
                    var isMedia = MediaPathParser.IsMediaFilePath(file);
                    yield return (name, MakeChildProviderPath(info.ProviderPath, name), isMedia);
                }

                yield break;

            case MediaPathKind.MediaFile:
                foreach (var name in _virtualMediaChildren)
                    yield return (name, MakeChildProviderPath(info.ProviderPath, name), name != MediaPathParser.FormatNode);
                yield break;

            case MediaPathKind.Chapters:
                {
                    if (!TryGetMediaFile(info.PhysicalPath, out var mediaFile) || mediaFile is null)
                        yield break;

                    for (var i = 0; i < mediaFile.Chapters.Length; i++)
                    {
                        var name = i.ToString();
                        yield return (name, MakeChildProviderPath(info.ProviderPath, name), false);
                    }

                    yield break;
                }

            case MediaPathKind.Streams:
                foreach (var name in _streamTypeChildren)
                    yield return (name, MakeChildProviderPath(info.ProviderPath, name), true);
                yield break;

            case MediaPathKind.StreamType:
                {
                    if (!TryGetMediaFile(info.PhysicalPath, out var mediaFile) || mediaFile is null)
                        yield break;

                    var count = CountStreams(mediaFile, info.StreamType!);
                    for (var i = 0; i < count; i++)
                    {
                        var name = i.ToString();
                        yield return (name, MakeChildProviderPath(info.ProviderPath, name), false);
                    }

                    yield break;
                }

            default:
                yield break;
        }
    }

    private bool TryWriteItem(MediaPathInfo info)
    {
        switch (info.Kind)
        {
            case MediaPathKind.FileSystemDirectory:
                WriteItemObject(new DirectoryInfo(info.PhysicalPath), info.ProviderPath, isContainer: true);
                return true;

            case MediaPathKind.FileSystemFile:
                WriteItemObject(new FileInfo(info.PhysicalPath), info.ProviderPath, isContainer: false);
                return true;

            case MediaPathKind.MediaFile:
                if (!TryGetMediaFile(info.PhysicalPath, out var mediaFile) || mediaFile is null)
                    return false;
                WriteItemObject(mediaFile, info.ProviderPath, isContainer: true);
                return true;

            case MediaPathKind.Format:
                if (!TryGetMediaFile(info.PhysicalPath, out mediaFile) || mediaFile is null)
                    return false;
                WriteItemObject(mediaFile.Format, info.ProviderPath, isContainer: false);
                return true;

            case MediaPathKind.Chapters:
                WriteItemObject(new MediaContainerItem("chapters", info.PhysicalPath), info.ProviderPath, isContainer: true);
                return true;

            case MediaPathKind.Chapter:
                if (!TryGetChapter(info, out var chapter) || chapter is null)
                    return false;
                WriteItemObject(chapter, info.ProviderPath, isContainer: false);
                return true;

            case MediaPathKind.Streams:
                WriteItemObject(new MediaContainerItem("streams", info.PhysicalPath), info.ProviderPath, isContainer: true);
                return true;

            case MediaPathKind.StreamType:
                WriteItemObject(new MediaContainerItem(info.StreamType!, info.PhysicalPath), info.ProviderPath, isContainer: true);
                return true;

            case MediaPathKind.Stream:
                if (!TryGetStream(info, out var stream) || stream is null)
                    return false;
                WriteItemObject(stream, info.ProviderPath, isContainer: false);
                return true;

            default:
                return false;
        }
    }

    private bool TryGetChapter(MediaPathInfo info, out MediaChapter? chapter)
    {
        chapter = null;
        if (!info.Index.HasValue)
            return false;
        if (!TryGetMediaFile(info.PhysicalPath, out var mediaFile) || mediaFile is null)
            return false;
        if (info.Index.Value >= mediaFile.Chapters.Length)
            return false;

        chapter = mediaFile.Chapters[info.Index.Value];
        return true;
    }

    private bool TryGetStream(MediaPathInfo info, out MediaStream? stream)
    {
        stream = null;
        if (!info.Index.HasValue || string.IsNullOrEmpty(info.StreamType))
            return false;
        if (!TryGetMediaFile(info.PhysicalPath, out var mediaFile) || mediaFile is null)
            return false;

        if (info.StreamType.Equals(MediaPathParser.AllStreamsNode, StringComparison.OrdinalIgnoreCase))
        {
            stream = mediaFile.Streams.FirstOrDefault(s => s.Index == info.Index.Value);
            return stream is not null;
        }

        var typed = mediaFile.Streams
            .Where(s => s.Type.Equals(info.StreamType, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (info.Index.Value >= typed.Length)
            return false;

        stream = typed[info.Index.Value];
        return true;
    }

    private static int CountStreams(MediaFile mediaFile, string streamType)
    {
        if (streamType.Equals(MediaPathParser.AllStreamsNode, StringComparison.OrdinalIgnoreCase))
            return mediaFile.Streams.Length;

        return mediaFile.Streams.Count(s => s.Type.Equals(streamType, StringComparison.OrdinalIgnoreCase));
    }

    private bool TryGetMediaFile(string physicalPath, out MediaFile? mediaFile)
    {
        mediaFile = null;
        var drive = PSDriveInfo as MediaDriveInfo;
        if (drive is not null && drive.TryGetCachedMediaFile(physicalPath, out mediaFile) && mediaFile is not null)
            return true;

        try
        {
            var reader = ModuleServices.GetRequiredService<IMediaReaderService>();
            mediaFile = reader.GetMediaFileAsync(physicalPath).ConfigureAwait(false).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            WriteError(new ErrorRecord(
                ex,
                "MediaFileReadFailed",
                ErrorCategory.ReadError,
                physicalPath));
            return false;
        }

        if (mediaFile is null)
        {
            WriteError(new ErrorRecord(
                new InvalidOperationException($"Failed to read media file: {physicalPath}"),
                "MediaFileReadFailed",
                ErrorCategory.ReadError,
                physicalPath));
            return false;
        }

        drive?.SetCachedMediaFile(physicalPath, mediaFile);
        return true;
    }

    private MediaPathInfo? TryResolve(string path)
    {
        var root = PSDriveInfo?.Root;
        if (string.IsNullOrWhiteSpace(root))
            return null;

        return MediaPathParser.TryParse(
            root,
            GetProviderRelativePath(path),
            File.Exists,
            Directory.Exists);
    }

    private string GetProviderRelativePath(string? path)
    {
        var root = PSDriveInfo?.Root;
        if (string.IsNullOrWhiteSpace(root))
            return MediaPathParser.NormalizeProviderPath(path);

        return MediaPathParser.ToProviderRelativePath(root, path, PSDriveInfo?.Name);
    }

    private static bool IsDriveRootPath(string path, string? root)
    {
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrEmpty(path))
            return false;

        try
        {
            var fullPath = Path.GetFullPath(ToOsPath(path));
            var fullRoot = Path.GetFullPath(ToOsPath(root));
            return string.Equals(fullPath, fullRoot, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return string.Equals(
                Path.TrimEndingDirectorySeparator(ToOsPath(path)),
                Path.TrimEndingDirectorySeparator(ToOsPath(root)),
                StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string ToOsPath(string path) =>
        path.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);

    private static string MakeChildProviderPath(string parentProviderPath, string childName)
    {
        if (string.IsNullOrEmpty(parentProviderPath))
            return childName;

        return MediaPathParser.JoinSegments(parentProviderPath, childName);
    }
}
