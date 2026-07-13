namespace Dadstart.Labs.MediaForge.Services;

/// <summary>
/// <see cref="IProgress{T}"/> implementation that invokes the handler synchronously on the reporting thread.
/// Avoids <see cref="Progress{T}"/> SynchronizationContext marshaling, which can deadlock PowerShell pipelines.
/// </summary>
internal sealed class SynchronousProgress<T>(Action<T> handler) : IProgress<T>
{
    private readonly Action<T> _handler = handler ?? throw new ArgumentNullException(nameof(handler));

    public void Report(T value) => _handler(value);
}
