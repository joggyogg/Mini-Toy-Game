using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.XR;

#if !UNITY_2023_1_OR_NEWER
using System.Reflection;
#endif

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace JeffGrawAssets.FlexibleUI
{
public class FlexibleBlurFeature : ScriptableRendererFeature 
{
    private enum FilterMode { Point, Bilinear }

#if UNITY_EDITOR
    public static readonly string RenderPassEventFieldName = nameof(renderPassEvent);
    public static readonly string DestinationFilterModeFieldName = nameof(destinationFilterMode);
    public static readonly string UIBlurLayersSeeLowerFieldName = nameof(uiBlurLayersSeeLower);
    public static readonly string BlurredImagesSeeUIBlursFieldName = nameof(blurredImagesSeeUIBlurs);
    public static readonly string BlurredImageLayersSeeLowerFieldName = nameof(blurredImageLayersSeeLower);
    public static readonly string UseComputeShadersFieldName = nameof(useComputeShaders);
    public static readonly string ResultFormatFieldName = nameof(resultFormat);
    public static readonly string BlurFormatFieldName = nameof(blurFormat);
    public static readonly string PlatformDataFieldName = nameof(platformData);
    public static readonly string LayerResolutionRatioFieldName = nameof(layerResolutionRatio);
    public static readonly string MaxLayerResolutionFieldName = nameof(maxLayerResolution);
    public static readonly string OverlayCompatibilityFixFieldName = nameof(overlayCompatibilityFix);
    //public static readonly string TestCaseFieldName = nameof(testCase);

    [HideInInspector] public string platformData;

    public void UsePlatformSettings(BuildTarget target)
    {
        var targetKey = BuildPipeline.GetBuildTargetName(target);
        var dataDict = DecodePlatformData(platformData);

        if (!dataDict.TryGetValue(targetKey, out var value))
            return;

        useComputeShaders = value.useComputeShaders;
        resultFormat = value.resultFormat;
        blurFormat = value.blurFormat;
        layerResolutionRatio = value.layerResolutionRatio;
        maxLayerResolution = value.maxLayerResolution;
    }

    public static Dictionary<string, (bool useComputeShaders, GraphicsFormat resultFormat, GraphicsFormat blurFormat, float layerResolutionRatio, int maxLayerResolution)> DecodePlatformData(string input)
    {
        var dictionary = new Dictionary<string, (bool useComputeShaders, GraphicsFormat resultFormat, GraphicsFormat blurFormat, float layerResolutionRatio, int maxLayerResolution)>();
        if (string.IsNullOrEmpty(input))
            return dictionary;

        var entries = input.Split(';');
        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry)) continue;
            var parts = entry.Split(':');
            var key = parts[0];
            var values = parts[1].Split(',');
            if (!bool.TryParse(values[0], out var useComputeShaderValue)) { useComputeShaderValue = false; }
            var resultFormatValue = (GraphicsFormat)Enum.Parse(typeof(GraphicsFormat), values[1]);
            var blurFormatValue = (GraphicsFormat)Enum.Parse(typeof(GraphicsFormat), values[2]);
            var layerResolutionRatioValue = 1f;
            var maxLayerResolutionValue = 1080;
            if (values.Length > 3)
            {
                float.TryParse(values[3], out layerResolutionRatioValue);
                int.TryParse(values[4], out maxLayerResolutionValue);
            }
            dictionary.Add(key, (useComputeShaderValue, resultFormatValue, blurFormatValue, layerResolutionRatioValue, maxLayerResolutionValue));
        }
        return dictionary;
    }

    public static string EncodePlatformData(Dictionary<string, (bool useComputeShaders, GraphicsFormat resultFormat, GraphicsFormat blurFormat, float layerResolutionRatio, int maxLayerResolution)> input)
    {
        var entries = input.Select(x => $"{x.Key}:{x.Value.useComputeShaders},{x.Value.resultFormat},{x.Value.blurFormat},{x.Value.layerResolutionRatio.ToString(CultureInfo.InvariantCulture)},{x.Value.maxLayerResolution.ToString(CultureInfo.InvariantCulture)}");
        return string.Join(";", entries);
    }
#endif

    public static readonly Dictionary<(Camera camera, int featureIdx), List<IBlur>> ImageBasedBlurDict = new();
    public static readonly Dictionary<(Camera camera, int featureIdx), int> ImageBasedLayersPerCameraDict = new();
    public static readonly Dictionary<GraphicsFormat, GraphicsFormat> ResultFormatFallbackDict = new();
    public static readonly Dictionary<GraphicsFormat, GraphicsFormat> BlurFormatFallbackDict = new();

    static FlexibleBlurFeature()
    {
        SceneManager.sceneLoaded += (_, _) => RemoveEmptyDictEntriesOnStartup();
#if UNITY_EDITOR
        EditorSceneManager.sceneOpened += (_, _) => RemoveEmptyDictEntriesOnStartup();
#endif
        void RemoveEmptyDictEntriesOnStartup()
        {
            for (int i = 0; i < ImageBasedBlurDict.Count; i++)
            {
                var key = ImageBasedBlurDict.ElementAt(i).Key;
                if (key.camera)
                    continue;

                ImageBasedBlurDict.Remove(key);
                i--;
            }

            for (int i = 0; i < ImageBasedLayersPerCameraDict.Count; i++)
            {
                var key = ImageBasedLayersPerCameraDict.ElementAt(i).Key;
                if (key.camera)
                    continue;

                ImageBasedLayersPerCameraDict.Remove(key);
                i--;
            }
        }
    }

    public static bool GloballyPaused { get; set; }

    public static readonly Dictionary<(Camera, int featureIdx), FlexibleBlurPass> GlobalFlexibleBlurPassDict = new();
    public static readonly Dictionary<(Camera camera, int featureIdx, int layer), RTHandle> NewLayerHandles = new();
    public static readonly List<Shader> RegisteredImageShaders = new();
    public static readonly Dictionary<(Shader shader, Camera camera, int featureIdx, int layer), Material> NewMaterials = new();

    [SerializeField][Tooltip("What stage of the rendering pipeline blurs are drawn in. If set to After Rendering Transparents or above, then blurred Images should reference a Camera *below* their Canvas Camera to avoid accumulation and becoming blown out. UIBlurs are generally less sensitive to blow out.")]
    private RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
    [SerializeField][Tooltip("What filtering mode destination (blur layer) textures should use. Use point filtering for a small performance gain, or for artistic effect (eg. paired with a low layer resolution for a pixelated look). Otherwise, stick with bilinear.")]
    private FilterMode destinationFilterMode = FilterMode.Bilinear;
    [SerializeField][Tooltip("When enabled, UIBlur layers stack so that higher layers blur the results of lower layers. Costs one blit per layer, after the first layer.")]
    private bool uiBlurLayersSeeLower = true;
    [SerializeField][Tooltip("When enabled, FlexibleImage will blur the results of UIBlurs. No significant performance difference, but occasionally requires an additional render texture.")]
    private bool blurredImagesSeeUIBlurs = true;
    [SerializeField][Tooltip("When enabled, FlexibleImage layers stack so that higher layers blur the results of lower layers. Costs one blit and requires an additional render texture per layer, after the first layer.")]
    private bool blurredImageLayersSeeLower = true;

    //[SerializeField]private bool testCase = false;
    //public static bool TestCase { get; private set; }

    [SerializeField] private bool useComputeShaders;
    [SerializeField] private bool overlayCompatibilityFix;
    [SerializeField] private GraphicsFormat resultFormat = GraphicsFormat.R16G16B16A16_SFloat;
    [SerializeField] private GraphicsFormat blurFormat = GraphicsFormat.R16G16B16A16_SFloat;
    [FormerlySerializedAs("LayerResolutionRatio")] [SerializeField] private float layerResolutionRatio = 1f;
    [SerializeField] private int maxLayerResolution = 1080;

    private FlexibleBlurPass pass;

    public override void Create()
    {
        pass?.Dispose();
        GlobalFlexibleBlurPassDict.Clear();
        TryPreregisterShaders();
        pass = new(FindFeatureIdx(), renderPassEvent, (UnityEngine.FilterMode)destinationFilterMode, useComputeShaders, VerifyResultFormat(resultFormat), VerifyBlurFormat(blurFormat), uiBlurLayersSeeLower, blurredImagesSeeUIBlurs, blurredImageLayersSeeLower, overlayCompatibilityFix, maxLayerResolution, layerResolutionRatio);

        int FindFeatureIdx()
        {
            var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (!urpAsset)
                return 0;

#if UNITY_2023_1_OR_NEWER
            foreach (var rendererData in urpAsset.rendererDataList)
#else
            var field = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
                return 0;

            if (field.GetValue(urpAsset) is not ScriptableRendererData[] rendererDataArray)
                return 0;

            foreach (var rendererData in rendererDataArray)
#endif
            {
                if (rendererData is not UniversalRendererData)
                    continue;

                int thisFeatureIdx = 0;
                foreach (var feature in rendererData.rendererFeatures)
                {
                    if (feature == this)
                        return thisFeatureIdx;
                    if (feature is FlexibleBlurFeature)
                        thisFeatureIdx++;
                }
            }
            return 0;
        }
    }

    public static GraphicsFormat VerifyResultFormat(GraphicsFormat format, bool silentAndDontUpdateDict = false)
    {
        if (ResultFormatFallbackDict.TryGetValue(format, out var existingValue))
            return existingValue;

#if UNITY_2023_2_OR_NEWER
        var filter = GraphicsFormatUsage.Render;
#else
        var filter = FormatUsage.Render;
#endif
        if (SystemInfo.IsFormatSupported(format, filter))
            return ResultFormatFallbackDict[format] = format;

        var fallbackFormat = SystemInfo.IsFormatSupported(GraphicsFormat.R8G8B8A8_SRGB, filter) 
            ? GraphicsFormat.R8G8B8A8_SRGB
            : GraphicsFormat.B8G8R8A8_UNorm;

        if (silentAndDontUpdateDict)
            return fallbackFormat;

        Debug.LogWarning($"Unsupported graphics format {format} for result format. Using fallback format {fallbackFormat}. This warning will display once.");
        return ResultFormatFallbackDict[format] = fallbackFormat;
    }

    public static GraphicsFormat VerifyBlurFormat(GraphicsFormat format, bool silentAndDontUpdateDict = false)
    {
        if (BlurFormatFallbackDict.TryGetValue(format, out var existingValue))  
            return existingValue;

#if UNITY_2023_2_OR_NEWER
        var filter = GraphicsFormatUsage.Render;
#else
        var filter = FormatUsage.Render;
#endif
        if (SystemInfo.IsFormatSupported(format, filter))
            return BlurFormatFallbackDict[format] = format;

        var fallbackFormat = QualitySettings.activeColorSpace == ColorSpace.Linear && SystemInfo.IsFormatSupported(GraphicsFormat.B10G11R11_UFloatPack32, filter) 
            ? GraphicsFormat.B10G11R11_UFloatPack32
            : SystemInfo.IsFormatSupported(GraphicsFormat.B10G11R11_UFloatPack32, filter)
                ? GraphicsFormat.R8G8B8A8_SRGB
                : GraphicsFormat.B8G8R8A8_UNorm;

        if (silentAndDontUpdateDict)
            return fallbackFormat;

        Debug.LogWarning($"Unsupported graphics format {format} for blur format. Using fallback format {fallbackFormat}. This warning will display once.");
        return BlurFormatFallbackDict[format] = fallbackFormat;
    }

    private void TryPreregisterShaders()
    {
        var blurredImageShader = Shader.Find("Hidden/JeffGrawAssets/BlurredImage");
        if (blurredImageShader != null && !RegisteredImageShaders.Contains(blurredImageShader))
            RegisteredImageShaders.Add(blurredImageShader);

        var flexibleImageShader = Shader.Find("Hidden/JeffGrawAssets/ProceduralBlurredImage");
        if (flexibleImageShader != null && !RegisteredImageShaders.Contains(flexibleImageShader))
            RegisteredImageShaders.Add(flexibleImageShader);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        pass.ConfigureInput(ScriptableRenderPassInput.Color);
#if !UNITY_2022_2_OR_NEWER
        pass.Setup(renderer, renderingData);
#endif
        renderer.EnqueuePass(pass);
    }

