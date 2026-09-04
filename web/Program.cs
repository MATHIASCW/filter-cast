using System.Diagnostics;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = 500L * 1024 * 1024);
var app = builder.Build();

var projectRoot = Directory.GetParent(app.Environment.ContentRootPath)?.FullName ?? throw new InvalidOperationException("Cannot determine project root.");
var inputDirectory = Path.Combine(projectRoot, "input");
var outputDirectory = Path.Combine(projectRoot, "output");
var scriptPath = Path.Combine(projectRoot, "filter_media.py");

if (!File.Exists(scriptPath))
	throw new InvalidOperationException($"filter_media.py not found at: {scriptPath}");

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapPost("/api/process", async (HttpRequest request, CancellationToken cancellationToken) =>
{
	var form = await request.ReadFormAsync(cancellationToken);
	var mode = form["mode"].ToString();
	var audioIndex = form["audioIndex"].ToString();
	var language = form["language"].ToString();
	var title = form["title"].ToString();
	var separate = form["separate"].ToString();
	var device = form["device"].ToString();
	var url = form["url"].ToString();
	var upload = form.Files.GetFile("media");

	if (upload is null && string.IsNullOrWhiteSpace(url))
		return Results.BadRequest(new { error = "Add a file or a URL." });
	if (!string.IsNullOrWhiteSpace(url) && (!Uri.TryCreate(url, UriKind.Absolute, out var parsed) || parsed.Scheme is not ("http" or "https")))
		return Results.BadRequest(new { error = "The URL must start with http:// or https://." });
	if (!File.Exists(scriptPath))
		return Results.Problem("filter_media.py was not found at the project root.");

	Directory.CreateDirectory(inputDirectory);
	Directory.CreateDirectory(outputDirectory);
	if (upload is not null)
	{
		var extension = Path.GetExtension(upload.FileName).ToLowerInvariant();
		var safeName = $"upload_{Guid.NewGuid():N}{extension}";
		await using var file = File.Create(Path.Combine(inputDirectory, safeName));
		await upload.CopyToAsync(file, cancellationToken);
	}

	var arguments = new List<string> { scriptPath, "--input", inputDirectory, "--output", outputDirectory };
	if (!string.IsNullOrWhiteSpace(url)) arguments.AddRange(["--download", url, "--download-mode", mode is "audio" or "video" ? mode : "both"]);
	if (!string.IsNullOrWhiteSpace(mode)) arguments.AddRange(["--mode", mode]);
	if (int.TryParse(audioIndex, out var index) && index >= 0) arguments.AddRange(["--audio-index", index.ToString()]);
	if (!string.IsNullOrWhiteSpace(language)) arguments.AddRange(["--language", language]);
	if (!string.IsNullOrWhiteSpace(title)) arguments.AddRange(["--title", title]);
	if (!string.IsNullOrWhiteSpace(separate)) arguments.AddRange(["--separate", separate, "--device", string.IsNullOrWhiteSpace(device) ? "auto" : device]);

	var result = await RunPythonAsync(arguments, projectRoot, cancellationToken);
	return result.ExitCode == 0
		? Results.Ok(new { output = result.Output })
		: Results.BadRequest(new { error = result.Output });
});

app.MapGet("/api/results", () =>
{
	Directory.CreateDirectory(outputDirectory);
	var files = Directory.GetFiles(outputDirectory, "*", SearchOption.AllDirectories)
		.Select(path => new { name = Path.GetRelativePath(outputDirectory, path), url = "/results/" + Uri.EscapeDataString(Path.GetRelativePath(outputDirectory, path).Replace('\\', '/')) });
	return Results.Ok(files);
});

app.UseStaticFiles(new StaticFileOptions
{
	RequestPath = "/results",
	FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(outputDirectory),
});

app.Run("http://127.0.0.1:5080");

static async Task<(int ExitCode, string Output)> RunPythonAsync(List<string> arguments, string workingDirectory, CancellationToken cancellationToken)
{
	var startInfo = new ProcessStartInfo("python")
	{
		WorkingDirectory = workingDirectory,
		RedirectStandardOutput = true,
		RedirectStandardError = true,
		UseShellExecute = false,
		CreateNoWindow = true
	};
	foreach (var argument in arguments)
		startInfo.ArgumentList.Add(argument);
	
	using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Python not found.");
	var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
	var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
	await process.WaitForExitAsync(cancellationToken);
	
	var stdout = await outputTask;
	var stderr = await errorTask;
	var output = (stdout + stderr).Trim();
	return (process.ExitCode, output);
}
