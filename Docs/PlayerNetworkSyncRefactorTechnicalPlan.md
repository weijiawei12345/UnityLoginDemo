# 玩家网络同步渐进式重构技术计划

> **实施要求：**执行本计划时使用 `superpowers:subagent-driven-development` 或 `superpowers:executing-plans`，按阶段完成测试、资源接线和双客户端验收。本文件仅是实施计划，本轮不修改玩家运行代码。

## 目标

在保留 Photon Fusion 1.1.0 Shared Mode、现有 `Player Mixamo` 预制体和 `CharacterController + NetworkTransform + NetworkMecanimAnimator` 技术栈的前提下，渐进拆分玩家输入、移动规则、网络 Tick 执行、动画表现与本地摄像机绑定。

完成后应达到以下结果：

- 本地设备输入只由 Input Authority 采集，不再由代理玩家读取。
- 角色移动只在 `FixedUpdateNetwork` 中消费 Fusion 当前 Tick 的 `PlayerInputFrame`。
- 移动规则可脱离 Unity 场景进行 EditMode 测试。
- 动画参数从已计算的移动状态派生，不重复猜测输入或落地状态。
- 摄像机只绑定本地 Input Authority 玩家。
- 每个类型只有一个主要职责，公共契约和复杂 Tick/权限原因有明确注释。
- 每个迁移阶段均可编译、可回滚；最终删除旧 `PlayerMovement.cs`，不保留双控制链。

## 非目标

- 不升级 Fusion SDK，不从 Shared Mode 切换到 Host/Server Mode。
- 不引入新的 Unity Input System；本期继续使用 Legacy Input Manager。
- 不重写 `NetworkTransform` 插值、Fusion 预测或底层传输。
- 不修改认证、大厅、房间列表、昵称持久化或 Firestore 契约。
- 不增加战斗、技能、耐力、斜坡滑动、二段跳、冲刺冷却等新玩法。
- 不引入依赖注入容器、全局事件总线、通用有限状态机或自研网络框架。
- Unity MCP 只用于读取配置和资源事实；双客户端运行验收仍由人工或 ParrelSync 完成。

## 已确认事实

### 工程与 Fusion

- Unity：`2022.3.62f2c1`。
- Fusion：`Assets/Photon/Fusion/package.json` 中版本为 `1.1.0`。
- 输入后端：Legacy Input Manager。
- `FusionSessionCoordinator.CreateRunner()` 设置 `_runner.ProvideInput = true`。
- 会话模式为 `GameMode.Shared`。
- `NetworkProjectConfig.fusion` 当前客户端 Tick Rate 为 64，`PlayerCount` 为 10，未启用模拟网络条件。
- Fusion v1 本地 XML 明确说明：`OnInput` 用 `NetworkInput.Set(...)` 提交输入；`NetworkBehaviour.GetInput<T>` 在 `FixedUpdateNetwork` 读取当前 Tick、来自对象 Input Authority 的输入。

### 场景与预制体

- 当前打开场景是 `Assets/FireBase+Photon/Scenes/Play.unity`，未进入 Play Mode，场景未标脏。
- `Play` 场景包含 `Main Camera`，其上挂有 `ThirdPersonCamera`；包含 `Player Spawner`，其上挂有 `PlayerSpawner`。
- `PlayerSpawner.PlayerPrefab` 指向 `Assets/FireBase+Photon/Prefab/Player Mixamo.prefab`。
- 玩家预制体带 `FusionPrefab` 标签，并包含：
  - `NetworkObject`
  - `NetworkTransform`
  - `NetworkMecanimAnimator`
  - `CharacterController`
  - `PlayerMovement`
  - `NetworkPlayer`
- `NetworkMecanimAnimator.Animator` 指向模型子节点的 `Animator`。
- 预制体实际引用 `Assets/FireBase+Photon/Mixamo/Animation/Animation/Locomotions/Player.controller`，参数为 `Speed`、`Jump`、`Grounded`、`FreeFall`、`MotionSpeed`。
- `Assets/FireBase+Photon/Charactor/Player.controller` 是另一个仅含 Idle、无参数的控制器，不是玩家预制体当前使用的控制器。实施时不得误改该资产。

