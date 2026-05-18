using System;

namespace ClaudeSessionManager.Wpf.Models;

public record SessionEntry(string SessionId, DateTime LastModified, string FirstMessage, string FilePath, string? GitBranch);
