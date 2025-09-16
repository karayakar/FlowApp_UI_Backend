

using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;




/* =======================
 * Yardımcılar
 * ======================= */

sealed class Jmes
{
    private readonly DevLab.JmesPath.JmesPath _jp = new();
    public object? Eval(object? input, string expr)
    {
        if (input is null) return null;
        var json = JsonSerializer.Serialize(input);
        var result = _jp.Transform(json, expr);
        return string.IsNullOrWhiteSpace(result) ? null : JsonSerializer.Deserialize<object?>(result);
    }
}

sealed class Hbs
{
    public string Render(string template, object? model)
    {
        var t = HandlebarsDotNet.Handlebars.Compile(template);

        return t(model);
    }
}

static class JsonUtil
{
    public static object? SetByPath(object? root, string path, object? value)
    {
        if (root is null) root = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(path)) return value;

        var parts = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
        object? cursor = root;
        for (int i = 0; i < parts.Length; i++)
        {
            var key = parts[i];

            if (i == parts.Length - 1)
            {
                if (cursor is IDictionary<string, object?> dict)
                {
                    dict[key] = value;
                    return root;
                }
                throw new InvalidOperationException("Target path is not an object to set property.");
            }
            else
            {
                if (cursor is IDictionary<string, object?> dict)
                {
                    if (!dict.TryGetValue(key, out var next) || next is null)
                    {
                        next = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                        dict[key] = next;
                    }
                    cursor = next;
                }
                else
                {
                    throw new InvalidOperationException("Intermediate path is not an object.");
                }
            }
        }
        return root;
    }
}

/* =======================
 * CONTROL
 * ======================= */

sealed class IfNode : INodeProcessor
{
    public string Type => "if";
    private readonly Jmes _jmes = new();

    public Task<NodeResult> ExecuteAsync(NodeExecutionArgs args)
    {
        var cond = args.Node.Settings.TryGetValue("condition", out var c) ? c?.ToString() : "true";
        var ok = false;

        if (!string.IsNullOrWhiteSpace(cond))
        {
            var evaluated = _jmes.Eval(new { inputs = args.Inputs }, cond!);
            ok = evaluated switch
            {
                bool b => b,
                string s => bool.TryParse(s, out var r) && r,
                JsonElement e when e.ValueKind == JsonValueKind.True => true,
                JsonElement e when e.ValueKind == JsonValueKind.False => false,
                _ => evaluated != null
            };
        }

        var res = new NodeResult();
        res.Outputs[ok ? "true" : "false"] = args.Inputs.TryGetValue("input", out var v) ? v : args.Inputs;
        return Task.FromResult(res);
    }
}

