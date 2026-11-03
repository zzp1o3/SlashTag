<h1 align="center">SlashTag</h1>

<p align="center"><b>把待办当成怪物：标记、斜斩、升级。</b></p>

<p align="center">
  <a href="README.md">English</a> ·
  <a href="README.zh-CN.md">简体中文</a> ·
  <a href="https://github.com/zzp1o3/SlashTag/releases">下载</a> ·
  <a href="#mcp-接口给-agent-用">MCP 工具</a>
</p>

<p align="center">
  <img alt="平台" src="https://img.shields.io/badge/platform-Windows%2010%2F11-0078D4?style=flat-square" />
  <img alt=".NET 10" src="https://img.shields.io/badge/.NET-10-512BD4?style=flat-square" />
  <img alt="WPF" src="https://img.shields.io/badge/UI-WPF-68217A?style=flat-square" />
  <img alt="SQLite" src="https://img.shields.io/badge/storage-SQLite-003B57?style=flat-square" />
  <img alt="MCP" src="https://img.shields.io/badge/agent-MCP-FF6FB5?style=flat-square" />
</p>

Windows 桌面像素风待办便签：置顶、可缩放、托盘常驻。把每条任务当成一只怪物——一行输入抓进来，用斜斩把它砍掉，连续完成叠出连击，经验升级。长期待办和每日次数任务（比如喝水 ×8）每天 0 点刷新，但完成记录一条不丢。

<p align="center">
  <img src="https://github.com/user-attachments/assets/39fffd60-ce1d-4f00-afc3-944a83c24695" width="42%" alt="任务列表与每日次数任务" />
  <img src="https://github.com/user-attachments/assets/cf8a34c9-3c5f-4d3f-b5b5-b0e3dcad6f9e" width="42%" alt="斜斩完成特效" />
</p>
<p align="center">
  <img src="https://github.com/user-attachments/assets/b03880ce-170d-48a2-a4d3-3e69d4447d11" width="42%" alt="连击与升级反馈" />
  <img src="https://github.com/user-attachments/assets/a3419e16-0842-4f43-8765-c45e4697bad7" width="42%" alt="带次数进度的历史记录" />
</p>

**演示**

https://github.com/user-attachments/assets/3098c0e8-2fbe-4cbd-9e52-f4c209686c67

## 为什么是 SlashTag

- **有打击感，不是打勾。** 完成任务时斜线扫过、行断成两半、粒子爆裂、窗口抖动；2.6 秒内连续完成叠出连击，×3 起 `PERFECT`，斜斩方向和颜色随连击交替变化。
- **是任务，也是怪物。** 长期待办和每日次数任务每天 0 点刷新，但每次勾选都留在历史里、都给经验；升级实时结算，弹出 `LEVEL UP` 横幅和彩纸。
- **为 agent 而生。** 独立 stdio MCP 服务和便签共用同一个 SQLite 文件，agent 可以查询、新建、改时间、完成、撤销、删除、读历史；便签开着时约 1.2 秒内自动刷新。
- **一个文件，无需安装。** GUI 是自包含单文件 exe，数据就是一个 SQLite 文件，备份即复制。

## 功能

- **快速录入**：点 `[ + NEW QUEST ]` 或标题栏的 `+`，输入标题回车即可；点输入框右侧的齿轮打开完整编辑。
- **任务操作**：左侧方框勾选完成，悬停行尾出现 `×` 删除，双击标题编辑。
- **像素动效**：新增任务落地砸入；完成时斜斩断行、粒子爆裂、窗口抖动。2.6 秒内连续完成累积连击，×3 起出现 `PERFECT`；斜线颜色随连击从白到黄再到洋红。系统关闭「动画效果」时自动停用。
- **两个可选时间**：开始时间、截止时间互相独立，都可以留空。
  - 开始时间：提前 5 分钟通知一次，不参与超时判断。
  - 截止时间：到点仍未勾选则通知一次，并进入超时状态。