### 当前运行代码

- `PlayerMovement` 同时负责输入、移动、跳跃、冲刺开关、摄像机绑定和 Animator 参数。
- `Update` 与 `FixedUpdateNetwork` 都直接调用 `Input`，网络 Tick 未使用 `GetInput<T>`。
- 本地控制和摄像机绑定以 `HasStateAuthority` 为门槛，而不是语义更明确的 `HasInputAuthority`。
- `PlayerSpawner` 使用 `runner.Spawn(..., player, ..., NetworkSpawnFlags.SharedModeStateAuthLocalPlayer)`，为本地玩家分配 Input Authority，并在 Shared Mode 下请求本地 State Authority。
- `NetworkPlayer` 已正确区分 Input Authority 请求与 State Authority 写入，用于昵称同步；本计划不改变其职责。
- 现有 EditMode 测试程序集只引用 Lobby Core，尚不能直接承载新的玩家移动纯规则测试。

## 根因分析

### 1. 输入与网络 Tick 脱节

`PlayerMovement.FixedUpdateNetwork` 直接读取 `Input.GetAxis`。这绕过 Fusion 输入缓冲，使输入没有明确的 Tick 所属、丢包重放语义或统一采样入口。代理对象虽然被 Authority 判断挡住，但组件本身仍把设备输入和网络模拟绑在一起。

### 2. 权限语义混用

当前 Shared Mode 下本地角色通常同时持有 Input Authority 与 State Authority，因此 `HasStateAuthority` 暂时能驱动本地角色。但这只是当前生成策略的结果，不应代替职责语义：设备采集和摄像机属于 Input Authority，网络状态写入属于 State Authority。

### 3. 跳跃生命周期错误

当前 Tick 中先在 `isJumpPress && isGrounded` 分支设置 `isJumping = true`，随后又执行 `if (isJumping && isGrounded)`，可能在同一 Tick 立即清除跳跃锁和 Animator 的 Jump 状态。`isJumpPress` 又在 Tick 尾部无条件清除，渲染帧与网络 Tick 的先后会影响结果。

### 4. 动画来源重复且不稳定

`Grounded` 和 `FreeFall` 在同一 Tick 被写入两次；`Jump`、`Speed` 与移动计算混杂。Animator 既像状态存储，又像表现输出，难以判断代理端展示的是输入意图还是移动结果。

### 5. 配置与依赖暴露不规范

速度、重力、跳跃力、动画映射值均为公开字段；组件依赖在运行时临时查找，缺少启动时验证。字段命名不符合私有序列化字段惯例，也不利于建立稳定配置契约。

## 推荐架构

```text
Legacy Unity Input
        |
        v
LegacyPlayerInputSource          仅本地设备采样
        |
        v
FusionPlayerInputCallbacks       OnInput 提交当前 PlayerInputFrame
        |
        v
PlayerInputFrame                 Fusion INetworkInput DTO
        |
        v
FusionPlayerMotor                FixedUpdateNetwork + GetInput<T>
        |
        +--> MovementRules       纯计算与状态迁移
        |
        +--> CharacterController.Move
        |
        +--> PlayerMovementState 权威移动结果
        |
        v
FusionPlayerAnimationPresenter   Animator/NetworkMecanimAnimator 表现

HasInputAuthority --> PlayerCameraBinder --> Main Camera
```

## 目标目录与文件职责

```text
Assets/FireBase+Photon/Scripts/Player/
  Input/
    PlayerInputFrame.cs
    IPlayerInputSource.cs
    LegacyPlayerInputSource.cs
    FusionPlayerInputCallbacks.cs
  Movement/
    Core/
      ARPG.Player.Movement.Core.asmdef
      PlayerMovementConfigData.cs
      PlayerMovementState.cs
      MovementRules.cs
    PlayerMovementConfig.cs
    FusionPlayerMotor.cs
  Animation/
    PlayerAnimationState.cs
    FusionPlayerAnimationPresenter.cs
  Camera/
    PlayerCameraBinder.cs
    FirstPersonCamera.cs
    ThirdPersonCamera.cs
  Network/
    PlayerSpawner.cs
    NetworkPlayer.cs

Assets/FireBase+Photon/Tests/Editor/Player/
  MovementRulesTests.cs
  PlayerInputFrameTests.cs
  PlayerPrefabContractTests.cs
```

