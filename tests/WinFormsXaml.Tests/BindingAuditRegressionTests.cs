using System;
using System.Collections;
using System.ComponentModel;
using System.Reflection;
using System.Windows.Forms;
using WinFormsXaml;

namespace WinFormsXaml.Tests
{
    public sealed class ExactHeaderControl : Control
    {
        private string _header;

        public string Header
        {
            get { return _header; }
            set
            {
                if (String.Equals(_header, value, StringComparison.Ordinal))
                    return;

                _header = value;
                OnHeaderChanged(EventArgs.Empty);
            }
        }

        public event EventHandler HeaderChanged;

        private void OnHeaderChanged(EventArgs e)
        {
            EventHandler handler = HeaderChanged;

            if (handler != null)
                handler(this, e);
        }
    }

    public sealed class ExactStyleControl : Control
    {
        private string _style;

        public string Style
        {
            get { return _style; }
            set
            {
                if (String.Equals(_style, value, StringComparison.Ordinal))
                    return;

                _style = value;
                OnStyleChanged(EventArgs.Empty);
            }
        }

        public new event EventHandler StyleChanged;

        private new void OnStyleChanged(EventArgs e)
        {
            EventHandler handler = StyleChanged;

            if (handler != null)
                handler(this, e);
        }
    }

    public sealed class RetainingValueControl : Control
    {
        private string _value;
        private EventHandler _valueChanged;

        public static bool ThrowAfterAdd;
        public static bool ThrowOnRemove;
        public static RetainingValueControl LastInstance;

        public int AddCount;
        public int RemoveAttemptCount;

        public RetainingValueControl()
        {
            LastInstance = this;
        }

        public string Value
        {
            get { return _value; }
            set
            {
                if (String.Equals(
                        _value,
                        value,
                        StringComparison.Ordinal))
                {
                    return;
                }

                _value = value;
                RaiseValueChanged();
            }
        }

        public event EventHandler ValueChanged
        {
            add
            {
                AddCount++;
                _valueChanged += value;

                if (ThrowAfterAdd)
                {
                    throw new InvalidOperationException(
                        "Target add failed after retaining its handler.");
                }
            }
            remove
            {
                RemoveAttemptCount++;

                if (ThrowOnRemove)
                {
                    throw new InvalidOperationException(
                        "Target remove failed before detaching its handler.");
                }

                _valueChanged -= value;
            }
        }

        public int SubscriberCount
        {
            get
            {
                return _valueChanged == null
                    ? 0
                    : _valueChanged.GetInvocationList().Length;
            }
        }

        public Delegate RetainedHandler
        {
            get { return _valueChanged; }
        }

        public void RaiseValueChanged()
        {
            EventHandler handler = _valueChanged;

            if (handler != null)
                handler(this, EventArgs.Empty);
        }
    }

    internal delegate void DynamicTargetAddReentry(
        RetainingDisposedComponent target,
        object runtime);

    public sealed class RetainingDisposedComponent : IComponent
    {
        private EventHandler _disposed;
        private ISite _site;
        private string _value;

        public static bool ThrowAfterAdd;
        public static bool ThrowOnRemove;
        public static bool RaiseDuringAdd;
        internal static DynamicTargetAddReentry ReenterAfterRaise;
        public static RetainingDisposedComponent LastInstance;

        public int AddCount;
        public int RemoveAttemptCount;

        public RetainingDisposedComponent()
        {
            LastInstance = this;
        }

        public string Value
        {
            get { return _value; }
            set { _value = value; }
        }

        public ISite Site
        {
            get { return _site; }
            set { _site = value; }
        }

        public event EventHandler Disposed
        {
            add
            {
                AddCount++;
                _disposed += value;

                DynamicTargetAddReentry reentry = RaiseDuringAdd
                    ? ReenterAfterRaise
                    : null;
                object runtime = null;

                if (reentry != null && value.Target != null)
                {
                    FieldInfo ownerField = value.Target.GetType().GetField(
                        "_owner",
                        BindingFlags.Instance | BindingFlags.NonPublic);

                    if (ownerField != null)
                        runtime = ownerField.GetValue(value.Target);
                }

                if (RaiseDuringAdd)
                    value(new object(), EventArgs.Empty);

                if (reentry != null)
                {
                    ReenterAfterRaise = null;
                    RaiseDuringAdd = false;
                    reentry(this, runtime);
                }

                if (ThrowAfterAdd)
                {
                    throw new InvalidOperationException(
                        "Disposed add failed after retaining its handler.");
                }
            }
            remove
            {
                RemoveAttemptCount++;

                if (ThrowOnRemove)
                {
                    throw new InvalidOperationException(
                        "Disposed remove failed before detaching its handler.");
                }

                _disposed -= value;
            }
        }

        public int SubscriberCount
        {
            get
            {
                return _disposed == null
                    ? 0
                    : _disposed.GetInvocationList().Length;
            }
        }

        public Delegate RetainedHandler
        {
            get { return _disposed; }
        }

        public void RaiseDisposed(object sender)
        {
            EventHandler handler = _disposed;

            if (handler != null)
                handler(sender, EventArgs.Empty);
        }

        public void Dispose()
        {
            // Deliberately retain handlers so failed runtime cleanup stays visible.
        }
    }

    internal static class BindingAuditRegressionTests
    {
        private sealed class AuditState
        {
            public readonly PropertyBinding<int> Number;
            public readonly PropertyBinding<string> Header;
            public readonly PropertyBinding<string> Style;
            public readonly PropertyBinding<string> OuterText;
            public readonly PropertyBinding<bool> Checked;

            public AuditState()
            {
                Number = new PropertyBinding<int>(1);
                Header = new PropertyBinding<string>("Header source");
                Style = new PropertyBinding<string>("native style source");
                OuterText = new PropertyBinding<string>("outer value");
                Checked = new PropertyBinding<bool>(false);
            }

            public string DescribeItem(object item)
            {
                return item == null ? "null item" : "non-null item";
            }

            public string FormatItem(string value)
            {
                return "Item: " + value;
            }
        }

        private sealed class AggregateNotifyState : INotifyPropertyChanged
        {
            private string _first;
            private string _second;
            private PropertyChangedEventHandler _propertyChanged;

            public AggregateNotifyState()
            {
                _first = "First";
                _second = "Second";
            }

            public event PropertyChangedEventHandler PropertyChanged
            {
                add { _propertyChanged += value; }
                remove { _propertyChanged -= value; }
            }

            public string First
            {
                get { return _first; }
                set
                {
                    if (String.Equals(
                            _first,
                            value,
                            StringComparison.Ordinal))
                    {
                        return;
                    }

                    _first = value;
                    RaisePropertyChanged("First");
                }
            }

            public string Second
            {
                get { return _second; }
                set
                {
                    if (String.Equals(
                            _second,
                            value,
                            StringComparison.Ordinal))
                    {
                        return;
                    }

                    _second = value;
                    RaisePropertyChanged("Second");
                }
            }

            public int SubscriberCount
            {
                get
                {
                    return _propertyChanged == null
                        ? 0
                        : _propertyChanged.GetInvocationList().Length;
                }
            }

            public void Raise(string propertyName)
            {
                RaisePropertyChanged(propertyName);
            }

            public string Format(string first, string second)
            {
                return first + " / " + second;
            }

            private void RaisePropertyChanged(string propertyName)
            {
                PropertyChangedEventHandler handler = _propertyChanged;

                if (handler != null)
                {
                    handler(
                        this,
                        new PropertyChangedEventArgs(propertyName));
                }
            }
        }

        private sealed class NotifyBranch : INotifyPropertyChanged
        {
            private string _text;
            private PropertyChangedEventHandler _propertyChanged;

            public NotifyBranch(string text)
            {
                _text = text;
            }

            public event PropertyChangedEventHandler PropertyChanged
            {
                add { _propertyChanged += value; }
                remove { _propertyChanged -= value; }
            }

            public string Text
            {
                get { return _text; }
                set
                {
                    _text = value;
                    RaiseText();
                }
            }

            public void RaiseText()
            {
                PropertyChangedEventHandler handler = _propertyChanged;

                if (handler != null)
                {
                    handler(
                        this,
                        new PropertyChangedEventArgs("Text"));
                }
            }
        }

        private sealed class NotifyBranchState : INotifyPropertyChanged
        {
            private NotifyBranch _branch;
            private PropertyChangedEventHandler _propertyChanged;

            public NotifyBranchState()
            {
                _branch = new NotifyBranch("Same");
            }

            public event PropertyChangedEventHandler PropertyChanged
            {
                add { _propertyChanged += value; }
                remove { _propertyChanged -= value; }
            }

            public NotifyBranch Branch
            {
                get { return _branch; }
                set
                {
                    _branch = value;

                    PropertyChangedEventHandler handler = _propertyChanged;

                    if (handler != null)
                    {
                        handler(
                            this,
                            new PropertyChangedEventArgs("Branch"));
                    }
                }
            }
        }

        private sealed class AttachedBindingState
        {
            public readonly PropertyBinding<int> Row;
            public string DiagnosticRow;

            public AttachedBindingState()
            {
                Row = new PropertyBinding<int>(0);
                DiagnosticRow = "0";
            }
        }

        private sealed class FunctionPlanState
        {
            public string Value;

            public FunctionPlanState(string value)
            {
                Value = value;
            }

            public string Echo(string value)
            {
                return "echo:" + value;
            }

            public string ParseNumber(int value)
            {
                return "number:" + value.ToString();
            }
        }

        private sealed class ReentrantDisposalNotifyState
            : INotifyPropertyChanged
        {
            private PropertyChangedEventHandler _propertyChanged;

            public RetainingDisposedComponent Target;
            public bool RaiseDisposedOnRemove;

            public event PropertyChangedEventHandler PropertyChanged
            {
                add { _propertyChanged += value; }
                remove
                {
                    _propertyChanged -= value;

                    if (RaiseDisposedOnRemove && Target != null)
                    {
                        RaiseDisposedOnRemove = false;
                        Target.RaiseDisposed(new object());
                    }
                }
            }

            public string Value
            {
                get { return "Reentrant value"; }
            }

            public int SubscriberCount
            {
                get
                {
                    return _propertyChanged == null
                        ? 0
                        : _propertyChanged.GetInvocationList().Length;
                }
            }
        }

        private sealed class IndexedConditionRow : INotifyPropertyChanged
        {
            private bool _show;
            private PropertyChangedEventHandler _propertyChanged;

            public readonly string Id;
            public int AddCount;
            public int RemoveCount;

            public IndexedConditionRow(string id)
            {
                Id = id;
                _show = true;
            }

            public event PropertyChangedEventHandler PropertyChanged
            {
                add
                {
                    AddCount++;
                    _propertyChanged += value;
                }
                remove
                {
                    RemoveCount++;
                    _propertyChanged -= value;
                }
            }

            public bool Show
            {
                get { return _show; }
                set
                {
                    if (_show == value)
                        return;

                    _show = value;
                    PropertyChangedEventHandler handler =
                        _propertyChanged;

                    if (handler != null)
                    {
                        handler(
                            this,
                            new PropertyChangedEventArgs("Show"));
                    }
                }
            }

            public int SubscriberCount
            {
                get
                {
                    return _propertyChanged == null
                        ? 0
                        : _propertyChanged.GetInvocationList().Length;
                }
            }
        }

        private sealed class RetainingNotifySource : INotifyPropertyChanged
        {
            private PropertyChangedEventHandler _propertyChanged;

            public bool ThrowAfterAdd;
            public bool ThrowOnRemove;
            public bool DisposeRuntimeDuringAdd;
            public int AddCount;
            public int RemoveAttemptCount;
            public XamlRuntime RuntimeSeenDuringAdd;

