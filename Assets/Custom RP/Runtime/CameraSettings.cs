using System;
using UnityEngine.Rendering;
using UnityEngine;

[Serializable]
public class CameraSettings
{
    /// <summary>
    /// renderingLayerMask的优势是更灵活，一个物体可以同时属于多个layer。
    /// 然而要注意要裁剪大量物体时，还是CullingMask效率更高，因为在Cull阶段裁掉，就不再需要后续其他操作了。
    /// </summary>
    [RenderingLayerMaskField]
    public int renderingLayerMask = -1;

    /// <summary>
    /// 一般不需要使用。可以用在单独相机照在原点的角色，受特定的光照影响的情况。只需要设置相机的RenderingLayerMask以及光的RenderingLayerMask。
    /// 比如角色Renderer设置为Layer1 | Layer2, 主场景光照和主场景相机设置为Layer1， 原点处专门照单个角色模型的光照和相机设置为Layer2。
    /// 当然类似的需求也可以通过将角色Renderer设置为 Layer2 来解决。
    /// </summary>
    public bool maskLights = false;

    public bool enablePostFX = true;
    public PostFXSettings postFXSettings = null;
    public bool copyColor, copyDepth = false;

    [Serializable]
    public struct FinalBlendMode
    {

        public BlendMode source, destination;
    }

    /// <summary>
    /// 正常（默认）：Blend One Zero
    /// 相机要直接叠加到已有FrameBuffer上： Blend One OneMinusSrcAlpha
    /// 相机渲染到半透RT，并用UI显示： Blend One Zero， UI 要用 One OneMinusSrcAlpha（UI如果用 SrcAlpha则无bloom出的 additive效果）
    /// </summary>
    public FinalBlendMode finalBlendMode = new FinalBlendMode
    {
        source = BlendMode.One,
        destination = BlendMode.Zero
    };
}