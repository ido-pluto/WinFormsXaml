using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Windows.Forms;

namespace WinFormsXaml
{
    /// <summary>
    /// A small code-behind base for an embedded XML Form. The parameterless
    /// convention loads Derived.Type.FullName.xml from the derived assembly.
    /// Loading is deferred until WinForm or Ui is first requested. A derived
    /// constructor may request either property, so binding state assigned in that
    /// constructor must be initialized before the first such access.
    /// </summary>
    public abstract partial class XmlForm : IDisposable, INotifyPropertyChanged
    {
        private readonly Assembly _resourceAssembly;
        private readonly string _resourceNameOrFragment;
        private readonly PresetManager _presetManager;
        private readonly object _includeRequestsSync = new object();
        private List<XmlIncludeRequest> _includeRequests;
        private XmlIncludeRequest[] _includeRequestSnapshot;
        private bool _includeRequestsSealed;
        private bool _includeRequestsSnapshotted;
        private XamlRuntime _ui;
        private bool _loading;
        private bool _loadedNotificationRaised;
        private bool _runtimeLoadFailed;
        private bool _disposed;

        /// <summary>Raised when conventional code-behind state changes.</summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Uses the derived type's full name plus ".xml" as the embedded
        /// resource name and loads it from the derived type's assembly.
        /// </summary>
        protected XmlForm()
        {
            _resourceAssembly = null;
            _resourceNameOrFragment = null;
            _presetManager = null;
        }

        /// <summary>
        /// Uses a complete or partial embedded XML resource name from the
        /// derived type's assembly.
        /// </summary>
        /// <param name="resourceNameOrFragment">
        /// The complete manifest resource name or an unambiguous path fragment.
        /// </param>
        protected XmlForm(string resourceNameOrFragment)
            : this(null, resourceNameOrFragment, null)
        {
        }

        /// <summary>
        /// Uses a complete or partial embedded XML resource name from the
        /// derived type's assembly and the supplied preset manager.
        /// </summary>
        /// <param name="resourceNameOrFragment">
        /// The complete manifest resource name or an unambiguous path fragment.
        /// </param>
        /// <param name="presetManager">
        /// The preset manager to share with the XML runtime, or <see langword="null"/>
        /// to create a runtime-owned manager.
        /// </param>
        protected XmlForm(
            string resourceNameOrFragment,
            PresetManager presetManager)
            : this(null, resourceNameOrFragment, presetManager)
        {
        }

        /// <summary>
        /// Uses a complete or partial embedded XML resource name from the
        /// supplied assembly.
        /// </summary>
        /// <param name="resourceAssembly">
        /// The assembly containing the XML resource, or <see langword="null"/>
        /// to use the derived type's assembly.
        /// </param>
        /// <param name="resourceNameOrFragment">
        /// The complete manifest resource name or an unambiguous path fragment.
        /// </param>
        protected XmlForm(
            Assembly resourceAssembly,
            string resourceNameOrFragment)
            : this(resourceAssembly, resourceNameOrFragment, null)
        {
        }

        /// <summary>
        /// Uses a complete or partial embedded XML resource name from the
        /// supplied assembly and the supplied preset manager.
        /// </summary>
        /// <param name="resourceAssembly">
        /// The assembly containing the XML resource, or <see langword="null"/>
        /// to use the derived type's assembly.
        /// </param>
        /// <param name="resourceNameOrFragment">
        /// The complete manifest resource name or an unambiguous path fragment.
        /// </param>
        /// <param name="presetManager">
        /// The preset manager to share with the XML runtime, or <see langword="null"/>
        /// to create a runtime-owned manager.
        /// </param>
        /// <exception cref="ArgumentException">
        /// <paramref name="resourceNameOrFragment"/> is null or empty.
        /// </exception>
        protected XmlForm(
            Assembly resourceAssembly,
            string resourceNameOrFragment,
            PresetManager presetManager)
        {
            if (String.IsNullOrEmpty(resourceNameOrFragment))
            {
                throw new ArgumentException(
                    "An embedded Form resource name or path fragment is required.",
                    "resourceNameOrFragment");
            }

            _resourceAssembly = resourceAssembly;
            _resourceNameOrFragment = resourceNameOrFragment;
            _presetManager = presetManager;
        }

