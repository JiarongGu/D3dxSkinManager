# D3dxSkinManager

[English](README.md) · **简体中文**

**面向 3DMigoto / XXMI 游戏 Mod 的管理器——收藏、整理、修复并部署你的模组。**

把你的整个模组收藏放进一个整洁、压缩的库里。一键开关模组、在游戏更新后修复它们、从模组站点下载新模组，然后启动游戏——本应用会把正确的文件放到 XXMI 期望的位置。

[![最新版本](https://img.shields.io/github/v/release/JiarongGu/D3dxSkinManager?label=下载)](https://github.com/JiarongGu/D3dxSkinManager/releases/latest)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)
[![Windows](https://img.shields.io/badge/Windows-10%2F11%20x64-0078D6)](https://github.com/JiarongGu/D3dxSkinManager/releases/latest)

![D3dxSkinManager——按角色整理的模组库](docs/user-guide/images/library.png)

> 与 **XXMI**（GIMI / ZZMI / SRMI / WWMI / HIMI / EFMI）**协同工作**。XXMI 负责游戏内注入与启动；本应用是围绕它的库与工作台。

---

## 📥 下载与安装

**[⬇️ 下载最新版本](https://github.com/JiarongGu/D3dxSkinManager/releases/latest)**（Windows x64）

1. 从 [Releases](https://github.com/JiarongGu/D3dxSkinManager/releases/latest) 下载 ZIP。
2. 解压到任意文件夹。
3. 运行 **`D3dxSkinManager.exe`**（启动器会在需要时自动安装 .NET 10 运行时）。

**系统要求：** Windows 10/11（64 位）。

---

## 📖 使用指南

第一次使用？完整指南会带你完成设置并逐一介绍每个功能，配有分步示例：

- **[使用指南（中文）](docs/user-guide/USER_GUIDE.cn.md)**
- **[User Guide (English)](docs/user-guide/USER_GUIDE.en.md)**

同一份指南也内置在应用中——点击右下角的版本号即可打开**帮助与文档**。

---

## ✨ 功能

- 📦 **导入** —— 拖放 `.zip` / `.7z` / `.rar` 压缩包或文件夹；一切都会被规整进一个紧凑的库。
- 🗂️ **整理** —— 层级分类、自定义标签，以及强大的搜索。
- ⚡ **一键开关** —— 把模组部署进 XXMI 的 `Mods` 文件夹；每个分类同一时间只有一个模组生效。
- 🌐 **远程库** —— 无需离开应用即可浏览并从站点（**Hui站**、**GameBanana**）下载模组。
- 🛠️ **更新后修复** —— 游戏更新后重新修复模组哈希，并内置健康度**分析**扫描。
- 🔗 **合并与优化** —— 把同角色的多个变体合并到一个**切换键**下；去重文件以节省空间。
- ⌨️ **按键与配置编辑器** —— 重绑切换键（键盘 + 手柄）并调整模组的安全设置。
- 🖼️ **预览与预设** —— 浏览模组截图；保存并恢复整套配装。
- 🎮 **XXMI 启动** —— 通过 XXMI 一键启动；每个游戏一个独立配置文件。
- ☁️ **在线存储** —— 在应用内登录下载站点（夸克）。
- 🌏 **中文与 English。**

![远程库——在应用内浏览并下载模组](docs/user-guide/images/remote.png)

---

## 🎮 支持的游戏

任何有 **XXMI** 导入器支持的游戏：

- 原神（GIMI）
- 绝区零（ZZMI）
- 崩坏：星穹铁道（SRMI）
- 鸣潮（WWMI）
- 崩坏3（HIMI）
- 终末地（EFMI）

> 你需要另行设置 **XXMI**（它负责把模组加载进游戏）。本应用管理并部署你的模组库，并可为你通过 XXMI 启动游戏。

---

## 🆘 需要帮助？

- **[📖 使用指南](docs/user-guide/USER_GUIDE.cn.md)** —— 每个功能怎么用
- **[📝 更新日志](CHANGELOG.md)** —— 有什么新变化
- **[🐛 报告问题 / 💡 提交建议](https://github.com/JiarongGu/D3dxSkinManager/issues)**

---

## 🔧 面向开发者

从源码构建、架构与贡献者文档都在 **[docs 文件夹](docs/)** 中——从 **[开发指南](docs/core/DEVELOPMENT.md)** 和 **[AI 指南](docs/AI_GUIDE.md)** 开始。

使用 .NET 10 + WinForms + WebView2（后端）与 React 19 + TypeScript + Vite（前端）构建。

---

## 许可证

基于 [MIT 许可证](LICENSE) 发布。© D3dxSkinManager 贡献者。
