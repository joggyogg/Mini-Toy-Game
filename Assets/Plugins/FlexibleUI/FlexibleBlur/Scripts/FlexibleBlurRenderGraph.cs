#if UNITY_2023_3_OR_NEWER
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR;

namespace JeffGrawAssets.FlexibleUI
{
public partial class FlexibleBlurPass
{
    class BlurPassData
    {
        public PooledListDictionary<(BlurSettings blurSettings, float alpha), List<IBlur>, IBlur> batchedBlurs;
        public TextureHandle source;
        public TextureHandle temporaryUIBlurFullScreenHandle;
        public List<TextureHandle> instanceHandles = new();
        public List<TextureHandle> textureHandles = new();
        public List<(int width, int height)> textureDimensions = new();
        public Queue<int> textureIndices = new();
        public List<IBlur> blurAreas, blurredImageAreas;
        public Camera camera;
        public int featureIdx;
        public float renderScale, originalWidth, originalHeight;
        public bool singlePassVR, multiPassVR, rightEye, haveUIBlurAreas, haveBlurredImageAreas, blurredImageLayersSeeLower, blurredImageSeeUIBlurs, uiBlurLayersSeeLower, useComputeShaders, overlayCompatibilityFix;
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        if (!blurLayerAdddedThisFrame && (IndividuallyPaused || FlexibleBlurFeature.GloballyPaused))
            return;

        blurLayerAdddedThisFrame = false;
        var cameraData = frameData.Get<UniversalCameraData>();
        if (cameraData.isPreviewCamera)
            return;

        var camera = cameraData.camera;
        var key = (camera, featureIdx);
        SharedSetup(camera.pixelWidth, camera.pixelHeight, cameraData.renderScale);

        UIBlur.BlurDict.TryGetValue(key, out var blurAreas);
        FlexibleBlurFeature.ImageBasedBlurDict.TryGetValue(key, out var blurredImageAreas);
        var haveUIBlurAreas = blurAreas is { Count: > 0 };
        var haveBlurredImageAreas = blurredImageAreas is { Count: > 0 };

        Setup(cameraData.camera, cameraData.cameraTargetDescriptor);
        if (!haveUIBlurAreas && !haveBlurredImageAreas)
            return;

        var textureDescriptor = cameraData.cameraTargetDescriptor;
        textureDescriptor.enableRandomWrite = useComputeShaders;
        textureDescriptor.msaaSamples = 1;
        textureDescriptor.depthStencilFormat = GraphicsFormat.None;
        textureDescriptor.graphicsFormat = blurGraphicsFormat;

        var (originalWidth, originalHeight) = (textureDescriptor.width, textureDescriptor.height);
        var renderScale = cameraData.renderScale;
        var singlePassVR = XRSettings.enabled && XRSettings.stereoRenderingMode >= XRSettings.StereoRenderingMode.SinglePassInstanced;
        var rightEye = XRSettings.enabled && cameraData.xr.multipassId == 1;

        using var builder = renderGraph.AddUnsafePass<BlurPassData>(ProfilerTag, out var passData);

        (passData.camera, passData.featureIdx, passData.renderScale, passData.originalWidth, passData.originalHeight, passData.singlePassVR, passData.haveUIBlurAreas, passData.haveBlurredImageAreas, passData.blurAreas, passData.blurredImageAreas, passData.batchedBlurs, passData.blurredImageSeeUIBlurs, passData.blurredImageLayersSeeLower, passData.uiBlurLayersSeeLower, passData.useComputeShaders, passData.overlayCompatibilityFix) = 
        (         camera,          featureIdx,          renderScale,          originalWidth,          originalHeight,          singlePassVR,          haveUIBlurAreas,          haveBlurredImageAreas,          blurAreas,          blurredImageAreas,          batchedBlurs,          blurredImageSeeUIBlurs,          blurredImageLayersSeeLower,          uiBlurLayersSeeLower,          useComputeShaders,          overlayCompatibilityFix);

