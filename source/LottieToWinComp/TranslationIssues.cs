﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieToWinComp
{
    /// <summary>
    /// Issues.
    /// </summary>
    sealed class TranslationIssues
    {
        readonly HashSet<(string Code, string Description)> _issues = new HashSet<(string Code, string Description)>();
        readonly bool _throwOnIssue;

        internal TranslationIssues(bool throwOnIssue)
        {
            _throwOnIssue = throwOnIssue;
        }

        internal (string Code, string Description)[] GetIssues() => _issues.ToArray();

        internal void AnimatedRectangleWithTrimPath() => Report("LT0001", "Rectangle with animated size and TrimPath");

        internal void AnimatedTrimOffsetWithStaticTrimOffset() => Report("LT0002", "Animated trim offset with static trim offset");

        internal void AnimationMultiplication() => Report("LT0003", "Multiplication of two or more animated values");

        internal void BlendMode(string blendMode) => Report("LT0004", $"Blend mode: {blendMode}");

        internal void CombiningAnimatedShapes() => Report("LT0005", "Combining animated shapes");

        internal void GradientFill() => Report("LT0006", "Gradient fill");

        internal void GradientStroke() => Report("LT0007", "Gradient stroke");

        internal void ImageLayerIsNotSupported() => Report("LT0009", "Image layers is not supported");

        internal void MergingALargeNumberOfShapes() => Report("LT0010", "Merging a large number of shapes");

        internal void MultipleAnimatedRoundedCorners() => Report("LT0011", "Multiple animated rounded corners");

        internal void MultipleFills() => Report("LT0012", "Multiple fills");

        internal void MultipleStrokes() => Report("LT0013", "Multiple strokes");

        internal void MultipleTrimPaths() => Report("LT0014", "Multiple trim paths");

        internal void OpacityAndColorAnimatedTogether() => Report("LT0015", "Opacity and color animated at the same time");

        internal void PathWithRoundedCorners() => Report("LT0016", "Path with rounded corners");

        internal void Polystar() => Report("LT0017", "Polystar");

        internal void Repeater() => Report("LT0018", "Repeater");

        internal void TextLayer() => Report("LT0019", "Text layer");

        internal void ThreeDIsNotSupported() => Report("LT0020", "3d composition is not supported");

        internal void ThreeDLayerIsNotSupported() => Report("LT0021", "3d layer is not supported");

        internal void TimeStretch() => Report("LT0022", "Time stretch");

        internal void MaskWithInvert() => Report("LT0023", "Mask with invert");

        internal void MaskWithUnsupportedMode(string mode) => Report("LT0024", $"Mask mode: {mode}");

        internal void MaskWithAlpha() => Report("LT0025", "Mask with alpha value other than 1");

        internal void MultipleShapeMasks() => Report("LT0026", "Mask with multiple shapes");

        internal void CombiningMultipleShapes() => Report("LT0027", "CombiningMultipleShapes");

        internal void ReferencedAssetDoesNotExist() => Report("LT0028", "ReferencedAssetDoesNotExist");

        internal void InvalidAssetReferenceFromCurrentLayer(string currentLayerType, string assetRefId, string assetType, string expectedAssetType) => Report("LT0029", $"{currentLayerType} referenced asset {assetRefId} of type {assetType} which is invalid. Expected asset of type {expectedAssetType}.");

        void Report(string code, string description)
        {
            _issues.Add((code, description));

            if (_throwOnIssue)
            {
                throw new NotSupportedException($"{code}: {description}");
            }
        }
    }
}
