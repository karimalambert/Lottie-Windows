// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma warning disable SA1601 // Partial elements should be documented
#pragma warning disable SA1205 // Partial elements should declare access

using System.Text.Json;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Serialization
{
    sealed partial class TestJsonReader
    {
        // May return null if there was a problem reading the layer.
        Layer ReadLayer(in JsonElement obj)
        {
            // Not clear whether we need to read these fields.
            IgnoreFieldThatIsNotYetSupported(in obj, "bounds");
            IgnoreFieldThatIsNotYetSupported(in obj, "sy");
            IgnoreFieldThatIsNotYetSupported(in obj, "td");

            // Field 'hasMask' is deprecated and thus we are intentionally ignoring it
            IgnoreFieldIntentionally(in obj, "hasMask");

            var layerArgs = default(Layer.LayerArgs);

            layerArgs.Name = ReadName(in obj);
            var index = ReadInt(in obj, "ind");

            if (!index.HasValue)
            {
                return null;
            }

            layerArgs.Index = index.Value;
            layerArgs.Parent = ReadInt(in obj, "parent");
            layerArgs.Is3d = ReadBool(in obj, "ddd", false);
            layerArgs.AutoOrient = ReadBool(in obj, "ao", false);
            layerArgs.BlendMode = BmToBlendMode(ReadDouble(in obj, "bm", 0));
            layerArgs.IsHidden = ReadBool(in obj, "hd", false);
            var render = ReadBool(in obj, "render", true);

            if (!render)
            {
                _issues.LayerWithRenderFalse();
                return null;
            }

            // Warnings
            if (layerArgs.Name.EndsWith(".ai") ||
                ReadString(in obj, "cl") == "ai")
            {
                _issues.IllustratorLayers();
            }

            if (obj.TryGetProperty("ef", out _))
            {
                _issues.LayerEffectsIsNotSupported(layerArgs.Name);
            }

            // ----------------------
            // Layer Transform
            // ----------------------
            var shapeLayerContentArgs = default(ShapeLayerContent.ShapeLayerContentArgs);
            ReadShapeLayerContentArgs(obj, ref shapeLayerContentArgs);

            //layerArgs.Transform = ReadTransform(obj.GetNamedObject("ks"), in shapeLayerContentArgs);
            // ------------------------------
            // Layer Animation
            // ------------------------------
            layerArgs.TimeStretch = ReadDouble(in obj, "sr", 1.0);

            // Time when the layer starts
            layerArgs.StartFrame = ReadDouble(in obj, "st") ?? double.NaN;

            // Time when the layer becomes visible.
            layerArgs.InFrame = ReadDouble(in obj, "ip") ?? double.NaN;
            layerArgs.OutFrame = ReadDouble(in obj, "op") ?? double.NaN;

            // NOTE: The spec specifies this as 'maskProperties' but the BodyMovin tool exports
            // 'masksProperties' with the plural 'masks'.
            //var maskProperties = obj.GetNamedArray("masksProperties", null);
            //layerArgs.Masks = maskProperties != null ? ReadMaskProperties(maskProperties) : null;
            layerArgs.LayerMatteType = TTToMatteType(ReadDouble(in obj, "tt", (double)Layer.MatteType.None));

            var (isLayerTypeValid, layerType) = TyToLayerType(ReadDouble(in obj, "ty", double.NaN));

            if (!isLayerTypeValid)
            {
                return null;
            }

            return new TextLayer(in layerArgs, null);
        }

        void ReadShapeLayerContentArgs(in JsonElement obj, ref ShapeLayerContent.ShapeLayerContentArgs args)
        {
            args.Name = ReadName(obj);
            args.MatchName = ReadMatchName(obj);
            args.BlendMode = BmToBlendMode(ReadDouble(in obj, "bm", 0));
        }
    }
}
