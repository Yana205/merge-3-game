// MagicalCrystal — animated procedural crystal for the merge-3 cells.
// Written in the built-in *sprite* shader structure (the same form
// Sprites/Default uses) so it renders reliably on a URP SpriteRenderer — the
// previous mesh-style URP pass compiled but never drew on the sprite cells.
//
// The sprite texture is used only as an alpha mask (the cell is a white square),
// so the whole cell is filled with the animated crystal:
//   1. Scrolling procedural noise      4. Pulsing brightness
//   2. Noise base<->glow colour blend  5. Ripple UV distortion
//   3. Breathing alpha                 6. Radial edge vignette
Shader "Merge3/MagicalCrystal"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint (vertex)", Color) = (1,1,1,1)

        [Header(Colors)]
        _BaseColor   ("Crystal Base Color", Color) = (0.16, 0.42, 0.90, 1)
        _GlowColor   ("Glow / Highlight Color", Color) = (0.55, 0.90, 1.0, 1)

        [Header(Noise)]
        _ScrollSpeed ("Scroll Speed", Float) = 0.40
        _NoiseScale  ("Noise Scale", Float) = 5.0

        [Header(Animation)]
        _PulseSpeed     ("Pulse Speed", Float) = 2.0
        _PulseStrength  ("Pulse Strength", Range(0,1)) = 0.20
        _RippleSpeed    ("Ripple Speed", Float) = 1.5
        _RippleStrength ("Ripple Strength", Range(0,0.2)) = 0.03

        [Header(Alpha)]
        _AlphaBase      ("Alpha Base", Range(0,1)) = 0.85
        _AlphaBreathe   ("Alpha Breathe", Range(0,1)) = 0.20

        [Header(Vignette)]
        _VignetteStrength ("Vignette Strength", Range(0,1)) = 0.35
    }

    SubShader
    {
        Tags
        {
            "Queue"            = "Transparent"
            "IgnoreProjector"  = "True"
            "RenderType"       = "Transparent"
            "PreviewType"      = "Plane"
            "CanUseSpriteAtlas"= "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _Color;
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

            // --- Procedural value noise (no texture) -----------------------
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
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = dot(hash2(i + float2(0, 0)), f - float2(0, 0));
                float b = dot(hash2(i + float2(1, 0)), f - float2(1, 0));
                float c = dot(hash2(i + float2(0, 1)), f - float2(0, 1));
                float d = dot(hash2(i + float2(1, 1)), f - float2(1, 1));
                float n = lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
                return n * 0.5 + 0.5;
            }

            float fbm(float2 p)
            {
                return valueNoise(p) * 0.65 + valueNoise(p * 2.03 + 7.3) * 0.35;
            }

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex   = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color    = IN.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float t = _Time.y;
                float2 uv = IN.texcoord;

                // (5) Ripple.
                uv.x += sin(uv.y * 12.0 + t * _RippleSpeed) * _RippleStrength;

                // (1) Scroll noise upward.
                float2 nUV = uv;
                nUV.y -= t * _ScrollSpeed;
                float n = fbm(nUV * _NoiseScale);

                // (2) Base -> glow blend by noise.
                float blend = smoothstep(0.35, 0.75, n);
                float3 col = lerp(_BaseColor.rgb, _GlowColor.rgb, blend);

                // (4) Pulsing brightness.
                float pulse = 1.0 + sin(t * _PulseSpeed) * _PulseStrength;
                col *= pulse;

                // (6) Radial vignette.
                float2 cc = uv - 0.5;
                float dist = length(cc) * 1.41421356;
                float vignette = 1.0 - smoothstep(0.55, 1.0, dist) * _VignetteStrength;
                col *= vignette;

                // (3) Breathing alpha, masked by the sprite (white square) & tint.
                float breathe = _AlphaBreathe * (0.5 + 0.5 * sin(t * _PulseSpeed * 0.5));
                float alpha = saturate((_AlphaBase + breathe) * (0.55 + 0.45 * n)) * vignette;
                float spriteA = tex2D(_MainTex, IN.texcoord).a * IN.color.a;

                return fixed4(col * IN.color.rgb, alpha * spriteA);
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}
