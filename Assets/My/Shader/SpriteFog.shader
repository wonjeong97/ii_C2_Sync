Shader "Custom/SpritesFog"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
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

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ PIXELSNAP_ON
            // 안개 기능 컴파일
            #pragma multi_compile_fog 

            #include "UnityCG.cginc"

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
                // 안개 좌표 (팩터 계산용)
                UNITY_FOG_COORDS(1) 
            };

            fixed4 _Color;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                #ifdef PIXELSNAP_ON
                OUT.vertex = UnityPixelSnap (OUT.vertex);
                #endif

                // 안개 좌표 계산
                UNITY_TRANSFER_FOG(OUT,OUT.vertex); 

                return OUT;
            }

            sampler2D _MainTex;
            sampler2D _AlphaTex;

            fixed4 SampleSpriteTexture (float2 uv)
            {
                fixed4 color = tex2D (_MainTex, uv);
                return color;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 c = SampleSpriteTexture(IN.texcoord) * IN.color;
                
                // 기존 알파 곱하기
                c.rgb *= c.a; 

                // ★ [핵심] 안개 팩터를 직접 가져와서 Alpha에 적용
                // UNITY_APPLY_FOG 매크로는 색상만 섞지만, 우리는 투명하게 만들고 싶음
                #if defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2)
                    float fogFactor = 0;
                    #if defined(FOG_LINEAR)
                         c.a *= IN.fogCoord; 
                    #else
                         c.a *= IN.fogCoord;
                    #endif
                #endif
                
                UNITY_APPLY_FOG(IN.fogCoord, c);

                return c;
            }
        ENDCG
        }
    }
}