            public event PropertyChangedEventHandler PropertyChanged
            {
                add
                {
                    AddCount++;
                    _propertyChanged += value;

                    if (DisposeRuntimeDuringAdd)
                    {
                        DisposeRuntimeDuringAdd = false;
                        object forwarder = value == null
                            ? null
                            : value.Target;
                        FieldInfo ownerField = forwarder == null
                            ? null
                            : forwarder.GetType().GetField(
                                "_owner",
                                BindingFlags.Instance |
                                BindingFlags.NonPublic);
                        RuntimeSeenDuringAdd = ownerField == null
                            ? null
                            : ownerField.GetValue(forwarder)
                                as XamlRuntime;

                        if (RuntimeSeenDuringAdd != null)
                            RuntimeSeenDuringAdd.Dispose();
                    }

                    if (ThrowAfterAdd)
                    {
                        throw new InvalidOperationException(
                            "Observable add failed after retaining its handler.");
                    }
                }
                remove
                {
                    RemoveAttemptCount++;

                    if (ThrowOnRemove)
                    {
                        throw new InvalidOperationException(
                            "Observable remove failed before detaching its handler.");
                    }

                    _propertyChanged -= value;
                }
            }

            public string Value
            {
                get { return "Observable value"; }
            }

            public int SubscriberCount
            {
                get
                {
                    return _propertyChanged == null
                        ? 0
                        : _propertyChanged.GetInvocationList().Length;
                }
            }

            public Delegate RetainedHandler
            {
                get { return _propertyChanged; }
            }

            public void RaisePropertyChanged()
            {
                PropertyChangedEventHandler handler = _propertyChanged;

                if (handler != null)
                {
                    handler(
                        this,
                        new PropertyChangedEventArgs("Value"));
                }
            }
        }

        private sealed class ObservableRebindState
        {
            public RetainingNotifySource Current;
        }

        private sealed class BindingMemberCacheProbe
        {
            public string Caption;
        }

        private sealed class CountingBindingList : IBindingList
        {
            private readonly ArrayList _items = new ArrayList();
            private ListChangedEventHandler _listChanged;
            public bool ThrowAfterAdd;
            public bool ThrowOnRemove;
            public bool ThrowAfterRemove;
            public int AddCount;
            public int RemoveAttemptCount;
            public bool HandlerWasDisabledAtRemove;

            public int SubscriberCount
            {
                get
                {
                    return _listChanged == null
                        ? 0
                        : _listChanged.GetInvocationList().Length;
                }
            }

            public event ListChangedEventHandler ListChanged
            {
                add
                {
                    AddCount++;
                    _listChanged += value;

                    if (ThrowAfterAdd)
                    {
                        throw new InvalidOperationException(
                            "ListChanged add failed after retaining its handler.");
                    }
                }
                remove
                {
                    RemoveAttemptCount++;
                    HandlerWasDisabledAtRemove =
                        IsRetainedForwarderDisabled(_listChanged);

                    if (ThrowOnRemove)
                    {
                        throw new InvalidOperationException(
                            "ListChanged remove failed before detaching.");
                    }

                    _listChanged -= value;

                    if (ThrowAfterRemove)
                    {
                        ThrowAfterRemove = false;
                        throw new InvalidOperationException(
                            "ListChanged remove failed after detaching.");
                    }
                }
            }

            public Delegate RetainedHandler
            {
                get { return _listChanged; }
            }

            public bool AllowEdit { get { return true; } }
            public bool AllowNew { get { return true; } }
            public bool AllowRemove { get { return true; } }
            public bool IsSorted { get { return false; } }
            public ListSortDirection SortDirection
            {
                get { return ListSortDirection.Ascending; }
            }
            public PropertyDescriptor SortProperty { get { return null; } }
            public bool SupportsChangeNotification { get { return true; } }
            public bool SupportsSearching { get { return false; } }
            public bool SupportsSorting { get { return false; } }
            public bool IsFixedSize { get { return false; } }
            public bool IsReadOnly { get { return false; } }
            public int Count { get { return _items.Count; } }
            public bool IsSynchronized { get { return false; } }
            public object SyncRoot { get { return _items.SyncRoot; } }

            public object this[int index]
            {
                get { return _items[index]; }
                set { _items[index] = value; }
            }

            public int Add(object value)
            {
                int index = _items.Add(value);
                RaiseListChanged(
                    new ListChangedEventArgs(
                        ListChangedType.ItemAdded,
                        index));
                return index;
            }

            public object AddNew()
            {
                object value = new object();
                Add(value);
                return value;
            }

            public void Clear()
            {
                _items.Clear();
                RaiseListChanged(
                    new ListChangedEventArgs(
                        ListChangedType.Reset,
                        -1));
            }

            public bool Contains(object value)
            {
                return _items.Contains(value);
            }

            public int IndexOf(object value)
            {
                return _items.IndexOf(value);
            }

            public void Insert(int index, object value)
            {
                _items.Insert(index, value);
                RaiseListChanged(
                    new ListChangedEventArgs(
                        ListChangedType.ItemAdded,
                        index));
            }

            public void Remove(object value)
            {
                int index = _items.IndexOf(value);

                if (index < 0)
                    return;

                _items.RemoveAt(index);
                RaiseListChanged(
                    new ListChangedEventArgs(
                        ListChangedType.ItemDeleted,
                        index));
            }

            public void RemoveAt(int index)
            {
                _items.RemoveAt(index);
                RaiseListChanged(
                    new ListChangedEventArgs(
                        ListChangedType.ItemDeleted,
                        index));
            }

            public void CopyTo(Array array, int index)
            {
                _items.CopyTo(array, index);
            }

            public IEnumerator GetEnumerator()
            {
                return _items.GetEnumerator();
            }

            public void AddIndex(PropertyDescriptor property)
            {
            }

            public void ApplySort(
                PropertyDescriptor property,
                ListSortDirection direction)
            {
                throw new NotSupportedException();
            }

            public int Find(PropertyDescriptor property, object key)
            {
                return -1;
            }

            public void RemoveIndex(PropertyDescriptor property)
            {
            }

            public void RemoveSort()
            {
            }

            private void RaiseListChanged(ListChangedEventArgs e)
            {
                ListChangedEventHandler handler = _listChanged;

                if (handler != null)
                    handler(this, e);
            }

            private static bool IsRetainedForwarderDisabled(
                Delegate retainedHandler)
            {
                if (retainedHandler == null)
                    return false;

                Delegate[] handlers = retainedHandler.GetInvocationList();
                object forwarder = handlers.Length == 0
                    ? null
                    : handlers[0].Target;

                if (forwarder == null)
                    return false;

                FieldInfo ownerField = forwarder.GetType().GetField(
                    "_owner",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                return ownerField != null &&
                    ownerField.GetValue(forwarder) == null;
            }
        }

        public static void Run()
        {
            TestPropertyBindingUsesInternalRuntimeContract();
            TestBindingMemberCacheUsesTypeBuckets();
            TestObservableSourceForwardersReleaseRuntimeGraphs();
            TestObservableSourceAddReentryIsDeferredAndInert();
            TestObservableDispatchIndexesPendingRegistrations();
            TestSynchronousDispatchDropsDetachedPendingRegistrations();
            TestObservableTargetForwarderReleasesRuntimeGraph();
            TestDynamicTargetDisposalAddFailureIsInert();
            TestDynamicTargetDisposalAddReentryDoesNotPublish();
            TestDynamicTargetDisposalIsInertBeforeSourceCleanup();
            TestDynamicTargetDisposalRetryDoesNotBlockReplacement();
            TestFormStartPositionIsCanonical();
            TestFormResizePropertiesAreCanonical();
            TestLayoutBindingInvalidatesOnlyItsAffectedSubtree();
            TestUnchangedBindingSkipsSetterAndRebindsDependencies();
            TestNotifySourceUsesExactPropertyBuckets();
            TestScopedLayoutSkipsUnrelatedBranch();
            TestAttachedBindingReloadUsesStableElementMetadata();
            TestAggregateDependenciesUseSourceIndex();
            TestFunctionArgumentsObserveCurrentContext();
            TestFunctionParameterMetadataIsCached();
            TestFunctionInvocationPlansAreSafeAndReleased();
            TestLegacyFunctionBindingSyntaxIsRejected();
            TestRemovedItemTemplateAliasesAreRejected();
            TestRemovedPackageAliasesAreRejected();
            TestPresetImportModeIsCanonical();
            TestIndexedAggregateReconciliationKeepsSourceSubscriptions();
            TestInvalidTwoWayEditCanRecover();
            TestExactPropertyBeatsMarkupFallback();
            TestExactStyleAndResourceStyleRemainIndependent();
            TestNullItemDoesNotUseOuterCodeBehind();
            TestItemSourceForwardersReleaseRuntimeGraphs();
            TestItemSourceReplacementRemoveFailureCanRetry();
            TestRuntimeDisposeDetachesItemSource();
            TestDynamicCleanupContinuesAfterItemSourceFailure();
        }

        private static void TestLayoutBindingInvalidatesOnlyItsAffectedSubtree()
        {
            AuditState state = new AuditState();
            XamlRuntime runtime = XamlRuntime.Load(
                "<Panel Size='300,100'>" +
                " <Label Name='Bound' Location='0,0' Size='100,20' " +
                "   Text='{Binding Header}' />" +
                " <Label Name='Unrelated' Location='0,30' Size='100,20' " +
                "   Text='Stable' />" +
                "</Panel>",
                state);
            Label unrelated = null;
            int unrelatedInvalidations = 0;
            InvalidateEventHandler invalidated =
                delegate(object sender, InvalidateEventArgs e)
                {
                    unrelatedInvalidations++;
                };

            try
            {
                runtime.RootControl.CreateControl();
                unrelated = runtime.Get<Label>("Unrelated");
                unrelated.Invalidated += invalidated;

                state.Header.Value = "Updated header";
                Application.DoEvents();

                AssertEqual(
                    0,
                    unrelatedInvalidations,
                    "a layout-affecting binding does not recursively invalidate " +
                    "an unchanged sibling subtree");
                AssertEqual(
                    "Updated header",
                    runtime.Get<Label>("Bound").Text,
                    "the targeted layout binding refreshes on the owner dispatch path");
            }
            finally
            {
                if (unrelated != null)
                    unrelated.Invalidated -= invalidated;

                runtime.Dispose();
            }
        }

        private static void
            TestUnchangedBindingSkipsSetterAndRebindsDependencies()
        {
            NotifyBranchState state = new NotifyBranchState();
            XamlRuntime runtime = XamlRuntime.Load(
                "<Label Name='Target' " +
                "Text='{Binding Branch.Text}' />",
                state);
            int textChangedCount = 0;
            EventHandler changed =
                delegate { textChangedCount++; };

            try
            {
                Label target = runtime.Get<Label>("Target");
                runtime.RootControl.CreateControl();
                target.TextChanged += changed;
                AssertEqual(
                    "Same",
                    target.Text,
                    "the initial nested binding assigns its value");

                runtime.ReloadBinding("Target", "Text");
                AssertEqual(
                    0,
                    textChangedCount,
                    "an unchanged explicit reload leaves the native target alone");

                NotifyBranch replacement = new NotifyBranch("Same");
                state.Branch = replacement;
                DrainCallbacks();
                AssertEqual(
                    0,
                    textChangedCount,
                    "same terminal value is quiet after branch replacement");

                replacement.Text = "Changed";
                DrainCallbacks();
                AssertEqual(
                    1,
                    textChangedCount,
                    "the replacement branch remains reactively subscribed");
                AssertEqual(
                    "Changed",
                    target.Text,
                    "the replacement branch publishes its later value");

                target.Text = "External";
                replacement.RaiseText();
                DrainCallbacks();
                AssertEqual(
                    "Changed",
                    target.Text,
                    "a same source value still repairs an externally changed target");
            }
            finally
            {
                Label target = runtime.Get<Label>("Target");
                target.TextChanged -= changed;
                runtime.Dispose();
            }
        }

