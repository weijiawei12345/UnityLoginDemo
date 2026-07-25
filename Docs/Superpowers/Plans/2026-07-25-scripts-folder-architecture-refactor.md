# Scripts Folder Architecture Refactor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将场景流和玩家相关脚本迁移到按职责划分的目录，保持 Unity GUID、组件绑定、命名空间与运行时行为不变。

**Architecture:** 使用 Unity AssetDatabase 进行移动，以保留 `.meta` GUID 和现有场景/Prefab 的脚本引用。目录表达业务能力：`GameFlow` 管理场景，`Player` 管理联网实体、移动、相机、动画与玩家资料；`Auth` 与 `UI/TextFx` 保持原位。

**Tech Stack:** Unity 2022.3.62f2c1、Unity AssetDatabase、Unity MCP、Git。

---

### Task 1: 迁移场景流脚本

**Files:**

- Move: `Assets/FireBase+Photon/Scripts/Controller/GameSceneController.cs` -> `Assets/FireBase+Photon/Scripts/GameFlow/GameSceneController.cs`
- Move: `Assets/FireBase+Photon/Scripts/Data/GameSceneIds.cs` -> `Assets/FireBase+Photon/Scripts/GameFlow/GameSceneIds.cs`

- [ ] **Step 1: 验证迁移前置条件。**

确认两个源脚本及其 `.meta` 存在，`GameFlow` 目标目录不存在同名资产；不修改脚本内容。

- [ ] **Step 2: 通过 Unity AssetDatabase 创建目录并移动资产。**

使用 Unity MCP 的 `unity_asset` 创建 `Assets/FireBase+Photon/Scripts/GameFlow`，再以 `manage_file/move` 移动两个 `.cs` 资产。Unity 同步移动对应 `.meta`，不得使用复制后删除。

- [ ] **Step 3: 检查场景流资产与编译状态。**

刷新并编译 Unity 项目，等待 `isCompiling: false`，确认错误日志为 0 条。

- [ ] **Step 4: 提交场景流迁移。**

```powershell
git add -- 'Assets/FireBase+Photon/Scripts/Controller/GameSceneController.cs' 'Assets/FireBase+Photon/Scripts/Controller/GameSceneController.cs.meta' 'Assets/FireBase+Photon/Scripts/Data/GameSceneIds.cs' 'Assets/FireBase+Photon/Scripts/Data/GameSceneIds.cs.meta' 'Assets/FireBase+Photon/Scripts/GameFlow'
git commit -m "refactor: organize game flow scripts"
```

### Task 2: 迁移玩家运行时脚本

**Files:**

- Move: `Assets/FireBase+Photon/Scripts/NetworkPlayer.cs` -> `Assets/FireBase+Photon/Scripts/Player/Network/NetworkPlayer.cs`
- Move: `Assets/FireBase+Photon/Scripts/PlayerSpawner.cs` -> `Assets/FireBase+Photon/Scripts/Player/Network/PlayerSpawner.cs`
- Move: `Assets/FireBase+Photon/Scripts/PlayerMovement.cs` -> `Assets/FireBase+Photon/Scripts/Player/Movement/PlayerMovement.cs`
- Move: `Assets/FireBase+Photon/Scripts/FirstPersonCamera.cs` -> `Assets/FireBase+Photon/Scripts/Player/Camera/FirstPersonCamera.cs`
- Move: `Assets/FireBase+Photon/Scripts/ThirdPersonCamera.cs` -> `Assets/FireBase+Photon/Scripts/Player/Camera/ThirdPersonCamera.cs`
- Move: `Assets/FireBase+Photon/Scripts/IdleRootMotionBehaviour.cs` -> `Assets/FireBase+Photon/Scripts/Player/Animation/IdleRootMotionBehaviour.cs`

- [ ] **Step 1: 验证六个根目录脚本及其 `.meta` 均存在。**

确认目标路径不存在同名资产，并记录 `NetworkPlayer`、`PlayerSpawner`、`PlayerMovement` 仍保持原类名，避免影响 Fusion 和场景组件。

- [ ] **Step 2: 创建 `Player` 子目录并移动资产。**

