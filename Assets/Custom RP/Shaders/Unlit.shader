Shader "Custom RP/Unlit"
{
    Properties
    {
        _BaseColor("Color",Color) = (1.0,1.0,1.0,1.0)
        _BaseMap("BaseMap", 2D) = "white" {}

    }
    SubShader
    {

        Pass
        {
            HLSLPROGRAM
            #pragma vertex UnlitPassVertex
            #pragma fragment UnlitPassFragment

            #include "UnlitPass.hlsl"
            ENDHLSL

        }

        
    }

	CustomEditor "CustomShaderGUI"

}
