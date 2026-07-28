# Fusion v1 大厅与房间联机技术方案

## 目标与结论

为当前 ARPG 建立一个基于 Photon Fusion **1.1.0** 的“大厅 -> 房间列表 -> 创建/加入房间 -> 进入游戏”流程。首期统一采用 **Shared Mode**，沿用现有 `NetworkPlayer` 的 `HasStateAuthority` 写入方式；不使用 PUN 的 `PhotonNetwork`、`PhotonView` 或 PUN 房间 API。

项目已具备 Fusion Cloud 配置（固定 `hk` 区域）和玩家网络对象，但尚未实现 Session Lobby、会话列表 UI 与可控的房间状态机。`Play` 场景的 `FusionBootstrap` 当前为 Automatic + Shared，且会自行 `StartGame` 到默认房间；它必须在本方案实施时被改为 Manual 或移除。否则新增 Coordinator 会产生双 Runner 或绕过大厅。

## 事实依据与边界

- 引擎：Unity 2022.3.62f2；SDK：`com.exitgames.photonfusion` 1.1.0。
- Fusion 入口配置为 `Assets/Photon/Fusion/Resources/PhotonAppSettings.asset`，PUN 的 `PhotonServerSettings.asset` 与本方案无关。
- `NetworkPlayer` 已用 `[Networked, OnChangedRender]` 同步昵称；`PlayerSpawner` 已在 `IPlayerJoined.PlayerJoined` 中生成玩家。
- `FusionAuthSessionBridge` 已监听断线和 Runner 关闭以释放 Firebase 在线租约；不得在新增流程中绕过它。
- `PlayerSpawner` 是 `Play` 场景对象。没有经由网络场景加载到该场景前，不能承诺玩家生成。
- 用户提供的 Fusion Stage 官方页面在本次检索时被站点 Cookie/人机验证拦截，无法取得正文。下列 API 语义以项目安装包的 Fusion v1 `Fusion.Runtime.xml` 交叉核对，并保留教程链接供实施前人工复核：<https://doc.photonengine.com/zh-tw/fusion/v1/industries-samples/fusion-stage>。

## 设计

### 会话流程

```text
登录完成
  -> FusionSessionCoordinator 使用唯一 Runner prefab（接管既有 Prototype Runner）
  -> JoinSessionLobby(SessionLobby.Shared, "arpg-v1")
  -> OnSessionListUpdated 更新只读房间列表 UI
  -> 创建或选择房间
  -> StartGame(GameMode.Shared, SessionName, PlayerCount, SessionProperties)
  -> IPlayerJoined 生成本地拥有的玩家
  -> 离开/断线: Runner.Shutdown -> 释放租约 -> 重新获取租约后才可重入大厅
```

`FusionSessionCoordinator` 是唯一允许启动、关闭或替换 Runner 的服务。实施前先把 `Play` 场景的 `FusionBootstrap` 切至 Manual（或删除），并将其现有 `Prototype Runner` 作为唯一 Runner prefab；UI 只能调用 Coordinator 的 `JoinLobbyAsync`、`CreateRoomAsync`、`JoinRoomAsync` 和 `LeaveAsync`。会话列表回调应由独立 `FusionLobbyService : INetworkRunnerCallbacks` 消费，不能继续放在认证桥接类中。

Lobby 位于登录后的持久 UI 场景。创建/加入成功时，Coordinator 通过 `StartGameArgs.Scene` 和 `INetworkSceneManager` 网络化加载 `Play`；在 `OnSceneLoadDone` 后确认该场景的 `PlayerSpawner` 已纳入 Runner，再启用战斗 UI。玩家对象以 `PlayerRef -> NetworkObject` 映射管理，`IPlayerLeft` 只由其合法 Authority 清理。

### 房间模型

