using System.Text.Json;
// + ekle:
using System.Linq;

public sealed class FlowRunner
{
    private readonly INodeRegistry _registry;
    public FlowRunner(INodeRegistry registry) => _registry = registry;

public async Task<FlowRunResult> ExecuteOnceAsync(FlowSnapshot snap, object? payload = null, CancellationToken ct = default)
{
    var logger = new ConsoleFlowLogger();
    var ctx = new FlowContext { Cancellation = ct, Logger = logger };

    ctx.Inputs = new Dictionary<string, Dictionary<string, object?>>();
    OutputStore.Clear();

    // 1) Seed/payload enjeksiyonu
    if (payload is FlowInputs seed && seed.Values?.Count > 0)
    {
        foreach (var kv in seed.Values)
        {
            var (nodeId, handle) = kv.Key;
            var value = kv.Value;

            if (!ctx.Outputs.TryGetValue(nodeId, out var outMap))
                ctx.Outputs[nodeId] = outMap = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            outMap[handle] = value;

            if (!ctx.Inputs.TryGetValue(nodeId, out var inMap))
                ctx.Inputs[nodeId] = inMap = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            inMap[handle] = value;
        }
    }
    else if (payload != null)
    {
        var firstTrigger = snap.Nodes.FirstOrDefault(n =>
            n.Type.Equals("httpListen", StringComparison.OrdinalIgnoreCase) ||
            n.Type.Equals("manualTrigger", StringComparison.OrdinalIgnoreCase) ||
            n.Type.EndsWith("Trigger", StringComparison.OrdinalIgnoreCase)
        );
        if (firstTrigger != null)
        {
            var reqObj = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["body"] = payload,
                ["method"] = "EXECUTE",
                ["path"] = "/api/execute"
            };
            ctx.Outputs[firstTrigger.Id] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["request"] = reqObj,
                ["context"] = payload
            };
            ctx.Inputs[firstTrigger.Id] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["request"] = reqObj,
                ["context"] = payload
            };
        }
    }

    var inEdges = snap.Edges.GroupBy(e => e.Target).ToDictionary(g => g.Key, g => g.ToList());
    var outEdges = snap.Edges.GroupBy(e => e.Source).ToDictionary(g => g.Key, g => g.ToList());

    var pending = new Queue<FlowNode>(snap.Nodes);
    var executed = new HashSet<string>();
    int safety = 0;

    while (pending.Count > 0 && safety++ < 10000)
    {
        var node = pending.Dequeue();
        if (executed.Contains(node.Id)) continue;

        var inputsMap = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        // Edge'lerden gelenler
        if (inEdges.TryGetValue(node.Id, out var ins))
        {
            var allAvailable = true;
            foreach (var edge in ins)
            {
                // Kaynak handle belirleme
                var srcOut =
                    !string.IsNullOrWhiteSpace(edge.SourceHandle) ? edge.SourceHandle! :
                    (ctx.Outputs.TryGetValue(edge.Source, out var preOut) && preOut.Count == 1) ? preOut.Keys.First() :
                    GuessSourceHandle(snap, edge.Source);

                // Hedef handle belirleme
                var targetKey =
                    !string.IsNullOrWhiteSpace(edge.TargetHandle) ? edge.TargetHandle! :
                    GuessTargetHandle(snap, node.Id);

                if (!ctx.Outputs.TryGetValue(edge.Source, out var outDict) || !outDict.TryGetValue(srcOut, out var value))
                {
                    allAvailable = false;
                    break;
                }
                inputsMap[targetKey] = value;
            }
            if (!allAvailable) { pending.Enqueue(node); continue; }
        }

        // Trigger gibi inputs'u olmayan nodelar için: önceden seed edilen kendi output'unu input olarak geçir
        if (inputsMap.Count == 0 && ctx.Outputs.TryGetValue(node.Id, out var selfSeed))
        {
            foreach (var kv in selfSeed)
                inputsMap[kv.Key] = kv.Value;
        }

        // Bu node'un inputlarını kaydet
        ctx.Inputs[node.Id] = new Dictionary<string, object?>(inputsMap);

        // Node'u çalıştır
        var proc = _registry.Resolve(node.Type);
        NodeResult result;
        try
        {
            result = await proc.ExecuteAsync(new NodeExecutionArgs
            {
                Node = node,
                Context = ctx,
                Inputs = inputsMap
            });
        }
        catch (Exception ex)
        {
            ctx.Logger.Error($"Node {node.Label} ({node.Type}) failed", ex);
            return new FlowRunResult { Context = ctx, Success = false };
        }

        // Mappings (varsa) uygula
        if (node.Mappings?.Count > 0)
        {
            var jmes = new Jmes();
            var hbs = new Hbs();

            foreach (var map in node.Mappings)
            {
                object? value = null;
                if (string.Equals(map.Language, "jmespath", StringComparison.OrdinalIgnoreCase))
                    value = jmes.Eval(new { inputs = inputsMap, outputs = result.Outputs }, map.Expression);
                else
                    value = hbs.Render(map.Expression, new { inputs = inputsMap, outputs = result.Outputs });

                var root = result.Outputs.TryGetValue("response", out var r)
                    ? r
                    : result.Outputs.TryGetValue("result", out var rr) ? rr
                    : new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

                var updated = JsonUtil.SetByPath(root, map.TargetPath, value);
                if (result.Outputs.ContainsKey("response")) result.Outputs["response"] = updated;
                else result.Outputs["result"] = updated;
            }
        }

        ctx.Outputs[node.Id] = result.Outputs;
        OutputStore.Set(node.Id, result.Outputs);

        executed.Add(node.Id);

        if (outEdges.TryGetValue(node.Id, out var outs))
        {
            foreach (var e in outs)
            {
                var tgt = snap.Nodes.FirstOrDefault(n => n.Id == e.Target);
                if (tgt != null) pending.Enqueue(tgt);
            }
        }
    }

    var success = executed.Count > 0;
    return new FlowRunResult { Context = ctx, Success = success };

    // --- yardımcılar ---
    static string GuessSourceHandle(FlowSnapshot s, string sourceId)
    {
        var sn = s.Nodes.FirstOrDefault(n => n.Id == sourceId);
        if (sn == null) return "result";
        if (sn.Type.Equals("httpListen", StringComparison.OrdinalIgnoreCase)) return "context";
        var one = sn.IO?.Outputs?.FirstOrDefault(o => !string.IsNullOrWhiteSpace(o.Name))?.Name;
        return string.IsNullOrWhiteSpace(one) ? "result" : one!;
    }
    static string GuessTargetHandle(FlowSnapshot s, string targetId)
    {
        var tn = s.Nodes.FirstOrDefault(n => n.Id == targetId);
        if (tn?.IO?.Inputs?.Any(i => i.Name == "context") == true) return "context";
        var one = tn?.IO?.Inputs?.FirstOrDefault(i => !string.IsNullOrWhiteSpace(i.Name))?.Name;
        return string.IsNullOrWhiteSpace(one) ? "context" : one!;
    }
}

}