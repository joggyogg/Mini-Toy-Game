// Beast - Advanced Tessellation Shader <http://u3d.as/JxL>
// Copyright (c) Amazing Assets <https://amazingassets.world>
 
#ifndef UNIVERSAL_BAKEDLIT_INPUT_INCLUDED
#define UNIVERSAL_BAKEDLIT_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    float4 _BaseMap_TexelSize;
    half4 _BaseColor;
    half _Cutoff;
    half _Glossiness;
    half _Metallic;
    half _Surface;
	UNITY_TEXTURE_STREAMING_DEBUG_VARS;


//Beast
float _Beast_TessellationFactor;
float _Beast_TessellationMinDistance;
float _Beast_TessellationMaxDistance;
float _Beast_TessellationEdgeLength;
float _Beast_TessellationPhong;
float4 _Beast_TessellationDisplaceMap_TexelSize;
half4 _Beast_TessellationDisplaceMap_ST;
half _Beast_TessellationDisplaceMapUVSet;
int _Beast_TessellationDisplaceMapChannel;
float _Beast_TessellationDisplaceStrength;
float _Beast_TessellationTriplanarUVScale;
float _Beast_TessellationNormalCoef;
float _Beast_TessellationTangentCoef;
float _Beast_TessellationShadowPassLOD;
float _Beast_TessellationDepthPassLOD;
float _Beast_TessellationUseSmoothNormals;

half4 _Beast_TessellationMaskMap_ST;
float _Beast_TessellationMaskMapOffset;
half _Beast_TessellationMaskMapUVSet; 
int _Beast_TessellationMaskMapChannel;

CBUFFER_END

TEXTURE2D(_Beast_TessellationDisplaceMap); SAMPLER(sampler_Beast_TessellationDisplaceMap);
TEXTURE2D(_Beast_TessellationMaskMap); SAMPLER(sampler_Beast_TessellationMaskMap);

#ifdef UNITY_DOTS_INSTANCING_ENABLED

    UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
        UNITY_DOTS_INSTANCED_PROP(float4, _BaseColor)
        UNITY_DOTS_INSTANCED_PROP(float , _Cutoff)
        UNITY_DOTS_INSTANCED_PROP(float , _Glossiness)
        UNITY_DOTS_INSTANCED_PROP(float , _Metallic)
        UNITY_DOTS_INSTANCED_PROP(float , _Surface)
    UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)

static float4 unity_DOTS_Sampled_BaseColor;
static float  unity_DOTS_Sampled_Cutoff;
static float  unity_DOTS_Sampled_Glossiness;
static float  unity_DOTS_Sampled_Metallic;
static float  unity_DOTS_Sampled_Surface;

void SetupDOTSBakedLitMaterialPropertyCaches()
{
    unity_DOTS_Sampled_BaseColor  = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _BaseColor);
    unity_DOTS_Sampled_Cutoff     = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _Cutoff);
    unity_DOTS_Sampled_Glossiness = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _Glossiness);
    unity_DOTS_Sampled_Metallic   = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _Metallic);
    unity_DOTS_Sampled_Surface    = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _Surface);
}

#undef UNITY_SETUP_DOTS_MATERIAL_PROPERTY_CACHES
#define UNITY_SETUP_DOTS_MATERIAL_PROPERTY_CACHES() SetupDOTSBakedLitMaterialPropertyCaches()

#define _BaseColor          unity_DOTS_Sampled_BaseColor
#define _Cutoff             unity_DOTS_Sampled_Cutoff
#define _Glossiness         unity_DOTS_Sampled_Glossiness
#define _Metallic           unity_DOTS_Sampled_Metallic
#define _Surface            unity_DOTS_Sampled_Surface

#endif

#endif
