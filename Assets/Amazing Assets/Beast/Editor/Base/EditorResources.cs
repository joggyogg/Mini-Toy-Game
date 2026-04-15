// Beast - Advanced Tessellation Shader <http://u3d.as/JxL>
// Copyright (c) Amazing Assets <https://amazingassets.world>
 
using UnityEngine;
using UnityEngine.UI;

namespace AmazingAssets.Beast.Editor
{
    public class EditorResources : MonoBehaviour
    {
        static GUIStyle guiStyleOptionsHeader;
        public static GUIStyle GUIStyleOptionsHeader
        {
            get
            {
                if(guiStyleOptionsHeader == null)
                    guiStyleOptionsHeader = new GUIStyle((GUIStyle)"SettingsHeader");

                return guiStyleOptionsHeader;
            }
        }

        static GUIStyle guiStyleButtonTab;
        public static GUIStyle GUIStyleButtonTab
        {
            get
            {
                if (guiStyleButtonTab == null)
                {
                    guiStyleButtonTab = new GUIStyle(GUIStyle.none);

                    if (UnityEditor.EditorGUIUtility.isProSkin)
                        guiStyleButtonTab.normal.textColor = Color.white * 0.95f;
                }

                return guiStyleButtonTab;
            }
        }
        static GUIStyle guiStyleBox;
        public static GUIStyle GUIStyleBox
        {
            get
            {
                if (guiStyleBox == null)
                    guiStyleBox = new GUIStyle("Box");

                return guiStyleBox;
            }
        }



        static int guiStyleOptionsHeaderHeight;
        public static int GUIStyleOptionsHeaderHeight
        {
            get
            {
                if (guiStyleOptionsHeaderHeight == 0)
                    guiStyleOptionsHeaderHeight = Mathf.CeilToInt(GUIStyleOptionsHeader.CalcSize(new GUIContent("Manage")).y);

                return guiStyleOptionsHeaderHeight;
            }
        }


        static Texture2D iconRemoveItem;
        public static Texture2D IconRemoveItem
        {
            get
            {
                if (iconRemoveItem == null)
                    iconRemoveItem = (Texture2D)UnityEditor.EditorGUIUtility.IconContent("P4_DeletedLocal").image;

                return iconRemoveItem;
            }
        }
    }
}
