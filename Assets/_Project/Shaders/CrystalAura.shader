// CrystalAura — a ShaderToy GLSL plasma ported to a Built-in RP CG shader.
//
// Source shape (classic ShaderToy "plasma", public-domain pattern):
//   void mainImage(out vec4 O, vec2 U){
//     vec2 uv = U/iResolution.xy;
//     float t = iTime;
//     float v = sin(uv.x*10.+t) + sin(uv.y*10.+t)
//             + sin((uv.x+uv.y)*10.+t) + sin(length(uv-.5)*20.-t);
//     vec3 col = .5+.5*cos(vec3(0,2,4)+v);
//     O = vec4(col,1);
//   }
//
// Port notes: iResolution/iTime -> UV already 0..1 + _Time.y; fragCoord math
// dropped; two Inspector tints replace the fixed cos() palette; _Intensity is
// exposed so a C# MonoBehaviour can push it at runtime (see CrystalAuraController).
//
// BUILT-IN RP NOTE: this project uses the Built-in Render Pipeline (no URP
// package installed). An earlier URP version of this shader failed to compile
// ("Couldn't open include file .../universal/ShaderLibrary/Core.hlsl") and was
// stripped entirely from WebGL builds ("All subshaders removed"), so the aura
// vanished in browser builds. Keep this shader on UnityCG.cginc.
Shader "Merge3/CrystalAura"
{
    Properties
    {
        _TintA     ("Tint A", Color) = (0.20, 0.45, 0.95, 1)
        _TintB     ("Tint B", Color) = (0.85, 0.55, 1.00, 1)
        _Speed     ("Speed", Float) = 1.0
        _Scale     ("Pattern Scale", Float) = 10.0
        _Intensity ("Intensity (driven at runtime)", Range(0,2)) = 1.0
        _Alpha     ("Alpha", Range(0,1)) = 0.85
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "Transparent"
            "Queue"           = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType"     = "Plane"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "AuraForward"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f     { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            float4 _TintA;
            float4 _TintB;
            float  _Speed;
            float  _Scale;
            float  _Intensity;
            float  _Alpha;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float t = _Time.y * _Speed;

                // Ported plasma sum.
                float v = sin(uv.x * _Scale + t)
                        + sin(uv.y * _Scale + t)
                        + sin((uv.x + uv.y) * _Scale + t)
                        + sin(length(uv - 0.5) * (_Scale * 2.0) - t);

                // v ~ [-4,4] -> [0,1] wave, sharpened by _Intensity (the runtime knob).
                float w = saturate((v * 0.25) * 0.5 + 0.5);
                w = pow(w, max(0.0001, 2.0 - _Intensity));   // higher intensity => brighter/wider

                float3 col = lerp(_TintA.rgb, _TintB.rgb, w) * _Intensity;
                float alpha = _Alpha * saturate(0.35 + 0.65 * w);
                return fixed4(col, alpha);
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}
