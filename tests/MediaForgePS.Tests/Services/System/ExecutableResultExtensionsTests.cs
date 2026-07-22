using System;
using System.IO;
using Dadstart.Labs.MediaForge.Services.System;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Services.System;

public class ExecutableResultExtensionsTests
{
    [Fact]
    public void ThrowIfInfrastructureFailure_WhenExceptionSet_ThrowsWithInner()
    {
        var inner = new InvalidOperationException("boom");
        var result = new ExecutableResult(null, null, null, inner);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            result.ThrowIfInfrastructureFailure("ffmpeg"));

        Assert.Same(inner, ex.InnerException);
        Assert.Contains("ffmpeg failed", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ThrowIfInfrastructureFailure_WhenNoException_DoesNotThrow()
    {
        var result = new ExecutableResult("out", null, 0);

        result.ThrowIfInfrastructureFailure("ffmpeg");
    }

    [Fact]
    public void EnsureProcessSuccess_WhenExitCodeZero_DoesNotThrow()
    {
        var result = new ExecutableResult("out", null, 0);

        result.EnsureProcessSuccess("ffmpeg");
    }

    [Fact]
    public void EnsureProcessSuccess_WhenExitCodeNonZero_IncludesStderr()
    {
        var result = new ExecutableResult(null, "bad args", 2);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            result.EnsureProcessSuccess("ffmpeg"));

        Assert.Contains("exit code 2", ex.Message, StringComparison.Ordinal);
        Assert.Contains("bad args", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EnsureProcessSuccess_WhenExceptionPresent_PrefersInfrastructureFailure()
    {
        var inner = new IOException("pipe broken");
        var result = new ExecutableResult(null, "ignored", 1, inner);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            result.EnsureProcessSuccess("OCR"));

        Assert.Same(inner, ex.InnerException);
        Assert.DoesNotContain("exit code", ex.Message, StringComparison.Ordinal);
    }
}
