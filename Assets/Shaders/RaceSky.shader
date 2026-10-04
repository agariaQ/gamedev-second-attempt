Shader "Skybox/RaceSky"
{
    Properties
    {
        [Header(Sky)]
        _SkyTop ("Sky Top", Color) = (0.20, 0.42, 0.82, 1)
        _SkyHorizon ("Sky Horizon", Color) = (0.72, 0.80, 0.90, 1)
        _GroundColor ("Below Horizon", Color) = (0.36, 0.38, 0.36, 1)

        [Header(Sun)]
        _SunColor ("Sun Color", Color) = (1.0, 0.95, 0.85, 1)
        _SunSize ("Sun Size", Range(0.0005, 0.02)) = 0.003
        _SunGlow ("Sun Glow", Range(0, 2)) = 0.7
        _SunDirection ("Fallback Sun Direction", Vector) = (0.5, 0.6, 0.4, 0)

        [Header(Clouds)]
        [NoScaleOffset] _CloudTex ("Cloud Noise (R shapes, G detail, B swirl)", 2D) = "gray" {}
        _CloudColor ("Cloud Color", Color) = (1.0, 1.0, 1.0, 1)
        _CloudShadow ("Cloud Shadow Color", Color) = (0.55, 0.60, 0.68, 1)
        _CloudCoverage ("Cloud Coverage", Range(0, 1)) = 0.45
        _CloudSoftness ("Cloud Softness", Range(0.01, 0.6)) = 0.25
        _CloudScale ("Cloud Scale", Range(0.1, 5)) = 1.0
        _CloudSpeed ("Cloud Wind (XY)", Vector) = (0.012, 0.004, 0, 0)
        _CloudCurve ("Cloud Layer Curve", Range(0.02, 1)) = 0.15
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _SkyTop;
            float4 _SkyHorizon;
            float4 _GroundColor;
            float4 _SunColor;
            float _SunSize;
            float _SunGlow;
            float4 _SunDirection;
            sampler2D _CloudTex;
            float4 _CloudColor;
            float4 _CloudShadow;
            float _CloudCoverage;
            float _CloudSoftness;
            float _CloudScale;
            float4 _CloudSpeed;
            float _CloudCurve;

            float4 _MainLightPosition;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 direction : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.direction = v.vertex.xyz;
                return o;
            }

            float CloudDensity(float2 uv, float2 wind, float detail)
            {
                return tex2D(_CloudTex, uv + wind).r * 0.72 + detail * 0.28;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 direction = normalize(i.direction);
                float3 sunDirection = _MainLightPosition.xyz;
                if (dot(sunDirection, sunDirection) < 0.01)
                {
                    sunDirection = _SunDirection.xyz;
                }
                sunDirection = normalize(sunDirection);

                float height = direction.y;
                if (height <= 0.0)
                {
                    return fixed4(lerp(_SkyHorizon.rgb, _GroundColor.rgb, saturate(-height * 6.0)), 1.0);
                }

                float sunDot = saturate(dot(direction, sunDirection));

                float3 sky = lerp(_SkyHorizon.rgb, _SkyTop.rgb, pow(saturate(height), 0.5));
                sky += _SunColor.rgb * pow(sunDot, 12.0) * _SunGlow * 0.5;
                sky += _SunColor.rgb * smoothstep(1.0 - _SunSize, 1.0 - _SunSize * 0.5, sunDot) * 4.0;

                float2 uv = direction.xz / (height + _CloudCurve) * _CloudScale * 0.12;
                float2 wind = _CloudSpeed.xy * _Time.y;

                float swirl = tex2D(_CloudTex, uv * 0.5 + wind * 0.3).b - 0.5;
                float2 shapeUV = uv + swirl * 0.08;
                float detail = tex2D(_CloudTex, uv * 3.1 + wind * 1.7 - swirl * 0.05).g;

                float density = CloudDensity(shapeUV, wind, detail);
                float coverage = smoothstep(1.0 - _CloudCoverage, 1.0 - _CloudCoverage + _CloudSoftness, density);

                float2 sunStep = sunDirection.xz * 0.02;
                float towardSun1 = CloudDensity(shapeUV + sunStep, wind, detail);
                float towardSun2 = CloudDensity(shapeUV + sunStep * 2.0, wind, detail);
                float shadow = saturate((towardSun1 - density) * 2.5 + (towardSun2 - density) * 1.5);
                float3 cloud = lerp(_CloudColor.rgb, _CloudShadow.rgb, shadow);

                cloud *= lerp(1.0, 0.85, saturate((density - (1.0 - _CloudCoverage)) * 2.0));
                cloud += _SunColor.rgb * pow(sunDot, 8.0) * (1.0 - coverage) * 0.6;

                float horizonFade = smoothstep(0.0, 0.18, height);
                float3 color = lerp(sky, cloud, coverage * horizonFade);
                return fixed4(color, 1.0);
            }
            ENDCG
        }
    }

    Fallback Off
}