sealed class SwitchNode : INodeProcessor
{
    public string Type => "switch";
    public Task<NodeResult> ExecuteAsync(NodeExecutionArgs args)
    {
        var val = args.Inputs.TryGetValue("value", out var v) ? v : null;
        var res = new NodeResult();
        if (args.Node.Settings.TryGetValue("cases", out var casesObj) && casesObj is JsonElement el && el.ValueKind == JsonValueKind.Array)
        {
            foreach (var ce in el.EnumerateArray())
            {
                var name = ce.GetString() ?? "";
                if (name.Equals(val?.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    res.Outputs[name] = val;
                    return Task.FromResult(res);
                }
            }
        }
        res.Outputs["default"] = val;
        return Task.FromResult(res);
    }
}

sealed class LoopNode : INodeProcessor
{
    public string Type => "loop";
    public Task<NodeResult> ExecuteAsync(NodeExecutionArgs args)
    {
        var iterations = args.Node.Settings.TryGetValue("iterations", out var it) && int.TryParse(it?.ToString(), out var n) ? n : 1;
        var arr = new List<object?>();
        for (int i = 0; i < iterations; i++) arr.Add(args.Inputs.TryGetValue("input", out var v) ? v : null);
        return Task.FromResult(new NodeResult { Outputs = { ["each"] = arr, ["done"] = true } });
    }
}

sealed class DelayNode : INodeProcessor
{
    public string Type => "delay";
    public async Task<NodeResult> ExecuteAsync(NodeExecutionArgs args)
    {
        var ms = args.Node.Settings.TryGetValue("ms", out var s) && int.TryParse(s?.ToString(), out var n) ? n : 1000;
        await Task.Delay(ms, args.Context.Cancellation.Value);
        return new NodeResult { Outputs = { ["output"] = args.Inputs.TryGetValue("input", out var v) ? v : null } };
    }
}

// Try/Catch kontrol düğümü – engine seviyesinde tam “çevreleme” gerektirir;
// burada basitçe geleni try koluna iletir.
sealed class TryCatchNode : INodeProcessor
{
    public string Type => "tryCatch";
    public Task<NodeResult> ExecuteAsync(NodeExecutionArgs args)
    {
        var res = new NodeResult();
        res.Outputs["try"] = args.Inputs.TryGetValue("input", out var v) ? v : null;
        // “catch/finally” için engine tarafında özel hata yakalama uygunsa genişletilir.
        return Task.FromResult(res);
    }
}

/* =======================
 * DATA
 * ======================= */

sealed class JsonTransformNode : INodeProcessor
{
    public string Type => "jsonTransform";
    private readonly Jmes _jmes = new();
    public Task<NodeResult> ExecuteAsync(NodeExecutionArgs args)
    {
        var expr = args.Node.Settings.TryGetValue("expression", out var e) ? e?.ToString() : "";
        var input = args.Inputs.TryGetValue("input", out var v) ? v : args.Inputs;
        var result = string.IsNullOrWhiteSpace(expr) ? input : _jmes.Eval(input, expr!);
        return Task.FromResult(new NodeResult { Outputs = { ["result"] = result } });
    }
}

sealed class ArrayMapNode : INodeProcessor
{
    public string Type => "arrayMap";
    private readonly Jmes _jmes = new();
    public Task<NodeResult> ExecuteAsync(NodeExecutionArgs args)
    {
        var mapper = args.Node.Settings.TryGetValue("mapper", out var m) ? m?.ToString() : "";
        var arr = args.Inputs.TryGetValue("array", out var v) ? v : null;
        var result = _jmes.Eval(arr, mapper ?? "") ?? arr;
        return Task.FromResult(new NodeResult { Outputs = { ["result"] = result } });
    }
}

sealed class ArrayFilterNode : INodeProcessor
{
    public string Type => "arrayFilter";
    private readonly Jmes _jmes = new();
    public Task<NodeResult> ExecuteAsync(NodeExecutionArgs args)
    {
        var pred = args.Node.Settings.TryGetValue("predicate", out var p) ? p?.ToString() : "";
        var arr = args.Inputs.TryGetValue("array", out var v) ? v : null;
        var expr = string.IsNullOrWhiteSpace(pred) ? "" : $"[{pred}]";
        var result = _jmes.Eval(arr, expr) ?? arr;
        return Task.FromResult(new NodeResult { Outputs = { ["result"] = result } });
    }
}

sealed class MathOpNode : INodeProcessor
{
    public string Type => "mathOp";
    public Task<NodeResult> ExecuteAsync(NodeExecutionArgs args)
    {
        // Settings.expression (ör: "a + b")
        var expr = args.Node.Settings.TryGetValue("expression", out var e) ? e?.ToString() ?? "a + b" : "a + b";
        var a = ToDouble(args.Inputs.TryGetValue("a", out var av) ? av : 0);
        var b = ToDouble(args.Inputs.TryGetValue("b", out var bv) ? bv : 0);

        double result = expr.Contains("*") ? a * b
                       : expr.Contains("/") ? (b == 0 ? double.NaN : a / b)
                       : expr.Contains("-") ? a - b
                       : /* default + */ a + b;

        return Task.FromResult(new NodeResult { Outputs = { ["result"] = result } });

        static double ToDouble(object? x)
            => x switch
            {
                null => 0,
                double d => d,
                float f => f,
                int i => i,
                long l => l,
                decimal m => (double)m,
                string s when double.TryParse(s, out var d2) => d2,
                JsonElement je when je.ValueKind == JsonValueKind.Number && je.TryGetDouble(out var jd) => jd,
                _ => 0
            };
    }
}

sealed class StringConcatNode : INodeProcessor
{
    public string Type => "stringConcat";
    public Task<NodeResult> ExecuteAsync(NodeExecutionArgs args)
    {
        var sep = args.Node.Settings.TryGetValue("separator", out var s) ? s?.ToString() : " ";
        var left = args.Inputs.TryGetValue("left", out var l) ? l?.ToString() ?? "" : "";
        var right = args.Inputs.TryGetValue("right", out var r) ? r?.ToString() ?? "" : "";
        return Task.FromResult(new NodeResult { Outputs = { ["text"] = string.Join(sep, new[] { left, right }) } });
    }
}

sealed class StringTemplateNode : INodeProcessor
{
    public string Type => "stringTemplate";
    private readonly Hbs _hbs = new();

    public Task<NodeResult> ExecuteAsync(NodeExecutionArgs args)
    {
        var template = args.Node.Settings.TryGetValue("template", out var t) ? t?.ToString() ?? "" : "";
        var text = "";
        // model seçim mantığı: context > tek input > tüm inputs
        object? model = args.Inputs.TryGetValue("context", out var c) ? c
                        : (args.Inputs.Count == 1 ? args.Inputs.First().Value : args.Inputs);
        try
        {

            if (model is JsonElement je)
            {
                model = je.ValueKind == JsonValueKind.Object
                    ? JsonSerializer.Deserialize<Dictionary<string, object?>>(je.GetRawText())
                    : JsonSerializer.Deserialize<object?>(je.GetRawText());
            }
            else if (model is not IDictionary<string, object?> && model is not string)
            {

            }
            else if (model != null)
            {
                //var json = JsonSerializer.Serialize(model);
                model = JsonSerializer.Deserialize<dynamic>(model.ToString());
                if (model is JsonElement jes)
                {
                    model = jes.ValueKind == JsonValueKind.Object
                        ? JsonSerializer.Deserialize<Dictionary<string, object?>>(jes.GetRawText())
                        : JsonSerializer.Deserialize<object?>(jes.GetRawText());
                }


            }
            text = _hbs.Render(template, model);

        }
        catch (Exception)
        {


        }
        return Task.FromResult(new NodeResult { Outputs = { ["text"] = text } });
    }
}

sealed class RegexExtractNode : INodeProcessor
{
    public string Type => "regexExtract";
    public Task<NodeResult> ExecuteAsync(NodeExecutionArgs args)
    {
        var pattern = args.Node.Settings.TryGetValue("pattern", out var p) ? p?.ToString() ?? "" : "";
        var text = args.Inputs.TryGetValue("text", out var t) ? t?.ToString() ?? "" : "";
        var matches = Regex.Matches(text, pattern).Select(m => m.Value).ToArray();
        return Task.FromResult(new NodeResult { Outputs = { ["matches"] = matches } });
    }
}

sealed class JsonMergeNode : INodeProcessor
{
    public string Type => "jsonMerge";
    public Task<NodeResult> ExecuteAsync(NodeExecutionArgs args)
    {
        var strategy = args.Node.Settings.TryGetValue("strategy", out var s) ? s?.ToString() ?? "shallow" : "shallow";
        var left = args.Inputs.TryGetValue("left", out var l) ? l : null;
        var right = args.Inputs.TryGetValue("right", out var r) ? r : null;

        var result = new { left = left, right = right };
            //strategy.Equals("deep", StringComparison.OrdinalIgnoreCase)
          //  ? DeepMerge(left, right)
           // : ShallowMerge(left, right);

        return Task.FromResult(new NodeResult { Outputs = { ["result"] = result } });

        static IDictionary<string, object?> MaterializeObject(object? o)
        {
            if (o is IDictionary<string, object?> d) return new Dictionary<string, object?>(d, StringComparer.OrdinalIgnoreCase);
            if (o is JsonElement je && je.ValueKind == JsonValueKind.Object)
                return JsonSerializer.Deserialize<Dictionary<string, object?>>(je.GetRawText()) ?? new();
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }

        static IDictionary<string, object?> ShallowMerge(IDictionary<string, object?> a, IDictionary<string, object?> b)
        {
            var res = new Dictionary<string, object?>(a, StringComparer.OrdinalIgnoreCase);
            foreach (var kv in b) res[kv.Key] = kv.Value;
            return res;
        }

        static IDictionary<string, object?> DeepMerge(IDictionary<string, object?> a, IDictionary<string, object?> b)
        {
            var res = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var k in a.Keys.Union(b.Keys, StringComparer.OrdinalIgnoreCase))
            {
                a.TryGetValue(k, out var av);
                b.TryGetValue(k, out var bv);

                if (av is IDictionary<string, object?> ao && bv is IDictionary<string, object?> bo)
                    res[k] = DeepMerge(ao, bo);
                else
                    res[k] = bv ?? av;
            }
            return res;
        }
    }
}

