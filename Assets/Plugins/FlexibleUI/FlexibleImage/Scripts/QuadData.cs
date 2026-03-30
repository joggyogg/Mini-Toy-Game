using System;
using UnityEngine;

namespace JeffGrawAssets.FlexibleUI
{ 
[Serializable]
[ExecuteAlways]
public class QuadData : ISerializationCallbackReceiver
{
    [Flags] public enum QuadModifiers { DisableSprite = 1, ForceSimpleMesh = 2}
    public enum Topology { Original, Flipped, X }
    public enum ColorGridWrapMode : byte { Clamp, Repeat, Mirror, PingPong}
    public enum FeatherMode { Inwards, Outwards, Bidirectional }
    public enum StrokeOriginLocation { Center, Perimeter, Outline }
    public enum GradientType { SDF, Angle, Radial, Conical, Noise }
    public enum CutoutType { OR, AND }
    public enum PatternOriginPosition { Center, Left, Right, Top, Bottom }
    public enum SpritePatternRotation { Sprite, Offset }
    public enum SpritePatternOffsetDirection { Zero, FortyFive, Ninety, OneThirtyFive }
    public enum CollapsedEdgeType { Top, Bottom, Left, Right }
    public enum AnimationDrivenBy { Selectable, Script }

    public enum PatternType
    {
        BottomRightToTopLeft = 0, Vertical = 1, BottomLeftToTopRight = 2, Horizontal = 3, DiamondShape = 4, CircleShape = 9,
        SquareShape = 14, CrossShape = 19, Fractal = 25, Sprite = 26, StraightGrid = 28, DiagonalGrid = 29, SquareGrid = 30, DiamondGrid = 31
    }

    public enum CutoutFillOrigin
    {
        Left, Right, Top, Bottom, HorizontalFromCenter, HorizontalFromPerimeter, VerticalFromCenter, VerticalFromPerimeter, 
        BothFromCenter, BothFromPerimeter, BothFromCenterCross, BothFromPerimeterCross, TopLeft, TopRight, BottomLeft, BottomRight
    }

#if UNITY_EDITOR
    public static readonly string EnabledFieldName = nameof(_enabled);
    public static readonly string AdvancedQuadSettingsFieldName = nameof(_advancedQuadSettings);
    public static readonly string ColorPresetFieldName = nameof(_colorPreset);
    public static readonly string PrimaryColorWrapModeXFieldName = nameof(_primaryColorWrapModeX);
    public static readonly string PrimaryColorWrapModeYFieldName = nameof(_primaryColorWrapModeY);
    public static readonly string OutlineColorWrapModeXFieldName = nameof(_outlineColorWrapModeX);
    public static readonly string OutlineColorWrapModeYFieldName = nameof(_outlineColorWrapModeY);
    public static readonly string ProceduralGradientColorWrapModeXFieldName = nameof(_proceduralGradientColorWrapModeX);
    public static readonly string ProceduralGradientColorWrapModeYFieldName = nameof(_proceduralGradientColorWrapModeY);
    public static readonly string PatternColorWrapModeXFieldName = nameof(_patternColorWrapModeX);
    public static readonly string PatternColorWrapModeYFieldName = nameof(_patternColorWrapModeY);
    public static readonly string PrimaryColorPresetMixFieldName = nameof(_primaryColorPresetMix);
    public static readonly string ProceduralGradientColorPresetMixFieldName = nameof(_proceduralGradientColorPresetMix);
    public static readonly string PatternColorPresetMixFieldName = nameof(_patternColorPresetMix);
    public static readonly string ProceduralGradientAspectCorrectionFieldName = nameof(_proceduralGradientAspectCorrection);
    public static readonly string OutlineColorPresetMixFieldName = nameof(_outlineColorPresetMix);
    public static readonly string NormalizeChamferFieldName = nameof(_normalizeChamfer);
    public static readonly string ConcavityIsSmoothingFieldName = nameof(_concavityIsSmoothing);
    public static readonly string CollapsedEdgeFieldName = nameof(_collapsedEdge);
    public static readonly string CollapseIntoParallelogramFieldName = nameof(_collapseIntoParallelogram);
    public static readonly string MirrorCollapseFieldName = nameof(_mirrorCollapse);
    public static readonly string EdgeCollapseAmountIsAbsoluteFieldName = nameof(_edgeCollapseAmountIsAbsolute);
    public static readonly string FitRotationWithinBoundsFieldName = nameof(_fitRotatedImageWithinBounds);
    public static readonly string OutlineFadeTowardsPerimeterFieldName = nameof(_outlineFadeTowardsPerimeter);
    public static readonly string OutlineAdjustsChamferFieldName = nameof(_outlineAdjustsChamfer);
    public static readonly string ProceduralGradientTypeFieldName = nameof(_proceduralGradientType);
    public static readonly string ProceduralGradientAffectsInteriorFieldName = nameof(_proceduralGradientAffectsInterior);
    public static readonly string ProceduralGradientAffectsOutlineFieldName = nameof(_proceduralGradientAffectsOutline);
    public static readonly string PatternAffectsInteriorFieldName = nameof(_patternAffectsInterior);
    public static readonly string PatternAffectsOutlineFieldName = nameof(_patternAffectsOutline);
    public static readonly string ProceduralGradientPositionFromPointerFieldName = nameof(_proceduralGradientPositionFromPointer);
    public static readonly string NoiseGradientAlternateModeFieldName = nameof(_noiseGradientAlternateMode);
    public static readonly string ScreenSpaceProceduralGradientFieldName = nameof(_screenSpaceProceduralGradient);
    public static readonly string ScreenSpacePatternFieldName = nameof(_screenSpacePattern);
    public static readonly string SoftPatternFieldName = nameof(_softPattern);
    public static readonly string SpritePatternRotationModeFieldName = nameof(_spritePatternRotationMode);
    public static readonly string SpritePatternOffsetDirectionDegreesFieldName = nameof(_spritePatternOffsetDirectionDegrees);
    public static readonly string PatternFieldName = nameof(_pattern);
    public static readonly string ScanlinePatternSpeedIsStaticOffsetFieldName = nameof(_scanlinePatternSpeedIsStaticOffset);
    public static readonly string PatternOriginPosFieldName = nameof(_patternOriginPos);
    public static readonly string CutoutRuleFieldName = nameof(_cutoutRule);
    public static readonly string CutoutEnabledFieldName = nameof(_cutoutEnabled);
    public static readonly string CutoutOnlyAffectsOutlineFieldName = nameof(_cutoutOnlyAffectsOutline);
    public static readonly string InvertCutoutFieldName = nameof(_invertCutout);
    public static readonly string PrimaryColorDimensionsFieldName = nameof(_primaryColorDimensions);
    public static readonly string OutlineColorDimensionsFieldName = nameof(_outlineColorDimensions);
    public static readonly string ProceduralGradientColorDimensionsFieldName = nameof(_proceduralGradientColorDimensions);
    public static readonly string PatternColorDimensionsFieldName = nameof(_patternColorDimensions);
    public static readonly string ProceduralGradientAlphaIsBlendFieldName = nameof(_proceduralGradientAlphaIsBlend);
    public static readonly string PatternColorAlphaIsBlendFieldName = nameof(_patternColorAlphaIsBlend);
    public static readonly string OutlineAlphaIsBlendFieldName = nameof(_outlineAlphaIsBlend);
    public static readonly string AddInteriorOutlineFieldName = nameof(_addInteriorOutline);
    public static readonly string OutlineExpandsOutwardsFieldName = nameof(_outlineExpandsOutward);
    public static readonly string OutlineAccommodatesCollapsedEdgeFieldName = nameof(_outlineAccommodatesCollapsedEdge);
    public static readonly string MeshSubdivisionsFieldName = nameof(_meshSubdivisions);
    public static readonly string MeshTopologyFieldName = nameof(_meshTopology);
    public static readonly string SizeModifierAspectCorrectionFieldName = nameof(_sizeModifierAspectCorrection);
    public static readonly string ProceduralGradientInvertFieldName = nameof(_proceduralGradientInvert);
    public static readonly string SoftnessFeatherModeFieldName = nameof(_softnessFeatherMode);
    public static readonly string StrokeOriginFieldName = nameof(_strokeOrigin);
    public static readonly string AnchorMinFieldName = nameof(_anchorMin);
    public static readonly string AnchorMaxFieldName = nameof(_anchorMax);
    public static readonly string AnchoredPositionFieldName = nameof(_anchoredPosition);
    public static readonly string SizeDeltaFieldName = nameof(_sizeDelta);
    public static readonly string PivotFieldName = nameof(_pivot);

    public void PreviewInEditor(AnimationValues animationValues, int prevState, int prevSubstate, int state, int substate, float percentageDone)
    {
        var prev = proceduralAnimationStates[prevState].proceduralProperties[prevSubstate];
        var next = proceduralAnimationStates[state].proceduralProperties[substate];
        animationValues.SetCurrentProps(new ProceduralProperties(), false);
        ProceduralAnimationState.LerpProperties(animationValues.CurrentProperties, prev, next, percentageDone);
    }

    // Only used for previewing in the editor, and specifically used for undo.
    public int editorSelectedAnimationState, editorSelectedAnimationSubState;
#endif

    public string name;

    [SerializeField] private Vector2 _anchorMin;
    [SerializeField] private Vector2 _anchorMax = Vector2.one;
    [SerializeField] private Vector2 _anchoredPosition;
    [SerializeField] private Vector2 _sizeDelta;
    [SerializeField] private Vector2 _pivot = new (0.5f, 0.5f);

