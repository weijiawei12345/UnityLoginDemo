# Auth Folder Architecture Refactor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将 Auth 的 16 个脚本按职责迁移到领域、应用、表现、基础设施和联机目录，保持 Unity 引用与认证行为不变。

**Architecture:** 使用 Unity AssetDatabase 移动 `.cs` 资产，从而保留原 `.meta` GUID。保留所有现有类型名与命名空间；目录只表达职责，不引入新抽象或修改认证、持久化与联机流程。

**Tech Stack:** Unity 2022.3.62f2c1、Unity AssetDatabase、Unity MCP、Git。

---

### Task 1: 迁移领域与应用编排

**Files:**

- Move: `AuthModels.cs` -> `Domain/AuthModels.cs`
- Move: `AuthController.cs` -> `Application/Authentication/AuthController.cs`
- Move: `AuthRequestValidator.cs` -> `Application/Authentication/AuthRequestValidator.cs`
- Move: `UserNameController.cs` -> `Application/Profile/UserNameController.cs`
- Move: `AuthLoginFlowCoordinator.cs` -> `Application/Flow/AuthLoginFlowCoordinator.cs`

- [ ] 验证五个源资产与 `.meta` 存在，目标无冲突。
- [ ] 通过 Unity MCP 创建 `Domain`、`Application/Authentication`、`Application/Profile`、`Application/Flow` 并逐项移动资产。
- [ ] 等待 Unity 域重载，确认错误日志为 0 条。
- [ ] 提交：`refactor: organize auth domain and application scripts`。

### Task 2: 迁移表现层与 Firebase/Firestore 基础设施

**Files:**

- Move: `AuthUIView.cs` -> `View/Login/AuthUIView.cs`
- Move: `AuthFormBindings.cs` -> `View/Login/AuthFormBindings.cs`
- Move: `AuthFormRequestFactory.cs` -> `View/Login/AuthFormRequestFactory.cs`
- Move: `UsernamePanelView.cs` -> `View/Profile/UsernamePanelView.cs`
- Move: `LoadingOverlayView.cs` -> `View/Shared/LoadingOverlayView.cs`
- Move: `FirebaseAuthManager.cs` -> `Infrastructure/Firebase/FirebaseAuthManager.cs`
- Move: `FirestoreClient.cs` -> `Infrastructure/Firestore/FirestoreClient.cs`
- Move: `FirestoreAuthSessionRepository.cs` -> `Infrastructure/Firestore/FirestoreAuthSessionRepository.cs`
- Move: `FirestoreUserProfileRepository.cs` -> `Infrastructure/Firestore/FirestoreUserProfileRepository.cs`

- [ ] 验证九个源资产与 `.meta` 存在，目标无冲突。
- [ ] 通过 Unity MCP 创建目标目录并逐项移动资产；不修改场景、Prefab、命名空间或控件路径。
- [ ] 等待 Unity 域重载，确认错误日志为 0 条，并校验 `LoginMenu` 无缺失脚本。
- [ ] 提交：`refactor: organize auth presentation and infrastructure scripts`。

### Task 3: 迁移联机边界并完成回归验证

**Files:**

- Move: `FusionAuthSessionBridge.cs` -> `Networking/Fusion/FusionAuthSessionBridge.cs`
- Move: `AuthSessionGuard.cs` -> `Networking/Session/AuthSessionGuard.cs`
- Modify: `Docs/Superpowers/WorkStatus.md`

- [ ] 验证两个源资产与 `.meta` 存在，目标无冲突。
- [ ] 创建 `Networking/Fusion` 与 `Networking/Session` 后通过 Unity MCP 移动资产。
- [ ] 验证 Auth 根目录不再直接包含 C# 文件；检查 `LoginMenu`、`Play` 无缺失脚本，Unity 错误日志为 0 条。
- [ ] 更新工作状态并提交：`refactor: organize auth networking scripts`。