/* =======================
 * NETWORK
 * ======================= */

sealed class HttpRequestNode : INodeProcessor
{
    public string Type => "httpRequest";
    private readonly HttpClient _http;
    public HttpRequestNode(HttpClient http) => _http = http;

    public async Task<NodeResult> ExecuteAsync(NodeExecutionArgs args)
    {
        var s = args.Node.Settings;
        var method = s.TryGetValue("method", out var m) ? (m?.ToString() ?? "GET").ToUpperInvariant() : "GET";
        var url = s.TryGetValue("url", out var u) ? (u?.ToString() ?? "") : "";
        var parse = s.TryGetValue("parse", out var p) ? (p?.ToString() ?? "json") : "json";
        var body = s.TryGetValue("body", out var b) ? b : null;

        var timeoutSec = s.TryGetValue("timeoutSec", out var ts) && int.TryParse(ts?.ToString(), out var n) ? n : 60;
        var proxyUrl = s.TryGetValue("proxy", out var px) ? px?.ToString() : null;

        HttpClient clientToUse = _http;
        _http.Timeout = TimeSpan.FromSeconds(timeoutSec);

        HttpClient? tempClient = null;
        if (!string.IsNullOrWhiteSpace(proxyUrl))
        {
            var handler = new HttpClientHandler
            {
                Proxy = new WebProxy(proxyUrl),
                UseProxy = true
            };
            tempClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(timeoutSec) };
            clientToUse = tempClient;
        }

        try
        {
            using var req = new HttpRequestMessage(new HttpMethod(method), url);

            if (s.TryGetValue("headers", out var h) && h is JsonElement he && he.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in he.EnumerateObject())
                {
                    var val = prop.Value.ToString();
                    if (prop.Name.Equals("Authorization", StringComparison.OrdinalIgnoreCase))
                        clientToUse.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse(val);
                    else
                        req.Headers.TryAddWithoutValidation(prop.Name, val);
                }
            }

            if (!string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase) && body is not null)
            {
                if (body is string sbody)
                    req.Content = new StringContent(sbody, Encoding.UTF8, "application/json");
                else
                    req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            }

            var resp = await clientToUse.SendAsync(req, args.Context.Cancellation.Value);
            var text = await resp.Content.ReadAsStringAsync(args.Context.Cancellation.Value);

            object? parsed = text;
            if (parse.Equals("json", StringComparison.OrdinalIgnoreCase))
            {
                try { parsed = JsonSerializer.Deserialize<object?>(text); } catch { parsed = text; }
            }

            return new NodeResult
            {
                Outputs =
                {
                    ["response"] = parsed,
                    ["status"] = (int)resp.StatusCode,
                    ["text"] = text
                }
            };
        }
        finally
        {
            tempClient?.Dispose();
        }
    }
}

