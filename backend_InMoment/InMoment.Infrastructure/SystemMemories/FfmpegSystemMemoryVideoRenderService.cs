using System.Diagnostics;
using System.Globalization;
using System.Text;
using InMoment.Application.Abstractions.Storage;
using InMoment.Domain.Common;
using InMoment.Domain.SystemMemories;
using InMoment.Infrastructure.Media;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InMoment.Infrastructure.SystemMemories;

public interface ISystemMemoryVideoRenderService
{
    Task<SystemMemoryVideoRenderResult> RenderAsync(
        Guid userId,
        Guid memoryId,
        SystemMemoryPeriod period,
        IReadOnlyList<SystemMemoryVideoSource> sources,
        CancellationToken ct);
}

public sealed record SystemMemoryVideoSource(
    Guid Id,
    string StorageKey,
    string ContentType,
    DateTime CreatedAt,
    string? Caption);

public sealed record SystemMemoryVideoRenderResult(
    string StorageKey,
    string ContentType,
    long SizeBytes);

public sealed class FfmpegSystemMemoryVideoRenderService : ISystemMemoryVideoRenderService
{
    private const string OutputContentType = "video/mp4";
    private const int OutputWidth = 1080;
    private const int OutputHeight = 1920;
    private const int OutputFps = 30;
    private const double PhotoSecondsPerMoment = 1.45;
    private const double VideoSecondsPerMoment = 3.2;
    private const double VideoStartOffsetSeconds = 0.35;

    private readonly IFileStorage _storage;
    private readonly VideoProcessingOptions _options;
    private readonly ILogger<FfmpegSystemMemoryVideoRenderService> _logger;

