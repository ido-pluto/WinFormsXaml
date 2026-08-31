using System;
using System.Collections;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace WinFormsXaml
{
    public sealed partial class XamlRuntime
    {
        private delegate void ObservableBindingChangedCallback(
            object owner,
            long revision);

        private sealed class ObservableReferenceComparer : IEqualityComparer
        {
            public new bool Equals(object left, object right)
            {
                return Object.ReferenceEquals(left, right);
            }

            public int GetHashCode(object value)
            {
                return value == null
                    ? 0
                    : RuntimeHelpers.GetHashCode(value);
            }
        }

        private sealed class ObservableTargetPropertyCacheKey
        {
            private readonly Type _targetType;
            private readonly string _propertyName;

            public ObservableTargetPropertyCacheKey(
                Type targetType,
                string propertyName)
            {
                _targetType = targetType;
                _propertyName = propertyName;
            }

            public override bool Equals(object value)
            {
                ObservableTargetPropertyCacheKey other =
                    value as ObservableTargetPropertyCacheKey;

                return other != null &&
                    Object.ReferenceEquals(_targetType, other._targetType) &&
                    String.Equals(
                        _propertyName,
                        other._propertyName,
                        StringComparison.OrdinalIgnoreCase);
            }

            public override int GetHashCode()
            {
                int typeHash =
                    _targetType == null
                        ? 0
                        : RuntimeHelpers.GetHashCode(_targetType);
                int nameHash =
                    _propertyName == null
                        ? 0
                        : StringComparer.OrdinalIgnoreCase.GetHashCode(
                            _propertyName);

                return typeHash ^ nameHash;
            }
        }

        private sealed class ObservableTargetProperty
        {
            public readonly string RequestedName;
            public readonly string ResolvedName;
            public readonly PropertyDescriptor Descriptor;
            public readonly EventDescriptor AlternateChangedEvent;

            public ObservableTargetProperty(
                string requestedName,
                string resolvedName,
                PropertyDescriptor descriptor,
                EventDescriptor alternateChangedEvent)
            {
                RequestedName = requestedName;
                ResolvedName = resolvedName;
                Descriptor = descriptor;
                AlternateChangedEvent = alternateChangedEvent;
            }
        }

        private sealed class ObservableSourceSubscription
        {
            public object Source;
            public IPropertyBindingRuntime RuntimeBinding;
            public INotifyPropertyChanged NotifySource;
            public ObservableSourceForwarder Forwarder;
            public EventHandler Handler;
            public PropertyChangedEventHandler PropertyChangedHandler;
            public readonly ArrayList Dependents = new ArrayList();
            public readonly Hashtable NotifyDependentsByProperty =
                new Hashtable(StringComparer.Ordinal);
            public readonly ArrayList NotifyWildcardDependents =
                new ArrayList();
            public readonly ArrayList NotifyUnindexedDependents =
                new ArrayList();
            public bool Attached;
            public bool Adding;
            public bool DetachRequested;
        }

        private sealed class ObservableSourceForwarder
        {
            private volatile XamlRuntime _owner;
            private volatile ObservableSourceSubscription _subscription;

            public ObservableSourceForwarder(
                XamlRuntime owner,
                ObservableSourceSubscription subscription)
            {
                _subscription = subscription;
                _owner = owner;
            }

            public void OnValueChanged(object sender, EventArgs e)
            {
                XamlRuntime owner = _owner;
                ObservableSourceSubscription subscription =
                    _subscription;

                if (owner != null && subscription != null)
                {
                    owner.OnObservableSourceValueChanged(subscription);
                }
            }

            public void OnPropertyChanged(
                object sender,
                PropertyChangedEventArgs e)
            {
                XamlRuntime owner = _owner;
                ObservableSourceSubscription subscription =
                    _subscription;

                if (owner != null && subscription != null)
                {
                    owner.OnObservableSourcePropertyChanged(
                        subscription,
                        e);
                }
            }

            public void Disable()
            {
                _owner = null;
                _subscription = null;
            }
        }

        private sealed class ObservableTargetForwarder
        {
            private volatile XamlRuntime _owner;
            private volatile ObservableBindingRegistration _registration;

            public ObservableTargetForwarder(
                XamlRuntime owner,
                ObservableBindingRegistration registration)
            {
                _registration = registration;
                _owner = owner;
            }

            public void OnValueChanged(object sender, EventArgs e)
            {
                XamlRuntime owner = _owner;
                ObservableBindingRegistration registration =
                    _registration;

                if (owner != null && registration != null)
                    owner.OnObservableTargetValueChanged(registration);
            }

            public void OnDateRangeChanged(
                object sender,
                DateRangeEventArgs e)
            {
                OnValueChanged(sender, e);
            }

            public void OnTreeViewChanged(
                object sender,
                TreeViewEventArgs e)
            {
                OnValueChanged(sender, e);
            }

            public void OnSplitterMoved(
                object sender,
                SplitterEventArgs e)
            {
                OnValueChanged(sender, e);
            }

            public void OnWebBrowserNavigated(
                object sender,
                WebBrowserNavigatedEventArgs e)
            {
                OnValueChanged(sender, e);
            }

            public void OnScrolled(
                object sender,
                ScrollEventArgs e)
            {
                OnValueChanged(sender, e);
            }

            public void Disable()
            {
                _owner = null;
                _registration = null;
            }
        }

        // Dependency identity lets a rebind ignore late notifications from the
        // detached branch without ignoring newer changes on the current branch.
        private sealed class ObservableSourceSignal
        {
            public BindingPathDependency Dependency;
            public bool MayRebind;
            public long Order;
        }

        private sealed class ObservableBindingRegistration
        {
            public object Owner;
            public object Target;
            public string TargetPropertyName;
            public BindingMode Mode;
            public BindingUpdateSourceTrigger UpdateSourceTrigger;
            public ObservableTargetProperty TargetProperty;
            public IPropertyBindingRuntime TargetRuntimeBinding;
            public ObservableBindingChangedCallback Callback;
            public ArrayList PathDependencies;
            public BindingDependencySourceIndex DependencySourceIndex;
            public ArrayList SourceSubscriptions;
            public ArrayList AttachingSourceSubscriptions;
            public BindingPathDependency TerminalDependency;
            public ObservableTargetForwarder TargetForwarder;
            public EventHandler TargetChangedHandler;
            public Delegate TargetChangedDelegate;
            public object LastAlternateTargetValue;
            public bool HasLastAlternateTargetValue;
            public bool AlternateSnapshotRefreshRequested;
            public bool TargetHandlerAttached;
            public bool Active;
            public long Revision;
            public int SourceWriteDepth;
            public object SourceWriteSource;
            public IPropertyBindingRuntime SourceWriteRuntimeBinding;
            public long SourceWriteRevision;
            public long SourceWriteExpectedVersion;
            public int SuppressedTargetSignalCount;
            public int SuppressedTargetExpectedSignalCount;
            public bool PendingSource;
            public bool PendingTarget;
            public bool PendingSourceMayRebind;
            public ArrayList PendingSourceSignals;
            public object PendingTargetValue;
            public long PendingSourceOrder;
            public long PendingTargetOrder;
            public long PendingSourceRevision;
            public long PendingTargetRevision;
            public long PendingTargetSourceVersion;
            public bool HasPendingTargetSourceVersion;
            public bool PendingDispatchIndexed;
            public bool PendingDispatchQueued;
        }

        private sealed class ObservableDispatchWork
        {
            public ObservableBindingRegistration Registration;
            public long Revision;
            public bool TargetToSource;
            public long ExpectedSourceVersion;
            public long Order;
            public long TargetOrder;
            public bool SourceMayRebind;
            public bool ReplayTargetAfterRebind;
            public ArrayList SourceSignals;
            public object CapturedTargetValue;
        }

        private sealed class ObservableDispatchWorkComparer : IComparer
        {
            public int Compare(object leftValue, object rightValue)
            {
                ObservableDispatchWork left =
                    leftValue as ObservableDispatchWork;
                ObservableDispatchWork right =
                    rightValue as ObservableDispatchWork;

                if (Object.ReferenceEquals(left, right))
                    return 0;
                if (left == null)
                    return 1;
                if (right == null)
                    return -1;

                if (left.Order > right.Order)
                    return -1;
                if (left.Order < right.Order)
                    return 1;

                return 0;
            }
        }

        private const int ObservableTargetPropertyCacheLimit = 512;

        private static readonly object _observableTargetPropertyCacheSync =
            new object();
        private static readonly Hashtable _observableTargetPropertyCache =
            new Hashtable();
        private static readonly ObservableTargetProperty
            _missingObservableTargetProperty =
                new ObservableTargetProperty(null, null, null, null);
        private static readonly IEqualityComparer _observableReferenceComparer =
            new ObservableReferenceComparer();
        private static readonly IComparer _observableDispatchWorkComparer =
            new ObservableDispatchWorkComparer();

        private readonly object _observableBindingSync = new object();
        private Hashtable _observableSourceSubscriptions;
        private Hashtable _observableRegistrationsByOwner;
        private Hashtable _observableTargetUpdateDepthByOwner;
        private ArrayList _observableBindingRegistrations;
        private ArrayList _observablePendingRegistrations;
        private int _observablePendingRegistrationCount;
        private bool _observableBindingSubscriptionsDisposed;
        private bool _observableDisposalClaimed;
        private bool _observableDispatchQueued;
        private bool _observableDispatchRunning;
        private bool _observableDispatchDebt;
        private bool _observableSynchronousDispatchActive;
        private bool _observableRootReady;
        private int _observableDispatchPostEpoch;
        private long _observableSignalSequence;
        private long _observableRevisionSequence;
        private Control _observableHookedRoot;
        private bool _observableRootHandleHooked;
        private Control _observableRootlessDispatcher;
        private readonly int _observableOwnerThreadId =
            System.Threading.Thread.CurrentThread.ManagedThreadId;
    }
}