sealed class WebsocketSendNode : INodeProcessor
{
    public string Type => "websocket";
    public async Task<NodeResult> ExecuteAsync(NodeExecutionArgs args)
    {
        var settings = Helper.GetByPath(args.Node.Settings, "settings");
        var url = Helper.GetByPath(settings, "url").ToString();



        if (string.IsNullOrWhiteSpace(url))
            throw new InvalidOperationException("websocket: 'url' is required.");

        var message = Helper.GetByPath(settings, "message").ToString();


        // Opsiyonel ayarlar
        var expectResponse = Helper.GetByPath(settings, "expectResponse").ToString();

        var closeAfterSend = Helper.GetByPath(settings, "closeAfterSend").ToString();
        var connectTimeoutSec = Convert.ToInt32(Helper.GetByPath(settings, "connectTimeoutSec").ToString());

        var sendTimeoutSec = Convert.ToInt32(Helper.GetByPath(settings, "sendTimeoutSec").ToString());

        var receiveTimeoutSec = Convert.ToInt32(Helper.GetByPath(settings, "receiveTimeoutSec").ToString());

        //var bufferSize = Convert.ToInt32(Helper.GetByPath(settings, "bufferSize").ToString());

        //var proxyUrl = Helper.GetByPath(settings, "proxy").ToString();

        // Subprotocols
        //var sp = new List<string>();
        //var subprotocols = Helper.GetByPath(settings, "subprotocols");

        // foreach (var it in subprotocols)
        //if (it.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(it.GetString()))
        //    sp.Add(it.GetString()!);


        // Headers
        //var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        //if (s.TryGetValue("headers", out var h) && h is JsonElement he && he.ValueKind == JsonValueKind.Object)
        //{
        //    foreach (var p in he.EnumerateObject())
        //        headers[p.Name] = p.Value.ToString();
        //}

        //var sw = System.Diagnostics.Stopwatch.StartNew();

        //using var cws = new ClientWebSocket();

        //// Proxy
        //if (!string.IsNullOrWhiteSpace(proxyUrl))
        //    cws.Options.Proxy = new WebProxy(proxyUrl);

        //// Subprotocols
        //foreach (var proto in subprotocols)
        //    cws.Options.AddSubProtocol(proto);

        //// Headers
        //foreach (var kv in headers)
        //    cws.Options.SetRequestHeader(kv.Key, kv.Value);


        // Connect (timeout ile)
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var cws = new ClientWebSocket();
        using var ctsConnect = CancellationTokenSource.CreateLinkedTokenSource(args.Context.Cancellation.Value);
        ctsConnect.CancelAfter(TimeSpan.FromSeconds(connectTimeoutSec));

        try
        {
            await cws.ConnectAsync(new Uri(url), ctsConnect.Token);
        }
        catch (OperationCanceledException)
        {
            return TimeoutResult("connect-timeout", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            return ErrorResult("connect-error", ex, sw.ElapsedMilliseconds);
        }

        // Send (timeout ile)
        using var ctsSend = CancellationTokenSource.CreateLinkedTokenSource(args.Context.Cancellation.Value);
        ctsSend.CancelAfter(TimeSpan.FromSeconds(sendTimeoutSec));

        try
        {
            var seg = new ArraySegment<byte>(Encoding.UTF8.GetBytes(message ?? ""));
            await cws.SendAsync(seg, WebSocketMessageType.Text, endOfMessage: true, cancellationToken: ctsSend.Token);
        }
        catch (OperationCanceledException)
        {
            // Bağlantıyı kapatmayı dene
            SafeAbort(cws);
            return TimeoutResult("send-timeout", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            SafeAbort(cws);
            return ErrorResult("send-error", ex, sw.ElapsedMilliseconds);
        }

        object? responseObj = null;
        string status = "ok";

        // Receive (opsiyonel)
        if (expectResponse == "true")
        {
            using var ctsRecv = CancellationTokenSource.CreateLinkedTokenSource(args.Context.Cancellation.Value);
            ctsRecv.CancelAfter(TimeSpan.FromSeconds(receiveTimeoutSec));

            try
            {
                var buffer = new byte[1024];
                using var ms = new MemoryStream();

                while (true)
                {
                    var result = await cws.ReceiveAsync(new ArraySegment<byte>(buffer), ctsRecv.Token);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        status = "closed-by-remote";
                        break;
                    }

                    ms.Write(buffer, 0, result.Count);

                    if (result.EndOfMessage)
                    {
                        // yalnızca tek mesaj okuyoruz
                        break;
                    }
                }

                var text = Encoding.UTF8.GetString(ms.ToArray());
                // basit JSON parse denemesi
                responseObj = TryJson(text) ?? text;
            }
            catch (OperationCanceledException)
            {
                status = "timeout";
            }
            catch (Exception ex)
            {
                status = $"error:{ex.GetType().Name}";
                responseObj = new { error = ex.Message };
            }
        }

        // Close
        if (closeAfterSend == "true" && cws.State == WebSocketState.Open)
        {
            try
            {
                using var ctsClose = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await cws.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", ctsClose.Token);
            }
            catch
            {
                // yut
            }
        }

        sw.Stop();

        return new NodeResult
        {
            Outputs =
            {
                ["ack"] = new
                {
                    url,
                    sentBytes = Encoding.UTF8.GetByteCount(message ?? ""),
                    subprotocol = cws.SubProtocol,
                    connected = true,
                    elapsedMs = sw.ElapsedMilliseconds
                },
                ["response"] = responseObj,
                ["status"] = status,
                ["rttMs"] = sw.ElapsedMilliseconds
            }
        };
    }

    private static bool Truthy(object? o)
    {
        return o switch
        {
            null => false,
            bool b => b,
            string s => s.Equals("true", StringComparison.OrdinalIgnoreCase) || s == "1" || s.Equals("yes", StringComparison.OrdinalIgnoreCase),
            JsonElement je when je.ValueKind == JsonValueKind.True => true,
            JsonElement je when je.ValueKind == JsonValueKind.String =>
                Truthy(je.GetString()),
            _ => false
        };
    }

    private static int GetInt(Dictionary<string, object?> dict, string key, int def)
    {
        if (!dict.TryGetValue(key, out var v) || v is null) return def;
        return v switch
        {
            int i => i,
            long l => (int)l,
            string s when int.TryParse(s, out var n) => n,
            JsonElement je when je.ValueKind == JsonValueKind.Number && je.TryGetInt32(out var n) => n,
            _ => def
        };
    }

    private static object? TryJson(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        try { return JsonSerializer.Deserialize<object?>(text); }
        catch { return null; }
    }

    private static void SafeAbort(ClientWebSocket cws)
    {
        try { cws.Abort(); } catch { /* ignore */ }
    }

    private static NodeResult TimeoutResult(string phase, long elapsedMs)
    {
        return new NodeResult
        {
            Outputs =
            {
                ["ack"] = new { connected = false, phase, elapsedMs },
                ["status"] = "timeout",
                ["response"] = null,
                ["rttMs"] = elapsedMs
            }
        };
    }

    private static NodeResult ErrorResult(string phase, Exception ex, long elapsedMs)
    {
        return new NodeResult
        {
            Outputs =
            {
                ["ack"] = new { connected = false, phase, error = ex.Message, elapsedMs },
                ["status"] = "error",
                ["response"] = new { error = ex.Message, type = ex.GetType().Name },
                ["rttMs"] = elapsedMs
            }
        };
    }


}

sealed class EmailSendNode : INodeProcessor
{
    public string Type => "emailSend";
    public Task<NodeResult> ExecuteAsync(NodeExecutionArgs args)
    {
        // Demo/stub: gerçek SMTP sağlayıcı yoksa 202 dön.
        var to = args.Node.Settings.TryGetValue("to", out var t) ? t?.ToString() : "";
        var subject = args.Node.Settings.TryGetValue("subject", out var s) ? s?.ToString() : "";
        var body = args.Node.Settings.TryGetValue("body", out var b) ? b?.ToString() : (args.Inputs.TryGetValue("body", out var ib) ? ib?.ToString() : "");
        var status = 202;
        return Task.FromResult(new NodeResult { Outputs = { ["status"] = status } });
    }
}

/* =======================
 * AI (stubs)
 * ======================= */

sealed class OpenAiChatNode : INodeProcessor
{
    public string Type => "openaiChat";
    public Task<NodeResult> ExecuteAsync(NodeExecutionArgs args)
    {
        var model = args.Node.Settings.TryGetValue("model", out var m) ? m?.ToString() : "gpt-4o-mini";
        var prompt = args.Node.Settings.TryGetValue("prompt", out var p) ? p?.ToString() : (args.Inputs.TryGetValue("prompt", out var ip) ? ip?.ToString() : "");
        // Stub: gerçek çağrı yok → response’u echo yapalım
        var response = $"[stub:{model}] {prompt}";
        return Task.FromResult(new NodeResult { Outputs = { ["response"] = response } });
    }
}

sealed class TtsNode : INodeProcessor
{
    public string Type => "tts";
    public Task<NodeResult> ExecuteAsync(NodeExecutionArgs args)
    {
        var voice = args.Node.Settings.TryGetValue("voice", out var v) ? v?.ToString() : "tr-TR";
        var text = args.Inputs.TryGetValue("text", out var t) ? t?.ToString() ?? "" : "";
        var audio = new { voice, text, bytes = Array.Empty<byte>() }; // stub
        return Task.FromResult(new NodeResult { Outputs = { ["audio"] = audio } });
    }
}

sealed class SttNode : INodeProcessor
{
    public string Type => "stt";
    public Task<NodeResult> ExecuteAsync(NodeExecutionArgs args)
    {
        var lang = args.Node.Settings.TryGetValue("language", out var l) ? l?.ToString() : "tr-TR";
        // stub: "audio" objesini "text"e çeviriyormuş gibi
        var text = $"[stub:stt {lang}]";
        return Task.FromResult(new NodeResult { Outputs = { ["text"] = text } });
    }
}

/* =======================
 * STORAGE / FILE
 * ======================= */

sealed class ReadFileNode : INodeProcessor
{
    public string Type => "readFile";
    public async Task<NodeResult> ExecuteAsync(NodeExecutionArgs args)
    {
        var path = args.Node.Settings.TryGetValue("path", out var p) ? p?.ToString() ?? "" : "";
        var encoding = args.Node.Settings.TryGetValue("encoding", out var e) ? e?.ToString() ?? "utf-8" : "utf-8";
        var enc = encoding.ToLower().Contains("utf") ? Encoding.UTF8 : Encoding.UTF8;
        var content = System.IO.File.Exists(path) ? await System.IO.File.ReadAllTextAsync(path, enc, args.Context.Cancellation.Value) : "";
        return new NodeResult { Outputs = { ["content"] = content } };
    }
}

sealed class WriteFileNode : INodeProcessor
{
    public string Type => "writeFile";
    public async Task<NodeResult> ExecuteAsync(NodeExecutionArgs args)
    {
        var path = args.Node.Settings.TryGetValue("path", out var p) ? p?.ToString() ?? "" : "";
        var apppath = AppDomain.CurrentDomain.BaseDirectory + path.Replace("/", "\\");

        if (!Directory.Exists(Path.GetDirectoryName(apppath)))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(apppath));
        }
        var encoding = args.Node.Settings.TryGetValue("encoding", out var e) ? e?.ToString() ?? "utf-8" : "utf-8";
        var enc = encoding.ToLower().Contains("utf") ? Encoding.UTF8 : Encoding.UTF8;
        var content = args.Inputs.TryGetValue("content", out var c) ? c?.ToString() ?? "" : "";
        await System.IO.File.WriteAllTextAsync(apppath, content, enc, args.Context.Cancellation.Value);
        return new NodeResult { Outputs = { ["success"] = true, ["file"] = apppath } };
    }
}

