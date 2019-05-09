// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections;
using System.Collections.Generic;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.YamlData
{
    /// <summary>
    /// A sequence of objects.
    /// </summary>
#if PUBLIC_YamlData
    public
#endif
    sealed class YamlSequence : YamlObject, IEnumerable<YamlObject>
    {
        readonly List<YamlObject> _sequence = new List<YamlObject>();

        /// <summary>
        /// Initializes a new instance of the <see cref="YamlSequence"/> class.
        /// </summary>
        public YamlSequence()
        {
        }

        YamlSequence(IEnumerable<YamlObject> items)
        {
            _sequence.AddRange(items);
        }

        /// <summary>
        /// Appends the given object to the sequence.
        /// </summary>
        /// <param name="obj">The object to append.</param>
        public void Add(YamlObject obj)
        {
            _sequence.Add(obj);
        }

        /// <summary>
        /// Returns a new <see cref="YamlSequence"/> containing the given items.
        /// </summary>
        /// <param name="items">The inital set of items.</param>
        /// <returns>A <see cref="YamlSequence"/> containing the given items.</returns>
        public static YamlSequence FromSequence(IEnumerable<YamlObject> items)
        {
            var result = new YamlSequence();
            result._sequence.AddRange(items);
            return result;
        }

        internal override YamlObjectKind Kind => YamlObjectKind.Sequence;

        IEnumerator<YamlObject> IEnumerable<YamlObject>.GetEnumerator()
        {
            return ((IEnumerable<YamlObject>)_sequence).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<YamlObject>)this).GetEnumerator();
    }
}