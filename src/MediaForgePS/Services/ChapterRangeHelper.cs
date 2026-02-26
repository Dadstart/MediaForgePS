using System;
using System.Collections.Generic;
using System.Management.Automation;
using Dadstart.Labs.MediaForge.Models;

namespace Dadstart.Labs.MediaForge.Services;

/// <summary>
/// Helper methods for chapter range normalization from PowerShell inputs.
/// </summary>
public static class ChapterRangeHelper
{
    /// <summary>
    /// Normalizes chapter range inputs to a typed list.
    /// </summary>
    /// <param name="chapterRanges">Range inputs from cmdlet parameter binding.</param>
    /// <returns>Normalized ranges with start, end, and optional output name.</returns>
    public static List<(int Start, int End, string? OutputName)> NormalizeChapterRanges(object[] chapterRanges)
    {
        var list = new List<(int, int, string?)>();
        for (var i = 0; i < chapterRanges.Length; i++)
        {
            var item = chapterRanges[i];
            if (item is ChapterRange chapterRange)
            {
                list.Add((chapterRange.Start, chapterRange.End, chapterRange.OutputName));
                continue;
            }

            var psObj = item as PSObject;
            var startProp = psObj?.Properties["Start"]?.Value;
            var endProp = psObj?.Properties["End"]?.Value;
            var outputNameProp = psObj?.Properties["OutputName"]?.Value;

            if (startProp == null || endProp == null)
                throw new ArgumentException($"Chapter range at index {i} is missing Start or End property.");

            if (!LanguagePrimitives.TryConvertTo(startProp, out int start) ||
                !LanguagePrimitives.TryConvertTo(endProp, out int end))
            {
                throw new ArgumentException($"Chapter range at index {i}: Start and End must be integers.");
            }

            list.Add((start, end, outputNameProp?.ToString()));
        }

        return list;
    }
}
