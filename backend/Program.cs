// Program.cs
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Mime;
using System.Collections.Concurrent;

// DI ile sağladıkların:
// - INodeRegistry, DefaultNodeRegistry
// - FlowRunner
// - FlowSnapshot, FlowInputs, Helper
// - SnapshotStore, OutputStore

var builder = WebApplication.CreateBuilder(args);

// JSON: camelCase + null ignore + enum'lar string
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddSingleton<HttpClient>();
builder.Services.AddSingleton<INodeRegistry, DefaultNodeRegistry>();
builder.Services.AddSingleton<FlowRunner>();

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()
    );
});

var app = builder.Build();

app.UseCors();



// ---- Snapshot yükle (UI export JSON’u) ----
// Gelen UI snapshot’ını normalize eder, PipelineId atar, "Flows/" klasörüne kaydeder ve belleğe alır.
app.MapPost("/api/snapshot", async (HttpContext http) =>
{
    using var doc = await JsonDocument.ParseAsync(http.Request.Body);
    var snap = Helper.NormalizeIfUiSnapshot(doc); // mevcut yardımcıyı kullan
    if (snap is null)
        return Results.BadRequest(new { error = "Invalid snapshot" });

    if (string.IsNullOrWhiteSpace(snap.PipelineId))
        snap.PipelineId = Guid.NewGuid().ToString("N");

    if (!TriggerValidator.HasAtLeastOneTrigger(snap))
        return Results.BadRequest(new { error = "Pipeline must include at least one Trigger node (e.g., httpListen)." });

    // Dosyaya yaz + belleğe koy
    var id = FlowFileStore.Save(snap);
    SnapshotStore.Set(snap);

    return Results.Ok(new { ok = true, pipelineId = id, title = snap.Title });
});

// ---- Flow listesini getir (Flows/ klasöründen) ----
app.MapGet("/api/flows", () =>
{
    var items = FlowFileStore.List();
    return Results.Json(new StoredFlowListResponse
    {
        Ok = true,
        Items = items
    });
});

// ---- Tekil flow getir ----
//app.MapGet("/api/flows/{id}", (string id) =>
//{
//    if (!FlowFileStore.TryGet(id, out var snap) || snap is null)
//        return Results.Json(new StoredFlowGetResponse { Ok = false, Error = "Not found" }, statusCode: 404);

//    return Results.Json(new StoredFlowGetResponse { Ok = true, Snapshot = snap });
//});

// Program.cs
app.MapGet("/api/flows/{id}", (string id) =>
{
    var snap = FlowFileStore.Load(id);
    if (snap == null) return Results.NotFound(new { error = "Not found" });
    var d = JsonSerializer.Deserialize<dynamic>(snap);
    return Results.Ok(new { snapshot = snap });
});

// ---- Flow’u belleğe aktif snapshot olarak yükle ----
app.MapPost("/api/flows/{id}/load", (string id) =>
{
    if (!FlowFileStore.TryGet(id, out var snap) || snap is null)
        return Results.NotFound(new { ok = false, error = "Not found" });

    SnapshotStore.Set(snap);
    return Results.Ok(new { ok = true, id, title = snap.Title });
});

app.MapPost("/api/flows/save", async (HttpContext http) =>
{
    var BaseDir = Path.Combine(AppContext.BaseDirectory, "Flows");
    using var reader = new StreamReader(http.Request.Body);
    var body = await reader.ReadToEndAsync();
    var root = JsonDocument.Parse(body).RootElement;
    var snapEl = root.GetProperty("snapshot");

    if (!snapEl.TryGetProperty("pipelineId", out var pipelineId))
        return Results.BadRequest(new { error = "Missing name or snapshot" });

    var json = snapEl.GetRawText();
    var id = pipelineId;// Guid.NewGuid().ToString("N");
    var file = Path.Combine(BaseDir, $"{id}.json");
    File.WriteAllText(file, json); // Gelen JSON'u aynen kaydet!
    var snap = Helper.NormalizeIfUiSnapshot(JsonDocument.Parse(snapEl.GetRawText()));
    if (snap is null)
        return Results.BadRequest(new { error = "Invalid snapshot" });

    FlowFileStore.Save(snap);
    //SnapshotStore.Set(snap);
    return Results.Ok(new { ok = true, pipelineId = id });
});

