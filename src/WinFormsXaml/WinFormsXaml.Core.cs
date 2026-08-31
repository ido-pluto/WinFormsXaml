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
    /// <summary>
    /// Loads an XML object tree into native Windows Forms objects and retains
    /// the metadata required for binding, preset, layout, and item reloads.
    /// </summary>
    public sealed partial class XamlRuntime : IDisposable
    {
        private object _root;
        private object _eventTarget;
        private XmlForm _xmlFormLifetimeTarget;
        private IDisposable _ownedMarkupClassTarget;
        private object _failedLoadRoot;
        private bool _failedLoadRollbackPending;
        private bool _disposeInProgress;
        private bool _disposeCompleted;
        private bool _rootDisposalCompleted;
        private string _basePath;
        private Assembly _markupAssembly;
        private Assembly _activeMarkupAssembly;
        private string _markupSource;
        private string _activeMarkupSource;
        private string _activeMarkupElementPathPrefix;
        private ToolTip _toolTip;

        private Dictionary<string, object> _namedObjects;
        private Dictionary<object, ElementInfo> _elementInfos;
        private long _elementCollapseRevision;

        private List<StyleDefinition> _implicitStyles;
        private Dictionary<string, StyleDefinition> _namedStyles;

        // While an ItemTemplate is being instantiated, x:Name values belong
        // to that template instance and must not be registered globally.
        private int _templateBuildDepth;

        // XML attributes can only contain text. Function/property bindings may
        // return real CLR objects (Image, Font, Padding, custom objects, etc.).
        // Such values are temporarily represented by an internal token while
        // the XML element is being built and are then assigned as the real type.
        private Dictionary<string, object> _boundObjectValues;
        private int _nextBoundObjectId;

        // During an incremental ItemsControl refresh, Function bindings are
        // evaluated once for diffing and cached here while a replacement item
        // is built. This prevents an expensive Function (for example an image
        // lookup/decoder) from being called twice during the same refresh.
        private Hashtable _activeFunctionResultCache;

        // While an ItemTemplate is instantiated, each XML element is annotated with
        // an internal logical path. BuildElement records the Control created for that
        // path here. The resulting map lets ReloadItems patch a specific Text/Image/
        // color/size/etc. property instead of regenerating the whole item tree.
        private Hashtable _activeTemplateElementMap;
        private object _activeTemplateDataContext;
        private ArrayList _activeTemplateStyleBindingSlots;
        private CompiledItemTemplate _activeCompiledItemTemplate;

        // Reflection over all code-behind methods on every Function binding is expensive.
        // Cache the small candidate list for each function name once per XAML runtime.
        private Hashtable _bindingFunctionMethodsCache;
        // MethodInfo.GetParameters returns an array on every call. Function
        // bindings reuse the same reflected candidates, so retain their immutable
        // parameter metadata for the lifetime of this runtime as well.
        private Hashtable _bindingFunctionParametersCache;
        private Hashtable _bindingFunctionInvocationPlans;
        private int _bindingFunctionInvocationPlanCount;
        private long _bindingFunctionInvocationPlanHitCount;
        private Hashtable _eventHandlerMethodsCache;
        private MethodInfo[] _eventTargetMethods;
        private Dictionary<XmlElement, CompiledItemTemplate> _compiledItemTemplates;
        private Hashtable _xamlTypeCache;
        private Hashtable _resolvedTypeNameCache;
        private Hashtable _implicitStyleMatchCaches;
        private int _implicitStyleMatchCacheEntryCount;
        private long _implicitStyleMatchCacheHitCount;
        private Hashtable _resolvedStyleChainCaches;
        private int _resolvedStyleChainCacheEntryCount;
        private long _resolvedStyleChainCacheHitCount;
        private Hashtable _convertedStringValueCaches;
        private int _convertedStringValueCacheEntryCount;
        private long _convertedStringValueCacheHitCount;
        private Hashtable _templateExpressionPlanCache;
        private Hashtable _functionArgumentPartsCache;
        private ArrayList _decodedImageCache;
        private WeakDecodedImageCacheEntry[] _decodedImageMru;
        private int _decodedImageCacheValidationGeneration;
        private int _decodedImageCacheForcedValidationDepth;
        // Target buckets use reference identity; their property keys are
        // ordinal-ignore-case. Shared IDisposable values are counted by identity.
        private Hashtable _ownedPropertyValues;
        private Hashtable _ownedPropertyValueReferenceCounts;
        private ArrayList _boundEvents;
        // Global ordering remains available for deterministic disposal, while
        // normal lookup/release work stays within the target's identity bucket.
        private Hashtable _boundEventsByTarget;
        private ArrayList _boundEventReleaseTargets;
        private long _boundEventDisposalEpoch;

        // Binding paths are evaluated very frequently. Reflection member lookup is cached
        // globally by (runtime Type, member name); only GetValue remains on the hot path.
        // Admission stops at the limits so a stream of novel markup cannot evict the
        // established hot set or retain arbitrarily many Type/string instances.
        private const int BindingMemberTypeCacheLimit = 256;
        private const int BindingMemberNameCacheLimit = 256;
        private const int BindingPathPartsCacheLimit = 512;
        private static Hashtable _bindingMemberLookupCache = new Hashtable();
        private static object _bindingMemberLookupCacheLock = new object();
        private static Hashtable _bindingPathPartsCache = new Hashtable();
        private static object _bindingPathPartsCacheLock = new object();
        private const int ReflectionTypeCacheLimit = 256;
        private const int ReflectionMemberNameCacheLimit = 256;
        private static Hashtable _propertyInfoCache = new Hashtable();
        private static Hashtable _eventInfoCache = new Hashtable();
        private static object _reflectionInfoCacheLock = new object();
        private static object _missingReflectionInfo = new object();
        private static readonly object _nullItemDataContext = new object();
        private static readonly MethodInfo[] _emptyMethodInfoArray =
            new MethodInfo[0];

        // Fonts are immutable GDI resources and commonly repeat across generated controls.
        // Weak values share live instances without pinning every dynamic combination for the
        // lifetime of the process; the lookup table is pruned in GetCachedFont.
        private static Hashtable _fontCache = new Hashtable();
        private static object _fontCacheLock = new object();
        private const int ValueParseCacheLimit = 256;
        private static Hashtable _colorParseCache = new Hashtable(StringComparer.OrdinalIgnoreCase);
        private static Hashtable _thicknessParseCache = new Hashtable(StringComparer.Ordinal);
        private static object _valueParseCacheLock = new object();
        private static object _missingTypeCacheValue = new object();
        private static readonly object[] _emptyObjectArray = new object[0];
        private static readonly string[] _emptyStringArray = new string[0];
        private static readonly StyleDefinition[] _emptyStyleDefinitionArray =
            new StyleDefinition[0];
        private const int ImplicitStyleMatchCacheLimit = 128;
        private const int ImplicitStyleMatchCachePerScopeLimit = 16;
        private const int ResolvedStyleChainCacheLimit = 128;
        private const int ResolvedStyleChainCachePerScopeLimit = 32;
        private const int ConvertedStringValueCacheLimit = 256;
        private const int ConvertedStringValueCachePerTypeLimit = 64;
        private const int BindingFunctionMethodCacheLimit = 128;
        private const int BindingFunctionParameterCacheLimit = 256;
        private const int EventHandlerMethodCacheLimit = 128;
        private const int XamlTypeCacheLimit = 256;
        private const int ResolvedTypeNameCacheLimit = 256;
        private const int TemplateExpressionPlanCacheLimit = 512;
        private const int FunctionArgumentPartsCacheLimit = 256;
        private const int RuntimeMetadataCacheKeyLengthLimit = 1024;

        private sealed class RuntimeObjectReferenceComparer
            : System.Collections.IEqualityComparer,
              IEqualityComparer<object>
        {
            public new bool Equals(object left, object right)
            {
                return Object.ReferenceEquals(left, right);
            }

            public int GetHashCode(object value)
            {
                return value == null
                    ? 0
                    : System.Runtime.CompilerServices.RuntimeHelpers
                        .GetHashCode(value);
            }
        }

        private static readonly RuntimeObjectReferenceComparer
            _runtimeObjectReferenceComparer =
                new RuntimeObjectReferenceComparer();

        private static object GetItemDataContext(object item)
        {
            return item == null
                ? _nullItemDataContext
                : item;
        }

        private static object UnwrapDataContext(object dataContext)
        {
            return Object.ReferenceEquals(
                    dataContext,
                    _nullItemDataContext)
                ? null
                : dataContext;
        }

        private object ResolveBindingSource(object dataContext)
        {
            if (Object.ReferenceEquals(
                    dataContext,
                    _nullItemDataContext))
            {
                return null;
            }

            return dataContext == null
                ? _eventTarget
                : dataContext;
        }

        private object ResolveBindingSource(
            object dataContext,
            BindingExpressionPlan plan)
        {
            BindingSourceKind source = plan == null
                ? BindingSourceKind.Current
                : plan.Source;

            if (source == BindingSourceKind.CodeBehind)
            {
                object codeBehind =
                    GetComponentEventTarget(dataContext);

                if (codeBehind == null)
                {
                    throw new InvalidOperationException(
                        "Binding Source=CodeBehind requires a code-behind/event target.");
                }

                return codeBehind;
            }

            return ResolveBindingSource(dataContext);
        }

        private object GetComponentEventTarget(object dataContext)
        {
            ComponentValueContext componentContext =
                dataContext as ComponentValueContext;

            if (componentContext != null &&
                componentContext.CodeBehind != null)
            {
                return componentContext.CodeBehind;
            }

            return _activeComponentEventTarget == null
                ? _eventTarget
                : _activeComponentEventTarget;
        }

        private enum TemplateExpressionKind
        {
            Literal,
            Binding,
            Function,
            Preset,
            PresetCondition,
            Interpolated
        }

        private sealed class TemplateExpressionPlan
        {
            public TemplateExpressionKind Kind;
            public BindingExpressionPlan BindingPlan;
            public string MethodName;
            public string ArgumentText;
            public string PresetSetName;
            public string PresetKey;
            public PresetConditionExpressionPlan PresetConditionPlan;
            public bool AutomaticDataContext;
        }

        private sealed class WeakDecodedImageCacheEntry
        {
            public WeakReference Source;
            public WeakReference Image;
            public ulong ContentFingerprint;
            public int ContentValidationGeneration;
        }

        private sealed class OwnedPropertyValue
        {
            public object Target;
            public string PropertyName;
            public IDisposable Value;
        }

        private sealed class OwnedPropertyValueReferenceCount
        {
            public int Count;
        }

        private interface IEventHandlerForwarder
        {
            void Disable();
        }

        private sealed class BoundEventRegistration
        {
            public object Target;
            public EventInfo Event;
            public Delegate Handler;
            public Delegate SourceHandler;
            public IEventHandlerForwarder Forwarder;
            public long Revision;
            public long DisposalEpoch;
            public bool LocalOwner;
            public bool StyleOwner;
            public bool Disabled;
            public bool AddAttempted;
            public bool Adding;
            public bool DetachRequested;
            public bool Removing;
            public bool Detached;
            public bool Tracked;
        }

        private sealed class BoundEventTargetBucket
        {
            public readonly ArrayList Registrations = new ArrayList();
        }

        private sealed class EventHandlerForwarder<TEventArgs>
            : IEventHandlerForwarder
            where TEventArgs : EventArgs
        {
            private EventHandler<TEventArgs> _handler;
            private bool _enabled;

            public EventHandlerForwarder(Delegate handler)
            {
                if (handler.Target == null)
                {
                    _handler =
                        (EventHandler<TEventArgs>)Delegate.CreateDelegate(
                            typeof(EventHandler<TEventArgs>),
                            handler.Method);
                }
                else
                {
                    _handler =
                        (EventHandler<TEventArgs>)Delegate.CreateDelegate(
                            typeof(EventHandler<TEventArgs>),
                            handler.Target,
                            handler.Method);
                }

                _enabled = true;
            }

            public void Disable()
            {
                _enabled = false;
                _handler = null;
            }

            public void Invoke(object sender, TEventArgs e)
            {
                EventHandler<TEventArgs> handler = _handler;

                if (_enabled && handler != null)
                    handler(sender, e);
            }
        }

        private sealed class BindingMemberLookup
        {
            public PropertyInfo Property;
            public FieldInfo Field;
            public bool Missing;
        }

        private sealed class PropertyReflectionCache
        {
            public Hashtable Members;
            public PropertyInfo[][] DeclaredByDepth;
        }

        private sealed class EventReflectionCache
        {
            public Hashtable Members;
            public EventInfo[][] DeclaredByDepth;
        }

        private XamlRuntime(
            object eventTarget,
            string basePath,
            PresetManager presetManager,
            Assembly markupAssembly)
        {
            _eventTarget = eventTarget;
            _basePath = basePath;
            _markupAssembly = markupAssembly;
            _activeMarkupAssembly = markupAssembly;

            InitializeDynamicFeatures(presetManager);

            _namedObjects =
                new Dictionary<string, object>(
                    StringComparer.OrdinalIgnoreCase);

            _elementInfos =
                new Dictionary<object, ElementInfo>(
                    _runtimeObjectReferenceComparer);
            _elementCollapseRevision = 0L;

            _implicitStyles =
                new List<StyleDefinition>();

            _namedStyles =
                new Dictionary<string, StyleDefinition>(
                    StringComparer.OrdinalIgnoreCase);

            _boundObjectValues =
                new Dictionary<string, object>();

            _nextBoundObjectId = 1;

            _ownedPropertyValues =
                new Hashtable(_runtimeObjectReferenceComparer);
            _ownedPropertyValueReferenceCounts =
                new Hashtable(_runtimeObjectReferenceComparer);
            _boundEvents = new ArrayList();
            _boundEventsByTarget =
                new Hashtable(_runtimeObjectReferenceComparer);
            _boundEventReleaseTargets = new ArrayList();
            _boundEventDisposalEpoch = 0;

            _bindingFunctionInvocationPlanCount = 0;
            _bindingFunctionInvocationPlanHitCount = 0L;

            _xamlTypeCache =
                new Hashtable(
                    StringComparer.OrdinalIgnoreCase);

            _resolvedTypeNameCache =
                new Hashtable(
                    StringComparer.OrdinalIgnoreCase);

            _implicitStyleMatchCacheEntryCount = 0;
            _implicitStyleMatchCacheHitCount = 0L;
            _resolvedStyleChainCacheEntryCount = 0;
            _resolvedStyleChainCacheHitCount = 0L;
            _convertedStringValueCacheEntryCount = 0;
            _convertedStringValueCacheHitCount = 0L;

        }

        // ============================================================
        // PUBLIC API
        // ============================================================

        /// <summary>Gets the object created from the root XML element.</summary>
        public object Root
        {
            get { return _root; }
        }

        /// <summary>Gets whether this runtime reached terminal disposal.</summary>
        public bool IsDisposed
        {
            get { return _disposeCompleted; }
        }

        /// <summary>Gets the root as a Control, or null for a non-control root.</summary>
        public Control RootControl
        {
            get { return _root as Control; }
        }

        /// <summary>
        /// Gets the root as a Form. This is the common entry point for XML files
        /// whose root element is Form.
        /// </summary>
        public Form Form
        {
            get
            {
                Form form = _root as Form;

                if (form == null)
                {
                    throw new InvalidOperationException(
                        "The XML root is not a System.Windows.Forms.Form.");
                }

                return form;
            }
        }

        /// <summary>Gets the case-insensitive map of globally named objects.</summary>
        public IDictionary<string, object> NamedObjects
        {
            get { return _namedObjects; }
        }

        /// <summary>Gets the names registered in the global object tree.</summary>
        public ICollection<string> Names
        {
            get { return _namedObjects.Keys; }
        }

        /// <summary>Gets a required named object.</summary>
        public object this[string name]
        {
            get
            {
                object value;

                if (!_namedObjects.TryGetValue(name, out value))
                {
                    throw new KeyNotFoundException(
                        "No XAML element named '" +
                        name +
                        "' exists.");
                }

                return value;
            }
        }

        /// <summary>Gets a required named object as the requested CLR type.</summary>
        public T Get<T>(string name) where T : class
        {
            object value = this[name];

            T result = value as T;

            if (result == null)
            {
                throw new InvalidCastException(
                    "'" +
                    name +
                    "' is " +
                    value.GetType().FullName +
                    ", not " +
                    typeof(T).FullName +
                    ".");
            }

            return result;
        }

        /// <summary>Gets a required named object as a WinForms Control.</summary>
        public Control GetControl(string name)
        {
            Control control =
                this[name] as Control;

            if (control == null)
            {
                throw new InvalidCastException(
                    "'" +
                    name +
                    "' is not a WinForms Control.");
            }

            return control;
        }

        /// <summary>Returns whether the global object tree contains a name.</summary>
        public bool Contains(string name)
        {
            return _namedObjects.ContainsKey(name);
        }

        /// <summary>Gets a required named ItemsControl.</summary>
        public ItemsControl GetItemsControl(string name)
        {
            object value = this[name];

            ItemsControl items =
                value as ItemsControl;

            if (items == null)
            {
                throw new InvalidCastException(
                    "'" +
                    name +
                    "' is not an ItemsControl.");
            }

            return items;
        }

        /// <summary>Assigns an IEnumerable to a named ItemsControl and renders it.</summary>
        public void SetItems(
            string name,
            IEnumerable items)
        {
            GetItemsControl(name).SetItems(items);
        }

        /// <summary>Re-enumerates the existing ItemsControl source and incrementally refreshes its UI.</summary>
        public void ReloadItems(string name)
        {
            GetItemsControl(name).ReloadItems();
        }

        /// <summary>Forces all repeated item visuals to rebuild, bypassing keyed control reuse.</summary>
        public void ForceReloadItems(string name)
        {
            GetItemsControl(name).ForceReloadItems();
        }

        /// <summary>Clears the source and rendered children of a named ItemsControl.</summary>
        public void ClearItems(string name)
        {
            GetItemsControl(name).ClearItems();
        }

        /// <summary>Loads an XML interface without a code-behind target.</summary>
        public static XamlRuntime Load(string xaml)
        {
            return Load(xaml, null, null, null, null, "inline XML");
        }

        /// <summary>Loads an XML interface with a binding and event target.</summary>
        public static XamlRuntime Load(
            string xaml,
            object eventTarget)
        {
            return Load(
                xaml,
                eventTarget,
                null,
                null,
                null,
                "inline XML");
        }

        /// <summary>
        /// Loads an XML interface and resolves relative files from a base path.
        /// </summary>
        public static XamlRuntime Load(
            string xaml,
            object eventTarget,
            string basePath)
        {
            return Load(
                xaml,
                eventTarget,
                basePath,
                null,
                null,
                "inline XML");
        }

        /// <summary>
        /// Loads XAML with a preset manager that may be shared across forms.
        /// Inline preset declarations add only missing values to the supplied
        /// manager, preserving values and selections already configured by the app.
        /// </summary>
        public static XamlRuntime Load(
            string xaml,
            object eventTarget,
            string basePath,
            PresetManager presetManager)
        {
            return Load(
                xaml,
                eventTarget,
                basePath,
                presetManager,
                null,
                "inline XML");
        }

        private static XamlRuntime Load(
            string xaml,
            object eventTarget,
            string basePath,
            PresetManager presetManager,
            Assembly markupAssembly,
            string markupSource)
        {
            if (String.IsNullOrEmpty(xaml))
            {
                throw new ArgumentException(
                    "XAML cannot be empty.",
                    "xaml");
            }

            MarkupXmlDocument document =
                new MarkupXmlDocument();

            document.PreserveWhitespace =
                false;

            document.XmlResolver = null;

            try
            {
                document.LoadMarkup(xaml);
            }
            catch (XmlException ex)
            {
                throw new WinFormsXamlLoadException(
                    markupSource,
                    null,
                    null,
                    ex.LineNumber,
                    ex.LinePosition,
                    ex);
            }
            catch (Exception ex)
            {
                throw new WinFormsXamlLoadException(
                    markupSource,
                    null,
                    null,
                    0,
                    0,
                    ex);
            }

            return Load(
                document,
                eventTarget,
                basePath,
                presetManager,
                markupAssembly,
                markupSource);
        }

        private static XamlRuntime Load(
            Stream xamlStream,
            object eventTarget,
            string basePath,
            PresetManager presetManager,
            Assembly markupAssembly,
            string markupSource)
        {
            if (xamlStream == null)
                throw new ArgumentNullException("xamlStream");

            if (xamlStream.CanSeek &&
                xamlStream.Position >= xamlStream.Length)
            {
                throw new ArgumentException(
                    "XAML cannot be empty.",
                    "xaml");
            }

            MarkupXmlDocument document =
                new MarkupXmlDocument();

            document.PreserveWhitespace =
                false;

            document.XmlResolver = null;

            try
            {
                document.LoadMarkup(xamlStream);
            }
            catch (XmlException ex)
            {
                throw new WinFormsXamlLoadException(
                    markupSource,
                    null,
                    null,
                    ex.LineNumber,
                    ex.LinePosition,
                    ex);
            }
            catch (Exception ex)
            {
                throw new WinFormsXamlLoadException(
                    markupSource,
                    null,
                    null,
                    0,
                    0,
                    ex);
            }

            return Load(
                document,
                eventTarget,
                basePath,
                presetManager,
                markupAssembly,
                markupSource);
        }

        private static XamlRuntime Load(
            MarkupXmlDocument document,
            object eventTarget,
            string basePath,
            PresetManager presetManager,
            Assembly markupAssembly,
            string markupSource)
        {
            if (document.DocumentElement == null)
            {
                throw new WinFormsXamlLoadException(
                    markupSource,
                    null,
                    null,
                    0,
                    0,
                    new InvalidOperationException(
                        "The XML contains no root element."));
            }

            Type markupClassType;
            bool ownsMarkupClassTarget = false;

            try
            {
                // Resolve the Class type before composition so it supplies the
                // assembly context for registered includes. Only XmlForm needs
                // early construction: its constructor can queue Include calls.
                markupClassType = ResolveMarkupClassType(
                    document.DocumentElement,
                    eventTarget,
                    markupAssembly);

                if (eventTarget == null &&
                    markupClassType != null &&
                    typeof(XmlForm).IsAssignableFrom(markupClassType))
                {
                    eventTarget = CreateMarkupClassTarget(markupClassType);
                    ownsMarkupClassTarget = true;
                }
            }
            catch (WinFormsXamlLoadException)
            {
                throw;
            }
            catch (Exception ex)
            {
                int lineNumber;
                int linePosition;

                MarkupXmlDocument.GetLocation(
                    document.DocumentElement,
                    "Class",
                    out lineNumber,
                    out linePosition);

                throw new WinFormsXamlLoadException(
                    markupSource,
                    GetMarkupElementPath(
                        document.DocumentElement,
                        null),
                    "Class",
                    lineNumber,
                    linePosition,
                    ex);
            }

            XmlForm suppliedXmlForm = eventTarget as XmlForm;
            IList programmaticIncludes = null;
            Assembly includeMarkupAssembly = markupAssembly;

            if (includeMarkupAssembly == null && eventTarget != null)
                includeMarkupAssembly = eventTarget.GetType().Assembly;
            else if (includeMarkupAssembly == null && markupClassType != null)
                includeMarkupAssembly = markupClassType.Assembly;

            try
            {
                if (suppliedXmlForm != null)
                {
                    if (ownsMarkupClassTarget && suppliedXmlForm.IsLoaded)
                    {
                        throw new InvalidOperationException(
                            "An XmlForm created from the XML Class attribute " +
                            "started its own lazy load from its constructor. " +
                            "That constructor may call Include, but it must not " +
                            "access WinForm, Ui, Get, or Presets while the outer " +
                            "document is still being composed.");
                    }

                    programmaticIncludes =
                        suppliedXmlForm.SnapshotIncludeRequestsForLoad();
                }

                ComposeIncludes(
                    document,
                    basePath,
                    includeMarkupAssembly,
                    markupSource,
                    programmaticIncludes);
            }
            catch (Exception ex)
            {
                DisposeOwnedMarkupClassTarget(
                    eventTarget,
                    ownsMarkupClassTarget);

                if (ex is WinFormsXamlLoadException)
                    throw;

                int lineNumber;
                int linePosition;

                MarkupXmlDocument.GetLocation(
                    document.DocumentElement,
                    null,
                    out lineNumber,
                    out linePosition);

                throw new WinFormsXamlLoadException(
                    markupSource,
                    GetMarkupElementPath(
                        document.DocumentElement,
                        null),
                    null,
                    lineNumber,
                    linePosition,
                    ex);
            }

            if (eventTarget == null && markupClassType != null)
            {
                try
                {
                    eventTarget = CreateMarkupClassTarget(markupClassType);
                    ownsMarkupClassTarget = true;
                }
                catch (Exception ex)
                {
                    int lineNumber;
                    int linePosition;

                    MarkupXmlDocument.GetLocation(
                        document.DocumentElement,
                        "Class",
                        out lineNumber,
                        out linePosition);

                    throw new WinFormsXamlLoadException(
                        markupSource,
                        GetMarkupElementPath(
                            document.DocumentElement,
                            null),
                        "Class",
                        lineNumber,
                        linePosition,
                        ex);
                }
            }

            XamlRuntime runtime;

            try
            {
                runtime =
                    new XamlRuntime(
                        eventTarget,
                        basePath,
                        presetManager,
                        markupAssembly);
            }
            catch (Exception ex)
            {
                DisposeOwnedMarkupClassTarget(
                    eventTarget,
                    ownsMarkupClassTarget);

                if (ex is WinFormsXamlLoadException)
                    throw;

                int lineNumber;
                int linePosition;

                MarkupXmlDocument.GetLocation(
                    document.DocumentElement,
                    null,
                    out lineNumber,
                    out linePosition);

                throw new WinFormsXamlLoadException(
                    markupSource,
                    GetMarkupElementPath(
                        document.DocumentElement,
                        null),
                    null,
                    lineNumber,
                    linePosition,
                    ex);
            }

            runtime._markupSource = markupSource;
            runtime._activeMarkupSource = markupSource;

            XmlForm xmlFormTarget = eventTarget as XmlForm;

            if (ownsMarkupClassTarget)
            {
                runtime._ownedMarkupClassTarget =
                    eventTarget as IDisposable;
            }

            try
            {
                if (xmlFormTarget != null)
                {
                    xmlFormTarget.AttachRuntime(runtime);
                    runtime._xmlFormLifetimeTarget =
                        xmlFormTarget;
                }

                runtime._root =
                    runtime.BuildElement(
                        document.DocumentElement);

                runtime.InitializeNamedMemberWiring();
                runtime.OnDynamicRootReady();

                Control rootControl =
                    runtime._root as Control;

                if (rootControl != null)
                {
                    runtime.ApplyInheritedProperties(
                        rootControl,
                        null);

                    runtime.PerformLayoutRecursive(
                        rootControl);
                }

                if (xmlFormTarget != null)
                {
                    xmlFormTarget.CompleteRuntimeLoad(runtime);
                }

                return runtime;
            }
            catch (Exception ex)
            {
                if (xmlFormTarget != null &&
                    Object.ReferenceEquals(
                        runtime._xmlFormLifetimeTarget,
                        xmlFormTarget))
                {
                    xmlFormTarget.MarkRuntimeLoadFailed(runtime);
                }

                RollbackFailedLoad(runtime);

                if (ex is WinFormsXamlLoadException)
                    throw;

                throw runtime.CreateMarkupLoadException(
                    document.DocumentElement,
                    null,
                    ex);
            }
        }

        private static void DisposeOwnedMarkupClassTarget(
            object eventTarget,
            bool ownsMarkupClassTarget)
        {
            if (!ownsMarkupClassTarget)
                return;

            IDisposable disposableTarget = eventTarget as IDisposable;

            if (disposableTarget == null)
                return;

            try
            {
                disposableTarget.Dispose();
            }
            catch
            {
                // Preserve the load failure. Cleanup exceptions must not replace
                // the error that prevented the Form from loading.
            }
        }

        internal static void RollbackFailedLoad(XamlRuntime runtime)
        {
            if (runtime == null)
                return;

            if (!runtime._failedLoadRollbackPending)
            {
                runtime._failedLoadRoot = runtime._root;
                runtime._failedLoadRollbackPending = true;
            }

            Exception cleanupError = null;

            try
            {
                runtime.Dispose();
            }
            catch (Exception ex)
            {
                cleanupError = ex;

                // The load exception remains the primary failure. Continue
                // retaining retry debt even if cleanup code on a custom
                // component or code-behind object is broken.
            }

            if (cleanupError != null)
            {
                try
                {
                    runtime.RetainFailedLoadDisposalRetry();
                }
                catch
                {
                    // Never replace the construction failure with retry-hook
                    // setup. The target/runtime references remain published.
                }
            }

            runtime.CompleteFailedLoadRollbackIfSafe();
        }

        /// <summary>
        /// Disposes the XML-created root and releases retained bindings,
        /// subscriptions, metadata, and runtime-owned resources.
        /// </summary>
        public void Dispose()
        {
            if (_disposeInProgress)
                return;

            // Terminal disposal can still retain inert event-removal debt
            // when a custom remove accessor failed. Repeated Dispose calls
            // remain a no-op once that debt is empty, but while it exists they
            // provide the explicit retry boundary promised by IDisposable.
            // Do not revisit the root or any already released owned state.
            if (_disposeCompleted)
            {
                RetryCompletedDisposalDebt();
                return;
            }

            VerifyCanDispose();

            _disposeInProgress = true;

            try
            {
                DisposeCore();
                _disposeCompleted = true;
            }
            finally
            {
                _disposeInProgress = false;
            }
        }

        private void RetryCompletedDisposalDebt()
        {
            if (!HasRetainedEventRemovalDebt())
                return;

            // A custom event accessor may touch native Control state. Retained
            // removals therefore keep the same owner-thread requirement as
            // the initial runtime cleanup, even after root compaction.
            VerifyCanDispose();

            bool hasDynamicTargetDebt =
                _dynamicTargetDisposalRetryHooks != null &&
                _dynamicTargetDisposalRetryHooks.Count != 0;
            bool hasBoundEventDebt =
                _boundEvents != null &&
                _boundEvents.Count != 0;

            _disposeInProgress = true;

            try
            {
                Exception firstError = null;

                if (hasDynamicTargetDebt)
                {
                    try
                    {
                        RetryDynamicTargetDisposalHooks();
                    }
                    catch (Exception ex)
                    {
                        firstError = ex;
                    }
                }

                if (hasBoundEventDebt)
                {
                    try
                    {
                        DisposeBoundEvents();
                    }
                    catch (Exception ex)
                    {
                        if (firstError == null)
                            firstError = ex;
                    }
                }

                if (firstError != null)
                {
                    throw new InvalidOperationException(
                        "One or more retained WinFormsXaml event removals " +
                        "could not be retried: " + firstError.Message,
                        firstError);
                }
            }
            finally
            {
                _disposeInProgress = false;
            }
        }

        private bool HasRetainedEventRemovalDebt()
        {
            return
                (_dynamicTargetDisposalRetryHooks != null &&
                 _dynamicTargetDisposalRetryHooks.Count != 0) ||
                (_boundEvents != null && _boundEvents.Count != 0);
        }

        private void DisposeCore()
        {
            XmlForm xmlFormForPreparation =
                _xmlFormLifetimeTarget;

            if (xmlFormForPreparation != null)
            {
                xmlFormForPreparation
                    .PrepareForOwningRuntimeDisposal(this);
            }

            Exception firstError = null;
            Exception terminalRootError = null;
            bool completionBlocked = false;

            // Every successful load transfers ownership of its XML-created
            // root to the runtime, including direct XamlRuntime.Load calls and
            // non-Control roots. Dispose it while callbacks can still use the
            // runtime. A Control's Disposed hook reenters Dispose, so the outer
            // pass owns completion. Failed construction keeps its separate
            // rollback path because that path must retain retry debt.
            DisposeOwnedRoot(
                ref firstError,
                ref terminalRootError);

            if (firstError != null)
                completionBlocked = true;

            // Claim observable disposal only after every XmlForm-owned worker
            // has passed its bounded stop gate. A timed-out attempt can then be
            // retried without partially shutting down the runtime.
            PrepareObservableBindingDisposal();

            try
            {
                DisposeDynamicFeatures();
            }
            catch (Exception ex)
            {
                if (firstError == null)
                    firstError = ex;

                completionBlocked = true;
            }

            try
            {
                DisposeBoundEvents();
            }
            catch (Exception ex)
            {
                if (firstError == null)
                    firstError = ex;

                completionBlocked = true;
            }

            try
            {
                DisposeOwnedPropertyValues();
            }
            catch (Exception ex)
            {
                if (firstError == null)
                    firstError = ex;

                completionBlocked = completionBlocked ||
                    (_ownedPropertyValues != null &&
                     _ownedPropertyValues.Count != 0);
            }

            if (_decodedImageCache != null)
            {
                // Owned decoded images were released above. Drop the remaining
                // weak lookup entries as well so a disposed-but-still-referenced
                // runtime retains no cache bookkeeping.
                _decodedImageCache.Clear();
                _decodedImageCache = null;
            }

            _decodedImageMru = null;

            try
            {
                DisposeNamedMemberWiring();
            }
            catch (Exception ex)
            {
                if (firstError == null)
                    firstError = ex;

                completionBlocked = true;
            }

            if (_toolTip != null)
            {
                ToolTip toolTip = _toolTip;
                _toolTip = null;

                try
                {
                    toolTip.Dispose();
                }
                catch (Exception ex)
                {
                    if (firstError == null)
                        firstError = ex;
                }
            }

            IDisposable ownedMarkupClassTarget =
                _ownedMarkupClassTarget;
            XmlForm xmlFormLifetimeTarget =
                _xmlFormLifetimeTarget;
            _eventTarget = null;
            _eventTargetMethods = null;
            _eventHandlerMethodsCache = null;
            _bindingFunctionMethodsCache = null;
            _bindingFunctionParametersCache = null;
            _bindingFunctionInvocationPlans = null;
            _bindingFunctionInvocationPlanCount = 0;
            _bindingFunctionInvocationPlanHitCount = 0L;
            if (_compiledItemTemplates != null)
                _compiledItemTemplates.Clear();
            _compiledItemTemplates = null;
            if (_xamlTypeCache != null)
                _xamlTypeCache.Clear();
            _xamlTypeCache = null;
            if (_resolvedTypeNameCache != null)
                _resolvedTypeNameCache.Clear();
            _resolvedTypeNameCache = null;
            if (_templateExpressionPlanCache != null)
                _templateExpressionPlanCache.Clear();
            _templateExpressionPlanCache = null;
            if (_functionArgumentPartsCache != null)
                _functionArgumentPartsCache.Clear();
            _functionArgumentPartsCache = null;
            if (_implicitStyleMatchCaches != null)
                _implicitStyleMatchCaches.Clear();
            _implicitStyleMatchCaches = null;
            _implicitStyleMatchCacheEntryCount = 0;
            _implicitStyleMatchCacheHitCount = 0L;
            if (_resolvedStyleChainCaches != null)
                _resolvedStyleChainCaches.Clear();
            _resolvedStyleChainCaches = null;
            _resolvedStyleChainCacheEntryCount = 0;
            _resolvedStyleChainCacheHitCount = 0L;
            if (_convertedStringValueCaches != null)
                _convertedStringValueCaches.Clear();
            _convertedStringValueCaches = null;
            _convertedStringValueCacheEntryCount = 0;
            _convertedStringValueCacheHitCount = 0L;
            _markupAssembly = null;
            _activeMarkupAssembly = null;

            bool xmlFormLifetimeReleased = false;

            if (xmlFormLifetimeTarget != null)
            {
                try
                {
                    xmlFormLifetimeReleased =
                        xmlFormLifetimeTarget
                            .DisposeFromOwningRuntime(this);

                    if (xmlFormLifetimeReleased &&
                        Object.ReferenceEquals(
                            _xmlFormLifetimeTarget,
                            xmlFormLifetimeTarget))
                    {
                        _xmlFormLifetimeTarget = null;
                    }
                }
                catch (Exception ex)
                {
                    if (firstError == null)
                        firstError = ex;

                    completionBlocked = true;
                }
            }

            if (ownedMarkupClassTarget != null)
            {
                bool ownedIsLifetimeTarget =
                    Object.ReferenceEquals(
                        ownedMarkupClassTarget,
                        xmlFormLifetimeTarget);

                if (!ownedIsLifetimeTarget || xmlFormLifetimeReleased)
                {
                    try
                    {
                        if (!ownedIsLifetimeTarget)
                            ownedMarkupClassTarget.Dispose();

                        if (Object.ReferenceEquals(
                            _ownedMarkupClassTarget,
                            ownedMarkupClassTarget))
                        {
                            _ownedMarkupClassTarget = null;
                        }
                    }
                    catch (Exception ex)
                    {
                        if (firstError == null)
                            firstError = ex;

                        completionBlocked = true;
                    }
                }
            }

            CompleteFailedLoadRollbackIfSafe();

            if (_failedLoadRollbackPending)
            {
                completionBlocked = true;

                if (firstError == null)
                {
                    firstError = new InvalidOperationException(
                        "Failed-load root cleanup is still waiting for owned " +
                        "background work to stop.");
                }
            }

            if (completionBlocked)
            {
                if (firstError == null)
                {
                    firstError = new InvalidOperationException(
                        "Runtime cleanup still has retryable ownership debt.");
                }

                if (terminalRootError != null)
                {
                    firstError = new InvalidOperationException(
                        terminalRootError.Message +
                        " A later retryable cleanup also failed: " +
                        firstError.Message,
                        terminalRootError);
                }

                throw new InvalidOperationException(
                    "One or more WinFormsXaml resources could not be released: " +
                    firstError.Message,
                    firstError);
            }

            CompactRetainedRuntimeState();

            Exception reportedError = terminalRootError;

            if (reportedError == null)
            {
                reportedError = firstError;
            }
            else if (firstError != null)
            {
                reportedError = new InvalidOperationException(
                    terminalRootError.Message +
                    " A later terminal cleanup also reported: " +
                    firstError.Message,
                    terminalRootError);
            }

            if (reportedError != null)
            {
                // The Control finished native disposal even though one of its
                // cleanup callbacks reported an error. All remaining runtime
                // cleanup also completed, so publish terminal state before
                // surfacing that callback error.
                _disposeCompleted = true;
                throw new InvalidOperationException(
                    "One or more WinFormsXaml resources could not be released: " +
                    reportedError.Message,
                    reportedError);
            }
        }

        private void DisposeOwnedRoot(
            ref Exception firstError,
            ref Exception terminalRootError)
        {
            if (_rootDisposalCompleted ||
                _failedLoadRollbackPending)
            {
                return;
            }

            object root = _root;

            if (root == null)
            {
                _rootDisposalCompleted = true;
                return;
            }

            Control rootControl = root as Control;

            if (rootControl != null && rootControl.IsDisposed)
            {
                _rootDisposalCompleted = true;
                return;
            }

            if (rootControl != null && rootControl.Disposing)
                return;

            IDisposable disposableRoot = root as IDisposable;

            if (disposableRoot == null)
            {
                _rootDisposalCompleted = true;
                return;
            }

            try
            {
                disposableRoot.Dispose();
                _rootDisposalCompleted = true;
            }
            catch (Exception ex)
            {
                if (rootControl != null && rootControl.IsDisposed)
                {
                    _rootDisposalCompleted = true;

                    if (terminalRootError == null)
                        terminalRootError = ex;
                }
                else if (firstError == null)
                {
                    firstError = ex;
                }
            }
        }

        private void CompactRetainedRuntimeState()
        {
            // Compaction happens only after every retryable cleanup operation
            // completed. Clear the published collections before replacing them
            // so previously returned references observe an empty runtime, while
            // the disposed runtime itself does not retain their peak capacity.
            _root = null;
            _failedLoadRoot = null;
            _rootDisposalCompleted = true;
            _basePath = null;
            _markupSource = null;
            _activeMarkupSource = null;
            _activeMarkupElementPathPrefix = null;

            if (_namedObjects != null)
            {
                _namedObjects.Clear();
                _namedObjects =
                    new Dictionary<string, object>(
                        StringComparer.OrdinalIgnoreCase);
            }

            if (_elementInfos != null)
            {
                _elementInfos.Clear();
                _elementInfos =
                    new Dictionary<object, ElementInfo>(
                        _runtimeObjectReferenceComparer);
            }

            if (_implicitStyles != null)
            {
                _implicitStyles.Clear();
                _implicitStyles = new List<StyleDefinition>();
            }

            if (_namedStyles != null)
            {
                _namedStyles.Clear();
                _namedStyles =
                    new Dictionary<string, StyleDefinition>(
                        StringComparer.OrdinalIgnoreCase);
            }

            if (_boundObjectValues != null)
            {
                _boundObjectValues.Clear();
                _boundObjectValues = new Dictionary<string, object>();
            }

            _activeFunctionResultCache = null;
            _activeTemplateElementMap = null;
            _activeTemplateDataContext = null;
            _activeTemplateStyleBindingSlots = null;
            _activeCompiledItemTemplate = null;
            _namedMemberAssignments = null;
            _namedMemberWiringReady = false;
            _resolvingPresetValues = null;
            _resolvingPresetDependencies = null;
            _activePresetDependencyMemo = null;

            lock (_reactiveItemUpdateSync)
                _pendingReactiveItemUpdates.Clear();

            lock (_componentTemplateCacheSync)
            {
                if (_componentTemplateCache != null)
                    _componentTemplateCache.Clear();

                _componentTemplateCache = null;
            }

            if (_componentInstancesByRoot != null)
                _componentInstancesByRoot.Clear();

            _componentInstancesByRoot = null;
            _activeXmlComponentBuildChain = null;
            _presetManager = null;
        }

        internal void VerifyCanDispose()
        {
            VerifyObservableBindingDisposalThread();
        }

        internal void ReleaseCompletedXmlFormLifetime(
            XmlForm xmlForm)
        {
            if (xmlForm == null)
                return;

            if (Object.ReferenceEquals(
                    _xmlFormLifetimeTarget,
                    xmlForm))
            {
                _xmlFormLifetimeTarget = null;
            }

            if (Object.ReferenceEquals(
                    _ownedMarkupClassTarget,
                    xmlForm))
            {
                _ownedMarkupClassTarget = null;
            }
        }

        private void RetainFailedLoadDisposalRetry()
        {
            XmlForm target = _xmlFormLifetimeTarget;

            if (target != null)
                target.RetainFailedLoadDisposalRetry(this);
        }

        private void CompleteFailedLoadRollbackIfSafe()
        {
            if (!_failedLoadRollbackPending)
                return;

            XmlForm target = _xmlFormLifetimeTarget;

            if (target != null &&
                target.HasTrackedOwnedThreads)
            {
                return;
            }

            object failedRoot = _failedLoadRoot;
            _failedLoadRoot = null;
            _failedLoadRollbackPending = false;
            _root = null;

            try
            {
                ReleaseCreatedElement(failedRoot);
            }
            catch
            {
                // The construction failure remains primary. Runtime disposal
                // retains its own retry debt; root rollback is best-effort once
                // no XmlForm-owned worker can still access the tree.
            }
        }

        // ============================================================
        // ELEMENT INFO
        // ============================================================

        private enum HorizontalXamlAlignment
        {
            Stretch,
            Left,
            Center,
            Right
        }

        private enum VerticalXamlAlignment
        {
            Stretch,
            Top,
            Center,
            Bottom
        }

        private sealed class FormIconState
        {
            public bool UseApplicationIcon;
            public bool ConfigurationReady;
            public bool FallbackApplied;
            public Icon NativeBaseline;
            public Icon FallbackValue;
        }

        private sealed class FlexBasisState
        {
            public bool WidthBasisKnown;
            public int WidthBasis;
            public bool HeightBasisKnown;
            public int HeightBasis;
            public bool LastArrangedWidthKnown;
            public int LastArrangedWidth;
            public bool LastArrangedHeightKnown;
            public int LastArrangedHeight;
        }

        private sealed class ElementInfo
        {
            public string XamlType;

            // Native WinForms parentage only covers Control.Controls. Keep the
            // complete XAML ownership tree as well so ToolStripItems, TreeNodes,
            // property-element values, and custom collection children are all
            // released when a build or item transaction is abandoned.
            public ArrayList LogicalChildren;

            // Local XAML values have higher precedence than style setters.
            // Keep only their property names; the normal attribute/property-element
            // paths remain responsible for applying and reloading the actual value.
            public ArrayList LocalValueProperties;

            // Dynamic named-style changes reuse the existing Control. Retain the
            // exact typed state displaced by each active style setter so an omitted
            // setter can fall back to the implicit style or original WinForms value.
            public Hashtable StyleValueSlots;
            public ArrayList ActiveStyleValueSlots;

            // Only Forms allocate this state. The executable icon is a fallback
            // below local Icon values and style setters, so the directive must
            // remain separate from normal Icon ownership.
            public FormIconState FormIcon;

            // WinForms property setters can raise synchronous change events. A
            // handler may reload Style while the previous style is still being
            // restored or applied, so serialize those requests per element.
            public bool StyleTransitionActive;
            public bool StyleTransitionPending;
            public string StyleTransitionCurrentValue;
            public string StyleTransitionPendingValue;

            // The selected named style is retained separately from the active
            // setter layer. Conditional implicit styles and conditional setters
            // rebuild that layer when their observable condition changes.
            public string AppliedNamedStyleValue;

            public Padding Margin;

            public HorizontalXamlAlignment HorizontalAlignment;
            public VerticalXamlAlignment VerticalAlignment;

            public bool WidthExplicit;
            public bool HeightExplicit;
            public bool DirectVirtualAutoSizeSuppressed;

            public bool Hidden;
            public bool Collapsed;
            public bool VisibilityCollapsed;
            public Hashtable ConditionStates;

            public int GridRow;
            public int GridColumn;

            public int GridRowSpan;
            public int GridColumnSpan;

            public bool DockExplicit;
            public DockStyle DockSide;

            public bool CanvasLeftSet;
            public bool CanvasTopSet;
            public bool CanvasRightSet;
            public bool CanvasBottomSet;

            public int CanvasLeft;
            public int CanvasTop;
            public int CanvasRight;
            public int CanvasBottom;

            public bool FlowDirectionExplicit;

            public bool FontFamilyExplicit;
            public bool FontFamilySet;

            public bool FontSizeExplicit;
            public bool FontSizeSet;

            public bool FontWeightExplicit;
            public bool FontWeightSet;

            public bool FontStyleExplicit;
            public bool FontStyleSet;

            public bool TextDecorationsExplicit;
            public bool TextDecorationsSet;

            public bool ForegroundExplicit;
            public bool ForegroundSet;

            public bool BackgroundExplicit;
            public bool BackgroundSet;

            // CSS-like FlexPanel metadata. These values belong to the child,
            // not to the WinForms control type itself, so FlexGrow works with
            // Button, TabControl, Panel, custom controls, etc.
            public float FlexGrow;

            // A growing child with an explicit main-axis size must keep that
            // authored size as its basis. Control.Bounds also stores the most
            // recent arranged size, so reading Width/Height on the next pass
            // would otherwise grow the basis repeatedly. The last arranged
            // value still lets direct code-behind size changes invalidate the
            // retained basis without intercepting native property setters.
            public FlexBasisState FlexBasis;

            // Tag is used as the lightweight DataContext for repeated controls.
            // Remember whether XAML explicitly assigned Tag so incremental item
            // reuse does not overwrite an intentional Tag value.
            public bool TagExplicit;

            public ElementInfo()
            {
                LogicalChildren =
                    new ArrayList();

                Margin =
                    new Padding(0);

                HorizontalAlignment =
                    HorizontalXamlAlignment.Stretch;

                VerticalAlignment =
                    VerticalXamlAlignment.Stretch;

                GridRow = 0;
                GridColumn = 0;

                GridRowSpan = 1;
                GridColumnSpan = 1;

                DockSide =
                    DockStyle.Left;

                FlexGrow = 0.0f;
                TagExplicit = false;
            }
        }

        private ElementInfo GetInfo(object value)
        {
            ElementInfo info;

            if (_elementInfos.TryGetValue(
                value,
                out info))
            {
                return info;
            }

            info =
                new ElementInfo();

            if (value != null)
            {
                info.XamlType =
                    value.GetType().Name;
            }

            _elementInfos[value] =
                info;

            return info;
        }

        private void SetElementVisibilityState(
            ElementInfo info,
            bool hidden,
            bool collapsed)
        {
            if (info == null)
                return;

            info.Hidden = hidden;
            info.VisibilityCollapsed = collapsed;
            RefreshElementCollapsedState(info);
        }

        private void SetElementConditionState(
            ElementInfo info,
            object conditionKey,
            bool visible)
        {
            if (info == null || conditionKey == null)
                return;

            if (info.ConditionStates == null)
            {
                info.ConditionStates =
                    new Hashtable(_runtimeObjectReferenceComparer);
            }

            info.ConditionStates[conditionKey] = visible;
            RefreshElementCollapsedState(info);
        }

        private void RemoveElementConditionState(
            ElementInfo info,
            object conditionKey)
        {
            if (info == null ||
                info.ConditionStates == null ||
                conditionKey == null)
            {
                return;
            }

            info.ConditionStates.Remove(conditionKey);
            RefreshElementCollapsedState(info);
        }

        private void RefreshElementCollapsedState(
            ElementInfo info)
        {
            bool conditionVisible = true;

            if (info.ConditionStates != null)
            {
                IDictionaryEnumerator enumerator =
                    info.ConditionStates.GetEnumerator();

                while (enumerator.MoveNext())
                {
                    object value = enumerator.Value;

                    if (value is bool && !(bool)value)
                    {
                        conditionVisible = false;
                        break;
                    }
                }
            }

            bool collapsed =
                info.VisibilityCollapsed || !conditionVisible;

            if (info.Collapsed == collapsed)
                return;

            info.Collapsed = collapsed;

            if (_elementCollapseRevision != Int64.MaxValue)
                _elementCollapseRevision++;
        }

        private long CaptureElementCollapseRevision()
        {
            return _elementCollapseRevision;
        }

        private bool ElementCollapseRevisionChanged(long captured)
        {
            // Once saturated, conservatively retain the verification scan.
            return captured == Int64.MaxValue ||
                captured != _elementCollapseRevision;
        }

        private static bool ApplyElementEffectiveVisibility(
            object target,
            ElementInfo info)
        {
            if (target == null || info == null)
                return false;

            bool visible = !info.Hidden && !info.Collapsed;
            Control control = target as Control;

            if (control != null)
            {
                control.Visible = visible;
                return true;
            }

            PropertyInfo property = FindProperty(
                target.GetType(),
                "Visible");

            if (property == null ||
                !property.CanWrite ||
                property.PropertyType != typeof(bool))
            {
                return false;
            }

            property.SetValue(target, visible, null);
            return true;
        }

        // ============================================================
        // STYLES
        // ============================================================

        private sealed class StyleDefinition
        {
            public string Key;
            public string TargetType;
            public string BasedOnKey;
            public string Condition;
            public string ConditionBindingKey;
            public DynamicBindingMarkup ConditionMarkup;
            public List<ConditionalStylePart> IncludeConditions;

            public List<StyleSetter> Setters;

            public StyleDefinition()
            {
                Setters =
                    new List<StyleSetter>();
                IncludeConditions =
                    new List<ConditionalStylePart>();
            }
        }

        private sealed class ConditionalStylePart
        {
            public string Expression;
            public string BindingKey;
            public DynamicBindingMarkup Markup;
        }

        private sealed class ImplicitStyleMatchScopeCache
        {
            public int SourceCount;
            public readonly ArrayList Entries = new ArrayList();
        }

        private sealed class ImplicitStyleMatchCacheEntry
        {
            public Type InstanceType;
            public string XamlType;
            public StyleDefinition[] Matches;
        }

        private sealed class ResolvedStyleChainScopeCache
        {
            public readonly Hashtable Chains =
                new Hashtable(_runtimeObjectReferenceComparer);
        }

        private sealed class StyleSetter
        {
            public string Property;
            public string Value;
            public DynamicBindingMarkup Markup;
            public string Condition;
            public string ConditionBindingKey;
            public DynamicBindingMarkup ConditionMarkup;
        }

        private delegate void RestoreStyleValue();

        private sealed class StyleValueSlot
        {
            public RestoreStyleValue Restore;
            public bool Active;
        }

        private sealed class StylePropertyValue
        {
            public PropertyInfo Property;
            public FieldInfo Field;
            public PropertyDescriptor Descriptor;
            public object Value;
            public string BaselineOwnershipKey;
            public int SizeAxis;
            public int FontPart;
            public bool ResetToDefault;
            public bool RuntimeOwned;
        }

        private sealed class StyleMetadataState
        {
            public bool Width;
            public bool WidthExplicit;
            public bool Height;
            public bool HeightExplicit;
            public bool Margin;
            public Padding MarginValue;
            public bool HorizontalAlignment;
            public HorizontalXamlAlignment HorizontalAlignmentValue;
            public bool VerticalAlignment;
            public VerticalXamlAlignment VerticalAlignmentValue;
            public bool FlexGrow;
            public float FlexGrowValue;
            public bool FlowDirection;
            public bool FlowDirectionExplicit;
            public bool ContentRightToLeft;
            public bool ContentRightToLeftValue;
            public bool Foreground;
            public bool ForegroundExplicit;
            public bool ForegroundSet;
            public bool Background;
            public bool BackgroundExplicit;
            public bool BackgroundSet;
            public bool FontFamily;
            public bool FontFamilyExplicit;
            public bool FontFamilySet;
            public bool FontSize;
            public bool FontSizeExplicit;
            public bool FontSizeSet;
            public bool FontWeight;
            public bool FontWeightExplicit;
            public bool FontWeightSet;
            public bool FontStyle;
            public bool FontStyleExplicit;
            public bool FontStyleSet;
            public bool TextDecorations;
            public bool TextDecorationsExplicit;
            public bool TextDecorationsSet;
            public bool Visibility;
            public bool Hidden;
            public bool Collapsed;
            public bool ToolTip;
            public string ToolTipValue;
        }

        private sealed class StyleRestoreFailure : Exception
        {
            public readonly bool BaselineMetadataRequired;

            public StyleRestoreFailure(
                Exception innerException,
                bool baselineMetadataRequired)
                : base(innerException == null
                    ? "A native style value could not be restored."
                    : innerException.Message,
                    innerException)
            {
                BaselineMetadataRequired = baselineMetadataRequired;
            }
        }

    }
}
