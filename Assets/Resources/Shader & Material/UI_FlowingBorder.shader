Shader "Custom/UI_FlowingBorder"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _BaseColor ("Base Border Color", Color) = (1, 1, 1, 0.0) 
        _LightColor ("Flowing Light Color", Color) = (1, 0.8, 0, 1) 
        _Speed ("Flow Speed", Range(-5.0, 5.0)) = 1.0 
        _TailLength ("Light Tail Length", Range(0.01, 1.0)) = 0.25 
        
        // 你的宽边框的真实比例（宽÷高）
        _Aspect ("Aspect Ratio (Width / Height)", Float) = 4.0 
        // 依然保留了你喜欢的双流光！
        _LightCount ("Light Count (Symmetry)", Range(1, 4)) = 2.0 
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        ZWrite Off Lighting Off Cull Off Fog { Mode Off } 
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR; 
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            sampler2D _MainTex;
            float4 _BaseColor;
            float4 _LightColor;
            float _Speed;
            float _TailLength;
            float _Aspect; 
            float _LightCount; 

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                half4 texColor = tex2D(_MainTex, i.uv);
                if (texColor.a < 0.05) return half4(0,0,0,0); // 剔除透明区域

                // 核心：直接用最稳的距离展开法，配合你画好的宽比例边框，绝对匀速且不撕裂
                float x = (i.uv.x - 0.5) * _Aspect;
                float y = i.uv.y - 0.5;

                float W = _Aspect * 0.5;
                float H = 0.5;

                // 计算当前像素到四条边的距离
                float dTop = H - y;
                float dBottom = y + H;
                float dRight = W - x;
                float dLeft = x + W;

                float minD = min(min(dTop, dBottom), min(dLeft, dRight));
                float p = 0.0;

                // 顺时针线性展开
                if (minD == dTop) {
                    p = x + W; 
                } else if (minD == dRight) {
                    p = 2.0 * W + (H - y); 
                } else if (minD == dBottom) {
                    p = 2.0 * W + 2.0 * H + (W - x); 
                } else {
                    p = 4.0 * W + 2.0 * H + (y + H); 
                }

                float totalPerimeter = 4.0 * W + 4.0 * H;
                float normalizedP = p / totalPerimeter;

                // 多重对称流光
                float timeOffset = frac(_Time.y * _Speed);
                float phase = frac((normalizedP - timeOffset) * _LightCount);

                float lightIntensity = smoothstep(1.0 - _TailLength, 1.0, phase);

                half3 finalRGB = lerp(_BaseColor.rgb, _LightColor.rgb, lightIntensity);
                half targetAlpha = lerp(_BaseColor.a, _LightColor.a, lightIntensity);
                half finalAlpha = texColor.a * targetAlpha * i.color.a;

                return half4(finalRGB * i.color.rgb, finalAlpha);
            }
            ENDCG
        }
    }
}