app.MapPost("/api/flows/save_old", async (HttpContext http) =>
{
    using var doc = await JsonDocument.ParseAsync(http.Request.Body);
    var root = doc.RootElement;

    if (!root.TryGetProperty("name", out var nameEl) || !root.TryGetProperty("snapshot", out var snapEl))
        return Results.BadRequest(new { error = "Missing name or snapshot" });

    var snap = Helper.NormalizeIfUiSnapshot(JsonDocument.Parse(snapEl.GetRawText()));
    if (snap is null)
        return Results.BadRequest(new { error = "Invalid snapshot" });

    snap.Title = nameEl.GetString() ?? snap.Title;
    if (string.IsNullOrWhiteSpace(snap.PipelineId))
        snap.PipelineId = Guid.NewGuid().ToString("N");

    var id = FlowFileStore.Save(snap);
    SnapshotStore.Set(snap);

    return Results.Ok(new { ok = true, pipelineId = id, title = snap.Title });
});
// ---- Execute (tam akışı bir kez çalıştır) ----
// 1) Body içinde "snapshot" varsa onu kullanır
// 2) Query ?id=... varsa FlowFileStore'dan yükler
// 3) Aksi halde SnapshotStore içindekini kullanır
app.MapPost("/api/execute_old", async (HttpContext http, FlowRunner runner, CancellationToken ct) =>
{
    FlowSnapshot? snap = null;
    FlowInputs seed = new() { _context = http };

    // Query: id=PIPELINE_GUID
    if (http.Request.Query.TryGetValue("id", out var qv) && !string.IsNullOrWhiteSpace(qv))
    {
        var id = qv.ToString();
        if (FlowFileStore.TryGet(id, out var fsnap) && fsnap is not null)
            snap = fsnap;
        else
            return Results.NotFound(new ExecuteResultDto { Ok = false, Error = "Pipeline not found" });
    }

    // Body: ExecutePayload (snapshot + seed)
    using var doc = await JsonDocument.ParseAsync(http.Request.Body);
    var root = doc.RootElement;

    if (root.TryGetProperty("seed", out var seedEl) && seedEl.ValueKind == JsonValueKind.Array)
    {
        foreach (var it in seedEl.EnumerateArray())
        {
            var nodeId = it.GetProperty("nodeId").GetString()!;
            var handle = it.GetProperty("handle").GetString()!;
            var valRaw = it.GetProperty("value").GetRawText();
            var valObj = JsonSerializer.Deserialize<object?>(valRaw);
            seed.Values[(nodeId, handle)] = valObj;
        }
    }

    if (snap is null)
    {
        if (root.TryGetProperty("snapshot", out var sEl))
            snap = Helper.NormalizeIfUiSnapshot(JsonDocument.Parse(sEl.GetRawText()));
        else
            snap = SnapshotStore.Get();
    }

    if (snap is null)
        return Results.BadRequest(new ExecuteResultDto { Ok = false, Error = "snapshot is missing" });

    if (!TriggerValidator.HasAtLeastOneTrigger(snap))
        return Results.BadRequest(new ExecuteResultDto { Ok = false, Error = "Pipeline must include at least one Trigger node." });

    var res = await runner.ExecuteOnceAsync(snap, seed, ct);
    return Results.Json(new { ok = res.Success, outputs = res.AllOutputs, pipelineId = snap.PipelineId });
});


