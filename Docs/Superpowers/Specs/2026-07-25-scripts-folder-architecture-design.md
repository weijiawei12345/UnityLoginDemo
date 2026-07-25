# Scripts Folder Architecture Design

**Goal:** 让 `Assets/FireBase+Photon/Scripts` 的目录按业务能力与运行时职责组织，同时保持 Unity 引用和业务行为不变。

## 决策

- 只通过 Unity AssetDatabase 移动现有脚本及其 `.meta`，保留 GUID、类名、命名空间和序列化字段。
- 不改认证模块内部目录。`Auth` 当前以认证闭环聚合，近期已完成职责拆分，继续移动不会带来对应收益。
- 不改 `UI/TextFx`。它是独立的通用文本动效子系统，目录与 `GameUI.TextFx` 命名空间一致。
- 不修改 Firebase、Firestore、Fusion、场景文件、Prefab 或运行时路径绑定。

## 目标目录与职责

| 目录 | 职责 | 脚本 |
| --- | --- | --- |
| `GameFlow` | 场景标识与切换流程 | `GameSceneIds`、`GameSceneController` |
| `Player/Network` | Fusion 玩家实体与入房生成 | `NetworkPlayer`、`PlayerSpawner` |
| `Player/Movement` | 本地玩家移动输入与网络同步 | `PlayerMovement` |
| `Player/Camera` | 第一、三人称相机行为 | `FirstPersonCamera`、`ThirdPersonCamera` |
| `Player/Animation` | Animator 状态机行为 | `IdleRootMotionBehaviour` |
| `Player/Profile` | 公开昵称的数据、同步与 Play 场景入口 | `PlayerDisplayNameData`、`PlayerNameSyncController`、`PlayRenameView` |

## 迁移映射

| 当前路径 | 目标路径 |
| --- | --- |
| `Controller/GameSceneController.cs` | `GameFlow/GameSceneController.cs` |
| `Data/GameSceneIds.cs` | `GameFlow/GameSceneIds.cs` |
| `NetworkPlayer.cs` | `Player/Network/NetworkPlayer.cs` |
| `PlayerSpawner.cs` | `Player/Network/PlayerSpawner.cs` |
| `PlayerMovement.cs` | `Player/Movement/PlayerMovement.cs` |
| `FirstPersonCamera.cs` | `Player/Camera/FirstPersonCamera.cs` |
| `ThirdPersonCamera.cs` | `Player/Camera/ThirdPersonCamera.cs` |
| `IdleRootMotionBehaviour.cs` | `Player/Animation/IdleRootMotionBehaviour.cs` |
| `Controller/PlayerNameSyncController.cs` | `Player/Profile/PlayerNameSyncController.cs` |
| `Data/PlayerDisplayNameData.cs` | `Player/Profile/PlayerDisplayNameData.cs` |
| `UI/PlayRenameView.cs` | `Player/Profile/PlayRenameView.cs` |

## 验证

1. 确认每个源脚本与 `.meta` 存在，且目标路径无同名资产。
2. 使用 Unity MCP 的资产移动能力迁移，禁止复制后删除。
3. 等待 Unity 域重载完成，检查编译错误日志。
4. 检查 `LoginMenu` 与 `Play` 场景不存在缺失脚本。
5. 搜索旧目录路径，确认不再有 C# 文件残留。

## 非目标

- 不统一 MonoBehaviour 的命名空间，避免扩大 Unity 组件类型迁移风险。
- 不创建 asmdef、测试程序集或新的运行时抽象。
- 不处理认证与网络模块的业务风险；该工作只改善工程导航与职责表达。
