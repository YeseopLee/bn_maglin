Shader "UI/RadialGradient"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        
        // Radial Gradient Properties
        _CenterColor ("Center Color", Color) = (1,1,1,1)
        _EdgeColor ("Edge Color", Color) = (1,1,1,0)
        _GradientPower ("Gradient Power", Range(0.1, 5.0)) = 1.0
        _GradientOffset ("Gradient Offset", Range(0.0, 1.0)) = 0.0
        _GradientScale ("Gradient Scale", Range(0.1, 2.0)) = 1.0
        
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
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

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord  : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _CenterColor;
            fixed4 _EdgeColor;
            float _GradientPower;
            float _GradientOffset;
            float _GradientScale;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);

                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);

                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // 텍스처 좌표를 -1 ~ 1 범위로 변환 (중심이 0,0이 되도록)
                float2 centeredUV = (IN.texcoord - 0.5) * 2.0;
                
                // 부드러운 사각형 그라데이션 - 가장자리에서 중앙으로
                float2 absUV = abs(centeredUV);
                float distanceX = absUV.x;
                float distanceY = absUV.y;
                // 두 축의 거리를 조합하여 부드러운 사각형 그라데이션 생성
                float distance = pow(pow(distanceX, 4.0) + pow(distanceY, 4.0), 0.25) * _GradientScale;
                
                // 그라데이션 계산 - offset은 그라데이션 시작점을 조절 (0에서 시작, offset만큼 뒤로 밀기)
                float gradient = saturate((distance - _GradientOffset) / (1.0 - _GradientOffset));
                gradient = pow(gradient, _GradientPower);
                
                // 중심 색상과 가장자리 색상 보간
                fixed4 gradientColor = lerp(_CenterColor, _EdgeColor, gradient);
                
                // 원본 텍스처 샘플링
                half4 texColor = tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd;
                
                // 최종 색상 계산 - 그라데이션만 적용 (색상 곱셈 문제 방지)
                half4 color = texColor * IN.color;
                // 그라데이션은 알파에만 적용하고 RGB는 원본 유지
                color.a *= gradientColor.a;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip (color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }
}
