Shader "UI/SpriteHeavyBlur"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (0.7, 0.7, 0.7, 1) 
        _Size ("Blur Size", Range(0, 20)) = 6.0
        _NoiseAmount ("Noise Amount", Range(0, 1)) = 0.6 
        _ColorShiftSpeed ("Color Shift Speed", Range(0, 20)) = 5.0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off Lighting Off ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            float _Size;
            float _NoiseAmount;
            float _ColorShiftSpeed;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            float rand(float2 uv)
            {
                return frac(sin(dot(uv, float2(12.9898, 78.233))) * 43758.5453);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.texcoord;
                
                // 提取当前像素最原始的颜色（主要为了拿到它真实的透明度）
                fixed4 originalColor = tex2D(_MainTex, uv);

                float2 texel = _MainTex_TexelSize.xy * _Size;
                fixed4 color = fixed4(0,0,0,0);
                float totalWeight = 0;

                // 暴力模糊
                for (int x = -3; x <= 3; x++)
                {
                    for (int y = -3; y <= 3; y++)
                    {
                        float weight = 1.0 / (1.0 + abs(x) + abs(y));
                        color += tex2D(_MainTex, uv + float2(x, y) * texel) * weight;
                        totalWeight += weight;
                    }
                }

                fixed4 blurColor = color / totalWeight;

                // 提取亮度
                float luminance = dot(blurColor.rgb, float3(0.299, 0.587, 0.114));

                // 霓虹光谱偏移
                float t = _Time.y * _ColorShiftSpeed;
                float3 shiftColor = float3(
                    sin(t) * 0.5 + 0.5,
                    sin(t + 2.094) * 0.5 + 0.5,
                    sin(t + 4.188) * 0.5 + 0.5
                );
                float3 psychoColor = float3(luminance, luminance, luminance) * shiftColor * 1.5;

                // 动态噪点
                float timeVal = _Time.y * 10.0;
                float r = rand(uv + timeVal);
                float g = rand(uv + timeVal + 1.0);
                float b = rand(uv + timeVal + 2.0);
                float3 noiseColor = float3(r, g, b);

                // 终极混合
                blurColor.rgb = lerp(psychoColor, noiseColor, _NoiseAmount);

                // 将模糊后溢出的透明度，强制替换回原图精准的透明度！
                // 这样四个透明的直角依然是透明的，完美保留圆角！
                blurColor.a = originalColor.a;

                return blurColor * i.color;
            }
            ENDCG
        }
    }
}