using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public class AnimatedTileGeneratorWindow : EditorWindow
{
    private enum FrameAxis
    {
        Horizontal,
        Vertical
    }

    private struct GridKey : IEquatable<GridKey>
    {
        public readonly int x;
        public readonly int y;

        public GridKey(int x, int y)
        {
            this.x = x;
            this.y = y;
        }

        public bool Equals(GridKey other)
        {
            return x == other.x && y == other.y;
        }

        public override bool Equals(object obj)
        {
            return obj is GridKey && Equals((GridKey)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (x * 397) ^ y;
            }
        }
    }

    private const string DefaultSourcePath = "Assets/Sprites/Tree.png";
    private const string DefaultOutputFolder = "Assets/Sprites/AniTreeTiles";

    [SerializeField] private string sourcePath = DefaultSourcePath;
    [SerializeField] private string outputFolder = DefaultOutputFolder;
    [SerializeField] private string tilePrefix = "Tree";
    [SerializeField] private int cellWidth = 64;
    [SerializeField] private int cellHeight = 64;
    [SerializeField] private int blockWidth = 3;
    [SerializeField] private int blockHeight = 3;
    [SerializeField] private int frameCount = 4;
    [SerializeField] private FrameAxis frameAxis = FrameAxis.Horizontal;
    [SerializeField] private int startColumn;
    [SerializeField] private int startRowFromTop;
    [SerializeField] private bool keepEmptyRects = true;
    [SerializeField] private bool requireAllFrames = true;
    [SerializeField] private float minSpeed = 1f;
    [SerializeField] private float maxSpeed = 1f;
    [SerializeField] private Tile.ColliderType colliderType = Tile.ColliderType.Sprite;

    [MenuItem("Tools/Animated Tiles/Generator")]
    private static void OpenWindow()
    {
        var window = GetWindow<AnimatedTileGeneratorWindow>("Animated Tile Generator");
        window.minSize = new Vector2(440f, 620f);
        window.Show();
    }

    [MenuItem("Tools/Animated Tiles/Generate Using Current Settings")]
    private static void GenerateUsingCurrentSettings()
    {
        var window = GetWindow<AnimatedTileGeneratorWindow>("Animated Tile Generator");
        window.Generate();
    }

    [MenuItem("Tools/Animated Tiles/Generate Default Tree Tiles")]
    private static void GenerateDefaultTreeTiles()
    {
        var window = CreateInstance<AnimatedTileGeneratorWindow>();
        window.Generate();
        DestroyImmediate(window);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Source and Output", EditorStyles.boldLabel);
        DrawPathField("Sprite sheet", ref sourcePath, true);
        DrawPathField("Output folder", ref outputFolder, false);
        tilePrefix = EditorGUILayout.TextField("Tile name prefix", tilePrefix);

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Grid Layout", EditorStyles.boldLabel);
        cellWidth = Mathf.Max(1, EditorGUILayout.IntField("Cell width", cellWidth));
        cellHeight = Mathf.Max(1, EditorGUILayout.IntField("Cell height", cellHeight));
        blockWidth = Mathf.Max(1, EditorGUILayout.IntField("Block width", blockWidth));
        blockHeight = Mathf.Max(1, EditorGUILayout.IntField("Block height", blockHeight));
        frameCount = Mathf.Max(1, EditorGUILayout.IntField("Frame count", frameCount));
        frameAxis = (FrameAxis)EditorGUILayout.EnumPopup("Frame axis", frameAxis);
        startColumn = Mathf.Max(0, EditorGUILayout.IntField("Start column", startColumn));
        startRowFromTop = Mathf.Max(0, EditorGUILayout.IntField("Start row from top", startRowFromTop));

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Animation and Collision", EditorStyles.boldLabel);
        keepEmptyRects = EditorGUILayout.Toggle("Keep empty Rects", keepEmptyRects);
        using (new EditorGUI.DisabledScope(keepEmptyRects))
        {
            requireAllFrames = EditorGUILayout.Toggle("Require all frames", requireAllFrames);
        }
        minSpeed = Mathf.Max(0f, EditorGUILayout.FloatField("Minimum speed", minSpeed));
        maxSpeed = Mathf.Max(minSpeed, EditorGUILayout.FloatField("Maximum speed", maxSpeed));
        colliderType = (Tile.ColliderType)EditorGUILayout.EnumPopup("Tile collider type", colliderType);

        EditorGUILayout.Space(8f);
        EditorGUILayout.HelpBox(
            "The tool matches sprites by their Rect grid coordinates, so it works with both keep-empty and compact Sprite Editor slicing. " +
            "For the current Tree sheet, use a 3x3 block, 4 horizontal frames, start column 0, and start row from top 0.",
            MessageType.Info);

        EditorGUILayout.Space(4f);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Use Selected Texture"))
                UseSelectedTexture();

            if (GUILayout.Button("Generate Animated Tiles", GUILayout.Height(28f)))
                Generate();
        }
    }

    private static void DrawPathField(string label, ref string path, bool requireTexture)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            path = EditorGUILayout.TextField(label, path);
            if (GUILayout.Button("...", GUILayout.Width(28f)))
            {
                var absolute = requireTexture
                    ? EditorUtility.OpenFilePanel(label, Application.dataPath, "png")
                    : EditorUtility.OpenFolderPanel(label, Application.dataPath, string.Empty);
                if (!string.IsNullOrEmpty(absolute) && absolute.StartsWith(Application.dataPath, StringComparison.OrdinalIgnoreCase))
                    path = "Assets" + absolute.Substring(Application.dataPath.Length).Replace('\\', '/');
            }
        }
    }

    private void UseSelectedTexture()
    {
        var selectedPath = AssetDatabase.GetAssetPath(Selection.activeObject);
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(selectedPath) != null)
            sourcePath = selectedPath;
        else
            ShowNotification(new GUIContent("Select a Texture2D asset first."));
    }

    private void Generate()
    {
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(sourcePath);
        if (texture == null)
        {
            EditorUtility.DisplayDialog("Animated Tile Generator", "Sprite sheet not found: " + sourcePath, "OK");
            return;
        }

        if (!ValidateSettings(texture))
            return;

        EnsureFolder(outputFolder);
        var spritesByGrid = LoadSpritesByGrid(sourcePath);
        var generated = new List<string>();
        var skipped = new List<string>();
        var tileNumber = 1;

        for (var localY = 0; localY < blockHeight; localY++)
        {
            for (var localX = 0; localX < blockWidth; localX++)
            {
                var frames = new Sprite[frameCount];
                var missing = 0;

                for (var frame = 0; frame < frameCount; frame++)
                {
                    var column = startColumn + localX;
                    var rowFromTop = startRowFromTop + localY;
                    if (frameAxis == FrameAxis.Horizontal)
                        column += frame * blockWidth;
                    else
                        rowFromTop += frame * blockHeight;

                    var gridKey = new GridKey(column, TextureRowToGridRow(texture, rowFromTop));
                    if (!spritesByGrid.TryGetValue(gridKey, out frames[frame]))
                        missing++;
                }

                if ((!keepEmptyRects && requireAllFrames && missing > 0) || (!keepEmptyRects && missing == frameCount))
                {
                    skipped.Add("(" + localX + "," + localY + ")");
                    continue;
                }

                var assetPath = outputFolder.TrimEnd('/') + "/" + tilePrefix + tileNumber + ".asset";
                var tile = AssetDatabase.LoadAssetAtPath<AnimatedTile>(assetPath);
                if (tile == null)
                {
                    tile = CreateInstance<AnimatedTile>();
                    AssetDatabase.CreateAsset(tile, assetPath);
                }

                ApplyTileData(tile, frames);
                generated.Add(assetPath);
                tileNumber++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var message = "Generated " + generated.Count + " animated tile(s).";
        if (skipped.Count > 0)
            message += " Skipped " + skipped.Count + " position(s): " + string.Join(", ", skipped.ToArray()) + ".";
        Debug.Log("[AnimatedTileGenerator] " + message);
        ShowNotification(new GUIContent(message));
    }

    private bool ValidateSettings(Texture2D texture)
    {
        if (string.IsNullOrEmpty(outputFolder) || string.IsNullOrEmpty(tilePrefix))
        {
            EditorUtility.DisplayDialog("Animated Tile Generator", "Output folder and tile prefix are required.", "OK");
            return false;
        }

        if (texture.width % cellWidth != 0 || texture.height % cellHeight != 0)
        {
            EditorUtility.DisplayDialog("Animated Tile Generator", "Cell size must divide the texture dimensions exactly.", "OK");
            return false;
        }

        var requiredWidth = blockWidth + (frameAxis == FrameAxis.Horizontal ? (frameCount - 1) * blockWidth : 0);
        var requiredHeight = blockHeight + (frameAxis == FrameAxis.Vertical ? (frameCount - 1) * blockHeight : 0);
        if (startColumn + requiredWidth > texture.width / cellWidth || startRowFromTop + requiredHeight > texture.height / cellHeight)
        {
            EditorUtility.DisplayDialog("Animated Tile Generator", "The selected grid layout exceeds the texture bounds.", "OK");
            return false;
        }

        if (maxSpeed < minSpeed)
            maxSpeed = minSpeed;
        return true;
    }

    private Dictionary<GridKey, Sprite> LoadSpritesByGrid(string path)
    {
        var result = new Dictionary<GridKey, Sprite>();
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            var sprite = asset as Sprite;
            if (sprite == null)
                continue;

            var column = Mathf.RoundToInt(sprite.rect.x / cellWidth);
            var row = Mathf.RoundToInt(sprite.rect.y / cellHeight);
            result[new GridKey(column, row)] = sprite;
        }
        return result;
    }

    private int TextureRowToGridRow(Texture2D texture, int rowFromTop)
    {
        return texture.height / cellHeight - 1 - rowFromTop;
    }

    private void ApplyTileData(AnimatedTile tile, Sprite[] frames)
    {
        var serializedTile = new SerializedObject(tile);
        var animatedSprites = serializedTile.FindProperty("m_AnimatedSprites");
        animatedSprites.arraySize = frames.Length;
        for (var i = 0; i < frames.Length; i++)
            animatedSprites.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];

        serializedTile.FindProperty("m_MinSpeed").floatValue = minSpeed;
        serializedTile.FindProperty("m_MaxSpeed").floatValue = maxSpeed;
        serializedTile.FindProperty("m_TileColliderType").enumValueIndex = (int)colliderType;
        serializedTile.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(tile);
    }

    private static void EnsureFolder(string path)
    {
        var parts = path.Split('/');
        var current = parts[0];
        for (var i = 1; i < parts.Length; i++)
        {
            var next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
