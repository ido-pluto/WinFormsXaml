using System;
using System.Collections;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace WinFormsXaml
{
    public sealed partial class XamlRuntime
    {
        private void OnObservableBindingRootReady()
        {
            Control root = RootControl;

            lock (_observableBindingSync)
            {
                if (_observableBindingSubscriptionsDisposed)
                    return;

                if (_observableDisposalClaimed)
                    return;

                _observableRootReady = true;
            }

            if (root == null)
            {
                if (_root == null)
                    return;

                root = EnsureObservableRootlessDispatcher();

                if (root == null)
                    return;
            }

            if (root.IsDisposed)
            {
                DisposeObservableBindingSubscriptions();
                return;
            }

            Control previousRoot = null;

            lock (_observableBindingSync)
            {
                if (_observableBindingSubscriptionsDisposed)
                    return;

                if (_observableRootHandleHooked &&
                    !Object.ReferenceEquals(
                        _observableHookedRoot,
                        root))
                {
                    previousRoot = _observableHookedRoot;
                    _observableRootHandleHooked = false;
                    _observableHookedRoot = null;
                }

                if (!_observableRootHandleHooked)
                {
                    root.HandleCreated +=
                        OnObservableRootHandleCreated;
                    root.HandleDestroyed +=
                        OnObservableRootHandleDestroyed;
                    _observableHookedRoot = root;
                    _observableRootHandleHooked = true;
                }
            }

            if (previousRoot != null)
            {
                previousRoot.HandleCreated -=
                    OnObservableRootHandleCreated;
                previousRoot.HandleDestroyed -=
                    OnObservableRootHandleDestroyed;
            }

            QueueObservableBindingDispatch();
        }

        private void DisposeObservableBindingSubscriptions()
        {
            Control hookedRoot;
            Control rootlessDispatcher;
            bool rootHandleHooked;
            bool retryOnly;

            PrepareObservableBindingDisposal();

            lock (_observableBindingSync)
            {
                if (_observableBindingSubscriptionsDisposed)
                {
                    rootlessDispatcher =
                        _observableRootlessDispatcher;

                    if (rootlessDispatcher == null)
                        return;

                    hookedRoot = null;
                    rootHandleHooked = false;
                    retryOnly = true;
                }
                else
                {
                    retryOnly = false;

                    _observableBindingSubscriptionsDisposed = true;
                    hookedRoot = _observableHookedRoot;
                    rootlessDispatcher = _observableRootlessDispatcher;
                    rootHandleHooked = _observableRootHandleHooked;
                    _observableHookedRoot = null;
                    _observableRootHandleHooked = false;

                    if (_observableBindingRegistrations != null)
                    {
                        ArrayList registrations =
                            new ArrayList(
                                _observableBindingRegistrations);
                        int i;

                        for (i = registrations.Count - 1;
                             i >= 0;
                             i--)
                        {
                            DetachObservableBindingUnderLock(
                                registrations[i] as
                                    ObservableBindingRegistration);
                        }

                        _observableBindingRegistrations.Clear();
                    }

                    if (_observableSourceSubscriptions != null)
                        _observableSourceSubscriptions.Clear();

                    if (_observableRegistrationsByOwner != null)
                        _observableRegistrationsByOwner.Clear();

                    if (_observableTargetUpdateDepthByOwner != null)
                        _observableTargetUpdateDepthByOwner.Clear();

                    if (_observablePendingRegistrations != null)
                        _observablePendingRegistrations.Clear();

                    _observablePendingRegistrationCount = 0;

                    _observableDispatchQueued = false;
                    _observableDispatchRunning = false;
                    _observableDispatchDebt = false;
                    _observableSynchronousDispatchActive = false;
                    _observableRootReady = false;
                    _observableDispatchPostEpoch++;
                }
            }

            if (!retryOnly && rootHandleHooked && hookedRoot != null)
            {
                hookedRoot.HandleCreated -=
                    OnObservableRootHandleCreated;
                hookedRoot.HandleDestroyed -=
                    OnObservableRootHandleDestroyed;
            }

            if (rootlessDispatcher != null)
            {
                rootlessDispatcher.Dispose();

                lock (_observableBindingSync)
                {
                    if (Object.ReferenceEquals(
                            _observableRootlessDispatcher,
                            rootlessDispatcher))
                    {
                        _observableRootlessDispatcher = null;
                    }
                }
            }
        }

        private void PrepareObservableBindingDisposal()
        {
            lock (_observableBindingSync)
            {
                VerifyObservableBindingDisposalThreadLocked();

                // Close the race with a late observable attachment. Once
                // disposal may proceed, no private dispatcher can be created
                // or published.
                _observableDisposalClaimed = true;
            }
        }

        private void VerifyObservableBindingDisposalThread()
        {
            lock (_observableBindingSync)
                VerifyObservableBindingDisposalThreadLocked();
        }

        private void VerifyObservableBindingDisposalThreadLocked()
        {
            // Failed XmlForm rollback may release and clear the partial root as
            // soon as its last worker physically terminates, while the paired
            // wrapper still owns retryable cleanup debt. Keep that debt on the
            // load thread just like an ordinary loaded runtime.
            bool requiresOwnerThread =
                _root != null ||
                _xmlFormLifetimeTarget != null ||
                _failedLoadRollbackPending ||
                HasRetainedEventRemovalDebt();

            if (requiresOwnerThread &&
                System.Threading.Thread.CurrentThread.ManagedThreadId !=
                    _observableOwnerThreadId)
            {
                throw new InvalidOperationException(
                    "A loaded WinFormsXaml runtime must be disposed on the " +
                    "thread that loaded it.");
            }
        }

        private Control EnsureObservableRootlessDispatcher()
        {
            Control dispatcher;

            lock (_observableBindingSync)
            {
                if (_observableBindingSubscriptionsDisposed ||
                    _observableDisposalClaimed ||
                    RootControl != null)
                {
                    return RootControl;
                }

                dispatcher = _observableRootlessDispatcher;

                if (dispatcher != null)
                    return dispatcher;

                if (_observableBindingRegistrations == null ||
                    _observableBindingRegistrations.Count == 0)
                {
                    return null;
                }
            }

            if (System.Threading.Thread.CurrentThread.ManagedThreadId !=
                _observableOwnerThreadId)
            {
                throw new InvalidOperationException(
                    "Reactive bindings for a non-Control XML root must be " +
                    "created on the thread that loaded the runtime.");
            }

            Control created = new Control();

            try
            {
                // A private native handle gives worker-thread source
                // notifications a real WinForms owner-thread dispatcher.
                // Same-thread rootless notifications still drain directly,
                // so runtimes that do not use a message loop stay useful.
                IntPtr ignoredHandle = created.Handle;
            }
            catch
            {
                created.Dispose();
                throw;
            }

            lock (_observableBindingSync)
            {
                if (_observableBindingSubscriptionsDisposed ||
                    _observableDisposalClaimed ||
                    RootControl != null)
                {
                    dispatcher = RootControl;
                }
                else if (_observableRootlessDispatcher != null)
                {
                    dispatcher = _observableRootlessDispatcher;
                }
                else
                {
                    _observableRootlessDispatcher = created;
                    dispatcher = created;
                    created = null;
                }
            }

            if (created != null)
                created.Dispose();

            return dispatcher;
        }

        private void OnObservableRootHandleCreated(
            object sender,
            EventArgs e)
        {
            QueueObservableBindingDispatch();
        }

        private void OnObservableRootHandleDestroyed(
            object sender,
            EventArgs e)
        {
            lock (_observableBindingSync)
            {
                if (_observableBindingSubscriptionsDisposed ||
                    !Object.ReferenceEquals(
                        sender,
                        _observableHookedRoot))
                {
                    return;
                }

                // BeginInvoke work belongs to the native handle that accepted
                // it. Revoke that post while retaining the logical binding debt;
                // HandleCreated will schedule the debt on the replacement handle.
                _observableDispatchQueued = false;
                _observableDispatchPostEpoch++;
                RefreshObservableDispatchDebtUnderLock();
            }
        }

        private void OnObservableSourceValueChanged(
            ObservableSourceSubscription subscription)
        {
            bool shouldQueue = false;

            lock (_observableBindingSync)
            {
                if (_observableBindingSubscriptionsDisposed ||
                    subscription == null ||
                    !subscription.Attached)
                {
                    return;
                }

                int i;
                long signalVersion;

                GetPropertyBindingSnapshot(
                    subscription.RuntimeBinding,
                    out signalVersion);

                for (i = 0;
                     i < subscription.Dependents.Count;
                     i++)
                {
                    ObservableBindingRegistration registration =
                        subscription.Dependents[i] as
                            ObservableBindingRegistration;

                    if (!IsObservableBindingActiveUnderLock(registration))
                    {
                        continue;
                    }

                    BindingPathDependency signalDependency =
                        FindObservableRuntimeDependency(
                            registration,
                            subscription.Source,
                            subscription.RuntimeBinding);

                    if (signalDependency == null)
                        continue;

                    bool terminalSignal =
                        registration.TerminalDependency != null &&
                        Object.ReferenceEquals(
                            registration.TerminalDependency.Source,
                            subscription.Source);

                    if (terminalSignal &&
                        registration.SourceWriteDepth > 0 &&
                        registration.SourceWriteRevision ==
                            registration.Revision &&
                        Object.ReferenceEquals(
                            registration.SourceWriteSource,
                            subscription.Source) &&
                        Object.ReferenceEquals(
                            registration.SourceWriteRuntimeBinding,
                            subscription.RuntimeBinding) &&
                        registration.SourceWriteExpectedVersion ==
                            signalVersion)
                    {
                        continue;
                    }

                    MarkObservableSourcePendingUnderLock(
                        registration,
                        signalDependency,
                        !terminalSignal);
                }

                shouldQueue =
                    _observableDispatchDebt &&
                    !_observableDispatchQueued &&
                    !_observableDispatchRunning;
            }

            if (shouldQueue)
                QueueObservableBindingDispatch();
        }

        private void OnObservableSourcePropertyChanged(
            ObservableSourceSubscription subscription,
            PropertyChangedEventArgs e)
        {
            bool shouldQueue = false;
            string propertyName = e == null
                ? null
                : e.PropertyName;

            lock (_observableBindingSync)
            {
                if (_observableBindingSubscriptionsDisposed ||
                    subscription == null ||
                    !subscription.Attached)
                {
                    return;
                }

                if (String.IsNullOrEmpty(propertyName))
                {
                    MarkNotifyPropertyDependentsPendingUnderLock(
                        subscription.Dependents,
                        null,
                        null,
                        subscription.Source,
                        propertyName);
                }
                else
                {
                    ArrayList exact =
                        subscription.NotifyDependentsByProperty[
                            propertyName] as ArrayList;
                    ArrayList wildcard =
                        subscription.NotifyWildcardDependents;

                    MarkNotifyPropertyDependentsPendingUnderLock(
                        exact,
                        null,
                        null,
                        subscription.Source,
                        propertyName);
                    MarkNotifyPropertyDependentsPendingUnderLock(
                        wildcard,
                        exact,
                        null,
                        subscription.Source,
                        propertyName);
                    MarkNotifyPropertyDependentsPendingUnderLock(
                        subscription.NotifyUnindexedDependents,
                        exact,
                        wildcard,
                        subscription.Source,
                        propertyName);
                }

                shouldQueue =
                    _observableDispatchDebt &&
                    !_observableDispatchQueued &&
                    !_observableDispatchRunning;
            }

            if (shouldQueue)
                QueueObservableBindingDispatch();
        }

        private void MarkNotifyPropertyDependentsPendingUnderLock(
            ArrayList dependents,
            ArrayList alreadyVisited,
            ArrayList alsoVisited,
            object source,
            string propertyName)
        {
            int i;

            for (i = 0; dependents != null && i < dependents.Count; i++)
            {
                ObservableBindingRegistration registration =
                    dependents[i] as ObservableBindingRegistration;

                if (!IsObservableBindingActiveUnderLock(registration) ||
                    (alreadyVisited != null &&
                     ContainsReference(alreadyVisited, registration)) ||
                    (alsoVisited != null &&
                     ContainsReference(alsoVisited, registration)))
                {
                    continue;
                }

                // Normal notifying properties intentionally use their standard
                // last-write-wins contract. They have no atomic version token,
                // so a notification remains eligible to reconcile a normalized
                // two-way target even when the value appears unchanged.
                MarkNotifyPropertySourcePendingUnderLock(
                    registration,
                    source,
                    propertyName);
            }
        }

        private static BindingPathDependency FindObservableRuntimeDependency(
            ObservableBindingRegistration registration,
            object source,
            IPropertyBindingRuntime runtimeBinding)
        {
            if (registration == null ||
                registration.PathDependencies == null)
            {
                return null;
            }

            object dependencies = GetObservableDependenciesForSource(
                registration,
                source);
            int dependencyCount =
                GetBindingDependencyBucketCount(dependencies);
            int i;

            for (i = 0;
                 i < dependencyCount;
                 i++)
            {
                BindingPathDependency dependency =
                    GetBindingDependencyFromBucket(
                        dependencies,
                        i);

                if (dependency != null &&
                    Object.ReferenceEquals(dependency.Source, source) &&
                    Object.ReferenceEquals(
                        dependency.RuntimeBinding,
                        runtimeBinding))
                {
                    return dependency;
                }
            }

            return null;
        }

        private bool MarkNotifyPropertySourcePendingUnderLock(
            ObservableBindingRegistration registration,
            object source,
            string propertyName)
        {
            if (registration == null ||
                registration.PathDependencies == null)
            {
                return false;
            }

            object dependencies = GetObservableDependenciesForSource(
                registration,
                source);
            int dependencyCount =
                GetBindingDependencyBucketCount(dependencies);
            bool wildcard = String.IsNullOrEmpty(propertyName);
            bool matched = false;
            long order = 0;
            int i;

            for (i = 0;
                 i < dependencyCount;
                 i++)
            {
                BindingPathDependency dependency =
                    GetBindingDependencyFromBucket(
                        dependencies,
                        i);

                if (dependency == null ||
                    dependency.RuntimeBinding != null ||
                    !Object.ReferenceEquals(dependency.Source, source) ||
                    (!wildcard &&
                     !String.IsNullOrEmpty(
                         dependency.NotifyMemberName) &&
                     !String.Equals(
                         dependency.NotifyMemberName,
                         propertyName,
                         StringComparison.Ordinal)))
                {
                    continue;
                }

                if (!matched)
                    order = NextObservableSignalUnderLock();

                matched = true;
                RecordObservableSourceSignalUnderLock(
                    registration,
                    dependency,
                    dependency.MayRebind,
                    order);
            }

            return matched;
        }

        private static object GetObservableDependenciesForSource(
            ObservableBindingRegistration registration,
            object source)
        {
            if (registration == null)
                return null;

            if (registration.DependencySourceIndex != null)
            {
                return registration.DependencySourceIndex.GetBucket(
                    source);
            }

            return registration.PathDependencies;
        }

        private static int GetBindingDependencyBucketCount(
            object bucket)
        {
            if (bucket is BindingPathDependency)
                return 1;

            ArrayList dependencies = bucket as ArrayList;
            return dependencies == null
                ? 0
                : dependencies.Count;
        }

        private static BindingPathDependency
            GetBindingDependencyFromBucket(
                object bucket,
                int index)
        {
            BindingPathDependency single =
                bucket as BindingPathDependency;

            if (single != null)
                return index == 0 ? single : null;

            ArrayList dependencies = bucket as ArrayList;

            if (dependencies == null ||
                index < 0 ||
                index >= dependencies.Count)
            {
                return null;
            }

            return dependencies[index] as BindingPathDependency;
        }

        private void OnObservableTargetValueChanged(
            ObservableBindingRegistration registration)
        {
            bool shouldQueue = false;
            bool alternateChangedEvent;
            long revision;
            BindingPathDependency terminal;

            lock (_observableBindingSync)
            {
                if (!IsObservableBindingActiveUnderLock(registration) ||
                    registration.Mode != BindingMode.TwoWay)
                {
                    return;
                }

                alternateChangedEvent =
                    registration.TargetProperty != null &&
                    registration.TargetProperty.AlternateChangedEvent != null;

                if (!alternateChangedEvent &&
                    IsObservableTargetUpdateSuppressedUnderLock(
                        registration.Owner))
                {
                    registration.SuppressedTargetSignalCount++;
                    return;
                }

                revision = registration.Revision;
                terminal = registration.TerminalDependency;
            }

            object targetValue;

            if (!TryGetObservableTargetValue(
                    registration,
                    out targetValue))
            {
                return;
            }

            lock (_observableBindingSync)
            {
                if (!IsObservableBindingActiveUnderLock(registration) ||
                    registration.Mode != BindingMode.TwoWay ||
                    registration.Revision != revision ||
                    !Object.ReferenceEquals(
                        registration.TerminalDependency,
                        terminal))
                {
                    return;
                }

                if (alternateChangedEvent)
                {
                    if (registration.HasLastAlternateTargetValue &&
                        Object.Equals(
                            registration.LastAlternateTargetValue,
                            targetValue))
                    {
                        return;
                    }

                    registration.LastAlternateTargetValue = targetValue;
                    registration.HasLastAlternateTargetValue = true;
                }

                if (IsObservableTargetUpdateSuppressedUnderLock(
                        registration.Owner))
                {
                    registration.SuppressedTargetSignalCount++;
                    return;
                }

                MarkObservableTargetPendingUnderLock(
                    registration,
                    targetValue);
                shouldQueue = !_observableDispatchRunning;
            }

            if (shouldQueue)
                QueueObservableBindingDispatch();
        }
    }
}