app.MapPost("/api/execute", async (HttpContext http, FlowRunner runner, CancellationToken ct) =>
{
    // Body'den snapshot ve payload'u oku
    string bodyText = await new StreamReader(http.Request.Body).ReadToEndAsync();
    if (string.IsNullOrWhiteSpace(bodyText))
        return Results.BadRequest(new { ok = false, error = "Body is empty" });

    using var doc = JsonDocument.Parse(bodyText);
    var root = doc.RootElement;

    // snapshot ve payload zorunlu
    if (!root.TryGetProperty("snapshot", out var sEl) || !root.TryGetProperty("payload", out var payloadEl))
        return Results.BadRequest(new { ok = false, error = "snapshot ve payload zorunlu" });

    var snap = Helper.NormalizeIfUiSnapshot(JsonDocument.Parse(sEl.GetRawText()));
    if (snap is null)
        return Results.BadRequest(new { ok = false, error = "snapshot is missing or invalid" });

    // İlk trigger node'unu bul
    var firstTrigger = snap.Nodes.FirstOrDefault(n =>
        n.Type.Equals("httpListen", StringComparison.OrdinalIgnoreCase) ||
        n.Type.Equals("manualTrigger", StringComparison.OrdinalIgnoreCase) ||
        n.Type.EndsWith("Trigger", StringComparison.OrdinalIgnoreCase)
    );
    if (firstTrigger == null)
        return Results.BadRequest(new { ok = false, error = "Flow'da trigger node yok" });

    // Payload'u hazırla
    object? bodyObj;
    try
    {
        bodyObj = JsonSerializer.Deserialize<object?>(payloadEl.GetRawText());
    }
    catch
    {
        return Results.BadRequest(new { ok = false, error = "Payload JSON parse hatası" });
    }

    // İlk trigger node'unun request ve context handle'larına payload'u inject et
    var reqObj = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
    {
        ["body"] = bodyObj,
        ["method"] = "EXECUTE",
        ["path"] = "/api/execute"
    };

    var flowInputs = new FlowInputs();
    flowInputs.Values[(firstTrigger.Id, "request")] = reqObj;
    flowInputs.Values[(firstTrigger.Id, "context")] = bodyObj;

    // Çalıştır
    var res = await runner.ExecuteOnceAsync(snap, flowInputs, ct);

    // UI için node çıktıları
    // Program.cs, /api/execute endpoint'inde:
var allInputs = res.Context.Inputs ?? new Dictionary<string, Dictionary<string, object?>>();
var allOutputs = res.AllOutputs ?? new Dictionary<string, Dictionary<string, object?>>();
var nodes = snap.Nodes.Select(n => new
{
    id = n.Id,
    label = string.IsNullOrWhiteSpace(n.Label) ? n.Type : n.Label,
    inputs = allInputs.TryGetValue(n.Id, out var ins) ? ins : new Dictionary<string, object?>(),
    outputs = allOutputs.TryGetValue(n.Id, out var outs) ? outs : new Dictionary<string, object?>(),
    error = (string?)null
});

    return Results.Json(new
    {
        ok = res.Success,
        outputs = allOutputs,
        nodes,
        pipelineId = snap.PipelineId
    });
});
    

