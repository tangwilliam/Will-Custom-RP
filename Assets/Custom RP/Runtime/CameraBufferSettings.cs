using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

[System.Serializable]
public class CameraBufferSettings
{
    public bool allowHDR;

    public bool copyColor,copyColorReflections,copyDepth, copyDepthReflections;

    [Range(0.1f,2f)]
    public float renderScale;

    public enum BicubicRescalingMode { Off, UpOnly, UpAndDown }; // UpSample时，Bicubic能够减少blocky的效果，取而代之为一定的模糊。详见：https://stackoverflow.com/questions/13501081/efficient-bicubic-filtering-code-in-glsl
    public BicubicRescalingMode bicubicRescaling;

    [Serializable]
    public struct FXAA
    {

        public bool enabled;

        [Range(0.0312f, 0.0833f)]
        public float fixedThreshold;

        [Range(0.063f, 0.333f)]
        public float relativeThreshold;

        [Range(0f, 1f)]
        public float subpixelBlending;

        public enum Quality { Low, Medium, High }

        public Quality quality;
    }

    [HideInInspector]
    public FXAA fxaa; // 暂时打算在重构PostFXStack之前，不增加FXAA
}
