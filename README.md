# CircleLight

Windows 桌面像素风待办便签：置顶、可缩放、托盘常驻，所有任务存在一个 SQLite 文件里。

## 功能

- **快速录入**：点 `[ + NEW QUEST ]` 或标题栏的 `+`，输入标题回车即可；点输入框右侧的齿轮打开完整编辑。
- **任务操作**：左侧方框勾选完成，悬停行尾出现 `×` 删除，双击标题编辑。
- **像素动效**：新增任务时落地砸入；勾选完成时斜斩断行、粒子爆裂并抖动窗口。2.6 秒内连续完成会累积连击，×3 起出现 `PERFECT`；斜线颜色随连击从白到黄再到洋红。系统关闭「动画效果」时自动停用。
- **两个可选时间**：开始时间、截止时间互相独立，都可以留空。
  - 开始时间：提前 5 分钟通知一次，不参与超时判断。
  - 截止时间：到点仍未勾选则通知一次，并进入超时状态。
- **超时记录**：完成任务时记录「超时时长 = 完成时间 − 截止时间」，没有截止时间的任务不产生超时记录。
- **长期待办**：勾选后不消失、不删除，保持打勾；取消勾选可以再做一次。
- **今日已完成**：当天勾选的普通任务折叠在 `CLEARED` 里，次日从便签消失，完成记录保留。
- **历史统计**：按天列出完成时间、是否长期、当时的超时时长，并显示累计完成数与等级（每 20 次完成升 1 级）。
- **系统集成**：系统托盘常驻、开机自启开关、单实例、记住窗口位置和大小、原生 Windows 通知。
- **启动补发**：错过的截止提醒会在下次启动时合并成一条汇总通知。
- **MCP 接口**：11 个标准 MCP 工具，agent 可以查询、新建、改时间、完成、删除任务并读取历史。

## 运行

需要 Windows 与 .NET 10 SDK。

```powershell
cd <仓库根目录>
dotnet run --project src/CircleLight
```

便携版（自包含单文件，不需要安装 .NET）：

```powershell
.\dist\CircleLight.exe
```

重新发布：

```powershell
powershell -ExecutionPolicy Bypass -File publish.ps1
```

发布结果在 `dist/`：`CircleLight.exe`（自包含，约 80 MB）和 `CircleLight.Mcp.exe`（框架依赖，需要 .NET 10 运行时）。

## 数据

- 数据库：`%LOCALAPPDATA%\CircleLight\circlelight.db`（单个 SQLite 文件，备份就是复制它）。
- 日志：`%LOCALAPPDATA%\CircleLight\error.log`。
- 开机启动项写在 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`，值名 `CircleLight`，可在设置里开关。

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

## MCP 接口（给 agent 用）

`CircleLight.Mcp` 是一个 stdio MCP 服务，与便签共用同一个 `circlelight.db`。工作区配置在 `.zcode/config.json`，指向 `dist/CircleLight.Mcp.exe`；ZCode 在会话启动时自动连接，改完配置后新开一个会话即可看到 `circlelight` 服务器的工具。重新发布用 `publish.ps1`。

| 工具 | 作用 |
| --- | --- |
| `list_tasks` | 未完成任务、长期待办、今日已完成、今日进度 |
| `get_task` | 按 id 读取任务 |
| `create_task` | 新建任务：title、start_at、due_at、long_term |
| `update_task` | 修改标题或长期标记 |
| `set_task_schedule` | 设置/清除开始与截止时间（空字符串清除） |
| `complete_task` | 勾选完成 |
| `uncomplete_task` | 撤销勾选（删除今天的完成记录） |
| `delete_task` | 软删除任务，历史保留 |
| `list_completed` | 某天（默认今天）的完成记录 |
| `get_history` | 最近 N 天按天分组的完成记录 |
| `get_overview` | 今日进度、等级、逾期数量、累计完成数 |

时间格式：`19:30`（今天）、`2026-11-03 19:30`。`start_at` 负责提前 5 分钟提醒，`due_at` 负责超时判断与超时时长记录，两者可以只设一个。工具失败时返回 `{"ok": false, "error": "..."}`。

- 便签开着时，MCP 的修改会在约 1.2 秒内自动刷新到界面。
- 便签关着时也可以操作，下次打开可见。
- 提醒仍由便签进程负责：MCP 新建的定时任务会进入同一套提醒轮询。

示例说法：

- 「把明天上午 10 点交周报加进待办，提前 5 分钟提醒我」
- 「今天完成了哪些事？」
- 「把写周报标记为完成」
- 「把今晚跑步的截止时间改到 22:30」

## 代码结构

```
src/CircleLight.Core/
  Data/TaskStore.cs             SQLite 表结构、任务与完成记录
  Util/UserTime.cs              用户时间输入解析
  Util/TimeText.cs              时间与时长展示
  Storage/AppPaths.cs           数据库路径
src/CircleLight/
  Services/ReminderService.cs   15 秒轮询、启动补发、跨天刷新
  Services/NotificationService.cs 原生 toast，失败退回应用内提示
  Services/TrayService.cs       托盘图标与菜单（图标运行时绘制）
  Services/StartupService.cs    开机自启注册表
  ViewModels/                   主界面、任务行、历史分组
  Views/                        任务编辑、历史、设置、兜底提示
  Styles/Pixel.xaml             颜色、按钮、复选框、滚动条、对话框外壳
  MainWindow.xaml               便签主界面（含 data_version 自动刷新）
src/CircleLight.Mcp/
  Program.cs                    stdio MCP 服务入口
  TodoTools.cs                  11 个待办工具
publish.ps1                     发布到 dist/ 的单文件 exe
```

## 设计记录

- [`GLOSSARY.md`](GLOSSARY.md)：任务、长期待办、开始/截止时间、超时、完成记录等术语。
- [`docs/adr/0001-tech-stack.md`](docs/adr/0001-tech-stack.md)：WPF + SQLite 单文件。
- [`docs/adr/0002-long-term-tasks.md`](docs/adr/0002-long-term-tasks.md)：长期待办的完成语义。
- [`docs/adr/0003-opaque-window.md`](docs/adr/0003-opaque-window.md)：为什么放弃分层窗口的硬阴影。
- [`docs/adr/0004-mcp-server.md`](docs/adr/0004-mcp-server.md)：MCP 独立进程与 GUI 的共享/刷新策略。

## 已知边界

- 仅支持 Windows。
- 单机使用，没有云同步或多端。
- 不做拖拽排序、重复规则、子任务、标签、日历视图。
- 通知依赖 Windows 通知中心；被系统专注助手/通知设置拦截时不会弹出，程序内会尝试兜底提示。
