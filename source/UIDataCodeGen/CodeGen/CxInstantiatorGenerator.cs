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
    sealed class CxInstantiatorGenerator : CppInstantiatorGeneratorBase
    {
        CxInstantiatorGenerator(
            CodegenConfiguration configuration,
            string headerFileName)
            : base(
                  configuration: configuration,
                  setCommentProperties: false,
                  new CxStringifier(),
                  headerFileName,
                  false)
        {
        }

        /// <summary>
        /// Returns the Cx code for a factory that will instantiate the given <see cref="Visual"/> as a
        /// Windows.UI.Composition Visual.
        /// </summary>
        /// <returns>A value tuple containing the cpp code, header code, and list of referenced asset files.</returns>
        public static (string cppText, string hText, IEnumerable<Uri> assetList) CreateFactoryCode(
            CodegenConfiguration configuration,
            string headerFileName)
        {
            var generator = new CxInstantiatorGenerator(
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
            builder.WriteLine($"{Wuc}::{T.CompositionPropertySet} {SourceInfo.ThemePropertiesFieldName}{{ nullptr }};");

            // Add fields for each of the theme properties.
            foreach (var prop in SourceInfo.SourceMetadata.PropertyBindings)
            {
                if (SourceInfo.GenerateDependencyObject)
                {
                    builder.WriteLine($"static Windows::UI::Xaml::DependencyProperty^ _{S.CamelCase(prop.Name)}Property;");
                    builder.WriteLine($"static void On{prop.Name}Changed(Windows::UI::Xaml::DependencyObject^ d, Windows::UI::Xaml::DependencyPropertyChangedEventArgs^ e);");
                }
                else
                {
                    var exposedTypeName = QualifiedTypeName(prop.ExposedType);
                    var initialValue = GetDefaultPropertyBindingValue(prop);

                    WriteInitializedField(builder, exposedTypeName, $"_theme{prop.Name}", S.VariableInitialization(initialValue));
                }
            }

            builder.WriteLine($"{Wuc}::{T.CompositionPropertySet} EnsureThemeProperties({Wuc}::{T.Compositor} compositor);");
            builder.WriteLine();
        }

        protected override void WritePublicConstants(CodeBuilder builder)
        {
            foreach (var c in SourceInfo.PublicConstants)
            {
                builder.WriteLine($"static property float {c.name} {{ float get() {{ return {S.Float(c.value)}; }} }}");
                builder.WriteLine();
            }
        }

        protected override void WritePublicThemeHeader(CodeBuilder builder)
        {
            // Write properties declarations for each themed property.
            foreach (var prop in SourceInfo.SourceMetadata.PropertyBindings)
            {
                if (SourceInfo.GenerateDependencyObject)
                {
                    builder.WriteLine($"static Windows::UI::Xaml::DependencyProperty^ {prop.Name}Property();");
                    builder.WriteLine();
                }

                builder.WriteLine($"property {QualifiedTypeName(prop.ExposedType)} {prop.Name}");
                builder.OpenScope();
                builder.WriteLine($"{QualifiedTypeName(prop.ExposedType)} get();");
                builder.WriteLine($"void set ({QualifiedTypeName(prop.ExposedType)} value);");
                builder.CloseScope();
                builder.WriteLine();
            }

            builder.WriteLine($"{(SourceInfo.Interface == null ? string.Empty : "virtual ")}{Wuc}::{T.CompositionPropertySet} GetThemeProperties({Wuc}::{T.Compositor} compositor);");
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
                WriteThemePropertyInitialization(builder, SourceInfo.ThemePropertiesFieldName, prop, prop.Name);
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
                if (SourceInfo.GenerateDependencyObject)
                {
                    // Write the dependency property accessor.
                    builder.WriteLine($"DependencyProperty^ {S.Namespace(SourceInfo.Namespace)}::{SourceClassName}::{prop.Name}Property()");
                    builder.OpenScope();
                    builder.WriteLine($"return _{S.CamelCase(prop.Name)}Property;");
                    builder.CloseScope();
                    builder.WriteLine();

                    // Write the dependency property change handler.
                    builder.WriteLine($"void {S.Namespace(SourceInfo.Namespace)}::{SourceClassName}::On{prop.Name}Changed(DependencyObject^ d, DependencyPropertyChangedEventArgs^ e)");
                    builder.OpenScope();
                    builder.WriteLine($"auto self = ({S.Namespace(SourceInfo.Namespace)}::{SourceClassName}^)d;");
                    builder.WriteLine();
                    builder.WriteLine("if (self->_themeProperties != nullptr)");
                    builder.OpenScope();
                    WriteThemePropertyInitialization(builder, $"self->{SourceInfo.ThemePropertiesFieldName}", prop, "e->NewValue");
                    builder.CloseScope();
                    builder.CloseScope();
                    builder.WriteLine();

                    // Write the dependency property initializer.
                    builder.WriteLine($"DependencyProperty^ {S.Namespace(SourceInfo.Namespace)}::{SourceClassName}::_{S.CamelCase(prop.Name)}Property =");
                    builder.Indent();
                    builder.WriteLine($"DependencyProperty::Register(");
                    builder.Indent();
                    builder.WriteLine($"{S.String(prop.Name)},");
                    builder.WriteLine($"{TypeName(prop.ExposedType)}::typeid,");
                    builder.WriteLine($"{S.Namespace(SourceInfo.Namespace)}::{SourceClassName}::typeid,");
                    builder.WriteLine($"ref new PropertyMetadata({GetDefaultPropertyBindingValue(prop)},");
                    builder.WriteLine($"ref new PropertyChangedCallback(&{S.Namespace(SourceInfo.Namespace)}::{SourceClassName}::On{prop.Name}Changed)));");
                    builder.UnIndent();
                    builder.UnIndent();
                    builder.WriteLine();
                }

                // Write the getter.
                builder.WriteLine($"{TypeName(prop.ExposedType)} {S.Namespace(SourceInfo.Namespace)}::{SourceClassName}::{prop.Name}::get()");
                builder.OpenScope();
                if (SourceInfo.GenerateDependencyObject)
                {
                    // Get the value from the dependency property.
                    builder.WriteLine($"return ({TypeName(prop.ExposedType)})GetValue(_{S.CamelCase(prop.Name)}Property);");
                }
                else
                {
                    // Get the value from the backing field.
                    builder.WriteLine($"return _theme{prop.Name};");
                }

                builder.CloseScope();
                builder.WriteLine();

                // Write the setter.
                builder.WriteLine($"void {S.Namespace(SourceInfo.Namespace)}::{SourceClassName}::{prop.Name}::set({TypeName(prop.ExposedType)} value)");
                builder.OpenScope();
                if (SourceInfo.GenerateDependencyObject)
                {
                    builder.WriteLine($"SetValue(_{S.CamelCase(prop.Name)}Property, value);");
                }
                else
                {
                    // This saves to the backing field, and updates the theme property
                    // set if one has been created.
                    builder.WriteLine($"_theme{prop.Name} = value;");
                    builder.WriteLine("if (_themeProperties != nullptr)");
                    builder.OpenScope();
                    WriteThemePropertyInitialization(builder, "_themeProperties", prop);
                    builder.CloseScope();
                }

                builder.CloseScope();
                builder.WriteLine();
            }
        }

        string GetDefaultPropertyBindingValue(PropertyBinding prop)
             => prop.ExposedType switch
             {
                 PropertySetValueType.Color => $"Windows::UI::ColorHelper::FromArgb({S.ColorArgs((WinCompData.Wui.Color)prop.DefaultValue)})",

                 // Scalars are stored as floats, but exposed as doubles as XAML markup prefers doubles.
                 PropertySetValueType.Scalar => S.Double((float)prop.DefaultValue),
                 PropertySetValueType.Vector2 => S.Vector2((Vector2)prop.DefaultValue),
                 PropertySetValueType.Vector3 => S.Vector3((Vector3)prop.DefaultValue),
                 PropertySetValueType.Vector4 => S.Vector4((Vector4)prop.DefaultValue),
                 _ => throw new InvalidOperationException(),
             };
    }
}
