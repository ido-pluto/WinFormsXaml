using System;
using System.Collections;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace WinFormsXaml
{
    public sealed partial class XamlRuntime
    {
        private void EnsureObservableBindingStorageUnderLock()
        {
            if (_observableSourceSubscriptions == null)
            {
                _observableSourceSubscriptions =
                    new Hashtable(_observableReferenceComparer);
            }

            if (_observableRegistrationsByOwner == null)
            {
                _observableRegistrationsByOwner =
                    new Hashtable(_observableReferenceComparer);
            }

            if (_observableTargetUpdateDepthByOwner == null)
            {
                _observableTargetUpdateDepthByOwner =
                    new Hashtable(_observableReferenceComparer);
            }

            if (_observableBindingRegistrations == null)
                _observableBindingRegistrations = new ArrayList();

            if (_observablePendingRegistrations == null)
                _observablePendingRegistrations = new ArrayList();
        }

        private void AddObservableRegistrationUnderLock(
            ObservableBindingRegistration registration)
        {
            _observableBindingRegistrations.Add(registration);

            ArrayList registrations =
                _observableRegistrationsByOwner[registration.Owner] as
                    ArrayList;

            if (registrations == null)
            {
                registrations = new ArrayList();
                _observableRegistrationsByOwner[registration.Owner] =
                    registrations;
            }

            registrations.Add(registration);
        }

        private bool AttachObservableDependenciesUnderLock(
            ObservableBindingRegistration registration,
            ArrayList dependencies,
            ArrayList subscriptions)
        {
            BindingDependencySourceIndex dependencySourceIndex =
                registration.DependencySourceIndex;
            int i;

            for (i = 0; i < dependencies.Count; i++)
            {
                BindingPathDependency dependency =
                    dependencies[i] as BindingPathDependency;

                if (dependency == null)
                    continue;

                if (dependencySourceIndex != null &&
                    !dependencySourceIndex.
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
                    return false;

                if (dependencySourceIndex != null ||
                    added ||
                    !ContainsReference(
                        subscriptions,
                        subscription))
                {
                    subscriptions.Add(subscription);
                }
            }

            return IsObservableBindingActiveUnderLock(registration);
        }

        private ObservableSourceSubscription
            AttachObservableDependencyUnderLock(
                ObservableBindingRegistration registration,
                BindingPathDependency dependency,
                out bool added)
        {
            added = false;

            if (dependency.Source == null ||
                (dependency.RuntimeBinding == null &&
                 !(dependency.Source is INotifyPropertyChanged)))
            {
                throw new InvalidOperationException(
                    "An observable binding dependency is incomplete.");
            }

            ObservableSourceSubscription subscription =
                _observableSourceSubscriptions[dependency.Source] as
                    ObservableSourceSubscription;
            bool created = false;

            if (subscription == null)
            {
                subscription = new ObservableSourceSubscription();
                subscription.Source = dependency.Source;
                subscription.RuntimeBinding =
                    dependency.RuntimeBinding;
                subscription.NotifySource =
                    dependency.Source as INotifyPropertyChanged;
                subscription.Forwarder =
                    new ObservableSourceForwarder(
                        this,
                        subscription);

                if (dependency.RuntimeBinding != null)
                {
                    subscription.Handler = new EventHandler(
                        subscription.Forwarder.OnValueChanged);
                }
                else
                {
                    subscription.PropertyChangedHandler =
                        new PropertyChangedEventHandler(
                            subscription.Forwarder.OnPropertyChanged);
                }

                // Publish every part cleanup needs before calling the
                // user-controlled event add accessor. A same-thread accessor
                // can reenter this monitor and dispose or replace the binding.
                subscription.Adding = true;
                subscription.Attached = true;
                _observableSourceSubscriptions[dependency.Source] =
                    subscription;
                created = true;
            }
            else if (subscription.Adding)
            {
                throw new InvalidOperationException(
                    "The same observable source re-entered while its " +
                    "notification handler was being attached.");
            }
            else if (!subscription.Attached ||
                subscription.DetachRequested)
            {
                throw new InvalidOperationException(
                    "An observable source attachment is no longer active.");
            }
            else if (!Object.ReferenceEquals(
                         subscription.RuntimeBinding,
                         dependency.RuntimeBinding) ||
                     !Object.ReferenceEquals(
                         subscription.NotifySource,
                         dependency.Source as INotifyPropertyChanged))
            {
                throw new InvalidOperationException(
                    "The same observable source resolved with inconsistent metadata.");
            }

            if (!ContainsReference(
                    subscription.Dependents,
                    registration))
            {
                subscription.Dependents.Add(registration);
                if (subscription.NotifySource != null)
                {
                    subscription.NotifyUnindexedDependents.Add(
                        registration);
                }
                TrackObservableDependencyAttachmentUnderLock(
                    registration,
                    subscription);
                added = true;
            }

            if (!created)
                return subscription;

            try
            {
                AttachObservableSourceHandler(subscription);
            }
            catch
            {
                subscription.Adding = false;
                ReleaseObservableDependencyUnderLock(
                    registration,
                    subscription);
                throw;
            }

            subscription.Adding = false;

            bool currentSubscription =
                subscription.Attached &&
                !subscription.DetachRequested &&
                _observableSourceSubscriptions != null &&
                Object.ReferenceEquals(
                    _observableSourceSubscriptions[dependency.Source],
                    subscription);
            bool currentDependent =
                IsObservableBindingActiveUnderLock(registration) &&
                ContainsReference(
                    subscription.Dependents,
                    registration);

            if (!currentSubscription || !currentDependent)
            {
                if (ContainsReference(
                        subscription.Dependents,
                        registration))
                {
                    ReleaseObservableDependencyUnderLock(
                        registration,
                        subscription);
                }
                else
                {
                    CompleteDeferredObservableSourceDetachUnderLock(
                        subscription);
                }

                added = false;
                return null;
            }

            return subscription;
        }

        private void ReleaseObservableDependencyUnderLock(
            ObservableBindingRegistration registration,
            ObservableSourceSubscription subscription)
        {
            if (registration == null || subscription == null)
                return;

            RemoveReference(
                subscription.Dependents,
                registration);
            RemoveObservableNotifyDependentIndexUnderLock(
                registration,
                subscription);
            RemoveObservableDependencyAttachmentUnderLock(
                registration,
                subscription);

            if (subscription.Dependents.Count != 0)
                return;

            subscription.Attached = false;

            if (subscription.Forwarder != null)
                subscription.Forwarder.Disable();

            if (_observableSourceSubscriptions != null &&
                subscription.Source != null &&
                Object.ReferenceEquals(
                    _observableSourceSubscriptions[subscription.Source],
                    subscription))
            {
                _observableSourceSubscriptions.Remove(
                    subscription.Source);
            }

            if (subscription.Adding)
            {
                // The add accessor has not returned yet. Removing now is not
                // sufficient when that accessor stores the handler afterward,
                // so leave an inert deferred removal for the outer attach.
                subscription.DetachRequested = true;
                return;
            }

            TryRemoveObservableSourceHandler(subscription);
            ClearObservableSourceSubscription(subscription);
        }

        private void CompleteDeferredObservableSourceDetachUnderLock(
            ObservableSourceSubscription subscription)
        {
            if (subscription == null ||
                subscription.Adding ||
                subscription.Dependents.Count != 0)
            {
                return;
            }

            subscription.Attached = false;

            if (subscription.Forwarder != null)
                subscription.Forwarder.Disable();

            if (_observableSourceSubscriptions != null &&
                subscription.Source != null &&
                Object.ReferenceEquals(
                    _observableSourceSubscriptions[subscription.Source],
                    subscription))
            {
                _observableSourceSubscriptions.Remove(
                    subscription.Source);
            }

            TryRemoveObservableSourceHandler(subscription);
            ClearObservableSourceSubscription(subscription);
        }

        private static void TrackObservableDependencyAttachmentUnderLock(
            ObservableBindingRegistration registration,
            ObservableSourceSubscription subscription)
        {
            if (registration.AttachingSourceSubscriptions == null)
            {
                registration.AttachingSourceSubscriptions =
                    new ArrayList();
            }

            if (!ContainsReference(
                    registration.AttachingSourceSubscriptions,
                    subscription))
            {
                registration.AttachingSourceSubscriptions.Add(
                    subscription);
            }
        }

        private static void RemoveObservableDependencyAttachmentUnderLock(
            ObservableBindingRegistration registration,
            ObservableSourceSubscription subscription)
        {
            if (registration == null ||
                registration.AttachingSourceSubscriptions == null)
            {
                return;
            }

            RemoveReference(
                registration.AttachingSourceSubscriptions,
                subscription);
        }

        private static void CompleteObservableDependencyAttachmentsUnderLock(
            ObservableBindingRegistration registration)
        {
            if (registration != null &&
                registration.AttachingSourceSubscriptions != null)
            {
                registration.AttachingSourceSubscriptions.Clear();
            }
        }

        private void RollBackObservableDependencyAttachmentsUnderLock(
            ObservableBindingRegistration registration,
            ArrayList subscriptions)
        {
            if (registration == null || subscriptions == null)
                return;

            int i;

            for (i = subscriptions.Count - 1; i >= 0; i--)
            {
                ObservableSourceSubscription subscription =
                    subscriptions[i] as ObservableSourceSubscription;

                if (subscription != null &&
                    ContainsReference(
                        subscription.Dependents,
                        registration))
                {
                    ReleaseObservableDependencyUnderLock(
                        registration,
                        subscription);
                }
            }

            CompleteObservableDependencyAttachmentsUnderLock(
                registration);
        }

        private static void AttachObservableSourceHandler(
            ObservableSourceSubscription subscription)
        {
            if (subscription.RuntimeBinding != null)
            {
                subscription.RuntimeBinding.ValueChanged +=
                    subscription.Handler;
            }
            else
            {
                subscription.NotifySource.PropertyChanged +=
                    subscription.PropertyChangedHandler;
            }
        }

        private static void TryRemoveObservableSourceHandler(
            ObservableSourceSubscription subscription)
        {
            try
            {
                if (subscription.RuntimeBinding != null)
                {
                    subscription.RuntimeBinding.ValueChanged -=
                        subscription.Handler;
                }
                else if (subscription.NotifySource != null)
                {
                    subscription.NotifySource.PropertyChanged -=
                        subscription.PropertyChangedHandler;
                }
            }
            catch
            {
                // The source can retain only the already-disabled forwarder.
            }
        }

        private static void ClearObservableSourceSubscription(
            ObservableSourceSubscription subscription)
        {
            subscription.Attached = false;
            subscription.Adding = false;
            subscription.DetachRequested = false;
            subscription.Dependents.Clear();
            subscription.NotifyDependentsByProperty.Clear();
            subscription.NotifyWildcardDependents.Clear();
            subscription.NotifyUnindexedDependents.Clear();
            subscription.Source = null;
            subscription.RuntimeBinding = null;
            subscription.NotifySource = null;
            subscription.Forwarder = null;
            subscription.Handler = null;
            subscription.PropertyChangedHandler = null;
        }

        private static void MarkObservableNotifyDependentUnindexedUnderLock(
            ObservableBindingRegistration registration,
            ObservableSourceSubscription subscription)
        {
            if (registration == null ||
                subscription == null ||
                subscription.NotifySource == null)
            {
                return;
            }

            RemoveObservableNotifyDependentIndexUnderLock(
                registration,
                subscription);

            if (!ContainsReference(
                    subscription.NotifyUnindexedDependents,
                    registration))
            {
                subscription.NotifyUnindexedDependents.Add(registration);
            }
        }

        private static void MarkObservableNotifyDependentsUnindexedUnderLock(
            ObservableBindingRegistration registration)
        {
            if (registration == null ||
                registration.SourceSubscriptions == null)
            {
                return;
            }

            int i;

            for (i = 0; i < registration.SourceSubscriptions.Count; i++)
            {
                MarkObservableNotifyDependentUnindexedUnderLock(
                    registration,
                    registration.SourceSubscriptions[i] as
                        ObservableSourceSubscription);
            }
        }

        private static void ReindexObservableNotifyDependentsUnderLock(
            ObservableBindingRegistration registration)
        {
            if (registration == null ||
                registration.SourceSubscriptions == null)
            {
                return;
            }

            int i;

            for (i = 0; i < registration.SourceSubscriptions.Count; i++)
            {
                ReindexObservableNotifyDependentUnderLock(
                    registration,
                    registration.SourceSubscriptions[i] as
                        ObservableSourceSubscription);
            }
        }

        private static void ReindexObservableNotifyDependentUnderLock(
            ObservableBindingRegistration registration,
            ObservableSourceSubscription subscription)
        {
            if (registration == null ||
                subscription == null ||
                subscription.NotifySource == null)
            {
                return;
            }

            RemoveObservableNotifyDependentIndexUnderLock(
                registration,
                subscription);

            object dependencies = GetObservableDependenciesForSource(
                registration,
                subscription.Source);
            int dependencyCount = GetBindingDependencyBucketCount(
                dependencies);
            int i;

            for (i = 0; i < dependencyCount; i++)
            {
                BindingPathDependency dependency =
                    GetBindingDependencyFromBucket(dependencies, i);

                if (dependency == null ||
                    dependency.RuntimeBinding != null ||
                    !Object.ReferenceEquals(
                        dependency.Source,
                        subscription.Source))
                {
                    continue;
                }

                if (String.IsNullOrEmpty(dependency.NotifyMemberName))
                {
                    if (!ContainsReference(
                            subscription.NotifyWildcardDependents,
                            registration))
                    {
                        subscription.NotifyWildcardDependents.Add(
                            registration);
                    }

                    continue;
                }

                ArrayList dependents =
                    subscription.NotifyDependentsByProperty[
                        dependency.NotifyMemberName] as ArrayList;

                if (dependents == null)
                {
                    dependents = new ArrayList();
                    subscription.NotifyDependentsByProperty[
                        dependency.NotifyMemberName] = dependents;
                }

                if (!ContainsReference(dependents, registration))
                    dependents.Add(registration);
            }
        }

        private static void RemoveObservableNotifyDependentIndexUnderLock(
            ObservableBindingRegistration registration,
            ObservableSourceSubscription subscription)
        {
            if (registration == null || subscription == null)
                return;

            RemoveReference(
                subscription.NotifyWildcardDependents,
                registration);
            RemoveReference(
                subscription.NotifyUnindexedDependents,
                registration);

            ArrayList emptyKeys = null;
            IDictionaryEnumerator iterator =
                subscription.NotifyDependentsByProperty.GetEnumerator();

            while (iterator.MoveNext())
            {
                ArrayList dependents = iterator.Value as ArrayList;

                if (dependents == null)
                    continue;

                RemoveReference(dependents, registration);

                if (dependents.Count == 0)
                {
                    if (emptyKeys == null)
                        emptyKeys = new ArrayList();

                    emptyKeys.Add(iterator.Key);
                }
            }

            int i;

            for (i = 0; emptyKeys != null && i < emptyKeys.Count; i++)
            {
                subscription.NotifyDependentsByProperty.Remove(
                    emptyKeys[i]);
            }
        }

        private void DetachObservableBindingUnderLock(
            ObservableBindingRegistration registration)
        {
            if (registration == null || !registration.Active)
                return;

            registration.Active = false;
            registration.Revision =
                NextObservableRevisionUnderLock();
            ClearObservablePendingUnderLock(registration);

            if (registration.TargetForwarder != null)
                registration.TargetForwarder.Disable();

            if (registration.TargetHandlerAttached &&
                (registration.TargetRuntimeBinding != null ||
                 (registration.TargetProperty != null &&
                  registration.TargetProperty.Descriptor != null)))
            {
                registration.TargetHandlerAttached = false;

                try
                {
                    if (registration.UpdateSourceTrigger ==
                            BindingUpdateSourceTrigger.LostFocus &&
                        registration.Target is Control)
                    {
                        ((Control)registration.Target).LostFocus -=
                            registration.TargetChangedHandler;
                    }
                    else if (registration.TargetRuntimeBinding != null)
                    {
                        registration.TargetRuntimeBinding.ValueChanged -=
                            registration.TargetChangedHandler;
                    }
                    else
                    {
                        EventDescriptor alternateChangedEvent =
                            registration.TargetProperty.AlternateChangedEvent;

                        if (alternateChangedEvent != null)
                        {
                            alternateChangedEvent.RemoveEventHandler(
                                registration.Target,
                                registration.TargetChangedDelegate);
                        }
                        else
                        {
                            registration.TargetProperty.Descriptor.
                                RemoveValueChanged(
                                    registration.Target,
                                    registration.TargetChangedHandler);
                        }
                    }
                }
                catch
                {
                    // A disposed custom descriptor or event accessor must not
                    // retain the rest of the runtime's observable graph.
                }
            }

            if (registration.SourceSubscriptions != null)
            {
                int i;

                for (i = registration.SourceSubscriptions.Count - 1;
                     i >= 0;
                     i--)
                {
                    ReleaseObservableDependencyUnderLock(
                        registration,
                        registration.SourceSubscriptions[i] as
                            ObservableSourceSubscription);
                }

                registration.SourceSubscriptions.Clear();
            }

            if (registration.AttachingSourceSubscriptions != null)
            {
                while (registration.AttachingSourceSubscriptions.Count > 0)
                {
                    int index =
                        registration.AttachingSourceSubscriptions.Count - 1;
                    ObservableSourceSubscription subscription =
                        registration.AttachingSourceSubscriptions[index]
                        as ObservableSourceSubscription;

                    if (subscription == null)
                    {
                        registration.AttachingSourceSubscriptions.RemoveAt(
                            index);
                        continue;
                    }

                    ReleaseObservableDependencyUnderLock(
                        registration,
                        subscription);
                }

                registration.AttachingSourceSubscriptions.Clear();
            }

            if (_observableBindingRegistrations != null)
            {
                RemoveReference(
                    _observableBindingRegistrations,
                    registration);
            }

            if (_observableRegistrationsByOwner != null &&
                registration.Owner != null)
            {
                ArrayList registrations =
                    _observableRegistrationsByOwner[registration.Owner] as
                        ArrayList;

                if (registrations != null)
                {
                    RemoveReference(registrations, registration);

                    if (registrations.Count == 0)
                    {
                        _observableRegistrationsByOwner.Remove(
                            registration.Owner);
                    }
                }
            }

            registration.PathDependencies = null;
            registration.DependencySourceIndex = null;
            registration.AttachingSourceSubscriptions = null;
            registration.TerminalDependency = null;
            registration.SuppressedTargetSignalCount = 0;
            registration.SuppressedTargetExpectedSignalCount = 0;
            registration.SourceWriteDepth = 0;
            registration.SourceWriteSource = null;
            registration.SourceWriteRuntimeBinding = null;
            registration.SourceWriteRevision = 0;
            registration.SourceWriteExpectedVersion = 0;
            registration.Callback = null;
            registration.TargetForwarder = null;
            registration.TargetChangedHandler = null;
            registration.TargetChangedDelegate = null;
            registration.LastAlternateTargetValue = null;
            registration.HasLastAlternateTargetValue = false;
            registration.AlternateSnapshotRefreshRequested = false;
            registration.TargetHandlerAttached = false;
            registration.TargetRuntimeBinding = null;
            registration.TargetProperty = null;
            registration.TargetPropertyName = null;
            registration.UpdateSourceTrigger =
                BindingUpdateSourceTrigger.PropertyChanged;
            registration.Target = null;
            registration.Owner = null;
            RefreshObservableDispatchDebtUnderLock();
        }

        private bool IsObservableBindingActiveUnderLock(
            ObservableBindingRegistration registration)
        {
            return !_observableBindingSubscriptionsDisposed &&
                registration != null &&
                registration.Active;
        }

        private bool IsObservableTargetUpdateSuppressedUnderLock(
            object owner)
        {
            if (owner == null ||
                _observableTargetUpdateDepthByOwner == null)
            {
                return false;
            }

            object retained =
                _observableTargetUpdateDepthByOwner[owner];

            return retained != null && (int)retained > 0;
        }

        private void ClearObservablePendingUnderLock(
            ObservableBindingRegistration registration)
        {
            if (registration == null)
                return;

            registration.PendingSource = false;
            registration.PendingTarget = false;
            registration.PendingSourceMayRebind = false;

            if (registration.PendingSourceSignals != null)
                registration.PendingSourceSignals.Clear();

            registration.PendingTargetValue = null;
            registration.PendingSourceOrder = 0;
            registration.PendingTargetOrder = 0;
            registration.PendingSourceRevision = 0;
            registration.PendingTargetRevision = 0;
            registration.PendingTargetSourceVersion = 0;
            registration.HasPendingTargetSourceVersion = false;

            UpdateObservablePendingRegistrationUnderLock(
                registration);
        }

        private void ClearObservableTargetPendingUnderLock(
            ObservableBindingRegistration registration)
        {
            if (registration == null)
                return;

            registration.PendingTarget = false;
            registration.PendingTargetValue = null;
            registration.PendingTargetOrder = 0;
            registration.PendingTargetRevision = 0;
            registration.PendingTargetSourceVersion = 0;
            registration.HasPendingTargetSourceVersion = false;

            UpdateObservablePendingRegistrationUnderLock(
                registration);
        }

        private void MarkObservableSourcePendingUnderLock(
            ObservableBindingRegistration registration,
            bool mayRebind)
        {
            if (!IsObservableBindingActiveUnderLock(registration))
                return;

            RecordObservableSourceSignalUnderLock(
                registration,
                null,
                mayRebind,
                NextObservableSignalUnderLock());
        }

        private void MarkObservableSourcePendingUnderLock(
            ObservableBindingRegistration registration,
            BindingPathDependency dependency,
            bool mayRebind)
        {
            if (!IsObservableBindingActiveUnderLock(registration))
                return;

            RecordObservableSourceSignalUnderLock(
                registration,
                dependency,
                mayRebind,
                NextObservableSignalUnderLock());
        }

        private void RecordObservableSourceSignalUnderLock(
            ObservableBindingRegistration registration,
            BindingPathDependency dependency,
            bool mayRebind,
            long order)
        {
            if (!IsObservableBindingActiveUnderLock(registration))
                return;

            if (registration.PendingSourceSignals == null)
                registration.PendingSourceSignals = new ArrayList();

            ObservableSourceSignal retained = null;
            int i;

            for (i = 0;
                 i < registration.PendingSourceSignals.Count;
                 i++)
            {
                ObservableSourceSignal candidate =
                    registration.PendingSourceSignals[i] as
                        ObservableSourceSignal;

                if (candidate != null &&
                    candidate.MayRebind == mayRebind &&
                    ObservableDependenciesMatch(
                        candidate.Dependency,
                        dependency))
                {
                    retained = candidate;
                    break;
                }
            }

            if (retained == null)
            {
                retained = new ObservableSourceSignal();
                retained.Dependency = dependency;
                retained.MayRebind = mayRebind;
                registration.PendingSourceSignals.Add(retained);
            }

            retained.Order = order;
            registration.PendingSource = true;
            registration.PendingSourceOrder = order;
            registration.PendingSourceRevision =
                registration.Revision;
            registration.PendingSourceMayRebind =
                registration.PendingSourceMayRebind || mayRebind;
            UpdateObservablePendingRegistrationUnderLock(
                registration);
        }

        private void MarkObservableTargetPendingUnderLock(
            ObservableBindingRegistration registration,
            object targetValue)
        {
            if (!IsObservableBindingActiveUnderLock(registration))
                return;

            BindingPathDependency terminal =
                registration.TerminalDependency;

            if (terminal == null)
                return;

            long sourceVersion =
                GetObservableSourceVersionUnderLock(terminal);

            registration.PendingTarget = true;
            registration.PendingTargetOrder =
                NextObservableSignalUnderLock();
            registration.PendingTargetRevision =
                registration.Revision;
            registration.PendingTargetSourceVersion =
                sourceVersion;
            registration.HasPendingTargetSourceVersion = true;
            registration.PendingTargetValue = targetValue;
            UpdateObservablePendingRegistrationUnderLock(
                registration);
        }

        private static long GetObservableSourceVersionUnderLock(
            BindingPathDependency dependency)
        {
            if (dependency == null)
                return 0;

            if (dependency.RuntimeBinding == null)
                return 0;

            long version;
            GetPropertyBindingSnapshot(
                dependency.RuntimeBinding,
                out version);
            return version;
        }

        private static bool HasNewerObservableSignalUnderLock(
            ObservableBindingRegistration registration,
            long order)
        {
            if (registration == null)
                return false;

            long sourceOrder =
                registration.PendingSource &&
                registration.PendingSourceRevision ==
                    registration.Revision
                    ? GetLatestCurrentObservableSourceSignalOrderUnderLock(
                        registration,
                        registration.PendingSourceSignals)
                    : 0;

            return sourceOrder > order ||
                (registration.PendingTarget &&
                 registration.PendingTargetRevision ==
                    registration.Revision &&
                 registration.PendingTargetOrder > order);
        }

        private static long
            GetLatestCurrentObservableSourceSignalOrderUnderLock(
                ObservableBindingRegistration registration,
                ArrayList signals)
        {
            if (registration == null || signals == null)
                return 0;

            long latestOrder = 0;
            int i;

            for (i = 0; i < signals.Count; i++)
            {
                ObservableSourceSignal signal =
                    signals[i] as ObservableSourceSignal;

                if (signal == null || signal.Order <= latestOrder)
                    continue;

                if (signal.Dependency == null ||
                    ContainsCurrentObservableDependencyUnderLock(
                        registration,
                        signal.Dependency))
                {
                    latestOrder = signal.Order;
                }
            }

            return latestOrder;
        }

        private static bool ContainsCurrentObservableDependencyUnderLock(
            ObservableBindingRegistration registration,
            BindingPathDependency dependency)
        {
            if (registration == null ||
                registration.PathDependencies == null ||
                dependency == null)
            {
                return false;
            }

            object dependencies = GetObservableDependenciesForSource(
                registration,
                dependency.Source);
            int dependencyCount =
                GetBindingDependencyBucketCount(dependencies);
            int i;

            for (i = 0;
                 i < dependencyCount;
                 i++)
            {
                BindingPathDependency current =
                    GetBindingDependencyFromBucket(
                        dependencies,
                        i);

                if (ObservableDependenciesMatch(current, dependency))
                    return true;
            }

            return false;
        }

        private void RefreshObservableDispatchDebtUnderLock()
        {
            _observableDispatchDebt =
                _observablePendingRegistrationCount > 0;
        }

        private void UpdateObservablePendingRegistrationUnderLock(
            ObservableBindingRegistration registration)
        {
            if (registration == null)
                return;

            bool pending =
                IsObservableBindingActiveUnderLock(registration) &&
                (registration.PendingSource ||
                 registration.PendingTarget);

            if (pending)
            {
                if (!registration.PendingDispatchIndexed)
                {
                    registration.PendingDispatchIndexed = true;
                    _observablePendingRegistrationCount++;
                }

                if (!registration.PendingDispatchQueued)
                {
                    if (_observablePendingRegistrations == null)
                        _observablePendingRegistrations = new ArrayList();

                    registration.PendingDispatchQueued = true;
                    _observablePendingRegistrations.Add(registration);
                }
            }
            else if (registration.PendingDispatchIndexed)
            {
                registration.PendingDispatchIndexed = false;

                if (_observablePendingRegistrationCount > 0)
                    _observablePendingRegistrationCount--;
            }

            _observableDispatchDebt =
                _observablePendingRegistrationCount > 0;
        }

        private long NextObservableRevisionUnderLock()
        {
            unchecked
            {
                _observableRevisionSequence++;
            }

            if (_observableRevisionSequence <= 0)
                _observableRevisionSequence = 1;

            return _observableRevisionSequence;
        }

        private long NextObservableSignalUnderLock()
        {
            unchecked
            {
                _observableSignalSequence++;
            }

            if (_observableSignalSequence <= 0)
                _observableSignalSequence = 1;

            return _observableSignalSequence;
        }
    }
}
