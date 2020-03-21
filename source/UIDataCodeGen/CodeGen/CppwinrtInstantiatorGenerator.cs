// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData;
using Microsoft.Toolkit.Uwp.UI.Lottie.WinCompData.MetaData;

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

            var hText = generator.GenerateHeaderText();

            var assetList = generator.GetAssetsList();

            return (cppText, hText, assetList);
        }

        protected override void WritePrivateThemeHeader(CodeBuilder builder)
        {
            // Add a field to hold the theme property set.
            builder.WriteLine($"winrt::{Wuc}::{T.CompositionPropertySet} {SourceInfo.ThemePropertiesFieldName}{{ nullptr }};");

            // Add fields for each of the theme properties.
            foreach (var prop in SourceInfo.SourceMetadata.PropertyBindings)
            {
                if (SourceInfo.GenerateDependencyObject)
                {
                    builder.WriteLine($"static Windows::UI::Xaml::DependencyProperty^ _{prop.Name}Property;");
                    builder.WriteLine($"static void On{prop.Name}Changed(Windows::UI::Xaml::DependencyObject^ d, Windows::UI::Xaml::DependencyPropertyChangedEventArgs^ e);");
                }
                else
                {
                    var exposedTypeName = QualifiedTypeName(prop.ExposedType);

                    var initialValue = prop.ExposedType switch
                    {
                        PropertySetValueType.Color => S.ColorArgs((WinCompData.Wui.Color)prop.DefaultValue),
                        PropertySetValueType.Scalar => S.Float((float)prop.DefaultValue),
                        PropertySetValueType.Vector2 => S.Vector2Args((Vector2)prop.DefaultValue),
                        PropertySetValueType.Vector3 => S.Vector3Args((Vector3)prop.DefaultValue),
                        PropertySetValueType.Vector4 => S.Vector4Args((Vector4)prop.DefaultValue),
                        _ => throw new InvalidOperationException(),
                    };

                    WriteInitializedField(builder, exposedTypeName, $"_theme{prop.Name}", S.VariableInitialization(initialValue));
                }
            }

            builder.WriteLine();
            builder.WriteLine($"winrt::{Wuc}::{T.CompositionPropertySet} EnsureThemeProperties(winrt::{Wuc}::{T.Compositor} compositor);");
            builder.WriteLine();
        }

        protected override void WritePublicConstants(CodeBuilder builder)
        {
            foreach (var c in SourceInfo.PublicConstants)
            {
                builder.WriteLine($"static const float {c.name}{{{S.Float(c.value)}}};");
                builder.WriteLine();
            }
        }

        protected override void WritePublicThemeHeader(CodeBuilder builder)
        {
            // Write properties declarations for each themed property.
            foreach (var prop in SourceInfo.SourceMetadata.PropertyBindings)
            {
                builder.WriteLine($"{QualifiedTypeName(prop.ExposedType)} {prop.Name}();");
                builder.WriteLine($"void {prop.Name}({QualifiedTypeName(prop.ExposedType)} value);");
            }

            // TODO - if IThemedAnimatedVisualSource is enabled then this becomes virtual, otherwise not.
            //builder.WriteLine($"virtual {wuc}::{_typeName.CompositionPropertySet} GetThemeProperties({wuc}::{_typeName.Compositor} compositor);");
            builder.WriteLine($"winrt::{Wuc}::{T.CompositionPropertySet} GetThemeProperties(winrt::{Wuc}::{T.Compositor} compositor);");
            builder.WriteLine();
        }

        protected override void WriteThemePropertyImpls(CodeBuilder builder)
        {
            builder.WriteLine($"{T.CompositionPropertySet} {S.Namespace(SourceInfo.Namespace)}::{SourceClassName}::EnsureThemeProperties({T.Compositor} compositor)");
            builder.OpenScope();
            builder.WriteLine($"if ({SourceInfo.ThemePropertiesFieldName} == nullptr)");
            builder.OpenScope();
            builder.WriteLine($"{SourceInfo.ThemePropertiesFieldName} = compositor{S.Deref}CreatePropertySet();");

            // Initialize the values in the property set.
            foreach (var prop in SourceInfo.SourceMetadata.PropertyBindings)
            {
                WriteThemePropertyInitialization(builder, SourceInfo.ThemePropertiesFieldName, prop);
            }

            builder.CloseScope();
            builder.WriteLine();
            builder.WriteLine($"return {SourceInfo.ThemePropertiesFieldName};");
            builder.CloseScope();
            builder.WriteLine();

            builder.WriteLine($"{T.CompositionPropertySet} {S.Namespace(SourceInfo.Namespace)}::{SourceClassName}::GetThemeProperties({T.Compositor} compositor)");
            builder.OpenScope();
            builder.WriteLine("return EnsureThemeProperties(compositor);");
            builder.CloseScope();
            builder.WriteLine();

            // Write property implementations for each theme property.
            foreach (var prop in SourceInfo.SourceMetadata.PropertyBindings)
            {
                // Write the getter. This just reads the values out of the backing field.
                builder.WriteLine($"{TypeName(prop.ExposedType)} {SourceInfo.Namespace}::{SourceClassName}::{prop.Name}()");
                builder.OpenScope();
                builder.WriteLine($"return _theme{prop.Name};");
                builder.CloseScope();
                builder.WriteLine();

                // Write the setter. This saves to the backing field, and updates the theme property
                // set if one has been created.
                builder.WriteLine($"void {SourceInfo.Namespace}::{SourceClassName}::{prop.Name}({TypeName(prop.ExposedType)} value)");
                builder.OpenScope();
                builder.WriteLine($"_theme{prop.Name} = value;");
                builder.WriteLine("if (_themeProperties != nullptr)");
                builder.OpenScope();
                WriteThemePropertyInitialization(builder, "_themeProperties", prop);
                builder.CloseScope();
                builder.CloseScope();
                builder.WriteLine();
            }
        }
    }
}