    public Vector2 AnchorMin
    {
        get => _anchorMin;
        set => _anchorMin = value;
    }
    public Vector2 AnchorMax
    {
        get => _anchorMax;
        set => _anchorMax = value;
    }
    public Vector2 AnchoredPosition
    {
        get => _anchoredPosition;
        set => _anchoredPosition = value;
    }
    public Vector2 SizeDelta
    {
        get => _sizeDelta;
        set => _sizeDelta = value;
    }
    public Vector2 Pivot
    {
        get => _pivot;
        set => _pivot = value;
    }

    public Vector2 GetQuadSizeAdjustment(in RectTransform rectTransform)
    {
        Vector2 parentSize = rectTransform.rect.size;
        Vector2 anchorDiff = _anchorMax - _anchorMin;
        return Vector2.Scale(parentSize, anchorDiff - Vector2.one) + _sizeDelta;
    }

    public Vector2 GetQuadPositionAdjustment(in RectTransform rectTransform)
    {
        Vector2 parentSize = rectTransform.rect.size;
        Vector2 anchorMinPos = Vector2.Scale(_anchorMin, parentSize);
        Vector2 anchorMaxPos = Vector2.Scale(_anchorMax, parentSize);
        Vector2 anchorCenter = Vector2.Lerp(anchorMinPos, anchorMaxPos, 0.5f);
        Vector2 pivotPos = anchorCenter + _anchoredPosition;
        Vector2 initialCenter = parentSize * 0.5f;
        return pivotPos + (Vector2.one * 0.5f - _pivot) * _sizeDelta - initialCenter;
    }

    public bool highlightedFix = true;
    public ProceduralAnimationState[] proceduralAnimationStates = { new(), new(), new(), new(), new() };
    public ProceduralProperties DefaultProceduralProps => proceduralAnimationStates[0].proceduralProperties[0];

    [SerializeField] private bool _enabled = true;
    public bool Enabled
    {
        get => _enabled;
        set
        {
            _enabled = value;
            SetVerticesDirty();
        }
    }

    [SerializeField] private QuadModifiers _advancedQuadSettings;
    public QuadModifiers AdvancedQuadSettings
    {
        get => _advancedQuadSettings;
        set
        {
            _advancedQuadSettings = value;
            SetVerticesDirty();
        }
    }

    private bool colorPresetCallbackAssigned;
    [SerializeField] private ColorPreset _colorPreset;
    public ColorPreset ColorPreset
    {
        get => _colorPreset;
        set
        {
            if (_colorPreset && colorPresetCallbackAssigned)
            {
                _colorPreset.ColorChangeEvent -= SetVerticesDirty;
                colorPresetCallbackAssigned = false;
            }
            _colorPreset = value;
            if (_colorPreset)
            {
                _colorPreset.ColorChangeEvent += SetVerticesDirty;
                colorPresetCallbackAssigned = true;
            }
            SetVerticesDirty();
        }
    }
    
    [SerializeField] private ColorGridWrapMode _primaryColorWrapModeX = ColorGridWrapMode.Clamp;
    public ColorGridWrapMode PrimaryColorWrapModeX
    {
        get => _primaryColorWrapModeX;
        set
        {
            _primaryColorWrapModeX = value;
            SetVerticesDirty();
        }
    }
    
    [SerializeField] private ColorGridWrapMode _primaryColorWrapModeY = ColorGridWrapMode.Clamp;
    public ColorGridWrapMode PrimaryColorWrapModeY
    {
        get => _primaryColorWrapModeY;
        set
        {
            _primaryColorWrapModeY = value;
            SetVerticesDirty();
        }
    }

    [SerializeField] private ColorGridWrapMode _outlineColorWrapModeX = ColorGridWrapMode.Clamp;
    public ColorGridWrapMode OutlineColorWrapModeX
    {
        get => _outlineColorWrapModeX;
        set
        {
            _outlineColorWrapModeX = value;
            SetVerticesDirty();
        }
    }

    [SerializeField] private ColorGridWrapMode _outlineColorWrapModeY = ColorGridWrapMode.Clamp;
    public ColorGridWrapMode OutlineColorWrapModeY
    {
        get => _outlineColorWrapModeY;
        set
        {
            _outlineColorWrapModeY = value;
            SetVerticesDirty();
        }
    }

    [SerializeField] private ColorGridWrapMode _proceduralGradientColorWrapModeX = ColorGridWrapMode.Clamp;
    public ColorGridWrapMode ProceduralGradientColorWrapModeX
    {
        get => _proceduralGradientColorWrapModeX;
        set
        {
            _proceduralGradientColorWrapModeX = value;
            SetVerticesDirty();
        }
    }

    [SerializeField] private ColorGridWrapMode _proceduralGradientColorWrapModeY = ColorGridWrapMode.Clamp;
    public ColorGridWrapMode ProceduralGradientColorWrapModeY
    {
        get => _proceduralGradientColorWrapModeY;
        set
        {
            _proceduralGradientColorWrapModeY = value;
            SetVerticesDirty();
        }
    }

    [SerializeField] private ColorGridWrapMode _patternColorWrapModeX = ColorGridWrapMode.Clamp;
    public ColorGridWrapMode PatternColorWrapModeX
    {
        get => _patternColorWrapModeX;
        set
        {
            _patternColorWrapModeX = value;
            SetVerticesDirty();
        }
    }

    [SerializeField] private ColorGridWrapMode _patternColorWrapModeY = ColorGridWrapMode.Clamp;
    public ColorGridWrapMode PatternColorWrapModeY
    {
        get => _patternColorWrapModeY;
        set
        {
            _patternColorWrapModeY = value;
            SetVerticesDirty();
        }
    }

    public Vector4 UVRect
    {
        get => DefaultProceduralProps.uvRect;
        set
        {
            DefaultProceduralProps.uvRect = value;
            SetVerticesDirty();
        }
    }
    
    public byte PrimaryColorFade
    {
        get => DefaultProceduralProps.primaryColorFade;
        set
        {
            DefaultProceduralProps.primaryColorFade = value;
            SetVerticesDirty();
        }
    }

    public Vector2 RawSizeModifier
    {
        get => DefaultProceduralProps.sizeModifier;
        set
        {
            DefaultProceduralProps.sizeModifier = value;
            SetVerticesDirty();
            SetRayCastAreaDirty(FlexibleImage.AdvancedRaycastOptions.Size);
        }
    }

    public Vector2 GetSizeModifier(in RectTransform rectTransform, ProceduralProperties animationProps = null)
    {
        var (softness, sizeModifier) =  animationProps != null
            ? (animationProps.softness, animationProps.sizeModifier)
            : (DefaultProceduralProps.softness, DefaultProceduralProps.sizeModifier);

        sizeModifier += GetQuadSizeAdjustment(rectTransform);
        var softnessContribution = softness * SoftnessFeatherMode switch
        {
            FeatherMode.Inwards       => 0f,
            FeatherMode.Bidirectional => 1f,
            _                         => 2f
        };

        if (!SizeModifierAspectCorrection)
            return new Vector2(softnessContribution + sizeModifier.x, softnessContribution + sizeModifier.y);

        var (rectWidth, rectHeight) = (rectTransform.rect.width, rectTransform.rect.height);
        return rectWidth > rectHeight 
            ? new Vector2(softnessContribution + sizeModifier.x, softnessContribution + sizeModifier.y * (rectHeight / rectWidth)) 
            : new Vector2(softnessContribution + sizeModifier.x * (rectWidth / rectHeight), softnessContribution + sizeModifier.y);
    }

    [SerializeField] private bool _sizeModifierAspectCorrection;
    public bool SizeModifierAspectCorrection
    {
        get => _sizeModifierAspectCorrection;
        set
        {
            _sizeModifierAspectCorrection = value;
            SetVerticesDirty();
            SetRayCastAreaDirty(FlexibleImage.AdvancedRaycastOptions.Size);

        }
    }

    public Vector2 Offset
    {
        get => DefaultProceduralProps.offset;
        set
        {
            DefaultProceduralProps.offset = value;
            SetVerticesDirty();
            SetRayCastAreaDirty(FlexibleImage.AdvancedRaycastOptions.Offset);

        }
    }
    
    public float Rotation
    {
        get => DefaultProceduralProps.rotation;
        set
        {
            DefaultProceduralProps.rotation = value;
            SetVerticesDirty();
            if (!FitRotatedImageWithinBounds)
                SetRayCastAreaDirty(FlexibleImage.AdvancedRaycastOptions.Rotation);

        }
    }
    
    [SerializeField] private bool _fitRotatedImageWithinBounds;
    public bool FitRotatedImageWithinBounds
    {
        get => _fitRotatedImageWithinBounds;
        set
        {
            _fitRotatedImageWithinBounds = value;
            SetVerticesDirty();
            SetRayCastAreaDirty(FlexibleImage.AdvancedRaycastOptions.Rotation);

        }
    }

    [SerializeField] private bool[] _cutoutEnabled = new bool[4];
    public bool[] CutoutEnabled
    {
        get => _cutoutEnabled;
        set
        {
            _cutoutEnabled = value;
            SetVerticesDirty();
        }
    }
    
    public Vector4 Cutout
    {
        get => DefaultProceduralProps.cutout;
        set
        {
            DefaultProceduralProps.cutout = value;
            SetVerticesDirty();
        }
    }
    
