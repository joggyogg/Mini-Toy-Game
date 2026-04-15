// Beast - Advanced Tessellation Shader <http://u3d.as/JxL>
// Copyright (c) Amazing Assets <https://amazingassets.world>
 
using System;
using System.Linq;

using UnityEngine;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEngine.Rendering;
using System.Reflection;
using System.Collections.Generic;
using System.Collections;

namespace AmazingAssets.Beast.Editor.Universal.ShaderGUI
{
    internal class LitShader : BaseShaderGUI
    {
        static readonly string[] workflowModeNames = Enum.GetNames(typeof(LitGUI.WorkflowMode));

        private LitGUI.LitProperties litProperties;
        private LitDetailGUI.LitProperties litDetailProperties;


        //Beast
        private PropertyDrawer.BeastProperties beastProperties;


        public override void FillAdditionalFoldouts(MaterialHeaderScopeList materialScopesList)
        {
            materialScopesList.RegisterHeaderScope(LitDetailGUI.Styles.detailInputs, Expandable.Details, _ => LitDetailGUI.DoDetailArea(litDetailProperties, materialEditor));
            materialScopesList.RegisterHeaderScope(PropertyDrawer.Styles.beastHeader, PropertyDrawer.Expandable.Beast, _ => PropertyDrawer.DoBeastArea(beastProperties, materialEditor));

            //Assign help URL
            {
                FieldInfo fieldInfo = typeof(MaterialHeaderScopeList).GetField("m_Items", BindingFlags.NonPublic | BindingFlags.Instance);
                if (fieldInfo != null && typeof(IList).IsAssignableFrom(fieldInfo.FieldType))
                {
                    var m_Items = (IList)fieldInfo.GetValue(materialScopesList);
                    if (m_Items != null && m_Items.Count > 0)
                    {
                        var lastElement = m_Items[m_Items.Count - 1];

                        PropertyInfo urlProperty = lastElement.GetType().GetProperty("url", BindingFlags.Public | BindingFlags.Instance);
                        if (urlProperty != null)
                        {
                            urlProperty.SetValue(lastElement, "https://amazing-assets.gitbook.io/beast-advanced-tessellation-shader");

                            m_Items[m_Items.Count - 1] = lastElement;
                        }
                    }
                }
            }

            if (beastProperties._CurvedWorldBendSettings != null)
                materialScopesList.RegisterHeaderScope(PropertyDrawer.Styles.curvedWorldHeader, PropertyDrawer.Expandable.CurvedWorld, _ => PropertyDrawer.DoCurvedWorldArea(beastProperties, materialEditor));
        }

        // collect properties from the material properties
        public override void FindProperties(MaterialProperty[] properties)
        {
            base.FindProperties(properties);
            litProperties = new LitGUI.LitProperties(properties);
            litDetailProperties = new LitDetailGUI.LitProperties(properties);

            //Beast
            beastProperties = new PropertyDrawer.BeastProperties(properties);
        }

        // material changed check
        public override void ValidateMaterial(Material material)
        {
            SetMaterialKeywords(material, LitGUI.SetMaterialKeywords, LitDetailGUI.SetMaterialKeywords);

            //Beast
            PropertyDrawer.SetMaterialKeywords(material);
        }

        // material main surface options
        public override void DrawSurfaceOptions(Material material)
        {
            // Use default labelWidth
            EditorGUIUtility.labelWidth = 0f;

            if (litProperties.workflowMode != null)
                DoPopup(LitGUI.Styles.workflowModeText, litProperties.workflowMode, workflowModeNames);

            base.DrawSurfaceOptions(material);
        }

        // material main surface inputs
        public override void DrawSurfaceInputs(Material material)
        {
            base.DrawSurfaceInputs(material);
            LitGUI.Inputs(litProperties, materialEditor, material);
            DrawEmissionProperties(material, true);
            DrawTileOffset(materialEditor, baseMapProp);
        }

        // material main advanced options
        public override void DrawAdvancedOptions(Material material)
        {
            if (litProperties.reflections != null && litProperties.highlights != null)
            {
                materialEditor.ShaderProperty(litProperties.highlights, LitGUI.Styles.highlightsText);
                materialEditor.ShaderProperty(litProperties.reflections, LitGUI.Styles.reflectionsText);
            }

            base.DrawAdvancedOptions(material);
        }

        public override void AssignNewShaderToMaterial(Material material, Shader oldShader, Shader newShader)
        {
            if (material == null)
                throw new ArgumentNullException("material");

            // _Emission property is lost after assigning Standard shader to the material
            // thus transfer it before assigning the new shader
            if (material.HasProperty("_Emission"))
            {
                material.SetColor("_EmissionColor", material.GetColor("_Emission"));
            }

            base.AssignNewShaderToMaterial(material, oldShader, newShader);

            if (oldShader == null || !oldShader.name.Contains("Legacy Shaders/"))
            {
                SetupMaterialBlendMode(material);
                return;
            }

            SurfaceType surfaceType = SurfaceType.Opaque;
            BlendMode blendMode = BlendMode.Alpha;
            if (oldShader.name.Contains("/Transparent/Cutout/"))
            {
                surfaceType = SurfaceType.Opaque;
                material.SetFloat("_AlphaClip", 1);
            }
            else if (oldShader.name.Contains("/Transparent/"))
            {
                // NOTE: legacy shaders did not provide physically based transparency
                // therefore Fade mode
                surfaceType = SurfaceType.Transparent;
                blendMode = BlendMode.Alpha;
            }
            material.SetFloat("_Blend", (float)blendMode);

            material.SetFloat("_Surface", (float)surfaceType);
            if (surfaceType == SurfaceType.Opaque)
            {
                material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            else
            {
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }

            if (oldShader.name.Equals("Standard (Specular setup)"))
            {
                material.SetFloat("_WorkflowMode", (float)LitGUI.WorkflowMode.Specular);
                Texture texture = material.GetTexture("_SpecGlossMap");
                if (texture != null)
                    material.SetTexture("_MetallicSpecGlossMap", texture);
            }
            else
            {
                material.SetFloat("_WorkflowMode", (float)LitGUI.WorkflowMode.Metallic);
                Texture texture = material.GetTexture("_MetallicGlossMap");
                if (texture != null)
                    material.SetTexture("_MetallicSpecGlossMap", texture);
            }
        }
    }
}
