# ARPG

本仓库是一个使用 Unity 制作的 ARPG 原型，包含账号注册/登录、玩家昵称持久化、同账号单点登录（顶号），以及 Photon Fusion 联机玩家生成与昵称同步。

> 本文以仓库代码、Unity 项目设置和 `D:\桌面\后端配置` 中于 2026-07-25 截取的后台画面为依据。截图已尽量遮蔽账号、UID 与应用 ID；若画面中仍残留项目编号、UID 片段等标识，请勿当作可用凭据使用，也不要将真实密钥或用户数据提交到仓库。

## 快速结论

| 项目 | 实际技术选型 |
| --- | --- |
| Unity 版本 | **Unity 2022.3.62f2**。`ProjectSettings/ProjectVersion.txt` 当前记录为 `2022.3.62f2c1`。 |
| 身份认证 | Firebase Authentication，当前代码使用邮箱/密码。 |
| 数据库 | Cloud Firestore（NoSQL 文档数据库），不是 MySQL/PostgreSQL/MongoDB，也没有本地 SQL schema。 |
| 联机 | Photon Fusion，连接 Photon Cloud；后台截图显示应用使用 Lobbies V2、20 CCU。 |
| 前端与后端通信 | Unity 通过 Firebase Unity SDK 直连 Authentication/Firestore；通过 Fusion SDK 连 Photon Cloud。没有 RESTful API、GraphQL、自建 WebSocket 服务或自建 Node.js/Python 后端。 |
| 本地运行 | 只需 Unity 与互联网连接。Firebase/Photon 均为云端托管服务，因此没有 Docker Compose、数据库容器或后端进程需要启动。 |

## 系统架构

![系统架构](Docs/images/backend-configuration/system-architecture.drawio.png)

可在 draw.io/diagrams.net 中编辑源文件：[system-architecture.drawio](Docs/images/backend-configuration/system-architecture.drawio)。

```mermaid
flowchart LR
    U[Unity 客户端\nUnity 2022.3.62f2]
    A[Firebase Authentication\n邮箱/密码认证]
    F[(Cloud Firestore\nUsers/{uid})]
    R[Firestore 安全规则\nrequest.auth.uid == uid]
    P[Photon Cloud\nFusion / Lobbies V2]
    O[其他在线玩家\nFusion Shared Mode]
    U -->|Firebase Unity SDK| A
    U -->|Firestore：档案、Listen 顶号、心跳兜底| F
    R -->|授权| F
    U -->|Fusion SDK| P
    P -->|房间与状态同步| O
```

1. Unity 登录界面将邮箱与密码交给 `FirebaseAuthManager`，由 Firebase Authentication 完成注册或登录。
2. 登录成功后，`AuthController` 生成新的会话 ID，并通过 `ForceAcquireAsync` 覆盖 Firestore `Users/{uid}` 的在线会话。
3. 同 UID 的旧客户端以 **Firestore `Listen` 为主要检测**（`activeSessionId` 被覆盖即踢下线）；**每 3 秒心跳为兜底**（`HeartbeatAsync` 返回 `Replaced` 时同样踢下线）。被踢后退出 Fusion、清除 Firebase 登录态并返回 `LoginMenu`。
4. 玩家昵称由 Firestore 保存，`NetworkPlayer` 再将昵称同步到 Fusion 的网络对象。
5. Fusion 将实时房间与状态同步交给 Photon Cloud。它与 Firestore 的档案/会话数据是两条独立链路。

## 项目结构

```text
Assets/FireBase+Photon/
├─ Scenes/
│  ├─ LoginMenu.unity             # 启动场景
│  └─ Play.unity                  # 游戏场景
└─ Scripts/
   ├─ Auth/
   │  ├─ Domain/                  # 认证/会话相关模型
   │  ├─ Application/             # 登录、注册、昵称与流程编排
   │  ├─ Infrastructure/
   │  │  ├─ Firebase/             # Firebase 初始化与邮箱认证
   │  │  └─ Firestore/            # 档案和会话数据访问
   │  ├─ Networking/              # Firestore 会话与 Fusion 生命周期桥接
   │  └─ View/
   │     ├─ Login/                # 登录/注册表单与绑定
   │     ├─ Profile/              # 昵称面板
   │     └─ Shared/               # Loading 等共用 UI
   ├─ Player/Network/             # Fusion 玩家生成、网络昵称
   └─ GameFlow/                   # LoginMenu / Play 场景切换
```

