using UnityEngine;
using UnityEngine.Rendering;

public class Shadows
{
    static int
        s_DirShadowAtlasId = Shader.PropertyToID("_DirectionalShadowAtlas"),
        s_DirShadowMatricesId = Shader.PropertyToID("_DirectionalShadowMatrices"),
        s_CascadeCountId = Shader.PropertyToID("_CascadeCount"),
        s_CascadeCullingSpheresId = Shader.PropertyToID("_CascadeCullingSpheres"),
        s_ShadowDistanceFadeId = Shader.PropertyToID("_ShadowDistanceFade"),
        s_ShadowPancakingId = Shader.PropertyToID("_ShadowPancaking"), // Only Enable shadow pancaking when casting Diectional Shadow
        s_CascadeDataId = Shader.PropertyToID("_CascadeData"),
        s_ShadowAtlasSizeId = Shader.PropertyToID("_ShadowAtlasSize"),

        s_OtherShadowAtlasId = Shader.PropertyToID("_OtherShadowAtlas"),
        s_OtherShadowMatricesId = Shader.PropertyToID("_OtherShadowMatrices"),
        s_OtherShadowAtlasSizeId = Shader.PropertyToID("_OtherShadowAtlasSize"),
        s_OtherShadowTilesId = Shader.PropertyToID("_OtherShadowTiles");

    static Matrix4x4[] s_DirShadowMatrices = new Matrix4x4[m_MaxShadowedDirectionalLightCount * m_MaxCascades];
    static Matrix4x4[] s_OtherShadowMatrices = new Matrix4x4[m_MaxShadowedOtherLightCount];
    static Vector4[] s_OtherShadowTiles = new Vector4[m_MaxShadowedOtherLightCount];
    static Vector4[] s_CascadeCullingSpheres = new Vector4[m_MaxCascades];
    static Vector4[] s_CascadeData = new Vector4[m_MaxCascades];

    static string[] s_DirectionalFilterKeywords = {
        "_DIRECTIONAL_PCF3",
        "_DIRECTIONAL_PCF5",
        "_DIRECTIONAL_PCF7",
    };
    static string[] s_OtherLightFilterKeywords = {
        "_OTHER_PCF3",
        "_OTHER_PCF5",
        "_OTHER_PCF7",
    };

    static string[] s_CascadeBlendModeKeywords =
    {
        "_CASCADE_BLEND_SOFT",
        "_CASCADE_BLEND_DITHER"
    };

    static string[] s_ShadowMaskKeywords =
    {
        "_SHADOW_MASK_ALWAYS",
        "_SHADOW_MASK_DISTANCE"
    };

    const string m_BufferName = "Shadows";

    CommandBuffer m_CommandBuffer = new CommandBuffer
    {
        name = m_BufferName
    };

    bool m_UseShadowMask;

    ScriptableRenderContext m_Context;

    CullingResults m_CullingResults;

    ShadowSettings m_ShadowSettings;

    const int m_MaxShadowedDirectionalLightCount = 4, m_MaxShadowedOtherLightCount = 16;
    const int m_MaxCascades = 4;

    int m_ShadowedDirectionalLightCount, m_ShadowedOtherLightCount;

    Vector4 m_AtlasSizes;

    struct ShadowedDirectionalLight
    {
        public int visibleLightIndex;
        public float slopeScaleBias;
        public float nearPlaneOffset;
    }
    ShadowedDirectionalLight[] m_ShadowedDirectionalLights = new ShadowedDirectionalLight[m_MaxShadowedDirectionalLightCount];

    struct ShadowedOtherLight
    {
        public bool isPoint;
        public int visibleLightIndex;
        public float slopeScaleBias;
        public float normalBias;
    }
    ShadowedOtherLight[] m_ShadowedOtherLights = new ShadowedOtherLight[m_MaxShadowedOtherLightCount];

    public void Setup(
        ScriptableRenderContext context, CullingResults cullingResults,
        ShadowSettings settings
    )
    {
        this.m_Context = context;
        this.m_CullingResults = cullingResults;
        this.m_ShadowSettings = settings;
        this.m_UseShadowMask = false;

        m_ShadowedDirectionalLightCount = m_ShadowedOtherLightCount = 0;

    }

