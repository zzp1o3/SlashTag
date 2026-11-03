# CircleLight 项目约定

## 待办数据

- 待办通过 `circlelight` MCP 服务操作，工具见 `README.md` 的「MCP 接口」。不要直接改 `%LOCALAPPDATA%\CircleLight\circlelight.db`。
- 新建任务前先 `list_tasks` 查重；时间用 `yyyy-MM-dd HH:mm` 或 `HH:mm`（今天）。
- 完成、撤销、删除分别用 `complete_task`、`uncomplete_task`、`delete_task`，不要用 SQL 模拟。
- 用户说「明天下午三点提醒我」这类话时，先换算成绝对日期，再用 `create_task` / `set_task_schedule`。
- `start_at` 是提前 5 分钟提醒的锚点；`due_at` 是超时判断与超时时长记录的锚点，两者可以只设一个。

## 开发

- 共享数据层在 `src/CircleLight.Core`，GUI（`src/CircleLight`）与 MCP 服务（`src/CircleLight.Mcp`）都依赖它。
- 构建：`dotnet build -c Release`。工作区 MCP 配置指向 `dist/CircleLight.Mcp.exe`，用 `publish.ps1` 重新发布；仓库移动后要同步改 `.zcode/config.json`。
- 设计决策记录在 `docs/adr/`；领域术语在 `GLOSSARY.md`。
- 修改时间/提醒逻辑时，同时验证 GUI 手动操作与 MCP 工具两条路径。
