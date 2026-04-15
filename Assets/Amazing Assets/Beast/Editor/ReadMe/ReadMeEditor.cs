// Beast - Advanced Tessellation Shader <http://u3d.as/JxL>
// Copyright (c) Amazing Assets <https://amazingassets.world>

using System.IO;

using UnityEngine;
using UnityEditor;


namespace AmazingAssets.Beast.Editor
{
    [CustomEditor(typeof(ReadMe))]
    [InitializeOnLoad]
    public class ReadMeEditor : UnityEditor.Editor
    {
        const float k_Space = 16f;


        GUIStyle guiStyleTitle;
        GUIStyle GUIStyleTitle
        {
            get
            {
                if (guiStyleTitle == null)
                {
                    guiStyleTitle = new GUIStyle(EditorStyles.boldLabel);
                    guiStyleTitle.alignment = TextAnchor.MiddleCenter;
                    guiStyleTitle.wordWrap = true;
                    guiStyleTitle.fontSize = 18;
                }

                return guiStyleTitle;
            }
        }

        GUIStyle guiStyleHeading;
        GUIStyle GUIStyleHeading
        {
            get
            {
                if (guiStyleHeading == null)
                {
                    guiStyleHeading = new GUIStyle(EditorStyles.boldLabel);
                    guiStyleHeading.fontSize = 16;
                    guiStyleHeading.wordWrap = true;
                }

                return guiStyleHeading;
            }
        }
        GUIStyle guiStyleLink;
        GUIStyle GUIStyleLink
        {
            get
            {
                if (guiStyleLink == null)
                {
                    guiStyleLink = new GUIStyle(EditorStyles.label);
                    guiStyleLink.richText = true;
                    guiStyleLink.fontSize = 13;


                    guiStyleLink.normal.textColor = new Color(0x00 / 255f, 0x78 / 255f, 0xDA / 255f, 1f);
                    guiStyleLink.stretchWidth = false;
                }

                return guiStyleLink;
            }
        }

        GUIStyle guiStyleLabelWrappedRichText;
        GUIStyle GUIStyleLabelWrappedRichText
        {
            get
            {
                if (guiStyleLabelWrappedRichText == null)
                {
                    guiStyleLabelWrappedRichText = new GUIStyle(EditorStyles.wordWrappedLabel);
                    guiStyleLabelWrappedRichText.richText = true;
                }

                return guiStyleLabelWrappedRichText;
            }
        }

        Texture2D m_logo;
        Texture2D Logo
        {
            get
            {
                if (m_logo == null)
                {
                    m_logo = LoadIcon("Logo");
                }
                return m_logo;
            }
        }


        protected override void OnHeaderGUI()
        {
            var iconWidth = Mathf.Min(UnityEditor.EditorGUIUtility.currentViewWidth / 3f - 20f, 128f);

            GUILayout.BeginHorizontal("In BigTitle");
            {
                GUILayout.Space(k_Space);
                Rect logoRect = EditorGUILayout.GetControlRect(GUILayout.Width(iconWidth), GUILayout.Height(iconWidth));
                if (GUI.Button(logoRect, Logo))
                    Application.OpenURL(AssetInfo.storeURL);

                UnityEditor.EditorGUIUtility.AddCursorRect(logoRect, MouseCursor.Link);

                GUILayout.Space(k_Space);
                GUILayout.BeginVertical();
                {
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(AssetInfo.name, GUIStyleTitle);
                    GUILayout.Label("Version " + AssetInfo.version, EditorStyles.centeredGreyMiniLabel);
                    GUILayout.FlexibleSpace();
                }

                GUILayout.EndVertical();
                GUILayout.FlexibleSpace();
            }
            GUILayout.EndHorizontal();
        }
        public override void OnInspectorGUI()
        {
            //Thank you
            {
                Rect reviewRect = EditorGUILayout.GetControlRect();
                GUILayout.Label($"Thank you for using <b>{AssetInfo.name}</b>.\nIf you find this asset useful and enjoy it, please consider leaving a review and rating. Your feedback encourages us to continue supporting, updating, and improving it.", GUIStyleLabelWrappedRichText);
                if (LinkLabel(new GUIContent("Leave review and rating")))
                    Application.OpenURL(AssetInfo.storeURL);
            }

            GUILayout.Space(k_Space * 1.5f);
            //Example scenes
            {
                GUILayout.Label("Example Scenes", GUIStyleHeading);

                if (LinkLabel(new GUIContent("Download example scenes (BiRP)")))
                    Application.OpenURL("https://drive.google.com/open?id=1XMuWITS4ohk4TC0f-lG-70dAHaf2v7gr&usp=drive_fs");

                GUILayout.Space(2);
                if (LinkLabel(new GUIContent("Download example scenes (URP)")))
                    Application.OpenURL("https://drive.google.com/open?id=18G2io2gAqpHs1Jz5McVc6teCqjOHhU9u&usp=drive_fs");
            }

            GUILayout.Space(k_Space);
            //Documentation
            {
                GUILayout.Label("Documentation", GUIStyleHeading);

                if (LinkLabel(new GUIContent("Open online documentation")))
                    Application.OpenURL(AssetInfo.documentationURL);
            }

            GUILayout.Space(k_Space);
            //Forum
            {
                GUILayout.Label("Forum", GUIStyleHeading);

                if (LinkLabel(new GUIContent("Get answers")))
                    Application.OpenURL(AssetInfo.forumURL);
            }

            GUILayout.Space(k_Space);
            //Support
            {
                GUILayout.Label("Support and bug report", GUIStyleHeading);

                if (LinkLabel(new GUIContent("Submit a report")))
                    Application.OpenURL("mailto:" + AssetInfo.supportMail);
            }

            GUILayout.Space(k_Space);
            //Support
            {
                GUILayout.Label("More Assets", GUIStyleHeading);

                if (LinkLabel(new GUIContent("Open publisher page")))
                    Application.OpenURL(AssetInfo.publisherPage);
            }
        }

        bool LinkLabel(GUIContent label, params GUILayoutOption[] options)
        {
            var position = GUILayoutUtility.GetRect(label, GUIStyleLink, options);

            Handles.BeginGUI();
            Handles.color = GUIStyleLink.normal.textColor;
            Handles.DrawLine(new Vector3(position.xMin, position.yMax), new Vector3(position.xMax, position.yMax));
            Handles.color = Color.white;
            Handles.EndGUI();

            UnityEditor.EditorGUIUtility.AddCursorRect(position, MouseCursor.Link);

            return GUI.Button(position, label, GUIStyleLink);
        }
        string GetThisAssetProjectPath()
        {
            string fileName = "AmazingAssets.Beast.Editor";

            string[] assets = AssetDatabase.FindAssets(fileName, null);
            if (assets != null && assets.Length > 0)
            {
                string currentFilePath = AssetDatabase.GUIDToAssetPath(assets[0]);
                return Path.GetDirectoryName(Path.GetDirectoryName(currentFilePath));
            }
            else
            {
                return string.Empty;
            }
        }
        Texture2D LoadIcon(string name)
        {
            string iconPath = Path.Combine(GetThisAssetProjectPath(), "Editor", "ReadMe", name);
            if (File.Exists(iconPath) == false)
                iconPath += ".png";

            byte[] bytes = File.ReadAllBytes(iconPath);
            Texture2D icon = new Texture2D(2, 2);
            icon.LoadImage(bytes);

            return icon;
        }
    }
}