// Copyright(c) Microsoft Corporation.All rights reserved.
// Licensed under the MIT License.

using System;
using System.Linq;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.UIData.CodeGen.Tables
{
    sealed class LottieMarkersMonospaceTableFormatter : MonospaceTableFormatter
    {
        internal static string[] GetMarkersDescriptionLines(Stringifier stringifier, SourceMetadata.Lottie metadata)
        {
            var uberHeader = new[] {
                ColumnData.Create("Marker"),
                ColumnData.Create("Start", TextAlignment.Center, 2),
                ColumnData.Empty,
                ColumnData.Create("Duration"),
                ColumnData.Create("Api"),
            };

            var header = new[] {
                ColumnData.Empty,
                ColumnData.Create("Frame", TextAlignment.Right, 1),
                ColumnData.Create("mS", TextAlignment.Right, 1),
                ColumnData.Create("mS", TextAlignment.Right, 1),
                ColumnData.Empty,
            };

            var markers = metadata.Markers;

            var records =
                (from marker in markers
                 let name = marker.name
                 let start = marker.frame
                 let duration = marker.duration

                 // Ignore markers that refer to frames after the end.
                 where start <= metadata.DurationInFrames

                 let progress = start / metadata.DurationInFrames
                 let startMs = progress * metadata.Duration.TotalMilliseconds
                 let endProgress = progress + (duration / metadata.Duration)
                 let api = duration <= TimeSpan.Zero
                         ? $"player{stringifier.Deref}SetProgress({stringifier.Float(progress)})"
                         : $"player{stringifier.Deref}PlayAsync({stringifier.Float(progress)}, {stringifier.Float(endProgress)}, _)"
                 select new[]
                 {
                     ColumnData.Create(name, TextAlignment.Left),
                     ColumnData.Create(start),
                     ColumnData.Create(startMs),
                     ColumnData.Create(duration.TotalMilliseconds),
                     ColumnData.Create(api, TextAlignment.Left),
                 }).ToArray();

            return GetTableLines(new[] { uberHeader, header }, records).ToArray();
        }
    }
}