### 文件边界

| 类型 | 唯一职责 | 允许依赖 | 禁止事项 |
| --- | --- | --- | --- |
| `PlayerInputFrame` | 表达一个 Fusion Tick 的移动、视角方向和离散按钮 | Fusion 值类型 | 读取 Unity Input、引用场景对象 |
| `IPlayerInputSource` | 定义本地输入快照接口 | `PlayerInputFrame` | 网络、移动、Animator |
| `LegacyPlayerInputSource` | 把 Legacy Input 转为快照并缓存边沿按钮 | Unity Input | 调用 `CharacterController.Move` |
| `FusionPlayerInputCallbacks` | 在 Runner `OnInput` 提交快照 | Fusion、输入源 | 维护角色移动状态 |
| `PlayerMovementConfigData` | 纯规则所需数值 | `System`、Unity 数学值类型 | Unity 对象引用 |
| `PlayerMovementConfig` | Inspector 配置与校验，并导出纯数据 | Unity | 执行移动 |
| `PlayerMovementState` | 保存速度、落地、跳跃 Tick 等运行状态 | 纯值类型 | Animator、Camera |
| `MovementRules` | 纯计算水平速度、重力和跳跃状态迁移 | Core 类型 | `Input`、Fusion、MonoBehaviour |
| `FusionPlayerMotor` | 消费 Tick 输入并驱动 CharacterController/网络状态 | Fusion、Unity、Core | 采集设备输入、绑定摄像机 |
| `PlayerAnimationState` | 表达动画所需的派生结果 | 值类型 | 写 Animator |
| `FusionPlayerAnimationPresenter` | 把移动结果映射到 Animator | Unity、Fusion | 决定游戏状态 |
| `PlayerCameraBinder` | 为本地 Input Authority 绑定/解绑相机目标 | Unity、Fusion | 移动或写网络状态 |
| `PlayerSpawner` | 生成、登记、销毁玩家对象 | Fusion | 输入采集和移动规则 |
| `NetworkPlayer` | 昵称网络状态与世界标签 | Fusion、TMP | 承担移动同步 |

## 核心契约草图

以下代码用于锁定类型和职责，实施时可按 Fusion v1 编译结果调整语法，但不得改变权限边界。

```csharp
namespace ARPG.Player.Input
{
    public enum PlayerInputButton
    {
        Jump,
        Sprint
    }

    public struct PlayerInputFrame : Fusion.INetworkInput
    {
        public Vector2 Move;
        public Vector3 CameraForward;
        public Fusion.NetworkButtons Buttons;
    }
}
```

`CameraForward` 只保留水平投影并在提交前归一化。移动执行端不得访问 `Camera.main`，这样代理、重模拟和测试均不依赖本地相机。

```csharp
public interface IPlayerInputSource
{
    PlayerInputFrame Capture();
    void ConsumeTickButtons();
}
```

`LegacyPlayerInputSource.Update` 负责捕获 `GetButtonDown("Jump")` 这类渲染帧边沿，`Capture` 返回当前连续轴和尚未消费的离散按钮。只有 `OnInput` 成功提交后才调用 `ConsumeTickButtons`，避免一次按键在渲染帧与网络 Tick 错位时丢失。

```csharp
public readonly struct PlayerMovementConfigData
{
    public readonly float RunSpeed;
    public readonly float WalkSpeedMultiplier;
    public readonly float Gravity;
    public readonly float GroundStickVelocity;
    public readonly float JumpSpeed;
    public readonly float RotationSpeed;
}
```

配置保持 YAGNI：只迁移现有行为需要的值。`AnimatorSpeedMax` 属于表现映射，应放在动画 Presenter 配置中，而不是移动 Core。

```csharp
public struct PlayerMovementState
{
    public Vector3 Velocity;
    public bool IsGrounded;
    public int JumpStartedTick;
}
```

