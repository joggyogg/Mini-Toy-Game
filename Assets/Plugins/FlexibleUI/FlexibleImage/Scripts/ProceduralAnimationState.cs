using System;
using System.Collections.Generic;
using UnityEngine;

namespace JeffGrawAssets.FlexibleUI
{
public class AnimationValues
{
    public ProceduralProperties lastReachedStateProperties = new();

    private ProceduralProperties _currentProperties;
    public ProceduralProperties CurrentProperties => _currentProperties;
    public void SetCurrentProps(ProceduralProperties newProps, bool safeCopy)
    {
        if (safeCopy)
        {
            if (currentPropsAreOutsideRef)
                _currentProperties = new ProceduralProperties();

            _currentProperties.Copy(newProps);
            currentPropsAreOutsideRef = false;
        }
        else
        {
            _currentProperties = newProps;
            currentPropsAreOutsideRef = true;
        }
    }

    public float time, adjustedTime;
    public int lastSelectionStateIdx = -1, lastReachedStateIdx = -1;
    public bool checkUnwind;

    private bool currentPropsAreOutsideRef = true;

    public void Reset()
    {
        lastReachedStateProperties.Copy(_currentProperties);
        SetCurrentProps(lastReachedStateProperties, true);
        time = 0f;
    }
}

[Serializable]
public class ProceduralAnimationState
{
    public enum PlaybackType { Once, Repeat, PingPong }

    public List<ProceduralProperties> proceduralProperties;
    public PlaybackType playbackType;
    public int loopStartIdx;
    public int unwindToIdx = -1;
    public float unwindRate = 1f;

    public ProceduralAnimationState() => proceduralProperties = new();

    public ProceduralAnimationState(ProceduralAnimationState toClone)
    {
        playbackType = toClone.playbackType;
        loopStartIdx = toClone.loopStartIdx;
        proceduralProperties = new List<ProceduralProperties>(toClone.proceduralProperties);
    }

    public ProceduralProperties PopulateIfEmptyAndGetFirstProps()
    {
        if (proceduralProperties.Count == 0)
        {
            proceduralProperties.Add(new ProceduralProperties());
            proceduralProperties[0].SetDefaultColors();
        }

        return proceduralProperties[0];
    }
    
    public bool Unwind(AnimationValues animationValues)
    {
        if (unwindToIdx < 0)
            return false;

        animationValues.adjustedTime -= Time.unscaledDeltaTime * Mathf.Max(0.01f, unwindRate);
        var interpolationPercent = GetInterpolationPoints(animationValues, out var first, out var second, out var secondIdx);
        if (secondIdx < unwindToIdx || secondIdx == unwindToIdx && Mathf.Approximately(interpolationPercent, 0f))
        {
            animationValues.lastReachedStateProperties.Copy(first);
            return false;
        }

        LerpProperties(animationValues.CurrentProperties, first, second, interpolationPercent);
        return true;
    }

    public bool ComputeProperties(AnimationValues animationValues)
    {
        animationValues.time += Time.unscaledDeltaTime;
        animationValues.adjustedTime = GetAdjustedTime(animationValues.time, out var finished);
        var interpolationPercent = GetInterpolationPoints(animationValues, out var first, out var second, out _);
        if (finished)
        {
            animationValues.SetCurrentProps(second, true);
            return true;
        }
        LerpProperties(animationValues.CurrentProperties, first, second, interpolationPercent);
        return false;
    }

    public float GetAdjustedTime(float inputTime, out bool finished)
    {
        finished = false;

        var maxTime = 0f;
        var loopStartTime = 0f;
        for (int i = 0; i < proceduralProperties.Count; i++)
        {
            if (i == loopStartIdx)
                loopStartTime = maxTime;

            maxTime += proceduralProperties[i].duration;
        }

        if (inputTime <= maxTime)
            return inputTime;

        switch (playbackType)
        {
            case PlaybackType.Repeat:
                inputTime = loopStartTime + Mathf.Repeat(inputTime - loopStartTime, maxTime - loopStartTime);
                break;
            case PlaybackType.PingPong:
                inputTime = loopStartTime + Mathf.PingPong(inputTime - loopStartTime, maxTime - loopStartTime);
                break;
            case PlaybackType.Once:
            default:
                finished = true;
                inputTime = maxTime;
                break;
        }
        return inputTime;
    }

