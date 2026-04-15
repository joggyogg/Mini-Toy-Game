Shader "UI/JunctionGateWheel"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 texcoord      : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float4 _ClipRect;
            float4 _MainTex_ST;

            // ── Junction parameters (set from C#) ──
            float4 _GroupAngles;     // group centre angles in radians
            float4 _BoundaryAngles;  // boundary angles in radians
            float4 _ExitCounts;      // exit count per group (float)
            float4 _ActiveIndices;   // active pip index per group (float)
            float  _JunctionId;      // -1 = none

            // ── Constants matching the original CPU renderer ──
            #define WHEEL_R    0.46
            #define BORDER_W   0.025
            #define ARC_OUTER  (WHEEL_R - BORDER_W)
            #define ARC_INNER  0.22
            #define GAP_HALF   3.0     // half-gap at group boundary (degrees)
            #define PIP_GAP    2.5     // gap between pips (degrees)
            #define PALE_BLEND 0.68
            #define RAD2DEG    57.2957795
            #define DEG2RAD    0.0174532925

            static const half4 _GCol[4] =
            {
                half4(1.00, 0.85, 0.10, 1),   // 0 Yellow
                half4(0.10, 0.70, 0.15, 1),   // 1 Green
                half4(0.10, 0.50, 0.85, 1),   // 2 Blue
                half4(0.95, 0.30, 0.10, 1)    // 3 Orange/Red
            };
            static const half4 _CenterCol = half4(0.30, 0.30, 0.30, 1);

            // ── Helpers ──

            // Index a float4 by a variable int (safe on all platforms).
            float idx4(float4 v, int i)
            {
                if (i == 0) return v.x;
                if (i == 1) return v.y;
                if (i == 2) return v.z;
                return v.w;
            }

            half4 groupColor(int i)
            {
                if (i == 0) return _GCol[0];
                if (i == 1) return _GCol[1];
                if (i == 2) return _GCol[2];
                return _GCol[3];
            }

            // Signed angle difference in degrees, result in [-180, 180].
            float deltaAngle(float a, float b)
            {
                float d = b - a;
                d = d - 360.0 * floor((d + 180.0) / 360.0);
                return d;
            }

            // ── 3x5 bitmap font (15 bits per digit) ──
            // Bits 14..12 = row 0, 11..9 = row 1, …, 2..0 = row 4.
            // Within each row: bit 2 = left, bit 1 = centre, bit 0 = right.
            static const uint _Digits[10] =
            {
                31599u,  // 0
                11415u,  // 1
                29671u,  // 2
                29647u,  // 3
                23497u,  // 4
                31183u,  // 5
                31215u,  // 6
                29330u,  // 7
                31727u,  // 8
                31695u   // 9
            };

            float sampleDigit(int digit, int col, int row)
            {
                if (digit < 0 || digit > 9) return 0;
                if ((uint)col > 2u || (uint)row > 4u) return 0;
                uint bits = _Digits[digit];
                int bitIdx = (4 - row) * 3 + (2 - col);
                return (float)((bits >> bitIdx) & 1u);
            }

            // Draw a multi-digit number centred at `centre` in UV space.
            float drawNumber(float2 uv, float2 centre, float cell, int number)
            {
                if (number < 0) return 0;

                int count = 1;
                if (number >= 100) count = 3;
                else if (number >= 10) count = 2;

                float digitW = cell * 4.0;          // 3 cells + 1 gap
                float totalW = count * digitW - cell;
                float totalH = cell * 5.0;

                float2 origin = centre - float2(totalW * 0.5, totalH * 0.5);
                float2 local  = uv - origin;

                if (local.x < 0 || local.y < 0 || local.x >= totalW || local.y >= totalH)
                    return 0;

                int slot = (int)(local.x / digitW);
                if (slot >= count) return 0;

                float slotX = local.x - slot * digitW;
                if (slotX >= cell * 3.0) return 0;   // in gap

                int col = (int)(slotX / cell);
                int row = (int)(local.y / cell);

                int d;
                if (count == 3)      d = (slot == 0) ? (number / 100) % 10
                                       : (slot == 1) ? (number / 10) % 10
                                       : number % 10;
                else if (count == 2) d = (slot == 0) ? (number / 10) % 10
                                       : number % 10;
                else                 d = number % 10;

                return sampleDigit(d, col, row);
            }

            // ── Vertex / Fragment ──

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition = v.vertex;
                o.vertex   = UnityObjectToClipPos(o.worldPosition);
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color    = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // UV centred at origin, range -0.5..0.5.
                float2 uv = IN.texcoord - 0.5;
                uv.y = -uv.y;                  // match CPU renderer Y convention

                float dist = length(uv);

                // Per-pixel anti-aliasing width.
                float aa = fwidth(dist) * 1.5;

                // Outside the wheel — fully transparent.
                float wheelMask = 1.0 - smoothstep(WHEEL_R - aa, WHEEL_R + aa, dist);
                if (wheelMask < 0.001) return fixed4(0,0,0,0);

                half4 col = half4(1,1,1,1);     // default: white border / gap

                // ── Arc band ──
                float arcMask = smoothstep(ARC_INNER - aa, ARC_INNER + aa, dist)
                              * (1.0 - smoothstep(ARC_OUTER - aa, ARC_OUTER + aa, dist));

                if (arcMask > 0.01)
                {
                    float ang = atan2(-uv.y, uv.x) * RAD2DEG;

                    // Group / boundary angles in degrees.
                    float gDeg[4], bDeg[4];
                    gDeg[0] = _GroupAngles.x    * RAD2DEG;
                    gDeg[1] = _GroupAngles.y    * RAD2DEG;
                    gDeg[2] = _GroupAngles.z    * RAD2DEG;
                    gDeg[3] = _GroupAngles.w    * RAD2DEG;
                    bDeg[0] = _BoundaryAngles.x * RAD2DEG;
                    bDeg[1] = _BoundaryAngles.y * RAD2DEG;
                    bDeg[2] = _BoundaryAngles.z * RAD2DEG;
                    bDeg[3] = _BoundaryAngles.w * RAD2DEG;

                    // Find nearest group.
                    int best = 0;
                    float bestD = 999.0;
                    [unroll] for (int g = 0; g < 4; g++)
                    {
                        float d = abs(deltaAngle(ang, gDeg[g]));
                        if (d < bestD) { bestD = d; best = g; }
                    }

                    // Boundary-gap check.
                    float minBound = 999.0;
                    [unroll] for (int b = 0; b < 4; b++)
                    {
                        float d2 = abs(deltaAngle(ang, bDeg[b]));
                        minBound = min(minBound, d2);
                    }

                    bool inGap = minBound < GAP_HALF;

                    int N         = (int)round(idx4(_ExitCounts,   best));
                    int activeIdx = (int)round(idx4(_ActiveIndices, best));

                    if (!inGap && N > 0)
                    {
                        float centerDeg  = gDeg[best];
                        float halfSpan   = abs(deltaAngle(centerDeg, bDeg[best]));
                        float availHalf  = halfSpan - GAP_HALF;
                        float delt       = deltaAngle(centerDeg, ang);

                        if (abs(delt) <= availHalf)
                        {
                            float totalSpan = availHalf * 2.0;
                            float totalGaps = (N - 1) * PIP_GAP;
                            float pipW      = (totalSpan - totalGaps) / (float)N;
                            float localPos  = delt + availHalf;

                            float accum = 0;
                            int pipIdx = -1;
                            [loop] for (int p = 0; p < 16; p++)
                            {
                                if (p >= N) break;
                                if (localPos >= accum && localPos < accum + pipW)
                                {
                                    pipIdx = p;
                                    break;
                                }
                                accum += pipW + PIP_GAP;
                            }

                            if (pipIdx >= 0)
                            {
                                half4 vivid = groupColor(best);
                                col = ((N - 1 - pipIdx) == activeIdx)
                                    ? vivid
                                    : lerp(vivid, half4(1,1,1,1), PALE_BLEND);
                            }
                        }
                    }

                    // Blend arc result with white base at arc edges.
                    col = lerp(half4(1,1,1, col.a), col, arcMask);
                }

                // ── Centre number ──
                int jId = (int)round(_JunctionId);
                if (jId >= 0 && dist < ARC_INNER)
                {
                    float cell    = 2.0 / 256.0;   // match original scale=2 on 256 texture
                    float numMask = drawNumber(uv, float2(0,0), cell, jId);
                    col = lerp(col, _CenterCol, numMask);
                }

                col.a *= wheelMask;

                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a - 0.001);
                #endif

                return col * IN.color;
            }
            ENDCG
        }
    }
}