        passData.multiPassVR = XRSettings.enabled && XRSettings.stereoRenderingMode == XRSettings.StereoRenderingMode.MultiPass;
        passData.textureIndices.Clear();
        passData.textureHandles.Clear();
        passData.textureDimensions.Clear();
        passData.instanceHandles.Clear();
        passData.source = frameData.Get<UniversalResourceData>().activeColorTexture;
        builder.UseTexture(passData.source, AccessFlags.ReadWrite);

        foreach (var rtHandle in currentRTHandleList)
        {
            var texture = renderGraph.ImportTexture(rtHandle);
            passData.instanceHandles.Add(texture);
            builder.UseTexture(texture);
        }

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

        builder.SetRenderFunc(static(BlurPassData data, UnsafeGraphContext context) => Pass(data, context));

        void HandleUIBlurs(bool useTempRT = false)
        {
            if (!haveUIBlurAreas)
                return;

            if (useTempRT)
            {
                textureDescriptor.width = originalWidth;
                textureDescriptor.height = originalHeight;
                passData.temporaryUIBlurFullScreenHandle = UniversalRenderer.CreateRenderGraphTexture(renderGraph, textureDescriptor, nameof(BlurPassData.temporaryUIBlurFullScreenHandle), false, FilterMode.Bilinear);
                builder.UseTexture(passData.temporaryUIBlurFullScreenHandle);
            }

            foreach (var blurArea in blurAreas)
                AddTemporaryTextures(blurArea.Settings, rightEye ? blurArea.Common.BlurRegionRight : blurArea.Common.BlurRegion, blurArea.Alpha);
        }

        void HandleBlurredImages()
        {
            if (!haveBlurredImageAreas)
                return;

            int currentLayer = blurredImageAreas[0].Layer;
            int currentPriority = blurredImageAreas[0].Priority;
            foreach (var blurImage in blurredImageAreas)
            {
                if (blurImage.Layer > currentLayer || blurImage.Priority > currentPriority)
                {
                    TryApplyBatchedBlurs();
                    currentLayer = blurImage.Layer;
                    currentPriority = blurImage.Priority;
                }

                if (blurImage.CanBatch)
                    batchedBlurs.Add((blurImage.Settings, blurImage.Alpha), blurImage);
                else
                    AddTemporaryTextures(blurImage.Settings, rightEye ? blurImage.Common.BlurRegionRight : blurImage.Common.BlurRegion, blurImage.Alpha);
            }

            TryApplyBatchedBlurs();

            void TryApplyBatchedBlurs()
            {
                foreach (var kvp in batchedBlurs)
                {
                    var fillEntireRenderTexture = false;
                    foreach (var blur in kvp.Value)
                    {
                        if (!blur.FillEntireRenderTexture)
                            continue;

                        fillEntireRenderTexture = true;
                        break;
                    }

                    if (!fillEntireRenderTexture && kvp.Value.Count == 1)
                    {
                        var blurImage = kvp.Value[0];
                        AddTemporaryTextures(blurImage.Settings, rightEye ? blurImage.Common.BlurRegionRight : blurImage.Common.BlurRegion, blurImage.Alpha);
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

                            if (!singlePassVR)
                                continue;

                            minX = Math.Min(minX, batchedBlur.MinX(true));
                            minY = Math.Min(minY, batchedBlur.MinY(true));
                            maxX = Math.Max(maxX, batchedBlur.MaxX(true));
                            maxY = Math.Max(maxY, batchedBlur.MaxY(true));
                        }
                    }

                    var blurRegion = UIBlurCommon.ComputeBlurRegion(minX, minY, maxX, maxY);
                    var settings = kvp.Key.blurSettings;
                    AddTemporaryTextures(settings, blurRegion, kvp.Key.alpha);
                }

                batchedBlurs.Clear();
            }
        }

