# 篝火堡垒 · Hearthhold

Windows PC 原型，版本 0.4.0-preview。原创的建造与异步攻城方向项目。

当前交付包括一个轻量的 Windows 原生试玩程序，以及共享相同 C# 规则、战斗逻辑和模型几何的 Unity 3D Windows 试玩版。原生程序用 WinForms/GDI+ 绘制等距画面；Unity 客户端把同一份几何转为 URP 三维网格。Unity 端已完成首次导入、脚本编译、Windows x64 构建和实际画面烟雾测试。

## 本地构建与试玩

Git 仓库只保存源码、配置、测试和必要文档，不提交可执行文件、发布压缩包、Unity 安装器、测试数据或用户存档。克隆后在项目目录执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\build-preview.ps1 -Test -Package
```

随后双击 `artifacts/WindowsPreview/Hearthhold.exe`，或解压 `artifacts/Hearthhold-0.2.0-win-x64.zip`。运行环境为 Windows x64 和 .NET Framework 4.x；本机已完成编译和交互验证，无需 Unity 或 .NET SDK。

Unity 3D 版使用 Unity `6000.6.0f1` 构建：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\build-unity.ps1 -Action Build -Package
```

构建后运行 `artifacts/WindowsUnity/Hearthhold.exe`，发布包为 `artifacts/Hearthhold-0.4.0-unity-win-x64.zip`。首次构建需要已安装、登录并激活的 Unity 编辑器；发布包运行时不需要安装 Unity。

第一次进入时，已有一座主城、两座金矿、一座晶露池、一座远征营、两座防御建筑和一段城墙，初始资源足够体验建造与升级。可以先收取产出、摆放建筑，再点右下角“出发远征”。

![Unity 3D 基地画面](artifacts/screenshots/20-unity-home-v04.png)

## 本轮更新

- 0.4 强化 Unity 表现层：建筑选中圆环、带模型的建造/移动预览、橙色投兵边界和平滑兵种移动。
- 远征加入三维远程弹道、近战冲击圈、治疗范围圈、受击闪光与建筑摧毁碎片；血条会按健康、受伤、濒危变色。
- 战斗信息显示存活与待命人数，地图投兵提示增加高对比底板；新增首页和交战阶段两套成品烟雾测试。
- Unity 工程已升级并锁定为 Unity 6000.6.0f1 / URP 17.6.0，生成完整项目设置、资源 `.meta`、主场景和包锁文件；Windows x64 构建已实际成功。
- 重做 7 类建筑、1—3 级外观和 4 类兵种：加入屋瓦、拱门、砖石接缝、金矿道具、晶簇、武器与护甲，建筑卡片和兵种图鉴使用对应模型。
- 选中建筑后按 Delete 或点击“拆除建筑”，确认后拆除并返还建设、历次升级投入的 50%。议事堡不可拆；返还受仓储上限限制；拆除不可撤销。
- 建造卡片显示“已建 / 上限”。上限随议事堡升级增加，拆除释放名额，移动不消耗名额。
- 按 I 打开兵种图鉴；战斗右侧显示当前兵种的职责、数值、战术和弱点；本次运行第一次出征自动显示投兵教学。

| 建筑 | 议事堡 1 级 | 2 级 | 3 级 |
|---|---:|---:|---:|
| 议事堡 | 1 | 1 | 1 |
| 金矿 | 3 | 4 | 5 |
| 晶露池 | 2 | 3 | 4 |
| 远征营 | 1 | 2 | 3 |
| 重弩炮 | 2 | 3 | 4 |
| 哨塔 | 2 | 3 | 4 |
| 石墙 | 40 | 70 | 100 |

拆除前会先收取已经产生的收益；拆除最后一座远征营后，需要重建才能出征。确认拆除也会清空移动撤销记录，避免复活已拆除建筑。

## 已实现

