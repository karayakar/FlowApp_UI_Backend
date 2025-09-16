// FlowHost/HttpListenHost.cs
using Microsoft.AspNetCore.WebUtilities;

using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;
public static class JsonExtensions
{
    public static IReadOnlyDictionary<string, object?> ToDictionary(this JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("JsonElement is not an object.");

        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var prop in element.EnumerateObject())
        {
            dict[prop.Name] = ConvertValue(prop.Value);
        }

        return dict;
    }

    public static IReadOnlyDictionary<string, object?> ToDictionary(object obj)
    {
        var dict = new Dictionary<string, object?>();

        foreach (var prop in obj.GetType().GetProperties())
        {
            dict[prop.Name] = prop.GetValue(obj);
        }

        return new ReadOnlyDictionary<string, object?>(dict);
    }
    private static object? ConvertValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out var l) => l,
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Object => element.ToDictionary(),
            JsonValueKind.Array => element.EnumerateArray().Select(ConvertValue).ToList(),
            _ => element.ToString()
        };
    }
}
public static class HttpListenHost
{
    public static void MapHttpListenEndpoints(WebApplication app, FlowSnapshot snapshot, FlowRunner runner)
    {
        foreach (var node in snapshot.Nodes.Where(n => n.Type.Equals("httpListen", StringComparison.OrdinalIgnoreCase)))
        {
            var method = node.Settings.TryGetValue("method", out var m) ? (m?.ToString() ?? "POST").ToUpperInvariant() : "POST";
            var path = node.Settings.TryGetValue("path", out var p) ? (p?.ToString() ?? "/api/ingest") : "/api/ingest";
            var parse = node.Settings.TryGetValue("parse", out var pr) ? (pr?.ToString() ?? "json").ToLowerInvariant() : "json";
            var auth = node.Settings.TryGetValue("auth", out var au) ? (au?.ToString() ?? "none").ToLowerInvariant() : "none";

            // NOT: Port bilgisi host seviyesinde yönetilir (Kestrel). Node.settings.port sadece dokümantatif.

            app.MapMethods(path, new[] { method }, async (HttpContext http, CancellationToken ct) =>
            {
                // 1) Auth
                if (!await CheckAuthAsync(http, auth))
                    return Results.Unauthorized();

                // 2) Request parçalama
                var requestObj = await BuildRequestObjectAsync(http, parse, ct);

                // 3) Context objesi
                var contextObj = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["remoteIp"] = http.Connection.RemoteIpAddress?.ToString(),
                    ["receivedAt"] = DateTimeOffset.UtcNow.ToString("O"),
                    ["user"] = http.User?.Identity?.IsAuthenticated == true ? http.User.Identity?.Name : null
                };

                // 4) Seed → Flow’u çalıştır
                var seed = new FlowInputs();
                seed.Values[(node.Id, "request")] = requestObj;
                seed.Values[(node.Id, "context")] = contextObj;

                var result = await runner.ExecuteOnceAsync(snapshot, seed, ct);

                // 5) Yanıt: Basit varsayılan. İstersen httpReply node’u ile özelleştirilebilir.
                var status = 200;
                object? payload = new { ok = result.Success, outputs = result.AllOutputs };
                return Results.Json(payload, statusCode: status);
            });

            Console.WriteLine($"[HTTP LISTEN] {method} {path}");
        }
    }

    private static async Task<bool> CheckAuthAsync(HttpContext http, string authMode)
    {
        if (authMode == "none") return true;

        if (authMode == "basic")
        {
            // Authorization: Basic base64(user:pass)
            if (!http.Request.Headers.TryGetValue("Authorization", out var hdr)) return false;
            var token = hdr.ToString();
            if (!token.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase)) return false;
            // Burada user/pass doğrulaması appsettings ya da secrets üzerinden yapılabilir
            // Örnek/stub: sadece header varlığını kontrol ettik
            return true;
        }

        if (authMode == "bearer")
        {
            // Authorization: Bearer <token>
            if (!http.Request.Headers.TryGetValue("Authorization", out var hdr)) return false;
            var token = hdr.ToString();
            if (!token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return false;
            // Burada JWT/opaque token doğrulaması yapabilirsin.
            return true;
        }

        return false;
    }

    private static async Task<Dictionary<string, object?>> BuildRequestObjectAsync(HttpContext http, string parseMode, CancellationToken ct)
    {
        var headers = http.Request.Headers.ToDictionary(k => k.Key, v => (object?)string.Join(",", v.Value));
        var query = http.Request.Query.ToDictionary(k => k.Key, v => (object?)string.Join(",", v.Value));
        var routeV = http.GetRouteData()?.Values?.ToDictionary(k => k.Key, v => v.Value) ?? new Dictionary<string, object?>();

        object? bodyObj = null;
        string? rawText = null;

        if (string.Equals(http.Request.Method, "GET", StringComparison.OrdinalIgnoreCase))
        {
            // GET’te gövde beklemiyoruz; ama gelirse text olarak okuyalım
            using var reader = new StreamReader(http.Request.Body);
            rawText = await reader.ReadToEndAsync(ct);
            bodyObj = string.IsNullOrWhiteSpace(rawText) ? null : rawText;
        }
        else
        {
            switch (parseMode)
            {
                case "json":
                    {
                        using var reader = new StreamReader(http.Request.Body, Encoding.UTF8, leaveOpen: false);
                        rawText = await reader.ReadToEndAsync(ct);
                        try { bodyObj = string.IsNullOrWhiteSpace(rawText) ? null : JsonSerializer.Deserialize<object?>(rawText); }
                        catch { bodyObj = rawText; }
                        break;
                    }
                case "form":
                    {
                        // application/x-www-form-urlencoded veya multipart/form-data
                        if (http.Request.HasFormContentType)
                        {
                            var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                            foreach (var f in http.Request.Form)
                                dict[f.Key] = (string?)f.Value.ToString();

                            // Dosyalar varsa
                            if (http.Request.Form.Files?.Count > 0)
                            {
                                var files = new List<object?>();
                                foreach (var file in http.Request.Form.Files)
                                {
                                    using var ms = new MemoryStream();
                                    await file.CopyToAsync(ms, ct);
                                    files.Add(new
                                    {
                                        file.FileName,
                                        file.ContentType,
                                        file.Length,
                                        Base64 = Convert.ToBase64String(ms.ToArray())
                                    });
                                }
                                dict["_files"] = files;
                            }
                            bodyObj = dict;
                        }
                        else
                        {
                            // url-encoded değilse text gibi oku
                            using var reader = new StreamReader(http.Request.Body, Encoding.UTF8, leaveOpen: false);
                            rawText = await reader.ReadToEndAsync(ct);
                            bodyObj = ParseFormLike(rawText);
                        }
                        break;
                    }
                case "text":
                    {
                        using var reader = new StreamReader(http.Request.Body, Encoding.UTF8, leaveOpen: false);
                        rawText = await reader.ReadToEndAsync(ct);
                        bodyObj = rawText;
                        break;
                    }
                case "raw":
                default:
                    {
                        using var ms = new MemoryStream();
                        await http.Request.Body.CopyToAsync(ms, ct);
                        bodyObj = ms.ToArray(); // byte[]
                        break;
                    }
            }
        }

        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["headers"] = headers,
            ["query"] = query,
            ["routeValues"] = routeV,
            ["body"] = bodyObj,
            ["raw"] = rawText, // json/text için
            ["method"] = http.Request.Method,
            ["path"] = http.Request.Path.ToString(),
            ["contentType"] = http.Request.ContentType
        };
    }

    private static Dictionary<string, string?> ParseFormLike(string? text)
    {
        // key=value&key2=value2 biçimini kaba çözümle
        var dict = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(text)) return dict;

        var parsed = QueryHelpers.ParseQuery(text.StartsWith("?") ? text : "?" + text);
        foreach (var kv in parsed)
            dict[kv.Key] = kv.Value.ToString();

        return dict;
    }
}
