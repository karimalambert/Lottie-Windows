// Copyright(c) Microsoft Corporation.All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie.LottieMetadata;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.UIData.CodeGen.Tables
{
    sealed class LottieMarkersMonospaceTableFormatter : MonospaceTableFormatter
    {
        internal static string[] GetMarkersDescriptionLines(
            Stringifier stringifier,
            IEnumerable<(Marker marker, string startConstant, string endConstant)> markers)
        {
            var uberHeader = new[] {
                ColumnData.Create("Marker"),
                ColumnData.Create("Constant", 2),
                ColumnData.Empty,
                ColumnData.Create("Start", 2),
                ColumnData.Empty,
                ColumnData.Create("Duration"),
                ColumnData.Create("Progress", 2),
                ColumnData.Empty,
            };

            var header = new[] {
                ColumnData.Empty,
                ColumnData.Create("start", TextAlignment.Right),
                ColumnData.Create("end", TextAlignment.Left),
                ColumnData.Create("Frame", TextAlignment.Right),
                ColumnData.Create("mS", TextAlignment.Right),
                ColumnData.Create("mS", TextAlignment.Right),
                ColumnData.Create("start", TextAlignment.Right),
                ColumnData.Create("end", TextAlignment.Left),
            };

            var records =
                (from m in markers
                 let marker = m.marker
                 select new[]
                 {
                     ColumnData.Create(marker.Name, TextAlignment.Left),
                     ColumnData.Create(m.startConstant, TextAlignment.Left),
                     ColumnData.Create(m.endConstant, TextAlignment.Left),
                     ColumnData.Create(marker.Frame.Number),
                     ColumnData.Create(marker.Frame.Time.TotalMilliseconds),
                     ColumnData.Create(marker.Duration.Time.TotalMilliseconds),
                     ColumnData.Create(stringifier.Float(marker.Frame.Progress), TextAlignment.Left),
                     ColumnData.Create(stringifier.Float((marker.Frame + marker.Duration).Progress), TextAlignment.Left),
                 }).ToArray();

            return GetTableLines(new[] { uberHeader, header }, records).ToArray();
        }
    }
}
