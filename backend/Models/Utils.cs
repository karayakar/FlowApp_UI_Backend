using System.Text.Json;

using DevLab.JmesPath;

using HandlebarsDotNet;




//static class JsonUtil
//{
//    public static object? SetByPath(object? root, string path, object? value)
//    {
//        if (root is null) root = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
//        if (string.IsNullOrWhiteSpace(path)) return value;

//        var parts = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
//        object? cursor = root;
//        for (int i = 0; i < parts.Length; i++)
//        {
//            var key = parts[i];

//            if (i == parts.Length - 1)
//            {
//                if (cursor is IDictionary<string, object?> dict)
//                {
//                    dict[key] = value;
//                    return root;
//                }
//                throw new InvalidOperationException("Target path is not an object to set property.");
//            }
//            else
//            {
//                if (cursor is IDictionary<string, object?> dict)
//                {
//                    if (!dict.TryGetValue(key, out var next) || next is null)
//                    {
//                        next = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
//                        dict[key] = next;
//                    }
//                    cursor = next;
//                }
//                else
//                {
//                    throw new InvalidOperationException("Intermediate path is not an object.");
//                }
//            }
//        }
//        return root;
//    }
//}

//sealed class Jmes
//{
//    private readonly JmesPath _jp = new();
//    public object? Eval(object? input, string expr)
//    {
//        if (input is null) return null;
//        var json = JsonSerializer.Serialize(input);
//        var result = _jp.Transform(json, expr);
//        return string.IsNullOrWhiteSpace(result) ? null : JsonSerializer.Deserialize<object?>(result);
//    }
//}

//sealed class Hbs
//{
//    public string Render(string template, object? model)
//    {
//        var t = Handlebars.Compile(template);
//        return t(model ?? new { });
//    }
//}
