# Firebase Auth / Firestore / Fusion 技术方案（短版）

## 1. 目标与范围

目标是让登录认证、用户档案、在线会话和 Fusion 联机各自负责单一职责，同时保留当前项目的登录、改名、顶号下线和玩家名同步行为。本文是重构前的技术方案，不包含本轮业务代码修改。

当前项目的实际联机库是 Photon Fusion。`Play` 场景中的 `FusionBootstrap` 自动启动 Runner；项目自有代码没有显式的 Firebase Token -> Fusion Custom Authentication 串接，因此当前“认证”主要是客户端顺序控制，不能等同于服务端可信认证。

## 2. 当前链路

```text
LoginMenu/AuthUIView
    -> AuthController
        -> FirebaseAuthManager (Firebase Auth 初始化、邮箱登录/注册)
        -> FirestoreAuthSessionRepository (Users/{uid} 写 activeSessionId)
        -> AuthSessionGuard (监听 + 3 秒心跳 + 顶号下线)
    -> UsernamePanelView
        -> UserNameController
            -> FirestoreUserProfileRepository (Users/{uid}.name)
    -> GameSceneController.LoadPlaySceneAsync()
        -> Play/FusionBootstrap 自动启动 NetworkRunner
            -> PlayerSpawner.Spawn(NetworkPlayer)
                -> NetworkPlayer 同步 NetworkString<_32> PlayerName
```

退出或 Photon 断线时，`FusionAuthSessionBridge` 通知 `AuthSessionGuard` 释放 Firestore 在线锁。当前断线释放后没有统一的认证状态恢复策略，需要在重构中明确“重连、回登录页、还是保留 Firebase 登录”的状态机。

## 3. 目标职责边界

| 层 | 建议职责 | 不应依赖 |
| --- | --- | --- |
| UI | 采集输入、显示 `AuthState`/错误、触发命令 | Firebase、Firestore、Fusion SDK |
| Application | 编排登录、注册、登出、昵称和进入游戏 | Unity 场景层级细节 |
| Domain | `AuthSession`、`UserProfile`、状态和校验规则 | Firebase、Fusion、MonoBehaviour |
| Firebase Adapter | Firebase Auth 初始化、登录、刷新 Token、登出 | UI、场景切换 |
| Firestore Repositories | 仅负责 Profile 和 Session Lease 数据读写 | Fusion、UI |
| Fusion Adapter | 带认证上下文启动/关闭 Runner、同步玩家展示名 | Firestore 具体实现 |
| Game Flow | 场景切换与网络生命周期协调 | 表单校验、数据库字段 |

建议只保留一个会话状态源，例如 `AuthSessionStore`；移除 `AuthController.CurrentUser` 与 `UserSession` 的双写。外部 SDK 通过接口注入，方便 EditMode 测试和替换实现。

## 4. 数据边界

将当前混在 `Users/{uid}` 的数据拆成两个职责明确的文档：

```text
Users/{uid}                 -> UserProfile: name, level, coins, timestamps
Users/{uid}/session/current -> SessionLease: activeSessionId, online, heartbeat
```

Profile Repository 只监听/读写 Profile；Session Lease Repository 只做抢占、心跳、条件释放。这样心跳不会触发档案监听，规则也可以分别限制“用户只能访问自己的档案和会话”。Firestore 规则必须纳入版本库并验证 `request.auth.uid == uid`，不能依赖客户端逻辑保护。

## 5. 关键状态流

1. `Initializing`：Firebase 依赖检查失败可重试，并向 UI 暴露明确错误。
2. `Authenticating`：Firebase Auth 返回用户和 ID Token。
3. `AcquiringSession`：事务抢占当前用户的 Session Lease；失败则退出 Firebase Auth。
4. `LoadingProfile`：读取 Profile；首次用户进入昵称设置流程。
5. `Connecting`：Fusion Adapter 使用已验证的认证上下文连接，不能只依赖“场景已加载”。
6. `InGame`：Session Lease 心跳和 Fusion 连接状态由同一个生命周期协调器管理。
7. `Replaced/Disconnected/SigningOut`：先停止 Runner，再按策略释放 Lease、清理本地会话并回到登录页或进入重连状态。

## 6. 渐进式实施顺序

1. 先补 Firestore 规则、认证状态枚举、Profile/Session 数据契约和关键流程测试，不移动现有文件。
2. 提取 `IFirebaseAuthGateway`、`IUserProfileRepository`、`ISessionLeaseRepository`，让现有 Controller 通过构造注入工作。
3. 拆出 `AuthApplicationService`，把 `AuthUIView` 缩减为 UI 绑定和状态渲染；保留现有按钮和场景作为兼容入口。
4. 拆出 `FusionConnectionCoordinator`，把 Fusion 启动、断线、关闭和 Session Lease 绑定在一个可测试的协调层；明确 Custom Authentication 或“仅客户端原型”边界。
5. 最后按职责移动目录、统一命名和日志，逐步删除未使用的 `CurrentUser`、重复模型和旧路径绑定。

## 7. 面试版说明

“Firebase Auth 负责身份，Firestore 负责用户档案和单点登录租约，Fusion 负责实时房间和玩家状态。应用层先完成 Firebase 登录，再通过 Session Lease 防止同一账号多端同时在线，读取档案后才进入 Play。Fusion 连接由独立协调器管理，网络玩家只接收已准备好的展示名，不直接访问 Firebase。UI 只处理输入和状态显示，因此更换存储、网络方案或登录方式不会扩散到界面代码。”