`JumpStartedTick` 初始为无效值。起跳 Tick 内不得执行落地清除；只有后续 Tick 经历非落地后再次落地，或满足明确的最小 Tick 条件，才生成 Landing 状态。

## 权限和状态所有权

| 行为 | 权限/时机 | 说明 |
| --- | --- | --- |
| 读取键盘、鼠标 | 本地 Runner 的输入回调链 | 不放在玩家代理组件中 |
| 绑定摄像机 | `HasInputAuthority`，`Spawned/Despawned` | Shared Mode 下不依赖 State Authority |
| 读取 `PlayerInputFrame` | `FixedUpdateNetwork` + `GetInput<T>` | 输入来自对象 Input Authority |
| 执行 `CharacterController.Move` | 有效模拟阶段；写复制状态前检查 State Authority | 具体 Fusion v1 行为在阶段 4 双端验证 |
| 写 `[Networked]` 移动/动画状态 | `HasStateAuthority` | 代理只读取 |
| 写 Animator | Presenter 的 `Render` 或变化回调 | 从移动结果派生 |
| 昵称修改 | 保持现有 `NetworkPlayer` 契约 | 不纳入本重构 |

## 注释与代码规范

- 命名空间统一采用 `ARPG.Player.Input`、`ARPG.Player.Movement`、`ARPG.Player.Animation`、`ARPG.Player.Camera`、`ARPG.Player.Network`。
- MonoBehaviour 字段使用 `[SerializeField] private`，字段名使用 `_camelCase`；只读公开状态使用属性。
- 公共类型、公共接口、公开方法必须有 XML 注释，说明职责、调用方或权限前提。
- 对 `HasInputAuthority`、`HasStateAuthority`、`GetInput<T>`、按钮消费和跳跃 Tick 生命周期写“为什么”注释。
- 不给 `Awake`、简单赋值、空值返回等自解释代码添加逐行注释。
- 依赖在 `Awake`/`Spawned` 一次解析并验证；缺少 `CharacterController`、Animator 或配置时输出包含对象名和组件名的错误，并禁用对应组件。
- 使用 `Animator.StringToHash` 缓存参数哈希，不在每帧传字符串。
- 接口只用于真实替换点：输入源需要接口以便测试和未来输入系统迁移；`MovementRules` 不再额外包装接口。
- `PlayerMovementConfig` 负责 Inspector 友好的校验，Core 使用不可变数据，避免纯规则依赖 `ScriptableObject`。
- 遵循开闭原则的方式是稳定 DTO/规则边界和组合组件，不是为每个方法创建接口或基类。

## 分阶段实施计划

### 阶段 1：固化基线与资源契约

**修改/新增：**

- 新增 `Assets/FireBase+Photon/Tests/Editor/Player/PlayerPrefabContractTests.cs`。
- 修改 `Assets/FireBase+Photon/Tests/Editor/ARPG.Tests.EditMode.asmdef`，加入测试所需的 Fusion/Unity 引用；以 Unity 编译器实际程序集名为准。
- 不修改玩家预制体和运行脚本。

**测试内容：**

- 预制体存在且具有 FusionPrefab 标签。
- 根节点具有 `NetworkObject`、`NetworkTransform`、`NetworkMecanimAnimator`、`CharacterController`、旧 `PlayerMovement` 和 `NetworkPlayer`。
- `NetworkMecanimAnimator` 引用了 Animator。
- 实际 Animator Controller 路径为 Locomotions 下的 `Player.controller`。
- 五个 Animator 参数存在且类型匹配。
- `Play` 场景的 `PlayerSpawner.PlayerPrefab` 指向该预制体。

**验证：**运行 Unity EditMode 全部测试，预期全部通过；记录当前两客户端移动、转向、跳跃、冲刺与动画观察结果，失败项也作为基线保留。

**回滚点：**仅新增测试和测试程序集引用，可单独回退。

### 阶段 2：建立 Fusion 输入采集链

**新增：**

- `Input/PlayerInputFrame.cs`
- `Input/IPlayerInputSource.cs`
- `Input/LegacyPlayerInputSource.cs`
- `Input/FusionPlayerInputCallbacks.cs`
- `Tests/Editor/Player/PlayerInputFrameTests.cs`

**修改：**

