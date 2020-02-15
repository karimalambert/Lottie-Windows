﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.UIData.CodeGen
{
    /// <summary>
    /// Specifies the configuration of a code generators.
    /// </summary>
#if PUBLIC_UIDataCodeGen
    public
#endif
    sealed class CodegenConfiguration
    {
        /// <summary>
        /// The name for the resulting IAnimatedVisualSource implementation.
        /// </summary>
        public string ClassName { get; set; }

        /// <summary>
        /// The namespace for the generated code.
        /// </summary>
        public string Namespace { get; set; }

        /// <summary>
        /// The width of the animated visual.
        /// </summary>
        public double Width { get; set; }

        /// <summary>
        /// The height of the animated visual.
        /// </summary>
        public double Height { get; set; }

        /// <summary>
        /// The duration of the animated visual.
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Determines whether the code generator should disable optimizations. Setting
        /// it to <c>true</c> may make the generated code easier to modify, although
        /// less efficient.
        /// </summary>
        public bool DisableOptimization { get; set; }

        /// <summary>
        /// The object graphs for which source will be generated.
        /// </summary>
        public IReadOnlyList<(CompositionObject graphRoot, uint requiredUapVersion)> ObjectGraphs { get; set; }

        /// <summary>
        /// Information about the source.
        /// </summary>
        public IReadOnlyDictionary<Guid, object> SourceMetadata { get; set; }
    }
}
