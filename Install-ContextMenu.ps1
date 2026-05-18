# Registry 등록 - Windows 폴더 우클릭 메뉴에 "ClaudeSessionManager에서 열기" 추가
# HKCU 영역 (관리자 권한 불필요)
param(
    [string]$ExePath = $null
)

if (-not $ExePath) {
    $ExePath = Join-Path $PSScriptRoot "bin\Release\net9.0-windows\ClaudeSessionManager.exe"
    if (-not (Test-Path $ExePath)) {
        $ExePath = Join-Path $PSScriptRoot "bin\Debug\net9.0-windows\ClaudeSessionManager.exe"
    }
}

if (-not (Test-Path $ExePath)) {
    Write-Error "EXE not found: $ExePath. Specify -ExePath parameter."
    exit 1
}

$MenuName = "ClaudeSessionManager에서 열기"
$KeyName = "ClaudeSessionManager"

$entries = @(
    @{ Path = "HKCU:\Software\Classes\Directory\shell\$KeyName"; Cmd = "`"$ExePath`" `"%1`"" },
    @{ Path = "HKCU:\Software\Classes\Directory\Background\shell\$KeyName"; Cmd = "`"$ExePath`" `"%V`"" }
)

# 옛 키 (PowerShellSplitter) 가 남아있으면 정리
$oldKey = "PowerShellSplitter"
foreach ($base in @("HKCU:\Software\Classes\Directory\shell", "HKCU:\Software\Classes\Directory\Background\shell")) {
    $oldPath = Join-Path $base $oldKey
    if (Test-Path $oldPath) {
        Remove-Item -Path $oldPath -Recurse -Force
        Write-Host "Cleaned old key: $oldPath"
    }
}

foreach ($entry in $entries) {
    New-Item -Path $entry.Path -Force | Out-Null
    Set-ItemProperty -Path $entry.Path -Name "(default)" -Value $MenuName
    Set-ItemProperty -Path $entry.Path -Name "Icon" -Value "`"$ExePath`""

    $cmdPath = Join-Path $entry.Path "command"
    New-Item -Path $cmdPath -Force | Out-Null
    Set-ItemProperty -Path $cmdPath -Name "(default)" -Value $entry.Cmd

    Write-Host "Registered: $($entry.Path)"
}

Write-Host "`nDone. ExePath: $ExePath"
Write-Host "메뉴 표시: 폴더 우클릭 -> '더 많은 옵션 표시' (Win11) 또는 직접 표시 (Win10)"