- **超时记录**：完成任务时记录「超时时长 = 完成时间 − 截止时间」，没有截止时间的任务不产生超时记录。
- **长期待办**：勾选后不消失、不删除，保持打勾到当天结束；每天 0 点自动取消勾选，可以再做一次。当天的完成记录保留，再次完成会再记一条经验。
- **每日任务**：带目标次数（如喝水 ×8）。每次勾选进度 +1 并记一条经验，达到目标后勾选完成；达标后取消勾选进度清零、完成记录保留；再次勾选继续累加经验；次日零点自动清零，任务留在列表。
- **今日已完成**：当天勾选的普通任务折叠在 `CLEARED` 里，次日从便签消失；任务被删除后，当天的完成记录仍留在 `CLEARED`。
- **历史统计**：按天列出每次勾选的时间、进度（每日任务显示 `3/8`）、是否长期、当时的超时时长，并显示累计完成数与等级（每 20 次完成升 1 级；升级时有 `LEVEL UP` 横幅、彩纸和鼓励语）。
- **窗口内切换页面**：历史、设置、任务编辑都在同一个便签窗口里打开，标题栏用 `←` 返回。
- **系统集成**：系统托盘常驻、开机自启开关、单实例、记住窗口位置和大小、原生 Windows 通知。
- **启动补发**：错过的截止提醒会在下次启动时合并成一条汇总通知。
- **MCP 接口**：11 个标准 MCP 工具，agent 可以查询、新建、改时间、完成、删除任务并读取历史。

## 技术栈

| 层 | 选型 | 原因 |
| --- | --- | --- |
| 界面 | C# + WPF（.NET 10） | 原生 Windows，自绘像素外壳与控件，不依赖 UI 框架 |
| 数据 | SQLite（Microsoft.Data.Sqlite） | 单文件存储、备份简单、GUI 与 agent 共用 |
| Agent | ModelContextProtocol C# SDK（stdio） | 同一套领域层直接暴露为 MCP 工具，不另建服务 |
| 通知 | CommunityToolkit.WinUI.Notifications | 原生 Windows toast，失败时退回应用内提示 |
| 发布 | 单文件发布 | 便携 exe，GUI 无需安装运行时 |

解决方案分三层：`SlashTag.Core`（领域与存储）、`SlashTag`（WPF 便签）、`SlashTag.Mcp`（stdio 服务）。

## 运行

