using UnityEngine;
using UnityEngine.Serialization;

namespace JeffGrawAssets.FlexibleUI
{
[ExecuteAlways]
[RequireComponent(typeof(FlexibleImage))]
public class AnimationClipAdapter : MonoBehaviour
{
    public int quadIdx;

    // Primary Colors (3×3)
    [HideInInspector] public Color colorPrimary0_0;
    [HideInInspector] public Color colorPrimary0_1;
    [HideInInspector] public Color colorPrimary0_2;
    [HideInInspector] public Color colorPrimary1_0;
    [HideInInspector] public Color colorPrimary1_1;
    [HideInInspector] public Color colorPrimary1_2;
    [HideInInspector] public Color colorPrimary2_0;
    [HideInInspector] public Color colorPrimary2_1;
    [HideInInspector] public Color colorPrimary2_2;

    // Outline Colors (3×3)
    [HideInInspector] public Color colorOutline0_0;
    [HideInInspector] public Color colorOutline0_1;
    [HideInInspector] public Color colorOutline0_2;
    [HideInInspector] public Color colorOutline1_0;
    [HideInInspector] public Color colorOutline1_1;
    [HideInInspector] public Color colorOutline1_2;
    [HideInInspector] public Color colorOutline2_0;
    [HideInInspector] public Color colorOutline2_1;
    [HideInInspector] public Color colorOutline2_2;

    // Procedural Gradient Colors (3×3)
    [HideInInspector] public Color colorProceduralGradient0_0;
    [HideInInspector] public Color colorProceduralGradient0_1;
    [HideInInspector] public Color colorProceduralGradient0_2;
    [HideInInspector] public Color colorProceduralGradient1_0;
    [HideInInspector] public Color colorProceduralGradient1_1;
    [HideInInspector] public Color colorProceduralGradient1_2;
    [HideInInspector] public Color colorProceduralGradient2_0;
    [HideInInspector] public Color colorProceduralGradient2_1;
    [HideInInspector] public Color colorProceduralGradient2_2;

    // Pattern Colors (3×3)
    [HideInInspector] public Color colorPattern0_0;
    [HideInInspector] public Color colorPattern0_1;
    [HideInInspector] public Color colorPattern0_2;
    [HideInInspector] public Color colorPattern1_0;
    [HideInInspector] public Color colorPattern1_1;
    [HideInInspector] public Color colorPattern1_2;
    [HideInInspector] public Color colorPattern2_0;
    [HideInInspector] public Color colorPattern2_1;
    [HideInInspector] public Color colorPattern2_2;

    // Color grid advanced settings
    [HideInInspector] public Vector2 primaryColorOffset;
    [HideInInspector] public Vector2 outlineColorOffset;
    [HideInInspector] public Vector2 proceduralGradientColorOffset;
    [HideInInspector] public Vector2 patternColorOffset;
    [HideInInspector] public Vector2 primaryColorScale;
    [HideInInspector] public Vector2 outlineColorScale;
    [HideInInspector] public Vector2 proceduralGradientColorScale;
    [HideInInspector] public Vector2 patternColorScale;
    [HideInInspector] public float primaryColorRotation;
    [HideInInspector] public float outlineColorRotation;
    [HideInInspector] public float proceduralGradientColorRotation;
    [HideInInspector] public float patternColorRotation;

    // Remaining ProceduralAnimationStateProperties
    [HideInInspector] public Vector4 uvRect;
    [HideInInspector] public Vector2 offset;
    [HideInInspector] public float stroke;
    [HideInInspector] public Vector2 sizeModifier;
    [HideInInspector] public float softness;
    [HideInInspector] public float rotation;
    [HideInInspector] public Vector4 cutout;
    [HideInInspector] public Vector4 cornerChamfer;
    [HideInInspector] public Vector4 cornerConcavity;
    [HideInInspector] public float collapsedCornerChamfer;
    [HideInInspector] public float collapsedCornerConcavity;
    [HideInInspector] public float collapseEdgeAmount;
    [HideInInspector] public float collapseEdgePosition;
    [HideInInspector] public float collapseEdgeAmountAbsolute;
    [HideInInspector] public byte primaryColorFade;
    [HideInInspector] public float outlineWidth;
    [HideInInspector] public Vector2 proceduralGradientPosition;
    [HideInInspector] public Vector2 radialGradientSize;
    [HideInInspector] public float radialGradientStrength;
    [HideInInspector] public Vector2 angleGradientStrength;
    [FormerlySerializedAs("angleGradientAngle")] [HideInInspector] public float proceduralGradientAngle;
    [HideInInspector] public float sdfGradientInnerDistance;
    [HideInInspector] public float sdfGradientOuterDistance;
    [HideInInspector] public float sdfGradientInnerReach;
    [HideInInspector] public float sdfGradientOuterReach;
    [HideInInspector] public float conicalGradientTailStrength;
    [HideInInspector] public float conicalGradientCurvature;
    [HideInInspector] public uint noiseSeed;
    [HideInInspector] public float noiseScale;
    [HideInInspector] public float noiseEdge;
    [HideInInspector] public float noiseStrength;
    [HideInInspector] public float patternDensity;
    [HideInInspector] public float patternSpeed;
    [HideInInspector] public float patternCellParam;
    [HideInInspector] public byte patternLineThickness;
    [HideInInspector] public int patternSpriteRotation;

