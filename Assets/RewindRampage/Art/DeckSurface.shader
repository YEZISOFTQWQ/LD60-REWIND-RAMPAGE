Shader "RewindRampage/DeckSurface"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Grain ("Soft matte finish", Range(0,0.1)) = 0.035
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
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma target 2.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct appdata { float4 vertex : POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 surface : TEXCOORD0; float2 local : TEXCOORD1; };
            fixed4 _Color;
            float4 _ClipRect;
            float _Grain;
            v2f vert(appdata v)
            {
                v2f o; o.local = v.vertex.xy; o.vertex = UnityObjectToClipPos(v.vertex);
                o.surface = v.uv; o.color = v.color * _Color; return o;
            }
            float Grain(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            fixed4 frag(v2f i) : SV_Target
            {
                float2 p = i.surface;
                float fine = Grain(floor(p * 1.35));
                // A quiet matte surface, stationary in deck space; no animated texture.
                float metal = 1.0 - smoothstep(0.55, 0.85, max(i.color.r, max(i.color.g, i.color.b)));
                fixed4 result = i.color;
                result.rgb *= 1.0 + metal * _Grain * (fine - 0.5);
                #ifdef UNITY_UI_CLIP_RECT
                result.a *= UnityGet2DClipping(i.local, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(result.a - 0.001);
                #endif
                return result;
            }
            ENDCG
        }
    }
}
