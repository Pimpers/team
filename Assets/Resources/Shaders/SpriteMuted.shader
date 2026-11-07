// A sprite with less saturation (lerped toward its own grey) and a little darker: the
// stage's trees and bushes sit back behind the fight, their PNGs untouched. Used by ParallaxBackground; lives in
// Resources so Shader.Find finds it in a build.
// One pass (Universal2D, the 2D renderer's). The tint is applied after the muting (see frag).
Shader "CatsSmash/SpriteMuted"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Saturation ("Saturation", Range(0, 1)) = 0.65
        _Brightness ("Brightness", Range(0, 1)) = 0.9
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off ZWrite Off

        Pass
        {
            Tags { "LightMode" = "Universal2D" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _Saturation;
                float _Brightness;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                // the picture loses colour, then the renderer's tint goes on: muting after the tint turned the
                // stage's pink-red mood haze (a near-white PNG tinted) into a pale grey fog that read brighter
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                half grey = dot(c.rgb, half3(0.3, 0.59, 0.11));
                c.rgb = lerp(grey.xxx, c.rgb, _Saturation) * _Brightness;
                return c * i.color;
            }
            ENDHLSL
        }
    }
}
