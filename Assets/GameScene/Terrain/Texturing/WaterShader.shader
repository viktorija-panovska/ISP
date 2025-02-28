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

			float _TextureScale, _WaveSpeed, _WaveAmplitude;
			sampler2D _MainTex, _NoiseTex;

			struct appdata
			{
			    float4 vertex : POSITION;
				float4 uv : TEXCOORD0;
			};

			struct v2f
			{
			    float4 vertex : SV_POSITION;
				float4 uv : TEXCOORD0;
			};

			v2f vert(appdata i)
            {
                v2f o;

				// convert mesh vertex position to clip space
                o.vertex = UnityObjectToClipPos(i.vertex);

				// wave animation
				float noiseSample = tex2Dlod(_NoiseTex, float4(i.uv.xy, 0, 0));
				o.vertex.y += sin(_Time * _WaveSpeed * noiseSample) * _WaveAmplitude;

				// texture 
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