    private float GetInterpolationPoints(AnimationValues animationValues, out ProceduralProperties first, out ProceduralProperties second, out int secondIdx)
    {
        first = animationValues.lastReachedStateProperties;
        secondIdx = proceduralProperties.Count - 1;
        second = proceduralProperties[secondIdx];
        var time = Mathf.Max(0f, animationValues.adjustedTime);
        var cumulativeTime = 0f;
        var interpolationPercent = 0f;

        for (int i = 0; i < proceduralProperties.Count; i++)
        {
            var props = proceduralProperties[i];
            if (time <= cumulativeTime + props.duration)
            {
                if (i == 0)
                {
                    second = props;
                    interpolationPercent = time > 0f ? time / props.duration : 0f;
                }
                else
                {
                    first = proceduralProperties[i - 1];
                    second = props;
                    interpolationPercent = (time - cumulativeTime) / props.duration;
                }
                secondIdx = i;
                break;
            }
            cumulativeTime += props.duration;
        }

        return interpolationPercent;
    }

    public static void LerpProperties(ProceduralProperties active, ProceduralProperties first, ProceduralProperties second, float percentDone)
    {
        percentDone = ApplyEasing(percentDone, second.interpolationType);

        active.uvRect = Vector4.Lerp(first.uvRect, second.uvRect, percentDone);
        active.cornerChamfer = Vector4.Lerp(first.cornerChamfer, second.cornerChamfer, percentDone);
        active.cornerConcavity = Vector4.Lerp(first.cornerConcavity, second.cornerConcavity, percentDone);
        active.collapsedCornerChamfer = Mathf.Lerp(first.collapsedCornerChamfer, second.collapsedCornerChamfer, percentDone);
        active.collapsedCornerConcavity = Mathf.Lerp(first.collapsedCornerConcavity, second.collapsedCornerConcavity, percentDone);
        active.collapseEdgeAmount = Mathf.Lerp(first.collapseEdgeAmount, second.collapseEdgeAmount, percentDone);
        active.collapseEdgeAmountAbsolute = Mathf.Lerp(first.collapseEdgeAmountAbsolute, second.collapseEdgeAmountAbsolute, percentDone);
        active.collapseEdgePosition = Mathf.Lerp(first.collapseEdgePosition, second.collapseEdgePosition, percentDone);
        active.primaryColorFade = (byte)Mathf.Lerp(first.primaryColorFade, second.primaryColorFade, percentDone);
        active.offset = Vector2.Lerp(first.offset, second.offset, percentDone);
        active.rotation = Mathf.Lerp(first.rotation, second.rotation, percentDone);
        active.sizeModifier = Vector2.Lerp(first.sizeModifier, second.sizeModifier, percentDone);
        active.stroke = Mathf.Lerp(first.stroke, second.stroke, percentDone);
        active.softness = Mathf.Lerp(first.softness, second.softness, percentDone);
        active.cutout = Vector4.Lerp(first.cutout, second.cutout, percentDone);
        active.outlineWidth = Mathf.Lerp(first.outlineWidth, second.outlineWidth, percentDone);
        active.proceduralGradientPosition = Vector2.Lerp(first.proceduralGradientPosition, second.proceduralGradientPosition, percentDone);
        active.radialGradientSize = Vector2.Lerp(first.radialGradientSize, second.radialGradientSize, percentDone);
        active.radialGradientStrength = Mathf.Lerp(first.radialGradientStrength, second.radialGradientStrength, percentDone);
        active.angleGradientStrength = Vector2.Lerp(first.angleGradientStrength, second.angleGradientStrength, percentDone);
        active.proceduralGradientAngle = Mathf.Lerp(first.proceduralGradientAngle, second.proceduralGradientAngle, percentDone);
        active.sdfGradientInnerDistance = Mathf.Lerp(first.sdfGradientInnerDistance, second.sdfGradientInnerDistance, percentDone);
        active.sdfGradientOuterDistance = Mathf.Lerp(first.sdfGradientOuterDistance, second.sdfGradientOuterDistance, percentDone);
        active.sdfGradientInnerReach = Mathf.Lerp(first.sdfGradientInnerReach, second.sdfGradientInnerReach, percentDone);
        active.sdfGradientOuterReach = Mathf.Lerp(first.sdfGradientOuterReach, second.sdfGradientOuterReach, percentDone);
        active.proceduralGradientPointerStrength = Mathf.Lerp(first.proceduralGradientPointerStrength, second.proceduralGradientPointerStrength, percentDone);
        active.conicalGradientCurvature = Mathf.Lerp(first.conicalGradientCurvature, second.conicalGradientCurvature, percentDone);
        active.conicalGradientTailStrength = Mathf.Lerp(first.conicalGradientTailStrength, second.conicalGradientTailStrength, percentDone);
        active.noiseSeed = (uint)Mathf.Lerp(first.noiseSeed, second.noiseSeed, percentDone);
        active.noiseScale = Mathf.Lerp(first.noiseScale, second.noiseScale, percentDone);
        active.noiseEdge = Mathf.Lerp(first.noiseEdge, second.noiseEdge, percentDone);
        active.noiseStrength = Mathf.Lerp(first.noiseStrength, second.noiseStrength, percentDone);
        active.patternDensity = Mathf.Lerp(first.patternDensity, second.patternDensity, percentDone);
        active.patternSpeed = Mathf.Lerp(first.patternSpeed, second.patternSpeed, percentDone);
        active.patternCellParam = Mathf.Lerp(first.patternCellParam, second.patternCellParam, percentDone);
        active.patternLineThickness = (byte)Mathf.Lerp(first.patternLineThickness, second.patternLineThickness, percentDone);
        active.patternSpriteRotation = (byte)Mathf.Lerp(first.patternSpriteRotation, second.patternSpriteRotation, percentDone);

        active.primaryColorOffset = Vector2.Lerp(first.primaryColorOffset, second.primaryColorOffset, percentDone);
        active.outlineColorOffset = Vector2.Lerp(first.outlineColorOffset, second.outlineColorOffset, percentDone);
        active.proceduralGradientColorOffset = Vector2.Lerp(first.proceduralGradientColorOffset, second.proceduralGradientColorOffset, percentDone);
        active.patternColorOffset = Vector2.Lerp(first.patternColorOffset, second.patternColorOffset, percentDone);
        active.primaryColorScale = Vector2.Lerp(first.primaryColorScale, second.primaryColorScale, percentDone);
        active.outlineColorScale = Vector2.Lerp(first.outlineColorScale, second.outlineColorScale, percentDone);
        active.proceduralGradientColorScale = Vector2.Lerp(first.proceduralGradientColorScale, second.proceduralGradientColorScale, percentDone);
        active.patternColorScale = Vector2.Lerp(first.patternColorScale, second.patternColorScale, percentDone);
        active.primaryColorRotation = Mathf.Lerp(first.primaryColorRotation, second.primaryColorRotation, percentDone);
        active.outlineColorRotation = Mathf.Lerp(first.outlineColorRotation, second.outlineColorRotation, percentDone);
        active.proceduralGradientColorRotation = Mathf.Lerp(first.proceduralGradientColorRotation, second.proceduralGradientColorRotation, percentDone);
        active.patternColorRotation = Mathf.Lerp(first.patternColorRotation, second.patternColorRotation, percentDone);

        for (int i = 0; i < ProceduralProperties.Colors1dArrayLength; i++)
        {
            active.primaryColors[i] = Color.Lerp(first.primaryColors[i], second.primaryColors[i], percentDone);
            active.outlineColors[i] = Color.Lerp(first.outlineColors[i], second.outlineColors[i], percentDone);
            active.proceduralGradientColors[i] = Color.Lerp(first.proceduralGradientColors[i], second.proceduralGradientColors[i], percentDone);
            active.patternColors[i] = Color.Lerp(first.patternColors[i], second.patternColors[i], percentDone);
        }
    }

