<h1 align="center">SlashTag</h1>

<p align="center"><b>Tasks as monsters. Tag them, slash them, level up.</b></p>

<p align="center">
  <a href="README.zh-CN.md">简体中文</a> ·
  <a href="https://github.com/zzp1o3/SlashTag/releases">Download</a> ·
  <a href="#mcp-interface-for-agents">MCP tools</a>
</p>

<p align="center">
  <img alt="Platform" src="https://img.shields.io/badge/platform-Windows%2010%2F11-0078D4?style=flat-square" />
  <img alt=".NET 10" src="https://img.shields.io/badge/.NET-10-512BD4?style=flat-square" />
  <img alt="WPF" src="https://img.shields.io/badge/UI-WPF-68217A?style=flat-square" />
  <img alt="SQLite" src="https://img.shields.io/badge/storage-SQLite-003B57?style=flat-square" />
  <img alt="MCP" src="https://img.shields.io/badge/agent-MCP-FF6FB5?style=flat-square" />
</p>

A pixel-art Windows todo note that treats every task like an enemy: capture it in one line, cut it down with a slash, chain completions into a combo, and watch your level rise. Long-term tasks and counted daily quests (drink water ×8) reset at midnight while keeping every completion in history.

<p align="center">
  <img src="https://github.com/user-attachments/assets/39fffd60-ce1d-4f00-afc3-944a83c24695" width="42%" alt="Quest log with daily counted tasks" />
  <img src="https://github.com/user-attachments/assets/cf8a34c9-3c5f-4d3f-b5b5-b0e3dcad6f9e" width="42%" alt="Slash completion effect" />
</p>
<p align="center">
  <img src="https://github.com/user-attachments/assets/b03880ce-170d-48a2-a4d3-3e69d4447d11" width="42%" alt="Combo and level-up feedback" />
  <img src="https://github.com/user-attachments/assets/a3419e16-0842-4f43-8765-c45e4697bad7" width="42%" alt="History with per-check progress" />
</p>

**Demo**

https://github.com/user-attachments/assets/3098c0e8-2fbe-4cbd-9e52-f4c209686c67

## Why SlashTag

- **Impact, not checkboxes.** Completing a task sweeps a pixel slash across the row, splits it in half, bursts particles and shakes the note. Finish within 2.6 seconds to build a combo; ×3 triggers `PERFECT`. Slash directions and colors alternate as the combo climbs.
- **Quests, not records.** Long-term tasks and counted daily tasks (`drink water ×8`) reset at midnight, keep every check in history, and award XP per check. Levels update live, with a `LEVEL UP` banner and confetti.
- **Agent-native.** A stdio MCP server shares the same SQLite file as the note. Agents can list, create, schedule, complete, undo, delete, and read history or overview; the note refreshes within ~1.2 s while open.
- **One file, no installer.** The GUI is a self-contained single-file executable, and all data lives in one SQLite file you can copy for backup.

## Features

- **Quick capture** — one-line input, Enter to add. The gear opens the full editor.
- **Two independent times** — a start time for a 5-minute-early reminder, a due time for overdue tracking. Either can be empty.
- **Daily quests** — `target_count` 1–99. Every check adds progress and XP; reaching the target checks the task. Midnight resets progress, not history.
- **Long-term tasks** — stay in the list and reset every midnight, so they can be done again the next day.
- **CLEARED** — today's finished normal tasks fold away; deleting a task keeps its completion record in `CLEARED` for the day.
- **History** — grouped by day, with progress per check (`3/8`), overdue duration, lifetime count and level.
- **Desktop integration** — always-on-top note, tray icon, autostart toggle, single instance, remembered window bounds, native Windows toasts.
- **UI preferences** — switch Chinese/English in Settings; Ctrl+wheel zooms the note and fonts (0.8×–1.6×); a slim 5 px capsule scrollbar.
- **Edge auto-hide** — click the pin to keep the note open. Unpinned, it slides into the outer edge of its monitor leaving a yellow `!` tab; hover the tab to slide it back, and it hides again 1 s after the pointer leaves. Multi-monitor and mixed-DPI aware.

## Tech stack

| Layer | Choice | Why |
| --- | --- | --- |
| UI | C# + WPF on .NET 10 | Native Windows, custom pixel chrome and controls without a UI framework |
| Data | SQLite via `Microsoft.Data.Sqlite` | One portable file, simple backup, shared by GUI and agent |
| Agent | `ModelContextProtocol` C# SDK (stdio) | The same domain layer exposed as MCP tools, no separate service |
| Notifications | `CommunityToolkit.WinUI.Notifications` | Native Windows toasts, with an in-app fallback |
| Packaging | Single-file publish | Portable exe, no installer or runtime for the GUI |

