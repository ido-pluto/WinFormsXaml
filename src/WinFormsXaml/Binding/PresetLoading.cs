using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Xml;
using System.Windows.Forms;

namespace WinFormsXaml
{
    public sealed partial class XamlRuntime
    {
        private static bool IsDisposedTarget(object target)
        {
            Control control = target as Control;
            return control != null && control.IsDisposed;
        }

        private static bool IsEventAttribute(
            object target,
            string attributeName)
        {
            if (target == null ||
                String.IsNullOrEmpty(attributeName) ||
                attributeName.IndexOf('.') >= 0)
            {
                return false;
            }

            Type type = target.GetType();

            // ApplyAttribute gives a CLR property precedence over an event with
            // the same name, so retained bindings must make the same choice.
            if (FindProperty(type, attributeName) != null)
                return false;

            return FindEvent(type, attributeName) != null;
        }

        private void PreReadPresets(XmlElement element)
        {
            if (element == null)
                return;

            // Preset declarations in an ItemTemplate are imported once from the
            // compiled annotated template. Per-row XML clones keep the declarations
            // for diagnostics, but must not merge the same preset document again.
            if (GetCompiledItemTemplateStyleScope(element) != null)
                return;

            XmlNode node = element.FirstChild;

            while (node != null)
            {
                XmlElement child = node as XmlElement;

                if (child != null)
                {
                    if (IsPresetDefinitionElement(child))
                    {
                        LoadPresetDefinition(child);
                    }
                    else if (IsPropertyElement(child))
                    {
                        string propertyName =
                            GetPropertyElementName(child.LocalName);

                        if (EqualsIgnoreCase(propertyName, "Resources"))
                        {
                            PreReadPresets(child);
                        }
                    }
                }

                node = node.NextSibling;
            }
        }

        private void LoadPresetDefinition(XmlElement element)
        {
            if (_loadedPresetElements.ContainsKey(element))
                return;

            _loadedPresetElements[element] = true;

            PresetImportMode importMode =
                _presetManagerWasProvided
                    ? PresetImportMode.PreserveExisting
                    : PresetImportMode.Merge;

            string source =
                GetAttributeIgnoreNamespace(element, "Source");

            if (!String.IsNullOrEmpty(source))
            {
                string sourceKind =
                    GetAttributeIgnoreNamespace(element, "SourceKind");

                if (EqualsIgnoreCase(sourceKind, "EmbeddedResource") ||
                    source.StartsWith(
                        "embedded://",
                        StringComparison.OrdinalIgnoreCase))
                {
                    string resourceName = source;

                    if (source.StartsWith(
                        "embedded://",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        resourceName = source.Substring("embedded://".Length);
                    }

                    Assembly assembly = ResolvePresetAssembly(
                        GetAttributeIgnoreNamespace(element, "Assembly"));

                    _presetManager.LoadEmbeddedResource(
                        assembly,
                        resourceName,
                        importMode);
                }
                else
                {
                    _presetManager.LoadFile(
                        ResolvePath(source),
                        importMode);
                }
            }

            if (HasInlinePresetChildren(element))
                _presetManager.LoadXml(element.OuterXml, importMode);
        }

        private Assembly ResolvePresetAssembly(string assemblyName)
        {
            if (!String.IsNullOrEmpty(assemblyName))
                return Assembly.Load(assemblyName);

            if (_activeMarkupAssembly != null)
                return _activeMarkupAssembly;

            if (_markupAssembly != null)
                return _markupAssembly;

            if (_eventTarget != null)
                return _eventTarget.GetType().Assembly;

            Assembly entry = Assembly.GetEntryAssembly();
            return entry == null ? Assembly.GetExecutingAssembly() : entry;
        }

        private static bool HasInlinePresetChildren(XmlElement element)
        {
            XmlNode node = element.FirstChild;

            while (node != null)
            {
                XmlElement child = node as XmlElement;

                if (child != null &&
                    EqualsIgnoreCase(child.LocalName, "Preset"))
                {
                    return true;
                }

                node = node.NextSibling;
            }

            return false;
        }

        private static bool IsPresetDefinitionElement(XmlElement element)
        {
            return
                element != null &&
                EqualsIgnoreCase(element.LocalName, "Presets");
        }

        private object ResolvePresetValue(
            string setName,
            string key)
        {
            return ResolveReactivePresetValue(setName, key);
        }

        private static bool TryParsePresetExpression(
            string value,
            out string setName,
            out string key)
        {
            setName = null;
            key = null;

            if (String.IsNullOrEmpty(value))
                return false;

            string text = value.Trim();
            const string prefix = "{Preset ";

            if (!IsSingleMarkupExpression(text) ||
                !text.StartsWith(
                    prefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string path =
                text.Substring(
                    prefix.Length,
                    text.Length - prefix.Length - 1).Trim();

            if (LooksLikePresetConditionExpression(path))
                return false;

            int separator = path.IndexOf('.');

            if (separator <= 0 || separator == path.Length - 1)
            {
                throw new FormatException(
                    "Preset expressions use {Preset SetName.Key}.");
            }

            setName = path.Substring(0, separator).Trim();
            key = path.Substring(separator + 1).Trim();
            return true;
        }

        private static bool TryParsePresetExpression(
            string value,
            int expressionStart,
            int expressionEnd,
            out string setName,
            out string key)
        {
            setName = null;
            key = null;

            const string prefix = "{Preset ";

            if (String.IsNullOrEmpty(value) ||
                expressionStart < 0 ||
                expressionEnd < expressionStart ||
                expressionEnd >= value.Length ||
                value[expressionEnd] != '}' ||
                expressionEnd - expressionStart < prefix.Length ||
                String.Compare(
                    value,
                    expressionStart,
                    prefix,
                    0,
                    prefix.Length,
                    StringComparison.OrdinalIgnoreCase) != 0)
            {
                return false;
            }

            int pathStart = expressionStart + prefix.Length;
            int pathEnd = expressionEnd;

            while (pathStart < pathEnd &&
                   Char.IsWhiteSpace(value[pathStart]))
            {
                pathStart++;
            }

            while (pathEnd > pathStart &&
                   Char.IsWhiteSpace(value[pathEnd - 1]))
            {
                pathEnd--;
            }

            string body = value.Substring(
                pathStart,
                pathEnd - pathStart);

            if (LooksLikePresetConditionExpression(body))
                return false;

            // Match IsSingleMarkupExpression from the standalone parser. A
            // nested markup opening makes this candidate incomplete rather
            // than turning its inner text into a preset path.
            int i;

            for (i = pathStart; i < pathEnd; i++)
            {
                if (value[i] == '{' || value[i] == '}')
                    return false;
            }

            int separator = value.IndexOf(
                '.',
                pathStart,
                pathEnd - pathStart);

            if (separator <= pathStart ||
                separator == pathEnd - 1)
            {
                throw new FormatException(
                    "Preset expressions use {Preset SetName.Key}.");
            }

            setName = value.Substring(
                pathStart,
                separator - pathStart).Trim();
            key = value.Substring(
                separator + 1,
                pathEnd - separator - 1).Trim();
            return true;
        }

        private static bool ContainsPresetExpression(string value)
        {
            return
                !String.IsNullOrEmpty(value) &&
                value.IndexOf(
                    "{Preset ",
                    StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
