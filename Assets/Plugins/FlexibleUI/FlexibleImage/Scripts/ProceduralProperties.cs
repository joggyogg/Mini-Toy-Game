using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace JeffGrawAssets.FlexibleUI
{
[Serializable]
public class ProceduralProperties
{
    public const int Colors2dArrayDimensionSize = 3;
    public const int Colors1dArrayLength = Colors2dArrayDimensionSize * Colors2dArrayDimensionSize;

    public enum InterpolationType
    {
        Linear,
        QuadraticEaseIn,
        QuadraticEaseOut,
        QuadraticEaseInOut,
        SineEaseIn,
        SineEaseOut,
        SineEaseInOut,
        CircularEaseIn,
        CircularEaseOut,
        CircularEaseInOut,
        QuinticEaseIn,
        QuinticEaseOut,
        QuinticEaseInOut
    }

    public InterpolationType interpolationType = InterpolationType.Linear;
    public float duration = 0.1f;

    public Vector4 uvRect = new (0,0, 1, 1);
    public Vector2 offset;
    public float stroke;
    public Vector2 sizeModifier;
    public float softness = 1f;
    public float rotation;
    public Vector4 cutout;
    public Vector4 cornerChamfer;
    public Vector4 cornerConcavity;
    public float collapsedCornerChamfer;
    public float collapsedCornerConcavity;
    [Range(0, 1)]
    public float collapseEdgeAmount;
    [Min(0)]
    public float collapseEdgeAmountAbsolute;
    [Range(0, 1)]
    public float collapseEdgePosition;

    public Color[] primaryColors = new Color[Colors1dArrayLength];
    public Color[] outlineColors = new Color[Colors1dArrayLength];
    public Color[] proceduralGradientColors = new Color[Colors1dArrayLength];
    public Color[] patternColors = new Color[Colors1dArrayLength];
    public Vector2 primaryColorOffset;
    public Vector2 outlineColorOffset;
    public Vector2 proceduralGradientColorOffset;
    public Vector2 patternColorOffset;
    public float primaryColorRotation;
    public float outlineColorRotation;
    public float proceduralGradientColorRotation;
    public float patternColorRotation;
    public Vector2 primaryColorScale = Vector2.one;
    public Vector2 outlineColorScale = Vector2.one;
    public Vector2 proceduralGradientColorScale = Vector2.one;
    public Vector2 patternColorScale = Vector2.one;

    [Range(0, 255)]
    public byte primaryColorFade = 255;
    [Range(0, 511.875f)]
    public float outlineWidth;
    public Vector2 proceduralGradientPosition = new (0.5f, 0.5f);
    public Vector2 radialGradientSize = new (0.5f, 0.5f);
    [Range(0, 1)]
    public float radialGradientStrength = 0.5f;
    public Vector2 angleGradientStrength = new (0.5f, 0.5f);
    [FormerlySerializedAs("angleGradientAngle")]
    public float proceduralGradientAngle;
    public float sdfGradientInnerDistance;
    public float sdfGradientOuterDistance;
    [Range(0, 1)]
    public float sdfGradientInnerReach;
    [Range(0, 1)]
    public float sdfGradientOuterReach;
    [FormerlySerializedAs("sdfGradientPointerStrength")] [Range(0,1)]
    public float proceduralGradientPointerStrength = 0.5f;
    [Range(-1, 1)]
    public float conicalGradientCurvature;
    [Range(0, 1)]
    public float conicalGradientTailStrength = 0.5f;
    [Range(0, 32767)]
    public uint noiseSeed;
    [Range(0,1)]
    public float noiseScale = 0.5f;
    [Range(0,1)]
    public float noiseEdge = 0.5f;
    [Range(0,1)]
    public float noiseStrength = 0.5f;
    [Range(0, 1)]
    public float patternDensity;
    [Range(-1,1)]
    public float patternSpeed;
    [Range(0,1)]
    public float patternCellParam = 0.5f;
    [Range(0, 255)]
    public byte patternLineThickness = 127;
    public int patternSpriteRotation;

    public ProceduralProperties () {}
    public ProceduralProperties (ProceduralProperties other) => Copy(other);

    public void Copy(ProceduralProperties other)
    {
        interpolationType = other.interpolationType;
        duration = other.duration;
        uvRect = other.uvRect;
        offset = other.offset;
        stroke = other.stroke;
        softness = other.softness;
        rotation = other.rotation;
        cutout = other.cutout;
        cornerChamfer = other.cornerChamfer;
        cornerConcavity = other.cornerConcavity;
        collapsedCornerChamfer = other.collapsedCornerChamfer;
        collapsedCornerConcavity = other.collapsedCornerConcavity;
        collapseEdgeAmount = other.collapseEdgeAmount;
        collapseEdgeAmountAbsolute = other.collapseEdgeAmountAbsolute;
        collapseEdgePosition = other.collapseEdgePosition;
        sizeModifier = other.sizeModifier;
        outlineWidth = other.outlineWidth;
        proceduralGradientPosition = other.proceduralGradientPosition;
        radialGradientSize = other.radialGradientSize;
        radialGradientStrength = other.radialGradientStrength;
        angleGradientStrength = other.angleGradientStrength;
        proceduralGradientAngle = other.proceduralGradientAngle;
        sdfGradientInnerDistance = other.sdfGradientInnerDistance;
        sdfGradientOuterDistance = other.sdfGradientOuterDistance;
        sdfGradientInnerReach = other.sdfGradientInnerReach;
        sdfGradientOuterReach = other.sdfGradientOuterReach;
        proceduralGradientPointerStrength = other.proceduralGradientPointerStrength;
        conicalGradientCurvature = other.conicalGradientCurvature;
        conicalGradientTailStrength = other.conicalGradientTailStrength;
        noiseSeed = other.noiseSeed;
        noiseScale = other.noiseScale;
        noiseEdge = other.noiseEdge;
        noiseStrength = other.noiseStrength;
        patternDensity = other.patternDensity;
        patternSpeed = other.patternSpeed;
        patternCellParam = other.patternCellParam;
        patternLineThickness = other.patternLineThickness;
        patternSpriteRotation = other.patternSpriteRotation;
        primaryColorOffset = other.primaryColorOffset;
        outlineColorOffset = other.outlineColorOffset;
        proceduralGradientColorOffset = other.proceduralGradientColorOffset;
        patternColorOffset = other.patternColorOffset;
        primaryColorRotation = other.primaryColorRotation;
        outlineColorRotation = other.outlineColorRotation;
        proceduralGradientColorRotation = other.proceduralGradientColorRotation;
        patternColorRotation = other.patternColorRotation;
        primaryColorScale = other.primaryColorScale;
        outlineColorScale = other.outlineColorScale;
        proceduralGradientColorScale = other.proceduralGradientColorScale;
        patternColorScale = other.patternColorScale;
        primaryColorFade = other.primaryColorFade;
        if (primaryColors.Length != Colors1dArrayLength)
        {
            primaryColors = new Color[Colors1dArrayLength];
            outlineColors = new Color[Colors1dArrayLength];
            proceduralGradientColors = new Color[Colors1dArrayLength];
            patternColors = new Color[Colors1dArrayLength];
        }
        if (other.primaryColors.Length != Colors1dArrayLength)
        {
            var oldLength = other.primaryColors.Length;
            Array.Resize(ref other.primaryColors, Colors1dArrayLength);
            Array.Resize(ref other.outlineColors, Colors1dArrayLength);
            Array.Resize(ref other.proceduralGradientColors, Colors1dArrayLength);
            Array.Resize(ref other.patternColors, Colors1dArrayLength);
            for (int i = oldLength; i < Colors1dArrayLength; i++)
            {
                other.primaryColors[i] = Color.white;
                other.outlineColors[i] = Color.black;
                other.proceduralGradientColors[i] = Color.black;
                other.patternColors[i] = Color.black;
            }
        }
        Array.Copy(other.primaryColors, 0, primaryColors, 0, Colors1dArrayLength);
        Array.Copy(other.outlineColors, 0, outlineColors, 0, Colors1dArrayLength);
        Array.Copy(other.proceduralGradientColors, 0, proceduralGradientColors, 0, Colors1dArrayLength);
        Array.Copy(other.patternColors, 0, patternColors, 0, Colors1dArrayLength);
    }

    public bool ValuesEqual(ProceduralProperties other)
    {
        if (other == null) return false;
        if (interpolationType != other.interpolationType) return false;
        if (uvRect != other.uvRect) return false;
        if (!Mathf.Approximately(duration, other.duration)) return false;
        if (offset != other.offset) return false;
        if (!Mathf.Approximately(stroke, other.stroke)) return false;
        if (sizeModifier != other.sizeModifier) return false;
        if (!Mathf.Approximately(softness, other.softness)) return false;
        if (!Mathf.Approximately(rotation, other.rotation)) return false;
        if (cutout != other.cutout) return false;
        if (cornerChamfer != other.cornerChamfer) return false;
        if (cornerConcavity != other.cornerConcavity) return false;
        if (!Mathf.Approximately(collapsedCornerChamfer, other.collapsedCornerChamfer)) return false;
        if (!Mathf.Approximately(collapsedCornerConcavity, other.collapsedCornerConcavity)) return false;
        if (!Mathf.Approximately(collapseEdgeAmount, other.collapseEdgeAmount)) return false;
        if (!Mathf.Approximately(collapseEdgeAmountAbsolute, other.collapseEdgeAmountAbsolute)) return false;
        if (!Mathf.Approximately(collapseEdgePosition, other.collapseEdgePosition)) return false;
        if (primaryColors.Length != other.primaryColors.Length) return false;
        if (proceduralGradientColors.Length != other.proceduralGradientColors.Length) return false;
        if (outlineColors.Length != other.outlineColors.Length) return false;
        if (primaryColorOffset != other.primaryColorOffset) return false;
        if (outlineColorOffset != other.outlineColorOffset) return false;
        if (proceduralGradientColorOffset != other.proceduralGradientColorOffset) return false;
        if (patternColorOffset != other.patternColorOffset) return false;
        if (!Mathf.Approximately(primaryColorRotation, other.primaryColorRotation)) return false;
        if (!Mathf.Approximately(outlineColorRotation, other.outlineColorRotation)) return false;
        if (!Mathf.Approximately(proceduralGradientColorRotation, other.proceduralGradientColorRotation)) return false;
        if (!Mathf.Approximately(patternColorRotation, other.patternColorRotation)) return false;
        if (primaryColorScale != other.primaryColorScale) return false;
        if (outlineColorScale != other.outlineColorScale) return false;
        if (proceduralGradientColorScale != other.proceduralGradientColorScale) return false;
        if (patternColorScale != other.patternColorScale) return false;
        if (primaryColorFade != other.primaryColorFade) return false;
        for (int i = 0; i < Colors1dArrayLength; i++)
        {
            if (primaryColors[i]            != other.primaryColors[i])            return false;
            if (outlineColors[i]            != other.outlineColors[i])            return false;
            if (proceduralGradientColors[i] != other.proceduralGradientColors[i]) return false;
            if (patternColors[i]            != other.patternColors[i])            return false;
        }
        if (!Mathf.Approximately(outlineWidth, other.outlineWidth)) return false;
        if (proceduralGradientPosition != other.proceduralGradientPosition) return false;
        if (radialGradientSize != other.radialGradientSize) return false;
        if (!Mathf.Approximately(radialGradientStrength, other.radialGradientStrength)) return false;
        if (angleGradientStrength != other.angleGradientStrength) return false;
        if (!Mathf.Approximately(proceduralGradientAngle, other.proceduralGradientAngle)) return false;
        if (!Mathf.Approximately(sdfGradientInnerDistance, other.sdfGradientInnerDistance)) return false;
        if (!Mathf.Approximately(sdfGradientOuterDistance, other.sdfGradientOuterDistance)) return false;
        if (!Mathf.Approximately(sdfGradientInnerReach, other.sdfGradientInnerReach)) return false;
        if (!Mathf.Approximately(sdfGradientOuterReach, other.sdfGradientOuterReach)) return false;
        if (!Mathf.Approximately(proceduralGradientPointerStrength, other.proceduralGradientPointerStrength)) return false;
        if (!Mathf.Approximately(conicalGradientCurvature, other.conicalGradientCurvature)) return false;
        if (!Mathf.Approximately(conicalGradientTailStrength, other.conicalGradientTailStrength)) return false;
        if (noiseSeed != other.noiseSeed) return false;
        if (!Mathf.Approximately(noiseScale, other.noiseScale)) return false;
        if (!Mathf.Approximately(noiseEdge, other.noiseEdge)) return false;
        if (!Mathf.Approximately(noiseStrength, other.noiseStrength)) return false;
        if (!Mathf.Approximately(patternDensity, other.patternDensity)) return false;
        if (!Mathf.Approximately(patternSpeed, other.patternSpeed)) return false;
        if (!Mathf.Approximately(patternCellParam, other.patternCellParam)) return false;
        if (patternLineThickness != other.patternLineThickness) return false;
        if (patternSpriteRotation != other.patternSpriteRotation) return false;
        return true;
    }

    public void SetDefaultColors()
    {
        for (int i = 0; i < Colors1dArrayLength; i++)
        {
            primaryColors[i] = Color.white;
            outlineColors[i] = Color.black;
            proceduralGradientColors[i] = Color.black;
            patternColors[i] = Color.black;
        }
    }

    // Spiral indexing allows changing Colors2dArrayDimensionSize non-destructively. Ideally, we'd just use a 2D array, but those cannot be serialized by Unity.
    public static int GetColorSpiralIndex(int x, int y)
    {
        var k = Mathf.Max(x, y);
        var indexOffset = x == k ? y : k + 1 + (k - 1 - x);
        return k * k + indexOffset;
    }

    public Color GetPrimaryColor() => primaryColors[0];
    public Color GetProceduralGradientColor() => proceduralGradientColors[0];
    public Color GetOutlineColor() => outlineColors[0];
    public Color GetPrimaryColorAtCell(int indexX, int indexY) => GetColor(primaryColors, indexX, indexY);
    public Color GetOutlineColorAtCell(int indexX, int indexY) => GetColor(outlineColors, indexX, indexY);
    public Color GetProceduralGradientColorAtCell(int indexX, int indexY) => GetColor(proceduralGradientColors, indexX, indexY);
    public Color GetPatternColorAtCell(int indexX, int indexY) => GetColor(patternColors, indexX, indexY);

    public Color GetColor(Color[] colorsArray, int indexX, int indexY)
    {
        var index = GetColorSpiralIndex(indexX, indexY);
        if (index >= 0 && index < colorsArray.Length)
            return colorsArray[index];

        Debug.LogWarning($"Tried to get color at index [{indexX}, {indexY}] but index was out of bounds.");
        return Color.clear;
    }

    public void SetPrimaryColor(Color color) => primaryColors[0] = color;
    public void SetProceduralGradientColor(Color color) => proceduralGradientColors[0] = color;
    public void SetOutlineColor(Color color) => outlineColors[0] = color;
    public bool SetPrimaryColorAtCell(int indexX, int indexY, Color color) => SetColor(primaryColors, indexX, indexY, color);
    public bool SetOutlineColorAtCell(int indexX, int indexY, Color color) => SetColor(outlineColors, indexX, indexY, color);
    public bool SetProceduralGradientColorAtCell(int indexX, int indexY, Color color) => SetColor(proceduralGradientColors, indexX, indexY, color);
    public bool SetPatternColorAtCell(int indexX, int indexY, Color color) => SetColor(patternColors, indexX, indexY, color);

    public bool SetColor(Color[] colorsArray, int indexX, int indexY, Color color)
    {
        var index = GetColorSpiralIndex(indexX, indexY);
        if (index >= 0 && index < colorsArray.Length)
        {
            colorsArray[index] = color;
            return true;
        }
        Debug.LogWarning($"Tried to set color at index [{indexX}, {indexY}] but index was out of bounds.");
        return false;
    }
}
}
