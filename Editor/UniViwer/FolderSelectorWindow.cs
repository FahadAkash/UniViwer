using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class FolderSelectorWindow : EditorWindow
{
    private List<string> selectedFolders = new List<string>();
    private Vector2 scrollPos;
    private GUIStyle dropAreaStyle;
    private GUIStyle processButtonStyle;
    private GUIStyle folderLabelStyle;
    private Color highlightColor = new Color(0.3f, 0.6f, 1f, 0.3f);
    private Texture2D folderIcon;
    private Texture2D scriptIcon;

    // Add a menu item to open this window
    [MenuItem("Window/Folder Selector")]
    public static void ShowWindow()
    {
        var window = GetWindow<FolderSelectorWindow>("📁 Folder Selector");
        window.minSize = new Vector2(400, 300);
    }

    private void OnEnable()
    {
        folderIcon = EditorGUIUtility.IconContent("Folder Icon").image as Texture2D;
        scriptIcon = EditorGUIUtility.IconContent("cs Script Icon").image as Texture2D;
        InitializeStyles();
    }

    private void DrawSelectedFolders()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("📦 Selected Folders:", folderLabelStyle);

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos,
            GUILayout.Height(150));

        if (selectedFolders.Count == 0)
        {
            EditorGUILayout.LabelField("No folders selected yet...",
                new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                {
                    fontSize = 12,
                    normal = { textColor = EditorGUIUtility.isProSkin ?
                        Color.gray : new Color(0.4f, 0.4f, 0.4f) }
                });
        }
        else
        {
            foreach (var folder in selectedFolders)
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                GUILayout.Label(folderIcon, GUILayout.Width(20), GUILayout.Height(20));
                EditorGUILayout.LabelField(folder, folderLabelStyle);
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("×", GUILayout.Width(20)))
                {
                    selectedFolders.Remove(folder);
                    GUI.changed = true;
                }
                EditorGUILayout.EndHorizontal();
            }
        }
        EditorGUILayout.EndScrollView();
    }

      private void HandleDragAndDrop(Rect dropArea)
    {
        Event evt = Event.current;
        switch (evt.type)
        {
            case EventType.DragUpdated:
            case EventType.DragPerform:
                if (dropArea.Contains(evt.mousePosition))
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    if (evt.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();
                        foreach (var path in DragAndDrop.paths)
                        {
                            if (AssetDatabase.IsValidFolder(path) && 
                                !selectedFolders.Contains(path))
                            {
                                selectedFolders.Add(path);
                                GUI.changed = true;
                            }
                        }
                    }
                    evt.Use();
                    Repaint();
                }
                break;
        }
    }
    
    private void InitializeStyles()
    {
        dropAreaStyle = new GUIStyle(EditorStyles.helpBox)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 14,
            normal = {
                textColor = EditorGUIUtility.isProSkin ? Color.white : Color.black,
                background = MakeTex(1, 1, EditorGUIUtility.isProSkin ?
                    new Color(0.2f, 0.2f, 0.2f, 0.8f) :
                    new Color(0.95f, 0.95f, 0.95f, 0.8f))
            },
            hover = {
                background = MakeTex(1, 1, highlightColor)
            }
        };

        processButtonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            padding = new RectOffset(20, 20, 10, 10),
            normal = {
                background = MakeTex(1, 1, EditorGUIUtility.isProSkin ?
                    new Color(0.2f, 0.4f, 0.6f) :
                    new Color(0.2f, 0.6f, 1f)),
                textColor = Color.white
            },
            hover = {
                background = MakeTex(1, 1, EditorGUIUtility.isProSkin ?
                    new Color(0.3f, 0.5f, 0.7f) :
                    new Color(0.3f, 0.7f, 1f))
            },
            active = {
                background = MakeTex(1, 1, EditorGUIUtility.isProSkin ?
                    new Color(0.1f, 0.3f, 0.5f) :
                    new Color(0.1f, 0.5f, 0.9f))
            }
        };

        folderLabelStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 12,
            padding = new RectOffset(25, 0, 5, 5),
            normal = { textColor = EditorGUIUtility.isProSkin ?
                new Color(0.8f, 0.9f, 1f) :
                new Color(0.1f, 0.3f, 0.6f) }
        };
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

    private void DrawProcessButton()
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        
        var buttonContent = new GUIContent(
            "🚀 Process Scripts", 
            EditorGUIUtility.IconContent("PlayButton").image
        );
        
        if (GUILayout.Button(buttonContent, processButtonStyle, GUILayout.Width(200)))
        {
            if (selectedFolders.Count > 0)
            {
                ProcessFolders();
            }
            else
            {
                EditorUtility.DisplayDialog("⚠️ Alert", "Please select folders first!", "OK");
            }
        }
        
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    private void OnGUI()
    {
        // Header
        EditorGUILayout.LabelField("Select Folders to Analyze", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        // Drop area for dragging folders
        EditorGUILayout.LabelField("Drag folders here:");
        Rect dropArea = GUILayoutUtility.GetRect(0, 50, GUILayout.ExpandWidth(true));
        GUI.Box(dropArea, "Drop Folders Here", EditorStyles.helpBox);

        Event evt = Event.current;
        if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
        {
            if (dropArea.Contains(evt.mousePosition))
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    foreach (var path in DragAndDrop.paths)
                    {
                        if (AssetDatabase.IsValidFolder(path) && !selectedFolders.Contains(path))
                        {
                            selectedFolders.Add(path);
                        }
                    }
                }
                Event.current.Use();
            }
        }

        // Display selected folders
        EditorGUILayout.LabelField("Selected Folders:", EditorStyles.miniBoldLabel);
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(100));
        if (selectedFolders.Count == 0)
        {
            EditorGUILayout.LabelField("No folders selected.", EditorStyles.centeredGreyMiniLabel);
        }
        else
        {
            foreach (var folder in selectedFolders)
            {
                EditorGUILayout.LabelField($"- {folder}");
            }
        }
        EditorGUILayout.EndScrollView();

        // Process button
        EditorGUILayout.Space(10);
        if (GUILayout.Button("Process Scripts", GUILayout.Height(30)))
        {
            if (selectedFolders.Count > 0)
            {
                ProcessFolders();
            }
            else
            {
                EditorUtility.DisplayDialog("Error", "Please select at least one folder.", "OK");
            }
        }
    }

    private void DrawHeader()
    {
        EditorGUILayout.LabelField("📂 Select Folders to Analyze",
            new GUIStyle(EditorStyles.largeLabel)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = EditorGUIUtility.isProSkin ?
                    Color.white : Color.black }
            }, GUILayout.Height(40));
    }

    private void DrawDropArea()
    {
        var dragText = selectedFolders.Count > 0 ?
            "Drag more folders here" : "Drag folders here";

        EditorGUILayout.BeginVertical();
        GUILayout.Space(10);

        Rect dropArea = GUILayoutUtility.GetRect(0, 80, GUILayout.ExpandWidth(true));
        if (Event.current.type == EventType.Repaint)
        {
            dropAreaStyle.Draw(dropArea, GUIContent.none, false, false, false, false);
        }

        GUI.Label(dropArea, $"▼ {dragText} ▼", dropAreaStyle);
        HandleDragAndDrop(dropArea);

        EditorGUILayout.EndVertical();
    }

    private void ProcessFolders()
    {
        List<ScriptInfo> scriptInfos = new List<ScriptInfo>();
        Dictionary<Type, string> typeToPath = new Dictionary<Type, string>();

        // Step 1: Collect scripts from selected folders
        foreach (var folder in selectedFolders)
        {
            string[] guids = AssetDatabase.FindAssets("t:script", new[] { folder });
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                MonoScript monoScript = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (monoScript != null)
                {
                    Type classType = monoScript.GetClass();
                    if (classType != null)
                    {
                        typeToPath[classType] = path;
                    }
                }
            }
        }

        // Step 2: Analyze scripts and create ScriptInfo objects
        foreach (var kvp in typeToPath)
        {
            Type type = kvp.Key;
            string path = kvp.Value;
            string folderPath = Path.GetDirectoryName(path).Replace("\\", "/");

            ScriptInfo info = new ScriptInfo
            {
                FolderPath = folderPath,
                ScriptName = Path.GetFileName(path),
                ClassName = type.FullName, // Use FullName for accurate scene matching
                BaseClass = type.BaseType?.Name ?? "None",
                GUID = AssetDatabase.AssetPathToGUID(path), // Set GUID
                AssetPath = path, // Set AssetPath
                Fields = new List<string>(),
                Methods = new List<string>(),
                Dependencies = new List<string>()
            };

            // Extract fields
            var fields = type.GetFields(System.Reflection.BindingFlags.Public |
                                        System.Reflection.BindingFlags.NonPublic |
                                        System.Reflection.BindingFlags.Instance);
            foreach (var field in fields)
            {
                string access = field.IsPublic ? "public" : "private";
                info.Fields.Add($"{access} {field.FieldType.Name} {field.Name}");
                if (typeToPath.ContainsKey(field.FieldType))
                {
                    info.Dependencies.Add(field.FieldType.Name);
                }
            }

            // Extract methods
            var methods = type.GetMethods(System.Reflection.BindingFlags.Public |
                                          System.Reflection.BindingFlags.NonPublic |
                                          System.Reflection.BindingFlags.Instance |
                                          System.Reflection.BindingFlags.DeclaredOnly);
            foreach (var method in methods)
            {
                if (method.Name.StartsWith("get_") || method.Name.StartsWith("set_")) continue;
                string access = method.IsPublic ? "public" : "private";
                string methodSig = $"{access} {method.ReturnType.Name} {method.Name}(";
                var parameters = method.GetParameters();
                for (int i = 0; i < parameters.Length; i++)
                {
                    methodSig += parameters[i].ParameterType.Name;
                    if (i < parameters.Length - 1) methodSig += ", ";
                    if (typeToPath.ContainsKey(parameters[i].ParameterType))
                    {
                        info.Dependencies.Add(parameters[i].ParameterType.Name);
                    }
                }
                methodSig += ")";
                info.Methods.Add(methodSig);
            }

            // Add base class dependency
            if (type.BaseType != null && typeToPath.ContainsKey(type.BaseType))
            {
                info.Dependencies.Add(type.BaseType.Name);
            }

            info.Dependencies = info.Dependencies.Distinct().ToList();
            scriptInfos.Add(info);
        }

        // Step 3: Show the UML Viewer window
        UMLViewerWindow.ShowWindow(scriptInfos);
    }
}