    [SerializeField] private Vector2Int _primaryColorDimensions = Vector2Int.one;
    public Vector2Int PrimaryColorDimensions
    {
        get => _primaryColorDimensions;
        set
        {
            value.Clamp(Vector2Int.one, new Vector2Int(ProceduralProperties.Colors2dArrayDimensionSize, ProceduralProperties.Colors2dArrayDimensionSize));
            _primaryColorDimensions = value;
            SetVerticesDirty();
        }
    }

    [SerializeField] private Vector2Int _outlineColorDimensions = Vector2Int.one;
    public Vector2Int OutlineColorDimensions
    {
        get => _outlineColorDimensions;
        set
        {
            value.Clamp(Vector2Int.one, new Vector2Int(ProceduralProperties.Colors2dArrayDimensionSize, ProceduralProperties.Colors2dArrayDimensionSize));
            _outlineColorDimensions = value;
            SetVerticesDirty();
        }
    }

    [SerializeField] private Vector2Int _proceduralGradientColorDimensions = Vector2Int.one;
    public Vector2Int ProceduralGradientColorDimensions
    {
        get => _proceduralGradientColorDimensions;
        set
        {
            value.Clamp(Vector2Int.one, new Vector2Int(ProceduralProperties.Colors2dArrayDimensionSize, ProceduralProperties.Colors2dArrayDimensionSize));
            _proceduralGradientColorDimensions = value;
            SetVerticesDirty();
        }
    }

    [SerializeField] private Vector2Int _patternColorDimensions = Vector2Int.one;
    public Vector2Int PatternColorDimensions
    {
        get => _patternColorDimensions;
        set
        {
            value.Clamp(Vector2Int.one, new Vector2Int(ProceduralProperties.Colors2dArrayDimensionSize, ProceduralProperties.Colors2dArrayDimensionSize));
            _patternColorDimensions = value;
            SetVerticesDirty();
        }
    }

    public Color[] PrimaryColors
    {
        get
        {
            SetVerticesDirty();
            return DefaultProceduralProps.primaryColors;
        }
        set
        {
            DefaultProceduralProps.primaryColors = value;
            SetVerticesDirty();
        }
    }

    public Color[] OutlineColors
    {
        get
        {
            SetVerticesDirty();
            return DefaultProceduralProps.outlineColors;
        }
        set
        {
            DefaultProceduralProps.outlineColors = value;
            SetVerticesDirty();
        }
    }

    public Color[] ProceduralGradientColors
    {
        get
        {
            SetVerticesDirty();
            return DefaultProceduralProps.proceduralGradientColors;
        }
        set
        {
            DefaultProceduralProps.proceduralGradientColors = value;
            SetVerticesDirty();
        }
    }

    public Color[] PatternColors
    {
        get
        {
            SetVerticesDirty();
            return DefaultProceduralProps.patternColors;
        }
        set
        {
            if (value.Length != ProceduralProperties.Colors2dArrayDimensionSize * ProceduralProperties.Colors1dArrayLength)
                Array.Resize(ref value, ProceduralProperties.Colors1dArrayLength);

            DefaultProceduralProps.patternColors = value;
            SetVerticesDirty();
        }
    }

    public Color GetPrimaryColorAtCell(int x, int y) => DefaultProceduralProps.GetPrimaryColorAtCell(x, y);
    public Color GetOutlineColorAtCell(int x, int y) => DefaultProceduralProps.GetOutlineColorAtCell(x, y);
    public Color GetProceduralGradientColorAtCell(int x, int y) => DefaultProceduralProps.GetProceduralGradientColorAtCell(x, y);
    public Color GetPatternColorAtCell(int x, int y) => DefaultProceduralProps.GetProceduralGradientColorAtCell(x, y);
    public bool SetPrimaryColorAtCell(int x, int y, Color c)
    {
        SetVerticesDirty();
        return DefaultProceduralProps.SetPrimaryColorAtCell(x, y, c);
    }

    public bool SetOutlineColorAtCell(int x, int y, Color c)
    {
        SetVerticesDirty();
        return DefaultProceduralProps.SetOutlineColorAtCell(x, y, c);
    }

    public bool SetProceduralGradientColorAtCell(int x, int y, Color c)
    {
        SetVerticesDirty();
        return DefaultProceduralProps.SetProceduralGradientColorAtCell(x, y, c);
    }

    public bool SetPatternColorAtCell(int x, int y, Color c)
    {
        SetVerticesDirty();
        return DefaultProceduralProps.SetProceduralGradientColorAtCell(x, y, c);
    }

    public Vector2 PrimaryColorOffset
    {
        get => DefaultProceduralProps.primaryColorOffset;
        set
        {
            DefaultProceduralProps.primaryColorOffset = value;
            SetVerticesDirty();
        }
    }

    public Vector2 OutlineColorOffset
    {
        get => DefaultProceduralProps.outlineColorOffset;
        set
        {
            DefaultProceduralProps.outlineColorOffset = value;
            SetVerticesDirty();
        }
    }

    public Vector2 ProceduralGradientColorOffset
    {
        get => DefaultProceduralProps.proceduralGradientColorOffset;
        set
        {
            DefaultProceduralProps.proceduralGradientColorOffset = value;
            SetVerticesDirty();
        }
    }

    public Vector2 PatternColorOffset
    {
        get => DefaultProceduralProps.patternColorOffset;
        set
        {
            DefaultProceduralProps.patternColorOffset = value;
            SetVerticesDirty();
        }
    }

    public float PrimaryColorRotation
    {
        get => DefaultProceduralProps.primaryColorRotation;
        set
        {
            DefaultProceduralProps.primaryColorRotation = value;
            SetVerticesDirty();
        }
    }

    public float OutlineColorRotation
    {
        get => DefaultProceduralProps.outlineColorRotation;
        set
        {
            DefaultProceduralProps.outlineColorRotation = value;
            SetVerticesDirty();
        }
    }

    public float ProceduralGradientColorRotation
    {
        get => DefaultProceduralProps.proceduralGradientColorRotation;
        set
        {
            DefaultProceduralProps.proceduralGradientColorRotation = value;
            SetVerticesDirty();
        }
    }

    public float PatternColorRotation
    {
        get => DefaultProceduralProps.patternColorRotation;
        set
        {
            DefaultProceduralProps.patternColorRotation = value;
            SetVerticesDirty();
        }
    }

    public Vector2 PrimaryColorScale
    {
        get => DefaultProceduralProps.primaryColorScale;
        set
        {
            DefaultProceduralProps.primaryColorScale = value;
            SetVerticesDirty();
        }
    }

    public Vector2 OutlineColorScale
    {
        get => DefaultProceduralProps.outlineColorScale;
        set
        {
            DefaultProceduralProps.outlineColorScale = value;
            SetVerticesDirty();
        }
    }

    public Vector2 ProceduralGradientColorScale
    {
        get => DefaultProceduralProps.proceduralGradientColorScale;
        set
        {
            DefaultProceduralProps.proceduralGradientColorScale = value;
            SetVerticesDirty();
        }
    }

    public Vector2 PatternColorScale
    {
        get => DefaultProceduralProps.patternColorScale;
        set
        {
            DefaultProceduralProps.patternColorScale = value;
            SetVerticesDirty();
        }
    }

    [SerializeField] private bool _outlineFadeTowardsPerimeter;
    public bool OutlineFadeTowardsInterior
    {
        get => _outlineFadeTowardsPerimeter;
        set
        {
            _outlineFadeTowardsPerimeter = value;
            SetVerticesDirty();
        }
    }

    [SerializeField] private bool _outlineAdjustsChamfer;
    public bool OutlineAdjustsChamfer
    {
        get => _outlineAdjustsChamfer;
        set
        {
            _outlineAdjustsChamfer = value;
            SetVerticesDirty();
            SetRayCastAreaDirty(FlexibleImage.AdvancedRaycastOptions.ChamferAndCollapse);

        }
    }

    public float GetOutlineWithoutCollapsedEdgeAdjustment(ProceduralProperties animationProps = null) => (animationProps ?? DefaultProceduralProps).outlineWidth;
    public float GetOutlineWidth(in RectTransform rectTransform, ProceduralProperties animationProps = null)
    {
        var outlineWidth = GetOutlineWithoutCollapsedEdgeAdjustment(animationProps);
        if (!OutlineAccommodatesCollapsedEdge)
            return outlineWidth;

        var sizeMod = GetSizeModifier(rectTransform);
        var (width, height) = (rectTransform.rect.width + sizeMod.x, rectTransform.rect.height + sizeMod.y);
        var maxAspect = Mathf.Max(width, height) / Mathf.Min(width, height);
        return outlineWidth * 2.41421356237f * maxAspect;
    }

    public void SetOutlineWidth(float value)
    {
        DefaultProceduralProps.outlineWidth = value;
        SetVerticesDirty();
        if (OutlineExpandsOutward)
            SetRayCastAreaDirty(FlexibleImage.AdvancedRaycastOptions.IgnoreOutline);
    }

    [SerializeField] [Range(0, 1)] private float _primaryColorPresetMix = 1f;
    public float PrimaryColorPresetMix
    {
        get => ColorPreset != null ? _primaryColorPresetMix : 0f;
        set
        {
            _primaryColorPresetMix = value;
            SetVerticesDirty();
        }
    }

