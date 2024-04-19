using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using UnityEditor;
using UnityEngine;

public class SymbolicLinkWindow : EditorWindow
{
    [MenuItem("Tools/Symbolic Link")]
    static void OpenSymbolicLinkWindow()
    {
        GetWindow<SymbolicLinkWindow>("Symbolic Link");
    }
    
    private const string SaveDataPath = "Assets/Editor/SymbolicLinkWindowData.asset";
    private SymbolicLinkWindowScriptableObject data;
    
    private Vector2 _scrollPos = Vector2.zero;
    private List<(string Path, string Link)> _symLinkDirs = new();
    
    private bool isOpenCreate;
    private string createTarget;
    private string createLinkTo;

    private void Awake()
    {
        LoadData();
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<SymbolicLinkWindowScriptableObject>();
            data.RootDir = Application.dataPath;
            SaveData();
        }
        createTarget = Application.dataPath;
    }

    private void OnGUI()
    {
        using (new GUILayout.HorizontalScope())
        {
            GUILayout.Label("Root", GUILayout.Width(100f));
            GUILayout.Label(GetRelativePathFromAssetsDir(data.RootDir));

            if (GUILayout.Button("Select", GUILayout.Width(80f)))
            {
                var path = EditorUtility.OpenFolderPanel("Root", Application.dataPath, string.Empty);
                if (string.IsNullOrEmpty(path)) return;

                data.RootDir = path;
                SaveData();
            }
        }
        
        EditorGUILayout.Space(5f);
        GUILayout.Box("", GUILayout.Height(2), GUILayout.ExpandWidth(true));

        using (new GUILayout.HorizontalScope())
        {
            GUILayout.Label("Symbolic Link", GUILayout.Width(100f));
            
            if (GUILayout.Button("Search", GUILayout.Width(80f)))
            {
                _symLinkDirs = SearchSymbolicLink(); 
            }

            if (GUILayout.Button("Clear", GUILayout.Width(80f)))
            {
                _symLinkDirs.Clear();
            }
        }
        
        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
        {
            EditorGUI.indentLevel++;
            
            if (_symLinkDirs.Count == 0)
            {
                EditorGUILayout.LabelField("No results");
            }
            else
            {
                using (new GUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Path", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("LinkTo", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("", GUILayout.Width(80f));
                }
                
                var tmpLinks = _symLinkDirs.ToArray();
                foreach (var symLink in tmpLinks)
                {
                    using (new GUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(GetRelativePathFromAssetsDir(symLink.Path));
                        EditorGUILayout.LabelField(GetRelativePathFromAssetsDir(symLink.Link));
                        if (GUILayout.Button("Remove", GUILayout.Width(80f)))
                        {
                            DeleteSymlink(symLink.Path);
                            _symLinkDirs = SearchSymbolicLink(); 
                        }
                    }
                }
            }

            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndScrollView();
        
        EditorGUILayout.Space(5f);
        GUILayout.Box("", GUILayout.Height(2), GUILayout.ExpandWidth(true));
        
        isOpenCreate = EditorGUILayout.BeginFoldoutHeaderGroup(isOpenCreate, "Add");
        if (isOpenCreate)
        {
            EditorGUI.indentLevel++;

            using (new GUILayout.HorizontalScope())
            {
                createLinkTo = EditorGUILayout.TextField("LinkTo", createLinkTo);
                if (GUILayout.Button("Select", GUILayout.Width(120f)))
                {
                    var path = EditorUtility.OpenFolderPanel("Root", data.RootDir, string.Empty);
                    if (string.IsNullOrEmpty(path)) return;

                    GUI.FocusControl("");
                    createLinkTo = path;
                }
            }
            using (new GUILayout.HorizontalScope())
            {
                createTarget = EditorGUILayout.TextField("Target", createTarget);
                if (GUILayout.Button("Set Root path", GUILayout.Width(120f)))
                {
                    GUI.FocusControl("");
                    createTarget = data.RootDir;
                }
            }

            EditorGUILayout.Space(2f);
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Reset", GUILayout.Width(80f)))
                {
                    GUI.FocusControl("");
                    createTarget = data.RootDir;
                    createLinkTo = "";
                }
                if (GUILayout.Button("Create", GUILayout.Width(80f)))
                {
                    if (!Directory.Exists(createLinkTo))
                    {
                        Debug.Log("LinkTo directory is not found");
                        return;
                    }
                    if (Directory.Exists(createTarget))
                    {
                        Debug.Log("Target directory exists");
                        return;
                    }

                    Cmd.CreateSymbolicLink(createLinkTo, createTarget);
                    AssetDatabase.Refresh();
                }
            }

            EditorGUI.indentLevel--;
        }
    }

    private List<(string Path, string Link)> SearchSymbolicLink()
    {
        var dirs = Directory.EnumerateDirectories(data.RootDir, "*", SearchOption.AllDirectories);
        return dirs.Select(x =>
            {
                // Unityだと \ ではなく / 区切りになるっぽいので変換
                var path = Path.GetFullPath(x);
                var link = ResolveLinkTarget(path);
                return new { Path = path, SymLink = path != link, Link = link };
            })
            .Where(x => x.SymLink)
            .Select(x => (x.Path, x.Link))
            .ToList();
    }

    private void SaveData()
    {
        if (!AssetDatabase.Contains(data))
        {
            AssetDatabase.CreateAsset(data, SaveDataPath);
        }

        data.hideFlags = HideFlags.NotEditable;
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
    private void LoadData()
    {
        var asset = AssetDatabase.LoadAssetAtPath<SymbolicLinkWindowScriptableObject>(SaveDataPath);
        if (asset != null)
            data = asset;
    }

    private void DeleteSymlink(string path)
    {
        Directory.Delete(path);

        var meta = $"{path}.meta";
        if (File.Exists(meta))
            File.Delete(meta);
    }

    private const string DefaltDataDir = "Assets";
    private string GetRelativePathFromAssetsDir(string targetPath)
    {
        var relativePath = Path.GetRelativePath(Application.dataPath, targetPath);
        relativePath = relativePath == "."
            ? DefaltDataDir
            : Path.Combine(DefaltDataDir, relativePath);

        return relativePath;
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string lpFileName, int dwDesiredAccess, int dwShareMode, IntPtr securityAttributes, int dwCreationDisposition, int dwFlagsAndAttributes, IntPtr hTemplateFile);

    [DllImport("kernel32.dll", EntryPoint = "GetFinalPathNameByHandleW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetFinalPathNameByHandle([In] SafeFileHandle hFile, [Out] StringBuilder lpszFilePath, [In] int cchFilePath, [In] int dwFlags);

    private const int CREATION_DISPOSITION_OPEN_EXISTING = 3;
    private const int FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;

    private static string ResolveLinkTarget(string path)
    {
        if (!Directory.Exists(path) && !File.Exists(path))
        {
            throw new IOException("Path not found");
        }

        SafeFileHandle directoryHandle = CreateFile(path, 0, 2, IntPtr.Zero, CREATION_DISPOSITION_OPEN_EXISTING, FILE_FLAG_BACKUP_SEMANTICS, IntPtr.Zero); //Handle file / folder

        if (directoryHandle.IsInvalid)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        StringBuilder result = new StringBuilder(512);
        int mResult = GetFinalPathNameByHandle(directoryHandle, result, result.Capacity, 0);

        if (mResult < 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        if (result.Length >= 4 && result[0] == '\\' && result[1] == '\\' && result[2] == '?' && result[3] == '\\')
        {
            return result.ToString().Substring(4); // "\\?\" remove
        }
        return result.ToString();
    }
}

public static class Cmd
{
    public static void CreateSymbolicLink(string src, string dest)
    {
        Execute($"/k mklink /D \"{dest}\" \"{src}\"");
    }
    private static void Execute(string args, bool hidden = true)
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo();
        startInfo.FileName = "cmd.exe";
        startInfo.Arguments = args;
        startInfo.Verb = "RunAs";
        startInfo.UseShellExecute = true;
        if (hidden) startInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
        System.Diagnostics.Process.Start(startInfo);
    }
}