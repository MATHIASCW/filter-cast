using System.Diagnostics;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = 500L * 1024 * 1024);
var app = builder.Build();

var projectRoot = Directory.GetParent(app.Environment.ContentRootPath)?.FullName ?? throw new InvalidOperationException("Cannot determine project root.");
var inputDirectory = Path.Combine(projectRoot, "input");
var outputDirectory = Path.Combine(projectRoot, "output");
var scriptPath = Path.Combine(projectRoot, "filter_media.py");
Directory.CreateDirectory(outputDirectory);

if (!File.Exists(scriptPath))
	throw new InvalidOperationException($"filter_media.py not found at: {scriptPath}");

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapPost("/api/process", async (HttpRequest request, CancellationToken cancellationToken) =>
{
	var form = await request.ReadFormAsync(cancellationToken);
	var mode = form["mode"].ToString();
	var audioIndex = form["audioIndex"].ToString();
	var separate = form["separate"].ToString();
	var aiStem = form["aiStem"].ToString();
	var device = form["device"].ToString();
	var url = form["url"].ToString();
	var upload = form.Files.GetFile("media");

	if (upload is null && string.IsNullOrWhiteSpace(url))
		return Results.BadRequest(new { error = "Add a file or a URL." });
	if (!string.IsNullOrWhiteSpace(url) && (!Uri.TryCreate(url, UriKind.Absolute, out var parsed) || parsed.Scheme is not ("http" or "https")))
		return Results.BadRequest(new { error = "The URL must start with http:// or https://." });
	if (!File.Exists(scriptPath))
		return Results.Problem("filter_media.py was not found at the project root.");

	var separating = !string.IsNullOrWhiteSpace(separate);
	var jobId = $"{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..6]}";
	var jobInput = Path.Combine(inputDirectory, jobId);
	var jobOutput = Path.Combine(outputDirectory, jobId);
	Directory.CreateDirectory(jobInput);
	Directory.CreateDirectory(jobOutput);

	try
	{
		if (upload is not null)
		{
			var extension = Path.GetExtension(upload.FileName).ToLowerInvariant();
			var safeName = $"upload_{Guid.NewGuid():N}{extension}";
			await using var file = File.Create(Path.Combine(jobInput, safeName));
			await upload.CopyToAsync(file, cancellationToken);
		}

		var arguments = new List<string> { scriptPath, "--input", jobInput, "--output", jobOutput };
		if (!string.IsNullOrWhiteSpace(url))
		{
			var downloadMode = separating ? "audio" : mode is "audio" or "video" ? mode : "both";
			arguments.AddRange(["--download", url, "--download-mode", downloadMode]);
		}

		if (separating)
		{
			var stem = aiStem == "all" ? "all" : "vocals";
			arguments.AddRange(["--separate", stem, "--device", device is "cuda" or "cpu" ? device : "auto"]);
		}
		else
		{
			if (!string.IsNullOrWhiteSpace(mode)) arguments.AddRange(["--mode", mode]);
			if (int.TryParse(audioIndex, out var index) && index >= 0) arguments.AddRange(["--audio-index", index.ToString()]);
		}

		var result = await RunPythonAsync(arguments, projectRoot, cancellationToken);
		if (result.ExitCode == 0)
			return Results.Ok(new { output = result.Output });

		if (!Directory.EnumerateFileSystemEntries(jobOutput).Any())
			TryDeleteDirectory(jobOutput);
		return Results.BadRequest(new { error = result.Output });
	}
	finally
	{
		TryDeleteDirectory(jobInput);
	}
});

app.MapPost("/api/probe", async (HttpRequest request, CancellationToken cancellationToken) =>
{
	var form = await request.ReadFormAsync(cancellationToken);
	var upload = form.Files.GetFile("media");
	if (upload is null)
		return Results.BadRequest(new { error = "Add a file." });

	var probeDirectory = Path.Combine(Path.GetTempPath(), $"filter-cast-probe-{Guid.NewGuid():N}");
	Directory.CreateDirectory(probeDirectory);
	try
	{
		var probePath = Path.Combine(probeDirectory, "probe" + Path.GetExtension(upload.FileName).ToLowerInvariant());
		await using (var file = File.Create(probePath))
			await upload.CopyToAsync(file, cancellationToken);

		var result = await RunPythonAsync([scriptPath, "--probe", probePath], projectRoot, cancellationToken);
		if (result.ExitCode != 0)
			return Results.Content("[]", "application/json");
		try
		{
			using var document = System.Text.Json.JsonDocument.Parse(result.Stdout);
			return Results.Content(document.RootElement.GetRawText(), "application/json");
		}
		catch (System.Text.Json.JsonException)
		{
			return Results.Content("[]", "application/json");
		}
	}
	finally
	{
		TryDeleteDirectory(probeDirectory);
	}
});

app.MapGet("/api/results", () =>
{
	Directory.CreateDirectory(outputDirectory);
	var files = Directory.GetFiles(outputDirectory, "*", SearchOption.AllDirectories)
		.OrderByDescending(path => File.GetLastWriteTimeUtc(path))
		.Select(path =>
		{
			var relative = Path.GetRelativePath(outputDirectory, path).Replace('\\', '/');
			return new { name = relative, url = "/results/" + string.Join('/', relative.Split('/').Select(part => Uri.EscapeDataString(part))) };
		});
	return Results.Ok(files);
});

app.UseStaticFiles(new StaticFileOptions
{
	RequestPath = "/results",
	FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(outputDirectory),
});

app.Run("http://127.0.0.1:5080");

static async Task<(int ExitCode, string Output, string Stdout)> RunPythonAsync(List<string> arguments, string workingDirectory, CancellationToken cancellationToken)
{
	var startInfo = new ProcessStartInfo("python")
	{
		WorkingDirectory = workingDirectory,
		RedirectStandardOutput = true,
		RedirectStandardError = true,
		UseShellExecute = false,
		CreateNoWindow = true
	};
	startInfo.EnvironmentVariables["PYTHONUTF8"] = "1";
	foreach (var argument in arguments)
		startInfo.ArgumentList.Add(argument);
	
	using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Python not found.");
	var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
	var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
	await process.WaitForExitAsync(cancellationToken);
	
	var stdout = await outputTask;
	var stderr = await errorTask;
	var output = (stdout + stderr).Trim();
	return (process.ExitCode, output, stdout.Trim());
}

static void TryDeleteDirectory(string path)
{
	try { if (Directory.Exists(path)) Directory.Delete(path, true); }
	catch (IOException) { }
	catch (UnauthorizedAccessException) { }
}

public partial class Program { }
