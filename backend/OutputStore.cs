using System.Collections.Concurrent;

public static class OutputStore
{
    // Son çalıştırmadaki tüm node çıktıları
    // outputs[nodeId] = { handle => value }
    private static readonly ConcurrentDictionary<string, Dictionary<string, object?>> _outputs
        = new(StringComparer.OrdinalIgnoreCase);

    public static void Clear() => _outputs.Clear();

    public static void Set(string nodeId, Dictionary<string, object?> outputs)
        => _outputs[nodeId] = outputs;

    public static bool TryGet(string nodeId, out Dictionary<string, object?> outputs)
        => _outputs.TryGetValue(nodeId, out outputs!);

    public static bool TryGet(string nodeId, string handle, out object? value)
    {
        value = null;
        return _outputs.TryGetValue(nodeId, out var dict) && dict.TryGetValue(handle, out value);
    }

    public static IReadOnlyDictionary<string, Dictionary<string, object?>> GetAll() => _outputs;
}
