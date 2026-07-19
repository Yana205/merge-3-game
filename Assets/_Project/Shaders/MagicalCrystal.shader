// MagicalCrystal — URP HLSL shader for the merge-3 crystal cells.
// The "Magical Water" assignment, retimed for enchanted crystal: scrolling
// procedural noise, an Inspector-tinted base->glow blend, breathing alpha, and a
// few extra animated properties so the cells feel alive under the gems.
//
// Animated / interactive effects (>= 5):
//   1. Scrolling procedural noise (UV.y offset by _Time.y * _ScrollSpeed)
//   2. Noise-driven base<->glow color blend (smoothstep)
//   3. Breathing alpha (sin over time)
//   4. Pulsing brightness (sin over time)
//   5. Ripple UV distortion (sin on UV)
//   6. Edge vignette (radial falloff)
Shader "Merge3/MagicalCrystal"
{
    Properties
    {
        [Header(Colors)]
        _BaseColor   ("Crystal Base Color", Color) = (0.12, 0.35, 0.75, 1)
        _GlowColor   ("Glow / Highlight Color", Color) = (0.55, 0.95, 1.0, 1)

        [Header(Noise)]
        _ScrollSpeed ("Scroll Speed", Float) = 0.35
        _NoiseScale  ("Noise Scale", Float) = 5.0

        [Header(Animation)]
        _PulseSpeed     ("Pulse Speed", Float) = 2.0
        _PulseStrength  ("Pulse Strength", Range(0,1)) = 0.15
        _RippleSpeed    ("Ripple Speed", Float) = 1.5
        _RippleStrength ("Ripple Strength", Range(0,0.2)) = 0.03

        [Header(Alpha)]
        _AlphaBase      ("Alpha Base", Range(0,1)) = 0.7
        _AlphaBreathe   ("Alpha Breathe", Range(0,1)) = 0.25

        [Header(Vignette)]
        _VignetteStrength ("Vignette Strength", Range(0,1)) = 0.6
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "Queue"          = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector"= "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "CrystalForward"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _GlowColor;
                float  _ScrollSpeed;
                float  _NoiseScale;
                float  _PulseSpeed;
                float  _PulseStrength;
                float  _RippleSpeed;
                float  _RippleStrength;
                float  _AlphaBase;
                float  _AlphaBreathe;
                float  _VignetteStrength;
            CBUFFER_END

            // --- Procedural value noise (no texture) ---------------------------
            float2 hash2(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)),
                           dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453) * 2.0 - 1.0;
            }

            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);   // smoothstep interpolant

                float a = dot(hash2(i + float2(0, 0)), f - float2(0, 0));
                float b = dot(hash2(i + float2(1, 0)), f - float2(1, 0));
                float c = dot(hash2(i + float2(0, 1)), f - float2(0, 1));
                float d = dot(hash2(i + float2(1, 1)), f - float2(1, 1));

                float n = lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
                return n * 0.5 + 0.5;                 // -> [0,1]
            }

            // Two octaves for a richer, cloudier crystal shimmer.
            float fbm(float2 p)
            {
                return valueNoise(p) * 0.65 + valueNoise(p * 2.03 + 7.3) * 0.35;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float t = _Time.y;
                float2 uv = IN.uv;

                // (5) Ripple: horizontal wobble driven by sin on the V axis.
                uv.x += sin(uv.y * 12.0 + t * _RippleSpeed) * _RippleStrength;

                // (1) Scroll the noise field upward over time.
                float2 nUV = uv;
                nUV.y -= t * _ScrollSpeed;
                float n = fbm(nUV * _NoiseScale);

                // (2) Blend base -> glow by the noise value.
                float blend = smoothstep(0.35, 0.75, n);
                float3 col = lerp(_BaseColor.rgb, _GlowColor.rgb, blend);

                // (4) Pulsing brightness.
                float pulse = 1.0 + sin(t * _PulseSpeed) * _PulseStrength;
                col *= pulse;

                // (6) Radial edge vignette (darker + more transparent at the rim).
                float2 c = uv - 0.5;
                float dist = length(c) * 1.41421356;          // 0 center -> ~1 corner
                float vignette = 1.0 - smoothstep(0.55, 1.0, dist) * _VignetteStrength;

                // (3) Breathing alpha: base + a slow sin, modulated by noise + vignette.
                float breathe = _AlphaBreathe * (0.5 + 0.5 * sin(t * _PulseSpeed * 0.5));
                float alpha = saturate((_AlphaBase + breathe) * (0.55 + 0.45 * n));
                alpha *= _BaseColor.a * vignette;

                col *= vignette;
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/Unlit"
}
