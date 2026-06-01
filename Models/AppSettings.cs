using System.Text.Json.Serialization;
namespace ClaudeSessionManager.Wpf.Models;

public class AppSettings {
    [JsonPropertyName("terminal")]
    public TerminalSettings Terminal { get; set; } = new();

    [JsonPropertyName("sidebar")]
    public SidebarSettings Sidebar { get; set; } = new();
}

public class SidebarSettings {
    [JsonPropertyName("sidebarWidth")]          public double SidebarWidth          { get; set; } = 280;
    [JsonPropertyName("autoRefreshIntervalMs")] public int    AutoRefreshIntervalMs { get; set; } = 3000;
    [JsonPropertyName("visible")]               public bool   Visible               { get; set; } = true;
    [JsonPropertyName("maxSessions")]           public int    MaxSessions           { get; set; } = 50;
}
public class TerminalSettings {
    [JsonPropertyName("shellPath")]   public string   ShellPath   { get; set; } = "powershell.exe";
    [JsonPropertyName("shellArgs")]   public string[] ShellArgs   { get; set; } = new[] { "-NoLogo" };
    [JsonPropertyName("cols")]        public int      Cols        { get; set; } = 80;
    [JsonPropertyName("rows")]        public int      Rows        { get; set; } = 25;
    [JsonPropertyName("defaultMode")]    public int      DefaultMode    { get; set; } = 1;
    [JsonPropertyName("startupCommand")] public string   StartupCommand { get; set; } = "Remove-Module PSReadLine -ErrorAction SilentlyContinue; Clear-Host";
    [JsonPropertyName("lastCwds")]        public string[] LastCwds       { get; set; } = new string[4];
    [JsonPropertyName("scrollback")]      public int    Scrollback      { get; set; } = 1000;
    [JsonPropertyName("theme")]           public string Theme           { get; set; } = "light+";
    [JsonPropertyName("cursorBlink")]     public bool   CursorBlink     { get; set; } = true;
    [JsonPropertyName("virtualHost")]     public string VirtualHost     { get; set; } = "terminal.local";
    [JsonPropertyName("batchIntervalMs")] public int    BatchIntervalMs { get; set; } = 16;
    [JsonPropertyName("resizeDebounceMs")] public int   ResizeDebounceMs { get; set; } = 150;
    [JsonPropertyName("fontFamily")]      public string FontFamily      { get; set; } = "Noto Sans Mono, Cascadia Mono, Consolas, monospace";
    [JsonPropertyName("fontSize")]        public int    FontSize        { get; set; } = 13;
    [JsonPropertyName("onOpenCommand")]   public string OnOpenCommand   { get; set; } = "claude --dangerously-skip-permissions";
}
