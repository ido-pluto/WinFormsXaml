using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml;
using System.Windows.Forms;

namespace WinFormsXaml
{
    public sealed partial class XamlRuntime : IDisposable
    {
        // ============================================================
        // INNER TEXT
        // ============================================================

        private void ApplyInnerText(
            object instance,
            XmlElement element)
        {
            if (HasElementChildren(
                element))
            {
                return;
            }

            string text =
                element.InnerText;

            if (String.IsNullOrEmpty(
                text))
            {
                return;
            }

            text =
                text.Trim();

            if (text.Length == 0)
                return;

            if (HasAttributeIgnoreNamespace(
                    element,
                    "Text") ||
                HasAttributeIgnoreNamespace(
                    element,
                    "Content") ||
                HasAttributeIgnoreNamespace(
                    element,
                    "Header"))
            {
                return;
            }

            TrySetProperty(
                instance,
                "Text",
                text);
        }

        private void PostConfigure(
            object instance)
        {
            PostConfigure(instance, null);
        }

        private void PostConfigure(
            object instance,
            XmlElement declarationElement)
        {
            ItemsControl itemsControl =
                instance as ItemsControl;

            if (itemsControl != null)
            {
                itemsControl.CompleteXamlInitialization(
                    declarationElement);
            }

            TabView tabView =
                instance as TabView;

            if (tabView != null)
                tabView.CompleteXamlInitialization();
        }

        // ============================================================
        // NAMES
        // ============================================================

        private void RegisterName(
            string name,
            object value)
        {
            if (String.IsNullOrEmpty(
                name))
            {
                return;
            }

            if (_namedObjects.ContainsKey(
                name))
            {
                throw new InvalidOperationException(
                    "Duplicate XAML name '" +
                    name +
                    "'.");
            }

            _namedObjects.Add(
                name,
                value);

            try
            {
                WireRegisteredName(
                    name,
                    value);
            }
            catch
            {
                _namedObjects.Remove(name);
                throw;
            }
        }

        private void RegisterNativeName(
            object instance)
        {
            PropertyInfo property =
                FindProperty(
                    instance.GetType(),
                    "Name");

            if (property == null ||
                !property.CanRead ||
                property.PropertyType !=
                    typeof(string))
            {
                return;
            }

            string name =
                property.GetValue(
                    instance,
                    null) as string;

            if (!String.IsNullOrEmpty(
                name))
            {
                RegisterName(
                    name,
                    instance);
            }
        }

        private void SetNativeName(
            object instance,
            string name)
        {
            PropertyInfo property =
                FindProperty(
                    instance.GetType(),
                    "Name");

            if (property != null &&
                property.CanWrite &&
                property.PropertyType ==
                    typeof(string))
            {
                property.SetValue(
                    instance,
                    name,
                    null);
            }
        }

        private static string GetDeclaredName(
            XmlElement element)
        {
            int i;

            for (i = 0;
                 i < element.Attributes.Count;
                 i++)
            {
                XmlAttribute attribute =
                    element.Attributes[i];

                if (EqualsIgnoreCase(
                    attribute.LocalName,
                    "Name"))
                {
                    return attribute.Value;
                }
            }

            return null;
        }

        // ============================================================
        // SIMPLE ITEMS
        // ============================================================

        private static bool IsSimpleItem(
            string name)
        {
            return EqualsIgnoreCase(name, "Item");
        }

        private static object GetSimpleItemValue(
            XmlElement element)
        {
            string value =
                GetAttributeIgnoreNamespace(
                    element,
                    "Content");

            if (String.IsNullOrEmpty(
                value))
            {
                value =
                    GetAttributeIgnoreNamespace(
                        element,
                        "Text");
            }

            if (String.IsNullOrEmpty(
                value))
            {
                value =
                    element.InnerText;
            }

            return value;
        }

        // ============================================================
        // REFLECTION
        // ============================================================

