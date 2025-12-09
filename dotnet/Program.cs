using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.IO;
using System.Net.Http.Headers;
using DotNetEnv;

class Program
{
    private static readonly HttpClient client = new HttpClient();
    private static string apiKey = "";
    private static string baseUrl = "https://vision-agent.api.reka.ai";
    
    private static readonly string responseFormat = @"- Subject: [Detailed character/object description with 15+ specific physical attributes, clothing, age, build, facial features, ethnicity, hair, eyes, posture, mannerisms, emotional state] -Action: [Specific actions, movements, gestures, behaviors, timing, sequence, transitions, micro-expressions, body language, interaction patterns]- Scene: [Detailed environment description including location, props, background elements, lighting setup, weather, time of day, architectural details]- Style: [Camera shot type, angle, movement, lighting style, visual aesthetic, aspect ratio, film grade, color palette, depth of field, focus techniques]";

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
            Console.Write("\nSelect an option (1-6): ");
            string? choice = Console.ReadLine();

            switch (choice)
            {
                case "1":
                    await GetAllVideos();
                    break;
                case "2":
                    await CaptionVideo();
                    break;
                case "3":
                    await UploadVideo();
                    break;
                case "4":
                    await UploadPhoto();
                    break;
                case "5":
                    await DeleteVideo();
                    break;
                case "6":
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
        Console.WriteLine("║ 1. Get all videos in library           ║");
        Console.WriteLine("║ 2. Caption a video by ID               ║");
        Console.WriteLine("║ 3. Upload a video                      ║");
        Console.WriteLine("║ 4. Upload a photo                      ║");
        Console.WriteLine("║ 5. Delete a video by ID                ║");
        Console.WriteLine("║ 6. Exit                                ║");
        Console.WriteLine("╚════════════════════════════════════════╝");
    }

    static async Task GetAllVideos()
    {
        try
        {
            Console.WriteLine("\n📹 Getting all videos in library...\n");

            var requestBody = new { video_ids = Array.Empty<string>() };
            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/videos/list")
            {
                Content = content
            };
            request.Headers.Add("X-Api-Key", apiKey);

            var response = await client.SendAsync(request);
            await DisplayResponse(response);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
        }
    }

    static async Task CaptionVideo()
    {
        try
        {
            Console.WriteLine("\n🎬 Caption a video by ID\n");
            Console.Write("Enter video ID: ");
            string? videoId = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(videoId))
            {
                Console.WriteLine("Video ID is required.");
                return;
            }

            var message = new
            {
                role = "user",
                content = $"What is happening in this video? Reply using text only (no markdown) filling those 4 sections: {responseFormat}"
            };

            var requestBody = new
            {
                video_id = videoId,
                messages = new[] { message }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/qa/chat")
            {
                Content = content
            };
            request.Headers.Add("X-Api-Key", apiKey);

            var response = await client.SendAsync(request);
            await DisplayQAResponse(response);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
        }
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
        try
        {
            Console.WriteLine("\n🗑️  Delete a video by ID\n");
            Console.Write("Enter video ID to delete: ");
            string? videoId = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(videoId))
            {
                Console.WriteLine("Video ID is required.");
                return;
            }

            var requestBody = new { video_ids = new[] { videoId } };
            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/videos/delete")
            {
                Content = content
            };
            request.Headers.Add("X-Api-Key", apiKey);

            var response = await client.SendAsync(request);
            await DisplayResponse(response);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
        }
    }

    static async Task DisplayResponse(HttpResponseMessage response)
    {
        Console.WriteLine($"Status Code: {response.StatusCode}");
        string responseBody = await response.Content.ReadAsStringAsync();

        try
        {
            var jsonDocument = JsonDocument.Parse(responseBody);
            var options = new JsonSerializerOptions { WriteIndented = true };
            string prettyJson = JsonSerializer.Serialize(jsonDocument.RootElement, options);
            Console.WriteLine(prettyJson);
        }
        catch
        {
            Console.WriteLine(responseBody);
        }
    }



    static async Task DisplayQAResponse(HttpResponseMessage response)
    {
        Console.WriteLine($"Status Code: {response.StatusCode}");
        string responseBody = await response.Content.ReadAsStringAsync();
        
        // await SaveToFile(responseBody, "raw_captioned.json");

        try
        {
            var jsonDocument = JsonDocument.Parse(responseBody);
            
            if (jsonDocument.RootElement.TryGetProperty("chat_response", out JsonElement chatResponseElement))
            {
                string chatResponseString = chatResponseElement.GetString() ?? "";

                var chatResponseDoc = JsonDocument.Parse(chatResponseString);

                var stringBuilder = new StringBuilder();

                if (chatResponseDoc.RootElement.TryGetProperty("sections", out JsonElement sectionsElement)
                    && sectionsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement section in sectionsElement.EnumerateArray())
                    {
                        if (section.TryGetProperty("markdown", out JsonElement markdownElement))
                        {
                            string? markdown = markdownElement.GetString();
                            if (!string.IsNullOrEmpty(markdown))
                            {
                                stringBuilder.Append(markdown);
                            }
                        }
                    }
                }

                string cleanText = stringBuilder.ToString();
                await SaveToFile(cleanText, "video_captioned.json");
                Console.WriteLine(cleanText);
            }
            else
            {
                // Fallback to original behavior if structure is different
                var options = new JsonSerializerOptions { WriteIndented = true };
                string prettyJson = JsonSerializer.Serialize(jsonDocument.RootElement, options);
                
                await SaveToFile(prettyJson, "video_bak_captioned.json");
                Console.WriteLine(prettyJson);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Error parsing response: {ex.Message}");
            Console.WriteLine(responseBody);
        }
    }

    private static async Task SaveToFile(string cleanJson, string fileName)
    {
        string dataFolder = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "data");
        Directory.CreateDirectory(dataFolder);
        string filePath = Path.Combine(dataFolder, fileName);
        await File.WriteAllTextAsync(filePath, cleanJson);
        Console.WriteLine($"\n✅ Response saved to {filePath}\n");
    }
}