        /// <summary>Gets whether the embedded XML has been loaded.</summary>
        public bool IsLoaded
        {
            get
            {
                return
                    !_disposed &&
                    !_runtimeLoadFailed &&
                    _ui != null;
            }
        }

        /// <summary>Gets the native Form created by the embedded XML.</summary>
        public Form WinForm
        {
            get { return EnsureLoaded().Form; }
        }

        /// <summary>
        /// Gets the retained <see cref="XamlRuntime"/> for derived classes.
        /// </summary>
        protected XamlRuntime Ui
        {
            get { return EnsureLoaded(); }
        }

        /// <summary>
        /// Gets the preset manager owned or shared by this XML Form.
        /// This is the code-behind shortcut for <c>Ui.Presets</c>.
        /// </summary>
        protected PresetManager Presets
        {
            get { return EnsureLoaded().Presets; }
        }

        /// <summary>
        /// Queues reusable XML content for insertion before this Form's own
        /// root content. Calls retain their order. This method must run before
        /// WinForm, Ui, Get, Presets, or another operation starts loading the
        /// XML Form.
        /// </summary>
        /// <param name="source">
        /// A non-empty include name, embedded-resource reference, or file
        /// reference understood by the normal include resolver.
        /// </param>
        protected void Include(string source)
        {
            Include(source, IncludeSourceKind.Registered);
        }

        /// <summary>
        /// Queues reusable XML content with an explicit source kind for
        /// insertion before this Form's own root content. Calls retain their
        /// order.
        /// </summary>
        /// <param name="source">The non-empty include reference.</param>
        /// <param name="sourceKind">How the include reference is resolved.</param>
        protected void Include(
            string source,
            IncludeSourceKind sourceKind)
        {
            if (source == null)
                throw new ArgumentNullException("source");

            string normalizedSource = source.Trim();

            if (normalizedSource.Length == 0)
            {
                throw new ArgumentException(
                    "An XML include reference is required.",
                    "source");
            }

            ValidateIncludeSourceKind(sourceKind);

            lock (_includeRequestsSync)
            {
                if (_disposed)
                    throw new ObjectDisposedException(GetType().FullName);

                if (_includeRequestsSealed ||
                    _loading ||
                    _ui != null ||
                    _runtimeLoadFailed)
                {
                    throw new InvalidOperationException(
                        "Include must be called before this XML Form starts " +
                        "loading. Call Include before WinForm, Ui, Get, or " +
                        "Presets; use a declarative <Includes> element when " +
                        "content must be inserted at a specific XML position.");
                }

                if (_includeRequests == null)
                    _includeRequests = new List<XmlIncludeRequest>();

                Assembly assembly = _resourceAssembly == null
                    ? GetType().Assembly
                    : _resourceAssembly;

                _includeRequests.Add(
                    new XmlIncludeRequest(
                        normalizedSource,
                        sourceKind,
                        assembly));
            }
        }

        /// <summary>Gets a named XML object for derived code-behind classes.</summary>
        protected T Get<T>(string name) where T : class
        {
            return EnsureLoaded().Get<T>(name);
        }

        /// <summary>Reloads every retained snapshot binding.</summary>
        protected void ReloadBindings()
        {
            EnsureLoaded().ReloadBindings();
        }

        /// <summary>
        /// Reloads retained bindings on one named object and its control subtree.
        /// </summary>
        protected void ReloadBindings(string name)
        {
            EnsureLoaded().ReloadBindings(name);
        }

        /// <summary>Reloads one property binding on one named object.</summary>
        protected void ReloadBinding(
            string name,
            string propertyName)
        {
            EnsureLoaded().ReloadBinding(name, propertyName);
        }

        /// <summary>
        /// Commits one named target property's current value to its TwoWay
        /// binding source. Use this with UpdateSourceTrigger=Explicit.
        /// </summary>
        protected void UpdateBindingSource(
            string name,
            string propertyName)
        {
            EnsureLoaded().UpdateBindingSource(name, propertyName);
        }

        /// <summary>
        /// Commits one target object's current property value to its TwoWay
        /// binding source.
        /// </summary>
        protected void UpdateBindingSource(
            object target,
            string propertyName)
        {
            EnsureLoaded().UpdateBindingSource(target, propertyName);
        }

        /// <summary>
        /// Called once after the XML runtime is completely loaded and before
        /// the first WinForm or Ui access returns.
        /// </summary>
        protected virtual void OnLoaded(EventArgs e)
        {
        }