// ---- Spesifik Node/Handle seed enjeksiyonu ----
app.MapPost("/api/inject", async (HttpContext http, FlowRunner runner, CancellationToken ct) =>
{
    var payload = await JsonSerializer.DeserializeAsync<InjectPayload>(
        http.Request.Body,
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

    if (payload is null)
        return Results.BadRequest(new { error = "Invalid payload" });

    var snap = SnapshotStore.Get();
    if (snap is null)
        return Results.BadRequest(new { error = "snapshot is missing" });

    var seed = new FlowInputs();
    seed.Values[(payload.NodeId, payload.Handle)] = payload.Value;

    var res = await runner.ExecuteOnceAsync(snap, seed, ct);
    return Results.Json(new { ok = res.Success, outputs = res.AllOutputs, pipelineId = snap.PipelineId });
});

// ---- Dinamik Trigger endpointlerini kur ----
// Bu uç, mevcut Snapshot içindeki httpListen ve webhookIn nodelarından KESTREL route’larını üretir.
var mappedRoutes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

app.MapPost("/api/triggers/load", (FlowRunner runner) =>
{
    var snap = SnapshotStore.Get();
    if (snap is null)
        return Results.BadRequest(new { error = "snapshot not set" });

    // httpListen
    foreach (var n in snap.Nodes.Where(n => n.Type.Equals("httpListen", StringComparison.OrdinalIgnoreCase)))
    {
        var node = n; // closure safety

        var method = node.Settings.TryGetValue("method", out var m) ? (m?.ToString() ?? "POST").ToUpperInvariant() : "POST";
        var path = node.Settings.TryGetValue("path", out var p) ? (p?.ToString() ?? "/api/ingest") : "/api/ingest";
        var parse = node.Settings.TryGetValue("parse", out var pr) ? (pr?.ToString() ?? "json").ToLowerInvariant() : "json";

        var key = $"{method} {path} => {node.Id}";
        if (mappedRoutes.Contains(key)) continue;

        app.MapMethods(path, new[] { method }, async (HttpContext ctx, CancellationToken ct) =>
        {
            var bodyText = await new StreamReader(ctx.Request.Body).ReadToEndAsync(ct);
            object? bodyObj = bodyText;

            if (parse == "json")
            {
                try { bodyObj = JsonSerializer.Deserialize<object?>(bodyText); }
                catch { bodyObj = bodyText; }
            }

            var reqObj = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["headers"] = ctx.Request.Headers.ToDictionary(k => k.Key, v => (object?)string.Join(",", v.Value)),
                ["query"] = ctx.Request.Query.ToDictionary(k => k.Key, v => (object?)string.Join(",", v.Value)),
                ["body"] = bodyObj,
                ["method"] = ctx.Request.Method,
                ["path"] = ctx.Request.Path.ToString()
            };

            var seed = new FlowInputs();
            seed.Values[(node.Id, "request")] = reqObj;
            seed.Values[(node.Id, "context")] = new { remoteIp = ctx.Connection.RemoteIpAddress?.ToString() };

            var curSnap = SnapshotStore.Get() ?? snap; // snapshot değişmiş olabilir
            var result = await runner.ExecuteOnceAsync(curSnap, seed, ct);
            return Results.Json(new { ok = result.Success, outputs = result.AllOutputs, pipelineId = curSnap.PipelineId });
        });

        mappedRoutes.Add(key);
        Console.WriteLine($"[HTTP LISTEN] {method} {path} (node: {node.Id})");
    }

    // webhookIn
    foreach (var n in snap.Nodes.Where(n => n.Type.Equals("webhookIn", StringComparison.OrdinalIgnoreCase)))
    {
        var node = n;

        var path = node.Settings.TryGetValue("path", out var p) ? (p?.ToString() ?? "/webhook/provider") : "/webhook/provider";
        var sigHdr = node.Settings.TryGetValue("signatureHeader", out var sh) ? (sh?.ToString() ?? "X-Signature") : "X-Signature";
        var secret = node.Settings.TryGetValue("secret", out var s) ? (s?.ToString() ?? "") : "";

        var key = $"POST {path} => {node.Id}";
        if (mappedRoutes.Contains(key)) continue;

        app.MapPost(path, async (HttpContext ctx, CancellationToken ct) =>
        {
            var bodyText = await new StreamReader(ctx.Request.Body).ReadToEndAsync(ct);
            var signature = ctx.Request.Headers[sigHdr].FirstOrDefault();
            bool verified = true;

            if (!string.IsNullOrWhiteSpace(secret))
            {
                using var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(secret));
                var hash = Convert.ToHexString(hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(bodyText))).ToLowerInvariant();
                verified = !string.IsNullOrWhiteSpace(signature) && signature.ToLowerInvariant().Contains(hash);
            }

            object? bodyObj = null;
            try { bodyObj = JsonSerializer.Deserialize<object?>(bodyText); } catch { bodyObj = bodyText; }

            var seed = new FlowInputs();
            seed.Values[(node.Id, "event")] = new
            {
                verified,
                headers = ctx.Request.Headers.ToDictionary(k => k.Key, v => (object?)string.Join(",", v.Value)),
                body = bodyObj
            };
            seed.Values[(node.Id, "raw")] = new { text = bodyText };

            var curSnap = SnapshotStore.Get() ?? snap;
            var result = await runner.ExecuteOnceAsync(curSnap, seed, ct);
            return Results.Json(new { ok = result.Success, outputs = result.AllOutputs, verified, pipelineId = curSnap.PipelineId });
        });

        mappedRoutes.Add(key);
        Console.WriteLine($"[WEBHOOK] POST {path} (hdr: {sigHdr}) (node: {node.Id})");
    }

    return Results.Ok(new { ok = true, mapped = mappedRoutes.Count });
});

// ---- Çalışma Sonuçlarını Okuma ----
app.MapGet("/api/outputs", () =>
{
    var all = OutputStore.GetAll();
    return Results.Json(new { ok = true, outputs = all });
});

app.MapGet("/api/outputs/{nodeId}", (string nodeId, string? handle) =>
{
    if (string.IsNullOrWhiteSpace(handle))
    {
        if (OutputStore.TryGet(nodeId, out var outs))
            return Results.Json(new { ok = true, nodeId, outputs = outs });

        return Results.NotFound(new { ok = false, error = "node outputs not found" });
    }
    else
    {
        if (OutputStore.TryGet(nodeId, handle, out var value))
            return Results.Json(new { ok = true, nodeId, handle, value });

        return Results.NotFound(new { ok = false, error = "handle not found" });
    }
});