    private static float ApplyEasing(float t, ProceduralProperties.InterpolationType interpolationType) =>
        interpolationType switch
        {
            ProceduralProperties.InterpolationType.QuadraticEaseIn    => t * t,
            ProceduralProperties.InterpolationType.QuadraticEaseOut   => t * (2 - t),
            ProceduralProperties.InterpolationType.QuadraticEaseInOut => t < 0.5f ? 2 * t * t : -1 + (4 - 2 * t) * t,
            ProceduralProperties.InterpolationType.SineEaseIn         => 1 - (float)Math.Cos(t * Math.PI * 0.5f),
            ProceduralProperties.InterpolationType.SineEaseOut        => (float)Math.Sin(t * Math.PI * 0.5f),
            ProceduralProperties.InterpolationType.SineEaseInOut      => 0.5f * (1 - (float)Math.Cos(t * Math.PI)),
            ProceduralProperties.InterpolationType.CircularEaseIn     => 1 - (float)Math.Sqrt(1 - t * t),
            ProceduralProperties.InterpolationType.CircularEaseOut    => (float)Math.Sqrt(1 - (t - 1) * (t - 1)),
            ProceduralProperties.InterpolationType.CircularEaseInOut  => t < 0.5f ? 0.5f * (1 - (float)Math.Sqrt(1 - 4 * t * t)) : 0.5f * ((float)Math.Sqrt(1 - (2 * t - 2) * (2 * t - 2)) + 1),
            ProceduralProperties.InterpolationType.QuinticEaseIn      => t * t * t * t * t,
            ProceduralProperties.InterpolationType.QuinticEaseOut     => (float)Math.Pow(t - 1, 5) + 1,
            ProceduralProperties.InterpolationType.QuinticEaseInOut   => t < 0.5f ? 16 * t * t * t * t * t : 0.5f * (float)Math.Pow(2 * t - 2, 5) + 1,
            _ => t
        };
}
}
