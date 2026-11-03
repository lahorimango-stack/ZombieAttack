// Made with Amplify Shader Editor
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "FORGE3D/DamageFX/PBR Metallic"
{
	Properties
	{
		[Header(DamageFX)]
		_DamageMap("DamageMap", 2D) = "white" {}
		_HeatMap("HeatMap", 2D) = "white" {}
		_ClipTiling("ClipTiling", Float) = 0.16
		_ClipDirtTiling("ClipDirtTiling", Float) = 0.21
		_ClipOffset("ClipOffset", Float) = -0.01
		_DirtTiling("DirtTiling", Float) = 3
		_DirtOffset("DirtOffset", Float) = 0.1
		_DirtStr("DirtStr", Range( 0 , 10000)) = 11
		_DirtSharpen("DirtSharpen", Range( 0.003 , 10)) = 1
		_HeatOffset("HeatOffset", Float) = 0.03
		_HeatStr("HeatStr", Range( 0 , 10000)) = 10000
		_HeatSharpness("HeatSharpness", Range( 0.03 , 10)) = 2.87
		_BurnOffset("BurnOffset", Float) = 0.01
		_BurnStr("BurnStr", Range( 0 , 10000)) = 655
		_BurnEdge("BurnEdge", Range( 0.03 , 10)) = 0.31
		_ClipCut("ClipCut", Range( 0.001 , 1)) = 0.081
		_VertexDamage("VertexDamage", Float) = 0
		_Cutoff( "Mask Clip Value", Float ) = 0.98
		_Tint("Tint", Color) = (0,0,0,0)
		_DirtColor("DirtColor", Color) = (0,0,0,0)
		_DirtMetallic("DirtMetallic", Float) = 0
		_DirtSmoothness("DirtSmoothness", Float) = 0
		_Albedo("Albedo", 2D) = "white" {}
		_Normal("Normal", 2D) = "bump" {}
		_MetallicSmoothness("MetallicSmoothness", 2D) = "white" {}
		_EmissionMask("EmissionMask", 2D) = "white" {}
		_NormalScale("Normal Scale", Float) = 1
		_Metallic("Metallic", Float) = 0
		_Smoothness("Smoothness", Float) = 0
		_Emission("Emission", Color) = (0,0,0,0)
		_EmissionBoost("Emission Boost", Float) = 0
		[HideInInspector] _texcoord2( "", 2D ) = "white" {}
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
	}

	SubShader
	{
		Tags{ "RenderType" = "Opaque"  "Queue" = "AlphaTest+0" "IsEmissive" = "true"  }
		Cull Off
		CGPROGRAM
		#include "UnityStandardUtils.cginc"
		#include "UnityShaderVariables.cginc"
		#pragma target 3.0
		#pragma multi_compile_instancing
		#pragma surface surf Standard keepalpha addshadow fullforwardshadows vertex:vertexDataFunc 
		struct Input
		{
			float2 uv_texcoord;
			float2 uv2_texcoord2;
			float3 worldPos;
		};

		uniform float _NormalScale;
		uniform sampler2D _Normal;
		uniform float4 _Normal_ST;
		uniform float4 _Tint;
		uniform sampler2D _Albedo;
		uniform float4 _Albedo_ST;
		uniform float4 _DirtColor;
		uniform sampler2D _DamageMap;
		uniform float _DirtTiling;
		uniform float _DirtStr;
		uniform float4 _Points[200];
		uniform float4 _Data[200];
		uniform float _BurnOffset;
		uniform float _HeatOffset;
		uniform float _DirtOffset;
		uniform float _ClipOffset;
		uniform float _DirtSharpen;
		uniform float _BurnEdge;
		uniform sampler2D _HeatMap;
		uniform float4 _HeatMap_ST;
		uniform float _BurnStr;
		uniform float _ClipDirtTiling;
		uniform float _ClipTiling;
		uniform float _HeatSharpness;
		uniform float _HeatStr;
		uniform float4 _Emission;
		uniform float _EmissionBoost;
		uniform sampler2D _EmissionMask;
		uniform float4 _EmissionMask_ST;
		uniform sampler2D _MetallicSmoothness;
		uniform float4 _MetallicSmoothness_ST;
		uniform float _Metallic;
		uniform float _DirtMetallic;
		uniform float _Smoothness;
		uniform float _DirtSmoothness;
		uniform float _ClipCut;
		uniform float _VertexDamage;
		uniform float _Cutoff = 0.98;


		float4 For1_g606( float4 PointsHack , float4 DataHack , float3 VertexPos , int MaxPoints , float BurnOffset , float HeatOffset , float DirtOffset , float ClipOffset )
		{
			float4 damageMask = 0;
			for(int i=0; i<MaxPoints;i++)
			{
			float localDistance = distance(_Points[i].xyz, VertexPos.xyz) + _Points[i].w;
			damageMask.x += (1 - saturate(localDistance + (-1 * DirtOffset))) * _Data[i].x;
			damageMask.y += (1 - saturate(localDistance + (-1 * BurnOffset))) * _Data[i].z;
			damageMask.z += (1 - saturate(localDistance + (-1 * HeatOffset))) * _Data[i].y;
			damageMask.w += (1 - saturate(localDistance + (-1 * ClipOffset))) * _Data[i].w;
			}
			return damageMask;
		}


		void vertexDataFunc( inout appdata_full v, out Input o )
		{
			UNITY_INITIALIZE_OUTPUT( Input, o );
			float4 PointsHack1_g606 = _Points[clamp(0,0,(200 - 1))];
			float4 DataHack1_g606 = _Data[0];
			float3 ase_vertex3Pos = v.vertex.xyz;
			float3 VertexPos1_g606 = ase_vertex3Pos;
			int MaxPoints1_g606 = 200;
			float BurnOffset1_g606 = _BurnOffset;
			float HeatOffset1_g606 = _HeatOffset;
			float DirtOffset1_g606 = _DirtOffset;
			float ClipOffset1_g606 = _ClipOffset;
			float4 localFor1_g606 = For1_g606( PointsHack1_g606 , DataHack1_g606 , VertexPos1_g606 , MaxPoints1_g606 , BurnOffset1_g606 , HeatOffset1_g606 , DirtOffset1_g606 , ClipOffset1_g606 );
			float ClipMask270_g605 = localFor1_g606.w;
			float2 temp_cast_0 = (_ClipDirtTiling).xx;
			float2 uv2_TexCoord64_g605 = v.texcoord1.xy * temp_cast_0 + float2( 0,0 );
			float2 temp_cast_1 = (_ClipTiling).xx;
			float2 uv2_TexCoord41_g605 = v.texcoord1.xy * temp_cast_1 + float2( 0,0 );
			float temp_output_288_0_g605 = ( tex2Dlod( _DamageMap, float4( uv2_TexCoord64_g605, 0, 0) ).r * tex2Dlod( _DamageMap, float4( uv2_TexCoord41_g605, 0, 0) ).a );
			float temp_output_391_0_g605 = saturate( ( saturate( ( ClipMask270_g605 * 10 ) ) / temp_output_288_0_g605 ) );
			float3 ase_vertexNormal = v.normal.xyz;
			v.vertex.xyz += ( temp_output_391_0_g605 * _VertexDamage * ase_vertexNormal * temp_output_288_0_g605 );
		}

		void surf( Input i , inout SurfaceOutputStandard o )
		{
			float2 uv_Normal = i.uv_texcoord * _Normal_ST.xy + _Normal_ST.zw;
			o.Normal = UnpackScaleNormal( tex2D( _Normal, uv_Normal ) ,_NormalScale );
			float2 uv_Albedo = i.uv_texcoord * _Albedo_ST.xy + _Albedo_ST.zw;
			float2 temp_cast_0 = (_DirtTiling).xx;
			float2 uv2_TexCoord130_g605 = i.uv2_texcoord2 * temp_cast_0 + float2( 0,0 );
			float4 PointsHack1_g606 = _Points[clamp(0,0,(200 - 1))];
			float4 DataHack1_g606 = _Data[0];
			float3 ase_vertex3Pos = mul( unity_WorldToObject, float4( i.worldPos , 1 ) );
			float3 VertexPos1_g606 = ase_vertex3Pos;
			int MaxPoints1_g606 = 200;
			float BurnOffset1_g606 = _BurnOffset;
			float HeatOffset1_g606 = _HeatOffset;
			float DirtOffset1_g606 = _DirtOffset;
			float ClipOffset1_g606 = _ClipOffset;
			float4 localFor1_g606 = For1_g606( PointsHack1_g606 , DataHack1_g606 , VertexPos1_g606 , MaxPoints1_g606 , BurnOffset1_g606 , HeatOffset1_g606 , DirtOffset1_g606 , ClipOffset1_g606 );
			float Dirtmask30_g605 = localFor1_g606.x;
			float temp_output_139_0_g605 = pow( Dirtmask30_g605 , _DirtSharpen );
			float temp_output_469_44 = saturate( ( tex2D( _DamageMap, uv2_TexCoord130_g605 ).a * _DirtStr * temp_output_139_0_g605 ) );
			float4 lerpResult266 = lerp( ( _Tint * tex2D( _Albedo, uv_Albedo ) ) , _DirtColor , temp_output_469_44);
			o.Albedo = lerpResult266.rgb;
			float BurnMask268_g605 = localFor1_g606.y;
			float mulTime231_g605 = _Time.y * 1;
			float2 uv_HeatMap = i.uv2_texcoord2 * _HeatMap_ST.xy + _HeatMap_ST.zw;
			float2 panner229_g605 = ( uv_HeatMap + mulTime231_g605 * float2( 0.025,0.025 ));
			float4 tex2DNode204_g605 = tex2D( _HeatMap, panner229_g605 );
			float ClipMask270_g605 = localFor1_g606.w;
			float2 temp_cast_2 = (_ClipDirtTiling).xx;
			float2 uv2_TexCoord64_g605 = i.uv2_texcoord2 * temp_cast_2 + float2( 0,0 );
			float2 temp_cast_3 = (_ClipTiling).xx;
			float2 uv2_TexCoord41_g605 = i.uv2_texcoord2 * temp_cast_3 + float2( 0,0 );
			float temp_output_288_0_g605 = ( tex2D( _DamageMap, uv2_TexCoord64_g605 ).r * tex2D( _DamageMap, uv2_TexCoord41_g605 ).a );
			float temp_output_391_0_g605 = saturate( ( saturate( ( ClipMask270_g605 * 10 ) ) / temp_output_288_0_g605 ) );
			float BrunMaskMask355_g605 = saturate( ( temp_output_391_0_g605 * BurnMask268_g605 ) );
			float HeatMask269_g605 = localFor1_g606.z;
			float2 uv_EmissionMask = i.uv_texcoord * _EmissionMask_ST.xy + _EmissionMask_ST.zw;
			o.Emission = ( max( float4( 0,0,0,0 ) , ( ( pow( BurnMask268_g605 , _BurnEdge ) * tex2DNode204_g605 * _BurnStr * BrunMaskMask355_g605 ) + ( saturate( pow( HeatMask269_g605 , _HeatSharpness ) ) * tex2DNode204_g605 * _HeatStr ) ) ) + ( _Emission * _EmissionBoost * tex2D( _EmissionMask, uv_EmissionMask ) ) ).rgb;
			float2 uv_MetallicSmoothness = i.uv_texcoord * _MetallicSmoothness_ST.xy + _MetallicSmoothness_ST.zw;
			float4 tex2DNode10 = tex2D( _MetallicSmoothness, uv_MetallicSmoothness );
			float3 temp_cast_5 = (_DirtMetallic).xxx;
			float3 lerpResult268 = lerp( ( (tex2DNode10).rgb * _Metallic ) , temp_cast_5 , temp_output_469_44);
			o.Metallic = lerpResult268.x;
			float lerpResult267 = lerp( ( tex2DNode10.a * _Smoothness ) , _DirtSmoothness , temp_output_469_44);
			o.Smoothness = lerpResult267;
			o.Alpha = 1;
			clip( saturate( step( _ClipCut , ( 1.0 - temp_output_391_0_g605 ) ) ) - _Cutoff );
		}

		ENDCG
	}
	Fallback "Diffuse"
	CustomEditor "ASEMaterialInspector"
}
/*ASEBEGIN
Version=14501
1927;29;1906;1004;1048.832;670.5576;1;True;False
Node;AmplifyShaderEditor.SamplerNode;10;-903.5,70;Float;True;Property;_MetallicSmoothness;MetallicSmoothness;25;0;Create;True;0;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;6;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;16;-554.7,176.1001;Float;False;Property;_Smoothness;Smoothness;29;0;Create;True;0;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;14;-553.5,10;Float;False;Property;_Metallic;Metallic;28;0;Create;True;0;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.ColorNode;462;-811.894,-511.2078;Float;False;Property;_Tint;Tint;19;0;Create;True;0;0,0,0,0;0,0,0,0;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;7;-901.7002,-324.3002;Float;True;Property;_Albedo;Albedo;23;0;Create;True;0;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;6;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ColorNode;18;-898.5,278;Float;False;Property;_Emission;Emission;30;0;Create;True;0;0,0,0,0;0,0,0,0;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;22;-897.5,552;Float;True;Property;_EmissionMask;EmissionMask;26;0;Create;True;0;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;6;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ComponentMaskNode;12;-554.5,-76;Float;False;True;True;True;False;1;0;COLOR;0,0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode;19;-894.5,454;Float;False;Property;_EmissionBoost;Emission Boost;31;0;Create;True;0;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode;469;36.30084,-26.29645;Float;False;DamageFX;0;;605;947c011340f36384dab084490b24dfb9;0;0;4;COLOR;366;FLOAT;286;FLOAT;44;FLOAT3;414
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;463;-364.6952,-389.0079;Float;False;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode;173;-168.7649,478.3382;Float;False;Property;_DirtSmoothness;DirtSmoothness;22;0;Create;True;0;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;172;-162.7649,390.3382;Float;False;Property;_DirtMetallic;DirtMetallic;21;0;Create;True;0;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;15;-279.5,-87;Float;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;17;-279.5,15;Float;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;20;-604.5,281;Float;False;3;3;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;2;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.ColorNode;28;-165.9561,209.8253;Float;False;Property;_DirtColor;DirtColor;20;0;Create;True;0;0,0,0,0;0,0,0,0;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;9;-1109.5,-88;Float;False;Property;_NormalScale;Normal Scale;27;0;Create;True;0;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;267;781.0072,222.328;Float;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;8;-898.5,-134;Float;True;Property;_Normal;Normal;24;0;Create;True;0;None;None;True;0;True;bump;Auto;True;Object;-1;Auto;Texture2D;6;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.LerpOp;266;766.5085,-150.9761;Float;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleAddOpNode;270;790.4194,54.63877;Float;False;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.LerpOp;268;791.7708,-340.8315;Float;False;3;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;3;1108.766,-45.66158;Float;False;True;7;Float;ASEMaterialInspector;0;0;Standard;FORGE3D/DamageFX/PBR Metallic;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;False;False;Off;0;0;False;0;0;False;0;Custom;0.98;True;True;0;True;Opaque;;AlphaTest;All;True;True;True;True;True;True;True;True;True;True;True;True;True;True;True;True;True;False;0;255;255;0;0;0;0;0;0;0;0;False;0;4;10;25;False;0.5;True;0;One;One;0;Zero;Zero;Add;Add;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;Relative;0;;18;-1;-1;-1;0;0;0;False;0;0;16;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;12;0;10;0
WireConnection;463;0;462;0
WireConnection;463;1;7;0
WireConnection;15;0;12;0
WireConnection;15;1;14;0
WireConnection;17;0;10;4
WireConnection;17;1;16;0
WireConnection;20;0;18;0
WireConnection;20;1;19;0
WireConnection;20;2;22;0
WireConnection;267;0;17;0
WireConnection;267;1;173;0
WireConnection;267;2;469;44
WireConnection;8;5;9;0
WireConnection;266;0;463;0
WireConnection;266;1;28;0
WireConnection;266;2;469;44
WireConnection;270;0;469;366
WireConnection;270;1;20;0
WireConnection;268;0;15;0
WireConnection;268;1;172;0
WireConnection;268;2;469;44
WireConnection;3;0;266;0
WireConnection;3;1;8;0
WireConnection;3;2;270;0
WireConnection;3;3;268;0
WireConnection;3;4;267;0
WireConnection;3;10;469;286
WireConnection;3;11;469;414
ASEEND*/
//CHKSM=D9041397D562B4E835B69784F82DDA82CBEC12A9