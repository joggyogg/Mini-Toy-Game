// Beast - Advanced Tessellation Shader <http://u3d.as/JxL>
// Copyright (c) Amazing Assets <https://amazingassets.world>
 
using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;

using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;


namespace AmazingAssets.Beast.Editor
{
    public class BeastEditorWindow : EditorWindow
    {
        public static class Enum
        {
            public enum SaveLocation { SameAsSourceAsset, SameAsSourceAssetButInSubfolder, CustomFolder }
            public enum ContexMenuData { Reset, UseSelectedMesh, HighlightLastSavedFile, OpenSaveFolder }
            public enum SaveMeshData { Default, Custom }
            public enum TextureSaveFormat { JPG, PNG, TGA }
        }



        Dictionary<string, List<Mesh>> meshBatchObjects;
        int meshBatchObjectsMeshCount = 0;
        static List<UnityEngine.Object> meshPickerObjects;

        Enum.SaveLocation meshSaveLocation = Enum.SaveLocation.SameAsSourceAsset;
        string meshSaveFolderCustomPath = string.Empty;
        string meshLastSavedFilePath = string.Empty;
        string meshFilePrefix = "Beast_";
        string meshFileSuffix = string.Empty;
        string meshSubfolderName = string.Empty;
        bool meshReplaceSceneMeshes;

        ModelImporterMeshCompression meshCompression = ModelImporterMeshCompression.Low;
        Enum.SaveMeshData meshSaveWithMesh = Enum.SaveMeshData.Default;
        bool meshOptimizeUV0 = true, meshOptimizeUV2 = true, meshOptimizeUV3 = true,
             meshOptimizeUV5 = true, meshOptimizeUV6 = true, meshOptimizeUV7 = true, meshOptimizeUV8 = true,
             meshOptimizeNormal = true, meshOptimizeTangent = true, meshOptimizeColor = true, meshOptimizeSkin = true;
        static GUIContent[] meshOptimizeNames = new GUIContent[] { new GUIContent("UV0"), new GUIContent("UV2"), new GUIContent("UV3"), new GUIContent("UV4", "Smooth normal data is saved in this UV4 buffer"),
                                                                   new GUIContent("UV5"), new GUIContent("UV6"), new GUIContent("UV7"), new GUIContent("UV8"),
                                                                   new GUIContent("Normal"), new GUIContent("Tangent"), new GUIContent("Color"), new GUIContent("Skin")};

        int meshPickerID = 123456789;
        Vector2 meshObjectsScroll;
             

        //Editor        
        static bool isDataLoaded;



        [MenuItem("Window/Amazing Assets/" + AssetInfo.name, false, 1301)]
        static public void ShowWindow()
        {
            EditorWindow window = EditorWindow.GetWindow(typeof(BeastEditorWindow));
            window.titleContent = new GUIContent(AssetInfo.name);

            window.minSize = new Vector2(400, 360);
        }

        void OnFocus()
        {
            UnityEditor.EditorUtility.ClearProgressBar();

            LoadEditorSettings();
        }
        void OnLostFocus()
        {
            SaveEditorSettings();
        }
        void OnDestroy()
        {
            isDataLoaded = false;
            SaveEditorSettings();
        }
        void OnGUI()
        {
            LoadResources();

            CatchContextMenu();


            GUILayout.Space(10);
            using (new EditorGUIHelper.GUILayoutBeginHorizontal())
            {
                EditorGUILayout.LabelField(string.Empty, GUILayout.MaxWidth(1));

                using (new EditorGUIHelper.EditorGUILayoutBeginVertical())
                {
                    DrawMeshObjcetsArray();

                    DrawSaveOptions();
                }

                EditorGUILayout.LabelField(string.Empty, GUILayout.MaxWidth(3));
            }

            DrawGenerateButton();



            CatchMeshDragAndDrop();

            if (meshPickerObjects != null)
            {
                AddMeshDrops(meshPickerObjects.ToArray());

                meshPickerObjects.Clear();
                meshPickerObjects = null;
            }

            CatchMeshPicker();
        }

        void LoadResources()
        {
            if (meshBatchObjects == null)
                meshBatchObjects = new Dictionary<string, List<Mesh>>();


            if (isDataLoaded == false)
                LoadEditorSettings();
        }
        void ResetEditorSettings()
        {
            meshSaveLocation = Enum.SaveLocation.SameAsSourceAsset;
            meshSaveFolderCustomPath = string.Empty;
            meshLastSavedFilePath = string.Empty;
            meshFilePrefix = "Beast_";
            meshFileSuffix = string.Empty;
            meshSubfolderName = string.Empty;
            meshReplaceSceneMeshes = false;

            meshCompression = ModelImporterMeshCompression.Low;
            meshSaveWithMesh = Enum.SaveMeshData.Default;

            meshOptimizeUV0 = meshOptimizeUV2 = meshOptimizeUV3 = meshOptimizeUV5 = meshOptimizeUV6 = meshOptimizeUV7 = meshOptimizeUV8 = true;
            meshOptimizeNormal = meshOptimizeTangent = meshOptimizeColor = meshOptimizeSkin = true;
        }
        void SaveEditorSettings()
        {
            string saveData = string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18},{19}",
                                             (int)meshSaveLocation, meshSaveFolderCustomPath, meshLastSavedFilePath, meshFilePrefix, meshFileSuffix, meshSubfolderName, meshReplaceSceneMeshes ? 1 : 0,
                                             (int)meshCompression, (int)meshSaveWithMesh,
                                             meshOptimizeUV0 ? 1 : 0, meshOptimizeUV2 ? 1 : 0, meshOptimizeUV3 ? 1 : 0, meshOptimizeUV5 ? 1 : 0, meshOptimizeUV6 ? 1 : 0, meshOptimizeUV7 ? 1 : 0, meshOptimizeUV8 ? 1 : 0,
                                             meshOptimizeNormal ? 1 : 0, meshOptimizeTangent ? 1 : 0, meshOptimizeColor ? 1 : 0, meshOptimizeSkin ? 1 : 0
                                            );


