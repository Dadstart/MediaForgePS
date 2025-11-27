using Dadstart.Labs.MediaForge.Models;

namespace Dadstart.Labs.MediaForge.Services;

public interface IMediaReaderService
{
    Task<MediaFile?> GetMediaFileAsync(string path);
}