- 大厅名固定为 `arpg-v1`，避免测试会话和正式会话混杂。
- 每个会话明确设置 `SessionName`、`PlayerCount`、`IsOpen`、`IsVisible` 和 `CustomLobbyName`。
- `SessionProperties` 仅放可公开筛选/展示的短字段：`map`、`difficulty`、`phase`、`build`。禁止放 Firebase UID、令牌、背包或任何可信业务状态。
- 首期房间上限建议 4 人。房间元数据由创建者任命的 MasterClient Lobby Manager 写入 `Runner.SessionInfo.IsOpen` 或公开 `phase` 属性；该权限是产品协作约定，不是防作弊边界。MasterClient 迁移时，新 MasterClient 从网络化房间状态恢复管理权。
- 加入指定房间时，`GameMode.Shared + SessionName`，且不启用客户端建房；创建房间则使用 `GameMode.Shared`。`EnableClientSessionCreation` 在 v1 文档中针对 Client 模式定义，Shared Mode 的实际行为须由两客户端集成测试确认，失败时禁止隐式建房并提示“房间不存在”。

## 权限分层与模式策略

权限流图见 [FusionAuthorityModeFlow.drawio](FusionAuthorityModeFlow.drawio)。核心原则是：**Input Authority 只表达输入意图，State Authority 才能计算并发布游戏结果；UI 和渲染从不决定真实状态。** 这一区分在 Shared 中也必须保留，即使同一客户端暂时拥有两种权限。

| 层 | 唯一职责 | Fusion 边界 |
| --- | --- | --- |
| 输入适配 | 把 WASD、空格、鼠标等本地设备数据采样成 `PlayerInput`，不移动角色。 | `INetworkInput`、`INetworkRunnerCallbacks.OnInput`、`HasInputAuthority` |
| 领域模拟 | 根据输入、碰撞、冷却和规则计算移动、跳跃、伤害；不读 Unity `Input`，不触碰 UI。 | 纯 C# 规则服务，由网络行为调用 |
| 权威发布 | 验证输入后写入位置、速度、动画和持久网络状态。 | `HasStateAuthority`、`[Networked]`、`FixedUpdateNetwork`、`GetInput<T>` |
| 表现 | 读取同步状态更新摄像机、动画、标签和音效；不回写游戏结果。 | `Render`、`OnChangedRender`、`HasInputAuthority` 仅用于本地镜头/UI |

当前 `PlayerSpawner` 在 `Runner.Spawn(..., player)` 中把 `player` 设为角色的 **Input Authority**。Shared 模式下，生成角色的本机也通常持有该角色的 State Authority；这只是当前模式下的重合，不是两项权限等价。`PlayerMovement` 目前在 `HasStateAuthority` 下直接读取 Unity 输入，`NetworkPlayer.Local` 也实际表示“本机 State Authority 角色”，两者都必须解耦。

### 按模式的权威矩阵

| `GameMode` | 输入权威 | 角色状态权威 | 生成与离开清理 | 房间/世界管理 |
| --- | --- | --- | --- | --- |
| `Shared` | 每个角色归属的 `PlayerRef` | 首期为生成该角色的玩家 | 本机为自己生成；`DestroyWhenStateAuthorityLeaves` 销毁角色，Registry 清理映射 | `Runner.IsSharedModeMasterClient` 管理 Room metadata；世界对象须显式按 MasterClient 方式生成 |
| `Host` | 每个加入者自身 | Host | Host 为每个 `PlayerJoined` 生成角色，传入该玩家的 `PlayerRef` 作为 Input Authority；Host 在 `PlayerLeft` 显式 Despawn | Host |
| `Server` | 每个加入者自身 | Dedicated Server，无本地玩家 | Server 为每个 `PlayerJoined` 生成角色，传入该玩家的 `PlayerRef`；Server 在 `PlayerLeft` 显式 Despawn | Dedicated Server |
| `Client` | 本机玩家 | Host/Server | 不生成、销毁或直接写角色状态 | Host/Server |
| `AutoHostOrClient` | 进入后按实际角色解析 | 首个玩家为 Host，其他人为 Client | 按最终 `IsServer`/`IsClient` 套用 Host 或 Client 生命周期 | Host |
| `Single` | 本机 | 本机 | 本机生成与销毁 | 本机 |

