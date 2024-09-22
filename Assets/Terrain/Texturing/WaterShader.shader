Shader "Custom/WaterShader"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0

        _NoiseTex ("Noise texture", 2D) = "white" {}
        _NoiseScale ("Noise scale", Range(0.01, 0.1)) = 0.01
        _WaveAmplitude ("Wave amplitude", Range(0.01, 0.1)) = 0.015
        _WaveSpeed ("Wave speed", Range(0.01, 0.3)) = 0.15

    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows alpha vertex:vert
        #pragma target 3.0

        sampler2D _MainTex;
        half _Glossiness;
        half _Metallic;
        fixed4 _Color;

        sampler2D _NoiseTex;
        float _NoiseScale;
        float _WaveAmplitude;
        float _WaveSpeed;

        struct Input
        {
            float2 uv_MainTex;
        };


        void vert(inout appdata_full v) 
        {
            float2 NoiseUV = float2((v.texcoord.xy + _Time * _WaveSpeed) * _NoiseScale);
            float NoiseValue = tex2Dlod(_NoiseTex, float4(NoiseUV, 0, 0)).x * _WaveAmplitude;

            v.vertex = v.vertex + float4(0, NoiseValue, 0, 0);
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // Albedo comes from a texture tinted by color
            fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            // Metallic and smoothness come from slider variables
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
