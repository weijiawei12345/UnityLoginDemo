using UnityEditor;
using UnityEngine;

namespace GameUI.TextFx.Editor
{
    [CustomEditor(typeof(TMPTextAnimator))]
    public sealed class TMPTextAnimatorEditor : UnityEditor.Editor
    {
        private bool _loop = true;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();

            TMPTextAnimator animator = (TMPTextAnimator)target;
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("预览", EditorStyles.boldLabel);
            _loop = EditorGUILayout.Toggle("Loop", _loop);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Open Preview Window"))
                {
                    TMPTextEffectPreviewWindow.Open();
                }

                if (GUILayout.Button("Play Default"))
                {
                    TMPTextEffectPreset preset = animator.GetDefaultPresetCopy();
                    if (preset == null)
                    {
                        preset = TMPTextEffectPreset.DefaultBounceJump(animator.Text != null ? animator.Text.text : "Loading");
                    }

                    if (!Application.isPlaying)
                    {
                        DG.Tweening.DOTween.Init(false, true, DG.Tweening.LogBehaviour.ErrorsOnly);
                        TMPTextEffectEditorPreviewDriver.BeginPreview();
                    }

                    animator.PlayPreview(preset, _loop);
                    EditorUtility.SetDirty(animator);
                }

                if (GUILayout.Button("Stop"))
                {
                    animator.Stop();
                    if (!Application.isPlaying)
                    {
                        TMPTextEffectEditorPreviewDriver.EndPreview();
                    }

                    EditorUtility.SetDirty(animator);
                }
            }

            EditorGUILayout.HelpBox(
                "更完整的可视化调参请打开 Tools → TextFx → Effect Preview。",
                MessageType.Info);
        }
    }
}
