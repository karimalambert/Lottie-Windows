// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Xml.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Wui;
using Microsoft.Toolkit.Uwp.UI.Lottie.YamlData;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.Tools
{
    /// <summary>
    /// Serializes a <see cref="CompositionObject"/> graph into an XML format.
    /// </summary>
    /// <remarks>The format is only designed for human consumption, and should
    /// not be relied upon for deserialization.</remarks>
#if PUBLIC_WinCompData
    public
#endif
    sealed class CompositionObjectYamlSerializer
    {
        CompositionObjectYamlSerializer()
        {
        }

        public static void WriteYaml(CompositionObject root, TextWriter writer, string comment = null)
        {
            var serializer = new CompositionObjectYamlSerializer();

            // Convert the CompositionObject graph into the Yaml data model.
            var yaml = serializer.FromCompositionObject(root);

            var yamlWriter = new YamlWriter(writer);
            var documentStart = "--- !Composition";
            if (!string.IsNullOrWhiteSpace(comment))
            {
                documentStart += $" # {comment}";
            }

            yamlWriter.Write(documentStart);
            yamlWriter.WriteObject(yaml);
        }

        public static XDocument ToXml(CompositionObject compositionObject)
        {
            return new CompositionObjectYamlSerializer().ToXDocument(compositionObject);
        }

        XDocument ToXDocument(CompositionObject compositionObject)
        {
            return new XDocument(FromCompositionObject(compositionObject));
        }

        YamlMap FromCompositionObject(CompositionObject obj)
        {
            if (obj == null)
            {
                return null;
            }

            var result = new YamlMap
            {
                { "Type", obj.Type.ToString() },
            };

            switch (obj.Type)
            {
                case CompositionObjectType.AnimationController:
                    return FromAnimationController(result, (AnimationController)obj);

                case CompositionObjectType.ColorKeyFrameAnimation:
                case CompositionObjectType.ExpressionAnimation:
                case CompositionObjectType.PathKeyFrameAnimation:
                case CompositionObjectType.ScalarKeyFrameAnimation:
                case CompositionObjectType.Vector2KeyFrameAnimation:
                    return FromCompositionAnimation((CompositionAnimation)obj, result);

                case CompositionObjectType.CompositionColorBrush:
                case CompositionObjectType.CompositionEffectBrush:
                case CompositionObjectType.CompositionSurfaceBrush:
                    return FromCompositionBrush((CompositionBrush)obj, result);

                case CompositionObjectType.CompositionContainerShape:
                case CompositionObjectType.CompositionSpriteShape:
                    return FromCompositionShape((CompositionShape)obj, result);

                case CompositionObjectType.CompositionEllipseGeometry:
                case CompositionObjectType.CompositionPathGeometry:
                case CompositionObjectType.CompositionRectangleGeometry:
                case CompositionObjectType.CompositionRoundedRectangleGeometry:
                    return FromCompositionGeometry((CompositionGeometry)obj, result);

                case CompositionObjectType.CompositionPropertySet:
                    return FromCompositionPropertySet((CompositionPropertySet)obj, result);

                case CompositionObjectType.CompositionViewBox:
                    return FromCompositionViewBox((CompositionViewBox)obj, result);

                case CompositionObjectType.ContainerVisual:
                case CompositionObjectType.ShapeVisual:
                case CompositionObjectType.SpriteVisual:
                    return FromVisual((Visual)obj, result);

                case CompositionObjectType.CubicBezierEasingFunction:
                case CompositionObjectType.LinearEasingFunction:
                case CompositionObjectType.StepEasingFunction:
                    return FromCompositionEasingFunction((CompositionEasingFunction)obj, result);

                case CompositionObjectType.InsetClip:
                case CompositionObjectType.CompositionGeometricClip:
                    return FromCompositionClip((CompositionClip)obj, result);

                default:
                    throw new InvalidOperationException();
            }
        }

        YamlMap FromCompositionAnimation(CompositionAnimation obj, YamlMap result)
        {
            result.Add("Target", obj.Target);

            switch (obj.Type)
            {
                case CompositionObjectType.ExpressionAnimation:
                    return FromExpressionAnimation((ExpressionAnimation)obj, result);
                case CompositionObjectType.ColorKeyFrameAnimation:
                    return FromColorKeyFrameAnimation((ColorKeyFrameAnimation)obj, result);
                case CompositionObjectType.PathKeyFrameAnimation:
                    return FromPathKeyFrameAnimation((PathKeyFrameAnimation)obj, result);
                case CompositionObjectType.ScalarKeyFrameAnimation:
                    return FromScalarKeyFrameAnimation((ScalarKeyFrameAnimation)obj, result);
                case CompositionObjectType.Vector2KeyFrameAnimation:
                    return FromVector2KeyFrameAnimation((Vector2KeyFrameAnimation)obj, result);
                default:
                    throw new InvalidOperationException();
            }
        }

        YamlMap FromColorKeyFrameAnimation(ColorKeyFrameAnimation obj, YamlMap result)
        {
            return result;

            //return new XElement(GetCompositionObjectName(obj), GetContents());
            //IEnumerable<XObject> GetContents()
            //{
            //    foreach (var item in GetCompositionObjectContents(obj))
            //    {
            //        return item;
            //    }
            //}
        }

        YamlMap FromCompositionBrush(CompositionBrush obj, YamlMap result)
        {
            switch (obj.Type)
            {
                case CompositionObjectType.CompositionColorBrush:
                    return FromCompositionColorBrush((CompositionColorBrush)obj, result);
                case CompositionObjectType.CompositionEffectBrush:
                    return FromCompositionEffectBrush((CompositionEffectBrush)obj, result);
                case CompositionObjectType.CompositionSurfaceBrush:
                    return FromCompositionSurfaceBrush((CompositionSurfaceBrush)obj, result);
                default:
                    throw new InvalidOperationException();
            }
        }

        YamlMap FromCompositionColorBrush(CompositionColorBrush obj, YamlMap result)
        {
            AddColorProperty(nameof(obj.Color), obj.Color, result);
            return result;
        }

        YamlMap FromCompositionEffectBrush(CompositionEffectBrush obj, YamlMap result)
        {
            return result;
        }

        YamlMap FromCompositionSurfaceBrush(CompositionSurfaceBrush obj, YamlMap result)
        {
            return result;
        }

        YamlMap FromCompositionContainerShape(CompositionContainerShape obj, YamlMap result)
        {
            AddSequenceProperty("Shapes", obj.Shapes, result);
            return result;
        }

        YamlMap FromCompositionGeometry(CompositionGeometry obj, YamlMap result)
        {
            AddFloatPropertyDefault1(nameof(obj.TrimEnd), obj.TrimEnd, result);
            AddFloatPropertyDefault0(nameof(obj.TrimOffset), obj.TrimOffset, result);
            AddFloatPropertyDefault0(nameof(obj.TrimStart), obj.TrimStart, result);

            switch (obj.Type)
            {
                case CompositionObjectType.CompositionEllipseGeometry:
                    return FromCompositionEllipseGeometry((CompositionEllipseGeometry)obj, result);
                case CompositionObjectType.CompositionPathGeometry:
                    return FromCompositionPathGeometry((CompositionPathGeometry)obj, result);
                case CompositionObjectType.CompositionRectangleGeometry:
                    return FromCompositionRectangleGeometry((CompositionRectangleGeometry)obj, result);
                case CompositionObjectType.CompositionRoundedRectangleGeometry:
                    return FromCompositionRoundedRectangleGeometry((CompositionRoundedRectangleGeometry)obj, result);
                default:
                    throw new InvalidOperationException();
            }
        }

        YamlMap FromCompositionEllipseGeometry(CompositionEllipseGeometry obj, YamlMap result)
        {
            return result;

            //return new XElement(GetCompositionObjectName(obj), GetContents());
            //IEnumerable<XObject> GetContents()
            //{
            //    foreach (var item in GetCompositionGeometryContents(obj))
            //    {
            //        return item;
            //    }

            //    return FromVector2(nameof(obj.Center), obj.Center);
            //    return FromVector2(nameof(obj.Radius), obj.Radius);
            //}
        }

        YamlMap FromCompositionPropertySet(CompositionPropertySet obj, YamlMap result)
        {
            foreach (var propertyName in obj.PropertyNames)
            {
                result.Add(propertyName, "TODO - support property values");
            }

            return result;
        }

        YamlMap AddCompositionPathProperty(string name, CompositionPath path, YamlMap result)
        {
            if (path != null)
            {
                result.Add(name, FromCompositionPath(path));
            }

            return result;
        }

        YamlMap FromCompositionPath(CompositionPath obj)
        {
            var result = new YamlMap { { "Type", "CompositionPath" } };
            AddIGeometrySource2DProperty(nameof(obj.Source), obj.Source, result);
            return result;
        }

        YamlMap FromCompositionPathGeometry(CompositionPathGeometry obj, YamlMap result)
        {
            AddCompositionPathProperty(nameof(obj.Path), obj.Path, result);
            return result;
        }

        YamlMap FromCompositionRectangleGeometry(CompositionRectangleGeometry obj, YamlMap result)
        {
            return result;

            //return new XElement(GetCompositionObjectName(obj), GetContents());
            //IEnumerable<XObject> GetContents()
            //{
            //    foreach (var item in GetCompositionGeometryContents(obj))
            //    {
            //        return item;
            //    }

            //    if (obj.Offset != null)
            //    {
            //        return FromVector2(nameof(obj.Offset), obj.Offset.Value);
            //    }

            //    return FromVector2(nameof(obj.Size), obj.Size);
            //}
        }

        YamlMap FromCompositionRoundedRectangleGeometry(CompositionRoundedRectangleGeometry obj, YamlMap result)
        {
            return result;

            //return new XElement(GetCompositionObjectName(obj), GetContents());
            //IEnumerable<XObject> GetContents()
            //{
            //    foreach (var item in GetCompositionGeometryContents(obj))
            //    {
            //        return item;
            //    }

            //    if (obj.Offset != null)
            //    {
            //        return FromVector2(nameof(obj.Offset), obj.Offset.Value);
            //    }

            //    return FromVector2(nameof(obj.Size), obj.Size);
            //    return FromVector2(nameof(obj.CornerRadius), obj.CornerRadius);
            //}
        }

        YamlMap FromCompositionShape(CompositionShape obj, YamlMap result)
        {
            AddVector2PropertyDefault0(nameof(obj.CenterPoint), obj.CenterPoint, result);
            AddVector2PropertyDefault0(nameof(obj.Offset), obj.Offset, result);
            AddFloatProperty(nameof(obj.RotationAngleInDegrees), obj.RotationAngleInDegrees, result);
            AddVector2PropertyDefault1(nameof(obj.Scale), obj.Scale, result);
            AddMatrix3x2Property(nameof(obj.TransformMatrix), obj.TransformMatrix, result);
            switch (obj.Type)
            {
                case CompositionObjectType.CompositionContainerShape:
                    return FromCompositionContainerShape((CompositionContainerShape)obj, result);
                case CompositionObjectType.CompositionSpriteShape:
                    return FromCompositionSpriteShape((CompositionSpriteShape)obj, result);
                default:
                    throw new InvalidOperationException();
            }
        }

        YamlMap FromCompositionSpriteShape(CompositionSpriteShape obj, YamlMap result)
        {
            AddCompositionBrushProperty(nameof(obj.FillBrush), obj.FillBrush, result);
            AddCompositionObjectProperty(nameof(obj.Geometry), obj.Geometry, result);

            // TODO: IsStrokeNonScaling
            AddCompositionBrushProperty(nameof(obj.StrokeBrush), obj.StrokeBrush, result);

            // TODO: StrokeDashArray
            // TODO: StrokeDashCap
            AddFloatPropertyDefault0(nameof(obj.StrokeDashOffset), obj.StrokeDashOffset, result);

            // TODO: StrokeEndCap
            // TODO: StrokeLineJoin
            AddFloatPropertyDefault1(nameof(obj.StrokeMiterLimit), obj.StrokeMiterLimit, result);

            // TODO: StrokeStartCap
            AddFloatPropertyDefault1(nameof(obj.StrokeThickness), obj.StrokeThickness, result);

            return result;
        }

        YamlMap AddViewBoxProperty(string name, CompositionViewBox obj, YamlMap result)
        {
            if (obj != null)
            {
                result.Add(name, FromCompositionObject(obj));
            }

            return result;
        }

        YamlMap FromCompositionViewBox(CompositionViewBox obj, YamlMap result)
        {
            AddVector2Property(nameof(obj.Size), obj.Size, result);
            return result;
        }

        YamlMap FromCompositionEasingFunction(CompositionEasingFunction obj, YamlMap result)
        {
            switch (obj.Type)
            {
                case CompositionObjectType.CubicBezierEasingFunction:
                    return FromCubicBezierEasingFunction((CubicBezierEasingFunction)obj, result);
                case CompositionObjectType.LinearEasingFunction:
                    return FromLinearEasingFunction((LinearEasingFunction)obj, result);
                case CompositionObjectType.StepEasingFunction:
                    return FromStepEasingFunction((StepEasingFunction)obj, result);
                default:
                    throw new InvalidOperationException();
            }
        }

        YamlMap FromCubicBezierEasingFunction(CubicBezierEasingFunction obj, YamlMap result)
        {
            return result;

            //return new XElement(GetCompositionObjectName(obj), GetContents());
            //IEnumerable<XObject> GetContents()
            //{
            //    foreach (var item in GetCompositionObjectContents(obj))
            //    {
            //        return item;
            //    }
            //}
        }

        YamlMap FromCompositionClip(CompositionClip obj, YamlMap result)
        {
            AddVector2PropertyDefault0(nameof(obj.CenterPoint), obj.CenterPoint, result);
            AddVector2PropertyDefault1(nameof(obj.Scale), obj.Scale, result);

            switch (obj.Type)
            {
                case CompositionObjectType.CompositionGeometricClip:
                    return FromCompositionGeometricClip((CompositionGeometricClip)obj, result);
                case CompositionObjectType.InsetClip:
                    return FromInsetClip((InsetClip)obj, result);
                default:
                    throw new InvalidOperationException();
            }
        }

        YamlMap FromInsetClip(InsetClip obj, YamlMap result)
        {
            AddFloatPropertyDefault0(nameof(obj.BottomInset), obj.LeftInset, result);
            AddFloatPropertyDefault0(nameof(obj.LeftInset), obj.LeftInset, result);
            AddFloatPropertyDefault0(nameof(obj.RightInset), obj.RightInset, result);
            AddFloatPropertyDefault0(nameof(obj.TopInset), obj.LeftInset, result);
            return result;
        }

        YamlMap FromCompositionGeometricClip(CompositionGeometricClip obj, YamlMap result)
        {
            return result;

            //return new XElement(GetCompositionObjectName(obj), GetContents());
            //IEnumerable<XObject> GetContents()
            //{
            //    if (obj.Geometry != null)
            //    {
            //        return new XElement("Geometry", FromCompositionObject(obj.Geometry));
            //    }
            //}
        }

        //IEnumerable<XObject> GetCompositionClipContents(CompositionClip obj)
        //{
        //    foreach (var item in GetCompositionObjectContents(obj))
        //    {
        //        return item;
        //    }

        //    return FromVector2DefaultZero(nameof(obj.CenterPoint), obj.CenterPoint);
        //    return FromVector2DefaultOne(nameof(obj.Scale), obj.Scale);
        //}
        YamlMap FromLinearEasingFunction(LinearEasingFunction obj, YamlMap result)
        {
            return result;

            //return new XElement(GetCompositionObjectName(obj), GetContents());
            //IEnumerable<XObject> GetContents()
            //{
            //    foreach (var item in GetCompositionObjectContents(obj))
            //    {
            //        return item;
            //    }
            //}
        }

        YamlMap FromPathKeyFrameAnimation(PathKeyFrameAnimation obj, YamlMap result)
        {
            return result;

            //return new XElement(GetCompositionObjectName(obj), GetContents());
            //IEnumerable<XObject> GetContents()
            //{
            //    foreach (var item in GetCompositionObjectContents(obj))
            //    {
            //        return item;
            //    }
            //}
        }

        YamlMap FromScalarKeyFrameAnimation(ScalarKeyFrameAnimation obj, YamlMap result)
        {
            return result;

            //return new XElement(GetCompositionObjectName(obj), GetContents());
            //IEnumerable<XObject> GetContents()
            //{
            //    foreach (var item in GetCompositionObjectContents(obj))
            //    {
            //        return item;
            //    }
            //}
        }

        YamlMap FromVisual(Visual obj, YamlMap result)
        {
            AddVector3Property(nameof(obj.CenterPoint), obj.CenterPoint, result);
            AddCompositionObjectProperty(nameof(obj.Clip), obj.Clip, result);
            AddVector3Property(nameof(obj.Offset), obj.Offset, result);
            AddFloatProperty(nameof(obj.Opacity), obj.Opacity, result);
            AddFloatProperty(nameof(obj.RotationAngleInDegrees), obj.RotationAngleInDegrees, result);
            AddVector3Property(nameof(obj.RotationAxis), obj.RotationAxis, result);
            AddVector3Property(nameof(obj.Scale), obj.Scale, result);
            AddVector2Property(nameof(obj.Size), obj.Size, result);
            AddMatrix4x4Property(nameof(obj.TransformMatrix), obj.TransformMatrix, result);

            //    foreach (var item in FromAnimatableVector3("Offset", obj.Animators, obj.Offset))
            //    {
            //        return item;
            //    }

            //    foreach (var item in FromAnimatableVector3("CenterPoint", obj.Animators, obj.CenterPoint))
            //    {
            //        return item;
            //    }

            //    if (obj.RotationAngleInDegrees.HasValue)
            //    {
            //        return new XAttribute("RotationAngleInDegrees", obj.RotationAngleInDegrees.Value);
            //    }

            //    foreach (var item in FromAnimatableVector3("Scale", obj.Animators, obj.Scale))
            //    {
            //        return item;
            //    }

            //    if (obj.Clip != null)
            //    {
            //        return new XElement("Clip", FromCompositionObject(obj.Clip));
            //    }
            //}
            switch (obj.Type)
            {
                case CompositionObjectType.ContainerVisual:
                case CompositionObjectType.ShapeVisual:
                case CompositionObjectType.SpriteVisual:
                    return FromContainerVisual((ContainerVisual)obj, result);
                default:
                    throw new InvalidOperationException();
            }
        }

        YamlMap FromContainerVisual(ContainerVisual obj, YamlMap result)
        {
            AddSequenceProperty(nameof(obj.Children), obj.Children, result);

            switch (obj.Type)
            {
                case CompositionObjectType.ContainerVisual:
                    return result;
                case CompositionObjectType.ShapeVisual:
                    return FromShapeVisual((ShapeVisual)obj, result);
                case CompositionObjectType.SpriteVisual:
                    return FromSpriteVisual((SpriteVisual)obj, result);
                default:
                    throw new InvalidOperationException();
            }
        }

        YamlMap FromShapeVisual(ShapeVisual obj, YamlMap result)
        {
            AddSequenceProperty(nameof(obj.Shapes), obj.Shapes, result);
            AddCompositionObjectProperty(nameof(obj.ViewBox), obj.ViewBox, result);
            return result;
        }

        YamlMap FromSpriteVisual(SpriteVisual obj, YamlMap result)
        {
            AddCompositionBrushProperty(nameof(obj.Brush), obj.Brush, result);
            return result;
        }

        YamlMap FromStepEasingFunction(StepEasingFunction obj, YamlMap result)
        {
            return result;

            //return new XElement(GetCompositionObjectName(obj), GetContents());
            //IEnumerable<XObject> GetContents()
            //{
            //    foreach (var item in GetCompositionObjectContents(obj))
            //    {
            //        return item;
            //    }
            //}
        }

        YamlMap FromVector2KeyFrameAnimation(Vector2KeyFrameAnimation obj, YamlMap result)
        {
            return result;

            //return new XElement(GetCompositionObjectName(obj), GetContents());
            //IEnumerable<XObject> GetContents()
            //{
            //    foreach (var item in GetCompositionObjectContents(obj))
            //    {
            //        return item;
            //    }
            //}
        }

        YamlMap FromAnimationController(YamlMap result, AnimationController obj)
        {
            return result;

            //return new XElement(GetCompositionObjectName(obj), GetContents());
            //IEnumerable<XObject> GetContents()
            //{
            //    foreach (var item in GetCompositionObjectContents(obj))
            //    {
            //        return item;
            //    }
            //}
        }

        //YamlMap GetCompositionObjectContents(CompositionObject obj)
        //{
        //    var result = new YamlMap();

        //    // Find the animations that are targetting properties in the property set.
        //    var propertySetAnimators =
        //        from pn in obj.Properties.PropertyNames
        //        from an in obj.Animators
        //        where an.AnimatedProperty == pn
        //        select an;

        //    if (!obj.Properties.IsEmpty)
        //    {
        //        return FromCompositionPropertySet(obj.Properties, propertySetAnimators);
        //    }

        //    {
        //        { "Name", obj.Name },
        //    };
        //    return result;
        //}

        //IEnumerable<XObject> GetCompositionGeometryContents(CompositionGeometry obj)
        //{
        //    foreach (var item in GetCompositionObjectContents(obj))
        //    {
        //        return item;
        //    }

        //    foreach (var item in FromAnimatableScalar(nameof(obj.TrimStart), obj.Animators, obj.TrimStart))
        //    {
        //        return item;
        //    }

        //    foreach (var item in FromAnimatableScalar(nameof(obj.TrimEnd), obj.Animators, obj.TrimEnd))
        //    {
        //        return item;
        //    }

        //    foreach (var item in FromAnimatableScalar(nameof(obj.TrimOffset), obj.Animators, obj.TrimOffset))
        //    {
        //        return item;
        //    }
        //}
        //IEnumerable<XObject> GetCompositionShapeContents(CompositionShape obj)
        //{
        //    foreach (var item in GetCompositionObjectContents(obj))
        //    {
        //        return item;
        //    }

        //    foreach (var item in FromAnimatableVector2(nameof(obj.CenterPoint), obj.Animators, obj.CenterPoint))
        //    {
        //        return item;
        //    }

        //    foreach (var item in FromAnimatableVector2(nameof(obj.Offset), obj.Animators, obj.Offset))
        //    {
        //        return item;
        //    }

        //    foreach (var item in FromAnimatableScalar(nameof(obj.RotationAngleInDegrees), obj.Animators, obj.RotationAngleInDegrees))
        //    {
        //        return item;
        //    }

        //    foreach (var item in FromAnimatableVector2(nameof(obj.Scale), obj.Animators, obj.Scale))
        //    {
        //        return item;
        //    }
        //}
        YamlMap AddIGeometrySource2DProperty(string name, Wg.IGeometrySource2D obj, YamlMap result)
        {
            if (obj != null)
            {
                result.Add(name, FromIGeometrySource2D(obj));
            }

            return result;
        }

        YamlMap FromIGeometrySource2D(Wg.IGeometrySource2D obj)
        {
            var result = new YamlMap();

            if (obj is Mgcg.CanvasGeometry)
            {
                result.Add("Type", "CanvasGeometry");
            }
            else
            {
                // No other types are currently supported.
                throw new InvalidOperationException();
            }

            return result;
        }

        YamlMap FromCompositionPropertySet(CompositionPropertySet obj, IEnumerable<CompositionObject.Animator> animators, YamlMap result)
        {
            return result;

            //return new XElement("PropertySet", GetContents());
            //IEnumerable<XObject> GetContents()
            //{
            //    foreach (var prop in obj.ScalarProperties)
            //    {
            //        foreach (var item in FromAnimatableScalar(prop.Key, animators, prop.Value))
            //        {
            //            return item;
            //        }
            //    }

            //    foreach (var prop in obj.Vector2Properties)
            //    {
            //        foreach (var item in FromAnimatableVector2(prop.Key, animators, prop.Value))
            //        {
            //            return item;
            //        }
            //    }
            //}
        }

        YamlMap FromAnimation<T>(string name, CompositionAnimation animation, T? initialValue, YamlMap result)
            where T : struct
        {
            switch (animation.Type)
            {
                case CompositionObjectType.ExpressionAnimation:
                    return FromExpressionAnimation((ExpressionAnimation)animation, result);
                case CompositionObjectType.ColorKeyFrameAnimation:
                case CompositionObjectType.PathKeyFrameAnimation:
                case CompositionObjectType.ScalarKeyFrameAnimation:
                case CompositionObjectType.Vector2KeyFrameAnimation:
                case CompositionObjectType.Vector3KeyFrameAnimation:
                    return FromKeyFrameAnimation(name, (KeyFrameAnimation<T>)animation, initialValue, result);
                default:
                    throw new InvalidOperationException();
            }
        }

        YamlMap FromExpressionAnimation(ExpressionAnimation obj, YamlMap result)
        {
            return result;

            //return new XElement(name ?? GetCompositionObjectName(obj), GetContents());
            //IEnumerable<XObject> GetContents()
            //{
            //    foreach (var item in GetCompositionObjectContents(obj))
            //    {
            //        return item;
            //    }

            //    if (obj.Target != null && obj.Target != name)
            //    {
            //        return new XAttribute("Target", obj.Target);
            //    }

            //    return new XText(obj.Expression.ToString());
            //}
        }

        YamlMap FromKeyFrameAnimation<T>(string name, KeyFrameAnimation<T> obj, T? initialValue, YamlMap result)
            where T : struct
        {
            return result;

            //return new XElement(name, GetContents());
            //IEnumerable<XObject> GetContents()
            //{
            //    if (obj.Target != null && obj.Target != name)
            //    {
            //        return new XAttribute("Target", obj.Target);
            //    }

            //    var keyFramesString = string.Join(", ", obj.KeyFrames.Select(kf => $"({GetKeyFrameValue(kf)}@{kf.Progress})"));

            //    if (initialValue.HasValue)
            //    {
            //        return new XText($"{initialValue}, {keyFramesString}");
            //    }
            //    else
            //    {
            //        return new XText(keyFramesString);
            //    }
            //}
        }

        static string GetKeyFrameValue<T>(KeyFrameAnimation<T>.KeyFrame kf)
        {
            switch (kf.Type)
            {
                case KeyFrameAnimation<T>.KeyFrameType.Expression:
                    var expressionKeyFrame = (KeyFrameAnimation<T>.ExpressionKeyFrame)kf;
                    return $"\"{expressionKeyFrame.Expression}\"";
                case KeyFrameAnimation<T>.KeyFrameType.Value:
                    var valueKeyFrame = (KeyFrameAnimation<T>.ValueKeyFrame)kf;
                    return valueKeyFrame.Value.ToString();
                default:
                    throw new InvalidOperationException();
            }
        }

        //IEnumerable<XObject> FromAnimatableScalar(string name, IEnumerable<CompositionObject.Animator> animators, float? initialValue)
        //{
        //    var animation = animators.Where(a => a.AnimatedProperty == name).FirstOrDefault()?.Animation;

        //    if (animation != null)
        //    {
        //        return FromAnimation(name, animation, initialValue);
        //    }
        //    else
        //    {
        //        if (initialValue.HasValue)
        //        {
        //            return FromScalar(name, initialValue.Value);
        //        }
        //    }
        //}

        //IEnumerable<XObject> FromAnimatableVector2(string name, IEnumerable<CompositionObject.Animator> animators, Vector2? initialValue)
        //{
        //    var animation = animators.Where(a => a.AnimatedProperty == name).FirstOrDefault()?.Animation;

        //    if (animation != null)
        //    {
        //        return FromAnimation(name, animation, initialValue);
        //    }
        //    else
        //    {
        //        if (initialValue.HasValue)
        //        {
        //            return FromVector2(name, initialValue.Value);
        //        }
        //    }
        //}

        //IEnumerable<XObject> FromAnimatableVector3(string name, IEnumerable<CompositionObject.Animator> animators, Vector3? initialValue)
        //{
        //    var animation = animators.Where(a => a.AnimatedProperty == name).FirstOrDefault()?.Animation;

        //    if (animation != null)
        //    {
        //        return FromAnimation(name, animation, initialValue);
        //    }
        //    else
        //    {
        //        if (initialValue.HasValue)
        //        {
        //            return FromVector3(name, initialValue.Value);
        //        }
        //    }
        //}

        //YamlMap FromScalar(string name, float value)
        //{
        //    return new XElement(name, new XAttribute("ScalarValue", value));
        //}
        YamlMap FromVector2DefaultZero(string name, Vector2? obj) => FromVector2(name, obj, new Vector2(0, 0));

        YamlMap FromVector2DefaultOne(string name, Vector2? obj) => FromVector2(name, obj, new Vector2(1, 1));

        YamlMap FromVector2(string name, Vector2? obj, Vector2 defaultIfNull)
            => FromVector2(name, obj.HasValue ? obj.Value : defaultIfNull);

        YamlMap FromVector2(string name, Vector2 obj)
        {
            return new YamlMap
            {
                { "Name", name },
                { nameof(obj.X), obj.X },
                { nameof(obj.Y), obj.Y },
            };
        }

        YamlMap AddVector2PropertyDefault0(string name, Vector2? obj, YamlMap result)
            => AddVector2PropertyDefaultN(name, obj, result, 0);

        YamlMap AddVector2PropertyDefault1(string name, Vector2? obj, YamlMap result)
            => AddVector2PropertyDefaultN(name, obj, result, 1);

        YamlMap AddVector2PropertyDefaultN(string name, Vector2? obj, YamlMap result, float n)
        {
            if (obj.HasValue && obj.Value.X != n && obj.Value.Y != n)
            {
                AddVector2Property(name, obj, result);
            }

            return result;
        }

        YamlMap AddVector2Property(string name, Vector2? obj, YamlMap result)
        {
            if (obj.HasValue)
            {
                result.Add(name, new YamlMap
                {
                    { nameof(obj.Value.X), obj.Value.X },
                    { nameof(obj.Value.Y), obj.Value.Y },
                });
            }

            return result;
        }

        YamlMap AddVector3Property(string name, Vector3? obj, YamlMap result)
        {
            if (obj.HasValue)
            {
                result.Add(name, new YamlMap
                {
                    { nameof(obj.Value.X), obj.Value.X },
                    { nameof(obj.Value.Y), obj.Value.Y },
                    { nameof(obj.Value.Z), obj.Value.Z },
                });
            }

            return result;
        }

        YamlMap AddMatrix3x2Property(string name, Matrix3x2? obj, YamlMap result)
        {
            if (obj.HasValue && !obj.Value.IsIdentity)
            {
                result.Add(name, new YamlMap
                {
                    { nameof(obj.Value.M11), obj.Value.M11 },
                    { nameof(obj.Value.M12), obj.Value.M12 },
                    { nameof(obj.Value.M21), obj.Value.M21 },
                    { nameof(obj.Value.M22), obj.Value.M22 },
                    { nameof(obj.Value.M31), obj.Value.M31 },
                    { nameof(obj.Value.M32), obj.Value.M32 },
                });
            }

            return result;
        }

        YamlMap AddMatrix4x4Property(string name, Matrix4x4? obj, YamlMap result)
        {
            if (obj.HasValue && !obj.Value.IsIdentity)
            {
                result.Add(name, new YamlMap
                {
                    { nameof(obj.Value.M11), obj.Value.M11 },
                    { nameof(obj.Value.M12), obj.Value.M12 },
                    { nameof(obj.Value.M13), obj.Value.M13 },
                    { nameof(obj.Value.M14), obj.Value.M14 },
                    { nameof(obj.Value.M21), obj.Value.M21 },
                    { nameof(obj.Value.M22), obj.Value.M22 },
                    { nameof(obj.Value.M23), obj.Value.M23 },
                    { nameof(obj.Value.M24), obj.Value.M24 },
                    { nameof(obj.Value.M31), obj.Value.M31 },
                    { nameof(obj.Value.M32), obj.Value.M32 },
                    { nameof(obj.Value.M33), obj.Value.M33 },
                    { nameof(obj.Value.M34), obj.Value.M34 },
                    { nameof(obj.Value.M41), obj.Value.M41 },
                    { nameof(obj.Value.M42), obj.Value.M42 },
                    { nameof(obj.Value.M43), obj.Value.M43 },
                    { nameof(obj.Value.M44), obj.Value.M44 },
                });
            }

            return result;
        }

        YamlMap AddFloatProperty(string name, float? obj, YamlMap result)
        {
            if (obj.HasValue)
            {
                result.Add(name, obj.Value);
            }

            return result;
        }

        YamlMap AddFloatPropertyDefault0(string name, float? obj, YamlMap result)
            => AddFloatPropertyDefaultN(name, obj, result, 0);

        YamlMap AddFloatPropertyDefault1(string name, float? obj, YamlMap result)
            => AddFloatPropertyDefaultN(name, obj, result, 1);

        YamlMap AddFloatPropertyDefaultN(string name, float? obj, YamlMap result, float defaultValue)
        {
            if (obj.HasValue && obj.Value != defaultValue)
            {
                AddFloatProperty(name, obj, result);
            }

            return result;
        }

        YamlMap AddSequenceProperty(string name, IEnumerable<CompositionObject> items, YamlMap result)
        {
            if (items.Any())
            {
                result.Add(name, YamlSequence.FromSequence(items.Select(i => FromCompositionObject(i))));
            }

            return result;
        }

        YamlMap AddCompositionObjectProperty(string name, CompositionObject obj, YamlMap result)
        {
            if (obj != null)
            {
                result.Add(name, FromCompositionObject(obj));
            }

            return result;
        }

        YamlMap AddCompositionBrushProperty(string name, CompositionBrush obj, YamlMap result)
        {
            if (obj != null)
            {
                switch (obj.Type)
                {
                    case CompositionObjectType.CompositionColorBrush:
                        AddColorProperty(name, ((CompositionColorBrush)obj).Color, result);
                        break;
                    case CompositionObjectType.CompositionEffectBrush:
                    case CompositionObjectType.CompositionSurfaceBrush:
                        result.Add(name, FromCompositionObject(obj));
                        break;
                    default:
                        throw new InvalidOperationException();
                }
            }

            return result;
        }

        YamlMap AddColorProperty(string name, Color color, YamlMap result)
        {
            result.Add(name, color.Name);
            return result;
        }
    }
}