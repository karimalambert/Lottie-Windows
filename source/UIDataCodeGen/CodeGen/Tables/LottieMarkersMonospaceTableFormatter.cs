// Copyright(c) Microsoft Corporation.All rights reserved.
// Licensed under the MIT License.

using System;
using System.Linq;
using System.Net;

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
                 let startFrame = marker.frame
                 let durationInFrames = marker.durationInFrames

                 // Ignore markers that refer to frames after the end.
                 where startFrame <= metadata.DurationInFrames

                 let startProgress = startFrame / metadata.DurationInFrames
                 let startMs = startProgress * metadata.Duration.TotalMilliseconds

                 let endFrame = startFrame + durationInFrames
                 let endProgress = endFrame / metadata.DurationInFrames
                 let durationMs = (endProgress - startProgress) * metadata.Duration.TotalMilliseconds

                 let api = durationInFrames <= 0
                         ? $"player{stringifier.Deref}SetProgress({stringifier.Float(startProgress)})"
                         : $"player{stringifier.Deref}PlayAsync({stringifier.Float(startProgress)}, {stringifier.Float(endProgress)}, _)"
                 select new[]
                 {
                     ColumnData.Create(name, TextAlignment.Left),
                     ColumnData.Create(startFrame),
                     ColumnData.Create(startMs),
                     ColumnData.Create(durationMs),
                     ColumnData.Create(api, TextAlignment.Left),
                 }).ToArray();

            return GetTableLines(new[] { uberHeader, header }, records).ToArray();
        }
    }
}
