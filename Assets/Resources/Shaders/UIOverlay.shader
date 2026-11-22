// Menú - Grano de película en modo "overlay" para UI
// Gris 0.5 = no cambia nada; más claro aclara y más oscuro oscurece (como mix-blend-mode: overlay en zonas oscuras).
// La opacidad del grano es el alfa del color del RawImage (0.08 a 0.13 = muy sutil).
Shader "Cosanle/UI Overlay"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "gray" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }

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
        Blend DstColor SrcColor      // resultado = 2 x grano x fondo
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float4 _ClipRect;

            v2f vert (appdata_t v)
            {
                v2f o;
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed3 noise = tex2D(_MainTex, i.texcoord).rgb;
                float amount = i.color.a;
                #ifdef UNITY_UI_CLIP_RECT
                amount *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif
                // Acercamos el ruido al gris 0.5 según la opacidad (0.5 = sin efecto)
                fixed3 c = lerp(fixed3(0.5, 0.5, 0.5), noise, amount);
                return fixed4(c, 1);
            }
            ENDCG
        }
    }
}