            PlayerPrefs.SetString("BeastMeshGeneratorEditor", saveData);
        }
        void LoadEditorSettings()
        {
            isDataLoaded = true;

            ResetEditorSettings();


            string key = PlayerPrefs.GetString("BeastMeshGeneratorEditor", string.Empty);
            if (string.IsNullOrEmpty(key))
                return;

            string[] data = key.Split(',');
            if (data.Length != 20)
                return;


            int index = 0;
            int iOut;

            if (int.TryParse(data[index++], out iOut)) meshSaveLocation = (Enum.SaveLocation)iOut;
            meshSaveFolderCustomPath = data[index++];
            meshLastSavedFilePath = data[index++];
            meshFilePrefix = data[index++];
            meshFileSuffix = data[index++];
            meshSubfolderName = data[index++];
            if (int.TryParse(data[index++], out iOut)) meshReplaceSceneMeshes = iOut == 1 ? true : false;
            if (int.TryParse(data[index++], out iOut)) meshCompression = (ModelImporterMeshCompression)iOut;
            if (int.TryParse(data[index++], out iOut)) meshSaveWithMesh = (Enum.SaveMeshData)iOut;

            if (int.TryParse(data[index++], out iOut)) meshOptimizeUV0 = iOut == 1 ? true : false;
            if (int.TryParse(data[index++], out iOut)) meshOptimizeUV2 = iOut == 1 ? true : false;
            if (int.TryParse(data[index++], out iOut)) meshOptimizeUV3 = iOut == 1 ? true : false;
            if (int.TryParse(data[index++], out iOut)) meshOptimizeUV5 = iOut == 1 ? true : false;
            if (int.TryParse(data[index++], out iOut)) meshOptimizeUV6 = iOut == 1 ? true : false;
            if (int.TryParse(data[index++], out iOut)) meshOptimizeUV7 = iOut == 1 ? true : false;
            if (int.TryParse(data[index++], out iOut)) meshOptimizeUV8 = iOut == 1 ? true : false;

            if (int.TryParse(data[index++], out iOut)) meshOptimizeNormal = iOut == 1 ? true : false;
            if (int.TryParse(data[index++], out iOut)) meshOptimizeTangent = iOut == 1 ? true : false;
            if (int.TryParse(data[index++], out iOut)) meshOptimizeColor = iOut == 1 ? true : false;
            if (int.TryParse(data[index++], out iOut)) meshOptimizeSkin = iOut == 1 ? true : false;
        }
        void DrawMeshObjcetsArray()
        {
            using (new EditorGUIHelper.EditorGUILayoutBeginHorizontal())
            {
                using (new EditorGUIHelper.GUIEnabled((Selection.objects == null || Selection.objects.Length == 0) ? false : true))
                {
                    if (GUILayout.Button("Add Selected", GUILayout.Height(30)))
                    {
                        AddMeshDrops(Selection.objects);
                    }
                }

                if (GUILayout.Button("Add All Scene Meshes", GUILayout.Height(30)))
                {
                    List<UnityEngine.Object> objectsInScene = new List<UnityEngine.Object>();

                    foreach (GameObject go in Resources.FindObjectsOfTypeAll(typeof(GameObject)) as GameObject[])
                    {
                        if (!UnityEditor.EditorUtility.IsPersistent(go.transform.root.gameObject) && !(go.hideFlags == HideFlags.NotEditable || go.hideFlags == HideFlags.HideAndDontSave))
                            objectsInScene.Add(go);
                    }

                    AddMeshDrops(objectsInScene.ToArray());
                }

                if (GUILayout.Button("Add Custom", GUILayout.Height(30)))
                {
                    UnityEditor.EditorGUIUtility.ShowObjectPicker<Mesh>(null, true, string.Empty, meshPickerID);
                }

                using (new EditorGUIHelper.GUIEnabled(meshBatchObjects != null && meshBatchObjects.Count > 0))
                {
                    if (GUILayout.Button("Remove All", GUILayout.Height(30)))
                    {
                        if (meshBatchObjects != null)
                            meshBatchObjects.Clear();

                        meshBatchObjects = null;

                        Repaint();
                    }
                }
            }

            if (meshBatchObjects == null || meshBatchObjects.Count == 0)
            {
                EditorGUILayout.HelpBox("Drag and drop Mesh assets from Hierarchy and Project windows here.", MessageType.Info);
            }
            else
            {
                GUILayout.Space(8);
                using (new EditorGUIHelper.EditorGUILayoutBeginVertical(EditorStyles.helpBox))
                {
                    float scrollHeightMax = (meshBatchObjectsMeshCount + meshBatchObjects.Count) * (UnityEditor.EditorGUIUtility.singleLineHeight + 2) + 4;
                    if (scrollHeightMax + 240 > position.height)
                        scrollHeightMax = position.height - 240;


                    meshObjectsScroll = EditorGUILayout.BeginScrollView(meshObjectsScroll, GUILayout.MaxHeight(scrollHeightMax));


                    bool containsNULL = false;

                    foreach (KeyValuePair<string, List<Mesh>> entry in meshBatchObjects)
                    {
                        if (entry.Value == null || entry.Value.Count == 0)
                            continue;

                        string labelName = string.Empty;
                        if (string.IsNullOrEmpty(entry.Key) == false)
                            labelName = Path.GetFileName(entry.Key);
                        else if (entry.Value[0] != null && string.IsNullOrEmpty(entry.Value[0].name) == false)
                            labelName = entry.Value[0].name;


                        EditorGUILayout.LabelField(labelName, EditorStyles.miniLabel);

                        using (new EditorGUIHelper.EditorGUIIndentLevel(entry.Value.Count > 1 ? 1 : 0))
                        {
                            for (int i = 0; i < entry.Value.Count; i++)
                            {
                                if (entry.Value[i] == null)
                                {
                                    containsNULL = true;
                                    continue;
                                }
                                else
                                {
                                    using (new EditorGUIHelper.EditorGUILayoutBeginHorizontal())
                                    {
                                        using (new EditorGUIHelper.GUIEnabled(false))
                                        {
                                            EditorGUILayout.ObjectField(entry.Value[i], typeof(Mesh), false);
                                        }

                                        if (GUILayout.Button(new GUIContent(EditorResources.IconRemoveItem, "Remove: " + entry.Value[i].name), GUILayout.MaxWidth(24), GUILayout.MaxHeight(18)))
                                        {
                                            entry.Value[i] = null;
                                            containsNULL = true;
                                            continue;
                                        }
                                    }
                                }
                            }
                        }
                    }

                    EditorGUILayout.EndScrollView();



                    //Remove null elements
                    if (containsNULL)
                    {
                        foreach (string key in meshBatchObjects.Keys.ToList())
                        {
                            meshBatchObjects[key] = meshBatchObjects[key].Where(c => c != null).ToList();
                        }

                        meshBatchObjects = meshBatchObjects.Where(f => f.Value.Count > 0).ToDictionary(x => x.Key, x => x.Value);

                        meshBatchObjectsMeshCount = meshBatchObjects.Values.Sum(list => list.Count);

                        Repaint();
                    }
                }

                GUILayout.Space(3);
            }
        }
        void DrawSaveOptions()
        {
            if (meshBatchObjects == null || meshBatchObjects.Count == 0)
                return;


            GUILayout.Space(5);

            #region Settings & Save Options
            Rect controlRect;

            using (new EditorGUIHelper.EditorGUILayoutBeginVertical(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("File Name");
                Rect drawRect = UnityEngine.GUILayoutUtility.GetLastRect();
                drawRect.xMin += UnityEditor.EditorGUIUtility.labelWidth;

                using (new EditorGUIHelper.EditorGUIUtilityLabelWidth(35))
                {
                    Color backgroundColor = Color.white;
                    if (EditorUtilities.ContainsInvalidFileNameCharacters(meshFilePrefix))
                        backgroundColor = Color.red;
                    if(meshSaveLocation == Enum.SaveLocation.SameAsSourceAsset && string.IsNullOrWhiteSpace(meshFilePrefix) && string.IsNullOrWhiteSpace(meshFileSuffix))
                        backgroundColor = Color.red;

                    using (new EditorGUIHelper.GUIBackgroundColor(backgroundColor))
                    {
                        meshFilePrefix = EditorGUI.TextField(new Rect(drawRect.xMin, drawRect.yMin, drawRect.width / 2f - 2, drawRect.height), "Prefix", meshFilePrefix);
                    }


                    backgroundColor = Color.white;
                    if (EditorUtilities.ContainsInvalidFileNameCharacters(meshFileSuffix))
                        backgroundColor = Color.red;
                    if (meshSaveLocation == Enum.SaveLocation.SameAsSourceAsset && string.IsNullOrWhiteSpace(meshFilePrefix) && string.IsNullOrWhiteSpace(meshFileSuffix))
                        backgroundColor = Color.red;

                    using (new EditorGUIHelper.GUIBackgroundColor(backgroundColor))
                    {
                        meshFileSuffix = EditorGUI.TextField(new Rect(drawRect.xMin + drawRect.width / 2f + 2, drawRect.yMin, drawRect.width / 2f - 4, drawRect.height), "Suffix", meshFileSuffix);
                    }
                }

                meshSaveLocation = (Enum.SaveLocation)EditorGUILayout.EnumPopup("Save Location", meshSaveLocation);
                using (new EditorGUIHelper.EditorGUIIndentLevel(1))
                {
                    if (meshSaveLocation == Enum.SaveLocation.SameAsSourceAssetButInSubfolder)
                    {
                        if (meshSubfolderName == null) meshSubfolderName = string.Empty;
                        using (new EditorGUIHelper.GUIBackgroundColor(EditorUtilities.ContainsInvalidFileNameCharacters(meshSubfolderName) ? Color.red : Color.white))
                        {
                            meshSubfolderName = EditorGUILayout.TextField("Subfolder Name", meshSubfolderName);

                            if(string.IsNullOrWhiteSpace(meshSubfolderName))
                            {
                                using (new EditorGUIHelper.GUIEnabled(false))
                                {
                                    EditorGUI.LabelField(GUILayoutUtility.GetLastRect(), " ", "Beast");
                                }
                            }
                        }
                    }
                    else if (meshSaveLocation == Enum.SaveLocation.CustomFolder)
                    {
                        using (new EditorGUIHelper.GUIBackgroundColor(Directory.Exists(meshSaveFolderCustomPath) ? Color.white : Color.red))
                        {
                            EditorGUILayout.LabelField("Path");
                            controlRect = UnityEngine.GUILayoutUtility.GetLastRect();

                            using (new EditorGUIHelper.GUIBackgroundColor(EditorUtilities.IsPathProjectRelative(meshSaveFolderCustomPath) ? GUI.skin.settings.selectionColor : Color.white))
                            {
                                meshSaveFolderCustomPath = EditorGUI.TextField(new Rect(controlRect.xMin, controlRect.yMin, controlRect.width - 33, controlRect.height), " ", meshSaveFolderCustomPath);
                            }
                        }

                        if (GUI.Button(new Rect(controlRect.xMax - 30, controlRect.yMin, 30, controlRect.height), "..."))
                        {
                            string newPathName = meshSaveFolderCustomPath;
                            newPathName = UnityEditor.EditorUtility.OpenFolderPanel("Select Folder", Directory.Exists(meshSaveFolderCustomPath) ? meshSaveFolderCustomPath : Application.dataPath, string.Empty);
                            if (string.IsNullOrEmpty(newPathName) == false)
                            {
                                meshSaveFolderCustomPath = newPathName;

                                if (EditorUtilities.IsPathProjectRelative(meshSaveFolderCustomPath))
                                    meshSaveFolderCustomPath = EditorUtilities.ConvertPathToProjectRelative(meshSaveFolderCustomPath);

                                SaveEditorSettings();
                            }
                        }
                    }
                }

                meshCompression = (ModelImporterMeshCompression)EditorGUILayout.EnumPopup("Mesh Compression", meshCompression);

                meshSaveWithMesh = (Enum.SaveMeshData)EditorGUILayout.EnumPopup("Save Mesh Data", meshSaveWithMesh);

                if (meshSaveWithMesh == Enum.SaveMeshData.Custom)
                {
                    using (new EditorGUIHelper.EditorGUILayoutBeginHorizontal())
                    {
                        EditorGUILayout.LabelField(string.Empty, GUILayout.MaxWidth(UnityEditor.EditorGUIUtility.labelWidth));

                        using (new EditorGUIHelper.EditorGUILayoutBeginVertical())
                        {
                            int buttonsCountHorizontal = 4;

                            Rect rc1 = EditorGUILayout.GetControlRect();
                            Rect rc4 = EditorGUILayout.GetControlRect();
                            rc4 = EditorGUILayout.GetControlRect();

                            Rect position = new Rect(rc1.xMin, rc1.yMin, rc1.width, (rc4.yMax - rc1.yMin));


                            Rect[] rectArray = CalcButtonRects(position, meshOptimizeNames, buttonsCountHorizontal);

                            int i = 0;
                            meshOptimizeUV0 = GUI.Toggle(rectArray[i], meshOptimizeUV0, meshOptimizeNames[i], "Button");
                            meshOptimizeUV2 = GUI.Toggle(rectArray[++i], meshOptimizeUV2, meshOptimizeNames[i], "Button");
                            meshOptimizeUV3 = GUI.Toggle(rectArray[++i], meshOptimizeUV3, meshOptimizeNames[i], "Button");

                            using (new EditorGUIHelper.GUIEnabled(false))    //Smooth Normal data is save here. Always On
                            {
                                GUI.Toggle(rectArray[++i], true, meshOptimizeNames[i], "Button");
                            }

                            meshOptimizeUV5 = GUI.Toggle(rectArray[++i], meshOptimizeUV5, meshOptimizeNames[i], "Button");
                            meshOptimizeUV6 = GUI.Toggle(rectArray[++i], meshOptimizeUV6, meshOptimizeNames[i], "Button");
                            meshOptimizeUV7 = GUI.Toggle(rectArray[++i], meshOptimizeUV7, meshOptimizeNames[i], "Button");
                            meshOptimizeUV8 = GUI.Toggle(rectArray[++i], meshOptimizeUV8, meshOptimizeNames[i], "Button");

                            meshOptimizeNormal = GUI.Toggle(rectArray[++i], meshOptimizeNormal, meshOptimizeNames[i], "Button");
                            meshOptimizeTangent = GUI.Toggle(rectArray[++i], meshOptimizeTangent, meshOptimizeNames[i], "Button");
                            meshOptimizeColor = GUI.Toggle(rectArray[++i], meshOptimizeColor, meshOptimizeNames[i], "Button");
                            meshOptimizeSkin = GUI.Toggle(rectArray[++i], meshOptimizeSkin, meshOptimizeNames[i], "Button");
                        }
                    }
                }

                bool canReplaceSceneMeshes = true;
                if (meshSaveLocation == Enum.SaveLocation.CustomFolder)
                    canReplaceSceneMeshes = EditorUtilities.IsPathProjectRelative(meshSaveFolderCustomPath);

                if (canReplaceSceneMeshes)
                    meshReplaceSceneMeshes = EditorGUILayout.Toggle("Replace Scene Meshes", meshReplaceSceneMeshes);
                else
                {
                    using (new EditorGUIHelper.GUIEnabled(false))
                    {
                        EditorGUILayout.Toggle("Replace Scene Meshes", false);
                    }
                }
            }
            #endregion


            //Reserve for 
            EditorGUILayout.GetControlRect(GUILayout.Height(50));
        }
        void DrawGenerateButton()
        {
            if (meshBatchObjects == null || meshBatchObjects.Count == 0)
                return;


            using (new EditorGUIHelper.GUIEnabled(IsMeshGeneratorReady()))
            {
                Rect controlRect = EditorGUILayout.GetControlRect();

                string buttonName = "Generate Mesh";
                if (meshBatchObjectsMeshCount > 1)
                    buttonName = "Generate (" + meshBatchObjectsMeshCount + ") Meshes";

                if (GUI.Button(new Rect(controlRect.xMin + 4, this.position.height - 50, controlRect.width - 10, 40), buttonName))
                {

                    float currentIndex = 0;


                    Dictionary<Mesh, Mesh> generatedMeshInfo = new Dictionary<Mesh, Mesh>();


                    foreach (KeyValuePair<string, List<Mesh>> entry in meshBatchObjects)
                    {
                        if (entry.Key == null || entry.Value == null || entry.Value.Count == 0 || entry.Value.Where(a => a != null).Count() == 0)
                            continue;



                        string saveFolderName = string.Empty;
                        switch (meshSaveLocation)
                        {
                            case Enum.SaveLocation.SameAsSourceAsset:
                                {
                                    try
                                    {
                                        saveFolderName = Path.GetDirectoryName(entry.Key);
                                    }
                                    catch (Exception)
                                    {
                                        saveFolderName = string.Empty;
                                    }


                                    if (string.IsNullOrEmpty(saveFolderName) || saveFolderName.IndexOf("Assets") != 0)
                                        saveFolderName = "Assets";

                                }
                                break;

                            case Enum.SaveLocation.SameAsSourceAssetButInSubfolder:
                                {
                                    try
                                    {
                                        saveFolderName = Path.GetDirectoryName(entry.Key);
                                    }
                                    catch (Exception)
                                    {
                                        saveFolderName = string.Empty;
                                    }


                                    if (string.IsNullOrEmpty(saveFolderName) || saveFolderName.IndexOf("Assets") != 0)
                                        saveFolderName = "Assets";


                                    saveFolderName = Path.Combine(saveFolderName, string.IsNullOrEmpty(meshSubfolderName.Trim()) ? "Beast" : meshSubfolderName.Trim());

                                    if (Directory.Exists(saveFolderName) == false)
                                        Directory.CreateDirectory(saveFolderName);
                                }
                                break;

                            case Enum.SaveLocation.CustomFolder:
                                {
                                    saveFolderName = meshSaveFolderCustomPath;

                                    if (Directory.Exists(saveFolderName) == false)
                                        Directory.CreateDirectory(saveFolderName);
                                }
                                break;
                        }

                        for (int i = 0; i < entry.Value.Count; i++)
                        {
                            if (entry.Value[i] == null)
                                continue;


                            UnityEditor.EditorUtility.DisplayProgressBar("Hold On", entry.Value[i].name, (currentIndex++) / meshBatchObjectsMeshCount);


                            //Is mesh readable?
                            if (entry.Value[i].isReadable == false)
                            {
                                ModelImporter modelImporter = AssetImporter.GetAtPath(entry.Key) as ModelImporter;
                                if (modelImporter != null)
                                {
                                    modelImporter.isReadable = true;

                                    AssetDatabase.ImportAsset(entry.Key);

                                    AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                                }
                            }



                            Mesh beastMesh = entry.Value[i].GenerateSmoothNormals();
                            if (beastMesh != null)
                            {
                                string meshName = entry.Value[i].name;
                                if (string.IsNullOrEmpty(meshName.Trim()))
                                    meshName = entry.Value[i].GetInstanceID() + "_Beast";

                                string saveAssetName = (string.IsNullOrEmpty(meshFilePrefix.Trim()) ? string.Empty : meshFilePrefix.Trim()) + meshName + (string.IsNullOrEmpty(meshFileSuffix.Trim()) ? string.Empty : meshFileSuffix.Trim()) + ".asset";
                                string saveAssetPath = Path.Combine(saveFolderName, saveAssetName);

                                #region Optimize
                                if (meshSaveWithMesh == Enum.SaveMeshData.Custom)
                                {
                                    if (meshOptimizeUV0 == false)
                                        beastMesh.uv = null;
                                    if (meshOptimizeUV2 == false)
                                        beastMesh.uv2 = null;
                                    if (meshOptimizeUV3 == false)
                                        beastMesh.uv3 = null;
                                    if (meshOptimizeUV5 == false)
                                        beastMesh.uv5 = null;
                                    if (meshOptimizeUV6 == false)
                                        beastMesh.uv6 = null;
                                    if (meshOptimizeUV7 == false)
                                        beastMesh.uv7 = null;
                                    if (meshOptimizeUV8 == false)
                                        beastMesh.uv8 = null;
                                    if (meshOptimizeNormal == false)
                                        beastMesh.normals = null;
                                    if (meshOptimizeTangent == false)
                                        beastMesh.tangents = null;
                                    if (meshOptimizeColor == false)
                                        beastMesh.colors = null;
                                    if (meshOptimizeSkin == false)
                                    {
                                        beastMesh.boneWeights = null;
                                        beastMesh.bindposes = null;
                                    }
                                }
                                #endregion


                                //Compression
                                if (meshCompression != ModelImporterMeshCompression.Off)
                                    MeshUtility.SetMeshCompression(beastMesh, meshCompression);

                                if (EditorUtilities.ConvertFullPathToProjectRelative(saveAssetPath, out saveAssetPath))
                                    AssetDatabase.CreateAsset(beastMesh, saveAssetPath);
                                else
                                {
                                    //Generate assets in temp folder and then copy to the final destination
                                    string tempFolderPath = Path.Combine(Application.dataPath, "Beast_TEMP");
                                    if (Directory.Exists(tempFolderPath) == false)
                                        Directory.CreateDirectory(tempFolderPath);


                                    string tempFilePath = Path.Combine("Assets", "Beast_TEMP", saveAssetName);
                                    AssetDatabase.CreateAsset(beastMesh, tempFilePath);

                                    //File.Move method fails if destination file already exists. It is nessesary to delete previus file first.
                                    if (File.Exists(saveAssetPath))
                                        File.Delete(saveAssetPath);


                                    File.Move(Path.Combine(tempFolderPath, saveAssetName), saveAssetPath);


                                    //Delete .meta file
                                    string metaFilePath = tempFilePath + ".meta";
                                    if (File.Exists(metaFilePath))
                                        File.Delete(metaFilePath);

                                    AssetDatabase.DeleteAsset(EditorUtilities.ConvertPathToProjectRelative(tempFolderPath));
                                }


                                generatedMeshInfo.Add(entry.Value[i], beastMesh);


                                meshLastSavedFilePath = saveAssetPath;
                            }
                        }
                    }


                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();


                    if (meshReplaceSceneMeshes && EditorUtilities.IsPathProjectRelative(meshLastSavedFilePath))
                    {
                        List<GameObject> rootObjects = new List<GameObject>();
                        Scene scene = SceneManager.GetActiveScene();
                        scene.GetRootGameObjects(rootObjects);

                        Undo.IncrementCurrentGroup();
                        Undo.SetCurrentGroupName("Replace Beast Mesh");
                        var undoGroupIndex = Undo.GetCurrentGroup();

                        // iterate root objects and do something
                        for (int r = 0; r < rootObjects.Count; ++r)
                        {
                            MeshFilter[] meshFilters = rootObjects[r].GetComponentsInChildren<MeshFilter>(true);
                            SkinnedMeshRenderer[] skinnedMeshRenderers = rootObjects[r].GetComponentsInChildren<SkinnedMeshRenderer>(true);


                            foreach (KeyValuePair<string, List<Mesh>> entry in meshBatchObjects)
                            {
                                if (entry.Key == null)
                                    continue;


                                for (int e = 0; e < entry.Value.Count; e++)
                                {
                                    if (entry.Value[e] == null)
                                        continue;


                                    for (int m = 0; m < meshFilters.Length; m++)
                                    {
                                        if (meshFilters[m].sharedMesh == entry.Value[e])
                                        {
                                            Undo.RecordObject(meshFilters[m], " ");
                                            meshFilters[m].sharedMesh = generatedMeshInfo[entry.Value[e]];
                                        }
                                    }


                                    for (int s = 0; s < skinnedMeshRenderers.Length; s++)
                                    {
                                        if (skinnedMeshRenderers[s].sharedMesh == entry.Value[e])
                                        {
                                            Undo.RecordObject(skinnedMeshRenderers[s], " ");
                                            skinnedMeshRenderers[s].sharedMesh = generatedMeshInfo[entry.Value[e]];
                                        }
                                    }
                                }
                            }
                        }

                        Undo.CollapseUndoOperations(undoGroupIndex);
                    }

                    SaveEditorSettings();


                    generatedMeshInfo.Clear();

                    UnityEditor.EditorUtility.ClearProgressBar();
                }
            }
        }

        bool IsMeshGeneratorReady()
        {
            if ((meshBatchObjects == null || meshBatchObjects.Count == 0) ||
               (EditorUtilities.ContainsInvalidFileNameCharacters(meshFilePrefix) || EditorUtilities.ContainsInvalidFileNameCharacters(meshFileSuffix)) ||
               (meshSaveLocation == Enum.SaveLocation.SameAsSourceAsset && (string.IsNullOrEmpty(meshFilePrefix) && string.IsNullOrEmpty(meshFileSuffix))) ||
               (meshSaveLocation == Enum.SaveLocation.SameAsSourceAssetButInSubfolder && EditorUtilities.ContainsInvalidFileNameCharacters(meshSubfolderName)) ||
               (meshSaveLocation == Enum.SaveLocation.CustomFolder && Directory.Exists(meshSaveFolderCustomPath) == false))
            {
                return false;
            }

            return true;
        }

        void CatchMeshDragAndDrop()
        {
            Rect drop_area = new Rect(0, 0, this.position.width, this.position.height);

            Event evt = Event.current;
            switch (evt.type)
            {
                case EventType.DragUpdated:
                case EventType.DragPerform:
                    if (!drop_area.Contains(evt.mousePosition))
                        return;

                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                    if (evt.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();

                        AddMeshDrops(DragAndDrop.objectReferences);
                    }
                    break;
            }
        }
        void CatchMeshPicker()
        {
            if (Event.current.commandName == "ObjectSelectorUpdated")
            {
                if (UnityEditor.EditorGUIUtility.GetObjectPickerControlID() == meshPickerID)
                {
                    if (UnityEditor.EditorGUIUtility.GetObjectPickerObject() != null)
                    {
                        if (meshPickerObjects == null)
                            meshPickerObjects = new List<UnityEngine.Object>();

                        meshPickerObjects.Add(UnityEditor.EditorGUIUtility.GetObjectPickerObject());
                    }
                }
            }
        }
        void AddMeshDrops(UnityEngine.Object[] drops)
        {
            if (drops == null || drops.Length == 0)
                return;


            if (meshBatchObjects == null)
                meshBatchObjects = new Dictionary<string, List<Mesh>>();


            for (int i = 0; i < drops.Length; i++)
            {
                if (drops[i] == null)
                    continue;


                GameObject gameObj = drops[i] as GameObject;

                if (gameObj != null)
                {
                    AddObjectToObjectsArray(gameObj);
                }
                else
                {
                    Mesh meshData = drops[i] as Mesh;

                    if (meshData != null)
                    {
                        AddMeshToObjectsArray(meshData);
                    }
                    else
                    {
                        //May be it is a folder?
                        string path = AssetDatabase.GetAssetPath(drops[i].GetEntityId());

                        AddObjectToObjectsArray(path);
                    }
                }
            }

            meshBatchObjectsMeshCount = meshBatchObjects.Values.SelectMany(list => list).Distinct().Count();

            Repaint();
        }
        void AddObjectToObjectsArray(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath) == false && Directory.Exists(folderPath))
            {
                //Get all files from folder by filter
                //https://stackoverflow.com/questions/163162/can-you-call-directory-getfiles-with-multiple-filters

                string[] assets = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories).
                    Where(s => s.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) || s.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) || s.EndsWith(".obj", StringComparison.OrdinalIgnoreCase)).ToArray();

                if (assets != null && assets.Length > 0)
                {
                    for (int j = 0; j < assets.Length; j++)
                    {
                        UnityEditor.EditorUtility.DisplayProgressBar("Hold On", assets[j], (float)j / assets.Length);

                        AddObjectToObjectsArray(AssetDatabase.LoadAssetAtPath(assets[j], typeof(UnityEngine.Object)));
                    }

                    UnityEditor.EditorUtility.ClearProgressBar();
                }
            }
        }
        void AddObjectToObjectsArray(UnityEngine.Object obj)
        {
            GameObject gameObj = obj as GameObject;

            if (gameObj != null)
            {
                foreach (MeshFilter mf in gameObj.GetComponentsInChildren<MeshFilter>())
                {
                    if (mf != null && mf.sharedMesh != null)
                    {
                        AddMeshToObjectsArray(mf.sharedMesh);
                    }
                }

                foreach (SkinnedMeshRenderer smr in gameObj.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    if (smr != null && smr.sharedMesh != null)
                    {
                        AddMeshToObjectsArray(smr.sharedMesh);
                    }
                }
            }
            else //May be it is an .asset file
            {
                Mesh assetMesh = obj as Mesh;

                if (assetMesh != null)
                    AddMeshToObjectsArray(assetMesh);
            }
        }
        void AddMeshToObjectsArray(Mesh mesh)
        {
            if (mesh == null)
                return;

            string rootAssetPath = AssetDatabase.GetAssetPath(mesh);

            if (meshBatchObjects.ContainsKey(rootAssetPath) == false)
                meshBatchObjects.Add(rootAssetPath, new List<Mesh>());

            if (meshBatchObjects[rootAssetPath].Contains(mesh) == false)
                meshBatchObjects[rootAssetPath].Add(mesh);
        }

        void CatchContextMenu()
        {
            var evt = Event.current;
            var contextRect = new Rect(10, 10, this.position.width, this.position.height);
            if (evt.type == EventType.ContextClick)
            {
                var mousePos = evt.mousePosition;
                if (contextRect.Contains(mousePos))
                {
                    if (meshBatchObjects != null && meshBatchObjects.Count >= 0)
                    {
                        GenericMenu menu = new GenericMenu();


                        #region Use Selected Mesh
                        if (Selection.activeObject == null)
                            menu.AddDisabledItem(new GUIContent("Use Selected Object"), false);
                        else
                            menu.AddItem(new GUIContent("Use Selected Object"), false, CallbackContextMenu, Enum.ContexMenuData.UseSelectedMesh);
                        #endregion


                        #region Highlight
                        if (File.Exists(meshLastSavedFilePath))
                            menu.AddItem(new GUIContent("Highlight Last Saved File"), false, CallbackContextMenu, Enum.ContexMenuData.HighlightLastSavedFile);
                        else
                            menu.AddDisabledItem(new GUIContent("Highlight Last Saved File"));
                        #endregion


                        #region Open Save Folder
                        if (meshSaveLocation == Enum.SaveLocation.CustomFolder)
                        {
                            if (Directory.Exists(meshSaveFolderCustomPath))
                                menu.AddItem(new GUIContent("Open Save Folder"), false, CallbackContextMenu, Enum.ContexMenuData.OpenSaveFolder);
                            else
                                menu.AddDisabledItem(new GUIContent("Open Save Folder"));
                        }
                        #endregion

                        menu.AddSeparator(string.Empty);
                        menu.AddItem(new GUIContent("Reset"), false, CallbackContextMenu, Enum.ContexMenuData.Reset);


                        menu.ShowAsContext();
                    }
                }
            }
        }
        void CallbackContextMenu(object obj)
        {
            switch ((Enum.ContexMenuData)obj)
            {
                case Enum.ContexMenuData.UseSelectedMesh:
                    {
                        AddMeshDrops(Selection.objects);
                    }
                    break;

                case Enum.ContexMenuData.HighlightLastSavedFile:
                    {
                        if (meshLastSavedFilePath.StartsWith(Application.dataPath))
                        {
                            string filePath = "Assets" + meshLastSavedFilePath.Substring(Application.dataPath.Length);

                            UnityEngine.Object mesh = AssetDatabase.LoadAssetAtPath(filePath, typeof(Mesh));
                            if (mesh != null)
                                PingObject(mesh);
                        }
                        else if (meshLastSavedFilePath.IndexOf("Assets") == 0)
                        {
                            UnityEngine.Object mesh = AssetDatabase.LoadAssetAtPath(meshLastSavedFilePath, typeof(Mesh));
                            if (mesh != null)
                                PingObject(mesh);
                        }
                        else
                        {
                            if (File.Exists(meshLastSavedFilePath))
                            {
                                string argument = "/select, \"" + meshLastSavedFilePath.Replace("/", "\\") + "\"";

                                System.Diagnostics.Process.Start("explorer.exe", argument);
                            }
                        }
                    }
                    break;

                case Enum.ContexMenuData.OpenSaveFolder:
                    {
                        Process.Start("file://" + meshSaveFolderCustomPath);
                    }
                    break;

                case Enum.ContexMenuData.Reset:
                    {
                        ResetEditorSettings();
                    }
                    break;

                default:
                    break;
            }
        }

        static Rect[] CalcButtonRects(Rect position, GUIContent[] contents, int xCount)
        {
            GUIStyle style = GUI.skin.button;
            GUI.ToolbarButtonSize buttonSize = GUI.ToolbarButtonSize.Fixed;



            int length = contents.Length;
            int num1 = length / xCount;
            if ((uint)(length % xCount) > 0U)
                ++num1;
            float num2 = (float)CalcTotalHorizSpacing(xCount, style, style, style, style);
            float num3 = (float)(Mathf.Max(style.margin.top, style.margin.bottom) * (num1 - 1));
            float elemWidth = (position.width - num2) / (float)xCount;
            float elemHeight = (position.height - num3) / (float)num1;
            if ((double)style.fixedWidth != 0.0)
                elemWidth = style.fixedWidth;
            if ((double)style.fixedHeight != 0.0)
                elemHeight = style.fixedHeight;


            int num = 0;
            float x = position.xMin;
            float yMin = position.yMin;
            GUIStyle guiStyle1 = style;
            Rect[] rectArray = new Rect[length];
            if (length > 1)
                guiStyle1 = style;
            for (int index = 0; index < length; ++index)
            {
                float width = 0.0f;
                switch (buttonSize)
                {
                    case GUI.ToolbarButtonSize.Fixed:
                        width = elemWidth;
                        break;
                    case GUI.ToolbarButtonSize.FitToContents:
                        width = guiStyle1.CalcSize(contents[index]).x;
                        break;
                }
                rectArray[index] = new Rect(x, yMin, width, elemHeight);
                rectArray[index] = GUIUtility.AlignRectToDevice(rectArray[index]);
                GUIStyle guiStyle2 = style;
                if (index == length - 2 || index == xCount - 2)
                    guiStyle2 = style;
                x = rectArray[index].xMax + (float)Mathf.Max(guiStyle1.margin.right, guiStyle2.margin.left);
                ++num;
                if (num >= xCount)
                {
                    num = 0;
                    yMin += elemHeight + (float)Mathf.Max(style.margin.top, style.margin.bottom);
                    x = position.xMin;
                    guiStyle2 = style;
                }
                guiStyle1 = guiStyle2;
            }
            return rectArray;
        }
        static int CalcTotalHorizSpacing(int xCount, GUIStyle style, GUIStyle firstStyle, GUIStyle midStyle, GUIStyle lastStyle)
        {
            if (xCount < 2)
                return 0;
            if (xCount == 2)
                return Mathf.Max(firstStyle.margin.right, lastStyle.margin.left);

            int num = Mathf.Max(midStyle.margin.left, midStyle.margin.right);
            return Mathf.Max(firstStyle.margin.right, midStyle.margin.left) + Mathf.Max(midStyle.margin.right, lastStyle.margin.left) + num * (xCount - 3);
        }

        void PingObject(UnityEngine.Object obj)
        {
            if (obj != null)
            {
                // Select the object in the project folder
                Selection.activeObject = obj;

                // Also flash the folder yellow to highlight it
                UnityEditor.EditorGUIUtility.PingObject(obj);
            }
        }
    }
}