    [SerializeField] [Range(0, 1)] private float _outlineColorPresetMix = 1f;
    public float OutlineColorPresetMix
    {
        get => ColorPreset != null ? _outlineColorPresetMix : 0f;
        set
        {
            _outlineColorPresetMix = value;
            SetVerticesDirty();
        }
    }

    [SerializeField] [Range(0, 1)] private float _proceduralGradientColorPresetMix = 1f;
    public float ProceduralGradientColorPresetMix
    {
        get => ColorPreset != null ? _proceduralGradientColorPresetMix : 0f;
        set
        {
            _proceduralGradientColorPresetMix = value;
            SetVerticesDirty();
        }
    }

    [SerializeField] [Range(0, 1)] private float _patternColorPresetMix = 1f;
    public float PatternColorPresetMix
    {
        get => ColorPreset != null ? _patternColorPresetMix : 0f;
        set
        {
            _patternColorPresetMix = value;
            SetVerticesDirty();
        }
    }

    [SerializeField] private GradientType _proceduralGradientType;
    public GradientType ProceduralGradientType
    {
        get => _proceduralGradientType;
        set
        {
            _proceduralGradientType = value;
            SetVerticesDirty();
        }
    }

    [SerializeField] private bool _proceduralGradientAlphaIsBlend;
    public bool ProceduralGradientAlphaIsBlend
    {
        get => _proceduralGradientAlphaIsBlend;
        set
        {
            _proceduralGradientAlphaIsBlend = value;
            SetVerticesDirty();
        }
    }
    
    [SerializeField] private bool _patternColorAlphaIsBlend;
    public bool PatternColorAlphaIsBlend
    {
        get => _patternColorAlphaIsBlend;
        set
        {
            _patternColorAlphaIsBlend = value;
            SetVerticesDirty();
        }
    }
    
    [SerializeField] private bool _proceduralGradientAspectCorrection;
    public bool ProceduralGradientAspectCorrection
    {
        get => _proceduralGradientAspectCorrection;
        set
        {
            _proceduralGradientAspectCorrection = value;
            SetVerticesDirty();
        }
    }

    [SerializeField] private bool _proceduralGradientAffectsInterior = true;
    public bool ProceduralGradientAffectsInterior
    {
        get => _proceduralGradientAffectsInterior;
        set
        {
            _proceduralGradientAffectsInterior = value;
            SetVerticesDirty();
        }
    }

    [SerializeField] private bool _proceduralGradientAffectsOutline;
    public bool ProceduralGradientAffectsOutline
    {
        get => _proceduralGradientAffectsOutline;
        set
        {
            _proceduralGradientAffectsOutline = value;
            SetVerticesDirty();
        }
    }
    
    [SerializeField] private bool _patternAffectsInterior = true;
    public bool PatternAffectsInterior
    {
        get => _patternAffectsInterior;
        set
        {
            _patternAffectsInterior = value;
            SetVerticesDirty();
        }
    }

    [SerializeField] private bool _patternAffectsOutline;
    public bool PatternAffectsOutline
    {
        get => _patternAffectsOutline;
        set
        {
            _patternAffectsOutline = value;
            SetVerticesDirty();
        }
    }

    public Vector2 ProceduralGradientPosition
    {
        get => DefaultProceduralProps.proceduralGradientPosition;
        set
        {
            DefaultProceduralProps.proceduralGradientPosition = value;
            SetVerticesDirty();
        }
    }

    public Vector2 RadialGradientSize
    {
        get => DefaultProceduralProps.radialGradientSize;
        set
        {
            DefaultProceduralProps.radialGradientSize = value;
            SetVerticesDirty();
        }
    }
    
    public float RadialGradientStrength
    {
        get => DefaultProceduralProps.radialGradientStrength;
        set
        {
            DefaultProceduralProps.radialGradientStrength = value;
            SetVerticesDirty();
        }
    }

    public Vector2 AngleGradientStrength
    {
        get => DefaultProceduralProps.angleGradientStrength;
        set
        {
            DefaultProceduralProps.angleGradientStrength = value;
            SetVerticesDirty();
        }
    }

    public float AngleGradientAngle
    {
        get => DefaultProceduralProps.proceduralGradientAngle;
        set
        {
            DefaultProceduralProps.proceduralGradientAngle = value;
            SetVerticesDirty();
        }
    }

    public float SDFGradientInnerDistance
    {
        get => DefaultProceduralProps.sdfGradientInnerDistance;
        set
        {
            DefaultProceduralProps.sdfGradientInnerDistance = value;
            SetVerticesDirty();
        }
    }

    public float SDFGradientOuterDistance
    {
        get => DefaultProceduralProps.sdfGradientOuterDistance;
        set
        {
            DefaultProceduralProps.sdfGradientOuterDistance = value;
            SetVerticesDirty();
        }
    }
    
    public float SDFGradientInnerReach
    {
        get => DefaultProceduralProps.sdfGradientInnerReach;
        set
        {
            DefaultProceduralProps.sdfGradientInnerReach = value;
            SetVerticesDirty();
        }
    }

    public float SDFGradientOuterReach
    {
        get => DefaultProceduralProps.sdfGradientOuterReach;
        set
        {
            DefaultProceduralProps.sdfGradientOuterReach = value;
            SetVerticesDirty();
        }
    }

    public float ConicalGradientTailStrength
    {
        get => DefaultProceduralProps.conicalGradientTailStrength;
        set
        {
            DefaultProceduralProps.conicalGradientTailStrength = value;
            SetVerticesDirty();
        }
    }

    public float ConicalGradientCurvature
    {
        get => DefaultProceduralProps.conicalGradientCurvature;
        set
        {
            DefaultProceduralProps.conicalGradientCurvature = value;
            SetVerticesDirty();
        }
    }

    public uint NoiseSeed
    {
        get => DefaultProceduralProps.noiseSeed;
        set
        {
            DefaultProceduralProps.noiseSeed = value;
            SetVerticesDirty();
        }
    }

    public float NoiseScale
    {
        get => DefaultProceduralProps.noiseScale;
        set
        {
            DefaultProceduralProps.noiseScale = value;
            SetVerticesDirty();
        }
    }

    public float NoiseEdge
    {
        get => DefaultProceduralProps.noiseEdge;
        set
        {
            DefaultProceduralProps.noiseEdge = value;
            SetVerticesDirty();
        }
    }

    public float NoiseStrength
    {
        get => DefaultProceduralProps.noiseStrength;
        set
        {
            DefaultProceduralProps.noiseStrength = value;
            SetVerticesDirty();
        }
    }

    [SerializeField] bool _proceduralGradientPositionFromPointer;
    public bool ProceduralGradientPositionFromPointer
    {
        get => _proceduralGradientPositionFromPointer;
        set
        {
            _proceduralGradientPositionFromPointer = value;
            SetVerticesDirty();
        }
    }

    [SerializeField] private bool _noiseGradientAlternateMode;
    public bool NoiseGradientAlternateMode
    {
        get => _noiseGradientAlternateMode;
        set
        {
            _noiseGradientAlternateMode = value;
            SetVerticesDirty();
        }
    }

    [SerializeField] bool _screenSpaceProceduralGradient;
    public bool ScreenSpaceProceduralGradient
    {
        get => _screenSpaceProceduralGradient;
        set
        {
            _screenSpaceProceduralGradient = value;
            SetVerticesDirty();
        }
    }

    [SerializeField] bool _screenSpacePattern;
    public bool ScreenSpacePattern
    {
        get => _screenSpacePattern;
        set
        {
            _screenSpacePattern = value;
            SetVerticesDirty();
        }
    }

    [SerializeField] bool _softPattern;
    public bool SoftPattern
    {
        get => _softPattern;
        set
        {
            _softPattern = value;
            SetVerticesDirty();
        }
    }

    [SerializeField] SpritePatternRotation _spritePatternRotationMode;
    public SpritePatternRotation SpritePatternRotationMode
    {
        get => _spritePatternRotationMode;
        set
        {
            _spritePatternRotationMode = value;
            SetVerticesDirty();
        }
    }

    // Only does something when SpritePatternRotationMode is set to SpritePatternRotation.Offset!
    [SerializeField] SpritePatternOffsetDirection _spritePatternOffsetDirectionDegrees;
    public SpritePatternOffsetDirection SpritePatternOffsetDirectionDegrees
    {
        get => _spritePatternOffsetDirectionDegrees;
        set
        {
            _spritePatternOffsetDirectionDegrees = value;
            SetVerticesDirty();
        }
    }

    [SerializeField] PatternType _pattern = PatternType.Horizontal;
    public PatternType Pattern
    {
        get => _pattern;
        set
        {
            _pattern = value;
            SetVerticesDirty();
        }
    }

    [SerializeField] private PatternOriginPosition _patternOriginPos;
    public PatternOriginPosition PatternOriginPos
    {
        get => _patternOriginPos;
        set
        {
            _patternOriginPos = value;
            SetVerticesDirty();
        }
    }

    [SerializeField] private bool _scanlinePatternSpeedIsStaticOffset;
    public bool ScanlinePatternSpeedIsStaticOffset
    {
        get => _scanlinePatternSpeedIsStaticOffset;
        set
        {
            _scanlinePatternSpeedIsStaticOffset = value;
            SetVerticesDirty();
        }
    }

    public float PatternDensity
    {
        get => DefaultProceduralProps.patternDensity;
        set
        {
            DefaultProceduralProps.patternDensity = value;
            SetVerticesDirty();
        }
    }

