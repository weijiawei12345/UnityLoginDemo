# Firebase Auth / Firestore / Fusion 代码审查

**审查范围**：19 个项目自有 C# 文件，共 2,837 行；`LoginMenu`、`Play` 场景、Build Settings、Fusion App Settings。第三方 Photon/Firebase SDK 源码未作为业务代码审查。

**当前 Git 状态**：只有两份 TMP 字体资源变更，与认证链路无关，因此本报告以当前业务基线审查，不把字体差异算作代码问题。

**总体结论**：`REQUEST_CHANGES`。结构已经有 Auth、Repository、Controller、View 的初步分层，Unity 编译成功；但认证和联机之间缺少可信的服务端边界，且断线、Firestore 降级和数据文档职责存在实际风险。

## Findings

### P0 - Critical

无。

### P1 - High

1. **Firebase 登录没有形成 Photon 的服务端认证边界**
   - 证据：`Assets/FireBase+Photon/Scenes/Play.unity:866` 使用 Fusion Bootstrap 自动启动 Runner；`Assets/FireBase+Photon/Scenes/Play.unity:870` 配置自动启动；`Assets/FireBase+Photon/Scripts/Auth/FusionAuthSessionBridge.cs:65` 的 `OnCustomAuthenticationResponse` 为空。项目自有代码没有把 Firebase ID Token 交给 Photon Custom Authentication，也没有显式拒绝未通过 Firebase 的入房请求。
   - 影响：当前流程只能表达“客户端先登录再进入场景”，不能阻止被修改的客户端直接启动 Runner、伪造昵称或绕过 `AuthSessionGuard`。
   - 建议：增加 `FusionConnectionCoordinator`，在连接前获取有效 Firebase ID Token；在 Photon Webhook/Custom Auth 服务端验证 Token、UID 和会话状态。若当前阶段只做客户端原型，应在文档中明确这是“协作式客户端保护”，不能宣称端到端认证。

2. **Photon 短暂断线会释放 Firestore 会话，但保留 Firebase 登录状态**
   - 证据：`Assets/FireBase+Photon/Scripts/Auth/FusionAuthSessionBridge.cs:39-53` 在断线时调用 `AuthSessionGuard.NotifyPhotonOffline()`；`Assets/FireBase+Photon/Scripts/Auth/AuthSessionGuard.cs:247-259` 清空本地租约并释放 Firestore，但没有清理 `UserSession`、Firebase Auth 或启动重连/回登录流程。
   - 影响：用户可能仍显示为 Firebase 已登录，却不再有心跳、监听和单点登录保护；第二个客户端登录后，第一端不会再被可靠地发现和处理。
   - 建议：将断线定义为显式状态事件，由统一协调器决定重连或登出；只有用户主动登出、顶号或确认离开游戏时释放 Lease。所有退出路径使用可等待的异步关闭流程。

3. **Firestore 读取失败时会把邮箱前缀作为网络展示名广播**
   - 证据：`Assets/FireBase+Photon/Scripts/Auth/AuthModels.cs:26,39-47` 从邮箱生成默认 Name；`Assets/FireBase+Photon/Scripts/Auth/UsernamePanelView.cs:103-110` 在档案读取失败时允许继续进入 Play；`Assets/FireBase+Photon/Scripts/NetworkPlayer.cs:21-27` 进入网络对象时直接同步该名称。
   - 影响：Firestore 暂时不可用时，认证身份的邮箱前缀会泄露给房间内其他玩家，并且绕过首次昵称设置。
   - 建议：将 `AuthEmail` 与 `PublicDisplayName` 分离；Firestore 失败时只允许进入离线/重试状态，或使用固定的本地匿名名，不得把邮箱派生值写入 Fusion。

4. **Firestore 安全规则未纳入项目，无法证明档案和会话写入受 UID 限制**
   - 证据：仓库未找到 `firestore.rules` 或等效规则文件；业务代码直接写入 `Users/{uid}`，见 `Assets/FireBase+Photon/Scripts/Auth/FirestoreAuthSessionRepository.cs:183-185` 和 `Assets/FireBase+Photon/Scripts/Auth/FirestoreUserProfileRepository.cs:101-103`。
   - 影响：无法从版本库确认是否存在跨用户读写、任意覆盖 `activeSessionId` 或读取其他用户档案的 IDOR 风险；客户端校验不能替代 Firestore Rules。
   - 建议：把规则和 Emulator 测试纳入版本库，至少验证未登录拒绝、用户只能访问自己的文档、Profile 与 Session 字段不可越权修改。

### P2 - Medium

5. **Profile 与 Session Lease 共用一个热点文档，监听和持久化职责耦合**
   - 证据：两个 Repository 都使用 `Users/{uid}`；`FirestoreAuthSessionRepository.ListenSession` 监听整个文档，心跳每 3 秒更新同一文档；Profile Repository 也在同一文档写 `name`。
   - 影响：心跳会触发档案文档监听和额外读取，字段规则、并发更新和未来扩展都会互相影响。
   - 建议：拆为 Profile 文档与 `session/current` 文档，并让每个 Repository 只拥有自己的字段契约。