    // QuadData-specific fields
    [HideInInspector] public Vector2 anchorMin;
    [HideInInspector] public Vector2 anchorMax;
    [HideInInspector] public Vector2 anchoredPosition;
    [HideInInspector] public Vector2 sizeDelta;
    [HideInInspector] public Vector2 pivot;
    [HideInInspector] public bool sizeModifierAspectCorrection;
    [HideInInspector] public bool fitRotatedImageWithinBounds;
    [HideInInspector] public bool outlineFadeTowardsInterior;
    [HideInInspector] public bool outlineAdjustsChamfer;
    [HideInInspector] public float primaryColorPresetMix;
    [HideInInspector] public float outlineColorPresetMix;
    [HideInInspector] public float proceduralGradientColorPresetMix;
    [HideInInspector] public float patternColorPresetMix;
    [HideInInspector] public int proceduralGradientType;
    [HideInInspector] public bool proceduralGradientAlphaIsBlend;
    [HideInInspector] public bool patternColorAlphaIsBlend;
    [HideInInspector] public bool proceduralGradientAspectCorrection;
    [HideInInspector] public bool proceduralGradientAffectsInterior;
    [HideInInspector] public bool proceduralGradientAffectsOutline;
    [HideInInspector] public bool patternAffectsInterior;
    [HideInInspector] public bool patternAffectsOutline;
    [HideInInspector] public bool screenSpaceProceduralGradient;
    [HideInInspector] public bool screenSpacePattern;
    [HideInInspector] public int pattern;
    [HideInInspector] public int patternOriginPos;
    [HideInInspector] public int softnessFeatherMode;
    [HideInInspector] public int strokeOrigin;
    [HideInInspector] public bool normalizeChamfer;
    [HideInInspector] public int collapsedEdge;
    [HideInInspector] public bool collapseIntoParallelogram;
    [HideInInspector] public bool mirrorCollapse;
    [HideInInspector] public bool proceduralGradientInvert;
    [HideInInspector] public bool outlineAlphaIsBlend;
    [HideInInspector] public bool addInteriorOutline;
    [HideInInspector] public bool outlineExpandsOutward;
    [HideInInspector] public bool outlineAccommodatesCollapsedEdge;
    [HideInInspector] public bool cutoutOnlyAffectsOutline;
    [HideInInspector] public bool invertCutout;
    [HideInInspector] public int cutoutRule;
    [HideInInspector] public int meshSubdivisions;
    [HideInInspector] public bool concavityIsSmoothing;

    // Previous values for ProceduralProperties
    private Color[] colorPrimaryPrev  = new Color[ProceduralProperties.Colors1dArrayLength];
    private Color[] colorOutlinePrev  = new Color[ProceduralProperties.Colors1dArrayLength];
    private Color[] colorGradientPrev = new Color[ProceduralProperties.Colors1dArrayLength];
    private Color[] colorPatternPrev  = new Color[ProceduralProperties.Colors1dArrayLength];
    private Vector4 uvRectPrev, cutoutPrev, cornerChamferPrev, cornerConcavityPrev;
    private Vector2 offsetPrev, sizeModifierPrev, proceduralGradientPositionPrev, radialGradientSizePrev, angleGradientStrengthPrev;
    private Vector2 primaryColorOffsetPrev, outlineColorOffsetPrev, proceduralGradientColorOffsetPrev, patternColorOffsetPrev;
    private Vector2 primaryColorScalePrev, outlineColorScalePrev, proceduralGradientColorScalePrev, patternColorScalePrev;
    private uint noiseSeedPrev;
    private int patternSpriteRotationPrev;
    private float noiseScalePrev, noiseEdgePrev, noiseStrengthPrev;
    private float primaryColorRotationPrev, outlineColorRotationPrev, proceduralGradientColorRotationPrev, patternColorRotationPrev;
    private float strokePrev, softnessPrev, rotationPrev, collapsedCornerChamferPrev, collapsedCornerConcavityPrev;
    private float collapseEdgeAmountPrev, collapseEdgePositionPrev, collapseEdgeAmountAbsolutePrev, outlineWidthPrev, radialGradientStrengthPrev;
    private float proceduralGradientAnglePrev, sdfGradientInnerDistancePrev, sdfGradientOuterDistancePrev, sdfGradientInnerReachPrev, sdfGradientOuterReachPrev;
    private float conicalGradientTailStrengthPrev, conicalGradientCurvaturePrev, patternDensityPrev, patternSpeedPrev, patternCellParamPrev;
    private byte primaryColorFadePrev, patternLineThicknessPrev;

    // Previous values for QuadData-specific fields
    private Vector2 anchorMinPrev, anchorMaxPrev, anchoredPositionPrev, sizeDeltaPrev, pivotPrev;
    private float primaryColorPresetMixPrev, outlineColorPresetMixPrev, proceduralGradientColorPresetMixPrev, patternColorPresetMixPrev;
    private int proceduralGradientTypePrev, patternPrev, patternOriginPosPrev, meshSubdivisionsPrev;
    private int softnessFeatherModePrev, strokeOriginPrev, collapsedEdgePrev, cutoutRulePrev;
    private bool sizeModifierAspectCorrectionPrev, fitRotatedImageWithinBoundsPrev, outlineFadeTowardsInteriorPrev, outlineAdjustsChamferPrev;
    private bool proceduralGradientAlphaIsBlendPrev, patternColorAlphaIsBlendPrev, proceduralGradientAspectCorrectionPrev;
    private bool proceduralGradientAffectsInteriorPrev, proceduralGradientAffectsOutlinePrev;
    private bool patternAffectsInteriorPrev, patternAffectsOutlinePrev, screenSpaceProceduralGradientPrev, screenSpacePatternPrev;
    private bool normalizeChamferPrev, collapseIntoParallelogramPrev, mirrorCollapsePrev, proceduralGradientInvertPrev;
    private bool outlineAlphaIsBlendPrev, addInteriorOutlinePrev, outlineExpandsOutwardPrev, outlineAccommodatesCollapsedEdgePrev;
    private bool cutoutOnlyAffectsOutlinePrev, invertCutoutPrev, concavityIsSmoothingPrev;

    private FlexibleImage target;
    private QuadData quadData;

#if UNITY_EDITOR
    protected void OnValidate() => target = GetComponent<FlexibleImage>();
#endif

    void OnEnable()
    {
        target = GetComponent<FlexibleImage>();
        SyncFromImage();
    }