    public float PatternSpeed
    {
        get => DefaultProceduralProps.patternSpeed;
        set
        {
            DefaultProceduralProps.patternSpeed = value;
            SetVerticesDirty();
        }
    }

    public float PatternCellParam
    {
        get => DefaultProceduralProps.patternCellParam;
        set
        {
            DefaultProceduralProps.patternCellParam = value;
            SetVerticesDirty();
        }
    }

    public byte PatternLineThickness
    {
        get => DefaultProceduralProps.patternLineThickness;
        set
        {
            DefaultProceduralProps.patternLineThickness = value;
            SetVerticesDirty();
        }
    }

    public int PatternSpriteRotation
    {
        get => DefaultProceduralProps.patternSpriteRotation;
        set
        {
            DefaultProceduralProps.patternSpriteRotation = value;
            SetVerticesDirty();
        }
    }

    public float Softness
    {
        get => DefaultProceduralProps.softness;
        set
        {
            DefaultProceduralProps.softness = value;
            SetVerticesDirty();
        }
    }
    
    [SerializeField] private FeatherMode _softnessFeatherMode = FeatherMode.Bidirectional;
    public FeatherMode SoftnessFeatherMode
    {
        get => _softnessFeatherMode;
        set
        {
            _softnessFeatherMode = value;
            SetVerticesDirty();
            SetRayCastAreaDirty(FlexibleImage.AdvancedRaycastOptions.Size);

        }
    }

    [SerializeField] private StrokeOriginLocation _strokeOrigin = StrokeOriginLocation.Center;
    public StrokeOriginLocation StrokeOrigin
    {
        get => _strokeOrigin;
        set
        {
            _strokeOrigin = value;
            SetVerticesDirty();
        }
    }

    public float Stroke
    {
        get => DefaultProceduralProps.stroke;
        set
        {
            DefaultProceduralProps.stroke = value;
            SetVerticesDirty();
        }
    }

    [SerializeField] private bool _normalizeChamfer = true;
    public bool NormalizeChamfer
    {
        get => _normalizeChamfer;
        set
        {
            _normalizeChamfer = value;
            SetVerticesDirty();
        }
    }

    public Vector4 CornerChamfer
    {
        get => DefaultProceduralProps.cornerChamfer;
        set
        {
            DefaultProceduralProps.cornerChamfer = value;
            SetVerticesDirty();
        }
    }
    
    [SerializeField] private bool _concavityIsSmoothing;
    public bool ConcavityIsSmoothing
    {
        get => _concavityIsSmoothing;
        set
        {
            _concavityIsSmoothing = value;
            SetVerticesDirty();
        }
    }

    public Vector4 CornerConcavity
    {
        get => DefaultProceduralProps.cornerConcavity;
        set
        {
            DefaultProceduralProps.cornerConcavity = value;
            SetVerticesDirty();
        }
    }

    public float CollapsedCornerChamfer
    {
        get => DefaultProceduralProps.collapsedCornerChamfer;
        set
        {
            DefaultProceduralProps.collapsedCornerChamfer = value;
            if (!CollapseIntoParallelogram && !MirrorCollapse && Mathf.Approximately(CollapseEdgeAmount, 1))
                SetVerticesDirty();
        }
    }

    public float CollapsedCornerConcavity
    {
        get => DefaultProceduralProps.collapsedCornerConcavity;
        set
        {
            DefaultProceduralProps.collapsedCornerConcavity = value;
            if (!CollapseIntoParallelogram && !MirrorCollapse && Mathf.Approximately(CollapseEdgeAmount, 1))
                SetVerticesDirty();
        }
    }
    
    [SerializeField] private CollapsedEdgeType _collapsedEdge = 0;
    public CollapsedEdgeType CollapsedEdge
    {
        get => _collapsedEdge;
        set
        {
            _collapsedEdge = value;
            SetVerticesDirty();
            SetRayCastAreaDirty(FlexibleImage.AdvancedRaycastOptions.ChamferAndCollapse);

        }
    }

    [SerializeField] private bool _collapseIntoParallelogram;
    public bool CollapseIntoParallelogram
    {
        get => _collapseIntoParallelogram;
        set
        {
            _collapseIntoParallelogram= value;
            SetVerticesDirty();
            SetRayCastAreaDirty(FlexibleImage.AdvancedRaycastOptions.ChamferAndCollapse);

        }
    }

    [SerializeField] private bool _mirrorCollapse;
    public bool MirrorCollapse
    {
        get => _mirrorCollapse;
        set
        {
            _mirrorCollapse = value;
            SetVerticesDirty();
            SetRayCastAreaDirty(FlexibleImage.AdvancedRaycastOptions.ChamferAndCollapse);

        }
    }

    public float CollapseEdgeAmount
    {
        get => DefaultProceduralProps.collapseEdgeAmount;
        set
        {
            DefaultProceduralProps.collapseEdgeAmount = value;
            SetVerticesDirty();
            SetRayCastAreaDirty(FlexibleImage.AdvancedRaycastOptions.ChamferAndCollapse);

        }
    }

    public float CollapseEdgeAmountAbsolute
    {
        get => DefaultProceduralProps.collapseEdgeAmountAbsolute;
        set
        {
            DefaultProceduralProps.collapseEdgeAmountAbsolute = value;
            SetVerticesDirty();
            SetRayCastAreaDirty(FlexibleImage.AdvancedRaycastOptions.ChamferAndCollapse);

        }
    }

    [SerializeField] private bool _edgeCollapseAmountIsAbsolute;

    public bool EdgeCollapseAmountIsAbsolute
    {
        get => _edgeCollapseAmountIsAbsolute;
        set
        {
            _edgeCollapseAmountIsAbsolute = value;
            SetVerticesDirty();
            SetRayCastAreaDirty(FlexibleImage.AdvancedRaycastOptions.ChamferAndCollapse);
        }
    }

    public float CollapseEdgePosition
    {
        get => DefaultProceduralProps.collapseEdgePosition;
        set
        {
            DefaultProceduralProps.collapseEdgePosition = value;
            SetVerticesDirty();
            SetRayCastAreaDirty(FlexibleImage.AdvancedRaycastOptions.ChamferAndCollapse);

        }
    }

    [SerializeField] private bool _proceduralGradientInvert;
    public bool ProceduralGradientInvert
    {
        get => _proceduralGradientInvert;
        set
        {
            _proceduralGradientInvert = value;
            SetVerticesDirty();
        }
    }

    [SerializeField] private bool _outlineAlphaIsBlend;
    public bool OutlineAlphaIsBlend
    {
        get => _outlineAlphaIsBlend;
        set
        {
            _outlineAlphaIsBlend = value;
            SetVerticesDirty();
        }
    }

    [SerializeField] private bool _addInteriorOutline;
    public bool AddInteriorOutline
    {
        get => _addInteriorOutline;
        set
        {
            _addInteriorOutline = value;
            SetVerticesDirty();
        }
    }

    [SerializeField] private bool _outlineExpandsOutward;
    public bool OutlineExpandsOutward
    {
        get => _outlineExpandsOutward;
        set
        {
            _outlineAlphaIsBlend = value;
            SetVerticesDirty();
            SetRayCastAreaDirty(FlexibleImage.AdvancedRaycastOptions.Size);
        }
    }
    
    [SerializeField] private bool _outlineAccommodatesCollapsedEdge;
    public bool OutlineAccommodatesCollapsedEdge
    {
        get => _outlineAccommodatesCollapsedEdge && OutlineExpandsOutward;
        set
        {
            _outlineAccommodatesCollapsedEdge = value;
            SetVerticesDirty();
            SetRayCastAreaDirty(FlexibleImage.AdvancedRaycastOptions.Size);
        }
    }

    [SerializeField] private bool _cutoutOnlyAffectsOutline;
    public bool CutoutOnlyAffectsOutline
    {
        get => _cutoutOnlyAffectsOutline;
        set
        {
            _cutoutOnlyAffectsOutline = value;
            SetVerticesDirty();
        }
    }

    [SerializeField] private bool _invertCutout;
    public bool InvertCutout
    {
        get => _invertCutout;
        set
        {
            _invertCutout = value;
            SetVerticesDirty();
        }
    }

    [SerializeField] private CutoutType _cutoutRule;
    public CutoutType CutoutRule
    {
        get => _cutoutRule;
        set
        {
            _cutoutRule = value;
            SetVerticesDirty();
        }
    }
    
    public bool UsingVertexColor => PrimaryColorDimensions * ProceduralGradientColorDimensions * OutlineColorDimensions * PatternColorDimensions != Vector2Int.one;

    [SerializeField] private int _meshSubdivisions = 2; // Mesh can be subdivided to tweak the appearance of vertex color gradients.
    public int MeshSubdivisions
    {
        get => UsingVertexColor ? _meshSubdivisions : 0;
        set
        {
            _meshSubdivisions = Mathf.Clamp(value, 0 , FlexibleImage.MaxMeshSubdivisions);
            SetVerticesDirty();
        }
    }

    [SerializeField] private Topology _meshTopology; // Flip the diagonal edges of the mesh, which also changes the appearance of vertex color gradients. 
    public Topology MeshTopology
    {
        get => UsingVertexColor ? _meshTopology : Topology.Original;
        set
        {
            _meshTopology = value;
            SetVerticesDirty();
        }
    }

    [NonSerialized] public QuadDataContainer container;
    private bool animationFinished;

