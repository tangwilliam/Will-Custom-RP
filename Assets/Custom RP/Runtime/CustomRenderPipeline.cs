using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public partial class CustomRenderPipeline:RenderPipeline
{
    protected CameraRenderer m_CameraRenderer;

    bool m_UseDynamicBatching = true;
    bool m_UseGPUInstancing = true;
    bool m_UseSRPBatcher = true;
    bool m_UseLightsPerObject = false;
    bool m_UseHDR = true;

    ShadowSettings m_ShadowSettings;
    PostFXSettings m_PostFXSettings;

    int m_ColorLUTResolution;

    public CustomRenderPipeline( bool useDynamicBatching, bool useGPUInstancing, bool useSRPBatching, bool useLightsPerObject, 
        bool useHDR ,ShadowSettings shadowSettings, PostFXSettings postFXSettings, int colorLUTResolution, Shader cameraRendererShader )
    {
        m_UseDynamicBatching = useDynamicBatching;
        m_UseGPUInstancing = useGPUInstancing;
        m_UseSRPBatcher = useSRPBatching;
        m_UseLightsPerObject = useLightsPerObject;
        m_UseHDR = useHDR;
        m_ShadowSettings = shadowSettings;
        m_PostFXSettings = postFXSettings;
        m_ColorLUTResolution = colorLUTResolution;

        GraphicsSettings.useScriptableRenderPipelineBatching = m_UseSRPBatcher;
        GraphicsSettings.lightsUseLinearIntensity = true;

        m_CameraRenderer = new CameraRenderer(cameraRendererShader);

        InitializeForEditor();
    }

    protected override void Render( ScriptableRenderContext context, Camera[] cameras )
    {

        foreach( Camera camera in cameras)
        {
            m_CameraRenderer.Render(context, camera, m_UseDynamicBatching, m_UseGPUInstancing, m_UseLightsPerObject , m_UseHDR,m_ShadowSettings, 
                m_PostFXSettings, m_ColorLUTResolution);
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        DisposeForEditor();
        m_CameraRenderer.Dispose();
    }
}
