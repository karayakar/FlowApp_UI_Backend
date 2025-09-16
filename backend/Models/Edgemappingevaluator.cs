

using System.Text.Json;

public static class EdgeMappingEvaluator
{
    public static object Apply(object source, EdgeMapping mapping)
    {
        var target = mapping.TargetTemplate != null
            ? DeepClone(mapping.TargetTemplate)
            : new Dictionary<string, object?>();

        foreach (var rule in mapping.Rules)
        {
            object? value = null;
            switch (rule.Language)
            {
                case MappingLanguage.JmesPath:
                    value = EvaluateJmesPath(source, rule.Expression);
                    break;
                case MappingLanguage.Handlebars:
                    value = EvaluateHandlebars(source, rule.Expression);
                    break;
                default:
                    throw new NotSupportedException("Unsupported language");
            }
            SetByPath(ref target, rule.TargetPath, value);
        }

        return target;
    }

    static object? EvaluateJmesPath(object source, string expr)
    {
        var engine = new DevLab.JmesPath.JmesPath(); // jmespath.net
        var json = JsonSerializer.Serialize(source);
        var result = engine.Transform(json, expr); // returns JSON string
        return JsonSerializer.Deserialize<object?>(result);
    }

    static object EvaluateHandlebars(object source, string template)
    {
        var compiled = HandlebarsDotNet.Handlebars.Compile(template);
        var rendered = compiled(source);
        // string döndürür; hedef türü string ise direkt yaz, değilse JSON parse dene:
        if (LooksLikeJson(rendered))
            return JsonSerializer.Deserialize<object>(rendered) ?? rendered;
        return rendered;
    }

    static bool LooksLikeJson(string s) => s.TrimStart().StartsWith("{") || s.TrimStart().StartsWith("[");

    // basit SetByPath – hedefi (Dictionary/List) üstünden yürüt
    static void SetByPath(ref object target, string path, object? value)
    {
        // pratik uygulama: Newtonsoft kullanmak kolaydır; burada özet bırakıyorum
        var j = Newtonsoft.Json.Linq.JToken.FromObject(target ?? new { });
        var parts = System.Text.RegularExpressions.Regex.Replace(path, @"\[(\d+)\]", @".$1").Split('.');
        var cur = j;
        for (int i = 0; i < parts.Length - 1; i++)
        {
            var k = parts[i];
            if (string.IsNullOrEmpty(k)) continue;
            var next = cur[k];
            if (next == null || next.Type == Newtonsoft.Json.Linq.JTokenType.Null)
            {
                // bir sonraki parça sayı mı?
                var nextKey = parts[i + 1];
                cur[k] = int.TryParse(nextKey, out _) ? new Newtonsoft.Json.Linq.JArray() : new Newtonsoft.Json.Linq.JObject();
                next = cur[k];
            }
            cur = next!;
        }
        var last = parts[^1];
        cur[last] = value != null ? Newtonsoft.Json.Linq.JToken.FromObject(value) : Newtonsoft.Json.Linq.JValue.CreateNull();
        target = j.ToObject<object>()!;
    }

    static T DeepClone<T>(T v) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(v))!;
}