#if UNITY_2022_2_OR_NEWER && !UNITY_6000_4_OR_NEWER
#if UNITY_2023_3_OR_NEWER
    [Obsolete]
#endif
    public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData) =>
        pass.Setup(renderer, renderingData);
#endif

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;
 
        foreach (var layerKvp in NewLayerHandles)
        {
            if (GlobalFlexibleBlurPassDict.TryGetValue((layerKvp.Key.camera, layerKvp.Key.featureIdx), out var blurPass))
                blurPass.InstanceHandleSystem.Release(layerKvp.Value);
        }

        foreach (var matKvp in NewMaterials)
            CoreUtils.Destroy(matKvp.Value);

        NewLayerHandles.Clear();
        NewMaterials.Clear();
        GlobalFlexibleBlurPassDict.Clear();

        pass?.Dispose();
    }
}

public partial class FlexibleBlurPass : ScriptableRenderPass
{
    private const string ProfilerTag = nameof(FlexibleBlurPass);
    private const string ImageShaderBlurKeyword = "HAS_BLUR";
    private const string BlursShader = "Hidden/JeffGrawAssets/Blurs";
    private const string FullScreenBlitsShader = "Hidden/JeffGrawAssets/FullScreenBlits";
    private const string RegionalBlitsShader = "Hidden/JeffGrawAssets/RegionalBlits";
    private const string QuadBlitsShader = "Hidden/JeffGrawAssets/QuadBlits";

    private const int ThreadGroupSizeX = 8;
    private const int ThreadGroupSizeY = 8;
    public static event Action<float> ComputeBlurEvent;

    public readonly RTHandleSystem InstanceHandleSystem;

    private static Matrix4x4 OverlayUIProjectionMatrix = Matrix4x4.identity;
    private static int currentBlurPassIdx;
    private static int UIBlurIntermediateID = Shader.PropertyToID("FlexibleBlurUIBlurIntermediate");
    private static int Temp1Id = Shader.PropertyToID("FlexibleBlurIntermediateRT_0");
    private static int Temp2Id = Shader.PropertyToID("FlexibleBlurIntermediateRT_1");

    // fragment PropertyIDs
    private static readonly int BlurSampleDistID = Shader.PropertyToID("_BlurSampleDistance");
    private static readonly int SampleOffsetID = Shader.PropertyToID("_SampleOffset");
    private static readonly int TapsPerSideHorID = Shader.PropertyToID("_TapsPerSideHor");
    private static readonly int TapsPerSideVertID = Shader.PropertyToID("_TapsPerSideVert");
    private static readonly int BlurIterationID = Shader.PropertyToID("_BlurIteration");
    private static readonly int OffsetCenterID = Shader.PropertyToID("_OffsetCenter");
    private static readonly int SourceOffsetID = Shader.PropertyToID("_SourceOffset");
    private static readonly int SourceOffsetRightID = Shader.PropertyToID("_SourceOffsetRight");
    private static readonly int ScaleFactorID = Shader.PropertyToID("_ScaleFactor");
    private static readonly int ScaleFactorRightID = Shader.PropertyToID("_ScaleFactorRight");
    private static readonly int TintID = Shader.PropertyToID("_Tint");
    private static readonly int VibrancyID = Shader.PropertyToID("_Vibrancy");
    private static readonly int BrightnessID = Shader.PropertyToID("_Brightness");
    private static readonly int ContrastID = Shader.PropertyToID("_Contrast");
    private static readonly int DitherStrengthID = Shader.PropertyToID("_DitherStrength");
    private static readonly int DestinationRegionSizeID = Shader.PropertyToID("_DestinationRegionSize");
    private static readonly int DestinationRegionSizeRightID = Shader.PropertyToID("_DestinationRegionSizeRight");
    private static readonly int CornersID = Shader.PropertyToID("_Corners");
    private static readonly int CornersRightID = Shader.PropertyToID("_CornersRight");
    private static readonly int MainTexID = Shader.PropertyToID("_MainTex");
    private static readonly int DestinationTexID = Shader.PropertyToID("_DestTex");
    private static readonly int RenderScaleID = Shader.PropertyToID("_RenderScale");
    private static readonly int BlurRegionID = Shader.PropertyToID("_BlurRegion");
    private static readonly int BlurRegionRightID = Shader.PropertyToID("_BlurRegionRight");
    // compute PropertyIDs
    private static readonly int ComputeSourceID = Shader.PropertyToID("Source");
    private static readonly int ComputeResultID = Shader.PropertyToID("Result");
    private static readonly int ComputeResultDimensionsID = Shader.PropertyToID("ResultDimensions");
    private static readonly int ComputeSampleDistID = Shader.PropertyToID("SampleDist");
    private static readonly int TapsPerSideHorComputeID = Shader.PropertyToID("TapsPerSideHor");
    private static readonly int TapsPerSideVertComputeID = Shader.PropertyToID("TapsPerSideVert");
    private static readonly int ComputeSampleOffsetID = Shader.PropertyToID("SampleOffset");
    private static readonly int ComputeOffsetCenterID = Shader.PropertyToID("OffsetCenter");
    private static readonly int ComputeBlurIterationID = Shader.PropertyToID("BlurIteration");

