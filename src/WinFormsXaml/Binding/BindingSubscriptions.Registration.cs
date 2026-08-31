using System;
using System.Collections;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace WinFormsXaml
{
    public sealed partial class XamlRuntime
    {
        private ObservableBindingRegistration AttachObservableBinding(
            object owner,
            object target,
            string targetPropertyName,
            BindingMode mode,
            BindingUpdateSourceTrigger updateSourceTrigger,
            BindingPathResult pathResult,
            ObservableBindingChangedCallback callback)
        {
            if (owner == null)
                throw new ArgumentNullException("owner");

            if (target == null && mode == BindingMode.TwoWay)
                throw new ArgumentNullException("target");

            if (callback == null)
                throw new ArgumentNullException("callback");

            if (pathResult == null)
                throw new ArgumentNullException("pathResult");

            if (mode == BindingMode.OneWay &&
                pathResult.Dependencies.Count == 0)
            {
                return null;
            }

            IPropertyBindingRuntime targetRuntimeBinding =
                target != null &&
                EqualsIgnoreCase(targetPropertyName, "Value")
                    ? target as IPropertyBindingRuntime
                    : null;
            ObservableTargetProperty targetProperty =
                targetRuntimeBinding != null || target == null
                    ? _missingObservableTargetProperty
                    : ResolveObservableTargetProperty(
                        target.GetType(),
                        targetPropertyName);

            if (mode == BindingMode.TwoWay)
            {
                ValidateObservableTwoWayEndpoints(
                    target,
                    targetPropertyName,
                    targetProperty,
                    targetRuntimeBinding,
                    updateSourceTrigger,
                    pathResult);
            }

            ObservableBindingRegistration registration =
                new ObservableBindingRegistration();

            registration.Owner = owner;
            registration.Target = target;
            registration.TargetPropertyName = targetPropertyName;
            registration.Mode = mode;
            registration.UpdateSourceTrigger = updateSourceTrigger;
            registration.TargetProperty = targetProperty;
            registration.TargetRuntimeBinding = targetRuntimeBinding;
            registration.Callback = callback;
            registration.PathDependencies =
                CopyBindingPathDependencies(pathResult.Dependencies);
            registration.DependencySourceIndex =
                pathResult.DependencySourceIndex;
            registration.SourceSubscriptions = new ArrayList();
            registration.TerminalDependency =
                pathResult.TerminalDependency;
            registration.Active = true;

            if (mode == BindingMode.TwoWay &&
                updateSourceTrigger !=
                    BindingUpdateSourceTrigger.Explicit)
            {
                registration.TargetForwarder =
                    new ObservableTargetForwarder(
                        this,
                        registration);
                registration.TargetChangedHandler = new EventHandler(
                    registration.TargetForwarder.OnValueChanged);
            }

            bool shouldQueue = false;
            bool ensureRootlessDispatcher = false;

            lock (_observableBindingSync)
            {
                if (_observableBindingSubscriptionsDisposed ||
                    _observableDisposalClaimed ||
                    _dynamicFeaturesDisposed)
                {
                    registration.Active = false;
                    return null;
                }

                EnsureObservableBindingStorageUnderLock();
                registration.Revision =
                    NextObservableRevisionUnderLock();

                AddObservableRegistrationUnderLock(registration);

                try
                {
                    if (!AttachObservableDependenciesUnderLock(
                            registration,
                            registration.PathDependencies,
                            registration.SourceSubscriptions) ||
                        !IsObservableBindingActiveUnderLock(registration))
                    {
                        DetachObservableBindingUnderLock(registration);
                        return null;
                    }

                    CompleteObservableDependencyAttachmentsUnderLock(
                        registration);
                    ReindexObservableNotifyDependentsUnderLock(
                        registration);

                    if (mode == BindingMode.TwoWay &&
                        updateSourceTrigger !=
                            BindingUpdateSourceTrigger.Explicit)
                    {
                        registration.TargetHandlerAttached = true;

                        if (updateSourceTrigger ==
                            BindingUpdateSourceTrigger.LostFocus)
                        {
                            ((Control)target).LostFocus +=
                                registration.TargetChangedHandler;
                        }
                        else if (targetRuntimeBinding != null)
                        {
                            targetRuntimeBinding.ValueChanged +=
                                registration.TargetChangedHandler;
                        }
                        else
                        {
                            EventDescriptor alternateChangedEvent =
                                targetProperty.AlternateChangedEvent;

                            if (alternateChangedEvent != null)
                            {
                                registration.LastAlternateTargetValue =
                                    targetProperty.Descriptor.GetValue(target);
                                registration.HasLastAlternateTargetValue = true;
                                registration.TargetChangedDelegate =
                                    CreateObservableTargetChangedDelegate(
                                        alternateChangedEvent,
                                        registration.TargetForwarder);
                                alternateChangedEvent.AddEventHandler(
                                    target,
                                    registration.TargetChangedDelegate);
                            }
                            else
                            {
                                targetProperty.Descriptor.AddValueChanged(
                                    target,
                                    registration.TargetChangedHandler);
                            }
                        }

                        if (!IsObservableBindingActiveUnderLock(registration))
                            return null;
                    }

                    bool sourceChanged =
                        HaveObservableDependencySnapshotsChanged(
                            registration.PathDependencies);

                    if (sourceChanged)
                    {
                        MarkObservableSourcePendingUnderLock(
                            registration,
                            true);
                        shouldQueue = true;
                    }

                    ensureRootlessDispatcher =
                        _observableRootReady &&
                        RootControl == null &&
                        _observableRootlessDispatcher == null;
                }
                catch
                {
                    DetachObservableBindingUnderLock(registration);
                    throw;
                }
            }

            if (ensureRootlessDispatcher)
            {
                try
                {
                    OnObservableBindingRootReady();
                }
                catch
                {
                    DetachObservableBinding(registration);
                    throw;
                }
            }

            if (shouldQueue)
                QueueObservableBindingDispatch();

            return registration;
        }

        private static Delegate CreateObservableTargetChangedDelegate(
            EventDescriptor changedEvent,
            ObservableTargetForwarder forwarder)
        {
            if (changedEvent == null)
                throw new ArgumentNullException("changedEvent");

            if (forwarder == null)
                throw new ArgumentNullException("forwarder");

            Type eventType = changedEvent.EventType;

            if (eventType == typeof(EventHandler))
            {
                return new EventHandler(forwarder.OnValueChanged);
            }

            if (eventType == typeof(DateRangeEventHandler))
            {
                return new DateRangeEventHandler(
                    forwarder.OnDateRangeChanged);
            }

            if (eventType == typeof(TreeViewEventHandler))
            {
                return new TreeViewEventHandler(
                    forwarder.OnTreeViewChanged);
            }

            if (eventType == typeof(SplitterEventHandler))
            {
                return new SplitterEventHandler(
                    forwarder.OnSplitterMoved);
            }

            if (eventType == typeof(WebBrowserNavigatedEventHandler))
            {
                return new WebBrowserNavigatedEventHandler(
                    forwarder.OnWebBrowserNavigated);
            }

            if (eventType == typeof(ScrollEventHandler))
            {
                return new ScrollEventHandler(forwarder.OnScrolled);
            }

            throw new InvalidOperationException(
                "Mode=TwoWay cannot subscribe to alternate event '" +
                changedEvent.Name +
                "' because its handler type '" +
                (eventType == null ? "<unknown>" : eventType.FullName) +
                "' is not supported.");
        }

        private void UpdateObservableBinding(
            ObservableBindingRegistration registration,
            BindingPathResult pathResult)
        {
            if (registration == null)
                throw new ArgumentNullException("registration");

            if (pathResult == null)
                throw new ArgumentNullException("pathResult");

            if (registration.Mode == BindingMode.TwoWay)
            {
                ValidateObservableTwoWayEndpoints(
                    registration.Target,
                    registration.TargetPropertyName,
                    registration.TargetProperty,
                    registration.TargetRuntimeBinding,
                    registration.UpdateSourceTrigger,
                    pathResult);
            }

            ArrayList desiredDependencies =
                CopyBindingPathDependencies(pathResult.Dependencies);
            BindingDependencySourceIndex desiredDependencySourceIndex =
                pathResult.DependencySourceIndex;
            bool shouldQueue = false;

            lock (_observableBindingSync)
            {
                if (!IsObservableBindingActiveUnderLock(registration))
                    return;

                if (ObservableBindingMatchesUnderLock(
                        registration,
                        pathResult))
                {
                    return;
                }

                ArrayList desiredSubscriptions = new ArrayList();
                ArrayList addedSubscriptions = new ArrayList();
                bool attachmentSuperseded = false;
                int i;

                MarkObservableNotifyDependentsUnindexedUnderLock(
                    registration);

                try
                {
                    for (i = 0;
                         i < desiredDependencies.Count;
                         i++)
                    {
                        BindingPathDependency dependency =
                            desiredDependencies[i] as BindingPathDependency;

                        if (dependency == null)
                            continue;

                        if (desiredDependencySourceIndex != null &&
                            !desiredDependencySourceIndex.
                                IsFirstDependencyForSource(
                                    dependency))
                        {
                            continue;
                        }

                        bool added;
                        ObservableSourceSubscription subscription =
                            AttachObservableDependencyUnderLock(
                                registration,
                                dependency,
                                out added);

                        if (subscription == null)
                        {
                            attachmentSuperseded = true;
                            break;
                        }

                        if (desiredDependencySourceIndex != null ||
                            !ContainsReference(
                                desiredSubscriptions,
                                subscription))
                        {
                            desiredSubscriptions.Add(subscription);
                        }

                        if (added)
                            addedSubscriptions.Add(subscription);
                    }
                }
                catch
                {
                    RollBackObservableDependencyAttachmentsUnderLock(
                        registration,
                        addedSubscriptions);
                    ReindexObservableNotifyDependentsUnderLock(
                        registration);

                    throw;
                }

                if (attachmentSuperseded ||
                    !IsObservableBindingActiveUnderLock(registration))
                {
                    RollBackObservableDependencyAttachmentsUnderLock(
                        registration,
                        addedSubscriptions);

                    if (IsObservableBindingActiveUnderLock(registration))
                        DetachObservableBindingUnderLock(registration);

                    return;
                }

                ArrayList previousSubscriptions =
                    registration.SourceSubscriptions;

                for (i = previousSubscriptions.Count - 1;
                     i >= 0;
                     i--)
                {
                    ObservableSourceSubscription previous =
                        previousSubscriptions[i] as
                            ObservableSourceSubscription;

                    bool retained =
                        desiredDependencySourceIndex != null
                            ? previous != null &&
                                desiredDependencySourceIndex.ContainsSource(
                                    previous.Source)
                            : ContainsReference(
                                desiredSubscriptions,
                                previous);

                    if (!retained)
                    {
                        ReleaseObservableDependencyUnderLock(
                            registration,
                            previous);

                        if (!IsObservableBindingActiveUnderLock(registration))
                            return;
                    }
                }

                registration.PathDependencies = desiredDependencies;
                registration.DependencySourceIndex =
                    desiredDependencySourceIndex;
                registration.SourceSubscriptions = desiredSubscriptions;
                CompleteObservableDependencyAttachmentsUnderLock(
                    registration);
                ReindexObservableNotifyDependentsUnderLock(
                    registration);
                registration.TerminalDependency =
                    pathResult.TerminalDependency;
                registration.Revision =
                    NextObservableRevisionUnderLock();
                ClearObservablePendingUnderLock(registration);

                if (HaveObservableDependencySnapshotsChanged(
                        registration.PathDependencies))
                {
                    MarkObservableSourcePendingUnderLock(
                        registration,
                        true);
                    shouldQueue = true;
                }

                RefreshObservableDispatchDebtUnderLock();
            }

            if (shouldQueue)
                QueueObservableBindingDispatch();
        }

        private bool ObservableBindingMatches(
            ObservableBindingRegistration registration,
            BindingPathResult pathResult)
        {
            if (registration == null || pathResult == null)
                return false;

            lock (_observableBindingSync)
            {
                return IsObservableBindingActiveUnderLock(registration) &&
                    ObservableBindingMatchesUnderLock(
                        registration,
                        pathResult);
            }
        }

        private static bool ObservableBindingMatchesUnderLock(
            ObservableBindingRegistration registration,
            BindingPathResult pathResult)
        {
            if (registration.PathDependencies == null ||
                registration.PathDependencies.Count !=
                    pathResult.Dependencies.Count)
            {
                return false;
            }

            int i;

            for (i = 0;
                 i < registration.PathDependencies.Count;
                 i++)
            {
                BindingPathDependency current =
                    registration.PathDependencies[i] as
                        BindingPathDependency;
                BindingPathDependency candidate =
                    pathResult.Dependencies[i] as
                        BindingPathDependency;

                if (!ObservableDependenciesMatch(
                        current,
                        candidate))
                {
                    return false;
                }
            }

            return ObservableDependenciesMatch(
                registration.TerminalDependency,
                pathResult.TerminalDependency);
        }

        private static bool ObservableDependenciesMatch(
            BindingPathDependency left,
            BindingPathDependency right)
        {
            if (left == null || right == null)
                return left == null && right == null;

            return Object.ReferenceEquals(left.Source, right.Source) &&
                Object.ReferenceEquals(
                    left.RuntimeBinding,
                    right.RuntimeBinding) &&
                Object.ReferenceEquals(
                    left.NotifyProperty,
                    right.NotifyProperty) &&
                Object.ReferenceEquals(
                    left.NotifyField,
                    right.NotifyField) &&
                left.MayRebind == right.MayRebind;
        }

        private void DetachObservableBinding(
            ObservableBindingRegistration registration)
        {
            if (registration == null)
                return;

            lock (_observableBindingSync)
                DetachObservableBindingUnderLock(registration);
        }

        private void DetachObservableBindings(object owner)
        {
            if (owner == null)
                return;

            ArrayList registrations = null;

            lock (_observableBindingSync)
            {
                if (_observableRegistrationsByOwner != null)
                {
                    ArrayList retained =
                        _observableRegistrationsByOwner[owner] as ArrayList;

                    if (retained != null)
                        registrations = new ArrayList(retained);
                }
            }

            if (registrations == null)
                return;

            int i;

            for (i = registrations.Count - 1; i >= 0; i--)
            {
                DetachObservableBinding(
                    registrations[i] as
                        ObservableBindingRegistration);
            }
        }

        private void BeginObservableTargetUpdate(
            object owner,
            string expectedTargetPropertyName)
        {
            if (owner == null)
                return;

            lock (_observableBindingSync)
            {
                if (_observableBindingSubscriptionsDisposed)
                    return;

                EnsureObservableBindingStorageUnderLock();

                object retained =
                    _observableTargetUpdateDepthByOwner[owner];
                int depth =
                    retained == null
                        ? 0
                        : (int)retained;

                ArrayList registrations =
                    _observableRegistrationsByOwner[owner] as ArrayList;
                int i;

                for (i = 0;
                     registrations != null && i < registrations.Count;
                     i++)
                {
                    ObservableBindingRegistration registration =
                        registrations[i] as ObservableBindingRegistration;

                    if (registration == null ||
                        registration.Mode != BindingMode.TwoWay)
                    {
                        continue;
                    }

                    if (depth == 0)
                    {
                        registration.SuppressedTargetSignalCount = 0;
                        registration.SuppressedTargetExpectedSignalCount = 0;
                    }

                    if (ObservableRegistrationTargetsProperty(
                            registration,
                            expectedTargetPropertyName))
                    {
                        // This synchronous source-to-target application is newer
                        // than any target edit already waiting for dispatch.
                        // A target notification raised during the setter is
                        // captured below and receives a newer signal order.
                        ClearObservableTargetPendingUnderLock(registration);
                        registration.SuppressedTargetExpectedSignalCount++;

                        if (registration.TargetProperty != null &&
                            registration.TargetProperty.AlternateChangedEvent !=
                                null)
                        {
                            registration.AlternateSnapshotRefreshRequested =
                                true;
                        }
                    }
                }

                RefreshObservableDispatchDebtUnderLock();

                _observableTargetUpdateDepthByOwner[owner] =
                    depth + 1;
            }
        }

        private void EndObservableTargetUpdate(object owner)
        {
            if (owner == null)
                return;

            ArrayList reconcile = null;
            ArrayList refreshAlternateSnapshots = null;

            lock (_observableBindingSync)
            {
                if (_observableTargetUpdateDepthByOwner == null)
                    return;

                object retained =
                    _observableTargetUpdateDepthByOwner[owner];

                if (retained == null)
                    return;

                int depth = (int)retained;

                if (depth > 1)
                {
                    _observableTargetUpdateDepthByOwner[owner] = depth - 1;
                    return;
                }

                _observableTargetUpdateDepthByOwner.Remove(owner);

                ArrayList registrations =
                    _observableRegistrationsByOwner == null
                        ? null
                        : _observableRegistrationsByOwner[owner] as ArrayList;
                int i;

                for (i = 0;
                     registrations != null && i < registrations.Count;
                     i++)
                {
                    ObservableBindingRegistration registration =
                        registrations[i] as ObservableBindingRegistration;

                    if (registration == null)
                        continue;

                    int signalCount =
                        registration.SuppressedTargetSignalCount;
                    int expectedSignalCount =
                        registration.SuppressedTargetExpectedSignalCount;
                    registration.SuppressedTargetSignalCount = 0;
                    registration.SuppressedTargetExpectedSignalCount = 0;

                    if (registration.AlternateSnapshotRefreshRequested)
                    {
                        registration.AlternateSnapshotRefreshRequested = false;

                        if (refreshAlternateSnapshots == null)
                            refreshAlternateSnapshots = new ArrayList();

                        refreshAlternateSnapshots.Add(registration);
                    }

                    // Ignore only the notifications budgeted for the property
                    // this runtime setter is applying. Any extra notification,
                    // including one from another property, is a real target edit.
                    if (signalCount > expectedSignalCount &&
                        IsObservableBindingActiveUnderLock(registration) &&
                        registration.Mode == BindingMode.TwoWay)
                    {
                        if (reconcile == null)
                            reconcile = new ArrayList();

                        reconcile.Add(registration);
                    }
                }
            }

            bool shouldQueue = false;
            int n;

            for (n = 0;
                 refreshAlternateSnapshots != null &&
                 n < refreshAlternateSnapshots.Count;
                 n++)
            {
                RefreshObservableAlternateTargetSnapshot(
                    refreshAlternateSnapshots[n] as
                        ObservableBindingRegistration);
            }

            for (n = 0; reconcile != null && n < reconcile.Count; n++)
            {
                ObservableBindingRegistration registration =
                    reconcile[n] as ObservableBindingRegistration;
                object targetValue;

                if (!TryGetObservableTargetValue(
                        registration,
                        out targetValue))
                {
                    continue;
                }

                lock (_observableBindingSync)
                {
                    if (!IsObservableBindingActiveUnderLock(registration) ||
                        registration.Mode != BindingMode.TwoWay ||
                        IsObservableTargetUpdateSuppressedUnderLock(owner))
                    {
                        continue;
                    }

                    MarkObservableTargetPendingUnderLock(
                        registration,
                        targetValue);
                    shouldQueue = true;
                }
            }

            if (shouldQueue)
                QueueObservableBindingDispatch();
        }

        private void RefreshObservableAlternateTargetSnapshot(
            ObservableBindingRegistration registration)
        {
            if (registration == null ||
                registration.Target == null ||
                registration.TargetProperty == null ||
                registration.TargetProperty.Descriptor == null ||
                registration.TargetProperty.AlternateChangedEvent == null)
            {
                return;
            }

            object value;

            try
            {
                value = registration.TargetProperty.Descriptor.GetValue(
                    registration.Target);
            }
            catch
            {
                // Preserve the original target setter result. A later genuine
                // event will retry the same descriptor read through the normal
                // target-change path.
                return;
            }

            lock (_observableBindingSync)
            {
                if (!IsObservableBindingActiveUnderLock(registration) ||
                    registration.TargetProperty == null ||
                    registration.TargetProperty.AlternateChangedEvent == null)
                {
                    return;
                }

                registration.LastAlternateTargetValue = value;
                registration.HasLastAlternateTargetValue = true;
            }
        }

        private static bool ObservableRegistrationTargetsProperty(
            ObservableBindingRegistration registration,
            string propertyName)
        {
            if (registration == null || String.IsNullOrEmpty(propertyName))
                return false;

            if (EqualsIgnoreCase(
                    registration.TargetPropertyName,
                    propertyName))
            {
                return true;
            }

            return registration.TargetProperty != null &&
                EqualsIgnoreCase(
                    registration.TargetProperty.ResolvedName,
                    propertyName);
        }

        private bool IsObservableBindingCurrent(
            ObservableBindingRegistration registration,
            object owner,
            long revision)
        {
            if (registration == null || owner == null)
                return false;

            lock (_observableBindingSync)
            {
                return IsObservableBindingActiveUnderLock(registration) &&
                    Object.ReferenceEquals(registration.Owner, owner) &&
                    registration.Revision == revision;
            }
        }

        private static bool HaveObservableDependencySnapshotsChanged(
            ArrayList dependencies)
        {
            int i;

            for (i = 0;
                 dependencies != null && i < dependencies.Count;
                 i++)
            {
                BindingPathDependency dependency =
                    dependencies[i] as BindingPathDependency;

                if (dependency == null)
                    continue;

                if (dependency.RuntimeBinding != null)
                {
                    long currentVersion;
                    GetPropertyBindingSnapshot(
                        dependency.RuntimeBinding,
                        out currentVersion);

                    if (currentVersion != dependency.Version)
                        return true;
                }
                else
                {
                    object currentValue =
                        GetNotifyDependencyValue(dependency);

                    if (dependency.MayRebind)
                    {
                        if (!Object.ReferenceEquals(
                                currentValue,
                                dependency.SnapshotValue))
                        {
                            return true;
                        }
                    }
                    else if (!Object.Equals(
                                 currentValue,
                                 dependency.SnapshotValue))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TrySetObservableSourceValue(
            BindingPathDependency dependency,
            object value,
            long expectedVersion,
            out bool versionConflict)
        {
            versionConflict = false;

            if (dependency == null ||
                dependency.Source == null)
            {
                return false;
            }

            Type valueType =
                GetBindingDependencyValueType(dependency);

            if (valueType == null)
                return false;

            object converted;

            if (!TryConvertObjectValue(
                    value,
                    valueType,
                    out converted))
            {
                return false;
            }

            if (dependency.RuntimeBinding != null)
            {
                bool applied =
                    dependency.RuntimeBinding.TrySetSnapshot(
                        expectedVersion,
                        converted);
                versionConflict = !applied;
                return applied;
            }

            if (!IsWritableNotifyPropertyDependency(dependency))
                return false;

            dependency.NotifyProperty.SetValue(
                dependency.Source,
                converted,
                null);
            return true;
        }

        private static object GetNotifyDependencyValue(
            BindingPathDependency dependency)
        {
            if (dependency == null || dependency.Source == null)
            {
                throw new InvalidOperationException(
                    "A notifying binding dependency is incomplete.");
            }

            if (dependency.NotifyProperty != null)
            {
                return dependency.NotifyProperty.GetValue(
                    dependency.Source,
                    null);
            }

            if (dependency.NotifyField != null)
                return dependency.NotifyField.GetValue(dependency.Source);

            throw new InvalidOperationException(
                "A notifying binding dependency has no CLR member.");
        }

        private static bool IsWritableNotifyPropertyDependency(
            BindingPathDependency dependency)
        {
            return dependency != null &&
                dependency.RuntimeBinding == null &&
                dependency.Source is INotifyPropertyChanged &&
                dependency.NotifyProperty != null &&
                dependency.NotifyProperty.GetIndexParameters().Length == 0 &&
                dependency.NotifyProperty.GetSetMethod() != null;
        }

        private static bool TryGetObservableTargetValue(
            ObservableBindingRegistration registration,
            out object value)
        {
            value = null;

            if (registration == null ||
                registration.Target == null)
            {
                return false;
            }

            if (registration.TargetRuntimeBinding != null)
            {
                long ignoredVersion;
                value = registration.TargetRuntimeBinding.GetSnapshot(
                    out ignoredVersion);
                return true;
            }

            if (registration.TargetProperty == null ||
                registration.TargetProperty.Descriptor == null)
            {
                return false;
            }

            value = registration.TargetProperty.Descriptor.GetValue(
                registration.Target);
            return true;
        }

        private bool TrySetObservableTargetValue(
            ObservableBindingRegistration registration,
            object value)
        {
            if (registration == null ||
                registration.Target == null)
            {
                return false;
            }

            object converted;
            Type targetType;

            if (registration.TargetRuntimeBinding != null)
            {
                targetType =
                    registration.TargetRuntimeBinding.ValueType;
            }
            else
            {
                if (registration.TargetProperty == null ||
                    registration.TargetProperty.Descriptor == null ||
                    registration.TargetProperty.Descriptor.IsReadOnly)
                {
                    return false;
                }

                targetType =
                    registration.TargetProperty.Descriptor.PropertyType;
            }

            if (!TryConvertObjectValue(
                    value,
                    targetType,
                    out converted))
            {
                return false;
            }

            BeginObservableTargetUpdate(
                registration.Owner,
                registration.TargetPropertyName);

            try
            {
                if (registration.TargetRuntimeBinding != null)
                {
                    registration.TargetRuntimeBinding.SetValue(converted);
                }
                else
                {
                    registration.TargetProperty.Descriptor.SetValue(
                        registration.Target,
                        converted);
                }
            }
            finally
            {
                EndObservableTargetUpdate(registration.Owner);
            }

            return true;
        }

        private static ArrayList CopyBindingPathDependencies(
            ArrayList dependencies)
        {
            ArrayList copy = new ArrayList();

            if (dependencies == null)
                return copy;

            int i;

            for (i = 0; i < dependencies.Count; i++)
            {
                BindingPathDependency dependency =
                    dependencies[i] as BindingPathDependency;

                if (dependency != null)
                    copy.Add(dependency);
            }

            return copy;
        }

        private static void RemoveReference(
            ArrayList values,
            object candidate)
        {
            if (values == null)
                return;

            int i;

            for (i = values.Count - 1; i >= 0; i--)
            {
                if (Object.ReferenceEquals(values[i], candidate))
                {
                    values.RemoveAt(i);
                    return;
                }
            }
        }

        private static void ValidateObservableTwoWayEndpoints(
            object target,
            string targetPropertyName,
            ObservableTargetProperty targetProperty,
            IPropertyBindingRuntime targetRuntimeBinding,
            BindingUpdateSourceTrigger updateSourceTrigger,
            BindingPathResult pathResult)
        {
            if (pathResult.HasComputedExpression)
            {
                throw new InvalidOperationException(
                    "Mode=TwoWay cannot be combined with a computed " +
                    "Binding expression.");
            }

            if (pathResult.HasNegation)
            {
                throw new InvalidOperationException(
                    "Mode=TwoWay cannot be combined with the ! binding operator.");
            }

            if (pathResult.TerminalDependency == null ||
                pathResult.TerminalDependency.Source == null ||
                (pathResult.TerminalDependency.RuntimeBinding == null &&
                 !IsWritableNotifyPropertyDependency(
                     pathResult.TerminalDependency)))
            {
                throw new InvalidOperationException(
                    "Mode=TwoWay requires the Binding path to end in a " +
                    "writable PropertyBinding<T> or notifying CLR property.");
            }

            if (targetRuntimeBinding == null &&
                (targetProperty == null ||
                 targetProperty.Descriptor == null))
            {
                throw new InvalidOperationException(
                    "Mode=TwoWay could not resolve target property '" +
                    targetPropertyName +
                    "' on " +
                    target.GetType().FullName +
                    ".");
            }

            if (targetRuntimeBinding == null &&
                targetProperty.Descriptor.IsReadOnly)
            {
                throw new InvalidOperationException(
                    "Mode=TwoWay requires writable target property '" +
                    targetPropertyName +
                    "'.");
            }

            if (updateSourceTrigger ==
                    BindingUpdateSourceTrigger.LostFocus &&
                !(target is Control))
            {
                throw new InvalidOperationException(
                    "UpdateSourceTrigger=LostFocus requires a WinForms " +
                    "Control target.");
            }

            if (targetRuntimeBinding == null &&
                updateSourceTrigger ==
                    BindingUpdateSourceTrigger.PropertyChanged &&
                !targetProperty.Descriptor.SupportsChangeEvents &&
                targetProperty.AlternateChangedEvent == null)
            {
                throw new InvalidOperationException(
                    "Mode=TwoWay requires reliable change notification for " +
                    "target property '" +
                    targetPropertyName +
                    "'.");
            }
        }

        private static ObservableTargetProperty
            ResolveObservableTargetProperty(
                Type targetType,
                string propertyName)
        {
            if (targetType == null ||
                String.IsNullOrEmpty(propertyName))
            {
                return _missingObservableTargetProperty;
            }

            ObservableTargetPropertyCacheKey key =
                new ObservableTargetPropertyCacheKey(
                    targetType,
                    propertyName);

            lock (_observableTargetPropertyCacheSync)
            {
                ObservableTargetProperty cached =
                    _observableTargetPropertyCache[key] as
                        ObservableTargetProperty;

                if (cached != null)
                    return cached;
            }

            PropertyDescriptorCollection properties =
                TypeDescriptor.GetProperties(targetType);
            string alias =
                GetObservableTargetPropertyAlias(
                    targetType,
                    propertyName);
            bool preferAlias =
                ShouldPreferObservableTargetPropertyAlias(
                    propertyName,
                    alias);
            PropertyDescriptor descriptor = null;
            string resolvedName = propertyName;

            if (preferAlias)
            {
                descriptor = properties.Find(alias, true);

                if (descriptor != null)
                    resolvedName = alias;
            }

            if (descriptor == null)
                descriptor = properties.Find(propertyName, true);

            if (descriptor != null &&
                alias != null &&
                !preferAlias &&
                descriptor.IsReadOnly)
            {
                descriptor = null;
            }

            if (descriptor == null && alias != null && !preferAlias)
            {
                descriptor = properties.Find(alias, true);

                if (descriptor != null)
                    resolvedName = alias;
            }

            EventDescriptor alternateChangedEvent = null;

            if (descriptor != null && !descriptor.SupportsChangeEvents)
            {
                string alternateEventName =
                    GetObservableTargetAlternateChangedEventName(
                        targetType,
                        resolvedName);

                if (!String.IsNullOrEmpty(alternateEventName))
                {
                    alternateChangedEvent =
                        TypeDescriptor.GetEvents(targetType).Find(
                            alternateEventName,
                            true);
                }
            }

            ObservableTargetProperty resolved =
                descriptor == null
                    ? _missingObservableTargetProperty
                    : new ObservableTargetProperty(
                        propertyName,
                        resolvedName,
                        descriptor,
                        alternateChangedEvent);

            lock (_observableTargetPropertyCacheSync)
            {
                ObservableTargetProperty cached =
                    _observableTargetPropertyCache[key] as
                        ObservableTargetProperty;

                if (cached != null)
                    return cached;

                // Preserve the established hot set. Clearing here lets a stream
                // of novel target properties repeatedly evict common descriptors.
                if (_observableTargetPropertyCache.Count <
                    ObservableTargetPropertyCacheLimit)
                {
                    _observableTargetPropertyCache[key] = resolved;
                }
            }

            return resolved;
        }

        private static string
            GetObservableTargetAlternateChangedEventName(
                Type targetType,
                string propertyName)
        {
            if (targetType == null || String.IsNullOrEmpty(propertyName))
                return null;

            if (typeof(Control).IsAssignableFrom(targetType) &&
                (EqualsIgnoreCase(propertyName, "Width") ||
                 EqualsIgnoreCase(propertyName, "Height")))
            {
                return "SizeChanged";
            }

            if (typeof(Control).IsAssignableFrom(targetType) &&
                (EqualsIgnoreCase(propertyName, "Left") ||
                 EqualsIgnoreCase(propertyName, "Top")))
            {
                return "LocationChanged";
            }

            if ((typeof(TextBoxBase).IsAssignableFrom(targetType) ||
                 typeof(ToolStripTextBox).IsAssignableFrom(targetType)) &&
                EqualsIgnoreCase(propertyName, "Lines"))
            {
                return "TextChanged";
            }

            if (typeof(RichTextBox).IsAssignableFrom(targetType) &&
                EqualsIgnoreCase(propertyName, "Rtf"))
            {
                return "TextChanged";
            }

            if (typeof(DataGridView).IsAssignableFrom(targetType) &&
                IsDataGridViewScrollProperty(propertyName))
            {
                return "Scroll";
            }

            if ((typeof(ComboBox).IsAssignableFrom(targetType) ||
                 typeof(ListBox).IsAssignableFrom(targetType)) &&
                EqualsIgnoreCase(propertyName, "SelectedItem"))
            {
                return "SelectedIndexChanged";
            }

            if (typeof(ToolStripComboBox).IsAssignableFrom(targetType) &&
                EqualsIgnoreCase(propertyName, "SelectedItem"))
            {
                return "SelectedIndexChanged";
            }

            if (typeof(DomainUpDown).IsAssignableFrom(targetType) &&
                EqualsIgnoreCase(propertyName, "SelectedIndex"))
            {
                return "SelectedItemChanged";
            }

            if (typeof(MonthCalendar).IsAssignableFrom(targetType) &&
                (EqualsIgnoreCase(propertyName, "SelectionStart") ||
                 EqualsIgnoreCase(propertyName, "SelectionEnd") ||
                 EqualsIgnoreCase(propertyName, "SelectionRange")))
            {
                return "DateChanged";
            }

            if (typeof(TreeView).IsAssignableFrom(targetType) &&
                EqualsIgnoreCase(propertyName, "SelectedNode"))
            {
                return "AfterSelect";
            }

            if (typeof(TabControl).IsAssignableFrom(targetType) &&
                EqualsIgnoreCase(propertyName, "SelectedTab"))
            {
                return "SelectedIndexChanged";
            }

            if (typeof(RichTextBox).IsAssignableFrom(targetType) &&
                IsRichTextSelectionProperty(propertyName))
            {
                return "SelectionChanged";
            }

            if (typeof(Form).IsAssignableFrom(targetType) &&
                EqualsIgnoreCase(propertyName, "WindowState"))
            {
                return "SizeChanged";
            }

            if (typeof(SplitContainer).IsAssignableFrom(targetType) &&
                EqualsIgnoreCase(propertyName, "SplitterDistance"))
            {
                return "SplitterMoved";
            }

            if (typeof(Splitter).IsAssignableFrom(targetType) &&
                EqualsIgnoreCase(propertyName, "SplitPosition"))
            {
                return "SplitterMoved";
            }

            if (typeof(WebBrowser).IsAssignableFrom(targetType) &&
                EqualsIgnoreCase(propertyName, "Url"))
            {
                return "Navigated";
            }

            if (typeof(ScrollableControl).IsAssignableFrom(targetType) &&
                EqualsIgnoreCase(propertyName, "AutoScrollPosition"))
            {
                return "Scroll";
            }

            if (typeof(PropertyGrid).IsAssignableFrom(targetType) &&
                EqualsIgnoreCase(propertyName, "SelectedObject"))
            {
                return "SelectedObjectsChanged";
            }

            return null;
        }

        private static bool IsDataGridViewScrollProperty(
            string propertyName)
        {
            return
                EqualsIgnoreCase(propertyName, "FirstDisplayedCell") ||
                EqualsIgnoreCase(
                    propertyName,
                    "FirstDisplayedScrollingColumnIndex") ||
                EqualsIgnoreCase(
                    propertyName,
                    "FirstDisplayedScrollingRowIndex") ||
                EqualsIgnoreCase(
                    propertyName,
                    "HorizontalScrollingOffset");
        }

        private static bool IsRichTextSelectionProperty(
            string propertyName)
        {
            return
                EqualsIgnoreCase(propertyName, "SelectedText") ||
                EqualsIgnoreCase(propertyName, "SelectionAlignment") ||
                EqualsIgnoreCase(propertyName, "SelectionBullet") ||
                EqualsIgnoreCase(propertyName, "SelectionCharOffset") ||
                EqualsIgnoreCase(propertyName, "SelectionColor") ||
                EqualsIgnoreCase(propertyName, "SelectionFont") ||
                EqualsIgnoreCase(propertyName, "SelectionHangingIndent") ||
                EqualsIgnoreCase(propertyName, "SelectionIndent") ||
                EqualsIgnoreCase(propertyName, "SelectionLength") ||
                EqualsIgnoreCase(propertyName, "SelectionProtected") ||
                EqualsIgnoreCase(propertyName, "SelectionRightIndent") ||
                EqualsIgnoreCase(propertyName, "SelectionStart") ||
                EqualsIgnoreCase(propertyName, "SelectionTabs");
        }

        private static bool ShouldPreferObservableTargetPropertyAlias(
            string propertyName,
            string alias)
        {
            if (String.IsNullOrEmpty(alias))
                return false;

            // Content-like aliases deliberately defer to a real writable CLR
            // property in TryApplyWpfProperty. Every other alias returned here
            // is the native property that the mapped setter chooses first.
            return
                !String.Equals(
                    propertyName,
                    "Content",
                    StringComparison.OrdinalIgnoreCase) &&
                !String.Equals(
                    propertyName,
                    "Header",
                    StringComparison.OrdinalIgnoreCase) &&
                !String.Equals(
                    propertyName,
                    "Title",
                    StringComparison.OrdinalIgnoreCase);
        }

        private static string GetObservableTargetPropertyAlias(
            Type targetType,
            string propertyName)
        {
            if (String.Equals(
                    propertyName,
                    "Content",
                    StringComparison.OrdinalIgnoreCase) ||
                String.Equals(
                    propertyName,
                    "Header",
                    StringComparison.OrdinalIgnoreCase) ||
                String.Equals(
                    propertyName,
                    "Title",
                    StringComparison.OrdinalIgnoreCase))
            {
                return "Text";
            }

            if (String.Equals(
                    propertyName,
                    "IsChecked",
                    StringComparison.OrdinalIgnoreCase) &&
                targetType != null &&
                (typeof(CheckBox).IsAssignableFrom(targetType) ||
                 typeof(RadioButton).IsAssignableFrom(targetType)))
            {
                return "Checked";
            }

            if (String.Equals(
                    propertyName,
                    "IsEnabled",
                    StringComparison.OrdinalIgnoreCase) &&
                targetType != null &&
                typeof(Control).IsAssignableFrom(targetType))
            {
                return "Enabled";
            }

            if (String.Equals(
                    propertyName,
                    "IsTabStop",
                    StringComparison.OrdinalIgnoreCase) &&
                targetType != null &&
                typeof(Control).IsAssignableFrom(targetType))
            {
                return "TabStop";
            }

            if (String.Equals(
                    propertyName,
                    "IsReadOnly",
                    StringComparison.OrdinalIgnoreCase) &&
                targetType != null &&
                typeof(TextBoxBase).IsAssignableFrom(targetType))
            {
                return "ReadOnly";
            }

            if (String.Equals(
                    propertyName,
                    "Source",
                    StringComparison.OrdinalIgnoreCase) &&
                targetType != null &&
                typeof(WebBrowser).IsAssignableFrom(targetType))
            {
                return "Url";
            }

            if (String.Equals(
                    propertyName,
                    "Foreground",
                    StringComparison.OrdinalIgnoreCase))
            {
                return "ForeColor";
            }

            if (String.Equals(
                    propertyName,
                    "Background",
                    StringComparison.OrdinalIgnoreCase))
            {
                return "BackColor";
            }

            return null;
        }
    }
}
