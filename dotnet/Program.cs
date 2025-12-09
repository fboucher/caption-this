using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.IO;
using System.Net.Http.Headers;
using DotNetEnv;

class Program
{
    private static readonly HttpClient client = new();
    private static string apiKey = "";
    private static readonly string baseUrl = "https://vision-agent.api.reka.ai";
    
    static async Task Main(string[] args)
    {
        Env.TraversePath().Load();
        apiKey = Environment.GetEnvironmentVariable("API_KEY")!;

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Console.WriteLine("API Key is required. Exiting...");
            return;
        }

        bool running = true;
        while (running)
        {
            DisplayMenu();
            Console.Write("\nSelect an option (1-7): ");
            string? choice = Console.ReadLine();

            switch (choice)
            {
                case "1":
                    await ListVideos();
                    break;
                case "2":
                    await CaptionVideo();
                    break;
                case "3":
                    await UploadVideo();
                    break;
                case "4":
                    await ListImages();
                    break;
                // case "5":
                //     await CaptionImage();
                //     break;
                case "6":
                    await UploadPhoto();
                    break;
                case "7":
                    await DeleteVideo();
                    break;
                case "x":
                    running = false;
                    Console.WriteLine("\nGoodbye!");
                    break;
                default:
                    Console.WriteLine("\nInvalid option. Please try again.");
                    break;
            }

            if (running)
            {
                Console.WriteLine("\nPress Enter to continue...");
                Console.ReadLine();
                Console.Clear();
            }
        }
    }



    static void DisplayMenu()
    {
        Console.WriteLine("\n╔════════════════════════════════════════╗");
        Console.WriteLine("║         Caption This - Main Menu       ║");
        Console.WriteLine("╠════════════════════════════════════════╣");
        Console.WriteLine("║ 1. List videos                         ║");
        Console.WriteLine("║ 2. Caption a video by ID               ║");
        Console.WriteLine("║ 3. Upload a video                      ║");
        Console.WriteLine("║  ------------------------------------  ║");
        // Console.WriteLine("║ 4. List images                         ║");
        // Console.WriteLine("║ 5. Caption a image by ID               ║");
        // Console.WriteLine("║ 6. Upload a image                      ║");
        // Console.WriteLine("║  ------------------------------------  ║");
        Console.WriteLine("║ 7. Delete a video by ID                ║");
        Console.WriteLine("║  ------------------------------------  ║");
        Console.WriteLine("║ x. Exit                                ║");
        Console.WriteLine("╚════════════════════════════════════════╝");
    }

    static async Task ListVideos()
    {
        Console.WriteLine("\n📹 Getting all videos in library...\n");
        var response = await SendRequest("/videos/list", new { video_ids = Array.Empty<string>() });
        await DisplayVideosResponse(response);
    }

    // static async Task CaptionImage()
    // {
    //     Console.WriteLine("\n🎬 Caption a image by ID\n");
    //     Console.Write("Enter image ID: ");
    //     string? imageId = Console.ReadLine();

    //     if (string.IsNullOrWhiteSpace(imageId))
    //     {
    //         Console.WriteLine("Video ID is required.");
    //         return;
    //     }

    //     var requestBody = new
    //     {
    //         image_id = imageId,
    //         messages = new[]
    //         {
    //             new
    //             {
    //                 role = "user",
    //                 content = "Write a prompt that would generate this exact image using an AI image generation model"
    //             }
    //         }
    //     };

    //     var response = await SendRequest("/qa/chat", requestBody);
    //     await DisplayQAResponse(response);
    // }

    static async Task CaptionVideo()
    {
        Console.WriteLine("\n🎬 Caption a video by ID\n");
        Console.Write("Enter video ID: ");
        string? videoId = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(videoId))
        {
            Console.WriteLine("Video ID is required.");
            return;
        }

        var requestBody = new
        {
            video_id = videoId,
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = "Write a prompt, in plain text (no marldown), that would generate this exact video using an AI image generation model"
                }
            }
        };

        var response = await SendRequest("/qa/chat", requestBody);
        await DisplayQAResponse(response);
    }

    static async Task UploadVideo()
    {
        try
        {
            Console.WriteLine("\n📤 Upload a video\n");
            Console.WriteLine($"Current folder: {Directory.GetCurrentDirectory()}\n");
            Console.Write("Enter video file path: ");
            string? filePath = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                Console.WriteLine("File not found.");
                return;
            }

            string fileName = Path.GetFileName(filePath);

            using (var form = new MultipartFormDataContent())
            {
                var fileContent = new ByteArrayContent(File.ReadAllBytes(filePath));
                fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("video/mp4");
                form.Add(fileContent, "file", fileName);
                form.Add(new StringContent(fileName), "video_name");
                form.Add(new StringContent("true"), "index");

                var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/videos/upload")
                {
                    Content = form
                };
                request.Headers.Add("X-Api-Key", apiKey);

                var response = await client.SendAsync(request);
                await DisplayResponse(response);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
        }
    }

    static async Task UploadPhoto()
    {
        try
        {
            Console.WriteLine("\n🖼️  Upload a photo\n");
            Console.WriteLine($"Current folder: {Directory.GetCurrentDirectory()}\n");
            Console.Write("Enter photo file path: ");
            string? filePath = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                Console.WriteLine("File not found.");
                return;
            }

            string fileName = Path.GetFileName(filePath);
            string contentType = filePath.EndsWith(".png") ? "image/png" : "image/jpeg";

            using (var form = new MultipartFormDataContent())
            {
                var fileContent = new ByteArrayContent(File.ReadAllBytes(filePath));
                fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
                form.Add(fileContent, "images", fileName);

                var metadata = new
                {
                    requests = new[]
                    {
                        new
                        {
                            indexing_config = new { index = true },
                            metadata = new { }
                        }
                    }
                };
                var metadataJson = JsonSerializer.Serialize(metadata);
                form.Add(new StringContent(metadataJson), "metadata");

                var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/images/upload")
                {
                    Content = form
                };
                request.Headers.Add("X-Api-Key", apiKey);

                var response = await client.SendAsync(request);
                await DisplayResponse(response);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
        }
    }

    static async Task DeleteVideo()
    {
        Console.WriteLine("\n🗑️  Delete a video by ID\n");
        Console.Write("Enter video ID to delete: ");
        string? videoId = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(videoId))
        {
            Console.WriteLine("Video ID is required.");
            return;
        }

        var response = await SendRequest("/videos/delete", new { video_ids = new[] { videoId } });
        await DisplayResponse(response);
    }

    static async Task DisplayVideosResponse(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body);

        if (json.RootElement.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array)
        {
            Console.WriteLine($"{"Video ID",-40} {"Video Name"}");
            Console.WriteLine(new string('-', 80));

            foreach (var video in results.EnumerateArray())
            {
                var id = video.TryGetProperty("video_id", out var idEl) ? idEl.GetString() ?? "N/A" : "N/A";
                var name = video.TryGetProperty("metadata", out var meta) && meta.TryGetProperty("video_name", out var nameEl) 
                    ? nameEl.GetString() ?? "N/A" : "N/A";
                Console.WriteLine($"{id,-40} {name}");
            }
        }
        else
        {
            Console.WriteLine(JsonSerializer.Serialize(json.RootElement, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    static async Task DisplayResponse(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        try
        {
            var json = JsonDocument.Parse(body);
            Console.WriteLine(JsonSerializer.Serialize(json.RootElement, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            Console.WriteLine(body);
        }
    }

    static async Task DisplayQAResponse(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body);
        
        if (json.RootElement.TryGetProperty("chat_response", out var chatResponse))
        {
            var chatJson = JsonDocument.Parse(chatResponse.GetString() ?? "");
            var text = new StringBuilder();

            if (chatJson.RootElement.TryGetProperty("sections", out var sections) && sections.ValueKind == JsonValueKind.Array)
            {
                foreach (var section in sections.EnumerateArray())
                {
                    if (section.TryGetProperty("markdown", out var markdown))
                    {
                        text.Append(markdown.GetString());
                    }
                }
            }

            var caption = text.ToString();
            await SaveToFile(caption, "video_captioned.json");
            Console.WriteLine(caption);
        }
        else
        {
            Console.WriteLine(JsonSerializer.Serialize(json.RootElement, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    static async Task ListImages()
    {
        Console.WriteLine("\n🖼️  Getting all images in library...\n");
        var response = await SendRequest("/images/list", new { image_ids = Array.Empty<string>() });
        await DisplayImagesResponse(response);
    }

    static async Task DisplayImagesResponse(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body);

        if (json.RootElement.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array)
        {
            var count = results.GetArrayLength();
            Console.WriteLine($"Found {count} image(s)\n");
            
            if (count > 0)
            {
                Console.WriteLine($"{"Image ID",-40} {"Image URL"}");
                Console.WriteLine(new string('-', 120));

                foreach (var image in results.EnumerateArray())
                {
                    var id = image.TryGetProperty("image_id", out var idEl) ? idEl.GetString() ?? "N/A" : "N/A";
                    var url = image.TryGetProperty("image_url", out var urlEl) ? urlEl.GetString() ?? "N/A" : "N/A";
                    Console.WriteLine($"{id,-40} {url}");
                }
            }
        }
        else
        {
            Console.WriteLine(JsonSerializer.Serialize(json.RootElement, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    static async Task<HttpResponseMessage> SendRequest(string endpoint, object body)
    {
        try
        {
            var json = JsonSerializer.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}{endpoint}") { Content = content };
            request.Headers.Add("X-Api-Key", apiKey);
            return await client.SendAsync(request);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            throw;
        }
    }

    static async Task SaveToFile(string content, string fileName)
    {
        var dataFolder = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "data");
        Directory.CreateDirectory(dataFolder);
        var filePath = Path.Combine(dataFolder, fileName);
        await File.WriteAllTextAsync(filePath, content);
        Console.WriteLine($"\n✅ Response saved to {filePath}\n");
    }
}