    private static readonly Dictionary<(Camera camera, int featureIdx), List<RTHandle>> globalRTHandleDict = new();
    private static readonly Dictionary<Shader, Dictionary<(Camera camera, int featureIdx), List<Material>>> globalImageMatDict = new();

    private static ComputeShader computeBlurs, vrComputeBlurs;
    private static Material blursMat, fullScreenBlitsMat, regionalBlitsMat, quadBlitsMat;

    private readonly FilterMode destinationFilterMode;
    private readonly GraphicsFormat blurGraphicsFormat, resultGraphicsFormat;
    private readonly Dictionary<(Camera camera, int featureIdx), List<RTHandle>> instanceLayerRTHandleDict = new();
    private readonly Dictionary<Shader, Dictionary<(Camera camera, int featureIdx), List<Material>>> instanceImageMatDict = new();
    private readonly PooledListDictionary<(BlurSettings blurSettings, float alpha), List<IBlur>, IBlur> batchedBlurs = new ();
    private readonly float layerResolutionRatio;
    private readonly int featureIdx, maxLayerResolution;
    private readonly bool enabled, uiBlurLayersSeeLower, blurredImageSeeUIBlurs, blurredImageLayersSeeLower, useComputeShaders, overlayCompatibilityFix;

    public bool IndividuallyPaused { get; set; }

    private List<RTHandle> currentRTHandleList;
    private Camera currentCamera;
    private Vector2 layerTextureScaleFactor;
    private XRSettings.StereoRenderingMode prevStereoRenderingMode;
    private int prevFrameCount, numLayerTextures;
    private int blurComputedFrame = -1;
    private static bool blurLayerAdddedThisFrame;

    public static bool TryGetImageMaterial(Camera camera, int featureIndex, int layer, Shader shader, out Material mat)
    {
        if (!globalImageMatDict.TryGetValue(shader, out var innerGlobalImageMatDict))
        {
            innerGlobalImageMatDict = globalImageMatDict[shader] = new Dictionary<(Camera, int), List<Material>>();
            if (!FlexibleBlurFeature.RegisteredImageShaders.Contains(shader))
                FlexibleBlurFeature.RegisteredImageShaders.Add(shader);
        }

        if (innerGlobalImageMatDict.TryGetValue((camera, featureIndex), out var matList))
        {
            if (layer < matList.Count)
            {
                mat = matList[layer];
                return true;
            }
        }

        var key = (shader, camera, featureIndex, layer);
        if (FlexibleBlurFeature.NewMaterials.TryGetValue(key, out mat))
            return true;

        if (!FlexibleBlurFeature.GlobalFlexibleBlurPassDict.TryGetValue((camera, featureIndex), out _))
            return false;

        mat = CoreUtils.CreateEngineMaterial(shader);
        mat.EnableKeyword(ImageShaderBlurKeyword);
        FlexibleBlurFeature.NewMaterials[key] = mat;
        return true;
    }

    public static bool TryGetImageRT (Camera camera, int featureIndex, int layer, out RTHandle handle)
    {
        if (globalRTHandleDict.TryGetValue((camera, featureIndex), out var rtList))
        {
            if (layer < rtList.Count)
            {
                handle = rtList[layer];
                return true;
            }
        }

        var key = (camera, featureIndex, layer);
        if (FlexibleBlurFeature.NewLayerHandles.TryGetValue(key, out handle))
            return true;

        if (!FlexibleBlurFeature.GlobalFlexibleBlurPassDict.TryGetValue((camera, featureIndex), out var pass))
            return false;

        var layerScale = Mathf.Min(pass.layerResolutionRatio, (float)pass.maxLayerResolution / camera.pixelHeight);
        var layerScaleVec2 = new Vector2(layerScale, layerScale);

        (VRTextureUsage vrUsage, TextureDimension dimension, int slices) = XRSettings.stereoRenderingMode >= XRSettings.StereoRenderingMode.SinglePassInstanced ? (VRTextureUsage.TwoEyes, TextureDimension.Tex2DArray, 2) : (VRTextureUsage.None, TextureDimension.Tex2D, 1);
#if UNITY_2022_2_OR_NEWER
        handle = pass.InstanceHandleSystem.Alloc(layerScaleVec2, colorFormat: pass.resultGraphicsFormat, memoryless: RenderTextureMemoryless.Depth, filterMode: pass.destinationFilterMode, useMipMap: false, msaaSamples: MSAASamples.None, vrUsage: vrUsage, dimension: dimension, slices: slices);
#else
        handle = pass.InstanceHandleSystem.Alloc(layerScaleVec2, colorFormat: pass.resultGraphicsFormat, memoryless: RenderTextureMemoryless.Depth, filterMode: pass.destinationFilterMode, useMipMap: false, msaaSamples: MSAASamples.None, dimension: dimension, slices: slices);
#endif
        FlexibleBlurFeature.NewLayerHandles[key] = handle;
        blurLayerAdddedThisFrame = true;
        return true;
    }

    public FlexibleBlurPass(int featureIdx, RenderPassEvent renderPassEvent, FilterMode destinationFilterMode, bool useComputeShaders, GraphicsFormat resultGraphicsFormat, GraphicsFormat blurGraphicsFormat, bool uiBlurLayersSeeLower, bool blurredImageSeeUIBlurs, bool blurredImageLayersSeeLower, bool overlayCompatibilityFix, int maxLayerResolution, float layerResolutionRatio)
    {
        InstanceHandleSystem = new();

        (this.featureIdx, this.renderPassEvent, this.destinationFilterMode, this.useComputeShaders, this.resultGraphicsFormat, this.blurGraphicsFormat, this.uiBlurLayersSeeLower, this.blurredImageSeeUIBlurs, this.blurredImageLayersSeeLower, this.overlayCompatibilityFix, this.maxLayerResolution, this.layerResolutionRatio) = 
        (featureIdx,           renderPassEvent,      destinationFilterMode,      useComputeShaders,      resultGraphicsFormat,      blurGraphicsFormat,      uiBlurLayersSeeLower,      blurredImageSeeUIBlurs,      blurredImageLayersSeeLower,      overlayCompatibilityFix,      maxLayerResolution,      layerResolutionRatio);

        if (useComputeShaders)
        {
            if (computeBlurs == null)
            {
                computeBlurs = (ComputeShader)Resources.Load("ComputeBlurs");
                vrComputeBlurs = (ComputeShader)Resources.Load("VRComputeBlurs");
            }
        }
        else if (blursMat == null)
        {
            blursMat = CoreUtils.CreateEngineMaterial(Shader.Find(BlursShader));
        }

        if (fullScreenBlitsMat == null)
        {
            fullScreenBlitsMat = CoreUtils.CreateEngineMaterial(Shader.Find(FullScreenBlitsShader));
            regionalBlitsMat = CoreUtils.CreateEngineMaterial(Shader.Find(RegionalBlitsShader));
            quadBlitsMat = CoreUtils.CreateEngineMaterial(Shader.Find(QuadBlitsShader));
        }

        if (XRSettings.enabled)
            InstanceHandleSystem.Initialize(XRSettings.eyeTextureWidth, XRSettings.eyeTextureHeight);
        else
            InstanceHandleSystem.Initialize(Screen.width, Screen.height);

        prevStereoRenderingMode = XRSettings.stereoRenderingMode;
    }