/* =======================
 * CACHE
 * ======================= */

sealed class CacheSetNode : INodeProcessor
{
    public string Type => "cacheSet";
    private static readonly ConcurrentDictionary<string, (object? Value, DateTimeOffset? Exp)> Cache = new();

    public Task<NodeResult> ExecuteAsync(NodeExecutionArgs args)
    {
        var key = args.Node.Settings.TryGetValue("key", out var k) ? k?.ToString() ?? "" : "";
        var ttl = args.Node.Settings.TryGetValue("ttlSec", out var t) && int.TryParse(t?.ToString(), out var s) ? s : 300;
        var value = args.Inputs.TryGetValue("value", out var v) ? v : null;

        var exp = ttl > 0 ? DateTimeOffset.UtcNow.AddSeconds(ttl) : DateTimeOffset.UtcNow;
        Cache[key] = (value, exp);
        return Task.FromResult(new NodeResult { Outputs = { ["success"] = true } });
    }

    public static (bool Found, object? Value) TryGet(string key)
    {
        if (Cache.TryGetValue(key, out var item))
        {
            if (item.Exp is { } e && e < DateTimeOffset.UtcNow)
            {
                Cache.TryRemove(key, out _);
                return (false, null);
            }
            return (true, item.Value);
        }
        return (false, null);
    }
}

