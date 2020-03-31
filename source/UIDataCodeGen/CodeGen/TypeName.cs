// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Toolkit.Uwp.UI.Lottie.UIData.CodeGen
{
    /// <summary>
    /// The name of a type.
    /// </summary>
#if PUBLIC_UIDataCodeGen
    public
#endif
    sealed class TypeName
    {
        readonly string _namespaceName;

        internal TypeName(string namespaceName, string unqualifiedName)
        {
            _namespaceName = namespaceName;
            UnqualifiedName = unqualifiedName;
        }

        public string UnqualifiedName { get; }

        public string GetNamespace(Stringifier stringifier)
        {
            return stringifier.Namespace(_namespaceName);
        }
    }
}