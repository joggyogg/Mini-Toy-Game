
//https://docs.unity3d.com/Manual/SL-SurfaceShaderTessellation.html


Shader "Unity Tessellation" {
	Properties{
		_Glossiness("Smoothness", Range(0,1)) = 0.5
		_Metallic("Metallic", Range(0,1)) = 0.0

		_Color("Color", Color) = (1,1,1,1)
		[NoScaleOffset] _MainTex("Albedo (RGB)", 2D) = "white" {}
		_UVScale("UV Scale", float) = 1

		_Displacement ("Displacement", float) = 0.3
		[NoScaleOffset] _DispTex ("Disp Texture", 2D) = "black" {}
		_Tess("Tessellation", Range(1,32)) = 4		
		
	}
	SubShader {
		Tags { "RenderType"="Opaque" }
		LOD 200
		
		CGPROGRAM
		// Physically based Standard lighting model, and enable shadows on all light types
		#pragma surface surf Standard fullforwardshadows addshadow vertex:disp tessellate:tessFixed nolightmap

		// Use shader model 3.0 target, to get nicer looking lighting
		#pragma target 5.0
        #include "Tessellation.cginc"

            struct appdata {
                float4 vertex : POSITION;
                float4 tangent : TANGENT;
                float3 normal : NORMAL;
                float2 texcoord : TEXCOORD0;
            };


		float _Tess;
		float _UVScale;

		float4 tessFixed()
		{
			return _Tess;
		}

		sampler2D _DispTex;
		float _Displacement;

		void disp(inout appdata v)
		{
			float d = tex2Dlod(_DispTex, float4(v.texcoord.xy * _UVScale, 0, 0)).r * _Displacement;
			v.vertex.xyz += v.normal * d;
		}




		sampler2D _MainTex;

		struct Input {
			float2 uv_MainTex;
		};

		half _Glossiness;
		half _Metallic;
		fixed4 _Color;

		void surf (Input IN, inout SurfaceOutputStandard o) {
			// Albedo comes from a texture tinted by color
			fixed4 c = tex2D (_MainTex, IN.uv_MainTex * _UVScale) * _Color;
			o.Albedo = c.rgb;
			// Metallic and smoothness come from slider variables
			o.Metallic = _Metallic;
			o.Smoothness = _Glossiness;
			o.Alpha = c.a;
		}
		ENDCG
	}
	FallBack "Diffuse"
}
