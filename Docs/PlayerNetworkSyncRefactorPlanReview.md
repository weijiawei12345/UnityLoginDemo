# 玩家网络同步重构计划审查

## 审查结论

**结论：有条件通过。**

`PlayerNetworkSyncRefactorTechnicalPlan.md` 采用渐进式职责拆分，符合范围 A、Shared Mode 约束和“稳定轻量化”目标。计划能够解决当前输入时机、权限混用、跳跃 Tick 生命周期、Animator 重复写入和 `PlayerMovement` God Object 问题。

实施前必须守住本文列出的阻断条件。尤其不能把 Fusion 2 API、Host/Server 权限模型或新的网络移动组件未经验证地带入 Fusion 1.1.0 项目。

## 审查依据

- `Assets/FireBase+Photon/Scripts/Player/Movement/PlayerMovement.cs`
- `Assets/FireBase+Photon/Scripts/Player/Network/PlayerSpawner.cs`
- `Assets/FireBase+Photon/Scripts/Player/Network/NetworkPlayer.cs`
- `Assets/FireBase+Photon/Scripts/Networking/Lobby/FusionSessionCoordinator.cs`
- `Assets/FireBase+Photon/Prefab/Player Mixamo.prefab`
- `Assets/FireBase+Photon/Scenes/Play.unity`
- `Assets/FireBase+Photon/Mixamo/Animation/Animation/Locomotions/Player.controller`
- `Assets/Photon/Fusion/Assemblies/Fusion.Runtime.xml`
- `Assets/Photon/Fusion/Resources/NetworkProjectConfig.fusion`
- Unity MCP 只读结果：编辑器、场景层级、预制体组件、实际 Animator Controller 与参数。

## 阻断项

### P1-1：必须先用 Fusion v1 实际编译确认输入 DTO 和按钮 API

计划使用 `INetworkInput`、`NetworkInput.Set`、`GetInput<T>` 的方向正确，本地 Fusion v1 XML 也支持该数据流。但 `NetworkButtons` 的具体 API、字段可编织性和程序集引用必须以项目安装的 1.1.0 编译结果为准。

**放行条件：**阶段 2 的最小 DTO、`OnInput` 和 `GetInput<T>` 编译通过，并有 EditMode 测试或最小运行日志证明按钮边沿只消费一次。

### P1-2：任一提交中只能存在一个角色位移写入方

阶段 4 允许短暂保留旧 `PlayerMovement` 作为回滚资源，但不能让它与 `FusionPlayerMotor` 同时启用。否则两个组件都会调用 `CharacterController.Move`，测试结果失真。

**放行条件：**预制体契约测试或编辑器校验明确断言旧控制器与新 Motor 不会同时启用。

### P1-3：任一阶段只能存在一个 Animator 参数写入方

当前 Animator 参数全部由 `PlayerMovement` 写入。阶段 5 引入 Presenter 后，必须在同一资源切换中撤销旧写入，否则 `Grounded`、`FreeFall`、`Jump` 和 `Speed` 会发生竞争。

**放行条件：**仓库搜索和组件检查证明 Presenter 是唯一写入方。

### P1-4：双客户端验收不可由 Unity MCP 资源检查替代

Unity MCP 能证明场景、组件和控制器事实，不能证明 Shared Mode 下两台客户端的 Authority、输入隔离、插值和动画一致性。

**放行条件：**阶段 4 至 8 使用 ParrelSync 或两个独立客户端完成验收矩阵，并保留结果记录。

## 重要审查意见

### P2-1：权限边界正确，但文档不得承诺 Host/Server 级权威性

Shared Mode 中本地生成策略会让玩家通常同时持有 Input Authority 和 State Authority。计划正确地区分了职责，但这不是专用服务器防作弊边界。文档和面试讲解应使用“职责分离、网络状态所有权”，不要表述为“服务端完全验证”。

### P2-2：`PlayerInputFrame` 应携带相机基向量，而不是让 Motor 查找 Camera

这能保证网络 Tick、代理对象和重模拟不依赖本地场景 Camera。建议只提交水平归一化方向；若最终只需要 yaw，可改为量化角度，但本期没有必要提前优化带宽。

### P2-3：离散按钮必须锁存到 Fusion 采样成功

`GetButtonDown` 发生在渲染帧，`OnInput` 发生在网络采样时机。只用一个普通 bool 并在 Tick 尾清除，会重现当前偶发丢输入问题。计划提出“提交后消费”是必要设计，不应在实现中简化掉。

### P2-4：跳跃状态必须由 Tick 生命周期表达

当前代码在同一 Tick 设置 `isJumping` 后立即按 `isGrounded` 清除。使用 `JumpStartedTick` 或等价状态可修复根因。实现测试必须覆盖“起跳 Tick 不落地”和“经历空中后仅落地一次”。

### P2-5：动画应来自移动结果，而不是再次读取输入

`Speed` 应从实际水平速度派生，`Grounded/FreeFall/Jump` 应来自 Motor 状态。Presenter 不得调用 Unity Input，也不得改变 CharacterController 或网络权威状态。

### P2-6：实际 Animator Controller 路径必须锁定

仓库存在两个名为 `Player.controller` 的资产。玩家预制体实际使用 `Mixamo/Animation/Animation/Locomotions/Player.controller`，而 `Charactor/Player.controller` 无预期参数。计划已识别此风险，资源契约测试必须在第一阶段落地。

### P2-7：Core asmdef 的收益成立，但边界需保持小

