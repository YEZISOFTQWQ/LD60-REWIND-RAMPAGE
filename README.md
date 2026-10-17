# REWIND RAMPAGE — Demo v0.1

俯视角二维生存街机：前进布置路线，倒退执行攻击。

## 启动

1. 用 Unity **2022.3.62f3c1** 打开这个工程。
2. 打开 `Assets/Scenes/SampleScene.unity`，点击编辑器 Play。
3. 在标题界面按 **Space**，或点击 **PLAY TAPE**。

Windows 试玩程序位于 `Builds/Windows/RewindRampage.exe`，可直接双击运行。该目录为本机导出产物，已由 `.gitignore` 排除；需要重建时使用 Unity 菜单 **Rewind Rampage → Build Windows preview**。

场景、资源、Prefab 与组件引用已经生成，不需要手动挂脚本。工程文件夹为 `LD60-REWIND-RAMPAGE`，该目录同时作为 Git 仓库根目录。支持 Windows 编辑器和桌面浏览器 WebGL，推荐 1600×1000 或 16:9 横屏。

## 项目组织

```text
LD60-REWIND-RAMPAGE/
├── Assets/                  # Unity 资源、源码、设计说明、WebGL 页面模板
├── Packages/                # Unity 依赖
├── ProjectSettings/         # Unity 设置
├── docs/                    # GitHub Pages 在线试玩输出
│   ├── .nojekyll
│   ├── index.html
│   ├── Build/
│   └── TemplateData/
├── .gitignore
├── .gitattributes
└── README.md
```

`Library/`、`Temp/`、`Logs/`、`UserSettings/` 等为 Unity 本机生成目录，已被 Git 忽略。`docs/Build/` 与 `docs/TemplateData/` 会正常纳入版本管理。

提交身份：**YEZISOFTQWQ**，邮箱 `154974816+YEZISOFTQWQ@users.noreply.github.com`，仅设置在本仓库。

## 在线试玩与导出

发布此仓库后，在 GitHub 仓库 **Settings → Pages** 中选择 **Deploy from a branch → main → /docs**。所有游戏资源使用相对路径，适用于项目子目录站点。`.nojekyll` 避免 Jekyll 过滤 Unity 文件。

要更新在线版本，在装有对应 **WebGL Build Support** 的 Unity 编辑器中选择 **Rewind Rampage → Build WebGL to docs**。该菜单导出真正的 Unity WebGL 游戏，并从 `Assets/WebGLTemplates/Rewind/` 生成带加载进度、中文键位提示和全屏按钮的网页。

采用 Gzip 压缩与 Unity 客户端解压回退，静态托管无需配置 Content-Encoding 响应头。浏览器需要 WebGL 2 与键盘；请通过 HTTP / HTTPS 访问，直接双击 `index.html` 的 `file://` 方式会显示说明。

本地预览可在工程根目录运行 `python -m http.server 8000 --directory docs`，再访问 `http://localhost:8000/`。

## 操作和规则

| 操作 | 效果 |
| --- | --- |
| WASD / 方向键 | 归一化移动，速度 5.5，受竞技场边界限制 |
| Space | 标题开始、游戏中倒带、结算后重开 |
| Esc | 暂停 / 继续 |
| M / 屏幕声音按钮 | 开关音效与磁带底噪 |

- 按 0.05 秒间隔保存最近三秒的位置和时间。保留最多约 60 个常规采样间隔，额外保存精确的窗口边界与当前端点。
- 青蓝色轨迹只提示路线，越新的部分越亮。需要至少一秒**有效移动**才可启动倒带，静止等待不会充能。
- 倒带按原始时间的五倍逆序播放，三秒轨迹约耗时 0.6 秒；期间不录制、不接受移动输入、敌人和生成器暂停，局内计时继续。
- 当前扫过的路径转为金色。每个跨越的采样线段分别做点到线段距离判定，攻击半径 0.55，加上敌人半径 0.32。
- 单次倒带中，每个敌人只命中一次。连杀得分依次为 100、150、200、250……，五杀共 1000 分。
- 结束后清空已消费的轨迹，在最早记录的位置重新录制；冷却两秒，额外无敌 0.3 秒。冷却期间可继续录制。
- 玩家三点生命，正常接触追踪者损失一点，受伤后无敌 0.75 秒。追踪者一击死亡，基准速度 2.5。
- 开局三名慢速追踪者专门布置在初始路线附近，带出生预警，便于体验首次反杀。后续敌人从边缘生成，避开玩家五单位内的位置；生成间隔由 1.5 秒渐变至 0.38 秒，最多 35 名。
- 90 秒结束或生命归零进入结算，显示分数、总击杀、最大单次倒带击杀、最高连击、生存时间、倒带次数。最高分保存在本机 PlayerPrefs。
- 暂停与窗口失焦会停止游戏逻辑，不修改全局 `Time.timeScale`。

## 表现

深色网格、青色三角玩家、红色圆形追踪者、分段金色扫掠、击杀碎片与分数弹字、连击提示、轻微震屏、录制条、角色冷却环，以及 REC / REW / PLAY 磁带界面。倒带马达、击杀、受伤与提示声音由程序合成，不依赖外部音频下载。中文指引使用已烘焙的静态 TMP 字体。

## 修改参数

选中 `Assets/RewindRampage/Settings/DemoTuning.asset`，可调整移动、录制、倒带、伤害、生存与生成参数。竞技场网格和 HUD 初始生命格为场景作者生成内容；修改竞技场尺寸后，需要通过下面的菜单重新生成场景。

Unity 菜单 **Rewind Rampage → Create or regenerate demo scene** 可以重新生成入口场景与追踪者 Prefab。该操作会替换 `SampleScene` 的内容；保留手动修改前请先另存场景。

## 代码归属

| 文件 | 负责内容 |
| --- | --- |
| `GameManager.cs` | 明确初始化顺序、输入与更新流程、计时、得分和结算 |
| `PlayerController.cs` | 移动、生命、无敌和角色显示 |
| `TrailRecorder.cs` | 有界时间窗口、规则采样和快照 |
| `RewindController.cs` | 倒带条件、按时间逆序逐段播放和冷却 |
| `TrailDamageSystem.cs` | 线段扫掠、去重和连击计分 |
| `EnemyController.cs` / `EnemySpawner.cs` | 预警、追踪、对象池和难度曲线 |
| `FeedbackController.cs` | 有界碎片 / 弹字池、轨迹显示、合成音效和震屏 |
| `RewindHud.cs` | 从游戏状态更新 HUD、中文指引和菜单 |
| `Editor/DemoSceneBuilder.cs` | 用 Unity API 作者化场景、资源、字体和 UI |

原始设计文档保存在 `Assets/RewindRampage/Documentation/REWIND_RAMPAGE_Design.md`，项目上下文在其 `AI/UnityProjectContext.md`。

2026-10-17 已通过 Unity 脚本编译、Windows x64 构建和 WebGL 构建；Windows 试玩程序已启动供用户操作。按照用户要求没有编写或运行测试，也没有进行自动化游戏操作。构建与启动日志保留在本机 `Logs/`，不进入版本管理。