- 40×40 逻辑地图；边界、建筑占地、资源消耗检查。
- 议事堡、金矿、晶露池、远征营、重弩炮、哨塔、石墙，共 7 类建筑。
- 单个建筑选择、移动、升级、确认拆除；分类型建造数量上限；移动撤销/重做；按住连续铺墙。
- 两种资源、仓储上限、离线产出（最多 8 小时）、收取。
- 主城和其他建筑 1—3 级；其他建筑等级受主城限制；本原型升级即时完成。
- 先锋、游侠、铁卫、破城手 4 类兵种；预设每次远征的兵力，无训练队列。
- 三个预设敌方基地；侦察阶段不计时；首名士兵部署后开始 180 秒战斗。
- 地面寻路、可破坏城墙、远程跨墙攻击、防御塔反击、两次范围治疗。
- 破坏率、星级、结算和返回基地；奖励在单次战斗对象内只结算一次。
- 自动存档、原子替换、备份恢复；读档失败时保留原文件并明确报错。
- 可调窗口、F11 无边框全屏、镜头移动/缩放、快捷键帮助。

## 操作

| 操作 | 键位 |
|---|---|
| 移动镜头 | WASD / 鼠标中键拖动 |
| 缩放 / 复位 | 滚轮 / Home |
| 选择建筑 | 左键点建筑 |
| 建造 | 点底部建筑卡，再点地面 |
| 连续铺墙 | 选石墙，按住左键拖动 |
| 移动 / 升级所选建筑 | M / U |
| 拆除所选建筑（需确认） | Delete |
| 撤销 / 重做移动 | Ctrl+Z / Ctrl+Y |
| 收取资源 / 手动保存 | C / Ctrl+S |
| 选择兵种 / 连续投兵 | 1—4 / 在外圈按住左键 |
| 兵种图鉴与战术介绍 | I |
| 选择范围治疗 | Q，再点击友军附近 |
| 取消当前操作 | 右键 / Esc |
| 网格 / 帮助 / 全屏 | G / F1 / F11 |

帮助、兵种图鉴和教学面板打开时，本地战斗暂停。推荐顺序：铁卫吸引火力 → 破城手开墙 → 先锋跟进 → 游侠后排输出；铁卫受伤后再按 Q 治疗。音效开关目前仅控制建造提示音，没有完整的战斗配音或音乐。

## 存档

Windows 试玩：`Hearthhold.exe` 同级的 `Saves/village.xml`，上一份存档为 `village.xml.bak`。

Unity：`Application.persistentDataPath/village.xml`。两个客户端默认使用独立存档位置。

每 15 秒以及建造、升级、拆除、收取、结算、正常退出时保存。战斗中关闭程序会放弃未结算的本场进度，保留自己的基地。战斗回放/断点续战没有实现。

0.2 兼容 0.1 存档。更新前退出游戏并备份 `Saves` 文件夹；在原目录替换程序即可继续原村庄，换目录则手动复制 `Saves`。旧存档中超出新上限的建筑会保留，可以移动或拆除，但数量降到上限以下前不能再建同类建筑。本次开发没有修改用户存档。

不要在同一个目录同时启动多个试玩实例；当前存档没有跨进程写入锁。如果主存档与备份都损坏，程序报错退出，不会自动创建新村覆盖它们。

## 本机构建与测试