- `Networking/Lobby/FusionSessionCoordinator.cs`：创建 Runner 时挂载并初始化唯一的 `FusionPlayerInputCallbacks`；不得新建第二个 Runner 回调宿主。

**实施步骤：**

1. 定义 Move、CameraForward、Jump、Sprint 输入契约。
2. 用可注入/可替换的读取方法测试轴归一化、相机水平投影和按钮锁存。
3. 在 `OnInput` 中调用 `input.Set(frame)`。
4. 保持旧 `PlayerMovement` 继续驱动角色，本阶段只并行提交输入，不消费新输入。

**验证：**Unity 编译通过；EditMode 测试验证按键在多个 `Update` 与单个 `OnInput` 之间只提交一次；双客户端进入房间不改变旧移动行为。

**回滚点：**移除新增回调组件和文件即可回到旧链路。

### 阶段 3：抽离配置、状态和纯移动规则

**新增：**

- `Movement/Core/ARPG.Player.Movement.Core.asmdef`
- `Movement/Core/PlayerMovementConfigData.cs`
- `Movement/Core/PlayerMovementState.cs`
- `Movement/Core/MovementRules.cs`
- `Movement/PlayerMovementConfig.cs`
- `Tests/Editor/Player/MovementRulesTests.cs`

**修改：**

- `ARPG.Tests.EditMode.asmdef` 引用 `ARPG.Player.Movement.Core`。

**必须覆盖的测试：**

- 无输入时水平速度为零，重力继续累计。
- 移动输入长度被限制为 1，斜向移动不加速。
- Walk 使用 `RunSpeed * WalkSpeedMultiplier`，Sprint 使用 `RunSpeed`。
- 落地时垂直速度被限制为 `GroundStickVelocity`，不是持续累积负值。
- 只有落地且 Jump 按钮出现上升沿时才能起跳。
- 起跳 Tick 不产生 Landing。
- 空中重复 Jump 不改变垂直速度。
- 经历空中状态后再次落地时只产生一次 Landing。
- 零/负配置值在 `PlayerMovementConfig.OnValidate` 被限制或报告。

**验证：**Core 测试不加载场景、不创建 GameObject；Unity EditMode 全部通过。

**回滚点：**新 Core 尚未接管运行行为，可独立回退。

### 阶段 4：使用 `FusionPlayerMotor` 接管网络 Tick 移动

**新增：**

- `Movement/FusionPlayerMotor.cs`

**修改：**

- `Player Mixamo.prefab`：增加 `PlayerMovementConfig` 与 `FusionPlayerMotor`，复制现有值：Run 5、Walk multiplier 0.5、Gravity -9.81、Jump speed 5；`GroundStickVelocity` 初始采用 -1。
- `PlayerMovement.cs`：暂时保留但禁用，作为一个阶段内的资源回滚点；禁止两个组件同时调用 `CharacterController.Move`。

**Motor 固定流程：**

1. 在 `Spawned` 验证 `CharacterController` 和配置。
2. 在 `FixedUpdateNetwork` 调用 `GetInput<PlayerInputFrame>(out frame)`。
3. 无输入包时使用零移动、无离散按钮，不读取 Unity Input。
4. 将 CameraForward 转为水平基向量，计算世界移动方向。
5. 调用 `MovementRules` 更新状态。
6. 单次调用 `CharacterController.Move`。
7. 从实际位移、垂直速度和 Grounded 结果生成可复制的移动状态。
8. 只在有移动方向时更新朝向；旋转规则使用配置值，保留当前即时转向可作为首版默认。

**关键限制：**

- Shared Mode 下 State Authority 通常在本机，但实现仍要显式保护网络状态写入。
- 不同时引入 `NetworkCharacterController`，以免改变碰撞和移动栈。
- 在确认 `NetworkTransform` 与 `CharacterController` 双端表现前，不调整插值选项。

**验证：**单客户端功能验收后再进行 ParrelSync 双客户端；确认双方只能控制自己的玩家，代理不读取本机键盘，位置和朝向均可见。

**回滚点：**重新启用旧 `PlayerMovement`、禁用 Motor；不得同时启用。

### 阶段 5：动画改为移动结果派生

