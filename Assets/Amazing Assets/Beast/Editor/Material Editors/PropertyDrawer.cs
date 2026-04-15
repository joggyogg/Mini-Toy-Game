// Beast - Advanced Tessellation Shader <http://u3d.as/JxL>
// Copyright (c) Amazing Assets <https://amazingassets.world>
 
using System;
using System.Reflection;

using UnityEngine;
using UnityEditor;


namespace AmazingAssets.Beast.Editor.Universal.ShaderGUI
{
    public class PropertyDrawer
    {
        public static class Styles
        {
            public static readonly GUIContent beastHeader = new GUIContent("Beast Tessellation");
            public static readonly GUIContent curvedWorldHeader = new GUIContent("Curved World");

        }
        
        public enum Expandable
        {
            CurvedWorld = 1 << 4,
            Beast = 1 << 5,
        }

        public enum TessellationMode
        {
            Fixed,
            DistanceBased,
            EdgeLength,
            Phong
        }
        public enum UVMapping
        {
            Default,
            Triplanar
        }
        public enum Recalculate
        {
            None,
            Normals,
            Tangents,
        }

        public struct BeastProperties
        {
            public MaterialProperty _Beast_Tessellation_Type;
            public MaterialProperty _Beast_Tessellation_UV_Mapping;
            public MaterialProperty _Beast_TessellationFactor;
            public MaterialProperty _Beast_TessellationMinDistance;
            public MaterialProperty _Beast_TessellationMaxDistance;
            public MaterialProperty _Beast_TessellationEdgeLength;
            public MaterialProperty _Beast_TessellationPhong;
            public MaterialProperty _Beast_TessellationDisplaceMap;
            public MaterialProperty _Beast_TessellationDisplaceMapUVSet;
            public MaterialProperty _Beast_TessellationDisplaceMapChannel;
            public MaterialProperty _Beast_TessellationDisplaceStrength;
            public MaterialProperty _Beast_TessellationTriplanarUVScale;
            public MaterialProperty _Beast_TessellationShadowPassLOD;
            public MaterialProperty _Beast_TessellationDepthPassLOD;
            public MaterialProperty _Beast_TessellationUseSmoothNormals;
            public MaterialProperty _Beast_Generate;
            public MaterialProperty _Beast_TessellationNormalCoef;
            public MaterialProperty _Beast_TessellationTangentCoef;

            public MaterialProperty _Beast_TessellationMaskMap_Mode;
            public MaterialProperty _Beast_TessellationMaskMap;
            public MaterialProperty _Beast_TessellationMaskMapOffset;
            public MaterialProperty _Beast_TessellationMaskMapUVSet;
            public MaterialProperty _Beast_TessellationMaskMapChannel;

            //Curved World
            public MaterialProperty _CurvedWorldBendSettings;