独立 Core 可让移动规则脱离 GameObject 测试，符合可读性和面试讲解目标。Core 只应包含配置数据、运行状态和规则；不要把 Fusion DTO、MonoBehaviour、Animator 或 Camera 放入 Core。

### P2-8：`NetworkPlayer` 不应成为新的玩家聚合对象

`NetworkPlayer` 目前负责昵称网络状态和世界标签。把 Motor、动画、相机入口继续塞入该类会形成新的 God Object，也会扩大昵称功能的变更风险。计划将其列为不修改文件是正确的。

### P2-9：`PlayerSpawner` 修改必须最小化

Spawner 已完成 Shared Mode 本地玩家生成与登记。除非新组件需要明确的初始化次序或错误报告，否则不应把输入回调、移动配置或摄像机逻辑加入 Spawner。

## 过度抽象检查

| 候选抽象 | 结论 | 理由 |
| --- | --- | --- |
| `IPlayerInputSource` | 保留 | 存在 Legacy Input 与测试替身两个真实实现需求，也为未来 Input System 留扩展点 |
| `IMovementRules` | 不引入 | 规则可用静态/无状态类型测试，没有运行时替换需求 |
| `IAnimationPresenter` | 不引入 | 当前只有一个 Animator 实现，没有多态收益 |
| `ICameraBinder` | 不引入 | 当前仅一个本地绑定策略 |
| 通用状态机 | 不引入 | 当前只有移动/跳跃少量状态，显式字段和 Tick 更清晰 |
| 依赖注入容器 | 不引入 | 场景组件数量少，序列化引用和显式初始化足够 |
| ScriptableObject 配置 | 可选但非必需 | 若仅一个玩家配置，MonoBehaviour/序列化配置组件更轻；不要为模式而引入资产管理成本 |

## SOLID 与代码规范审查

- **单一职责：通过。**输入、规则、执行、表现、相机被清楚拆分。
- **开闭原则：通过但需克制。**输入源接口和数据边界允许替换输入实现；不要求所有类接口化。
- **里氏替换：不适用。**计划没有不必要的继承层级。
- **接口隔离：通过。**唯一接口很小，只表达捕获和消费输入。
- **依赖倒置：部分采用且合理。**高层网络回调依赖输入源契约；Unity/Fusion 边界仍由具体适配器承担。
- **封装：需要实施落实。**公开可写字段应改为私有序列化字段，公开状态只读。
- **注释：方向正确。**XML 注释用于公共契约，原因型注释用于 Authority/Tick；禁止逐行翻译代码。
- **命名：通过。**`InputFrame`、`Motor`、`Presenter`、`Binder` 明确体现层次和职责。

## 测试计划审查

计划的测试层次合理：

1. 纯规则 EditMode 测试覆盖速度、重力、跳跃和 Tick 生命周期。
2. 资源契约测试防止预制体组件、引用和 Animator 参数漂移。
3. Unity 编译与场景 Missing Script 校验覆盖装配错误。
4. 双客户端验收覆盖 Authority、输入隔离、同步和表现。

仍需注意：现有 `ARPG.Tests.EditMode.asmdef` 只引用 Lobby Core。实施者必须显式补充玩家 Core 和 Fusion/Unity 测试所需引用，并避免为了测试把全部运行程序集无边界地暴露给 Core。

## 阶段可运行性审查

| 阶段 | 可运行性 | 审查意见 |
| --- | --- | --- |
| 1 基线测试 | 是 | 不改运行代码，风险最低 |
| 2 输入采集链 | 是 | 新输入只提交不消费，不改变旧移动 |
| 3 纯规则 | 是 | 新 Core 未接管运行行为 |
| 4 Motor 接管 | 有条件 | 必须保证旧控制器禁用，先单端再双端 |
| 5 动画 Presenter | 有条件 | 必须保证 Animator 单写入方 |
| 6 Camera Binder | 有条件 | 必须按 Input Authority 绑定并正确解绑 |
| 7 删除旧控制器 | 是 | 前提是 4 至 6 已通过，删除单独提交 |
| 8 最终验收 | 是 | 失败项应阻断完成声明 |

## 模糊点收敛

- “冲刺”在当前代码中实际是按 Shift 后保持 Sprint，停止移动时复位，不是带持续时间、冷却或位移曲线的 Dash。本计划沿用 Sprint 语义，文件和变量不应命名为 Dash。
- “权威移动状态”指 State Authority 写入并由代理读取的状态，不等于专用服务器验证。
- “动画一致性”指同一移动阶段在两端呈现相同参数/状态序列，不要求逐帧完全一致。
- “保持可运行”指每个阶段提交后能编译并进入现有房间流程；阶段 4 至 6 不允许双控制器并行作为长期状态。
- “增强注释”不等于增加注释数量，而是补足公共契约、权限原因和 Tick 生命周期解释。

## 最终批准条件

满足以下条件后，可以开始按计划实施：

- 接受 Fusion v1 本地 API 与实际编译结果优先于外部示例。
- 接受不升级 Input System、不替换 NetworkTransform、不扩大到战斗系统的非目标。
- 接受接口数量保持最小，不为展示设计模式而制造抽象。
- 接受阶段 4、5、6 的单写入方约束。
- 接受最终完成必须包含双客户端验收，Unity MCP 检查不能替代运行验证。

在这些边界下，本计划可执行、可测试、可回滚，且能显著提升文件架构、代码可读性、注释质量和面试讲解清晰度。
