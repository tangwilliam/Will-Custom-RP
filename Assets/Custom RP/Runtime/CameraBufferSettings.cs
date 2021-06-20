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

}
