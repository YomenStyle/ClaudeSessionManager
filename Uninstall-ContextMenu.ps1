# Registry 등록 해제
$KeyName = "ClaudeSessionManager"
$paths = @(
    "HKCU:\Software\Classes\Directory\shell\$KeyName",
    "HKCU:\Software\Classes\Directory\Background\shell\$KeyName",
    "HKCU:\Software\Classes\Directory\shell\PowerShellSplitter",
    "HKCU:\Software\Classes\Directory\Background\shell\PowerShellSplitter"
)
foreach ($p in $paths) {
    if (Test-Path $p) {
        Remove-Item -Path $p -Recurse -Force
        Write-Host "Removed: $p"
    } else {
        Write-Host "Not found: $p"
    }
}