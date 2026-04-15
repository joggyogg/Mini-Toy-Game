// Beast - Advanced Tessellation Shader <http://u3d.as/JxL>
// Copyright (c) Amazing Assets <https://amazingassets.world>
 
using System;
using System.IO;

using UnityEngine;


namespace AmazingAssets.Beast.Editor
{
    static internal class EditorUtilities
    {
        static public char[] invalidFileNameCharachters = Path.GetInvalidFileNameChars();


        static public bool ConvertFullPathToProjectRelative(string path, out string newPath)
        {
            if (string.IsNullOrEmpty(path))
            {
                newPath = string.Empty;
                return false;
            }


            if (path.IndexOf("Assets") == 0)
            {
                newPath = path;
                return true;
            }


            path = path.Replace("\\", "/").Replace("\"", "/");
            if (path.StartsWith(Application.dataPath))
            {
                newPath = "Assets" + path.Substring(Application.dataPath.Length);
                return true;
            }
            else
            {
                newPath = path;
                return false;
            }
        }
        static public string ConvertPathToProjectRelative(string path)
        {
            //Before using this method, make sure path 'is' project relative

            return NormalizePath("Assets" + path.Substring(Application.dataPath.Length));
        }
        static public bool IsPathProjectRelative(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            if (Directory.Exists(path) == false)
                return false;

            if (path.IndexOf("Assets") == 0)
                return true;


            return NormalizePath(path).Contains(NormalizePath(Application.dataPath));
        }
        static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return path;
            else
                return path.Replace("//", "/").Replace("\\\\", "/").Replace("\\", "/");
        }
        static public string RemoveInvalidCharacters(string name)
        {
            if (string.IsNullOrEmpty(name))
                return string.Empty;
            else
            {
                if (name.IndexOfAny(invalidFileNameCharachters) == -1)
                    return name;
                else
                    return string.Concat(name.Split(invalidFileNameCharachters, StringSplitOptions.RemoveEmptyEntries));
            }
        }
        static public bool ContainsInvalidFileNameCharacters(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;
            else
                return name.IndexOfAny(invalidFileNameCharachters) >= 0;
        }
    }
}