        private static PropertyInfo FindProperty(
            Type type,
            string name)
        {
            RecordCompiledControlBlueprintMemberLookup();

            if (type == null || String.IsNullOrEmpty(name))
                return null;

            object cached;
            PropertyReflectionCache cache;

            lock (_reflectionInfoCacheLock)
            {
                cache =
                    _propertyInfoCache[type] as PropertyReflectionCache;

                if (cache != null && cache.Members.ContainsKey(name))
                {
                    cached = cache.Members[name];
                    return Object.ReferenceEquals(cached, _missingReflectionInfo)
                        ? null
                        : cached as PropertyInfo;
                }
            }

            if (cache == null)
            {
                PropertyReflectionCache candidate =
                    BuildPropertyReflectionCache(type);

                lock (_reflectionInfoCacheLock)
                {
                    cache =
                        _propertyInfoCache[type] as PropertyReflectionCache;

                    if (cache == null &&
                        _propertyInfoCache.Count < ReflectionTypeCacheLimit)
                    {
                        _propertyInfoCache[type] = candidate;
                        cache = candidate;
                    }
                }
            }

            PropertyInfo property = cache == null
                ? FindDeclaredProperty(type, name)
                : FindDeclaredProperty(cache.DeclaredByDepth, name);

            lock (_reflectionInfoCacheLock)
            {
                cache =
                    _propertyInfoCache[type] as PropertyReflectionCache;

                if (cache == null)
                    return property;

                if (cache.Members.ContainsKey(name))
                {
                    cached = cache.Members[name];
                    return Object.ReferenceEquals(
                            cached,
                            _missingReflectionInfo)
                        ? null
                        : cached as PropertyInfo;
                }

                if (cache.Members.Count < ReflectionMemberNameCacheLimit)
                {
                    cache.Members[name] = property == null
                        ? _missingReflectionInfo
                        : (object)property;
                }
            }

            return property;
        }

        private static PropertyReflectionCache
            BuildPropertyReflectionCache(Type type)
        {
            ArrayList levels = new ArrayList();
            Type current = type;

            while (current != null)
            {
                levels.Add(
                    current.GetProperties(
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.DeclaredOnly));
                current = current.BaseType;
            }

            PropertyReflectionCache cache =
                new PropertyReflectionCache();
            cache.Members = new Hashtable(StringComparer.Ordinal);
            cache.DeclaredByDepth =
                (PropertyInfo[][])levels.ToArray(typeof(PropertyInfo[]));
            return cache;
        }

