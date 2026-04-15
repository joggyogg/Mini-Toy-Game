// Beast - Advanced Tessellation Shader <http://u3d.as/JxL>
// Copyright (c) Amazing Assets <https://amazingassets.world>
 
Shader "Amazing Assets/Beast/Baked Lit"
{
    Properties
    {
//[HideInInspector][CurvedWorldBendSettings] _CurvedWorldBendSettings("0|1|1", Vector) = (0, 0, 0, 0)

        [MainTexture] _BaseMap("Texture", 2D) = "white" {}
        [MainColor]   _BaseColor("Color", Color) = (1, 1, 1, 1)
        _Cutoff("AlphaCutout", Range(0.0, 1.0)) = 0.5
        _BumpMap("Normal Map", 2D) = "bump" {}

        // BlendMode
        _Surface("__surface", Float) = 0.0
        _Blend("__mode", Float) = 0.0
        _Cull("__cull", Float) = 2.0
        [ToggleUI] _AlphaClip("__clip", Float) = 0.0
        [HideInInspector] _BlendOp("__blendop", Float) = 0.0 
        [HideInInspector] _SrcBlend("__src", Float) = 1.0
        [HideInInspector] _DstBlend("__dst", Float) = 0.0 
        [HideInInspector] _SrcBlendAlpha("__srcA", Float) = 1.0
        [HideInInspector] _DstBlendAlpha("__dstA", Float) = 0.0
        [HideInInspector] _ZWrite("__zw", Float) = 1.0
        [HideInInspector] _AlphaToMask("__alphaToMask", Float) = 0.0
        [HideInInspector] _AddPrecomputedVelocity("_AddPrecomputedVelocity", Float) = 0.0
        [HideInInspector] _XRMotionVectorsPass("_XRMotionVectorsPass", Float) = 1.0

        // Editmode props
        _QueueOffset("Queue offset", Float) = 0.0

        [HideInInspector][NoScaleOffset]unity_Lightmaps("unity_Lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_LightmapsInd("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_ShadowMasks("unity_ShadowMasks", 2DArray) = "" {}

        //Tessellation
		[KeywordEnum(Fixed, Distance Based, Edge Length, Phong)] _Beast_Tessellation_Type ("", Float) = 0
        [KeywordEnum(Default, Triplanar)] _Beast_Tessellation_UV_Mapping ("", Float) = 0
		_Beast_TessellationFactor("", Range(1, 64)) = 4
		_Beast_TessellationMinDistance("", float) = 10
		_Beast_TessellationMaxDistance("", float) = 35
		_Beast_TessellationEdgeLength("", Range(2, 64)) = 16
		_Beast_TessellationPhong("", Range(0, 1)) = 0.5
		_Beast_TessellationDisplaceMap("", 2D) = "black" {}
		[Enum(UV0,0,UV1,1)] _Beast_TessellationDisplaceMapUVSet("", Float) = 0
		[Enum(Red,0, Green,1, Blue,2, Alpha,3)] _Beast_TessellationDisplaceMapChannel("", Float) = 0
	    _Beast_TessellationDisplaceStrength("", float) = 0
        _Beast_TessellationTriplanarUVScale("", float) = 1
		_Beast_TessellationShadowPassLOD("", Range(0, 1)) = 1
		_Beast_TessellationDepthPassLOD("", Range(0, 1)) = 1
		_Beast_TessellationUseSmoothNormals("", float) = 0
        [KeywordEnum(None, Normals, Normals And Tangent)] _Beast_Generate ("", Float) = 0
		_Beast_TessellationNormalCoef("", Float) = 1
		_Beast_TessellationTangentCoef("", Float) = 1

        _Beast_TessellationMaskMap_Mode("", float) = 0
        _Beast_TessellationMaskMap("", 2D) = "white" {}
        _Beast_TessellationMaskMapOffset("", Range(-1, 1)) = 0
        [Enum(UV0,0,UV1,1)] _Beast_TessellationMaskMapUVSet("", Float) = 0
        [Enum(Red,0, Green,1, Blue,2, Alpha,3)] _Beast_TessellationMaskMapChannel("", Float) = 0
    }

    SubShader
    {
        Tags 
        { 
        	"RenderType" = "Opaque" 
        	"IgnoreProjector" = "True" 
        	"RenderPipeline" = "UniversalPipeline" 
        	"ShaderModel"="4.6"
        }
        LOD 100

        // -------------------------------------
        // Render State Commands
        Blend [_SrcBlend][_DstBlend], [_SrcBlendAlpha][_DstBlendAlpha]
        ZWrite [_ZWrite]
        Cull [_Cull]

        Pass
        {
            Name "BakedLit"
            Tags
            {
                "LightMode" = "UniversalForwardOnly"
            }

            // -------------------------------------
            // Render State Commands
            AlphaToMask[_AlphaToMask]

            HLSLPROGRAM
            #pragma exclude_renderers gles gles3 glcore
            #pragma target 4.6
            #define UNITY_CAN_COMPILE_TESSELLATION

			// -------------------------------------
            // Shader Stages
            //#pragma vertex BakedLitForwardPassVertex
			#pragma vertex tessvert_surf       
			#pragma hull hs_surf
			#pragma domain ds_surf
            #pragma fragment BakedLitForwardPassFragment

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local_fragment _SURFACE_TYPE_TRANSPARENT
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _ALPHAMODULATE_ON

            // -------------------------------------
            // Universal Pipeline keywords
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fragment _ LIGHTMAP_BICUBIC_SAMPLING
            #pragma multi_compile_fragment _ REFLECTION_PROBE_ROTATION
            #pragma multi_compile _ USE_LEGACY_LIGHTMAPS
            #pragma multi_compile _ DEBUG_DISPLAY
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            // -------------------------------------
            // Includes            
            // Lighting include is needed because of GI
            #include "BeastBakedLitInput.hlsl"
            #include "BeastBakedLitForwardPass.hlsl" 


//#define CURVEDWORLD_BEND_TYPE_CLASSICRUNNER_X_POSITIVE
//#define CURVEDWORLD_BEND_ID_1
//#pragma shader_feature_local CURVEDWORLD_DISABLED_ON
//#pragma shader_feature_local CURVEDWORLD_NORMAL_TRANSFORMATION_ON
//#include "Assets/Amazing Assets/Curved World/Shaders/Core/CurvedWorldTransform.cginc"

#define _BEAST_TESSELLATION_PASS_UNIVERSAL_FORWARD_ONLY_BAKED
#pragma shader_feature_local _BEAST_TESSELLATION_TYPE_FIXED _BEAST_TESSELLATION_TYPE_DISTANCE_BASED _BEAST_TESSELLATION_TYPE_EDGE_LENGTH _BEAST_TESSELLATION_TYPE_PHONG
#pragma shader_feature_local _BEAST_TESSELLATION_UV_MAPPING_DEFAULT _BEAST_TESSELLATION_UV_MAPPING_TRIPLANAR
#pragma shader_feature_local _BEAST_TESSELLATION_MASK_MAP
#pragma shader_feature_local _ _BEAST_GENERATE_NORMALS _BEAST_GENERATE_NORMALS_AND_TANGENT
#include "Beast.cginc"

            ENDHLSL
        }

        Pass
        {
            Tags
            {
                "LightMode" = "DepthOnly"
            }

            // -------------------------------------
            // Render State Commands
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma exclude_renderers gles gles3 glcore
            #pragma target 4.6
            #define UNITY_CAN_COMPILE_TESSELLATION

			// -------------------------------------
            // Shader Stages
            //#pragma vertex DepthOnlyVertex
            #pragma vertex tessvert_surf       
			#pragma hull hs_surf
			#pragma domain ds_surf
            #pragma fragment DepthOnlyFragment

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _ALPHATEST_ON

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile _ LOD_FADE_CROSSFADE

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

			// -------------------------------------
            // Includes
            #include "BeastBakedLitInput.hlsl"
            #include "BeastDepthOnlyPass.hlsl"


//#define CURVEDWORLD_BEND_TYPE_CLASSICRUNNER_X_POSITIVE
//#define CURVEDWORLD_BEND_ID_1
//#pragma shader_feature_local CURVEDWORLD_DISABLED_ON
//#pragma shader_feature_local CURVEDWORLD_NORMAL_TRANSFORMATION_ON
//#include "Assets/Amazing Assets/Curved World/Shaders/Core/CurvedWorldTransform.cginc"

#define _BEAST_TESSELLATION_PASS_DEPTH_ONLY
#pragma shader_feature_local _BEAST_TESSELLATION_TYPE_FIXED _BEAST_TESSELLATION_TYPE_DISTANCE_BASED _BEAST_TESSELLATION_TYPE_EDGE_LENGTH _BEAST_TESSELLATION_TYPE_PHONG
#pragma shader_feature_local _BEAST_TESSELLATION_UV_MAPPING_DEFAULT _BEAST_TESSELLATION_UV_MAPPING_TRIPLANAR
#pragma shader_feature_local _BEAST_TESSELLATION_MASK_MAP
#pragma shader_feature_local _ _BEAST_GENERATE_NORMALS _BEAST_GENERATE_NORMALS_AND_TANGENT
#include "Beast.cginc"

            ENDHLSL
        }

        // This pass is used when drawing to a _CameraNormalsTexture texture with the forward renderer or the depthNormal prepass with the deferred renderer.
        Pass
        {
            Name "DepthNormalsOnly"
            Tags
            {
                "LightMode" = "DepthNormalsOnly"
            }

            // -------------------------------------
            // Render State Commands
            ZWrite On
            Cull[_Cull]
             
            HLSLPROGRAM
            #pragma exclude_renderers gles gles3 glcore
            #pragma target 4.6
            #define UNITY_CAN_COMPILE_TESSELLATION

			// -------------------------------------
            // Shader Stages
            //#pragma vertex DepthNormalsVertex
            #pragma vertex tessvert_surf       
			#pragma hull hs_surf
			#pragma domain ds_surf
            #pragma fragment DepthNormalsFragment

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _ _NORMALMAP
            #pragma shader_feature_local_fragment _ALPHATEST_ON

            // -------------------------------------
            // Universal Pipeline keywords
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT // forward-only variant
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

			// -------------------------------------
            // Includes
            #include "BeastBakedLitInput.hlsl"
            #include "BeastBakedLitDepthNormalsPass.hlsl"


//#define CURVEDWORLD_BEND_TYPE_CLASSICRUNNER_X_POSITIVE
//#define CURVEDWORLD_BEND_ID_1
//#pragma shader_feature_local CURVEDWORLD_DISABLED_ON
//#pragma shader_feature_local CURVEDWORLD_NORMAL_TRANSFORMATION_ON
//#include "Assets/Amazing Assets/Curved World/Shaders/Core/CurvedWorldTransform.cginc"

#define _BEAST_TESSELLATION_PASS_DEPTH_NORMALS_ONLY
#pragma shader_feature_local _BEAST_TESSELLATION_TYPE_FIXED _BEAST_TESSELLATION_TYPE_DISTANCE_BASED _BEAST_TESSELLATION_TYPE_EDGE_LENGTH _BEAST_TESSELLATION_TYPE_PHONG
#pragma shader_feature_local _BEAST_TESSELLATION_UV_MAPPING_DEFAULT _BEAST_TESSELLATION_UV_MAPPING_TRIPLANAR
#pragma shader_feature_local _BEAST_TESSELLATION_MASK_MAP
#pragma shader_feature_local _ _BEAST_GENERATE_NORMALS _BEAST_GENERATE_NORMALS_AND_TANGENT
#include "Beast.cginc"

            ENDHLSL
        }

        // Same as DepthNormals pass, but used for deferred renderer and forwardOnly materials.
        Pass
        {
            Name "DepthNormalsOnly"
            Tags
            {
                "LightMode" = "DepthNormalsOnly"
            }

            // -------------------------------------
            // Render State Commands
            ZWrite On
            Cull[_Cull]

            HLSLPROGRAM
            #pragma exclude_renderers gles gles3 glcore
            #pragma target 4.6
            #define UNITY_CAN_COMPILE_TESSELLATION

			// -------------------------------------
            // Shader Stages
            //#pragma vertex DepthNormalsVertex
            #pragma vertex tessvert_surf       
			#pragma hull hs_surf
			#pragma domain ds_surf
            #pragma fragment DepthNormalsFragment

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _ _NORMALMAP
            #pragma shader_feature_local_fragment _ALPHATEST_ON

            // -------------------------------------
            // Universal Pipeline keywords
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT // forward-only variant
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            //--------------------------------------
            // Defines
            #define BUMP_SCALE_NOT_SUPPORTED 1

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

			// -------------------------------------
            // Includes
            #include "BeastBakedLitInput.hlsl"
            #include "BeastDepthNormalsPass.hlsl"


//#define CURVEDWORLD_BEND_TYPE_CLASSICRUNNER_X_POSITIVE
//#define CURVEDWORLD_BEND_ID_1
//#pragma shader_feature_local CURVEDWORLD_DISABLED_ON
//#pragma shader_feature_local CURVEDWORLD_NORMAL_TRANSFORMATION_ON
//#include "Assets/Amazing Assets/Curved World/Shaders/Core/CurvedWorldTransform.cginc"

#define _BEAST_TESSELLATION_PASS_DEPTH_NORMALS_ONLY
#pragma shader_feature_local _BEAST_TESSELLATION_TYPE_FIXED _BEAST_TESSELLATION_TYPE_DISTANCE_BASED _BEAST_TESSELLATION_TYPE_EDGE_LENGTH _BEAST_TESSELLATION_TYPE_PHONG
#pragma shader_feature_local _BEAST_TESSELLATION_UV_MAPPING_DEFAULT _BEAST_TESSELLATION_UV_MAPPING_TRIPLANAR
#pragma shader_feature_local _BEAST_TESSELLATION_MASK_MAP
#pragma shader_feature_local _ _BEAST_GENERATE_NORMALS _BEAST_GENERATE_NORMALS_AND_TANGENT
#include "Beast.cginc"

            ENDHLSL
        }

        // This pass it not used during regular rendering, only for lightmap baking.
        Pass
        {
            Name "Meta"
            Tags
            {
                "LightMode" = "Meta"
            }

            // -------------------------------------
            // Render State Commands
            Cull Off

            HLSLPROGRAM
            #pragma exclude_renderers gles gles3 glcore 
            #pragma target 4.5

			// -------------------------------------
            // Shader Stages
            #pragma vertex UniversalVertexMeta
            #pragma fragment UniversalFragmentMetaUnlit

            // -------------------------------------
            // Unity defined keywords
            #pragma shader_feature EDITOR_VISUALIZATION

			// -------------------------------------
            // Includes
            #include "BeastBakedLitInput.hlsl"
            #include "BeastBakedLitMetaPass.hlsl"

            ENDHLSL
        }

        Pass
        {
            Name "Universal2D"
            Tags
            {
                "LightMode" = "Universal2D"
            }

            // -------------------------------------
            // Render State Commands
            Blend[_SrcBlend][_DstBlend]
            ZWrite[_ZWrite]
            Cull[_Cull]

            HLSLPROGRAM
            #pragma exclude_renderers gles gles3 glcore
            #pragma target 4.5

            // -------------------------------------
            // Shader Stages
            #pragma vertex vert
            #pragma fragment frag

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _ALPHAPREMULTIPLY_ON

            // -------------------------------------
            // Includes
            #include "BeastBakedLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/Utils/Universal2D.hlsl"

            ENDHLSL
        }

        Pass
        {
            Name "MotionVectors"
            Tags { "LightMode" = "MotionVectors" }
            ColorMask RG

            HLSLPROGRAM
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma shader_feature_local_vertex _ADD_PRECOMPUTED_VELOCITY

            #include "Packages/com.unity.render-pipelines.universal/Shaders/BakedLitInput.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ObjectMotionVectors.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "XRMotionVectors"
            Tags { "LightMode" = "XRMotionVectors" }
            ColorMask RGBA

            // Stencil write for obj motion pixels
            Stencil
            {
                WriteMask 1
                Ref 1
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma shader_feature_local_vertex _ADD_PRECOMPUTED_VELOCITY
            #define APPLICATION_SPACE_WARP_MOTION 1

            #include "Packages/com.unity.render-pipelines.universal/Shaders/BakedLitInput.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ObjectMotionVectors.hlsl"
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Baked Lit"
    CustomEditor "AmazingAssets.Beast.Editor.Universal.ShaderGUI.BakedLitShader"
}
