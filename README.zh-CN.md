# Windows Clipboard Workspace（暂定项目名称）

**Windows / WPF 实验性社区移植分支；尚未确定正式品牌名称**

> **非官方独立衍生版本。** 本 Windows 项目受到 [Panzer-Jack 开发的 Clipori](https://github.com/Panzer-Jack/Clipori) 启发，并非原作者的官方 Windows 版本，也不代表原作者为此提供背书。产品名称与图标正在重新设计；当前开发程序仍暂用 `Clipori.Windows.exe`，不代表最终品牌。

[English](README.md) · [Windows 开发说明](windows/README.md) · [手工测试清单](windows/WINDOWS-SMOKE-TEST.md) · [原版 macOS 项目](https://github.com/Panzer-Jack/Clipori)

## 为什么开发这个分支？

Clipori 将轻量剪贴板与 AI 搜索、可选的自动分类结合起来，把转瞬即逝的复制内容变成可以找回的信息。我很认同这个产品理念，因此尝试将其移植到 Windows 10/11。

长期希望进一步探索**注重隐私的 AI 上下文工作空间**：在本地以结构化方式存储原始记录，按需将选定内容导出为 Markdown / JSON 上下文，并探索由用户授权的摘要、分类、去重及关联整理。**这些只是拟议方向，不是当前 Windows 版本的已交付功能。**

## Windows 版本进度

**已发布 v0.2.0-preview.1 预览版，但尚未完成 Windows 桌面端全面实测或代码签名。** 代码已具备部分功能框架，但编译成功不等于实际交互均已验证。

| 功能 | 状态 |
| --- | --- |
| Windows 10/11 x64、C# / .NET 10 WPF | 已有初步实现 |
| 文本、链接、图片采集；代码识别 | 已有初步实现，文件粘贴与代码识别仍需完善 |
| 本地 SQLite 历史记录及 PNG 缓存 | 已有初步实现 |
| 浮动卡片栏、全局快捷键、基础搜索、固定及笔记 | 已有初步实现，托盘和交互需验证 |
| 点击卡片复制 | 代码已实现，使用侧测试显示可复制 |
| 将内容直接粘贴回之前聚焦的应用 | **已知问题：焦点恢复或模拟粘贴可能失败** |
| AI 检索与可选自动分类 | 已有初步实现，仍需验证实际模型连接及隐私行为 |
| Star 知识收藏、Markdown 导出、同步和 AI 自动整理 | **规划中，尚未实现** |

### 近期优先事项（P0/P1）

- **P0 稳定性：** 修复焦点恢复与直接粘贴、增加诊断日志和错误记录、修复托盘图标、点击界面外自动隐藏，以及完善 Windows 手工测试。
- **P1 格式兼容性：** 补充文件粘贴，改进代码内容识别，并逐步完善与原版的使用体验差距。
- **M2 Star：** 收藏创建独立持久化快照、可选知识库路径、Markdown 单条笔记与生成式索引。
- **M3+ 扩展：** 可选的 Notion / Obsidian 同步、浏览器剪藏、用户授权的 AI 上下文与自动整理。

详细规划请看 [Pin/Star 产品路线图](docs/PRODUCT-STAR-ROADMAP.md) 和 [可机器读取的状态文件](project-site/roadmap.json)。

本地剪贴板采集与向 AI 服务发送信息必须区分。剪贴板可能包含敏感信息，不应在未经明确同意的情况下将整个数据库发送给第三方 AI。

## Windows 源码构建

需要 Windows 10/11 x64 和 .NET 10 SDK。从仓库根目录运行：

```powershell
cd windows
.\build-windows.ps1
.\publish\Clipori-Windows-win-x64\Clipori.Windows.exe
```

可执行文件名暂未完成品牌迁移。详见 [Windows 开发说明](windows/README.md) 与 [手工测试清单](windows/WINDOWS-SMOKE-TEST.md)。目前**不宣称已经提供官方或全面验证的 Windows 安装包**。

## 原作者、修改说明及许可证

- **原项目：** [Panzer-Jack/Clipori](https://github.com/Panzer-Jack/Clipori)（macOS / SwiftUI）。感谢原作者提供产品理念与开源代码。
- **Windows 分支：** 在 [`windows/`](windows/) 目录新增独立 WPF/.NET 实现，并规划独立的 Windows 开发方向。
- **许可证：** 原项目采用 [Apache License 2.0](LICENSE)。重新分发涉及的源码或二进制时，应遵守许可证、署名和修改声明要求。参见 [Windows 署名与修改说明](windows/NOTICE.md)，正式发行前还需要复核第三方依赖的许可。
- **品牌：** 文中提及 Clipori 是为了说明项目来源，**不代表官方合作或认可**。Windows 版本计划采用独立名称与图标；Apache-2.0 许可本身不授予商标或官方品牌使用权。

Mac 原版的说明、演示与发行包，请查看 [Clipori 原仓库](https://github.com/Panzer-Jack/Clipori)。本分支首页暂不将原作者的猫咪图标用于 Windows 品牌展示。

_本 README 于 2026-10-14 因 Windows 移植而重写。原版源码的既有版权及署名声明仍属于相应权利人。_