using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using Unity.Collections;

class Lighting
{
    const int m_MaxDirLightCount = 4;
    const int m_MaxOtherLightCount = 64;

    static int
        s_DirLightCountId = Shader.PropertyToID("_DirectionalLightCount"),
        s_DirLightColorsId = Shader.PropertyToID("_DirectionalLightColors"),
        s_DirLightDirectionsAndMasksId = Shader.PropertyToID("_DirectionalLightDirectionsAndMasks"),
        s_DirLightShadowDataId = Shader.PropertyToID("_DirectionalLightShadowData"),

        s_OtherLightCountId = Shader.PropertyToID("_OtherLightCount"),
        s_OtherLightColorsId = Shader.PropertyToID("_OtherLightColors"),
        s_OtherLightPositionsId = Shader.PropertyToID("_OtherLightPositions"),
        s_OtherLightDirectionsAndMasksId = Shader.PropertyToID("_OtherLightDirectionsAndMasks"),
        s_OtherLightSpotAnglesId = Shader.PropertyToID("_OtherLightSpotAngles"),
        s_OtherLightShadowDataId = Shader.PropertyToID("_OtherLightShadowData");

    static Vector4[]
        s_DirLightColors = new Vector4[m_MaxDirLightCount],
        s_DirLightDirectionsAndMasks = new Vector4[m_MaxDirLightCount],
        s_DirLightShadowData = new Vector4[m_MaxDirLightCount],

        s_OtherLightColors = new Vector4[m_MaxOtherLightCount],
        s_OtherLightPositions = new Vector4[m_MaxOtherLightCount],
        s_OtherLightDirectionsAndMasks = new Vector4[m_MaxOtherLightCount],
        s_OtherLightSpotAngles = new Vector4[m_MaxOtherLightCount],
        s_OtherLightShadowData = new Vector4[m_MaxOtherLightCount];

    const string m_LightsPerObjectKeywordName = "_LIGHTS_PER_OBJECT";
    private const string m_BufferName = "Lighting";
    private CommandBuffer m_CommandBuffer = new CommandBuffer
    {
        name = m_BufferName
    };
    private CullingResults m_CullingResults;
   
    private Shadows m_Shadows = new Shadows();

    public void Setup( ScriptableRenderContext context, CullingResults cullingResults, ShadowSettings shadowSettings, bool useLightsPerObject, int renderingLayerMask)
    {
        m_CullingResults = cullingResults;

        m_CommandBuffer.BeginSample(m_BufferName);
        m_Shadows.Setup( context, cullingResults, shadowSettings );
        SetupLights( useLightsPerObject, renderingLayerMask ); 
        m_Shadows.Render(); 
        m_CommandBuffer.EndSample(m_BufferName);
        context.ExecuteCommandBuffer(m_CommandBuffer);
        m_CommandBuffer.Clear();
    }


    public void Cleanup()
    {
        m_Shadows.Cleanup();
    }

    /// <summary>
    /// <para>参数1用于按指定顺序存储光照强度、阴影信息等到若干个不同数组中，以在Shader中使用时能够对应起来;并按同样顺序使用相应级联信息投射阴影且将级联信息传递给Shader。</para>
    /// <para>参数2用于投射阴影时传给CullingResult来获取光的相关几何数据</para>
    /// </summary>
    /// <param name="index"></param>
    /// <param name="visibleIndex"></param>
    /// <param name="visibleLight"></param>
    private void SetupDirectionalLight( int index , int visibleIndex , ref VisibleLight visibleLight, Light light )
    {
        s_DirLightColors[index] = visibleLight.finalColor;
        // Unity的坐标系中的习惯是模型空间的Forward是z正方向，即模型空间中物体是面向z正方向的。
        // 于是对于直射光源这种不存在缩放的物体，世界空间矩阵的第三列就正好是Forward在世界空间的朝向。
        s_DirLightDirectionsAndMasks[index] = -visibleLight.localToWorldMatrix.GetColumn(2);
        s_DirLightDirectionsAndMasks[index].w = light.renderingLayerMask.ReinterpretAsFloat();

        s_DirLightShadowData[index] = m_Shadows.ReserveDirectionalShadows( light, visibleIndex);
    }

    private void SetupPointLight( int index, int visibleIndex, ref VisibleLight visibleLight, Light light)
    {
        s_OtherLightColors[index] = visibleLight.finalColor;

        s_OtherLightDirectionsAndMasks[index] = Vector4.zero; // 对点光源来说方向没有意义
        s_OtherLightDirectionsAndMasks[index].w = light.renderingLayerMask.ReinterpretAsFloat();

        Vector4 position = visibleLight.localToWorldMatrix.GetColumn(3);
        position.w = 1.0f / Mathf.Max(visibleLight.range * visibleLight.range , 0.00001f);
        s_OtherLightPositions[index] = position;
        s_OtherLightSpotAngles[index] = new Vector4(0, 1); // 这个值使Shader中不被SpotAtten影响
        s_OtherLightShadowData[index] = m_Shadows.ReserveOtherLightShadows(light, visibleIndex);
    }

