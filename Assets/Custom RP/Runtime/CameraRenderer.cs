using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

public partial class CameraRenderer
{
    //------------------------------
    // Variables

    // 针对WebGL 2.0 等平台， CopyTexture()方法不能使用。故需要使用 Draw()去绘制深度图。虽然效率低了点，但至少可用
    static bool s_CopyTextureSupported = SystemInfo.copyTextureSupport > CopyTextureSupport.None; 

    static ShaderTagId
    s_UnlitShaderTagId = new ShaderTagId("SRPDefaultUnlit"),
    s_LitShaderTagId = new ShaderTagId("CustomLit");
    static int
        s_ColorAttachmentId = Shader.PropertyToID("_CameraColorAttachment"),
        s_DepthAttachmentId = Shader.PropertyToID("_CameraDepthAttachment"),
        s_FrameBufferId = Shader.PropertyToID("_CameraFrameBuffer"),
        s_SourceTextureId = Shader.PropertyToID("_SourceTexture"),
        s_ColorTextureId = Shader.PropertyToID("_CameraColorTexture"),
        s_DepthTextureId = Shader.PropertyToID("_CameraDepthTexture"),
        s_SrcBlendId = Shader.PropertyToID("_CameraSrcBlend"),
        s_DstBlendId = Shader.PropertyToID("_CameraDstBlend");

    static CameraSettings s_DefaultCameraSettings = new CameraSettings();

    const string m_BufferName = "Render Camera";

    ScriptableRenderContext m_Context;
    Camera m_Camera;
    CommandBuffer m_CommondBuffer = new CommandBuffer { name = m_BufferName };

    CullingResults m_CullingResults;

    Lighting m_Lighting = new Lighting();
    PostFXStack m_PostFXStack = new PostFXStack();
    int m_PostFXTargetId;
    bool m_UseColorTexture;
    bool m_UseDepthTexture;
    int m_RtWidth;
    int m_RtHeight;

    bool m_UseHDR;

    Material m_Material;

    Texture2D m_MissingTexture;

    //------------------------------
    // Methods

