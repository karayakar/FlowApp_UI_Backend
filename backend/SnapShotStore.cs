
public static class SnapshotStore
{
    private static FlowSnapshot? _currentSnapshot;
    private static readonly object _snapshotLock = new();

    public static FlowSnapshot? Get()
    {
        lock (_snapshotLock)
        {
            return _currentSnapshot;
        }
    }

    public static void Set(FlowSnapshot snap)
    {
        lock (_snapshotLock)
        {
            _currentSnapshot = snap;
        }
    }
}
