using System.Diagnostics;
using InMoment.Application.Abstractions.Media;
using InMoment.Application.Abstractions.Storage;
using InMoment.Domain.Common;
using Microsoft.Extensions.Options;

namespace InMoment.Infrastructure.Media;

public sealed class FfmpegVideoProcessingService : IVideoProcessingService
{
    private const string OutputContentType = "video/mp4";

    private readonly IFileStorage _storage;
    private readonly VideoProcessingOptions _options;

    public FfmpegVideoProcessingService(
        IFileStorage storage,
        IOptions<VideoProcessingOptions> options)
    {
        _storage = storage;
        _options = options.Value;
    }

    public async Task<VideoProcessingResult> TrimAndNormalizeAsync(
        VideoProcessingRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.SourceStorageKey))
            throw new ValidationException("SourceStorageKey is required.");

        if (string.IsNullOrWhiteSpace(request.TargetStorageKey))
            throw new ValidationException("TargetStorageKey is required.");

        if (request.TrimStartMs < 0)
            throw new ValidationException("TrimStartMs must be greater than or equal to zero.");

        if (request.TrimEndMs <= request.TrimStartMs)
            throw new ValidationException("TrimEndMs must be greater than TrimStartMs.");

        var tempRoot = Path.Combine(Path.GetTempPath(), "inmoment-video-processing");
        Directory.CreateDirectory(tempRoot);

        var jobId = Guid.NewGuid().ToString("N");
        var inputPath = Path.Combine(tempRoot, $"{jobId}.input");
        var outputPath = Path.Combine(tempRoot, $"{jobId}.output.mp4");

        try
        {
            await _storage.DownloadToFileAsync(request.SourceStorageKey, inputPath, ct);

            await RunFfmpegAsync(
                inputPath,
                outputPath,
                request.TrimStartMs,
                request.TrimEndMs,
                ct);

            var sizeBytes = await _storage.UploadFileAsync(
                request.TargetStorageKey,
                outputPath,
                OutputContentType,
                ct);

            return new VideoProcessingResult(
                request.TargetStorageKey,
                OutputContentType,
                sizeBytes);
        }
        finally
        {
            TryDelete(inputPath);
            TryDelete(outputPath);
        }
    }

    private async Task RunFfmpegAsync(
        string inputPath,
        string outputPath,
        long trimStartMs,
        long trimEndMs,
        CancellationToken ct)
    {
        var startSeconds = trimStartMs / 1000.0;
        var durationSeconds = (trimEndMs - trimStartMs) / 1000.0;

        var ffmpegPath = string.IsNullOrWhiteSpace(_options.FfmpegPath)
            ? "ffmpeg"
            : _options.FfmpegPath.Trim();

        var psi = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        psi.ArgumentList.Add("-y");

        psi.ArgumentList.Add("-ss");
        psi.ArgumentList.Add(startSeconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));

        psi.ArgumentList.Add("-t");
        psi.ArgumentList.Add(durationSeconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));

        psi.ArgumentList.Add("-i");
        psi.ArgumentList.Add(inputPath);

        psi.ArgumentList.Add("-map");
        psi.ArgumentList.Add("0:v:0");

        psi.ArgumentList.Add("-map");
        psi.ArgumentList.Add("0:a?");

        psi.ArgumentList.Add("-c:v");
        psi.ArgumentList.Add("libx264");

        psi.ArgumentList.Add("-preset");
        psi.ArgumentList.Add("veryfast");

        psi.ArgumentList.Add("-profile:v");
        psi.ArgumentList.Add("baseline");

        psi.ArgumentList.Add("-level");
        psi.ArgumentList.Add("3.1");

        psi.ArgumentList.Add("-pix_fmt");
        psi.ArgumentList.Add("yuv420p");

        psi.ArgumentList.Add("-c:a");
        psi.ArgumentList.Add("aac");

        psi.ArgumentList.Add("-b:a");
        psi.ArgumentList.Add("128k");

        psi.ArgumentList.Add("-movflags");
        psi.ArgumentList.Add("+faststart");

        psi.ArgumentList.Add(outputPath);

        using var process = Process.Start(psi)
            ?? throw new ValidationException("Failed to start FFmpeg.");

        await using var registration = ct.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch
            {
                // ignored
            }
        });

        var stderrTask = process.StandardError.ReadToEndAsync(ct);
        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);

        await process.WaitForExitAsync(ct);

        var stderr = await stderrTask;
        _ = await stdoutTask;

        if (process.ExitCode != 0)
        {
            var message = string.IsNullOrWhiteSpace(stderr)
                ? "Video processing failed."
                : stderr[^Math.Min(stderr.Length, 1200)..];

            throw new ValidationException($"Video processing failed: {message}");
        }

        if (!File.Exists(outputPath) || new FileInfo(outputPath).Length <= 0)
            throw new ValidationException("Video processing produced empty file.");
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // ignored
        }
    }
}

public sealed class VideoProcessingOptions
{
    public const string SectionName = "VideoProcessing";

    public string FfmpegPath { get; set; } = "ffmpeg";
}