构建场景已在 `ProjectSettings/EditorBuildSettings.asset` 中启用，顺序是 `LoginMenu`、`Play`。`LoginMenu` 是本机运行的入口。

## 数据库与后端

### Cloud Firestore

本项目使用 Cloud Firestore 的默认数据库。它是 Firebase 托管的文档型 NoSQL 数据库，客户端通过 Firebase Unity SDK 直接访问，而非经过自建 API。

当前代码使用的集合与字段如下：

```text
Users/{uid}
├─ name: string
├─ activeSessionId: string
├─ sessionOnline: bool
└─ sessionHeartbeatUnix: number
```

- `FirestoreUserProfileRepository` 只读写 `Users/{uid}.name`，写入使用 `SetOptions.MergeAll`，不会覆盖未来新增的字段。
- `FirestoreAuthSessionRepository` 维护 `activeSessionId`、在线状态与 Unix 时间戳心跳；新端登录直接 `ForceAcquireAsync` 覆盖会话。
- `AuthSessionGuard` 同时启动 Firestore **`ListenSession`（主路径）** 与 **每 3 秒 `HeartbeatAsync`（兜底）**；任一路径发现 `activeSessionId` 已被其他端覆盖时，本端退出 Fusion、清除 Firebase 登录态并返回 `LoginMenu`。
- 桌面版 Firestore 本地持久化已在 `FirestoreClient` 明确关闭（`PersistenceEnabled = false`），以回避该 SDK 在 Windows Standalone 上的原生稳定性问题。

### Firestore 安全规则

当前后台截图及项目设计对应的规则是：用户只能访问自己 UID 对应的文档。

```javascript
rules_version = '2';

service cloud.firestore {
  match /databases/{database}/documents {
    match /Users/{uid} {
      allow read, write: if request.auth != null && request.auth.uid == uid;
    }
  }
}
```

此规则适用于本项目目前的直连 BaaS 结构。它防止 A 用户读写 B 用户的档案，但不会把客户端变成可信服务器：用户仍可写自己的允许字段。排行榜、付费、货币发放、反作弊等高价值逻辑必须迁移至可信服务端（例如 Cloud Functions/Cloud Run 或自建后端）后再开放。

### 为什么没有后端启动命令

仓库没有 `docker-compose.yml`、SQL migration、Node.js `package.json` 或 Python 服务入口。运行时依赖的是 Firebase 与 Photon Cloud 的托管服务，因此：

- 不需要启动 MySQL/PostgreSQL/MongoDB；
- 不需要启动 REST、GraphQL 或 WebSocket 后端；
- 不存在可在 Unity Inspector 中改写的 `Server URL`；
- 本地离线时，注册、登录、Firestore 档案和 Photon 联机都会失败或不可用。

## 通信机制

| 链路 | 协议/SDK | 用途 | 配置位置 |
| --- | --- | --- | --- |
| Unity -> Firebase Authentication | Firebase Unity SDK | 邮箱/密码注册与登录、登出 | `Assets/google-services.json`、`Assets/StreamingAssets/google-services-desktop.json` |
| Unity -> Cloud Firestore | Firebase Unity SDK | `Users/{uid}` 档案、会话 Listen、心跳兜底 | 同上，以及 Firebase Console 的 Firestore 规则 |
| Unity -> Photon Cloud | Photon Fusion SDK | 房间、实时玩家状态与昵称同步 | `Assets/Photon/Fusion/Resources/PhotonAppSettings.asset` |
| Fusion 断线 -> Firestore | 本地 C# 桥接 | 断线/离房后释放在线会话 | `FusionAuthSessionBridge.cs`、`AuthSessionGuard.cs` |

