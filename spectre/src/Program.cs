using System;
using System.Text;
using System.Text.Json;
using Spectre.Console;
using SpectreCaptionThis;

var console = AnsiConsole.Console;

try
{
    var client = new RekaClient();

    var banner = new FigletText("Caption This")
        .Centered()
        .Color(Color.Violet);
    console.Write(banner);
    console.MarkupLine("[grey]Spectre Edition • net10.0[/]");

    while (true)
    {
        var selection = console.Prompt(
            new SelectionPrompt<string>()
                .Title("[bold white]Main Menu[/]")
                .PageSize(10)
                .AddChoices(
                    "List Videos",
                    "Upload Video",
                    "List Images",
                    "Upload Image",
                    "Quit"));

        if (selection == "Quit")
        {
            console.MarkupLine("\n[green]Goodbye![/]");
            break;
        }

        switch (selection)
        {
            case "List Videos":
                await ShowVideosListAndActions(client);
                break;
            case "Upload Video":
                await UploadVideoFlow(client);
                break;
            case "List Images":
                await ShowImagesListAndActions(client);
                break;
            case "Upload Image":
                await UploadImageFlow(client);
                break;
        }

        console.MarkupLine("\n[grey]Press Enter to continue…[/]");
        Console.ReadLine();
        console.Clear(true);
        console.Write(banner);
        console.MarkupLine("[grey]Spectre Edition • net10.0[/]");
    }
}
catch (Exception ex)
{
    AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
}

static async Task RunWithSpinner(string title, Func<Task> action)
{
    await AnsiConsole.Status()
        .Spinner(Spinner.Known.Dots12)
        .SpinnerStyle(Style.Parse("green bold"))
        .StartAsync(title, async _ => { await action(); });
}

static async Task RunWithProgress(string title, Func<ProgressContext, Task> action)
{
    await AnsiConsole.Progress()
        .Columns(new ProgressColumn[]
        {
            new TaskDescriptionColumn(),
            new ProgressBarColumn(),
            new PercentageColumn(),
            new RemainingTimeColumn(),
            new SpinnerColumn()
        })
        .StartAsync(async ctx => { await action(ctx); });
}

static void DisplayVideos(string json)
{
    var doc = JsonDocument.Parse(json);
    if (!doc.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
    {
        ShowJson(json);
        return;
    }

    var table = new Table().Border(TableBorder.Rounded).Title("[bold]Videos[/]");
    table.AddColumn(new TableColumn("ID").Centered());
    table.AddColumn(new TableColumn("Name"));

    foreach (var v in results.EnumerateArray())
    {
        var id = v.TryGetProperty("video_id", out var idEl) ? idEl.GetString() ?? "N/A" : "N/A";
        var name = v.TryGetProperty("metadata", out var meta) && meta.TryGetProperty("video_name", out var nameEl)
            ? nameEl.GetString() ?? "N/A" : "N/A";
        table.AddRow(new Markup(Escape(id)), new Markup(Escape(name)));
    }

    AnsiConsole.Write(table);
}

static void DisplayImages(string json)
{
    var doc = JsonDocument.Parse(json);
    if (!doc.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
    {
        ShowJson(json);
        return;
    }

    var count = results.GetArrayLength();
    var table = new Table().Border(TableBorder.Rounded).Title($"[bold]Images ({count})[/]");
    table.AddColumn(new TableColumn("ID").Centered());
    table.AddColumn(new TableColumn("URL"));

    foreach (var v in results.EnumerateArray())
    {
        var id = v.TryGetProperty("image_id", out var idEl) ? idEl.GetString() ?? "N/A" : "N/A";
        var url = v.TryGetProperty("image_url", out var urlEl) ? urlEl.GetString() ?? "N/A" : "N/A";
        table.AddRow(new Markup(Escape(id)), new Markup(Escape(url)));
    }

    AnsiConsole.Write(table);
}

static string ExtractVideoCaption(string json)
{
    try
    {
        var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("chat_response", out var chatResponse)) return string.Empty;
        var inner = JsonDocument.Parse(chatResponse.GetString() ?? "{}");
        var sb = new StringBuilder();
        if (inner.RootElement.TryGetProperty("sections", out var sections) && sections.ValueKind == JsonValueKind.Array)
        {
            foreach (var s in sections.EnumerateArray())
            {
                if (s.TryGetProperty("markdown", out var md)) sb.Append(md.GetString());
            }
        }
        return sb.ToString();
    }
    catch { return string.Empty; }
}

static void ShowJson(string raw)
{
    var formatted = RekaClient.FormatJson(raw);
    var panel = new Panel(formatted)
        .Header("Response")
        .Border(BoxBorder.Double)
        .Padding(1,1)
        .BorderStyle(new Style(Color.SteelBlue));
    AnsiConsole.Write(panel);
}

static void ShowPanel(string header, string content, Color color)
{
    var panel = new Panel(content)
        .Header(header)
        .Border(BoxBorder.Double)
        .Padding(1,1)
        .BorderStyle(new Style(color, decoration: Decoration.Bold));
    AnsiConsole.Write(panel);
}

static string AskId(string label)
{
    return AnsiConsole.Prompt(new TextPrompt<string>($"[bold]{label}[/]")
        .PromptStyle("yellow")
        .ValidationErrorMessage("[red]Required[/]")
        .Validate(id => string.IsNullOrWhiteSpace(id) ? ValidationResult.Error("Required") : ValidationResult.Success()));
}

static string AskPath(string label)
{
    var mode = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title($"[bold]{label}[/]")
            .AddChoices("Browse…", "Enter path"));

    if (mode == "Enter path")
    {
        return AnsiConsole.Prompt(new TextPrompt<string>("[bold]File path[/]")
            .PromptStyle("yellow")
            .ValidationErrorMessage("[red]File not found[/]")
            .Validate(p => System.IO.File.Exists(p) ? ValidationResult.Success() : ValidationResult.Error("File not found")));
    }

    return BrowseForFile();
}