            public BeastProperties(MaterialProperty[] properties)
            {
                _Beast_Tessellation_Type = BaseShaderGUI.FindProperty("_Beast_Tessellation_Type", properties);
                _Beast_Tessellation_UV_Mapping = BaseShaderGUI.FindProperty("_Beast_Tessellation_UV_Mapping", properties);
                _Beast_TessellationFactor = BaseShaderGUI.FindProperty("_Beast_TessellationFactor", properties);
                _Beast_TessellationMinDistance = BaseShaderGUI.FindProperty("_Beast_TessellationMinDistance", properties);
                _Beast_TessellationMaxDistance = BaseShaderGUI.FindProperty("_Beast_TessellationMaxDistance", properties);
                _Beast_TessellationEdgeLength = BaseShaderGUI.FindProperty("_Beast_TessellationEdgeLength", properties);
                _Beast_TessellationPhong = BaseShaderGUI.FindProperty("_Beast_TessellationPhong", properties);
                _Beast_TessellationDisplaceMap = BaseShaderGUI.FindProperty("_Beast_TessellationDisplaceMap", properties);
                _Beast_TessellationDisplaceMapUVSet = BaseShaderGUI.FindProperty("_Beast_TessellationDisplaceMapUVSet", properties);
                _Beast_TessellationDisplaceMapChannel = BaseShaderGUI.FindProperty("_Beast_TessellationDisplaceMapChannel", properties);
                _Beast_TessellationDisplaceStrength = BaseShaderGUI.FindProperty("_Beast_TessellationDisplaceStrength", properties);
                _Beast_TessellationTriplanarUVScale = BaseShaderGUI.FindProperty("_Beast_TessellationTriplanarUVScale", properties);
                _Beast_TessellationShadowPassLOD = BaseShaderGUI.FindProperty("_Beast_TessellationShadowPassLOD", properties);
                _Beast_TessellationDepthPassLOD = BaseShaderGUI.FindProperty("_Beast_TessellationDepthPassLOD", properties);
                _Beast_TessellationUseSmoothNormals = BaseShaderGUI.FindProperty("_Beast_TessellationUseSmoothNormals", properties);
                _Beast_Generate = BaseShaderGUI.FindProperty("_Beast_Generate", properties);
                _Beast_TessellationNormalCoef = BaseShaderGUI.FindProperty("_Beast_TessellationNormalCoef", properties);
                _Beast_TessellationTangentCoef = BaseShaderGUI.FindProperty("_Beast_TessellationTangentCoef", properties);

                _Beast_TessellationMaskMap_Mode = BaseShaderGUI.FindProperty("_Beast_TessellationMaskMap_Mode", properties);
                _Beast_TessellationMaskMap = BaseShaderGUI.FindProperty("_Beast_TessellationMaskMap", properties);
                _Beast_TessellationMaskMapOffset = BaseShaderGUI.FindProperty("_Beast_TessellationMaskMapOffset", properties);
                _Beast_TessellationMaskMapUVSet = BaseShaderGUI.FindProperty("_Beast_TessellationMaskMapUVSet", properties);
                _Beast_TessellationMaskMapChannel = BaseShaderGUI.FindProperty("_Beast_TessellationMaskMapChannel", properties);

                //Curved World
                _CurvedWorldBendSettings = BaseShaderGUI.FindProperty("_CurvedWorldBendSettings", properties, false);
            }
        }
        
        

        static MethodInfo curvedWorldSetKeywords = null;

