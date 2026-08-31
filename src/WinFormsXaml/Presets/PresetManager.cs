using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;

namespace WinFormsXaml
{
    /// <summary>
    /// Identifies the preset scope affected by a manager change. Null members
    /// indicate that the notification covers a broader scope.
    /// </summary>
    public sealed class PresetChangedEventArgs : EventArgs
    {
        private readonly string _setName;
        private readonly string _presetName;
        private readonly string _key;

        internal PresetChangedEventArgs(
            string setName,
            string presetName,
            string key)
        {
            _setName = setName;
            _presetName = presetName;
            _key = key;
        }

        /// <summary>Gets the changed preset-set name, or null for all sets.</summary>
        public string SetName
        {
            get { return _setName; }
        }

        /// <summary>Gets the changed preset name, or null for the complete set.</summary>
        public string PresetName
        {
            get { return _presetName; }
        }

        /// <summary>Gets the changed key, or null for the complete preset.</summary>
        public string Key
        {
            get { return _key; }
        }
    }

    /// <summary>
    /// Controls how imported preset definitions interact with existing data.
    /// </summary>
    public enum PresetImportMode
    {
        /// <summary>
        /// Adds missing definitions and overwrites matching values. Supplied
        /// Selected and Default attributes replace the corresponding state.
        /// </summary>
        Merge = 0,

        /// <summary>
        /// Adds missing sets, presets, and keys without overwriting existing
        /// values or an existing set's selected or default state.
        /// </summary>
        PreserveExisting = 1,

        /// <summary>
        /// Replaces each existing set named by the import as a whole.
        /// Unnamed existing sets are left intact.
        /// </summary>
        Replace = 2
    }

    /// <summary>
    /// Stores named groups of switchable values. A manager can be shared by
    /// multiple XamlRuntime instances so one selection updates every form.
    /// </summary>
    /// <remarks>
    /// Instances are not thread-safe. The caller must serialize public
    /// mutations and imports and perform them on the UI thread that owns any
    /// subscribing XamlRuntime instances.
    /// </remarks>
    public sealed class PresetManager
    {
        private readonly Dictionary<string, PresetSet> _sets;
        private EventHandler<PresetChangedEventArgs> _changed;
        private Delegate[] _changedSubscribers;
        private int _notificationDeferralDepth;
        private PresetChangedEventArgs _deferredChange;

