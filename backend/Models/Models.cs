using System.Text.Json;



public enum MappingLanguage { Handlebars, JmesPath, Jq }

public sealed class EdgeMappingRule
{
    public string TargetPath { get; set; } = "";
    public string Expression { get; set; } = "";
    public MappingLanguage Language { get; set; } = MappingLanguage.JmesPath;
}

public sealed class EdgeMapping
{
    public object? SourceSample { get; set; }
    public object? TargetTemplate { get; set; }
    public List<EdgeMappingRule> Rules { get; set; } = new();
}
//public sealed class FlowSnapshot
//{
//    public int Version { get; set; } = 1;
//    public List<FlowNode> Nodes { get; set; } = new();
//    public List<FlowEdge> Edges { get; set; } = new();
//}

public sealed class FlowNode
{
    public HttpContext _context { get; set; }
    public string Id { get; set; } = default!;

    public string icon { get; set; }
    public string Type { get; set; } = default!;
    public string Label { get; set; } = "";
    public NodeIO IO { get; set; } = new();
    public position position { get; set; }

    public measured measured { get; set; }
    public Dictionary<string, object?> Settings { get; set; } = new();
    public List<NodeMapping> Mappings { get; set; } = new();
}
public sealed class position
{
    public float x { get; set; }
    public float y { get; set; }
}

public sealed class measured
{
    public int height { get; set; }
    public int width { get; set; }
}

public sealed class NodeIO
{
    public List<IOPort> Inputs { get; set; } = new();
    public List<IOPort> Outputs { get; set; } = new();
}
public sealed class IOPort { public string Name { get; set; } = ""; public string Type { get; set; } = "any"; }

public sealed class NodeMapping
{
    public string TargetPath { get; set; } = "";
    public string Expression { get; set; } = "";
    public string Language { get; set; } = "handlebars"; // handlebars|jmespath
}

public sealed class FlowEdge
{
    public string Id { get; set; } = default!;
    public string Source { get; set; } = default!;
    public string Target { get; set; } = default!;
    public string? SourceHandle { get; set; }
    public string? TargetHandle { get; set; }
    public string? Label { get; set; }
}

public interface IFlowLogger
{
    void Info(string msg);
    void Warn(string msg);
    void Error(string msg, Exception? ex = null);
}

public sealed class ConsoleFlowLogger : IFlowLogger
{
    public void Info(string msg) => Console.WriteLine($"[INFO] {DateTime.Now:HH:mm:ss} {msg}");
    public void Warn(string msg) => Console.WriteLine($"[WARN] {DateTime.Now:HH:mm:ss} {msg}");
    public void Error(string msg, Exception? ex = null)
    {
        Console.WriteLine($"[ERROR] {DateTime.Now:HH:mm:ss} {msg}");
        if (ex != null) Console.WriteLine(ex);
    }
}

public sealed class FlowContext
{
    public HttpContext _context { get; set; }
    public required CancellationToken? Cancellation { get; set; }
    public required IFlowLogger Logger { get; set; }
    public IDictionary<string, object?> Globals { get; set; } = new Dictionary<string, object?>();
    // outputs[nodeId][handleName] = value
    public Dictionary<string, Dictionary<string, object?>> Outputs { get; set; } = new();
    public Dictionary<string, Dictionary<string, object?>> Inputs { get; set; } = new();
}

public sealed class NodeExecutionArgs
{
    public required FlowNode Node { get; init; }
    public required FlowContext Context { get; init; }
    public required IReadOnlyDictionary<string, object?> Inputs { get; init; }
}

public sealed class NodeResult
{
    public Dictionary<string, object?> Outputs { get; init; } = new();
}

public interface INodeProcessor
{
    string Type { get; }
    Task<NodeResult> ExecuteAsync(NodeExecutionArgs args);
}

public interface INodeRegistry
{
    INodeProcessor Resolve(string nodeType);
}

public sealed class FlowInputs
{
    public HttpContext _context { get; set; }
    public Dictionary<(string NodeId, string Handle), object?> Values { get; set; } = new();
}

public sealed class FlowRunResult
{
    public required FlowContext Context { get; init; }
    public bool Success { get; init; }
    public Dictionary<string, Dictionary<string, object?>> AllOutputs => Context.Outputs;
}