        public static void DoBeastArea(BeastProperties properties, MaterialEditor materialEditor)
        {
            materialEditor.ShaderProperty(properties._Beast_Tessellation_Type, "Type");

            TessellationMode mode = (TessellationMode)properties._Beast_Tessellation_Type.floatValue;


            switch (mode)
            {
                case TessellationMode.Fixed:
                    materialEditor.RangeProperty(properties._Beast_TessellationFactor, "Factor");
                    break;

                case TessellationMode.DistanceBased:
                    materialEditor.RangeProperty(properties._Beast_TessellationFactor, "Factor");

                    using (new EditorGUIHelper.EditorGUIIndentLevel(1))
                    {
                        EditorGUI.BeginChangeCheck();
                        materialEditor.FloatProperty(properties._Beast_TessellationMinDistance, "Min Distance");
                        if (EditorGUI.EndChangeCheck())
                        {
                            if (properties._Beast_TessellationMinDistance.floatValue < 0)
                                properties._Beast_TessellationMinDistance.floatValue = 0;

                            if (properties._Beast_TessellationMinDistance.floatValue > properties._Beast_TessellationMaxDistance.floatValue)
                                properties._Beast_TessellationMaxDistance.floatValue = properties._Beast_TessellationMinDistance.floatValue;
                        }

                        EditorGUI.BeginChangeCheck();
                        materialEditor.FloatProperty(properties._Beast_TessellationMaxDistance, "Max Distance");
                        if (EditorGUI.EndChangeCheck())
                        {
                            if (properties._Beast_TessellationMaxDistance.floatValue < 0)
                                properties._Beast_TessellationMaxDistance.floatValue = 0;
                            if (properties._Beast_TessellationMaxDistance.floatValue < properties._Beast_TessellationMinDistance.floatValue)
                                properties._Beast_TessellationMinDistance.floatValue = properties._Beast_TessellationMaxDistance.floatValue;
                        }
                    }
                    break;

                case TessellationMode.EdgeLength:
                    materialEditor.RangeProperty(properties._Beast_TessellationEdgeLength, "Edge Length");
                    break;

                case TessellationMode.Phong:
                    materialEditor.RangeProperty(properties._Beast_TessellationEdgeLength, "Edge Length");
                    materialEditor.RangeProperty(properties._Beast_TessellationPhong, "Phong");
                    break;
            }

            if (mode != TessellationMode.Phong)
            {
                using (new EditorGUIHelper.EditorGUIUtilityFieldWidth(UnityEditor.EditorGUIUtility.fieldWidth * 2))
                {
                    materialEditor.TexturePropertySingleLine(new GUIContent("Displace Map"), properties._Beast_TessellationDisplaceMap);
                }
                materialEditor.ShaderProperty(properties._Beast_TessellationDisplaceMapChannel, new GUIContent("Channel"), 1);
                materialEditor.ShaderProperty(properties._Beast_TessellationDisplaceStrength, "Strength", 1);
                materialEditor.ShaderProperty(properties._Beast_Tessellation_UV_Mapping, "UV Mapping", 1);

                if ((UVMapping)properties._Beast_Tessellation_UV_Mapping.floatValue == UVMapping.Triplanar)
                {
                    materialEditor.ShaderProperty(properties._Beast_TessellationTriplanarUVScale, "UV Scale");
                }
                else
                {                    
                    materialEditor.ShaderProperty(properties._Beast_TessellationDisplaceMapUVSet, "UV Set", 1);
                    materialEditor.TextureScaleOffsetProperty(properties._Beast_TessellationDisplaceMap);
                }
            }

            GUILayout.Space(10);
            using (new EditorGUIHelper.EditorGUILayoutBeginVertical(EditorStyles.helpBox))
            {
                using (new EditorGUIHelper.EditorGUIUtilityFieldWidth(UnityEditor.EditorGUIUtility.fieldWidth * 2))
                {
                    bool maskMapEnabled = properties._Beast_TessellationMaskMap_Mode.floatValue > 0.5;
                    EditorGUI.BeginChangeCheck();
                    maskMapEnabled = EditorGUILayout.Toggle("Use Mask Map", maskMapEnabled);
                    if (EditorGUI.EndChangeCheck())
                    {
                        if (maskMapEnabled)
                        {
                            ((Material)materialEditor.target).SetFloat("_Beast_TessellationMaskMap_Mode", 1);
                            ((Material)materialEditor.target).EnableKeyword("_BEAST_TESSELLATION_MASK_MAP");
                        }
                        else
                        {
                            ((Material)materialEditor.target).SetFloat("_Beast_TessellationMaskMap_Mode", 0);
                            ((Material)materialEditor.target).DisableKeyword("_BEAST_TESSELLATION_MASK_MAP");
                        }
                    }

                    if (maskMapEnabled)
                    {
                        materialEditor.TexturePropertySingleLine(new GUIContent("Mask Map"), properties._Beast_TessellationMaskMap);
                        materialEditor.ShaderProperty(properties._Beast_TessellationMaskMapOffset, new GUIContent("Offset"), 1);
                        materialEditor.ShaderProperty(properties._Beast_TessellationMaskMapChannel, new GUIContent("Channel"), 1);
                        materialEditor.ShaderProperty(properties._Beast_TessellationMaskMapUVSet, "UV Set", 1);
                        materialEditor.TextureScaleOffsetProperty(properties._Beast_TessellationMaskMap);                        
                    }
                }
            }


            GUILayout.Space(5);
            materialEditor.ShaderProperty(properties._Beast_Generate, "Recalculate");

            switch ((Recalculate)properties._Beast_Generate.floatValue)
            {
                case Recalculate.Normals:
                    materialEditor.ShaderProperty(properties._Beast_TessellationNormalCoef, "   Normal Coef");
                    break;

                case Recalculate.Tangents:
                    materialEditor.ShaderProperty(properties._Beast_TessellationNormalCoef, "   Normal Coef");
                    materialEditor.ShaderProperty(properties._Beast_TessellationTangentCoef, "   Tangent Coef");
                    break;
            }


            materialEditor.RangeProperty(properties._Beast_TessellationShadowPassLOD, "Shadow Pass LOD");
            materialEditor.RangeProperty(properties._Beast_TessellationDepthPassLOD, "Depth Pass LOD");


            GUILayout.Space(5);
            bool useSmoothNormals = properties._Beast_TessellationUseSmoothNormals.floatValue > 0.5f;
            useSmoothNormals = EditorGUILayout.Toggle("Use Smooth Normals", useSmoothNormals);
                properties._Beast_TessellationUseSmoothNormals.floatValue = useSmoothNormals ? 1 : 0;


            if (useSmoothNormals)
            {
                if (materialEditor.HelpBoxWithButton(new GUIContent("Shader will use smooth normals from mesh UV4."), new GUIContent("Bake")))
                {
                    using (new EditorGUIHelper.GUIEnabled(Selection.activeGameObject != null))
                    {
                        BeastEditorWindow.ShowWindow();
                    }
                }
            }
        }