    public void Dispose()
    {
        foreach (var shader in FlexibleBlurFeature.RegisteredImageShaders)
        {
            if (!instanceImageMatDict.TryGetValue(shader, out var innerInstanceImageMatDict))
                continue;

            foreach (var kvp in innerInstanceImageMatDict)
            {
                var (camera, instanceMatList) = (kvp.Key, kvp.Value);
                if (globalImageMatDict.TryGetValue(shader, out var innerGlobalImageMatDict) && innerGlobalImageMatDict.TryGetValue(camera, out var globalMatList) && globalMatList == instanceMatList)
                    innerGlobalImageMatDict.Remove(camera);

                instanceMatList.ForEach(CoreUtils.Destroy);
            }
        }

        foreach (var kvp in instanceLayerRTHandleDict)
        {
            var (camera, instanceRtList) = (kvp.Key, kvp.Value);
            if (globalRTHandleDict.TryGetValue(camera, out var globalRtList) && globalRtList == instanceRtList)
                globalRTHandleDict.Remove(camera);
        }

        InstanceHandleSystem.Dispose();
    }

    private void SharedSetup(int cameraPixelWidth, int cameraPixelHeight, float renderScale)
    {
        renderScale = Mathf.Min(renderScale, 1f);
        var layerScale = Mathf.Min(layerResolutionRatio, (float)maxLayerResolution / cameraPixelHeight);
        layerTextureScaleFactor = new Vector2(layerScale, layerScale);

        var frameCount = Time.renderedFrameCount;
        if (blurComputedFrame == frameCount)
            return;

        blurComputedFrame = frameCount;

        var filteringPadding = 1.5f / (renderScale * renderScale) / layerScale;
        ComputeBlurEvent?.Invoke(filteringPadding);

        if (overlayCompatibilityFix)
            return;

        OverlayUIProjectionMatrix = Matrix4x4.Ortho(0, cameraPixelWidth, 0, cameraPixelHeight, -1000f, 1000f);
    }

    public void Setup(ScriptableRenderer _, in RenderingData renderingData)
    {
        var camData = renderingData.cameraData;
        SharedSetup(camData.camera.pixelWidth, camData.camera.pixelHeight, camData.renderScale);
        Setup(camData.camera, camData.cameraTargetDescriptor);
    }

    private void Setup(Camera camera, RenderTextureDescriptor descriptor)
    {
        currentCamera = camera;
        var key = (currentCamera, featureIdx);

        int texturesNeededForUiBlurs = 0;
        UIBlur.LayersPerCameraDict.TryGetValue(key, out var uiBlurLayers);
        if (uiBlurLayers > 0)
            texturesNeededForUiBlurs++;

        var stereoRenderingMode = XRSettings.stereoRenderingMode;
        if (prevStereoRenderingMode != stereoRenderingMode)
        {
            prevStereoRenderingMode = stereoRenderingMode;
            foreach (var kvp in instanceLayerRTHandleDict)
            {
                var (instanceCamera, instanceRtList) = (kvp.Key, kvp.Value);
                instanceRtList.ForEach(InstanceHandleSystem.Release);

                if (globalRTHandleDict.TryGetValue(instanceCamera, out var globalRtList) && globalRtList == instanceRtList)
                    globalRTHandleDict.Remove(instanceCamera);
            }
        }

        (VRTextureUsage vrUsage, TextureDimension dimension, int slices) = stereoRenderingMode >= XRSettings.StereoRenderingMode.SinglePassInstanced ? (VRTextureUsage.TwoEyes, TextureDimension.Tex2DArray, 2) : (VRTextureUsage.None, TextureDimension.Tex2D, 1);

        if (FlexibleBlurFeature.ImageBasedBlurDict.TryGetValue(key, out var blurredImageAreas) && blurredImageAreas is { Count: > 0 })
        {
            FlexibleBlurFeature.ImageBasedLayersPerCameraDict.TryGetValue(key, out var texturesNeededForImageBlurs);

            numLayerTextures = Math.Max(texturesNeededForUiBlurs, texturesNeededForImageBlurs);

            if (!instanceLayerRTHandleDict.TryGetValue(key, out currentRTHandleList))
                currentRTHandleList = instanceLayerRTHandleDict[key] = new List<RTHandle>();

            while (currentRTHandleList.Count < numLayerTextures)
            {
                blurLayerAdddedThisFrame = true;
                var newHandleKey = (camera, featureIdx, currentRTHandleList.Count);
                if (FlexibleBlurFeature.NewLayerHandles.TryGetValue(newHandleKey, out var newHandle))
                {
                    currentRTHandleList.Add(newHandle);
                    FlexibleBlurFeature.NewLayerHandles.Remove(newHandleKey);
                }
                else
                {
#if UNITY_2022_2_OR_NEWER
                    currentRTHandleList.Add(InstanceHandleSystem.Alloc(layerTextureScaleFactor, wrapMode: TextureWrapMode.Clamp, colorFormat: resultGraphicsFormat, memoryless: RenderTextureMemoryless.Depth, filterMode: destinationFilterMode, useMipMap: false, msaaSamples: MSAASamples.None, vrUsage: vrUsage, dimension: dimension, slices: slices));
#else
                    currentRTHandleList.Add(InstanceHandleSystem.Alloc(layerTextureScaleFactor, wrapMode: TextureWrapMode.Clamp, colorFormat: resultGraphicsFormat, memoryless: RenderTextureMemoryless.Depth, filterMode: destinationFilterMode, useMipMap: false, msaaSamples: MSAASamples.None, dimension: dimension, slices: slices));
#endif
                }
            }

            globalRTHandleDict[key] = currentRTHandleList;

            foreach (var shader in FlexibleBlurFeature.RegisteredImageShaders)
            {
                if (!instanceImageMatDict.TryGetValue(shader, out var innerImageMatDict))
                    innerImageMatDict = instanceImageMatDict[shader] = new Dictionary<(Camera, int), List<Material>>();

                if (!innerImageMatDict.TryGetValue(key, out var matList))
                    matList = innerImageMatDict[key] = new List<Material>();

                while (matList.Count < texturesNeededForImageBlurs)
                {
                    var newMaterialKey = (shader, camera, featureIdx, matList.Count);
                    if (FlexibleBlurFeature.NewMaterials.TryGetValue(newMaterialKey, out var newMaterial))
                    {
                        matList.Add(newMaterial);
                        FlexibleBlurFeature.NewMaterials.Remove(newMaterialKey);
                    }
                    else
                    {
                        var newMat = CoreUtils.CreateEngineMaterial(shader);
                        newMat.EnableKeyword(ImageShaderBlurKeyword);
                        matList.Add(newMat);
                    }
                }

                if (!globalImageMatDict.TryGetValue(shader, out var innerGlobalImageMatDict))
                    innerGlobalImageMatDict = globalImageMatDict[shader] = new Dictionary<(Camera, int), List<Material>>();

                innerGlobalImageMatDict[key] = matList;
            }
        }
        else if (texturesNeededForUiBlurs == 1)
        {
            numLayerTextures = 1;
            currentRTHandleList ??= new List<RTHandle>(1);
            if (currentRTHandleList.Count == 0)
            {
#if UNITY_2022_2_OR_NEWER
                currentRTHandleList.Add(InstanceHandleSystem.Alloc(layerTextureScaleFactor, wrapMode: TextureWrapMode.Clamp, colorFormat: resultGraphicsFormat, memoryless: RenderTextureMemoryless.Depth, filterMode: destinationFilterMode, useMipMap: false, msaaSamples: MSAASamples.None, vrUsage: vrUsage, dimension: dimension, slices: slices));
#else
                currentRTHandleList.Add(InstanceHandleSystem.Alloc(layerTextureScaleFactor, wrapMode: TextureWrapMode.Clamp, colorFormat: resultGraphicsFormat, memoryless: RenderTextureMemoryless.Depth, filterMode: destinationFilterMode, useMipMap: false, msaaSamples: MSAASamples.None, dimension: dimension, slices: slices));
#endif
            }
        }

        var cameraDescriptor = descriptor;
        if (InstanceHandleSystem.GetMaxWidth() != cameraDescriptor.width || InstanceHandleSystem.GetMaxHeight() != cameraDescriptor.height)
            InstanceHandleSystem.ResetReferenceSize(cameraDescriptor.width, cameraDescriptor.height);

        FlexibleBlurFeature.GlobalFlexibleBlurPassDict[(camera, featureIdx)] = this;
    }

