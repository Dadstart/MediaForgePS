using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dadstart.Labs.MediaForge.Models;

namespace Dadstart.Labs.MediaForge.Parsers;
public interface IMediaModelParser
{
    MediaFile ParseFile(string path, string raw);
    MediaChapter ParseChapter(string raw);
    MediaFormat ParseFormat(string raw);
    MediaStream ParseStream(string raw);
}