        public static void DoCurvedWorldArea(BeastProperties properties, MaterialEditor materialEditor)
        {
            if(properties._CurvedWorldBendSettings == null)
            {
                EditorGUILayout.HelpBox("Curved World is not installed.", MessageType.Warning);
            }
            else
            {
                materialEditor.ShaderProperty(properties._CurvedWorldBendSettings, "Bend Type");
            }
        }

        public static void SetMaterialKeywords(Material material)
        {
            if (material.HasProperty("_CurvedWorldBendSettings"))
            {
                //AmazingAssets.CurvedWorldEditor.MaterialProperties.SetKeyWords(material);


                if (curvedWorldSetKeywords == null)
                {
                    var mpType = Type.GetType("AmazingAssets.CurvedWorldEditor.MaterialProperties");

                    if (mpType != null)
                        curvedWorldSetKeywords = mpType.GetMethod("SetKeyWords", BindingFlags.Public | BindingFlags.Static);
                }

                if (curvedWorldSetKeywords != null)
                    curvedWorldSetKeywords.Invoke(curvedWorldSetKeywords, new object[] { material });
            }

            switch ((Recalculate)material.GetFloat("_Beast_Generate"))
            {
                case Recalculate.None:
                    material.DisableKeyword("_BEAST_GENERATE_NORMALS");
                    material.DisableKeyword("_BEAST_GENERATE_NORMALS_AND_TANGENT");
                    break;

                case Recalculate.Normals:
                    material.EnableKeyword("_BEAST_GENERATE_NORMALS");
                    material.DisableKeyword("_BEAST_GENERATE_NORMALS_AND_TANGENT");
                    break;

                case Recalculate.Tangents:
                    material.DisableKeyword("_BEAST_GENERATE_NORMALS");
                    material.EnableKeyword("_BEAST_GENERATE_NORMALS_AND_TANGENT");
                    break;
            }

            switch ((TessellationMode)material.GetFloat("_Beast_Tessellation_Type"))
            {
                case TessellationMode.Fixed:
                    material.EnableKeyword("_BEAST_TESSELLATION_TYPE_FIXED");
                    material.DisableKeyword("_BEAST_TESSELLATION_TYPE_DISTANCE_BASED");
                    material.DisableKeyword("_BEAST_TESSELLATION_TYPE_EDGE_LENGTH");
                    material.DisableKeyword("_BEAST_TESSELLATION_TYPE_PHONG");
                    break;

                case TessellationMode.DistanceBased:
                    material.EnableKeyword("_BEAST_TESSELLATION_TYPE_DISTANCE_BASED");
                    material.DisableKeyword("_BEAST_TESSELLATION_TYPE_FIXED");
                    material.DisableKeyword("_BEAST_TESSELLATION_TYPE_EDGE_LENGTH");
                    material.DisableKeyword("_BEAST_TESSELLATION_TYPE_PHONG");
                    break;

                case TessellationMode.EdgeLength:
                    material.EnableKeyword("_BEAST_TESSELLATION_TYPE_EDGE_LENGTH");
                    material.DisableKeyword("_BEAST_TESSELLATION_TYPE_FIXED");
                    material.DisableKeyword("_BEAST_TESSELLATION_TYPE_DISTANCE_BASED");
                    material.DisableKeyword("_BEAST_TESSELLATION_TYPE_PHONG");
                    break;

                case TessellationMode.Phong:
                    material.EnableKeyword("_BEAST_TESSELLATION_TYPE_PHONG");
                    material.DisableKeyword("_BEAST_TESSELLATION_TYPE_EDGE_LENGTH");
                    material.DisableKeyword("_BEAST_TESSELLATION_TYPE_FIXED");
                    material.DisableKeyword("_BEAST_TESSELLATION_TYPE_DISTANCE_BASED");
                    break;
            }

            if (material.GetFloat("_Beast_TessellationMaskMap_Mode") > 0.5f)
                material.EnableKeyword("_BEAST_TESSELLATION_MASK_MAP");
            else
                material.DisableKeyword("_BEAST_TESSELLATION_MASK_MAP");
        }
    }
}
