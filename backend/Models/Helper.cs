
using System.Text.Json;



public static class Helper
{
    public static object? GetByPath(this object? root, string path)
    {
        if (root == null || string.IsNullOrWhiteSpace(path))
            return null;

        var parts = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
        object? current = root;

        foreach (var part in parts)
        {
            if (current == null) return null;

            if (current is Dictionary<string, object?> dict)
            {
                dict.TryGetValue(part, out current);
                continue;
            }

            if (current is JsonElement je)
            {
                if (je.ValueKind == JsonValueKind.Object && je.TryGetProperty(part, out var child))
                {
                    current = child;
                    continue;
                }
                // Eğer hala object değilse string olarak parse etmeyi dene
                if (je.ValueKind == JsonValueKind.String)
                    return je.GetString();
                if (je.ValueKind == JsonValueKind.Number)
                {
                    if (je.TryGetInt64(out var i)) return i;
                    if (je.TryGetDouble(out var d)) return d;
                }
                if (je.ValueKind == JsonValueKind.True) return true;
                if (je.ValueKind == JsonValueKind.False) return false;
                if (je.ValueKind == JsonValueKind.Null) return null;
                return je.ToString();
            }

            // Eğer JSON string saklanmışsa Dictionary'e deserialize et
            if (current is string str && str.StartsWith("{"))
            {
                try
                {
                    var tmp = JsonSerializer.Deserialize<Dictionary<string, object?>>(str);
                    current = tmp;
                    if (tmp != null && tmp.TryGetValue(part, out var sub))
                        current = sub;
                    else
                        return null;
                    continue;
                }
                catch
                {
                    return null;
                }
            }

            return null;
        }

        // JsonElement varsa string'e/primitive'e dönüştür
        if (current is JsonElement finalJe)
        {
            if (finalJe.ValueKind == JsonValueKind.String) return finalJe.GetString();
            if (finalJe.ValueKind == JsonValueKind.Number)
            {
                if (finalJe.TryGetInt64(out var i)) return i;
                if (finalJe.TryGetDouble(out var d)) return d;
            }
            if (finalJe.ValueKind == JsonValueKind.True) return true;
            if (finalJe.ValueKind == JsonValueKind.False) return false;
            if (finalJe.ValueKind == JsonValueKind.Null) return null;
            return finalJe.ToString();
        }

        return current;
    }

    public static string? GetStringByPath(this object? root, string path, string? fallback = null)
    {
        var val = GetByPath(root, path);
        return val?.ToString() ?? fallback;
    }
    public static FlowSnapshot NormalizeIfUiSnapshot(JsonDocument doc)
    {
        // UI export’unda node.data altında gerçek bilgiler var
        // Bunu alıp engine’in beklediği modele çeviriyoruz.
        var root = doc.RootElement;

        var nodes = new List<FlowNode>();
        foreach (var n in root.GetProperty("nodes").EnumerateArray())
        {
            var id = n.GetProperty("id").GetString()!;
            var type = n.TryGetProperty("type", out var tEl) ? tEl.GetString() ?? "" : "";
            if (string.Equals(type, "flowNode", StringComparison.OrdinalIgnoreCase) && n.TryGetProperty("data", out var dataEl))
            {
                var nodeType = dataEl.GetProperty("nodeType").GetString() ?? type;
                var label = dataEl.TryGetProperty("label", out var le) ? le.GetString() ?? "" : "";
                var settings = dataEl.TryGetProperty("settings", out var se) ? JsonSerializer.Deserialize<Dictionary<string, object?>>(se.GetRawText())! : new();
                var inputs = dataEl.TryGetProperty("io", out var ioEl) && ioEl.TryGetProperty("inputs", out var inEl)
                                ? JsonSerializer.Deserialize<List<IOPort>>(inEl.GetRawText())! : new();
                var outputs = dataEl.TryGetProperty("io", out var ioEl2) && ioEl2.TryGetProperty("outputs", out var outEl)
                                ? JsonSerializer.Deserialize<List<IOPort>>(outEl.GetRawText())! : new();
                var mappings = dataEl.TryGetProperty("mappings", out var mpEl)
                                ? JsonSerializer.Deserialize<List<NodeMapping>>(mpEl.GetRawText())! : new();

                nodes.Add(new FlowNode
                {
                    Id = id,
                    Type = nodeType,
                    Label = label,
                    Settings = settings,
                    IO = new NodeIO { Inputs = inputs, Outputs = outputs },
                    Mappings = mappings
                });
            }
            else
            {
                // Zaten engine formatındaysa PAS geç
                var fallback = JsonSerializer.Deserialize<FlowNode>(n.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
                nodes.Add(fallback);
            }
        }

        var edges = root.TryGetProperty("edges", out var edgesEl)
          ? JsonSerializer.Deserialize<List<FlowEdge>>(edgesEl.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!
          : new();

        return new FlowSnapshot { Nodes = nodes, Edges = edges, Version = 1 };
    }

}