这与 Fusion v1 API 定义一致：`Shared` 是连接 Photon Cloud Fusion Plugin 的游戏客户端；`Host` 同时启动游戏服务器和本地玩家；`Server` 是无本地玩家的 Dedicated Server；`Client` 连接 Host 或 Server；`AutoHostOrClient` 由首位加入者成为 Host。不要以 PUN 的“房主等于所有对象权威”假设解释 Shared。`Runner.IsSharedModeMasterClient` 仅用于 Shared 下的房间协调；会话开放、可见性和公开属性是 Room metadata，应由该管理器调用 `SessionInfo.IsOpen`、`IsVisible`、`UpdateCustomProperties`，而不是伪装为某个 `NetworkObject` 的 `[Networked]` 字段。MasterClient 不是共享世界对象的自动权威：需要迁移的对象必须显式使用 `NetworkObjectFlags.MasterClientObject` 或以 `NetworkSpawnFlags.SharedModeStateAuthMasterClient` 生成，才能在 MasterClient 离开时自动转移 State Authority。

### 可扩展架构与开闭约束

采用 **Strategy + Factory + Adapter**，而不是在角色脚本中散布 `if (Runner.GameMode == ...)`：

- `IAuthorityPolicy` 是策略端口，回答“此对象的本机是否可采样输入、模拟、写状态、执行管理命令”。Factory 按 **实际 Runner 角色** 创建组合策略：Shared 使用 `SharedAuthorityPolicy`，Host/Server 端使用 `ServerAuthorityPolicy`，Client 使用 `ClientAuthorityPolicy`，Single 使用 `SingleAuthorityPolicy`；`AutoHostOrClient` 必须根据 `IsServer`/`IsClient` 解析，而非只检查启动时的 `GameMode`。Host 的本地输入由 `LocalInputPolicy` 与 `ServerAuthorityPolicy` 组合。新增模式或规则只增加策略实现，不修改 `PlayerMotor`、UI 或领域规则，满足开闭原则。
- `IPlayerInputSource` 是输入端口；Fusion 适配器在 `OnInput` 输出 `PlayerInput`，`PlayerMotor` 仅在 `FixedUpdateNetwork` 中通过 `GetInput<PlayerInput>` 消费。`PlayerInput` 包含移动向量、视角和 `NetworkButtons`（跳跃、冲刺、攻击），不得传位置、速度、伤害或背包值。
- `IPlayerMotor`/`MovementRules` 是无 Fusion 依赖的领域规则；`FusionPlayerMotor : NetworkBehaviour` 只负责把网络输入交给规则、以 `Runner.DeltaTime` 推进并写回网络状态。角色预制体必须经审计后使用 `NetworkTransform`、KCC 或等价网络组件同步权威端结果；`CharacterController.Move` 本身不构成网络同步。
- `PlayerAvatarRegistry` 用 `PlayerRef -> NetworkObject` 管理玩家对象，并分别暴露 Input-owned 与 State-owned 查询；废弃语义含糊的 `NetworkPlayer.Local`。`PlayerSpawner`、离开清理和改名流程只依赖该端口，不依赖静态对象查找。
- 认证、档案和网络分别由 `UserNameController`、`PlayerIdentitySyncService`、`FusionPlayerIdentityAdapter` 负责。`NetworkPlayer.Spawned` 不再从 State Authority 的本地档案初始化每个角色名称。角色生成后，Input Authority 提交一次受限身份/展示名命令；Shared 由自身 State Authority 写入，Host/Server 由 `RpcSources.InputAuthority -> RpcTargets.StateAuthority` 接收、校验发送者与对象 Input Authority 匹配并写入 `[Networked] PlayerName`。展示名作为非可信外观数据；若要把 UID 与经济权限绑定，Server 必须验证 Firebase 身份令牌，不能信任客户端传入 UID。

