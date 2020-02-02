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
        Asset ParseAsset(ref Utf8JsonReader reader)
        {
            ExpectToken(ref reader, JsonTokenType.StartObject);

            int e = 0;
            string id = null;

            double width = 0.0;
            double height = 0.0;
            string imagePath = null;
            string fileName = null;
            Layer[] layers = null;

            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.PropertyName:
                        {
                            var currentProperty = reader.GetString();
                            ConsumeToken(ref reader);

                            switch (currentProperty)
                            {
                                case "e":
                                    // TODO: unknown what this is. It shows up in image assets.
                                    e = ParseInt(ref reader);
                                    break;
                                case "h":
                                    height = ParseDouble(ref reader);
                                    break;
                                case "id":
                                    // Older lotties use a string. New lotties use an int. Handle either as strings.
                                    switch (reader.TokenType)
                                    {
                                        case JsonTokenType.String:
                                            id = reader.GetString();
                                            break;
                                        case JsonTokenType.Number:
                                            id = ParseInt(ref reader).ToString();
                                            break;
                                        default:
                                            throw UnexpectedTokenException(ref reader);
                                    }

                                    break;
                                case "layers":
                                    //layers = ParseLayers(ref reader).ToArray();
                                    reader.Skip();
                                    break;
                                case "p":
                                    fileName = reader.GetString();
                                    break;
                                case "u":
                                    imagePath = reader.GetString();
                                    break;
                                case "w":
                                    width = ParseDouble(ref reader);
                                    break;

                                // Report but ignore unexpected fields.
                                case "xt":
                                case "nm":
                                default:
                                    _issues.UnexpectedField(currentProperty);
                                    reader.Skip();
                                    break;
                            }
                        }

                        break;
                    case JsonTokenType.EndObject:
                        {
                            if (id is null)
                            {
                                throw Exception("Asset with no id", ref reader);
                            }

                            if (layers is object)
                            {
                                return new LayerCollectionAsset(id, new LayerCollection(layers));
                            }
                            else if (imagePath != null && fileName != null)
                            {
                                //return CreateImageAsset(id, width, height, imagePath, fileName);
                                return null;
                            }
                            else
                            {
                                _issues.AssetType("NaN");
                                return null;
                            }
                        }

                    default: throw UnexpectedTokenException(ref reader);
                }
            }

            throw EofException;
        }
    }
}
