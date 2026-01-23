Shader "Mycelium/GrowURP_Unlit"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (0.7, 1, 0.9, 1)
        _GlowColor("Glow Color", Color) = (0.2, 1, 0.7, 1)
        _Grow("Grow (0..1)", Range(0,1)) = 0
        _TipWidth("Tip Width", Range(0.0001, 0.2)) = 0.03
        _TipIntensity("Tip Intensity", Range(0,10)) = 2.0
        _BodyIntensity("Body Intensity", Range(0,5)) = 1.0
        _SoftClip("Soft Clip", Range(0,0.05)) = 0.01

        // --- Sway (дрожание/волны)
        _SwayAmp("Sway Amplitude (meters)", Range(0,0.2)) = 0.08
        _SwayFreq("Sway Frequency", Range(0,10)) = 1.8
        _SwaySpeed("Sway Speed", Range(0,10)) = 1.2
        _SwayAlongGrow("Sway by Grow (0=uniform, 1=more at tips)", Range(0,1)) = 1
        _SwayWorldScale("Sway World Scale", Range(0.1, 5)) = 1.0
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "ForwardUnlit"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;   // uv.x = глобальная длина 0..1
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _GlowColor;
                float _Grow;
                float _TipWidth;
                float _TipIntensity;
                float _BodyIntensity;
                float _SoftClip;

                float _SwayAmp;
                float _SwayFreq;
                float _SwaySpeed;
                float _SwayAlongGrow;
                float _SwayWorldScale;
            CBUFFER_END

            // Маленький "псевдо-шум" на основе синусов (достаточно для дрожания)
            float SineNoise(float3 p, float t)
            {
                // Не используем текстуры — всё процедурно.
                float n =
                    sin((p.x * 1.37 + p.z * 1.11) * _SwayFreq + t) +
                    sin((p.z * 1.73 + p.y * 0.97) * (_SwayFreq * 1.31) + t * 1.17) +
                    sin((p.y * 1.19 + p.x * 0.89) * (_SwayFreq * 1.71) + t * 0.93);
                return n / 3.0;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                // Исходная позиция в object space
                float3 posOS = IN.positionOS.xyz;

                // Переводим в world, чтобы волны были "в мире", а не в локале
                float3 posWS = TransformObjectToWorld(posOS);
                float t = _Time.y * _SwaySpeed;

                // Сила колыхания: от корня к кончикам (uv.x ~ 0 у корня, 1 у самых дальних)
                float swayMask = lerp(1.0, saturate(IN.uv.x), _SwayAlongGrow);
                // Для более "водорослевого" поведения усилим ближе к кончику нелинейно
                swayMask = pow(swayMask, 1.6);

                // Шум-значение
                float3 p = posWS * _SwayWorldScale;
                float n = SineNoise(p, t);

                // Направление смещения: вбок (XZ) + чуть вверх/вниз
                float3 offsetWS = float3(n, sin((p.x + p.z) * _SwayFreq + t) * 0.35, n) * _SwayAmp * swayMask;

                posWS += offsetWS;

                // Назад в object? не нужно — URP принимает world->clip
                VertexPositionInputs vpi = GetVertexPositionInputs(TransformWorldToObject(posWS));
                OUT.positionHCS = vpi.positionCS;

                VertexNormalInputs nrm = GetVertexNormalInputs(IN.normalOS);
                OUT.normalWS = nrm.normalWS;
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float u = IN.uv.x;
                float d = _Grow - u; // >0 уже выросло

                float a = saturate(d / max(_SoftClip, 1e-5));
                clip(a - 0.001);

                float nd = saturate(dot(normalize(IN.normalWS), normalize(float3(0.2, 1.0, 0.1))) * 0.5 + 0.5);

                float tip = 1.0 - saturate(abs(u - _Grow) / max(_TipWidth, 1e-5));
                tip = tip * tip;

                float3 col = _BaseColor.rgb * (_BodyIntensity * (0.6 + 0.4 * nd));
                col += _GlowColor.rgb * (tip * _TipIntensity);

                float alpha = _BaseColor.a * a;
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
}
