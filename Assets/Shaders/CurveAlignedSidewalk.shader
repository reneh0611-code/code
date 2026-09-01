Shader "CYDOY/Curve Aligned Sidewalk"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Base Color", 2D) = "white" {}
        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Strength", Range(0,2)) = 0.72
        _OcclusionMap ("Ambient Occlusion", 2D) = "white" {}
        _OcclusionStrength ("Occlusion Strength", Range(0,1)) = 0.85
        _Smoothness ("Smoothness", Range(0,1)) = 0.16
        _CenterSeamHalfWidth ("Center Seam Half Width", Range(0.0001,0.01)) = 0.00065
        _CenterSeamDarkness ("Center Seam Darkness", Range(0,1)) = 0.72
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 300

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert addshadow
        #pragma target 3.0

        #include "UnityCG.cginc"

        sampler2D _MainTex;
        sampler2D _BumpMap;
        sampler2D _OcclusionMap;
        fixed4 _Color;
        half _BumpScale;
        half _OcclusionStrength;
        half _Smoothness;
        half _CenterSeamHalfWidth;
        half _CenterSeamDarkness;

        struct Input
        {
            float2 uv_MainTex;
            float3 meshWorldNormal;
        };

        void vert(inout appdata_full vertex, out Input output)
        {
            UNITY_INITIALIZE_OUTPUT(Input, output);
            output.meshWorldNormal = UnityObjectToWorldNormal(vertex.normal);
        }

        void surf(Input input, inout SurfaceOutputStandard output)
        {
            fixed4 baseColor = tex2D(_MainTex, input.uv_MainTex) * _Color;

            // U=0 is the geometrically calculated center of the sidewalk. Add one
            // continuous grout line there, independent of the source texture's bond pattern.
            half distanceToCenter = abs(input.uv_MainTex.x);
            half antialias = max(fwidth(distanceToCenter), 0.00025h);
            half centerSeam = 1.0h - smoothstep(
                _CenterSeamHalfWidth,
                _CenterSeamHalfWidth + antialias,
                distanceToCenter);
            half topSurface = smoothstep(0.65h, 0.9h, normalize(input.meshWorldNormal).y);
            centerSeam *= topSurface;
            baseColor.rgb = lerp(
                baseColor.rgb,
                baseColor.rgb * _CenterSeamDarkness,
                centerSeam);

            output.Albedo = baseColor.rgb;
            output.Alpha = 1;
            output.Metallic = 0;
            output.Smoothness = _Smoothness;

            half occlusion = tex2D(_OcclusionMap, input.uv_MainTex).r;
            output.Occlusion = lerp(1.0h, occlusion, _OcclusionStrength);

            half3 sampledNormal = UnpackNormal(tex2D(_BumpMap, input.uv_MainTex));
            sampledNormal.xy *= _BumpScale;
            sampledNormal.z = sqrt(saturate(1.0h - dot(sampledNormal.xy, sampledNormal.xy)));
            output.Normal = sampledNormal;
        }
        ENDCG
    }

    Fallback "Standard"
}
