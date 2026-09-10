Shader "Hearthhold/ChromaKeySprite"
{
    Properties
    {
        _BaseMap("Atlas", 2D) = "white" {}
        _BaseColor("Tint", Color) = (1,1,1,1)
        _KeyColor("Chroma key", Color) = (1,0,1,1)
        _Threshold("Key threshold", Range(0.02,0.5)) = 0.12
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("Depth test", Float) = 4
        [Enum(Off,0,On,1)] _ZWrite("Depth write", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode"="UniversalForward" }
            Cull Off ZTest [_ZTest] ZWrite [_ZWrite] Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _KeyColor;
                half _Threshold;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half chroma = min(color.r, color.b) - color.g;
                half alpha = 1.0h - smoothstep(_Threshold, _Threshold + 0.3h, chroma);
                clip(alpha - 0.025h);
                half spill = max(0, chroma) * 0.85h;
                color.r = max(0, color.r - spill);
                color.b = max(0, color.b - spill);
                return half4(color.rgb * _BaseColor.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
