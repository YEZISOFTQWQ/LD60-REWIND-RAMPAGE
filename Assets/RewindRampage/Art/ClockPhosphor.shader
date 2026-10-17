Shader "RewindRampage/ClockPhosphor"
{
    Properties
    {
        _GlowFalloff ("Phosphor falloff", Float) = 13
        _GlowStrength ("Phosphor glow strength", Float) = 0.32
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off Lighting Off ZWrite Off
        Blend SrcAlpha One
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 shape : TEXCOORD1; fixed4 color : COLOR; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; float4 shape : TEXCOORD1; float2 local : TEXCOORD2; fixed4 color : COLOR; };
            float _GlowFalloff, _GlowStrength;
            v2f vert(appdata v)
            {
                v2f o; o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv; o.shape = v.shape; o.local = v.vertex.xy; o.color = v.color;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float2 p = (i.uv * 2.0 - 1.0) * (i.shape.xy + i.shape.w);
                float2 b = i.shape.xy;
                float d;
                if (i.shape.z < 0.5)
                    d = max(abs(p.y) - b.y, abs(p.x) + abs(p.y) * 0.75 - b.x);
                else if (i.shape.z < 1.5)
                {
                    float2 q = abs(p) - b;
                    float box = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0);
                    d = abs(box) - 0.018;
                }
                else
                    d = length(p) - b.x;
                float aa = max(0.001, fwidth(d));
                float core = 1.0 - smoothstep(-aa, aa, d);
                float glow = exp(-max(0.0, d) * _GlowFalloff);
                glow *= 1.0 - smoothstep(0.3, 0.55, max(0.0, d));
                float radius = i.shape.z < 0.5 ? b.y : i.shape.z < 1.5 ? 0.018 : b.x;
                float filament = pow(saturate(-d / max(0.001, radius)), 4.0);
                float scan = 0.91 + 0.09 * sin(i.vertex.y * 2.7 + _Time.y * 3.0);
                float grain = frac(sin(dot(floor(i.vertex.xy) + floor(_Time.y * 12.0), float2(12.9898, 78.233))) * 43758.5453);
                float tintStrength = max(i.color.r, max(i.color.g, i.color.b));
                float3 radiance = i.color.rgb * (core * 1.2 + glow * _GlowStrength) + filament * 0.45 * tintStrength;
                return fixed4(radiance * scan * (0.95 + grain * 0.05), i.color.a);
            }
            ENDCG
        }
    }
}
