Shader "Custom/TerrainShader"
{
    Properties
    {
        _TextureScale("Texture scale", float) = 5
        _WaterTexture ("Water texture", 2D) = "" {}
        _LandTexture ("Land texture", 2D) = "" {}

    }
        SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        float _TextureScale;
        sampler2D _WaterTexture;
        sampler2D _LandTexture;

        float minHeight;
        float maxHeight;
        int waterLevel;


        struct Input
        {
            float3 worldPos;
        };


        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float3 scaledWorldPos = IN.worldPos / _TextureScale;

            if (IN.worldPos.y > waterLevel)
                o.Albedo = tex2D(_LandTexture, scaledWorldPos.xz);
            else 
                o.Albedo = tex2D(_WaterTexture, scaledWorldPos.xz);
        }

        ENDCG
    }
    FallBack "Diffuse"
}