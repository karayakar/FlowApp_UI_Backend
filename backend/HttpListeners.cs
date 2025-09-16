//// Program.cs (veya ayrı static sınıf)
//using Microsoft.AspNetCore.Http;

//using System.Text;
//using System.Text.Json;

//static class HttpListenMapper
//{
//    // (method, path) -> nodeId
//    private static readonly Dictionary<string, string> _routeToNode = new(StringComparer.OrdinalIgnoreCase);

//    public static void MapFromSnapshot(WebApplication app, FlowSnapshot snapshot, FlowRunner runner)
//    {
//        _routeToNode.Clear();

//        foreach (var node in snapshot.Nodes ?? Enumerable.Empty<FlowNode>())
//        {
//            if (!string.Equals(node.Type, "httpListen", StringComparison.OrdinalIgnoreCase))
//                continue;

//            var s = node.Settings ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
//            var method = (s.TryGetValue("method", out var m) ? m?.ToString() : "POST")!.ToUpperInvariant();
//            var path = s.TryGetValue("path", out var p) ? p?.ToString() ?? "/api/ingest" : "/api/ingest";
//            var parse = s.TryGetValue("parse", out var g) ? g?.ToString() ?? "json" : "json";
//            // Not: port bilgisini Kestrel seviyesinde ayrı açıyorsan kullan; aksi halde host’un aktif URL’ünden dinlenir.

//            var key = $"{method} {path}";
//            if (_routeToNode.ContainsKey(key))
//            {
//                // İstersen burada throw et veya logla
//                // throw new InvalidOperationException($"Duplicate httpListen route: {key}");
//                _routeToNode[key] = node.Id; // “son kazansın” stratejisi
//            }
//            else
//            {
//                _routeToNode[key] = node.Id;
//            }

//            app.MapMethods(path, new[] { method }, async (HttpRequest req, HttpResponse res) =>
//            {
//                var nodeId = node.Id;
//                object? body = null;

//                // Parse body
//                if (string.Equals(parse, "json", StringComparison.OrdinalIgnoreCase))
//                {
//                    try { body = await JsonSerializer.DeserializeAsync<object>(req.Body); }
//                    catch { body = null; }
//                }
//                else if (string.Equals(parse, "text", StringComparison.OrdinalIgnoreCase))
//                {
//                    using var sr = new StreamReader(req.Body);
//                    body = await sr.ReadToEndAsync();
//                }
//                else if (string.Equals(parse, "form", StringComparison.OrdinalIgnoreCase))
//                {
//                    if (!req.HasFormContentType) await req.ReadFormAsync();
//                    body = req.Form?.ToDictionary(k => k.Key, v => (object?)v.Value.ToString());
//                }
//                else // raw
//                {
//                    using var ms = new MemoryStream();
//                    await req.Body.CopyToAsync(ms);
//                    body = ms.ToArray();
//                }

//                // Request & Context paketle
//                var requestObj = new
//                {
//                    method = req.Method,
//                    path = req.Path.ToString(),
//                    headers = req.Headers.ToDictionary(k => k.Key, v => v.Value.ToString()),
//                    query = req.Query.ToDictionary(k => k.Key, v => v.Value.ToString()),
//                    body
//                };
//                var contextObj = new
//                {
//                    remoteIp = req.HttpContext.Connection.RemoteIpAddress?.ToString(),
//                    time = DateTimeOffset.UtcNow
//                };

//                // Seed hazırla: node giriş handle'ları
//                var seed = new Dictionary<string, Dictionary<string, object?>>
//                {
//                    [nodeId] = new()
//                    {
//                        ["request"] = requestObj,
//                        ["context"] = contextObj
//                    }
//                };

//                // Akışı tetikle: bu noktada senin runtime/execute API'ne göre değişir
//                // 1) Eğer public bir Execute(seed, startNodeId) varsa:
//                // var result = await runner.ExecuteAsync(seed, startNodeId: nodeId);
//                // 2) Yoksa OutputStore’a yazıp Execute() çağıran bir wrapper kullan:
//                // OutputStore.Inject(nodeId, "request", requestObj);
//                // OutputStore.Inject(nodeId, "context", contextObj);
//            var result = await runner.ExecuteOnceAsync(snapshot, seed);

//                // HTTP cevabı: istersen flow çıktısına göre döndür
//                // Örn: httpListen → next → httpResponse gibi bir node yoksa basit 200 json dön
//                await res.WriteAsJsonAsync(new { ok = true, nodeId, accepted = true });
//            });
//        }
//    }
//}
