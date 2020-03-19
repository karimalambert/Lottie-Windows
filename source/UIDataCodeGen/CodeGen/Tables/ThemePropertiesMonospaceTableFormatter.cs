// Copyright(c) Microsoft Corporation.All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie.UIData.CodeGen;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.UIData.CodeGen.Tables
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
                ColumnData.Create("Theme property"),
                ColumnData.Create("Type"),
                ColumnData.Create("Exposed as"),
            };

            var records =
                (from name in names
                 select new[] {
                     ColumnData.Create(name.Name, TextAlignment.Left, 1),
                     ColumnData.Create(name.ExposedType.ToString()),
                     ColumnData.Create(name.ActualType.ToString()),
                 }).ToArray();

            return GetTableLines(new[] { header }, records).ToArray();
        }
    }
}