    public override void FrameCleanup(CommandBuffer cmd)
    {
        for (int i = 0; i < FlexibleBlurFeature.NewLayerHandles.Count; i++)
        {
            var element = FlexibleBlurFeature.NewLayerHandles.ElementAt(i);
            if (element.Key.camera != currentCamera)
                continue;

            InstanceHandleSystem.Release(element.Value);
            FlexibleBlurFeature.NewLayerHandles.Remove(element.Key);
            i--;
        }

        for (int i = 0; i < FlexibleBlurFeature.NewMaterials.Count; i++)
        {
            var element = FlexibleBlurFeature.NewMaterials.ElementAt(i);
            if (element.Key.camera != currentCamera)
                continue;

            CoreUtils.Destroy(element.Value);
            FlexibleBlurFeature.NewMaterials.Remove(element.Key);
            i--;
        }

        // At the end of every frame, check for cameras that have been destroyed and free any resources they may have used.
        var frameCount = Time.frameCount;
        if (frameCount != prevFrameCount)
        {
            // Somewhat roundabout approach to removing null keys, but has the benefit of 0 allocations.
            int idx = 0;
            while (idx < instanceLayerRTHandleDict.Count)
            {
                foreach (var key in instanceLayerRTHandleDict.Keys)
                {
                    if (key.camera)
                    {
                        idx++;
                        continue;
                    }

                    foreach (var rtHandle in instanceLayerRTHandleDict[key])
                        InstanceHandleSystem.Release(rtHandle);

                    instanceLayerRTHandleDict.Remove(key);
                    globalRTHandleDict.Remove(key);
                    idx = 0;
                    break;
                }
            }

            foreach (var shader in FlexibleBlurFeature.RegisteredImageShaders)
            {
                if (!instanceImageMatDict.TryGetValue(shader, out var innerImageMatDict) || !globalImageMatDict.TryGetValue(shader, out var innerGlobalImageMatDict))
                    continue;

                idx = 0;
                while (idx < innerImageMatDict.Count)
                {
                    foreach (var key in innerImageMatDict.Keys)
                    {
                        if (key.camera)
                        {
                            idx++;
                            continue;
                        }
            
                        foreach (var material in innerImageMatDict[key])
                            CoreUtils.Destroy(material);
            
                        innerImageMatDict.Remove(key);
                        innerGlobalImageMatDict.Remove(key);
                        idx = 0;
                        break;
                    }
                }
            }
        }

        prevFrameCount = frameCount;

        while (currentRTHandleList?.Count > numLayerTextures)
        {
            var handle = currentRTHandleList[^1];
            currentRTHandleList.RemoveAt(currentRTHandleList.Count - 1);
            InstanceHandleSystem.Release(handle);
        }
    }
#if !UNITY_6000_4_OR_NEWER
#if UNITY_2023_3_OR_NEWER
    [Obsolete]
#endif
    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        if (!blurLayerAdddedThisFrame && (IndividuallyPaused || FlexibleBlurFeature.GloballyPaused))
            return;

        blurLayerAdddedThisFrame = false;
        var camera = renderingData.cameraData.camera;
        var key = (camera, featureIdx);

        UIBlur.BlurDict.TryGetValue(key, out var blurAreas);
        FlexibleBlurFeature.ImageBasedBlurDict.TryGetValue(key, out var blurredImageAreas);

        var haveUIBlurAreas = blurAreas is { Count: > 0 };
        var haveBlurredImageAreas = blurredImageAreas is { Count: > 0 };
        if (!haveUIBlurAreas && !haveBlurredImageAreas)
            return;

        var rightEye = XRSettings.enabled && renderingData.cameraData.xr.multipassId == 1;
        var singlePassVR = XRSettings.enabled && XRSettings.stereoRenderingMode >= XRSettings.StereoRenderingMode.SinglePassInstanced;
        var multiPassVR = XRSettings.enabled && XRSettings.stereoRenderingMode == XRSettings.StereoRenderingMode.MultiPass;

        var renderScale = renderingData.cameraData.renderScale;
        var cameraTargetDescriptor = renderingData.cameraData.cameraTargetDescriptor;
        var originalHeight = cameraTargetDescriptor.height;
        var originalWidth = cameraTargetDescriptor.width;
#if UNITY_2022_2_OR_NEWER
        var cameraRT = renderingData.cameraData.renderer.cameraColorTargetHandle;
#else
        var cameraRT = renderingData.cameraData.renderer.cameraColorTarget;
#endif
        cameraTargetDescriptor.graphicsFormat = blurGraphicsFormat;
        cameraTargetDescriptor.enableRandomWrite = useComputeShaders;
        cameraTargetDescriptor.useMipMap = false;
        cameraTargetDescriptor.msaaSamples = 1;
        cameraTargetDescriptor.depthBufferBits = 0;
        cameraTargetDescriptor.depthStencilFormat = GraphicsFormat.None;
        var destinationRequiresClear = haveUIBlurAreas && (!haveBlurredImageAreas || blurredImageSeeUIBlurs);