当前联机实现使用 Fusion。`Assets/Photon/PhotonUnityNetworking/Resources/PhotonServerSettings.asset` 是 PUN 的独立设置资源；不要用它替代 Fusion 的 `PhotonAppSettings.asset`。Fusion 设置当前使用 Photon Name Server、固定香港区域（`hk`），没有填写自建服务器地址。

Firebase Console 的截图显示 **Google** 登录提供方已启用，但当前仓库的 `FirebaseAuthManager` 只调用 `CreateUserWithEmailAndPasswordAsync` 与 `SignInWithEmailAndPasswordAsync`。因此，Google 登录尚未由 Unity UI/代码接入；仅在 Console 启用不代表游戏内已经可用。

## 首次配置

### 1. 安装并打开 Unity

1. 在 Unity Hub 安装 **Unity 2022.3.62f2**，并包含目标平台的 Build Support。
2. 使用 Unity Hub 的 **Add/Open** 打开本仓库根目录。
3. 等待 Unity 导入资源和生成项目文件；不要用其它主版本打开后再提交 `ProjectSettings/ProjectVersion.txt`。
4. 如 Package Manager 报 `com.unitymcp.server` 找不到，检查 `Packages/manifest.json` 中的本地绝对路径是否在当前机器存在。该包是开发辅助工具，不是游戏运行时后端。

### 2. 配置 Firebase

1. 打开 Firebase Console，创建或选择项目。
2. 在 **项目设置 -> 常规** 添加 Android 应用；包名必须与 Unity 的 `Project Settings -> Player -> Android -> Package Name` 完全一致。
3. 下载新的 `google-services.json`，替换 `Assets/google-services.json`；桌面目标也同步替换 `Assets/StreamingAssets/google-services-desktop.json`。
4. 在 **Authentication -> 登录方法** 启用 **电子邮件地址/密码**。这是当前代码唯一实际调用的认证方式。
5. 在 **Firestore Database** 创建默认数据库，并在 **规则** 页发布上一节的规则。
6. 首次登录后，游戏会按需创建/更新 `Users/{Firebase UID}`。不需要预先手工创建用户文档。

若未来接入 Google 登录，除启用提供方外，还需在 Unity 侧实现 Google 凭据取得与 `FirebaseAuth.SignInWithCredentialAsync` 调用，并为 Android 发布签名配置 SHA 指纹；这不属于当前代码已完成的功能。

### 3. 配置 Photon Fusion

1. 登录 Photon Dashboard，创建 **Fusion** 应用。
2. 在 Dashboard 中确认该应用可用的 CCU 与 Lobbies；本项目提供的截图是 Lobbies V2、20 CCU 的配置示例，不应把截图中的应用 ID 复制到新环境。
3. 在 Unity 中打开 `Assets/Photon/Fusion/Resources/PhotonAppSettings.asset`，将 Dashboard 的 Fusion **App ID** 填入 `AppIdFusion`。
4. 保持 `UseNameServer` 启用；如需固定区域，设置 `FixedRegion`（现有项目使用 `hk`）。将其清空可让 Photon 选择最佳区域。
5. 不填写 `Server` 或 `Port`，除非你明确切换到自建 Photon Server；当前项目设计连接 Photon Cloud。

### 4. 运行与验证

1. 打开 `Assets/FireBase+Photon/Scenes/LoginMenu.unity`。
2. 点击 Play，使用邮箱/密码注册一个测试账号；注册成功后按当前流程会登出并返回登录页。
3. 用该账号登录，设置昵称并进入 `Play`。
4. 用两个 Unity Editor 实例或一个 Editor 加一个 Build 测试 Fusion 联机；可使用仓库现有的 ParrelSync 工具辅助多开。
5. 用相同账号从第二个实例登录，确认第一个实例通过 Listen（或心跳兜底）发现会话覆盖后回到登录场景。

## Unity 构建规范

项目必须使用 **Unity 2022.3.62f2** 开发与构建。每次发布前请在该版本执行：