    public FfmpegSystemMemoryVideoRenderService(
        IFileStorage storage,
        IOptions<VideoProcessingOptions> options,
        ILogger<FfmpegSystemMemoryVideoRenderService> logger)
    {
        _storage = storage;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<SystemMemoryVideoRenderResult> RenderAsync(
        Guid userId,
        Guid memoryId,
        SystemMemoryPeriod period,
        IReadOnlyList<SystemMemoryVideoSource> sources,
        CancellationToken ct)
    {
        if (userId == Guid.Empty)
            throw new ValidationException("UserId is required.");

        if (memoryId == Guid.Empty)
            throw new ValidationException("MemoryId is required.");

        if (sources.Count == 0)
            throw new ValidationException("System memory video requires at least one source media.");

        var tempRoot = Path.Combine(
            Path.GetTempPath(),
            "inmoment-system-memories",
            memoryId.ToString("N"));

        Directory.CreateDirectory(tempRoot);

        var inputPaths = new List<string>();
        var segmentPaths = new List<string>();
        var concatListPath = Path.Combine(tempRoot, "segments.txt");
        var concatenatedPath = Path.Combine(tempRoot, "memory.concat.mp4");
        var outputPath = Path.Combine(tempRoot, "memory.mp4");

        try
        {
            var filtered = sources
                .Where(x => !string.IsNullOrWhiteSpace(x.StorageKey))
                .OrderBy(x => x.CreatedAt)
                .ToList();

            var maxMoments = 22;
            var buckets = Math.Min(maxMoments, filtered.Count);

            var grouped = new List<SystemMemoryVideoSource>();

            if (filtered.Count <= maxMoments)
            {
                grouped = filtered;
            }
            else
            {
                var step = filtered.Count / (double)buckets;

                for (int i = 0; i < buckets; i++)
                {
                    var start = (int)Math.Floor(i * step);
                    var end = (int)Math.Floor((i + 1) * step);

                    if (end <= start) end = start + 1;
                    if (end > filtered.Count) end = filtered.Count;

                    var slice = filtered.GetRange(start, end - start);

                    var random = slice[Random.Shared.Next(slice.Count)];
                    grouped.Add(random);
                }
            }

            var videoCount = grouped.Count(x => IsVideo(x.ContentType));
            var maxVideo = (int)(grouped.Count * 0.5);

            if (videoCount > maxVideo)
            {
                var videos = grouped.Where(x => IsVideo(x.ContentType)).ToList();
                var toRemove = videos.Skip(maxVideo).ToList();

                foreach (var v in toRemove)
                    grouped.Remove(v);
            }

            var orderedSources = grouped.OrderBy(x => x.CreatedAt).ToList();

            for (var i = 0; i < orderedSources.Count; i++)
            {
                var source = orderedSources[i];

                var inputPath = Path.Combine(
                    tempRoot,
                    $"source_{i:000}{GuessExtension(source.ContentType, source.StorageKey)}");

                var segmentPath = Path.Combine(tempRoot, $"segment_{i:000}.mp4");

                await _storage.DownloadToFileAsync(source.StorageKey, inputPath, ct);
                inputPaths.Add(inputPath);

                if (IsVideo(source.ContentType))
                {
                    await RenderVideoSegmentAsync(inputPath, segmentPath, ct);
                }
                else
                {
                    await RenderPhotoSegmentAsync(inputPath, segmentPath, ct);
                }

                segmentPaths.Add(segmentPath);
            }

            if (segmentPaths.Count == 0)
                throw new ValidationException("System memory video has no readable source media.");

            await WriteConcatListAsync(concatListPath, segmentPaths, ct);
            await ConcatenateSegmentsAsync(concatListPath, concatenatedPath, ct);
            await AddBackgroundAudioAsync(concatenatedPath, outputPath, ct);

            var key = BuildStorageKey(userId, memoryId, period);
            var sizeBytes = await _storage.UploadFileAsync(key, outputPath, OutputContentType, ct);

            _logger.LogInformation(
                "Generated system memory video {MemoryId} for user {UserId}. Period: {Period} months. Sources: {SourceCount}, Size: {SizeBytes}.",
                memoryId,
                userId,
                (int)period,
                segmentPaths.Count,
                sizeBytes);

            return new SystemMemoryVideoRenderResult(key, OutputContentType, sizeBytes);
        }
        finally
        {
            TryDelete(outputPath);
            TryDelete(concatenatedPath);
            TryDelete(concatListPath);

            foreach (var path in segmentPaths)
                TryDelete(path);

            foreach (var path in inputPaths)
                TryDelete(path);

            TryDeleteDirectory(tempRoot);
        }
    }

    private async Task RenderPhotoSegmentAsync(
        string inputPath,
        string segmentPath,
        CancellationToken ct)
    {
        var duration = PhotoSecondsPerMoment.ToString("0.###", CultureInfo.InvariantCulture);

        var psi = CreateFfmpegStartInfo(ResolveFfmpegPath());
        psi.ArgumentList.Add("-y");
        psi.ArgumentList.Add("-hide_banner");
        psi.ArgumentList.Add("-loglevel");
        psi.ArgumentList.Add("error");

        psi.ArgumentList.Add("-loop");
        psi.ArgumentList.Add("1");
        psi.ArgumentList.Add("-t");
        psi.ArgumentList.Add(duration);
        psi.ArgumentList.Add("-i");
        psi.ArgumentList.Add(inputPath);

        psi.ArgumentList.Add("-f");
        psi.ArgumentList.Add("lavfi");
        psi.ArgumentList.Add("-t");
        psi.ArgumentList.Add(duration);
        psi.ArgumentList.Add("-i");
        psi.ArgumentList.Add("anullsrc=channel_layout=stereo:sample_rate=44100");

        psi.ArgumentList.Add("-map");
        psi.ArgumentList.Add("0:v:0");
        psi.ArgumentList.Add("-map");
        psi.ArgumentList.Add("1:a:0");

        AddCommonVideoFilter(psi);
        AddCommonEncodingArgs(psi);

        psi.ArgumentList.Add("-shortest");
        psi.ArgumentList.Add(segmentPath);

        await RunProcessAsync(psi, "System memory photo segment rendering failed.", ct);
        EnsureNonEmptyFile(segmentPath, "System memory photo segment rendering produced empty file.");
    }

    private async Task RenderVideoSegmentAsync(
        string inputPath,
        string segmentPath,
        CancellationToken ct)
    {
        var hasAudio = await HasAudioStreamAsync(inputPath, ct);
        var duration = VideoSecondsPerMoment.ToString("0.###", CultureInfo.InvariantCulture);
        var startOffset = VideoStartOffsetSeconds.ToString("0.###", CultureInfo.InvariantCulture);

        var psi = CreateFfmpegStartInfo(ResolveFfmpegPath());
        psi.ArgumentList.Add("-y");
        psi.ArgumentList.Add("-hide_banner");
        psi.ArgumentList.Add("-loglevel");
        psi.ArgumentList.Add("error");

        psi.ArgumentList.Add("-ss");
        psi.ArgumentList.Add(startOffset);
        psi.ArgumentList.Add("-t");
        psi.ArgumentList.Add(duration);
        psi.ArgumentList.Add("-i");
        psi.ArgumentList.Add(inputPath);

        if (!hasAudio)
        {
            psi.ArgumentList.Add("-f");
            psi.ArgumentList.Add("lavfi");
            psi.ArgumentList.Add("-t");
            psi.ArgumentList.Add(duration);
            psi.ArgumentList.Add("-i");
            psi.ArgumentList.Add("anullsrc=channel_layout=stereo:sample_rate=44100");
        }

        psi.ArgumentList.Add("-map");
        psi.ArgumentList.Add("0:v:0");
        psi.ArgumentList.Add("-map");
        psi.ArgumentList.Add(hasAudio ? "0:a:0" : "1:a:0");

        AddCommonVideoFilter(psi);
        AddCommonEncodingArgs(psi);

        psi.ArgumentList.Add("-af");
        psi.ArgumentList.Add("aresample=async=1:first_pts=0,volume=1.0");

        psi.ArgumentList.Add("-shortest");
        psi.ArgumentList.Add(segmentPath);

        await RunProcessAsync(psi, "System memory video segment rendering failed.", ct);
        EnsureNonEmptyFile(segmentPath, "System memory video segment rendering produced empty file.");
    }

    private async Task<bool> HasAudioStreamAsync(string inputPath, CancellationToken ct)
    {
        var psi = CreateFfmpegStartInfo(ResolveFfmpegPath());
        psi.ArgumentList.Add("-hide_banner");
        psi.ArgumentList.Add("-loglevel");
        psi.ArgumentList.Add("error");
        psi.ArgumentList.Add("-i");
        psi.ArgumentList.Add(inputPath);
        psi.ArgumentList.Add("-map");
        psi.ArgumentList.Add("0:a:0");
        psi.ArgumentList.Add("-frames:a");
        psi.ArgumentList.Add("1");
        psi.ArgumentList.Add("-f");
        psi.ArgumentList.Add("null");
        psi.ArgumentList.Add("-");

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
        _ = await stderrTask;
        _ = await stdoutTask;

        return process.ExitCode == 0;
    }

    private async Task WriteConcatListAsync(
        string concatListPath,
        IReadOnlyList<string> segmentPaths,
        CancellationToken ct)
    {
        var builder = new StringBuilder();

        foreach (var path in segmentPaths)
        {
            var normalized = path.Replace("\\", "/").Replace("'", "'\\''");
            builder.Append("file '").Append(normalized).AppendLine("'");
        }

        await File.WriteAllTextAsync(concatListPath, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), ct);
    }