    public void SetVerticesDirty() => container.MessageVerticesDirty();

    public void SetRayCastAreaDirty(FlexibleImage.AdvancedRaycastOptions flags = (FlexibleImage.AdvancedRaycastOptions)(-1)) => container.MessageRaycastAreaDirty(this, flags);

    public QuadData() => proceduralAnimationStates[0].PopulateIfEmptyAndGetFirstProps();

    public QuadData(QuadDataContainer container, string name = null)
    {
        this.container = container;
        proceduralAnimationStates[0].PopulateIfEmptyAndGetFirstProps();
        if (name != null)
            this.name = name;
    }

    public QuadData(QuadDataContainer container, QuadData other)
    {
        this.container = container;
        Copy(other);
    }

    ~QuadData()
    {
        if (ColorPreset != null)
            ColorPreset.ColorChangeEvent -= SetVerticesDirty;
    }

    public void Copy(QuadData other, bool setDirty = true)
    {
        for (int i = 0; i < proceduralAnimationStates.Length; i++)
        {
            proceduralAnimationStates[i] = new ProceduralAnimationState();
            for (int j = 0; j < other.proceduralAnimationStates[i].proceduralProperties.Count; j++)
            {
                var newProps = new ProceduralProperties(other.proceduralAnimationStates[i].proceduralProperties[j]);
                proceduralAnimationStates[i].proceduralProperties.Add(newProps);
            }
        }

        if (_colorPreset && colorPresetCallbackAssigned)
        {
            _colorPreset.ColorChangeEvent -= SetVerticesDirty;
            colorPresetCallbackAssigned = false;
        }

        _colorPreset = other._colorPreset;
        if (_colorPreset)
        {
            _colorPreset.ColorChangeEvent += SetVerticesDirty;
            colorPresetCallbackAssigned = true;
        }

        highlightedFix = other.highlightedFix;
        _concavityIsSmoothing = other._concavityIsSmoothing;
        _advancedQuadSettings = other._advancedQuadSettings;
        _anchorMin = other._anchorMin;
        _anchorMax = other._anchorMax;
        _anchoredPosition = other._anchoredPosition;
        _sizeDelta = other._sizeDelta;
        _pivot = other._pivot;
        _sizeModifierAspectCorrection = other._sizeModifierAspectCorrection;
        _fitRotatedImageWithinBounds = other._fitRotatedImageWithinBounds;
        _cutoutEnabled = other._cutoutEnabled;
        _primaryColorDimensions = other._primaryColorDimensions;
        _outlineColorDimensions = other._outlineColorDimensions;
        _proceduralGradientColorDimensions = other.ProceduralGradientColorDimensions;
        _patternColorDimensions = other._patternColorDimensions;
        _outlineFadeTowardsPerimeter = other._outlineFadeTowardsPerimeter;
        _outlineAdjustsChamfer = other._outlineAdjustsChamfer;
        _primaryColorWrapModeX = other._primaryColorWrapModeX;
        _primaryColorWrapModeY = other._primaryColorWrapModeY;
        _outlineColorWrapModeX = other._outlineColorWrapModeX;
        _outlineColorWrapModeY = other._outlineColorWrapModeY;
        _proceduralGradientColorWrapModeX = other._proceduralGradientColorWrapModeX;
        _proceduralGradientColorWrapModeY = other._proceduralGradientColorWrapModeY;
        _patternColorWrapModeX = other._patternColorWrapModeX;
        _patternColorWrapModeY = other._patternColorWrapModeY;
        _primaryColorPresetMix = other._primaryColorPresetMix;
        _outlineColorPresetMix = other._outlineColorPresetMix;
        _proceduralGradientColorPresetMix = other._proceduralGradientColorPresetMix;
        _patternColorPresetMix = other._patternColorPresetMix;
        _proceduralGradientType = other._proceduralGradientType;
        _proceduralGradientAlphaIsBlend = other._proceduralGradientAlphaIsBlend;
        _patternColorAlphaIsBlend = other._patternColorAlphaIsBlend;
        _proceduralGradientAspectCorrection = other._proceduralGradientAspectCorrection;
        _proceduralGradientAffectsInterior = other._proceduralGradientAffectsInterior;
        _proceduralGradientAffectsOutline = other._proceduralGradientAffectsOutline;
        _patternAffectsInterior = other._patternAffectsInterior;
        _patternAffectsOutline = other._patternAffectsOutline;
        _proceduralGradientPositionFromPointer = other._proceduralGradientPositionFromPointer;
        _screenSpaceProceduralGradient = other._screenSpaceProceduralGradient;
        _screenSpacePattern = other._screenSpacePattern;
        _softPattern = other._softPattern;
        _spritePatternRotationMode = other._spritePatternRotationMode;
        _spritePatternOffsetDirectionDegrees = other.SpritePatternOffsetDirectionDegrees;
        _pattern = other._pattern;
        _scanlinePatternSpeedIsStaticOffset = other._scanlinePatternSpeedIsStaticOffset;
        _patternOriginPos = other._patternOriginPos;
        _softnessFeatherMode = other._softnessFeatherMode;
        _strokeOrigin = other._strokeOrigin;
        _normalizeChamfer = other._normalizeChamfer;
        _collapsedEdge = other._collapsedEdge;
        _collapseIntoParallelogram = other._collapseIntoParallelogram;
        _mirrorCollapse = other._mirrorCollapse;
        _edgeCollapseAmountIsAbsolute = other._edgeCollapseAmountIsAbsolute;
        _proceduralGradientInvert = other._proceduralGradientInvert;
        _noiseGradientAlternateMode = other._noiseGradientAlternateMode;
        _outlineAlphaIsBlend = other._outlineAlphaIsBlend;
        _addInteriorOutline = other._addInteriorOutline;
        _outlineExpandsOutward = other._outlineExpandsOutward;
        _outlineAccommodatesCollapsedEdge = other._outlineAccommodatesCollapsedEdge;
        _cutoutOnlyAffectsOutline = other._cutoutOnlyAffectsOutline;
        _invertCutout = other._invertCutout;
        _cutoutRule = other._cutoutRule;
        _meshSubdivisions = other._meshSubdivisions;
        _meshTopology = other._meshTopology;

        if (setDirty)
        {
            SetVerticesDirty();
            SetRayCastAreaDirty();
        }
    }

