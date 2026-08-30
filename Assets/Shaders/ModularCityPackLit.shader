Shader "CYDOY/Modular City Pack Lit"
{
    Properties
    {
        _MainTex ("Base Color", 2D) = "white" {}
        [Normal] _BumpMap ("Normal", 2D) = "bump" {}
        _BumpScale ("Normal Strength", Range(0, 2)) = 1
        _MetallicMap ("Metallic", 2D) = "black" {}
        _RoughnessMap ("Roughness", 2D) = "white" {}
        _Metallic ("Metallic Strength", Range(0, 1)) = 1
        _Smoothness ("Smoothness Strength", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 300

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;
        sampler2D _BumpMap;
        sampler2D _MetallicMap;
        sampler2D _RoughnessMap;
        half _BumpScale;
        half _Metallic;
        half _Smoothness;

        struct Input
        {
            float2 uv_MainTex;
        };

        void surf(Input input, inout SurfaceOutputStandard output)
        {
            fixed4 baseColor = tex2D(_MainTex, input.uv_MainTex);
            half metallic = tex2D(_MetallicMap, input.uv_MainTex).r;
            half roughness = tex2D(_RoughnessMap, input.uv_MainTex).r;
            output.Albedo = baseColor.rgb;
            output.Normal = UnpackScaleNormal(tex2D(_BumpMap, input.uv_MainTex), _BumpScale);
            output.Metallic = saturate(metallic * _Metallic);
            output.Smoothness = saturate((1.0h - roughness) * _Smoothness);
            output.Alpha = baseColor.a;
        }
        ENDCG
    }
    FallBack "Standard"
}