**新增：**

- `Animation/PlayerAnimationState.cs`
- `Animation/FusionPlayerAnimationPresenter.cs`

**修改：**

- `Player Mixamo.prefab`：挂载 Presenter，引用模型 Animator 和 Motor。
- `Player.controller`：移除 `Idle Walk Run Blend` 上的 `IdleRootMotionBehaviour`，网络玩家始终禁用 Root Motion，避免模型子节点脱离网络根节点。

**删除：**

- `IdleRootMotionBehaviour.cs` 及其 `.meta`：Animator 位于 `Ch46_nonPBR` 子节点，运行期开启 Root Motion 会形成独立于 `FusionPlayerMotor` 的第二条位移链。

**参数映射：**

- `Speed`：由实际水平速度 / RunSpeed 归一化，再映射到当前 Blend Tree 的 0-15 范围。
- `MotionSpeed`：默认 1；若无真实业务需求，不网络化。
- `Grounded`：来自移动状态；落地 Tick 使用 `CharacterController.Move` 返回的 `CollisionFlags.Below` 立即校正，不等待下一 Tick 的 `isGrounded`。
- `FreeFall`：`!Grounded && VerticalVelocity < 0`。
- `Jump`：仅在起跳和上升阶段保持；下降前释放并交由 `FreeFall` 表现，避免 `JumpStart` 延迟到落地后才退出。

**验证：**资源契约测试检查参数哈希对应的名称和类型；双客户端观察本地与代理的 Idle、Walk、Sprint、JumpStart、FreeFall、JumpLand 一致。

**回滚点：**禁用 Presenter 并恢复旧控制器的 Animator 写入；只允许单一 Animator 写入方。

### 阶段 6：独立摄像机绑定与本地权限

**新增：**

- `Camera/PlayerCameraBinder.cs`

**修改：**

- `Player Mixamo.prefab`：挂载 Binder。
- `FirstPersonCamera.cs`、`ThirdPersonCamera.cs`：把 `Target` 改为封装属性或 `Bind/Unbind` 方法，保留现有相机操作行为。

**行为要求：**

- `Spawned` 中只有 `HasInputAuthority` 的玩家查找 `Camera.main` 并绑定。
- `Despawned` 只在目标仍是自己时解绑，避免旧玩家清除新玩家相机目标。
- 找不到 Main Camera 或支持的相机组件时只报告一次明确错误，不影响网络移动。
- Motor 不保存 Camera 引用；输入源只采样相机朝向。

**验证：**两客户端相机各自跟随自己的玩家；加入者不会改变主机相机目标；离房/重新加入后无残留目标。

**回滚点：**恢复旧摄像机绑定前必须先禁用 Binder，保持单一绑定入口。

### 阶段 7：预制体切换与删除旧控制器

**修改：**

- `Player Mixamo.prefab`：确认新输入、Motor、Presenter、Binder 引用完整；删除旧 `PlayerMovement` 组件。
- `PlayerPrefabContractTests.cs`：把“存在旧 PlayerMovement”断言改为“不存在旧组件，且新组件齐全”。
- 根据编译引用更新 `.meta` 和 asmdef。

**删除：**

- `Assets/FireBase+Photon/Scripts/Player/Movement/PlayerMovement.cs`
- 对应 `.meta`

**保持不变：**

- `PlayerSpawner` 的 Shared Mode 生成标志。
- `NetworkPlayer` 的昵称 RPC 和标签逻辑。
- `NetworkTransform`、`NetworkMecanimAnimator` 和 Animator Controller 资产。

**验证：**仓库搜索不存在 `PlayerMovement` 类型和旧字段名引用；Unity 编译、EditMode 测试、资源校验全部通过。

**回滚点：**以阶段 6 提交为完整回滚点；删除旧脚本必须独立提交。

### 阶段 8：最终双客户端验收与文档收口

**自动验证：**

- Unity 编译无错误。
- 全部 EditMode 测试通过。
- 玩家预制体无 Missing Script，必需引用非空。
- `Play` 场景无 Missing Script。
- `rg` 确认玩家移动代码中不存在 `Input.Get*` 和 `Camera.main`。
- `rg` 确认 Animator 参数只由 Presenter 写入。