禁止的耦合包括：领域规则直接调用 `UnityEngine.Input`、UI 直接设置 `[Networked]` 字段、Client 直接写角色位置/伤害、以及通过 RPC 同步应持久存在的状态。RPC 只传命令或瞬时通知；最终状态始终由 State Authority 写入并通过快照复制。

### 迁移顺序

1. 定义 `PlayerInput`、`IPlayerInputSource`、`IAuthorityPolicy`、`PlayerAvatarRegistry` 和无 Fusion 依赖的 `MovementRules`，先补单元测试覆盖跳跃、重力和冷却规则。
2. 以 Fusion 适配器替换 `PlayerMovement` 对 `Input.GetAxis` 的直接读取：本地 Input Authority 在 `OnInput` 采样，State Authority 在 `FixedUpdateNetwork` 经 `GetInput` 消费；保留 Shared 的行为结果。
3. 重构昵称与玩家生成：Shared 保留“本机生成自己”，Host/Server 改由 Server-side Spawner 为每个 `PlayerJoined` 生成并绑定该 `PlayerRef` 的 Input Authority；`PlayerLeft` 在 Host/Server 显式 Despawn。初始昵称和改名均走受限身份命令，禁止 `Spawned` 从 Host/Server 本地档案复制名称。
4. 将房间管理与玩法管理分离：Shared 下由 MasterClient Lobby Manager 更新 `SessionInfo`；可迁移世界对象显式标记 `MasterClientObject` 或以 `SharedModeStateAuthMasterClient` 生成，实现 MasterClient 迁移恢复。
5. 在不改动 UI、输入 DTO 和领域规则的前提下，以 Host，再以 Dedicated Server 运行相同会话场景，验证策略替换即可完成模式迁移；另测 `AutoHostOrClient` 首位与后续加入者分别解析为 Server 和 Client 策略。

新增验收：Shared 两客户端中每个玩家只能驱动自己的输入角色；远端 Proxy 无法通过 UI、RPC 或本地输入改写他人位置和昵称；Host/Server 为每位 Client 生成一名 Input-owned 角色且不复用 Host 名称，普通 Client 的输入仍可驱动自己的角色，但只有 Host/Server 发布位置、跳跃和昵称；Shared 的 `DestroyWhenStateAuthorityLeaves` 与 Host/Server 的显式 Despawn 分别正确清理；标记为 MasterClient 对象的共享世界状态在迁移后可继续管理；经济、伤害和掉落的最终写入只在 Server 策略下通过。

## Fusion v1 编码规范

| 范畴 | 规范 | 推荐 API |
| --- | --- | --- |
| Runner 生命周期 | 先禁用自动 Bootstrap；只由 Coordinator 使用唯一 prefab 创建并 `await` 启动。每次失败、离房或切换场景都完成 `Shutdown` 后再允许下一次启动。 | `NetworkRunner.StartGame(StartGameArgs)`、`Shutdown()` |
| 大厅与列表 | 入大厅后才以 `OnSessionListUpdated` 驱动 UI；回调仅更新内存快照，不直接创建网络对象。 | `JoinSessionLobby`、`SessionInfo`、`INetworkRunnerCallbacks` |
| 网络状态 | 持久且所有客户端都要读取的状态使用 `[Networked]` 自动属性；只由 `HasStateAuthority` 写入。变化后的显示刷新使用 `OnChangedRender`。 | `NetworkBehaviour`、`[Networked]`、`[OnChangedRender]` |
| 模拟 | 影响游戏结果的逻辑只放在 `FixedUpdateNetwork`，以 `Runner.DeltaTime` 推进；不要在其中直接依赖 `Time.deltaTime`、未同步随机数或本地 UI 状态。 | `FixedUpdateNetwork()` |
| 输入 | Input Authority 在 Unity `Update` 采样并缓存；`OnInput` 提交 `INetworkInput`，State Authority 在模拟 tick 中用 `GetInput` 消费。 | `HasInputAuthority`、`INetworkInput`、`OnInput`、`GetInput<T>` |
| RPC | RPC 只用于瞬时命令或通知，明确标注来源/目标并在 State Authority 再校验。不能用 RPC 替代 `[Networked]` 持久状态。 | `[Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]` |
| 对象生命周期 | 只能由拥有 State Authority 的一方生成/销毁，生成统一使用 `Runner.Spawn`，离开时清理对应 `NetworkObject` 映射。 | `IPlayerJoined`、`IPlayerLeft`、`Runner.Spawn`、`Runner.Despawn` |
| 回调注册 | `AddCallbacks` 与 `RemoveCallbacks` 成对出现；每个回调类只承担一个领域职责。 | `INetworkRunnerCallbacks` |

