using Dadstart.Labs.MediaForge.Models;

namespace Dadstart.Labs.MediaForge.Services.Ffmpeg;

public interface IFfprobeService
{
    Task<FfprobeResult> Execute(string path, IEnumerable<string> arguments);
}
