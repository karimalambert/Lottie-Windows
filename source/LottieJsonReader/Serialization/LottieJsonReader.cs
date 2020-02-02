// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma warning disable SA1601 // Partial elements should be documented
#pragma warning disable SA1205 // Partial elements should declare access

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Microsoft.Toolkit.Uwp.UI.Lottie.GenericData;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieData.Serialization
{
    // See: https://github.com/airbnb/lottie-web/tree/master/docs/json for the (usually out-of-date) schema.
    // See: https://helpx.adobe.com/pdf/after_effects_reference.pdf for the After Effects semantics.
#if PUBLIC
    public
#endif
    sealed partial class TestJsonReader
    {
        delegate T? ElementReader<T>(in JsonElement element)
            where T : struct;

        delegate T ElementRefReader<T>(in JsonElement element)
            where T : class;

        delegate T Parser<T>(ref Utf8JsonReader reader);

        delegate T Reader<T>(in JsonElement element);

        static class EmptySentinels<T>
        {
            internal static List<T> EmptyList { get; } = new List<T>();
        }

        readonly Options _options;
        readonly ParsingIssues _issues = new ParsingIssues(throwOnIssue: false);

        /// <summary>
        /// Specifies optional behavior for the reader.
        /// </summary>
        [Flags]
        public enum Options
        {
            None = 0,

            /// <summary>
            /// Do not ignore the alpha channel when reading color values from arrays.
            /// </summary>
            /// <description>
            /// Lottie files produced by BodyMovin include an alpha channel value that
            /// is ignored by renderers. By default the <see cref="LottieCompositionReader" />
            /// will set the alpha channel to 1.0. By enabling this option the alpha channel
            /// will be set to whatever is in the Lottie file. This option does not apply to
            /// color values read from hex strings.
            /// </description>
            DoNotIgnoreAlpha = 1,

            /// <summary>
            /// Do not read the Name values.
            /// </summary>
            IgnoreNames = 2,

            /// <summary>
            /// Do not read the Match Name values.
            /// </summary>
            IgnoreMatchNames = 4,
        }

        /// <summary>
        /// Parses a Lottie file to create a <see cref="LottieComposition"/>.
        /// </summary>
        /// <returns>A <see cref="LottieComposition"/> read from the json stream.</returns>
        public static LottieComposition ReadLottieCompositionFromJsonStream(
            Stream stream,
            Options options,
            out IReadOnlyList<(string Code, string Description)> issues)
        {
            issues = Array.Empty<(string Code, string Description)>();

            var reader = new TestJsonReader(options);
            var streamReader = new StreamReader(stream);
            var jsonString = streamReader.ReadToEnd();
            var jsonBytes = Encoding.UTF8.GetBytes(jsonString);
            var jsonReader = new Utf8JsonReader(jsonBytes, isFinalBlock: true, state: default);
            return reader.ParseLottieComposition(ref jsonReader);
        }

        TestJsonReader(Options options)
        {
            _options = options;
        }

        LottieComposition ParseLottieComposition(ref Utf8JsonReader reader)
        {
            string version = null;
            double? framesPerSecond = null;
            double? inPoint = null;
            double? outPoint = null;
            double? width = null;
            double? height = null;
            string name = null;

            bool? is3d = null;
            var assets = Array.Empty<Asset>();
            var chars = Array.Empty<Char>();
            var fonts = Array.Empty<Font>();
            var layers = Array.Empty<Layer>();
            var markers = Array.Empty<Marker>();
            Dictionary<string, GenericDataObject> extraData = null;

            ConsumeToken(ref reader);

            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    default:
                        // Here means the JSON was invalid or our parser got confused. There is no way to
                        // recover from this, so throw.
                        throw UnexpectedTokenException(ref reader);

                    case JsonTokenType.Comment:
                        // Ignore comments.
                        ConsumeToken(ref reader);
                        break;

                    case JsonTokenType.PropertyName:
                        {
                            var currentProperty = reader.GetString();

                            ConsumeToken(ref reader);

                            switch (currentProperty)
                            {
                                case "assets":
                                    assets = ParseArrayOf(ref reader, ParseAsset);
                                    break;
                                case "chars":
                                    //chars = ParseArrayOf(ref reader, ParseChar);
                                    break;
                                case "ddd":
                                    is3d = ParseBool(ref reader);
                                    break;
                                case "fr":
                                    framesPerSecond = ParseDouble(ref reader);
                                    break;
                                case "fonts":
                                    //fonts = ParseFonts(ref reader).ToArray();
                                    break;
                                case "layers":
                                    layers = ParseArrayOf(ref reader, ReadLayer);
                                    break;
                                case "h":
                                    height = ParseDouble(ref reader);
                                    break;
                                case "ip":
                                    inPoint = ParseDouble(ref reader);
                                    break;
                                case "op":
                                    outPoint = ParseDouble(ref reader);
                                    break;
                                case "markers":
                                    markers = ParseArrayOf(ref reader, ParseMarker);
                                    break;
                                case "nm":
                                    name = reader.GetString();
                                    break;
                                case "v":
                                    version = reader.GetString();
                                    break;
                                case "w":
                                    width = ParseDouble(ref reader);
                                    break;

                                // Treat any other property as an extension of the BodyMovin format.
                                default:
                                    _issues.UnexpectedField(currentProperty);
                                    if (extraData is null)
                                    {
                                        extraData = new Dictionary<string, GenericDataObject>();
                                    }

                                    //extraData.Add(currentProperty, JsonToGenericData.JTokenToGenericData(JToken.Load(reader, s_jsonLoadSettings)));
                                    break;
                            }
                        }

                        break;
                    case JsonTokenType.EndObject:
                        {
                            // Check that the required fields were found. If any are missing, throw.
                            if (version is null)
                            {
                                throw Exception("Version parameter not found.", ref reader);
                            }

                            if (!width.HasValue)
                            {
                                throw Exception("Width parameter not found.", ref reader);
                            }

                            if (!height.HasValue)
                            {
                                throw Exception("Height parameter not found.", ref reader);
                            }

                            if (!inPoint.HasValue)
                            {
                                throw Exception("Start frame parameter not found.", ref reader);
                            }

                            if (!outPoint.HasValue)
                            {
                                Exception("End frame parameter not found.", ref reader);
                            }

                            if (layers is null)
                            {
                                throw Exception("No layers found.", ref reader);
                            }

                            int[] versions;
                            try
                            {
                                versions = version.Split('.').Select(int.Parse).ToArray();
                            }
                            catch (FormatException)
                            {
                                // Ignore
                                versions = new[] { 0, 0, 0 };
                            }
                            catch (OverflowException)
                            {
                                // Ignore
                                versions = new[] { 0, 0, 0 };
                            }

                            var result = new LottieComposition(
                                                name: name ?? string.Empty,
                                                width: width ?? 0.0,
                                                height: height ?? 0.0,
                                                inPoint: inPoint ?? 0.0,
                                                outPoint: outPoint ?? 0.0,
                                                framesPerSecond: framesPerSecond ?? 0.0,
                                                is3d: false,
                                                version: new Version(versions[0], versions[1], versions[2]),
                                                assets: new AssetCollection(assets),
                                                chars: chars,
                                                extraData: extraData is null ? GenericDataMap.Empty : GenericDataMap.Create(extraData),
                                                fonts: fonts,
                                                layers: new LayerCollection(layers),
                                                markers: markers);
                            return result;
                        }
                }
            }

            throw EofException;
        }

        static JsonDocument ParseJsonDocument(ref Utf8JsonReader reader)
        {
            if (!JsonDocument.TryParseValue(ref reader, out var document))
            {
                throw UnexpectedTokenException(ref reader);
            }

            return document;
        }

        T[] ParseArrayOf<T>(ref Utf8JsonReader reader, Reader<T> documentReader)
        {
            var jsonDocuments = ParseArrayOf(ref reader, ParseJsonDocument);

            var result = EmptySentinels<T>.EmptyList;
            for (var i = 0; i < jsonDocuments.Length; i++)
            {
                if (ReferenceEquals(result, EmptySentinels<T>.EmptyList))
                {
                    result = new List<T>();
                }

                using (var jsonDocument = jsonDocuments[i])
                {
                    var read = documentReader(jsonDocument.RootElement);

                    if (read != null)
                    {
                        result.Add(read);
                    }
                }
            }

            return result.ToArray();
        }

        T[] ParseArrayOf<T>(ref Utf8JsonReader reader, Parser<T> parser)
        {
            ExpectToken(ref reader, JsonTokenType.StartArray);

            var result = EmptySentinels<T>.EmptyList;
            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.StartObject:
                        if (ReferenceEquals(result, EmptySentinels<T>.EmptyList))
                        {
                            result = new List<T>();
                        }

                        result.Add(parser(ref reader));
                        break;

                    case JsonTokenType.EndArray:
                        return result.ToArray();

                    default:
                        throw UnexpectedTokenException(ref reader);
                }
            }

            throw EofException;
        }

        // We got to the end of the file while still reading. Fatal.
        static LottieCompositionReaderException EofException => Exception("EOF");

        // The JSON is malformed - we found an unexpected token. Fatal.
        static LottieCompositionReaderException UnexpectedTokenException(ref Utf8JsonReader reader) => Exception($"Unexpected token: {reader.TokenType}", ref reader);

        static LottieCompositionReaderException UnexpectedTokenException(in JsonElement element) => Exception($"Unexpected token: {element.ValueKind}");

        static LottieCompositionReaderException Exception(string message) => new LottieCompositionReaderException(message);

        static LottieCompositionReaderException Exception(string message, ref Utf8JsonReader reader) => new LottieCompositionReaderException(message);

        // The code we hit is supposed to be unreachable. This indicates a bug.
        static Exception Unreachable => new InvalidOperationException("Unreachable code executed");

        // Indicates that the given field will not be read because we don't yet support it.
        [Conditional("CheckForUnparsedFields")]
        void IgnoreFieldThatIsNotYetSupported(in JsonElement obj, string fieldName)
        {
        }

        // Indicates that the given field is not read because we don't need to read it.
        [Conditional("CheckForUnparsedFields")]
        void IgnoreFieldIntentionally(in JsonElement obj, string fieldName)
        {
        }
    }
}