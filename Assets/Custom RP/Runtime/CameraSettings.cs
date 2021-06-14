using System;
using UnityEngine.Rendering;

[Serializable]
public class CameraSettings
{

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