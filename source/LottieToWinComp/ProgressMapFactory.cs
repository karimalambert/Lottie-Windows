﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Toolkit.Uwp.UI.Lottie.LottieData;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.LottieToWinComp
{
    // Creates progress mapping variables and animations. This is used by spatial bezier
    // animations to remap the Progress value of the animation to a smaller range so
    // that there is a value that progresses linearly from 0 to 1 between 2 key frames.
    sealed class ProgressMapFactory
    {
        readonly List<ProgressVariable> _progressVariables = new List<ProgressVariable>();

        readonly TimeSpan _duration;

        int _variableCounter;

        internal ProgressMapFactory(TimeSpan duration)
        {
            _duration = duration;
        }

        // Returns the name of the variable that is animated from 0 to 1 from start to end.
        internal string GetVariableForProgressMapping(float start, float end, Easing easing, double scale, double offset)
        {
            var range = new Range(start, end, easing);

            // Try to add the range to an existing variable.
            foreach (var variable in _progressVariables)
            {
                if (variable.Scale == scale && variable.Offset == offset && variable.TryAddRange(range))
                {
                    // An equivalent range has already been added.
                    return variable.VariableName;
                }
            }

            // There was no room for this range in any existing variable. Create a new variable.
            var newVariable = new ProgressVariable($"t{_variableCounter++}", scale, offset);
            _progressVariables.Add(newVariable);

            var success = newVariable.TryAddRange(range);

            Debug.Assert(success, "Invariant");

            return newVariable.VariableName;
        }

        internal IEnumerable<(string, double scale, double offset, (float rangeStart, float rangeEnd, Easing easing)[])> GetVariables()
        {
            foreach (var entry in _progressVariables)
            {
                var keyframes =
                    (from range in entry.Ranges
                     where range.Easing != null
                     select (range.Start, range.End, range.Easing)).ToArray();

                yield return (entry.VariableName, entry.Scale, entry.Offset, keyframes);
            }
        }

        sealed class ProgressVariable
        {
            Range _firstRange;

            internal ProgressVariable(string name, double scale, double offset)
            {
                VariableName = name;
                Scale = scale;
                Offset = offset;
            }

            internal string VariableName { get; }

            internal double Scale { get; }

            internal double Offset { get; }

            internal IEnumerable<Range> Ranges
            {
                get
                {
                    var cur = _firstRange;
                    while (cur != null)
                    {
                        yield return cur;
                        cur = cur.Next;
                    }
                }
            }

            internal bool TryAddRange(Range range)
            {
                if (_firstRange == null)
                {
                    _firstRange = range;
                    return true;
                }

                var cur = _firstRange;

                while (cur.Start > range.Start && cur.Next != null)
                {
                    cur = cur.Next;
                }

                if (cur.Next is null)
                {
                    // Got to the end of the list. Add the range on the end.
                    if (range.Start >= cur.End)
                    {
                        cur.Next = range;
                        return true;
                    }
                    else
                    {
                        // The range overlaps the last range. No room to add.
                        return false;
                    }
                }

                // cur.Start <= range.Start.
                if (cur.Start == range.Start && cur.End == range.End && cur.Easing == range.Easing)
                {
                    // cur is equivalent. No need to add.
                    return true;
                }

                var next = cur.Next;

                if (next.Start == range.Start && next.End == range.End && next.Easing == range.Easing)
                {
                    // next is equivalent. No need to add.
                    return true;
                }

                // Try to insert between cur and next.
                if (cur.End >= range.Start || range.End > next.Start)
                {
                    // No room.
                    return false;
                }

                // Insert the range between cur and next.
                cur.Next = range;
                range.Next = next;

                return true;
            }
        }

        // Describes a range over which remapping is done, and an easing. If the easing
        // is null, the range is currently unused.
        sealed class Range
        {
            internal Range(float start, float end, Easing easing) => (Start, End, Easing) = (start, end, easing);

            internal float Start { get; set; }

            internal float End { get; set; }

            internal Easing Easing { get; set; }

            internal Range Next { get; set; }

            bool Equals(Range other)
                => (!(other is null)) && (other.Start == Start) && (other.End == End) && other.Easing.Equals(Easing);

            public override bool Equals(object obj) => Equals(obj as Range);

            public override int GetHashCode()
                => Easing.GetHashCode() ^ Start.GetHashCode() ^ End.GetHashCode();
        }
    }
}