    private async Task ConcatenateSegmentsAsync(
        string concatListPath,
        string outputPath,
        CancellationToken ct)
    {
        var psi = CreateFfmpegStartInfo(ResolveFfmpegPath());
        psi.ArgumentList.Add("-y");
        psi.ArgumentList.Add("-hide_banner");
        psi.ArgumentList.Add("-loglevel");
        psi.ArgumentList.Add("error");
        psi.ArgumentList.Add("-f");
        psi.ArgumentList.Add("concat");
        psi.ArgumentList.Add("-safe");
        psi.ArgumentList.Add("0");
        psi.ArgumentList.Add("-i");
        psi.ArgumentList.Add(concatListPath);
        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add("copy");
        psi.ArgumentList.Add("-movflags");
        psi.ArgumentList.Add("+faststart");
        psi.ArgumentList.Add(outputPath);

        await RunProcessAsync(psi, "System memory segment concatenation failed.", ct);
        EnsureNonEmptyFile(outputPath, "System memory segment concatenation produced empty file.");
    }

    private async Task AddBackgroundAudioAsync(
         string inputPath,
         string outputPath,
         CancellationToken ct)
    {
        var musicPath = Path.Combine(
            AppContext.BaseDirectory,
            "SystemMemories",
            "Assets",
            "music.mp3");

        if (!File.Exists(musicPath))
        {
            File.Copy(inputPath, outputPath, true);
            return;
        }

        var psi = CreateFfmpegStartInfo(ResolveFfmpegPath());
        psi.ArgumentList.Add("-y");
        psi.ArgumentList.Add("-hide_banner");
        psi.ArgumentList.Add("-loglevel");
        psi.ArgumentList.Add("error");

        psi.ArgumentList.Add("-i");
        psi.ArgumentList.Add(inputPath);

        psi.ArgumentList.Add("-stream_loop");
        psi.ArgumentList.Add("-1");

        psi.ArgumentList.Add("-i");
        psi.ArgumentList.Add(musicPath);

        psi.ArgumentList.Add("-filter_complex");
        psi.ArgumentList.Add(
            "[0:a]volume=1.0[a0];" +
            "[1:a]volume=0.14[bg];" +
            "[a0][bg]amix=inputs=2:duration=first:dropout_transition=0[a]"
        );

        psi.ArgumentList.Add("-map");
        psi.ArgumentList.Add("0:v:0");

        psi.ArgumentList.Add("-map");
        psi.ArgumentList.Add("[a]");

        psi.ArgumentList.Add("-c:v");
        psi.ArgumentList.Add("copy");

        psi.ArgumentList.Add("-c:a");
        psi.ArgumentList.Add("aac");

        psi.ArgumentList.Add("-b:a");
        psi.ArgumentList.Add("128k");

        psi.ArgumentList.Add("-shortest");

        psi.ArgumentList.Add("-movflags");
        psi.ArgumentList.Add("+faststart");

        psi.ArgumentList.Add(outputPath);

        await RunProcessAsync(psi, "System memory background audio mixing failed.", ct);
        EnsureNonEmptyFile(outputPath, "System memory background audio mixing produced empty file.");
    }

