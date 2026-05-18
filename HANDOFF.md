# Claude Session Manager — 핸드오프

이 폴더에서 새 Claude Code 세션을 시작해 작업을 이어간다. 이 문서를 먼저 읽고, 아래 "다음 단계" 순서대로 진행.

---

## 한 줄 목표

여러 Claude Code 세션을 한 곳에서 관리하는 PowerShell **TUI** 도구. AgentVillage 의 별칭(`~/.claude/agent_village/aliases.json`) 과 데이터 연동.

---

## 환경

- Windows 11, PowerShell 5.1 (기본)
- Python 3.13 (가용)
- `claude` CLI in PATH (`--resume <session-id>` / `-p "<prompt>"` 지원)
- AgentVillage 프로젝트 위치: `G:\AgentVillage` (이 도구와 데이터만 연동, 코드는 독립)
- 이 프로젝트 위치: `G:\ClaudeSessionManager`

---

## 확정 사양 (사용자 결정 완료)

| 항목 | 값 |
|------|----|
| 형식 | PowerShell **TUI** (목록에서 선택). 자체 TUI 또는 `Out-GridView`/`Out-ConsoleGridView` 선택은 탐색부 결정 |
| 위치 | `G:\ClaudeSessionManager` (독립 저장소) |
| 기능 | (1) 세션 별칭 set/get/remove (2) 별칭으로 세션 재개 (3) 양방향 매핑 session↔alias (4) 세션 목록 표시 (주소록) |
| 별칭 저장 | `~/.claude/agent_village/aliases.json` — **AgentVillage 와 공유** (양쪽 변경 즉시 반영) |
| 별칭 검증 | AgentVillage 의 `validate_alias` 규칙과 동일 (길이 ≤ `ALIAS_MAX_LEN = 64`, 그 외 규칙은 코드 확인 필요) |

---

## AgentVillage 연동 포인트

별칭 파일 스키마는 `G:\AgentVillage\src\session_manager.py` 의 다음을 참고:
- `ALIAS_FILE = Path.home() / ".claude" / "agent_village" / "aliases.json"` (line 36)
- `read_alias_map()` (line 45) / `_write_alias_map_atomic()` (line 54): NamedTemporaryFile + `os.replace` 원자 쓰기 패턴
- `validate_alias` 규칙 (정확한 규칙은 그 함수 본문 확인 — 길이/공백/중복/정규식)
- `ALIAS_MAX_LEN = 64` (constants.py 또는 session_manager 상단)

스키마 추정: `{ session_id: { "alias": "..." } }` — 코드로 정확히 확인 필요.

세션 목록은 `~/.claude/projects/<sanitized-cwd>/<session-id>.jsonl` 글롭. cwd 변환 규칙은 `transcript_watcher.py` 또는 `session_manager.find_session_file` 참고.

**중요**: PowerShell 도구도 원자 쓰기 (`tempfile + Move-Item -Force` 같은 패턴) 적용. AgentVillage 가 동시에 쓸 수 있어 race condition 회피 필요.

---

## 다음 단계 (3-부서 파이프라인 적용)

신규 프로젝트지만 핵심 런타임 코드 (PowerShell 스크립트) 이므로 **CLAUDE.md 규칙대로 파이프라인 엄수**:

### 1. 탐색부 — 명세 조사
리더 `code-architect` 에게 지시서 받기 → 멤버 `system-analyzer` 에게 위임 → 리더 OK/REDO.