6. **`AuthUIView` 是 UI、异步编排和层级反射绑定的 God Object**
   - 证据：`Assets/FireBase+Photon/Scripts/Auth/AuthUIView.cs` 共 653 行，同时负责输入校验展示、面板切换、Firebase 初始化、登录注册、档案检查、场景切换和大量字符串路径查找。
   - 影响：UI 层级改名会破坏认证流程；任何认证流程变化都需要修改同一大类，难以单元测试和面试讲解。
   - 建议：保留 `AuthUIView` 只做 View Binding/State Rendering，将登录注册编排移到 Application Service，将路径查找改为 Prefab/Inspector 引用或专用 Binder。

7. **静态全局状态和重复会话状态降低可测试性**
   - 证据：`UserSession` 位于 `AuthModels.cs:98-114`，`AuthController` 又维护 `CurrentUser`（`AuthController.cs:16`），并直接 new Repository（`AuthController.cs:14`）。
   - 影响：状态可能双写、难以替换依赖，测试之间容易互相污染；`CurrentUser` 在项目内没有消费者。
   - 建议：引入单一 `AuthSessionStore`，通过接口注入 Auth、Profile、Lease 实现；确认无反射消费者后删除未使用的 `CurrentUser`。

8. **多个 `async void` 和 fire-and-forget 路径缺少统一异常与生命周期处理**
   - 证据：`AuthUIView.cs:112,209,246,272`、`UsernamePanelView.cs:82,194` 使用 `async void`；`AuthController.cs:84-86`、`AuthSessionGuard.cs:66,113,235` 丢弃 Task。
   - 影响：场景切换或对象销毁后，异步回调可能继续访问 UI；未观察异常无法被调用方重试或展示。
   - 建议：业务方法返回 `Task`，View 只保留 Unity 事件入口并捕获异常；引入取消令牌/版本号检查，确保 await 返回后对象仍有效。

9. **Firebase 初始化失败会永久缓存，无法恢复瞬时故障**
   - 证据：`FirebaseAuthManager.cs:18,52-59` 只缓存 `_initializationTask`，包括失败结果；没有重置或重试策略。
   - 影响：启动时网络或依赖检查短暂失败后，用户只能重启客户端。
   - 建议：将初始化状态显式化，区分不可恢复配置错误和可重试网络错误，并提供带退避的重试入口。

10. **应用退出时的 Lease 释放不可等待**
   - 证据：`AuthSessionGuard.cs:224-235` 在 `OnApplicationQuit` 中启动 `_repository.ReleaseAsync` 后立即结束进程。
   - 影响：`sessionOnline` 可能长期保持旧值；当前新登录会覆盖它，但在线状态和监控语义不可靠。
   - 建议：使用心跳过期时间作为权威判断，并在服务端/规则侧设计租约过期；不要把应用退出时的网络写入当作唯一清理机制。

### P3 - Low

11. **昵称长度契约不一致**
   - `UserNameController.cs:11` 限制 16 字符，而 `PlayerDisplayNameData.cs:11` 和 `NetworkPlayer.cs:16` 使用 32 字符。建议定义一个共享的 `DisplayNamePolicy`，明确 Firestore 与网络层的不同上限。

12. **Fusion 回调桥接包含大量空实现，隐藏真正关心的生命周期事件**
   - `FusionAuthSessionBridge.cs:56-72` 为接口填充多个空回调。可以保留 SDK 要求，但应通过注释或适配基类突出 `OnShutdown`/`OnDisconnectedFromServer` 是本项目真正的边界。

## Removal / Iteration Plan

### 可在确认测试后移除

- `AuthController.CurrentUser`：仓库搜索没有发现读取方，先加登录/登出流程测试，再删除双写状态。

### 暂缓删除

- `UserData.Level`、`Coins`、时间字段：当前只有默认值，没有 Firestore 读写；它们可能是后续玩家档案契约，先移动到明确的 Profile 模型，不直接删除。
- `FusionAuthSessionBridge` 的空回调：属于 SDK 接口实现，不应机械删除。

## 验证缺口

- Unity MCP 编译请求完成，编辑器随后处于非编译状态；未发现业务编译错误。
- `LoginMenu` 场景校验未发现缺失脚本，但发现 6 组 TMP 通用子节点重名警告。
- 未发现项目自有 EditMode/PlayMode 测试。
- 未验证真实 Firebase Rules、Photon Custom Auth/Webhook、两端顶号、Photon 断线重连和 Firestore 不可用时的实际运行行为。

## 建议实施顺序

先处理 P1-4 的认证边界、断线状态、隐私降级和 Firestore Rules；再处理 P2-5/6 的数据与 UI 解耦；最后处理 P2-7/8/9/10 和 P3 命名清理。每一步都保持现有场景入口可运行，并补充登录成功、Firestore 失败、顶号、断线、主动登出和昵称同步测试。