sealed class CacheGetNode : INodeProcessor
{
    public string Type => "cacheGet";
    public Task<NodeResult> ExecuteAsync(NodeExecutionArgs args)
    {
        var key = args.Node.Settings.TryGetValue("key", out var k) ? k?.ToString() ?? "" : "";
        var (found, value) = CacheSetNode.TryGet(key);
        return Task.FromResult(new NodeResult { Outputs = { ["value"] = value, ["found"] = found } });
    }
}

/* =======================
 * TRIGGERS (event-driven)
 * Not: Gerçek dinleme Host/Minimal API tarafında. Burada seed edilen veriyi
 *      “passthrough” olarak çıkışlara yansıtırız.
 * ======================= */

sealed class HttpListenNode : INodeProcessor
{
    public string Type => "httpListen";

    public Task<NodeResult> ExecuteAsync(NodeExecutionArgs args)
    {
        // 1) Öncelik: gelen inputs
        if (!args.Inputs.TryGetValue("request", out var req))
            req = null;
        if (!args.Inputs.TryGetValue("context", out var ctxVal))
            ctxVal = null;

        // 2) Inputs boşsa, pre-seed edilmiş Context.Outputs'u kullan
        if (req is null || ctxVal is null)
        {
            if (args.Context.Outputs.TryGetValue(args.Node.Id, out var seeded))
            {
                if (req is null && seeded.TryGetValue("request", out var r2)) req = r2;
                if (ctxVal is null && seeded.TryGetValue("context", out var c2)) ctxVal = c2;
            }
        }

        // 3) Yine de null kalırsa boş obje koy
        req ??= new { };
        ctxVal ??= new { };

        return Task.FromResult(new NodeResult
        {
            Outputs =
            {
                ["request"] = req,
                ["context"] = ctxVal
            }
        });
    }
}
sealed class WebhookInNode : INodeProcessor
{
    public string Type => "webhookIn";
    public Task<NodeResult> ExecuteAsync(NodeExecutionArgs args)
    {
        var ev = args.Inputs.TryGetValue("event", out var e) ? e : new { };
        var raw = args.Inputs.TryGetValue("raw", out var r) ? r : new { };
        return Task.FromResult(new NodeResult { Outputs = { ["event"] = ev, ["raw"] = raw } });
    }
}

sealed class WebsocketInNode : INodeProcessor
{
    public string Type => "websocketIn";
    public Task<NodeResult> ExecuteAsync(NodeExecutionArgs args)
    {
        var msg = args.Inputs.TryGetValue("message", out var m) ? m : null;
        var meta = args.Inputs.TryGetValue("meta", out var me) ? me : null;
        return Task.FromResult(new NodeResult { Outputs = { ["message"] = msg, ["meta"] = meta } });
    }
}

sealed class SseSubscribeNode : INodeProcessor
{
    public string Type => "sseSubscribe";
    public Task<NodeResult> ExecuteAsync(NodeExecutionArgs args)
    {
        var ev = args.Inputs.TryGetValue("event", out var e) ? e : null;
        var data = args.Inputs.TryGetValue("data", out var d) ? d : null;
        return Task.FromResult(new NodeResult { Outputs = { ["event"] = ev, ["data"] = data } });
    }
}

sealed class CronNode : INodeProcessor
{
    public string Type => "cron";
    public Task<NodeResult> ExecuteAsync(NodeExecutionArgs args)
    {
        // Engine içinde periyodik tetik yok; seed veya manual run ile “tick”
        var tick = new { now = DateTimeOffset.UtcNow };
        return Task.FromResult(new NodeResult { Outputs = { ["tick"] = tick } });
    }
}

sealed class IntervalTimerNode : INodeProcessor
{
    public string Type => "intervalTimer";
    public Task<NodeResult> ExecuteAsync(NodeExecutionArgs args)
    {
        var tick = new { now = DateTimeOffset.UtcNow };
        return Task.FromResult(new NodeResult { Outputs = { ["tick"] = tick } });
    }
}

sealed class HttpPollerNode : INodeProcessor
{
    public string Type => "httpPoller";
    private readonly HttpClient _http;
    public HttpPollerNode(HttpClient http) => _http = http;