预编译版：从 [Releases](https://github.com/zzp1o3/SlashTag/releases) 下载 `SlashTag.exe`，双击即可运行（自包含，不需要安装 .NET）。

从源码运行需要 Windows 与 .NET 10 SDK：

```powershell
cd <仓库根目录>
dotnet run --project src/SlashTag
```

重新发布到 `dist/`：

```powershell
powershell -ExecutionPolicy Bypass -File publish.ps1
```

发布结果：`SlashTag.exe`（自包含，约 80 MB）和 `SlashTag.Mcp.exe`（框架依赖，需要 .NET 10 运行时）。

## 交互速查

| 操作 | 位置 |
| --- | --- |
| 拖动窗口 | 黄色标题条空白处 |
| 缩放窗口 | 拖动任意窗口边缘 |
| 收进托盘 | 标题条右侧 `×` |
| 从托盘回来 | 托盘图标双击，或再次启动 exe（单实例会唤回） |
| 展开/收起今日已完成 | `▸ CLEARED n` |
| 撤销完成 | 在展开的已完成列表里取消勾选 |
| 快速新建 | `[ + NEW QUEST ]`，输入标题后回车 |
| 完整编辑 | 双击任务标题，或快速输入框右侧齿轮 |
| 保存 / 取消 | 回车 / Esc |
| 返回主列表 | 页面标题栏左侧 `←` 或 Esc |

## MCP 接口（给 agent 用）

`SlashTag.Mcp` 是一个 stdio MCP 服务，与便签共用同一个 `slashtag.db`。

先构建：

```powershell
powershell -ExecutionPolicy Bypass -File publish.ps1
# -> dist\SlashTag.Mcp.exe
```

发布出来的是框架依赖版本，需要安装 .NET 10 运行时。不想装运行时的话，发布自包含版本：

```powershell
dotnet publish src/SlashTag.Mcp/SlashTag.Mcp.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o dist
```

### 通用 MCP 客户端（Claude Desktop、Cursor 等）

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

### ZCode 工作区

仓库根目录的 `.zcode/config.json`：

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

注册后按客户端提示信任/启用该服务，再新开一个会话。工具名形如 `mcp__slashtag__list_tasks`。

验证：让 agent「列出我的待办」，它应该调用 `list_tasks`。Windows 下用绝对路径，JSON 里反斜杠要写成 `\\`。agent 和便签需要用同一个 Windows 用户运行，才能共用 `%LOCALAPPDATA%\SlashTag\slashtag.db`。

便签开着时，MCP 的修改约 1.2 秒内刷新到界面；便签关着时也可以操作，下次打开可见。提醒仍由便签进程负责。

| 工具 | 作用 |
| --- | --- |
| `list_tasks` | 未完成任务、长期待办、次数任务、今日已完成、今日进度 |
| `get_task` | 按 id 读取任务 |
| `create_task` | 新建任务：title、start_at、due_at、long_term、target_count |
| `update_task` | 修改标题、长期标记或目标次数 |
| `set_task_schedule` | 设置/清除开始与截止时间（空字符串清除） |
| `complete_task` | 勾选完成；次数任务每次 +1，达标才勾选 |
| `uncomplete_task` | 撤销勾选；普通任务删当天记录，长期/次数任务保留记录 |
| `delete_task` | 软删除任务，历史保留 |
| `list_completed` | 某天（默认今天）的完成记录 |
| `get_history` | 最近 N 天按天分组的完成记录 |
| `get_overview` | 今日进度、等级、逾期数量、累计完成数 |

时间格式：`19:30`（今天）、`2026-11-03 19:30`。`start_at` 负责提前 5 分钟提醒，`due_at` 负责超时判断与超时时长记录，两者可以只设一个。`target_count`（1-99，默认 1）大于 1 时是每日任务，`progress` 字段返回 `3/8` 这样的进度。工具失败时返回 `{"ok": false, "error": "..."}`。

- 便签开着时，MCP 的修改会在约 1.2 秒内自动刷新到界面。
- 便签关着时也可以操作，下次打开可见。
- 提醒仍由便签进程负责：MCP 新建的定时任务会进入同一套提醒轮询。

示例说法：

- 「把明天上午 10 点交周报加进待办，提前 5 分钟提醒我」
- 「今天完成了哪些事？」
- 「把写周报标记为完成」
- 「把今晚跑步的截止时间改到 22:30」

## 数据与隐私

- 全部数据留在本机：无账号、无遥测、无网络请求。
- 数据库：`%LOCALAPPDATA%\SlashTag\slashtag.db`，备份就是复制它。
- 日志：`%LOCALAPPDATA%\SlashTag\error.log`。
- 开机启动项：`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`，值名 `SlashTag`。

## 代码结构

```
src/SlashTag.Core/
  Data/TaskStore.cs             SQLite 表结构、任务与完成记录
  Util/UserTime.cs              用户时间输入解析
  Util/TimeText.cs              时间与时长展示
  Storage/AppPaths.cs           数据库路径与旧版本数据迁移
src/SlashTag/
  Services/ReminderService.cs   15 秒轮询、启动补发、跨天刷新
  Services/EffectService.cs     斜斩、粒子、连击、升级动效
  Services/NotificationService.cs 原生 toast，失败退回应用内提示
  Services/TrayService.cs       托盘图标与菜单
  Services/StartupService.cs    开机自启注册表
  ViewModels/                   主界面、任务行、历史分组
  Views/                        历史、设置、编辑器页面与兜底提示
  Styles/Pixel.xaml             颜色、按钮、复选框、滚动条、对话框外壳
  MainWindow.xaml               便签主界面（含 data_version 自动刷新）
src/SlashTag.Mcp/
  Program.cs                    stdio MCP 服务入口
  TodoTools.cs                  11 个待办工具
publish.ps1                     发布到 dist/ 的单文件 exe
```

## 设计记录

- [`GLOSSARY.md`](GLOSSARY.md)：任务、长期待办、每日任务、开始/截止时间、超时、完成记录等术语。
- [`docs/adr/0001-tech-stack.md`](docs/adr/0001-tech-stack.md)：WPF + SQLite 单文件。
- [`docs/adr/0002-long-term-tasks.md`](docs/adr/0002-long-term-tasks.md)：长期待办的完成语义。
- [`docs/adr/0003-opaque-window.md`](docs/adr/0003-opaque-window.md)：为什么放弃分层窗口的硬阴影。
- [`docs/adr/0004-mcp-server.md`](docs/adr/0004-mcp-server.md)：MCP 独立进程与 GUI 的共享/刷新策略。

## 已知边界

- 仅支持 Windows。
- 单机使用，没有云同步或多端。
- 不做拖拽排序、重复规则、子任务、标签、日历视图。
- 通知依赖 Windows 通知中心；被系统专注助手/通知设置拦截时不会弹出，程序内会尝试兜底提示。

---

[English](README.md) · [简体中文](README.zh-CN.md)
