using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEditor;

 public partial class CameraRenderer
{
    //------------------------------
    // Variables

    partial void DrawUnsupportedShaders();

    partial void DrawGizmosBeforeFX();
    partial void DrawGizmosAfterFX();

    partial void PrepareBuffer();

    partial void PrepareForSceneWindow();

#if UNITY_EDITOR
    static ShaderTagId[] s_LegacyShaderTagIds =
    {
        new ShaderTagId("Always"),
        new ShaderTagId("ForwardBase"),
        new ShaderTagId("PrepassBase"),
        new ShaderTagId("Vertex"),
        new ShaderTagId("VertexLMRGBM"),
        new ShaderTagId("VertexLM")
    };
    static Material s_ErrorShaderMaterial = new Material(Shader.Find("Hidden/InternalErrorShader"));
   

    //------------------------------
    // Methods

 
    partial void DrawUnsupportedShaders()
    {
        var sortingSettings = new SortingSettings(m_Camera);
        var drawingSettings = new DrawingSettings(s_LegacyShaderTagIds[0], sortingSettings)
        {
            overrideMaterial = s_ErrorShaderMaterial
        };
        for(int i = 1; i < s_LegacyShaderTagIds.Length; i++)
        {
            drawingSettings.SetShaderPassName(i, s_LegacyShaderTagIds[i]);
        }
        var filteringSettings = FilteringSettings.defaultValue;
        m_Context.DrawRenderers(m_CullingResults, ref drawingSettings, ref filteringSettings);

    }

    partial void DrawGizmosBeforeFX()
    {
        if (Handles.ShouldRenderGizmos())
        {
            m_Context.DrawGizmos(m_Camera, GizmoSubset.PreImageEffects);
        }
    }
    partial void DrawGizmosAfterFX()
    {
        if (Handles.ShouldRenderGizmos())
        {
            m_Context.DrawGizmos(m_Camera, GizmoSubset.PostImageEffects);
        }
    }

    string m_SampleName { get; set; }

    /// <summary>
    /// 为每个相机的 CommandBuffer 设置名称。以便在FrameDebugger或Profiler中调试跟踪
    /// </summary>
    partial void PrepareBuffer()
    {
        Profiler.BeginSample("Editor Only");
        //每帧都在拿m_Camera.name，每帧都在生成一个string，这样会导致GC。所以只在编辑器状态下用。
        m_CommondBuffer.name = m_SampleName = m_Camera.name;
        Profiler.EndSample();
    }


    partial void PrepareForSceneWindow()
    {
        if(m_Camera.cameraType == CameraType.SceneView)
        {
            ScriptableRenderContext.EmitWorldGeometryForSceneView(m_Camera);
        }
    }


#else

    // build中就不再专门根据不同的相机来设置 CommandBuffer.name。 避免GC
    const string m_SampleName = m_BufferName;

#endif

}
