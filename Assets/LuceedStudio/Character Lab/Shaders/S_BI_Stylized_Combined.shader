// Made with Amplify Shader Editor v1.9.9.1
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "Luceed Studio/Built-in Stylized Combined"
{
	Properties
	{
		[Header(Color)] _Color( "Color", Color ) = ( 1, 1, 1, 1 )
		_MainTex( "MainTex", 2D ) = "white" {}
		[Header(Ambient)] _AmbientColor( "Ambient Color", Color ) = ( 0, 0, 0, 1 )
		_AmbientThreshold( "Ambient Threshold", Range( -1, 1 ) ) = 0
		_AmbientSmoothing( "Ambient Smoothing", Range( 0.01, 1 ) ) = 1
		_AmbientOpacity( "Ambient Opacity", Range( 0, 1 ) ) = 1
		[Header(Shadow)] _ShadowColor( "Shadow Color", Color ) = ( 0, 0, 0, 0 )
		_ShadowThreshold( "Shadow Threshold", Range( -1, 1 ) ) = 0
		_ShadowSharpness( "Shadow Smoothing", Range( 0.01, 1 ) ) = 1
		_ShadowOpacity( "Shadow Opacity", Range( 0, 1 ) ) = 1
		_IndirectDiffuseContribution( "Indirect Diffuse Contribution", Range( 0, 1 ) ) = 1
		[HDR][Header(Specular)] _SpecularTint( "Specular Tint", Color ) = ( 1, 1, 1, 1 )
		_SpecularTexture( "Specular Texture", 2D ) = "white" {}
		_SpecularThreshold( "Specular Threshold", Range( -1, -0.5 ) ) = -0.7
		_SpecularSmoothing( "Specular Smoothing", Range( 0.001, 1 ) ) = 1
		_SpecularOpacity( "Specular Opacity", Range( 0, 1 ) ) = 1
		[HDR][Header(Rim)] _RimColor( "Rim Color", Color ) = ( 1, 1, 1, 0 )
		_RimPower( "Rim Power", Range( 0.01, 1 ) ) = 0.4
		_RimOffset( "Rim Offset", Range( 0, 1 ) ) = 0.6
		_RimOpacity( "Rim Opacity", Range( 0, 1 ) ) = 1
		[HDR][Header(Backlight)] _BacklightColor( "Backlight Color", Color ) = ( 1, 1, 1, 0 )
		_BacklightPower( "Backlight Power", Range( 0.01, 1 ) ) = 0.5
		_BacklightOffset( "Backlight Offset", Range( -1, 1 ) ) = 0.95
		_BacklightOpacity( "Backlight Opacity", Range( 0, 1 ) ) = 0
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
		[Header(Forward Rendering Options)]
		[ToggleOff] _SpecularHighlights("Specular Highlights", Float) = 1.0
		[ToggleOff] _GlossyReflections("Reflections", Float) = 1.0
	}

	SubShader
	{
		Tags{ "RenderType" = "Opaque"  "Queue" = "Geometry+0" }
		Cull Back
		CGINCLUDE
		#include "UnityPBSLighting.cginc"
		#include "UnityShaderVariables.cginc"
		#include "UnityCG.cginc"
		#include "UnityStandardBRDF.cginc"
		#include "Lighting.cginc"
		#pragma target 3.0
		#pragma shader_feature _SPECULARHIGHLIGHTS_OFF
		#pragma shader_feature _GLOSSYREFLECTIONS_OFF
		#define ASE_VERSION 19901
		#ifdef UNITY_PASS_SHADOWCASTER
			#undef INTERNAL_DATA
			#undef WorldReflectionVector
			#undef WorldNormalVector
			#define INTERNAL_DATA half3 internalSurfaceTtoW0; half3 internalSurfaceTtoW1; half3 internalSurfaceTtoW2;
			#define WorldReflectionVector(data,normal) reflect (data.worldRefl, half3(dot(data.internalSurfaceTtoW0,normal), dot(data.internalSurfaceTtoW1,normal), dot(data.internalSurfaceTtoW2,normal)))
			#define WorldNormalVector(data,normal) half3(dot(data.internalSurfaceTtoW0,normal), dot(data.internalSurfaceTtoW1,normal), dot(data.internalSurfaceTtoW2,normal))
		#endif
		struct Input
		{
			float2 uv_texcoord;
			float3 worldNormal;
			INTERNAL_DATA
			float3 worldPos;
		};

		struct SurfaceOutputCustomLightingCustom
		{
			half3 Albedo;
			half3 Normal;
			half3 Emission;
			half Metallic;
			half Smoothness;
			half Occlusion;
			half Alpha;
			Input SurfInput;
			UnityGIInput GIData;
		};

		uniform float4 _SpecularTint;
		uniform sampler2D _SpecularTexture;
		uniform float4 _SpecularTexture_ST;
		uniform float _SpecularThreshold;
		uniform float _SpecularSmoothing;
		uniform float _SpecularOpacity;
		uniform float _IndirectDiffuseContribution;
		uniform float _ShadowThreshold;
		uniform float _ShadowSharpness;
		uniform float4 _ShadowColor;
		uniform float _ShadowOpacity;
		uniform float4 _Color;
		uniform sampler2D _MainTex;
		uniform float4 _MainTex_ST;
		uniform float4 _AmbientColor;
		uniform float _AmbientThreshold;
		uniform float _AmbientSmoothing;
		uniform float _AmbientOpacity;
		uniform float _RimOffset;
		uniform float _RimPower;
		uniform float _RimOpacity;
		uniform float4 _RimColor;
		uniform float _BacklightOffset;
		uniform float _BacklightPower;
		uniform float _BacklightOpacity;
		uniform float4 _BacklightColor;

		inline half4 LightingStandardCustomLighting( inout SurfaceOutputCustomLightingCustom s, half3 viewDir, UnityGI gi )
		{
			UnityGIInput data = s.GIData;
			Input i = s.SurfInput;
			half4 c = 0;
			#ifdef UNITY_PASS_FORWARDBASE
			float ase_lightAtten = data.atten;
			if( _LightColor0.a == 0)
			ase_lightAtten = 0;
			#else
			float3 ase_lightAttenRGB = gi.light.color / ( ( _LightColor0.rgb ) + 0.000001 );
			float ase_lightAtten = max( max( ase_lightAttenRGB.r, ase_lightAttenRGB.g ), ase_lightAttenRGB.b );
			#endif
			#if defined(HANDLE_SHADOWS_BLENDING_IN_GI)
			half bakedAtten = UnitySampleBakedOcclusion(data.lightmapUV.xy, data.worldPos);
			float zDist = dot(_WorldSpaceCameraPos - data.worldPos, UNITY_MATRIX_V[2].xyz);
			float fadeDist = UnityComputeShadowFadeDistance(data.worldPos, zDist);
			ase_lightAtten = UnityMixRealtimeAndBakedShadows(data.atten, bakedAtten, UnityComputeShadowFade(fadeDist));
			#endif
			float3 HighlightColor83_g22 = (_SpecularTint).rgb;
			#if defined(LIGHTMAP_ON) && ( UNITY_VERSION < 560 || ( defined(LIGHTMAP_SHADOW_MIXING) && !defined(SHADOWS_SHADOWMASK) && defined(SHADOWS_SCREEN) ) )//aselc
			float4 ase_lightColor = 0;
			#else //aselc
			float4 ase_lightColor = _LightColor0;
			#endif //aselc
			float LightAttenuation196_g22 = ase_lightAtten;
			float3 LightColorFalloff80_g22 = ( ase_lightColor.rgb * LightAttenuation196_g22 );
			float2 UV151_g22 = i.uv_texcoord;
			float3 ase_normalWS = WorldNormalVector( i, float3( 0, 0, 1 ) );
			float3 ase_normalWSNorm = normalize( ase_normalWS );
			float3 ase_positionWS = i.worldPos;
			#if defined(LIGHTMAP_ON) && UNITY_VERSION < 560 //aseld
			float3 ase_lightDirWS = 0;
			#else //aseld
			float3 ase_lightDirWS = normalize( UnityWorldSpaceLightDir( ase_positionWS ) );
			#endif //aseld
			float3 ase_viewVectorWS = ( _WorldSpaceCameraPos.xyz - ase_positionWS );
			float3 ase_viewDirSafeWS = Unity_SafeNormalize( ase_viewVectorWS );
			float3 normalizeResult137_g22 = normalize( ( ase_lightDirWS + ase_viewDirSafeWS ) );
			float dotResult141_g22 = dot( ase_normalWSNorm , normalizeResult137_g22 );
			float NdotLV140_g22 = dotResult141_g22;
			float temp_output_188_0_g22 = ( saturate( ( ( NdotLV140_g22 + _SpecularThreshold ) / _SpecularSmoothing ) ) * _SpecularOpacity );
			float3 temp_cast_1 = (1.0).xxx;
			UnityGI gi102_g22 = gi;
			float3 diffNorm102_g22 = normalize( WorldNormalVector( i , ase_normalWSNorm ) );
			gi102_g22 = UnityGI_Base( data, 1, diffNorm102_g22 );
			float3 indirectDiffuse102_g22 = gi102_g22.indirect.diffuse + diffNorm102_g22 * 0.0001;
			float3 lerpResult40_g22 = lerp( temp_cast_1 , indirectDiffuse102_g22 , _IndirectDiffuseContribution);
			float temp_output_31_0_g22 = ( 1.0 - _WorldSpaceLightPos0.w );
			float4 temp_cast_4 = (temp_output_31_0_g22).xxxx;
			float dotResult10_g22 = dot( ase_normalWSNorm , ase_lightDirWS );
			float NdotL11_g22 = dotResult10_g22;
			float4 temp_cast_5 = (( saturate( ( ( NdotL11_g22 + _ShadowThreshold ) / _ShadowSharpness ) ) * LightAttenuation196_g22 )).xxxx;
			float4 lerpResult44_g22 = lerp( temp_cast_4 , max( temp_cast_5 , _ShadowColor ) , _ShadowOpacity);
			float2 uv_MainTex = i.uv_texcoord * _MainTex_ST.xy + _MainTex_ST.zw;
			float4 tex2DNode12 = tex2D( _MainTex, uv_MainTex );
			float4 temp_output_16_0 = ( _Color * tex2DNode12 );
			float4 lerpResult71 = lerp( temp_output_16_0 , ( temp_output_16_0 * float4( (_AmbientColor).rgb , 0.0 ) ) , saturate( ( ( ( 1.0 - tex2DNode12.a ) + _AmbientThreshold ) / _AmbientSmoothing ) ));
			float4 lerpResult93 = lerp( temp_output_16_0 , lerpResult71 , _AmbientOpacity);
			float dotResult38_g22 = dot( ase_normalWSNorm , ase_viewDirSafeWS );
			float NdotV138_g22 = dotResult38_g22;
			float temp_output_85_0_g22 = ( saturate( NdotL11_g22 ) * pow( ( 1.0 - saturate( ( NdotV138_g22 + _RimOffset ) ) ) , _RimPower ) * _RimOpacity );
			c.rgb = ( float4( ( HighlightColor83_g22 * LightColorFalloff80_g22 * tex2D( _SpecularTexture, (UV151_g22*_SpecularTexture_ST.xy + _SpecularTexture_ST.zw) ).r * temp_output_188_0_g22 ) , 0.0 ) + ( ( float4( ( lerpResult40_g22 * ase_lightColor.a * temp_output_31_0_g22 ) , 0.0 ) + ( float4( ase_lightColor.rgb , 0.0 ) * lerpResult44_g22 ) ) * float4( (lerpResult93).xyz , 0.0 ) ) + float4( ( temp_output_85_0_g22 * LightColorFalloff80_g22 * (_RimColor).rgb ) , 0.0 ) + float4( ( ( pow( saturate( ( 1.0 - ( saturate( NdotLV140_g22 ) + _BacklightOffset ) ) ) , _BacklightPower ) * _BacklightOpacity ) * (_BacklightColor).rgb ) , 0.0 ) ).rgb;
			c.a = 1;
			return c;
		}

		inline void LightingStandardCustomLighting_GI( inout SurfaceOutputCustomLightingCustom s, UnityGIInput data, inout UnityGI gi )
		{
			s.GIData = data;
		}

		void surf( Input i , inout SurfaceOutputCustomLightingCustom o )
		{
			o.SurfInput = i;
			o.Normal = float3(0,0,1);
		}

		ENDCG
		CGPROGRAM
		#pragma surface surf StandardCustomLighting keepalpha fullforwardshadows 

		ENDCG
		Pass
		{
			Name "ShadowCaster"
			Tags{ "LightMode" = "ShadowCaster" }
			ZWrite On
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 3.0
			#pragma multi_compile_shadowcaster
			#pragma multi_compile UNITY_PASS_SHADOWCASTER
			#pragma skip_variants FOG_LINEAR FOG_EXP FOG_EXP2
			#include "HLSLSupport.cginc"
			#if ( SHADER_API_D3D11 || SHADER_API_GLCORE || SHADER_API_GLES || SHADER_API_GLES3 || SHADER_API_METAL || SHADER_API_VULKAN )
				#define CAN_SKIP_VPOS
			#endif
			#include "UnityCG.cginc"
			#include "Lighting.cginc"
			#include "UnityPBSLighting.cginc"
			struct v2f
			{
				V2F_SHADOW_CASTER;
				float2 customPack1 : TEXCOORD1;
				float4 tSpace0 : TEXCOORD2;
				float4 tSpace1 : TEXCOORD3;
				float4 tSpace2 : TEXCOORD4;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};
			v2f vert( appdata_full v )
			{
				v2f o;
				UNITY_SETUP_INSTANCE_ID( v );
				UNITY_INITIALIZE_OUTPUT( v2f, o );
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO( o );
				UNITY_TRANSFER_INSTANCE_ID( v, o );
				Input customInputData;
				float3 worldPos = mul( unity_ObjectToWorld, v.vertex ).xyz;
				half3 worldNormal = UnityObjectToWorldNormal( v.normal );
				half3 worldTangent = UnityObjectToWorldDir( v.tangent.xyz );
				half tangentSign = v.tangent.w * unity_WorldTransformParams.w;
				half3 worldBinormal = cross( worldNormal, worldTangent ) * tangentSign;
				o.tSpace0 = float4( worldTangent.x, worldBinormal.x, worldNormal.x, worldPos.x );
				o.tSpace1 = float4( worldTangent.y, worldBinormal.y, worldNormal.y, worldPos.y );
				o.tSpace2 = float4( worldTangent.z, worldBinormal.z, worldNormal.z, worldPos.z );
				o.customPack1.xy = customInputData.uv_texcoord;
				o.customPack1.xy = v.texcoord;
				TRANSFER_SHADOW_CASTER_NORMALOFFSET( o )
				return o;
			}
			half4 frag( v2f IN
			#if !defined( CAN_SKIP_VPOS )
			, UNITY_VPOS_TYPE vpos : VPOS
			#endif
			) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID( IN );
				Input surfIN;
				UNITY_INITIALIZE_OUTPUT( Input, surfIN );
				surfIN.uv_texcoord = IN.customPack1.xy;
				float3 worldPos = float3( IN.tSpace0.w, IN.tSpace1.w, IN.tSpace2.w );
				half3 worldViewDir = normalize( UnityWorldSpaceViewDir( worldPos ) );
				surfIN.worldPos = worldPos;
				surfIN.worldNormal = float3( IN.tSpace0.z, IN.tSpace1.z, IN.tSpace2.z );
				surfIN.internalSurfaceTtoW0 = IN.tSpace0.xyz;
				surfIN.internalSurfaceTtoW1 = IN.tSpace1.xyz;
				surfIN.internalSurfaceTtoW2 = IN.tSpace2.xyz;
				SurfaceOutputCustomLightingCustom o;
				UNITY_INITIALIZE_OUTPUT( SurfaceOutputCustomLightingCustom, o )
				surf( surfIN, o );
				#if defined( CAN_SKIP_VPOS )
				float2 vpos = IN.pos;
				#endif
				SHADOW_CASTER_FRAGMENT( IN )
			}
			ENDCG
		}
	}
	Fallback "Diffuse"
}
/*ASEBEGIN
Version=19901
Node;AmplifyShaderEditor.TexturePropertyNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;11;-1895.403,-1049.411;Inherit;True;Property;_MainTex;MainTex;1;0;Create;True;0;0;0;False;0;False;None;None;False;white;Auto;Texture2D;-1;0;2;SAMPLER2D;0;SAMPLERSTATE;1
Node;AmplifyShaderEditor.TextureCoordinatesNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;15;-1482.245,-964.7909;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;12;-1173.706,-1043.51;Inherit;True;Property;_TextureSample0;Texture Sample 0;1;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.OneMinusNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;65;-839.4844,-946.1136;Inherit;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;104;-905.5862,-680.667;Float;False;Property;_AmbientThreshold;Ambient Threshold;3;0;Create;True;0;0;0;False;0;False;0;-0.112;-1;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.ColorNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;18;-891.6646,-1331.393;Inherit;False;Property;_Color;Color;0;1;[Header];Create;True;1;Color;0;0;False;0;False;1,1,1,1;1,1,1,1;True;True;0;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.ColorNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;61;-529.6748,-1125.107;Inherit;False;Property;_AmbientColor;Ambient Color;2;1;[Header];Create;True;1;Ambient;0;0;False;0;False;0,0,0,1;0,0,0,1;True;True;0;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SimpleAddOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;101;-546.1381,-947.4923;Inherit;True;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;103;-594.511,-679.0408;Float;False;Property;_AmbientSmoothing;Ambient Smoothing;4;0;Create;True;0;0;0;False;0;False;1;0.089;0.01;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;16;-439.8516,-1329.673;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SwizzleNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;95;-290.7588,-1125.451;Inherit;False;FLOAT3;0;1;2;3;1;0;COLOR;0,0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleDivideOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;102;-246.9601,-953.2776;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0.01;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;67;-97.85022,-1148.417;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT3;0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SaturateNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;100;-80.62116,-956.6194;Inherit;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;71;128.9205,-1173.198;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;105;145.0945,-1020.496;Float;False;Property;_AmbientOpacity;Ambient Opacity;5;0;Create;True;0;0;0;False;0;False;1;1;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;93;553.6294,-1317.559;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.FunctionNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;92;874.1266,-1317.502;Inherit;False;Func_Stylized;6;;22;a155e7cce67663f41963c02c06ef3ce2;8,107,1,203,0,144,0,130,1,109,1,128,1,111,1,228,1;2;190;FLOAT2;0,0;False;108;FLOAT4;0,0,0,0;False;2;COLOR;0;FLOAT;106
Node;AmplifyShaderEditor.StandardSurfaceOutputNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;9;1327,-1557.67;Float;False;True;-1;2;;0;0;CustomLighting;Luceed Studio/Built-in Stylized Combined;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;True;False;Back;0;False;;0;False;;False;0;False;;0;False;;False;0;Opaque;0.5;True;True;0;False;Opaque;;Geometry;All;12;all;True;True;True;True;0;False;;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;2;15;10;25;False;0.5;True;0;0;False;;0;False;;0;0;False;;0;False;;0;False;;0;False;;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;True;Relative;0;;-1;-1;-1;-1;0;False;0;0;False;;-1;0;False;;0;0;0;False;0.1;False;;0;False;;False;16;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT3;0,0,0;False;4;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;16;FLOAT4;0,0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;15;2;11;0
WireConnection;12;0;11;0
WireConnection;12;1;15;0
WireConnection;65;0;12;4
WireConnection;101;0;65;0
WireConnection;101;1;104;0
WireConnection;16;0;18;0
WireConnection;16;1;12;0
WireConnection;95;0;61;0
WireConnection;102;0;101;0
WireConnection;102;1;103;0
WireConnection;67;0;16;0
WireConnection;67;1;95;0
WireConnection;100;0;102;0
WireConnection;71;0;16;0
WireConnection;71;1;67;0
WireConnection;71;2;100;0
WireConnection;93;0;16;0
WireConnection;93;1;71;0
WireConnection;93;2;105;0
WireConnection;92;108;93;0
WireConnection;9;13;92;0
ASEEND*/
//CHKSM=ECEC1AB788C0F385DF8D674E5D430010D6AF4F30