1. `Assets -> Reimport All` 仅在依赖或导入异常时使用，平时不应作为构建步骤。
2. 打开 `LoginMenu.unity`，确认 Console 没有红色编译错误。
3. 在 `File -> Build Settings` 确认 `LoginMenu` 与 `Play` 已勾选。
4. 选择目标平台后执行 Build，并在目标设备验证 Firebase 登录与 Photon 联机。

本 README 不新增第三方运行时包；现有 Firebase、Photon Fusion、TextMeshPro、Cinemachine、DOTween 与 ParrelSync 等依赖仍以仓库当前状态为准。

## 后台配置截图

以下截图均来自 `D:\桌面\后端配置`，已尽量去识别化。它们是当前配置的佐证，不能代替新 Firebase/Photon 项目的实际配置。若仍可见项目编号或 UID 片段，请视为已脱敏不完全的残留信息，勿用于生产或公开传播。

| 截图 | 说明 |
| --- | --- |
| ![Firebase 项目设置](Docs/images/backend-configuration/firebase-project-settings.png) | Firebase 项目常规设置。 |
| ![Firebase Android 应用](Docs/images/backend-configuration/firebase-android-app.png) | Android 应用与 `google-services.json` 下载入口。 |
| ![Firebase Authentication 用户](Docs/images/backend-configuration/firebase-auth-users.png) | Authentication 用户管理页，用户资料已遮蔽。 |
| ![Firebase 登录提供方](Docs/images/backend-configuration/firebase-sign-in-providers.png) | 邮箱/密码与 Google 提供方在 Console 中已启用。 |
| ![Firestore Users 文档](Docs/images/backend-configuration/firestore-users-document.png) | `Users` 集合与会话字段示例，文档 UID 已遮蔽。 |
| ![Firestore 规则](Docs/images/backend-configuration/firestore-security-rules.png) | `Users/{uid}` 的归属访问规则。 |
| ![Photon Dashboard 应用概览](Docs/images/backend-configuration/photon-dashboard-overview.png) | Photon 应用、Lobbies V2 与 CCU 配额，应用 ID 已遮蔽。 |
| ![Photon Dashboard 应用详情](Docs/images/backend-configuration/photon-dashboard-details.png) | Photon 应用详情，应用 ID 已遮蔽。 |

## AI 使用说明

本次文档编写使用 AI 协助完成以下工作：梳理 Unity 工程中 Firebase/Fusion 的调用链、比对配置截图与实际资源路径、识别「已在 Console 启用但尚未由代码接入」的 Google 登录差异，并生成结构化 README。AI 的价值在于快速建立跨目录的架构视图、减少遗漏和重复查找。

所有结论均以仓库内的 C#、Unity 资源、项目设置和所提供截图复核后写入；AI 不替代 Firebase/Photon 帐号权限，也不直接管理线上数据或发布配置。开发者仍应在目标 Unity 版本、目标 Firebase 项目和 Photon 应用上完成实际构建与联机验收。

## 关键源码索引

- Firebase 初始化及邮箱认证：`Assets/FireBase+Photon/Scripts/Auth/Infrastructure/Firebase/FirebaseAuthManager.cs`
- Firestore 初始化与桌面持久化设置：`Assets/FireBase+Photon/Scripts/Auth/Infrastructure/Firestore/FirestoreClient.cs`
- 玩家昵称档案：`Assets/FireBase+Photon/Scripts/Auth/Infrastructure/Firestore/FirestoreUserProfileRepository.cs`
- 单点登录会话：`Assets/FireBase+Photon/Scripts/Auth/Infrastructure/Firestore/FirestoreAuthSessionRepository.cs`
- 被顶号与 Fusion 生命周期：`Assets/FireBase+Photon/Scripts/Auth/Networking/Session/AuthSessionGuard.cs`、`Assets/FireBase+Photon/Scripts/Auth/Networking/Fusion/FusionAuthSessionBridge.cs`
- Fusion 网络玩家：`Assets/FireBase+Photon/Scripts/Player/Network/PlayerSpawner.cs`、`Assets/FireBase+Photon/Scripts/Player/Network/NetworkPlayer.cs`
