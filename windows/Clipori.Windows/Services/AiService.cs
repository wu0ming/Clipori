using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Clipori.Windows.Models;

namespace Clipori.Windows.Services;

internal static class AiService
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(45) };

    public static async Task<string> AskAsync(UserSettings settings, string question, IEnumerable<ClipEntry> source, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(settings.ApiKey) || string.IsNullOrWhiteSpace(settings.AiModel))
            throw new InvalidOperationException("请先在设置中配置模型名称和 API Key。");
        var allowed = source.Where(e => e.Kind != "image" && e.Kind != "files").Take(35)
            .Select((e, i) => $"{i + 1}. [{e.TypeLabel}] {e.Title.TakeChars(100)}: {e.Text.TakeChars(500)}");
        string prompt = "以下是用户主动选择用于本次检索的本地剪贴板文本片段。根据问题找到相关内容，引用编号；若无关联如实说明。\n" +
                        string.Join("\n", allowed) + "\n\n用户问题：" + question;
        return await CompleteAsync(settings, prompt, cancellationToken);
    }

    public static async Task<string> SuggestCategoryAsync(UserSettings settings, ClipEntry entry,
        IReadOnlyCollection<string> allowedCategories, CancellationToken cancellationToken = default)
    {
        if (allowedCategories.Count == 0 || entry.Kind == "image" || entry.Kind == "files") return "";
        string prompt = "你是剪贴板分类器。仅输出一个匹配分类的名字；如果没有合适分类，输出 NONE。候选分类：" +
                        string.Join("、", allowedCategories) + "\n待分类文本：" + entry.Text.TakeChars(900);
        string response = (await CompleteAsync(settings, prompt, cancellationToken)).Trim();
        return allowedCategories.FirstOrDefault(c => response.Equals(c, StringComparison.OrdinalIgnoreCase)) ?? "";
    }

    private static async Task<string> CompleteAsync(UserSettings settings, string prompt, CancellationToken token)
    {
        string endpoint = settings.AiEndpoint.TrimEnd('/');
        bool anthropic = settings.AiProtocol.Equals("Anthropic", StringComparison.OrdinalIgnoreCase);
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var baseUri) || baseUri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("AI Endpoint 必须是 HTTPS 地址。");
        string path = anthropic ? "/v1/messages" : "/chat/completions";
        if (!anthropic && endpoint.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)) path = "/chat/completions";
        else if (!anthropic) path = "/v1/chat/completions";
        if (anthropic && endpoint.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)) path = "/messages";
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint + path);
        if (anthropic)
        {
            request.Headers.TryAddWithoutValidation("x-api-key", settings.ApiKey);
            request.Headers.TryAddWithoutValidation("anthropic-version", "2023-06-01");
            request.Content = JsonContent(new { model = settings.AiModel, max_tokens = 1000, messages = new[] { new { role = "user", content = prompt } } });
        }
        else
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
            request.Content = JsonContent(new { model = settings.AiModel, messages = new[] { new { role = "user", content = prompt } }, temperature = 0.2 });
        }
        using var response = await Client.SendAsync(request, token);
        string body = await response.Content.ReadAsStringAsync(token);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException("AI 服务返回 " + (int)response.StatusCode + "；请检查模型和 API 地址。");
        using var json = JsonDocument.Parse(body);
        if (anthropic)
            return json.RootElement.GetProperty("content")[0].GetProperty("text").GetString() ?? "";
        return json.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
    }
    private static StringContent JsonContent(object value) =>
        new(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");

    private static string TakeChars(this string str, int max) => str.Length <= max ? str : str[..max];
}