    /// <summary>
    /// 根据CullingResult和光序号，计算VP矩阵（投射阴影和采样shadowmap时都用到）等参数，调用 Context.DrawShadows()投射阴影，即绘制Shadowmap
    /// </summary>
    public void Render()
    {
        if (m_ShadowedDirectionalLightCount > 0)
        {
            RenderDirectionalShadows();
        }
        else
        {
            // 为处理 WebGL 2.0 等平台在没有创建RT而释放该id的RT时会报错的问题，而创建一个无用的RT用来Cleanup()时释放。
            m_CommandBuffer.GetTemporaryRT(s_DirShadowAtlasId, 1, 1, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
        }

        if(m_ShadowedOtherLightCount > 0)
        {
            RenderOtherShadows();
        }
        else
        {
            m_CommandBuffer.GetTemporaryRT(s_OtherShadowAtlasId, 1, 1, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
        }

        m_CommandBuffer.BeginSample(m_BufferName);
        SetKeywords(s_ShadowMaskKeywords, m_UseShadowMask ? 
            ( QualitySettings.shadowmaskMode == ShadowmaskMode.DistanceShadowmask ? 1 : 0 ) : -1); // ShadowMask

        m_CommandBuffer.SetGlobalInt(s_CascadeCountId, m_ShadowSettings.directional.cascadeCount); // 放这里是因为没有直射光也需要设置这个值
        float f = 1f - m_ShadowSettings.directional.cascadeFade;
        m_CommandBuffer.SetGlobalVector(s_ShadowDistanceFadeId,
            new Vector4(1f / m_ShadowSettings.m_MaxDistance, 1f / m_ShadowSettings.m_DistanceFade, 1f / (1f - f * f)));

        m_CommandBuffer.SetGlobalVector(s_ShadowAtlasSizeId, m_AtlasSizes);

        m_CommandBuffer.EndSample(m_BufferName);
        ExecuteCommandBuffer();
    }

    public void Cleanup()
    {
        m_CommandBuffer.ReleaseTemporaryRT(s_DirShadowAtlasId);
        if(m_ShadowedOtherLightCount > 0)
        {
            m_CommandBuffer.ReleaseTemporaryRT(s_OtherShadowAtlasId);
        }

        ExecuteCommandBuffer();
    }

    void RenderDirectionalShadows()
    {
        m_AtlasSizes = Vector4.zero;
        int atlasSize = (int)m_ShadowSettings.directional.atlasSize;
        m_AtlasSizes.x = atlasSize;
        m_AtlasSizes.y = 1f / atlasSize;
        m_CommandBuffer.GetTemporaryRT(s_DirShadowAtlasId, atlasSize, atlasSize, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
        m_CommandBuffer.SetRenderTarget(s_DirShadowAtlasId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        m_CommandBuffer.ClearRenderTarget(true, false, Color.clear);
        m_CommandBuffer.SetGlobalFloat(s_ShadowPancakingId, 1.0f);
        m_CommandBuffer.BeginSample(m_BufferName);
        ExecuteCommandBuffer();

        int tiles = m_ShadowedDirectionalLightCount * m_ShadowSettings.directional.cascadeCount; // 切块数量取决于 光数量 和 级联数量
        int split = tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4;
        int tileSize = atlasSize / split;

        for ( int i = 0; i < m_ShadowedDirectionalLightCount; i++)
        {
            RenderDirectionalShadows(i, split, tileSize);
        }

        m_CommandBuffer.SetGlobalMatrixArray(s_DirShadowMatricesId, s_DirShadowMatrices);
        m_CommandBuffer.SetGlobalVectorArray(s_CascadeCullingSpheresId, s_CascadeCullingSpheres);
        m_CommandBuffer.SetGlobalVectorArray(s_CascadeDataId, s_CascadeData);

        SetKeywords( s_DirectionalFilterKeywords, (int)m_ShadowSettings.directional.filter - 1 );
        SetKeywords( s_CascadeBlendModeKeywords , (int)m_ShadowSettings.directional.cascadeBlendMode - 1);

        

        m_CommandBuffer.EndSample(m_BufferName);
        ExecuteCommandBuffer();

    }

    void SetKeywords( string[] keywords, int enabledIndex)
    {
        for (int i = 0; i < keywords.Length; i++)
        {
            if (i == enabledIndex)
            {
                m_CommandBuffer.EnableShaderKeyword(keywords[i]);
            }
            else
            {
                m_CommandBuffer.DisableShaderKeyword(keywords[i]);
            }
        }
    }

    void RenderDirectionalShadows(int index, int split, int tileSize)
    {

        ShadowedDirectionalLight light = m_ShadowedDirectionalLights[index];
        var shadowDrawSettings =
            new ShadowDrawingSettings(m_CullingResults, light.visibleLightIndex)
            {
                useRenderingLayerMaskTest = true
            };

        int cascadeCount = m_ShadowSettings.directional.cascadeCount;
        Vector3 cascadeRatios = m_ShadowSettings.directional.CascadeRatios;
        for (int i = 0; i < cascadeCount; i++)
        {
            // 求取光的几何数据：每盏光的光源空间VP矩阵、级联splitSphere
            // 求取splitData 以在投射阴影时只调用相应空间内的几何体的 Shadow Cast Pass
            m_CullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(
                light.visibleLightIndex, i, cascadeCount, cascadeRatios, tileSize,light.nearPlaneOffset, 
                out Matrix4x4 viewMatrix, out Matrix4x4 projectionMatrix, out ShadowSplitData splitData);

            // 减少同一个物体重复在不同级联里投射阴影的情况，适当做cull。值越接近1，就cull得越多。如果发现级联相接处出现空洞，那么有可能是它cull得太多。
            float cullingFactor = Mathf.Max(0f, 0.8f - m_ShadowSettings.directional.cascadeFade);
            splitData.shadowCascadeBlendCullingFactor = cullingFactor; 

            shadowDrawSettings.splitData = splitData;

            if(index == 0)
            {
                SetCascadedData(i, splitData.cullingSphere, tileSize);
            }

            // 计算并记录每盏光在采样其shadowmap时需要使用的变换矩阵
            int tileIndex = index * cascadeCount + i;
            float tileScale = 1f / split;
            s_DirShadowMatrices[tileIndex] = ConvertToAtlasMatrix(projectionMatrix * viewMatrix, SetTileViewport(tileIndex , split, tileSize), tileScale);

            // 使用Context 绘制阴影。这步骤需要准备：通过CommandBuffer设置好绘制时的VP矩阵，调用 DrawShadows()进行绘制。
            // DrawShadows()的参数包含 cullingResults 和 lightIndex来定位哪些物体需要Cast Shadow。
            m_CommandBuffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
            m_CommandBuffer.SetGlobalDepthBias(0f, light.slopeScaleBias);
            ExecuteCommandBuffer(); // 注意这里要执行CommandBuffer了，这样才能确保前面的这些先执行，然后再 DrawShadows()。

            m_Context.DrawShadows(ref shadowDrawSettings);
            m_CommandBuffer.SetGlobalDepthBias(0f, 0f);
        }
        
    }

    void RenderOtherShadows()
    {
        int atlasSize = (int)m_ShadowSettings.otherLight.atlasSize;
        m_AtlasSizes.z = atlasSize;
        m_AtlasSizes.w = 1f / atlasSize;
        m_CommandBuffer.GetTemporaryRT(s_OtherShadowAtlasId, atlasSize, atlasSize, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
        m_CommandBuffer.SetRenderTarget(s_OtherShadowAtlasId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        m_CommandBuffer.ClearRenderTarget(true, false, Color.clear);
        m_CommandBuffer.SetGlobalFloat(s_ShadowPancakingId, 0.0f);
        m_CommandBuffer.BeginSample(m_BufferName);
        ExecuteCommandBuffer();

        int tiles = m_ShadowedOtherLightCount; // 切块数量取决于 光数量 和 级联数量
        int split = tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4;
        int tileSize = atlasSize / split;

        
        for (int i = 0; i < m_ShadowedOtherLightCount; )
        {
            if(m_ShadowedOtherLights[i].isPoint)
            {
                RenderPointShadows(i, split, tileSize);
                i += 6;
            }
            else
            {
                RenderSpotShadows(i, split, tileSize);
                i += 1;
            }
        }

        m_CommandBuffer.SetGlobalMatrixArray(s_OtherShadowMatricesId, s_OtherShadowMatrices);
        m_CommandBuffer.SetGlobalVectorArray(s_OtherShadowTilesId, s_OtherShadowTiles);

        SetKeywords(s_OtherLightFilterKeywords, (int)m_ShadowSettings.otherLight.filter - 1);

        m_CommandBuffer.EndSample(m_BufferName);
        ExecuteCommandBuffer();

    }

    void RenderSpotShadows(int index, int split, int tileSize)
    {

        ShadowedOtherLight light = m_ShadowedOtherLights[index];
        var shadowDrawSettings =
            new ShadowDrawingSettings(m_CullingResults, light.visibleLightIndex)
            {
                useRenderingLayerMaskTest = true
            };

        // 求取光的几何数据：每盏光的光源空间VP矩阵、级联splitSphere
        // 求取splitData 以在投射阴影时只调用相应空间内的几何体的 Shadow Cast Pass
        m_CullingResults.ComputeSpotShadowMatricesAndCullingPrimitives(light.visibleLightIndex, out Matrix4x4 viewMatrix, out Matrix4x4 projMatrix, out ShadowSplitData splitData);
       
        shadowDrawSettings.splitData = splitData;

        // 计算并记录每盏光在采样其shadowmap时需要使用的变换矩阵
        int tileIndex = index;
        float tileScale = 1.0f / split;// Shadowmap(UV范围(0,1))中单个tile所占的UV大小，如 0.5,0.25等
        Vector2 offset = SetTileViewport(tileIndex, split, tileSize); // 本offset是整数
        s_OtherShadowMatrices[tileIndex] = ConvertToAtlasMatrix(projMatrix * viewMatrix, offset , tileScale);

        Vector2 offsetInShadowMapUV = offset * tileScale; // Shadowmap(UV范围(0,1))中本tile在xy方向的起始UV值。如(0,0.5)
        float texelSize = (1.0f / (float)tileSize) * (1.0f / projMatrix.m00) * 2.0f; // texelSizeForOrth * tanθ * 2 
        float filterSize = texelSize * (int)m_ShadowSettings.otherLight.filter + 1f; // filter越大，单像素采样ShadowMap时涉及的像素范围越大
        float bias = light.normalBias * filterSize * 1.4142136f; // 注意Shader中不要再乘light.normalBias了
        SetOtherShadowTilesData(index, offsetInShadowMapUV, tileScale , bias);

        // 使用Context 绘制阴影。这步骤需要准备：通过CommandBuffer设置好绘制时的VP矩阵，调用 DrawShadows()进行绘制。
        // DrawShadows()的参数包含 cullingResults 和 lightIndex来定位哪些物体需要Cast Shadow。
        m_CommandBuffer.SetViewProjectionMatrices(viewMatrix, projMatrix);
        m_CommandBuffer.SetGlobalDepthBias(0f, light.slopeScaleBias);
        ExecuteCommandBuffer(); // 注意这里要执行CommandBuffer了，这样才能确保前面的这些先执行，然后再 DrawShadows()。

        m_Context.DrawShadows(ref shadowDrawSettings);
        m_CommandBuffer.SetGlobalDepthBias(0f, 0f);
        
    }

    void RenderPointShadows(int index, int split, int tileSize)
    {

        ShadowedOtherLight light = m_ShadowedOtherLights[index];
        var shadowDrawSettings =
            new ShadowDrawingSettings(m_CullingResults, light.visibleLightIndex)
            {
                useRenderingLayerMaskTest = true
            };

        float texelSize = 2.0f / (float)tileSize; // The field of view for cubemap faces is always 90°
        float filterSize = texelSize * (int)m_ShadowSettings.otherLight.filter + 1f; // filter越大，单像素采样ShadowMap时涉及的像素范围越大
        float bias = light.normalBias * filterSize * 1.4142136f; // 注意Shader中不要再乘light.normalBias了
        float tileScale = 1.0f / split;// Shadowmap(UV范围(0,1))中单个tile所占的UV大小，如 0.5,0.25等
        float fovBias = Mathf.Atan(1f + bias + filterSize) * Mathf.Rad2Deg * 2f - 90f;
        for (int i = 0; i < 6; i++)
        {
            // 求取光的几何数据：每盏光的光源空间VP矩阵、级联splitSphere
            // 求取splitData 以在投射阴影时只调用相应空间内的几何体的 Shadow Cast Pass
            m_CullingResults.ComputePointShadowMatricesAndCullingPrimitives(light.visibleLightIndex, (CubemapFace)i, fovBias, out Matrix4x4 viewMatrix, out Matrix4x4 projMatrix, 
                                                                                out ShadowSplitData splitData);

            // 这是为了应对Unity为Point Light 渲染 SHadowMap时上下颠倒导致的是三角面的反面的问题，暂未理解，详见：https://catlikecoding.com/unity/tutorials/custom-srp/point-and-spot-shadows/
            viewMatrix.m11 = -viewMatrix.m11;
            viewMatrix.m12 = -viewMatrix.m12;
            viewMatrix.m13 = -viewMatrix.m13;

            shadowDrawSettings.splitData = splitData;

            // 计算并记录每盏光在采样其shadowmap时需要使用的变换矩阵
            int tileIndex = index + i;
            
            Vector2 offset = SetTileViewport(tileIndex, split, tileSize); // 本offset是整数
            s_OtherShadowMatrices[tileIndex] = ConvertToAtlasMatrix(projMatrix * viewMatrix, offset, tileScale);

            Vector2 offsetInShadowMapUV = offset * tileScale; // Shadowmap(UV范围(0,1))中本tile在xy方向的起始UV值。如(0,0.5)
            SetOtherShadowTilesData(tileIndex, offsetInShadowMapUV, tileScale, bias);

            // 使用Context 绘制阴影。这步骤需要准备：通过CommandBuffer设置好绘制时的VP矩阵，调用 DrawShadows()进行绘制。
            // DrawShadows()的参数包含 cullingResults 和 lightIndex来定位哪些物体需要Cast Shadow。
            m_CommandBuffer.SetViewProjectionMatrices(viewMatrix, projMatrix);
            m_CommandBuffer.SetGlobalDepthBias(0f, light.slopeScaleBias);
            ExecuteCommandBuffer(); // 注意这里要执行CommandBuffer了，这样才能确保前面的这些先执行，然后再 DrawShadows()。

            m_Context.DrawShadows(ref shadowDrawSettings);
            m_CommandBuffer.SetGlobalDepthBias(0f, 0f);
        }

    }

    void SetOtherShadowTilesData( int index , Vector2 offset, float tileScale , float bias)
    {
        float border = 0.5f * m_AtlasSizes.w;
        Vector4 data;
        data.x = offset.x + border;
        data.y = offset.y + border;
        data.z = tileScale - 2f * border;
        data.w = bias;
        s_OtherShadowTiles[index] = data;
    }
    

    /// <summary>
    /// <para>m_CullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives() 会根据级联的设置，计算出每个splitSphere的位置和大小。</para>
    /// <para>每个splitSphere都会作为Uniform帮助Shader逐片元地知道自己身处第几个级联，以使用正确的uv区域采样shadowmap</para>
    /// </summary>
    /// <param name="index"></param>
    /// <param name="cullingSphere"></param>
    /// <param name="tileSize"></param>
    void SetCascadedData( int index, Vector4 cullingSphere, float tileSize)
    {
        float texelSize = ( 2f * cullingSphere.w / tileSize );
        float filterSize = texelSize * ((float)m_ShadowSettings.directional.filter + 1f);
        cullingSphere.w -= filterSize;
        cullingSphere.w *= cullingSphere.w;
        s_CascadeCullingSpheres[index] = cullingSphere;
        s_CascadeData[index] = new Vector4(1f / cullingSphere.w, filterSize * 1.4142136f);
    }


    /// <summary>
    /// <para>将(-w,w)的齐次裁剪空间的坐标变换到ShadowMap的(0,w)的齐次裁剪纹理空间。因为是图集，所以要考虑Offset和split。</para>
    /// <para>步骤：将点坐标从(-w,w)变换到(0,w)，然后根据split缩小坐标点的位置（理解为将每个大的视锥体都缩成小视锥体），然后根据offset在xy方向移动平移每个小视锥体。</para>
    /// <para>因为结果仍然是齐次裁剪空间的(所有tiles总共起来范围是(0,w)，注意非(-w,w))，对于透视投影的光源(spot/point)，在Shader中乘以本矩阵后，还要除以w，才能将众小视锥体变成小立方体，用于ShadowMap采样</para>
    /// <para>本函数得出的转换矩阵，用于Shader中：将世界空间的点坐标，变换到光源空间、然后进一步变换到图集形式的光源的齐次裁剪空间。</para>
    /// 为什么不直接将坐标变换到(-1,1)再做split和offset，或者变换到(0,1）再做split和offset ？ 因为矩阵乘法无法实现除以w的计算。
    /// </summary>
    /// <param name="m"></param>
    /// <param name="offset"></param>
    /// <param name="scale"></param>
    /// <returns></returns>
    Matrix4x4 ConvertToAtlasMatrix( Matrix4x4 m, Vector2 offset, float scale)
    {
        if (SystemInfo.usesReversedZBuffer)
        {
            m.m20 = -m.m20;
            m.m21 = -m.m21;
            m.m22 = -m.m22;
            m.m23 = -m.m23;
        }

        // 其实就是先乘以 scale(scale = 1/split = tileSize) 的缩放矩阵(缩放xy)，再乘以 offset*scale 的位移(只位移xy)矩阵。为了避免少算一些无效的矩阵元素，改成了直接计算其中的部分元素。
        m.m00 = (0.5f * (m.m00 + m.m30) + offset.x * m.m30) * scale;
        m.m01 = (0.5f * (m.m01 + m.m31) + offset.x * m.m31) * scale;
        m.m02 = (0.5f * (m.m02 + m.m32) + offset.x * m.m32) * scale;
        m.m03 = (0.5f * (m.m03 + m.m33) + offset.x * m.m33) * scale;
        m.m10 = (0.5f * (m.m10 + m.m30) + offset.y * m.m30) * scale;
        m.m11 = (0.5f * (m.m11 + m.m31) + offset.y * m.m31) * scale;
        m.m12 = (0.5f * (m.m12 + m.m32) + offset.y * m.m32) * scale;
        m.m13 = (0.5f * (m.m13 + m.m33) + offset.y * m.m33) * scale;
        m.m20 = 0.5f * (m.m20 + m.m30);
        m.m21 = 0.5f * (m.m21 + m.m31);
        m.m22 = 0.5f * (m.m22 + m.m32);
        m.m23 = 0.5f * (m.m23 + m.m33);

        return m;
    }

    Vector2 SetTileViewport(int index, int split, float tileSize)
    {
        Vector2 offset = new Vector2(index % split, index / split);
        m_CommandBuffer.SetViewport(new Rect(
            offset.x * tileSize, offset.y * tileSize, tileSize, tileSize
        ));
        return offset;
    }

    void ExecuteCommandBuffer()
    {
        m_Context.ExecuteCommandBuffer(m_CommandBuffer);
        m_CommandBuffer.Clear();
    }

    /// <summary>
    /// <para>记录可见的每盏光的阴影强度和相应序号。这个visibleLight序号可以用于渲染阴影时定位light而正确计算其调用Cast Shadow Pass 的VP矩阵，以及谁要投射阴影。</para> 
    /// <para>该函数存入ShadowData的是Shader采样Shadowmap需要的shadowStrength和light的索引（该索引从0开始连续递增，指示在处理场景中要处理的光中的第几个，用于从矩阵数组中选用对应采样Shadowmap的矩阵）</para>
    /// <para>该函数给m_ShadowedDirectionalLights[]数组从0开始依次填入投射阴影的光的数据（如visibleLightIndex用于传给CullingResult找到对应光的splitSphere等数据），Cast Shadows时会按照本数组的顺序，本函数的返回值填入Shader采样用的数组s_DirLightShadowData[]，于是两个数组顺序对应起来了</para>
    /// </summary>
    /// <param name="light"></param>
    /// <param name="visibleLightIndex"></param>
    /// <returns></returns>
    public Vector4 ReserveDirectionalShadows(Light light, int visibleLightIndex)
    {

        if (m_ShadowedDirectionalLightCount < m_MaxShadowedDirectionalLightCount &&
            light.shadows != LightShadows.None && light.shadowStrength > 0f )
        {
            int maskChannel = -1;
            LightBakingOutput lightBaking = light.bakingOutput;
            if (lightBaking.lightmapBakeType == LightmapBakeType.Mixed && lightBaking.mixedLightingMode == MixedLightingMode.Shadowmask)
            {
                m_UseShadowMask = true;
                maskChannel = lightBaking.occlusionMaskChannel; // 每盏光对应的存储在ShadowMask烘焙贴图中的通道
            }

            if (!m_CullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds b)) // 当相机远离物体到：最大的投射阴影的splitSphere内都没有该光能投射的物体时，在ShadowMask等模式下，在Shader中渲染那么远的物体时要计算该光烘焙的ShadowMask
            {
                return new Vector4(-light.shadowStrength, 0, 0, maskChannel);
            }

            m_ShadowedDirectionalLights[m_ShadowedDirectionalLightCount] =
                new ShadowedDirectionalLight
                {
                    visibleLightIndex = visibleLightIndex,
                    slopeScaleBias = light.shadowBias,
                    nearPlaneOffset = light.shadowNearPlane
                };

            return new Vector4(light.shadowStrength, 
                                m_ShadowSettings.directional.cascadeCount * m_ShadowedDirectionalLightCount++, // Shader中用于tileIndex计算
                                 light.shadowNormalBias,
                                 maskChannel );
        }

        return new Vector4(0f,0f,0f,-1f); // 最后一项-1 保证不会采样烘焙的ShadowMask
    }

    public Vector4 ReserveOtherLightShadows(Light light, int visibleLightIndex)
    {
        
        if (light.shadows == LightShadows.None || light.shadowStrength <= 0f)
        {
            return new Vector4(0f, 0f, 0f, -1f);
        }

        int maskChannel = -1;
        LightBakingOutput lightBaking = light.bakingOutput;
        if (lightBaking.lightmapBakeType == LightmapBakeType.Mixed && lightBaking.mixedLightingMode == MixedLightingMode.Shadowmask)
        {
            m_UseShadowMask = true;
            maskChannel = lightBaking.occlusionMaskChannel; // 每盏光对应的存储在ShadowMask烘焙贴图中的通道
        }

        bool isPoint = (light.type == LightType.Point) ? true : false;
        int newLightCount = m_ShadowedOtherLightCount + (isPoint ? 6 : 1);
        if ( newLightCount > m_MaxShadowedOtherLightCount || // 当计数超过最大允许值，则采样烘焙的光照贴图
            !m_CullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds b)) // 当相机远离物体到：最大的投射阴影的splitSphere内都没有该光能投射的物体时，在ShadowMask等模式下，在Shader中渲染那么远的物体时要计算该光烘焙的ShadowMask
        {
            return new Vector4(-light.shadowStrength, 0f, 0f, maskChannel);
        }

        m_ShadowedOtherLights[m_ShadowedOtherLightCount] = new ShadowedOtherLight
        {
            isPoint = isPoint,
            visibleLightIndex = visibleLightIndex,
            slopeScaleBias = light.shadowBias,
            normalBias = light.shadowNormalBias
        };

        Vector4 data = new Vector4(light.shadowStrength, m_ShadowedOtherLightCount, isPoint ? 1f : 0f, maskChannel);
        m_ShadowedOtherLightCount = newLightCount;
        return data;
    }
}