static string AskText(string label)
{
    return AnsiConsole.Prompt(new TextPrompt<string>($"[bold]{label}[/]")
        .PromptStyle("yellow")
        .ValidationErrorMessage("[red]Required[/]")
        .Validate(t => string.IsNullOrWhiteSpace(t) ? ValidationResult.Error("Required") : ValidationResult.Success()));
}

static string Escape(string value) => Markup.Escape(value);

// Lightweight file browser using SelectionPrompt
static string BrowseForFile()
{
    var cwd = Environment.CurrentDirectory;
    while (true)
    {
        var entries = Directory.EnumerateFileSystemEntries(cwd)
            .Select(path => new
            {
                Path = path,
                Name = Path.GetFileName(path),
                IsDir = Directory.Exists(path)
            })
            .OrderByDescending(e => e.IsDir)
            .ThenBy(e => e.Name)
            .ToList();

        var choices = new List<string>();
        choices.Add("⬆️  .. (up)");
        choices.AddRange(entries.Select(e => e.IsDir ? $"📁 {e.Name}" : $"📄 {e.Name}"));
        choices.Add("✅ Select this folder…");

        var selected = AnsiConsole.Prompt(new SelectionPrompt<string>()
            .Title($"[bold]Browse:[/] [grey]{Markup.Escape(cwd)}[/]")
            .PageSize(12)
            .MoreChoicesText("[grey](scroll to see more)[/]")
            .AddChoices(choices));

        if (selected.StartsWith("⬆️"))
        {
            var parent = Directory.GetParent(cwd)?.FullName;
            if (!string.IsNullOrEmpty(parent)) cwd = parent;
            continue;
        }

        if (selected == "✅ Select this folder…")
        {
            // Let user pick a file from current folder via type-in with autovalidation
            var fileName = AnsiConsole.Prompt(new TextPrompt<string>("[bold]File name in this folder[/]")
                .PromptStyle("yellow")
                .ValidationErrorMessage("[red]File not found[/]")
                .Validate(name =>
                {
                    var full = Path.Combine(cwd, name);
                    return File.Exists(full) ? ValidationResult.Success() : ValidationResult.Error("File not found");
                }));
            return Path.Combine(cwd, fileName);
        }

        // Extract actual name
        var name = selected.Substring(2).Trim();
        var fullPath = Path.Combine(cwd, name);

        if (Directory.Exists(fullPath))
        {
            cwd = fullPath;
            continue;
        }

        if (File.Exists(fullPath))
        {
            return fullPath;
        }

        AnsiConsole.MarkupLine("[red]Selection invalid. Try again.[/]");
    }
}

// New unified flows
static async Task ShowVideosListAndActions(RekaClient client)
{
    string videosJson = string.Empty;
    await RunWithSpinner("Fetching videos", async () =>
    {
        var v = await client.ListVideosAsync();
        videosJson = await v.Content.ReadAsStringAsync();
    });

    var items = BuildUnifiedItems(videosJson, "{}")
        .FindAll(i => i.Kind == ItemKind.Video);
    if (items.Count == 0)
    {
        AnsiConsole.MarkupLine("[yellow]No videos found.[/]");
        return;
    }

    var choice = AnsiConsole.Prompt(
        new SelectionPrompt<UnifiedItem>()
            .Title("[bold]Select a video[/]")
            .UseConverter(i => i.Display)
            .PageSize(12)
            .AddChoices(items));

    var captionChoice = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title("[bold]Caption style[/]")
            .AddChoices("Short", "Detailed"));
    var type = captionChoice == "Detailed" ? CaptionPromptType.Detailed : CaptionPromptType.Short;

    await RunWithProgress("Captioning video", async ctx =>
    {
        var task = ctx.AddTask("Querying Reka");
        task.Increment(30);
        var resp = await client.CaptionVideoAsync(choice.IdOrUrl, type);
        task.Increment(60);
        var caption = ExtractVideoCaption(await resp.Content.ReadAsStringAsync());
        task.Increment(10);
        if (!string.IsNullOrWhiteSpace(caption))
        {
            await RekaClient.SaveToFileAsync(caption, "video_captioned.json");
            ShowPanel("Video Caption", caption, Color.Aqua);
        }
        else
        {
            ShowJson(await resp.Content.ReadAsStringAsync());
        }
    });
}

