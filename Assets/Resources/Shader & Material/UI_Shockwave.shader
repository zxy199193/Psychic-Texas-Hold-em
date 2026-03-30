Shader "Custom/UI_Shockwave"
{
    Properties
    {
        _Center ("Center (X,Y)", Vector) = (0.5, 0.5, 0, 0)
        _Radius ("Radius", Range(0, 2)) = 0.0
        _Thickness ("Thickness", Range(0.01, 0.5)) = 0.1
        _Force ("Distortion Force", Range(0, 1)) = 0.1
    }
    SubShader
    {
        // 关键设置：必须在所有半透明 UI 之后渲染
        Tags { "Queue"="Transparent+100" "RenderType"="Transparent" "IgnoreProjector"="True" }
        ZWrite Off Lighting Off Cull Off Fog { Mode Off } Blend SrcAlpha OneMinusSrcAlpha

        // 核心魔法：抓取当前屏幕的画面，存入 _BackgroundTexture
        GrabPass { "_BackgroundTexture" }

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
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 grabPos : TEXCOORD0;
                float2 uv : TEXCOORD1;
            };

            sampler2D _BackgroundTexture;
            float4 _Center;
            float _Radius;
            float _Thickness;
            float _Force;

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                // 计算屏幕抓取坐标
                o.grabPos = ComputeGrabScreenPos(o.vertex);
                o.uv = v.uv;
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                float2 dir = i.uv - _Center.xy;
                float dist = length(dir);

                float diff = abs(dist - _Radius);
                float mask = smoothstep(_Thickness, 0.0, diff);

                float2 offset = normalize(dir) * mask * _Force;

                float4 screenUv = i.grabPos;
                screenUv.xy += offset * screenUv.w; 
                
                half4 col = tex2Dproj(_BackgroundTexture, screenUv);
                
                // 【终极修复魔法】：让像素的透明度等于水波的强度！
                // 波纹最强的地方不透明（发挥扭曲），没有波纹的地方全透明（露出上一层）！
                col.a = mask; 

                return col;
            }
            ENDCG
        }
    }
}