**双客户端矩阵：**

| 场景 | 主机/先加入者 | 加入者 | 代理观察 | 通过标准 |
| --- | --- | --- | --- | --- |
| 生成 | 生成一个本地玩家 | 生成一个本地玩家 | 双方各见两个玩家 | 无重复对象 |
| 输入隔离 | WASD 只动自己 | WASD 只动自己 | 对方不受本机输入影响 | 权限正确 |
| 转向 | 相机相对方向正确 | 相机相对方向正确 | 朝向连续可见 | 无错误目标 |
| Walk/Sprint | 速度切换正确 | 速度切换正确 | Speed 动画匹配 | 无斜向加速 |
| Jump | 单次按键单次起跳 | 同左 | Jump/FreeFall/Land 顺序一致 | 起跳 Tick 不立刻落地 |
| 空中输入 | 不能重复起跳 | 同左 | 状态无抖动 | 无二段跳 |
| 相机 | 跟随本地玩家 | 跟随本地玩家 | 不绑定代理 | 重进房可恢复 |
| 离开 | 玩家正确销毁 | 玩家正确销毁 | 无残留标签/相机目标 | 无 MissingReference |

**验收记录：**在实施 PR 或提交说明中记录 Unity 版本、Fusion 版本、两客户端方式、测试日期、通过项、已知限制和日志截图路径。

## 文件级修改清单

### 新增文件

- `Assets/FireBase+Photon/Scripts/Player/Input/PlayerInputFrame.cs`
- `Assets/FireBase+Photon/Scripts/Player/Input/IPlayerInputSource.cs`
- `Assets/FireBase+Photon/Scripts/Player/Input/LegacyPlayerInputSource.cs`
- `Assets/FireBase+Photon/Scripts/Player/Input/FusionPlayerInputCallbacks.cs`
- `Assets/FireBase+Photon/Scripts/Player/Movement/Core/ARPG.Player.Movement.Core.asmdef`
- `Assets/FireBase+Photon/Scripts/Player/Movement/Core/PlayerMovementConfigData.cs`
- `Assets/FireBase+Photon/Scripts/Player/Movement/Core/PlayerMovementState.cs`
- `Assets/FireBase+Photon/Scripts/Player/Movement/Core/MovementRules.cs`
- `Assets/FireBase+Photon/Scripts/Player/Movement/PlayerMovementConfig.cs`
- `Assets/FireBase+Photon/Scripts/Player/Movement/FusionPlayerMotor.cs`
- `Assets/FireBase+Photon/Scripts/Player/Animation/PlayerAnimationState.cs`
- `Assets/FireBase+Photon/Scripts/Player/Animation/FusionPlayerAnimationPresenter.cs`
- `Assets/FireBase+Photon/Scripts/Player/Camera/PlayerCameraBinder.cs`
- `Assets/FireBase+Photon/Tests/Editor/Player/MovementRulesTests.cs`
- `Assets/FireBase+Photon/Tests/Editor/Player/PlayerInputFrameTests.cs`
- `Assets/FireBase+Photon/Tests/Editor/Player/PlayerPrefabContractTests.cs`

### 修改文件

- `Assets/FireBase+Photon/Scripts/Networking/Lobby/FusionSessionCoordinator.cs`
- `Assets/FireBase+Photon/Scripts/Player/Camera/FirstPersonCamera.cs`
- `Assets/FireBase+Photon/Scripts/Player/Camera/ThirdPersonCamera.cs`
- `Assets/FireBase+Photon/Mixamo/Animation/Animation/Locomotions/Player.controller`
- `Assets/FireBase+Photon/Scripts/Player/Network/PlayerSpawner.cs`：仅在新组件初始化或错误报告确有需要时修改。
- `Assets/FireBase+Photon/Tests/Editor/ARPG.Tests.EditMode.asmdef`
- `Assets/FireBase+Photon/Prefab/Player Mixamo.prefab`

### 删除文件

- `Assets/FireBase+Photon/Scripts/Player/Animation/IdleRootMotionBehaviour.cs`
- `Assets/FireBase+Photon/Scripts/Player/Animation/IdleRootMotionBehaviour.cs.meta`