The solution is split into three projects: `SlashTag.Core` (domain and storage), `SlashTag` (WPF app), and `SlashTag.Mcp` (stdio server).

## Quick start

Download `SlashTag.exe` from [Releases](https://github.com/zzp1o3/SlashTag/releases) and run it — self-contained, no .NET installation needed.

Or build from source (Windows + .NET 10 SDK):

```powershell
dotnet run --project src/SlashTag
```

Publish portable binaries to `dist/`:

```powershell
powershell -ExecutionPolicy Bypass -File publish.ps1
```

## MCP interface for agents

`SlashTag.Mcp` is a stdio MCP server sharing the same `slashtag.db` as the note.

Build it first:

```powershell
powershell -ExecutionPolicy Bypass -File publish.ps1
# -> dist\SlashTag.Mcp.exe
```

The published server is framework-dependent and needs the .NET 10 runtime. To ship it without one, publish a self-contained copy:

```powershell
dotnet publish src/SlashTag.Mcp/SlashTag.Mcp.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o dist
```

### Generic MCP client (Claude Desktop, Cursor, …)

```json
{
  "mcpServers": {
    "slashtag": {
      "command": "C:\\path\\to\\dist\\SlashTag.Mcp.exe",
      "args": []
    }
  }
}
```

### ZCode workspace

`.zcode/config.json` in the repo root:

```json
{
  "mcp": {
    "servers": {
      "slashtag": {
        "type": "stdio",
        "command": "C:\\path\\to\\dist\\SlashTag.Mcp.exe",
        "args": [],
        "enabled": true
      }
    }
  }
}
```

Register the server, trust/enable it if your client asks, then start a new session. Tool names look like `mcp__slashtag__list_tasks`.

Verify it works by asking the agent to "list my todos"; it should call `list_tasks`. Use absolute paths — JSON needs `\\` on Windows. The agent and the note should run as the same Windows user so they share `%LOCALAPPDATA%\SlashTag\slashtag.db`.

While the note is open, agent writes show up in about 1.2 s; with the note closed, changes are visible next time it opens. Reminders stay with the note process.

| Tool | What it does |
| --- | --- |
| `list_tasks` | Active, long-term, counted and cleared-today tasks plus today's progress |
| `get_task` | Read one task by id |
| `create_task` | Create a task: title, start_at, due_at, long_term, target_count |
| `update_task` | Change title, long-term flag or target count |
| `set_task_schedule` | Set or clear start/due times (`""` clears) |
| `complete_task` | Complete a task; counted tasks advance by one |
| `uncomplete_task` | Undo; normal tasks drop today's record, long-term/counted keep it |
| `delete_task` | Soft delete; history is preserved |
| `list_completed` | Completion records for a day (default today) |
| `get_history` | Completion records grouped by day for the last N days |
| `get_overview` | Today's progress, level, overdue count, lifetime completions |

Times accept `19:30` (today) or `2026-11-03 19:30`. `start_at` drives the early reminder, `due_at` drives overdue tracking. `target_count` above 1 makes a daily task; `progress` returns values like `3/8`. Failures return `{"ok": false, "error": "..."}`.

## Data and privacy

- Everything stays on your machine: no account, no telemetry, no network calls.
- Database: `%LOCALAPPDATA%\SlashTag\slashtag.db` — copy it to back up.
- Log: `%LOCALAPPDATA%\SlashTag\error.log`.
- Autostart: `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, value `SlashTag`.

## Project layout

```
src/SlashTag.Core/        domain, SQLite store, time parsing, paths
src/SlashTag/             WPF note, effects, reminders, tray, pages
src/SlashTag.Mcp/         stdio MCP server and the 11 tools
publish.ps1               single-file publish to dist/
```

## Design notes

- [`GLOSSARY.md`](GLOSSARY.md) — tasks, long-term tasks, daily tasks, overdue, completion records.
- [`docs/adr/0001-tech-stack.md`](docs/adr/0001-tech-stack.md) — WPF + SQLite single file.
- [`docs/adr/0002-long-term-tasks.md`](docs/adr/0002-long-term-tasks.md) — completion semantics.
- [`docs/adr/0003-opaque-window.md`](docs/adr/0003-opaque-window.md) — why layered windows were dropped.
- [`docs/adr/0004-mcp-server.md`](docs/adr/0004-mcp-server.md) — sharing and refresh strategy for the MCP server.

## Known limits

- Windows only.
- Local and single-device; no cloud sync.
- No drag reordering, recurrence rules, subtasks, tags or calendar view.
- Toasts depend on Windows notification settings; an in-app fallback is shown when they are blocked.

---

[English](README.md) · [简体中文](README.zh-CN.md)
