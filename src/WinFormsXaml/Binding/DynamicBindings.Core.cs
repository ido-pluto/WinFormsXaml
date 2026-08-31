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
        private sealed class DynamicBindingMarkup
        {
            public string MarkupSource;
            public string ElementPath;
            public string PropertyName;
            public int LineNumber;
            public int LinePosition;
        }

        private sealed class DynamicPropertyBinding
        {
            public object Target;
            public string PropertyName;
            public string PropertyKey;
            public string Expression;
            public object DataContext;
            public bool InnerText;
            public bool UsesPreset;
            public bool MayUsePreset;
            public bool PresetValueStateKnown;
            public bool PresetValueUnset;
            public RestoreStyleValue PresetBaselineRestore;
            public bool StyleSetter;
            public bool StyleCondition;
            public bool ConditionalProperty;
            public string ConditionedPropertyName;
            public object ConditionedPropertyValue;
            public object ConditionedPropertyBaseline;
            public bool ConditionedPropertyApplied;
            public bool Active;
            public ArrayList ObservableRegistrations;
            public bool HasInitialObservableSnapshot;
            public BindingExpressionPlan InitialDirectPlan;
            public BindingPathResult InitialPathResult;
            public object EventTarget;
            public Dictionary<string, StyleDefinition> ComponentNamedStyles;
            public List<StyleDefinition> ComponentImplicitStyles;
            public DynamicBindingMarkup Markup;
        }

        private sealed class DynamicBindingReloadRequest
        {
            public object Target;
            public string PropertyName;
            public bool PresetsOnly;
            public PresetChangedEventArgs PresetChange;
        }

        private sealed class DynamicTargetDisposalRegistration
        {
            public object Target;
            public IComponent Component;
            public WeakReference ComponentReference;
            public DynamicTargetDisposalForwarder Forwarder;
            public EventHandler Handler;
            public bool AddAttempted;
            public bool Adding;
            public bool DetachRequested;
            public bool DisposedObserved;
            public bool Removing;
            public bool RetryQueued;
            public bool Detached;
        }

        private sealed class DynamicTargetDisposalForwarder
        {
            private volatile XamlRuntime _owner;
            private volatile DynamicTargetDisposalRegistration _registration;
            private volatile object _target;

            public DynamicTargetDisposalForwarder(
                XamlRuntime owner,
                DynamicTargetDisposalRegistration registration,
                object target)
            {
                _registration = registration;
                _target = target;
                _owner = owner;
            }

            public void OnDisposed(object sender, EventArgs e)
            {
                XamlRuntime owner = _owner;
                DynamicTargetDisposalRegistration registration =
                    _registration;
                object target = _target;

                if (owner != null &&
                    registration != null &&
                    target != null)
                {
                    owner.OnDynamicTargetDisposed(
                        registration,
                        target);
                }
            }

            public void Disable()
            {
                _owner = null;
                _registration = null;
                _target = null;
            }
        }

        private PresetManager _presetManager;
        private bool _presetManagerWasProvided;
        private ArrayList _dynamicPropertyBindings;
        private ArrayList _presetDynamicPropertyBindings;
        private Hashtable _dynamicBindingSlotsByTarget;
        private Hashtable _dynamicTargetDisposalHooks;
        private ArrayList _dynamicTargetDisposalRetryHooks;
        private bool _retryingDynamicTargetDisposalHooks;
        private Hashtable _disposingDynamicTargets;
        private Hashtable _loadedPresetElements;
        private ArrayList _itemsControls;
        private Hashtable _itemsControlSet;
        private ArrayList _presetItemsControls;
        private Hashtable _presetItemsControlSet;
        private bool _reloadingDynamicBindings;
        private bool _drainingDynamicBindingReloads;
        private ArrayList _pendingDynamicBindingReloads;
        private readonly object _presetRefreshSync = new object();
        private PresetChangedEventArgs _pendingPresetChange;
        private bool _presetChangePending;
        private bool _presetRefreshQueued;
        private bool _presetRefreshActive;
        private bool _presetRefreshRetryBlocked;
        private Hashtable _activePresetDependencyMemo;
        private bool _rootHandleHooked;
        private bool _rootDisposedHooked;
        private bool _dynamicFeaturesDisposed;
        private Hashtable _conditionalStyleRefreshTargets;

        /// <summary>
        /// Gets the preset manager used by this runtime.
        /// </summary>
        public PresetManager Presets
        {
            get { return _presetManager; }
        }

        /// <summary>
        /// Re-evaluates every retained property binding, including registered
        /// ItemsControl templates through their optimized reload path. If a
        /// preset refresh previously failed, this explicitly retries its
        /// retained dependency scope before the full reload.
        /// </summary>
        public void ReloadBindings()
        {
            RetryFailedPresetRefresh();
            ReloadDynamicBindings(null, null, false, null);
            ReloadRegisteredItemsControls(null);
        }

        /// <summary>
        /// Re-evaluates bindings on the named object and, for Controls, its subtree.
        /// Descendant ItemsControls also perform their optimized template refresh.
        /// </summary>
        public void ReloadBindings(string name)
        {
            object target = this[name];
            ReloadDynamicBindings(target, null, false, null);
            ReloadRegisteredItemsControls(target);
        }

        /// <summary>Re-evaluates one property binding on a named object.</summary>
        public void ReloadBinding(
            string name,
            string propertyName)
        {
            if (String.IsNullOrEmpty(propertyName))
                throw new ArgumentException("A property name is required.", "propertyName");

            ReloadDynamicBindings(
                this[name],
                propertyName,
                false,
                null);
        }

        private void InitializeDynamicFeatures(
            PresetManager presetManager)
        {
            _dynamicPropertyBindings = new ArrayList();
            _presetDynamicPropertyBindings = new ArrayList();
            _dynamicBindingSlotsByTarget =
                new Hashtable(_observableReferenceComparer);
            _dynamicTargetDisposalHooks =
                new Hashtable(_observableReferenceComparer);
            _dynamicTargetDisposalRetryHooks = new ArrayList();
            _retryingDynamicTargetDisposalHooks = false;
            _disposingDynamicTargets =
                new Hashtable(_observableReferenceComparer);
            _pendingDynamicBindingReloads = new ArrayList();
            _loadedPresetElements = new Hashtable();
            _itemsControls = new ArrayList();
            _itemsControlSet =
                new Hashtable(_observableReferenceComparer);
            _presetItemsControls = new ArrayList();
            _presetItemsControlSet =
                new Hashtable(_observableReferenceComparer);
            _presetRefreshRetryBlocked = false;
            _presetManagerWasProvided = presetManager != null;
            _presetManager =
                presetManager == null
                    ? new PresetManager()
                    : presetManager;

            _presetManager.Changed += OnPresetManagerChanged;
        }

        private void DisposeDynamicFeatures()
        {
            PrepareObservableBindingDisposal();

            bool retryOnly;

            lock (_presetRefreshSync)
            {
                retryOnly = _dynamicFeaturesDisposed;

                if (!retryOnly)
                {
                    _dynamicFeaturesDisposed = true;
                    _pendingPresetChange = null;
                    _presetChangePending = false;
                    _presetRefreshQueued = false;
                    _presetRefreshActive = false;
                    _presetRefreshRetryBlocked = false;
                }
            }

            if (retryOnly)
            {
                Exception retryError = null;

                try
                {
                    DisposeObservableBindingSubscriptions();
                }
                catch (Exception ex)
                {
                    retryError = ex;
                }

                try
                {
                    RetryDynamicTargetDisposalHooks();
                }
                catch (Exception ex)
                {
                    if (retryError == null)
                        retryError = ex;
                }

                try
                {
                    ReleaseAllComponentInstances();
                }
                catch (Exception ex)
                {
                    if (retryError == null)
                        retryError = ex;
                }

                if (retryError != null)
                    throw retryError;

                return;
            }

            Exception firstError = null;
            Control root = RootControl;
            bool rootHandleHooked = _rootHandleHooked;
            bool rootDisposedHooked = _rootDisposedHooked;
            _rootHandleHooked = false;
            _rootDisposedHooked = false;

            if (root != null && rootHandleHooked)
            {
                try
                {
                    root.HandleCreated -= OnDynamicRootHandleCreated;
                }
                catch (Exception ex)
                {
                    firstError = ex;
                }
            }

            if (root != null && rootDisposedHooked)
            {
                try
                {
                    root.Disposed -= OnDynamicRootDisposed;
                }
                catch (Exception ex)
                {
                    if (firstError == null)
                        firstError = ex;
                }
            }

            if (_presetManager != null)
            {
                try
                {
                    _presetManager.Changed -= OnPresetManagerChanged;
                }
                catch (Exception ex)
                {
                    if (firstError == null)
                        firstError = ex;
                }
            }

            try
            {
                DisposeObservableBindingSubscriptions();
            }
            catch (Exception ex)
            {
                if (firstError == null)
                    firstError = ex;
            }

            // Disposal owns the whole filtered list. Clear it once before
            // per-binding source detachment so mass shutdown does not perform
            // one linear filtered-list removal per binding.
            if (_presetDynamicPropertyBindings != null)
                _presetDynamicPropertyBindings.Clear();

            if (_dynamicPropertyBindings != null)
            {
                ArrayList retainedBindings =
                    new ArrayList(_dynamicPropertyBindings);
                int bindingIndex;

                for (bindingIndex = retainedBindings.Count - 1;
                     bindingIndex >= 0;
                     bindingIndex--)
                {
                    try
                    {
                        DeactivateDynamicBinding(
                            retainedBindings[bindingIndex] as
                                DynamicPropertyBinding);
                    }
                    catch (Exception ex)
                    {
                        if (firstError == null)
                            firstError = ex;
                    }
                }

                _dynamicPropertyBindings.Clear();
            }

            if (_dynamicBindingSlotsByTarget != null)
                _dynamicBindingSlotsByTarget.Clear();

            try
            {
                ReleaseAllDynamicTargetDisposalHooks();
            }
            catch (Exception ex)
            {
                if (firstError == null)
                    firstError = ex;
            }

            if (_pendingDynamicBindingReloads != null)
                _pendingDynamicBindingReloads.Clear();

            ReleaseAllCompiledItemTemplates();

            if (_loadedPresetElements != null)
                _loadedPresetElements.Clear();

            lock (_componentTemplateCacheSync)
            {
                if (_componentTemplateCache != null)
                    _componentTemplateCache.Clear();
            }

            if (_itemsControls != null)
            {
                ArrayList registeredItems =
                    new ArrayList(_itemsControls);
                int i;

                for (i = 0; i < registeredItems.Count; i++)
                {
                    ItemsControl items =
                        registeredItems[i] as ItemsControl;

                    if (items == null)
                        continue;

                    if (!items.IsDisposed)
                    {
                        try
                        {
                            CancelItemsRefresh(items, false);
                        }
                        catch (Exception ex)
                        {
                            if (firstError == null)
                                firstError = ex;
                        }
                    }

                    try
                    {
                        DeactivateItemsControlBindingSlots(items);
                    }
                    catch (Exception ex)
                    {
                        if (firstError == null)
                            firstError = ex;
                    }

                    try
                    {
                        items.ReleaseRuntimeItemSourceObservation();
                    }
                    catch (Exception ex)
                    {
                        if (firstError == null)
                            firstError = ex;
                    }

                    items.ReleaseTemplateDeclarationContext();
                }

                _itemsControls.Clear();
            }

            if (_presetItemsControls != null)
                _presetItemsControls.Clear();

            if (_itemsControlSet != null)
                _itemsControlSet.Clear();

            if (_presetItemsControlSet != null)
                _presetItemsControlSet.Clear();

            try
            {
                ReleaseAllComponentInstances();
            }
            catch (Exception ex)
            {
                if (firstError == null)
                    firstError = ex;
            }

            _activeComponentDataContext = null;
            _activeComponentEventTarget = null;
            _activeComponentNamedStyles = null;
            _activeComponentImplicitStyles = null;
            _activeCompiledItemTemplate = null;
            _componentContentProjectionDepth = 0;
            _activeComponentContentRoot = null;

            if (_componentContentProjections != null)
            {
                _componentContentProjections.Clear();
                _componentContentProjections = null;
            }

            if (_componentChildrenSlotMarkers != null)
            {
                _componentChildrenSlotMarkers.Clear();
                _componentChildrenSlotMarkers = null;
            }

            if (firstError != null)
            {
                throw new InvalidOperationException(
                    "One or more dynamic WinFormsXaml resources could not " +
                    "be released: " + firstError.Message,
                    firstError);
            }
        }

    }
}
