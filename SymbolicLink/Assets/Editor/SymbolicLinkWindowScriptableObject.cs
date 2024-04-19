using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[Serializable]
public class SymbolicLinkWindowScriptableObject : ScriptableObject
{
    [SerializeField]
    private string rootDir;

    public string RootDir
    {
        get => rootDir;
        set => rootDir = value;
    }
}
