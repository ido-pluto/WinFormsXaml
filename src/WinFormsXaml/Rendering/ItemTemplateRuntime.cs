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
        private static ArrayList CloneArrayList(
            ArrayList source)
        {
            if (source == null)
                return new ArrayList();

            ArrayList copy = new ArrayList(source.Count);

            int i;

            for (i = 0; i < source.Count; i++)
                copy.Add(source[i]);

            return copy;
        }

        private static Hashtable CloneHashtable(
            Hashtable source)
        {
            Hashtable copy = source == null
                ? new Hashtable()
                : new Hashtable(source.Count);

            if (source == null)
                return copy;

            foreach (DictionaryEntry entry in source)
                copy[entry.Key] = entry.Value;

            return copy;
        }

        private static Hashtable BuildOldItemBuckets(
            ArrayList oldRecords)
        {
            Hashtable buckets = oldRecords == null
                ? new Hashtable()
                : new Hashtable(oldRecords.Count);

            if (oldRecords == null)
                return buckets;

            int i;

            for (i = 0; i < oldRecords.Count; i++)
            {
                RenderedItemRecord record =
                    oldRecords[i] as RenderedItemRecord;

                if (record == null)
                    continue;

                object existing = buckets[record.Key];

                if (existing == null)
                {
                    // Unique keys are overwhelmingly common. Store the record
                    // directly so a normal refresh does not allocate one Queue
                    // per realized item.
                    buckets[record.Key] = record;
                    continue;
                }

                Queue bucket = existing as Queue;

                if (bucket == null)
                {
                    bucket = new Queue(2);
                    bucket.Enqueue(existing);
                    buckets[record.Key] = bucket;
                }

                bucket.Enqueue(record);
            }

            return buckets;
        }

        private static RenderedItemRecord TakeOldItemRecord(
            Hashtable buckets,
            string key)
        {
            object existing = buckets[key];

            RenderedItemRecord record =
                existing as RenderedItemRecord;

            if (record != null)
            {
                buckets.Remove(key);
                return record;
            }

            Queue bucket = existing as Queue;

            if (bucket == null || bucket.Count == 0)
                return null;

            record = bucket.Dequeue() as RenderedItemRecord;

            if (bucket.Count == 0)
                buckets.Remove(key);

            return record;
        }

        private string GetStableItemKey(
            ItemsControl host,
            object item,
            int index)
        {
            object value = null;
            bool found = false;

            if (item != null &&
                !String.IsNullOrEmpty(host.ItemKeyPath))
            {
                value = TryResolveBindingPathNoThrow(
                    item,
                    host.ItemKeyPath,
                    out found);
            }
            else if (item != null)
            {
                int i;

                for (i = 0; i < CommonItemKeyPaths.Length; i++)
                {
                    value = TryResolveBindingPathNoThrow(
                        item,
                        CommonItemKeyPaths[i],
                        out found);

                    if (found)
                        break;
                }
            }

            if (!found || value == null)
                return "#index:" + index.ToString(
                    CultureInfo.InvariantCulture);

            return value.GetType().FullName +
                   "|" +
                   BindingValueToString(value);
        }

        /// <summary>
        /// Scans an ItemTemplate once and records only expressions that actually
        /// parse as Function bindings. ReloadItems then invokes this compact list
        /// instead of walking the XML tree once per item.
        /// </summary>
        private ArrayList CollectTemplateFunctionExpressions(
            XmlElement templateRoot)
        {
            ArrayList result = new ArrayList();

            if (templateRoot != null)
            {
                CollectTemplateFunctionExpressionsRecursive(
                    templateRoot,
                    result);
            }

            return result;
        }

        private void CollectTemplateFunctionExpressionsRecursive(
            XmlElement element,
            ArrayList result)
        {
            if (IsNestedItemsTemplateContainer(element))
                return;

            int i;

            for (i = 0; i < element.Attributes.Count; i++)
            {
                XmlAttribute attribute = element.Attributes[i];

                if (EqualsIgnoreCase(
                        attribute.LocalName,
                        MarkupXmlDocument.LocationAttributeName) ||
                    EqualsIgnoreCase(attribute.Name, "xmlns") ||
                    EqualsIgnoreCase(attribute.Prefix, "xmlns"))
                {
                    continue;
                }

                CollectFunctionExpressionsFromText(
                    attribute.Value,
                    result);
            }

            XmlNode node = element.FirstChild;

            while (node != null)
            {
                XmlElement child = node as XmlElement;

                if (child != null)
                {
                    CollectTemplateFunctionExpressionsRecursive(
                        child,
                        result);
                }
                else if (node.NodeType == XmlNodeType.Text ||
                         node.NodeType == XmlNodeType.CDATA)
                {
                    CollectFunctionExpressionsFromText(
                        node.Value,
                        result);
                }

                node = node.NextSibling;
            }
        }

        private static void CollectFunctionExpressionsFromText(
            string value,
            ArrayList result)
        {
            if (String.IsNullOrEmpty(value))
                return;

            int searchFrom = 0;

            while (searchFrom < value.Length)
            {
                int start = value.IndexOf('{', searchFrom);

                if (start < 0)
                    break;

                int end = value.IndexOf('}', start + 1);

                if (end < 0)
                    break;

                string expression = value.Substring(
                    start,
                    end - start + 1);

                string methodName;
                string argumentText;
                bool automaticDataContext;

                if (TryParseFunctionExpression(
                    expression,
                    out methodName,
                    out argumentText,
                    out automaticDataContext))
                {
                    result.Add(expression);
                }

                searchFrom = end + 1;
            }
        }

        /// <summary>
        /// Re-evaluates all Function bindings that can affect this template and
        /// hashes their return values. The actual return objects are retained in
        /// functionResults so a changed item can build from the exact values that
        /// were just compared, without calling the Functions twice.
        /// </summary>
        private int ComputeTemplateFunctionFingerprint(
            ItemsControl host,
            object item,
            Hashtable functionResults)
        {
            if (host == null ||
                host.TemplateRoot == null)
            {
                return 0;
            }

            ArrayList expressions =
                host.TemplateFunctionExpressions;

            if (expressions == null)
            {
                expressions =
                    CollectTemplateFunctionExpressions(
                        host.TemplateRoot);

                host.TemplateFunctionExpressions = expressions;
            }

            if (expressions.Count == 0)
                return 0;

            Hashtable previousCache =
                _activeFunctionResultCache;

            _activeFunctionResultCache = functionResults;

            try
            {
                unchecked
                {
                    int hash = 17;
                    int i;

                    for (i = 0; i < expressions.Count; i++)
                    {
                        string expression =
                            expressions[i] as string;

                        if (String.IsNullOrEmpty(expression))
                            continue;

                        object result;

                        if (!TryResolveFunctionExpression(
                            expression,
                            item,
                            out result))
                        {
                            continue;
                        }

                        hash = hash * 31 +
                            expression.GetHashCode();

                        hash = hash * 31 +
                            ComputeFunctionResultHash(result);
                    }

                    return hash;
                }
            }
            finally
            {
                _activeFunctionResultCache =
                    previousCache;
            }
        }

        private static bool AreFunctionResultMapsEquivalent(
            Hashtable oldValues,
            Hashtable newValues)
        {
            if (Object.ReferenceEquals(oldValues, newValues))
                return true;

            if (oldValues == null || newValues == null)
                return false;

            if (oldValues.Count != newValues.Count)
                return false;

            foreach (DictionaryEntry entry in newValues)
            {
                if (!oldValues.ContainsKey(entry.Key))
                    return false;

                if (!AreFunctionResultsEquivalent(
                    oldValues[entry.Key],
                    entry.Value))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool AreFunctionResultsEquivalent(
            object oldValue,
            object newValue)
        {
            if (Object.ReferenceEquals(oldValue, newValue))
                return true;

            if (oldValue == null || newValue == null)
                return false;

            byte[] oldBytes = oldValue as byte[];
            byte[] newBytes = newValue as byte[];

            if (oldBytes != null || newBytes != null)
            {
                if (oldBytes == null || newBytes == null ||
                    oldBytes.Length != newBytes.Length)
                {
                    return false;
                }

                int i;
                for (i = 0; i < oldBytes.Length; i++)
                {
                    if (oldBytes[i] != newBytes[i])
                        return false;
                }

                return true;
            }

            // Image/Icon and arbitrary reference objects intentionally use
            // identity. This is the cheap/lazy behavior expected for caches:
            // same object = same rendered value, replacement object = changed.
            if (oldValue is Image || newValue is Image ||
                oldValue is Icon || newValue is Icon)
            {
                return false;
            }

            Type oldType = oldValue.GetType();
            Type newType = newValue.GetType();

            if (oldType != newType)
                return false;

            if (oldType.IsValueType ||
                oldValue is string)
            {
                return oldValue.Equals(newValue);
            }

            return false;
        }

        private static bool IsByteImageSourceSlot(
            RenderBindingSlot slot)
        {
            return slot != null &&
                   slot.Target is PictureBox &&
                   EqualsIgnoreCase(slot.AttributeName, "Source");
        }

        private static bool AreRenderBindingSlotValuesEquivalent(
            RenderBindingSlot slot,
            object newValue)
        {
            if (slot == null)
                return false;

            byte[] bytes = newValue as byte[];

            // byte[] is mutable. Reference equality is a valid shortcut for all
            // ordinary binding values, but not for an encoded PictureBox source
            // after the application explicitly asks to refresh that item.
            if (bytes != null &&
                IsByteImageSourceSlot(slot) &&
                Object.ReferenceEquals(slot.LastValue, bytes))
            {
                return slot.LastByteImageFingerprintKnown &&
                       slot.LastByteImageFingerprint ==
                           ComputeByteImageFingerprint(bytes);
            }

            return AreFunctionResultsEquivalent(
                slot.LastValue,
                newValue);
        }

        private static void CommitRenderBindingSlotValue(
            RenderBindingSlot slot,
            object value)
        {
            if (slot == null)
                return;

            slot.LastValue = value;
            slot.LastByteImageFingerprintKnown = false;
            slot.LastByteImageFingerprint = 0;

            byte[] bytes = value as byte[];

            if (bytes == null || !IsByteImageSourceSlot(slot))
                return;

            slot.LastByteImageFingerprint =
                ComputeByteImageFingerprint(bytes);
            slot.LastByteImageFingerprintKnown = true;
        }

        private static int ComputeFunctionResultHash(
            object value)
        {
            if (value == null)
                return 0;

            byte[] bytes = value as byte[];

            if (bytes != null)
            {
                unchecked
                {
                    int hash = 17;
                    int i;

                    // Function results are part of the rendered output, so use
                    // the full byte[] here rather than the sampled item hash.
                    for (i = 0; i < bytes.Length; i++)
                    {
                        hash = hash * 31 + bytes[i];
                    }

                    return hash;
                }
            }

            Image image = value as Image;

            if (image != null)
            {
                unchecked
                {
                    // Image has no cheap, reliable pixel-content equality API.
                    // Identity is exactly right for the common cache pattern: the
                    // cache returns the same Image while unchanged and a new Image
                    // when its contents are replaced. If an image is mutated in place, the PictureBox already references the
                    // same object; replace the cached Image object when its contents change.
                    int hash =
                        System.Runtime.CompilerServices.RuntimeHelpers
                            .GetHashCode(image);

                    hash = hash * 31 + image.Width;
                    hash = hash * 31 + image.Height;
                    hash = hash * 31 + image.Flags;

                    return hash;
                }
            }

            Icon icon = value as Icon;

            if (icon != null)
            {
                return System.Runtime.CompilerServices.RuntimeHelpers
                    .GetHashCode(icon);
            }

            Type type = value.GetType();

            if (type.IsPrimitive ||
                type.IsEnum ||
                value is string ||
                value is decimal ||
                value is DateTime ||
                value is Guid ||
                value is Color ||
                value is Uri)
            {
                return ComputeSimpleValueHash(value);
            }

            // For other reference results, prefer identity. Function bindings are
            // render callbacks and a replacement returned object normally means a
            // replacement render value. Value-type results use their own hash.
            if (!type.IsValueType)
            {
                return System.Runtime.CompilerServices.RuntimeHelpers
                    .GetHashCode(value);
            }

            return value.GetHashCode();
        }

        private object GetItemVersionValue(
            ItemsControl host,
            object item,
            out bool found)
        {
            found = false;

            if (host == null ||
                item == null ||
                String.IsNullOrEmpty(host.ItemVersionPath))
            {
                return null;
            }

            return TryResolveBindingPathNoThrow(
                item,
                host.ItemVersionPath,
                out found);
        }

        private static int ComputeSimpleValueHash(object value)
        {
            if (value == null)
                return 0;

            byte[] bytes = value as byte[];

            if (bytes != null)
            {
                unchecked
                {
                    int hash = 17;
                    hash = hash * 31 + bytes.Length;

                    int sample = Math.Min(32, bytes.Length);
                    int i;

                    for (i = 0; i < sample; i++)
                    {
                        hash = hash * 31 + bytes[i];

                        int right = bytes.Length - 1 - i;

                        if (right >= sample)
                            hash = hash * 31 + bytes[right];
                    }

                    return hash;
                }
            }

            return value.GetHashCode();
        }

        private static BindingMemberLookup GetCachedBindingMember(
            Type type,
            string name)
        {
            if (type == null || String.IsNullOrEmpty(name))
                return null;

            BindingMemberLookup cached;

            lock (_bindingMemberLookupCacheLock)
            {
                Hashtable members =
                    _bindingMemberLookupCache[type] as Hashtable;
                cached = members == null
                    ? null
                    : members[name] as BindingMemberLookup;

                if (cached != null)
                    return cached;
            }

            BindingMemberLookup lookup =
                new BindingMemberLookup();

            lookup.Property =
                type.GetProperty(
                    name,
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.IgnoreCase);

            if (lookup.Property == null ||
                !lookup.Property.CanRead)
            {
                lookup.Property = null;
                lookup.Field =
                    type.GetField(
                        name,
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.IgnoreCase);
            }

            lookup.Missing =
                lookup.Property == null &&
                lookup.Field == null;

            lock (_bindingMemberLookupCacheLock)
            {
                Hashtable members =
                    _bindingMemberLookupCache[type] as Hashtable;

                if (members == null)
                {
                    if (_bindingMemberLookupCache.Count >=
                        BindingMemberTypeCacheLimit)
                    {
                        return lookup;
                    }

                    members = new Hashtable(
                        StringComparer.OrdinalIgnoreCase);
                    _bindingMemberLookupCache[type] = members;
                }

                cached = members[name] as BindingMemberLookup;

                if (cached != null)
                    return cached;

                if (members.Count < BindingMemberNameCacheLimit)
                    members[name] = lookup;
            }

            return lookup;
        }

        private static string[] GetCachedBindingPathParts(
            string path)
        {
            if (String.IsNullOrEmpty(path))
                return _emptyStringArray;

            string cacheKey = path.Trim();

            // A single dot is the documented whole-context path. Every other
            // dot separates two member names; silently dropping an empty
            // segment would turn a typo such as "Customer..Name" into the
            // valid but different path "Customer.Name".
            if (cacheKey == ".")
                return _emptyStringArray;

            string[] cached;

            lock (_bindingPathPartsCacheLock)
            {
                cached = _bindingPathPartsCache[cacheKey] as string[];

                if (cached != null)
                    return cached;
            }

            string[] raw = cacheKey.Split('.');
            int i;

            for (i = 0; i < raw.Length; i++)
            {
                string part = raw[i].Trim();

                if (part.Length == 0)
                {
                    throw new InvalidOperationException(
                        "Binding paths cannot contain empty member segments: '" +
                        path + "'. Use '.' only when binding the complete " +
                        "current value.");
                }

                raw[i] = part;
            }

            string[] result = raw;

            lock (_bindingPathPartsCacheLock)
            {
                cached = _bindingPathPartsCache[cacheKey] as string[];

                if (cached != null)
                    return cached;

                if (_bindingPathPartsCache.Count <
                    BindingPathPartsCacheLimit)
                {
                    _bindingPathPartsCache[cacheKey] = result;
                }
            }

            return result;
        }

        private static object TryResolveBindingPathNoThrow(
            object source,
            string path,
            out bool found)
        {
            found = false;

            if (source == null ||
                String.IsNullOrEmpty(path))
            {
                return null;
            }

            object current = source;
            string[] parts = GetCachedBindingPathParts(path);
            int i;

            for (i = 0; i < parts.Length; i++)
            {
                if (current == null)
                    return null;

                string part = parts[i];
                Type type = current.GetType();

                BindingMemberLookup member =
                    GetCachedBindingMember(
                        type,
                        part);

                if (member != null && member.Property != null)
                {
                    current = member.Property.GetValue(current, null);
                    continue;
                }

                if (member != null && member.Field != null)
                {
                    current = member.Field.GetValue(current);
                    continue;
                }

                IDictionary dict = current as IDictionary;

                if (dict != null && dict.Contains(part))
                {
                    current = dict[part];
                    continue;
                }

                return null;
            }

            found = true;
            return current;
        }

        private static void RestoreItemsScrollPosition(
            ItemsControl host,
            int previousScrollX,
            int previousScrollY)
        {
            if (host == null || !host.AutoScroll)
                return;

            bool previousSuppress =
                host.DirectVirtualSuppressScrollRefresh;
            host.DirectVirtualSuppressScrollRefresh = true;

            try
            {
                int logical = host.Orientation == Orientation.Vertical
                    ? previousScrollY
                    : previousScrollX;

                host.SetLogicalScrollOffset(logical);
            }
            finally
            {
                host.DirectVirtualSuppressScrollRefresh =
                    previousSuppress;
            }
        }

    }
}