通过 Unity MCP 创建 `Player/Network`、`Player/Movement`、`Player/Camera`、`Player/Animation`，再逐项以 AssetDatabase 移动六个脚本资产。

- [ ] **Step 3: 校验场景组件与编译。**

检查 Unity 编译错误日志为 0 条；使用 `unity_validation.validate_scene` 检查当前 `LoginMenu` 无缺失脚本，并打开 `Play` 场景重复检查。

- [ ] **Step 4: 提交玩家运行时迁移。**

```powershell
git add -- 'Assets/FireBase+Photon/Scripts/NetworkPlayer.cs' 'Assets/FireBase+Photon/Scripts/NetworkPlayer.cs.meta' 'Assets/FireBase+Photon/Scripts/PlayerSpawner.cs' 'Assets/FireBase+Photon/Scripts/PlayerSpawner.cs.meta' 'Assets/FireBase+Photon/Scripts/PlayerMovement.cs' 'Assets/FireBase+Photon/Scripts/PlayerMovement.cs.meta' 'Assets/FireBase+Photon/Scripts/FirstPersonCamera.cs' 'Assets/FireBase+Photon/Scripts/FirstPersonCamera.cs.meta' 'Assets/FireBase+Photon/Scripts/ThirdPersonCamera.cs' 'Assets/FireBase+Photon/Scripts/ThirdPersonCamera.cs.meta' 'Assets/FireBase+Photon/Scripts/IdleRootMotionBehaviour.cs' 'Assets/FireBase+Photon/Scripts/IdleRootMotionBehaviour.cs.meta' 'Assets/FireBase+Photon/Scripts/Player'
git commit -m "refactor: organize player runtime scripts"
```

### Task 3: 迁移玩家资料脚本并完成回归检查

**Files:**

- Move: `Assets/FireBase+Photon/Scripts/Controller/PlayerNameSyncController.cs` -> `Assets/FireBase+Photon/Scripts/Player/Profile/PlayerNameSyncController.cs`
- Move: `Assets/FireBase+Photon/Scripts/Data/PlayerDisplayNameData.cs` -> `Assets/FireBase+Photon/Scripts/Player/Profile/PlayerDisplayNameData.cs`
- Move: `Assets/FireBase+Photon/Scripts/UI/PlayRenameView.cs` -> `Assets/FireBase+Photon/Scripts/Player/Profile/PlayRenameView.cs`
- Modify: `Docs/Superpowers/WorkStatus.md`

- [ ] **Step 1: 验证资料脚本迁移前置条件。**

确认三个源脚本及其 `.meta` 存在、`Player/Profile` 中无冲突资产。保持 `ARPG.Player` 命名空间和 `PlayRenameView` 全局 MonoBehaviour 类型不变。

- [ ] **Step 2: 通过 Unity AssetDatabase 移动资料资产。**

创建 `Assets/FireBase+Photon/Scripts/Player/Profile` 后移动三个脚本资产。`UI/TextFx` 保持原路径，`Auth` 保持原路径。

- [ ] **Step 3: 清理空的旧分类目录。**

仅在 `Controller` 与 `Data` 内没有其他资产时删除这两个空目录；若目录仍包含非迁移资产，则保留并在工作状态中说明。不要删除用户已有资源或 `UI` 目录。

- [ ] **Step 4: 执行最终 Unity 与 Git 验证。**

确认 Unity 错误日志为 0 条，分别验证 `LoginMenu` 与 `Play` 场景没有缺失脚本；搜索 `Scripts/Controller`、`Scripts/Data` 和 `Scripts/UI/PlayRenameView.cs` 不再存在 C# 资产；运行 `git diff --check`。

- [ ] **Step 5: 更新状态并提交资料迁移。**

```powershell
git add -- 'Assets/FireBase+Photon/Scripts/Controller' 'Assets/FireBase+Photon/Scripts/Data' 'Assets/FireBase+Photon/Scripts/UI/PlayRenameView.cs' 'Assets/FireBase+Photon/Scripts/UI/PlayRenameView.cs.meta' 'Assets/FireBase+Photon/Scripts/Player/Profile' 'Docs/Superpowers/WorkStatus.md'
git commit -m "refactor: organize player profile scripts"
```
