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
    /// ������Ĭ�ϣ���Blend One Zero
    /// ���Ҫֱ�ӵ��ӵ�����FrameBuffer�ϣ� Blend One OneMinusSrcAlpha
    /// �����Ⱦ����͸RT������UI��ʾ�� Blend One Zero�� UI Ҫ�� One OneMinusSrcAlpha��UI����� SrcAlpha����bloom���� additiveЧ����
    /// </summary>
    public FinalBlendMode finalBlendMode = new FinalBlendMode
    {
        source = BlendMode.One,
        destination = BlendMode.Zero
    };
}