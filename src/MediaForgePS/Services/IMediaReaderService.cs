using Dadstart.Labs.MediaForge.Models;

namespace Dadstart.Labs.MediaForge.Services;

public interface IMediaReaderService
{
    MediaFile GetMediaFile(string path);
}