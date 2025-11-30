using Dadstart.Labs.MediaForge.Cmdlets;
using Dadstart.Labs.MediaForge.Models;
using Dadstart.Labs.MediaForge.Module;
using Dadstart.Labs.MediaForge.Services;
using Dadstart.Labs.MediaForge.Services.System;
using Microsoft.Extensions.Logging;
using Moq;
using System.Management.Automation;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Cmdlets;

public class GetMediaFileCommandTests
{
    [Fact]
    public void Path_Property_CanBeSet()
    {
        // Arrange
        var cmdlet = new GetMediaFileCommand();

        // Act
        cmdlet.Path = "test.mkv";

        // Assert
        Assert.Equal("test.mkv", cmdlet.Path);
    }

    [Fact]
    public void Path_Property_InitializesToEmptyString()
    {
        // Arrange & Act
        var cmdlet = new GetMediaFileCommand();

        // Assert
        Assert.Equal(string.Empty, cmdlet.Path);
    }
}
