﻿// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Harmony/Hardware Skinning"
{
	Properties
    {
		[PerRendererData] _Color ("Color", Color) = (1,1,1,1)
		[HideInInspector][PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
		[HideInInspector][PerRendererData] _MaskTex ("Mask Texture", 2D) = "white" {}
	}
	CustomEditor "ToonBoom.Harmony.HarmonyShaderGUI"
	SubShader
    {
		Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
			"CanUseSpriteAtlas"="True"
			"DisableBatching"="True"
        }
		ZWrite Off
        Lighting Off
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha
        ZTest [unity_GUIZTestMode]
		
        Pass
        {
		    CGPROGRAM
		    // Use shader model 3.0 target, to get nicer looking lighting
		    #pragma target 3.0
            #pragma vertex vert_vct
            #pragma fragment frag_mult
			
			// Include this pragma to see shader symbols in graphics captures (RenderDoc)
			// #pragma enable_d3d11_debug_symbols 

		    sampler2D _MainTex;
            sampler2D _MaskTex;
		    float4 _Color;
            float4x4 _Bones[32];

		    struct vin_vct 
            {
                float4 position : POSITION;
                float3 texCoordAndOpacity : TEXCOORD0;
                float4 fxParams : TEXCOORD1;
                float4 fxViewport : TEXCOORD2;
                float4 boneParams : TEXCOORD3;
            };

            struct v2f_vct
            {
                float4 position : SV_POSITION;
                fixed4 color : COLOR;
                float2 texCoord : TEXCOORD0;
                float4 fxParams : TEXCOORD1;
                float4 fxViewport : TEXCOORD2;
            };

		    v2f_vct vert_vct(vin_vct v)
            {
                v2f_vct o;
                float4x4 skinMatrix = v.boneParams.y * _Bones[int(v.boneParams.x)]
                    + v.boneParams.w * _Bones[int(v.boneParams.z)];

                o.position = UnityObjectToClipPos(mul(skinMatrix, v.position));

		    	float opacity = v.texCoordAndOpacity.z;
                o.color = float4(_Color.x, _Color.y, _Color.z, _Color.w * opacity);
		    	
                o.texCoord = v.texCoordAndOpacity.xy;
		    	
                o.fxParams = v.fxParams;
		    	
                o.fxViewport = v.fxViewport;
		    	
                return o;
            }

            fixed4 frag_mult(v2f_vct i) : SV_Target
            {
                float2 uv = clamp(i.fxParams.xy, i.fxViewport.xy, i.fxViewport.zw);
                fixed mult = (i.fxParams.z + i.fxParams.w * tex2D(_MaskTex, uv).a);

                return i.color * mult * tex2D(_MainTex, i.texCoord);
            }

		    ENDCG
        }
	}
}