        void AddTemporaryTextures(BlurSettings settings, Vector4 blurRegion, float alpha)
        {
            blurRegion *= renderScale;
            var handles = passData.textureHandles;
            var dimensions = passData.textureDimensions;
            var indices = passData.textureIndices;
            var currentTextureIdx = 0;

            var aspect = blurRegion.z / blurRegion.w;
            var scale = renderScale * (settings.referenceResolution > 0 ? (float)settings.referenceResolution / originalHeight : 1f);
            textureDescriptor.height = Mathf.Max(1, Mathf.RoundToInt(scale * blurRegion.w));
            textureDescriptor.width = Mathf.Max(1, Mathf.RoundToInt(textureDescriptor.height * aspect));
            var referenceHeightForDownScale = textureDescriptor.height;

            FindExistingHandleOrAddNew();

            bool temp2NeedsInit = true;
            if (alpha <= 0)
                return;

            int totalIterations = 0;
            foreach (var section in settings.downscaleSections)
            {
                var iterations = section.iterations;
                var (isSeparable, _, _, _, _) = section.GetSectionBehaviour();

                for (int i = 0; i < iterations; i++, totalIterations++)
                {
                    textureDescriptor.height = Mathf.Max(1, Mathf.RoundToInt(referenceHeightForDownScale / Mathf.Pow(1 + alpha, totalIterations + 1)));
                    textureDescriptor.width = Mathf.Max(1, Mathf.RoundToInt(textureDescriptor.height * aspect));
                    FindExistingHandleOrAddNew();

                    if (!isSeparable)
                    {
                        temp2NeedsInit = true;
                        continue;
                    }

                    temp2NeedsInit = false;
                    FindExistingHandleOrAddNew();
                }
            }

            if (temp2NeedsInit)
                FindExistingHandleOrAddNew();

            void FindExistingHandleOrAddNew()
            {
                const string textureName = "blurTexture";

                while (true)
                {
                    if (currentTextureIdx >= handles.Count)
                    {
                        var newHandle = UniversalRenderer.CreateRenderGraphTexture(renderGraph, textureDescriptor, textureName, false, FilterMode.Bilinear);
                        dimensions.Add((textureDescriptor.width, textureDescriptor.height));
                        handles.Add(newHandle);
                        builder.UseTexture(newHandle, AccessFlags.ReadWrite);
                        indices.Enqueue(currentTextureIdx++);
                        return;
                    }

                    var existingDimensions = dimensions[currentTextureIdx];
                    if (existingDimensions.width == textureDescriptor.width && existingDimensions.height == textureDescriptor.height)
                    {
                        indices.Enqueue(currentTextureIdx++);
                        return;
                    }

                    currentTextureIdx++;
                }
            }
        }
    }

    private static void Pass(BlurPassData data, UnsafeGraphContext context)
    {
        var cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);

        var (textureHandle, textureDimensions, textureIndices, camera, featureIdx, renderScale, originalWidth, originalHeight, singlePassVR, multiPassVR, rightEye, haveUIBlurAreas, haveBlurredImageAreas, blurAreas, blurredImageAreas, batchedBlurs, blurredImageLayersSeeLower, blurredImageSeeUIBlurs, uiBlurLayersSeeLower, useComputeShaders, overlayCompatibilityFix) =
            (data.textureHandles, data.textureDimensions, data.textureIndices, data.camera, data.featureIdx, data.renderScale, data.originalWidth, data.originalHeight, data.singlePassVR, data.multiPassVR, data.rightEye, data.haveUIBlurAreas, data.haveBlurredImageAreas, data.blurAreas, data.blurredImageAreas,data.batchedBlurs, data.blurredImageLayersSeeLower, data.blurredImageSeeUIBlurs, data.uiBlurLayersSeeLower, data.useComputeShaders, data.overlayCompatibilityFix);

        var destinationRequiresClear = haveUIBlurAreas && (!haveBlurredImageAreas || blurredImageSeeUIBlurs);
        var key = (camera, featureIdx);

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