现有 `PlayerMovement` 在 `FixedUpdateNetwork` 中直接调用 `Input.GetAxis` 和读取非网络化的跳跃/冲刺状态。首期大厅完成后应优先迁移到上述输入管线，否则 Shared Mode 下的输入重放、观察端表现和未来 Host/Server 迁移都会缺乏清晰边界。

## 关键 API 使用约定

```csharp
// 入大厅。只有此调用成功后，OnSessionListUpdated 才是房间 UI 的数据源。
await runner.JoinSessionLobby(SessionLobby.Shared, "arpg-v1");

// 创建房间。StartGame 的结果必须 await 并检查成功状态后再切换 UI。
var result = await runner.StartGame(new StartGameArgs {
    GameMode = GameMode.Shared,
    SessionName = roomName,
    PlayerCount = 4,
    CustomLobbyName = "arpg-v1",
    IsOpen = true,
    IsVisible = true,
    SessionProperties = properties,
});
```

`SessionName` 为 `null` 时代表随机匹配，不可用于“加入指定房间”。`SessionProperties` 创建时是初始会话属性，随机匹配时也是筛选条件。`PlayerCount` 覆盖全局 Network Project Config 的人数设置。以上均为项目内 Fusion v1 API 文档的定义。

## 实施顺序与验收

1. 将 `Play` 的 `FusionBootstrap` 改为 Manual 或移除，明确唯一 Runner prefab、`StartGameArgs.Scene` 与网络场景管理器；禁止第二个 Runner。
2. 新建 `FusionSessionCoordinator`、`FusionLobbyService` 与房间 DTO/UI；保留 `FusionAuthSessionBridge` 只负责认证租约。
3. 实现入大厅、会话列表、创建、指定加入、快速匹配、退出和失败提示；所有异步操作防重复提交并可取消。
4. 增加“离房重入”认证状态机：`Shutdown` 触发当前租约释放后，使用已登录 Firebase 用户生成新的 sessionId，`ForceAcquireAsync` 成功才调用 `AuthSessionGuard.Begin` 并重建 Lobby Runner；抢占或获取失败回登录页，不能带旧 sessionId 重连。
5. 在 `IPlayerLeft` 中按 Authority 清理玩家对象；补齐房间满员、关闭、同名、断线、重复点击、MasterClient 迁移、场景加载和回大厅的状态测试。
6. 将 `PlayerMovement` 改为 `INetworkInput` 输入采集与 tick 消费，再开始战斗/技能的网络化。

验收标准：两台独立客户端能在 `hk` 区域看到同一 `arpg-v1` 大厅中的公开房间；创建者与加入者在网络加载 `Play` 后均能生成且看到对方昵称；满员或关闭房间不可加入；任意一方离房/断线后 Firebase 在线租约被释放，重新获取新租约后可再次进入大厅或房间；全流程只有一个 Runner。

## 后续决策

Shared Mode 适合当前原型和小规模协作，但客户端持有 State Authority，不适合把掉落、货币、伤害结算作为可信结果。若 ARPG 后续要防作弊或支持持久经济，应把战斗权威迁往 Host/Server 或 Dedicated Server，并保持本方案的大厅和会话接口不变。