    public CameraRenderer( Shader shader)
    {
        m_Material = CoreUtils.CreateEngineMaterial(shader);

        m_MissingTexture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave, name = "Custom Missing" };
        m_MissingTexture.SetPixel(0, 0, Color.white * 0.5f);
        m_MissingTexture.Apply();
    }
  
    public void Dispose()
    {
        CoreUtils.Destroy(m_Material);
        CoreUtils.Destroy(m_MissingTexture);
    }

    protected void Setup()
    {
        // 设置VP矩阵等Camera使用的 Global Shader Uniform
        m_Context.SetupCameraProperties(m_Camera);

        CameraClearFlags flag = m_Camera.clearFlags;

        if ( m_UseColorTexture || m_UseDepthTexture )
        {
            if(flag > CameraClearFlags.Color)
            {
                flag = CameraClearFlags.Color;
            }
            m_CommondBuffer.GetTemporaryRT(s_ColorAttachmentId, m_RtWidth, m_RtHeight, 0, FilterMode.Bilinear, 
                m_UseHDR? RenderTextureFormat.DefaultHDR:RenderTextureFormat.Default);
            m_CommondBuffer.GetTemporaryRT(s_DepthAttachmentId, m_RtWidth, m_RtHeight, 32, FilterMode.Point,
                RenderTextureFormat.Depth);
            m_CommondBuffer.SetRenderTarget(s_ColorAttachmentId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
                                            s_DepthAttachmentId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            m_PostFXTargetId = s_ColorAttachmentId;
        }
        else
        {
            // 即便不做后效不用深度图也仍然要申请RT,因为性能分级往往要缩放 RenderTarget
            m_CommondBuffer.GetTemporaryRT(s_FrameBufferId, m_RtWidth, m_RtHeight, 32, FilterMode.Bilinear,
                m_UseHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);
            m_CommondBuffer.SetRenderTarget(s_FrameBufferId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            m_PostFXTargetId = s_FrameBufferId;
        }

        m_CommondBuffer.ClearRenderTarget( flag <= CameraClearFlags.Depth , flag == CameraClearFlags.Color, 
            flag == CameraClearFlags.Color ? m_Camera.backgroundColor.linear : Color.clear );
        m_CommondBuffer.BeginSample(m_SampleName);
        m_CommondBuffer.SetGlobalTexture(s_ColorTextureId, m_MissingTexture);
        m_CommondBuffer.SetGlobalTexture(s_DepthTextureId, m_MissingTexture);

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

    protected void DrawVisibleGeometry( bool useDynamicBatching, bool useGPUInstancing, bool useLightsPerObject, int renderingLayerMask )
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
        var filteringSettings = new FilteringSettings(RenderQueueRange.opaque, renderingLayerMask : (uint)renderingLayerMask); // renderingLayerMask的优势是更灵活，一个物体可以同时属于多个layer。然而要注意要裁剪大量物体时，还是CullingMask效率更高，因为在Cull阶段裁掉，就不再需要后续其他操作了。
        m_Context.DrawRenderers(m_CullingResults, ref drawingSettings, ref filteringSettings);

        m_Context.DrawSkybox(m_Camera);

        if(m_UseColorTexture || m_UseDepthTexture)
        {
            CopyAttachments(); // 拷贝颜色缓冲或深度图
        }

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

    public void Render(ScriptableRenderContext context, Camera camera, bool useDynamicBatching, bool useGPUInstancing, bool useLightsPerObject , 
        CameraBufferSettings cameraBufferSettings,ShadowSettings shadowSettings, PostFXSettings defaultPostFXSettings, int colorLUTResolution)
    {
        m_Context = context;
        m_Camera = camera;
        var crpCamera = m_Camera.GetComponent<CustomRenderPipelineCamera>(); // todo: 优化为不要每帧GetComponent()
        CameraSettings cameraSettings = crpCamera ? crpCamera.Settings : s_DefaultCameraSettings;
        PostFXSettings postFXSettings = (cameraSettings.postFXSettings != null) ? cameraSettings.postFXSettings : defaultPostFXSettings;

        if(m_Camera.cameraType == CameraType.Reflection)
        {
            m_UseColorTexture = cameraBufferSettings.copyColorReflections;
            m_UseDepthTexture = cameraBufferSettings.copyDepthReflections; // Reflection Probe 等反射相机是否使用深度图只能通过管线设置来设置
        }
        else
        {
            m_UseColorTexture = cameraSettings.copyColor && cameraBufferSettings.copyColor;
            m_UseDepthTexture = cameraSettings.copyDepth && cameraBufferSettings.copyDepth;
        }
        
        m_UseHDR = camera.allowHDR && cameraBufferSettings.allowHDR;
        m_RtWidth = Mathf.RoundToInt(m_Camera.pixelWidth * cameraSettings.renderTargetScale);
        m_RtHeight = Mathf.RoundToInt(m_Camera.pixelHeight * cameraSettings.renderTargetScale);

        PrepareBuffer(); // 在使用CommandBuffer之前，为它准备好名字。以便在FrameDebugger或Profiler中调试跟踪。不放在后面的Setup()中是因为要让编辑器下逐相机命名，但build则不这么做。
        PrepareForSceneWindow(); // For UI to Show in SceneView

        if (!Cull(shadowSettings.m_MaxDistance))
        {
            return;
        }

        m_CommondBuffer.BeginSample(m_BufferName);
        ExecuteCommandBuffer();
        int lightsMask = cameraSettings.maskLights ? cameraSettings.renderingLayerMask : -1;
        m_Lighting.Setup(m_Context, m_CullingResults, shadowSettings, useLightsPerObject, lightsMask ); // 该步骤不仅设置了光照数据，还渲染了Shadowmap
        m_PostFXStack.Setup(m_Context, m_Camera, m_RtWidth, m_RtHeight, m_UseHDR, postFXSettings, colorLUTResolution, cameraSettings.finalBlendMode, cameraSettings.enablePostFX);
        m_CommondBuffer.EndSample(m_BufferName);

        Setup(); // 根据相机参数设置绘制所需的变量，并将 PrepareBuffer()时获取到的名字设置给 m_CommondBuffer.BeginSample(),以便调试

        DrawVisibleGeometry( useDynamicBatching, useGPUInstancing, useLightsPerObject, cameraSettings.renderingLayerMask );
        DrawUnsupportedShaders();
        DrawGizmosBeforeFX();

        if (m_PostFXStack.IsActive)
        {
            m_PostFXStack.Render(m_PostFXTargetId, m_PostFXTargetId);
        }

        DrawFinal(cameraSettings.finalBlendMode); // 实际项目中这个步骤是渲染到一张RT上，然后用UI相机渲染的一个全屏UI去采样这张RT。这里目前仅仅是将结果拷贝到屏幕上。目的是方便控制渲染分辨率

        DrawGizmosAfterFX();

        Cleanup(); 
        Submit();
    }

    void DrawFinal( CameraSettings.FinalBlendMode finalBlendMode  )
    {
        m_CommondBuffer.SetGlobalFloat(s_SrcBlendId, (float)finalBlendMode.source);
        m_CommondBuffer.SetGlobalFloat(s_DstBlendId, (float)finalBlendMode.destination);

        int sourceId;
        if ( m_UseColorTexture || m_UseDepthTexture)
        {
            sourceId = s_ColorAttachmentId;
        }
        else
        {
            sourceId = s_FrameBufferId;
        }
        Draw(sourceId, BuiltinRenderTextureType.CameraTarget);

        m_CommondBuffer.SetGlobalFloat(s_SrcBlendId, 1f);
        m_CommondBuffer.SetGlobalFloat(s_DstBlendId, 0f);
    }

    void Draw( RenderTargetIdentifier from, RenderTargetIdentifier to, bool isDepth = false)
    {
        m_CommondBuffer.SetGlobalTexture(s_SourceTextureId, from);
        m_CommondBuffer.SetRenderTarget( to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        m_CommondBuffer.DrawProcedural(Matrix4x4.identity, m_Material, isDepth ? 1 : 0, MeshTopology.Triangles, 3);
    }

    void CopyAttachments()
    {
        if (m_UseColorTexture)
        {
            m_CommondBuffer.GetTemporaryRT(s_ColorTextureId, m_RtWidth, m_RtHeight, 0, FilterMode.Bilinear,
                m_UseHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);

            if (s_CopyTextureSupported)
            {
                m_CommondBuffer.CopyTexture(s_ColorAttachmentId, s_ColorTextureId);
            }
            else
            {
                Draw(s_ColorAttachmentId, s_ColorTextureId);
            }
        }

        if (m_UseDepthTexture)
        {
            m_CommondBuffer.GetTemporaryRT(s_DepthTextureId, m_RtWidth, m_RtHeight, 32, FilterMode.Point, RenderTextureFormat.Depth);

            if (s_CopyTextureSupported)
            {
                m_CommondBuffer.CopyTexture(s_DepthAttachmentId, s_DepthTextureId);
            }
            else
            {
                Draw(s_DepthAttachmentId, s_DepthTextureId, true);
            }
        }

        if (!s_CopyTextureSupported)
        {
            m_CommondBuffer.SetRenderTarget(s_ColorAttachmentId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
                                            s_DepthAttachmentId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        }

        ExecuteCommandBuffer();
    }

    void Cleanup()
    {
        m_Lighting.Cleanup();// 释放Shadowmap等操作

        if ( m_UseColorTexture || m_UseDepthTexture)
        {
            m_CommondBuffer.ReleaseTemporaryRT(s_ColorAttachmentId);
            m_CommondBuffer.ReleaseTemporaryRT(s_DepthAttachmentId);

            if (m_UseColorTexture)
            {
                m_CommondBuffer.ReleaseTemporaryRT(s_ColorTextureId);
            }
            if (m_UseDepthTexture)
            {
                m_CommondBuffer.ReleaseTemporaryRT(s_DepthTextureId);
            }
        }
        else
        {
            m_CommondBuffer.ReleaseTemporaryRT(s_FrameBufferId);
        }
    }
}
