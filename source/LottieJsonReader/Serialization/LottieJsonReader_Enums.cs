// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma warning disable SA1601 // Partial elements should be documented
#pragma warning disable SA1205 // Partial elements should declare access

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Microsoft.Toolkit.Uwp.UI.Lottie.GenericData;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Serialization
{
    sealed partial class TestJsonReader
    {
        BlendMode BmToBlendMode(double bm)
        {
            if (bm == (int)bm)
            {
                switch ((int)bm)
                {
                    case 0: return BlendMode.Normal;
                    case 1: return BlendMode.Multiply;
                    case 2: return BlendMode.Screen;
                    case 3: return BlendMode.Overlay;
                    case 4: return BlendMode.Darken;
                    case 5: return BlendMode.Lighten;
                    case 6: return BlendMode.ColorDodge;
                    case 7: return BlendMode.ColorBurn;
                    case 8: return BlendMode.HardLight;
                    case 9: return BlendMode.SoftLight;
                    case 10: return BlendMode.Difference;
                    case 11: return BlendMode.Exclusion;
                    case 12: return BlendMode.Hue;
                    case 13: return BlendMode.Saturation;
                    case 14: return BlendMode.Color;
                    case 15: return BlendMode.Luminosity;
                }
            }

            _issues.UnexpectedValueForType("BlendMode", bm.ToString());
            return BlendMode.Normal;
        }

        (bool success, Layer.LayerType layerType) TyToLayerType(double ty)
        {
            if (ty == (int)ty)
            {
                switch ((int)ty)
                {
                    case 0: return (true, Layer.LayerType.PreComp);
                    case 1: return (true, Layer.LayerType.Solid);
                    case 2: return (true, Layer.LayerType.Image);
                    case 3: return (true, Layer.LayerType.Null);
                    case 4: return (true, Layer.LayerType.Shape);
                    case 5: return (true, Layer.LayerType.Text);
                }
            }

            _issues.UnexpectedValueForType("LayerType", ty.ToString());
            return (false, Layer.LayerType.Null);
        }

        (bool success, Polystar.PolyStarType type) SyToPolystarType(double sy)
        {
            if (sy == (int)sy)
            {
                switch ((int)sy)
                {
                    case 1: return (true, Polystar.PolyStarType.Star);
                    case 2: return (true, Polystar.PolyStarType.Polygon);
                }
            }

            _issues.UnexpectedValueForType("PolyStartType", sy.ToString());
            return (false, Polystar.PolyStarType.Star);
        }

        ShapeStroke.LineCapType LcToLineCapType(double lc)
        {
            if (lc == (int)lc)
            {
                switch ((int)lc)
                {
                    case 1: return ShapeStroke.LineCapType.Butt;
                    case 2: return ShapeStroke.LineCapType.Round;
                    case 3: return ShapeStroke.LineCapType.Projected;
                }
            }

            _issues.UnexpectedValueForType("LineCapType", lc.ToString());
            return ShapeStroke.LineCapType.Butt;
        }

        ShapeStroke.LineJoinType LjToLineJoinType(double lj)
        {
            if (lj == (int)lj)
            {
                switch ((int)lj)
                {
                    case 1: return ShapeStroke.LineJoinType.Miter;
                    case 2: return ShapeStroke.LineJoinType.Round;
                    case 3: return ShapeStroke.LineJoinType.Bevel;
                }
            }

            _issues.UnexpectedValueForType("LineJoinType", lj.ToString());
            return ShapeStroke.LineJoinType.Miter;
        }

        TrimPath.TrimType MToTrimType(double m)
        {
            if (m == (int)m)
            {
                switch ((int)m)
                {
                    case 1: return TrimPath.TrimType.Simultaneously;
                    case 2: return TrimPath.TrimType.Individually;
                }
            }

            _issues.UnexpectedValueForType("TrimType", m.ToString());
            return TrimPath.TrimType.Simultaneously;
        }

        MergePaths.MergeMode MmToMergeMode(double mm)
        {
            if (mm == (int)mm)
            {
                switch ((int)mm)
                {
                    case 1: return MergePaths.MergeMode.Merge;
                    case 2: return MergePaths.MergeMode.Add;
                    case 3: return MergePaths.MergeMode.Subtract;
                    case 4: return MergePaths.MergeMode.Intersect;
                    case 5: return MergePaths.MergeMode.ExcludeIntersections;
                }
            }

            _issues.UnexpectedValueForType("MergeMode", mm.ToString());
            return MergePaths.MergeMode.Merge;
        }

        GradientType TToGradientType(double t)
        {
            if (t == (int)t)
            {
                switch ((int)t)
                {
                    case 1: return GradientType.Linear;
                    case 2: return GradientType.Radial;
                }
            }

            _issues.UnexpectedValueForType("GradientType", t.ToString());
            return GradientType.Linear;
        }

        enum GradientType
        {
            Linear,
            Radial,
        }

        Layer.MatteType TTToMatteType(double tt)
        {
            if (tt == (int)tt)
            {
                switch ((int)tt)
                {
                    case 0: return Layer.MatteType.None;
                    case 1: return Layer.MatteType.Add;
                    case 2: return Layer.MatteType.Invert;
                }
            }

            _issues.UnexpectedValueForType("MatteType", tt.ToString());
            return Layer.MatteType.None;
        }
    }
}
