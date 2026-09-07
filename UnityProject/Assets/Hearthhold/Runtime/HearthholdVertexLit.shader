Shader "Hearthhold/VertexLit"
{
    Properties { _BaseColor("Tint", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
        CBUFFER_END
        struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; };
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half3 normalWS : TEXCOORD1; half4 color : COLOR; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half3 normal = normalize(input.normalWS);
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half diffuse = saturate(dot(normal, mainLight.direction));
                half3 illumination = max(SampleSH(normal), half3(0.22,0.25,0.24));
                illumination += mainLight.color * diffuse * lerp(0.3h,1.0h,mainLight.shadowAttenuation);
                return half4(input.color.rgb * _BaseColor.rgb * illumination,1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            float3 _LightDirection;
            float4 ShadowVert(Attributes input) : SV_POSITION
            {
                float3 position = TransformObjectToWorld(input.positionOS.xyz);
                float3 normal = TransformObjectToWorldNormal(input.normalOS);
                float4 clip = TransformWorldToHClip(ApplyShadowBias(position, normal, _LightDirection));
                #if UNITY_REVERSED_Z
                    clip.z = min(clip.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    clip.z = max(clip.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return clip;
            }
            half4 ShadowFrag() : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
