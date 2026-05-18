using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ClaudeSessionManager.Wpf.Models;

namespace ClaudeSessionManager.Wpf.Services;

public class ClaudeSessionScanner
{
    private const int MaxScanLines = 50;
    private const int PreviewLength = 60;

    public async Task<IReadOnlyList<SessionEntry>> ScanAsync(string cwd, int maxSessions, CancellationToken ct = default)
    {
        try
        {
            var folder = ClaudeProjectPathMapper.ResolveProjectFolder(cwd);
            if (folder == null) return Array.Empty<SessionEntry>();

            var files = new DirectoryInfo(folder)
                .GetFiles("*.jsonl")
                .OrderByDescending(f => f.LastWriteTime)
                .Take(maxSessions)
                .ToList();

            var result = new List<SessionEntry>(files.Count);
            foreach (var fi in files)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var entry = await ParseFileAsync(fi, ct);
                    result.Add(entry);
                }
                catch (Exception fex)
                {
                    Debug.WriteLine($"[Scanner skip {fi.Name}] {fex.Message}");
                    continue;
                }
            }
            return result;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Scanner] {ex}");
            return Array.Empty<SessionEntry>();
        }
    }

    private async Task<SessionEntry> ParseFileAsync(FileInfo fi, CancellationToken ct)
    {
        var sessionId = Path.GetFileNameWithoutExtension(fi.Name);
        string firstMessage = string.Empty;
        string? gitBranch = null;

        using var stream = fi.OpenRead();
        using var reader = new StreamReader(stream);

        int lineCount = 0;
        while (!reader.EndOfStream && lineCount < MaxScanLines)
        {
            var line = await reader.ReadLineAsync();
            lineCount++;
            if (string.IsNullOrWhiteSpace(line)) continue;

            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;

                if (gitBranch == null && root.TryGetProperty("gitBranch", out var branchProp))
                {
                    gitBranch = branchProp.GetString();
                }

                if (string.IsNullOrEmpty(firstMessage)
                    && root.TryGetProperty("type", out var typeProp)
                    && typeProp.GetString() == "user"
                    && root.TryGetProperty("message", out var msgProp))
                {
                    if (msgProp.TryGetProperty("content", out var contentProp))
                    {
                        if (contentProp.ValueKind == JsonValueKind.String)
                        {
                            firstMessage = contentProp.GetString() ?? string.Empty;
                        }
                        else if (contentProp.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var element in contentProp.EnumerateArray())
                            {
                                if (element.ValueKind == JsonValueKind.Object
                                    && element.TryGetProperty("type", out var elemType)
                                    && elemType.GetString() == "text"
                                    && element.TryGetProperty("text", out var textProp))
                                {
                                    firstMessage = textProp.GetString() ?? string.Empty;
                                    break;
                                }
                            }
                        }
                    }
                }

                if (!string.IsNullOrEmpty(firstMessage) && gitBranch != null)
                    break;
            }
            catch (JsonException)
            {
                continue;
            }
        }

        if (firstMessage.Length > PreviewLength)
            firstMessage = firstMessage.Substring(0, PreviewLength) + "...";

        firstMessage = firstMessage.Replace("\r", " ").Replace("\n", " ").Trim();

        return new SessionEntry(sessionId, fi.LastWriteTime, firstMessage, fi.FullName, gitBranch);
    }
}
