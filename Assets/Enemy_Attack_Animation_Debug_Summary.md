# 敌人攻击动画卡帧问题总结

## 1. 问题描述

敌人追击玩家进入攻击范围后，攻击动画有时会停留在某一帧，表现为敌人长期处于攻击姿态，无法正常回到追击或待机状态。

涉及文件：

- `Assets/Scripts/Enemy_Movement.cs`
- `Assets/Attacking.anim`
- Unity Animator Controller：`Assets/Animation/Enemy/Enemy.controller`

## 2. Unity MCP 检测结果

Unity 版本：`2022.3.62f3c1`

运行时检测到：

- Enemy 的 `Animator` 已启用，且 `CullingMode` 为 `Always Animate`，不是被裁剪导致动画停止。
- Enemy 状态为 `Attacking`。
- `isAttacking = true`，速度为 `0`。
- 当前播放片段为 `Attacking`。
- 攻击动画长度约为 `0.6167s`，不是循环动画。
- 卡住时 `normalizedTime` 曾达到 `4.55` 以上，说明非循环动画已经播放结束，并停留在最后一帧。
- 攻击动画事件包括：
  - `0.3s`：`Attack`
  - `0.5833s`：原先为 `SetEnemyState`
- Console 中没有脚本异常，但可以看到 `Attack` 事件被重复触发。

## 3. 原始代码问题

### 3.1 `OnTriggerStay2D` 每帧覆盖攻击状态

原代码在玩家处于检测触发器内时，无条件执行：

```csharp
SetEnemyState(EnemyState.Chasing);
```

`OnTriggerStay2D` 会持续执行，而检测触发器的范围大于实际攻击范围。因此敌人已经处于 `Attacking` 时，仍会被强制切回 `Chasing`。

之后 `Update` 中的追击逻辑又发现玩家在攻击范围内，再次切回 `Attacking`：

```text
Attacking -> Chasing -> Attacking -> Chasing ...
```

这会不断修改 Animator 的布尔参数，使攻击动画被反复重置或与动画事件产生竞争。

### 3.2 进入攻击状态后没有立即结束追击逻辑

原来的 `ChasePlayer()` 在进入攻击状态后仍会继续设置速度：

```csharp
if (distance <= attackRange)
{
    SetEnemyState(EnemyState.Attacking);
}

rb.velocity = direction * speed;
```

虽然下一帧会停止速度，但状态切换和移动写入发生在同一次调用中，容易造成边界状态不清晰。

### 3.3 非循环动画依赖动画事件结束状态

攻击片段不是循环动画。如果 `isAttacking` 一直保持 `true`，Animator 会继续停留在攻击状态，动画播放完后自然显示最后一帧。

原动画事件中确实可以在编辑器里选择 `EnemyState.Idle`。Unity 会将枚举底层保存为整数，例如 `Idle` 对应 `intParameter = 0`。因此，枚举出现在事件面板并不代表配置错误。

但是，把完整状态切换直接绑定到动画事件存在两个风险：

- 动画资源或 Animator Controller 发生重载时，事件可能与当前内存中的资源状态不一致。
- 动画事件负责决定固定状态，而不是只通知“攻击动作结束”，容易与触发器和追击逻辑互相覆盖。

## 4. 本次修复内容

### 4.1 防止触发器覆盖攻击状态

现在只有敌人处于 `Idle` 时，`OnTriggerStay2D` 才会进入 `Chasing`：

```csharp
if (currentState == EnemyState.Idle)
{
    SetEnemyState(EnemyState.Chasing);
}
```

攻击状态不会再被每个物理帧强制改写。

### 4.2 进入攻击后立即停止追击

当玩家进入攻击范围时，先停止刚体速度，然后直接返回：

```csharp
if (Vector2.Distance(transform.position, player.position) <= attackRange)
{
    rb.velocity = Vector2.zero;
    SetEnemyState(EnemyState.Attacking);
    return;
}
```

### 4.3 增加明确的攻击结束入口

动画事件改为调用无参函数：

```csharp
public void EndAttack()
```

无参函数并不是因为枚举参数一定不能使用，而是让动画事件只承担“攻击结束通知”的职责。函数内部根据玩家是否存在以及当前距离决定进入待机、追击或下一轮攻击。

### 4.4 增加 Animator 播放时间兜底

为了防止动画事件因资源重载或 Animator 状态异常而丢失，攻击状态中增加了播放时间检查：

```csharp
if (animator.GetCurrentAnimatorStateInfo(0).IsName("Attacking")
    && animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1f)
{
    EndAttack();
}
```

这样即使末尾动画事件没有执行，攻击状态也不会永久停留在最后一帧。

### 4.5 通过追击状态重新进入下一次攻击

攻击结束且玩家仍在攻击范围时，先切换到 `Chasing`，由下一帧追击逻辑重新进入 `Attacking`。

这样可以确保非循环攻击片段重新从头播放，而不是在同一个 Animator 状态中重复设置相同的布尔值。

## 5. 验证结果

通过 Unity MCP 重新运行场景后：

- Enemy 仍然可以进入 `Attacking`。
- 攻击动画播放一轮后会重新从开头播放。
- `normalizedTime` 会回到接近 `0`，不再持续增长并停在末帧。
- `Attack` 事件按攻击周期重复触发。
- Unity 脚本编译通过，无警告。
- Console 无错误和警告。
- `Assets/Attacking.anim` 的末尾事件已保存为 `EndAttack`。

## 6. 可复用的状态机设计启发

### 状态只能由一个明确入口负责切换

触发器、Update、动画事件都可能参与状态切换时，应规定优先级，避免多个入口无条件覆盖彼此。

### `OnTriggerStay2D` 不应直接代表追击状态

触发器只能说明玩家在检测范围内，不等于敌人当前必须追击。进入追击前应检查当前状态。

### 攻击范围和检测范围要分开处理

玩家留在检测触发器内但离开攻击范围时，应从攻击回到追击，而不是直接待机或继续攻击。

### 非循环动画必须有可靠的退出机制

攻击、受击、死亡等非循环片段不能只依赖一个动画事件。可以使用动画事件加状态机播放时间检查的双重保障。

### 动画事件适合通知，不适合承载复杂决策

推荐使用类似 `EndAttack()` 的无参入口，再由脚本根据实时状态做决策。这样动画资源不需要知道敌人的完整业务状态，也能减少资源和代码之间的耦合。

### 同一状态的重复写入应谨慎处理

反复执行：

```csharp
SetBool("isAttacking", false);
SetBool("isAttacking", true);
```

可能导致动画重新进入或状态转换竞争。只有在确实需要重播动画时才应该执行这种重置，并最好通过明确的状态转换或 `Animator.Play` 控制。

