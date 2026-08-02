using ClickerFixer.Satellite.Services;

namespace ClickerFixer.Satellite.Tests;

internal class FakeDeviceScanner : IEvDevDeviceScanner
{
    private readonly Queue<Func<IReadOnlyList<IEvDevDeviceHandle>>> _results = new();

    public int ScanCallCount { get; private set; }

    /// <summary>Queues the result (or exception) for the next call to Scan().</summary>
    public void Enqueue(Func<IReadOnlyList<IEvDevDeviceHandle>> resultFactory) => _results.Enqueue(resultFactory);

    public void EnqueueDevices(params IEvDevDeviceHandle[] devices) => Enqueue(() => devices);

    public void EnqueueFailure(Exception ex) => Enqueue(() => throw ex);

    public IReadOnlyList<IEvDevDeviceHandle> Scan()
    {
        ScanCallCount++;
        if (_results.Count == 0)
            return Array.Empty<IEvDevDeviceHandle>();
        return _results.Dequeue()();
    }
}
