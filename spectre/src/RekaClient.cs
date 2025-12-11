using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using DotNetEnv;

namespace SpectreCaptionThis;

public enum CaptionPromptType
{
    Detailed,
    Short
}

public class RekaClient
{
    private readonly HttpClient _client = new();
    private readonly string _apiKey;
    private readonly string _baseUrl = "https://vision-agent.api.reka.ai";

    public RekaClient()
    {
        Env.TraversePath().Load();
        _apiKey = Environment.GetEnvironmentVariable("API_KEY") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            throw new InvalidOperationException("API Key is required. Set API_KEY in your environment.");
        }
    }

    public async Task<HttpResponseMessage> ListVideosAsync()
        => await SendRequestAsync("/videos/list", new { video_ids = Array.Empty<string>() });

    public async Task<HttpResponseMessage> DeleteVideoAsync(string videoId)
        => await SendRequestAsync("/videos/delete", new { video_ids = new[] { videoId } });

    public async Task<HttpResponseMessage> ListImagesAsync()
        => await SendRequestAsync("/images/list", new { image_ids = Array.Empty<string>() });

    public async Task<HttpResponseMessage> CaptionVideoAsync(string videoId, CaptionPromptType promptType)
    {
        string prompt = promptType == CaptionPromptType.Detailed
            ? "write a text description that could be used to recreate this video as accurately as possible using an AI video generation model. Include details about: the video (aspect ratio, composition, style, motion, pacing, type of lighting, camera point of view, it's position related to the subject), objects descriptions (colors, location), and a description of what is happening and the interactions between objects in the video."
            : "Write a short description of this video in 2-3 sentences.";

        var requestBody = new
        {
            video_id = videoId,
            messages = new[]
            {
                new { role = "user", content = prompt }
            }
        };
        return await SendRequestAsync("/qa/chat", requestBody);
    }

    public async Task<(HttpResponseMessage response, string? caption)> CaptionImageAsync(string imageUrl, CaptionPromptType promptType)
    {
        string promptText = promptType == CaptionPromptType.Detailed
            ? "Write a text description that could be used to recreate this image as accurately as possible using an AI image generation model. Include details about: the image (aspect ratio, composition, style, type of lighting), objects descriptions (colors, location, textures), and a description of what is happening and the interactions between objects or subjects in the image."
            : "Describe this image in detail. Include style. In 1-2 sentences, 50 words or less.";

        var requestBody = new
        {
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "image_url", image_url = imageUrl },
                        new { type = "text", text = promptText }
                    }
                }
            },
            model = "reka-flash"
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.reka.ai/v1/chat/completions") { Content = content };
        request.Headers.Add("X-Api-Key", _apiKey);

        var response = await _client.SendAsync(request);
        string body = await response.Content.ReadAsStringAsync();
        try
        {
            var responseJson = JsonDocument.Parse(body);
            if (responseJson.RootElement.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array)
            {
                var first = choices.EnumerateArray().FirstOrDefault();
                if (first.TryGetProperty("message", out var message) && message.TryGetProperty("content", out var contentText))
                {
                    return (response, contentText.GetString());
                }
            }
        }
        catch { }
        return (response, null);
    }

    public async Task<HttpResponseMessage> UploadVideoAsync(string filePath)
    {
        string fileName = Path.GetFileName(filePath);
        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(await File.ReadAllBytesAsync(filePath));
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("video/mp4");
        form.Add(fileContent, "file", fileName);
        form.Add(new StringContent(fileName), "video_name");
        form.Add(new StringContent("true"), "index");

        var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/videos/upload") { Content = form };
        request.Headers.Add("X-Api-Key", _apiKey);
        return await _client.SendAsync(request);
    }

    public async Task<HttpResponseMessage> UploadPhotoAsync(string filePath)
    {
        string fileName = Path.GetFileName(filePath);
        string contentType = filePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "image/png" : "image/jpeg";

        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(await File.ReadAllBytesAsync(filePath));
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        form.Add(fileContent, "images", fileName);

        var metadata = new
        {
            requests = new[]
            {
                new { indexing_config = new { index = true }, metadata = new { } }
            }
        };
        var metadataJson = JsonSerializer.Serialize(metadata);
        form.Add(new StringContent(metadataJson), "metadata");

        var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/images/upload") { Content = form };
        request.Headers.Add("X-Api-Key", _apiKey);
        return await _client.SendAsync(request);
    }

    public static async Task SaveToFileAsync(string content, string fileName)
    {
        var dataFolder = Path.Combine(AppContext.BaseDirectory, "data");
        Directory.CreateDirectory(dataFolder);
        var filePath = Path.Combine(dataFolder, fileName);
        await File.WriteAllTextAsync(filePath, content);
    }

    private async Task<HttpResponseMessage> SendRequestAsync(string endpoint, object body)
    {
        var json = JsonSerializer.Serialize(body);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}{endpoint}") { Content = content };
        request.Headers.Add("X-Api-Key", _apiKey);
        return await _client.SendAsync(request);
    }

    public static string FormatJson(string raw)
    {
        try
        {
            var json = JsonDocument.Parse(raw);
            return JsonSerializer.Serialize(json.RootElement, new JsonSerializerOptions { WriteIndented = true });
        }
        catch
        {
            return raw;
        }
    }
}