        var cmd = CommandBufferPool.Get(ProfilerTag);
        if (blurredImageSeeUIBlurs)
        {
            HandleUIBlurs();
            HandleBlurredImages();
        }
        else
        {
            HandleBlurredImages();
            HandleUIBlurs(haveBlurredImageAreas);
        }

        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);

        void HandleUIBlurs(bool useTempRT = false)
        {
            if (!haveUIBlurAreas)
                return;

            RenderTargetIdentifier layerRT;
            if (useTempRT)
            {
                destinationRequiresClear = !blurredImageSeeUIBlurs;
                cameraTargetDescriptor.height = originalHeight;
                cameraTargetDescriptor.width = originalWidth;
                cmd.GetTemporaryRT(UIBlurIntermediateID, cameraTargetDescriptor, FilterMode.Bilinear);
                layerRT = UIBlurIntermediateID;
            }
            else
            {
                layerRT = currentRTHandleList[0];
            }

            if (uiBlurLayersSeeLower)
            {
                int currentLayer = blurAreas[0].Layer;
                foreach (var blur in blurAreas)
                {
                    if (blur.Layer > currentLayer)
                    {
                        currentLayer = blur.Layer;
                        FullScreenBlit(cmd, layerRT, cameraRT, fullScreenBlitsMat, 1);
                    }

                    ApplyBlur(blur, cameraRT, layerRT);
                }
            }
            else
            {
                foreach (var blurArea in blurAreas)
                    ApplyBlur(blurArea, cameraRT, layerRT);
            }

            if (!destinationRequiresClear)
            {
                FullScreenBlit(cmd, layerRT, cameraRT, fullScreenBlitsMat, 1);
            }

            if (useTempRT)
                cmd.ReleaseTemporaryRT(UIBlurIntermediateID);
        }

        void HandleBlurredImages()
        {
            if (!haveBlurredImageAreas)
                return;

            int numImageLayers = FlexibleBlurFeature.ImageBasedLayersPerCameraDict[key];
            var source = cameraRT;
            int destinationIdx = 0;
            var destination = currentRTHandleList[destinationIdx];
            if (blurredImageLayersSeeLower && destinationIdx < numImageLayers - 1)
            {
                cmd.SetRenderTarget(new RenderTargetIdentifier(destination, 0, CubemapFace.Unknown, -1));
                cmd.ClearRenderTarget(false, true, Color.clear);
                cmd.SetRenderTarget(source);
                FullScreenBlit(cmd, source, destination, fullScreenBlitsMat);
            }

            int currentLayer = blurredImageAreas[0].Layer;
            int currentPriority = blurredImageAreas[0].Priority;
            foreach (var blurImage in blurredImageAreas)
            {
                if (blurImage.Layer > currentLayer || blurImage.Priority > currentPriority)
                {
                    TryApplyBatchedBlurs();

                    if (blurImage.Layer > currentLayer)
                    {
                        destination = currentRTHandleList[++destinationIdx];
                        if (blurredImageLayersSeeLower)
                        {
                            source = currentRTHandleList[destinationIdx - 1];
                            cmd.SetRenderTarget(source);

                            if (destinationIdx < numImageLayers - 1)
                            {
                                FullScreenBlit(cmd, source, destination, fullScreenBlitsMat);
                            }
                        }
                    }
                    currentLayer = blurImage.Layer;
                    currentPriority = blurImage.Priority;
                }

                if (blurImage.CanBatch)
                    batchedBlurs.Add((blurImage.Settings, blurImage.Alpha), blurImage);
                else
                    ApplyBlur(blurImage, source, destination);
            }

            TryApplyBatchedBlurs();

            if (blurredImageLayersSeeLower)
                cmd.SetRenderTarget(cameraRT);

            void TryApplyBatchedBlurs()
            {
                foreach (var kvp in batchedBlurs)
                {
                    bool fillEntireRenderTexture = false;
                    foreach (var blur in kvp.Value)
                    {
                        if (!blur.FillEntireRenderTexture)
                            continue;

                        fillEntireRenderTexture = true;
                        break;
                    }

                    if (!fillEntireRenderTexture && kvp.Value.Count == 1)
                    {
                        ApplyBlur(kvp.Value[0], source, destination);
                        continue;
                    }

                    float minX, minY, maxX, maxY;
                    if (fillEntireRenderTexture)
                    {
                        (minX, minY, maxX, maxY) = (0, 0, originalWidth, originalHeight);
                    }
                    else
                    {
                        (minX, minY, maxX, maxY) = (float.PositiveInfinity, float.PositiveInfinity, float.NegativeInfinity, float.NegativeInfinity);
                        foreach (var batchedBlur in kvp.Value)
                        {
                            minX = Math.Min(minX, batchedBlur.MinX(rightEye));
                            minY = Math.Min(minY, batchedBlur.MinY(rightEye));
                            maxX = Math.Max(maxX, batchedBlur.MaxX(rightEye));
                            maxY = Math.Max(maxY, batchedBlur.MaxY(rightEye));
                        }
                    }

                    if (destinationRequiresClear)
                    {
                        cmd.SetRenderTarget(new RenderTargetIdentifier(destination, 0, CubemapFace.Unknown, -1));
                        cmd.ClearRenderTarget(false, true, Color.clear);
                        cmd.SetRenderTarget(source);
                        destinationRequiresClear = false;
                    }

                    var settings = kvp.Key.blurSettings;
                    if (singlePassVR)
                    {
                        // Left eye extents already calculated. Min/Max that with the right eye values to get a region that covers both eyes.
                        foreach (var batchedBlur in kvp.Value)
                        {
                            minX = Math.Min(minX, batchedBlur.MinX(true));
                            minY = Math.Min(minY, batchedBlur.MinY(true));
                            maxX = Math.Max(maxX, batchedBlur.MaxX(true));
                            maxY = Math.Max(maxY, batchedBlur.MaxY(true));
                        }
                    }

                    var blurRegion = UIBlurCommon.ComputeBlurRegion(minX, minY, maxX, maxY);
                    ApplyBlurUnified(source, destination, settings, blurRegion, singlePassVR ? blurRegion : null, null, null, kvp.Key.alpha, false, Matrix4x4.identity);
                }

                batchedBlurs.Clear();
            }
        }

        void ApplyBlur(IBlur iBlur, RenderTargetIdentifier source, RenderTargetIdentifier destination)
        {
            if (!iBlur.HasVisiblePixels(rightEye))
                return;

            if (destinationRequiresClear)
            {
                cmd.SetRenderTarget(new RenderTargetIdentifier(destination, 0, CubemapFace.Unknown, -1));
                cmd.ClearRenderTarget(false, true, Color.clear);
                cmd.SetRenderTarget(source);
                destinationRequiresClear = false;
            }

            if (singlePassVR)
            {
                var (regionRight, cornersRight, region, corners) = (iBlur.Common.BlurRegionRight, iBlur.Common.ScreenCornersRight, iBlur.Common.BlurRegion, iBlur.Common.ScreenCorners);
                ApplyBlurUnified(source, destination, iBlur.Settings, region, regionRight, corners, cornersRight, iBlur.Alpha, iBlur.IsAngled, iBlur.Matrix, iBlur.Common.WorldCamera);
            }
            else
            {
                var (region, corners) = rightEye ? (iBlur.Common.BlurRegionRight, iBlur.Common.ScreenCornersRight) : (iBlur.Common.BlurRegion, iBlur.Common.ScreenCorners);
                ApplyBlurUnified(source, destination, iBlur.Settings, region, null, corners, null, iBlur.Alpha, iBlur.IsAngled, iBlur.Matrix, iBlur.Common.WorldCamera);
            }
        }

        void ApplyBlurUnified(RenderTargetIdentifier source, RenderTargetIdentifier destination, BlurSettings settings, Vector4 blurRegion, Vector4? blurRegionRight, Vector4[] blurCorners, Vector4[] blurCornersRight, float alpha, bool isAngled, Matrix4x4 transformationMatrix, Camera uiCamera = null)
        {
            blurRegion *= renderScale;
            var hasRightEye = blurRegionRight.HasValue;
            if (hasRightEye)
                blurRegionRight *= renderScale;

            var (blurRegionWidth, blurRegionHeight) = hasRightEye ? (Mathf.Max(blurRegion.z, blurRegionRight.Value.z), Mathf.Max(blurRegion.w, Mathf.Max(blurRegionRight.Value.w))) : (blurRegion.z, blurRegion.w);

            var aspect = blurRegionWidth / blurRegionHeight;
            var scale = renderScale * (settings.referenceResolution > 0 ? (float)settings.referenceResolution / originalHeight : 1f);

            cameraTargetDescriptor.height = Mathf.Max(1, Mathf.RoundToInt(scale * blurRegionHeight));
            cameraTargetDescriptor.width = Mathf.Max(1, Mathf.RoundToInt(cameraTargetDescriptor.height * aspect));

            var referenceHeightForDownScale = cameraTargetDescriptor.height;

            cmd.GetTemporaryRT(Temp1Id, cameraTargetDescriptor, FilterMode.Bilinear);
            var scaleFactor = new Vector2(originalWidth / blurRegion.z, originalHeight / blurRegion.w);
            var offset = scaleFactor * new Vector2(blurRegion.x / originalWidth, blurRegion.y / originalHeight);
            cmd.SetGlobalVector(ScaleFactorID, scaleFactor);
            cmd.SetGlobalVector(SourceOffsetID, offset);

            if (hasRightEye)
            {
                var scaleFactorRight = new Vector2(originalWidth / blurRegionRight.Value.z, originalHeight / blurRegionRight.Value.w);
                cmd.SetGlobalVector(ScaleFactorRightID, scaleFactorRight);
                var offsetRight = scaleFactorRight * new Vector2(blurRegionRight.Value.x / originalWidth, blurRegionRight.Value.y / originalHeight);
                cmd.SetGlobalVector(SourceOffsetRightID, offsetRight);
            }

            FullScreenBlit(cmd, source, Temp1Id, regionalBlitsMat, Convert.ToInt32(settings.hqResample));

            if (alpha > 0)
            {
                if (useComputeShaders)
                    ComputeBlur();
                else
                    TraditionalBlur();
            }
            FinalBlitToDestination();

            void ComputeBlur()
            {
                int totalIterations = 0;
                var threadGroupsX = (cameraTargetDescriptor.width + ThreadGroupSizeX - 1) / ThreadGroupSizeX;
                var threadGroupsY = (cameraTargetDescriptor.height + ThreadGroupSizeY - 1) / ThreadGroupSizeY;
                var computeShader = hasRightEye ? vrComputeBlurs : computeBlurs;
                var temp2NeedsInit = true;

                cmd.SetComputeIntParam(computeShader, ComputeOffsetCenterID, 0);
                foreach (var section in settings.downscaleSections)
                {
                    var (isSeparable, setSamplesPerSide, _, firstKernelIdx, secondKernelIdx) = section.GetSectionBehaviour();

                    if (setSamplesPerSide)
                    {
                        cmd.SetComputeIntParam(computeShader, TapsPerSideHorComputeID, section.horizontalSamplesPerSide);
                        cmd.SetComputeIntParam(computeShader, TapsPerSideVertComputeID, section.verticalSamplesPerSide);
                    }

                    var iterations = section.iterations;
                    var baseSampleDistance = section.sampleDistance;
                    var sampleOffset = 1f;

                    for (int i = 0; i < iterations; i++, totalIterations++)
                    {
                        cmd.SetComputeIntParam(computeShader, ComputeBlurIterationID, i);
                        sampleOffset *= 0.5f;
                        var evenIter = i % 2 == 0;
                        var lastPass = i == iterations - 1;
                        cmd.SetComputeFloatParam(computeShader, ComputeSampleOffsetID, !evenIter ? -sampleOffset : lastPass ? 0 : sampleOffset);

                        cameraTargetDescriptor.height = Mathf.Max(1, Mathf.RoundToInt(referenceHeightForDownScale / Mathf.Pow(1 + alpha, totalIterations + 1)));
                        cameraTargetDescriptor.width = Mathf.Max(1, Mathf.RoundToInt(cameraTargetDescriptor.height * aspect));

                        cmd.ReleaseTemporaryRT(Temp2Id);
                        cmd.GetTemporaryRT(Temp2Id, cameraTargetDescriptor, FilterMode.Bilinear);

                        cmd.SetComputeVectorParam(computeShader, ComputeResultDimensionsID, new Vector2(cameraTargetDescriptor.width, cameraTargetDescriptor.height));
                        cmd.SetComputeFloatParam(computeShader, ComputeSampleDistID, baseSampleDistance * renderScale);

                        cmd.SetComputeTextureParam(computeShader, firstKernelIdx, ComputeSourceID, Temp1Id);
                        cmd.SetComputeTextureParam(computeShader, firstKernelIdx, ComputeResultID, Temp2Id);

                        threadGroupsX = (cameraTargetDescriptor.width + ThreadGroupSizeX - 1) / ThreadGroupSizeX;
                        threadGroupsY = (cameraTargetDescriptor.height + ThreadGroupSizeY - 1) / ThreadGroupSizeY;
                        cmd.DispatchCompute(computeShader, firstKernelIdx, threadGroupsX, threadGroupsY, 1);
                        cmd.ReleaseTemporaryRT(Temp1Id);

                        if (!isSeparable)
                        {
                            temp2NeedsInit = true;
                            (Temp1Id, Temp2Id) = (Temp2Id, Temp1Id);
                            continue;
                        }

                        temp2NeedsInit = false;
                        cmd.GetTemporaryRT(Temp1Id, cameraTargetDescriptor, FilterMode.Bilinear);
                        cmd.SetComputeTextureParam(computeShader, secondKernelIdx, ComputeSourceID, Temp2Id);
                        cmd.SetComputeTextureParam(computeShader, secondKernelIdx, ComputeResultID, Temp1Id);
                        cmd.DispatchCompute(computeShader, secondKernelIdx, threadGroupsX, threadGroupsY, 1);
                    }
                }

                if (temp2NeedsInit)
                {
                    cmd.ReleaseTemporaryRT(Temp2Id);
                    cmd.GetTemporaryRT(Temp2Id, cameraTargetDescriptor, FilterMode.Bilinear);
                    cmd.SetComputeVectorParam(computeShader, ComputeResultDimensionsID, new Vector2(cameraTargetDescriptor.width, cameraTargetDescriptor.height));
                }

                totalIterations = 0;
                cmd.SetComputeIntParam(computeShader, ComputeOffsetCenterID, 1);
                foreach (var section in settings.blurSections)
                {
                    var (isSeparable, setSamplesPerSide, skip, firstKernelIdx, secondKernelIdx) = section.GetSectionBehaviour();
                    if (skip)
                        continue;

                    if (setSamplesPerSide)
                    {
                        cmd.SetComputeIntParam(computeShader, TapsPerSideHorComputeID, section.horizontalSamplesPerSide);
                        cmd.SetComputeIntParam(computeShader, TapsPerSideVertComputeID, section.verticalSamplesPerSide);
                    }

                    var iterations = section.iterations;
                    var baseSampleDistance = section.sampleDistance;

                    if (baseSampleDistance + settings.blurAdditionalDistancePerIteration <= 0)
                        continue;

                    if (!isSeparable)
                    {
                        for (int i = 0; i < iterations; i++, totalIterations++)
                        {
                            var evenIter = i % 2 == 0;
                            if (i == iterations - 1 && evenIter)
                                cmd.SetComputeFloatParam(computeShader, ComputeSampleOffsetID, 0);
                            else
                                cmd.SetComputeFloatParam(computeShader, ComputeSampleOffsetID, evenIter ? 0.5f : -0.5f);

                            cmd.SetComputeIntParam(computeShader, ComputeBlurIterationID, i);
                            var sampleDistance = alpha * (baseSampleDistance + settings.blurAdditionalDistancePerIteration * totalIterations);
                            cmd.SetComputeFloatParam(computeShader, ComputeSampleDistID, sampleDistance * renderScale);
                            cmd.SetComputeTextureParam(computeShader, firstKernelIdx, ComputeSourceID, Temp1Id);
                            cmd.SetComputeTextureParam(computeShader, firstKernelIdx, ComputeResultID, Temp2Id);
                            cmd.DispatchCompute(computeShader, firstKernelIdx, threadGroupsX, threadGroupsY, 1);
                            (Temp1Id, Temp2Id) = (Temp2Id, Temp1Id);
                        }
                    }
                    else
                    {
                        cmd.SetComputeTextureParam(computeShader, firstKernelIdx, ComputeSourceID, Temp1Id);
                        cmd.SetComputeTextureParam(computeShader, firstKernelIdx, ComputeResultID, Temp2Id);
                        cmd.SetComputeTextureParam(computeShader, secondKernelIdx, ComputeSourceID, Temp2Id);
                        cmd.SetComputeTextureParam(computeShader, secondKernelIdx, ComputeResultID, Temp1Id);
                        for (int i = 0; i < iterations; i++, totalIterations++)
                        {
                            var evenIter = i % 2 == 0;
                            if (i == iterations - 1 && evenIter)
                                cmd.SetComputeFloatParam(computeShader, ComputeSampleOffsetID, 0);
                            else
                                cmd.SetComputeFloatParam(computeShader, ComputeSampleOffsetID, evenIter ? 0.5f : -0.5f);

                            var sampleDistance = alpha * (baseSampleDistance + settings.blurAdditionalDistancePerIteration * totalIterations);
                            cmd.SetComputeFloatParam(computeShader, ComputeSampleDistID, sampleDistance * renderScale);
                            cmd.DispatchCompute(computeShader, firstKernelIdx, threadGroupsX, threadGroupsY, 1);
                            cmd.DispatchCompute(computeShader, secondKernelIdx, threadGroupsX, threadGroupsY, 1);
                        }
                    }
                }
            }

            void TraditionalBlur()
            {
                int totalIterations = 0;
                var temp2NeedsInit = true;
                cmd.SetGlobalInt(OffsetCenterID, 0);
                foreach (var section in settings.downscaleSections)
                { 
                    var (isSeparable, setSamplesPerSide, _, firstKernelIdx, secondKernelIdx) = section.GetSectionBehaviour();

                    if (setSamplesPerSide)
                    {
                        cmd.SetGlobalInt(TapsPerSideHorID, section.horizontalSamplesPerSide);
                        cmd.SetGlobalInt(TapsPerSideVertID, section.verticalSamplesPerSide);
                    }

                    var iterations = section.iterations;
                    var baseSampleDistance = section.sampleDistance;
                    var sampleOffset = 1f;

                    for (int i = 0; i < iterations; i++, totalIterations++)
                    {
                        cmd.SetGlobalInt(BlurIterationID, i);
                        sampleOffset *= 0.5f;
                        var evenIter = i % 2 == 0;
                        if (i == iterations - 1 && evenIter)
                            cmd.SetGlobalFloat(SampleOffsetID, 0);
                        else
                            cmd.SetGlobalFloat(SampleOffsetID, evenIter ? sampleOffset : -sampleOffset);

                        cameraTargetDescriptor.height = Mathf.Max(1, Mathf.RoundToInt(referenceHeightForDownScale / Mathf.Pow(1 + alpha, 1 + totalIterations)));
                        cameraTargetDescriptor.width = Mathf.Max(1, Mathf.RoundToInt(cameraTargetDescriptor.height * aspect));
                        cmd.ReleaseTemporaryRT(Temp2Id);
                        cmd.GetTemporaryRT(Temp2Id, cameraTargetDescriptor, FilterMode.Bilinear);

                        cmd.SetGlobalFloat(BlurSampleDistID, baseSampleDistance * renderScale);
                        FullScreenBlit(cmd, Temp1Id, Temp2Id, blursMat, firstKernelIdx);
                        cmd.ReleaseTemporaryRT(Temp1Id);

                        if (!isSeparable)
                        {
                            temp2NeedsInit = true;
                            (Temp1Id, Temp2Id) = (Temp2Id, Temp1Id);
                            continue;
                        }

                        temp2NeedsInit = false;
                        cmd.GetTemporaryRT(Temp1Id, cameraTargetDescriptor, FilterMode.Bilinear);
                        FullScreenBlit(cmd, Temp2Id, Temp1Id, blursMat, secondKernelIdx);
                    }
                }

                if (temp2NeedsInit)
                {
                    cmd.ReleaseTemporaryRT(Temp2Id);
                    cmd.GetTemporaryRT(Temp2Id, cameraTargetDescriptor, FilterMode.Bilinear);
                }

                totalIterations = 0;
                cmd.SetGlobalInt(OffsetCenterID, 1);
                foreach (var section in settings.blurSections)
                {
                    var (isSeparable, setSamplesPerSide, skip, firstKernelIdx, secondKernelIdx) = section.GetSectionBehaviour();
                    if (skip)
                        continue;

                    if (setSamplesPerSide)
                    {
                        cmd.SetGlobalInt(TapsPerSideHorID, section.horizontalSamplesPerSide);
                        cmd.SetGlobalInt(TapsPerSideVertID, section.verticalSamplesPerSide);
                    }

                    var baseSampleDistance = section.sampleDistance;
                    var iterations = section.iterations;

                    for (int i = 0; i < iterations; i++, totalIterations++)
                    {
                        cmd.SetGlobalInt(BlurIterationID, i);
                        var evenIter = i % 2 == 0;
                        if (i == iterations - 1 && evenIter)
                            cmd.SetGlobalFloat(SampleOffsetID, 0);
                        else
                            cmd.SetGlobalFloat(SampleOffsetID, evenIter ? 0.5f : -0.5f);

                        var sampleDistance = alpha * (baseSampleDistance + settings.blurAdditionalDistancePerIteration * totalIterations);
                        cmd.SetGlobalFloat(BlurSampleDistID, sampleDistance * renderScale);
                        FullScreenBlit(cmd, Temp1Id, Temp2Id, blursMat, firstKernelIdx);

                        if (!isSeparable)
                        {
                            (Temp1Id, Temp2Id) = (Temp2Id, Temp1Id);
                            continue;
                        }

                        FullScreenBlit(cmd, Temp2Id, Temp1Id, blursMat, secondKernelIdx);
                    }
                }
            }

            void FinalBlitToDestination()
            {
                cmd.SetGlobalFloat(DitherStrengthID, alpha * settings.ditherStrength);

                cmd.SetGlobalVector(DestinationRegionSizeID, blurRegion);
                if (hasRightEye)
                    cmd.SetGlobalVector(DestinationRegionSizeRightID, blurRegionRight.Value);

                cmd.SetGlobalFloat(VibrancyID, (alpha * settings.vibrancy + 1) * 0.5f);
                cmd.SetGlobalFloat(BrightnessID, alpha * settings.brightness);
                cmd.SetGlobalFloat(ContrastID, alpha * settings.contrast + 1);
                cmd.SetGlobalVector(TintID, alpha * settings.tint);

                var useQuadBlit = transformationMatrix != Matrix4x4.identity && (!overlayCompatibilityFix || uiCamera != null);
                if (useQuadBlit)
                {
                    if (multiPassVR)
                    {
                        BlitToQuad(cmd, Temp1Id, destination, quadBlitsMat, transformationMatrix, blurRegion, blurRegionRight, originalWidth, originalHeight, 0);
                    }
                    else
                    {
                        cmd.SetProjectionMatrix(uiCamera?.projectionMatrix ?? OverlayUIProjectionMatrix);
                        cmd.SetViewMatrix(uiCamera?.worldToCameraMatrix ?? Matrix4x4.identity);
                        BlitToQuad(cmd, Temp1Id, destination, quadBlitsMat, transformationMatrix, blurRegion, blurRegionRight, originalWidth, originalHeight, 0);
                        cmd.SetProjectionMatrix(camera.projectionMatrix);
                        cmd.SetViewMatrix(camera.worldToCameraMatrix);
                    }
                }
                else
                {
                    if (isAngled)
                    {
                        cmd.SetGlobalFloat(RenderScaleID, renderScale);
                        cmd.SetGlobalVectorArray(CornersID, blurCorners);
                        if (hasRightEye)
                            cmd.SetGlobalVectorArray(CornersRightID, blurCornersRight);
                    }

                    BlitToRegion(cmd, Temp1Id, destination, regionalBlitsMat, blurRegion, originalWidth, originalHeight, isAngled ? 3 : 2);
                }

                cmd.SetRenderTarget(cameraRT);
                cmd.ReleaseTemporaryRT(Temp1Id);
                cmd.ReleaseTemporaryRT(Temp2Id);
            }
        }
    }
