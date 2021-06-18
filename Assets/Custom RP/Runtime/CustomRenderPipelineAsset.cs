using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName ="Rendering/Custom Render Pipeline")]
public partial class CustomRenderPipelineAsset : RenderPipelineAsset
{
    [SerializeField]
    bool m_UseDynamicBatching = true, m_UseGPUInstancing = true, m_UseSRPBatcher = true, m_UseLightsPerObject = true;

    /// <summary>
    /// 全局管控 HDR，深度图等。
    /// </summary>
    [SerializeField]
    CameraBufferSettings m_CameraBufferSettings = new CameraBufferSettings
    {
        allowHDR = true,
        copyColor = true,
        copyDepth = true
    };

    [SerializeField]
    ShadowSettings m_ShadowSettings = default;

    [SerializeField]
    PostFXSettings m_PostFXSettings = default;

    public enum ColorLUTResolution { _16 = 16, _32 = 32, _64 = 64 }

    [SerializeField]
    ColorLUTResolution m_ColorLUTResolution = ColorLUTResolution._32;

    [SerializeField]
    Shader m_CameraRendererShader = default;

    protected override RenderPipeline CreatePipeline()
    {
        return new CustomRenderPipeline( m_UseDynamicBatching, m_UseGPUInstancing, m_UseSRPBatcher, m_UseLightsPerObject, m_CameraBufferSettings ,m_ShadowSettings,
            m_PostFXSettings, (int)m_ColorLUTResolution, m_CameraRendererShader );
    }
}