static async Task ShowImagesListAndActions(RekaClient client)
{
    string imagesJson = string.Empty;
    await RunWithSpinner("Fetching images", async () =>
    {
        var i = await client.ListImagesAsync();
        imagesJson = await i.Content.ReadAsStringAsync();
    });

    var items = BuildUnifiedItems("{}", imagesJson)
        .FindAll(i => i.Kind == ItemKind.Image);
    if (items.Count == 0)
    {
        AnsiConsole.MarkupLine("[yellow]No images found.[/]");
        return;
    }

    var choice = AnsiConsole.Prompt(
        new SelectionPrompt<UnifiedItem>()
            .Title("[bold]Select an image[/]")
            .UseConverter(i => i.Display)
            .PageSize(12)
            .AddChoices(items));

    var captionChoice = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title("[bold]Caption style[/]")
            .AddChoices("Short", "Detailed"));
    var type = captionChoice == "Detailed" ? CaptionPromptType.Detailed : CaptionPromptType.Short;

    await RunWithSpinner("Captioning image", async () =>
    {
        var (resp, caption) = await client.CaptionImageAsync(choice.IdOrUrl, type);
        if (!string.IsNullOrWhiteSpace(caption))
        {
            await RekaClient.SaveToFileAsync(caption!, "image_captioned.json");
            ShowPanel("Image Caption", caption!, Color.LightGreen);
        }
        else
        {
            ShowJson(await resp.Content.ReadAsStringAsync());
        }
    });
}

static async Task UploadVideoFlow(RekaClient client)
{
    var path = AskPath("Video file path");
    await RunWithSpinner("Uploading video", async () =>
    {
        var resp = await client.UploadVideoAsync(path);
        ShowJson(await resp.Content.ReadAsStringAsync());
    });
}

static async Task UploadImageFlow(RekaClient client)
{
    var path = AskPath("Image file path");
    await RunWithSpinner("Uploading image", async () =>
    {
        var resp = await client.UploadPhotoAsync(path);
        ShowJson(await resp.Content.ReadAsStringAsync());
    });
}


static List<UnifiedItem> BuildUnifiedItems(string videosJson, string imagesJson)
{
    var items = new List<UnifiedItem>();
    try
    {
        var vdoc = JsonDocument.Parse(videosJson);
        if (vdoc.RootElement.TryGetProperty("results", out var vres) && vres.ValueKind == JsonValueKind.Array)
        {
            foreach (var v in vres.EnumerateArray())
            {
                var id = v.TryGetProperty("video_id", out var idEl) ? idEl.GetString() ?? "" : "";
                var name = v.TryGetProperty("metadata", out var meta) && meta.TryGetProperty("video_name", out var nameEl) ? nameEl.GetString() ?? "" : "";
                if (!string.IsNullOrWhiteSpace(id))
                {
                    items.Add(new UnifiedItem
                    {
                        Kind = ItemKind.Video,
                        IdOrUrl = id,
                        Display = $"[blue]VIDEO[/] {Markup.Escape(id)}  •  {Markup.Escape(name)}"
                    });
                }
            }
        }
    }
    catch { }

    try
    {
        var idoc = JsonDocument.Parse(imagesJson);
        if (idoc.RootElement.TryGetProperty("results", out var ires) && ires.ValueKind == JsonValueKind.Array)
        {
            foreach (var i in ires.EnumerateArray())
            {
                var id = i.TryGetProperty("image_id", out var idEl) ? idEl.GetString() ?? "" : "";
                var url = i.TryGetProperty("image_url", out var urlEl) ? urlEl.GetString() ?? "" : "";
                if (!string.IsNullOrWhiteSpace(url))
                {
                    items.Add(new UnifiedItem
                    {
                        Kind = ItemKind.Image,
                        IdOrUrl = url,
                        Display = $"[green]IMAGE[/] {Markup.Escape(id)}  •  {Markup.Escape(url)}"
                    });
                }
            }
        }
    }
    catch { }

    return items;
}