        void HandleUIBlurs(bool useTempRT = false)
        {
            if (!haveUIBlurAreas)
                return;

            TextureHandle layerHandle;
            if (useTempRT)
            {
                destinationRequiresClear = !blurredImageSeeUIBlurs;
                layerHandle = data.temporaryUIBlurFullScreenHandle;
            }
            else
            {
                layerHandle = data.instanceHandles[0];
            }

            if (uiBlurLayersSeeLower)
            {
                int currentLayer = blurAreas[0].Layer;
                foreach (var blur in blurAreas)
                {
                    if (blur.Layer > currentLayer)
                    {
                        currentLayer = blur.Layer;
                        FullScreenBlit(cmd, layerHandle, data.source, fullScreenBlitsMat, 1);
                    }

                    ApplyBlurRenderGraph(blur, data.source, layerHandle);
                }
            }
            else
            {
                foreach (var x in blurAreas)
                    ApplyBlurRenderGraph(x, data.source, layerHandle);
            }

            if (!destinationRequiresClear)
                FullScreenBlit(cmd, layerHandle, data.source, fullScreenBlitsMat, 1);
        }

        void HandleBlurredImages()
        {
            if (!haveBlurredImageAreas)
                return;

            var numImageLayers = FlexibleBlurFeature.ImageBasedLayersPerCameraDict[key];
            var source = data.source;
            var destinationIdx = 0;
            var destination = data.instanceHandles[destinationIdx];
            if (blurredImageLayersSeeLower && destinationIdx < numImageLayers - 1)
            {
                cmd.SetRenderTarget(destination);
                cmd.ClearRenderTarget(false, true, Color.clear);
                cmd.SetRenderTarget(source);
                FullScreenBlit(cmd, source, destination, fullScreenBlitsMat);
            }

            var currentLayer = blurredImageAreas[0].Layer;
            var currentPriority = blurredImageAreas[0].Priority;
            foreach (var blurImage in blurredImageAreas)
            {
                if (blurImage.Layer > currentLayer || blurImage.Priority > currentPriority)
                {
                    TryApplyBatchedBlurs();

                    if (blurImage.Layer > currentLayer)
                    {
                        destination = data.instanceHandles[++destinationIdx];
                        if (blurredImageLayersSeeLower)
                        {
                            source = data.instanceHandles[destinationIdx - 1];
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
                    ApplyBlurRenderGraph(blurImage, source, destination);
            }

            TryApplyBatchedBlurs();

            if (blurredImageLayersSeeLower)
                cmd.SetRenderTarget(data.source);

            void TryApplyBatchedBlurs()
            {
                foreach (var kvp in batchedBlurs)
                {
                    var fillEntireRenderTexture = false;
                    foreach (var blur in kvp.Value)
                    {
                        if (!blur.FillEntireRenderTexture)
                            continue;

                        fillEntireRenderTexture = true;
                        break;
                    }

                    if (!fillEntireRenderTexture && kvp.Value.Count == 1)
                    {
                        ApplyBlurRenderGraph(kvp.Value[0], source, destination);
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
                        cmd.SetRenderTarget(destination);
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
                    ApplyBlurUnifiedRenderGraph(source, destination, settings, blurRegion, singlePassVR ? blurRegion : null, null, null, kvp.Key.alpha, false, Matrix4x4.identity);

                }

                batchedBlurs.Clear();
            }
        }

        void ApplyBlurRenderGraph(IBlur iBlur, RenderTargetIdentifier source, RenderTargetIdentifier destination)
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
                ApplyBlurUnifiedRenderGraph(source, destination, iBlur.Settings, region, regionRight, corners, cornersRight, iBlur.Alpha, iBlur.IsAngled, iBlur.Matrix, iBlur.Common.WorldCamera);
            }
            else
            {
                var (region, corners) = rightEye ? (iBlur.Common.BlurRegionRight, iBlur.Common.ScreenCornersRight) : (iBlur.Common.BlurRegion, iBlur.Common.ScreenCorners);
                ApplyBlurUnifiedRenderGraph(source, destination, iBlur.Settings, region, null, corners, null, iBlur.Alpha, iBlur.IsAngled, iBlur.Matrix, iBlur.Common.WorldCamera);
            }
        }

        void ApplyBlurUnifiedRenderGraph(RenderTargetIdentifier source, RenderTargetIdentifier destination, BlurSettings settings, Vector4 blurRegion, Vector4? blurRegionRight, Vector4[] blurCorners, Vector4[] blurCornersRight, float alpha, bool isAngled, Matrix4x4 transformationMatrix, Camera uiCamera = null)
        {
            blurRegion *= renderScale;

            var hasRightEye = blurRegionRight.HasValue;
            if (hasRightEye)
            {
                blurRegionRight *= renderScale;
                var scaleFactorRight = new Vector2(originalWidth / blurRegionRight.Value.z, originalHeight / blurRegionRight.Value.w);
                cmd.SetGlobalVector(ScaleFactorRightID, scaleFactorRight);
                var offsetRight = scaleFactorRight * new Vector2(blurRegionRight.Value.x / originalWidth, blurRegionRight.Value.y / originalHeight);
                cmd.SetGlobalVector(SourceOffsetRightID, offsetRight);
            }

            var handleIdx = textureIndices.Dequeue();
            var temp1 = textureHandle[handleIdx];
            var temp2 = temp1;
            var temp1Dimensions = textureDimensions[handleIdx];
            var temp2Dimensions = temp1Dimensions;

            var scaleFactor = new Vector2(originalWidth / blurRegion.z, originalHeight / blurRegion.w);
            var offset = scaleFactor * new Vector2(blurRegion.x / originalWidth, blurRegion.y / originalHeight);
            cmd.SetGlobalVector(ScaleFactorID, scaleFactor);
            cmd.SetGlobalVector(SourceOffsetID, offset);

            FullScreenBlit(cmd, source, temp1, regionalBlitsMat, Convert.ToInt32(settings.hqResample));

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
                var threadGroupsX = (temp1Dimensions.width + ThreadGroupSizeX - 1) / ThreadGroupSizeX;
                var threadGroupsY = (temp1Dimensions.height + ThreadGroupSizeY - 1) / ThreadGroupSizeY;
                var computeShader = hasRightEye ? vrComputeBlurs : computeBlurs;

                bool temp2NeedsInit = true;
                if (alpha > 0)
                {
                    cmd.SetComputeIntParam(computeShader, ComputeOffsetCenterID, 0);
                    foreach (var section in settings.downscaleSections)
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
                        var sampleOffset = 1f;

                        for (int i = 0; i < iterations; i++, totalIterations++)
                        {
                            cmd.SetComputeIntParam(computeShader, ComputeBlurIterationID, i);
                            sampleOffset *= 0.5f;
                            var evenIter = i % 2 == 0;
                            var lastPass = i == iterations - 1;
                            cmd.SetComputeFloatParam(computeShader, ComputeSampleOffsetID, !evenIter ? -sampleOffset : lastPass ? 0 : sampleOffset);

                            handleIdx = textureIndices.Dequeue();
                            temp2 = textureHandle[handleIdx];
                            temp2Dimensions = textureDimensions[handleIdx];

                            cmd.SetComputeVectorParam(computeShader, ComputeResultDimensionsID, new Vector2(temp2Dimensions.width, temp2Dimensions.height));
                            cmd.SetComputeFloatParam(computeShader, ComputeSampleDistID, baseSampleDistance * renderScale);

                            cmd.SetComputeTextureParam(computeShader, firstKernelIdx, ComputeSourceID, temp1);
                            cmd.SetComputeTextureParam(computeShader, firstKernelIdx, ComputeResultID, temp2);

                            threadGroupsX = (temp1Dimensions.width + ThreadGroupSizeX - 1) / ThreadGroupSizeX;
                            threadGroupsY = (temp1Dimensions.height + ThreadGroupSizeY - 1) / ThreadGroupSizeY;
                            cmd.DispatchCompute(computeShader, firstKernelIdx, threadGroupsX, threadGroupsY, 1);

                            if (!isSeparable)
                            {
                                temp2NeedsInit = true;
                                (temp1, temp1Dimensions, temp2, temp2Dimensions) = (temp2, temp2Dimensions, temp1, temp1Dimensions);
                                continue;
                            }

                            temp2NeedsInit = false;
                            handleIdx = textureIndices.Dequeue();
                            temp1 = textureHandle[handleIdx];
                            temp1Dimensions = textureDimensions[handleIdx];

                            cmd.SetComputeTextureParam(computeShader, secondKernelIdx, ComputeSourceID, temp2);
                            cmd.SetComputeTextureParam(computeShader, secondKernelIdx, ComputeResultID, temp1);
                            cmd.DispatchCompute(computeShader, secondKernelIdx, threadGroupsX, threadGroupsY, 1);
                        }
                    }
                }

                if (temp2NeedsInit)
                {
                    handleIdx = textureIndices.Dequeue();
                    temp2 = textureHandle[handleIdx];
                    temp2Dimensions = textureDimensions[handleIdx];
                    cmd.SetComputeVectorParam(computeShader, ComputeResultDimensionsID, new Vector2(temp2Dimensions.width, temp2Dimensions.height));
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
                            cmd.SetComputeTextureParam(computeShader, firstKernelIdx, ComputeSourceID, temp1);
                            cmd.SetComputeTextureParam(computeShader, firstKernelIdx, ComputeResultID, temp2);
                            cmd.DispatchCompute(computeShader, firstKernelIdx, threadGroupsX, threadGroupsY, 1);
                            (temp1, temp1Dimensions, temp2, temp2Dimensions) = (temp2, temp2Dimensions, temp1, temp1Dimensions);
                        }
                    }
                    else
                    {
                        cmd.SetComputeTextureParam(computeShader, firstKernelIdx, ComputeSourceID, temp1);
                        cmd.SetComputeTextureParam(computeShader, firstKernelIdx, ComputeResultID, temp2);
                        cmd.SetComputeTextureParam(computeShader, secondKernelIdx, ComputeSourceID, temp2);
                        cmd.SetComputeTextureParam(computeShader, secondKernelIdx, ComputeResultID, temp1);
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
                bool temp2NeedsInit = true;
                if (alpha > 0)
                {
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

                            handleIdx = textureIndices.Dequeue();
                            temp2 = textureHandle[handleIdx];
                            temp2Dimensions = textureDimensions[handleIdx];

                            cmd.SetGlobalFloat(BlurSampleDistID, baseSampleDistance * renderScale);
                            FullScreenBlit(cmd, temp1, temp2, blursMat, firstKernelIdx);

                            if (!isSeparable)
                            {
                                temp2NeedsInit = true;
                                (temp1, temp1Dimensions, temp2, temp2Dimensions) = (temp2, temp2Dimensions, temp1, temp1Dimensions);
                                continue;
                            }

                            temp2NeedsInit = false;
                            handleIdx = textureIndices.Dequeue();
                            temp1 = textureHandle[handleIdx];
                            temp1Dimensions = textureDimensions[handleIdx];
                            FullScreenBlit(cmd, temp2, temp1, blursMat, secondKernelIdx);
                        }
                    }
                }

