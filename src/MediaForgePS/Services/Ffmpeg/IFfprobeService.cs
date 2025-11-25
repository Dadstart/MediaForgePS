using Dadstart.Labs.MediaForge.Models;

namespace Dadstart.Labs.MediaForge.Services.Ffmpeg;

public interface IFfprobeService
{
    string Execute(string path, string arguments);
}