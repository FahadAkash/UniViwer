using System.Collections.Generic;

public class ScriptInfo
{
    public string FolderPath { get; set; }
    public string ScriptName { get; set; }
    public string ClassName { get; set; }
    public string BaseClass { get; set; }
    public List<string> Fields { get; set; } = new List<string>();
    public List<string> Methods { get; set; } = new List<string>();
    public List<string> Properties { get; set; } = new List<string>(); // Added for properties
    public List<string> Dependencies { get; set; } = new List<string>();
    public string GUID { get; set; }
    public string AssetPath { get; set; }
    public List<SceneRefInfo> SceneReferences { get; set; } = new List<SceneRefInfo>();
}

public class SceneRefInfo
{
    public string ScenePath;
    public List<string> GameObjectPaths = new List<string>();
}