                if (temp2NeedsInit)
                    temp2 = textureHandle[textureIndices.Dequeue()];


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
                        FullScreenBlit(cmd, temp1, temp2, blursMat, firstKernelIdx);

                        if (!isSeparable)
                        {
                            (temp1, temp2) = (temp2, temp1);
                            continue;
                        }

                        FullScreenBlit(cmd, temp2, temp1, blursMat, secondKernelIdx);
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
                        BlitToQuad(cmd, temp1, destination, quadBlitsMat, transformationMatrix, blurRegion, blurRegionRight, originalWidth, originalHeight, 0);
                    }
                    else
                    {
                        cmd.SetProjectionMatrix(uiCamera?.projectionMatrix ?? OverlayUIProjectionMatrix);
                        cmd.SetViewMatrix(uiCamera?.worldToCameraMatrix ?? Matrix4x4.identity);
                        BlitToQuad(cmd, temp1, destination, quadBlitsMat, transformationMatrix, blurRegion, blurRegionRight, originalWidth, originalHeight, 0);
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

                    BlitToRegion(cmd, temp1, destination, regionalBlitsMat, blurRegion, originalWidth, originalHeight, isAngled ? 3 : 2);
                }

                cmd.SetRenderTarget(data.source);
            }
        }
    }
}
}
#endif