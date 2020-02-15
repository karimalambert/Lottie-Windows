// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData;

namespace Microsoft.Toolkit.Uwp.UI.Lottie.UIData.CodeGen
{
#if PUBLIC_UIDataCodeGen
    public
#endif
    sealed class CppwinrtInstantiatorGenerator : CppInstantiatorGeneratorBase
    {
        CppwinrtInstantiatorGenerator(
            CodegenConfiguration configuration,
            string headerFileName)
            : base(
                  configuration: configuration,
                  setCommentProperties: false,
                  new CppwinrtStringifier(),
                  headerFileName,
                  true)
        {
        }

        /// <summary>
        /// Returns the Cppwinrt code for a factory that will instantiate the given <see cref="Visual"/> as a
        /// Windows.UI.Composition Visual.
        /// </summary>
        /// <returns>A value tuple containing the cpp code, header code, and list of referenced asset files.</returns>
        public static (string cppText, string hText, IEnumerable<Uri> assetList) CreateFactoryCode(
            CodegenConfiguration configuration,
            string headerFileName)
        {
            var generator = new CppwinrtInstantiatorGenerator(
                configuration: configuration,
                headerFileName: headerFileName);

            var cppText = generator.GenerateCode();

            var hText = generator.GenerateHeaderText(generator);

            var assetList = generator.GetAssetsList();

            return (cppText, hText, assetList);
        }
    }
}