        private static void TestNotifySourceUsesExactPropertyBuckets()
        {
            AggregateNotifyState state = new AggregateNotifyState();
            XamlRuntime runtime = XamlRuntime.Load(
                "<Panel>" +
                " <Label Text='{Binding First}' />" +
                " <Label Text='{Binding Second}' />" +
                "</Panel>",
                state);

            try
            {
                IDictionary subscriptions = GetRuntimeDictionary(
                    runtime,
                    "_observableSourceSubscriptions");
                object subscription = subscriptions[state];
                AssertTrue(
                    subscription != null,
                    "the notifying source has one shared subscription");
                FieldInfo bucketsField = subscription.GetType().GetField(
                    "NotifyDependentsByProperty",
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);
                AssertTrue(
                    bucketsField != null,
                    "the source subscription exposes its exact-property index");
                IDictionary buckets =
                    bucketsField.GetValue(subscription) as IDictionary;
                AssertTrue(
                    buckets != null &&
                    buckets.Contains("First") &&
                    buckets.Contains("Second"),
                    "each observed property has a dedicated dependent bucket");

                runtime.RootControl.CreateControl();
                state.Raise("First");
                AssertEqual(
                    1,
                    GetRuntimeInt(
                        runtime,
                        "_observablePendingRegistrationCount"),
                    "one exact property notification queues only its dependent");
                DrainCallbacks();

                state.Raise(null);
                AssertEqual(
                    2,
                    GetRuntimeInt(
                        runtime,
                        "_observablePendingRegistrationCount"),
                    "an empty property notification queues all source dependents");
                DrainCallbacks();
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestScopedLayoutSkipsUnrelatedBranch()
        {
            AuditState state = new AuditState();
            XamlRuntime runtime = XamlRuntime.Load(
                "<Panel Size='320,120'>" +
                " <Panel Name='Affected' " +
                "     Location='0,0' Size='150,100'>" +
                "   <Label Text='{Binding Header}' />" +
                " </Panel>" +
                " <Panel Name='Unrelated' " +
                "     Location='160,0' Size='150,100'>" +
                "   <Label Text='Stable' />" +
                " </Panel>" +
                "</Panel>",
                state);
            int affectedLayouts = 0;
            int unrelatedLayouts = 0;
            LayoutEventHandler affectedLayout =
                delegate { affectedLayouts++; };
            LayoutEventHandler unrelatedLayout =
                delegate { unrelatedLayouts++; };

            try
            {
                Panel affected = runtime.Get<Panel>("Affected");
                Panel unrelated = runtime.Get<Panel>("Unrelated");
                runtime.RootControl.CreateControl();
                affected.Layout += affectedLayout;
                unrelated.Layout += unrelatedLayout;

                runtime.ReloadBindings();
                AssertEqual(
                    0,
                    affectedLayouts,
                    "an unchanged native binding avoids explicit layout work");
                AssertEqual(
                    0,
                    unrelatedLayouts,
                    "an unchanged reload remains quiet outside the target branch");

                state.Header.Value = "A longer changed heading";
                DrainCallbacks();

                AssertTrue(
                    affectedLayouts > 0,
                    "the changed binding lays out its own ancestor branch");
                AssertEqual(
                    0,
                    unrelatedLayouts,
                    "scoped dynamic layout does not recurse into an unrelated branch");
            }
            finally
            {
                Panel affected = runtime.Get<Panel>("Affected");
                Panel unrelated = runtime.Get<Panel>("Unrelated");
                affected.Layout -= affectedLayout;
                unrelated.Layout -= unrelatedLayout;
                runtime.Dispose();
            }
        }

        private static void
            TestAttachedBindingReloadUsesStableElementMetadata()
        {
            AttachedBindingState state = new AttachedBindingState();
            XamlRuntime runtime = XamlRuntime.Load(
                "<Grid Width='100' Height='100'>" +
                "  <Grid.RowDefinitions>" +
                "    <RowDefinition Height='40' />" +
                "    <RowDefinition Height='*' />" +
                "  </Grid.RowDefinitions>" +
                "  <Label Name='Target' Grid.Row='{Binding Row}' />" +
                "  <Label Name='DiagnosticTarget' " +
                "         Grid.Row='{Binding DiagnosticRow}' />" +
                "</Grid>",
                state);

            try
            {
                Label target = runtime.Get<Label>("Target");
                IDictionary infos = GetRuntimeDictionary(
                    runtime,
                    "_elementInfos");
                object info = infos[target];

                AssertTrue(
                    info != null,
                    "the attached binding target has retained element metadata");

                MethodInfo directOverload = typeof(XamlRuntime).GetMethod(
                    "ApplyAttachedProperty",
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new Type[]
                    {
                        info.GetType(),
                        typeof(object),
                        typeof(string),
                        typeof(string)
                    },
                    null);
                FieldInfo rowField = info.GetType().GetField(
                    "GridRow",
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);

                AssertTrue(
                    directOverload != null,
                    "attached reloads have a direct name/value path");
                AssertTrue(
                    rowField != null,
                    "attached row metadata remains inspectable");

                runtime.RootControl.CreateControl();
                int i;

                for (i = 1; i <= 20; i++)
                {
                    state.Row.Value = i & 1;
                    DrainCallbacks();

                    AssertTrue(
                        Object.ReferenceEquals(info, infos[target]),
                        "attached reload reuses the target ElementInfo");
                    AssertEqual(
                        i & 1,
                        rowField.GetValue(info),
                        "attached reload applies the current value directly");
                }

                state.DiagnosticRow = "not-a-row";
                Exception failure = null;

                try
                {
                    runtime.ReloadBinding(
                        "DiagnosticTarget",
                        "Grid.Row");
                }
                catch (Exception ex)
                {
                    failure = ex;
                }

                AssertTrue(
                    failure is WinFormsXamlLoadException &&
                    failure.Message.IndexOf(
                        "Grid.Row",
                        StringComparison.OrdinalIgnoreCase) >= 0,
                    "the direct attached reload retains located diagnostics");

                state.DiagnosticRow = "1";
                runtime.ReloadBinding(
                    "DiagnosticTarget",
                    "Grid.Row");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestFunctionArgumentsObserveCurrentContext()
        {
            AggregateNotifyState state = new AggregateNotifyState();
            XamlRuntime runtime = XamlRuntime.Load(
                "<Label Name='Summary' " +
                "Text='{Function Format(First, Second)}' />",
                state);

            try
            {
                Label summary = runtime.Get<Label>("Summary");
                runtime.RootControl.CreateControl();

                AssertEqual(
                    "First / Second",
                    summary.Text,
                    "function arguments resolve against the Form context");

                state.First = "Updated";
                DrainCallbacks();

                AssertEqual(
                    "Updated / Second",
                    summary.Text,
                    "the first explicit function path refreshes automatically");

                state.Second = "Changed";
                DrainCallbacks();

                AssertEqual(
                    "Updated / Changed",
                    summary.Text,
                    "every explicit function path is observed");
            }
            finally
            {
                runtime.Dispose();
            }

            AssertEqual(
                0,
                state.SubscriberCount,
                "disposing a function binding detaches its Form dependencies");

            AuditState codeBehind = new AuditState();
            AggregateNotifyState item = new AggregateNotifyState();
            runtime = XamlRuntime.Load(
                "<ItemsControl Name='Rows' Virtualizing='false' " +
                "    ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label Text='{Function FormatItem(First)}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>",
                codeBehind);

            try
            {
                XamlRuntime.ItemsControl rows =
                    runtime.GetItemsControl("Rows");
                runtime.RootControl.CreateControl();
                rows.ItemsSource = new object[] { item };

                Label itemLabel = rows.Controls[0] as Label;
                AssertTrue(itemLabel != null, "function item template is rendered");
                AssertEqual(
                    "Item: First",
                    itemLabel.Text,
                    "function arguments resolve against the current item");

                item.First = "Current item";
                DrainCallbacks();

                AssertEqual(
                    "Item: Current item",
                    itemLabel.Text,
                    "current-item function paths refresh automatically");
            }
            finally
            {
                runtime.Dispose();
            }

            AssertEqual(
                0,
                item.SubscriberCount,
                "disposing an item function detaches current-context dependencies");
        }

        private static void TestFunctionParameterMetadataIsCached()
        {
            AggregateNotifyState state = new AggregateNotifyState();
            XamlRuntime runtime = XamlRuntime.Load(
                "<Label Name='Summary' " +
                "Text='{Function Format(First, Second)}' />",
                state);
            FieldInfo cacheField = typeof(XamlRuntime).GetField(
                "_bindingFunctionParametersCache",
                BindingFlags.Instance | BindingFlags.NonPublic);

            try
            {
                AssertTrue(
                    cacheField != null,
                    "function parameter cache is available");

                Hashtable cache =
                    cacheField.GetValue(runtime) as Hashtable;
                AssertTrue(
                    cache != null && cache.Count > 0,
                    "initial function evaluation caches parameter metadata");

                object cachedParameters = null;

                foreach (DictionaryEntry entry in cache)
                {
                    cachedParameters = entry.Value;
                    break;
                }

                runtime.ReloadBinding("Summary", "Text");

                bool retained = false;

                foreach (DictionaryEntry entry in cache)
                {
                    if (Object.ReferenceEquals(
                            cachedParameters,
                            entry.Value))
                    {
                        retained = true;
                        break;
                    }
                }

                AssertTrue(
                    retained,
                    "repeated function evaluation reuses reflected parameters");
            }
            finally
            {
                runtime.Dispose();
            }

            AssertTrue(
                cacheField.GetValue(runtime) == null,
                "disposing the runtime releases cached reflected parameters");
        }

        private static void TestFunctionInvocationPlansAreSafeAndReleased()
        {
            FieldInfo plansField = typeof(XamlRuntime).GetField(
                "_bindingFunctionInvocationPlans",
                BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo countField = typeof(XamlRuntime).GetField(
                "_bindingFunctionInvocationPlanCount",
                BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo hitField = typeof(XamlRuntime).GetField(
                "_bindingFunctionInvocationPlanHitCount",
                BindingFlags.Instance | BindingFlags.NonPublic);

            AssertTrue(
                plansField != null && countField != null && hitField != null,
                "function invocation plan diagnostics are available");

            FunctionPlanState state = new FunctionPlanState("first");
            XamlRuntime runtime = XamlRuntime.Load(
                "<Label Name='Direct' " +
                "Text='{Function Echo(Value)}' />",
                state);

            try
            {
                AssertEqual(
                    "echo:first",
                    runtime.Get<Label>("Direct").Text,
                    "the direct Function overload remains unchanged");
                AssertEqual(
                    1,
                    countField.GetValue(runtime),
                    "a value-independent exact overload is planned once");

                long hitsBefore = Convert.ToInt64(
                    hitField.GetValue(runtime));
                state.Value = "second";
                runtime.ReloadBinding("Direct", "Text");

                AssertEqual(
                    "echo:second",
                    runtime.Get<Label>("Direct").Text,
                    "a planned Function still receives the current value");
                AssertTrue(
                    Convert.ToInt64(hitField.GetValue(runtime)) > hitsBefore,
                    "repeated exact Function evaluation uses its plan");
            }
            finally
            {
                runtime.Dispose();
            }

            AssertTrue(
                plansField.GetValue(runtime) == null,
                "disposing the runtime releases Function invocation plans");

            state = new FunctionPlanState("12");
            runtime = XamlRuntime.Load(
                "<Label Name='Converted' " +
                "Text='{Function ParseNumber(Value)}' />",
                state);

            try
            {
                AssertEqual(
                    "number:12",
                    runtime.Get<Label>("Converted").Text,
                    "value-sensitive Function conversion still works");
                AssertEqual(
                    0,
                    countField.GetValue(runtime),
                    "a string-to-number overload is not cached by CLR type");

                state.Value = "27";
                runtime.ReloadBinding("Converted", "Text");

                AssertEqual(
                    "number:27",
                    runtime.Get<Label>("Converted").Text,
                    "value-sensitive conversion is recalculated every time");
                AssertEqual(
                    0,
                    countField.GetValue(runtime),
                    "repeated value-sensitive conversion remains unplanned");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestLegacyFunctionBindingSyntaxIsRejected()
        {
            AggregateNotifyState state = new AggregateNotifyState();

            AssertMarkupLoadFails(
                "<Label Text='{Binding Format(First)}' />",
                state,
                "function calls require the Function expression name");
            AssertMarkupLoadFails(
                "<Label Text='{Binding Function=Format, Argument=First}' />",
                state,
                "Function and Argument binding options are not aliases");
        }

        private static void TestRemovedItemTemplateAliasesAreRejected()
        {
            AssertMarkupLoadFails(
                "<ItemsControl><Template><Label /></Template></ItemsControl>",
                null,
                "Template elements are not item-template aliases");
            AssertMarkupLoadFails(
                "<ItemsControl><DataTemplate><Label /></DataTemplate></ItemsControl>",
                null,
                "DataTemplate elements are not item-template aliases");
            AssertMarkupLoadFails(
                "<ItemsControl><ItemsControl.Template>" +
                "<Label /></ItemsControl.Template></ItemsControl>",
                null,
                "ItemsControl.Template is not an ItemTemplate alias");
            AssertMarkupLoadFails(
                "<ItemsControl><ItemsControl.ItemTemplate>" +
                "<DataTemplate><Label /></DataTemplate>" +
                "</ItemsControl.ItemTemplate></ItemsControl>",
                null,
                "ItemTemplate does not accept DataTemplate wrappers");
        }

        private static void TestRemovedPackageAliasesAreRejected()
        {
            AssertMarkupLoadFails(
                "<WrapPanel />",
                null,
                "WrapPanel is not an alias for FlexPanel");
            AssertMarkupLoadFails(
                "<FlexPanel><TextBox FlexPanel.FlexGrow='1' /></FlexPanel>",
                null,
                "FlexPanel.FlexGrow is not an alias for FlexGrow");
            AssertMarkupLoadFails(
                "<Label TextColor='Red' />",
                null,
                "TextColor is not an alias for ForeColor");
            AssertMarkupLoadFails(
                "<Label Color='Red' />",
                null,
                "Color is not an alias for ForeColor");
            AssertMarkupLoadFails(
                "<Label BackgroundColor='Red' />",
                null,
                "BackgroundColor is not an alias for BackColor");
            AssertMarkupLoadFails(
                "<Border BorderColor='Red' />",
                null,
                "BorderColor is not an alias for BorderBrush");
            AssertMarkupLoadFails(
                "<Form><Form.Presets><Presets Name='Theme' Selected='Light'>" +
                "<Preset Name='Light'><Set Key='Surface' Value='White' />" +
                "</Preset></Presets></Form.Presets></Form>",
                null,
                "property-element preset wrappers are rejected");
        }

        private static void TestPresetImportModeIsCanonical()
        {
            Type managerType = typeof(PresetManager);

            AssertTrue(
                managerType.GetMethod(
                    "LoadXml",
                    new Type[]
                    {
                        typeof(string),
                        typeof(PresetImportMode)
                    }) != null,
                "LoadXml exposes PresetImportMode");
            AssertTrue(
                managerType.GetMethod(
                    "LoadFile",
                    new Type[]
                    {
                        typeof(string),
                        typeof(PresetImportMode)
                    }) != null,
                "LoadFile exposes PresetImportMode");
            AssertTrue(
                managerType.GetMethod(
                    "LoadEmbeddedResource",
                    new Type[]
                    {
                        typeof(Assembly),
                        typeof(string),
                        typeof(PresetImportMode)
                    }) != null,
                "LoadEmbeddedResource exposes PresetImportMode");
            AssertTrue(
                managerType.GetMethod(
                    "LoadXml",
                    new Type[] { typeof(string), typeof(bool) }) == null,
                "LoadXml has no boolean import-mode overload");
            AssertTrue(
                managerType.GetMethod(
                    "LoadFile",
                    new Type[] { typeof(string), typeof(bool) }) == null,
                "LoadFile has no boolean import-mode overload");
            AssertTrue(
                managerType.GetMethod(
                    "LoadEmbeddedResource",
                    new Type[]
                    {
                        typeof(Assembly),
                        typeof(string),
                        typeof(bool)
                    }) == null,
                "LoadEmbeddedResource has no boolean import-mode overload");
        }

        private static void TestFormResizePropertiesAreCanonical()
        {
            AssertMarkupLoadFails(
                "<Form ResizeMode='NoResize' />",
                null,
                "ResizeMode is not an alias for native Form resize properties");

            XamlRuntime runtime = XamlRuntime.Load(
                "<Form FormBorderStyle='FixedDialog' " +
                "MaximizeBox='false' MinimizeBox='false' />");

            try
            {
                AssertEqual(
                    FormBorderStyle.FixedDialog,
                    runtime.Form.FormBorderStyle,
                    "native FormBorderStyle remains writable");
                AssertTrue(
                    !runtime.Form.MaximizeBox,
                    "native MaximizeBox remains independent");
                AssertTrue(
                    !runtime.Form.MinimizeBox,
                    "native MinimizeBox remains independent");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void AssertMarkupLoadFails(
            string markup,
            object eventTarget,
            string message)
        {
            XamlRuntime runtime = null;
            bool rejected = false;

            try
            {
                runtime = XamlRuntime.Load(markup, eventTarget);
            }
            catch (WinFormsXamlLoadException)
            {
                rejected = true;
            }
            finally
            {
                if (runtime != null)
                    runtime.Dispose();
            }

            AssertTrue(rejected, message);
        }

        private static void TestPropertyBindingUsesInternalRuntimeContract()
        {
            Type bindingType = typeof(PropertyBinding<int>);
            Type runtimeContract = bindingType.Assembly.GetType(
                "WinFormsXaml.IPropertyBindingRuntime",
                false);

            AssertTrue(
                runtimeContract != null && runtimeContract.IsInterface,
                "PropertyBinding has an internal runtime contract");
            AssertTrue(
                !runtimeContract.IsPublic &&
                runtimeContract.IsAssignableFrom(bindingType),
                "PropertyBinding implements its non-public runtime contract");
            AssertTrue(
                bindingType.GetProperty(
                    "ValueType",
                    BindingFlags.Instance | BindingFlags.Public) == null &&
                bindingType.GetMethod(
                    "SetValue",
                    BindingFlags.Instance | BindingFlags.Public) == null,
                "the runtime contract does not expand the public API");
            AssertTrue(
                typeof(XamlRuntime).GetNestedType(
                    "PropertyBindingAccessor",
                    BindingFlags.NonPublic) == null &&
                typeof(XamlRuntime).GetField(
                    "_propertyBindingAccessorCache",
                    BindingFlags.Static | BindingFlags.NonPublic) == null,
                "PropertyBinding dispatch no longer retains reflection metadata");
        }

        private static void TestBindingMemberCacheUsesTypeBuckets()
        {
            BindingMemberCacheProbe state =
                new BindingMemberCacheProbe();
            state.Caption = "Cached caption";
            XamlRuntime runtime = XamlRuntime.Load(
                "<Panel>" +
                "  <Label Name='Lower' Text='{Binding caption}' />" +
                "  <Label Name='Upper' Text='{Binding CAPTION}' />" +
                "</Panel>",
                state);

            try
            {
                AssertEqual(
                    "Cached caption",
                    runtime.Get<Label>("Lower").Text,
                    "lower-case member binding resolves");
                AssertEqual(
                    "Cached caption",
                    runtime.Get<Label>("Upper").Text,
                    "upper-case member binding resolves");

                FieldInfo cacheField = typeof(XamlRuntime).GetField(
                    "_bindingMemberLookupCache",
                    BindingFlags.Static | BindingFlags.NonPublic);
                AssertTrue(
                    cacheField != null,
                    "binding member cache is available internally");
                IDictionary cache =
                    cacheField.GetValue(null) as IDictionary;
                AssertTrue(
                    cache != null,
                    "binding member cache uses dictionary storage");
                IDictionary members =
                    cache[typeof(BindingMemberCacheProbe)] as IDictionary;
                AssertTrue(
                    members != null,
                    "binding member cache has a per-Type bucket");
                AssertEqual(
                    1,
                    members.Count,
                    "case variants share one member lookup entry");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void
            TestObservableSourceForwardersReleaseRuntimeGraphs()
        {
            const string markup =
                "<Panel>" +
                "  <Label Text='{Binding Value}' />" +
                "  <Label Text='{Binding Value}' />" +
                "</Panel>";
            RetainingNotifySource addFailure =
                new RetainingNotifySource();
            addFailure.ThrowAfterAdd = true;
            addFailure.ThrowOnRemove = true;
            Exception surfaced = null;

            try
            {
                XamlRuntime failed = XamlRuntime.Load(
                    markup,
                    addFailure);
                failed.Dispose();
            }
            catch (Exception ex)
            {
                surfaced = ex;
            }

            AssertTrue(
                ContainsExceptionMessage(
                    surfaced,
                    "Observable add failed after retaining its handler."),
                "the original observable add failure is preserved");
            AssertEqual(
                1,
                addFailure.AddCount,
                "a failed observable source is attached once");
            AssertEqual(
                1,
                addFailure.RemoveAttemptCount,
                "a failed observable add receives one cleanup attempt");
            AssertEqual(
                1,
                addFailure.SubscriberCount,
                "the hostile source retained its failed handler");
            AssertObservableForwarderDisabled(
                addFailure.RetainedHandler,
                "_subscription",
                "failed observable add forwarder");
            addFailure.RaisePropertyChanged();

            RetainingNotifySource removeFailure =
                new RetainingNotifySource();
            removeFailure.ThrowOnRemove = true;
            XamlRuntime removeFailureRuntime =
                XamlRuntime.Load(markup, removeFailure);
            AssertEqual(
                1,
                removeFailure.AddCount,
                "pooled notifying bindings install one source handler");
            AssertEqual(
                1,
                removeFailure.SubscriberCount,
                "pooled notifying bindings share one source handler");

            removeFailureRuntime.Dispose();

            AssertEqual(
                1,
                removeFailure.RemoveAttemptCount,
                "the pooled source receives one final removal attempt");
            AssertEqual(
                1,
                removeFailure.SubscriberCount,
                "the hostile source retained its disposal handler");
            AssertObservableForwarderDisabled(
                removeFailure.RetainedHandler,
                "_subscription",
                "failed observable remove forwarder");
            removeFailure.RaisePropertyChanged();

            RetainingNotifySource normal =
                new RetainingNotifySource();
            XamlRuntime normalRuntime =
                XamlRuntime.Load(markup, normal);
            normalRuntime.Dispose();
            AssertEqual(
                1,
                normal.AddCount,
                "normal pooled source attach count");
            AssertEqual(
                1,
                normal.RemoveAttemptCount,
                "normal pooled source remove count");
            AssertEqual(
                0,
                normal.SubscriberCount,
                "normal pooled source is detached");

            PropertyBinding<string> propertyBinding =
                new PropertyBinding<string>("PropertyBinding value");
            XamlRuntime propertyRuntime = XamlRuntime.Load(
                "<Label Text='{Binding Value}' />",
                propertyBinding);
            FieldInfo handlersField =
                typeof(PropertyBinding<string>).GetField(
                    "_valueChanged",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo subscriberSnapshotField =
                typeof(PropertyBinding<string>).GetField(
                    "_valueChangedSubscribers",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            AssertTrue(
                handlersField != null && subscriberSnapshotField != null,
                "PropertyBinding handler storage is available internally");
            EventHandler retainedPropertyHandler =
                handlersField.GetValue(propertyBinding) as EventHandler;
            AssertTrue(
                retainedPropertyHandler != null,
                "PropertyBinding uses an observable source handler");

            propertyRuntime.Dispose();

            AssertEqual(
                null,
                handlersField.GetValue(propertyBinding),
                "PropertyBinding removes its observable source handler");
            AssertEqual(
                null,
                subscriberSnapshotField.GetValue(propertyBinding),
                "PropertyBinding releases its cached subscriber snapshot");
            AssertObservableForwarderDisabled(
                retainedPropertyHandler,
                "_subscription",
                "PropertyBinding source forwarder");
        }

        private static void
            TestObservableSourceAddReentryIsDeferredAndInert()
        {
            RetainingNotifySource initial =
                new RetainingNotifySource();
            initial.DisposeRuntimeDuringAdd = true;
            initial.ThrowOnRemove = true;
            XamlRuntime initialResult = null;

            try
            {
                try
                {
                    initialResult = XamlRuntime.Load(
                        "<Label Text='{Binding Value}' />",
                        initial);
                }
                catch
                {
                    // Disposal is deliberately reentrant with construction. The
                    // ownership assertions below are the required contract.
                }

                XamlRuntime initialRuntime =
                    initial.RuntimeSeenDuringAdd;
                AssertTrue(
                    initialRuntime != null && initialRuntime.IsDisposed,
                    "initial source add can dispose its in-flight runtime");
                AssertEqual(
                    1,
                    initial.AddCount,
                    "initial reentrant source is attached once");
                AssertEqual(
                    1,
                    initial.RemoveAttemptCount,
                    "initial reentrant detach is deferred until add returns");
                AssertEqual(
                    1,
                    initial.SubscriberCount,
                    "hostile deferred remove retains only an inert handler");
                AssertObservableForwarderDisabled(
                    initial.RetainedHandler,
                    "_subscription",
                    "initial reentrant source add forwarder");
                initial.RaisePropertyChanged();
            }
            finally
            {
                if (initialResult != null)
                    initialResult.Dispose();
            }

            RetainingNotifySource previous =
                new RetainingNotifySource();
            ObservableRebindState state =
                new ObservableRebindState();
            state.Current = previous;
            XamlRuntime runtime = XamlRuntime.Load(
                "<Label Name='Value' " +
                "Text='{Binding Current.Value}' />",
                state);
            RetainingNotifySource replacement =
                new RetainingNotifySource();
            replacement.DisposeRuntimeDuringAdd = true;
            replacement.ThrowOnRemove = true;

            try
            {
                state.Current = replacement;

                try
                {
                    runtime.ReloadBinding("Value", "Text");
                }
                catch
                {
                    // The runtime was intentionally disposed from the new
                    // source's add accessor; no stale attachment may survive.
                }

                AssertTrue(
                    Object.ReferenceEquals(
                        runtime,
                        replacement.RuntimeSeenDuringAdd),
                    "update source add observes the existing runtime");
                AssertTrue(
                    runtime.IsDisposed,
                    "update source add can dispose the runtime");
                AssertEqual(
                    0,
                    previous.SubscriberCount,
                    "update reentry detaches the committed old source");
                AssertEqual(
                    1,
                    replacement.AddCount,
                    "update reentrant source is attached once");
                AssertEqual(
                    1,
                    replacement.RemoveAttemptCount,
                    "update reentrant detach is deferred until add returns");
                AssertEqual(
                    1,
                    replacement.SubscriberCount,
                    "hostile update remove retains only an inert handler");
                AssertObservableForwarderDisabled(
                    replacement.RetainedHandler,
                    "_subscription",
                    "update reentrant source add forwarder");
                replacement.RaisePropertyChanged();
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void
            TestObservableDispatchIndexesPendingRegistrations()
        {
            AuditState state = new AuditState();
            XamlRuntime runtime = XamlRuntime.Load(
                "<Panel>" +
                "  <Label Text='{Binding Header}' />" +
                "  <Label Text='{Binding OuterText}' />" +
                "</Panel>",
                state);

            try
            {
                AssertEqual(
                    0,
                    GetRuntimeInt(
                        runtime,
                        "_observablePendingRegistrationCount"),
                    "reactive dispatch starts without pending registrations");

                state.Header.Value = "Header pending";
                state.Header.Value = "Header newest";

                AssertEqual(
                    1,
                    GetRuntimeInt(
                        runtime,
                        "_observablePendingRegistrationCount"),
                    "repeated source signals index one pending registration");
                AssertEqual(
                    1,
                    GetRuntimeCollectionCount(
                        runtime,
                        "_observablePendingRegistrations"),
                    "pending dispatch queue contains only the changed binding");

                state.OuterText.Value = "Outer pending";

                AssertEqual(
                    2,
                    GetRuntimeInt(
                        runtime,
                        "_observablePendingRegistrationCount"),
                    "a second changed binding adds one indexed registration");
                AssertEqual(
                    2,
                    GetRuntimeCollectionCount(
                        runtime,
                        "_observablePendingRegistrations"),
                    "pending dispatch avoids scanning unchanged registrations");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void
            TestSynchronousDispatchDropsDetachedPendingRegistrations()
        {
            AuditState state = new AuditState();
            XamlRuntime runtime = XamlRuntime.Load(
                "<Panel>" +
                "  <Label Name='Pending' Text='{Binding Header}' />" +
                "  <CheckBox Name='Editor' " +
                "      Checked='{Binding Checked, Mode=TwoWay}' />" +
                "</Panel>",
                state);

            try
            {
                Label pending = runtime.Get<Label>("Pending");
                CheckBox editor = runtime.Get<CheckBox>("Editor");
                runtime.RootControl.CreateControl();
                Application.DoEvents();

                state.Header.Value = "Detached pending source";
                AssertEqual(
                    1,
                    GetRuntimeCollectionCount(
                        runtime,
                        "_observablePendingRegistrations"),
                    "source update is retained until owner-thread dispatch");

                pending.Dispose();
                AssertEqual(
                    0,
                    GetRuntimeInt(
                        runtime,
                        "_observablePendingRegistrationCount"),
                    "detaching the pending binding releases indexed debt");

                editor.Checked = true;

                AssertEqual(
                    true,
                    state.Checked.Value,
                    "owner-thread target edit still commits synchronously");
                AssertEqual(
                    0,
                    GetRuntimeCollectionCount(
                        runtime,
                        "_observablePendingRegistrations"),
                    "synchronous dispatch discards a detached queued entry");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void
            TestObservableTargetForwarderReleasesRuntimeGraph()
        {
            AuditState state = new AuditState();
            RetainingValueControl.ThrowAfterAdd = true;
            RetainingValueControl.ThrowOnRemove = true;
            RetainingValueControl.LastInstance = null;
            XamlRuntime runtime = null;

            try
            {
                Exception surfaced = null;

                try
                {
                    runtime = XamlRuntime.Load(
                        "<RetainingValueControl " +
                        "    Value='{Binding Header, Mode=TwoWay}' />",
                        state);
                }
                catch (Exception ex)
                {
                    surfaced = ex;
                    runtime = null;
                }

                AssertTrue(
                    ContainsExceptionMessage(
                        surfaced,
                        "Target add failed after retaining its handler."),
                    "the original target add failure is preserved");
                RetainingValueControl failedTarget =
                    RetainingValueControl.LastInstance;
                AssertTrue(
                    failedTarget != null,
                    "the failed two-way target is created");
                AssertEqual(
                    1,
                    failedTarget.AddCount,
                    "the failed two-way target receives one handler");
                AssertEqual(
                    1,
                    failedTarget.RemoveAttemptCount,
                    "the failed target add receives one cleanup attempt");
                AssertEqual(
                    1,
                    failedTarget.SubscriberCount,
                    "the hostile target retained its failed handler");
                AssertObservableForwarderDisabled(
                    failedTarget.RetainedHandler,
                    "_registration",
                    "failed observable target add forwarder");
                failedTarget.RaiseValueChanged();

                RetainingValueControl.ThrowAfterAdd = false;
                RetainingValueControl.LastInstance = null;
                runtime = XamlRuntime.Load(
                    "<RetainingValueControl " +
                    "    Value='{Binding Header, Mode=TwoWay}' />",
                    state);
                RetainingValueControl target =
                    RetainingValueControl.LastInstance;
                AssertTrue(
                    target != null,
                    "the hostile two-way target is created");
                AssertEqual(
                    1,
                    target.AddCount,
                    "the two-way target receives one handler");
                AssertEqual(
                    1,
                    target.SubscriberCount,
                    "the two-way target retains one handler");

                runtime.Dispose();
                runtime = null;

                AssertEqual(
                    1,
                    target.RemoveAttemptCount,
                    "the two-way target receives one removal attempt");
                AssertEqual(
                    1,
                    target.SubscriberCount,
                    "the hostile target retained its disposal handler");
                AssertObservableForwarderDisabled(
                    target.RetainedHandler,
                    "_registration",
                    "failed observable target remove forwarder");
                target.RaiseValueChanged();
            }
            finally
            {
                if (runtime != null)
                    runtime.Dispose();

                RetainingValueControl.ThrowAfterAdd = false;
                RetainingValueControl.ThrowOnRemove = false;
                RetainingValueControl.LastInstance = null;
            }
        }

        private static void AssertObservableForwarderDisabled(
            Delegate retainedHandler,
            string retainedFieldName,
            string message)
        {
            AssertTrue(
                retainedHandler != null,
                message + " is retained for inspection");

            Delegate[] handlers = retainedHandler.GetInvocationList();
            object forwarder = handlers.Length == 0
                ? null
                : handlers[0].Target;
            AssertTrue(
                forwarder != null,
                message + " has an owned target");

            Type forwarderType = forwarder.GetType();
            FieldInfo ownerField = forwarderType.GetField(
                "_owner",
                BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo retainedField = forwarderType.GetField(
                retainedFieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            AssertTrue(
                ownerField != null && retainedField != null,
                message + " exposes inert retention state internally");
            AssertEqual(
                null,
                ownerField.GetValue(forwarder),
                message + " releases its runtime");
            AssertEqual(
                null,
                retainedField.GetValue(forwarder),
                message + " releases its retained graph");
        }

        private static void TestDynamicTargetDisposalAddFailureIsInert()
        {
            AggregateNotifyState state = new AggregateNotifyState();
            RetainingDisposedComponent.ThrowAfterAdd = true;
            RetainingDisposedComponent.ThrowOnRemove = true;
            RetainingDisposedComponent.RaiseDuringAdd = false;
            RetainingDisposedComponent.ReenterAfterRaise = null;
            RetainingDisposedComponent.LastInstance = null;
            Exception surfaced = null;

            try
            {
                try
                {
                    XamlRuntime failed = XamlRuntime.Load(
                        "<RetainingDisposedComponent " +
                        "    Value='{Binding First}' />",
                        state);
                    failed.Dispose();
                }
                catch (Exception ex)
                {
                    surfaced = ex;
                }

                AssertTrue(
                    ContainsExceptionMessage(
                        surfaced,
                        "Disposed add failed after retaining its handler."),
                    "the original dynamic-target add failure is preserved");
                RetainingDisposedComponent target =
                    RetainingDisposedComponent.LastInstance;
                AssertTrue(
                    target != null,
                    "the failed dynamic target is created");
                AssertEqual(
                    1,
                    target.AddCount,
                    "the failed dynamic target receives one handler");
                AssertTrue(
                    target.RemoveAttemptCount >= 1,
                    "the failed dynamic-target add receives cleanup attempts");
                AssertEqual(
                    1,
                    target.SubscriberCount,
                    "the hostile dynamic target retained its failed handler");
                AssertObservableForwarderDisabled(
                    target.RetainedHandler,
                    "_target",
                    "failed dynamic-target add forwarder");
                AssertEqual(
                    0,
                    state.SubscriberCount,
                    "a failed dynamic target leaves no source subscription");
                target.RaiseDisposed(null);
            }
            finally
            {
                RetainingDisposedComponent.ThrowAfterAdd = false;
                RetainingDisposedComponent.ThrowOnRemove = false;
                RetainingDisposedComponent.RaiseDuringAdd = false;
                RetainingDisposedComponent.ReenterAfterRaise = null;
                RetainingDisposedComponent.LastInstance = null;
            }
        }

        private static void
            TestDynamicTargetDisposalAddReentryDoesNotPublish()
        {
            AggregateNotifyState state = new AggregateNotifyState();
            RetainingDisposedComponent.ThrowAfterAdd = false;
            RetainingDisposedComponent.ThrowOnRemove = false;
            RetainingDisposedComponent.RaiseDuringAdd = true;
            RetainingDisposedComponent.ReenterAfterRaise =
                new DynamicTargetAddReentry(
                    RegisterReentrantDynamicTargetBinding);
            RetainingDisposedComponent.LastInstance = null;
            XamlRuntime runtime = null;

            try
            {
                runtime = XamlRuntime.Load(
                    "<RetainingDisposedComponent " +
                    "    Value='{Binding First}' />",
                    state);
                RetainingDisposedComponent target =
                    RetainingDisposedComponent.LastInstance;
                AssertTrue(
                    target != null,
                    "the synchronously disposed dynamic target is created");
                AssertEqual(
                    1,
                    target.AddCount,
                    "the synchronously disposed target receives one handler");
                AssertEqual(
                    1,
                    target.RemoveAttemptCount,
                    "the synchronously disposed target is detached after add");
                AssertEqual(
                    0,
                    target.SubscriberCount,
                    "the synchronously disposed target retains no handler");
                AssertEqual(
                    0,
                    state.SubscriberCount,
                    "synchronous disposal prevents source observation");
                AssertEqual(
                    0,
                    GetRuntimeCollectionCount(
                        runtime,
                        "_dynamicTargetDisposalHooks"),
                    "synchronous disposal leaves no active target hook");
                AssertEqual(
                    0,
                    GetRuntimeCollectionCount(
                        runtime,
                        "_dynamicPropertyBindings"),
                    "synchronous disposal publishes no dynamic binding");
            }
            finally
            {
                if (runtime != null)
                    runtime.Dispose();

                RetainingDisposedComponent.ThrowAfterAdd = false;
                RetainingDisposedComponent.ThrowOnRemove = false;
                RetainingDisposedComponent.RaiseDuringAdd = false;
                RetainingDisposedComponent.ReenterAfterRaise = null;
                RetainingDisposedComponent.LastInstance = null;
            }
        }

        private static void
            TestDynamicTargetDisposalIsInertBeforeSourceCleanup()
        {
            ReentrantDisposalNotifyState state =
                new ReentrantDisposalNotifyState();
            RetainingDisposedComponent.ThrowAfterAdd = false;
            RetainingDisposedComponent.ThrowOnRemove = false;
            RetainingDisposedComponent.RaiseDuringAdd = false;
            RetainingDisposedComponent.ReenterAfterRaise = null;
            RetainingDisposedComponent.LastInstance = null;
            XamlRuntime runtime = null;

            try
            {
                runtime = XamlRuntime.Load(
                    "<RetainingDisposedComponent " +
                    "    Value='{Binding Value}' />",
                    state);
                RetainingDisposedComponent target =
                    RetainingDisposedComponent.LastInstance;
                AssertTrue(
                    target != null,
                    "the reentrant-cleanup target is created");
                state.Target = target;
                state.RaiseDisposedOnRemove = true;

                target.RaiseDisposed(new object());

                AssertEqual(
                    0,
                    state.SubscriberCount,
                    "target disposal releases its observable source");
                AssertEqual(
                    0,
                    target.SubscriberCount,
                    "the target hook is inert before source cleanup reenters");
                AssertEqual(
                    0,
                    GetRuntimeCollectionCount(
                        runtime,
                        "_dynamicPropertyBindings"),
                    "reentrant disposal removes each binding once");
                AssertEqual(
                    0,
                    GetRuntimeCollectionCount(
                        runtime,
                        "_dynamicTargetDisposalHooks"),
                    "reentrant disposal leaves no active target hook");
                AssertEqual(
                    0,
                    GetRuntimeCollectionCount(
                        runtime,
                        "_dynamicTargetDisposalRetryHooks"),
                    "successful reentrant cleanup leaves no retry debt");
            }
            finally
            {
                state.RaiseDisposedOnRemove = false;
                state.Target = null;

                if (runtime != null)
                    runtime.Dispose();

                RetainingDisposedComponent.ThrowAfterAdd = false;
                RetainingDisposedComponent.ThrowOnRemove = false;
                RetainingDisposedComponent.RaiseDuringAdd = false;
                RetainingDisposedComponent.ReenterAfterRaise = null;
                RetainingDisposedComponent.LastInstance = null;
            }
        }

        private static void
            TestDynamicTargetDisposalRetryDoesNotBlockReplacement()
        {
            AggregateNotifyState state = new AggregateNotifyState();
            RetainingDisposedComponent.ThrowAfterAdd = false;
            RetainingDisposedComponent.ThrowOnRemove = false;
            RetainingDisposedComponent.RaiseDuringAdd = false;
            RetainingDisposedComponent.ReenterAfterRaise = null;
            RetainingDisposedComponent.LastInstance = null;
            XamlRuntime runtime = null;

            try
            {
                runtime = XamlRuntime.Load(
                    "<RetainingDisposedComponent " +
                    "    Value='{Binding First}' />",
                    state);
                RetainingDisposedComponent target =
                    RetainingDisposedComponent.LastInstance;
                AssertTrue(target != null, "the dynamic target is created");
                AssertEqual(
                    1,
                    state.SubscriberCount,
                    "the first dynamic binding observes its source");
                Delegate firstHandler = target.RetainedHandler;
                object firstRegistration =
                    GetDynamicTargetDisposalRegistration(
                        runtime,
                        target);
                AssertTrue(
                    firstRegistration != null,
                    "the first target hook is published before disposal");
                RetainingDisposedComponent.ThrowOnRemove = true;

                target.RaiseDisposed(new object());

                AssertEqual(
                    0,
                    state.SubscriberCount,
                    "a bogus disposal sender still releases the actual target");
                AssertEqual(
                    0,
                    GetRuntimeCollectionCount(
                        runtime,
                        "_dynamicTargetDisposalHooks"),
                    "failed removal is no longer an active target hook");
                AssertEqual(
                    1,
                    GetRuntimeCollectionCount(
                        runtime,
                        "_dynamicTargetDisposalRetryHooks"),
                    "failed removal is retained as retry debt");
                AssertObservableForwarderDisabled(
                    firstHandler,
                    "_target",
                    "failed first dynamic-target removal forwarder");
                AssertDynamicTargetDisposalRetryIsWeak(
                    firstRegistration,
                    target,
                    "failed first dynamic-target removal");

                MethodInfo registerBinding =
                    GetDynamicBindingRegistrationMethod();
                AssertTrue(
                    registerBinding != null,
                    "dynamic binding registration is available internally");
                registerBinding.Invoke(
                    runtime,
                    new object[]
                    {
                        target,
                        "Value",
                        "{Binding First}",
                        state,
                        false
                    });

                InvokeStaleDynamicTargetDisposal(
                    runtime,
                    firstRegistration,
                    target);

                AssertEqual(
                    2,
                    target.AddCount,
                    "retry debt does not block a later target hook");
                AssertEqual(
                    1,
                    state.SubscriberCount,
                    "the replacement dynamic binding observes its source");
                AssertEqual(
                    1,
                    GetRuntimeCollectionCount(
                        runtime,
                        "_dynamicTargetDisposalHooks"),
                    "a stale callback cannot remove the replacement target hook");
                AssertEqual(
                    1,
                    GetRuntimeCollectionCount(
                        runtime,
                        "_dynamicPropertyBindings"),
                    "a stale callback cannot remove the replacement binding");
                AssertEqual(
                    1,
                    GetRuntimeCollectionCount(
                        runtime,
                        "_dynamicTargetDisposalRetryHooks"),
                    "the old failed removal remains separately retryable");

                Delegate[] retainedHandlers =
                    target.RetainedHandler.GetInvocationList();
                AssertEqual(
                    2,
                    retainedHandlers.Length,
                    "the hostile target retains old and replacement handlers");
                Delegate secondHandler = retainedHandlers[1];

                runtime.Dispose();

                AssertTrue(
                    runtime.IsDisposed,
                    "the first disposal completes with retry debt");
                AssertEqual(
                    0,
                    state.SubscriberCount,
                    "runtime disposal releases the replacement source");
                AssertEqual(
                    2,
                    target.SubscriberCount,
                    "failed removals remain available for retry");
                AssertEqual(
                    2,
                    GetRuntimeCollectionCount(
                        runtime,
                        "_dynamicTargetDisposalRetryHooks"),
                    "runtime disposal retains both failed removals as debt");
                AssertObservableForwarderDisabled(
                    secondHandler,
                    "_target",
                    "failed replacement dynamic-target removal forwarder");
                AssertAllDynamicTargetDisposalRetryDebtIsWeak(
                    runtime,
                    target,
                    2,
                    "runtime disposal retry debt");
                target.RaiseDisposed(null);

                RetainingDisposedComponent.ThrowOnRemove = false;
                runtime.Dispose();

                AssertEqual(
                    0,
                    target.SubscriberCount,
                    "a repeated runtime disposal retries retained removals");
                AssertEqual(
                    0,
                    GetRuntimeCollectionCount(
                        runtime,
                        "_dynamicTargetDisposalRetryHooks"),
                    "successful retry clears dynamic-target removal debt");
            }
            finally
            {
                RetainingDisposedComponent.ThrowAfterAdd = false;
                RetainingDisposedComponent.ThrowOnRemove = false;
                RetainingDisposedComponent.RaiseDuringAdd = false;
                RetainingDisposedComponent.ReenterAfterRaise = null;
                RetainingDisposedComponent.LastInstance = null;

                if (runtime != null)
                    runtime.Dispose();
            }
        }

        private static void RegisterReentrantDynamicTargetBinding(
            RetainingDisposedComponent target,
            object runtimeObject)
        {
            XamlRuntime runtime = runtimeObject as XamlRuntime;
            AssertTrue(
                runtime != null,
                "the in-flight disposal hook exposes its runtime internally");
            MethodInfo registerBinding =
                GetDynamicBindingRegistrationMethod();
            AssertTrue(
                registerBinding != null,
                "dynamic binding registration is available for add reentry");
            registerBinding.Invoke(
                runtime,
                new object[]
                {
                    target,
                    "Value",
                    "{Binding .}",
                    "reentrant value",
                    false
                });
        }

        private static object GetDynamicTargetDisposalRegistration(
            XamlRuntime runtime,
            object target)
        {
            FieldInfo field = typeof(XamlRuntime).GetField(
                "_dynamicTargetDisposalHooks",
                BindingFlags.Instance | BindingFlags.NonPublic);
            AssertTrue(
                field != null,
                "dynamic target disposal hooks are available internally");
            IDictionary hooks = field.GetValue(runtime) as IDictionary;
            AssertTrue(
                hooks != null,
                "dynamic target disposal hooks use dictionary storage");
            return hooks[target];
        }

        private static void InvokeStaleDynamicTargetDisposal(
            XamlRuntime runtime,
            object registration,
            object target)
        {
            MethodInfo callback = typeof(XamlRuntime).GetMethod(
                "OnDynamicTargetDisposed",
                BindingFlags.Instance | BindingFlags.NonPublic);
            AssertTrue(
                callback != null,
                "the dynamic target disposal callback is available internally");
            callback.Invoke(
                runtime,
                new object[] { registration, target });
        }

        private static void AssertDynamicTargetDisposalRetryIsWeak(
            object registration,
            object expectedTarget,
            string message)
        {
            AssertTrue(
                registration != null,
                message + " retains retry registration state");
            Type registrationType = registration.GetType();
            BindingFlags fields =
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic;
            FieldInfo targetField = registrationType.GetField(
                "Target",
                fields);
            FieldInfo componentField = registrationType.GetField(
                "Component",
                fields);
            FieldInfo referenceField = registrationType.GetField(
                "ComponentReference",
                fields);
            AssertTrue(
                targetField != null &&
                componentField != null &&
                referenceField != null,
                message + " exposes retry retention state internally");
            AssertEqual(
                null,
                targetField.GetValue(registration),
                message + " clears its strong target");
            AssertEqual(
                null,
                componentField.GetValue(registration),
                message + " clears its strong component");
            WeakReference reference =
                referenceField.GetValue(registration) as WeakReference;
            AssertTrue(
                reference != null &&
                Object.ReferenceEquals(reference.Target, expectedTarget),
                message + " retains only a live weak component reference");
        }

        private static void AssertAllDynamicTargetDisposalRetryDebtIsWeak(
            XamlRuntime runtime,
            object expectedTarget,
            int expectedCount,
            string message)
        {
            FieldInfo field = typeof(XamlRuntime).GetField(
                "_dynamicTargetDisposalRetryHooks",
                BindingFlags.Instance | BindingFlags.NonPublic);
            AssertTrue(
                field != null,
                message + " is available internally");
            IList registrations = field.GetValue(runtime) as IList;
            AssertTrue(
                registrations != null,
                message + " uses list storage");
            AssertEqual(
                expectedCount,
                registrations.Count,
                message + " has the expected registration count");

            int i;

            for (i = 0; i < registrations.Count; i++)
            {
                AssertDynamicTargetDisposalRetryIsWeak(
                    registrations[i],
                    expectedTarget,
                    message + " registration " + i.ToString());
            }
        }

        private static MethodInfo GetDynamicBindingRegistrationMethod()
        {
            return typeof(XamlRuntime).GetMethod(
                "RegisterDynamicBinding",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new Type[]
                {
                    typeof(object),
                    typeof(string),
                    typeof(string),
                    typeof(object),
                    typeof(bool)
                },
                null);
        }

        private static int GetRuntimeCollectionCount(
            XamlRuntime runtime,
            string fieldName)
        {
            FieldInfo field = typeof(XamlRuntime).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            AssertTrue(
                field != null,
                fieldName + " is available internally");
            ICollection collection = field.GetValue(runtime) as ICollection;
            AssertTrue(
                collection != null,
                fieldName + " is a retained collection");
            return collection.Count;
        }

        private static IDictionary GetRuntimeDictionary(
            XamlRuntime runtime,
            string fieldName)
        {
            FieldInfo field = typeof(XamlRuntime).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            AssertTrue(
                field != null,
                fieldName + " is available internally");
            IDictionary dictionary = field.GetValue(runtime) as IDictionary;
            AssertTrue(
                dictionary != null,
                fieldName + " uses dictionary storage");
            return dictionary;
        }

        private static int GetRuntimeInt(
            XamlRuntime runtime,
            string fieldName)
        {
            FieldInfo field = typeof(XamlRuntime).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            AssertTrue(
                field != null,
                fieldName + " is available internally");
            return (int)field.GetValue(runtime);
        }

        private static void AssertItemSourceForwarderDisabled(
            Delegate retainedHandler,
            string message)
        {
            AssertTrue(
                retainedHandler != null,
                message + " is retained for inspection");

            Delegate[] handlers = retainedHandler.GetInvocationList();
            object forwarder = handlers.Length == 0
                ? null
                : handlers[0].Target;
            AssertTrue(
                forwarder != null,
                message + " has an owned target");

            Type forwarderType = forwarder.GetType();
            FieldInfo ownerField = forwarderType.GetField(
                "_owner",
                BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo epochField = forwarderType.GetField(
                "_subscriptionEpoch",
                BindingFlags.Instance | BindingFlags.NonPublic);
            AssertTrue(
                ownerField != null &&
                epochField != null &&
                epochField.FieldType == typeof(int),
                message + " exposes owner and epoch state internally");
            AssertEqual(
                null,
                ownerField.GetValue(forwarder),
                message + " releases its ItemsControl");
        }

        private static bool ContainsExceptionMessage(
            Exception error,
            string expected)
        {
            Exception current = error;

            while (current != null)
            {
                if (current.Message != null &&
                    current.Message.IndexOf(
                        expected,
                        StringComparison.Ordinal) >= 0)
                {
                    return true;
                }

                current = current.InnerException;
            }

            return false;
        }

        private static void TestFormStartPositionIsCanonical()
        {
            const string styleMarkup =
                "<Form Style='Centered'>" +
                "  <Form.Resources>" +
                "    <Style Key='Centered' TargetType='Form'>" +
                "      <Setter Property='StartPosition' " +
                "              Value='CenterScreen' />" +
                "    </Style>" +
                "  </Form.Resources>" +
                "</Form>";
            XamlRuntime runtime = XamlRuntime.Load(styleMarkup);

            try
            {
                AssertEqual(
                    FormStartPosition.CenterScreen,
                    runtime.Form.StartPosition,
                    "native Form StartPosition works in styles");
            }
            finally
            {
                runtime.Dispose();
            }

            const string localMarkup =
                "<Form StartPosition='CenterParent' Style='Centered'>" +
                "  <Form.Resources>" +
                "    <Style Key='Centered' TargetType='Form'>" +
                "      <Setter Property='StartPosition' " +
                "              Value='CenterScreen' />" +
                "    </Style>" +
                "  </Form.Resources>" +
                "</Form>";
            runtime = XamlRuntime.Load(localMarkup);

            try
            {
                AssertEqual(
                    FormStartPosition.CenterParent,
                    runtime.Form.StartPosition,
                    "native Form StartPosition local values override styles");
            }
            finally
            {
                runtime.Dispose();
            }

            Exception surfaced = null;
            XamlRuntime aliasRuntime = null;

            try
            {
                aliasRuntime = XamlRuntime.Load(
                    "<Form WindowStartupLocation='CenterScreen' />");
            }
            catch (Exception error)
            {
                surfaced = error;
            }
            finally
            {
                if (aliasRuntime != null)
                    aliasRuntime.Dispose();
            }

            AssertTrue(
                ContainsExceptionMessage(
                    surfaced,
                    "Unsupported property/event 'WindowStartupLocation'"),
                "the removed WPF WindowStartupLocation alias is rejected");
        }

        private static void TestAggregateDependenciesUseSourceIndex()
        {
            AggregateNotifyState state = new AggregateNotifyState();
            XamlRuntime runtime = XamlRuntime.Load(
                "<Panel>" +
                "  <Label Name='Direct' Text='{Binding First}' />" +
                "  <Label Name='Aggregate' " +
                "      Text='{Binding First}|{Binding Second}' />" +
                "</Panel>",
                state);

            try
            {
                FieldInfo registrationsField =
                    typeof(XamlRuntime).GetField(
                        "_observableBindingRegistrations",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                AssertTrue(
                    registrationsField != null,
                    "observable registration storage is available");

                ArrayList registrations =
                    registrationsField.GetValue(runtime) as ArrayList;
                AssertTrue(
                    registrations != null,
                    "observable registrations are retained");

                object directIndex = null;
                object aggregateIndex = null;
                ArrayList aggregateDependencies = null;
                bool directFound = false;
                bool aggregateFound = false;
                int i;

                for (i = 0; i < registrations.Count; i++)
                {
                    object registration = registrations[i];

                    if (registration == null)
                        continue;

                    Type registrationType = registration.GetType();
                    FieldInfo dependenciesField =
                        registrationType.GetField(
                            "PathDependencies",
                            BindingFlags.Instance |
                            BindingFlags.Public |
                            BindingFlags.NonPublic);
                    FieldInfo indexField =
                        registrationType.GetField(
                            "DependencySourceIndex",
                            BindingFlags.Instance |
                            BindingFlags.Public |
                            BindingFlags.NonPublic);

                    AssertTrue(
                        dependenciesField != null && indexField != null,
                        "observable registration exposes dependency metadata internally");

                    ArrayList dependencies =
                        dependenciesField.GetValue(registration) as ArrayList;
                    object index = indexField.GetValue(registration);

                    if (dependencies != null && dependencies.Count == 1)
                    {
                        directFound = true;
                        directIndex = index;
                    }
                    else if (dependencies != null && dependencies.Count == 2)
                    {
                        aggregateFound = true;
                        aggregateIndex = index;
                        aggregateDependencies = dependencies;
                    }
                }

                AssertTrue(
                    directFound && directIndex == null,
                    "a short direct path keeps the linear fallback");
                AssertTrue(
                    aggregateFound && aggregateIndex != null,
                    "an interpolated path retains a source index");

                MethodInfo getBucket = aggregateIndex.GetType().GetMethod(
                    "GetBucket",
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);
                AssertTrue(
                    getBucket != null,
                    "the aggregate source index exposes an internal bucket lookup");

                MethodInfo containsSource =
                    aggregateIndex.GetType().GetMethod(
                        "ContainsSource",
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic);
                MethodInfo isFirstDependency =
                    aggregateIndex.GetType().GetMethod(
                        "IsFirstDependencyForSource",
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic);
                AssertTrue(
                    containsSource != null && isFirstDependency != null,
                    "the aggregate source index exposes reconciliation lookups");

                ArrayList sameSourceDependencies =
                    getBucket.Invoke(
                        aggregateIndex,
                        new object[] { state }) as ArrayList;
                AssertTrue(
                    sameSourceDependencies != null &&
                    sameSourceDependencies.Count == 2,
                    "same-source members remain distinct inside one indexed bucket");
                AssertTrue(
                    (bool)containsSource.Invoke(
                        aggregateIndex,
                        new object[] { state }),
                    "the aggregate source index finds an observed source");
                AssertTrue(
                    aggregateDependencies != null &&
                    (bool)isFirstDependency.Invoke(
                        aggregateIndex,
                        new object[] { aggregateDependencies[0] }) &&
                    !(bool)isFirstDependency.Invoke(
                        aggregateIndex,
                        new object[] { aggregateDependencies[1] }),
                    "only the first same-source dependency owns reconciliation");
                AssertEqual(
                    1,
                    state.SubscriberCount,
                    "direct and aggregate bindings share one source subscription");

                runtime.RootControl.CreateControl();
                state.Second = "Changed";
                DrainCallbacks();
                AssertEqual(
                    "First|Changed",
                    runtime.Get<Label>("Aggregate").Text,
                    "an indexed exact-member notification refreshes its aggregate");
            }
            finally
            {
                runtime.Dispose();
            }

            AssertEqual(
                0,
                state.SubscriberCount,
                "disposing indexed registrations releases the pooled source");
        }

        private static void
            TestIndexedAggregateReconciliationKeepsSourceSubscriptions()
        {
            const string markup =
                "<ItemsControl Name='Rows' Width='180' Height='60' " +
                "    AutoScroll='true' ItemKeyPath='Id' " +
                "    Virtualizing='true' VirtualizationThreshold='1' " +
                "    OverscanItems='0' FixedItemSize='20' " +
                "    ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label Condition='{Binding Show}' Text='Row' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";
            IndexedConditionRow first =
                new IndexedConditionRow("first");
            IndexedConditionRow second =
                new IndexedConditionRow("second");
            IndexedConditionRow third =
                new IndexedConditionRow("third");
            IndexedConditionRow replacement =
                new IndexedConditionRow("replacement");
            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ArrayList initial = new ArrayList();
                initial.Add(first);
                initial.Add(second);
                initial.Add(third);
                host.CreateControl();
                host.SetItems(initial);

                AssertIndexedConditionSubscription(
                    first,
                    1,
                    0,
                    1,
                    "initial first source");
                AssertIndexedConditionSubscription(
                    second,
                    1,
                    0,
                    1,
                    "initial second source");
                AssertIndexedConditionSubscription(
                    third,
                    1,
                    0,
                    1,
                    "initial third source");

                ArrayList reordered = new ArrayList();
                reordered.Add(third);
                reordered.Add(first);
                reordered.Add(second);
                host.SetItems(reordered);

                AssertIndexedConditionSubscription(
                    first,
                    1,
                    0,
                    1,
                    "reordered first source");
                AssertIndexedConditionSubscription(
                    second,
                    1,
                    0,
                    1,
                    "reordered second source");
                AssertIndexedConditionSubscription(
                    third,
                    1,
                    0,
                    1,
                    "reordered third source");

                ArrayList replaced = new ArrayList();
                replaced.Add(third);
                replaced.Add(replacement);
                replaced.Add(first);
                host.SetItems(replaced);

                AssertIndexedConditionSubscription(
                    first,
                    1,
                    0,
                    1,
                    "retained first source");
                AssertIndexedConditionSubscription(
                    second,
                    1,
                    1,
                    0,
                    "removed second source");
                AssertIndexedConditionSubscription(
                    third,
                    1,
                    0,
                    1,
                    "retained third source");
                AssertIndexedConditionSubscription(
                    replacement,
                    1,
                    0,
                    1,
                    "new replacement source");

                replacement.Show = false;
                DrainCallbacks();
                AssertEqual(
                    1,
                    replacement.SubscriberCount,
                    "the indexed replacement remains observable after refresh");
            }
            finally
            {
                runtime.Dispose();
            }

            AssertIndexedConditionSubscription(
                first,
                1,
                1,
                0,
                "disposed first source");
            AssertIndexedConditionSubscription(
                second,
                1,
                1,
                0,
                "disposed removed source");
            AssertIndexedConditionSubscription(
                third,
                1,
                1,
                0,
                "disposed third source");
            AssertIndexedConditionSubscription(
                replacement,
                1,
                1,
                0,
                "disposed replacement source");
        }

        private static void AssertIndexedConditionSubscription(
            IndexedConditionRow source,
            int expectedAdds,
            int expectedRemoves,
            int expectedSubscribers,
            string message)
        {
            AssertEqual(
                expectedAdds,
                source.AddCount,
                message + " add count");
            AssertEqual(
                expectedRemoves,
                source.RemoveCount,
                message + " remove count");
            AssertEqual(
                expectedSubscribers,
                source.SubscriberCount,
                message + " subscriber count");
        }

        private static void TestInvalidTwoWayEditCanRecover()
        {
            AuditState state = new AuditState();
            XamlRuntime runtime = XamlRuntime.Load(
                "<Panel>" +
                "  <TextBox Name='NumberEditor' " +
                "      Text='{Binding Number, Mode=TwoWay}' />" +
                "</Panel>",
                state);

            try
            {
                TextBox editor = runtime.Get<TextBox>("NumberEditor");
                runtime.RootControl.CreateControl();

                editor.Text = "not a number";
                DrainCallbacks();
                AssertEqual(
                    1,
                    state.Number.Value,
                    "an invalid target edit leaves the source unchanged");
                AssertEqual(
                    "not a number",
                    editor.Text,
                    "an invalid target edit stays visible");

                editor.Text = "42";
                DrainCallbacks();
                AssertEqual(
                    42,
                    state.Number.Value,
                    "a later valid edit updates the source");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestExactPropertyBeatsMarkupFallback()
        {
            AuditState state = new AuditState();
            XamlRuntime runtime = XamlRuntime.Load(
                "<ExactHeaderControl Name='Target' Text='native text' " +
                "    Header='{Binding Header, Mode=TwoWay}' />",
                state);

            try
            {
                ExactHeaderControl target =
                    runtime.Get<ExactHeaderControl>("Target");
                runtime.RootControl.CreateControl();

                AssertEqual(
                    "Header source",
                    target.Header,
                    "an exact Header property receives Header binding");
                AssertEqual(
                    "native text",
                    target.Text,
                    "the Header fallback does not overwrite Text");

                target.Header = "edited header";
                DrainCallbacks();
                AssertEqual(
                    "edited header",
                    state.Header.Value,
                    "two-way binding observes the exact Header property");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestExactStyleAndResourceStyleRemainIndependent()
        {
            AuditState state = new AuditState();
            XamlRuntime runtime = XamlRuntime.Load(
                "<Panel>" +
                "  <Panel.Resources>" +
                "    <Style Key='Named' TargetType='ExactStyleControl'>" +
                "      <Setter Property='Text' Value='resource style text' />" +
                "    </Style>" +
                "    <Style Key='ProgressNamed' TargetType='ProgressBar'>" +
                "      <Setter Property='Minimum' Value='5' />" +
                "    </Style>" +
                "  </Panel.Resources>" +
                "  <ExactStyleControl Name='Target' ResourceStyle='Named' " +
                "      Style='{Binding Style, Mode=TwoWay}' />" +
                "  <ProgressBar Name='Progress' ResourceStyle='ProgressNamed' " +
                "      Style='Continuous' />" +
                "</Panel>",
                state);

            try
            {
                ExactStyleControl target =
                    runtime.Get<ExactStyleControl>("Target");
                ProgressBar progress =
                    runtime.Get<ProgressBar>("Progress");
                runtime.RootControl.CreateControl();

                AssertEqual(
                    "resource style text",
                    target.Text,
                    "ResourceStyle selects the named resource style");
                AssertEqual(
                    "native style source",
                    target.Style,
                    "a writable CLR Style property remains an exact property");
                AssertEqual(
                    ProgressBarStyle.Continuous,
                    progress.Style,
                    "native ProgressBar.Style remains an exact property");
                AssertEqual(
                    5,
                    progress.Minimum,
                    "ResourceStyle also applies to a native Style control");

                state.Style.Value = "source update";
                DrainCallbacks();
                AssertEqual(
                    "source update",
                    target.Style,
                    "the exact Style property observes source updates");

                target.Style = "target edit";
                DrainCallbacks();
                AssertEqual(
                    "target edit",
                    state.Style.Value,
                    "the exact Style property supports two-way updates");
                AssertEqual(
                    "resource style text",
                    target.Text,
                    "exact Style updates do not replace ResourceStyle");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestNullItemDoesNotUseOuterCodeBehind()
        {
            AuditState state = new AuditState();
            XamlRuntime runtime = XamlRuntime.Load(
                "<ItemsControl Name='Rows' Virtualizing='false' " +
                "    ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <FlowLayoutPanel>" +
                "      <Label Text='{Binding OuterText}' />" +
                "      <Label Text='{Binding .}' />" +
                "      <Label Text='{Function DescribeItem}' />" +
                "    </FlowLayoutPanel>" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>",
                state);

            try
            {
                XamlRuntime.ItemsControl rows = runtime.GetItemsControl("Rows");
                rows.ItemsSource = new object[] { null };

                FlowLayoutPanel itemRoot =
                    rows.Controls[0] as FlowLayoutPanel;
                AssertTrue(itemRoot != null, "null item template is rendered");
                AssertEqual(
                    String.Empty,
                    itemRoot.Controls[0].Text,
                    "a null item path does not fall back to code-behind");
                AssertEqual(
                    String.Empty,
                    itemRoot.Controls[1].Text,
                    "Binding dot preserves a null item");
                AssertEqual(
                    "null item",
                    itemRoot.Controls[2].Text,
                    "a bare Function receives the explicit null item");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestItemSourceForwardersReleaseRuntimeGraphs()
        {
            CountingBindingList addFailure = new CountingBindingList();
            XamlRuntime addFailureRuntime = LoadItemSourceAuditRuntime();

            try
            {
                addFailure.ThrowAfterAdd = true;
                addFailure.ThrowOnRemove = true;
                Exception addError = null;

                try
                {
                    addFailureRuntime.GetItemsControl("Rows").ItemsSource =
                        addFailure;
                }
                catch (Exception ex)
                {
                    addError = ex;
                }

                AssertEqual(
                    "ListChanged add failed after retaining its handler.",
                    addError == null ? null : addError.Message,
                    "the original item-source add failure is preserved");
                AssertEqual(
                    1,
                    addFailure.AddCount,
                    "a failed item source receives one add attempt");
                AssertEqual(
                    1,
                    addFailure.RemoveAttemptCount,
                    "a failed item-source add receives one cleanup attempt");
                AssertEqual(
                    1,
                    addFailure.SubscriberCount,
                    "the hostile item source retained its failed handler");
                AssertTrue(
                    addFailure.HandlerWasDisabledAtRemove,
                    "the failed item-source forwarder is disabled before cleanup");
                AssertItemSourceForwarderDisabled(
                    addFailure.RetainedHandler,
                    "failed item-source add forwarder");
                addFailure.Add("late failed-add notification");
            }
            finally
            {
                addFailureRuntime.Dispose();
            }

            CountingBindingList disposalFailure =
                new CountingBindingList();
            XamlRuntime disposalRuntime = LoadItemSourceAuditRuntime();

            try
            {
                disposalRuntime.GetItemsControl("Rows").ItemsSource =
                    disposalFailure;
                disposalFailure.ThrowOnRemove = true;
                Exception disposeError = null;

                try
                {
                    disposalRuntime.Dispose();
                }
                catch (Exception ex)
                {
                    disposeError = ex;
                }

                AssertTrue(
                    ContainsExceptionMessage(
                        disposeError,
                        "ListChanged remove failed before detaching."),
                    "item-source disposal preserves the removal failure");
                AssertEqual(
                    1,
                    disposalFailure.RemoveAttemptCount,
                    "item-source disposal attempts removal once");
                AssertEqual(
                    1,
                    disposalFailure.SubscriberCount,
                    "the hostile item source retained its disposal handler");
                AssertTrue(
                    disposalFailure.HandlerWasDisabledAtRemove,
                    "the disposal forwarder is disabled before removal");
                AssertItemSourceForwarderDisabled(
                    disposalFailure.RetainedHandler,
                    "failed item-source disposal forwarder");
                AssertTrue(
                    disposalRuntime.IsDisposed,
                    "a reported item-source removal failure still disposes the runtime");
                disposalFailure.Add("late disposal notification");
            }
            finally
            {
                if (!disposalRuntime.IsDisposed)
                {
                    disposalFailure.ThrowOnRemove = false;
                    disposalRuntime.Dispose();
                }
            }
        }

        private static void TestItemSourceReplacementRemoveFailureCanRetry()
        {
            XamlRuntime runtime = LoadItemSourceAuditRuntime();
            CountingBindingList previous = new CountingBindingList();
            CountingBindingList desired = new CountingBindingList();

            try
            {
                XamlRuntime.ItemsControl rows =
                    runtime.GetItemsControl("Rows");
                rows.ItemsSource = previous;
                previous.ThrowOnRemove = true;
                Exception replacementError = null;

                try
                {
                    rows.ItemsSource = desired;
                }
                catch (Exception ex)
                {
                    replacementError = ex;
                }

                AssertEqual(
                    "ListChanged remove failed before detaching.",
                    replacementError == null
                        ? null
                        : replacementError.Message,
                    "item-source replacement preserves the old removal failure");
                AssertEqual(
                    1,
                    previous.RemoveAttemptCount,
                    "the old item source receives one removal attempt");
                AssertTrue(
                    previous.HandlerWasDisabledAtRemove,
                    "the old item-source forwarder is disabled before removal");
                AssertItemSourceForwarderDisabled(
                    previous.RetainedHandler,
                    "failed old item-source removal forwarder");
                AssertEqual(
                    0,
                    desired.AddCount,
                    "the desired source is not attached after old removal fails");
                AssertEqual(
                    0,
                    desired.SubscriberCount,
                    "the desired source remains unattached after the failure");

                rows.ItemsSource = desired;

                AssertEqual(
                    1,
                    desired.AddCount,
                    "retrying the desired source performs exactly one attach");
                AssertEqual(
                    1,
                    desired.SubscriberCount,
                    "retrying the desired source establishes observation");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static XamlRuntime LoadItemSourceAuditRuntime()
        {
            return XamlRuntime.Load(
                "<ItemsControl Name='Rows' Virtualizing='false' " +
                "    ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label Text='{Binding .}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>");
        }

        private static void TestRuntimeDisposeDetachesItemSource()
        {
            XamlRuntime runtime = XamlRuntime.Load(
                "<ItemsControl Name='Rows' Virtualizing='false' " +
                "    ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label Text='{Binding .}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>");
            XamlRuntime.ItemsControl rows = runtime.GetItemsControl("Rows");
            CountingBindingList source = new CountingBindingList();

            rows.ItemsSource = source;
            AssertEqual(
                1,
                source.SubscriberCount,
                "ItemsControl observes IBindingList before disposal");

            runtime.Dispose();

            AssertEqual(
                0,
                source.SubscriberCount,
                "runtime disposal detaches the retained IBindingList");
        }

        private static void TestDynamicCleanupContinuesAfterItemSourceFailure()
        {
            XamlRuntime runtime = XamlRuntime.Load(
                "<FlowLayoutPanel>" +
                "  <ItemsControl Name='First' Virtualizing='false' " +
                "      ProgressiveRendering='false'>" +
                "    <ItemsControl.ItemTemplate>" +
                "      <Label Text='{Binding .}' />" +
                "    </ItemsControl.ItemTemplate>" +
                "  </ItemsControl>" +
                "  <ItemsControl Name='Second' Virtualizing='false' " +
                "      ProgressiveRendering='false'>" +
                "    <ItemsControl.ItemTemplate>" +
                "      <Label Text='{Binding .}' />" +
                "    </ItemsControl.ItemTemplate>" +
                "  </ItemsControl>" +
                "</FlowLayoutPanel>");
            CountingBindingList first = new CountingBindingList();
            CountingBindingList second = new CountingBindingList();

            runtime.GetItemsControl("First").ItemsSource = first;
            runtime.GetItemsControl("Second").ItemsSource = second;
            first.ThrowAfterRemove = true;

            Exception disposeError = null;

            try
            {
                runtime.Dispose();
            }
            catch (Exception ex)
            {
                disposeError = ex;
            }

            AssertTrue(
                disposeError != null,
                "the first dynamic cleanup failure is reported");
            AssertEqual(
                0,
                first.SubscriberCount,
                "the throwing item source was detached before its error");
            AssertEqual(
                0,
                second.SubscriberCount,
                "later item sources are detached after an earlier failure");
            AssertTrue(
                runtime.IsDisposed,
                "a reported cleanup failure still leaves the runtime disposed");
        }

        private static void DrainCallbacks()
        {
            int i;

            for (i = 0; i < 4; i++)
                Application.DoEvents();
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private static void AssertEqual(
            object expected,
            object actual,
            string message)
        {
            if (!Object.Equals(expected, actual))
            {
                throw new InvalidOperationException(
                    message +
                    ". Expected: " +
                    (expected == null ? "<null>" : expected.ToString()) +
                    "; actual: " +
                    (actual == null ? "<null>" : actual.ToString()) +
                    ".");
            }
        }
    }
}