#endif

    private static void BlitToQuad(CommandBuffer cmd, RenderTargetIdentifier source, RenderTargetIdentifier destination, Material material, Matrix4x4 quadMatrix, Vector4 blurRegion, Vector4? blurRegionRight, float width, float height, int passIndex)
    {
        cmd.SetGlobalTexture(MainTexID, source);
        cmd.SetRenderTarget(new RenderTargetIdentifier
        (
            destination, 0, CubemapFace.Unknown, -1),
            RenderBufferLoadAction.DontCare,
            RenderBufferStoreAction.Store,
            RenderBufferLoadAction.DontCare,
            RenderBufferStoreAction.DontCare
        );

        cmd.SetGlobalVector(BlurRegionID, new Vector4(blurRegion.x / width, blurRegion.y / height, blurRegion.z / width, blurRegion.w / height));
        if (blurRegionRight.HasValue)
            cmd.SetGlobalVector(BlurRegionRightID, new Vector4(blurRegionRight.Value.x / width, blurRegionRight.Value.y / height, blurRegionRight.Value.z / width, blurRegionRight.Value.w / height));

        cmd.DrawMesh(FullScreenMesh, quadMatrix, material, 0, passIndex);
    }

    private static void BlitToRegion(CommandBuffer cmd, RenderTargetIdentifier source, RenderTargetIdentifier destination, Material material, Vector4 blurRegion, float width, float height, int passIndex = 0)
    {
        cmd.SetGlobalTexture(MainTexID, source);
        cmd.SetRenderTarget(new RenderTargetIdentifier
        (
            destination, 0, CubemapFace.Unknown, -1),
            RenderBufferLoadAction.Load,
            RenderBufferStoreAction.Store,
            RenderBufferLoadAction.DontCare,
            RenderBufferStoreAction.DontCare
        );

        var left     = blurRegion.x / width * 2 - 1;
        var right    = (blurRegion.x + blurRegion.z) / width * 2 - 1;
        var bottom   = blurRegion.y / height * 2 - 1;
        var top      = (blurRegion.y + blurRegion.w) / height * 2 - 1;

        var transformationMatrix = Matrix4x4.TRS(new Vector3((left + right) * 0.5f, (top + bottom) * 0.5f, 0), Quaternion.identity, new Vector3((right - left) * 0.5f, (top - bottom) * 0.5f, 1));
        cmd.DrawMesh(FullScreenMesh, transformationMatrix, material, 0, passIndex);
    }

    private static void FullScreenBlit(CommandBuffer cmd, RenderTargetIdentifier source, RenderTargetIdentifier destination, Material material, int passIndex = 0)
    {
        cmd.SetGlobalTexture(MainTexID, source);
        cmd.SetGlobalTexture(DestinationTexID, destination);
        cmd.SetRenderTarget(new RenderTargetIdentifier
        (
            destination, 0, CubemapFace.Unknown, -1),
            RenderBufferLoadAction.Load,
            RenderBufferStoreAction.Store,
            RenderBufferLoadAction.DontCare,
            RenderBufferStoreAction.DontCare
        );
        cmd.DrawMesh(FullScreenMesh, Matrix4x4.identity, material, 0, passIndex);
    }

    private static Mesh _fullScreenMesh;
    private static Mesh FullScreenMesh
    {
        get
        {
            if (_fullScreenMesh != null)
                return _fullScreenMesh;

            return _fullScreenMesh = GetDefaultQuadMesh(true);
        }
    }

    private static Mesh GetDefaultQuadMesh(bool markNoLongerReadable)
    {
        var mesh = new Mesh { name = "Quad" };
        mesh.SetVertices(new Vector3[]
        {
            new (-1.0f, -1.0f, 0.0f),
            new (-1.0f,  1.0f, 0.0f),
            new (1.0f, -1.0f, 0.0f),
            new (1.0f,  1.0f, 0.0f)
        });

        mesh.SetUVs(0, new Vector2[]
        {
            new (0.0f, 0.0f),
            new (0.0f, 1.0f),
            new (1.0f, 0.0f),
            new (1.0f, 1.0f)
        });

        mesh.SetIndices(new[] { 0, 1, 2, 2, 1, 3 }, MeshTopology.Triangles, 0, false);
        mesh.UploadMeshData(markNoLongerReadable);
        return mesh;
    }
}
}