Shader "CYDOY/World Aligned Sidewalk"
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
        _WorldTiling ("World Tiling", Range(0.02,2)) = 0.36
        _WorldRotation ("Compass Rotation", Range(0,360)) = 0
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
        float _WorldTiling;
        float _WorldRotation;

        struct Input
        {
            float3 worldPos;
            float3 meshWorldNormal;
            float3 meshWorldTangent;
            float3 meshWorldBinormal;
        };

        void vert(inout appdata_full vertex, out Input output)
        {
            UNITY_INITIALIZE_OUTPUT(Input, output);
            output.meshWorldNormal = UnityObjectToWorldNormal(vertex.normal);
            output.meshWorldTangent = UnityObjectToWorldDir(vertex.tangent.xyz);
            output.meshWorldBinormal = cross(
                output.meshWorldNormal,
                output.meshWorldTangent) * vertex.tangent.w * unity_WorldTransformParams.w;
        }

        void surf(Input input, inout SurfaceOutputStandard output)
        {
            float angle = radians(_WorldRotation);
            float cosine = cos(angle);
            float sine = sin(angle);
            float2 worldXZ = input.worldPos.xz;
            float2 worldUV = float2(
                cosine * worldXZ.x - sine * worldXZ.y,
                sine * worldXZ.x + cosine * worldXZ.y) * _WorldTiling;

            fixed4 baseColor = tex2D(_MainTex, worldUV) * _Color;
            output.Albedo = baseColor.rgb;
            output.Alpha = 1;
            output.Metallic = 0;
            output.Smoothness = _Smoothness;

            half occlusion = tex2D(_OcclusionMap, worldUV).r;
            output.Occlusion = lerp(1.0h, occlusion, _OcclusionStrength);

            // The texture is projected on the global X/Z plane. Transform its normal from that
            // fixed compass basis back into the mesh tangent basis expected by the Standard shader.
            half3 sampledNormal = UnpackNormal(tex2D(_BumpMap, worldUV));
            sampledNormal.xy *= _BumpScale;
            sampledNormal.z = sqrt(saturate(1.0h - dot(sampledNormal.xy, sampledNormal.xy)));

            float3 worldU = normalize(float3(cosine, 0, -sine));
            float3 worldV = normalize(float3(sine, 0, cosine));
            float3 mappedWorldNormal = normalize(
                sampledNormal.x * worldU +
                sampledNormal.y * worldV +
                sampledNormal.z * float3(0, 1, 0));

            float3 meshNormal = normalize(input.meshWorldNormal);
            float3 meshTangent = normalize(input.meshWorldTangent);
            float3 meshBinormal = normalize(input.meshWorldBinormal);
            output.Normal = normalize(float3(
                dot(mappedWorldNormal, meshTangent),
                dot(mappedWorldNormal, meshBinormal),
                dot(mappedWorldNormal, meshNormal)));
        }
        ENDCG
    }

    Fallback "Standard"
}
