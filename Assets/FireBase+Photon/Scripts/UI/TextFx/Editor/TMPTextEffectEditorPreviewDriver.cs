using DG.Tweening;
using UnityEditor;
using UnityEngine;

namespace GameUI.TextFx.Editor
{
    /// <summary>
    /// 编辑器下驱动 DOTween ManualUpdate，供 Preview Window / Inspector 按钮共用。
    /// </summary>
    [InitializeOnLoad]
    internal static class TMPTextEffectEditorPreviewDriver
    {
        private static int _activePreviewCount;
        private static double _lastTime;

        static TMPTextEffectEditorPreviewDriver()
        {
            EditorApplication.update += Tick;
            _lastTime = EditorApplication.timeSinceStartup;
        }

        public static void BeginPreview()
        {
            if (!Application.isPlaying)
            {
                DOTween.Init(false, true, LogBehaviour.ErrorsOnly);
            }

            _activePreviewCount++;
            _lastTime = EditorApplication.timeSinceStartup;
        }

        public static void EndPreview()
        {
            _activePreviewCount = Mathf.Max(0, _activePreviewCount - 1);
        }

        private static void Tick()
        {
            if (_activePreviewCount <= 0 || Application.isPlaying)
            {
                _lastTime = EditorApplication.timeSinceStartup;
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            float dt = Mathf.Max(0f, (float)(now - _lastTime));
            _lastTime = now;
            if (dt <= 0f)
            {
                return;
            }

            DOTween.ManualUpdate(dt, dt);
            SceneView.RepaintAll();
        }
    }
}
