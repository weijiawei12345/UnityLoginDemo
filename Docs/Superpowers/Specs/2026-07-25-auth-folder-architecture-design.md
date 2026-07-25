# Auth Folder Architecture Design

**Goal:** 将认证模块按领域、应用编排、表现层、SDK 基础设施和联机边界组织，使 Firebase、Firestore、Fusion 与 Unity UI 的职责可从目录直接识别。

## 约束

- 只使用 Unity AssetDatabase 移动脚本资产及其 `.meta`，保留 GUID、类名、命名空间与序列化字段。
- 不修改 Firebase、Firestore、Fusion、UI 或场景业务逻辑。
- `AuthUIView`、`UsernamePanelView`、`LoadingOverlayView` 仍为原有全局 MonoBehaviour 类型；其他代码继续使用 `ARPG.Auth`。

## 目标目录

| 目录 | 职责 | 脚本 |
| --- | --- | --- |
| `Domain` | 认证请求、结果和内存会话模型 | `AuthModels` |
| `Application/Authentication` | 登录注册用例与输入规则 | `AuthController`、`AuthRequestValidator` |
| `Application/Profile` | 昵称用例 | `UserNameController` |
| `Application/Flow` | 登录成功后的跨用例续流程 | `AuthLoginFlowCoordinator` |
| `View/Login` | 登录注册表单事件、输入与反馈 | `AuthUIView`、`AuthFormBindings`、`AuthFormRequestFactory` |
| `View/Profile` | 昵称面板表现与用户交互 | `UsernamePanelView` |
| `View/Shared` | 认证流程共享的加载表现 | `LoadingOverlayView` |
| `Infrastructure/Firebase` | Firebase Auth 初始化与 SDK 调用 | `FirebaseAuthManager` |
| `Infrastructure/Firestore` | Firestore 客户端、会话租约与档案持久化 | `FirestoreClient`、`FirestoreAuthSessionRepository`、`FirestoreUserProfileRepository` |
| `Networking/Fusion` | Fusion 生命周期事件桥接 | `FusionAuthSessionBridge` |
| `Networking/Session` | Fusion 断线、顶号与 Firestore 租约的会话边界 | `AuthSessionGuard` |

## 验证

1. 移动前确认 16 个源脚本和 `.meta` 都存在，目标无冲突资产。
2. 每批移动后等待 Unity 域重载，确认项目编译错误日志为 0 条。
3. 验证 `LoginMenu` 与 `Play` 场景不存在缺失脚本。
4. 确认 `Auth` 根目录不再直接包含 C# 脚本，只保留职责子目录。

## 非目标

- 不改变命名空间层级，避免 Unity 序列化组件类型迁移。
- 不将 `AuthSessionGuard` 的 Firestore 与 Fusion 协调职责拆成新类；本次只通过目录标注其联机会话边界。
- 不把共享 UI 提升到全局 `UI` 模块；当前 `LoadingOverlayView` 仍只服务认证入口。