        private static PropertyInfo FindDeclaredProperty(
            PropertyInfo[][] declaredByDepth,
            string name)
        {
            int level;

            for (level = 0; level < declaredByDepth.Length; level++)
            {
                PropertyInfo[] properties = declaredByDepth[level];
                PropertyInfo caseInsensitiveMatch = null;
                int i;

                for (i = 0; i < properties.Length; i++)
                {
                    PropertyInfo property = properties[i];

                    if (String.Equals(
                        property.Name,
                        name,
                        StringComparison.Ordinal))
                    {
                        return property;
                    }

                    if (caseInsensitiveMatch == null &&
                        String.Equals(
                            property.Name,
                            name,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        caseInsensitiveMatch = property;
                    }
                }

                if (caseInsensitiveMatch != null)
                    return caseInsensitiveMatch;
            }

            return null;
        }

        private static PropertyInfo FindDeclaredProperty(
            Type type,
            string name)
        {
            Type current = type;

            while (current != null)
            {
                PropertyInfo[] properties = current.GetProperties(
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.DeclaredOnly);
                PropertyInfo caseInsensitiveMatch = null;
                int i;

                for (i = 0; i < properties.Length; i++)
                {
                    PropertyInfo property = properties[i];

                    if (String.Equals(
                        property.Name,
                        name,
                        StringComparison.Ordinal))
                    {
                        return property;
                    }

                    if (caseInsensitiveMatch == null &&
                        String.Equals(
                            property.Name,
                            name,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        caseInsensitiveMatch = property;
                    }
                }

                if (caseInsensitiveMatch != null)
                    return caseInsensitiveMatch;

                current = current.BaseType;
            }

            return null;
        }

        private static EventInfo FindEvent(
            Type type,
            string name)
        {
            RecordCompiledControlBlueprintMemberLookup();

            if (type == null || String.IsNullOrEmpty(name))
                return null;

            object cached;
            EventReflectionCache cache;

            lock (_reflectionInfoCacheLock)
            {
                cache =
                    _eventInfoCache[type] as EventReflectionCache;

                if (cache != null && cache.Members.ContainsKey(name))
                {
                    cached = cache.Members[name];
                    return Object.ReferenceEquals(cached, _missingReflectionInfo)
                        ? null
                        : cached as EventInfo;
                }
            }

            if (cache == null)
            {
                EventReflectionCache candidate =
                    BuildEventReflectionCache(type);

                lock (_reflectionInfoCacheLock)
                {
                    cache =
                        _eventInfoCache[type] as EventReflectionCache;

                    if (cache == null &&
                        _eventInfoCache.Count < ReflectionTypeCacheLimit)
                    {
                        _eventInfoCache[type] = candidate;
                        cache = candidate;
                    }
                }
            }

            EventInfo eventInfo = cache == null
                ? FindDeclaredEvent(type, name)
                : FindDeclaredEvent(cache.DeclaredByDepth, name);

            lock (_reflectionInfoCacheLock)
            {
                cache =
                    _eventInfoCache[type] as EventReflectionCache;

                if (cache == null)
                    return eventInfo;

                if (cache.Members.ContainsKey(name))
                {
                    cached = cache.Members[name];
                    return Object.ReferenceEquals(
                            cached,
                            _missingReflectionInfo)
                        ? null
                        : cached as EventInfo;
                }

                if (cache.Members.Count < ReflectionMemberNameCacheLimit)
                {
                    cache.Members[name] = eventInfo == null
                        ? _missingReflectionInfo
                        : (object)eventInfo;
                }
            }

            return eventInfo;
        }

        private static EventReflectionCache BuildEventReflectionCache(
            Type type)
        {
            ArrayList levels = new ArrayList();
            Type current = type;

            while (current != null)
            {
                levels.Add(
                    current.GetEvents(
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.DeclaredOnly));
                current = current.BaseType;
            }

            EventReflectionCache cache = new EventReflectionCache();
            cache.Members = new Hashtable(StringComparer.Ordinal);
            cache.DeclaredByDepth =
                (EventInfo[][])levels.ToArray(typeof(EventInfo[]));
            return cache;
        }

        private static EventInfo FindDeclaredEvent(
            EventInfo[][] declaredByDepth,
            string name)
        {
            int level;

            for (level = 0; level < declaredByDepth.Length; level++)
            {
                EventInfo[] events = declaredByDepth[level];
                EventInfo caseInsensitiveMatch = null;
                int i;

                for (i = 0; i < events.Length; i++)
                {
                    EventInfo eventInfo = events[i];

                    if (String.Equals(
                        eventInfo.Name,
                        name,
                        StringComparison.Ordinal))
                    {
                        return eventInfo;
                    }

                    if (caseInsensitiveMatch == null &&
                        String.Equals(
                            eventInfo.Name,
                            name,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        caseInsensitiveMatch = eventInfo;
                    }
                }

                if (caseInsensitiveMatch != null)
                    return caseInsensitiveMatch;
            }

            return null;
        }

        private static EventInfo FindDeclaredEvent(
            Type type,
            string name)
        {
            Type current = type;

            while (current != null)
            {
                EventInfo[] events = current.GetEvents(
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.DeclaredOnly);
                EventInfo caseInsensitiveMatch = null;
                int i;

                for (i = 0; i < events.Length; i++)
                {
                    EventInfo eventInfo = events[i];

                    if (String.Equals(
                        eventInfo.Name,
                        name,
                        StringComparison.Ordinal))
                    {
                        return eventInfo;
                    }

                    if (caseInsensitiveMatch == null &&
                        String.Equals(
                            eventInfo.Name,
                            name,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        caseInsensitiveMatch = eventInfo;
                    }
                }

                if (caseInsensitiveMatch != null)
                    return caseInsensitiveMatch;

                current = current.BaseType;
            }

            return null;
        }

        // ============================================================
        // XML HELPERS
        // ============================================================

        private static bool IsPropertyElement(
            XmlElement element)
        {
            return
                element.LocalName.IndexOf('.') >= 0;
        }

        private static bool HasElementChildren(
            XmlElement element)
        {
            XmlNode node =
                element.FirstChild;

            while (node != null)
            {
                if (node is XmlElement)
                    return true;

                node =
                    node.NextSibling;
            }

            return false;
        }

        private static bool HasAttributeIgnoreNamespace(
            XmlElement element,
            string name)
        {
            int i;

            for (i = 0;
                 i < element.Attributes.Count;
                 i++)
            {
                if (EqualsIgnoreCase(
                    element.Attributes[i].LocalName,
                    name))
                {
                    return true;
                }
            }

            return false;
        }

        private static string GetAttributeIgnoreNamespace(
            XmlElement element,
            string name)
        {
            int i;

            for (i = 0;
                 i < element.Attributes.Count;
                 i++)
            {
                XmlAttribute attribute =
                    element.Attributes[i];

                if (EqualsIgnoreCase(
                    attribute.LocalName,
                    name))
                {
                    return attribute.Value;
                }
            }

            return null;
        }

        // ============================================================
        // PARSING
        // ============================================================

        private static Padding ParseThickness(
            string value)
        {
            if (value == null)
                return new Padding(0);

            string normalized = value.Trim().Replace(",", " ");

            while (normalized.IndexOf("  ") >= 0)
                normalized = normalized.Replace("  ", " ");

            lock (_valueParseCacheLock)
            {
                object cached = _thicknessParseCache[normalized];

                if (cached != null)
                    return (Padding)cached;
            }

            string[] parts = normalized.Split(new char[] { ' ' });
            Padding result;

            if (parts.Length == 1)
            {
                int all = ParsePixel(parts[0]);
                result = new Padding(all);
            }
            else if (parts.Length == 2)
            {
                int horizontal = ParsePixel(parts[0]);
                int vertical = ParsePixel(parts[1]);
                result = new Padding(
                    horizontal,
                    vertical,
                    horizontal,
                    vertical);
            }
            else if (parts.Length == 4)
            {
                result = new Padding(
                    ParsePixel(parts[0]),
                    ParsePixel(parts[1]),
                    ParsePixel(parts[2]),
                    ParsePixel(parts[3]));
            }
            else
            {
                throw new FormatException(
                    "Invalid Thickness '" + normalized + "'.");
            }

            lock (_valueParseCacheLock)
            {
                if (_thicknessParseCache.Count < ValueParseCacheLimit &&
                    !_thicknessParseCache.ContainsKey(normalized))
                {
                    _thicknessParseCache.Add(normalized, result);
                }
            }

            return result;
        }

        private static HorizontalXamlAlignment ParseHorizontalAlignment(
            string value)
        {
            if (EqualsIgnoreCase(
                value,
                "Left"))
            {
                return
                    HorizontalXamlAlignment.Left;
            }

            if (EqualsIgnoreCase(
                value,
                "Center"))
            {
                return
                    HorizontalXamlAlignment.Center;
            }

            if (EqualsIgnoreCase(
                value,
                "Right"))
            {
                return
                    HorizontalXamlAlignment.Right;
            }

            return
                HorizontalXamlAlignment.Stretch;
        }

        private static VerticalXamlAlignment ParseVerticalAlignment(
            string value)
        {
            if (EqualsIgnoreCase(
                value,
                "Top"))
            {
                return
                    VerticalXamlAlignment.Top;
            }

            if (EqualsIgnoreCase(
                value,
                "Center"))
            {
                return
                    VerticalXamlAlignment.Center;
            }

            if (EqualsIgnoreCase(
                value,
                "Bottom"))
            {
                return
                    VerticalXamlAlignment.Bottom;
            }

            return
                VerticalXamlAlignment.Stretch;
        }

        private static bool ParseBoolean(
            string value)
        {
            if (EqualsIgnoreCase(
                    value,
                    "1") ||
                EqualsIgnoreCase(
                    value,
                    "yes") ||
                EqualsIgnoreCase(
                    value,
                    "on") ||
                EqualsIgnoreCase(
                    value,
                    "visible"))
            {
                return true;
            }

            if (EqualsIgnoreCase(
                    value,
                    "0") ||
                EqualsIgnoreCase(
                    value,
                    "no") ||
                EqualsIgnoreCase(
                    value,
                    "off") ||
                EqualsIgnoreCase(
                    value,
                    "hidden") ||
                EqualsIgnoreCase(
                    value,
                    "collapsed"))
            {
                return false;
            }

            return Boolean.Parse(
                value);
        }

        private static int ParseInt(
            string value)
        {
            return Int32.Parse(
                value.Trim(),
                CultureInfo.InvariantCulture);
        }

        private static int ParsePixel(
            string value)
        {
            return (int)Math.Round(
                ParseFloat(
                    value));
        }

        private static float ParseFloat(
            string value)
        {
            value =
                value.Trim();

            if (value.EndsWith(
                "px",
                StringComparison.OrdinalIgnoreCase))
            {
                value =
                    value.Substring(
                        0,
                        value.Length - 2);
            }

            if (value.EndsWith(
                "pt",
                StringComparison.OrdinalIgnoreCase))
            {
                value =
                    value.Substring(
                        0,
                        value.Length - 2);
            }

            return Single.Parse(
                value,
                CultureInfo.InvariantCulture);
        }

        private static Color ParseColor(
            string value)
        {
            string normalized = value.Trim();

            lock (_valueParseCacheLock)
            {
                object cached = _colorParseCache[normalized];

                if (cached != null)
                    return (Color)cached;
            }

            Color result = ParseColorUncached(normalized);

            lock (_valueParseCacheLock)
            {
                if (_colorParseCache.Count < ValueParseCacheLimit &&
                    !_colorParseCache.ContainsKey(normalized))
                {
                    _colorParseCache.Add(normalized, result);
                }
            }

            return result;
        }

        private static Color ParseColorUncached(
            string value)
        {
            const string colorPrefix = "Color.";
            const string qualifiedColorPrefix = "System.Drawing.Color.";
            const string systemColorsPrefix = "SystemColors.";
            const string qualifiedSystemColorsPrefix =
                "System.Drawing.SystemColors.";

            if (value.StartsWith(
                    qualifiedColorPrefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                value = value.Substring(
                    qualifiedColorPrefix.Length);
            }
            else if (value.StartsWith(
                         colorPrefix,
                         StringComparison.OrdinalIgnoreCase))
            {
                value = value.Substring(colorPrefix.Length);
            }
            else
            {
                string systemColorName = null;

                if (value.StartsWith(
                        qualifiedSystemColorsPrefix,
                        StringComparison.OrdinalIgnoreCase))
                {
                    systemColorName = value.Substring(
                        qualifiedSystemColorsPrefix.Length);
                }
                else if (value.StartsWith(
                             systemColorsPrefix,
                             StringComparison.OrdinalIgnoreCase))
                {
                    systemColorName = value.Substring(
                        systemColorsPrefix.Length);
                }

                if (systemColorName != null)
                {
                    PropertyInfo systemColorProperty =
                        typeof(SystemColors).GetProperty(
                            systemColorName,
                            BindingFlags.Public |
                            BindingFlags.Static |
                            BindingFlags.IgnoreCase);

                    if (systemColorProperty == null ||
                        systemColorProperty.PropertyType != typeof(Color) ||
                        systemColorProperty.GetIndexParameters().Length != 0)
                    {
                        throw new FormatException(
                            "Unknown SystemColors value '" +
                            systemColorName +
                            "'.");
                    }

                    return (Color)systemColorProperty.GetValue(null, null);
                }
            }

            if (EqualsIgnoreCase(value, "Empty"))
                return Color.Empty;

            if (EqualsIgnoreCase(value, "Transparent"))
                return Color.Transparent;

            if (value.StartsWith("#"))
            {
                if (value.Length == 4)
                {
                    string r = new String(value[1], 2);
                    string g = new String(value[2], 2);
                    string b = new String(value[3], 2);

                    return Color.FromArgb(
                        Int32.Parse(r, NumberStyles.HexNumber),
                        Int32.Parse(g, NumberStyles.HexNumber),
                        Int32.Parse(b, NumberStyles.HexNumber));
                }

                if (value.Length == 7)
                {
                    return Color.FromArgb(
                        Int32.Parse(value.Substring(1, 2), NumberStyles.HexNumber),
                        Int32.Parse(value.Substring(3, 2), NumberStyles.HexNumber),
                        Int32.Parse(value.Substring(5, 2), NumberStyles.HexNumber));
                }

                if (value.Length == 9)
                {
                    return Color.FromArgb(
                        Int32.Parse(value.Substring(1, 2), NumberStyles.HexNumber),
                        Int32.Parse(value.Substring(3, 2), NumberStyles.HexNumber),
                        Int32.Parse(value.Substring(5, 2), NumberStyles.HexNumber),
                        Int32.Parse(value.Substring(7, 2), NumberStyles.HexNumber));
                }
            }

            Color named = Color.FromName(value);

            if (named.IsKnownColor || named.IsNamedColor)
                return named;

            ColorConverter converter = new ColorConverter();
            return (Color)converter.ConvertFromInvariantString(value);
        }

        private string ResolvePath(
            string value)
        {
            if (String.IsNullOrEmpty(
                value))
            {
                return value;
            }

            Uri uri;

            if (Uri.TryCreate(
                value,
                UriKind.Absolute,
                out uri))
            {
                if (uri.IsFile)
                    return uri.LocalPath;

                return value;
            }

            if (Path.IsPathRooted(
                value))
            {
                return value;
            }

            if (!String.IsNullOrEmpty(
                _basePath))
            {
                return Path.Combine(
                    _basePath,
                    value);
            }

            return value;
        }

        // ============================================================
        // HELPERS
        // ============================================================

        private static bool EqualsIgnoreCase(
            string a,
            string b)
        {
            return String.Equals(
                a,
                b,
                StringComparison.OrdinalIgnoreCase);
        }

        private static int Clamp(
            int value,
            int minimum,
            int maximum)
        {
            if (value < minimum)
                return minimum;

            if (value > maximum)
                return maximum;

            return value;
        }

        private static bool IsZeroPadding(
            Padding padding)
        {
            return
                padding.Left == 0 &&
                padding.Top == 0 &&
                padding.Right == 0 &&
                padding.Bottom == 0;
        }
    }
}
