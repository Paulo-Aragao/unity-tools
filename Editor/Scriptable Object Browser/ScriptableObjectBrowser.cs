using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class ScriptableObjectBrowser : EditorWindow
{
    // ── Layout ──────────────────────────────────────────────
    private const float LEFT_PANEL_WIDTH = 200f;
    private const float TOOLBAR_HEIGHT = 22f;
    private const float ITEM_HEIGHT = 22f;

    // ── State ───────────────────────────────────────────────
    private List<ScriptableObject> allAssets = new();
    private Dictionary<string, List<ScriptableObject>> byFolder = new();
    private List<string> folderKeys = new();           // full relative path, e.g. "Scriptables/Configs"
    private Dictionary<string, string> folderLabels = new(); // key → display name (last segment)

    private string selectedFolder = null;
    private ScriptableObject selectedAsset = null;

    private string searchQuery = "";
    private Vector2 leftScroll;
    private Vector2 rightScroll;

    // ── Styles ──────────────────────────────────────────────
    private GUIStyle styleFolderNormal;
    private GUIStyle styleFolderSelected;
    private GUIStyle styleAssetNormal;
    private GUIStyle styleAssetSelected;
    private GUIStyle styleHeader;
    private GUIStyle styleCount;
    private GUIStyle styleTag;
    private bool stylesReady;

    // ────────────────────────────────────────────────────────

    [MenuItem("Tools/Scriptable Object Browser  %#b")]
    public static void ShowWindow()
    {
        var win = GetWindow<ScriptableObjectBrowser>("SO Browser");
        win.minSize = new Vector2(560, 380);
        win.Refresh();
    }

    private void OnEnable() => Refresh();

    // ── Styles ──────────────────────────────────────────────

    private void InitStyles()
    {
        if (stylesReady) return;
        stylesReady = true;

        var blue = new Color(0.24f, 0.49f, 0.91f);

        styleFolderNormal = new GUIStyle(EditorStyles.label)
        {
            padding = new RectOffset(24, 4, 3, 3),
            fontSize = 12,
        };

        styleFolderSelected = new GUIStyle(styleFolderNormal)
        {
            normal = { textColor = Color.white, background = MakeTex(blue) },
            fontStyle = FontStyle.Bold,
        };

        styleAssetNormal = new GUIStyle(EditorStyles.label)
        {
            padding = new RectOffset(28, 4, 3, 3),
            fontSize = 12,
        };

        styleAssetSelected = new GUIStyle(styleAssetNormal)
        {
            normal = { textColor = Color.white, background = MakeTex(new Color(0.24f, 0.49f, 0.91f, 0.85f)) },
            fontStyle = FontStyle.Bold,
        };

        styleHeader = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 13,
            padding = new RectOffset(6, 4, 5, 3),
        };

        styleCount = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleRight,
            normal = { textColor = new Color(0.6f, 0.6f, 0.6f) },
            padding = new RectOffset(0, 8, 3, 3),
        };

        styleTag = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleRight,
            normal = { textColor = new Color(0.5f, 0.5f, 0.5f) },
            padding = new RectOffset(0, 8, 3, 3),
        };
    }

    // ── Main GUI ─────────────────────────────────────────────

    private void OnGUI()
    {
        InitStyles();
        DrawToolbar();

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawLeftPanel();
            DrawDivider();
            DrawRightPanel();
        }
    }

    // ── Toolbar ──────────────────────────────────────────────

    private void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar, GUILayout.Height(TOOLBAR_HEIGHT)))
        {
            GUILayout.Label("Search:", GUILayout.Width(50));

            EditorGUI.BeginChangeCheck();
            searchQuery = EditorGUILayout.TextField(searchQuery, EditorStyles.toolbarSearchField);
            if (EditorGUI.EndChangeCheck())
                selectedAsset = null;

            if (GUILayout.Button("✕", EditorStyles.toolbarButton, GUILayout.Width(22)))
            {
                searchQuery = "";
                GUI.FocusControl(null);
                Repaint();
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(58)))
                Refresh();

            GUILayout.Label($"{allAssets.Count} assets", EditorStyles.toolbarButton, GUILayout.Width(68));
        }
    }

    // ── Left Panel — Folders ──────────────────────────────────

    private void DrawLeftPanel()
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(LEFT_PANEL_WIDTH)))
        {
            GUILayout.Label("Folders", styleHeader);

            using var scroll = new EditorGUILayout.ScrollViewScope(leftScroll, GUILayout.ExpandHeight(true));
            leftScroll = scroll.scrollPosition;

            DrawFolderRow(null, "All", allAssets.Count);

            EditorGUILayout.Space(4);

            foreach (var key in GetFilteredFolders())
                DrawFolderRow(key, folderLabels[key], byFolder[key].Count);
        }
    }

    private void DrawFolderRow(string key, string label, int count)
    {
        bool isSelected = selectedFolder == key;
        var rect = GUILayoutUtility.GetRect(LEFT_PANEL_WIDTH, ITEM_HEIGHT);

        if (isSelected)
            EditorGUI.DrawRect(rect, new Color(0.24f, 0.49f, 0.91f));
        else if (Event.current.type == EventType.Repaint && rect.Contains(Event.current.mousePosition))
            EditorGUI.DrawRect(rect, new Color(1f, 1f, 1f, 0.04f));

        // Folder icon
        var folderIcon = key == null
            ? EditorGUIUtility.IconContent("d_Project").image
            : EditorGUIUtility.IconContent("Folder Icon").image;

        if (folderIcon != null)
            GUI.DrawTexture(new Rect(rect.x + 6, rect.y + 3, 16, 16), folderIcon, ScaleMode.ScaleToFit);

        var labelStyle = isSelected ? styleFolderSelected : styleFolderNormal;
        GUI.Label(new Rect(rect.x, rect.y, rect.width - 36, rect.height), label, labelStyle);
        GUI.Label(new Rect(rect.x, rect.y, rect.width - 4, rect.height), count.ToString(), styleCount);

        if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
        {
            selectedFolder = key;
            selectedAsset = null;
            Repaint();
        }

        if (Event.current.type == EventType.MouseMove && rect.Contains(Event.current.mousePosition))
            Repaint();
    }

    // ── Right Panel — Assets ──────────────────────────────────

    private void DrawRightPanel()
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
        {
            string header = selectedFolder == null ? "All Assets"
                : folderLabels.TryGetValue(selectedFolder, out var lbl) ? lbl : selectedFolder;
            GUILayout.Label(header, styleHeader);

            var assets = GetFilteredAssets();

            using var scroll = new EditorGUILayout.ScrollViewScope(rightScroll, GUILayout.ExpandHeight(true));
            rightScroll = scroll.scrollPosition;

            for (int i = 0; i < assets.Count; i++)
            {
                var asset = assets[i];
                if (asset == null) continue;

                bool isSelected = selectedAsset == asset;
                var rect = GUILayoutUtility.GetRect(GUIContent.none, styleAssetNormal,
                    GUILayout.Height(ITEM_HEIGHT), GUILayout.ExpandWidth(true));

                if (isSelected)
                    EditorGUI.DrawRect(rect, new Color(0.24f, 0.49f, 0.91f, 0.85f));
                else if (i % 2 == 0 && Event.current.type == EventType.Repaint)
                    EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.06f));

                // Asset icon
                var icon = AssetPreview.GetMiniThumbnail(asset);
                if (icon != null)
                    GUI.DrawTexture(new Rect(rect.x + 6, rect.y + 3, 16, 16), icon, ScaleMode.ScaleToFit);

                // Name
                var labelStyle = isSelected ? styleAssetSelected : styleAssetNormal;
                GUI.Label(new Rect(rect.x, rect.y, rect.width - 120, rect.height), asset.name, labelStyle);

                // Type tag on the right
                GUI.Label(new Rect(rect.x, rect.y, rect.width - 4, rect.height),
                    asset.GetType().Name, styleTag);

                // Click
                if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
                {
                    selectedAsset = asset;
                    Selection.activeObject = asset;
                    EditorGUIUtility.PingObject(asset);

                    if (Event.current.clickCount == 2)
                        AssetDatabase.OpenAsset(asset);

                    Repaint();
                }
            }

            if (assets.Count == 0)
            {
                GUILayout.Space(20);
                EditorGUILayout.HelpBox("No assets found.", MessageType.Info);
            }
        }
    }

    // ── Divider ───────────────────────────────────────────────

    private void DrawDivider()
    {
        var rect = GUILayoutUtility.GetRect(1, position.height, GUILayout.Width(1));
        EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.25f));
    }

    // ── Data ─────────────────────────────────────────────────

    public void Refresh()
    {
        allAssets.Clear();
        byFolder.Clear();
        folderKeys.Clear();
        folderLabels.Clear();

        var guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets/_Project" });

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (asset == null) continue;

            allAssets.Add(asset);

            // Folder key: path relative to Assets/_Project/
            // e.g. "Assets/_Project/Scriptables/Configs/Foo.asset" → "Scriptables/Configs"
            string dir = Path.GetDirectoryName(path).Replace('\\', '/');
            const string root = "Assets/_Project/";
            string folderKey = dir.StartsWith(root)
                ? dir.Substring(root.Length)
                : dir.Replace("Assets/", "");

            if (!byFolder.ContainsKey(folderKey))
            {
                byFolder[folderKey] = new List<ScriptableObject>();
                // Display label: last segment of path
                folderLabels[folderKey] = folderKey.Contains('/')
                    ? folderKey.Substring(folderKey.LastIndexOf('/') + 1)
                    : folderKey;
            }

            byFolder[folderKey].Add(asset);
        }

        folderKeys = byFolder.Keys.OrderBy(x => x).ToList();

        // Invalidate selection if folder no longer exists after refresh
        if (selectedFolder != null && !byFolder.ContainsKey(selectedFolder))
        {
            selectedFolder = null;
            selectedAsset = null;
        }

        Repaint();
    }

    private List<string> GetFilteredFolders()
    {
        if (string.IsNullOrEmpty(searchQuery)) return folderKeys;

        return folderKeys.Where(k =>
            folderLabels[k].IndexOf(searchQuery, StringComparison.OrdinalIgnoreCase) >= 0 ||
            byFolder[k].Any(a => a != null &&
                (a.name.IndexOf(searchQuery, StringComparison.OrdinalIgnoreCase) >= 0 ||
                 a.GetType().Name.IndexOf(searchQuery, StringComparison.OrdinalIgnoreCase) >= 0))
        ).ToList();
    }

    private List<ScriptableObject> GetFilteredAssets()
    {
        IEnumerable<ScriptableObject> source = selectedFolder == null
            ? allAssets
            : byFolder.ContainsKey(selectedFolder) ? byFolder[selectedFolder] : Enumerable.Empty<ScriptableObject>();

        if (!string.IsNullOrEmpty(searchQuery))
            source = source.Where(a => a != null && (
                a.name.IndexOf(searchQuery, StringComparison.OrdinalIgnoreCase) >= 0 ||
                a.GetType().Name.IndexOf(searchQuery, StringComparison.OrdinalIgnoreCase) >= 0));

        return source.ToList();
    }

    // ── Helpers ───────────────────────────────────────────────

    private static Texture2D MakeTex(Color color)
    {
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, color);
        tex.Apply();
        return tex;
    }
}
