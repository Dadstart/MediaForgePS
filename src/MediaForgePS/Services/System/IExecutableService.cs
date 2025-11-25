namespace Dadstart.Labs.MediaForge.Services.System;

public interface IExecutableService
{
    string Execute(string command, string arguments);
}