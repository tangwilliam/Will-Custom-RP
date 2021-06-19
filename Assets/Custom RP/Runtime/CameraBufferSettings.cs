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
}