### 不修改文件

- `Assets/FireBase+Photon/Scripts/Player/Network/NetworkPlayer.cs`
- `Assets/Photon/Fusion/Resources/NetworkProjectConfig.fusion`
- Firebase、认证、大厅 UI 和房间规则代码。

## 提交与回滚策略

建议每阶段至少一个独立提交：

1. `test: lock player prefab and animator contracts`
2. `feat: add fusion player input pipeline`
3. `refactor: extract player movement rules`
4. `refactor: move player motion to fusion ticks`
5. `refactor: derive player animation from movement state`
6. `refactor: isolate local player camera binding`
7. `refactor: remove legacy player movement controller`
8. `test: verify shared mode player synchronization`

阶段 4、5、6 的预制体切换必须与对应脚本在同一提交中，避免拉取中间提交后出现 Missing Script 或空引用。任何阶段失败时回退整个阶段提交，不在新旧控制链同时启用的状态下继续调试。

## 主要风险与控制措施

| 风险 | 影响 | 控制措施 |
| --- | --- | --- |
| Fusion v1 输入 API 与预想签名不同 | 编译阻断 | 以本地 `Fusion.Runtime.xml` 和实际编译为准，不套用 Fusion 2 示例 |
| Shared Mode 的 State Authority 行为被错误泛化 | 代理或重模拟异常 | 明确 Input/State 职责并做两客户端验证 |
| 渲染帧按键边沿在 Tick 之间丢失 | 跳跃偶发无响应 | 输入源锁存离散按钮，提交后消费 |
| Motor 与旧控制器同时移动 | 速度翻倍、碰撞异常 | 预制体契约测试确保任一阶段只有一个 Move 写入方 |
| Animator 有多个写入方 | 状态抖动 | 阶段 5 后只允许 Presenter 写参数 |
| 修改了错误的 Player.controller | 动画资源损坏 | 测试锁定预制体实际 Controller 路径 |
| 纯 Core 引用场景对象或 Fusion | 测试价值下降 | 独立 asmdef；允许 `Vector2/Vector3` 等 Unity 数学值类型，禁止 `UnityEngine.Object`、`MonoBehaviour` 和 Fusion 类型 |
| 接口和类型数量过多 | 可读性下降 | 只保留输入源接口；规则用静态/无状态服务，配置用数据对象 |
| Root Motion 与 CharacterController 冲突 | 模型子节点与网络根节点分离 | 网络玩家始终关闭 Root Motion，并用预制体契约测试禁止状态行为重新开启 |

## 完成定义

只有同时满足以下条件，才可宣布重构完成：

- 旧 `PlayerMovement.cs` 已删除且无引用。
- 玩家运行代码只通过 Fusion 输入链消费设备意图。
- Motor、Presenter、Binder 职责互不越界。
- 纯移动规则测试、预制体契约测试和现有 EditMode 测试全部通过。
- Unity 编译无错误，玩家预制体和 `Play` 场景无 Missing Script。
- 双客户端验收矩阵全部通过，或失败项被明确记录为阻断问题而非“已完成”。
- 文档、类型名、文件路径与最终代码一致。

## 面试讲解提纲

1. **问题识别：**原 `PlayerMovement` 是 God Object，并直接在网络 Tick 中读取本地 Input，权限和时序边界不清晰。
2. **设计选择：**没有重写网络栈，而是保留 Shared Mode 与现有预制体，按输入、规则、执行、表现、摄像机渐进拆分。
3. **网络原则：**Input Authority 表达意图，State Authority 发布状态，代理只渲染；`OnInput -> INetworkInput -> GetInput<T> -> FixedUpdateNetwork` 保证 Tick 归属。
4. **可测试性：**把重力、速度、跳跃生命周期抽成纯规则，Unity 场景只负责适配和执行。
5. **稳定迁移：**每阶段保持可运行，旧链只在切换阶段禁用，最终彻底删除，避免长期双实现。
6. **工程规范：**接口用于真实替换点，配置集中，公共契约有 XML 注释，复杂 Authority/Tick 代码解释原因，避免注释噪声和过度设计。
