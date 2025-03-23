using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class ScriptDataCollector
{
    public static List<ScriptInfo> CollectScriptInfos(string[] folderPaths)
    {
        var scriptInfos = new List<ScriptInfo>();
        var typeToPath = new Dictionary<Type, string>();

        foreach (string folder in folderPaths)
        {
            string[] guids = AssetDatabase.FindAssets("t:Script", new[] { folder });
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                MonoScript monoScript = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (monoScript != null)
                {
                    Type type = monoScript.GetClass();
                    if (type != null && !typeToPath.ContainsKey(type))
                    {
                        typeToPath[type] = path;
                    }
                }
            }
        }

        foreach (var kvp in typeToPath)
        {
            scriptInfos.Add(CreateScriptInfo(kvp.Key, kvp.Value));
        }

        return scriptInfos;
    }

    private static ScriptInfo CreateScriptInfo(Type type, string path)
    {
        return new ScriptInfo
        {
            FolderPath = Path.GetDirectoryName(path).Replace("\\", "/"),
            ScriptName = Path.GetFileName(path),
            ClassName = type.FullName,
            BaseClass = type.BaseType?.Name ?? "None",
            GUID = AssetDatabase.AssetPathToGUID(path),
            AssetPath = path,
            Fields = GetFields(type),
            Methods = GetMethods(type),
            Properties = GetProperties(type), // Added properties
            Dependencies = GetDependencies(type)
        };
    }

    private static List<string> GetFields(Type type)
    {
        var fields = type.GetFields(System.Reflection.BindingFlags.Public |
                                    System.Reflection.BindingFlags.NonPublic |
                                    System.Reflection.BindingFlags.Instance);
        return fields.Select(f =>
        {
            string access = f.IsPublic ? "public" : "private";
            return $"{access} {f.FieldType.Name} {f.Name}";
        }).ToList();
    }

    private static List<string> GetMethods(Type type)
    {
        var methods = type.GetMethods(System.Reflection.BindingFlags.Public |
                                      System.Reflection.BindingFlags.NonPublic |
                                      System.Reflection.BindingFlags.Instance |
                                      System.Reflection.BindingFlags.DeclaredOnly);
        return methods.Where(m => !m.Name.StartsWith("get_") && !m.Name.StartsWith("set_"))
                      .Select(m =>
                      {
                          string access = m.IsPublic ? "public" : "private";
                          string methodSig = $"{access} {m.ReturnType.Name} {m.Name}(";
                          var parameters = m.GetParameters();
                          methodSig += string.Join(", ", parameters.Select(p => $"{p.ParameterType.Name} {p.Name}"));
                          methodSig += ")";
                          return methodSig;
                      }).ToList();
    }

    private static List<string> GetProperties(Type type)
    {
        var properties = type.GetProperties(System.Reflection.BindingFlags.Public |
                                            System.Reflection.BindingFlags.NonPublic |
                                            System.Reflection.BindingFlags.Instance);
        return properties.Select(p =>
        {
            string access = p.GetGetMethod(true)?.IsPublic == true || p.GetSetMethod(true)?.IsPublic == true ? "public" : "private";
            string getSet = "";
            if (p.GetGetMethod(true) != null) getSet += "get; ";
            if (p.GetSetMethod(true) != null) getSet += "set; ";
            return $"{access} {p.PropertyType.Name} {p.Name} {{ {getSet.Trim()} }}";
        }).ToList();
    }

    private static List<string> GetDependencies(Type type)
    {
        var dependencies = new HashSet<string>();

        foreach (var field in type.GetFields())
        {
            if (field.FieldType.Assembly == type.Assembly)
                dependencies.Add(field.FieldType.Name);
        }

        foreach (var method in type.GetMethods())
        {
            foreach (var param in method.GetParameters())
            {
                if (param.ParameterType.Assembly == type.Assembly)
                    dependencies.Add(param.ParameterType.Name);
            }
        }

        if (type.BaseType?.Assembly == type.Assembly)
            dependencies.Add(type.BaseType.Name);

        return dependencies.ToList();
    }
}