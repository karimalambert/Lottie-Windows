// Copyright(c) Microsoft Corporation.All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie.UIData.CodeGen;

namespace CodeGen
{
    sealed class ThemePropertiesMonospaceTableFormatter : MonospaceTableFormatter
    {
        internal static string[] GetThemePropertyDescriptionLines(IEnumerable<PropertyBinding> names)
        {
            if (names is null)
            {
                return Array.Empty<string>();
            }

            var header = new[] {
                ("Theme property", TextAlignment.Center, 1),
                ("Type", TextAlignment.Center, 1),
                ("Exposed as", TextAlignment.Center, 1),
            };

            var records =
                (from name in names
                 select new[] {
                     (name.Name, TextAlignment.Left, 1),
                     (name.ExposedType.ToString(), TextAlignment.Center, 1),
                     (name.ActualType.ToString(), TextAlignment.Center, 1),
                 }).ToArray();

            return GetTableLines(new[] { header }, records).ToArray();
        }
    }
}