    public async Task<NodeResult> ExecuteAsync(NodeExecutionArgs args)
    {
        // Bir defalık poll (engine run başına)
        var s = args.Node.Settings;
        var url = s.TryGetValue("url", out var u) ? u?.ToString() ?? "" : "";
        var method = s.TryGetValue("method", out var m) ? m?.ToString() ?? "GET" : "GET";
        var parse = s.TryGetValue("parse", out var p) ? p?.ToString() ?? "json" : "json";

        using var req = new HttpRequestMessage(new HttpMethod(method), url);
        if (s.TryGetValue("headers", out var h) && h is JsonElement he && he.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in he.EnumerateObject())
                req.Headers.TryAddWithoutValidation(prop.Name, prop.Value.ToString());
        }

        var resp = await _http.SendAsync(req, args.Context.Cancellation.Value);
        var text = await resp.Content.ReadAsStringAsync(args.Context.Cancellation.Value);

        object? parsed = text;
        if (parse.Equals("json", StringComparison.OrdinalIgnoreCase))
            try { parsed = JsonSerializer.Deserialize<object?>(text); } catch { parsed = text; }

        return new NodeResult { Outputs = { ["data"] = parsed, ["status"] = (int)resp.StatusCode } };
    }
}

sealed class FileWatchNode : INodeProcessor
{
    public string Type => "fileWatch";
    public Task<NodeResult> ExecuteAsync(NodeExecutionArgs args)
    {
        // Seed ile “change” geldiyse aktar
        var change = args.Inputs.TryGetValue("change", out var ch) ? ch : null;
        return Task.FromResult(new NodeResult { Outputs = { ["change"] = change } });
    }
}

sealed class DirectoryWatchNode : INodeProcessor
{
    public string Type => "directoryWatch";
    public Task<NodeResult> ExecuteAsync(NodeExecutionArgs args)
    {
        var file = args.Inputs.TryGetValue("file", out var f) ? f : null;
        return Task.FromResult(new NodeResult { Outputs = { ["file"] = file } });
    }
}

sealed class MqttSubscribeNode : INodeProcessor
{
    public string Type => "mqttSubscribe";
    public Task<NodeResult> ExecuteAsync(NodeExecutionArgs args)
    {
        var message = args.Inputs.TryGetValue("message", out var m) ? m : null;
        var topic = args.Inputs.TryGetValue("topic", out var t) ? t : null;
        return Task.FromResult(new NodeResult { Outputs = { ["message"] = message, ["topic"] = topic } });
    }
}

sealed class QueueConsumeNode : INodeProcessor
{
    public string Type => "queueConsume";
    public Task<NodeResult> ExecuteAsync(NodeExecutionArgs args)
    {
        var message = args.Inputs.TryGetValue("message", out var m) ? m : null;
        var meta = args.Inputs.TryGetValue("meta", out var me) ? me : null;
        return Task.FromResult(new NodeResult { Outputs = { ["message"] = message, ["meta"] = meta } });
    }
}

sealed class SmtpInboundNode : INodeProcessor
{
    public string Type => "smtpInbound";
    public Task<NodeResult> ExecuteAsync(NodeExecutionArgs args)
    {
        var email = args.Inputs.TryGetValue("email", out var e) ? e : null;
        return Task.FromResult(new NodeResult { Outputs = { ["email"] = email } });
    }
}

sealed class KeyboardShortcutNode : INodeProcessor
{
    public string Type => "keyboardShortcut";
    public Task<NodeResult> ExecuteAsync(NodeExecutionArgs args)
    {
        var pressed = args.Inputs.TryGetValue("pressed", out var p) ? p : null;
        return Task.FromResult(new NodeResult { Outputs = { ["pressed"] = pressed } });
    }
}

sealed class ManualTriggerNode : INodeProcessor
{
    public string Type => "manualTrigger";
    public Task<NodeResult> ExecuteAsync(NodeExecutionArgs args)
    {
        var fired = args.Inputs.TryGetValue("fired", out var f) ? f : new { at = DateTimeOffset.UtcNow };
        return Task.FromResult(new NodeResult { Outputs = { ["fired"] = fired } });
    }
}

sealed class OnStartNode : INodeProcessor
{
    public string Type => "onStart";
    public Task<NodeResult> ExecuteAsync(NodeExecutionArgs args)
    {
        var boot = new { at = DateTimeOffset.UtcNow, once = args.Node.Settings.TryGetValue("once", out var o) && o is JsonElement je && je.ValueKind == JsonValueKind.True };
        return Task.FromResult(new NodeResult { Outputs = { ["boot"] = boot } });
    }
}

sealed class WebhookVerifyNode : INodeProcessor
{
    public string Type => "webhookVerify";
    public Task<NodeResult> ExecuteAsync(NodeExecutionArgs args)
    {
        var secret = args.Node.Settings.TryGetValue("secret", out var s) ? s?.ToString() ?? "" : "";
        var headerName = args.Node.Settings.TryGetValue("header", out var h) ? h?.ToString() ?? "X-Hub-Signature-256" : "X-Hub-Signature-256";

        // Inputs:
        //   event.headers[headerName]
        //   raw.text
        var headers = TryDict(args.Inputs.TryGetValue("event", out var ev) ? ev : null, "headers");
        var rawText = TryString(args.Inputs.TryGetValue("raw", out var raw) ? raw : null, "text");

        bool verified = false;
        if (!string.IsNullOrWhiteSpace(secret) && rawText is not null && headers is not null && headers.TryGetValue(headerName, out var sigObj))
        {
            var signature = sigObj?.ToString() ?? "";
            using var hmac = new System.Security.Cryptography.HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hash = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(rawText))).ToLowerInvariant();
            verified = signature.ToLowerInvariant().Contains(hash);
        }

        var res = new NodeResult();
        if (verified) res.Outputs["verified"] = new { ok = true };
        else res.Outputs["failed"] = new { ok = false };

        return Task.FromResult(res);

        static Dictionary<string, object?>? TryDict(object? obj, string key)
        {
            if (obj is null) return null;
            if (obj is IDictionary<string, object?> d) return new(d, StringComparer.OrdinalIgnoreCase);
            if (obj is JsonElement je)
            {
                if (je.ValueKind == JsonValueKind.Object && je.TryGetProperty(key, out var p))
                {
                    return JsonSerializer.Deserialize<Dictionary<string, object?>>(p.GetRawText());
                }
            }
            return null;
        }

        static string? TryString(object? obj, string key)
        {
            if (obj is null) return null;
            if (obj is string s) return s;
            if (obj is IDictionary<string, object?> d && d.TryGetValue(key, out var v)) return v?.ToString();
            if (obj is JsonElement je)
            {
                if (je.ValueKind == JsonValueKind.String) return je.GetString();
                if (je.ValueKind == JsonValueKind.Object && je.TryGetProperty(key, out var p) && p.ValueKind == JsonValueKind.String) return p.GetString();
            }
            return null;
        }
    }
}

