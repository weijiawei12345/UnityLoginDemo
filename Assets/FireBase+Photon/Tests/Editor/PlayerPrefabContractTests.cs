using System;
using Fusion;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ARPG.Tests
{
    public sealed class PlayerPrefabContractTests
    {
        private const string PrefabPath = "Assets/FireBase+Photon/Prefab/Player Mixamo.prefab";

        [Test]
        public void PlayerPrefab_UsesSingleRefactoredControlChain()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Type configType = Type.GetType("ARPG.Player.Movement.PlayerMovementConfig, Assembly-CSharp");
            Type motorType = Type.GetType("ARPG.Player.Movement.FusionPlayerMotor, Assembly-CSharp");
            Type presenterType = Type.GetType("ARPG.Player.Animation.FusionPlayerAnimationPresenter, Assembly-CSharp");
            Type binderType = Type.GetType("ARPG.Player.Camera.PlayerCameraBinder, Assembly-CSharp");
            Type legacyType = Type.GetType("PlayerMovement, Assembly-CSharp");

            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<NetworkObject>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<NetworkTransform>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<NetworkMecanimAnimator>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<CharacterController>(), Is.Not.Null);
            Assert.That(prefab.GetComponent(configType), Is.Not.Null);
            Assert.That(prefab.GetComponent(motorType), Is.Not.Null);
            Assert.That(prefab.GetComponent(presenterType), Is.Not.Null);
            Assert.That(prefab.GetComponent(binderType), Is.Not.Null);
            Assert.That(legacyType == null || prefab.GetComponent(legacyType) == null, Is.True);
        }

        [Test]
        public void PlayerPrefab_ReferencesExpectedAnimatorController()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Animator animator = prefab.GetComponentInChildren<Animator>(true);
            NetworkMecanimAnimator networkAnimator = prefab.GetComponent<NetworkMecanimAnimator>();

            Assert.That(animator, Is.Not.Null);
            Assert.That(networkAnimator.Animator, Is.SameAs(animator));
            Assert.That(
                AssetDatabase.GetAssetPath(animator.runtimeAnimatorController),
                Is.EqualTo("Assets/FireBase+Photon/Mixamo/Animation/Animation/Locomotions/Player.controller"));
        }

        [Test]
        public void PlayerPrefab_KeepsVisualModelOnNetworkRoot()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Animator animator = prefab.GetComponentInChildren<Animator>(true);
            AnimatorController controller = animator.runtimeAnimatorController as AnimatorController;

            Assert.That(animator.transform.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(animator.applyRootMotion, Is.False);
            Assert.That(controller, Is.Not.Null);

            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                foreach (ChildAnimatorState childState in layer.stateMachine.states)
                {
                    Assert.That(
                        Array.Exists(
                            childState.state.behaviours,
                            behaviour => behaviour != null && behaviour.GetType().Name == "IdleRootMotionBehaviour"),
                        Is.False,
                        $"Animator state '{childState.state.name}' must not enable Root Motion on the visual child.");
                }
            }
        }

        [Test]
        public void PlayerController_JumpStartContinuesIntoAirStates()
        {
            const string controllerPath =
                "Assets/FireBase+Photon/Mixamo/Animation/Animation/Locomotions/Player.controller";
            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);

            Assert.That(controller, Is.Not.Null);

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimatorState jumpStart = FindState(stateMachine, "JumpStart");
            AnimatorState inAir = FindState(stateMachine, "InAir");
            AnimatorState jumpLand = FindState(stateMachine, "JumpLand");
            AnimatorState locomotion = FindState(stateMachine, "Idle Walk Run Blend");

            Assert.That(jumpStart, Is.Not.Null);
            Assert.That(inAir, Is.Not.Null);
            Assert.That(jumpLand, Is.Not.Null);
            Assert.That(locomotion, Is.Not.Null);

            Assert.That(jumpStart.transitions, Is.Not.Empty);
            Assert.That(
                Array.TrueForAll(jumpStart.transitions, transition => transition.destinationState == inAir),
                Is.True,
                "JumpStart must continue into InAir, not return to the locomotion blend tree.");
            Assert.That(
                Array.TrueForAll(
                    jumpStart.transitions,
                    transition =>
                        transition.hasFixedDuration
                        && transition.duration >= 0.2f
                        && Mathf.Approximately(transition.offset, 0f)),
                Is.True,
                "JumpStart → InAir must use a fixed blend of at least 0.2s from InAir time 0.");
            Assert.That(
                Array.Exists(inAir.transitions, transition => transition.destinationState == jumpLand),
                Is.True,
                "InAir must land through JumpLand.");
            Assert.That(
                Array.Exists(jumpLand.transitions, transition => transition.destinationState == locomotion),
                Is.True,
                "JumpLand must return to Idle Walk Run Blend after landing.");
        }

        private static AnimatorState FindState(AnimatorStateMachine stateMachine, string stateName)
        {
            foreach (ChildAnimatorState childState in stateMachine.states)
            {
                if (childState.state != null && childState.state.name == stateName)
                {
                    return childState.state;
                }
            }

            return null;
        }
    }
}
