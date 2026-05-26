using System;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ClaudeSessionManager.Wpf.Services
{
    public static class SessionTitleWriter
    {
        private const string JsonlTypeAiTitle = "ai-title";
        private const byte LineFeed = 0x0A;

        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = false,
        };

        public static async Task AppendTitleAsync(
            string filePath,
            string sessionId,
            string newTitle,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(filePath));
            if (string.IsNullOrWhiteSpace(sessionId))
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(sessionId));
            if (string.IsNullOrWhiteSpace(newTitle))
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(newTitle));

            bool leadingLfNeeded;
            using (var probe = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                if (probe.Length > 0)
                {
                    probe.Seek(-1, SeekOrigin.End);
                    int last = probe.ReadByte();
                    leadingLfNeeded = last != LineFeed;
                }
                else
                {
                    leadingLfNeeded = false;
                }
            }

            var payload_obj = new { type = JsonlTypeAiTitle, aiTitle = newTitle, sessionId = sessionId };
            string json = JsonSerializer.Serialize(payload_obj, JsonOptions);
            string payload = (leadingLfNeeded ? "\n" : "") + json + "\n";
            byte[] buffer = Utf8NoBom.GetBytes(payload);

            using var fs = new FileStream(
                filePath,
                FileMode.Append,
                FileAccess.Write,
                FileShare.ReadWrite,
                bufferSize: 4096,
                useAsync: true);

            await fs.WriteAsync(buffer, ct);
            await fs.FlushAsync(ct);
        }
    }
}
