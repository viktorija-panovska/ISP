Shader "Custom/TerrainShader"
{
    Properties
    {
        _WaterTextureScale ("Water texture scale", float) = 5
        _WaterTexture ("Water texture", 2D) = "" {}

        _LandTextureScale ("Land texture scale", float) = 5
        _LandTexture ("Land texture", 2D) = "" {}

        _ShoreTextureScale ("Shore texture scale", float) = 5
        _ShoreTexture ("Shore texture", 2D) = "" {}
        _ShoreHeight ("Shore height", float) = 20
    }
    
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _WaterTexture;
        float _WaterTextureScale;

        sampler2D _LandTexture;
        float _LandTextureScale;

        sampler2D _ShoreTexture;
        float _ShoreTextureScale;
        float _ShoreHeight;


        int waterLevel;


        struct Input
        {
            float3 worldPos;
        };


        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            if (IN.worldPos.y >= waterLevel + _ShoreHeight)
                o.Albedo = tex2D(_LandTexture, (IN.worldPos / _LandTextureScale).xz);

            else if (IN.worldPos.y > waterLevel && IN.worldPos.y < waterLevel + _ShoreHeight)
                o.Albedo = tex2D(_ShoreTexture, (IN.worldPos / _ShoreTextureScale).xz);

            else 
                o.Albedo = tex2D(_WaterTexture, (IN.worldPos / _WaterTextureScale).xz);
        }

        ENDCG
    }
    FallBack "Diffuse"
}