        /// <summary>
        /// Raises PropertyChanged for a code-behind property.
        /// </summary>
        /// <param name="e">The property-change event data.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="e"/> is null.
        /// </exception>
        protected virtual void OnPropertyChanged(
            PropertyChangedEventArgs e)
        {
            if (e == null)
                throw new ArgumentNullException("e");

            PropertyChangedEventHandler handler = PropertyChanged;

            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        /// Assigns a backing field and raises <see cref="PropertyChanged"/> when
        /// the value differs according to <see cref="EqualityComparer{T}"/>.
        /// </summary>
        /// <typeparam name="T">The backing field's value type.</typeparam>
        /// <param name="field">The backing field to read and update.</param>
        /// <param name="value">The proposed value.</param>
        /// <param name="propertyName">The non-empty property name to publish.</param>
        /// <returns>
        /// <see langword="true"/> when the value changed; otherwise
        /// <see langword="false"/>.
        /// </returns>
        protected bool SetProperty<T>(
            ref T field,
            T value,
            string propertyName)
        {
            if (propertyName == null)
                throw new ArgumentNullException("propertyName");

            if (propertyName.Trim().Length == 0)
            {
                throw new ArgumentException(
                    "A property name is required.",
                    "propertyName");
            }

            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(
                new PropertyChangedEventArgs(propertyName));
            return true;
        }

        private XamlRuntime EnsureLoaded()
        {
            if (_disposed)
                throw new ObjectDisposedException(GetType().FullName);

            if (_runtimeLoadFailed)
            {
                throw new InvalidOperationException(
                    "The XML Form failed to load and is pending disposal.");
            }

            if (_ui != null)
                return _ui;

            if (_loading)
            {
                throw new InvalidOperationException(
                    "The XML Form was requested recursively while it was loading.");
            }

            SealIncludeRequests();
            _loading = true;

            try
            {
                Assembly assembly =
                    _resourceAssembly == null
                        ? GetType().Assembly
                        : _resourceAssembly;
                string resourceName;

                if (_resourceNameOrFragment == null)
                {
                    string typeName = GetType().FullName;

                    if (String.IsNullOrEmpty(typeName))
                    {
                        throw new InvalidOperationException(
                            "The XML Form type has no full name. Supply an " +
                            "explicit embedded resource name to the base constructor.");
                    }

                    // The parameterless convention remains an exact contract:
                    // Derived.Type.FullName.xml in the derived assembly.
                    resourceName = typeName + ".xml";
                }
                else
                {
                    resourceName =
                        XamlRuntime.FindEmbeddedXmlResource(
                            assembly,
                            _resourceNameOrFragment);
                }

                XamlRuntime loaded =
                    XamlRuntime.LoadEmbedded(
                        assembly,
                        resourceName,
                        this,
                        _presetManager);

                if (_ui == null)
                {
                    AttachRuntime(loaded);
                    CompleteRuntimeLoad(loaded);
                }
                else if (!Object.ReferenceEquals(_ui, loaded))
                {
                    XamlRuntime.RollbackFailedLoad(loaded);
                    throw new InvalidOperationException(
                        "The XML Form loaded a different runtime than the one attached to it.");
                }

                return loaded;
            }
            finally
            {
                _loading = false;
            }
        }

        internal void AttachRuntime(XamlRuntime runtime)
        {
            if (runtime == null)
                throw new ArgumentNullException("runtime");

            if (_disposed)
                throw new ObjectDisposedException(GetType().FullName);

            if (_ui != null && !Object.ReferenceEquals(_ui, runtime))
            {
                throw new InvalidOperationException(
                    "This XML Form already owns a different runtime.");
            }

            VerifyQueuedIncludesWereComposed();

            _ui = runtime;
            _runtimeLoadFailed = false;
        }

        internal void CompleteRuntimeLoad(XamlRuntime runtime)
        {
            if (!Object.ReferenceEquals(_ui, runtime))
            {
                throw new InvalidOperationException(
                    "The completed runtime is not attached to this XML Form.");
            }

            if (_loadedNotificationRaised)
                return;

            // XmlForm is deliberately a Form-specific convenience API.
            // Reject an invalid document before user OnLoaded code runs so
            // the normal load rollback can release its partial control tree.
            Form form = runtime.Root as Form;

            if (form == null)
            {
                throw new InvalidOperationException(
                    "XmlForm requires a System.Windows.Forms.Form root.");
            }

            AttachFormLifetime(form);
            OnLoaded(EventArgs.Empty);

            if (_disposed || !Object.ReferenceEquals(_ui, runtime))
            {
                throw new InvalidOperationException(
                    "The XML Form was disposed or detached while OnLoaded was running.");
            }

            _runtimeLoadFailed = false;
            _loadedNotificationRaised = true;
            ReleaseSuccessfulIncludeRequests();
        }

        internal void MarkRuntimeLoadFailed(
            XamlRuntime runtime)
        {
            if (!Object.ReferenceEquals(_ui, runtime))
                return;

            _runtimeLoadFailed = true;
            _loadedNotificationRaised = false;
        }

        internal void DetachRuntime(XamlRuntime runtime)
        {
            if (!Object.ReferenceEquals(_ui, runtime))
                return;

            DetachFormLifetime();
            _ui = null;
            _runtimeLoadFailed = false;
            _loadedNotificationRaised = false;
        }

        /// <summary>
        /// Loads this XML Form when necessary and starts its WinForms message
        /// loop. This is a shortcut for Application.Run(WinForm).
        /// </summary>
        public void Start()
        {
            Application.Run(WinForm);
        }

        /// <summary>
        /// Stops owned background work cooperatively and releases the XML
        /// runtime, native Form, bindings, and component code-behind state.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// An owned worker did not stop within the bounded timeout, or one or
        /// more owned resources could not be released. A timed-out disposal can
        /// be retried after the worker exits.
        /// </exception>
        public void Dispose()
        {
            DisposeXmlForm(true, null);
        }

        internal bool DisposeFromOwningRuntime(XamlRuntime runtime)
        {
            if (runtime == null)
                throw new ArgumentNullException("runtime");

            return DisposeXmlForm(false, runtime);
        }

        internal void PrepareForOwningRuntimeDisposal(
            XamlRuntime runtime)
        {
            if (runtime == null)
                throw new ArgumentNullException("runtime");

            PrepareXmlFormForOwningRuntimeDisposal(runtime);
        }

        /// <summary>Allows derived code-behind classes to release owned state.</summary>
        protected virtual void Dispose(bool disposing)
        {
        }

        /// <summary>
        /// Returns a stable, ordered snapshot for the runtime composition
        /// stage. Snapshotting also closes the pre-load Include API so a racing
        /// mutation cannot change the document being composed.
        /// </summary>
        internal IList SnapshotIncludeRequestsForLoad()
        {
            lock (_includeRequestsSync)
            {
                _includeRequestsSealed = true;
                _includeRequestsSnapshotted = true;

                if (_includeRequestSnapshot == null)
                {
                    _includeRequestSnapshot = _includeRequests == null
                        ? new XmlIncludeRequest[0]
                        : _includeRequests.ToArray();
                }

                return (IList)_includeRequestSnapshot.Clone();
            }
        }

        private void SealIncludeRequests()
        {
            lock (_includeRequestsSync)
                _includeRequestsSealed = true;
        }

        private void VerifyQueuedIncludesWereComposed()
        {
            lock (_includeRequestsSync)
            {
                _includeRequestsSealed = true;

                if (_includeRequests != null &&
                    _includeRequests.Count != 0 &&
                    !_includeRequestsSnapshotted)
                {
                    throw new InvalidOperationException(
                        "The XmlForm queued programmatic includes after the " +
                        "runtime's include-composition stage. Instantiate the " +
                        "XmlForm directly before loading its WinForm, or place " +
                        "the include in XML with <Includes Source=\"...\" />.");
                }
            }
        }

        private void ReleaseSuccessfulIncludeRequests()
        {
            lock (_includeRequestsSync)
            {
                if (_includeRequests != null)
                    _includeRequests.Clear();

                _includeRequests = null;
                _includeRequestSnapshot = null;
            }
        }

        private static void ValidateIncludeSourceKind(
            IncludeSourceKind sourceKind)
        {
            if (sourceKind != IncludeSourceKind.Registered &&
                sourceKind != IncludeSourceKind.EmbeddedResource &&
                sourceKind != IncludeSourceKind.File)
            {
                throw new ArgumentOutOfRangeException("sourceKind");
            }
        }
    }
}
