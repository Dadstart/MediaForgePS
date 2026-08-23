using Dadstart.Labs.MediaForge.Services.System;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Services.System;

public class ProcessTimeoutsTests
{
    [Fact]
    public void ProcessTimeouts_ArePositiveAndOrdered()
    {
        Assert.True(ProcessTimeouts.Probe > TimeSpan.Zero);
        Assert.True(ProcessTimeouts.Extract > ProcessTimeouts.Probe);
        Assert.True(ProcessTimeouts.Encode > ProcessTimeouts.Extract);
    }
}