    public Vector4 GetAdjustedChamfer(in RectTransform rectTransform, ProceduralProperties animationProps = null)
    {
        var (chamfer, collapseEdgeAmountRelative, collapseEdgeAmountAbsolute, collapsedCornerChamfer, collapseEdgePosition) = animationProps != null 
            ? (animationProps.cornerChamfer,         animationProps.collapseEdgeAmount,         animationProps.collapseEdgeAmountAbsolute,         animationProps.collapsedCornerChamfer,         animationProps.collapseEdgePosition) 
            : (DefaultProceduralProps.cornerChamfer, DefaultProceduralProps.collapseEdgeAmount, DefaultProceduralProps.collapseEdgeAmountAbsolute, DefaultProceduralProps.collapsedCornerChamfer, DefaultProceduralProps.collapseEdgePosition);

        float collapseEdgeAmount;
        if (EdgeCollapseAmountIsAbsolute)
        {
            var size = GetSizeModifier(rectTransform);
            size += rectTransform.rect.size;
            var dimensionalSize = CollapsedEdge <= CollapsedEdgeType.Bottom ? size.x : size.y;
            collapseEdgeAmount = Mathf.Min(1f, collapseEdgeAmountAbsolute / Mathf.Max(dimensionalSize, 0.01f));
        }
        else
        {
            collapseEdgeAmount = collapseEdgeAmountRelative;
        }
        
        if (!CollapseIntoParallelogram && !MirrorCollapse && Mathf.Approximately(collapseEdgeAmount, 1))
        {
            if ((int)CollapsedEdge == 0)
            {
                chamfer.x = collapsedCornerChamfer;
                chamfer.y = 0;
            }
            else if ((int)CollapsedEdge == 1)
            {
                chamfer.w = collapsedCornerChamfer;
                chamfer.z = 0;
            }
            else if ((int)CollapsedEdge == 2)
            {
                chamfer.x = collapsedCornerChamfer;
                chamfer.z = 0;
            }
            else
            {
                chamfer.w = collapsedCornerChamfer;
                chamfer.y = 0;
            }
        }

        // When normalized, no corner can affect more than half the overall image.
        if (NormalizeChamfer && !MirrorCollapse)
        {
            var sizeMod = GetSizeModifier(rectTransform);
            var (rectWidth, rectHeight) = (rectTransform.rect.width, rectTransform.rect.height);
            var (width, height) = (rectWidth + sizeMod.x, rectHeight + sizeMod.y);

            // Compute effective edge lengths accounting for collapsed edges
            var bl = new Vector2(0, 0);
            var br = new Vector2(width, 0);
            var tr = new Vector2(width, height);
            var tl = new Vector2(0, height);
            var collapseEdgeInt = (int)CollapsedEdge;
            if (collapseEdgeInt == 0) // Top
            {
                var innerCollapsePoint = (tr.x - tl.x) * collapseEdgePosition + tl.x;
                tl.x += (innerCollapsePoint - tl.x) * collapseEdgeAmount;
                tr.x += (innerCollapsePoint - tr.x) * collapseEdgeAmount;
                if (CollapseIntoParallelogram)
                {
                    var innerOppositeCollapsePoint = width - innerCollapsePoint;
                    bl.x += (innerOppositeCollapsePoint - bl.x) * collapseEdgeAmount;
                    br.x += (innerOppositeCollapsePoint - br.x) * collapseEdgeAmount;
                }
            }
            else if (collapseEdgeInt == 1) // Bottom
            {
                var innerCollapsePoint = (br.x - bl.x) * collapseEdgePosition + bl.x;
                bl.x += (innerCollapsePoint - bl.x) * collapseEdgeAmount;
                br.x += (innerCollapsePoint - br.x) * collapseEdgeAmount;
                if (CollapseIntoParallelogram)
                {
                    var innerOppositeCollapsePoint = width - innerCollapsePoint;
                    tl.x += (innerOppositeCollapsePoint - tl.x) * collapseEdgeAmount;
                    tr.x += (innerOppositeCollapsePoint - tr.x) * collapseEdgeAmount;
                }
            }
            else if (collapseEdgeInt == 2) // Left
            {
                var innerCollapsePoint = (tl.y - bl.y) * collapseEdgePosition + bl.y;
                bl.y += (innerCollapsePoint - bl.y) * collapseEdgeAmount;
                tl.y += (innerCollapsePoint - tl.y) * collapseEdgeAmount;
                if (CollapseIntoParallelogram)
                {
                    var innerOppositeCollapsePoint = height - innerCollapsePoint;
                    br.y += (innerOppositeCollapsePoint - br.y) * collapseEdgeAmount;
                    tr.y += (innerOppositeCollapsePoint - tr.y) * collapseEdgeAmount;
                }
            }
            else // Right
            {
                var innerCollapsePoint = (tr.y - br.y) * collapseEdgePosition + br.y;
                br.y += (innerCollapsePoint - br.y) * collapseEdgeAmount;
                tr.y += (innerCollapsePoint - tr.y) * collapseEdgeAmount;
                if (CollapseIntoParallelogram)
                {
                    var innerOppositeCollapsePoint = height - innerCollapsePoint;
                    bl.y += (innerOppositeCollapsePoint - bl.y) * collapseEdgeAmount;
                    tl.y += (innerOppositeCollapsePoint - tl.y) * collapseEdgeAmount;
                }
            }
            var effectiveTop = Vector2.Distance(tl, tr);
            var effectiveBottom = Vector2.Distance(bl, br);
            var effectiveLeft = Vector2.Distance(bl, tl);
            var effectiveRight = Vector2.Distance(br, tr);

            var shrink = Vector4.one;
            var totalChaferTop = chamfer.x + (effectiveRight == 0 ? chamfer.w : chamfer.y);
            if (totalChaferTop > 0 && effectiveTop > 0)
            {
                var shrinkFactor = effectiveTop / totalChaferTop;
                shrink.x = Mathf.Min(shrink.x, shrinkFactor);
                if (effectiveRight == 0)
                    shrink.w = Mathf.Min(shrink.w, shrinkFactor);
                else
                    shrink.y = Mathf.Min(shrink.y, shrinkFactor);
            }
            var totalChamferBottom = chamfer.w + (effectiveLeft == 0 ? chamfer.x : chamfer.z);
            if (totalChamferBottom > 0 && effectiveBottom > 0)
            {
                var shrinkFactor = effectiveBottom / totalChamferBottom;
                shrink.w = Mathf.Min(shrink.w, shrinkFactor);
                if (effectiveLeft == 0)
                    shrink.x = Mathf.Min(shrink.x, shrinkFactor);
                else
                    shrink.z = Mathf.Min(shrink.z, shrinkFactor);
            }
            var totalChamferLeft = chamfer.x + (effectiveBottom == 0 ? chamfer.w : chamfer.z);
            if (totalChamferLeft > 0 && effectiveLeft > 0)
            {
                var shrinkFactor = effectiveLeft / totalChamferLeft;
                shrink.x = Mathf.Min(shrink.x, shrinkFactor);
                if (effectiveBottom == 0)
                    shrink.w = Mathf.Min(shrink.w, shrinkFactor);
                else
                    shrink.z = Mathf.Min(shrink.z, shrinkFactor);
            }
            var totalChamferRight = chamfer.w + (effectiveTop == 0 ? chamfer.x : chamfer.y);
            if (totalChamferRight > 0 && effectiveRight > 0)
            {
                var shrinkFactor = effectiveRight / totalChamferRight;
                shrink.w = Mathf.Min(shrink.w, shrinkFactor);
                if (effectiveTop == 0)
                    shrink.x = Mathf.Min(shrink.x, shrinkFactor);
                else
                    shrink.y = Mathf.Min(shrink.y, shrinkFactor);
            }
            chamfer = Vector4.Scale(chamfer, shrink);
        }

        var adjustedConcavity = GetAdjustedConcavity();
        if (chamfer.magnitude > 0 && adjustedConcavity.magnitude > 0)
        {
            if (ConcavityIsSmoothing)
            {
                var circleMid = 1f - Mathf.Sqrt(0.5f);
                var scales = Vector4.one;
                if (adjustedConcavity.x > 0f && chamfer.x > 0f)
                {
                    var p = Mathf.Lerp(2f, 10f, adjustedConcavity.x);
                    var mid = 1f - Mathf.Pow(2f, -1f / p);
                    scales.x = circleMid / mid;
                }
                if (adjustedConcavity.y > 0f && chamfer.y > 0f)
                {
                    var p = Mathf.Lerp(2f, 10f, adjustedConcavity.y);
                    var mid = 1f - Mathf.Pow(2f, -1f / p);
                    scales.y = circleMid / mid;
                }
                if (adjustedConcavity.z > 0f && chamfer.z > 0f)
                {
                    var p = Mathf.Lerp(2f, 10f, adjustedConcavity.z);
                    var mid = 1f - Mathf.Pow(2f, -1f / p);
                    scales.z = circleMid / mid;
                }
                if (adjustedConcavity.w > 0f && chamfer.w > 0f)
                {
                    var p = Mathf.Lerp(2f, 10f, adjustedConcavity.w);
                    var mid = 1f - Mathf.Pow(2f, -1f / p);
                    scales.w = circleMid / mid;
                }
                chamfer = Vector4.Scale(chamfer, scales);
            }
            else
            {
                // Higher concavity requires more chamfer in that if we imagine each corner as an elastic band held between two points, then without adjustments, those points come closer together as concavity increases.
                // I haven't been able to work out the exact relationship, but we can get very close via curve-fitting.
                var scaledConcavity = adjustedConcavity;
                scaledConcavity.Scale(adjustedConcavity);
                var cc2 = scaledConcavity;
                scaledConcavity.Scale(adjustedConcavity);
                var cc3 = scaledConcavity;
                scaledConcavity.Scale(adjustedConcavity);
                var cc4 = scaledConcavity;
                chamfer.Scale(Vector4.one + 1.708333f * adjustedConcavity - 1.166667f * cc2 + 0.5416667f * cc3 - 0.08333333f * cc4);
            }
        }

        if (OutlineAdjustsChamfer)
        {
            if (OutlineExpandsOutward)
                chamfer += Vector4.one * GetOutlineWithoutCollapsedEdgeAdjustment(animationProps);
            else
                chamfer = Vector4.Max(Vector4.one * GetOutlineWithoutCollapsedEdgeAdjustment(animationProps), chamfer);
        }

        // Between the curve-fit and the OutlineAdjustsChamfer, it's possible to get a chamfer overflows the range of the packed vertex attribute. No one is likely to use values large enough to notice clamping.
        return Vector4.Min(Vector4.one * 4095.9375f, chamfer);
    }

    public Vector4 GetAdjustedConcavity(ProceduralProperties animationProps = null)
    {
        var collapseEdgeAmount = animationProps?.collapseEdgeAmount ?? DefaultProceduralProps.collapseEdgeAmount;
        if (CollapseIntoParallelogram || MirrorCollapse || collapseEdgeAmount < 1)
            return animationProps?.cornerConcavity ?? DefaultProceduralProps.cornerConcavity;

        var (concavity, collapsedCornerConcavity) = animationProps != null 
            ? (animationProps.cornerConcavity,         animationProps.collapsedCornerConcavity) 
            : (DefaultProceduralProps.cornerConcavity, DefaultProceduralProps.collapsedCornerConcavity);

        if ((int)CollapsedEdge == 0)
        {
            concavity.x = collapsedCornerConcavity;
            concavity.y = 0;
        }
        else if ((int)CollapsedEdge == 1)
        {
            concavity.w = collapsedCornerConcavity;
            concavity.z = 0;
        }
        else if ((int)CollapsedEdge == 2)
        {
            concavity.x = collapsedCornerConcavity;
            concavity.z = 0;
        }
        else
        {
            concavity.w = collapsedCornerConcavity;
            concavity.y = 0;
        }
        return concavity;
    }

