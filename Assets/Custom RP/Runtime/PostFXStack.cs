using UnityEngine;
using UnityEngine.Rendering;
using static PostFXSettings;

// ��ע�� ���StackĿǰ�ǰ��� CatLikeCoding ��SRP�̴̳��ܵġ������������ø��ֲ�ͬ��Ч�����Ѻá�
// todo: �ο�PPv2 �����Լ���xzj��Ŀ�������� Stack�͵�����Ч��Render()���н�������ܹ����������ط�����ʵ����Ŀ�и����������ò�ͬ��Ч��ϡ�
public partial class PostFXStack
{

    enum Pass
    {
        BloomAdd,
        BloomScatter,
        BloomScatterFinal,
        BloomHorizontal,
        BloomPrefilter,
        BloomPrefilterFireflies, // ���������˸������
        BloomVertical,
        ColorGradingAndToneMappingNone,
        ColorGradingAndACESToneMapping,
        ColorGradingAndNeutralToneMapping,
        COlorGradingAndReinhardToneMapping,
        ApplyColorGrading,
        FinalRescale,
        Copy,
        FXAA,
        FXAAWithLuma
    }

    const string bufferName = "Post FX";

    const string
        fxaaQualityLowKeyword = "FXAA_QUALITY_LOW",
        fxaaQualityMediumKeyword = "FXAA_QUALITY_MEDIUM";

    const int maxBloomPyramidLevels = 16;

    int colorLUTResolution;

    Vector2Int bufferSize;

    int
        bloomBicubicUpsamplingId = Shader.PropertyToID("_BloomBicubicUpsampling"),
        bloomIntensityId = Shader.PropertyToID("_BloomIntensity"),
        bloomPrefilterId = Shader.PropertyToID("_BloomPrefilter"),
        bloomThresholdId = Shader.PropertyToID("_BloomThreshold"),
        bloomResultId = Shader.PropertyToID("_BloomResultId"),
        fxSourceId = Shader.PropertyToID("_PostFXSource"),
        fxSource2Id = Shader.PropertyToID("_PostFXSource2"),
        colorAdjustmentsId = Shader.PropertyToID("_ColorAdjustments"),
        colorFilterId = Shader.PropertyToID("_ColorFilter"),
        whiteBalanceId = Shader.PropertyToID("_WhiteBalance"),
        splitToningShadowsId = Shader.PropertyToID("_SplitToningShadows"),
        splitToningHighlightsId = Shader.PropertyToID("_SplitToningHighlights"),
        channelMixerRedId = Shader.PropertyToID("_ChannelMixerRed"),
        channelMixerGreenId = Shader.PropertyToID("_ChannelMixerGreen"),
        channelMixerBlueId = Shader.PropertyToID("_ChannelMixerBlue"),
        smhShadowsId = Shader.PropertyToID("_SMHShadows"),
        smhMidtonesId = Shader.PropertyToID("_SMHMidtones"),
        smhHighlightsId = Shader.PropertyToID("_SMHHighlights"),
        smhRangeId = Shader.PropertyToID("_SMHRange"),
        colorGradingLUTId = Shader.PropertyToID("_ColorGradingLUT"),
        colorGradingLUTParametersId = Shader.PropertyToID("_ColorGradingLUTParameters"),
        colorGradingLUTInLogId = Shader.PropertyToID("_ColorGradingLUTInLogC"),
        enableColorGradingId = Shader.PropertyToID("_EnableColorGrading");

    int
        copyBicubicId = Shader.PropertyToID("_CopyBicubic"),
        finalResultId = Shader.PropertyToID("_FinalResult"),
        finalSrcBlendId = Shader.PropertyToID("_FinalSrcBlend"),
        finalDstBlendId = Shader.PropertyToID("_FinalDstBlend");

    int fxaaConfigId = Shader.PropertyToID("_FXAAConfig");

    CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName
    };

    ScriptableRenderContext context;

    Camera camera;

    PostFXSettings settings;

    CameraSettings.FinalBlendMode finalBlendMode;

    int bloomPyramidId;

    public bool IsActive => (settings != null) && enablePostFX; // ע�⣡ֻ��CameraSettings.overridePostFX = true ʱ��������settings�� IsActive �Ż�Ϊ false
    private bool enablePostFX = true;

    public bool useHDR;
    public bool enableColorGrading;

    CameraBufferSettings.BicubicRescalingMode bicubicRescaling;

    CameraBufferSettings.FXAA fxaa; // ��ʱ���������ع�PostFXStack֮ǰ����FXAA

    public PostFXStack()
    {
        bloomPyramidId = Shader.PropertyToID("_BloomPyramid0");
        for (int i = 1; i < maxBloomPyramidLevels * 2; i++)
        {
            Shader.PropertyToID("_BloomPyramid" + i);
        }
    }

    public void Setup(
        ScriptableRenderContext context, Camera camera, Vector2Int bufferSize , bool useHDR, PostFXSettings settings, 
        int colorLUTResolution, CameraSettings.FinalBlendMode finalBlendMode, bool enablePostFX, 
        CameraBufferSettings.BicubicRescalingMode bicubicRescaling, CameraBufferSettings.FXAA fxaa
    )
    {
        this.context = context;
        this.camera = camera;
        this.bufferSize = bufferSize;
        this.useHDR = useHDR;
        this.settings =
            camera.cameraType <= CameraType.SceneView ? settings : null;
        this.colorLUTResolution = colorLUTResolution;
        this.finalBlendMode = finalBlendMode;
        this.enablePostFX = enablePostFX;
        this.bicubicRescaling = bicubicRescaling;
        this.fxaa = fxaa;
        ApplySceneViewState();
    }

    public void Render(int sourceId)
    {

        if (DoBloom(sourceId))
        {
            DoFinal(bloomResultId);
            buffer.ReleaseTemporaryRT(bloomResultId);
        }
        else
        {
            DoFinal(sourceId);
        }
        
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    bool DoBloom(int sourceId)
    {
        
        PostFXSettings.BloomSettings bloom = settings.Bloom;

        int width, height;
        if (bloom.ignoreRenderScale)
        {
            width = camera.pixelWidth / 2;
            height = camera.pixelHeight / 2;
        }
        else
        {
            width = bufferSize.x / 2;
            height = bufferSize.y / 2;
        }

        if (
            bloom.maxIterations == 0 || bloom.intensity <= 0f ||
            height < bloom.downscaleLimit * 2 || width < bloom.downscaleLimit * 2
        )
        {
            return false;
        }

        buffer.BeginSample("Bloom");

        Vector4 threshold; // Soft Knee Curve for Threshold : https://catlikecoding.com/unity/tutorials/custom-srp/post-processing/
        threshold.x = Mathf.GammaToLinearSpace(bloom.threshold);
        threshold.y = threshold.x * bloom.thresholdKnee;
        threshold.z = 2f * threshold.y;
        threshold.w = 0.25f / (threshold.y + 0.00001f);
        threshold.y -= threshold.x;
        buffer.SetGlobalVector(bloomThresholdId, threshold);

        RenderTextureFormat format = useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;
        buffer.GetTemporaryRT( bloomPrefilterId, width, height, 0, FilterMode.Bilinear, format);
        buffer.GetTemporaryRT(bloomResultId, bufferSize.x, bufferSize.y, 0, FilterMode.Bilinear, format);
        Draw(sourceId, bloomPrefilterId, bloom.fadeFireFlies? Pass.BloomPrefilterFireflies: Pass.BloomPrefilter); // ʹ��Soft Knee Curve ����ֵ����ԭͼ����ֱ�Ӽ�ȥ��ֵҪ�õ�����͵�bloomЧ������
        width /= 2;
        height /= 2;

        int fromId = bloomPrefilterId, toId = bloomPyramidId + 1;
        int i;
        for (i = 0; i < bloom.maxIterations; i++)
        {
            if (height < bloom.downscaleLimit || width < bloom.downscaleLimit)
            {
                break;
            }
            int midId = toId - 1;
            buffer.GetTemporaryRT(
                midId, width, height, 0, FilterMode.Bilinear, format
            );
            buffer.GetTemporaryRT(
                toId, width, height, 0, FilterMode.Bilinear, format
            );
            Draw(fromId, midId, Pass.BloomHorizontal);
            Draw(midId, toId, Pass.BloomVertical);
            fromId = toId;
            toId += 2;
            width /= 2;
            height /= 2;
        }

        buffer.ReleaseTemporaryRT(bloomPrefilterId);
        buffer.SetGlobalFloat(
            bloomBicubicUpsamplingId, bloom.bicubicUpsampling ? 1f : 0f
        );

        Pass combinePass;
        Pass finalPass;
        float finalIntensity;
        if (bloom.mode == PostFXSettings.BloomSettings.Mode.Additive)
        {
            finalPass = combinePass = Pass.BloomAdd;
            buffer.SetGlobalFloat(bloomIntensityId, 1f);
            finalIntensity = bloom.intensity;
        }
        else
        {
            combinePass = Pass.BloomScatter;
            finalPass = Pass.BloomScatterFinal;
            buffer.SetGlobalFloat(bloomIntensityId, bloom.scatter);
            finalIntensity = Mathf.Min(bloom.intensity, 0.95f);
        }

        if (i > 1)
        {
            buffer.ReleaseTemporaryRT(fromId - 1);
            toId -= 5;
            for (i -= 1; i > 0; i--)
            {
                buffer.SetGlobalTexture(fxSource2Id, toId + 1);
                Draw(fromId, toId, combinePass);
                buffer.ReleaseTemporaryRT(fromId);
                buffer.ReleaseTemporaryRT(toId + 1);
                fromId = toId;
                toId -= 2;
            }
        }
        else
        {
            buffer.ReleaseTemporaryRT(bloomPyramidId);
        }
        buffer.SetGlobalFloat(bloomIntensityId, finalIntensity);
        buffer.SetGlobalTexture(fxSource2Id, sourceId); // ���һ���� fxSource2Id��ԭͼ
        Draw(fromId, bloomResultId, finalPass); 
        buffer.ReleaseTemporaryRT(fromId);
        buffer.EndSample("Bloom");
        return true;
    }

    bool DoFinal(int sourceId)
    {
        ConfigColorAdjustments();
        ConfigureWhiteBalance();
        ConfigureSplitToning();
        ConfigureChannelMixer();
        ConfigureShadowsMidtonesHighlights();
        buffer.BeginSample("Color Grading and ToneMapping");

        Pass pass = (int)settings.toneMappingSettings.mode + Pass.ColorGradingAndToneMappingNone;

        // �����Ƿ���Ҫ��ϵ�����frameBuffer�ϣ����� final Pass �Ļ��ģʽ��Ŀǰֻ�� colorGradingToneMapping���Pass����Ϊ finalPass
        buffer.SetGlobalFloat(finalSrcBlendId, (float)finalBlendMode.source);
        buffer.SetGlobalFloat(finalDstBlendId, (float)finalBlendMode.destination);

        if (enableColorGrading)
        {
            int lutWidth = colorLUTResolution * colorLUTResolution;
            int lutHeight = colorLUTResolution;
            buffer.GetTemporaryRT(colorGradingLUTId, lutWidth, lutHeight, 0, FilterMode.Bilinear, RenderTextureFormat.DefaultHDR);
            buffer.SetGlobalVector(colorGradingLUTParametersId, new Vector4(
                lutHeight, 0.5f / lutWidth, 0.5f / lutHeight, lutHeight / (lutHeight - 1f)
            ));

            buffer.SetGlobalFloat(colorGradingLUTInLogId, useHDR && pass != Pass.ColorGradingAndToneMappingNone ? 1.0f : 0.0f);

            Draw(sourceId, colorGradingLUTId, pass);

            buffer.SetGlobalVector(colorGradingLUTParametersId, new Vector4(
               1f / lutWidth, 1f / lutHeight, lutHeight - 1f, 0.0f
            ));
            DrawFinalWithScale(sourceId, Pass.ApplyColorGrading);
            buffer.ReleaseTemporaryRT(colorGradingLUTId);
        }
        else
        {
            DrawFinalWithScale(sourceId, pass);
        }

        buffer.EndSample("Color Grading and ToneMapping");
        return true;
    }

    void ConfigColorAdjustments()
    {
        ColorAdjustmentsSettings colorAdjustments = settings.ColorAdjustments;
        enableColorGrading = colorAdjustments.eanbleColorAdjust;
        buffer.SetGlobalFloat(enableColorGradingId, enableColorGrading ? 1f : 0.0f);
        if (!enableColorGrading) return;

        buffer.SetGlobalVector(colorAdjustmentsId, new Vector4(
            Mathf.Pow(2f, colorAdjustments.postExposure),
            colorAdjustments.contrast * 0.01f + 1f,
            colorAdjustments.hueShift * (1f / 360f),
            colorAdjustments.saturation * 0.01f + 1f
        ));
        buffer.SetGlobalColor(colorFilterId, colorAdjustments.colorFilter.linear);
    }

    void ConfigureWhiteBalance()
    {
        WhiteBalanceSettings whiteBalance = settings.WhiteBalance;
        buffer.SetGlobalVector(whiteBalanceId, ColorUtils.ColorBalanceToLMSCoeffs(
            whiteBalance.temperature, whiteBalance.tint
        ));
    }

    void ConfigureSplitToning()
    {
        SplitToningSettings splitToning = settings.SplitToning;
        Color splitColor = splitToning.shadows;
        splitColor.a = splitToning.balance * 0.01f;
        buffer.SetGlobalColor(splitToningShadowsId, splitColor);
        buffer.SetGlobalColor(splitToningHighlightsId, splitToning.highlights);
    }

    void ConfigureChannelMixer()
    {
        ChannelMixerSettings channelMixer = settings.ChannelMixer;
        buffer.SetGlobalVector(channelMixerRedId, channelMixer.red);
        buffer.SetGlobalVector(channelMixerGreenId, channelMixer.green);
        buffer.SetGlobalVector(channelMixerBlueId, channelMixer.blue);
    }

    void ConfigureShadowsMidtonesHighlights()
    {
        ShadowsMidtonesHighlightsSettings smh = settings.ShadowsMidtonesHighlights;
        buffer.SetGlobalColor(smhShadowsId, smh.shadows.linear);
        buffer.SetGlobalColor(smhMidtonesId, smh.midtones.linear);
        buffer.SetGlobalColor(smhHighlightsId, smh.highlights.linear);
        buffer.SetGlobalVector(smhRangeId, new Vector4(
            smh.shadowsStart, smh.shadowsEnd, smh.highlightsStart, smh.highLightsEnd
        ));
    }

    void Draw(RenderTargetIdentifier from, RenderTargetIdentifier to, Pass pass)
    {
        buffer.SetGlobalTexture(fxSourceId, from);
        buffer.SetRenderTarget(
            to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store
        );
        buffer.DrawProcedural(
            Matrix4x4.identity, settings.Material, (int)pass,
            MeshTopology.Triangles, 3
        );
    }

    /// <summary>
    /// <para>���� scale ��Ŀ���������� ColorGrading��ToneMapping Ҳ�� bufferSize�����������޷���������GPU������
    /// ��������bufferSize�±��LDR���������ŵ� CameraTargetʱ�Ͳ����׳��� HDR���Ų����ľ�ݻ�仯�����������⡣</para>
    /// ��HDR��LDR�Ĳ��������⣺�Լ��ڱ༭����û�й۲쵽���²���ToneMapping֮����ɫֵ�����Ѿ�û�г���1��ֵ���µģ����ر�ToneMapping�Բ����ԣ���
    /// </summary>
    /// <param name="from"></param>
    /// <param name="pass"></param>
    void DrawFinalWithScale(RenderTargetIdentifier from, Pass pass)
    {
        if (bufferSize.x == camera.pixelWidth)
        {
            DrawFinal(from, pass);
        }
        else
        {
            float bicubicSampling = (bicubicRescaling == CameraBufferSettings.BicubicRescalingMode.UpAndDown) ? 1.0f :
                    ((bicubicRescaling == CameraBufferSettings.BicubicRescalingMode.UpOnly && bufferSize.x < camera.pixelWidth) ? 1.0f : 0.0f);
            buffer.SetGlobalFloat(copyBicubicId, bicubicSampling);
            buffer.SetGlobalFloat(finalSrcBlendId, 1f);
            buffer.SetGlobalFloat(finalDstBlendId, 0f);
            buffer.GetTemporaryRT(
                finalResultId, bufferSize.x, bufferSize.y, 0,
                FilterMode.Bilinear, RenderTextureFormat.Default 
            ); // ע�����������RT�Ѿ��� LDR ����
            Draw(from, finalResultId, pass);
            DrawFinal(finalResultId, Pass.FinalRescale);
            buffer.ReleaseTemporaryRT(finalResultId);
        }
    }

    void DrawFinal(RenderTargetIdentifier from, Pass pass)
    {
        buffer.SetGlobalTexture(fxSourceId, from);
        buffer.SetRenderTarget( BuiltinRenderTextureType.CameraTarget, 
            finalBlendMode.destination == BlendMode.Zero ? RenderBufferLoadAction.DontCare : RenderBufferLoadAction.Load, 
            RenderBufferStoreAction.Store);
        buffer.SetViewport(camera.pixelRect);
        buffer.DrawProcedural(
            Matrix4x4.identity, settings.Material, (int)pass,
            MeshTopology.Triangles, 3
        );
    }
}