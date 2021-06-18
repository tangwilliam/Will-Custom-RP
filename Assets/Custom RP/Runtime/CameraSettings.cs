using System;
using UnityEngine.Rendering;
using UnityEngine;

[Serializable]
public class CameraSettings
{
    /// <summary>
    /// renderingLayerMask�������Ǹ���һ���������ͬʱ���ڶ��layer��
    /// Ȼ��Ҫע��Ҫ�ü���������ʱ������CullingMaskЧ�ʸ��ߣ���Ϊ��Cull�׶βõ����Ͳ�����Ҫ�������������ˡ�
    /// </summary>
    [RenderingLayerMaskField]
    public int renderingLayerMask = -1;

    /// <summary>
    /// һ�㲻��Ҫʹ�á��������ڵ����������ԭ��Ľ�ɫ�����ض��Ĺ���Ӱ��������ֻ��Ҫ���������RenderingLayerMask�Լ����RenderingLayerMask��
    /// �����ɫRenderer����ΪLayer1 | Layer2, ���������պ��������������ΪLayer1�� ԭ�㴦ר���յ�����ɫģ�͵Ĺ��պ��������ΪLayer2��
    /// ��Ȼ���Ƶ�����Ҳ����ͨ������ɫRenderer����Ϊ Layer2 �������
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