Shader "UI/SpriteHeavyBlur"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        // 叠加一层暗色，让模糊后的牌变得暗淡，增加辨认难度
        _Color ("Tint", Color) = (0.7, 0.7, 0.7, 1) 
        _Size ("Blur Size", Range(0, 20)) = 6.0
        // 保留了微弱的噪点功能，如果你连噪点都不想要，可以在材质球里把它拉到 0
        _NoiseAmount ("Noise Amount", Range(0, 1)) = 0.1 
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
                
                // 1. 提取原始透明度 (完美保留圆角)
                fixed4 originalColor = tex2D(_MainTex, uv);

                float2 texel = _MainTex_TexelSize.xy * _Size;
                fixed4 color = fixed4(0,0,0,0);
                float totalWeight = 0;

                // 2. 依然保留 49 次采样的暴力平滑模糊，彻底打碎图形边缘
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

                // 3. 动态噪点 (如果你把面板里的 NoiseAmount 设为 0，这步就完全不生效)
                float timeVal = _Time.y * 10.0;
                float r = rand(uv + timeVal);
                float g = rand(uv + timeVal + 1.0);
                float b = rand(uv + timeVal + 2.0);
                float3 noiseColor = float3(r, g, b);

                // 混合原本的模糊颜色和噪点
                blurColor.rgb = lerp(blurColor.rgb, noiseColor, _NoiseAmount);

                // 4. 将模糊后溢出的透明度，强制替换回原图精准的透明度！
                blurColor.a = originalColor.a;

                return blurColor * i.color;
            }
            ENDCG
        }
    }
}