sealed class BluetoothNotifyNode : INodeProcessor
{
    public string Type => "bluetoothNotify";
    public Task<NodeResult> ExecuteAsync(NodeExecutionArgs args)
    {
        var data = args.Inputs.TryGetValue("data", out var d) ? d : Array.Empty<byte>();
        return Task.FromResult(new NodeResult { Outputs = { ["data"] = data } });
    }
}

sealed class GeoFenceNode : INodeProcessor
{
    public string Type => "geoFence";
    public Task<NodeResult> ExecuteAsync(NodeExecutionArgs args)
    {
        var loc = args.Inputs.TryGetValue("location", out var l) ? l : null;
        return Task.FromResult(new NodeResult { Outputs = { ["location"] = loc } });
    }
}

sealed class BatteryLevelNode : INodeProcessor
{
    public string Type => "batteryLevel";
    public Task<NodeResult> ExecuteAsync(NodeExecutionArgs args)
    {
        var status = args.Inputs.TryGetValue("status", out var s) ? s : null;
        return Task.FromResult(new NodeResult { Outputs = { ["status"] = status } });
    }
}

sealed class ClipboardChangeNode : INodeProcessor
{
    public string Type => "clipboardChange";
    public Task<NodeResult> ExecuteAsync(NodeExecutionArgs args)
    {
        var text = args.Inputs.TryGetValue("text", out var t) ? t : null;
        return Task.FromResult(new NodeResult { Outputs = { ["text"] = text } });
    }
}

/* =======================
 * REGISTRY
 * ======================= */

public sealed class DefaultNodeRegistry : INodeRegistry
{
    private readonly Dictionary<string, INodeProcessor> _map;

    public DefaultNodeRegistry(HttpClient http)
    {
        _map = new(StringComparer.OrdinalIgnoreCase)
        {
            // Control
            ["if"] = new IfNode(),
            ["switch"] = new SwitchNode(),
            ["loop"] = new LoopNode(),
            ["delay"] = new DelayNode(),
            ["tryCatch"] = new TryCatchNode(),

            // Data
            ["jsonTransform"] = new JsonTransformNode(),
            ["arrayMap"] = new ArrayMapNode(),
            ["arrayFilter"] = new ArrayFilterNode(),
            ["mathOp"] = new MathOpNode(),
            ["stringConcat"] = new StringConcatNode(),
            ["stringTemplate"] = new StringTemplateNode(),
            ["regexExtract"] = new RegexExtractNode(),
            ["jsonMerge"] = new JsonMergeNode(),

            // Network
            ["httpRequest"] = new HttpRequestNode(http),
            ["websocket"] = new WebsocketSendNode(),
            ["emailSend"] = new EmailSendNode(),

            // AI (stubs)
            ["openaiChat"] = new OpenAiChatNode(),
            ["tts"] = new TtsNode(),
            ["stt"] = new SttNode(),

            // Storage / Cache
            ["readFile"] = new ReadFileNode(),
            ["writeFile"] = new WriteFileNode(),
            ["cacheGet"] = new CacheGetNode(),
            ["cacheSet"] = new CacheSetNode(),

            // Triggers (passthrough)
            ["httpListen"] = new HttpListenNode(),
            ["webhookIn"] = new WebhookInNode(),
            ["websocketIn"] = new WebsocketInNode(),
            ["sseSubscribe"] = new SseSubscribeNode(),
            ["cron"] = new CronNode(),
            ["intervalTimer"] = new IntervalTimerNode(),
            ["httpPoller"] = new HttpPollerNode(http),
            ["fileWatch"] = new FileWatchNode(),
            ["directoryWatch"] = new DirectoryWatchNode(),
            ["mqttSubscribe"] = new MqttSubscribeNode(),
            ["queueConsume"] = new QueueConsumeNode(),
            ["smtpInbound"] = new SmtpInboundNode(),
            ["keyboardShortcut"] = new KeyboardShortcutNode(),
            ["manualTrigger"] = new ManualTriggerNode(),
            ["onStart"] = new OnStartNode(),
            ["webhookVerify"] = new WebhookVerifyNode(),
            ["bluetoothNotify"] = new BluetoothNotifyNode(),
            ["geoFence"] = new GeoFenceNode(),
            ["batteryLevel"] = new BatteryLevelNode(),
            ["clipboardChange"] = new ClipboardChangeNode(),
        };
    }

    public INodeProcessor Resolve(string nodeType)
        => _map.TryGetValue(nodeType, out var p)
            ? p
            : throw new InvalidOperationException($"Processor not found for type '{nodeType}'");
}