조사 항목 (S1~S8):
- S1: AgentVillage 별칭 스키마 (read_alias_map, validate_alias 규칙)
- S2: Claude 세션 파일 위치/형식 (~/.claude/projects/.../*.jsonl, cwd 변환)
- S3: `claude` CLI 명령 (--resume, -p, Start-Process 패턴, Windows Terminal `wt` 사용 가능성)
- S4: PowerShell TUI 옵션 (Out-GridView GUI / Out-ConsoleGridView 외부 모듈 / 자체 TUI). 외부 의존 최소화 선호
- S5: 별칭 검증 규칙 동기화 (PowerShell 측에도 동일 적용)
- S6: 파일 락/원자 쓰기 (AgentVillage 와 race condition 회피)
- S7: 세션 목록 표시 형식 (컬럼: session_id 단축형, alias, cwd, last_active, working agent. 정렬: last_active desc)
- S8: 양방향 매핑 API 설계

### 2. 사용자 확인
탐색부 결과 보고 후 TUI 라이브러리 / 모듈 형식 / 외부 의존 확정.

### 3. 실행부
리더 `feature-builder` 지시서 → 멤버 `domain-implementer` (PowerShell) + `interface-builder` (모듈 인터페이스) 분배.

산출물 예:
- `G:\ClaudeSessionManager\ClaudeSessionManager.psm1` (모듈)
- `G:\ClaudeSessionManager\Show-ClaudeSessions.ps1` (TUI 진입점)
- `G:\ClaudeSessionManager\README.md` (사용법)
- `G:\ClaudeSessionManager\Tests\` (선택)

### 4. QA
리더 `quality-leader` 지시서 → `code-reviewer` + `build-validator` 병렬 → 종합 PASS.

---

## 규칙 환기

- **3-부서 파이프라인 필수**: 리더(opus, 도구 없음) 지시서 → 메인 Claude → 멤버(sonnet) 실행 → 리더 검증.
- **메인 Claude 의 직접 Edit/Write 금지**: Hook 시스템이 `.py / .js / .ts / .cpp` 등 차단. `.ps1 / .psm1` 은 현재 hook 미차단이지만, **규칙대로 멤버 위임 권장**.
- **리더 도구 사용 금지**: code-architect / feature-builder / quality-leader 는 텍스트 지시서만.
- **멤버 호출 전 리더 미경유 차단**: Hook 2 가 강제. 부서별 30분 윈도우.
- **토큰 효율 규칙 7**: Grep 선행 → Read offset/limit 50-100 → 원문 인용 최소화.

Hook 시스템 상태:
- `~/.claude/hooks/block_main_edit.py`: 메인이 런타임 코드 직접 수정 차단
- `~/.claude/hooks/check_leader_first.py`: 멤버 호출 전 리더 미경유 차단
- `~/.claude/hooks/block_leader_tools.py`: 리더 도구 사용 차단
- 모두 PreToolUse 로 settings.json 에 등록됨

---

## PowerShell 5.1 코딩 노트

- 한글 출력: 콘솔 cp949 기본 → `[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new()` 필요 (또는 `chcp 65001`)
- JSON 읽기: `Get-Content -Raw | ConvertFrom-Json` (PS 5.1 은 `-AsHashtable` 미지원, PSCustomObject 반환)
- JSON 쓰기: `ConvertTo-Json -Depth 10 | Set-Content -Encoding UTF8`
- 원자 쓰기: `[System.IO.File]::WriteAllText($tmp, $json, [Text.Encoding]::UTF8); Move-Item -Force $tmp $target`
- 파일 락: `[System.IO.File]::Open($path, 'Open', 'Read', 'None')` 또는 별도 lockfile
- 새 세션 launch: `Start-Process pwsh -ArgumentList '-NoExit', '-Command', "claude --resume $sid"` (또는 `wt new-tab` 으로 Windows Terminal 탭)

---

## 미진행 / 잔여 — AgentVillage 측

이 문서와 무관한 AgentVillage 작업 (참고용, 모두 완료 상태):
- 휴게실 가운데 배치 + 부서 4개 가로 일렬 (MAIN 가운데) ✓
- 부서 리더 단독 책상+PC / 멤버 공용 책상 ✓
- 만석 시 휴게실 입석 5명 fallback ✓
- Hook 시스템 (block_main_edit / check_leader_first / block_leader_tools) ✓

---

## 첫 명령 예시 (새 세션에서)

> 이 폴더의 HANDOFF.md 를 읽고 PowerShell 세션 매니저 탐색부부터 시작해줘.

또는

> HANDOFF.md 사양대로 진행. S1~S8 탐색 위임.
