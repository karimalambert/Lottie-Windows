// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma warning disable SA1601 // Partial elements should be documented
#pragma warning disable SA1205 // Partial elements should declare access

using System;
using System.Collections.Generic;
using System.Text.Json;
using PathGeometry = Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Sequence<Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.BezierSegment>;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Serialization
{
    sealed partial class TestJsonReader
    {
        static readonly AnimatableParser<double> s_animatableFloatParser = new SimpleAnimatableParser<double>(ReadDoubleOr0);
        static readonly AnimatableParser<Opacity> s_animatableOpacityParser = new SimpleAnimatableParser<Opacity>(ReadOpacity);
        static readonly AnimatableParser<Rotation> s_animatableRotationParser = new SimpleAnimatableParser<Rotation>(ReadRotation);
        static readonly AnimatableParser<Vector2> s_animatableVector2Parser = new SimpleAnimatableParser<Vector2>(ReadVector2FromJsonArray);
        static readonly AnimatableVector3Parser s_animatableVector3Parser = new AnimatableVector3Parser();
        static readonly AnimatableParser<PathGeometry> s_animatableGeometryParser = new AnimatableGeometryParser();
        static readonly Animatable<double> s_animatable_0 = new Animatable<double>(0, null);
        readonly AnimatableColorParser _animatableColorParser;


        sealed class SimpleAnimatableParser<T> : AnimatableParser<T>
            where T : IEquatable<T>
        {
            readonly Reader<T> _reader;

            internal SimpleAnimatableParser(Reader<T> reader)
            {
                _reader = reader;
            }

            protected override T ReadValue(in JsonElement obj) => _reader(in obj);
        }

        abstract class AnimatableParser<T>
            where T : IEquatable<T>
        {
            private protected AnimatableParser()
            {
            }

            protected abstract T ReadValue(in JsonElement obj);

            internal void ParseJson(TestJsonReader reader, in JsonElement obj, out IEnumerable<KeyFrame<T>> keyFrames, out T initialValue)
            {
                // Deprecated "a" field meant "isAnimated". The existence of key frames means the same thing.
                reader.IgnoreFieldIntentionally(in obj, "a");

                keyFrames = Array.Empty<KeyFrame<T>>();
                initialValue = default(T);

                foreach (var field in obj)
                {
                    switch (field.Key)
                    {
                        case "k":
                            {
                                var k = field.Value;
                                if (k.Type == JTokenType.Array)
                                {
                                    var kArray = k.AsArray();
                                    if (HasKeyframes(kArray))
                                    {
                                        keyFrames = ReadKeyFrames(reader, kArray).ToArray();
                                        initialValue = keyFrames.First().Value;
                                    }
                                }

                                if (keyFrames == Array.Empty<KeyFrame<T>>())
                                {
                                    initialValue = ReadValue(k);
                                }
                            }

                            break;

                        // Defines if property is animated. 0 or 1.
                        // Currently ignored because we derive this from the existence of keyframes.
                        case "a":
                            break;

                        // Property index. Used for expressions. Currently ignored because we don't support expressions.
                        case "ix":
                            // Do not report it as an issue - existence of "ix" doesn't mean that an expression is actually used.
                            break;

                        // Extremely rare fields seen in 1 Lottie file. Ignore.
                        case "nm": // Name
                        case "mn": // MatchName
                        case "hd": // IsHidden
                            break;

                        // Property expression. Currently ignored because we don't support expressions.
                        case "x":
                            reader._issues.Expressions();
                            break;
                        default:
                            reader._issues.UnexpectedField(field.Key);
                            break;
                    }
                }
            }

            static bool HasKeyframes(JArray array)
            {
                var firstItem = array[0];
                return firstItem.Type == JTokenType.Object && firstItem.AsObject().ContainsKey("t");
            }

            IEnumerable<KeyFrame<T>> ReadKeyFrames(LottieCompositionReader reader, JArray jsonArray)
            {
                int count = jsonArray.Count;

                if (count == 0)
                {
                    yield break;
                }

                // -
                // Keyframes are encoded in Lottie as an array consisting of a sequence
                // of start value with start frame and easing function. The final entry in the
                // array is the frame at which the last interpolation ends.
                // [
                //   { startValue_1, startFrame_1 },  # interpolates from startValue_1 to startValue_2 from startFrame_1 to startFrame_2
                //   { startValue_2, startFrame_2 },  # interpolates from startValue_2 to startValue_3 from startFrame_2 to startFrame_3
                //   { startValue_3, startFrame_3 },  # interpolates from startValue_3 to startValue_4 from startFrame_3 to startFrame_4
                //   { startValue_4, startFrame_4 }
                // ]
                // Earlier versions of Bodymovin used an endValue in each key frame.
                // [
                //   { startValue_1, endValue_1, startFrame_1 },  # interpolates from startValue_1 to endValue_1 from startFrame_1 to startFrame_2
                //   { startValue_2, endValue_2, startFrame_2 },  # interpolates from startValue_2 to endValue_2 from startFrame_2 to startFrame_3
                //   { startValue_3, endValue_3, startFrame_3 },  # interpolates from startValue_3 to endValue_3 from startFrame_3 to startFrame_4
                //   { startFrame_4 }
                // ]
                //
                // In order to handle the current and old formats, we detect the presence of the endValue field.
                // If there's an endValue field, the keyframes are using the old format.
                //
                // We convert these to keyframes that match the Windows.UI.Composition notion of a keyframe,
                // which is a triple: {endValue, endTime, easingFunction}.
                // An initial keyframe is created to describe the initial value. It has no easing function.
                //
                // -
                T endValue = default(T);

                // The initial keyframe has the same value as the initial value. Easing therefore doesn't
                // matter, but might as well use hold as it's the simplest (it does not interpolate).
                Easing easing = HoldEasing.Instance;

                // SpatialBeziers.
                var ti = default(Vector3);
                var to = default(Vector3);

                // NOTE: indexing an array with GetObjectAt is faster than enumerating.
                for (int i = 0; i < count; i++)
                {
                    var lottieKeyFrame = jsonArray[i].AsObject();

                    // "n" is a name on the keyframe. It is not useful and has been deprecated in Bodymovin.
                    reader.IgnoreFieldIntentionally(lottieKeyFrame, "n");

                    // Read the start frame.
                    var startFrame = lottieKeyFrame.GetNamedNumber("t", 0);

                    if (i == count - 1)
                    {
                        // This is the final key frame.
                        // If parsing the old format, this key frame will just have the "t" startFrame value.
                        // If parsing the new format, this key frame will also have the "s" startValue.
                        var finalStartValue = lottieKeyFrame.GetNamedValue("s");
                        if (finalStartValue is null)
                        {
                            // Old format.
                            yield return new KeyFrame<T>(startFrame, endValue, to, ti, easing);
                        }
                        else
                        {
                            // New format.
                            yield return new KeyFrame<T>(startFrame, ReadValue(finalStartValue), to, ti, easing);
                        }

                        // No more key frames to read.
                        break;
                    }

                    // Read the start value.
                    var startValue = ReadValue(lottieKeyFrame.GetNamedValue("s"));

                    // Output a keyframe that describes how to interpolate to this start value. The easing information
                    // comes from the previous Lottie keyframe.
                    yield return new KeyFrame<T>(startFrame, startValue, to, ti, easing);

                    // Spatial control points.
                    if (lottieKeyFrame.ContainsKey("ti"))
                    {
                        ti = ReadVector3FromJsonArray(lottieKeyFrame.GetNamedArray("ti"));
                        to = ReadVector3FromJsonArray(lottieKeyFrame.GetNamedArray("to"));
                    }

                    // Get the easing to the end value, and get the end value.
                    if (ReadBool(lottieKeyFrame, "h") == true)
                    {
                        // Hold the current value. The next value comes from the start
                        // of the next entry.
                        easing = HoldEasing.Instance;

                        // Synthesize an endValue. This is only used if this is the final frame.
                        endValue = startValue;
                    }
                    else
                    {
                        // Read the easing function parameters. If there are any parameters, it's a CubicBezierEasing.
                        var cp1Json = lottieKeyFrame.GetNamedObject("o", null);
                        var cp2Json = lottieKeyFrame.GetNamedObject("i", null);
                        if (cp1Json != null && cp2Json != null)
                        {
                            var cp1 = new Vector3(ReadFloat(cp1Json.GetNamedValue("x")), ReadFloat(cp1Json.GetNamedValue("y")), 0);
                            var cp2 = new Vector3(ReadFloat(cp2Json.GetNamedValue("x")), ReadFloat(cp2Json.GetNamedValue("y")), 0);
                            easing = new CubicBezierEasing(cp1, cp2);
                        }
                        else
                        {
                            easing = LinearEasing.Instance;
                        }

                        var endValueObject = lottieKeyFrame.GetNamedValue("e");
                        endValue = endValueObject != null ? ReadValue(endValueObject) : default(T);
                    }

                    // "e" is the end value of a key frame but has been deprecated because it should always be equal
                    // to the start value of the next key frame.
                    reader.IgnoreFieldIntentionally(lottieKeyFrame, "e");

                    reader.AssertAllFieldsRead(lottieKeyFrame);
                }
            }
        }

        sealed class AnimatableGeometryParser : AnimatableParser<PathGeometry>
        {
            protected override PathGeometry ReadValue(in JsonElement value)
            {
                JObject pointsData = null;
                if (value.ValueKind == JsonValueKind.Array)
                {
                    var firstItem = value.AsArray().First();
                    var firstItemAsObject = firstItem.AsObject();
                    if (firstItem.Type == JTokenType.Object && firstItemAsObject.ContainsKey("v"))
                    {
                        pointsData = firstItemAsObject;
                    }
                }
                else if (value.ValueKind == JsonValueKind.Object && value.AsObject().ContainsKey("v"))
                {
                    pointsData = value.AsObject();
                }

                if (pointsData is null)
                {
                    return null;
                }

                var vertices = pointsData.GetNamedArray("v", null);
                var inTangents = pointsData.GetNamedArray("i", null);
                var outTangents = pointsData.GetNamedArray("o", null);
                var isClosed = pointsData.GetNamedBoolean("c", false);

                if (vertices is null || inTangents is null || outTangents is null)
                {
                    throw new LottieCompositionReaderException($"Unable to process points array or tangents. {pointsData}");
                }

                var beziers = new BezierSegment[isClosed ? vertices.Count : Math.Max(vertices.Count - 1, 0)];

                if (beziers.Length > 0)
                {
                    // The vertices for the figure.
                    var verticesAsVector2 = ReadVector2Array(vertices);

                    // The control points that define the cubic beziers between the vertices.
                    var inTangentsAsVector2 = ReadVector2Array(inTangents);
                    var outTangentsAsVector2 = ReadVector2Array(outTangents);

                    if (verticesAsVector2.Length != inTangentsAsVector2.Length ||
                        verticesAsVector2.Length != outTangentsAsVector2.Length)
                    {
                        throw new LottieCompositionReaderException($"Invalid path data. {pointsData}");
                    }

                    var cp3 = verticesAsVector2[0];

                    for (var i = 0; i < beziers.Length; i++)
                    {
                        // cp0 is the start point of the segment.
                        var cp0 = cp3;

                        // cp1 is relative to cp0
                        var cp1 = cp0 + outTangentsAsVector2[i];

                        // cp3 is the endpoint of the segment.
                        cp3 = verticesAsVector2[(i + 1) % verticesAsVector2.Length];

                        // cp2 is relative to cp3
                        var cp2 = cp3 + inTangentsAsVector2[(i + 1) % inTangentsAsVector2.Length];

                        beziers[i] = new BezierSegment(
                            cp0: cp0,
                            cp1: cp1,
                            cp2: cp2,
                            cp3: cp3);
                    }
                }

                return new PathGeometry(beziers);
            }

            Vector2[] ReadVector2Array(in JsonElement array)
            {
                var list = EmptySentinels<Vector2>.EmptyList;

                foreach (var item in array.EnumerateArray())
                {
                    if (list == EmptySentinels<Vector2>.EmptyList)
                    {
                        list = new List<Vector2>();
                    }

                    list.Add(ReadVector2FromJsonArray(in item));
                }

                return list.ToArray();
            }
        }
    }
}