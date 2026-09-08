Shader "Hidden/HotCell/Visor"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _Snow ("Sensor noise", Range(0, 0.035)) = 0
        _Desaturation ("Desaturation", Range(0, 1)) = 0
        _NoiseTime ("Noise time", Float) = 0
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _Snow;
            float _Desaturation;
            float _NoiseTime;

            float SensorNoise(float2 pixel, float seed)
            {
                return frac(sin(dot(pixel, float2(12.9898, 78.233)) + seed * 37.719) * 43758.5453);
            }

            fixed4 frag(v2f_img input) : SV_Target
            {
                fixed4 source = tex2D(_MainTex, input.uv);
                float grey = dot(source.rgb, float3(0.2126, 0.7152, 0.0722));
                float3 color = lerp(source.rgb, grey.xxx, saturate(_Desaturation));
                float2 pixel = floor(input.uv * abs(_MainTex_TexelSize.zw));
                float stepIndex = floor(_NoiseTime);
                // Smooth interpolation and a small amplitude avoid abrupt flashes.
                float blend = frac(_NoiseTime);
                blend = blend * blend * (3.0 - 2.0 * blend);
                float noise = lerp(SensorNoise(pixel, stepIndex), SensorNoise(pixel, stepIndex + 1.0), blend);
                color += (noise - 0.5) * _Snow;
                return fixed4(saturate(color), source.a);
            }
            ENDCG
        }
    }
    Fallback Off
}
