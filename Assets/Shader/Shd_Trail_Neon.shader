// 260924_긋는 중인 선을 네온처럼 그린다.
//
// 띠 메시(CTrailMesh_Utility)가 넘겨 주는 것만 쓴다 — 텍스처가 없다.
//   · 꼭짓점 색 : 꼬리 → 머리 그라디언트(CGridRenderer가 섞어 둔다). 불 머리는 주황으로 들어온다
//   · u        : 지나온 거리. **굵기 한 칸이 1**이라 무늬가 선 굵기에 맞춰 반복된다
//   · v        : 띠를 가로지르는 0~1 (0.5가 한가운데)
//
// 네온은 '가운데는 하얗게 타고 가장자리로 갈수록 색만 번진다'가 핵심이라, v로 심지와 번짐을 만들고
// u로 빛을 흘린다. 기본 혼합은 가산(Blend SrcAlpha One) — 어두운 가림막 위에서 빛나 보인다.
// 밝은 보상 그림 위에서 너무 날아가면 재질에서 Dst를 OneMinusSrcAlpha로 바꾸면 보통 반투명이 된다.
Shader "LandGrab/Trail_Neon"
{
    Properties
    {
        [HDR] _CoreColor    ("심지 색", Color) = (1, 1, 1, 1)
        _CoreBlend          ("심지가 먹는 정도", Range(0, 1)) = 0.85
        _CoreWidth          ("심지 굵기(띠 대비)", Range(0, 1)) = 0.18
        _EdgeSoftness       ("심지 가장자리 부드럽기", Range(0.001, 1)) = 0.22
        _HaloPower          ("번짐이 죽는 속도", Range(0.5, 8)) = 2.5
        _Intensity          ("전체 밝기", Range(0, 4)) = 1.6

        _FlowSpeed          ("흐르는 속도", Range(-8, 8)) = 2.5
        _FlowTiling         ("흐르는 무늬 간격", Range(0, 4)) = 0.35
        _FlowStrength       ("흐르는 정도", Range(0, 1)) = 0.35

        _PulseSpeed         ("숨쉬는 속도", Range(0, 12)) = 3
        _PulseStrength      ("숨쉬는 정도", Range(0, 1)) = 0.12

        // 재질에서 혼합을 바꿀 수 있게 열어 둔다 — 기본은 가산
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5   // SrcAlpha
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 1   // One
    }

    SubShader
    {
        Tags
        {
            "RenderType"        = "Transparent"
            "Queue"             = "Transparent"
            "IgnoreProjector"   = "True"
            "RenderPipeline"    = "UniversalPipeline"
        }

        Pass
        {
            Name "TrailNeon"

            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            ZTest Always
            Cull Off            // 띠는 양면이다 — 감기 방향이 뒤집혀도 사라지지 않게

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float4 color        : COLOR;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float4 color        : COLOR;
                float2 uv           : TEXCOORD0;
            };

            // SRP 배처가 묶을 수 있게 한 덩어리로 둔다
            CBUFFER_START(UnityPerMaterial)
                half4  _CoreColor;
                half   _CoreBlend;
                half   _CoreWidth;
                half   _EdgeSoftness;
                half   _HaloPower;
                half   _Intensity;
                half   _FlowSpeed;
                half   _FlowTiling;
                half   _FlowStrength;
                half   _PulseSpeed;
                half   _PulseStrength;
                float  _SrcBlend;
                float  _DstBlend;
            CBUFFER_END

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.color      = IN.color;
                OUT.uv         = IN.uv;
                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                // v로 심지와 번짐을 만든다 — 0이 한가운데, 1이 띠 가장자리
                half fEdge = abs(IN.uv.y * 2.0h - 1.0h);

                half fCore = 1.0h - smoothstep(_CoreWidth, _CoreWidth + _EdgeSoftness, fEdge);
                half fHalo = pow(saturate(1.0h - fEdge), _HaloPower);

                // u로 빛을 흘린다. 굵기 한 칸이 1이라 간격이 굵기에 맞춰 반복된다
                half fFlow  = sin((IN.uv.x * _FlowTiling - _Time.y * _FlowSpeed) * 6.28318h) * 0.5h + 0.5h;
                half fPulse = sin(_Time.y * _PulseSpeed) * 0.5h + 0.5h;
                half fGain  = lerp(1.0h, fFlow, _FlowStrength) * lerp(1.0h, fPulse, _PulseStrength);

                // 가운데로 갈수록 심지 색이 먹는다 — 네온관이 하얗게 타는 그 느낌
                half3 cRGB = lerp(IN.color.rgb, _CoreColor.rgb, fCore * _CoreBlend);
                cRGB *= _Intensity * fGain;

                half fAlpha = IN.color.a * saturate(fHalo + fCore);
                return half4(cRGB, fAlpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
