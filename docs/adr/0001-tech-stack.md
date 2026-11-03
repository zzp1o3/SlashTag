# 0001 - 用 WPF + SQLite 单文件实现 Windows 待办便签

- 状态：已接受
- 日期：2026-11-03

## 背景

程序只在 Windows 上自用，需要：置顶无边框便签、可拖动缩放、系统托盘常驻、开机自启、原生通知、单文件本地数据。界面采用像素游戏任务面板风格，不追求跨平台。

## 决策

- **.NET 10 + WPF**：Windows 原生，托盘、通知、自启、无边框窗口都有成熟做法；`dotnet` CLI 可直接构建，无需 Visual Studio。
- **Microsoft.Data.Sqlite**：数据存为单个 SQLite 文件，位于 `%LOCALAPPDATA%\SlashTag\slashtag.db`，与 exe 分离，备份即复制一个文件。
- **CommunityToolkit.WinUI.Notifications**：在非打包 WPF 应用里发送 Windows 原生 toast。
- **不引入 MVVM 框架**：一个窗口、两三个对话框，手写 `INotifyPropertyChanged` 足够。

## 备选方案

- **Avalonia**：界面更现代、可跨平台；但跨平台对本项目没有价值，托盘与通知依赖第三方库，无边框窗口的坑更多。
- **WinUI 3**：视觉更贴近 Windows 11，但非打包部署、托盘与开机自启都更麻烦。
- **Electron / Tauri**：Web 技术做像素风很顺手，但体积、内存与系统集成成本不匹配一个小工具；Tauri 还需额外安装 Rust。
- **JSON 文件代替 SQLite**：人眼可读，但按天统计、完成记录查询要自己实现；数据量虽小，SQLite 仍更省事。

## 后果

- 程序绑定 Windows，无法直接复用到 Mac/Linux。
- 数据层与 UI 分离，未来若换 UI 框架，SQLite 文件可以直接沿用。
- 通知依赖系统通知中心；如果 toast 注册失败，需要退回应用内提示（实现中保留兜底）。
