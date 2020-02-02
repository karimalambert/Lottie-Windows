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
        Marker ParseMarker(ref Utf8JsonReader reader)
        {
            ExpectToken(ref reader, JsonTokenType.StartObject);

            string name = null;
            double durationMilliseconds = 0;
            double frame = 0;

            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.PropertyName:
                        var currentProperty = reader.GetString();

                        ConsumeToken(ref reader);

                        switch (currentProperty)
                        {
                            case "cm":
                                name = reader.GetString();
                                break;
                            case "dr":
                                durationMilliseconds = ParseDouble(ref reader);
                                break;
                            case "tm":
                                frame = ParseDouble(ref reader);
                                break;
                            default:
                                _issues.IgnoredField(currentProperty);
                                reader.Skip();
                                break;
                        }

                        break;
                    case JsonTokenType.EndObject:
                        return new Marker(name: name, frame: frame, durationMilliseconds: durationMilliseconds);
                    default:
                        throw UnexpectedTokenException(ref reader);
                }
            }

            throw EofException;
        }
    }
}