    private static void AddCommonVideoFilter(ProcessStartInfo psi)
    {
        psi.ArgumentList.Add("-vf");
        psi.ArgumentList.Add(
            $"scale={OutputWidth}:{OutputHeight}:force_original_aspect_ratio=increase," +
            $"crop={OutputWidth}:{OutputHeight}," +
            "setsar=1," +
            $"fps={OutputFps}," +
            "format=yuv420p");
    }

    private static void AddCommonEncodingArgs(ProcessStartInfo psi)
    {
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
        psi.ArgumentList.Add("-r");
        psi.ArgumentList.Add(OutputFps.ToString(CultureInfo.InvariantCulture));
        psi.ArgumentList.Add("-vsync");
        psi.ArgumentList.Add("cfr");
        psi.ArgumentList.Add("-c:a");
        psi.ArgumentList.Add("aac");
        psi.ArgumentList.Add("-b:a");
        psi.ArgumentList.Add("128k");
        psi.ArgumentList.Add("-ar");
        psi.ArgumentList.Add("44100");
        psi.ArgumentList.Add("-ac");
        psi.ArgumentList.Add("2");
        psi.ArgumentList.Add("-movflags");
        psi.ArgumentList.Add("+faststart");
    }

    private async Task RunProcessAsync(
        ProcessStartInfo psi,
        string errorPrefix,
        CancellationToken ct)
    {
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
                ? errorPrefix
                : stderr[^Math.Min(stderr.Length, 1800)..];

            throw new ValidationException($"{errorPrefix}: {message}");
        }
    }

    private static ProcessStartInfo CreateFfmpegStartInfo(string ffmpegPath)
    {
        return new ProcessStartInfo
        {
            FileName = ffmpegPath,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
    }

    private string ResolveFfmpegPath()
    {
        return string.IsNullOrWhiteSpace(_options.FfmpegPath)
            ? "ffmpeg"
            : _options.FfmpegPath.Trim();
    }

    private static string BuildStorageKey(Guid userId, Guid memoryId, SystemMemoryPeriod period)
    {
        var month = DateTime.UtcNow.ToString("yyyy.MM", CultureInfo.InvariantCulture);
        return $"system-memories/{userId:D}/{month}/{memoryId:D}-{(int)period}m.mp4";
    }

    private static bool IsVideo(string contentType)
    {
        return contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase);
    }

    private static string GuessExtension(string contentType, string storageKey)
    {
        if (contentType.Contains("jpeg", StringComparison.OrdinalIgnoreCase) ||
            contentType.Contains("jpg", StringComparison.OrdinalIgnoreCase))
            return ".jpg";

        if (contentType.Contains("png", StringComparison.OrdinalIgnoreCase))
            return ".png";

        if (contentType.Contains("webp", StringComparison.OrdinalIgnoreCase))
            return ".webp";

        if (contentType.Contains("mp4", StringComparison.OrdinalIgnoreCase))
            return ".mp4";

        if (contentType.Contains("quicktime", StringComparison.OrdinalIgnoreCase))
            return ".mov";

        if (contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
            return ".mp4";

        var ext = Path.GetExtension(storageKey);
        return string.IsNullOrWhiteSpace(ext) ? ".media" : ext;
    }

    private static void EnsureNonEmptyFile(string path, string message)
    {
        if (!File.Exists(path) || new FileInfo(path).Length <= 0)
            throw new ValidationException(message);
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

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // ignored
        }
    }
}
