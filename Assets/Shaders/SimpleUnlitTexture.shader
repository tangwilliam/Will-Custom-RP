
// This is a simple unlit Unity shader that is compatible with a custom Scriptable Render Pipeline.
// It applies a hardcoded color, and demonstrates the use of the LightMode Pass tag.
// It is not compatible with SRP Batcher.

Shader "Examples/SimpleUnlitTexture"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }

    SubShader
    {
        Pass
        {
            // The value of the LightMode Pass tag must match the ShaderTagId in ScriptableRenderContext.DrawRenderers
            Tags { "LightMode" = "ExampleLightModeTag"}

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            float4x4 unity_MatrixVP;
            float4x4 unity_ObjectToWorld;

            sampler2D _MainTex;
            float4 _MainTex_ST;

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                float4 worldPos = mul(unity_ObjectToWorld, IN.positionOS);
                OUT.positionCS = mul(unity_MatrixVP, worldPos);
                OUT.uv = IN.uv;
                return OUT;
            }

            float4 frag (Varyings IN) : SV_TARGET
            {
                float4 col = tex2D(_MainTex, IN.uv);
                return col;
            }
            ENDHLSL
        }
    }
}