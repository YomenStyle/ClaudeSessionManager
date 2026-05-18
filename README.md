# ClaudeSessionManager

## 1. 개요

Claude Code 세션을 PowerShell에서 관리하는 TUI 도구입니다.

- `~/.claude/projects/` 하위의 `.jsonl` 파일을 스캔해 세션 목록을 표시합니다.
- 세션에 짧은 별칭(alias)을 붙여 기억하기 쉽게 관리할 수 있습니다.
- AgentVillage(`E:\UAGCShell`)와 별칭 파일(`$env:USERPROFILE\.claude\agent_village\aliases.json`)을 공유합니다. AgentVillage는 별칭 파일을 읽기 전용으로 참조합니다.
- Out-GridView 기반 TUI로 세션을 선택해 바로 재개할 수 있습니다.

## 2. 요구사항

| 항목 | 최소 버전 |
|------|-----------|
| OS | Windows 11 |
| PowerShell | 5.1 이상 (pwsh 7+ 권장) |
| claude CLI | PATH에 등록되어 있어야 함 |
| Windows Terminal | 선택 사항 (없으면 pwsh/powershell fallback) |

## 3. 설치

```powershell
# 모듈 직접 임포트 (프로필에 추가 권장)
Import-Module G:\ClaudeSessionManager\ClaudeSessionManager.psm1

# 또는 현재 디렉토리에서
Import-Module .\ClaudeSessionManager.psm1
```

프로필에 영구 등록하려면 `$PROFILE`에 위 Import-Module 라인을 추가합니다.

## 4. 사용법

### TUI 세션 목록 (권장)

```powershell
# Out-GridView 창에서 세션 선택 후 OK 클릭 → 자동 재개
.\Show-ClaudeSessions.ps1
```

### 개별 명령

```powershell
# 세션 목록 조회 (최근 활성 순)
Get-ClaudeSession | Format-Table session_id_short, alias, cwd, last_active

# 별칭 설정
Set-SessionAlias -SessionId '<uuid>' -Alias 'my-project'

# 별칭 강제 덮어쓰기 (다른 세션이 이미 사용 중일 때)
Set-SessionAlias -SessionId '<uuid>' -Alias 'my-project' -Force

# 별칭 삭제 (빈 문자열 또는 '-' 전달)
Set-SessionAlias -SessionId '<uuid>' -Alias '-'

# 별칭으로 SessionId 조회
Get-SessionIdByAlias -Alias 'my-project'

# SessionId로 별칭 조회
Get-AliasBySessionId -SessionId '<uuid>'

# 세션 재개 (UUID 또는 별칭 모두 가능)
Resume-ClaudeSession -SessionIdOrAlias 'my-project'
Resume-ClaudeSession -SessionIdOrAlias '<uuid>'

# 별칭 제거
Remove-SessionAlias -SessionId '<uuid>'
```

## 5. AgentVillage 연동

별칭 파일 경로:

```
%USERPROFILE%\.claude\agent_village\aliases.json
```

스키마 예시:

```json
{
  "550e8400-e29b-41d4-a716-446655440000": {
    "alias": "my-project",
    "updated_at": "2026-05-14T10:00:00.0000000+09:00"
  }
}
```

- `ClaudeSessionManager`가 유일한 쓰기 주체입니다.
- AgentVillage(`E:\UAGCShell`)는 이 파일을 읽기 전용으로 참조해 세션 별칭을 표시합니다.
- 파일이 없으면 첫 `Set-SessionAlias` 호출 시 자동 생성됩니다.

## 6. 트러블슈팅

### Windows Terminal(wt)이 없을 때

`Resume-ClaudeSession`은 다음 순서로 fallback합니다.

1. `wt` (Windows Terminal) — 새 탭으로 열림
2. `pwsh` (PowerShell 7+)
3. `powershell` (Windows PowerShell 5.1)

### 한글 깨짐

```powershell
chcp 65001
```

또는 pwsh 프로필에 다음을 추가합니다.

```powershell
[Console]::OutputEncoding = [Text.Encoding]::UTF8
```

### 별칭 파일 손상

`$env:USERPROFILE\.claude\agent_village\aliases.json`이 JSON 파싱 실패 시 `Read-AliasMap`은 빈 해시테이블을 반환합니다. 데이터 손실 없이 graceful fallback됩니다. 손상된 파일은 수동으로 삭제하거나 복구하세요.

### Out-GridView 없음

Out-GridView는 Windows PowerShell 5.1 및 `Microsoft.PowerShell.GraphicalTools` 모듈이 필요합니다.

```powershell
Install-Module Microsoft.PowerShell.GraphicalTools -Scope CurrentUser
```

## 7. API 레퍼런스

### Get-ClaudeSession
`~/.claude/projects/` 하위 `.jsonl` 파일을 스캔해 세션 목록을 반환합니다. 최근 활성 순 정렬.

```powershell
Get-ClaudeSession
Get-ClaudeSession | Format-Table session_id_short, alias, cwd, last_active
```

### Set-SessionAlias
세션에 별칭을 설정합니다. `-Force`로 이미 사용 중인 별칭 강제 덮어쓰기 가능. 빈 문자열 또는 `'-'` 전달 시 별칭 삭제.

```powershell
Set-SessionAlias -SessionId '<uuid>' -Alias 'my-project'
Set-SessionAlias -SessionId '<uuid>' -Alias '-'
```

### Get-SessionIdByAlias
별칭으로 SessionId(UUID)를 조회합니다. 없으면 `$null` 반환.

```powershell
Get-SessionIdByAlias -Alias 'my-project'
```

### Get-AliasBySessionId
SessionId로 별칭을 조회합니다. 없으면 `$null` 반환.

```powershell
Get-AliasBySessionId -SessionId '<uuid>'
```

### Remove-SessionAlias
세션 별칭을 제거합니다.

```powershell
Remove-SessionAlias -SessionId '<uuid>'
```

### Resume-ClaudeSession
SessionId 또는 별칭으로 세션을 재개합니다. wt → pwsh → powershell 순으로 fallback.

```powershell
Resume-ClaudeSession -SessionIdOrAlias 'my-project'
Resume-ClaudeSession -SessionIdOrAlias '<uuid>'
```

### Read-AliasMap
별칭 맵을 읽어 hashtable로 반환합니다. 파일이 없거나 파싱 실패 시 빈 `@{}`.

```powershell
$map = Read-AliasMap
```

### Write-AliasMap
hashtable을 원자적으로 별칭 파일에 씁니다 (UTF-8 no BOM).

```powershell
Write-AliasMap -Map @{ '<sid>' = @{ alias='foo'; updated_at=(Get-Date).ToString('o') } }
```

### Test-AliasValid
별칭 후보를 검증합니다. `@{ ok; signal; normalized }` 반환. signal: `ok` / `delete` / `too_long` / `control_char` / `invalid_type`.

```powershell
Test-AliasValid 'my-session'   # @{ ok=$true; signal='ok'; normalized='my-session' }
```
