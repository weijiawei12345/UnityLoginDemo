---
status: investigating
trigger: "Diagnose only why the current Unity enemy attacks once and then remains in Idle instead of returning to Chasing."
created: 2026-07-20T00:00:00+08:00
updated: 2026-07-20T00:03:00+08:00
---

## Current Focus

hypothesis: The attack animation event transitions the enemy to Idle, and because the enemy/player physics pair is sleeping after the attack, no later trigger callback re-enters Chasing.
test: Inspect the Animator controller, scene/prefab Rigidbody2D and trigger configuration, related debug summary, and available Unity runtime inspection tools.
expecting: The controller should show the event-driven Idle transition, while physics configuration/runtime state should explain why the existing OnTriggerStay2D recovery path is not observed after the player stops.
next_action: Read controller and scene configuration, then perform a targeted runtime or static check of each competing transition path.

## Symptoms

expected: After the attack animation ends, the enemy should return to Chasing, then evaluate attack range and cooldown.
actual: Enemy attacks once, then remains Idle even though the player is still within the CircleCollider2D detection area and atkTimer has exceeded atkDelay.
errors: No Unity errors/warnings observed.
reproduction: Let Enemy attack player once, stop moving the player; inspect runtime state.
started: Started after adding attack cooldown.

## Eliminated

## Evidence

- timestamp: 2026-07-20T00:00:00+08:00
  checked: Unity MCP runtime state after the first attack
  found: currentState=Idle, cachedPlayer=true, distance=1.1847, atkTimer=2.3696 with atkDelay=1, Rigidbody2D.IsSleeping()=true, isIdle=true, isChasing=false, isAttacking=false
  implication: Detection and cooldown data remain valid; the failure is in state re-entry or state handling, not loss of the player reference or an unexpired cooldown.

- timestamp: 2026-07-20T00:00:00+08:00
  checked: Attacking.anim event metadata
  found: The animation event at 0.5833 calls SetEnemyState with intParameter 0.
  implication: The attack clip explicitly requests state value 0 at the animation event; the meaning of that value must be verified against the state enum/handler.

- timestamp: 2026-07-20T00:00:00+08:00
  checked: Reported Enemy_Movement behavior
  found: Update handles only Chasing and Attacking; OnTriggerStay2D returns while Attacking and otherwise sets Chasing.
  implication: If the animation event sets Idle after OnTriggerStay2D has already run, no reported Update branch or trigger callback necessarily transitions Idle back to Chasing.

- timestamp: 2026-07-20T00:02:00+08:00
  checked: Enemy_Movement.cs and Attacking.anim
  found: EnemyState is declared in enum order Idle=0, Chasing=1, Attacking=2, Dead=3; the attack clip's only event at 0.5833333 seconds invokes SetEnemyState with intParameter=0.
  implication: The event's value unambiguously maps to Idle at the end of the non-looping attack clip; it is not a request to return to Chasing.

- timestamp: 2026-07-20T00:02:00+08:00
  checked: Enemy_Movement.cs control flow
  found: Update increments atkTimer, but only calls ChasePlayer for Chasing and stops velocity for Attacking; Idle has no behavior. OnTriggerStay2D sets Chasing only when a physics trigger stay callback executes and the current state is not Attacking.
  implication: Once the animation event changes state to Idle, elapsed cooldown alone cannot cause a transition. The existing `cachedPlayer` reference is not consulted by Update while Idle.

- timestamp: 2026-07-20T00:03:00+08:00
  checked: Existing active debug session and related project summary
  found: The earlier attack-freeze investigation independently recorded the same enum/event mapping (`Idle` is 0 and the 0.5833-second event was `SetEnemyState`) and identified the current `OnTriggerStay2D` attack guard as a deliberate change to prevent attack restarts.
  implication: The current Idle result is consistent with the post-attack event plus missing Idle recovery, rather than the earlier repeated-Attacking freeze.

## Resolution

root_cause:
fix:
verification:
files_changed: []