app.MapGet("/api/routes", () => Results.Json(new { ok = true, routes = mappedRoutes }));

app.Run();

// ---- DTO'lar ----
record ExecutePayload(FlowSnapshot? Snapshot, List<SeedItem>? Seed);
record SeedItem(string NodeId, string Handle, object? Value);
record InjectPayload(string NodeId, string Handle, object? Value);

// -----------------------------
// FlowFileStore: JSON dosya depolama
// -----------------------------
static class FlowFileStore
{
    private static readonly object _lock = new();
    private static readonly string BaseDir = InitBaseDir();
    private static Dictionary<string, StoredFlowIndexItem> _index = BuildIndex();

    private static string InitBaseDir()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Flows");
        Directory.CreateDirectory(dir);
        return dir;
    }
    public static string Load(string id)
    {
        var file = Path.Combine(BaseDir, $"{id}.json");
        if (!File.Exists(file)) return null;
        var json = File.ReadAllText(file);
        //var d = JsonSerializer.Deserialize<FlowSnapshot>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        return json;
    }
    private static Dictionary<string, StoredFlowIndexItem> BuildIndex()
    {
        var dict = new Dictionary<string, StoredFlowIndexItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in Directory.GetFiles(BaseDir, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(f);
                var snap = JsonSerializer.Deserialize<FlowSnapshot>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (snap is null) continue;
                var id = snap.PipelineId ?? Path.GetFileNameWithoutExtension(f);
                var title = string.IsNullOrWhiteSpace(snap.Title) ? $"Flow {id}" : snap.Title;
                var savedTime = File.GetLastWriteTimeUtc(f);
                dict[id] = new StoredFlowIndexItem
                {
                    PipelineId = id,
                    Title = title,
                    SavedAtUtc = savedTime,
                    FileName = f
                };
            }
            catch { /* ignore */ }
        }
        return dict;
    }

    public static List<StoredFlowIndexItem> List()
    {
        lock (_lock) return _index.Values.OrderByDescending(i => i.SavedAtUtc).ToList();
    }

    public static bool TryGet(string id, out FlowSnapshot? snapshot)
    {
        lock (_lock)
        {
            if (_index.TryGetValue(id, out var item) && File.Exists(item.FileName))
            {
                try
                {
                    var json = File.ReadAllText(item.FileName);
                    snapshot = JsonSerializer.Deserialize<FlowSnapshot>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    // Eski kayıtlarda PipelineId boş olabilir
                    if (snapshot != null && string.IsNullOrWhiteSpace(snapshot.PipelineId))
                        snapshot.PipelineId = id;
                    return snapshot != null;
                }
                catch { /* ignore */ }
            }
        }
        snapshot = null;
        return false;
    }

    public static string Save(FlowSnapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(snapshot.PipelineId))
            snapshot.PipelineId = Guid.NewGuid().ToString("N");

        var id = snapshot.PipelineId;
        var title = string.IsNullOrWhiteSpace(snapshot.Title) ? $"Flow {id}" : snapshot.Title;

        var path = Path.Combine(BaseDir, $"{id}.json");
        var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });
        lock (_lock)
        {
            File.WriteAllText(path, json);
            _index[id] = new StoredFlowIndexItem
            {
                PipelineId = id,
                Title = title,
                SavedAtUtc = DateTime.UtcNow,
                FileName = path
            };
        }
        return id;
    }
}

// -----------------------------
// Utilities
// -----------------------------
static class TriggerValidator
{
    private static readonly string[] TriggerHints = new[]
    {
        "httpListen", "webhookIn", "websocketIn", "cron", "intervalTimer", "onStart", "manualTrigger",
        "sseSubscribe", "fileWatch", "directoryWatch", "mqttSubscribe", "queueConsume", "smtpInbound",
        "keyboardShortcut", "httpPoller", "bluetoothNotify", "geoFence", "batteryLevel", "clipboardChange",
        "webhookVerify"
    };

    public static bool HasAtLeastOneTrigger(FlowSnapshot snap)
    {
        if (snap?.Nodes is null || snap.Nodes.Count == 0) return false;
        return snap.Nodes.Any(n => TriggerHints.Any(h => n.Type.Contains(h, StringComparison.OrdinalIgnoreCase)));
    }
}