        /// <summary>Creates an empty preset manager.</summary>
        public PresetManager()
        {
            _sets =
                new Dictionary<string, PresetSet>(
                    StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Occurs after a preset value, selection, default, or definition changes.
        /// </summary>
        public event EventHandler<PresetChangedEventArgs> Changed
        {
            add
            {
                EventHandler<PresetChangedEventArgs> next =
                    (EventHandler<PresetChangedEventArgs>)Delegate.Combine(
                        _changed,
                        value);

                if (Object.ReferenceEquals(_changed, next))
                    return;

                _changed = next;
                _changedSubscribers =
                    next == null
                        ? null
                        : next.GetInvocationList();
            }
            remove
            {
                EventHandler<PresetChangedEventArgs> next =
                    (EventHandler<PresetChangedEventArgs>)Delegate.Remove(
                        _changed,
                        value);

                if (Object.ReferenceEquals(_changed, next))
                    return;

                _changed = next;
                _changedSubscribers =
                    next == null
                        ? null
                        : next.GetInvocationList();
            }
        }

        /// <summary>Gets a live, read-only view of the registered set names.</summary>
        public ICollection<string> Names
        {
            get { return _sets.Keys; }
        }

        /// <summary>Gets a named preset set.</summary>
        public PresetSet this[string name]
        {
            get { return GetSet(name); }
        }

        /// <summary>Returns whether a preset set with the supplied name exists.</summary>
        public bool Contains(string name)
        {
            return
                !String.IsNullOrEmpty(name) &&
                _sets.ContainsKey(name);
        }

        /// <summary>Gets a named set or throws when it is not registered.</summary>
        public PresetSet GetSet(string name)
        {
            PresetSet set;

            if (String.IsNullOrEmpty(name) ||
                !_sets.TryGetValue(name, out set))
            {
                throw new KeyNotFoundException(
                    "Preset set '" + name + "' was not found.");
            }

            return set;
        }

        /// <summary>Adds an empty named preset set.</summary>
        public PresetSet AddSet(string name)
        {
            ValidateName(name, "name");

            if (_sets.ContainsKey(name))
            {
                throw new InvalidOperationException(
                    "Preset set '" + name + "' already exists.");
            }

            PresetSet set =
                new PresetSet(this, name);

            _sets.Add(name, set);
            RaiseChanged(name, null, null);
            return set;
        }

        /// <summary>
        /// Removes and retires a named set. Returns false when it does not exist.
        /// </summary>
        public bool RemoveSet(string name)
        {
            PresetSet set;

            if (String.IsNullOrEmpty(name) ||
                !_sets.TryGetValue(name, out set))
            {
                return false;
            }

            _sets.Remove(name);
            set.Retire();
            RaiseChanged(name, null, null);
            return true;
        }

        /// <summary>Adds a preset to an existing set.</summary>
        public void AddPreset(
            string setName,
            string presetName)
        {
            GetSet(setName).AddPreset(presetName);
        }

        /// <summary>Removes a preset from an existing set.</summary>
        public bool RemovePreset(
            string setName,
            string presetName)
        {
            return GetSet(setName).RemovePreset(presetName);
        }

        /// <summary>Selects the active preset in a named set.</summary>
        public void Select(
            string setName,
            string presetName)
        {
            GetSet(setName).Select(presetName);
        }

        /// <summary>Sets the fallback preset in a named set.</summary>
        public void SetDefault(
            string setName,
            string presetName)
        {
            GetSet(setName).SetDefault(presetName);
        }

        /// <summary>Adds a new key and value to a named preset.</summary>
        public void AddValue(
            string setName,
            string presetName,
            string key,
            object value)
        {
            GetSet(setName)
                .GetPreset(presetName)
                .AddValue(key, value);
        }

        /// <summary>Adds or replaces a value in a named preset.</summary>
        public void SetValue(
            string setName,
            string presetName,
            string key,
            object value)
        {
            GetSet(setName)
                .GetPreset(presetName)
                .SetValue(key, value);
        }

        /// <summary>Removes a value from a named preset.</summary>
        public bool RemoveValue(
            string setName,
            string presetName,
            string key)
        {
            return GetSet(setName)
                .GetPreset(presetName)
                .RemoveValue(key);
        }

        /// <summary>
        /// Resolves a key from the selected preset, then the configured default.
        /// This strict C# API throws when neither preset defines the key.
        /// </summary>
        public object Resolve(
            string setName,
            string key)
        {
            return GetSet(setName).Resolve(key);
        }

        /// <summary>
        /// Tries the selected preset and then the configured default preset.
        /// Returns false when the known set defines neither value.
        /// </summary>
        public bool TryResolve(
            string setName,
            string key,
            out object value)
        {
            return GetSet(setName).TryResolve(key, out value);
        }

        /// <summary>
        /// Coalesces changes made inside the returned scope into one Changed
        /// notification. This batches UI refreshes; it does not roll back
        /// mutations when the caller throws.
        /// </summary>
        public IDisposable DeferNotifications()
        {
            _notificationDeferralDepth++;
            return new NotificationDeferral(this);
        }

        /// <summary>
        /// Transactionally merges preset definitions from an XML string.
        /// </summary>
        public void LoadXml(string xml)
        {
            LoadXml(xml, PresetImportMode.Merge);
        }

        /// <summary>
        /// Loads one Presets element or a document containing multiple Presets
        /// elements using the requested import behavior. Parsing and validation
        /// complete before any live preset is changed.
        /// </summary>
        public void LoadXml(
            string xml,
            PresetImportMode mode)
        {
            ValidateImportMode(mode);

            if (String.IsNullOrEmpty(xml) || xml.Trim().Length == 0)
                throw new ArgumentException("Preset XML cannot be empty.", "xml");

            XmlDocument document = CreateXmlDocument();

            using (StringReader textReader = new StringReader(xml))
            using (XmlTextReader reader = CreateXmlReader(textReader))
                document.Load(reader);

            ImportDocument(document, mode);
        }

        /// <summary>Transactionally merges presets from an XML file.</summary>
        public void LoadFile(string path)
        {
            LoadFile(path, PresetImportMode.Merge);
        }

        /// <summary>
        /// Loads presets directly from a file stream so the XML declaration and
        /// byte-order mark determine the document encoding.
        /// </summary>
        public void LoadFile(
            string path,
            PresetImportMode mode)
        {
            ValidateImportMode(mode);

            if (String.IsNullOrEmpty(path))
                throw new ArgumentException("A preset file path is required.", "path");

            XmlDocument document = CreateXmlDocument();

            using (Stream stream = File.OpenRead(path))
            using (XmlTextReader reader = CreateXmlReader(stream))
                document.Load(reader);

            ImportDocument(document, mode);
        }

        /// <summary>
        /// Transactionally merges presets from an embedded XML resource.
        /// </summary>
        public void LoadEmbeddedResource(
            Assembly assembly,
            string resourceName)
        {
            LoadEmbeddedResource(
                assembly,
                resourceName,
                PresetImportMode.Merge);
        }

        /// <summary>
        /// Loads presets directly from an embedded resource stream so the XML
        /// declaration and byte-order mark determine the document encoding.
        /// </summary>
        public void LoadEmbeddedResource(
            Assembly assembly,
            string resourceName,
            PresetImportMode mode)
        {
            ValidateImportMode(mode);

            if (assembly == null)
                throw new ArgumentNullException("assembly");

            if (String.IsNullOrEmpty(resourceName))
                throw new ArgumentException("A resource name is required.", "resourceName");

            Stream stream =
                assembly.GetManifestResourceStream(resourceName);

            if (stream == null)
            {
                throw new InvalidOperationException(
                    "Embedded preset resource '" + resourceName +
                    "' was not found in " + assembly.FullName + ".");
            }

            XmlDocument document = CreateXmlDocument();

            using (stream)
            using (XmlTextReader reader = CreateXmlReader(stream))
                document.Load(reader);

            ImportDocument(document, mode);
        }

        internal void RaiseChanged(
            string setName,
            string presetName,
            string key)
        {
            // A manager is commonly populated before any runtime subscribes.
            // There is no historical notification to retain outside a
            // deferral, so avoid allocating event data for that setup path.
            if (_notificationDeferralDepth == 0 &&
                _changedSubscribers == null)
            {
                return;
            }

            PresetChangedEventArgs args =
                new PresetChangedEventArgs(
                    setName,
                    presetName,
                    key);

            if (_notificationDeferralDepth > 0)
            {
                if (_deferredChange == null)
                {
                    _deferredChange = args;
                }
                else if (!HaveSameScope(_deferredChange, args))
                {
                    _deferredChange =
                        new PresetChangedEventArgs(null, null, null);
                }

                return;
            }

            Delegate[] subscribers = _changedSubscribers;

            if (subscribers != null)
                DispatchChanged(subscribers, args);
        }

        private void EndNotificationDeferral()
        {
            if (_notificationDeferralDepth <= 0)
                return;

            _notificationDeferralDepth--;

            if (_notificationDeferralDepth != 0 || _deferredChange == null)
                return;

            PresetChangedEventArgs args = _deferredChange;
            _deferredChange = null;

            Delegate[] subscribers = _changedSubscribers;

            if (subscribers != null)
                DispatchChanged(subscribers, args);
        }

        private void DispatchChanged(
            Delegate[] subscribers,
            PresetChangedEventArgs args)
        {
            Exception firstError = null;
            int i;

            for (i = 0; i < subscribers.Length; i++)
            {
                try
                {
                    ((EventHandler<PresetChangedEventArgs>)subscribers[i])(
                        this,
                        args);
                }
                catch (Exception ex)
                {
                    if (firstError == null)
                        firstError = ex;
                }
            }

            if (firstError != null)
                throw firstError;
        }

        private static bool HaveSameScope(
            PresetChangedEventArgs left,
            PresetChangedEventArgs right)
        {
            return
                String.Equals(
                    left.SetName,
                    right.SetName,
                    StringComparison.OrdinalIgnoreCase) &&
                String.Equals(
                    left.PresetName,
                    right.PresetName,
                    StringComparison.OrdinalIgnoreCase) &&
                String.Equals(
                    left.Key,
                    right.Key,
                    StringComparison.OrdinalIgnoreCase);
        }

        private void ImportDocument(
            XmlDocument document,
            PresetImportMode mode)
        {
            List<ImportedSetDefinition> definitions =
                ParseDefinitions(document);

            ValidateDefinitions(definitions, mode);
            ApplyDefinitions(definitions, mode);
        }

        private static XmlDocument CreateXmlDocument()
        {
            XmlDocument document = new XmlDocument();
            document.PreserveWhitespace = false;
            document.XmlResolver = null;
            return document;
        }

        private static XmlTextReader CreateXmlReader(TextReader reader)
        {
            XmlTextReader xmlReader = new XmlTextReader(reader);
            ConfigureXmlReader(xmlReader);
            return xmlReader;
        }

        private static XmlTextReader CreateXmlReader(Stream stream)
        {
            XmlTextReader xmlReader = new XmlTextReader(stream);
            ConfigureXmlReader(xmlReader);
            return xmlReader;
        }

        private static void ConfigureXmlReader(XmlTextReader reader)
        {
            reader.ProhibitDtd = true;
            reader.XmlResolver = null;
        }

        private static List<ImportedSetDefinition> ParseDefinitions(
            XmlDocument document)
        {
            if (document.DocumentElement == null)
                throw new InvalidOperationException("Preset XML has no root element.");

            List<ImportedSetDefinition> definitions =
                new List<ImportedSetDefinition>();
            Dictionary<string, bool> importedNames =
                new Dictionary<string, bool>(
                    StringComparer.OrdinalIgnoreCase);
            XmlElement root = document.DocumentElement;

            if (IsSetElement(root))
            {
                AddDefinition(root, definitions, importedNames);
            }
            else
            {
                ValidateAllowedAttributes(root, new string[0]);

                for (XmlNode node = root.FirstChild;
                     node != null;
                     node = node.NextSibling)
                {
                    XmlElement element = node as XmlElement;

                    if (element != null)
                    {
                        if (!IsSetElement(element))
                        {
                            throw new InvalidOperationException(
                                "Unexpected <" + element.LocalName +
                                "> element inside <" + root.LocalName +
                                ">. Only <Presets> elements are allowed.");
                        }

                        AddDefinition(element, definitions, importedNames);
                    }
                    else
                    {
                        ValidateIgnorableContent(node, root);
                    }
                }
            }

            if (definitions.Count == 0)
            {
                throw new InvalidOperationException(
                    "Preset XML must contain a <Presets> element.");
            }

            return definitions;
        }

        private static void AddDefinition(
            XmlElement element,
            List<ImportedSetDefinition> definitions,
            Dictionary<string, bool> importedNames)
        {
            ImportedSetDefinition definition = ParseSetDefinition(element);

            if (importedNames.ContainsKey(definition.Name))
            {
                throw new InvalidOperationException(
                    "Preset XML contains more than one <Presets> element " +
                    "named '" + definition.Name + "'.");
            }

            importedNames.Add(definition.Name, true);
            definitions.Add(definition);
        }

        private static ImportedSetDefinition ParseSetDefinition(
            XmlElement element)
        {
            ValidateAllowedAttributes(
                element,
                new string[] { "Name", "Selected", "Default" });

            string name = GetAttribute(element, "Name");
            ValidateImportedIdentifier(
                name,
                "The <Presets> Name attribute");

            ImportedSetDefinition definition =
                new ImportedSetDefinition(name);

            definition.SelectedName = GetAttribute(element, "Selected");
            definition.DefaultName = GetAttribute(element, "Default");

            if (definition.SelectedName != null)
            {
                ValidateImportedIdentifier(
                    definition.SelectedName,
                    "The Selected attribute of preset set '" + name + "'");
            }

            if (definition.DefaultName != null)
            {
                ValidateImportedIdentifier(
                    definition.DefaultName,
                    "The Default attribute of preset set '" + name + "'");
            }

            Dictionary<string, bool> importedNames =
                new Dictionary<string, bool>(
                    StringComparer.OrdinalIgnoreCase);

            for (XmlNode node = element.FirstChild;
                 node != null;
                 node = node.NextSibling)
            {
                XmlElement presetElement = node as XmlElement;

                if (presetElement == null)
                {
                    ValidateIgnorableContent(node, element);
                    continue;
                }

                if (!String.Equals(
                        presetElement.LocalName,
                        "Preset",
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "Unexpected <" + presetElement.LocalName +
                        "> element inside preset set '" + name +
                        "'. Only <Preset> elements are allowed.");
                }

                ImportedPresetDefinition preset =
                    ParsePresetDefinition(presetElement, name);

                if (importedNames.ContainsKey(preset.Name))
                {
                    throw new InvalidOperationException(
                        "Preset set '" + name +
                        "' contains more than one preset named '" +
                        preset.Name + "'.");
                }

                importedNames.Add(preset.Name, true);
                definition.Presets.Add(preset);
            }

            return definition;
        }

        private static ImportedPresetDefinition ParsePresetDefinition(
            XmlElement element,
            string setName)
        {
            ValidateAllowedAttributes(
                element,
                new string[] { "Name" });

            string name = GetAttribute(element, "Name");
            ValidateImportedIdentifier(
                name,
                "The <Preset> Name attribute in preset set '" +
                setName + "'");

            ImportedPresetDefinition definition =
                new ImportedPresetDefinition(name);
            Dictionary<string, bool> importedKeys =
                new Dictionary<string, bool>(
                    StringComparer.OrdinalIgnoreCase);

            for (XmlNode node = element.FirstChild;
                 node != null;
                 node = node.NextSibling)
            {
                XmlElement valueElement = node as XmlElement;

                if (valueElement == null)
                {
                    ValidateIgnorableContent(node, element);
                    continue;
                }

                if (!String.Equals(
                        valueElement.LocalName,
                        "Set",
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "Unexpected <" + valueElement.LocalName +
                        "> element inside preset '" + name +
                        "'. Only <Set> elements are allowed.");
                }

                ValidateAllowedAttributes(
                    valueElement,
                    new string[] { "Key", "Value" });

                string key = GetAttribute(valueElement, "Key");
                ValidateImportedIdentifier(
                    key,
                    "The <Set> Key attribute in preset '" + name + "'");

                if (importedKeys.ContainsKey(key))
                {
                    throw new InvalidOperationException(
                        "Preset '" + name +
                        "' contains duplicate key '" + key + "'.");
                }

                importedKeys.Add(key, true);

                string value = GetAttribute(valueElement, "Value");

                if (value == null)
                {
                    throw new InvalidOperationException(
                        "The <Set> element for key '" + key +
                        "' in preset '" + name +
                        "' requires a Value attribute.");
                }

                bool hasElementContent = false;

                for (XmlNode content = valueElement.FirstChild;
                     content != null;
                     content = content.NextSibling)
                {
                    if (content is XmlElement)
                    {
                        hasElementContent = true;
                        break;
                    }
                }

                if (hasElementContent ||
                    valueElement.InnerText.Trim().Length != 0)
                {
                    throw new InvalidOperationException(
                        "The <Set> element for key '" + key +
                        "' in preset '" + name +
                        "' must be empty. Declare its value with the Value attribute.");
                }

                definition.Values.Add(
                    new ImportedValueDefinition(key, value));
            }

            return definition;
        }

        private void ValidateDefinitions(
            List<ImportedSetDefinition> definitions,
            PresetImportMode mode)
        {
            int i;

            for (i = 0; i < definitions.Count; i++)
            {
                ImportedSetDefinition definition = definitions[i];
                PresetSet existingSet = null;
                bool includeExisting =
                    mode != PresetImportMode.Replace &&
                    _sets.TryGetValue(definition.Name, out existingSet);
                Dictionary<string, bool> availableNames =
                    new Dictionary<string, bool>(
                        StringComparer.OrdinalIgnoreCase);

                if (includeExisting)
                {
                    foreach (string presetName in existingSet.Names)
                        availableNames[presetName] = true;
                }

                int presetIndex;

                for (presetIndex = 0;
                     presetIndex < definition.Presets.Count;
                     presetIndex++)
                {
                    availableNames[definition.Presets[presetIndex].Name] = true;
                }

                ValidatePresetReference(
                    definition.Name,
                    "Selected",
                    definition.SelectedName,
                    availableNames);
                ValidatePresetReference(
                    definition.Name,
                    "Default",
                    definition.DefaultName,
                    availableNames);
            }
        }

        private static void ValidatePresetReference(
            string setName,
            string attributeName,
            string presetName,
            Dictionary<string, bool> availableNames)
        {
            if (presetName == null || availableNames.ContainsKey(presetName))
                return;

            throw new InvalidOperationException(
                "The " + attributeName + " preset '" + presetName +
                "' was not found in preset set '" + setName + "'.");
        }

        private void ApplyDefinitions(
            List<ImportedSetDefinition> definitions,
            PresetImportMode mode)
        {
            bool changed = false;
            int i;

            for (i = 0; i < definitions.Count; i++)
            {
                changed =
                    ApplySetDefinition(definitions[i], mode) ||
                    changed;
            }

            // Imports can affect many dependency scopes. One broad event keeps
            // listeners from observing intermediate state or refreshing twice.
            if (changed)
                RaiseChanged(null, null, null);
        }

        private bool ApplySetDefinition(
            ImportedSetDefinition definition,
            PresetImportMode mode)
        {
            PresetSet set;
            bool setExisted =
                _sets.TryGetValue(definition.Name, out set);
            bool changed = false;

            if (mode == PresetImportMode.Replace)
            {
                if (setExisted &&
                    IsEquivalentReplacement(set, definition))
                {
                    return false;
                }

                PresetSet replacement =
                    CreateReplacementSet(definition);

                _sets[definition.Name] = replacement;

                if (setExisted)
                    set.Retire();

                return true;
            }

            if (!setExisted)
            {
                set = new PresetSet(this, definition.Name);
                _sets.Add(definition.Name, set);
                changed = true;
            }

            bool preserveExistingState =
                mode == PresetImportMode.PreserveExisting && setExisted;
            int presetIndex;

            for (presetIndex = 0;
                 presetIndex < definition.Presets.Count;
                 presetIndex++)
            {
                ImportedPresetDefinition presetDefinition =
                    definition.Presets[presetIndex];
                Preset preset;

                if (set.Contains(presetDefinition.Name))
                {
                    preset = set.GetPreset(presetDefinition.Name);
                }
                else
                {
                    preset = set.AddPresetInternal(
                        presetDefinition.Name,
                        !preserveExistingState);
                    changed = true;
                }

                int valueIndex;

                for (valueIndex = 0;
                     valueIndex < presetDefinition.Values.Count;
                     valueIndex++)
                {
                    ImportedValueDefinition value =
                        presetDefinition.Values[valueIndex];

                    if (mode == PresetImportMode.PreserveExisting &&
                        setExisted && preset.Contains(value.Key))
                    {
                        continue;
                    }

                    object existingValue;
                    bool hasExistingValue =
                        preset.TryGetValue(
                            value.Key,
                            out existingValue);
                    string existingText = existingValue as string;

                    if (!hasExistingValue ||
                        existingText == null ||
                        !String.Equals(
                            existingText,
                            value.Value,
                            StringComparison.Ordinal))
                    {
                        preset.SetValueInternal(value.Key, value.Value);
                        changed = true;
                    }
                }
            }

            if (preserveExistingState)
                return changed;

            if (definition.DefaultName != null &&
                !String.Equals(
                    set.DefaultName,
                    definition.DefaultName,
                    StringComparison.OrdinalIgnoreCase))
            {
                set.SetDefaultInternal(definition.DefaultName);
                changed = true;
            }

            if (definition.SelectedName != null &&
                !String.Equals(
                    set.SelectedName,
                    definition.SelectedName,
                    StringComparison.OrdinalIgnoreCase))
            {
                set.SelectInternal(definition.SelectedName);
                changed = true;
            }
            else if (String.IsNullOrEmpty(set.SelectedName))
            {
                string before = set.SelectedName;
                set.SelectFirstAvailable();

                if (!String.Equals(
                        before,
                        set.SelectedName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    changed = true;
                }
            }

            return changed;
        }

        private PresetSet CreateReplacementSet(
            ImportedSetDefinition definition)
        {
            PresetSet replacement =
                new PresetSet(this, definition.Name);
            int presetIndex;

            for (presetIndex = 0;
                 presetIndex < definition.Presets.Count;
                 presetIndex++)
            {
                ImportedPresetDefinition presetDefinition =
                    definition.Presets[presetIndex];
                Preset preset = replacement.AddPresetInternal(
                    presetDefinition.Name,
                    true);
                int valueIndex;

                for (valueIndex = 0;
                     valueIndex < presetDefinition.Values.Count;
                     valueIndex++)
                {
                    ImportedValueDefinition value =
                        presetDefinition.Values[valueIndex];
                    preset.SetValueInternal(value.Key, value.Value);
                }
            }

            if (definition.DefaultName != null)
                replacement.SetDefaultInternal(definition.DefaultName);

            if (definition.SelectedName != null)
            {
                replacement.SelectInternal(definition.SelectedName);
            }
            else if (String.IsNullOrEmpty(replacement.SelectedName))
            {
                replacement.SelectFirstAvailable();
            }

            return replacement;
        }

        private static bool IsEquivalentReplacement(
            PresetSet existing,
            ImportedSetDefinition definition)
        {
            if (existing == null ||
                existing.Names.Count != definition.Presets.Count)
            {
                return false;
            }

            string selectedName = definition.SelectedName;

            if (selectedName == null && definition.Presets.Count != 0)
                selectedName = definition.Presets[0].Name;

            if (!String.Equals(
                    existing.SelectedName,
                    selectedName,
                    StringComparison.OrdinalIgnoreCase) ||
                !String.Equals(
                    existing.DefaultName,
                    definition.DefaultName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            int presetIndex;

            for (presetIndex = 0;
                 presetIndex < definition.Presets.Count;
                 presetIndex++)
            {
                ImportedPresetDefinition presetDefinition =
                    definition.Presets[presetIndex];

                if (!existing.Contains(presetDefinition.Name))
                    return false;

                Preset preset =
                    existing.GetPreset(presetDefinition.Name);

                if (preset.Keys.Count != presetDefinition.Values.Count)
                    return false;

                int valueIndex;

                for (valueIndex = 0;
                     valueIndex < presetDefinition.Values.Count;
                     valueIndex++)
                {
                    ImportedValueDefinition value =
                        presetDefinition.Values[valueIndex];
                    object existingValue;

                    if (!preset.TryGetValue(value.Key, out existingValue) ||
                        !String.Equals(
                            existingValue as string,
                            value.Value,
                            StringComparison.Ordinal))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static void ValidateImportMode(PresetImportMode mode)
        {
            if (mode != PresetImportMode.Merge &&
                mode != PresetImportMode.PreserveExisting &&
                mode != PresetImportMode.Replace)
            {
                throw new ArgumentOutOfRangeException("mode");
            }
        }

        private static void ValidateImportedIdentifier(
            string value,
            string description)
        {
            if (String.IsNullOrEmpty(value) || value.Trim().Length == 0)
            {
                throw new InvalidOperationException(
                    description + " must be non-empty.");
            }

            if (value.Length != value.Trim().Length)
            {
                throw new InvalidOperationException(
                    description +
                    " cannot contain leading or trailing whitespace.");
            }
        }

        private sealed class ImportedSetDefinition
        {
            private readonly string _name;
            private readonly List<ImportedPresetDefinition> _presets;
            private string _selectedName;
            private string _defaultName;

            internal ImportedSetDefinition(string name)
            {
                _name = name;
                _presets = new List<ImportedPresetDefinition>();
            }

            internal string Name
            {
                get { return _name; }
            }

            internal List<ImportedPresetDefinition> Presets
            {
                get { return _presets; }
            }

            internal string SelectedName
            {
                get { return _selectedName; }
                set { _selectedName = value; }
            }

            internal string DefaultName
            {
                get { return _defaultName; }
                set { _defaultName = value; }
            }
        }

        private sealed class NotificationDeferral : IDisposable
        {
            private PresetManager _owner;

            internal NotificationDeferral(PresetManager owner)
            {
                _owner = owner;
            }

            public void Dispose()
            {
                if (_owner == null)
                    return;

                PresetManager owner = _owner;
                _owner = null;
                owner.EndNotificationDeferral();
            }
        }

        private sealed class ImportedPresetDefinition
        {
            private readonly string _name;
            private readonly List<ImportedValueDefinition> _values;

            internal ImportedPresetDefinition(string name)
            {
                _name = name;
                _values = new List<ImportedValueDefinition>();
            }

            internal string Name
            {
                get { return _name; }
            }

            internal List<ImportedValueDefinition> Values
            {
                get { return _values; }
            }
        }

        private sealed class ImportedValueDefinition
        {
            private readonly string _key;
            private readonly string _value;

            internal ImportedValueDefinition(
                string key,
                string value)
            {
                _key = key;
                _value = value;
            }

            internal string Key
            {
                get { return _key; }
            }

            internal string Value
            {
                get { return _value; }
            }
        }

        private static bool IsSetElement(XmlElement element)
        {
            return
                element != null &&
                String.Equals(
                    element.LocalName,
                    "Presets",
                    StringComparison.OrdinalIgnoreCase);
        }

        private static string GetAttribute(
            XmlElement element,
            string name)
        {
            int i;

            for (i = 0; i < element.Attributes.Count; i++)
            {
                XmlAttribute attribute = element.Attributes[i];

                if (String.Equals(
                    attribute.LocalName,
                    name,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return attribute.Value;
                }
            }

            return null;
        }

        private static void ValidateAllowedAttributes(
            XmlElement element,
            string[] allowedNames)
        {
            Dictionary<string, bool> seen =
                new Dictionary<string, bool>(
                    StringComparer.OrdinalIgnoreCase);
            int attributeIndex;

            for (attributeIndex = 0;
                 attributeIndex < element.Attributes.Count;
                 attributeIndex++)
            {
                XmlAttribute attribute =
                    element.Attributes[attributeIndex];

                if (IsIgnoredMetadataAttribute(attribute))
                    continue;

                bool allowed = false;
                int nameIndex;

                for (nameIndex = 0;
                     nameIndex < allowedNames.Length;
                     nameIndex++)
                {
                    if (attribute.NamespaceURI.Length == 0 &&
                        String.Equals(
                            attribute.LocalName,
                            allowedNames[nameIndex],
                            StringComparison.OrdinalIgnoreCase))
                    {
                        allowed = true;
                        break;
                    }
                }

                if (!allowed)
                {
                    throw new InvalidOperationException(
                        "Unexpected '" + attribute.Name +
                        "' attribute on <" + element.LocalName + ">.");
                }

                if (seen.ContainsKey(attribute.LocalName))
                {
                    throw new InvalidOperationException(
                        "The <" + element.LocalName +
                        "> element contains the '" +
                        attribute.LocalName + "' attribute more than once.");
                }

                seen.Add(attribute.LocalName, true);
            }
        }

        private static bool IsIgnoredMetadataAttribute(
            XmlAttribute attribute)
        {
            if (attribute.NamespaceURI.Length == 0 &&
                (String.Equals(
                    attribute.Name,
                    "__WfxPath",
                    StringComparison.Ordinal) ||
                 String.Equals(
                    attribute.Name,
                    MarkupXmlDocument.LocationAttributeName,
                    StringComparison.Ordinal)))
            {
                return true;
            }

            if (String.Equals(
                    attribute.Name,
                    "xmlns",
                    StringComparison.Ordinal) ||
                String.Equals(
                    attribute.Prefix,
                    "xmlns",
                    StringComparison.Ordinal))
            {
                return true;
            }

            if (!String.Equals(
                    attribute.NamespaceURI,
                    "http://www.w3.org/2001/XMLSchema-instance",
                    StringComparison.Ordinal))
            {
                return false;
            }

            return String.Equals(
                    attribute.LocalName,
                    "schemaLocation",
                    StringComparison.Ordinal) ||
                String.Equals(
                    attribute.LocalName,
                    "noNamespaceSchemaLocation",
                    StringComparison.Ordinal);
        }

        private static void ValidateIgnorableContent(
            XmlNode node,
            XmlElement parent)
        {
            if (node == null ||
                node.NodeType == XmlNodeType.Comment ||
                node.NodeType == XmlNodeType.Whitespace ||
                node.NodeType == XmlNodeType.SignificantWhitespace ||
                node.NodeType == XmlNodeType.ProcessingInstruction)
            {
                return;
            }

            if ((node.NodeType == XmlNodeType.Text ||
                 node.NodeType == XmlNodeType.CDATA) &&
                node.Value != null &&
                node.Value.Trim().Length == 0)
            {
                return;
            }

            throw new InvalidOperationException(
                "Unexpected content inside <" +
                parent.LocalName + ">.");
        }

        private static void ValidateName(
            string value,
            string parameterName)
        {
            if (String.IsNullOrEmpty(value) || value.Trim().Length == 0)
            {
                throw new ArgumentException(
                    "A non-empty preset " + parameterName + " is required.",
                    parameterName);
            }

            if (value.Length != value.Trim().Length)
            {
                throw new ArgumentException(
                    "Preset " + parameterName +
                    " cannot contain leading or trailing whitespace.",
                    parameterName);
            }
        }
    }

    /// <summary>
    /// Represents one named collection of switchable presets with selected and
    /// optional default entries.
    /// </summary>
    public sealed class PresetSet
    {
        private readonly PresetManager _owner;
        private readonly string _name;
        private readonly Dictionary<string, Preset> _presets;
        private readonly List<Preset> _presetOrder;
        private string _selectedName;
        private string _defaultName;
        private bool _retired;

        internal PresetSet(
            PresetManager owner,
            string name)
        {
            _owner = owner;
            _name = name;
            _presets =
                new Dictionary<string, Preset>(
                    StringComparer.OrdinalIgnoreCase);
            _presetOrder = new List<Preset>();
        }

        /// <summary>Gets the immutable set name.</summary>
        public string Name
        {
            get { return _name; }
        }

        /// <summary>Gets or selects the active preset name.</summary>
        public string SelectedName
        {
            get { return _selectedName; }
            set { Select(value); }
        }

        /// <summary>Gets or sets the fallback preset name.</summary>
        public string DefaultName
        {
            get { return _defaultName; }
            set { SetDefault(value); }
        }

        /// <summary>Gets a live, read-only view of the preset names.</summary>
        public ICollection<string> Names
        {
            get { return _presets.Keys; }
        }

        /// <summary>Gets a named preset.</summary>
        public Preset this[string name]
        {
            get { return GetPreset(name); }
        }

        /// <summary>Returns whether this set contains a named preset.</summary>
        public bool Contains(string name)
        {
            return
                !String.IsNullOrEmpty(name) &&
                _presets.ContainsKey(name);
        }

        /// <summary>Gets a named preset or throws when it does not exist.</summary>
        public Preset GetPreset(string name)
        {
            Preset preset;

            if (String.IsNullOrEmpty(name) ||
                !_presets.TryGetValue(name, out preset))
            {
                throw new KeyNotFoundException(
                    "Preset '" + name + "' was not found in set '" + _name + "'.");
            }

            return preset;
        }

        /// <summary>Adds an empty preset and returns its mutable handle.</summary>
        public Preset AddPreset(string name)
        {
            EnsureActive();

            if (Contains(name))
            {
                throw new InvalidOperationException(
                    "Preset '" + name + "' already exists in set '" + _name + "'.");
            }

            Preset preset = AddPresetInternal(name, true);

            _owner.RaiseChanged(_name, name, null);
            return preset;
        }

        /// <summary>
        /// Removes and retires a preset. Returns false when it does not exist.
        /// </summary>
        public bool RemovePreset(string name)
        {
            EnsureActive();

            Preset preset;

            if (String.IsNullOrEmpty(name) ||
                !_presets.TryGetValue(name, out preset))
            {
                return false;
            }

            _presets.Remove(name);
            _presetOrder.Remove(preset);
            preset.Retire();

            if (String.Equals(
                _defaultName,
                name,
                StringComparison.OrdinalIgnoreCase))
            {
                _defaultName = null;
            }

            if (String.Equals(
                _selectedName,
                name,
                StringComparison.OrdinalIgnoreCase))
            {
                _selectedName = null;
                SelectFirstAvailable();
            }

            _owner.RaiseChanged(_name, name, null);
            return true;
        }

        /// <summary>Selects an existing preset.</summary>
        public void Select(string name)
        {
            EnsureActive();
            GetPreset(name);

            if (String.Equals(
                _selectedName,
                name,
                StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _selectedName = name;
            _owner.RaiseChanged(_name, name, null);
        }

        /// <summary>Sets an existing preset as the fallback.</summary>
        public void SetDefault(string name)
        {
            EnsureActive();
            GetPreset(name);

            if (String.Equals(
                _defaultName,
                name,
                StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _defaultName = name;
            _owner.RaiseChanged(_name, name, null);
        }

        /// <summary>
        /// Resolves a key from the selected preset and then the configured
        /// default. The strict API throws when neither preset defines it;
        /// markup uses TryResolve so an absent optional value leaves the
        /// target property at its normal default.
        /// </summary>
        public object Resolve(string key)
        {
            if (String.IsNullOrEmpty(key))
                throw new ArgumentException("A preset key is required.", "key");

            object value;

            if (TryResolve(key, out value))
                return value;

            throw new KeyNotFoundException(
                "Key '" + key + "' was not found in selected preset '" +
                _selectedName + "'" +
                (String.IsNullOrEmpty(_defaultName)
                    ? " and no default preset is configured"
                    : " or configured default preset '" +
                      _defaultName + "'") +
                " of set '" + _name + "'.");
        }

        /// <summary>
        /// Tries the selected preset and then the configured default preset.
        /// Unrelated presets are never searched implicitly.
        /// </summary>
        public bool TryResolve(string key, out object value)
        {
            EnsureActive();

            if (String.IsNullOrEmpty(key))
                throw new ArgumentException("A preset key is required.", "key");

            Preset preset;

            if (!String.IsNullOrEmpty(_selectedName) &&
                _presets.TryGetValue(_selectedName, out preset) &&
                preset.TryGetValue(key, out value))
            {
                return true;
            }

            if (!String.IsNullOrEmpty(_defaultName) &&
                !String.Equals(
                    _defaultName,
                    _selectedName,
                    StringComparison.OrdinalIgnoreCase) &&
                _presets.TryGetValue(_defaultName, out preset) &&
                preset.TryGetValue(key, out value))
            {
                return true;
            }

            value = null;
            return false;
        }

        internal Preset AddPresetInternal(string name)
        {
            return AddPresetInternal(name, true);
        }

        internal Preset AddPresetInternal(
            string name,
            bool selectIfNeeded)
        {
            EnsureActive();

            if (String.IsNullOrEmpty(name) || name.Trim().Length == 0)
                throw new ArgumentException("A preset name is required.", "name");

            if (name.Length != name.Trim().Length)
            {
                throw new ArgumentException(
                    "A preset name cannot contain leading or trailing whitespace.",
                    "name");
            }

            Preset preset =
                new Preset(_owner, _name, name);

            _presets.Add(name, preset);
            _presetOrder.Add(preset);

            if (selectIfNeeded && _selectedName == null)
                _selectedName = name;

            return preset;
        }

        internal void SelectInternal(string name)
        {
            _selectedName = name;
        }

        internal void SetDefaultInternal(string name)
        {
            _defaultName = name;
        }

        internal void SelectFirstAvailable()
        {
            if (!String.IsNullOrEmpty(_defaultName) &&
                _presets.ContainsKey(_defaultName))
            {
                _selectedName = _defaultName;
                return;
            }

            if (_presetOrder.Count > 0)
            {
                _selectedName = _presetOrder[0].Name;
                return;
            }
        }

        internal void Retire()
        {
            if (_retired)
                return;

            _retired = true;

            foreach (KeyValuePair<string, Preset> pair in _presets)
                pair.Value.Retire();
        }

        private void EnsureActive()
        {
            if (_retired)
            {
                throw new InvalidOperationException(
                    "Preset set '" + _name +
                    "' is no longer attached to its PresetManager.");
            }
        }
    }

    /// <summary>Represents the mutable key/value map for one named preset.</summary>
    public sealed class Preset
    {
        private readonly PresetManager _owner;
        private readonly string _setName;
        private readonly string _name;
        private readonly Dictionary<string, object> _values;
        private bool _retired;

        internal Preset(
            PresetManager owner,
            string setName,
            string name)
        {
            _owner = owner;
            _setName = setName;
            _name = name;
            _values =
                new Dictionary<string, object>(
                    StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>Gets the immutable preset name.</summary>
        public string Name
        {
            get { return _name; }
        }

        /// <summary>Gets a live, read-only view of the defined keys.</summary>
        public ICollection<string> Keys
        {
            get { return _values.Keys; }
        }

        /// <summary>Gets or replaces a value by key.</summary>
        public object this[string key]
        {
            get { return GetValue(key); }
            set { SetValue(key, value); }
        }

        /// <summary>Returns whether this preset defines a key.</summary>
        public bool Contains(string key)
        {
            return
                !String.IsNullOrEmpty(key) &&
                _values.ContainsKey(key);
        }

        /// <summary>Gets a value or throws when the key is not defined.</summary>
        public object GetValue(string key)
        {
            object value;

            if (!TryGetValue(key, out value))
            {
                throw new KeyNotFoundException(
                    "Key '" + key + "' was not found in preset '" + _name + "'.");
            }

            return value;
        }

        /// <summary>Tries to read a value without throwing for a missing key.</summary>
        public bool TryGetValue(
            string key,
            out object value)
        {
            value = null;

            return
                !String.IsNullOrEmpty(key) &&
                _values.TryGetValue(key, out value);
        }

        /// <summary>Adds a value and rejects an existing key.</summary>
        public void AddValue(
            string key,
            object value)
        {
            EnsureActive();
            ValidateKey(key);

            if (_values.ContainsKey(key))
            {
                throw new InvalidOperationException(
                    "Key '" + key + "' already exists in preset '" + _name + "'.");
            }

            _values.Add(key, value);
            _owner.RaiseChanged(_setName, _name, key);
        }

        /// <summary>Adds or replaces a value and notifies the owning manager.</summary>
        public void SetValue(
            string key,
            object value)
        {
            EnsureActive();
            ValidateKey(key);

            object existing;

            if (_values.TryGetValue(key, out existing) &&
                Object.Equals(existing, value))
            {
                return;
            }

            SetValueInternal(key, value);
            _owner.RaiseChanged(_setName, _name, key);
        }

        /// <summary>Removes a value and returns whether the key existed.</summary>
        public bool RemoveValue(string key)
        {
            EnsureActive();

            if (String.IsNullOrEmpty(key) || !_values.Remove(key))
                return false;

            _owner.RaiseChanged(_setName, _name, key);
            return true;
        }

        internal void SetValueInternal(
            string key,
            object value)
        {
            _values[key] = value;
        }

        internal void Retire()
        {
            _retired = true;
        }

        private void EnsureActive()
        {
            if (_retired)
            {
                throw new InvalidOperationException(
                    "Preset '" + _name + "' in set '" +
                    _setName +
                    "' is no longer attached to its PresetManager.");
            }
        }

        private static void ValidateKey(string key)
        {
            if (String.IsNullOrEmpty(key) || key.Trim().Length == 0)
                throw new ArgumentException("A preset key is required.", "key");

            if (key.Length != key.Trim().Length)
            {
                throw new ArgumentException(
                    "A preset key cannot contain leading or trailing whitespace.",
                    "key");
            }
        }
    }
}
