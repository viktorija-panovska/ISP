﻿Shader "Custom/WaterShader"
{
	Properties
	{
		_TextureScale("Texture scale", float) = 5
		_MainTex("Main Texture", 2D) = "white" {}
		_NoiseTex("Noise Texture", 2D) = "white" {}

		_WaveSpeed("Wave Speed", float) = 1.0
		_WaveAmplitude("Wave Amplitude", float) = 0.2
	}

	SubShader
	{
        Pass
        {
            CGPROGRAM

			#include "UnityCG.cginc"
			#pragma vertex vert
			#pragma fragment frag

			sampler2D _CameraDepthTexture;

			float _TextureScale;
			sampler2D _MainTex;
			sampler2D _NoiseTex;
			float _WaveSpeed;
			float _WaveAmplitude;

			struct appdata
			{
			    float4 vertex : POSITION;
				float4 uv : TEXCOORD1;
			};

			struct v2f
			{
			    float4 vertex : SV_POSITION;
				float4 uv : TEXCOORD0;
			};

			v2f vert(appdata i)
            {
                v2f o;

                // convert obj-space position to camera clip space
                o.vertex = UnityObjectToClipPos(i.vertex);

				// apply wave animation
				float noiseSample = tex2Dlod(_NoiseTex, float4(i.uv.xy, 0, 0));
				o.vertex.x += cos(_Time * _WaveSpeed * noiseSample) * _WaveAmplitude;
				o.vertex.y += sin(_Time * _WaveSpeed * noiseSample) * _WaveAmplitude;

				// texture coordinates 
				o.uv = i.uv;

                return o;
            }

            float4 frag(v2f i) : COLOR
            {
				return tex2D(_MainTex, i.uv * _TextureScale);
            }

            ENDCG
        }
	}
}