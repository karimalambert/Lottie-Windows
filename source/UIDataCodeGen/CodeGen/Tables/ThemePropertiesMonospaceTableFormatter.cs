// Copyright(c) Microsoft Corporation.All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.UIData.CodeGen.Tables
{
    sealed class ThemePropertiesMonospaceTableFormatter : MonospaceTableFormatter
    {
        internal static IEnumerable<string> GetThemePropertyDescriptionLines(IEnumerable<PropertyBinding> names)
        {
            if (names is null)
            {
                return Array.Empty<string>();
            }

            var header = new[] {
                Row.HeaderTop,
                new Row.ColumnData(
                        ColumnData.Create("Theme property"),
                        ColumnData.Create("Type"),
                        ColumnData.Create("Exposed as")),
                Row.HeaderBottom,
                };

            var records =
                (from name in names
                 select new Row.ColumnData(
                     ColumnData.Create(name.Name, TextAlignment.Left, 1),
                     ColumnData.Create(name.ExposedType.ToString()),
                     ColumnData.Create(name.ActualType.ToString())
                 )).ToArray();

            var rows = header.Concat(records).Append(Row.BodyBottom);

            return GetTableLines(rows);
        }
    }
}
