using DG.Tweening;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace GameUI.TextFx.Editor
{
    /// <summary>
    /// 可视化编辑 TMP 文字动效参数，并在 Scene 中预览。
    /// 菜单：Tools / TextFx / Effect Preview
    /// </summary>
    public sealed class TMPTextEffectPreviewWindow : EditorWindow
    {
        private const string PreviewRootName = "[TextFxPreview]";

        private TMPTextEffectPresetAsset _presetAsset;
        private TMPTextEffectPreset _draft = TMPTextEffectPreset.DefaultBounceJump("Loading");
        private TMPTextAnimator _targetAnimator;
        private Vector2 _scroll;
        private bool _loopPreview = true;
        private bool _isPreviewing;
        private double _lastEditorTime;
        private SerializedObject _assetSerialized;
        private SerializedProperty _presetProperty;

        [MenuItem("Tools/TextFx/Effect Preview")]
        public static void Open()
        {
            TMPTextEffectPreviewWindow window = GetWindow<TMPTextEffectPreviewWindow>();
            window.titleContent = new GUIContent("TextFx Preview");
            window.minSize = new Vector2(420f, 520f);
            window.Show();
        }

        private void OnEnable()
        {
            _lastEditorTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += OnEditorUpdate;
            Selection.selectionChanged += OnSelectionChanged;
            OnSelectionChanged();
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            Selection.selectionChanged -= OnSelectionChanged;
            StopPreviewInternal();
        }

        private void OnSelectionChanged()
        {
            if (Selection.activeGameObject == null)
            {
                return;
            }

            TMPTextAnimator animator = Selection.activeGameObject.GetComponent<TMPTextAnimator>();
            if (animator == null)
            {
                TMP_Text tmp = Selection.activeGameObject.GetComponent<TMP_Text>();
                if (tmp != null)
                {
                    animator = tmp.GetComponent<TMPTextAnimator>() ?? tmp.gameObject.AddComponent<TMPTextAnimator>();
                }
            }

            if (animator != null)
            {
                _targetAnimator = animator;
                Repaint();
            }
        }

        private void OnEditorUpdate()
        {
            if (!_isPreviewing || Application.isPlaying)
            {
                _lastEditorTime = EditorApplication.timeSinceStartup;
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            float dt = Mathf.Max(0f, (float)(now - _lastEditorTime));
            _lastEditorTime = now;
            if (dt > 0f)
            {
                DOTween.ManualUpdate(dt, dt);
                SceneView.RepaintAll();
                Repaint();
            }

            if (_targetAnimator != null && !_targetAnimator.IsPlaying)
            {
                _isPreviewing = false;
            }
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawTargetSection();
            EditorGUILayout.Space(8f);
            DrawPresetSourceSection();
            EditorGUILayout.Space(8f);
            DrawPresetFields();
            EditorGUILayout.Space(8f);
            DrawPreviewControls();
            EditorGUILayout.Space(8f);
            DrawHelp();

            EditorGUILayout.EndScrollView();
        }

        private void DrawTargetSection()
        {
            EditorGUILayout.LabelField("预览目标", EditorStyles.boldLabel);
            _targetAnimator = (TMPTextAnimator)EditorGUILayout.ObjectField(
                "TMPTextAnimator",
                _targetAnimator,
                typeof(TMPTextAnimator),
                true);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("使用当前选中"))
                {
                    OnSelectionChanged();
                }

                if (GUILayout.Button("创建预览对象"))
                {
                    CreatePreviewObject();
                }
            }
        }

        private void DrawPresetSourceSection()
        {
            EditorGUILayout.LabelField("预设来源", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            _presetAsset = (TMPTextEffectPresetAsset)EditorGUILayout.ObjectField(
                "Preset Asset",
                _presetAsset,
                typeof(TMPTextEffectPresetAsset),
                false);
            if (EditorGUI.EndChangeCheck())
            {
                BindAssetSerialized();
                if (_presetAsset != null && _presetAsset.Preset != null)
                {
                    _draft = _presetAsset.Preset.Clone();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("新建 Asset"))
                {
                    CreatePresetAsset();
                }

                if (GUILayout.Button("从 Asset 加载") && _presetAsset != null)
                {
                    _draft = _presetAsset.CreateRuntimeCopy();
                    GUI.FocusControl(null);
                }

                if (GUILayout.Button("保存到 Asset") && _presetAsset != null)
                {
                    Undo.RecordObject(_presetAsset, "Save TextFx Preset");
                    _presetAsset.Preset = _draft.Clone();
                    EditorUtility.SetDirty(_presetAsset);
                    AssetDatabase.SaveAssets();
                }
            }

            EditorGUILayout.LabelField("快速模板", EditorStyles.miniBoldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("跳跃"))
                {
                    _draft = TMPTextEffectPreset.DefaultBounceJump(
                        string.IsNullOrEmpty(_draft.DisplayText) ? "Loading" : _draft.DisplayText);
                }

                if (GUILayout.Button("打字机"))
                {
                    _draft = TMPTextEffectPreset.DefaultTypewriter(
                        string.IsNullOrEmpty(_draft.DisplayText) ? "Hello" : _draft.DisplayText);
                }

                if (GUILayout.Button("波浪"))
                {
                    _draft = TMPTextEffectPreset.DefaultWave(
                        string.IsNullOrEmpty(_draft.DisplayText) ? "Wave" : _draft.DisplayText);
                }

                if (GUILayout.Button("省略号"))
                {
                    _draft = TMPTextEffectPreset.DefaultLoadingDots(
                        string.IsNullOrEmpty(_draft.DotsBaseText) ? "Loading" : _draft.DotsBaseText);
                }
            }
        }

        private void DrawPresetFields()
        {
            EditorGUILayout.LabelField("动效参数", EditorStyles.boldLabel);
            if (_draft == null)
            {
                _draft = TMPTextEffectPreset.DefaultBounceJump("Loading");
            }

            _draft.EffectType = (TMPTextEffectType)EditorGUILayout.EnumPopup("效果类型", _draft.EffectType);
            _draft.DisplayText = EditorGUILayout.TextField("显示文本", _draft.DisplayText ?? string.Empty);

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("时间", EditorStyles.miniBoldLabel);
            _draft.Duration = EditorGUILayout.Slider("Duration", _draft.Duration, 0.05f, 2f);
            _draft.CharDelay = EditorGUILayout.Slider("Char Delay", _draft.CharDelay, 0f, 0.5f);
            _draft.FadeInDuration = EditorGUILayout.Slider("Fade In", _draft.FadeInDuration, 0f, 2f);
            _draft.FadeOutDuration = EditorGUILayout.Slider("Fade Out", _draft.FadeOutDuration, 0f, 2f);
            _draft.DotsInterval = EditorGUILayout.Slider("Dots Interval", _draft.DotsInterval, 0.05f, 1f);
            _draft.IgnoreTimeScale = EditorGUILayout.Toggle("Ignore Time Scale", _draft.IgnoreTimeScale);

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("运动", EditorStyles.miniBoldLabel);
            _draft.JumpHeight = EditorGUILayout.Slider("Jump Height", _draft.JumpHeight, 0f, 80f);
            _draft.WaveAmplitude = EditorGUILayout.Slider("Wave Amplitude", _draft.WaveAmplitude, 0f, 60f);
            _draft.WaveFrequency = EditorGUILayout.Slider("Wave Frequency", _draft.WaveFrequency, 0.1f, 8f);
            _draft.UseAnimationCurve = EditorGUILayout.Toggle("Use Animation Curve", _draft.UseAnimationCurve);
            if (_draft.UseAnimationCurve)
            {
                _draft.MoveCurve = EditorGUILayout.CurveField("Move Curve", _draft.MoveCurve ?? AnimationCurve.Linear(0f, 0f, 1f, 1f));
                _draft.FadeCurve = EditorGUILayout.CurveField("Fade Curve", _draft.FadeCurve ?? AnimationCurve.Linear(0f, 0f, 1f, 1f));
            }
            else
            {
                _draft.MoveEase = (Ease)EditorGUILayout.EnumPopup("Move Ease", _draft.MoveEase);
                _draft.FadeEase = (Ease)EditorGUILayout.EnumPopup("Fade Ease", _draft.FadeEase);
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("颜色 / 呼吸", EditorStyles.miniBoldLabel);
            _draft.ChangeColor = EditorGUILayout.Toggle("Change Color", _draft.ChangeColor);
            _draft.EndColor = EditorGUILayout.ColorField("End Color", _draft.EndColor);
            _draft.PulseMinAlpha = EditorGUILayout.Slider("Pulse Min Alpha", _draft.PulseMinAlpha, 0f, 1f);
            _draft.PulseDuration = EditorGUILayout.Slider("Pulse Duration", _draft.PulseDuration, 0.1f, 2f);
            _draft.DotsBaseText = EditorGUILayout.TextField("Dots Base Text", _draft.DotsBaseText ?? "Loading");

            if (_presetAsset != null && _assetSerialized != null && _presetProperty != null)
            {
                EditorGUILayout.HelpBox("当前也可在下方同步编辑 Asset 序列化字段。", MessageType.None);
            }
        }

        private void DrawPreviewControls()
        {
            EditorGUILayout.LabelField("预览", EditorStyles.boldLabel);
            _loopPreview = EditorGUILayout.Toggle("循环播放", _loopPreview);

            using (new EditorGUI.DisabledScope(_targetAnimator == null || _draft == null))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Play", GUILayout.Height(28f)))
                    {
                        StartPreview(loop: false);
                    }

                    if (GUILayout.Button("Play Loop", GUILayout.Height(28f)))
                    {
                        StartPreview(loop: true);
                    }

                    if (GUILayout.Button("Stop", GUILayout.Height(28f)))
                    {
                        StopPreviewInternal();
                    }
                }

                if (GUILayout.Button("应用到目标 Default Preset"))
                {
                    ApplyDraftToTargetDefault();
                }
            }

            string state = _isPreviewing || (_targetAnimator != null && _targetAnimator.IsPlaying)
                ? "Playing..."
                : "Idle";
            EditorGUILayout.LabelField("状态", state);
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Edit Mode 预览使用 DOTween ManualUpdate，可在 Game/Scene 视图中直接观看。", MessageType.Info);
            }
        }

        private void DrawHelp()
        {
            EditorGUILayout.HelpBox(
                "用法：\n" +
                "1. 选中场景中的 TMP 文本（自动挂 TMPTextAnimator）或点「创建预览对象」\n" +
                "2. 调参数 / 选模板 / 保存为 Preset Asset\n" +
                "3. Play / Play Loop 预览，Stop 停止\n" +
                "4. 「应用到目标 Default Preset」会写入组件默认预设（可勾选 Play On Enable）\n\n" +
                "给 Loading 页使用：\n" +
                "A. 将调好的预设保存为 Asset\n" +
                "B. 放到 Resources/TextFx/LoadingOverlayEffect.asset（自动加载）\n" +
                "   或挂到场景 Canvas 上的 LoadingOverlayView → Effect Preset Asset 槽位",
                MessageType.None);
        }

        private void StartPreview(bool loop)
        {
            if (_targetAnimator == null || _draft == null)
            {
                EditorUtility.DisplayDialog("TextFx", "请先指定 TMPTextAnimator 预览目标。", "OK");
                return;
            }

            if (!Application.isPlaying)
            {
                DOTween.Init(false, true, LogBehaviour.ErrorsOnly);
                TMPTextEffectEditorPreviewDriver.BeginPreview();
            }

            _loopPreview = loop;
            _targetAnimator.PlayPreview(_draft.Clone(), loop);
            _isPreviewing = true;
            _lastEditorTime = EditorApplication.timeSinceStartup;
            EditorUtility.SetDirty(_targetAnimator);
            SceneView.RepaintAll();
        }

        private void StopPreviewInternal()
        {
            if (_isPreviewing && !Application.isPlaying)
            {
                TMPTextEffectEditorPreviewDriver.EndPreview();
            }

            if (_targetAnimator != null)
            {
                _targetAnimator.Stop();
                EditorUtility.SetDirty(_targetAnimator);
            }

            _isPreviewing = false;
            SceneView.RepaintAll();
        }

        private void ApplyDraftToTargetDefault()
        {
            if (_targetAnimator == null || _draft == null)
            {
                return;
            }

            Undo.RecordObject(_targetAnimator, "Apply TextFx Default Preset");
            SerializedObject so = new SerializedObject(_targetAnimator);
            SerializedProperty presetProp = so.FindProperty("_defaultPreset");
            if (presetProp != null)
            {
                CopyPresetToProperty(presetProp, _draft);
                so.ApplyModifiedProperties();
            }
            else
            {
                _targetAnimator.SetDefaultPreset(_draft);
            }

            EditorUtility.SetDirty(_targetAnimator);
        }

        private static void CopyPresetToProperty(SerializedProperty root, TMPTextEffectPreset src)
        {
            if (root == null || src == null)
            {
                return;
            }

            SetEnum(root, "EffectType", (int)src.EffectType);
            SetString(root, "DisplayText", src.DisplayText);
            SetFloat(root, "Duration", src.Duration);
            SetFloat(root, "CharDelay", src.CharDelay);
            SetFloat(root, "FadeInDuration", src.FadeInDuration);
            SetFloat(root, "FadeOutDuration", src.FadeOutDuration);
            SetFloat(root, "DotsInterval", src.DotsInterval);
            SetBool(root, "IgnoreTimeScale", src.IgnoreTimeScale);
            SetFloat(root, "JumpHeight", src.JumpHeight);
            SetFloat(root, "WaveAmplitude", src.WaveAmplitude);
            SetFloat(root, "WaveFrequency", src.WaveFrequency);
            SetBool(root, "UseAnimationCurve", src.UseAnimationCurve);
            SetEnum(root, "MoveEase", (int)src.MoveEase);
            SetEnum(root, "FadeEase", (int)src.FadeEase);
            SetBool(root, "ChangeColor", src.ChangeColor);
            SetColor(root, "EndColor", src.EndColor);
            SetFloat(root, "PulseMinAlpha", src.PulseMinAlpha);
            SetFloat(root, "PulseDuration", src.PulseDuration);
            SetString(root, "DotsBaseText", src.DotsBaseText);

            SerializedProperty moveCurve = root.FindPropertyRelative("MoveCurve");
            if (moveCurve != null)
            {
                moveCurve.animationCurveValue = src.MoveCurve;
            }

            SerializedProperty fadeCurve = root.FindPropertyRelative("FadeCurve");
            if (fadeCurve != null)
            {
                fadeCurve.animationCurveValue = src.FadeCurve;
            }
        }

        private static void SetEnum(SerializedProperty root, string name, int value)
        {
            SerializedProperty p = root.FindPropertyRelative(name);
            if (p != null)
            {
                p.enumValueIndex = value;
            }
        }

        private static void SetString(SerializedProperty root, string name, string value)
        {
            SerializedProperty p = root.FindPropertyRelative(name);
            if (p != null)
            {
                p.stringValue = value ?? string.Empty;
            }
        }

        private static void SetFloat(SerializedProperty root, string name, float value)
        {
            SerializedProperty p = root.FindPropertyRelative(name);
            if (p != null)
            {
                p.floatValue = value;
            }
        }

        private static void SetBool(SerializedProperty root, string name, bool value)
        {
            SerializedProperty p = root.FindPropertyRelative(name);
            if (p != null)
            {
                p.boolValue = value;
            }
        }

        private static void SetColor(SerializedProperty root, string name, Color value)
        {
            SerializedProperty p = root.FindPropertyRelative(name);
            if (p != null)
            {
                p.colorValue = value;
            }
        }

        private void BindAssetSerialized()
        {
            _assetSerialized = _presetAsset != null ? new SerializedObject(_presetAsset) : null;
            _presetProperty = _assetSerialized != null ? _assetSerialized.FindProperty("Preset") : null;
        }

        private void CreatePresetAsset()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create TextFx Preset",
                "TMPTextEffectPreset",
                "asset",
                "选择保存路径");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            TMPTextEffectPresetAsset asset = CreateInstance<TMPTextEffectPresetAsset>();
            asset.Preset = _draft != null ? _draft.Clone() : TMPTextEffectPreset.DefaultBounceJump("Loading");
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            _presetAsset = asset;
            BindAssetSerialized();
            EditorGUIUtility.PingObject(asset);
        }

        private void CreatePreviewObject()
        {
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasGo = new GameObject(
                    "TextFxPreviewCanvas",
                    typeof(Canvas),
                    typeof(CanvasScaler),
                    typeof(GraphicRaycaster));
                canvas = canvasGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                Undo.RegisterCreatedObjectUndo(canvasGo, "Create TextFx Preview Canvas");
            }

            Transform parent = canvas.transform.Find(PreviewRootName);
            GameObject root;
            if (parent == null)
            {
                root = new GameObject(PreviewRootName, typeof(RectTransform));
                root.transform.SetParent(canvas.transform, false);
                Undo.RegisterCreatedObjectUndo(root, "Create TextFx Preview Root");
            }
            else
            {
                root = parent.gameObject;
            }

            GameObject textGo = new GameObject(
                "PreviewText",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            textGo.transform.SetParent(root.transform, false);
            Undo.RegisterCreatedObjectUndo(textGo, "Create TextFx Preview Text");

            RectTransform rect = textGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(700f, 120f);
            rect.anchoredPosition = Vector2.zero;

            TextMeshProUGUI tmp = textGo.GetComponent<TextMeshProUGUI>();
            tmp.font = TMP_Settings.defaultFontAsset;
            tmp.fontSize = 64f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.text = string.IsNullOrEmpty(_draft?.DisplayText) ? "Loading" : _draft.DisplayText;

            _targetAnimator = textGo.AddComponent<TMPTextAnimator>();
            Selection.activeGameObject = textGo;
            EditorGUIUtility.PingObject(textGo);
        }
    }
}
