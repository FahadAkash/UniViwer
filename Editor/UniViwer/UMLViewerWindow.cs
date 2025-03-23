using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class UMLViewerWindow : EditorWindow
{
    private enum Tab { UMLViewer, FolderTree }
    private Tab currentTab = Tab.UMLViewer;

    private List<ScriptInfo> scriptInfos;
    private Vector2 scrollPos;
    private float zoomLevel = 1.0f;
    private string umlSearchTerm = "";
    private string folderSearchTerm = "";
    private GUIStyle folderStyle, scriptStyle, dependencyStyle, headerStyle, sceneHeaderStyle;
    private Dictionary<string, bool> expandedStates = new Dictionary<string, bool>();
    private Dictionary<string, bool> sceneExpansionStates = new Dictionary<string, bool>();

    [System.Serializable]
    private class ViewerCache
    {
        public Dictionary<string, List<SceneRefInfo>> cachedData = new Dictionary<string, List<SceneRefInfo>>();
        public Dictionary<string, long> lastModifiedTimes = new Dictionary<string, long>();
    }

    private ViewerCache cache = new ViewerCache();

    public static void ShowWindow(List<ScriptInfo> infos)
    {
        var window = GetWindow<UMLViewerWindow>("Script UML Viewer");
        window.scriptInfos = infos;
        window.minSize = new Vector2(800, 600);
    }

    private void OnEnable()
    {
        LoadCache();
        InitializeStyles();
    }

    private void OnDisable()
    {
        SaveCache();
    }

    private void InitializeStyles()
    {
        folderStyle = new GUIStyle(EditorStyles.helpBox)
        {
            normal = { background = MakeTex(1, 1, new Color(0.1f, 0.1f, 0.1f, 0.9f)) },
            padding = new RectOffset(10, 10, 10, 10)
        };

        scriptStyle = new GUIStyle(EditorStyles.helpBox)
        {
            normal = { background = MakeTex(1, 1, new Color(0.2f, 0.2f, 0.2f, 0.9f)) },
            margin = new RectOffset(5, 5, 5, 5)
        };

        dependencyStyle = new GUIStyle(EditorStyles.label)
        {
            normal = { textColor = new Color(0.6f, 0.8f, 1f) },
            fontStyle = FontStyle.Italic
        };

        headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 16,
            normal = { textColor = new Color(0.9f, 0.9f, 0.9f) },
            alignment = TextAnchor.MiddleLeft
        };

        sceneHeaderStyle = new GUIStyle(EditorStyles.miniBoldLabel)
        {
            normal = { textColor = new Color(0.8f, 1f, 0.8f) },
            fontSize = 12
        };
    }

    private void OnGUI()
    {
        if (scriptInfos == null || scriptInfos.Count == 0)
        {
            DrawCenteredMessage("No script data to display");
            return;
        }

        DrawToolbar();
        HandleZoom();
        DrawContent();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        currentTab = (Tab)GUILayout.Toolbar((int)currentTab, new[] { "UML Viewer", "Folder Tree" }, EditorStyles.toolbarButton);
        GUILayout.FlexibleSpace();
        zoomLevel = EditorGUILayout.Slider(zoomLevel, 0.5f, 2.0f, GUILayout.Width(150));
        EditorGUILayout.EndHorizontal();
    }

    private void HandleZoom()
    {
        GUIUtility.ScaleAroundPivot(new Vector2(zoomLevel, zoomLevel), Vector2.zero);
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
    }

    private void DrawContent()
    {
        try
        {
            if (currentTab == Tab.UMLViewer) DrawUMLViewer();
            else DrawFolderTree();
        }
        finally
        {
            EditorGUILayout.EndScrollView();
        }
    }
    private void DrawSearchBar(ref string searchTerm, string placeholder)
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        searchTerm = EditorGUILayout.TextField(searchTerm, EditorStyles.toolbarSearchField);
        if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(50)))
        {
            searchTerm = "";
            GUI.FocusControl(null);
        }
        EditorGUILayout.EndHorizontal();

        if (string.IsNullOrEmpty(searchTerm))
        {
            EditorGUI.LabelField(GUILayoutUtility.GetLastRect(), $"🔍 {placeholder}",
                new GUIStyle(EditorStyles.miniLabel)
                {
                    normal = { textColor = Color.gray },
                    alignment = TextAnchor.MiddleCenter
                });
        }
    }
    private void DrawSection(string title, List<string> items, GUIStyle style = null)
    {
        EditorGUILayout.BeginVertical();
        EditorGUILayout.LabelField(title, EditorStyles.miniBoldLabel);
        if (items.Count == 0)
        {
            EditorGUILayout.LabelField("  None", EditorStyles.miniLabel);
        }
        else
        {
            foreach (var item in items)
            {
                EditorGUILayout.LabelField($"  • {item}", style ?? EditorStyles.label);
            }
        }
        EditorGUILayout.EndVertical();
    }
    private void DrawSection(string title, string content)
    {
        EditorGUILayout.BeginVertical();
        EditorGUILayout.LabelField(title, EditorStyles.miniBoldLabel);
        EditorGUILayout.LabelField(content, EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.EndVertical();
    }

    private Transform FindTransformByPath(GameObject[] roots, string path)
    {
        foreach (var root in roots)
        {
            var transform = root.transform.Find(path);
            if (transform != null) return transform;
        }
        return null;
    }

    private void DrawFolderTree()
    {
        EditorGUILayout.Space();
        DrawSearchBar(ref folderSearchTerm, "Search Folders, Scripts, Methods, Fields...");
        EditorGUILayout.Space(5);

        var filteredScripts = FilterScripts(folderSearchTerm);
        var matchingFolders = filteredScripts.Select(s => s.FolderPath).Distinct().ToList();

        if (matchingFolders.Count == 0)
        {
            DrawCenteredMessage("No matching folders found");
            return;
        }

        foreach (var folder in matchingFolders.OrderBy(f => f))
        {
            EditorGUILayout.BeginVertical(folderStyle);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"📁 {folder}", headerStyle);
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Show in Project", EditorStyles.miniButton, GUILayout.Width(100)))
            {
                if (AssetDatabase.IsValidFolder(folder))
                {
                    var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(folder);
                    if (asset != null)
                    {
                        EditorGUIUtility.PingObject(asset);
                    }
                    else
                    {
                        Debug.LogWarning($"Failed to load folder at {folder}");
                    }
                }
                else
                {
                    Debug.LogWarning($"Invalid folder path: {folder}");
                }
            }
            EditorGUILayout.EndHorizontal();

            var folderScripts = filteredScripts.Where(s => s.FolderPath == folder).ToList();
            if (folderScripts.Count == 0)
            {
                EditorGUILayout.LabelField("No scripts in this folder", EditorStyles.centeredGreyMiniLabel);
            }
            else
            {
                foreach (var script in folderScripts.OrderBy(s => s.ScriptName))
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"📄 {script.ScriptName}", GUILayout.Width(200));
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Open", EditorStyles.miniButton, GUILayout.Width(60)))
                    {
                        if (File.Exists(script.AssetPath))
                        {
                            var monoScript = AssetDatabase.LoadAssetAtPath<MonoScript>(script.AssetPath);
                            if (monoScript != null)
                            {
                                AssetDatabase.OpenAsset(monoScript);
                            }
                            else
                            {
                                Debug.LogWarning($"Failed to load MonoScript at {script.AssetPath}");
                            }
                        }
                        else
                        {
                            Debug.LogWarning($"Script file not found at {script.AssetPath}");
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }
    }
    private List<ScriptInfo> FilterScripts(string searchTerm)
    {
        if (scriptInfos == null) return new List<ScriptInfo>();

        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return scriptInfos;
        }

        string lowerSearchTerm = searchTerm.ToLowerInvariant();

        return scriptInfos.Where(s =>
            (s.ScriptName?.ToLowerInvariant().Contains(lowerSearchTerm) == true) ||
            (s.ClassName?.ToLowerInvariant().Contains(lowerSearchTerm) == true) ||
            (s.FolderPath?.ToLowerInvariant().Contains(lowerSearchTerm) == true) ||
            (s.Fields != null && s.Fields.Any(f => f.ToLowerInvariant().Contains(lowerSearchTerm))) ||
            (s.Methods != null && s.Methods.Any(m => m.ToLowerInvariant().Contains(lowerSearchTerm))) ||
            (s.Properties != null && s.Properties.Any(p => p.ToLowerInvariant().Contains(lowerSearchTerm)))
        ).ToList();
    }

    private void DrawUMLViewer()
    {
        EditorGUILayout.Space();
        DrawSearchBar(ref umlSearchTerm, "Search Scripts, Methods, Fields...");
        EditorGUILayout.Space(10);

        var filteredScripts = FilterScripts(umlSearchTerm);
        if (filteredScripts.Count == 0)
        {
            DrawCenteredMessage("No matching scripts found");
            return;
        }

        EditorGUILayout.BeginVertical();
        foreach (var group in filteredScripts.GroupBy(s => s.FolderPath).OrderBy(g => g.Key))
        {
            EditorGUILayout.BeginVertical(folderStyle);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"📁 {group.Key}", headerStyle);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            foreach (var script in group.OrderBy(s => s.ScriptName))
            {
                ProcessScriptSceneReferences(script);
                DrawScriptCard(script);
                EditorGUILayout.Space(5);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(15);
        }
        EditorGUILayout.EndVertical();
    }
    private void DrawScriptCard(ScriptInfo script)
    {
        string uniqueKey = $"{script.GUID}-{script.ClassName}";
        expandedStates.TryGetValue(uniqueKey, out bool isExpanded);

        EditorGUILayout.BeginVertical(scriptStyle);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(EditorGUIUtility.IconContent("cs Script Icon"), GUILayout.Width(30), GUILayout.Height(30));
        EditorGUILayout.BeginVertical();
        isExpanded = EditorGUILayout.Foldout(isExpanded, $"📄 {script.ClassName}", true,
            new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold });
        EditorGUILayout.LabelField($"📂 {script.FolderPath}", EditorStyles.miniLabel);
        EditorGUILayout.EndVertical();

        if (GUILayout.Button("Open Script", EditorStyles.miniButton, GUILayout.Width(80)))
        {
            if (File.Exists(script.AssetPath))
            {
                var monoScript = AssetDatabase.LoadAssetAtPath<MonoScript>(script.AssetPath);
                if (monoScript != null)
                {
                    AssetDatabase.OpenAsset(monoScript);
                }
                else
                {
                    Debug.LogWarning($"Failed to load MonoScript at {script.AssetPath}");
                }
            }
            else
            {
                Debug.LogWarning($"Script file not found at {script.AssetPath}");
            }
        }
        EditorGUILayout.EndHorizontal();

        if (isExpanded)
        {
            EditorGUILayout.Space(5);
            DrawScriptDetails(script);
        }

        expandedStates[uniqueKey] = isExpanded;
        EditorGUILayout.EndVertical();
    }

    private void DrawScriptDetails(ScriptInfo script)
    {
        EditorGUI.indentLevel++;

        EditorGUILayout.LabelField($"🆔 GUID: {script.GUID}", EditorStyles.miniLabel);
        if (File.Exists(script.AssetPath))
        {
            EditorGUILayout.LabelField($"📅 Last Modified: {File.GetLastWriteTime(script.AssetPath)}", EditorStyles.miniLabel);
        }

        DrawSection("📌 Fields", script.Fields);
        EditorGUILayout.Separator();
        DrawSection("⚙️ Methods", script.Methods);
        EditorGUILayout.Separator();
        DrawSection("🏠 Properties", script.Properties); // Assuming Properties is added to ScriptInfo
        EditorGUILayout.Separator();
        DrawSection("🔗 Dependencies", script.Dependencies, dependencyStyle);
        EditorGUILayout.Separator();
        DrawSceneReferences(script);

        EditorGUI.indentLevel--;
    }
    private void DrawSectionHeader(string title)
    {
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        EditorGUILayout.Space(2);
    }
    private void DrawCenteredMessage(string message)
    {
        EditorGUILayout.BeginVertical();
        GUILayout.FlexibleSpace();
        EditorGUILayout.LabelField(message, new GUIStyle(EditorStyles.centeredGreyMiniLabel)
        {
            fontSize = 14,
            normal = { textColor = new Color(0.7f, 0.7f, 0.7f) }
        }, GUILayout.ExpandWidth(true));
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndVertical();
    }

    private void DrawSceneReferences(ScriptInfo script)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("🌍 Scene References", sceneHeaderStyle);

        if (script.SceneReferences.Count == 0)
        {
            EditorGUILayout.HelpBox("Not used in any scenes", MessageType.Info);
            return;
        }

        foreach (var sceneRef in script.SceneReferences)
        {
            string sceneName = Path.GetFileNameWithoutExtension(sceneRef.ScenePath);
            bool isExpanded = EditorGUILayout.Foldout(
                sceneExpansionStates.GetValueOrDefault(sceneRef.ScenePath, false),
                $"📁 {sceneName}", true);

            if (isExpanded)
            {
                EditorGUI.indentLevel++;
                foreach (var goPath in sceneRef.GameObjectPaths)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($" 🎮 {goPath}");
                    if (GUILayout.Button("Focus", EditorStyles.miniButton, GUILayout.Width(60)))
                    {
                        FocusObjectInScene(sceneRef.ScenePath, goPath);
                    }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUI.indentLevel--;
            }

            sceneExpansionStates[sceneRef.ScenePath] = isExpanded;
        }
    }
    private void ProcessScriptSceneReferences(ScriptInfo script)
    {
        try
        {
           // Debug.Log($"Processing script with ClassName: {script.ClassName}, GUID: {script.GUID}");
            if (string.IsNullOrEmpty(script.ClassName)) return;

            if (cache.cachedData.TryGetValue(script.GUID, out var cached) &&
                File.Exists(script.AssetPath) &&
                File.GetLastWriteTime(script.AssetPath).Ticks == cache.lastModifiedTimes[script.GUID])
            {
                script.SceneReferences = cached;
                return;
            }

            script.SceneReferences.Clear();
            var scenePaths = AssetDatabase.FindAssets("t:Scene")
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .Where(p => !string.IsNullOrEmpty(p))
                .ToArray();

            foreach (var scenePath in scenePaths)
            {
                Debug.Log($"Processing scene: {scenePath}");
                Scene scene = default;
                try
                {
                    scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                    if (!scene.IsValid())
                    {
                        Debug.LogError($"Failed to open scene: {scenePath}");
                        continue;
                    }
                    Debug.Log($"Scene {scene.name} has {scene.GetRootGameObjects().Length} root game objects");
                    var sceneInfo = new SceneRefInfo { ScenePath = scenePath };

                    foreach (var go in scene.GetRootGameObjects())
                    {
                        try
                        {
                            Debug.Log($"Processing root game object: {go.name} with {go.GetComponents<MonoBehaviour>().Length} components");
                            FindScriptInChildren(go.transform, script.ClassName, sceneInfo);
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"Error processing game object {go.name}: {e.Message}");
                        }
                    }

                    if (sceneInfo.GameObjectPaths.Count > 0)
                    {
                        script.SceneReferences.Add(sceneInfo);
                        Debug.Log($"Found {sceneInfo.GameObjectPaths.Count} objects in scene {scenePath} for script {script.ClassName}");
                    }
                }
                finally
                {
                    if (scene.IsValid())
                    {
                        EditorSceneManager.CloseScene(scene, true);
                    }
                }
            }

            if (File.Exists(script.AssetPath))
            {
                cache.cachedData[script.GUID] = script.SceneReferences;
                cache.lastModifiedTimes[script.GUID] = File.GetLastWriteTime(script.AssetPath).Ticks;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error processing scene references for {script.ClassName}: {e.Message}");
        }
    }

    private void FindScriptInChildren(Transform parent, string className, SceneRefInfo sceneInfo)
    {
        if (parent == null) return;
        Debug.Log($"Searching components in {parent.name}");
        var components = parent.GetComponents<MonoBehaviour>();
        foreach (var c in components)
        {
            if (c != null)
            {
                Debug.Log($"Checking {parent.name}: {c.GetType().FullName} vs {className}");
                if (c.GetType().FullName == className)
                {
                    sceneInfo.GameObjectPaths.Add(parent.GetHierarchyPath());
                    Debug.Log($"Match found at {parent.GetHierarchyPath()}");
                }
            }
        }
        // Recursively check children
        foreach (Transform child in parent)
        {
            if (child != null)
            {
                FindScriptInChildren(child, className, sceneInfo);
            }
        }
    }

    private void LoadCache()
    {
        string cachePath = Path.Combine(Application.temporaryCachePath, "UMLViewer.cache");
        if (File.Exists(cachePath))
        {
            string json = File.ReadAllText(cachePath);
            cache = JsonUtility.FromJson<ViewerCache>(json);
        }
    }
    private void SaveCache()
    {
        string cachePath = Path.Combine(Application.temporaryCachePath, "UMLViewer.cache");
        string json = JsonUtility.ToJson(cache, true);
        File.WriteAllText(cachePath, json);
    }
    private Texture2D MakeTex(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        Array.Fill(pix, col);
        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }




    private void FocusObjectInScene(string scenePath, string objectPath)
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            try
            {
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                var transform = FindTransformByPath(scene.GetRootGameObjects(), objectPath);

                if (transform != null)
                {
                    Selection.activeGameObject = transform.gameObject;
                    EditorGUIUtility.PingObject(transform.gameObject);
                    SceneView.FrameLastActiveSceneView();
                }
                else
                {
                    Debug.LogWarning($"Object {objectPath} not found in scene {scene.name}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error loading scene: {e.Message}");
            }
        }
    }


}