    private void SyncFromImage()
    {
        if (target.ActiveQuadDataContainer.Count <= quadIdx)
        {
            quadData = null;
            Debug.LogWarning("Invalid quad index: " + quadIdx + " for " + name + "");
            return;
        }

        quadData = target.ActiveQuadDataContainer[quadIdx];
        var defaultProps = quadData.DefaultProceduralProps;

        // Sync from ProceduralProperties fields
        colorPrimary0_0 = colorPrimaryPrev[0] = defaultProps.primaryColors[0];
        colorPrimary1_0 = colorPrimaryPrev[1] = defaultProps.primaryColors[1];
        colorPrimary2_0 = colorPrimaryPrev[4] = defaultProps.primaryColors[4];
        colorPrimary0_1 = colorPrimaryPrev[3] = defaultProps.primaryColors[3];
        colorPrimary1_1 = colorPrimaryPrev[2] = defaultProps.primaryColors[2];
        colorPrimary2_1 = colorPrimaryPrev[5] = defaultProps.primaryColors[5];
        colorPrimary0_2 = colorPrimaryPrev[8] = defaultProps.primaryColors[8];
        colorPrimary1_2 = colorPrimaryPrev[7] = defaultProps.primaryColors[7];
        colorPrimary2_2 = colorPrimaryPrev[6] = defaultProps.primaryColors[6];
        
        colorOutline0_0 = colorOutlinePrev[0] = defaultProps.outlineColors[0];
        colorOutline1_0 = colorOutlinePrev[1] = defaultProps.outlineColors[1];
        colorOutline2_0 = colorOutlinePrev[4] = defaultProps.outlineColors[4];
        colorOutline0_1 = colorOutlinePrev[3] = defaultProps.outlineColors[3];
        colorOutline1_1 = colorOutlinePrev[2] = defaultProps.outlineColors[2];
        colorOutline2_1 = colorOutlinePrev[5] = defaultProps.outlineColors[5];
        colorOutline0_2 = colorOutlinePrev[8] = defaultProps.outlineColors[8];
        colorOutline1_2 = colorOutlinePrev[7] = defaultProps.outlineColors[7];
        colorOutline2_2 = colorOutlinePrev[6] = defaultProps.outlineColors[6];

        colorProceduralGradient0_0 = colorGradientPrev[0] = defaultProps.proceduralGradientColors[0];
        colorProceduralGradient1_0 = colorGradientPrev[1] = defaultProps.proceduralGradientColors[1];
        colorProceduralGradient2_0 = colorGradientPrev[4] = defaultProps.proceduralGradientColors[4];
        colorProceduralGradient0_1 = colorGradientPrev[3] = defaultProps.proceduralGradientColors[3];
        colorProceduralGradient1_1 = colorGradientPrev[2] = defaultProps.proceduralGradientColors[2];
        colorProceduralGradient2_1 = colorGradientPrev[5] = defaultProps.proceduralGradientColors[5];
        colorProceduralGradient0_2 = colorGradientPrev[8] = defaultProps.proceduralGradientColors[8];
        colorProceduralGradient1_2 = colorGradientPrev[7] = defaultProps.proceduralGradientColors[7];
        colorProceduralGradient2_2 = colorGradientPrev[6] = defaultProps.proceduralGradientColors[6];
        
        colorPattern0_0 = colorPatternPrev[0] = defaultProps.patternColors[0];
        colorPattern1_0 = colorPatternPrev[1] = defaultProps.patternColors[1];
        colorPattern2_0 = colorPatternPrev[4] = defaultProps.patternColors[4];
        colorPattern0_1 = colorPatternPrev[3] = defaultProps.patternColors[3];
        colorPattern1_1 = colorPatternPrev[2] = defaultProps.patternColors[2];
        colorPattern2_1 = colorPatternPrev[5] = defaultProps.patternColors[5];
        colorPattern0_2 = colorPatternPrev[8] = defaultProps.patternColors[8];
        colorPattern1_2 = colorPatternPrev[7] = defaultProps.patternColors[7];
        colorPattern2_2 = colorPatternPrev[6] = defaultProps.patternColors[6];

        primaryColorOffset = primaryColorOffsetPrev = defaultProps.primaryColorOffset;
        outlineColorOffset = outlineColorOffsetPrev = defaultProps.outlineColorOffset;
        proceduralGradientColorOffset = proceduralGradientColorOffsetPrev = defaultProps.proceduralGradientColorOffset;
        patternColorOffset = patternColorOffsetPrev = defaultProps.patternColorOffset;
        primaryColorRotation = primaryColorRotationPrev = defaultProps.primaryColorRotation;
        outlineColorRotation = outlineColorRotationPrev = defaultProps.outlineColorRotation;
        proceduralGradientColorRotation = proceduralGradientColorRotationPrev = defaultProps.proceduralGradientColorRotation;
        patternColorRotation = patternColorRotationPrev = defaultProps.patternColorRotation;
        primaryColorScale = primaryColorScalePrev = defaultProps.primaryColorScale;
        outlineColorScale = outlineColorScalePrev = defaultProps.outlineColorScale;
        proceduralGradientColorScale = proceduralGradientColorScalePrev = defaultProps.proceduralGradientColorScale;
        patternColorScale = patternColorScalePrev = defaultProps.patternColorScale;

        uvRect = uvRectPrev = defaultProps.uvRect;
        offsetPrev = offset = defaultProps.offset;
        strokePrev = stroke = defaultProps.stroke;
        sizeModifierPrev = sizeModifier = defaultProps.sizeModifier;
        softnessPrev = softness = defaultProps.softness;
        rotationPrev = rotation = defaultProps.rotation;
        cutoutPrev = cutout = defaultProps.cutout;
        cornerChamferPrev = cornerChamfer = defaultProps.cornerChamfer;
        cornerConcavityPrev = cornerConcavity = defaultProps.cornerConcavity;
        collapsedCornerChamferPrev = collapsedCornerChamfer = defaultProps.collapsedCornerChamfer;
        collapsedCornerConcavityPrev = collapsedCornerConcavity = defaultProps.collapsedCornerConcavity;
        collapseEdgeAmountPrev = collapseEdgeAmount = defaultProps.collapseEdgeAmount;
        collapseEdgePositionPrev = collapseEdgePosition = defaultProps.collapseEdgePosition;
        collapseEdgeAmountAbsolutePrev = collapseEdgeAmountAbsolute = defaultProps.collapseEdgeAmountAbsolute;
        primaryColorFadePrev = primaryColorFade = defaultProps.primaryColorFade;
        outlineWidthPrev = outlineWidth = defaultProps.outlineWidth;
        proceduralGradientPositionPrev = proceduralGradientPosition = defaultProps.proceduralGradientPosition;
        radialGradientSizePrev = radialGradientSize = defaultProps.radialGradientSize;
        radialGradientStrengthPrev = radialGradientStrength = defaultProps.radialGradientStrength;
        angleGradientStrengthPrev = angleGradientStrength = defaultProps.angleGradientStrength;
        proceduralGradientAnglePrev = proceduralGradientAngle = defaultProps.proceduralGradientAngle;
        sdfGradientInnerDistancePrev = sdfGradientInnerDistance = defaultProps.sdfGradientInnerDistance;
        sdfGradientOuterDistancePrev = sdfGradientOuterDistance = defaultProps.sdfGradientOuterDistance;
        sdfGradientInnerReachPrev = sdfGradientInnerReach = defaultProps.sdfGradientInnerReach;
        sdfGradientOuterReachPrev = sdfGradientOuterReach = defaultProps.sdfGradientOuterReach;
        conicalGradientTailStrengthPrev = conicalGradientTailStrength = defaultProps.conicalGradientTailStrength;
        conicalGradientCurvaturePrev = conicalGradientCurvature = defaultProps.conicalGradientCurvature;
        noiseSeedPrev = noiseSeed = defaultProps.noiseSeed;
        noiseScalePrev = noiseScale = defaultProps.noiseScale;
        noiseEdgePrev = noiseEdge = defaultProps.noiseEdge;
        noiseStrengthPrev = noiseStrength = defaultProps.noiseStrength;
        patternDensityPrev = patternDensity = defaultProps.patternDensity;
        patternSpeedPrev = patternSpeed = defaultProps.patternSpeed;
        patternCellParamPrev = patternCellParam = defaultProps.patternCellParam;
        patternLineThicknessPrev = patternLineThickness = defaultProps.patternLineThickness;
        patternSpriteRotationPrev = patternSpriteRotation = defaultProps.patternSpriteRotation;

        // Sync QuadData-specific fields
        anchorMinPrev = anchorMin = quadData.AnchorMin;
        anchorMaxPrev = anchorMax = quadData.AnchorMax;
        anchoredPositionPrev = anchoredPosition = quadData.AnchoredPosition;
        sizeDeltaPrev = sizeDelta = quadData.SizeDelta;
        pivotPrev = pivot = quadData.Pivot;
        sizeModifierAspectCorrectionPrev = sizeModifierAspectCorrection = quadData.SizeModifierAspectCorrection;
        fitRotatedImageWithinBoundsPrev = fitRotatedImageWithinBounds = quadData.FitRotatedImageWithinBounds;
        outlineFadeTowardsInteriorPrev = outlineFadeTowardsInterior = quadData.OutlineFadeTowardsInterior;
        outlineAdjustsChamferPrev = outlineAdjustsChamfer = quadData.OutlineAdjustsChamfer;
        primaryColorPresetMixPrev = primaryColorPresetMix = quadData.PrimaryColorPresetMix;
        outlineColorPresetMixPrev = outlineColorPresetMix = quadData.OutlineColorPresetMix;
        proceduralGradientColorPresetMixPrev = proceduralGradientColorPresetMix = quadData.ProceduralGradientColorPresetMix;
        patternColorPresetMixPrev = patternColorPresetMix = quadData.PatternColorPresetMix;
        proceduralGradientTypePrev = proceduralGradientType = (int)quadData.ProceduralGradientType;
        proceduralGradientAlphaIsBlendPrev = proceduralGradientAlphaIsBlend = quadData.ProceduralGradientAlphaIsBlend;
        patternColorAlphaIsBlendPrev = patternColorAlphaIsBlend = quadData.PatternColorAlphaIsBlend;
        proceduralGradientAspectCorrectionPrev = proceduralGradientAspectCorrection = quadData.ProceduralGradientAspectCorrection;
        proceduralGradientAffectsInteriorPrev = proceduralGradientAffectsInterior = quadData.ProceduralGradientAffectsInterior;
        proceduralGradientAffectsOutlinePrev = proceduralGradientAffectsOutline = quadData.ProceduralGradientAffectsOutline;
        patternAffectsInteriorPrev = patternAffectsInterior = quadData.PatternAffectsInterior;
        patternAffectsOutlinePrev = patternAffectsOutline = quadData.PatternAffectsOutline;
        screenSpaceProceduralGradientPrev = screenSpaceProceduralGradient = quadData.ScreenSpaceProceduralGradient;
        screenSpacePatternPrev = screenSpacePattern = quadData.ScreenSpacePattern;
        patternPrev = pattern = (int)quadData.Pattern;
        patternOriginPosPrev = patternOriginPos = (int)quadData.PatternOriginPos;
        softnessFeatherModePrev = softnessFeatherMode = (int)quadData.SoftnessFeatherMode;
        strokeOriginPrev = strokeOrigin = (int)quadData.StrokeOrigin;
        normalizeChamferPrev = normalizeChamfer = quadData.NormalizeChamfer;
        collapsedEdgePrev = collapsedEdge = (int)quadData.CollapsedEdge;
        collapseIntoParallelogramPrev = collapseIntoParallelogram = quadData.CollapseIntoParallelogram;
        mirrorCollapsePrev = mirrorCollapse = quadData.MirrorCollapse;
        proceduralGradientInvertPrev = proceduralGradientInvert = quadData.ProceduralGradientInvert;
        outlineAlphaIsBlendPrev = outlineAlphaIsBlend = quadData.OutlineAlphaIsBlend;
        addInteriorOutlinePrev = addInteriorOutline = quadData.AddInteriorOutline;
        outlineExpandsOutwardPrev = outlineExpandsOutward = quadData.OutlineExpandsOutward;
        outlineAccommodatesCollapsedEdgePrev = outlineAccommodatesCollapsedEdge = quadData.OutlineAccommodatesCollapsedEdge;
        cutoutOnlyAffectsOutlinePrev = cutoutOnlyAffectsOutline = quadData.CutoutOnlyAffectsOutline;
        invertCutoutPrev = invertCutout = quadData.InvertCutout;
        cutoutRulePrev = cutoutRule = (int)quadData.CutoutRule;
        meshSubdivisionsPrev = meshSubdivisions = quadData.MeshSubdivisions;
        concavityIsSmoothingPrev = concavityIsSmoothing = quadData.ConcavityIsSmoothing;
    }

