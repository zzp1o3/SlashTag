# 0004 - MCP 服务作为独立 stdio 进程，与 GUI 共享 SQLite

- 状态：已接受
- 日期：2026-11-03

## 背景

希望 agent 能读取并操作待办：查询任务、新建、改时间、勾选、删除、看历史。MCP 客户端通常以子进程方式启动 stdio 服务，而待办 GUI 是一个已经在运行的 WPF 进程。

## 决策

- 新增 `CircleLight.Mcp` 控制台进程，使用官方 `ModelContextProtocol` C# SDK（2.3.1），stdio 传输。
- 抽出 `CircleLight.Core`（`TaskStore`、模型、时间解析、数据路径），GUI 与 MCP 共用同一套数据逻辑。
- 两个进程各自持有 SQLite 连接，设置 `PRAGMA busy_timeout = 5000`。
- GUI 每 1.2 秒读取一次 `PRAGMA data_version`；发现外部提交就重载列表与统计。
- MCP 工具的内部错误（校验失败、任务不存在、数据库异常）统一返回 `{ "ok": false, "error": "..." }`，避免 SDK 把异常折叠成 “An error occurred invoking…” 而丢失原因。
- 工作区 `.zcode/config.json` 注册服务器 `circlelight`，会话启动时自动连接。

## 备选方案

- **在 WPF 进程里内嵌 HTTP MCP**：需要 ASP.NET Core、端口管理和鉴权，而且 agent 必须依赖 GUI 正在运行。
- **MCP 服务通过命名管道/HTTP 转发给 GUI**：多一套协议、重连和生命周期处理，收益不明显。
- **让 agent 直接读写 SQLite**：没有 schema 校验，绕过了提醒标记与完成记录逻辑，容易写坏数据。

## 后果

- agent 在便签关闭时也能操作；下次打开便签即可看到结果。
- 便签开着时，外部修改最多约 1.2 秒后出现在界面上。
- 多一个需要构建的 exe；`.zcode/config.json` 里是绝对路径，仓库移动后需要同步修改。
- 提醒仍只由 GUI 进程负责：MCP 创建的定时任务会进入同一套轮询，但 GUI 没运行就不会弹通知（与手动录入一致）。
- 两个连接共享一个数据库文件，写冲突由 busy_timeout 消化。
