using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

[System.Serializable]
public class CameraBufferSettings
{
    public bool allowHDR;

    public bool copyColor,copyColorReflections,copyDepth, copyDepthReflections;
}
