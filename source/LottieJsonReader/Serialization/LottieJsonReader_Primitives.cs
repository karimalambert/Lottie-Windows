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
        // Consumes a token from the stream.
        static void ConsumeToken(ref Utf8JsonReader reader)
        {
            if (!reader.Read())
            {
                throw EofException;
            }
        }

        static void ExpectToken(ref Utf8JsonReader reader, JsonTokenType tokenType)
        {
            if (reader.TokenType != tokenType)
            {
                throw UnexpectedTokenException(ref reader);
            }
        }

        static bool ParseBool(ref Utf8JsonReader reader)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.True:
                    return true;
                case JsonTokenType.False:
                    return false;
                case JsonTokenType.Number:
                    if (reader.TryGetInt32(out var intValue))
                    {
                        return intValue != 0;
                    }
                    else if (reader.TryGetDouble(out var doubleValue) ||
                             double.TryParse(reader.GetString(), out doubleValue))
                    {
                        return doubleValue != 0;
                    }

                    break;
            }

            throw Exception($"Expected a bool, but got {reader.TokenType}", ref reader);
        }

        static double ParseDouble(ref Utf8JsonReader reader)
        {
            ExpectToken(ref reader, JsonTokenType.Number);

            if (reader.TryGetDouble(out var doubleValue))
            {
                return doubleValue;
            }
            else if (double.TryParse(reader.GetString(), out var stringDoubleValue))
            {
                return stringDoubleValue;
            }

            throw Exception("Failed to read double", ref reader);
        }

        static int ParseInt(ref Utf8JsonReader reader)
        {
            ExpectToken(ref reader, JsonTokenType.Number);

            if (reader.TryGetInt32(out var intValue))
            {
                return intValue;
            }
            else if (reader.TryGetDouble(out var doubleValue) ||
                     double.TryParse(reader.GetString(), out doubleValue))
            {
                return checked((int)(long)Math.Round(doubleValue));
            }

            throw Exception("Failed to read int", ref reader);
        }

        string ReadName(in JsonElement obj)
        {
            if (_options.HasFlag(Options.IgnoreNames))
            {
                IgnoreFieldIntentionally(in obj, "nm");
                return string.Empty;
            }

            if (!obj.TryGetProperty("nm", out var result))
            {
                return string.Empty;
            }

            return result.GetString();
        }

        string ReadMatchName(in JsonElement obj)
        {
            if (_options.HasFlag(Options.IgnoreMatchNames))
            {
                IgnoreFieldIntentionally(in obj, "mn");
                return string.Empty;
            }

            if (!obj.TryGetProperty("mn", out var result))
            {
                return string.Empty;
            }

            return result.GetString();
        }

        static T ReadProperty<T>(in JsonElement obj, string propertyName, ElementRefReader<T> reader)
            where T : class
        {
            if (obj.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (!obj.TryGetProperty(propertyName, out var valueElement))
            {
                return null;
            }

            return reader(in valueElement);
        }

        static T? ReadProperty<T>(in JsonElement obj, string propertyName, ElementReader<T> reader)
            where T : struct
        {
            if (obj.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (!obj.TryGetProperty(propertyName, out var valueElement))
            {
                return null;
            }

            return reader(in valueElement);
        }

        static T ReadProperty<T>(in JsonElement obj, string propertyName, ElementReader<T> reader, T defaultValue)
            where T : struct
        {
            if (obj.ValueKind != JsonValueKind.Object)
            {
                return defaultValue;
            }

            if (!obj.TryGetProperty(propertyName, out var number))
            {
                return defaultValue;
            }

            return reader(in number) ?? defaultValue;
        }

        // TODO - rename these to ReadBoolProperty/ReadIntProperty/etc.
        static bool? ReadBool(in JsonElement element, string propertyName)
            => ReadProperty(in element, propertyName, ReadBool);

        static bool ReadBool(in JsonElement element, string propertyName, bool defaultValue)
            => ReadProperty(in element, propertyName, ReadBool, defaultValue);

        static double? ReadDouble(in JsonElement element, string propertyName)
            => ReadProperty(in element, propertyName, ReadDouble);

        static double ReadDouble(in JsonElement element, string propertyName, double defaultValue)
            => ReadProperty(in element, propertyName, ReadDouble, defaultValue);

        static int? ReadInt(in JsonElement element, string propertyName)
            => ReadProperty(in element, propertyName, ReadInt);

        static string ReadString(in JsonElement element, string propertyName)
            => ReadProperty(in element, propertyName, ReadString);

        static string ReadString(in JsonElement element)
            => element.GetString();

        static int? ReadInt(in JsonElement number)
        {
            if (number.TryGetInt32(out var intValue))
            {
                return intValue;
            }
            else if (number.TryGetDouble(out var doubleValue) ||
                     double.TryParse(number.GetString(), out doubleValue))
            {
                intValue = unchecked((int)(long)Math.Round(doubleValue));

                if (intValue == doubleValue)
                {
                    return intValue;
                }
            }

            return null;
        }

        static bool? ReadBool(in JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Undefined:
                    break;
                case JsonValueKind.Object:
                    break;
                case JsonValueKind.Array:
                    break;
                case JsonValueKind.String:
                    break;
                case JsonValueKind.Number:
                    // TODO - should this be != 0?
                    return ReadInt(in element)?.Equals(1);
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                case JsonValueKind.Null:
                    return false;
                default:
                    break;
            }

            throw UnexpectedTokenException(in element);
        }

        static double? ReadDouble(in JsonElement number)
        {
            if (number.TryGetInt32(out var intValue))
            {
                return intValue;
            }
            else if (number.TryGetDouble(out var doubleValue) ||
                     double.TryParse(number.GetString(), out doubleValue))
            {
                return doubleValue;
            }

            return null;
        }

        static double ReadDoubleOr0(in JsonElement number)
            => ReadDouble(in number) ?? 0;

        static Opacity ReadOpacity(in JsonElement number)
            => Opacity.FromFloat(ReadDoubleOr0(in number));

        static Rotation ReadRotation(in JsonElement number)
            => Rotation.FromDegrees(ReadDoubleOr0(in number));

        static Vector2 ReadVector2FromJsonArray(in JsonElement array)
        {
            double x = 0;
            double y = 0;
            int i = 0;

            // Allow any number of values to be specified. Assume 0 for any missing values.
            foreach (var item in array.EnumerateArray())
            {
                var number = ReadDoubleOr0(in item);
                switch (i)
                {
                    case 0:
                        x = number;
                        break;
                    case 1:
                        y = number;
                        break;
                }

                i++;
            }

            return new Vector2(x, y);
        }
    }
}
