<p align="center">
  <img src="docs/icon.png" width="128" alt="RightClickGuardian icon">
</p>

<h1 align="center">右键小守卫 · RightClickGuardian</h1>

<p align="center">
  Q 萌、直观且带持续守护能力的 Windows 右键菜单管理器。
</p>

<p align="center">
  <a href="https://github.com/linzifeng07/RightClickGuardian/releases/latest">下载最新版</a>
  ·
  <a href="CHANGELOG.md">更新日志</a>
  ·
  <a href="LICENSE">MIT License</a>
</p>

## 它能做什么

右键小守卫用于扫描、预览、关闭和恢复 Windows 资源管理器的右键菜单项目。
它不只处理常见的静态注册表命令，还会覆盖动态扩展、现代应用、不同文件类型、
“新建”“发送到”“打开方式”和 Win+X 等来源。

- 🧹 **深度扫描**：同时扫描 64/32 位、当前用户/整机注册位置。
- 🧩 **软件专区**：按软件自动归类右键功能，支持整组一键关闭或勾选后批量关闭。
- 🖱️ **侧键导航**：鼠标侧键可在分类页和软件详情间前进、后退，并恢复滚动位置。
- 🛡️ **防回写守护**：被关闭的菜单如果被其他软件重新写入，会再次自动压制。
- 🧪 **右键实验室**：直接读取 Windows 为 PNG、JPG、视频、文档、文件夹等对象
  生成的实际右键菜单。
- 🎨 **真实图标与友好名称**：能解析到的应用图标会直接显示，GUID 和长技术标识
  不会占满界面。
- ⚡ **流畅列表**：全部项目采用按需加载，首批 48 项，滚动时继续加载。
- 🧰 **可恢复操作**：关闭前保存策略和必要备份，可在界面中恢复。
- ⚠️ **核心项保护**：系统关键入口会单独标识，并在关闭前再次确认。

## 下载与运行

1. 从 [Releases](https://github.com/linzifeng07/RightClickGuardian/releases/latest)
   下载最新版压缩包。
2. 解压到固定文件夹。
3. 双击 `右键小守卫.exe`，允许管理员权限。
4. 等待扫描完成，选择需要关闭的项目。

在左侧进入“软件专区”，点击带有软件图标和名称缩写的卡片，即可查看该软件
添加的右键功能。可单独切换，也可全选、关闭选中项，或一键关闭该软件全部功能。
相同处理程序在多种文件类型下的重复注册会自动合并，界面不会铺满重复项目。

程序需要管理员权限，因为它需要修改系统级右键菜单设置、保护策略目录，并建立
后台守护任务。发布文件目前没有商业数字签名，Windows 可能显示“未知发布者”。

## 守护机制

开启“守护中”后，程序会创建任务计划：

```text
\RightClickGuardian\Guard
```

保护策略和备份默认保存在：

```text
C:\ProgramData\RightClickGuardian
```

后台守护约每 1.5 秒核验一次传统菜单规则，并定期刷新现代应用菜单的 CLSID。
暂停守护只停止自动核验，不会自动恢复已经关闭的菜单；需要显示时请在界面中点击
“恢复显示”。

## 扫描范围

- `HKCU` / `HKLM`
- 64 位 / 32 位注册表视图
- 文件、文件夹、目录、目录背景、桌面背景
- 磁盘分区、所有文件系统对象、此电脑、回收站、库
- 文件格式专属 `shell` 与 `ContextMenuHandlers`
- `SystemFileAssociations`
- 现代 AppX / MSIX 应用菜单
- 新建菜单、发送到、打开方式、Win+X
- Explorer `CommandStore`

## 源码构建

要求：

- Windows 10/11
- .NET Framework 4.8 Developer Pack
- Visual Studio 2022，或系统自带的 .NET Framework C# 编译器

使用 Visual Studio 打开：

```text
src/RightClickGuardian/RightClickGuardian.csproj
```

也可以在 PowerShell 中构建并运行测试：

```powershell
.\build.ps1 -RunTests
```

生成文件位于 `artifacts/`。

## 安全与边界

- 软件只管理 Windows 资源管理器的右键菜单来源。
- 某些应用完全自绘的内部菜单不属于 Explorer 右键菜单，无法由本工具控制。
- 关闭系统核心项可能影响常用操作，请保留备份并谨慎选择。
- 项目不会上传扫描结果、注册表内容或本机文件列表。

安全问题请参阅 [SECURITY.md](SECURITY.md)。

## English

RightClickGuardian is a cute but capable Windows Explorer context-menu manager.
It performs broad registry and AppX discovery, shows friendly names and icons,
offers a real context-menu lab for different file types, and continuously
re-applies disabled rules when third-party software writes them back.

## License

[MIT](LICENSE)