    private void SetupSpotLight(int index, int visibleIndex, ref VisibleLight visibleLight, Light light)
    {
        s_OtherLightColors[index] = visibleLight.finalColor;
        Vector4 position = visibleLight.localToWorldMatrix.GetColumn(3);
        position.w = 1.0f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
        s_OtherLightPositions[index] = position;
        s_OtherLightDirectionsAndMasks[index] = -visibleLight.localToWorldMatrix.GetColumn(2);
        s_OtherLightDirectionsAndMasks[index].w = light.renderingLayerMask.ReinterpretAsFloat();

        float innerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * light.innerSpotAngle);
        float outerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * visibleLight.spotAngle);
        float angleRangeInv = 1f / Mathf.Max(innerCos - outerCos, 0.001f);
        s_OtherLightSpotAngles[index] = new Vector4( angleRangeInv, -outerCos * angleRangeInv );
        s_OtherLightShadowData[index] = m_Shadows.ReserveOtherLightShadows(light, visibleIndex);
    }

    /// <summary>
    /// 设置光的颜色、方向等给GPU，并传递光的索引用于后续采样Shadowmap（让用于投射阴影的m_ShadowedDirectionalLights[] 和用于Shader中采样的 s_DirLightShadowData[]的每个item都对应同一盏光）
    /// </summary>
    private void SetupLights( bool useLightsPerObject, int renderingLayerMask )
    {
        // LightsPerObject 的原理：
        // GetLightIndexMap() 返回CullingResult包含的所有光的IndexMap，indexMap[i]对应的是其中第i个光，
        // 遍历到第i个光时，发现它是OtherLight，则将其值设置为OtherLight将传入Shader的光索引(循环体中当时的otherLightCount,通过该索引能够获取光颜色等信息)。
        // 其他光都设置为 -1，并将新 IndexMap设回CullingResult。Unity会在LightIndexMap中对于存入-1的光，不计入shader中的unity_LightIndices 和unity_LightingData
        // Shader中 unity_LightingData.y会指示对于当前物体，IndexMap中对它作用的光有几盏，unity_LightIndices中存放这几盏光在C#中传入的索引otherLightCount
        // unity_LightIndices只能存8盏光的索引，所以Shader中要手动限制循环在8以内，否则若unity_LightingData.y超出8时，计算得到的数据是错误的。
        // unity_LightingData，unity_LightIndices逐物体不同。Unity会根据每物体，从IndexMap中挑出它需要的光，存入unity_LightIndices，并算出对应个数unity_LightingData.y
        // 一般小型物件都能得到附近所有的光照计算，而大型物件比如大面积的地面，该方案效果不佳。
        // 不启用LightsPerObject时，OtherLightCount可能达到几十个。Shader中每个物体的每个片元都将对这几十个OtherLight求 GetLighting()。
        NativeArray<int> indexMap = useLightsPerObject ? m_CullingResults.GetLightIndexMap(Allocator.Temp) : default;

        NativeArray<VisibleLight> visibleLights = m_CullingResults.visibleLights;

        int dirLightCount = 0, otherLightCount = 0;

        int i = 0;
        for(; i < visibleLights.Length; i++)
        {
            int newIndex = -1;
            VisibleLight visibleLight = visibleLights[i];
            Light light = visibleLight.light;
            if((light.renderingLayerMask & renderingLayerMask )!= 0)
            {
                switch (visibleLights[i].lightType)
                {
                    case LightType.Directional:
                        {
                            if (dirLightCount < m_MaxDirLightCount)
                            {
                                SetupDirectionalLight(dirLightCount++, i, ref visibleLight, light);
                            }
                        }
                        break;
                    case LightType.Point:
                        {
                            if (otherLightCount < m_MaxOtherLightCount)
                            {
                                newIndex = otherLightCount;
                                SetupPointLight(otherLightCount++, i, ref visibleLight, light);
                            }
                        }
                        break;
                    case LightType.Spot:
                        {
                            if (otherLightCount < m_MaxOtherLightCount)
                            {
                                newIndex = otherLightCount;
                                SetupSpotLight(otherLightCount++, i, ref visibleLight, light);
                            }
                        }
                        break;
                    default: break;
                }
            }
            

            if (useLightsPerObject)
            {
                indexMap[i] = newIndex;
            }
        }


        if (useLightsPerObject)
        {
            for(; i < indexMap.Length; i++)
            {
                indexMap[i] = -1;
            }
            m_CullingResults.SetLightIndexMap(indexMap);
            indexMap.Dispose();

            m_CommandBuffer.EnableShaderKeyword(m_LightsPerObjectKeywordName);
        }
        else
        {
            m_CommandBuffer.DisableShaderKeyword(m_LightsPerObjectKeywordName);
        }

        m_CommandBuffer.SetGlobalInt(s_DirLightCountId, dirLightCount );
        if(dirLightCount > 0)
        {
            m_CommandBuffer.SetGlobalVectorArray(s_DirLightColorsId, s_DirLightColors);
            m_CommandBuffer.SetGlobalVectorArray(s_DirLightDirectionsAndMasksId, s_DirLightDirectionsAndMasks);
            m_CommandBuffer.SetGlobalVectorArray(s_DirLightShadowDataId, s_DirLightShadowData);
        }
        m_CommandBuffer.SetGlobalInt(s_OtherLightCountId, otherLightCount);
        if (otherLightCount > 0)
        {
            m_CommandBuffer.SetGlobalVectorArray(s_OtherLightColorsId, s_OtherLightColors);
            m_CommandBuffer.SetGlobalVectorArray(s_OtherLightPositionsId, s_OtherLightPositions);
            m_CommandBuffer.SetGlobalVectorArray(s_OtherLightDirectionsAndMasksId, s_OtherLightDirectionsAndMasks);
            m_CommandBuffer.SetGlobalVectorArray(s_OtherLightSpotAnglesId, s_OtherLightSpotAngles);
            m_CommandBuffer.SetGlobalVectorArray(s_OtherLightShadowDataId, s_OtherLightShadowData);
        }

    }



}