    public float GetStroke01(in RectTransform rectTransform, ProceduralProperties animationProps = null)
    {
        var sizeMod = GetSizeModifier(rectTransform);
        var (width, height) = (rectTransform.rect.width + sizeMod.x, rectTransform.rect.height + sizeMod.y);
        if (OutlineExpandsOutward)
        {
            var outlineWidth = 2 * GetOutlineWithoutCollapsedEdgeAdjustment();
            width += outlineWidth;
            height += outlineWidth;
        }

        var minSide = Mathf.Min(width, height) * 0.5f;
        var stroke01 = animationProps?.stroke ?? DefaultProceduralProps.stroke;

        if (StrokeOrigin == StrokeOriginLocation.Outline)
            stroke01 += GetOutlineWithoutCollapsedEdgeAdjustment();

        stroke01 /= minSide;
        stroke01 = Mathf.Clamp01(stroke01);
        if (StrokeOrigin == StrokeOriginLocation.Center)
            stroke01 = 1f - stroke01;

        // Don't adjust for softness if stroke == 1 (no stroke)
        if (Mathf.Approximately(stroke01, 1f))
            return 1;

        var scaledSoftness = Softness / minSide;
        if (SoftnessFeatherMode == FeatherMode.Inwards)
            stroke01 += scaledSoftness;
        else if (SoftnessFeatherMode == FeatherMode.Bidirectional)
            stroke01 += scaledSoftness * 0.5f;

        // Max: 1 - 1/4095, since the stroke disappears entirely at 1, creating a discontinuity when softness adjustment gets high enough.
        stroke01 = Mathf.Min(stroke01, 0.99975579975f);
        return stroke01;
    }

    public bool SetAnimationValues(AnimationValues animationValues, int currentSelectionStateIdx, bool pointerInside, bool pointerDown)
    {
        if (highlightedFix && pointerInside && !pointerDown && currentSelectionStateIdx < 4)
            currentSelectionStateIdx = 1;

        if (animationFinished && currentSelectionStateIdx == animationValues.lastReachedStateIdx)
            return false;

        animationFinished = false;
        var dirtiedVertices = false;
        if (proceduralAnimationStates[currentSelectionStateIdx].proceduralProperties.Count == 0)
            currentSelectionStateIdx = 0;

        if (animationValues.lastSelectionStateIdx != currentSelectionStateIdx)
        {
            animationValues.lastReachedStateIdx = animationValues.lastSelectionStateIdx;
            animationValues.lastSelectionStateIdx = currentSelectionStateIdx;
    
            if (animationValues.lastReachedStateIdx >= 0)
                animationValues.checkUnwind = true;
            else
                animationValues.Reset();
        }

        if (animationValues.checkUnwind)
        {
            if (proceduralAnimationStates[animationValues.lastReachedStateIdx].Unwind(animationValues))
            {
                dirtiedVertices = true;
            }
            else
            {
                animationValues.checkUnwind = false;
                animationValues.Reset();
            }
        }
        else
        {
            dirtiedVertices = true;
            animationFinished = proceduralAnimationStates[currentSelectionStateIdx].ComputeProperties(animationValues);
            if (animationFinished)
            {
                animationValues.SetCurrentProps(proceduralAnimationStates[currentSelectionStateIdx].proceduralProperties[^1], false);
                animationValues.lastReachedStateIdx = currentSelectionStateIdx;
            }
        }

        return dirtiedVertices;
    }

    // Helper method which uses the cutout region to create a "fill" effect.
    // Will generally *not* work well with an expanded outline when Massage Collapse is enabled.
    // Cutouts have a maximum size, so cutout fills will not work if the rect size (+ expanded outline) exceeds it.
    public void CutoutFill(RectTransform rectTransform, CutoutFillOrigin origin, float percent)
    {
        if (percent >= 1)
        {
            CutoutEnabled[0] = CutoutEnabled[1] = CutoutEnabled[2] = CutoutEnabled[3] = false;
            InvertCutout = false;
            return;
        }

        CutoutRule = CutoutType.OR;

        var totalSize = rectTransform.rect.size;
        if (OutlineExpandsOutward)
            totalSize += new Vector2(2, 2) * GetOutlineWidth(rectTransform, DefaultProceduralProps);

        if (origin <= CutoutFillOrigin.Right)
        {
            if (totalSize.x > 1023.5f)
            {
                Debug.LogWarning($"{rectTransform.name} is too wide for horizontal CutoutFill. Maximum width is 1023.5 canvas units.");
                return;
            }
            var absoluteCutoutSize = totalSize.x * percent;
            InvertCutout = false;
            CutoutEnabled[0] = CutoutEnabled[1] = true;
            CutoutEnabled[2] = CutoutEnabled[3] = false;
            Cutout = origin == CutoutFillOrigin.Left 
                ? new Vector4(absoluteCutoutSize, 0, 0, 0)
                : new Vector4(0, absoluteCutoutSize, 0, 0);
        }
        else if (origin <= CutoutFillOrigin.Bottom)
        {
            if (totalSize.y > 1023.5f)
            {
                Debug.LogWarning($"{rectTransform.name} is too tall for vertical CutoutFill. Maximum height is 1023.5 canvas units.");
                return;
            }
            var absoluteCutoutSize = totalSize.y * percent;
            InvertCutout = false;
            CutoutEnabled[0] = CutoutEnabled[1] = false;
            CutoutEnabled[2] = CutoutEnabled[3] = true;
            Cutout = origin == CutoutFillOrigin.Top
                ? new Vector4(0, 0, absoluteCutoutSize, 0)
                : new Vector4(0, 0, 0, absoluteCutoutSize);
        }
        else if (origin <= CutoutFillOrigin.HorizontalFromPerimeter)
        {
            if (totalSize.x > 2047f)
            {
                Debug.LogWarning($"{rectTransform.name} is too wide for mirrored horizontal CutoutFill. Maximum width is 2047 canvas units.");
                return;
            }
            var fromPerimeter = percent > 0 && origin == CutoutFillOrigin.HorizontalFromCenter;
            InvertCutout = fromPerimeter;
            percent = fromPerimeter ? 1 - percent : percent;
            var absoluteCutoutSize = totalSize.x * percent * 0.5f;
            CutoutEnabled[0] = CutoutEnabled[1] = true;
            CutoutEnabled[2] = CutoutEnabled[3] = false;
            Cutout = new Vector4(absoluteCutoutSize, absoluteCutoutSize, 0, 0);
        }
        else if (origin <= CutoutFillOrigin.VerticalFromPerimeter)
        {
            if (totalSize.y > 2047f)
            {
                Debug.LogWarning($"{rectTransform.name} is too tall for mirrored vertical CutoutFill. Maximum height is 2047 canvas units.");
                return;
            }
            var fromPerimeter = percent > 0 && origin == CutoutFillOrigin.VerticalFromCenter;
            InvertCutout = fromPerimeter;
            percent = fromPerimeter ? 1 - percent : percent;
            var absoluteCutoutSize = totalSize.y * percent * 0.5f;
            CutoutEnabled[0] = CutoutEnabled[1] = false;
            CutoutEnabled[2] = CutoutEnabled[3] = true;
            Cutout = new Vector4(0, 0, absoluteCutoutSize, absoluteCutoutSize);
        }
        else if (origin <= CutoutFillOrigin.BothFromPerimeterCross)
        {
            if (totalSize.x > 2047f || totalSize.y > 2047f)
            {
                Debug.LogWarning($"{rectTransform.name} is too large for horizontal + vertical CutoutFill. Maximum size is 2047x2047 canvas units.");
                return;
            }
            CutoutRule = origin <= CutoutFillOrigin.BothFromPerimeter ? QuadData.CutoutType.AND : QuadData.CutoutType.OR;
            var fromPerimeter = percent > 0 && origin is CutoutFillOrigin.BothFromCenter or CutoutFillOrigin.BothFromCenterCross;
            InvertCutout = fromPerimeter;
            percent = fromPerimeter ? 1 - percent : percent;
            var absoluteCutoutSizeH = totalSize.x * percent * 0.5f;
            var absoluteCutoutSizeV = totalSize.y * percent * 0.5f;
            CutoutEnabled[0] = CutoutEnabled[1] = true;
            CutoutEnabled[2] = CutoutEnabled[3] = true;
            Cutout = new Vector4(absoluteCutoutSizeH, absoluteCutoutSizeH, absoluteCutoutSizeV, absoluteCutoutSizeV);
        }
        else
        {
            if (totalSize.x > 1023.5f || totalSize.y > 1023.5f)
            {
                Debug.LogWarning($"{rectTransform.name} is too large for corner CutoutFill. Maximum size is 1023.5 x 1023.5 canvas units.");
                return;
            }

            CutoutRule = CutoutType.AND;
            InvertCutout = false;

            var absoluteCutoutSizeH = totalSize.x * percent;
            var absoluteCutoutSizeV = totalSize.y * percent;

            CutoutEnabled[0] = CutoutEnabled[1] = CutoutEnabled[2] = CutoutEnabled[3] = true;
            switch (origin)
            {
                case CutoutFillOrigin.TopLeft:
                    Cutout = new Vector4(absoluteCutoutSizeH, 0, absoluteCutoutSizeV, 0);
                    break;
                case CutoutFillOrigin.TopRight:
                    Cutout = new Vector4(0, absoluteCutoutSizeH, absoluteCutoutSizeV, 0);
                    break;
                case CutoutFillOrigin.BottomLeft:
                    Cutout = new Vector4(absoluteCutoutSizeH, 0, 0, absoluteCutoutSizeV);
                    break;
                case CutoutFillOrigin.BottomRight:
                    Cutout = new Vector4(0, absoluteCutoutSizeH, 0, absoluteCutoutSizeV);
                    break;
            }
        }
    }

    public void OnBeforeSerialize() {}
    public void OnAfterDeserialize()
    {
        if (ColorPreset != null)
            ColorPreset.ColorChangeEvent += SetVerticesDirty;
    }
}
}
