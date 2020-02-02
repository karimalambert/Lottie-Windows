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
        //// Reads the transform for a repeater. Repeater transforms are the same as regular transforms
        //// except they have an extra couple properties.
        //RepeaterTransform ReadRepeaterTransform(JObject obj, in ShapeLayerContent.ShapeLayerContentArgs shapeLayerContentArgs)
        //{
        //    var startOpacity = ReadOpacityFromObject(obj.GetNamedObject("so", null));
        //    var endOpacity = ReadOpacityFromObject(obj.GetNamedObject("eo", null));
        //    var transform = ReadTransform(obj, in shapeLayerContentArgs);
        //    return new RepeaterTransform(
        //        in shapeLayerContentArgs,
        //        transform.Anchor,
        //        transform.Position,
        //        transform.ScalePercent,
        //        transform.Rotation,
        //        transform.Opacity,
        //        startOpacity,
        //        endOpacity);
        //}

        Transform ReadTransform(JObject obj, in ShapeLayerContent.ShapeLayerContentArgs shapeLayerContentArgs)
        {
            var anchorJson = obj.GetNamedObject("a", null);

            var anchor =
                anchorJson != null
                ? ReadAnimatableVector3(anchorJson)
                : new AnimatableVector3(Vector3.Zero, null);

            var positionJson = obj.GetNamedObject("p", null);

            var position =
                positionJson != null
                    ? ReadAnimatableVector3(positionJson)
                    : new AnimatableVector3(Vector3.Zero, null);

            var scaleJson = obj.GetNamedObject("s", null);

            var scalePercent =
                scaleJson != null
                    ? ReadAnimatableVector3(scaleJson)
                    : new AnimatableVector3(new Vector3(100, 100, 100), null);

            var rotationJson = obj.GetNamedObject("r", null) ?? obj.GetNamedObject("rz", null);

            var rotation =
                    rotationJson != null
                        ? ReadAnimatableRotation(rotationJson)
                        : new Animatable<Rotation>(Rotation.None, null);

            var opacity = ReadOpacityFromO(obj);

            return new Transform(in shapeLayerContentArgs, anchor, position, scalePercent, rotation, opacity);
        }
    }
}