在项目目录执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\build-preview.ps1 -Test -Package
```

使用 Windows 自带 .NET Framework C# 编译器，编译共享核心和原生预览器，不下载依赖。`-Test` 执行核心测试及鼠标/键盘交互测试，并在 `artifacts/screenshots` 输出画面。`-Package` 生成仅含程序和说明的试玩压缩包，不打包用户存档或测试数据。

## Unity 工程

路径：`UnityProject`。工程基线已更新为本机实际安装的 Unity `6000.6.0f1` / URP `17.6.0`。

1. 在 Unity Hub 中添加 `UnityProject`，使用 Unity 6.6 编辑器打开。若要升级版本，先提交当前工程并验证包兼容性。
2. 首次打开会从 Unity Package Manager 获取 URP，并编译脚本。
3. 编辑器脚本自动创建 URP 配置、基础材质和 `Assets/Hearthhold/Scenes/Main.unity`。
4. 选择菜单 `Hearthhold > Open main scene`，点击 Play。场景和游戏对象在运行时生成，编辑模式的初始场景为空是预期行为。
5. 选择 `Hearthhold > Build Windows x64`，构建到 `artifacts/WindowsUnity`。

**当前机器已使用 Unity 6000.6.0f1 / URP 17.6.0 实际完成 Windows x64 构建。** 构建日志包含 `Build Finished, Result: Success` 和本轮输出路径标记；运行时双场景烟雾测试以独立测试存档启动程序，分别渲染 1440×900 聚落与交战画面并以退出码 0 结束。

Unity 客户端已接入新版网格、顶点色光照材质、拆除确认、建造上限、兵种图鉴和首次出征引导。自动构建、聚落与交战渲染已经验证；三场远征逐关人工验收和持续性能测试仍待完成，不能把烟雾测试视为全部玩法验收。

![Unity 0.4 战斗表现](artifacts/screenshots/21-unity-battle-v04.png)

2026-09-06 环境推进：默认官方下载入口实际返回 Hub 3.3.6，安装后其列表只包含 2020—2022 系列编辑器；随后使用官方指定版本入口取得 Hub 3.21.0，并成功升级到 `C:\Program Files\Unity Hub\Unity Hub.exe`。新安装器为 `artifacts/Installers/UnityHubSetup-3.21.0-x64.exe`，数字签名验证通过，签名主体为 Unity Technologies SF。安装器不包含在 0.2 试玩压缩包中。

Unity Hub 的命令行曾遇到用户配置原子重命名错误，但用户通过 Hub 完成编辑器安装和许可证准备后，编辑器批处理构建与运行不受该问题影响。

已添加 `tools/build-unity.ps1`：

```powershell
# 仅读取安装状态，不启动 Unity、不读取账号凭据
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\build-unity.ps1 -Action Check
# 编辑器安装并激活后，准备工程或构建 Windows 版
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\build-unity.ps1 -Action Prepare
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\build-unity.ps1 -Action Build
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\test-unity-player.ps1 -Case All
```

自定义安装位置可增加 `-EditorPath "E:\你的安装位置\Editor\Unity.exe"`。脚本校验工程要求与编辑器版本，不自动升级/降级；日志分别保存在 `artifacts/UnityLogs`，构建后检查完整产物与本轮日志成功标记，避免把旧文件当成新构建成功。`-Package` 会排除 Unity 明确标记为不可发布的备份目录，再生成 Windows 发布包。

Unity 客户端使用旧版 `Input` API。如果打开工程后出现输入后端异常，在 Player Settings 的 Active Input Handling 中选择 Input Manager (Old) 或 Both。编辑器首次准备脚本会在可访问相应设置时配置它。

## 目录

```text
Hearthhold/
  UnityProject/
    Assets/Hearthhold/
      Core/        纯 C# 数据、经济、战斗、寻路、存档、共享模型几何
      Runtime/     Unity 3D 表现与输入接入
      Editor/      首次工程准备与 Windows 构建
    Packages/      URP 与模块依赖
    ProjectSettings/
  NativePreview/   当前可运行的 Windows 等距预览器
  Tests/           核心行为与交互验证
  tools/           构建、测试、打包脚本
  release/         试玩说明
  docs/            开发状态和后续工作
  artifacts/       本机构建产物（不提交版本控制）
```

## 范围说明

这一版是第二轮可玩原型迭代，不是完整商业游戏。模型已从简单占位体块改为原创程序化细节模型，但仍未完成正式美术、角色骨骼和动作制作。尚未实现：联网账号、匹配、服务端校验、部落系统、训练/升级计时队列、10 个关卡、第二种法术、框选多建筑、完整音效、保存的战斗回放与设置持久化。

本地存档可修改、本地时钟可调整，均不能作为在线经济的信任来源。在线版需建立独立权威进度、战斗凭据和数据库事务结算。固定整数步长的回归测试证明同一运行环境下结果可复现，跨 Unity/服务端运行时的一致性仍须验证。

地图、模型、图形和名称均由本项目代码生成，没有使用《部落冲突》的原作素材。
