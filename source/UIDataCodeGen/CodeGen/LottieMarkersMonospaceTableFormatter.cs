// Copyright(c) Microsoft Corporation.All rights reserved.
// Licensed under the MIT License.

using System;
using System.Linq;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.UIData.CodeGen
{
    sealed class LottieMarkersMonospaceTableFormatter : MonospaceTableFormatter
    {
        internal static string[] GetMarkersDescriptionLines(Stringifier stringifier, SourceMetadata.Lottie metadata)
        {
            var uberHeader = new[] {
                ("Marker", TextAlignment.Center, 1),
                ("Start", TextAlignment.Center, 2),
                (string.Empty, default, 0),
                ("Duration", TextAlignment.Center, 1),
                ("Api", TextAlignment.Center, 1),
            };

            var header = new[] {
                (string.Empty, TextAlignment.Center, 1),
                ("Frame", TextAlignment.Right, 1),
                ("mS", TextAlignment.Right, 1),
                ("mS", TextAlignment.Right, 1),
                (string.Empty, TextAlignment.Center, 1),
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
                     (name, TextAlignment.Left, 1),
                     (start.ToString("0.0"), TextAlignment.Right, 1),
                     (startMs.ToString("0.0"), TextAlignment.Right, 1),
                     (duration.TotalMilliseconds.ToString("0.0"), TextAlignment.Right, 1),
                     (api, TextAlignment.Left, 1),
                 }).ToArray();

            return GetTableLines(new[] { uberHeader, header }, records).ToArray();
        }
    }
}
