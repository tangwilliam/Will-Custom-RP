using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

public partial class CameraRenderer
{
    //------------------------------
    // Variables

    static ShaderTagId
    s_UnlitShaderTagId = new ShaderTagId("SRPDefaultUnlit"),
    s_LitShaderTagId = new ShaderTagId("CustomLit");
    static int s_FrameBufferId = Shader.PropertyToID("_CameraFrameBuffer");
    static CameraSettings s_DefaultCameraSettings = new CameraSettings();

    const string m_BufferName = "Render Camera";

    ScriptableRenderContext m_Context;
    Camera m_Camera;
    CommandBuffer m_CommondBuffer = new CommandBuffer { name = m_BufferName };

    CullingResults m_CullingResults;

    Lighting m_Lighting = new Lighting();
    PostFXStack m_PostFXStack = new PostFXStack();

    bool m_UseHDR;

    

    //------------------------------
    // Methods

    protected void Setup()
    {
        // 设置VP矩阵等Camera使用的 Global Shader Uniform
        m_Context.SetupCameraProperties(m_Camera);

        CameraClearFlags flag = m_Camera.clearFlags;

        if (m_PostFXStack.IsActive) // 根据是否开启后效，判断Setup时要不要申请一张RT作为RenderTarget，如果有后效这张RT是要能拿到做后效的
        {
            if(flag > CameraClearFlags.Color)
            {
                flag = CameraClearFlags.Color;
            }
            m_CommondBuffer.GetTemporaryRT(s_FrameBufferId, m_Camera.pixelWidth, m_Camera.pixelHeight, 32, FilterMode.Bilinear, m_UseHDR? RenderTextureFormat.DefaultHDR:RenderTextureFormat.Default);
            m_CommondBuffer.SetRenderTarget(s_FrameBufferId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        }

        m_CommondBuffer.ClearRenderTarget( flag <= CameraClearFlags.Depth , flag == CameraClearFlags.Color, 
            flag == CameraClearFlags.Color ? m_Camera.backgroundColor.linear : Color.clear );
        m_CommondBuffer.BeginSample(m_SampleName);
        ExecuteCommandBuffer();

    }


    protected bool Cull( float maxShadowDistance )
    {
        if (m_Camera.TryGetCullingParameters(out ScriptableCullingParameters p))
        {
            p.shadowDistance = maxShadowDistance;
            m_CullingResults = m_Context.Cull(ref p);
            return true;
        }
        return false;
    }

    protected void DrawVisibleGeometry( bool useDynamicBatching, bool useGPUInstancing, bool useLightsPerObject )
    {
        PerObjectData lightsPerObjectFlags = useLightsPerObject ? (PerObjectData.LightIndices | PerObjectData.LightData) : PerObjectData.None;

        // Draw Opaque
        var sortingSettings = new SortingSettings(m_Camera) { criteria = SortingCriteria.CommonOpaque };
        var drawingSettings = new DrawingSettings(s_UnlitShaderTagId, sortingSettings) {
            enableDynamicBatching = useDynamicBatching,
            enableInstancing = useGPUInstancing,
            perObjectData = PerObjectData.ReflectionProbes 
                    | PerObjectData.Lightmaps | PerObjectData.ShadowMask 
                    | PerObjectData.LightProbe | PerObjectData.OcclusionProbe 
                    | PerObjectData.LightProbeProxyVolume | PerObjectData.OcclusionProbeProxyVolume
                    | lightsPerObjectFlags
        };
        // We can draw multiple passes by invoking SetShaderPassName on the drawing settings with a draw order index and tag as arguments.
        // 在CameraRender.Editor中我们使用这个接口将"Always""ForwardBase""ForwardAdd"等 lightMode的Pass依次绘制出来。
        // 这里第0个Pass已经是"SRPDefaultUnlit"，于是设置新的Pass从第1个开始。
        // SetShaderPassName为Pass设置的index顺序，并不影响物体绘制顺序。编辑器中实测仍以sortingSettings为主（比如由近到远）。
        drawingSettings.SetShaderPassName(1, s_LitShaderTagId);
        var filteringSettings = new FilteringSettings(RenderQueueRange.opaque);
        m_Context.DrawRenderers(m_CullingResults, ref drawingSettings, ref filteringSettings);

        m_Context.DrawSkybox(m_Camera);

        // Draw Transparent
        sortingSettings.criteria = SortingCriteria.CommonTransparent;
        filteringSettings.renderQueueRange = RenderQueueRange.transparent;
        m_Context.DrawRenderers(m_CullingResults, ref drawingSettings, ref filteringSettings);

    }



    protected void Submit()
    {
        m_CommondBuffer.EndSample(m_SampleName);
        ExecuteCommandBuffer();

        m_Context.Submit();
    }

    protected void ExecuteCommandBuffer()
    {
        m_Context.ExecuteCommandBuffer(m_CommondBuffer);
        m_CommondBuffer.Clear();
    }

    public void Render(ScriptableRenderContext context, Camera camera, bool useDynamicBatching, bool useGPUInstancing, bool useLightsPerObject , bool useHDR,ShadowSettings shadowSettings, PostFXSettings postFXSettings, int colorLUTResolution)
    {
        m_Context = context;
        m_Camera = camera;
        var crpCamera = m_Camera.GetComponent<CustomRenderPipelineCamera>(); // todo: 优化为不要每帧GetComponent()
        CameraSettings cameraSettings = crpCamera ? crpCamera.Settings : s_DefaultCameraSettings;
        m_UseHDR = camera.allowHDR && useHDR;

        PrepareBuffer(); // 在使用CommandBuffer之前，为它准备好名字。以便在FrameDebugger或Profiler中调试跟踪。不放在后面的Setup()中是因为要让编辑器下逐相机命名，但build则不这么做。
        PrepareForSceneWindow(); // For UI to Show in SceneView

        if (!Cull(shadowSettings.m_MaxDistance))
        {
            return;
        }

        m_CommondBuffer.BeginSample(m_BufferName);
        ExecuteCommandBuffer();
        m_Lighting.Setup(m_Context, m_CullingResults, shadowSettings, useLightsPerObject); // 该步骤不仅设置了光照数据，还渲染了Shadowmap
        m_PostFXStack.Setup(m_Context, m_Camera, m_UseHDR, postFXSettings, colorLUTResolution, cameraSettings.finalBlendMode);
        m_CommondBuffer.EndSample(m_BufferName);

        Setup(); // 根据相机参数设置绘制所需的变量，并将 PrepareBuffer()时获取到的名字设置给 m_CommondBuffer.BeginSample(),以便调试

        DrawVisibleGeometry( useDynamicBatching, useGPUInstancing, useLightsPerObject );
        DrawUnsupportedShaders();
        DrawGizmosBeforeFX();
        if (m_PostFXStack.IsActive)
        {
            m_PostFXStack.Render(s_FrameBufferId);
        }
        DrawGizmosAfterFX();

        Cleanup(); 
        Submit();
    }

    void Cleanup()
    {
        m_Lighting.Cleanup();// 释放Shadowmap等操作

        if (m_PostFXStack.IsActive)
        {
            m_CommondBuffer.ReleaseTemporaryRT(s_FrameBufferId);
        }
    }
}