    private static bool ColorsEquals(Color a, Color b) =>
        Mathf.Approximately(a.r, b.r) && Mathf.Approximately(a.g, b.g) && Mathf.Approximately(a.b, b.b) && Mathf.Approximately(a.a, b.a);

    private void OnDidApplyAnimationProperties()
    {
        if (quadData == null)
            return;

        var defaultProps = quadData.DefaultProceduralProps;

        // Update ProceduralProperties fields
        if (!ColorsEquals(colorPrimary0_0, colorPrimaryPrev[0])) defaultProps.primaryColors[0] = colorPrimaryPrev[0] = colorPrimary0_0;
        if (!ColorsEquals(colorPrimary1_0, colorPrimaryPrev[1])) defaultProps.primaryColors[1] = colorPrimaryPrev[1] = colorPrimary1_0;
        if (!ColorsEquals(colorPrimary2_0, colorPrimaryPrev[4])) defaultProps.primaryColors[4] = colorPrimaryPrev[4] = colorPrimary2_0;
        if (!ColorsEquals(colorPrimary0_1, colorPrimaryPrev[3])) defaultProps.primaryColors[3] = colorPrimaryPrev[3] = colorPrimary0_1;
        if (!ColorsEquals(colorPrimary1_1, colorPrimaryPrev[2])) defaultProps.primaryColors[2] = colorPrimaryPrev[2] = colorPrimary1_1;
        if (!ColorsEquals(colorPrimary2_1, colorPrimaryPrev[5])) defaultProps.primaryColors[5] = colorPrimaryPrev[5] = colorPrimary2_1;
        if (!ColorsEquals(colorPrimary0_2, colorPrimaryPrev[8])) defaultProps.primaryColors[8] = colorPrimaryPrev[8] = colorPrimary0_2;
        if (!ColorsEquals(colorPrimary1_2, colorPrimaryPrev[7])) defaultProps.primaryColors[7] = colorPrimaryPrev[7] = colorPrimary1_2;
        if (!ColorsEquals(colorPrimary2_2, colorPrimaryPrev[6])) defaultProps.primaryColors[6] = colorPrimaryPrev[6] = colorPrimary2_2;

        if (!ColorsEquals(colorOutline0_0, colorOutlinePrev[0])) defaultProps.outlineColors[0] = colorOutlinePrev[0] = colorOutline0_0;
        if (!ColorsEquals(colorOutline1_0, colorOutlinePrev[1])) defaultProps.outlineColors[1] = colorOutlinePrev[1] = colorOutline1_0;
        if (!ColorsEquals(colorOutline2_0, colorOutlinePrev[4])) defaultProps.outlineColors[4] = colorOutlinePrev[4] = colorOutline2_0;
        if (!ColorsEquals(colorOutline0_1, colorOutlinePrev[3])) defaultProps.outlineColors[3] = colorOutlinePrev[3] = colorOutline0_1;
        if (!ColorsEquals(colorOutline1_1, colorOutlinePrev[2])) defaultProps.outlineColors[2] = colorOutlinePrev[2] = colorOutline1_1;
        if (!ColorsEquals(colorOutline2_1, colorOutlinePrev[5])) defaultProps.outlineColors[5] = colorOutlinePrev[5] = colorOutline2_1;
        if (!ColorsEquals(colorOutline0_2, colorOutlinePrev[8])) defaultProps.outlineColors[8] = colorOutlinePrev[8] = colorOutline0_2;
        if (!ColorsEquals(colorOutline1_2, colorOutlinePrev[7])) defaultProps.outlineColors[7] = colorOutlinePrev[7] = colorOutline1_2;
        if (!ColorsEquals(colorOutline2_2, colorOutlinePrev[6])) defaultProps.outlineColors[6] = colorOutlinePrev[6] = colorOutline2_2;

        if (!ColorsEquals(colorProceduralGradient0_0, colorGradientPrev[0])) defaultProps.proceduralGradientColors[0] = colorGradientPrev[0] = colorProceduralGradient0_0;
        if (!ColorsEquals(colorProceduralGradient1_0, colorGradientPrev[1])) defaultProps.proceduralGradientColors[1] = colorGradientPrev[1] = colorProceduralGradient1_0;
        if (!ColorsEquals(colorProceduralGradient2_0, colorGradientPrev[4])) defaultProps.proceduralGradientColors[4] = colorGradientPrev[4] = colorProceduralGradient2_0;
        if (!ColorsEquals(colorProceduralGradient0_1, colorGradientPrev[3])) defaultProps.proceduralGradientColors[3] = colorGradientPrev[3] = colorProceduralGradient0_1;
        if (!ColorsEquals(colorProceduralGradient1_1, colorGradientPrev[2])) defaultProps.proceduralGradientColors[2] = colorGradientPrev[2] = colorProceduralGradient1_1;
        if (!ColorsEquals(colorProceduralGradient2_1, colorGradientPrev[5])) defaultProps.proceduralGradientColors[5] = colorGradientPrev[5] = colorProceduralGradient2_1;
        if (!ColorsEquals(colorProceduralGradient0_2, colorGradientPrev[8])) defaultProps.proceduralGradientColors[8] = colorGradientPrev[8] = colorProceduralGradient0_2;
        if (!ColorsEquals(colorProceduralGradient1_2, colorGradientPrev[7])) defaultProps.proceduralGradientColors[7] = colorGradientPrev[7] = colorProceduralGradient1_2;
        if (!ColorsEquals(colorProceduralGradient2_2, colorGradientPrev[6])) defaultProps.proceduralGradientColors[6] = colorGradientPrev[6] = colorProceduralGradient2_2;

        if (!ColorsEquals(colorPattern0_0, colorPatternPrev[0])) defaultProps.patternColors[0] = colorPatternPrev[0] = colorPattern0_0;
        if (!ColorsEquals(colorPattern1_0, colorPatternPrev[1])) defaultProps.patternColors[1] = colorPatternPrev[1] = colorPattern1_0;
        if (!ColorsEquals(colorPattern2_0, colorPatternPrev[4])) defaultProps.patternColors[4] = colorPatternPrev[4] = colorPattern2_0;
        if (!ColorsEquals(colorPattern0_1, colorPatternPrev[3])) defaultProps.patternColors[3] = colorPatternPrev[3] = colorPattern0_1;
        if (!ColorsEquals(colorPattern1_1, colorPatternPrev[2])) defaultProps.patternColors[2] = colorPatternPrev[2] = colorPattern1_1;
        if (!ColorsEquals(colorPattern2_1, colorPatternPrev[5])) defaultProps.patternColors[5] = colorPatternPrev[5] = colorPattern2_1;
        if (!ColorsEquals(colorPattern0_2, colorPatternPrev[8])) defaultProps.patternColors[8] = colorPatternPrev[8] = colorPattern0_2;
        if (!ColorsEquals(colorPattern1_2, colorPatternPrev[7])) defaultProps.patternColors[7] = colorPatternPrev[7] = colorPattern1_2;
        if (!ColorsEquals(colorPattern2_2, colorPatternPrev[6])) defaultProps.patternColors[6] = colorPatternPrev[6] = colorPattern2_2;
        
        if (primaryColorOffset != primaryColorOffsetPrev) defaultProps.primaryColorOffset = primaryColorOffsetPrev = primaryColorOffset;
        if (outlineColorOffset != outlineColorOffsetPrev) defaultProps.outlineColorOffset = outlineColorOffsetPrev = outlineColorOffset;
        if (proceduralGradientColorOffset != proceduralGradientColorOffsetPrev) defaultProps.proceduralGradientColorOffset = proceduralGradientColorOffsetPrev = proceduralGradientColorOffset;
        if (patternColorOffset != patternColorOffsetPrev) defaultProps.patternColorOffset = patternColorOffsetPrev = patternColorOffset;
        if (primaryColorScale != primaryColorScalePrev) defaultProps.primaryColorScale = primaryColorScalePrev = primaryColorScale;
        if (outlineColorScale != outlineColorScalePrev) defaultProps.outlineColorScale = outlineColorScalePrev = outlineColorScale;
        if (proceduralGradientColorScale != proceduralGradientColorScalePrev) defaultProps.proceduralGradientColorScale = proceduralGradientColorScalePrev = proceduralGradientColorScale;
        if (patternColorScale != patternColorScalePrev) defaultProps.patternColorScale = patternColorScalePrev = patternColorScale;
        if (!Mathf.Approximately(primaryColorRotation, primaryColorRotationPrev)) defaultProps.primaryColorRotation = primaryColorRotationPrev = primaryColorRotation;
        if (!Mathf.Approximately(outlineColorRotation, outlineColorRotationPrev)) defaultProps.outlineColorRotation = outlineColorRotationPrev = outlineColorRotation;
        if (!Mathf.Approximately(proceduralGradientColorRotation, proceduralGradientColorRotationPrev)) defaultProps.proceduralGradientColorRotation = proceduralGradientColorRotationPrev = proceduralGradientColorRotation;
        if (!Mathf.Approximately(patternColorRotation, patternColorRotationPrev)) defaultProps.patternColorRotation = patternColorRotationPrev = patternColorRotation;

        if (uvRect != uvRectPrev) defaultProps.uvRect = uvRectPrev = uvRect;
        if (offset != offsetPrev) defaultProps.offset = offsetPrev = offset;
        if (!Mathf.Approximately(stroke, strokePrev)) defaultProps.stroke = strokePrev = stroke;
        if (sizeModifier != sizeModifierPrev) defaultProps.sizeModifier = sizeModifierPrev = sizeModifier;
        if (!Mathf.Approximately(softness, softnessPrev)) defaultProps.softness = softnessPrev = softness;
        if (!Mathf.Approximately(rotation, rotationPrev)) defaultProps.rotation = rotationPrev = rotation;
        if (cutout != cutoutPrev) defaultProps.cutout = cutoutPrev = cutout;
        if (cornerChamfer != cornerChamferPrev) defaultProps.cornerChamfer = cornerChamferPrev = cornerChamfer;
        if (cornerConcavity != cornerConcavityPrev) defaultProps.cornerConcavity = cornerConcavityPrev = cornerConcavity;
        if (!Mathf.Approximately(collapsedCornerChamfer, collapsedCornerChamferPrev)) defaultProps.collapsedCornerChamfer = collapsedCornerChamferPrev = collapsedCornerChamfer;
        if (!Mathf.Approximately(collapsedCornerConcavity, collapsedCornerConcavityPrev)) defaultProps.collapsedCornerConcavity = collapsedCornerConcavityPrev = collapsedCornerConcavity;
        if (!Mathf.Approximately(collapseEdgeAmount, collapseEdgeAmountPrev)) defaultProps.collapseEdgeAmount = collapseEdgeAmountPrev = collapseEdgeAmount;
        if (!Mathf.Approximately(collapseEdgeAmountAbsolute, collapseEdgeAmountAbsolutePrev)) defaultProps.collapseEdgeAmountAbsolute = collapseEdgeAmountAbsolutePrev = collapseEdgeAmountAbsolute;
        if (!Mathf.Approximately(collapseEdgePosition, collapseEdgePositionPrev)) defaultProps.collapseEdgePosition = collapseEdgePositionPrev = collapseEdgePosition;
        if (primaryColorFade != primaryColorFadePrev) defaultProps.primaryColorFade = primaryColorFadePrev = primaryColorFade;
        if (!Mathf.Approximately(outlineWidth, outlineWidthPrev)) defaultProps.outlineWidth = outlineWidthPrev = outlineWidth;
        if (proceduralGradientPosition != proceduralGradientPositionPrev) defaultProps.proceduralGradientPosition = proceduralGradientPositionPrev = proceduralGradientPosition;
        if (radialGradientSize != radialGradientSizePrev) defaultProps.radialGradientSize = radialGradientSizePrev = radialGradientSize;
        if (!Mathf.Approximately(radialGradientStrength, radialGradientStrengthPrev)) defaultProps.radialGradientStrength = radialGradientStrengthPrev = radialGradientStrength;
        if (angleGradientStrength != angleGradientStrengthPrev) defaultProps.angleGradientStrength = angleGradientStrengthPrev = angleGradientStrength;
        if (!Mathf.Approximately(proceduralGradientAngle, proceduralGradientAnglePrev)) defaultProps.proceduralGradientAngle = proceduralGradientAnglePrev = proceduralGradientAngle;
        if (!Mathf.Approximately(sdfGradientInnerDistance, sdfGradientInnerDistancePrev)) defaultProps.sdfGradientInnerDistance = sdfGradientInnerDistancePrev = sdfGradientInnerDistance;
        if (!Mathf.Approximately(sdfGradientOuterDistance, sdfGradientOuterDistancePrev)) defaultProps.sdfGradientOuterDistance = sdfGradientOuterDistancePrev = sdfGradientOuterDistance;
        if (!Mathf.Approximately(sdfGradientInnerReach, sdfGradientInnerReachPrev)) defaultProps.sdfGradientInnerReach = sdfGradientInnerReachPrev = sdfGradientInnerReach;
        if (!Mathf.Approximately(sdfGradientOuterReach, sdfGradientOuterReachPrev)) defaultProps.sdfGradientOuterReach = sdfGradientOuterReachPrev = sdfGradientOuterReach;
        if (!Mathf.Approximately(conicalGradientTailStrength, conicalGradientTailStrengthPrev)) defaultProps.conicalGradientTailStrength = conicalGradientTailStrengthPrev = conicalGradientTailStrength;
        if (!Mathf.Approximately(conicalGradientCurvature, conicalGradientCurvaturePrev)) defaultProps.conicalGradientCurvature = conicalGradientCurvaturePrev = conicalGradientCurvature;
        if (noiseSeed != noiseSeedPrev) defaultProps.noiseSeed = noiseSeedPrev = noiseSeed;
        if (!Mathf.Approximately(noiseScale, noiseScalePrev)) defaultProps.noiseScale = noiseScalePrev = noiseScale;
        if (!Mathf.Approximately(noiseEdge, noiseEdgePrev)) defaultProps.noiseEdge = noiseEdgePrev = noiseEdge;
        if (!Mathf.Approximately(noiseStrength, noiseStrengthPrev)) defaultProps.noiseStrength = noiseStrengthPrev = noiseStrength;
        if (!Mathf.Approximately(patternDensity, patternDensityPrev)) defaultProps.patternDensity = patternDensityPrev = patternDensity;
        if (!Mathf.Approximately(patternSpeed, patternSpeedPrev)) defaultProps.patternSpeed = patternSpeedPrev = patternSpeed;
        if (!Mathf.Approximately(patternCellParam, patternCellParamPrev)) defaultProps.patternCellParam = patternCellParamPrev = patternCellParam;
        if (patternLineThickness != patternLineThicknessPrev) defaultProps.patternLineThickness = patternLineThicknessPrev = patternLineThickness;
        if (patternSpriteRotation != patternSpriteRotationPrev) defaultProps.patternSpriteRotation = patternSpriteRotationPrev = patternSpriteRotation;

        // Update QuadData-specific fields
        if (anchorMin != anchorMinPrev) quadData.AnchorMin = anchorMinPrev = anchorMin;
        if (anchorMax != anchorMaxPrev) quadData.AnchorMax = anchorMaxPrev = anchorMax;
        if (anchoredPosition != anchoredPositionPrev) quadData.AnchoredPosition = anchoredPositionPrev = anchoredPosition;
        if (sizeDelta != sizeDeltaPrev) quadData.SizeDelta = sizeDeltaPrev = sizeDelta;
        if (pivot != pivotPrev) quadData.Pivot = pivotPrev = pivot;
        if (sizeModifierAspectCorrection != sizeModifierAspectCorrectionPrev) quadData.SizeModifierAspectCorrection = sizeModifierAspectCorrectionPrev = sizeModifierAspectCorrection;
        if (fitRotatedImageWithinBounds != fitRotatedImageWithinBoundsPrev) quadData.FitRotatedImageWithinBounds = fitRotatedImageWithinBoundsPrev = fitRotatedImageWithinBounds;
        if (outlineFadeTowardsInterior != outlineFadeTowardsInteriorPrev) quadData.OutlineFadeTowardsInterior = outlineFadeTowardsInteriorPrev = outlineFadeTowardsInterior;
        if (outlineAdjustsChamfer != outlineAdjustsChamferPrev) quadData.OutlineAdjustsChamfer = outlineAdjustsChamferPrev = outlineAdjustsChamfer;
        if (!Mathf.Approximately(primaryColorPresetMix, primaryColorPresetMixPrev)) quadData.PrimaryColorPresetMix = primaryColorPresetMixPrev = primaryColorPresetMix;
        if (!Mathf.Approximately(outlineColorPresetMix, outlineColorPresetMixPrev)) quadData.OutlineColorPresetMix = outlineColorPresetMixPrev = outlineColorPresetMix;
        if (!Mathf.Approximately(proceduralGradientColorPresetMix, proceduralGradientColorPresetMixPrev)) quadData.ProceduralGradientColorPresetMix = proceduralGradientColorPresetMixPrev = proceduralGradientColorPresetMix;
        if (!Mathf.Approximately(patternColorPresetMix, patternColorPresetMixPrev)) quadData.PatternColorPresetMix = patternColorPresetMixPrev = patternColorPresetMix;
        if (proceduralGradientType != proceduralGradientTypePrev) quadData.ProceduralGradientType = (QuadData.GradientType)(proceduralGradientTypePrev = proceduralGradientType);
        if (proceduralGradientAlphaIsBlend != proceduralGradientAlphaIsBlendPrev) quadData.ProceduralGradientAlphaIsBlend = proceduralGradientAlphaIsBlendPrev = proceduralGradientAlphaIsBlend;
        if (patternColorAlphaIsBlend != patternColorAlphaIsBlendPrev) quadData.PatternColorAlphaIsBlend = patternColorAlphaIsBlendPrev = patternColorAlphaIsBlend;
        if (proceduralGradientAspectCorrection != proceduralGradientAspectCorrectionPrev) quadData.ProceduralGradientAspectCorrection = proceduralGradientAspectCorrectionPrev = proceduralGradientAspectCorrection;
        if (proceduralGradientAffectsInterior != proceduralGradientAffectsInteriorPrev) quadData.ProceduralGradientAffectsInterior = proceduralGradientAffectsInteriorPrev = proceduralGradientAffectsInterior;
        if (proceduralGradientAffectsOutline != proceduralGradientAffectsOutlinePrev) quadData.ProceduralGradientAffectsOutline = proceduralGradientAffectsOutlinePrev = proceduralGradientAffectsOutline;
        if (patternAffectsInterior != patternAffectsInteriorPrev) quadData.PatternAffectsInterior = patternAffectsInteriorPrev = patternAffectsInterior;
        if (patternAffectsOutline != patternAffectsOutlinePrev) quadData.PatternAffectsOutline = patternAffectsOutlinePrev = patternAffectsOutline;
        if (screenSpaceProceduralGradient != screenSpaceProceduralGradientPrev) quadData.ScreenSpaceProceduralGradient = screenSpaceProceduralGradientPrev = screenSpaceProceduralGradient;
        if (screenSpacePattern != screenSpacePatternPrev) quadData.ScreenSpacePattern = screenSpacePatternPrev = screenSpacePattern;
        if (pattern != patternPrev) quadData.Pattern = (QuadData.PatternType)(patternPrev = pattern);
        if (patternOriginPos != patternOriginPosPrev) quadData.PatternOriginPos = (QuadData.PatternOriginPosition)(patternOriginPosPrev = patternOriginPos);
        if (softnessFeatherMode != softnessFeatherModePrev) quadData.SoftnessFeatherMode = (QuadData.FeatherMode)(softnessFeatherModePrev = softnessFeatherMode);
        if (strokeOrigin != strokeOriginPrev) quadData.StrokeOrigin = (QuadData.StrokeOriginLocation)(strokeOriginPrev = strokeOrigin);
        if (normalizeChamfer != normalizeChamferPrev) quadData.NormalizeChamfer = normalizeChamferPrev = normalizeChamfer;
        if (collapsedEdge != collapsedEdgePrev) quadData.CollapsedEdge = (QuadData.CollapsedEdgeType)(collapsedEdgePrev = collapsedEdge);
        if (collapseIntoParallelogram != collapseIntoParallelogramPrev) quadData.CollapseIntoParallelogram = collapseIntoParallelogramPrev = collapseIntoParallelogram;
        if (mirrorCollapse != mirrorCollapsePrev) quadData.MirrorCollapse = mirrorCollapsePrev = mirrorCollapse;
        if (proceduralGradientInvert != proceduralGradientInvertPrev) quadData.ProceduralGradientInvert = proceduralGradientInvertPrev = proceduralGradientInvert;
        if (outlineAlphaIsBlend != outlineAlphaIsBlendPrev) quadData.OutlineAlphaIsBlend = outlineAlphaIsBlendPrev = outlineAlphaIsBlend;
        if (addInteriorOutline != addInteriorOutlinePrev) quadData.AddInteriorOutline = addInteriorOutlinePrev = addInteriorOutline;
        if (outlineExpandsOutward != outlineExpandsOutwardPrev) quadData.OutlineExpandsOutward = outlineExpandsOutwardPrev = outlineExpandsOutward;
        if (outlineAccommodatesCollapsedEdge != outlineAccommodatesCollapsedEdgePrev) quadData.OutlineAccommodatesCollapsedEdge = outlineAccommodatesCollapsedEdgePrev = outlineAccommodatesCollapsedEdge;
        if (cutoutOnlyAffectsOutline != cutoutOnlyAffectsOutlinePrev) quadData.CutoutOnlyAffectsOutline = cutoutOnlyAffectsOutlinePrev = cutoutOnlyAffectsOutline;
        if (invertCutout != invertCutoutPrev) quadData.InvertCutout = invertCutoutPrev = invertCutout;
        if (cutoutRule != cutoutRulePrev) quadData.CutoutRule = (QuadData.CutoutType)(cutoutRulePrev = cutoutRule);
        if (meshSubdivisions != meshSubdivisionsPrev) quadData.MeshSubdivisions = meshSubdivisionsPrev = meshSubdivisions;
        if (concavityIsSmoothing != concavityIsSmoothingPrev) quadData.ConcavityIsSmoothing = concavityIsSmoothingPrev = concavityIsSmoothing;

        target.SetVerticesDirty();
    }
}
}