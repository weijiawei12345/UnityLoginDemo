---
status: investigating
trigger: "Investigate and fix the Unity ARPG bug: enemies chasing the player sometimes leave their attack animation frozen on one attack frame."
created: 2026-07-20T00:00:00+08:00
updated: 2026-07-20T00:42:00+08:00
---

## Current Focus

hypothesis: The frozen frame is caused by the enemy state machine repeatedly re-entering Attacking from the aggro trigger, compounded by no explicit attack-range exit; the missing EndAttack receiver is a secondary defect.
test: Apply a minimal state-machine fix: trigger chase only when acquiring a player, guard duplicate state assignments, stop and return immediately on entering attack, explicitly leave attack when out of range, and implement EndAttack for the authored clip event. Then compile and repeat the original chase/attack observation.
expecting: Attack transitions will no longer be reset by OnTriggerStay2D, leaving attack range will restore Chasing/Idle, the EndAttack event will no longer be unresolved, and the Animator normalized time will advance/repeat.
next_action: Edit Assets/Scripts/Enemy_Movement.cs, then let Unity recompile and inspect Console/runtime state.

## Symptoms

expected: Enemy approaches player, attacks, and attack animation completes/repeats normally; leaving attack range returns to chase/idle.
actual: During chase-to-attack behavior, attack animation can become stuck on one frame and remain in attack state.
errors: Unknown; inspect Unity Console and runtime state.
reproduction: Run the current scene, let an enemy chase the player into attack range, observe repeated attack transitions and the Animator when it freezes.
started: Existing issue, current codebase.

## Eliminated

## Evidence

- timestamp: 2026-07-20T00:10:00+08:00
  checked: Assets/Scripts/Enemy_Movement.cs
  found: OnTriggerStay2D always calls SetEnemyState(Chasing), including while Attacking; Update then calls ChasePlayer and changes back to Attacking whenever distance is <= attackRange. SetEnemyState clears the previous bool and sets the new one on every transition.
  implication: A continuous trigger overlap can repeatedly toggle isAttacking off/on and restart the attack transition, which directly explains a frozen attack frame.

- timestamp: 2026-07-20T00:10:00+08:00
  checked: Assets inventory and required debugging guidance
  found: Enemy_Movement.cs is the only enemy state/movement script; relevant assets are Assets/Animation/Enemy/Enemy.controller, Idle.anim, Chasing.anim, and Assets/Attacking.anim. No prior debug knowledge base exists.
  implication: The behavior is concentrated in one script/controller pair; no competing enemy state owner was found in source search.

- timestamp: 2026-07-20T00:18:00+08:00
  checked: Assets/Animation/Enemy/Enemy.controller and Unity MCP controller inspection
  found: The serialized controller file on disk has empty parameter and layer arrays, while the live Unity controller inspection reports Idle, Chasing, and Attacking states plus isIdle/isChasing/isAttacking bools.
  implication: The live editor has unsaved or otherwise non-disk controller state; both representations must be treated as evidence, and the runtime controller is the one relevant to the observed behavior.

- timestamp: 2026-07-20T00:18:00+08:00
  checked: Assets/Attacking.anim and Unity Console logs
  found: Attacking is looped and contains Attack at normalized time 0.3 and EndAttack at 0.5833333; EnemyAtk defines Attack but no EndAttack method. Unity logs show repeated Attack events over multiple play sessions, but no current error was present in the retrieved log list.
  implication: The missing EndAttack receiver is a secondary candidate for an animation-event error, but it cannot explain the state-toggle loop by itself because Enemy_Movement currently has no EndAttack callback or clip-completion logic.

- timestamp: 2026-07-20T00:18:00+08:00
  checked: Live Unity scene hierarchy
  found: SampleScene has one Enemy with EnemyAtk, Enemy_Movement, CircleCollider2D, and Animator components; the player is a separate Player-tagged root.
  implication: The observed chase/attack behavior is owned by the expected script and Animator on the same GameObject, and the CircleCollider2D is the likely OnTriggerStay2D source.

- timestamp: 2026-07-20T00:33:00+08:00
  checked: Clean Play Mode runtime samples
  found: EnemyState is Attacking, isAttacking is true, distance is 1.171 (runtime attackRange 1.2), and Rigidbody2D velocity is zero. Animator normalizedTime stayed exactly 32.9046059 across four samples about 400 ms apart.
  implication: The original freeze is reproducible in the current Unity session and is an Animator progression/state-entry problem, not only a visual rendering issue.

- timestamp: 2026-07-20T00:38:00+08:00
  checked: Animator runtime controls and state data
  found: Animator is enabled with speed 1, Normal update mode, valid Enemy controller, attack state length 0.6166667, and no active transition. Fresh runtime logs did not show an EndAttack receiver error, but the clip still declares that event and no source component implements it.
  implication: Animator-level pausing and an active transition are eliminated as primary causes. The source-level trigger/state loop remains the direct causal mechanism; EndAttack remains a separate correctness issue worth closing in the same targeted change.

## Eliminated

- hypothesis: Animator is externally paused or disabled.
  evidence: Runtime inspection reported enabled=true, speed=1, updateMode=Normal, and no active transition.
  timestamp: 2026-07-20T00:38:00+08:00

- hypothesis: The missing EndAttack receiver alone causes the freeze.
  evidence: No fresh Console error was reported during the clean play observation, and Enemy_Movement has no callback path that could currently drive the state; the source already independently contains an Attacking/Chasing toggle loop.
  timestamp: 2026-07-20T00:38:00+08:00

## Resolution

root_cause: OnTriggerStay2D unconditionally changes Attacking back to Chasing on every physics tick while the player remains in the large aggro trigger; Update then changes it back to Attacking when within attackRange. SetEnemyState rewrites Animator bools on every toggle, repeatedly re-entering/resetting the attack animation. Attacking also lacks an explicit out-of-range transition, and the authored EndAttack event has no receiver.
fix:
verification:
files_changed: []
