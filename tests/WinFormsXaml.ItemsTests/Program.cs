using System;
using System.Collections;
using System.ComponentModel;
using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using WinFormsXaml;

namespace WinFormsXaml.ItemsTests
{
    internal sealed class Program
    {
        private delegate void TestMethod();

        private sealed class TestCase
        {
            private readonly string _name;
            private readonly TestMethod _method;

            public TestCase(string name, TestMethod method)
            {
                _name = name;
                _method = method;
            }

            public string Name
            {
                get { return _name; }
            }

            public TestMethod Method
            {
                get { return _method; }
            }
        }

        private sealed class ItemRow
        {
            public string Id;
            public int Version;
            public string Text;

            public ItemRow(
                string id,
                int version,
                string text)
            {
                Id = id;
                Version = version;
                Text = text;
            }
        }

        private sealed class VariableHeightRow
        {
            public string Id;
            public int Height;
            public string Text;

            public VariableHeightRow(
                string id,
                int height,
                string text)
            {
                Id = id;
                Height = height;
                Text = text;
            }
        }

        private sealed class ThrowingEnumerable : IEnumerable
        {
            public IEnumerator GetEnumerator()
            {
                throw new InvalidOperationException("Source enumeration failed.");
            }
        }

        private sealed class ReentrantThrowingEnumerable : IEnumerable
        {
            private readonly XamlRuntime.ItemsControl _host;
            private readonly IEnumerable _nestedItems;

            public ReentrantThrowingEnumerable(
                XamlRuntime.ItemsControl host,
                IEnumerable nestedItems)
            {
                _host = host;
                _nestedItems = nestedItems;
            }

            public IEnumerator GetEnumerator()
            {
                return new ReentrantThrowingEnumerator(
                    _host,
                    _nestedItems);
            }

            private sealed class ReentrantThrowingEnumerator : IEnumerator
            {
                private readonly XamlRuntime.ItemsControl _host;
                private readonly IEnumerable _nestedItems;
                private bool _invoked;

                public ReentrantThrowingEnumerator(
                    XamlRuntime.ItemsControl host,
                    IEnumerable nestedItems)
                {
                    _host = host;
                    _nestedItems = nestedItems;
                }

                public object Current
                {
                    get { return null; }
                }

                public bool MoveNext()
                {
                    if (_invoked)
                        return false;

                    _invoked = true;
                    _host.SetItems(_nestedItems);

                    throw new InvalidOperationException(
                        "Outer enumeration failed after starting nested work.");
                }

                public void Reset()
                {
                    throw new NotSupportedException();
                }
            }
        }

        private sealed class ReentrantVersionRow
        {
            public string Id;
            public string Text;
            public XamlRuntime.ItemsControl Host;
            public IEnumerable NestedItems;

            private bool _invoked;

            public int Version
            {
                get
                {
                    if (!_invoked)
                    {
                        _invoked = true;
                        Host.SetItems(NestedItems);
                    }

                    return 1;
                }
            }
        }

        private sealed class ThrowingVersionRow
        {
            public string Id;
            public string Text;

            public int Version
            {
                get
                {
                    throw new InvalidOperationException(
                        "Version resolution failed.");
                }
            }
        }

        private sealed class ThrowingTextRow
        {
            public string Id;

            public string Text
            {
                get
                {
                    throw new InvalidOperationException(
                        "Item template binding failed.");
                }
            }

            public ThrowingTextRow(string id)
            {
                Id = id;
            }
        }

        private sealed class MissingBindingRow
        {
            public string Id;

            public MissingBindingRow(string id)
            {
                Id = id;
            }
        }

        private sealed class NullableIntermediateBindingRow
        {
            public string Id;
            public ItemRow Child;

            public NullableIntermediateBindingRow(string id)
            {
                Id = id;
                Child = null;
            }
        }

        private sealed class ToggleTextRow
        {
            public string Id;
            public string Value;
            public bool ThrowOnRead;

            public string Text
            {
                get
                {
                    if (ThrowOnRead)
                    {
                        throw new InvalidOperationException(
                            "Item template binding failed on demand.");
                    }

                    return Value;
                }
            }

            public ToggleTextRow(
                string id,
                string value)
            {
                Id = id;
                Value = value;
            }
        }

        private sealed class ConditionalToggleRow
        {
            public string Id;
            public bool Show;
            public string Value;

            public string Text
            {
                get
                {
                    return Value;
                }
            }

            public ConditionalToggleRow(
                string id,
                string value)
            {
                Id = id;
                Show = true;
                Value = value;
            }
        }

        private sealed class VisibilityRow
        {
            public string Id;
            public int GridRow;
            public string Text;
            public string Visibility;

            public VisibilityRow(
                string id,
                int gridRow,
                string text,
                string visibility)
            {
                Id = id;
                GridRow = gridRow;
                Text = text;
                Visibility = visibility;
            }
        }

        private sealed class ConditionalVisibilityRow
        {
            public string Id;
            public string Text;
            public bool Show;
            public bool Visible;

            public ConditionalVisibilityRow(
                string id,
                string text,
                bool show,
                bool visible)
            {
                Id = id;
                Text = text;
                Show = show;
                Visible = visible;
            }
        }

        private sealed class ReactiveItemRow
        {
            public string Id;
            public readonly int Version;
            public readonly PropertyBinding<string> Text;
            public readonly PropertyBinding<bool> Show;

            public ReactiveItemRow(
                string id,
                string text,
                bool show)
            {
                Id = id;
                Version = 1;
                Text = new PropertyBinding<string>(text);
                Show = new PropertyBinding<bool>(show);
            }
        }

        private sealed class DualReactiveItemRow
        {
            public readonly string Id;
            public readonly PropertyBinding<string> Primary;
            public readonly PropertyBinding<string> Secondary;

            public DualReactiveItemRow(
                string id,
                string primary,
                string secondary)
            {
                Id = id;
                Primary = new PropertyBinding<string>(primary);
                Secondary = new PropertyBinding<string>(secondary);
            }
        }

        private sealed class ReactiveComponentConditionRow
        {
            public readonly string Id;
            public readonly int Version;
            public readonly string Text;
            public readonly PropertyBinding<bool> TemplateShow;
            public readonly PropertyBinding<bool> InvocationShow;

            public ReactiveComponentConditionRow(
                string id,
                string text,
                bool templateShow,
                bool invocationShow)
            {
                Id = id;
                Version = 1;
                Text = text;
                TemplateShow =
                    new PropertyBinding<bool>(templateShow);
                InvocationShow =
                    new PropertyBinding<bool>(invocationShow);
            }
        }

        private sealed class VersionedComponentGetterRow
        {
            private readonly PropertyBinding<bool> _templateShow;

            public readonly string Id;
            public readonly int Version;
            public readonly string Text;
            public readonly PropertyBinding<bool> InvocationShow;
            public int TemplateShowReadCount;

            public VersionedComponentGetterRow(
                string id,
                string text)
            {
                Id = id;
                Version = 1;
                Text = text;
                InvocationShow = new PropertyBinding<bool>(true);
                _templateShow = new PropertyBinding<bool>(true);
            }

            public PropertyBinding<bool> TemplateShow
            {
                get
                {
                    TemplateShowReadCount++;
                    return _templateShow;
                }
            }

            public PropertyBinding<bool> TemplateShowSource
            {
                get { return _templateShow; }
            }
        }

        private sealed class RollbackBuildState
        {
            public bool ThrowOnBuild;

            public string GetRollbackText()
            {
                if (ThrowOnBuild)
                {
                    throw new InvalidOperationException(
                        "Virtual component build failed on demand.");
                }

                return "Rollback component";
            }
        }

        private sealed class ReactiveItemEndpoint
        {
            public readonly PropertyBinding<string> Text;

            public ReactiveItemEndpoint(string text)
            {
                Text = new PropertyBinding<string>(text);
            }
        }

        private sealed class ReactiveEndpointRow
        {
            public string Id;
            public readonly PropertyBinding<ReactiveItemEndpoint> Endpoint;

            public ReactiveEndpointRow(
                string id,
                ReactiveItemEndpoint endpoint)
            {
                Id = id;
                Endpoint =
                    new PropertyBinding<ReactiveItemEndpoint>(endpoint);
            }
        }

        private sealed class CountingReactiveItemRow
        {
            private readonly PropertyBinding<string> _text;

            public string Id;
            public int TextReads;

            public CountingReactiveItemRow(
                string id,
                string text)
            {
                Id = id;
                _text = new PropertyBinding<string>(text);
            }

            public PropertyBinding<string> Text
            {
                get
                {
                    TextReads++;
                    return _text;
                }
            }

            public void SetText(string value)
            {
                _text.Value = value;
            }
        }

        private sealed class CountingVersionRow
        {
            private int _version;

            public string Id;
            public string Text;
            public int VersionReads;

            public CountingVersionRow(
                string id,
                int version,
                string text)
            {
                Id = id;
                _version = version;
                Text = text;
            }

            public int Version
            {
                get
                {
                    VersionReads++;
                    return _version;
                }
                set { _version = value; }
            }
        }

        private sealed class CountingEnumerable : IEnumerable
        {
            private readonly IList _items;

            public int GetEnumeratorCount;
            public int MoveNextCount;

            public CountingEnumerable(IList items)
            {
                _items = items;
            }

            public IEnumerator GetEnumerator()
            {
                GetEnumeratorCount++;
                return new CountingEnumerator(this, _items);
            }

            public void ResetCounts()
            {
                GetEnumeratorCount = 0;
                MoveNextCount = 0;
            }

            private sealed class CountingEnumerator : IEnumerator
            {
                private readonly CountingEnumerable _owner;
                private readonly IList _items;
                private int _index;

                public CountingEnumerator(
                    CountingEnumerable owner,
                    IList items)
                {
                    _owner = owner;
                    _items = items;
                    _index = -1;
                }

                public object Current
                {
                    get { return _items[_index]; }
                }

                public bool MoveNext()
                {
                    _owner.MoveNextCount++;
                    _index++;
                    return _index < _items.Count;
                }

                public void Reset()
                {
                    _index = -1;
                }
            }
        }

        private sealed class CountingListChangedRow : INotifyPropertyChanged
        {
            private string _text;

            public readonly string Id;
            public int TextReads;

            public CountingListChangedRow(
                string id,
                string text)
            {
                Id = id;
                _text = text;
            }

            public string Text
            {
                get
                {
                    TextReads++;
                    return _text;
                }
                set
                {
                    if (String.Equals(
                        _text,
                        value,
                        StringComparison.Ordinal))
                    {
                        return;
                    }

                    _text = value;
                    PropertyChangedEventHandler handler =
                        PropertyChanged;

                    if (handler != null)
                    {
                        handler(
                            this,
                            new PropertyChangedEventArgs("Text"));
                    }
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;
        }

        private sealed class CountingItemChangedList :
            BindingList<CountingListChangedRow>, IEnumerable
        {
            public int GetEnumeratorCount;

            IEnumerator IEnumerable.GetEnumerator()
            {
                GetEnumeratorCount++;
                return ((IEnumerable)Items).GetEnumerator();
            }

            public void ResetEnumerationCount()
            {
                GetEnumeratorCount = 0;
            }
        }

        private sealed class ObservedChangeOptimizationState
        {
            public readonly CountingItemChangedList Items;
            public int EvaluationCount;

            public ObservedChangeOptimizationState()
            {
                Items = new CountingItemChangedList();
            }

            public string Format(string value)
            {
                EvaluationCount++;
                return "formatted: " + value;
            }
        }

        private sealed class CountingStructuralList :
            BindingList<ItemRow>, IEnumerable
        {
            public int GetEnumeratorCount;

            IEnumerator IEnumerable.GetEnumerator()
            {
                GetEnumeratorCount++;
                return ((IEnumerable)Items).GetEnumerator();
            }

            public void ResetEnumerationCount()
            {
                GetEnumeratorCount = 0;
            }

            public void Move(int oldIndex, int newIndex)
            {
                ItemRow item = this[oldIndex];
                bool previousRaiseListChangedEvents =
                    RaiseListChangedEvents;
                RaiseListChangedEvents = false;

                try
                {
                    RemoveAt(oldIndex);
                    Insert(newIndex, item);
                }
                finally
                {
                    RaiseListChangedEvents =
                        previousRaiseListChangedEvents;
                }

                if (previousRaiseListChangedEvents)
                {
                    OnListChanged(
                        new ListChangedEventArgs(
                            ListChangedType.ItemMoved,
                            newIndex,
                            oldIndex));
                }
            }
        }

        private sealed class ObservableItemsState
        {
            public readonly ItemsBinding<ItemRow> Items;

            public ObservableItemsState()
            {
                Items = new ItemsBinding<ItemRow>();
            }
        }

        private sealed class ManualReloadItemsState
        {
            public readonly ItemsBinding<ItemRow> Items;
            public string Suffix;
            public int FormatCalls;

            public ManualReloadItemsState()
            {
                Items = new ItemsBinding<ItemRow>();
                Suffix = String.Empty;
            }

            public string Format(ItemRow item)
            {
                FormatCalls++;
                return item.Text + Suffix;
            }
        }

        private sealed class StructuralItemsState
        {
            public readonly CountingStructuralList Items;

            public StructuralItemsState()
            {
                Items = new CountingStructuralList();
            }
        }

        private sealed class ReplaceableItemsState
        {
            public readonly PropertyBinding<IEnumerable> Source;

            public ReplaceableItemsState(IEnumerable source)
            {
                Source = new PropertyBinding<IEnumerable>(source);
            }
        }

        private sealed class ReactiveItemsState
        {
            public readonly ItemsBinding<ReactiveItemRow> Items;

            public ReactiveItemsState()
            {
                Items = new ItemsBinding<ReactiveItemRow>();
            }
        }

        private sealed class ReactiveEndpointItemsState
        {
            public readonly ItemsBinding<ReactiveEndpointRow> Items;

            public ReactiveEndpointItemsState()
            {
                Items = new ItemsBinding<ReactiveEndpointRow>();
            }
        }

        private sealed class RollbackConditionState
        {
            public XamlRuntime.ItemsControl Host;
            public bool FailureArmed;
            public bool SubscriptionScrollAttempted;
            public int ExplicitConditionReadsAfterFailure;

            public void ScrollWhenSubscriptionIsRestored()
            {
                if (SubscriptionScrollAttempted || Host == null)
                    return;

                SubscriptionScrollAttempted = true;
                FailureArmed = false;
                Host.ScrollToIndex(30);
            }
        }

        private sealed class RollbackConditionRow : INotifyPropertyChanged
        {
            private readonly RollbackConditionState _state;
            private PropertyChangedEventHandler _propertyChanged;
            private bool _throwOnTextRead;

            public readonly string Id;
            public readonly string Value;
            public bool ScrollOnNextSubscription;

            public RollbackConditionRow(
                RollbackConditionState state,
                string id,
                string value,
                bool throwOnTextRead)
            {
                _state = state;
                Id = id;
                Value = value;
                _throwOnTextRead = throwOnTextRead;
            }

            public bool Show
            {
                get
                {
                    if (_state.FailureArmed)
                        _state.ExplicitConditionReadsAfterFailure++;

                    return true;
                }
            }

            public string Text
            {
                get
                {
                    if (_throwOnTextRead)
                    {
                        _throwOnTextRead = false;
                        _state.FailureArmed = true;
                        throw new InvalidOperationException(
                            "Rollback condition test build failed.");
                    }

                    return Value;
                }
            }

            public event PropertyChangedEventHandler PropertyChanged
            {
                add
                {
                    _propertyChanged += value;

                    if (ScrollOnNextSubscription)
                    {
                        ScrollOnNextSubscription = false;
                        _state.ScrollWhenSubscriptionIsRestored();
                    }
                }
                remove
                {
                    _propertyChanged -= value;
                }
            }

            public void NotifyShowChanged()
            {
                PropertyChangedEventHandler handler = _propertyChanged;

                if (handler != null)
                {
                    handler(
                        this,
                        new PropertyChangedEventArgs("Show"));
                }
            }
        }

        private sealed class RollbackSnapshotState
        {
            public bool FailNextBuild;
            public bool MutatedConditionDuringFailure;
            public RollbackSnapshotRow Target;
        }

        private sealed class RollbackSnapshotRow : INotifyPropertyChanged
        {
            private readonly RollbackSnapshotState _state;
            private readonly bool _failureTrigger;
            private PropertyChangedEventHandler _propertyChanged;
            private bool _show;

            public readonly string Id;
            public readonly string Value;

            public RollbackSnapshotRow(
                RollbackSnapshotState state,
                string id,
                string value,
                bool failureTrigger)
            {
                _state = state;
                _failureTrigger = failureTrigger;
                _show = true;
                Id = id;
                Value = value;
            }

            public bool Show
            {
                get { return _show; }
            }

            public string Text
            {
                get
                {
                    if (_failureTrigger && _state.FailNextBuild)
                    {
                        _state.FailNextBuild = false;
                        _state.Target.SetShowSilently(false);
                        _state.MutatedConditionDuringFailure = true;
                        throw new InvalidOperationException(
                            "Rollback snapshot test build failed.");
                    }

                    return Value;
                }
            }

            public void SetShowSilently(bool value)
            {
                _show = value;
            }

            public event PropertyChangedEventHandler PropertyChanged
            {
                add { _propertyChanged += value; }
                remove { _propertyChanged -= value; }
            }

            public void NotifyShowChanged()
            {
                PropertyChangedEventHandler handler = _propertyChanged;

                if (handler != null)
                {
                    handler(
                        this,
                        new PropertyChangedEventArgs("Show"));
                }
            }
        }

        private sealed class PlanningSubscriptionState
        {
            public XamlRuntime.ItemsControl Host;
            public IEnumerable NestedItems;
            public bool Reentered;

            public void StartNestedItemsRequest()
            {
                if (Reentered || Host == null)
                    return;

                Reentered = true;
                Host.SetItems(NestedItems);
            }
        }

        private sealed class PlanningSubscriptionRow : INotifyPropertyChanged
        {
            private readonly PlanningSubscriptionState _state;
            private PropertyChangedEventHandler _propertyChanged;

            public readonly string Id;
            public readonly string Text;
            public readonly bool Show;
            public bool ReenterOnSubscription;

            public PlanningSubscriptionRow(
                PlanningSubscriptionState state,
                string id,
                string text,
                bool reenterOnSubscription)
            {
                _state = state;
                Id = id;
                Text = text;
                Show = true;
                ReenterOnSubscription = reenterOnSubscription;
            }

            public event PropertyChangedEventHandler PropertyChanged
            {
                add
                {
                    _propertyChanged += value;

                    if (ReenterOnSubscription)
                    {
                        ReenterOnSubscription = false;
                        _state.StartNestedItemsRequest();
                    }
                }
                remove { _propertyChanged -= value; }
            }

            public void NotifyShowChanged()
            {
                PropertyChangedEventHandler handler = _propertyChanged;

                if (handler != null)
                {
                    handler(
                        this,
                        new PropertyChangedEventArgs("Show"));
                }
            }
        }

        [STAThread]
        private static int Main()
        {
            TestCase[] tests = new TestCase[]
            {
                new TestCase(
                    "ItemsControl logical scroll commands",
                    ItemsControlScrollCommandTests.RunAll),
                new TestCase(
                    "ItemsControl item scroll-into-view",
                    ItemsControlScrollIntoViewTests.RunAll),
                new TestCase(
                    "ItemsControl nonvirtual flex wrapping",
                    ItemsControlWrapTests.RunAll),
                new TestCase(
                    "ItemsControl smooth scrolling",
                    ItemsControlSmoothScrollTests.RunAll),
                new TestCase(
                    "ItemsControl scroll event efficiency",
                    ItemsControlScrollEventEfficiencyTests.RunAll),
                new TestCase(
                    "framework-owned scrollbars",
                    FrameworkScrollBarTests.RunAll),
                new TestCase(
                    "ItemsControl themed scrollbar integration",
                    ItemsControlThemedScrollBarTests.RunAll),
                new TestCase(
                    "nonvirtual deferred scroll bitmap transactions",
                    ItemsControlDeferredScrollBitmapTests.RunAll),
                new TestCase(
                    "natural styled smooth-scroll bursts",
                    ItemsControlStyledSmoothBurstTests.RunAll),
                new TestCase(
                    "horizontal RTL ItemsControl scrolling",
                    HorizontalRtlItemsControlTests.RunAll),
                new TestCase(
                    "nonvirtual item rendering optimizations",
                    NonVirtualItemsOptimizationTests.RunAll),
                new TestCase(
                    "nonvirtual geometry configuration optimizations",
                    NonVirtualGeometryConfigurationTests.RunAll),
                new TestCase(
                    "reactive rendered-record identity index",
                    ReactiveRenderedRecordIndexTests.RunAll),
                new TestCase(
                    "direct virtualization primitives",
                    VirtualizationPrimitiveTests.RunAll),
                new TestCase(
                    "direct virtualization origin rollback",
                    DirectVirtualizationOriginRollbackTests.RunAll),
                new TestCase(
                    "rapid and complex virtual-scroll coverage",
                    VirtualizationScrollStressTests.RunAll),
                new TestCase(
                    "compiled item-template control blueprints",
                    ItemTemplateBlueprintTests.RunAll),
                new TestCase(
                    "explicit lightweight item virtualization",
                    LightweightVirtualizationTests.RunAll),
                new TestCase(
                    "explicit cross-item control recycling",
                    CrossItemControlRecyclingTests.RunAll),
                new TestCase("keyed patch and reorder reuse", TestKeyedPatchAndReorder),
                new TestCase(
                    "unique keyed buckets avoid per-item queues",
                    TestUniqueKeyBucketsAvoidPerItemQueues),
                new TestCase("duplicate keys reuse controls FIFO", TestDuplicateKeyFifoReuse),
                new TestCase("version token and forced rebuild", TestVersionAndForcedRebuild),
                new TestCase(
                    "stable version path is read once per non-virtual refresh",
                    TestStableVersionPathReadOncePerNonVirtualRefresh),
                new TestCase(
                    "virtual realization reuses the captured version",
                    TestVirtualRealizationReusesCapturedVersion),
                new TestCase("clear removes realized items", TestClearItems),
                new TestCase(
                    "equal custom controls keep reference-identity ownership",
                    TestEqualCustomControlOwnership),
                new TestCase(
                    "failed refresh disposes every equal custom control",
                    TestFailedEqualCustomControlCleanup),
                new TestCase("property-element binding refresh", TestPropertyElementBinding),
                new TestCase("nested item template stays lazy", TestNestedTemplateBoundary),
                new TestCase("preset root condition restores item", TestPresetRootCondition),
                new TestCase("preset style setter refreshes realized item", TestPresetStyleSetterRefresh),
                new TestCase(
                    "reactive item root condition restores item",
                    TestReactiveItemRootCondition),
                new TestCase(
                    "reactive root condition selects keyed fallback",
                    TestReactiveRootConditionUsesKeyedFallback),
                new TestCase(
                    "component root conditions select keyed fallback",
                    TestComponentRootConditionsUseKeyedFallback),
                new TestCase(
                    "stable version reuses component condition dependencies",
                    TestStableVersionReusesComponentConditionDependencies),
                new TestCase(
                    "cached reactive item reactivates",
                    TestCachedReactiveItemReactivation),
                new TestCase(
                    "observable ItemsBinding mutations refresh",
                    TestObservableItemsBindingMutations),
                new TestCase(
                    "ItemsBinding manual reload APIs stay incremental",
                    TestItemsBindingManualReloadApis),
                new TestCase(
                    "IBindingList ItemChanged patches only changed records",
                    TestObservedItemChangedPatchesOnlyChangedRecords),
                new TestCase(
                    "IBindingList duplicate property changes use one precise batch",
                    TestObservedDuplicatePropertyChangesUseOnePreciseBatch),
                new TestCase(
                    "IBindingList 40-of-1000 property changes stay precise",
                    TestObservedFortyOfThousandChangesStayPrecise),
                new TestCase(
                    "IBindingList structural changes reuse exact snapshots",
                    TestObservedStructuralChangesReuseExactSnapshots),
                new TestCase(
                    "observable ItemsSource replacement ignores stale list",
                    TestObservableItemsSourceReplacement),
                new TestCase(
                    "reactive item two-way edit",
                    TestReactiveItemTwoWayEdit),
                new TestCase(
                    "coalesced reactive item slots share one detached batch",
                    TestCoalescedReactiveItemSlotsShareDetachedBatch),
                new TestCase(
                    "reactive item endpoint replacement preserves target edit",
                    TestReactiveItemEndpointReplacementPreservesTargetEdit),
                new TestCase(
                    "reactive item patch skips source enumeration and siblings",
                    TestReactiveItemPatchSkipsSourceEnumerationAndSiblings),
                new TestCase(
                    "reactive virtual item patch skips source enumeration and siblings",
                    TestReactiveVirtualItemPatchSkipsSourceEnumerationAndSiblings),
                new TestCase("viewport realization stays bounded", TestViewportVirtualization),
                new TestCase(
                    "fast variable-height viewport jumps retain visible controls",
                    TestFastVariableHeightViewportJumpsRetainVisibleControls),
                new TestCase(
                    "tiny end rows survive a large native scroll clamp",
                    TestTinyEndRowsSurviveLargeNativeScrollClamp),
                new TestCase(
                    "equivalent direct viewport refresh is a synchronous no-op",
                    TestEquivalentDirectViewportRefreshIsNoOp),
                new TestCase(
                    "overscan changes reconcile the direct viewport synchronously",
                    TestOverscanChangeReconcilesDirectViewportSynchronously),
                new TestCase(
                    "direct viewport overscan follows scroll direction without growing",
                    TestDirectViewportOverscanFollowsScrollDirection),
                new TestCase(
                    "direct viewport scrolling publishes before returning",
                    TestDirectViewportScrollPublishesSynchronously),
                new TestCase(
                    "validated direct viewport refresh rebuilds synchronously",
                    TestValidatedDirectViewportRefreshRebuildsSynchronously),
                new TestCase("canceled progressive patch restores committed value", TestCanceledProgressivePatch),
                new TestCase("rollback setter defers newest item request", TestRollbackSetterDefersNewestRequest),
                new TestCase("failed rollback retries the same provisional value", TestFailedRollbackRetriesProvisionalValue),
                new TestCase(
                    "failed direct-to-keyed transition resumes direct scrolling",
                    TestFailedDirectToKeyedTransitionResumesDirectScrolling),
                new TestCase("reload keeps newest progressive source", TestReloadKeepsNewestProgressiveSource),
                new TestCase("pre-handle worker update is rejected", TestPreHandleWorkerUpdate),
                new TestCase("forced virtual refresh rebuilds cached rows", TestVirtualForcedReload),
                new TestCase(
                    "preset root condition selects keyed fallback",
                    TestPresetRootConditionUsesKeyedFallback),
                new TestCase(
                    "unresolved item preset leaves the system baseline",
                    TestUnresolvedItemPresetLeavesSystemBaseline),
                new TestCase(
                    "item preset restores its baseline and resolves again",
                    TestItemPresetRestoresBaselineAndResolvesAgain),
                new TestCase("enumeration failure is reported", TestEnumerationFailure),
                new TestCase("enumeration failure callback sees committed source", TestEnumerationFailureCallbackSeesCommittedSource),
                new TestCase("reentrant enumeration preserves newest request", TestReentrantEnumerationPreservesNewestRequest),
                new TestCase("planning failure is reported", TestPlanningFailure),
                new TestCase(
                    "missing child item bindings report located refresh failures",
                    TestMissingChildItemBindingsReportLocatedRefreshFailures),
                new TestCase(
                    "default progressive missing child binding reports failure",
                    TestDefaultProgressiveMissingChildBindingReportsFailure),
                new TestCase(
                    "null item binding intermediates remain valid",
                    TestNullItemBindingIntermediateRemainsValid),
                new TestCase("reentrant direct planning preserves newest request", TestReentrantDirectPlanningPreservesNewestRequest),
                new TestCase(
                    "reentrant keyed-condition subscription preserves newest request",
                    TestReentrantRootConditionSubscriptionPreservesNewestRequest),
                new TestCase("reentrant direct preparation restores committed model", TestReentrantDirectPreparationRestoresCommittedModel),
                new TestCase("failed item build preserves committed list", TestFailedBuildPreservesCommittedList),
                new TestCase("failed direct build preserves committed model", TestFailedDirectBuildPreservesCommittedModel),
                new TestCase(
                    "failed keyed component refresh restores condition subscriptions",
                    TestFailedKeyedComponentRefreshRestoresConditionSubscriptions),
                new TestCase(
                    "keyed rollback reuses conditions and suppresses subscription scroll",
                    TestKeyedRollbackConditionRestoreIsReentrancySafe),
                new TestCase(
                    "keyed rollback preserves matching condition snapshots",
                    TestKeyedRollbackPreservesMatchingConditionSnapshots),
                new TestCase(
                    "failed direct viewport build preserves committed range",
                    TestFailedDirectViewportBuildPreservesCommittedRange),
                new TestCase("failed progressive build preserves empty commit", TestFailedProgressiveBuildPreservesEmptyCommit),
                new TestCase("failed virtual force preserves cached tree", TestFailedVirtualForcePreservesCachedTree),
                new TestCase("mixed rebuild preserves patched root visibility", TestMixedRebuildPreservesPatchedRootVisibility),
                new TestCase("root condition dominates visible binding order", TestRootConditionDominatesVisible),
                new TestCase("post-commit layout error keeps committed tree", TestPostCommitLayoutErrorKeepsCommittedTree),
                new TestCase("throwing completion keeps committed tree", TestThrowingCompletionKeepsCommittedTree),
                new TestCase("reentrant completion keeps nested refresh", TestReentrantCompletionKeepsNestedRefresh),
                new TestCase("reentrant failure keeps nested refresh", TestReentrantFailureKeepsNestedRefresh),
                new TestCase(
                    "slot deactivation continues after an independent failure",
                    TestRenderBindingDeactivationContinuesAfterFailure),
                new TestCase(
                    "record retirement disposes after a slot failure",
                    TestRenderedRecordRetirementContinuesAfterSlotFailure),
                new TestCase("runtime disposal cancels progressive refresh", TestRuntimeDisposalCancelsProgressiveRefresh),
                new TestCase(
                    "runtime disposal clears queued reactive item patch",
                    TestRuntimeDisposalClearsQueuedReactiveItemPatch)
            };

            int failed = 0;
            int executed = 0;
            int i;
            string filter = Environment.GetEnvironmentVariable(
                "WINFORMSXAML_TEST_FILTER");

            for (i = 0; i < tests.Length; i++)
            {
                if (!String.IsNullOrEmpty(filter) &&
                    tests[i].Name.IndexOf(
                        filter,
                        StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                executed++;

                try
                {
                    tests[i].Method();
                    Console.WriteLine("PASS  " + tests[i].Name);
                }
                catch (Exception ex)
                {
                    failed++;
                    Console.Error.WriteLine("FAIL  " + tests[i].Name);
                    Console.Error.WriteLine(ex.ToString());
                }
            }

            Console.WriteLine(
                "WinFormsXaml items: " +
                (executed - failed) +
                " passed, " +
                failed +
                " failed.");

            return failed == 0 ? 0 : 1;
        }

        private static void TestKeyedPatchAndReorder()
        {
            XamlRuntime runtime = LoadSimpleItemsControl(null);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");

                ItemRow first = new ItemRow("first", 1, "First");
                ItemRow second = new ItemRow("second", 1, "Second");
                ArrayList rows = new ArrayList();
                rows.Add(first);
                rows.Add(second);

                host.SetItems(rows);

                Label firstControl = GetItemLabel(host, first);
                Label secondControl = GetItemLabel(host, second);

                rows.Clear();
                rows.Add(second);
                rows.Add(first);
                host.ReloadItems();

                AssertSame(firstControl, GetItemLabel(host, first), "first keyed control");
                AssertSame(secondControl, GetItemLabel(host, second), "second keyed control");

                first.Text = "First updated";
                host.ReloadItems();

                AssertSame(firstControl, GetItemLabel(host, first), "patched control");
                AssertEqual("First updated", firstControl.Text, "patched text");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestDuplicateKeyFifoReuse()
        {
            XamlRuntime runtime = LoadSimpleItemsControl(null);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ItemRow oldFirst =
                    new ItemRow("duplicate", 1, "Old first");
                ItemRow oldSecond =
                    new ItemRow("duplicate", 1, "Old second");
                ArrayList oldRows = new ArrayList();
                oldRows.Add(oldFirst);
                oldRows.Add(oldSecond);
                host.SetItems(oldRows);

                Label firstControl = GetItemLabel(host, oldFirst);
                Label secondControl = GetItemLabel(host, oldSecond);

                ItemRow newFirst =
                    new ItemRow("duplicate", 1, "New first");
                ItemRow newSecond =
                    new ItemRow("duplicate", 1, "New second");
                ArrayList newRows = new ArrayList();
                newRows.Add(newFirst);
                newRows.Add(newSecond);
                host.SetItems(newRows);

                AssertSame(
                    firstControl,
                    GetItemLabel(host, newFirst),
                    "first duplicate key retains first-in control");
                AssertSame(
                    secondControl,
                    GetItemLabel(host, newSecond),
                    "second duplicate key retains second-in control");
                AssertEqual(
                    "New first",
                    firstControl.Text,
                    "first duplicate control is patched");
                AssertEqual(
                    "New second",
                    secondControl.Text,
                    "second duplicate control is patched");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestUniqueKeyBucketsAvoidPerItemQueues()
        {
            Type recordType = typeof(XamlRuntime).GetNestedType(
                "RenderedItemRecord",
                BindingFlags.NonPublic);
            MethodInfo buildBuckets = typeof(XamlRuntime).GetMethod(
                "BuildOldItemBuckets",
                BindingFlags.NonPublic | BindingFlags.Static);
            MethodInfo takeRecord = typeof(XamlRuntime).GetMethod(
                "TakeOldItemRecord",
                BindingFlags.NonPublic | BindingFlags.Static);

            AssertTrue(recordType != null, "rendered item record type exists");
            AssertTrue(buildBuckets != null, "old-item bucket builder exists");
            AssertTrue(takeRecord != null, "old-item bucket take helper exists");

            FieldInfo keyField = recordType.GetField(
                "Key",
                BindingFlags.Instance | BindingFlags.Public);
            AssertTrue(keyField != null, "rendered item key field exists");

            object unique = Activator.CreateInstance(recordType, true);
            object duplicateFirst = Activator.CreateInstance(recordType, true);
            object duplicateSecond = Activator.CreateInstance(recordType, true);

            keyField.SetValue(unique, "unique");
            keyField.SetValue(duplicateFirst, "duplicate");
            keyField.SetValue(duplicateSecond, "duplicate");

            ArrayList records = new ArrayList();
            records.Add(unique);
            records.Add(duplicateFirst);
            records.Add(duplicateSecond);

            Hashtable buckets = (Hashtable)buildBuckets.Invoke(
                null,
                new object[] { records });

            AssertSame(
                unique,
                buckets["unique"],
                "a unique key stores its record without a Queue");
            AssertTrue(
                buckets["duplicate"] is Queue,
                "a duplicate key promotes its records to a FIFO Queue");
            AssertSame(
                unique,
                takeRecord.Invoke(
                    null,
                    new object[] { buckets, "unique" }),
                "unique keyed record is returned directly");
            AssertSame(
                duplicateFirst,
                takeRecord.Invoke(
                    null,
                    new object[] { buckets, "duplicate" }),
                "first duplicate retains FIFO order");
            AssertSame(
                duplicateSecond,
                takeRecord.Invoke(
                    null,
                    new object[] { buckets, "duplicate" }),
                "second duplicate retains FIFO order");
        }

        private static void TestVersionAndForcedRebuild()
        {
            XamlRuntime runtime = LoadSimpleItemsControl("Version");

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");

                host.ReevaluateFunctionsOnRefresh = false;

                ItemRow row = new ItemRow("one", 1, "Initial");
                ArrayList rows = new ArrayList();
                rows.Add(row);
                host.SetItems(rows);

                Label original = GetItemLabel(host, row);

                row.Text = "Ignored until version changes";
                host.ReloadItems();
                AssertEqual("Initial", original.Text, "unchanged version fast path");

                row.Version++;
                host.ReloadItems();
                AssertSame(original, GetItemLabel(host, row), "versioned patch reuse");
                AssertEqual(
                    "Ignored until version changes",
                    original.Text,
                    "changed version text");

                row.Text = "Forced";
                host.ForceReloadItems();

                Label rebuilt = GetItemLabel(host, row);
                AssertNotSame(original, rebuilt, "forced replacement");
                AssertEqual("Forced", rebuilt.Text, "forced text");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestStableVersionPathReadOncePerNonVirtualRefresh()
        {
            XamlRuntime runtime = LoadSimpleItemsControl("Version");

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                host.ReevaluateFunctionsOnRefresh = false;

                CountingVersionRow row =
                    new CountingVersionRow("counted", 1, "Counted");
                ArrayList rows = new ArrayList();
                rows.Add(row);
                host.SetItems(rows);

                Label original = GetItemLabel(host, row);
                row.VersionReads = 0;
                host.ReloadItems();

                AssertEqual(
                    1,
                    row.VersionReads,
                    "stable ItemVersionPath getter is resolved once");
                AssertSame(
                    original,
                    GetItemLabel(host, row),
                    "stable version retains the realized control");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestVirtualRealizationReusesCapturedVersion()
        {
            const string markup =
                "<ItemsControl Name='Rows' Width='180' Height='100' AutoScroll='true' " +
                "ItemKeyPath='Id' ItemVersionPath='Version' Virtualizing='true' " +
                "VirtualizationThreshold='1' OverscanItems='1' FixedItemSize='20' " +
                "ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label AutoSize='true' Text='{Binding Text}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                host.ReevaluateFunctionsOnRefresh = false;

                ArrayList rows = new ArrayList();
                int i;

                for (i = 0; i < 20; i++)
                {
                    rows.Add(
                        new CountingVersionRow(
                            "virtual-" + i,
                            1,
                            "Virtual " + i));
                }

                host.CreateControl();
                host.SetItems(rows);

                for (i = 0; i < rows.Count; i++)
                    ((CountingVersionRow)rows[i]).VersionReads = 0;

                host.ReloadItems();

                AssertTrue(
                    host.RealizedCount > 0,
                    "virtual version fixture realizes a viewport");

                int inspected = 0;

                for (i = 0; i < rows.Count; i++)
                {
                    int reads =
                        ((CountingVersionRow)rows[i]).VersionReads;

                    AssertTrue(
                        reads == 0 || reads == 1,
                        "a realized version is captured at most once");

                    if (reads != 0)
                        inspected++;
                }

                AssertTrue(inspected > 0, "visible versions are inspected");
                AssertTrue(
                    inspected < rows.Count,
                    "off-screen versions remain lazy");

                CountingVersionRow finalRow =
                    (CountingVersionRow)rows[rows.Count - 1];
                AssertEqual(0, finalRow.VersionReads, "far version starts uncaptured");

                host.ScrollToIndex(rows.Count - 1);

                AssertEqual(
                    1,
                    finalRow.VersionReads,
                    "far version is captured when realized");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestClearItems()
        {
            XamlRuntime runtime = LoadSimpleItemsControl(null);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ArrayList rows = new ArrayList();
                rows.Add(new ItemRow("one", 1, "One"));
                rows.Add(new ItemRow("two", 1, "Two"));

                host.SetItems(rows);
                AssertEqual(2, host.RealizedCount, "initial realized count");

                host.ClearItems();
                AssertEqual(0, host.Count, "cleared data count");
                AssertEqual(0, host.RealizedCount, "cleared realized count");
                AssertEqual(0, CountItemLabels(host), "cleared label controls");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestEqualCustomControlOwnership()
        {
            const string markup =
                "<ItemsControl Name='Rows' ItemKeyPath='Id' " +
                "Virtualizing='false' ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <EqualLifecycleControl />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ItemRow first = new ItemRow("first", 1, "First");
                ItemRow second = new ItemRow("second", 1, "Second");
                ArrayList rows = new ArrayList();
                rows.Add(first);
                rows.Add(second);

                host.SetItems(rows);

                EqualLifecycleControl oldFirst =
                    GetEqualLifecycleControl(host, first);
                EqualLifecycleControl oldSecond =
                    GetEqualLifecycleControl(host, second);

                AssertNotSame(
                    oldFirst,
                    oldSecond,
                    "initial equal controls are distinct objects");

                host.ForceReloadItems();

                EqualLifecycleControl newFirst =
                    GetEqualLifecycleControl(host, first);
                EqualLifecycleControl newSecond =
                    GetEqualLifecycleControl(host, second);

                AssertNotSame(
                    oldFirst,
                    newFirst,
                    "forced refresh replaces first equal control");
                AssertNotSame(
                    oldSecond,
                    newSecond,
                    "forced refresh replaces second equal control");
                AssertTrue(
                    oldFirst.IsDisposed,
                    "first replaced equal control is disposed");
                AssertTrue(
                    oldSecond.IsDisposed,
                    "second replaced equal control is disposed");
                AssertEqual(
                    2,
                    CountEqualLifecycleControls(host),
                    "only the two current equal controls remain attached");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestFailedEqualCustomControlCleanup()
        {
            const string markup =
                "<ItemsControl Name='Rows' ItemKeyPath='Id' " +
                "Virtualizing='false' ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <EqualLifecycleControl Text='{Binding Text}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ArrayList committedRows = new ArrayList();
                host.SetItems(committedRows);

                host.ProgressiveRendering = true;
                host.ProgressiveInterval = 60000;
                host.ProgressiveBatchSize = 1;

                ArrayList failingRows = new ArrayList();
                failingRows.Add(new ItemRow("first", 1, "First"));
                failingRows.Add(new ItemRow("second", 1, "Second"));
                failingRows.Add(new ThrowingTextRow("broken"));

                EqualLifecycleControl.ResetLifetimeCounts();
                host.SetItems(failingRows);

                object state = GetPendingRefreshState(host);
                MethodInfo buildMethod = GetRuntimeMethod(
                    "BuildItemsRefreshBatch");
                MethodInfo failMethod = GetRuntimeMethod(
                    "FailItemsRefresh");
                Exception buildError = null;

                try
                {
                    buildMethod.Invoke(
                        runtime,
                        new object[] { state, 3 });
                }
                catch (TargetInvocationException ex)
                {
                    buildError = ex.InnerException == null
                        ? ex
                        : ex.InnerException;
                }

                AssertTrue(buildError != null, "equal-control refresh fails");
                AssertEqual(
                    2,
                    EqualLifecycleControl.CreatedCount,
                    "two equal controls were constructed before failure");

                failMethod.Invoke(
                    runtime,
                    new object[] { state, buildError, false });

                AssertEqual(
                    2,
                    EqualLifecycleControl.DisposedCount,
                    "both distinct equal controls are disposed during rollback");
                AssertEqual(
                    0,
                    CountEqualLifecycleControls(host),
                    "failed equal controls do not remain attached");
                AssertSame(
                    committedRows,
                    GetItemSource(host),
                    "failed equal-control refresh restores committed source");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestPropertyElementBinding()
        {
            const string markup =
                "<ItemsControl Name='Rows' ItemKeyPath='Id' " +
                "Virtualizing='false' ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label AutoSize='true'><Label.Text>{Binding Text}</Label.Text></Label>" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ItemRow row = new ItemRow("one", 1, "Before");
                ArrayList rows = new ArrayList();
                rows.Add(row);

                host.SetItems(rows);
                Label label = GetItemLabel(host, row);
                AssertEqual("Before", label.Text, "initial property-element text");

                row.Text = "After";
                host.ReloadItems();
                AssertSame(label, GetItemLabel(host, row), "property-element reuse");
                AssertEqual("After", label.Text, "refreshed property-element text");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestNestedTemplateBoundary()
        {
            const string markup =
                "<ItemsControl Name='Rows' ItemKeyPath='Id' " +
                "Virtualizing='false' ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <ItemsControl Virtualizing='false' ProgressiveRendering='false'>" +
                "      <ItemsControl.ItemTemplate>" +
                "        <Label AutoSize='true' Text='{Binding ChildText}' />" +
                "      </ItemsControl.ItemTemplate>" +
                "    </ItemsControl>" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ArrayList rows = new ArrayList();
                rows.Add(new ItemRow("outer", 1, "Outer"));

                host.SetItems(rows);
                AssertEqual(1, host.RealizedCount, "outer realized count");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestPresetRootCondition()
        {
            const string markup =
                "<Panel>" +
                "  <Presets Name='View' Selected='Hidden'>" +
                "    <Preset Name='Hidden'><Set Key='Show' Value='false' /></Preset>" +
                "    <Preset Name='Visible'><Set Key='Show' Value='true' /></Preset>" +
                "  </Presets>" +
                "  <ItemsControl Name='Rows' ItemKeyPath='Id' " +
                "  Virtualizing='false' ProgressiveRendering='false'>" +
                "    <ItemsControl.ItemTemplate>" +
                "      <Label AutoSize='true' Condition='{Preset View.Show}' Text='{Binding Text}' />" +
                "    </ItemsControl.ItemTemplate>" +
                "  </ItemsControl>" +
                "</Panel>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                FieldInfo templateTextField =
                    typeof(XamlRuntime.ItemsControl).GetField(
                        "TemplateOuterXml",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                string templateText =
                    templateTextField == null
                        ? null
                        : templateTextField.GetValue(host) as string;

                AssertTrue(
                    templateText != null &&
                    templateText.IndexOf(
                        "{Preset View.Show}",
                        StringComparison.Ordinal) >= 0,
                    "preset fan-out retains precomputed item template text");

                ArrayList rows = new ArrayList();
                rows.Add(new ItemRow("one", 1, "One"));
                host.SetItems(rows);

                Label hidden = GetItemLabel(host, rows[0]);
                AssertEqual(1, host.RealizedCount, "retained hidden control count");
                AssertTrue(!hidden.Visible, "preset-false root starts hidden");

                runtime.RootControl.CreateControl();
                runtime.Presets.Select("View", "Visible");

                AssertEqual(1, host.RealizedCount, "restored realized count");
                AssertEqual("One", GetItemLabel(host, rows[0]).Text, "restored item text");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestPresetStyleSetterRefresh()
        {
            const string markup =
                "<Panel Background='Yellow'>" +
                "  <Panel.Resources>" +
                "    <Style TargetType='Label'>" +
                "      <Setter Property='Foreground' " +
                "              Value='{Preset Theme.Foreground}' />" +
                "    </Style>" +
                "  </Panel.Resources>" +
                "  <Presets Name='Theme' Selected='Dark' Default='Default'>" +
                "    <Preset Name='Default'>" +
                "      <Set Key='Foreground' Value='Blue' />" +
                "    </Preset>" +
                "    <Preset Name='Light'>" +
                "      <Set Key='Other' Value='Light' />" +
                "    </Preset>" +
                "    <Preset Name='Dark'>" +
                "      <Set Key='Background' Value='Black' />" +
                "      <Set Key='Foreground' Value='White' />" +
                "    </Preset>" +
                "  </Presets>" +
                "  <ItemsControl Name='Rows' ItemKeyPath='Id' " +
                "  Virtualizing='false' ProgressiveRendering='false'>" +
                "    <ItemsControl.ItemTemplate>" +
                "      <Label AutoSize='true' Text='{Binding Text}' " +
                "             Background='{Preset Theme.Background}' />" +
                "    </ItemsControl.ItemTemplate>" +
                "  </ItemsControl>" +
                "</Panel>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ItemRow row = new ItemRow("one", 1, "One");
                ArrayList rows = new ArrayList();
                rows.Add(row);
                host.SetItems(rows);

                Label original = GetItemLabel(host, row);
                AssertEqual(
                    System.Drawing.Color.White,
                    original.ForeColor,
                    "initial dark implicit style preset");
                AssertEqual(
                    System.Drawing.Color.Black,
                    original.BackColor,
                    "initial dark direct preset");

                runtime.RootControl.CreateControl();
                runtime.Presets.Select("Theme", "Light");

                Label refreshed = GetItemLabel(host, row);
                AssertEqual(
                    System.Drawing.Color.Blue,
                    refreshed.ForeColor,
                    "light miss uses the configured default preset");
                AssertEqual(
                    System.Drawing.Color.Yellow,
                    refreshed.BackColor,
                    "light and default miss unset the old dark value");

                runtime.Presets.Select("Theme", "Dark");
                runtime.Presets.Select("Theme", "Light");

                Label restoredAgain = GetItemLabel(host, row);
                AssertEqual(
                    System.Drawing.Color.Blue,
                    restoredAgain.ForeColor,
                    "repeated light miss still uses the default preset");
                AssertEqual(
                    System.Drawing.Color.Yellow,
                    restoredAgain.BackColor,
                    "repeated light and default miss restores the baseline");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestReactiveItemRootCondition()
        {
            const string markup =
                "<ItemsControl Name='Rows' ItemKeyPath='Id' " +
                "Virtualizing='false' ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label AutoSize='true' Condition='{Binding Show}' " +
                "           Text='{Binding Text}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ReactiveItemRow row =
                    new ReactiveItemRow("one", "Reactive row", false);
                ArrayList rows = new ArrayList();
                rows.Add(row);

                CreateHandleAndDrainReactiveCallbacks(
                    runtime.RootControl);
                host.SetItems(rows);

                Label hidden = GetItemLabel(host, row);
                AssertTrue(!hidden.Visible, "reactive false row starts hidden");
                AssertEqual(
                    1,
                    GetPropertyBindingSubscriberCount(row.Show),
                    "hidden row condition remains subscribed");

                row.Show.Value = true;
                DrainReactiveCallbacks(runtime.RootControl);

                Label restored = GetItemLabel(host, row);
                AssertSame(hidden, restored, "reactive condition reuses row control");
                AssertTrue(restored.Visible, "reactive false-to-true row is visible");

                row.Text.Value = "Reactive row updated";
                DrainReactiveCallbacks(runtime.RootControl);
                AssertEqual(
                    "Reactive row updated",
                    restored.Text,
                    "restored row keeps reactive value binding");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestReactiveRootConditionUsesKeyedFallback()
        {
            const string markup =
                "<ItemsControl Name='Rows' Width='180' Height='100' AutoScroll='true' " +
                "ItemKeyPath='Id' ItemVersionPath='Version' Virtualizing='true' VirtualizationThreshold='1' " +
                "OverscanItems='0' FixedItemSize='20' ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label AutoSize='true' Condition='{Binding Show}' " +
                "           Text='{Binding Text}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ReactiveItemRow row =
                    new ReactiveItemRow("one", "Virtual reactive row", false);
                ArrayList rows = new ArrayList();
                rows.Add(row);

                CreateHandleAndDrainReactiveCallbacks(
                    runtime.RootControl);
                host.SetItems(rows);

                AssertTrue(
                    !host.IsVirtualizing,
                    "a root Condition selects the normal keyed renderer");
                AssertEqual(
                    1,
                    host.RealizedCount,
                    "the keyed renderer retains the hidden row control");
                Label hidden = GetItemLabel(host, row);
                AssertTrue(
                    !hidden.Visible,
                    "a false root Condition hides the retained row");
                AssertEqual(
                    1,
                    GetPropertyBindingSubscriberCount(row.Show),
                    "the keyed root condition remains subscribed");

                row.Show.Value = true;
                DrainReactiveCallbacks(runtime.RootControl);

                Label restored = GetItemLabel(host, row);
                AssertSame(
                    hidden,
                    restored,
                    "the keyed fallback keeps the row control");
                AssertTrue(restored.Visible, "false-to-true row is visible");
                AssertTrue(
                    host.AutoScrollMinSize.Height > 0,
                    "virtual false-to-true row restores extent");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestComponentRootConditionsUseKeyedFallback()
        {
            XamlRuntime.Register(
                Assembly.GetExecutingAssembly(),
                "WinFormsXaml.ItemsTests.Fixtures.VirtualConditionalCard.xml");

            const string markup =
                "<ItemsControl Name='Rows' Width='180' Height='100' AutoScroll='true' " +
                "ItemKeyPath='Id' ItemVersionPath='Version' Virtualizing='true' VirtualizationThreshold='1' " +
                "OverscanItems='0' FixedItemSize='20' ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <VirtualConditionalCard Text='{Binding Text}' " +
                "        TemplateShow='{Binding TemplateShow}' " +
                "        Condition='{Binding InvocationShow}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ReactiveComponentConditionRow row =
                    new ReactiveComponentConditionRow(
                        "component-row",
                        "Layered component",
                        false,
                        true);
                ArrayList rows = new ArrayList();
                rows.Add(row);

                CreateHandleAndDrainReactiveCallbacks(
                    runtime.RootControl);
                host.SetItems(rows);

                AssertTrue(
                    !host.IsVirtualizing,
                    "a component root Condition selects keyed fallback");
                AssertEqual(
                    1,
                    host.RealizedCount,
                    "keyed fallback retains one component tree");
                Label componentLabel = GetItemLabel(host, row);
                AssertTrue(
                    !componentLabel.Visible,
                    "false component-template condition hides the component");
                AssertTrue(
                    GetPropertyBindingSubscriberCount(row.TemplateShow) > 0,
                    "hidden component-template condition remains subscribed");
                AssertTrue(
                    GetPropertyBindingSubscriberCount(row.InvocationShow) > 0,
                    "hidden invocation condition remains subscribed");

                row.TemplateShow.Value = true;
                DrainReactiveCallbacks(runtime.RootControl);

                AssertSame(
                    componentLabel,
                    GetItemLabel(host, row),
                    "condition changes reuse the keyed component tree");
                AssertTrue(
                    componentLabel.Visible,
                    "both true component conditions show the item");

                row.InvocationShow.Value = false;
                DrainReactiveCallbacks(runtime.RootControl);

                AssertTrue(
                    !componentLabel.Visible,
                    "false invocation condition dominates true template condition");

                row.TemplateShow.Value = false;
                DrainReactiveCallbacks(runtime.RootControl);
                row.InvocationShow.Value = true;
                DrainReactiveCallbacks(runtime.RootControl);

                AssertTrue(
                    !componentLabel.Visible,
                    "true invocation cannot override false template condition");

                row.TemplateShow.Value = true;
                DrainReactiveCallbacks(runtime.RootControl);

                AssertTrue(
                    componentLabel.Visible,
                    "restoring both conditions shows the retained component");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void
            TestStableVersionReusesComponentConditionDependencies()
        {
            XamlRuntime.Register(
                Assembly.GetExecutingAssembly(),
                "WinFormsXaml.ItemsTests.Fixtures.VirtualConditionalCard.xml");

            const string markup =
                "<ItemsControl Name='Rows' Width='180' Height='100' AutoScroll='true' " +
                "ItemKeyPath='Id' ItemVersionPath='Version' Virtualizing='true' " +
                "VirtualizationThreshold='1' OverscanItems='0' FixedItemSize='20' " +
                "ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <VirtualConditionalCard Text='{Binding Text}' " +
                "        TemplateShow='{Binding TemplateShow}' " +
                "        Condition='{Binding InvocationShow}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ArrayList rows = new ArrayList();
                VersionedComponentGetterRow target = null;
                int i;

                for (i = 0; i < 40; i++)
                {
                    VersionedComponentGetterRow row =
                        new VersionedComponentGetterRow(
                            "stable-component-" + i,
                            "Stable component " + i);
                    rows.Add(row);

                    if (i == 30)
                        target = row;
                }

                CreateHandleAndDrainReactiveCallbacks(
                    runtime.RootControl);
                host.SetItems(rows);

                AssertTrue(
                    !host.IsVirtualizing,
                    "expanded component root Condition selects keyed fallback");
                Label targetLabel = GetItemLabel(host, target);
                AssertTrue(
                    targetLabel.Visible,
                    "the target is realized by the normal keyed renderer");
                AssertTrue(
                    target.TemplateShowReadCount > 0,
                    "initial component output reads the condition source");
                AssertTrue(
                    GetPropertyBindingSubscriberCount(
                        target.TemplateShowSource) > 0,
                    "initial component condition dependency is subscribed");

                int initialReadCount = target.TemplateShowReadCount;

                host.ReloadItems();

                AssertEqual(
                    initialReadCount,
                    target.TemplateShowReadCount,
                    "unchanged version does not reread component conditions");
                AssertSame(
                    targetLabel,
                    GetItemLabel(host, target),
                    "unchanged keyed output retains its control");
                AssertTrue(
                    GetPropertyBindingSubscriberCount(
                        target.TemplateShowSource) > 0,
                    "unchanged version retains the committed condition subscription");

                VersionedComponentGetterRow replacement =
                    new VersionedComponentGetterRow(
                        target.Id,
                        "Equal-version replacement");
                rows[30] = replacement;
                host.ReloadItems();

                AssertTrue(
                    replacement.TemplateShowReadCount > 0,
                    "equal-version replacement rebuilds component dependencies");
                Label replacementLabel =
                    GetItemLabel(host, replacement);
                AssertTrue(
                    GetPropertyBindingSubscriberCount(
                        replacement.TemplateShowSource) > 0,
                    "replacement component condition source is subscribed");
                AssertEqual(
                    0,
                    GetPropertyBindingSubscriberCount(
                        target.TemplateShowSource),
                    "replaced component condition source is detached");

                int replacementReadCount =
                    replacement.TemplateShowReadCount;
                replacement.TemplateShowSource.Value = false;
                DrainReactiveCallbacks(runtime.RootControl);

                AssertTrue(
                    replacement.TemplateShowReadCount >
                        replacementReadCount,
                    "observable invalidation reevaluates the component condition");
                AssertTrue(
                    !replacementLabel.Visible,
                    "retained dependency updates keyed component visibility");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestCachedReactiveItemReactivation()
        {
            const string markup =
                "<ItemsControl Name='Rows' Width='180' Height='100' AutoScroll='true' " +
                "ItemKeyPath='Id' Virtualizing='true' VirtualizationThreshold='1' " +
                "OverscanItems='1' FixedItemSize='20' VirtualizationCacheItems='64' " +
                "ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label AutoSize='true' Text='{Binding Text}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ArrayList rows = new ArrayList();
                int i;

                for (i = 0; i < 100; i++)
                {
                    rows.Add(
                        new ReactiveItemRow(
                            "reactive-" + i,
                            "Reactive " + i,
                            true));
                }

                ReactiveItemRow first =
                    (ReactiveItemRow)rows[0];

                CreateHandleAndDrainReactiveCallbacks(
                    runtime.RootControl);
                host.SetItems(rows);

                Label original = GetItemLabel(host, first);
                AssertEqual(
                    1,
                    GetPropertyBindingSubscriberCount(first.Text),
                    "realized virtual row is subscribed");

                host.ScrollToIndex(80);

                AssertTrue(host.VirtualCacheCount > 0, "reactive row enters cache");
                AssertTrue(
                    !original.Visible || original.Bounds.IsEmpty,
                    "cached reactive row is offscreen");
                AssertEqual(
                    0,
                    GetPropertyBindingSubscriberCount(first.Text),
                    "cached reactive row detaches subscription");

                first.Text.Value = "Changed while cached";
                host.ScrollToIndex(0);

                Label reactivated = GetItemLabel(host, first);
                AssertSame(original, reactivated, "cached row control is reused");
                AssertTrue(reactivated.Visible, "cached row becomes visible again");
                AssertEqual(
                    "Changed while cached",
                    reactivated.Text,
                    "reactivation resolves latest cached value");
                AssertEqual(
                    1,
                    GetPropertyBindingSubscriberCount(first.Text),
                    "reactivated row resubscribes once");

                first.Text.Value = "Changed after reactivation";
                DrainReactiveCallbacks(runtime.RootControl);
                AssertEqual(
                    "Changed after reactivation",
                    reactivated.Text,
                    "reactivated row receives later source changes");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestObservableItemsBindingMutations()
        {
            const string markup =
                "<ItemsControl Name='Rows' ItemsSource='{Binding Items}' " +
                "ItemKeyPath='Id' Virtualizing='false' ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label AutoSize='true' Text='{Binding Text}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            ObservableItemsState state = new ObservableItemsState();
            ItemRow first = new ItemRow("first", 1, "First");
            ItemRow second = new ItemRow("second", 1, "Second");
            state.Items.Add(first);
            state.Items.Add(second);

            XamlRuntime runtime = XamlRuntime.Load(markup, state);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                int completionCount = 0;

                CreateHandleAndDrainReactiveCallbacks(
                    runtime.RootControl);
                AssertEqual(2, host.Count, "initial ItemsBinding count");
                AssertEqual("First", GetItemLabel(host, first).Text, "initial first row");
                AssertEqual("Second", GetItemLabel(host, second).Text, "initial second row");
                host.RefreshCompleted +=
                    delegate(object sender, EventArgs e)
                    {
                        completionCount++;
                    };

                ItemRow added = new ItemRow("added", 1, "Added");
                int before = completionCount;
                state.Items.Add(added);
                DrainReactiveCallbacks(runtime.RootControl);
                AssertTrue(completionCount > before, "ItemsBinding Add refreshes");
                AssertEqual(3, host.Count, "ItemsBinding Add count");
                AssertEqual("Added", GetItemLabel(host, added).Text, "added row text");

                before = completionCount;
                state.Items.Remove(first);
                DrainReactiveCallbacks(runtime.RootControl);
                AssertTrue(completionCount > before, "ItemsBinding Remove refreshes");
                AssertEqual(2, host.Count, "ItemsBinding Remove count");
                AssertEqual(null, GetItemLabelOrNull(host, first), "removed row released");

                ItemRow replacement =
                    new ItemRow("replacement", 1, "Replacement");
                before = completionCount;
                state.Items[0] = replacement;
                DrainReactiveCallbacks(runtime.RootControl);
                AssertTrue(completionCount > before, "ItemsBinding replace refreshes");
                AssertEqual(
                    null,
                    GetItemLabelOrNull(host, second),
                    "replaced row released");

                Label replacementLabel = GetItemLabel(host, replacement);
                replacement.Text = "Replacement after reset";
                before = completionCount;
                state.Items.ReloadItems();
                DrainReactiveCallbacks(runtime.RootControl);
                AssertTrue(
                    completionCount > before,
                    "ItemsBinding ReloadItems refreshes");
                AssertSame(
                    replacementLabel,
                    GetItemLabel(host, replacement),
                    "ReloadItems patches existing row");
                AssertEqual(
                    "Replacement after reset",
                    replacementLabel.Text,
                    "ReloadItems re-reads ordinary item value");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestItemsBindingManualReloadApis()
        {
            const string markup =
                "<ItemsControl Name='Rows' ItemsSource='{Binding Items}' " +
                "ItemKeyPath='Id' Virtualizing='false' ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label AutoSize='true' Text='{Function Format(.)}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            ManualReloadItemsState state =
                new ManualReloadItemsState();
            ItemRow first = new ItemRow("first", 1, "First");
            ItemRow second = new ItemRow("second", 1, "Second");
            state.Items.Add(first);
            state.Items.Add(second);

            XamlRuntime runtime = XamlRuntime.Load(markup, state);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                CreateHandleAndDrainReactiveCallbacks(
                    runtime.RootControl);

                Label firstLabel = GetItemLabel(host, first);
                Label secondLabel = GetItemLabel(host, second);
                state.FormatCalls = 0;

                first.Text = "First changed";
                state.Suffix = " once";
                state.Items.ReloadItem(0);
                DrainReactiveCallbacks(runtime.RootControl);

                AssertEqual(
                    "First changed once",
                    firstLabel.Text,
                    "ReloadItem re-evaluates the requested row");
                AssertEqual(
                    "Second",
                    secondLabel.Text,
                    "ReloadItem leaves an unaffected row unchanged");
                AssertEqual(
                    1,
                    state.FormatCalls,
                    "ReloadItem invokes the Function only for its row");
                AssertSame(
                    firstLabel,
                    GetItemLabel(host, first),
                    "ReloadItem retains the requested row control");
                AssertSame(
                    secondLabel,
                    GetItemLabel(host, second),
                    "ReloadItem retains an unaffected row control");

                state.Suffix = " all";
                state.Items.ReloadItems();
                DrainReactiveCallbacks(runtime.RootControl);

                AssertEqual(
                    "First changed all",
                    firstLabel.Text,
                    "ReloadItems re-evaluates the first row");
                AssertEqual(
                    "Second all",
                    secondLabel.Text,
                    "ReloadItems re-evaluates every row");
                AssertEqual(
                    3,
                    state.FormatCalls,
                    "ReloadItems invokes the Function once per row");
                AssertSame(
                    firstLabel,
                    GetItemLabel(host, first),
                    "ReloadItems patches keyed controls in place");
                AssertSame(
                    secondLabel,
                    GetItemLabel(host, second),
                    "ReloadItems retains unaffected keyed controls");

                bool invalidIndexRejected = false;

                try
                {
                    state.Items.ReloadItem(state.Items.Count);
                }
                catch (ArgumentOutOfRangeException)
                {
                    invalidIndexRejected = true;
                }

                AssertTrue(
                    invalidIndexRejected,
                    "ReloadItem rejects an index outside the logical list");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestObservedItemChangedPatchesOnlyChangedRecords()
        {
            const string markup =
                "<ItemsControl Name='Rows' ItemKeyPath='Id' " +
                "Virtualizing='false' ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label AutoSize='true' Text='{Binding Text}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            CountingListChangedRow first =
                new CountingListChangedRow("first", "First");
            CountingListChangedRow second =
                new CountingListChangedRow("second", "Second");
            CountingListChangedRow untouched =
                new CountingListChangedRow("untouched", "Untouched");
            CountingItemChangedList source =
                new CountingItemChangedList();
            source.Add(first);
            source.Add(second);
            source.Add(untouched);

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");

                CreateHandleAndDrainReactiveCallbacks(
                    runtime.RootControl);
                host.ItemsSource = source;
                DrainReactiveCallbacks(runtime.RootControl);

                Label firstLabel = GetItemLabel(host, first);
                Label secondLabel = GetItemLabel(host, second);
                int untouchedReads = untouched.TextReads;
                int completionCount = 0;

                host.RefreshCompleted +=
                    delegate(object sender, EventArgs e)
                    {
                        completionCount++;
                    };

                source.ResetEnumerationCount();
                first.Text = "First changed";
                second.Text = "Second changed";
                DrainReactiveCallbacks(runtime.RootControl);

                AssertEqual(
                    0,
                    source.GetEnumeratorCount,
                    "coalesced ItemChanged notifications skip source enumeration");
                AssertEqual(
                    "First changed",
                    firstLabel.Text,
                    "first changed record is patched");
                AssertEqual(
                    "Second changed",
                    secondLabel.Text,
                    "second changed record is patched");
                AssertEqual(
                    untouchedReads,
                    untouched.TextReads,
                    "an unchanged sibling is not re-evaluated");
                AssertSame(
                    firstLabel,
                    GetItemLabel(host, first),
                    "first changed record retains its control tree");
                AssertSame(
                    secondLabel,
                    GetItemLabel(host, second),
                    "second changed record retains its control tree");
                AssertEqual(
                    1,
                    completionCount,
                    "coalesced ItemChanged notifications publish one completion");

                CountingListChangedRow replacement =
                    new CountingListChangedRow(
                        "first",
                        "Replacement");
                source.ResetEnumerationCount();
                source[0] = replacement;
                DrainReactiveCallbacks(runtime.RootControl);

                AssertTrue(
                    source.GetEnumeratorCount == 0,
                    "replacement ItemChanged reuses its exact event snapshot");
                AssertEqual(
                    "Replacement",
                    GetItemLabel(host, replacement).Text,
                    "replacement ItemChanged updates the DataContext");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void
            TestObservedDuplicatePropertyChangesUseOnePreciseBatch()
        {
            RunObservedPropertyChangeOptimizationScenario(
                40,
                40,
                2,
                "40-row duplicate property batch");
        }

        private static void TestObservedFortyOfThousandChangesStayPrecise()
        {
            RunObservedPropertyChangeOptimizationScenario(
                1000,
                40,
                1,
                "40-of-1000 property batch");
        }

        private static void RunObservedPropertyChangeOptimizationScenario(
            int rowCount,
            int changedCount,
            int writesPerRow,
            string message)
        {
            const string markup =
                "<ItemsControl Name='Rows' ItemsSource='{Binding Items}' " +
                "ItemKeyPath='Id' Virtualizing='false' " +
                "ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label AutoSize='true' " +
                "Text='{Function Format(Text)}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            ObservedChangeOptimizationState state =
                new ObservedChangeOptimizationState();
            int i;

            for (i = 0; i < rowCount; i++)
            {
                state.Items.Add(
                    new CountingListChangedRow(
                        "row-" + i,
                        "initial-" + i));
            }

            XamlRuntime runtime = XamlRuntime.Load(markup, state);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                CreateHandleAndDrainReactiveCallbacks(
                    runtime.RootControl);
                state.EvaluationCount = 0;
                state.Items.ResetEnumerationCount();
                host.ResetItemUpdatePostDiagnosticsForTest();
                int completionCount = 0;

                host.RefreshCompleted +=
                    delegate
                    {
                        completionCount++;
                    };

                for (i = 0; i < changedCount; i++)
                {
                    int index = rowCount == changedCount
                        ? i
                        : (i * 23) % rowCount;
                    CountingListChangedRow row = state.Items[index];
                    int write;

                    for (write = 0; write < writesPerRow; write++)
                    {
                        row.Text =
                            "changed-" + index + "-" + write;
                    }
                }

                AssertEqual(
                    1L,
                    host.ItemSourceReloadPostCountForTest,
                    message + " coalesces the list-change post");
                AssertEqual(
                    1L,
                    host.ReactiveItemUpdatePostCountForTest,
                    message + " coalesces the reactive-slot post");

                DrainReactiveCallbacks(runtime.RootControl);

                AssertEqual(
                    changedCount,
                    state.EvaluationCount,
                    message + " evaluates each affected Function once");
                AssertEqual(
                    0,
                    state.Items.GetEnumeratorCount,
                    message + " avoids broad source enumeration");
                AssertEqual(
                    1,
                    completionCount,
                    message + " publishes one completion");

                for (i = 0; i < changedCount; i++)
                {
                    int index = rowCount == changedCount
                        ? i
                        : (i * 23) % rowCount;
                    CountingListChangedRow row = state.Items[index];
                    string expected =
                        "formatted: changed-" +
                        index +
                        "-" +
                        (writesPerRow - 1);

                    AssertEqual(
                        expected,
                        GetItemLabel(host, row).Text,
                        message + " updates row " + index);
                }
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestObservedStructuralChangesReuseExactSnapshots()
        {
            const string markup =
                "<ItemsControl Name='Rows' Width='240' Height='220' " +
                "ItemsSource='{Binding Items}' ItemKeyPath='Id' " +
                "Virtualizing='false' ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label AutoSize='true' Text='{Binding Text}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";
            StructuralItemsState state =
                new StructuralItemsState();
            ItemRow first = new ItemRow("first", 1, "First");
            ItemRow second = new ItemRow("second", 1, "Second");
            ItemRow third = new ItemRow("third", 1, "Third");
            state.Items.Add(first);
            state.Items.Add(second);
            state.Items.Add(third);

            XamlRuntime runtime = XamlRuntime.Load(markup, state);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                CreateHandleAndDrainReactiveCallbacks(
                    runtime.RootControl);

                Label firstLabel = GetItemLabel(host, first);
                Label secondLabel = GetItemLabel(host, second);
                Label thirdLabel = GetItemLabel(host, third);

                state.Items.ResetEnumerationCount();
                state.Items.Move(0, 2);
                DrainReactiveCallbacks(runtime.RootControl);

                AssertEqual(
                    0,
                    state.Items.GetEnumeratorCount,
                    "a verified move batch skips source enumeration");
                AssertSame(
                    firstLabel,
                    GetItemLabel(host, first),
                    "a move retains the moved control tree");
                AssertSame(
                    secondLabel,
                    GetItemLabel(host, second),
                    "a move retains the first unaffected control tree");
                AssertSame(
                    thirdLabel,
                    GetItemLabel(host, third),
                    "a move retains the second unaffected control tree");
                AssertTrue(
                    secondLabel.Top < thirdLabel.Top &&
                    thirdLabel.Top < firstLabel.Top,
                    "the move snapshot publishes the requested visual order");

                ItemRow added =
                    new ItemRow("added", 1, "Added");
                state.Items.ResetEnumerationCount();
                state.Items.Add(added);
                DrainReactiveCallbacks(runtime.RootControl);
                AssertEqual(
                    0,
                    state.Items.GetEnumeratorCount,
                    "an exact ItemAdded snapshot skips source enumeration");
                AssertEqual(
                    "Added",
                    GetItemLabel(host, added).Text,
                    "the added snapshot item is rendered");

                state.Items.ResetEnumerationCount();
                state.Items.Remove(second);
                DrainReactiveCallbacks(runtime.RootControl);
                AssertEqual(
                    0,
                    state.Items.GetEnumeratorCount,
                    "an exact ItemDeleted snapshot skips source enumeration");
                AssertEqual(
                    null,
                    GetItemLabelOrNull(host, second),
                    "the deleted snapshot item is released");

                ItemRow replacement =
                    new ItemRow("replacement", 1, "Replacement");
                ItemRow displaced = state.Items[1];
                state.Items.ResetEnumerationCount();
                state.Items[1] = replacement;
                DrainReactiveCallbacks(runtime.RootControl);
                AssertEqual(
                    0,
                    state.Items.GetEnumeratorCount,
                    "an exact replacement snapshot skips source enumeration");
                AssertEqual(
                    null,
                    GetItemLabelOrNull(host, displaced),
                    "the displaced snapshot item is released");
                AssertEqual(
                    "Replacement",
                    GetItemLabel(host, replacement).Text,
                    "the replacement snapshot updates its DataContext");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestObservableItemsSourceReplacement()
        {
            ItemsBinding<ItemRow> oldItems = new ItemsBinding<ItemRow>();
            ItemRow oldRow = new ItemRow("old", 1, "Old");
            oldItems.Add(oldRow);

            ItemsBinding<ItemRow> newItems = new ItemsBinding<ItemRow>();
            ItemRow newRow = new ItemRow("new", 1, "New");
            newItems.Add(newRow);

            ReplaceableItemsState replaceableState =
                new ReplaceableItemsState(oldItems);
            const string replaceableMarkup =
                "<ItemsControl Name='Rows' ItemsSource='{Binding Source}' " +
                "ItemKeyPath='Id' Virtualizing='false' ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label AutoSize='true' Text='{Binding Text}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            XamlRuntime replacementRuntime =
                XamlRuntime.Load(replaceableMarkup, replaceableState);

            try
            {
                XamlRuntime.ItemsControl host =
                    replacementRuntime.GetItemsControl("Rows");
                int completionCount = 0;

                CreateHandleAndDrainReactiveCallbacks(
                    replacementRuntime.RootControl);
                AssertSame(oldItems, host.ItemsSource, "initial observable source");
                AssertEqual("Old", GetItemLabel(host, oldRow).Text, "initial old row");
                host.RefreshCompleted +=
                    delegate(object sender, EventArgs e)
                    {
                        completionCount++;
                    };

                ItemRow staleBeforeDispatch =
                    new ItemRow("stale-before", 1, "Stale before dispatch");
                replaceableState.Source.Value = newItems;
                oldItems.Add(staleBeforeDispatch);
                DrainReactiveCallbacks(replacementRuntime.RootControl);

                AssertSame(newItems, host.ItemsSource, "replacement source committed");
                AssertEqual(newItems.Count, host.Count, "replacement source count");
                AssertEqual("New", GetItemLabel(host, newRow).Text, "replacement row text");
                AssertEqual(
                    null,
                    GetItemLabelOrNull(host, staleBeforeDispatch),
                    "old-list notification queued during replacement is ignored");

                int afterReplacement = completionCount;
                ItemRow staleAfterDispatch =
                    new ItemRow("stale-after", 1, "Stale after dispatch");
                oldItems.Add(staleAfterDispatch);
                DrainReactiveCallbacks(replacementRuntime.RootControl);

                AssertEqual(
                    afterReplacement,
                    completionCount,
                    "detached old list cannot schedule a refresh");
                AssertEqual(
                    null,
                    GetItemLabelOrNull(host, staleAfterDispatch),
                    "detached old-list item is ignored");

                ItemRow current = new ItemRow("current", 1, "Current");
                newItems.Add(current);
                DrainReactiveCallbacks(replacementRuntime.RootControl);
                AssertTrue(
                    completionCount > afterReplacement,
                    "replacement list remains observed");
                AssertEqual("Current", GetItemLabel(host, current).Text, "current row text");
            }
            finally
            {
                DisposeRuntime(replacementRuntime);
            }
        }

        private static void TestReactiveItemTwoWayEdit()
        {
            const string markup =
                "<ItemsControl Name='Rows' ItemsSource='{Binding Items}' " +
                "ItemKeyPath='Id' Virtualizing='false' ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <TextBox Text='{Binding Text, Mode=TwoWay}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            ReactiveItemsState state = new ReactiveItemsState();
            ReactiveItemRow row =
                new ReactiveItemRow("editor", "Initial edit", true);
            state.Items.Add(row);

            XamlRuntime runtime = XamlRuntime.Load(markup, state);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                CreateHandleAndDrainReactiveCallbacks(
                    runtime.RootControl);

                TextBox editor = GetItemTextBox(host, row);
                AssertEqual("Initial edit", editor.Text, "initial item editor text");

                editor.Text = "Target item edit";
                DrainReactiveCallbacks(runtime.RootControl);
                AssertEqual(
                    "Target item edit",
                    row.Text.Value,
                    "item editor writes PropertyBinding source");

                row.Text.Value = "Source item edit";
                DrainReactiveCallbacks(runtime.RootControl);
                AssertEqual(
                    "Source item edit",
                    editor.Text,
                    "item PropertyBinding updates editor");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestCoalescedReactiveItemSlotsShareDetachedBatch()
        {
            const string markup =
                "<ItemsControl Name='Rows' ItemKeyPath='Id' " +
                "Virtualizing='false' ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <StackPanel>" +
                "      <Label Text='{Binding Primary}' />" +
                "      <Label Text='{Binding Secondary}' />" +
                "    </StackPanel>" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                DualReactiveItemRow row =
                    new DualReactiveItemRow(
                        "dual",
                        "Primary initial",
                        "Secondary initial");
                ArrayList rows = new ArrayList();
                rows.Add(row);

                CreateHandleAndDrainReactiveCallbacks(
                    runtime.RootControl);
                host.SetItems(rows);
                DrainReactiveCallbacks(runtime.RootControl);

                Control itemRoot = null;
                int i;

                for (i = 0; i < host.Controls.Count; i++)
                {
                    Control candidate = host.Controls[i];

                    if (Object.ReferenceEquals(candidate.Tag, row))
                    {
                        itemRoot = candidate;
                        break;
                    }
                }

                AssertTrue(itemRoot != null, "dual-slot item root is realized");
                AssertEqual(2, itemRoot.Controls.Count, "dual-slot label count");

                Label primary = null;
                Label secondary = null;

                for (i = 0; i < itemRoot.Controls.Count; i++)
                {
                    Label candidate = itemRoot.Controls[i] as Label;

                    if (candidate == null)
                        continue;

                    if (candidate.Text == "Primary initial")
                        primary = candidate;
                    else if (candidate.Text == "Secondary initial")
                        secondary = candidate;
                }

                AssertTrue(primary != null, "primary reactive label exists");
                AssertTrue(secondary != null, "secondary reactive label exists");

                int completionCount = 0;
                host.RefreshCompleted +=
                    delegate(object sender, EventArgs e)
                    {
                        completionCount++;
                    };

                row.Primary.Value = "Primary updated";
                row.Secondary.Value = "Secondary updated";
                DrainReactiveCallbacks(runtime.RootControl);

                AssertEqual(
                    "Primary updated",
                    primary.Text,
                    "first coalesced slot is applied");
                AssertEqual(
                    "Secondary updated",
                    secondary.Text,
                    "second coalesced slot is applied");
                AssertEqual(
                    0,
                    completionCount,
                    "coalesced local slots do not trigger a list refresh");
                AssertSame(
                    itemRoot,
                    primary.Parent,
                    "coalesced patch retains the item tree");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestReactiveItemEndpointReplacementPreservesTargetEdit()
        {
            const string markup =
                "<ItemsControl Name='Rows' ItemsSource='{Binding Items}' " +
                "ItemKeyPath='Id' Virtualizing='false' ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <TextBox Text='{Binding Endpoint.Text, Mode=TwoWay}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            ReactiveItemEndpoint originalEndpoint =
                new ReactiveItemEndpoint("Original endpoint");
            ReactiveEndpointRow row =
                new ReactiveEndpointRow("endpoint", originalEndpoint);
            ReactiveEndpointItemsState state =
                new ReactiveEndpointItemsState();
            state.Items.Add(row);

            XamlRuntime runtime = XamlRuntime.Load(markup, state);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                CreateHandleAndDrainReactiveCallbacks(
                    runtime.RootControl);

                TextBox editor = GetItemTextBox(host, row);
                ReactiveItemEndpoint replacementEndpoint =
                    new ReactiveItemEndpoint("Replacement endpoint");

                row.Endpoint.Value = replacementEndpoint;
                editor.Text = "Target edit before dispatch";
                DrainReactiveCallbacks(runtime.RootControl);

                AssertEqual(
                    "Original endpoint",
                    originalEndpoint.Text.Value,
                    "superseded endpoint is not edited");
                AssertEqual(
                    "Target edit before dispatch",
                    replacementEndpoint.Text.Value,
                    "pending target edit writes replacement endpoint");
                AssertEqual(
                    "Target edit before dispatch",
                    editor.Text,
                    "replacement keeps latest target value");
                AssertEqual(
                    0,
                    GetPropertyBindingSubscriberCount(originalEndpoint.Text),
                    "superseded endpoint detaches");
                AssertEqual(
                    1,
                    GetPropertyBindingSubscriberCount(replacementEndpoint.Text),
                    "replacement endpoint subscribes once");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestReactiveItemPatchSkipsSourceEnumerationAndSiblings()
        {
            const string markup =
                "<ItemsControl Name='Rows' ItemKeyPath='Id' " +
                "Virtualizing='false' ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label AutoSize='true' Text='{Binding Text}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                CountingReactiveItemRow first =
                    new CountingReactiveItemRow("first", "First");
                CountingReactiveItemRow second =
                    new CountingReactiveItemRow("second", "Second");
                CountingReactiveItemRow third =
                    new CountingReactiveItemRow("third", "Third");
                ArrayList rows = new ArrayList();
                rows.Add(first);
                rows.Add(second);
                rows.Add(third);
                CountingEnumerable source =
                    new CountingEnumerable(rows);

                CreateHandleAndDrainReactiveCallbacks(
                    runtime.RootControl);
                host.SetItems(source);
                DrainReactiveCallbacks(runtime.RootControl);

                Label firstLabel = GetItemLabel(host, first);
                Label secondLabel = GetItemLabel(host, second);
                Label thirdLabel = GetItemLabel(host, third);
                int completionCount = 0;

                host.RefreshCompleted +=
                    delegate(object sender, EventArgs e)
                    {
                        completionCount++;
                    };

                source.ResetCounts();
                first.TextReads = 0;
                second.TextReads = 0;
                third.TextReads = 0;

                first.SetText("First updated and wider");
                DrainReactiveCallbacks(runtime.RootControl);

                AssertEqual(
                    "First updated and wider",
                    firstLabel.Text,
                    "reactive item target updates in place");
                AssertEqual(
                    0,
                    source.GetEnumeratorCount,
                    "reactive item patch does not request an enumerator");
                AssertEqual(
                    0,
                    source.MoveNextCount,
                    "reactive item patch does not enumerate the source");
                AssertTrue(
                    first.TextReads > 0,
                    "changed item binding is evaluated");
                AssertEqual(
                    0,
                    second.TextReads,
                    "second item binding is not evaluated");
                AssertEqual(
                    0,
                    third.TextReads,
                    "third item binding is not evaluated");
                AssertEqual(
                    0,
                    completionCount,
                    "local reactive patch does not publish a list refresh");
                AssertSame(
                    firstLabel,
                    GetItemLabel(host, first),
                    "changed control is retained");
                AssertSame(
                    secondLabel,
                    GetItemLabel(host, second),
                    "unrelated second control is retained");
                AssertSame(
                    thirdLabel,
                    GetItemLabel(host, third),
                    "unrelated third control is retained");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestReactiveVirtualItemPatchSkipsSourceEnumerationAndSiblings()
        {
            const string markup =
                "<ItemsControl Name='Rows' Width='180' Height='100' AutoScroll='true' " +
                "ItemKeyPath='Id' Virtualizing='true' VirtualizationThreshold='1' " +
                "OverscanItems='1' FixedItemSize='20' ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label AutoSize='true' Text='{Binding Text}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ArrayList rows = new ArrayList();
                int i;

                for (i = 0; i < 50; i++)
                {
                    rows.Add(
                        new CountingReactiveItemRow(
                            "virtual-" + i,
                            "Virtual " + i));
                }

                CountingEnumerable source =
                    new CountingEnumerable(rows);
                CountingReactiveItemRow first =
                    (CountingReactiveItemRow)rows[0];
                CountingReactiveItemRow second =
                    (CountingReactiveItemRow)rows[1];

                CreateHandleAndDrainReactiveCallbacks(
                    runtime.RootControl);
                host.SetItems(source);
                DrainReactiveCallbacks(runtime.RootControl);

                Label firstLabel = GetItemLabel(host, first);
                Label secondLabel = GetItemLabel(host, second);
                int realizedBefore = host.RealizedCount;
                int completionCount = 0;

                host.RefreshCompleted +=
                    delegate(object sender, EventArgs e)
                    {
                        completionCount++;
                    };

                source.ResetCounts();

                for (i = 0; i < rows.Count; i++)
                {
                    ((CountingReactiveItemRow)rows[i]).TextReads = 0;
                }

                first.SetText("Virtual first updated");
                DrainReactiveCallbacks(runtime.RootControl);

                AssertEqual(
                    "Virtual first updated",
                    firstLabel.Text,
                    "realized virtual target updates in place");
                AssertEqual(
                    0,
                    source.GetEnumeratorCount,
                    "virtual reactive patch does not request an enumerator");
                AssertEqual(
                    0,
                    source.MoveNextCount,
                    "virtual reactive patch does not enumerate the source");
                AssertTrue(
                    first.TextReads > 0,
                    "changed virtual binding is evaluated");
                AssertEqual(
                    0,
                    second.TextReads,
                    "unrelated realized virtual binding is not evaluated");
                AssertEqual(
                    0,
                    completionCount,
                    "virtual local patch does not publish a list refresh");
                AssertEqual(
                    realizedBefore,
                    host.RealizedCount,
                    "virtual local patch keeps the realized window");
                AssertSame(
                    firstLabel,
                    GetItemLabel(host, first),
                    "changed virtual control is retained");
                AssertSame(
                    secondLabel,
                    GetItemLabel(host, second),
                    "unrelated virtual control is retained");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestViewportVirtualization()
        {
            const string markup =
                "<ItemsControl Name='Rows' Width='180' Height='100' AutoScroll='true' " +
                "ItemKeyPath='Id' Virtualizing='true' VirtualizationThreshold='1' " +
                "OverscanItems='1' FixedItemSize='20' ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label AutoSize='true' Text='{Binding Text}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ArrayList rows = new ArrayList();
                int i;

                for (i = 0; i < 100; i++)
                {
                    rows.Add(
                        new ItemRow(
                            "row-" + i,
                            1,
                            "Row " + i));
                }

                host.CreateControl();
                host.SetItems(rows);

                AssertTrue(host.IsVirtualizing, "virtualization active");
                AssertTrue(host.RealizedCount > 0, "initial realized range");
                AssertTrue(host.RealizedCount < host.Count, "bounded realized range");

                host.ScrollToIndex(80);
                AssertTrue(
                    GetItemLabelOrNull(host, rows[80]) != null,
                    "requested item realized");

                int visibleRows =
                    Math.Max(
                        1,
                        (host.ClientSize.Height + host.FixedItemSize - 1) /
                        host.FixedItemSize);
                int maximumRealizedRows =
                    visibleRows + 2 * host.OverscanItems;

                AssertTrue(
                    host.RealizedCount <= maximumRealizedRows,
                    "fixed-size realization does not grow its overscan budget");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void
            TestFastVariableHeightViewportJumpsRetainVisibleControls()
        {
            const string markup =
                "<ItemsControl Name='Rows' Width='220' Height='120' AutoScroll='true' " +
                "ItemKeyPath='Id' Virtualizing='true' VirtualizationThreshold='1' " +
                "OverscanItems='0' EstimatedItemSize='96' Spacing='0' " +
                "ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <StackPanel Height='{Binding Height}' Padding='4'>" +
                "      <Label AutoSize='true' Text='{Binding Text}' />" +
                "      <Label AutoSize='true' Text='Variable-height detail' />" +
                "    </StackPanel>" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                int[] heightPattern =
                    new int[] { 18, 176, 32, 224, 48, 12, 132 };
                ArrayList rows = new ArrayList();
                int i;

                for (i = 0; i < 240; i++)
                {
                    rows.Add(
                        new VariableHeightRow(
                            "variable-" + i,
                            heightPattern[i % heightPattern.Length],
                            "Variable row " + i));
                }

                host.CreateControl();
                host.SetItems(rows);

                int[] jumps =
                    new int[] { 181, 17, 226, 73, 238, 41, 154, 3, 207 };

                for (i = 0; i < jumps.Length; i++)
                {
                    VirtualViewportModel model =
                        host.DirectVirtualViewport;
                    long extent = model.GetExtent(jumps[i]);
                    long target =
                        model.GetOffset(jumps[i]) +
                        Math.Max(0L, (extent - 1L) / 2L);
                    int nativeTarget = target >= Int32.MaxValue
                        ? Int32.MaxValue
                        : (int)target;
                    bool previousSuppress =
                        host.DirectVirtualSuppressScrollRefresh;
                    host.DirectVirtualSuppressScrollRefresh = true;

                    try
                    {
                        host.AutoScrollPosition =
                            new Point(0, nativeTarget);
                    }
                    finally
                    {
                        host.DirectVirtualSuppressScrollRefresh =
                            previousSuppress;
                    }

                    AssertTrue(
                        host.HandleDirectVirtualViewportChanged(),
                        "fast jump is owned by the direct viewport");
                    AssertDirectVirtualViewportCovered(
                        host,
                        "fast variable-height jump " + i);
                }
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestTinyEndRowsSurviveLargeNativeScrollClamp()
        {
            const string markup =
                "<ItemsControl Name='Rows' Width='220' Height='120' AutoScroll='true' " +
                "ItemKeyPath='Id' Virtualizing='true' VirtualizationThreshold='1' " +
                "OverscanItems='0' EstimatedItemSize='4096' Spacing='0' " +
                "ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Panel Height='{Binding Height}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ArrayList rows = new ArrayList();
                int i;

                for (i = 0; i < 300; i++)
                {
                    rows.Add(
                        new VariableHeightRow(
                            "tiny-" + i,
                            1,
                            "Tiny row " + i));
                }

                host.CreateControl();
                host.SetItems(rows);
                host.ScrollToIndex(rows.Count - 1);

                AssertDirectVirtualViewportCovered(
                    host,
                    "large end clamp with one-pixel rows");
                AssertTrue(
                    host.RealizedCount <= host.ClientSize.Height + 2,
                    "conservative end repair remains bounded by viewport pixels");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestEquivalentDirectViewportRefreshIsNoOp()
        {
            XamlRuntime.ItemsControl host;
            XamlRuntime runtime =
                LoadDirectVirtualThresholdFixture(
                    1,
                    out host);

            try
            {
                ArrayList rows = (ArrayList)host.ItemsSource;
                ArrayList rendered = host.RenderedItems;
                int generation = host.DirectVirtualGeneration;
                int start = host.DirectVirtualRealizedStart;
                int end = host.DirectVirtualRealizedEnd;
                Label first = GetItemLabel(host, rows[0]);

                AssertTrue(
                    host.HandleDirectVirtualViewportChanged(),
                    "the direct viewport owns the layout request");

                AssertSame(
                    rendered,
                    host.RenderedItems,
                    "an equivalent range keeps the committed record list");
                AssertEqual(
                    generation,
                    host.DirectVirtualGeneration,
                    "an equivalent range keeps its generation");
                AssertEqual(
                    start,
                    host.DirectVirtualRealizedStart,
                    "an equivalent range keeps its start");
                AssertEqual(
                    end,
                    host.DirectVirtualRealizedEnd,
                    "an equivalent range keeps its end");
                AssertSame(
                    first,
                    GetItemLabel(host, rows[0]),
                    "an equivalent range keeps its control");
                AssertTrue(
                    !host.IsRefreshing &&
                    !host.DirectVirtualRefreshRunning,
                    "no deferred viewport work remains");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void
            TestOverscanChangeReconcilesDirectViewportSynchronously()
        {
            XamlRuntime.ItemsControl host;
            XamlRuntime runtime =
                LoadDirectVirtualThresholdFixture(
                    0,
                    out host);

            try
            {
                int generation = host.DirectVirtualGeneration;
                int initialStart = host.DirectVirtualRealizedStart;
                int initialEnd = host.DirectVirtualRealizedEnd;
                int initialCount = host.RealizedCount;

                host.OverscanItems = 8;

                AssertTrue(
                    host.DirectVirtualRealizedStart <= initialStart &&
                    host.DirectVirtualRealizedEnd >= initialEnd &&
                    host.RealizedCount > initialCount,
                    "larger overscan expands the committed direct range");
                AssertEqual(
                    generation,
                    host.DirectVirtualGeneration,
                    "overscan reconciliation keeps the active generation");
                AssertTrue(
                    !host.IsRefreshing &&
                    !host.DirectVirtualRefreshRunning,
                    "the overscan setter leaves no pending viewport work");
                AssertDirectVirtualIndicesSorted(host);
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestDirectViewportOverscanFollowsScrollDirection()
        {
            XamlRuntime.ItemsControl host;
            XamlRuntime runtime =
                LoadDirectVirtualThresholdFixture(
                    2,
                    out host);

            try
            {
                host.ScrollToIndex(20);

                AssertEqual(
                    20,
                    host.DirectVirtualRealizedStart,
                    "forward scrolling spends no rows behind the visible range");
                AssertEqual(
                    28,
                    host.DirectVirtualRealizedEnd,
                    "forward scrolling spends the unchanged budget ahead");
                AssertEqual(
                    9,
                    host.RealizedCount,
                    "forward scrolling keeps visible plus two-sided total budget");

                host.ScrollToIndex(10);

                AssertEqual(
                    6,
                    host.DirectVirtualRealizedStart,
                    "reverse scrolling spends the unchanged budget ahead of travel");
                AssertEqual(
                    14,
                    host.DirectVirtualRealizedEnd,
                    "reverse scrolling spends no rows beyond the trailing visible edge");
                AssertEqual(
                    9,
                    host.RealizedCount,
                    "reverse scrolling preserves the configured total budget");
                AssertDirectVirtualViewportCovered(
                    host,
                    "direction-aware direct viewport");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestDirectViewportScrollPublishesSynchronously()
        {
            XamlRuntime.ItemsControl host;
            XamlRuntime runtime =
                LoadDirectVirtualThresholdFixture(
                    1,
                    out host);

            try
            {
                ArrayList rows = (ArrayList)host.ItemsSource;
                int generation = host.DirectVirtualGeneration;
                int oldEnd = host.DirectVirtualRealizedEnd;

                host.ScrollToIndex(20);

                AssertTrue(
                    host.DirectVirtualRealizedStart <= 20 &&
                    host.DirectVirtualRealizedEnd >= 20 &&
                    host.DirectVirtualRealizedEnd > oldEnd,
                    "ScrollToIndex publishes a range containing the target");
                AssertEqual(
                    "Row 20",
                    GetItemLabel(host, rows[20]).Text,
                    "the requested row is usable before ScrollToIndex returns");
                AssertEqual(
                    generation,
                    host.DirectVirtualGeneration,
                    "viewport scrolling stays in the committed generation");
                AssertTrue(
                    !host.IsRefreshing &&
                    !host.DirectVirtualRefreshRunning,
                    "scrolling leaves no queued viewport work");
                AssertDirectVirtualIndicesSorted(host);
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void
            TestValidatedDirectViewportRefreshRebuildsSynchronously()
        {
            XamlRuntime.ItemsControl host;
            XamlRuntime runtime =
                LoadDirectVirtualThresholdFixture(
                    1,
                    out host);

            try
            {
                ArrayList rows = (ArrayList)host.ItemsSource;
                Label original = GetItemLabel(host, rows[0]);
                int generation = host.DirectVirtualGeneration;
                int start = host.DirectVirtualRealizedStart;
                int end = host.DirectVirtualRealizedEnd;

                runtime.RefreshDirectVirtualViewportSynchronously(
                    host,
                    false,
                    true);

                AssertNotSame(
                    original,
                    GetItemLabel(host, rows[0]),
                    "validated realization rebuilds the committed control");
                AssertEqual(
                    start,
                    host.DirectVirtualRealizedStart,
                    "validated realization preserves the requested start");
                AssertEqual(
                    end,
                    host.DirectVirtualRealizedEnd,
                    "validated realization preserves the requested end");
                AssertEqual(
                    generation,
                    host.DirectVirtualGeneration,
                    "validated realization stays in the active generation");
                AssertEqual(
                    0,
                    host.VirtualCacheCount,
                    "validated realization drains detached cache hints");
                AssertTrue(
                    !host.IsRefreshing &&
                    !host.DirectVirtualRefreshRunning,
                    "validated realization completes before returning");
                AssertDirectVirtualIndicesSorted(host);
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestCanceledProgressivePatch()
        {
            XamlRuntime runtime = LoadSimpleItemsControl("Version");

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ItemRow first = new ItemRow("first", 1, "First");
                ItemRow second = new ItemRow("second", 1, "Second");
                ArrayList rows = new ArrayList();
                rows.Add(first);
                rows.Add(second);

                host.ReevaluateFunctionsOnRefresh = false;
                host.SetItems(rows);

                Label firstControl = GetItemLabel(host, first);
                Label secondControl = GetItemLabel(host, second);

                host.ProgressiveRendering = true;
                host.ProgressiveInterval = 60000;
                host.ProgressiveBatchSize = 1;
                first.Version = 2;
                first.Text = "First pending";
                second.Version = 2;
                second.Text = "Second pending";
                host.ReloadItems();

                AssertTrue(host.IsRefreshing, "progressive refresh queued");
                AssertEqual("First pending", firstControl.Text, "advanced patch text");
                AssertEqual("Second", secondControl.Text, "unadvanced patch text");

                first.Version = 1;
                first.Text = "First";
                second.Version = 1;
                second.Text = "Second";
                host.ReloadItems();

                AssertEqual("First", firstControl.Text, "canceled patch rollback");
                AssertEqual("Second", secondControl.Text, "untouched committed value");
                AssertTrue(!host.IsRefreshing, "replacement refresh completed");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestRollbackSetterDefersNewestRequest()
        {
            const string markup =
                "<ItemsControl Name='Rows' ItemKeyPath='Id' ItemVersionPath='Version' " +
                "Virtualizing='false' ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <ReentrantTextLabel Text='{Binding Text}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ItemRow committed =
                    new ItemRow("row", 1, "Committed");
                ItemRow committedSecond =
                    new ItemRow("row-2", 1, "Second committed");
                ArrayList committedRows = new ArrayList();
                committedRows.Add(committed);
                committedRows.Add(committedSecond);

                host.ReevaluateFunctionsOnRefresh = false;
                host.SetItems(committedRows);
                Label label = GetItemLabel(host, committed);

                host.ProgressiveRendering = true;
                host.ProgressiveInterval = 60000;
                host.ProgressiveBatchSize = 1;
                committed.Version = 2;
                committed.Text = "Pending";
                committedSecond.Version = 2;
                committedSecond.Text = "Second pending";
                host.ReloadItems();
                AssertEqual("Pending", label.Text, "progressive value before cancellation");

                ItemRow nested = new ItemRow("row", 3, "Pending");
                ItemRow nestedSecond =
                    new ItemRow("row-2", 3, "Second pending");
                ArrayList nestedRows = new ArrayList();
                nestedRows.Add(nested);
                nestedRows.Add(nestedSecond);
                ItemRow staleOuter = new ItemRow("row", 4, "Stale outer");
                ItemRow staleOuterSecond =
                    new ItemRow("row-2", 4, "Second stale outer");
                ArrayList staleOuterRows = new ArrayList();
                staleOuterRows.Add(staleOuter);
                staleOuterRows.Add(staleOuterSecond);

                ReentrantTextLabel.Host = host;
                ReentrantTextLabel.NestedItems = nestedRows;
                ReentrantTextLabel.RollbackText = "Committed";
                ReentrantTextLabel.ReenterOnRollback = true;

                host.ProgressiveRendering = false;
                host.SetItems(staleOuterRows);

                AssertSame(
                    nestedRows,
                    GetItemSource(host),
                    "deferred nested source wins cancellation");
                AssertEqual(
                    "Pending",
                    GetItemLabel(host, nested).Text,
                    "nested diff observes restored binding metadata");
                AssertTrue(
                    !ReentrantTextLabel.ReenterOnRollback,
                    "rollback setter performed one reentrant request");
            }
            finally
            {
                ReentrantTextLabel.Host = null;
                ReentrantTextLabel.NestedItems = null;
                ReentrantTextLabel.RollbackText = null;
                ReentrantTextLabel.ReenterOnRollback = false;
                DisposeRuntime(runtime);
            }
        }

        private static void TestFailedRollbackRetriesProvisionalValue()
        {
            const string markup =
                "<ItemsControl Name='Rows' ItemKeyPath='Id' ItemVersionPath='Version' " +
                "Virtualizing='false' ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <FaultingRollbackLabel Text='{Binding Text}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ItemRow first = new ItemRow("first", 1, "Committed");
                ItemRow second =
                    new ItemRow("second", 1, "Second committed");
                ArrayList rows = new ArrayList();
                rows.Add(first);
                rows.Add(second);

                host.ReevaluateFunctionsOnRefresh = false;
                host.SetItems(rows);

                FaultingRollbackLabel firstLabel =
                    (FaultingRollbackLabel)GetItemLabel(host, first);
                FaultingRollbackLabel secondLabel =
                    (FaultingRollbackLabel)GetItemLabel(host, second);

                firstLabel.ThrowBeforeText = "Committed";
                secondLabel.ThrowBeforeText = "Second pending";
                host.ProgressiveRendering = true;
                host.ProgressiveInterval = 60000;
                host.ProgressiveBatchSize = 1;
                first.Version = 2;
                first.Text = "Pending";
                second.Version = 2;
                second.Text = "Second pending";
                host.ReloadItems();

                AssertEqual("Pending", firstLabel.Text, "provisional value applied");
                AssertEqual(
                    1,
                    firstLabel.PendingAssignmentCount,
                    "initial provisional setter count");

                object state = GetPendingRefreshState(host);
                MethodInfo applyMethod = GetRuntimeMethod(
                    "ApplyItemsPatchBatch");
                Exception patchError = null;

                try
                {
                    applyMethod.Invoke(
                        runtime,
                        new object[] { state, 1 });
                }
                catch (TargetInvocationException ex)
                {
                    patchError = ex.InnerException == null
                        ? ex
                        : ex.InnerException;
                }

                AssertTrue(patchError != null, "second provisional setter failed");

                MethodInfo failMethod = GetRuntimeMethod("FailItemsRefresh");
                failMethod.Invoke(
                    runtime,
                    new object[] { state, patchError, false });

                AssertEqual(
                    "Pending",
                    firstLabel.Text,
                    "failed rollback leaves provisional native value");
                AssertEqual(
                    1,
                    firstLabel.PendingAssignmentCount,
                    "rollback did not reapply the provisional value");

                host.ReloadItems();

                AssertEqual(
                    2,
                    firstLabel.PendingAssignmentCount,
                    "same provisional value is forced through the setter again");
                AssertEqual(
                    "Pending",
                    firstLabel.Text,
                    "retried provisional native value");

                secondLabel.ThrowBeforeText = "Second pending";
                state = GetPendingRefreshState(host);
                patchError = null;

                try
                {
                    applyMethod.Invoke(
                        runtime,
                        new object[] { state, 1 });
                }
                catch (TargetInvocationException ex)
                {
                    patchError = ex.InnerException == null
                        ? ex
                        : ex.InnerException;
                }

                AssertTrue(patchError != null, "repeated second setter failure");
                failMethod.Invoke(
                    runtime,
                    new object[] { state, patchError, false });

                AssertEqual(
                    "Committed",
                    firstLabel.Text,
                    "repeated failure restores the real committed value");
                AssertTrue(
                    !firstLabel.SawObjectMarkerText,
                    "internal retry state never reaches the Text setter");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void
            TestFailedDirectToKeyedTransitionResumesDirectScrolling()
        {
            const string markup =
                "<ItemsControl Name='Rows' Width='180' Height='100' AutoScroll='true' " +
                "ItemKeyPath='Id' Virtualizing='true' " +
                "VirtualizationThreshold='32' OverscanItems='1' FixedItemSize='20' " +
                "ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label Text='{Binding Text}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ArrayList committedRows = CreateToggleRows(100);

                host.CreateControl();
                host.SetItems(committedRows);

                AssertTrue(
                    host.IsVirtualizing &&
                    host.DirectVirtualViewport != null,
                    "fixture starts in the direct viewport");
                VirtualViewportModel committedModel =
                    host.DirectVirtualViewport;
                ArrayList committedRecords = host.RenderedItems;
                Label committedFirst =
                    GetItemLabel(host, committedRows[0]);
                ArrayList failingRows = new ArrayList();
                failingRows.Add(
                    new ThrowingTextRow(
                        "normal-transition-failure"));
                Exception error = null;

                try
                {
                    host.SetItems(failingRows);
                }
                catch (Exception ex)
                {
                    error = ex;
                }

                AssertTrue(
                    error != null,
                    "ineligible normal-renderer transition fails as requested");
                AssertTrue(
                    host.IsVirtualizing &&
                    host.DirectVirtualActive,
                    "failed transition restores direct viewport ownership");
                AssertSame(
                    committedRows,
                    GetItemSource(host),
                    "failed transition restores the committed source");
                AssertSame(
                    committedModel,
                    host.DirectVirtualViewport,
                    "failed transition restores the committed logical model");
                AssertSame(
                    committedRecords,
                    host.RenderedItems,
                    "failed transition restores the committed records");
                AssertSame(
                    committedFirst,
                    GetItemLabel(host, committedRows[0]),
                    "failed transition preserves the committed control");
                AssertEqual(
                    host.RefreshGeneration,
                    host.DirectVirtualGeneration,
                    "rollback hands the current generation to the direct viewport");

                host.ScrollToIndex(50);

                AssertTrue(
                    host.DirectVirtualRealizedStart <= 50 &&
                    host.DirectVirtualRealizedEnd >= 50,
                    "resumed direct viewport can publish a later range");
                AssertEqual(
                    "Toggle 50",
                    GetItemLabel(host, committedRows[50]).Text,
                    "resumed scrolling realizes the committed target row");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestReloadKeepsNewestProgressiveSource()
        {
            XamlRuntime runtime = LoadSimpleItemsControl(null);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ArrayList committedRows = new ArrayList();
                committedRows.Add(new ItemRow("old", 1, "Old"));
                host.SetItems(committedRows);

                ArrayList requestedRows = new ArrayList();
                requestedRows.Add(new ItemRow("new-1", 1, "New 1"));
                requestedRows.Add(new ItemRow("new-2", 1, "New 2"));
                requestedRows.Add(new ItemRow("new-3", 1, "New 3"));

                host.ProgressiveRendering = true;
                host.ProgressiveInterval = 60000;
                host.ProgressiveBatchSize = 1;
                host.SetItems(requestedRows);

                AssertTrue(host.IsRefreshing, "new source refresh queued");

                host.ProgressiveRendering = false;
                host.ReloadItems();

                AssertSame(
                    requestedRows,
                    GetItemSource(host),
                    "newest requested source after reload");
                AssertEqual(3, host.Count, "newest requested count");
                AssertEqual(
                    "New 1",
                    GetItemLabel(host, requestedRows[0]).Text,
                    "newest requested first row");
                AssertTrue(!host.IsRefreshing, "replacement reload completed");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestPreHandleWorkerUpdate()
        {
            XamlRuntime runtime = LoadSimpleItemsControl(null);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ArrayList rows = new ArrayList();
                rows.Add(new ItemRow("one", 1, "One"));
                Exception workerError = null;

                AssertTrue(!host.IsHandleCreated, "pre-handle test setup");

                Thread worker = new Thread(
                    (ThreadStart)delegate
                    {
                        try
                        {
                            host.SetItems(rows);
                        }
                        catch (Exception ex)
                        {
                            workerError = ex;
                        }
                    });

                worker.SetApartmentState(ApartmentState.STA);
                worker.Start();

                AssertTrue(worker.Join(5000), "worker completed");
                AssertTrue(
                    workerError is InvalidOperationException,
                    "pre-handle worker update throws InvalidOperationException");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestVirtualForcedReload()
        {
            const string markup =
                "<ItemsControl Name='Rows' Width='180' Height='100' AutoScroll='true' " +
                "ItemKeyPath='Id' Virtualizing='true' VirtualizationThreshold='1' " +
                "OverscanItems='1' FixedItemSize='20' VirtualizationCacheItems='64' " +
                "ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label AutoSize='true' Text='{Binding Text}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ArrayList rows = CreateRows(100);

                host.CreateControl();
                host.SetItems(rows);

                Label original = GetItemLabel(host, rows[0]);
                host.ScrollToIndex(80);

                AssertTrue(
                    !original.Visible || original.Bounds.IsEmpty,
                    "first row moved out of the realized viewport");
                AssertTrue(host.VirtualCacheCount > 0, "offscreen row cached");

                host.ForceReloadItems();
                host.ScrollToIndex(0);

                AssertNotSame(
                    original,
                    GetItemLabel(host, rows[0]),
                    "forced reload replaces cached row");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestPresetRootConditionUsesKeyedFallback()
        {
            const string markup =
                "<Panel>" +
                "  <Presets Name='View' Selected='Visible'>" +
                "    <Preset Name='Visible'><Set Key='Show' Value='true' /></Preset>" +
                "    <Preset Name='Hidden'><Set Key='Show' Value='false' /></Preset>" +
                "  </Presets>" +
                "  <ItemsControl Name='Rows' Width='180' Height='100' AutoScroll='true' " +
                "  ItemKeyPath='Id' ItemVersionPath='Version' Virtualizing='true' VirtualizationThreshold='1' " +
                "  OverscanItems='1' FixedItemSize='20' ProgressiveRendering='false'>" +
                "    <ItemsControl.ItemTemplate>" +
                "      <Label AutoSize='true' Condition='{Preset View.Show}' Text='{Binding Text}' />" +
                "    </ItemsControl.ItemTemplate>" +
                "  </ItemsControl>" +
                "</Panel>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ArrayList rows = CreateRows(40);

                runtime.RootControl.CreateControl();
                host.CreateControl();
                host.SetItems(rows);

                Label firstRealized = GetItemLabel(host, rows[0]);
                int visibleExtent = host.AutoScrollMinSize.Height;
                AssertTrue(
                    !host.IsVirtualizing,
                    "preset root Condition selects keyed fallback");
                AssertEqual(
                    rows.Count,
                    host.RealizedCount,
                    "normal renderer retains every keyed row");
                AssertTrue(visibleExtent > 0, "visible scroll extent");

                runtime.Presets.Select("View", "Hidden");

                AssertEqual(
                    rows.Count,
                    host.RealizedCount,
                    "hidden keyed rows remain realized");
                AssertEqual(0, host.VirtualCacheCount, "keyed fallback has no virtual cache");
                AssertTrue(
                    !firstRealized.IsDisposed &&
                    !firstRealized.Visible,
                    "hidden keyed row is retained and hidden");
                AssertEqual(0, host.AutoScrollMinSize.Height, "hidden scroll extent");
                AssertTrue(
                    host.AutoScrollMinSize.Height < visibleExtent,
                    "preset selection reduces scroll extent");

                runtime.Presets.Select("View", "Visible");

                AssertSame(
                    firstRealized,
                    GetItemLabel(host, rows[0]),
                    "visible preset reuses the keyed row");
                AssertTrue(firstRealized.Visible, "visible preset shows the keyed row");
                AssertTrue(
                    host.AutoScrollMinSize.Height > 0,
                    "visible preset restores stable-version extent");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestUnresolvedItemPresetLeavesSystemBaseline()
        {
            const string markup =
                "<Panel>\n" +
                "  <Presets Name='Theme' Selected='Active' Default='Base'>\n" +
                "    <Preset Name='Active'><Set Key='Other' Value='Active' /></Preset>\n" +
                "    <Preset Name='Base'><Set Key='Other' Value='Base' /></Preset>\n" +
                "    <Preset Name='Unrelated'><Set Key='Surface' Value='Wrong' /></Preset>\n" +
                "  </Presets>\n" +
                "  <ItemsControl Name='Rows' ItemKeyPath='Id' Virtualizing='false' ProgressiveRendering='false'>\n" +
                "    <ItemsControl.ItemTemplate>\n" +
                "      <Panel>\n" +
                "        <Label Name='PresetChild' Text='{Preset Theme.Surface}' />\n" +
                "      </Panel>\n" +
                "    </ItemsControl.ItemTemplate>\n" +
                "  </ItemsControl>\n" +
                "</Panel>";
            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ArrayList rows = new ArrayList();
                rows.Add(new ItemRow("one", 1, "One"));
                int failedCount = 0;

                host.RefreshFailed +=
                    delegate
                    {
                        failedCount++;
                    };

                host.SetItems(rows);

                Label label = GetItemLabel(host, rows[0]);

                AssertEqual(
                    String.Empty,
                    label.Text,
                    "initial unresolved preset keeps the Label system baseline");
                AssertTrue(
                    !String.Equals(
                        "Wrong",
                        label.Text,
                        StringComparison.Ordinal),
                    "initial unresolved preset does not scan unrelated presets");
                AssertEqual(null, host.LastRefreshError, "unresolved preset is not an error");
                AssertEqual(0, failedCount, "unresolved preset raises no failure event");
                AssertEqual(1, host.Count, "unresolved preset source is committed");
                AssertEqual(1, host.RealizedCount, "unresolved preset row is published");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestItemPresetRestoresBaselineAndResolvesAgain()
        {
            const string markup =
                "<Panel>\n" +
                "  <Presets Name='Theme' Selected='Active' Default='Base'>\n" +
                "    <Preset Name='Active'><Set Key='Surface' Value='Initial' /></Preset>\n" +
                "    <Preset Name='Base'><Set Key='Other' Value='Base' /></Preset>\n" +
                "    <Preset Name='Missing'><Set Key='Other' Value='Missing' /></Preset>\n" +
                "    <Preset Name='Unrelated'><Set Key='Surface' Value='Wrong' /></Preset>\n" +
                "  </Presets>\n" +
                "  <ItemsControl Name='Rows' ItemKeyPath='Id' Virtualizing='false' ProgressiveRendering='false'>\n" +
                "    <ItemsControl.ItemTemplate>\n" +
                "      <Panel>\n" +
                "        <Label Name='PresetChild' Text='{Preset Theme.Surface}' />\n" +
                "      </Panel>\n" +
                "    </ItemsControl.ItemTemplate>\n" +
                "  </ItemsControl>\n" +
                "</Panel>";
            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ItemRow row = new ItemRow("one", 1, "One");
                ArrayList committedRows = new ArrayList();
                committedRows.Add(row);

                runtime.RootControl.CreateControl();
                host.CreateControl();
                host.SetItems(committedRows);

                Label committedLabel = GetItemLabel(host, row);
                int failedCount = 0;

                host.RefreshFailed +=
                    delegate
                    {
                        failedCount++;
                    };

                runtime.Presets.Select("Theme", "Missing");

                AssertEqual(
                    null,
                    host.LastRefreshError,
                    "unresolved preset does not set LastRefreshError");
                AssertEqual(0, failedCount, "unresolved preset raises no failure event");
                AssertSame(
                    committedRows,
                    GetItemSource(host),
                    "unresolved preset keeps the committed source");
                AssertSame(
                    committedLabel,
                    GetItemLabel(host, row),
                    "unresolved preset keeps the committed child tree");
                AssertEqual(
                    String.Empty,
                    committedLabel.Text,
                    "unresolved preset restores the Label system baseline");

                runtime.Presets.Select("Theme", "Active");

                AssertSame(
                    committedLabel,
                    GetItemLabel(host, row),
                    "preset resolution reuses the committed child tree");
                AssertEqual(
                    "Initial",
                    committedLabel.Text,
                    "preset value resolves again after the baseline restore");
                AssertEqual(
                    null,
                    host.LastRefreshError,
                    "resolved preset keeps LastRefreshError clear");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestEnumerationFailure()
        {
            XamlRuntime runtime = LoadSimpleItemsControl(null);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                int failedCount = 0;

                host.RefreshFailed +=
                    delegate
                    {
                        failedCount++;
                    };

                Exception error = null;

                try
                {
                    host.SetItems(new ThrowingEnumerable());
                }
                catch (Exception ex)
                {
                    error = ex;
                }

                AssertTrue(error != null, "enumeration error propagated");
                AssertSame(error, host.LastRefreshError, "enumeration error retained");
                AssertEqual(1, failedCount, "enumeration failure event count");
                AssertTrue(!host.IsRefreshing, "enumeration failure refresh state");
                AssertEqual(0, host.RealizedCount, "enumeration failure visual state");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestEnumerationFailureCallbackSeesCommittedSource()
        {
            XamlRuntime runtime = LoadSimpleItemsControl(null);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ArrayList committedRows = new ArrayList();
                committedRows.Add(new ItemRow("committed", 1, "Committed"));
                host.SetItems(committedRows);

                object callbackSource = null;
                host.RefreshFailed +=
                    delegate
                    {
                        callbackSource = GetItemSource(host);
                    };

                try
                {
                    host.SetItems(new ThrowingEnumerable());
                }
                catch (InvalidOperationException)
                {
                }

                AssertSame(
                    committedRows,
                    callbackSource,
                    "source observed by failure callback");
                AssertSame(
                    committedRows,
                    GetItemSource(host),
                    "source after failure callback");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestReentrantEnumerationPreservesNewestRequest()
        {
            XamlRuntime runtime = LoadSimpleItemsControl(null);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ArrayList nestedRows = CreatePrefixedRows("nested-enumeration", 2);
                Exception surfaced = null;

                try
                {
                    host.SetItems(
                        new ReentrantThrowingEnumerable(
                            host,
                            nestedRows));
                }
                catch (Exception ex)
                {
                    surfaced = ex;
                }

                AssertTrue(surfaced != null, "outer enumeration error surfaced");
                AssertSame(
                    nestedRows,
                    GetItemSource(host),
                    "nested enumeration source retained");
                AssertEqual(2, host.Count, "nested enumeration count");
                AssertEqual(
                    "nested-enumeration 0",
                    GetItemLabel(host, nestedRows[0]).Text,
                    "nested enumeration first row");
                AssertEqual(
                    null,
                    host.LastRefreshError,
                    "outer enumeration did not replace nested error state");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestPlanningFailure()
        {
            XamlRuntime runtime = LoadSimpleItemsControl("Version");

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                int failedCount = 0;

                host.RefreshFailed +=
                    delegate
                    {
                        failedCount++;
                    };

                ThrowingVersionRow row = new ThrowingVersionRow();
                row.Id = "one";
                row.Text = "One";
                ArrayList rows = new ArrayList();
                rows.Add(row);
                Exception error = null;

                try
                {
                    host.SetItems(rows);
                }
                catch (Exception ex)
                {
                    error = ex;
                }

                AssertTrue(error != null, "planning error propagated");
                AssertSame(error, host.LastRefreshError, "planning error retained");
                AssertEqual(1, failedCount, "planning failure event count");
                AssertTrue(!host.IsRefreshing, "planning failure refresh state");
                AssertEqual(0, host.RealizedCount, "planning failure visual state");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void
            TestMissingChildItemBindingsReportLocatedRefreshFailures()
        {
            AssertMissingChildItemBindingFailure(
                "Text='{Binding Text}'",
                "Text",
                "direct child property");
            AssertMissingChildItemBindingFailure(
                "Text='prefix {Binding Text}'",
                "Text",
                "interpolated child property");
            AssertMissingChildItemBindingFailure(
                "Condition='{Binding Show}' Text='Visible'",
                "Condition",
                "child Condition");
        }

        private static void AssertMissingChildItemBindingFailure(
            string childAttributes,
            string expectedProperty,
            string message)
        {
            string markup =
                "<ItemsControl Name='Rows' ItemKeyPath='Id' Virtualizing='false' " +
                "ProgressiveRendering='false'>\n" +
                "  <ItemsControl.ItemTemplate>\n" +
                "    <Panel>\n" +
                "      <Label Name='BoundChild' " + childAttributes + " />\n" +
                "    </Panel>\n" +
                "  </ItemsControl.ItemTemplate>\n" +
                "</ItemsControl>";
            XamlRuntime constructionRuntime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl constructionHost =
                    constructionRuntime.GetItemsControl("Rows");
                ArrayList missingRows = new ArrayList();
                missingRows.Add(new MissingBindingRow("initial"));
                WinFormsXamlLoadException constructionFailure = null;

                try
                {
                    constructionHost.SetItems(missingRows);
                }
                catch (WinFormsXamlLoadException ex)
                {
                    constructionFailure = ex;
                }

                AssertMissingChildItemBindingDiagnostic(
                    constructionFailure,
                    expectedProperty,
                    message + " construction");
                AssertSame(
                    constructionFailure,
                    constructionHost.LastRefreshError,
                    message + " construction LastRefreshError");
                AssertEqual(
                    0,
                    constructionHost.RealizedCount,
                    message + " construction leaves no partial row");
            }
            finally
            {
                DisposeRuntime(constructionRuntime);
            }

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ArrayList committedRows = new ArrayList();
                object committedItem;

                if (String.Equals(
                        expectedProperty,
                        "Condition",
                        StringComparison.Ordinal))
                {
                    committedItem = new ConditionalToggleRow(
                        "same-key",
                        "Committed");
                }
                else
                {
                    committedItem =
                        new ItemRow("same-key", 1, "Committed");
                }

                committedRows.Add(committedItem);
                host.SetItems(committedRows);
                Label committedLabel =
                    GetItemLabel(host, committedItem);
                ArrayList missingRows = new ArrayList();
                missingRows.Add(new MissingBindingRow("same-key"));
                WinFormsXamlLoadException failure = null;

                try
                {
                    host.SetItems(missingRows);
                }
                catch (WinFormsXamlLoadException ex)
                {
                    failure = ex;
                }

                AssertMissingChildItemBindingDiagnostic(
                    failure,
                    expectedProperty,
                    message + " refresh");
                AssertSame(
                    failure,
                    host.LastRefreshError,
                    message + " LastRefreshError");
                AssertSame(
                    committedRows,
                    GetItemSource(host),
                    message + " committed source");
                AssertSame(
                    committedLabel,
                    GetItemLabel(host, committedItem),
                    message + " committed child tree");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void AssertMissingChildItemBindingDiagnostic(
            WinFormsXamlLoadException failure,
            string expectedProperty,
            string message)
        {
            AssertTrue(
                failure != null,
                message + " throws WinFormsXamlLoadException");
            AssertEqual(
                expectedProperty,
                failure.PropertyName,
                message + " diagnostic property");
            AssertTrue(
                failure.ElementPath.IndexOf(
                    "/Label#BoundChild",
                    StringComparison.Ordinal) >= 0,
                message + " diagnostic element path");
            AssertTrue(
                failure.LineNumber == 4 &&
                failure.LinePosition > 0,
                message + " diagnostic source location");
        }

        private static void
            TestDefaultProgressiveMissingChildBindingReportsFailure()
        {
            const string markup =
                "<ItemsControl Name='Rows' ItemKeyPath='Id' " +
                "Virtualizing='false'>\n" +
                "  <ItemsControl.ItemTemplate>\n" +
                "    <Panel>\n" +
                "      <Label Name='BoundChild' Text='{Binding Text}' />\n" +
                "    </Panel>\n" +
                "  </ItemsControl.ItemTemplate>\n" +
                "</ItemsControl>";
            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ArrayList committedRows = new ArrayList();
                ItemRow committed =
                    new ItemRow("committed", 1, "Committed");
                committedRows.Add(committed);
                host.SetItems(committedRows);
                Label committedLabel = GetItemLabel(host, committed);
                ArrayList missingRows = new ArrayList();
                missingRows.Add(new ItemRow("valid", 1, "Valid"));
                missingRows.Add(new MissingBindingRow("missing-two"));
                int failedCount = 0;

                host.RefreshFailed +=
                    delegate
                    {
                        failedCount++;
                    };

                host.ProgressiveBatchSize = 1;
                host.SetItems(missingRows);

                AssertTrue(
                    host.IsRefreshing,
                    "default progressive refresh is scheduled");
                AdvanceProgressiveTimer(host);

                WinFormsXamlLoadException failure =
                    host.LastRefreshError as WinFormsXamlLoadException;
                AssertMissingChildItemBindingDiagnostic(
                    failure,
                    "Text",
                    "default progressive child binding");
                AssertEqual(
                    1,
                    failedCount,
                    "default progressive failure event count");
                AssertTrue(
                    !host.IsRefreshing,
                    "default progressive failure completes refresh state");
                AssertSame(
                    committedRows,
                    GetItemSource(host),
                    "default progressive failure restores committed source");
                AssertSame(
                    committedLabel,
                    GetItemLabel(host, committed),
                    "default progressive failure preserves committed child tree");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestNullItemBindingIntermediateRemainsValid()
        {
            const string markup =
                "<ItemsControl Name='Rows' ItemKeyPath='Id' Virtualizing='false' " +
                "ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Panel>" +
                "      <Label Text='{Binding Child.Text}' />" +
                "    </Panel>" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";
            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                NullableIntermediateBindingRow row =
                    new NullableIntermediateBindingRow("nullable");
                ArrayList rows = new ArrayList();
                rows.Add(row);

                host.SetItems(rows);

                AssertEqual(
                    String.Empty,
                    GetItemLabel(host, row).Text,
                    "null intermediate renders an empty child value");
                AssertEqual(
                    null,
                    host.LastRefreshError,
                    "null intermediate does not report a missing member");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestReentrantDirectPlanningPreservesNewestRequest()
        {
            const string markup =
                "<ItemsControl Name='Rows' Width='180' Height='100' AutoScroll='true' " +
                "ItemKeyPath='Id' ItemVersionPath='Version' Virtualizing='true' " +
                "VirtualizationThreshold='1' OverscanItems='1' FixedItemSize='20' " +
                "ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label AutoSize='true' Text='{Binding Text}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ArrayList nestedRows = CreatePrefixedRows("nested-virtual", 4);
                ReentrantVersionRow outerRow = new ReentrantVersionRow();
                outerRow.Id = "outer";
                outerRow.Text = "Outer";
                outerRow.Host = host;
                outerRow.NestedItems = nestedRows;
                ArrayList outerRows = new ArrayList();
                outerRows.Add(outerRow);

                runtime.RootControl.CreateControl();
                host.CreateControl();
                host.SetItems(outerRows);

                AssertSame(
                    nestedRows,
                    GetItemSource(host),
                    "nested virtual source retained");
                AssertTrue(host.IsVirtualizing, "nested virtual model active");
                AssertTrue(host.RealizedCount > 0, "nested virtual rows realized");
                AssertEqual(
                    "nested-virtual 0",
                    GetItemLabel(host, nestedRows[0]).Text,
                    "nested virtual first row");
                AssertEqual(
                    null,
                    host.LastRefreshError,
                    "outer virtual planning did not replace nested error state");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void
            TestReentrantRootConditionSubscriptionPreservesNewestRequest()
        {
            const string markup =
                "<ItemsControl Name='Rows' Width='180' Height='100' AutoScroll='true' " +
                "ItemKeyPath='Id' Virtualizing='true' VirtualizationThreshold='1' " +
                "OverscanItems='0' FixedItemSize='20' ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label AutoSize='true' Condition='{Binding Show}' " +
                "        Text='{Binding Text}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ArrayList committedRows = CreateConditionalRows(40);
                ArrayList nestedRows = new ArrayList();
                int i;

                for (i = 0; i < 4; i++)
                {
                    nestedRows.Add(
                        new ConditionalToggleRow(
                            "nested-subscription-" + i,
                            "Nested subscription " + i));
                }

                PlanningSubscriptionState state =
                    new PlanningSubscriptionState();
                state.Host = host;
                state.NestedItems = nestedRows;
                ArrayList outerRows = new ArrayList();
                outerRows.Add(
                    new PlanningSubscriptionRow(
                        state,
                        "outer-subscription-0",
                        "Outer subscription 0",
                        true));
                outerRows.Add(
                    new PlanningSubscriptionRow(
                        state,
                        "outer-subscription-1",
                        "Outer subscription 1",
                        false));

                CreateHandleAndDrainReactiveCallbacks(
                    runtime.RootControl);
                host.SetItems(committedRows);
                AssertTrue(
                    !host.IsVirtualizing,
                    "root Condition keeps rollback on keyed fallback");
                host.SetItems(outerRows);

                AssertTrue(
                    state.Reentered,
                    "condition event add accessor started the nested request");
                AssertSame(
                    nestedRows,
                    GetItemSource(host),
                    "nested subscription source wins keyed planning");
                AssertTrue(
                    !host.IsVirtualizing,
                    "root Condition keeps reentrant planning on keyed fallback");
                AssertEqual(
                    nestedRows.Count,
                    host.Count,
                    "superseded planning cannot overwrite the nested source");
                AssertEqual(
                    "Nested subscription 0",
                    GetItemLabel(host, nestedRows[0]).Text,
                    "nested subscription row remains realized");
                AssertEqual(
                    null,
                    host.LastRefreshError,
                    "superseded subscription planning leaves nested error state intact");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestReentrantDirectPreparationRestoresCommittedModel()
        {
            const string markup =
                "<ItemsControl Name='Rows' Width='180' Height='100' AutoScroll='true' " +
                "ItemKeyPath='Id' Virtualizing='true' VirtualizationThreshold='1' " +
                "OverscanItems='1' FixedItemSize='20' ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label AutoSize='true' Text='{Binding Text}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ArrayList committedRows = CreateRows(40);

                host.CreateControl();
                host.SetItems(committedRows);

                System.Drawing.Size committedScrollSize =
                    host.AutoScrollMinSize;
                int committedLogicalCount =
                    host.DirectVirtualViewport.Count;
                long committedExtent =
                    host.DirectVirtualViewport.TotalExtent;
                ArrayList failingRows = new ArrayList();
                failingRows.Add(new ThrowingTextRow("nested-broken"));
                bool armed = true;
                bool nestedFailureObserved = false;

                host.Layout +=
                    delegate
                    {
                        if (!armed)
                            return;

                        armed = false;

                        try
                        {
                            host.SetItems(failingRows);
                        }
                        catch
                        {
                            nestedFailureObserved = true;
                        }
                    };

                ArrayList outerRows = CreatePrefixedRows("outer-model", 2);
                host.SetItems(outerRows);

                AssertTrue(nestedFailureObserved, "nested preparation failure observed");
                AssertSame(
                    committedRows,
                    GetItemSource(host),
                    "source after reentrant preparation failure");
                AssertEqual(40, host.Count, "count after reentrant preparation failure");
                AssertEqual(
                    committedScrollSize,
                    host.AutoScrollMinSize,
                    "scroll extent after reentrant preparation failure");
                AssertEqual(
                    committedLogicalCount,
                    host.DirectVirtualViewport.Count,
                    "logical count after reentrant preparation failure");
                AssertEqual(
                    committedExtent,
                    host.DirectVirtualViewport.TotalExtent,
                    "direct extent after reentrant preparation failure");

                host.ScrollToIndex(39);
                AssertEqual(
                    "Row 39",
                    GetItemLabel(host, committedRows[39]).Text,
                    "last committed row after reentrant preparation failure");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestFailedBuildPreservesCommittedList()
        {
            XamlRuntime runtime = LoadSimpleItemsControl(null);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ItemRow committedRow =
                    new ItemRow("committed", 1, "Committed");
                ArrayList committedRows = new ArrayList();
                committedRows.Add(committedRow);
                host.SetItems(committedRows);

                Label committedLabel =
                    GetItemLabel(host, committedRow);
                ArrayList failingRows = new ArrayList();
                failingRows.Add(new ThrowingTextRow("broken"));
                Exception error = null;

                try
                {
                    host.SetItems(failingRows);
                }
                catch (Exception ex)
                {
                    error = ex;
                }

                AssertTrue(error != null, "template build error propagated");
                AssertSame(
                    committedRows,
                    GetItemSource(host),
                    "committed item source after build failure");
                AssertEqual(1, host.Count, "committed count after build failure");
                AssertSame(
                    committedLabel,
                    GetItemLabel(host, committedRow),
                    "committed visible row after build failure");
                AssertEqual(
                    "Committed",
                    committedLabel.Text,
                    "committed visible text after build failure");
                AssertTrue(!host.IsRefreshing, "failed build refresh state");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestFailedDirectBuildPreservesCommittedModel()
        {
            const string markup =
                "<ItemsControl Name='Rows' Width='180' Height='100' AutoScroll='true' " +
                "ItemKeyPath='Id' Virtualizing='true' VirtualizationThreshold='1' " +
                "OverscanItems='1' FixedItemSize='20' ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label AutoSize='true' Text='{Binding Text}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ArrayList committedRows = CreateRows(40);

                host.CreateControl();
                host.SetItems(committedRows);

                object committedRow = committedRows[0];
                Label committedLabel =
                    GetItemLabel(host, committedRow);
                System.Drawing.Size committedExtent =
                    host.AutoScrollMinSize;
                ArrayList failingRows = new ArrayList();
                int i;

                for (i = 0; i < 8; i++)
                    failingRows.Add(new ThrowingTextRow("broken-" + i));

                Exception error = null;

                try
                {
                    host.SetItems(failingRows);
                }
                catch (Exception ex)
                {
                    error = ex;
                }

                AssertTrue(error != null, "virtual template build error propagated");
                AssertSame(
                    committedRows,
                    GetItemSource(host),
                    "committed virtual item source after build failure");
                AssertEqual(40, host.Count, "committed virtual count after build failure");
                AssertTrue(host.IsVirtualizing, "committed virtual mode after build failure");
                AssertEqual(
                    committedExtent,
                    host.AutoScrollMinSize,
                    "committed virtual extent after build failure");
                AssertSame(
                    committedLabel,
                    GetItemLabel(host, committedRow),
                    "committed virtual row after build failure");
                AssertEqual(
                    "Row 0",
                    committedLabel.Text,
                    "committed virtual text after build failure");
                AssertTrue(!host.IsRefreshing, "failed virtual build refresh state");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void
            TestFailedKeyedComponentRefreshRestoresConditionSubscriptions()
        {
            XamlRuntime.Register(
                Assembly.GetExecutingAssembly(),
                "WinFormsXaml.ItemsTests.Fixtures.VirtualRollbackCard.xml");

            const string markup =
                "<ItemsControl Name='Rows' Width='180' Height='100' AutoScroll='true' " +
                "ItemKeyPath='Id' ItemVersionPath='Version' Virtualizing='true' " +
                "VirtualizationThreshold='1' OverscanItems='0' FixedItemSize='20' " +
                "ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <VirtualRollbackCard TemplateShow='{Binding TemplateShow}' " +
                "        Condition='{Binding InvocationShow}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            RollbackBuildState state = new RollbackBuildState();
            XamlRuntime runtime = XamlRuntime.Load(markup, state);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ArrayList rows = new ArrayList();
                ReactiveComponentConditionRow target = null;
                int i;

                for (i = 0; i < 40; i++)
                {
                    ReactiveComponentConditionRow row =
                        new ReactiveComponentConditionRow(
                            "rollback-component-" + i,
                            "Committed component " + i,
                            true,
                            true);
                    rows.Add(row);

                    if (i == 30)
                        target = row;
                }

                CreateHandleAndDrainReactiveCallbacks(
                    runtime.RootControl);
                host.SetItems(rows);
                AssertTrue(
                    !host.IsVirtualizing,
                    "component root Condition selects keyed fallback");
                Label targetLabel = GetItemLabel(host, target);
                AssertTrue(
                    targetLabel.Visible,
                    "target component is committed before the failing refresh");

                state.ThrowOnBuild = true;
                Exception error = null;

                try
                {
                    host.ForceReloadItems();
                }
                catch (Exception ex)
                {
                    error = ex;
                }

                AssertTrue(error != null, "component build failure is surfaced");
                state.ThrowOnBuild = false;
                AssertTrue(
                    targetLabel.Visible,
                    "failed component refresh preserves committed visibility");
                AssertTrue(
                    GetPropertyBindingSubscriberCount(target.TemplateShow) > 0,
                    "rollback restores the component-template condition subscription");

                target.TemplateShow.Value = false;
                DrainReactiveCallbacks(runtime.RootControl);

                AssertTrue(
                    !targetLabel.Visible,
                    "restored component condition subscription updates keyed visibility");
                AssertSame(
                    rows,
                    GetItemSource(host),
                    "condition refresh retains the committed source");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void
            TestKeyedRollbackConditionRestoreIsReentrancySafe()
        {
            const string markup =
                "<ItemsControl Name='Rows' Width='180' Height='100' AutoScroll='true' " +
                "ItemKeyPath='Id' Virtualizing='true' VirtualizationThreshold='1' " +
                "OverscanItems='0' FixedItemSize='20' ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <FaultingRollbackLabel AutoSize='true' Condition='{Binding Show}' " +
                "        Text='{Binding Text}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            RollbackConditionState state = new RollbackConditionState();
            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                state.Host = host;
                ArrayList committedRows = new ArrayList();
                ArrayList failingRows = new ArrayList();
                int i;

                for (i = 0; i < 40; i++)
                {
                    committedRows.Add(
                        new RollbackConditionRow(
                            state,
                            "committed-" + i,
                            "Committed " + i,
                            false));
                    failingRows.Add(
                        new RollbackConditionRow(
                            state,
                            "committed-" + i,
                            "Failing " + i,
                            false));
                }

                CreateHandleAndDrainReactiveCallbacks(
                    runtime.RootControl);
                host.SetItems(committedRows);

                RollbackConditionRow committedFirst =
                    (RollbackConditionRow)committedRows[0];
                AssertTrue(
                    !host.IsVirtualizing,
                    "root Condition keeps rollback on keyed fallback");
                RollbackConditionRow committedTrigger =
                    (RollbackConditionRow)committedRows[30];
                Label committedLabel =
                    GetItemLabel(host, committedFirst);
                FaultingRollbackLabel failureLabel =
                    (FaultingRollbackLabel)GetItemLabel(
                        host,
                        committedRows[committedRows.Count - 1]);
                failureLabel.ThrowBeforeText = "Failing 39";
                committedTrigger.ScrollOnNextSubscription = true;
                Exception error = null;

                try
                {
                    host.SetItems(failingRows);
                }
                catch (Exception ex)
                {
                    error = ex;
                }

                AssertTrue(error != null, "rollback test failure is surfaced");
                AssertTrue(
                    state.SubscriptionScrollAttempted,
                    "restored condition subscription exercised its scroll accessor");
                AssertEqual(
                    0,
                    state.ExplicitConditionReadsAfterFailure,
                    "rollback does not explicitly reevaluate committed Conditions before restoring subscriptions");
                AssertSame(
                    committedRows,
                    GetItemSource(host),
                    "rollback condition source remains committed");
                AssertEqual(
                    0,
                    Math.Max(0, -host.AutoScrollPosition.Y),
                    "keyed rollback restores the committed native scroll position");
                AssertSame(
                    committedLabel,
                    GetItemLabel(host, committedFirst),
                    "subscription reentry preserves the committed visible row");
            }
            finally
            {
                state.Host = null;
                DisposeRuntime(runtime);
            }
        }

        private static void
            TestKeyedRollbackPreservesMatchingConditionSnapshots()
        {
            const string markup =
                "<ItemsControl Name='Rows' Width='180' Height='100' AutoScroll='true' " +
                "ItemKeyPath='Id' Virtualizing='true' VirtualizationThreshold='1' " +
                "OverscanItems='0' FixedItemSize='20' ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label AutoSize='true' Condition='{Binding Show}' " +
                "        Text='{Binding Text}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            RollbackSnapshotState state = new RollbackSnapshotState();
            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ArrayList rows = new ArrayList();
                int i;

                for (i = 0; i < 40; i++)
                {
                    RollbackSnapshotRow row =
                        new RollbackSnapshotRow(
                            state,
                            "snapshot-" + i,
                            "Snapshot " + i,
                            i == 0);
                    rows.Add(row);

                    if (i == 30)
                        state.Target = row;
                }

                CreateHandleAndDrainReactiveCallbacks(
                    runtime.RootControl);
                host.SetItems(rows);
                AssertTrue(
                    !host.IsVirtualizing,
                    "root Condition selects keyed fallback");
                Label targetLabel = GetItemLabel(host, state.Target);
                AssertTrue(
                    targetLabel.Visible,
                    "snapshot target starts visible in keyed output");

                state.FailNextBuild = true;
                Exception error = null;

                try
                {
                    host.ForceReloadItems();
                }
                catch (Exception ex)
                {
                    error = ex;
                }

                AssertTrue(error != null, "snapshot test failure is surfaced");
                AssertTrue(
                    state.MutatedConditionDuringFailure,
                    "later item build changed the committed condition source");
                AssertTrue(
                    targetLabel.Visible,
                    "failed rebuild retains committed target visibility");

                state.Target.NotifyShowChanged();
                DrainReactiveCallbacks(runtime.RootControl);

                AssertSame(
                    rows,
                    GetItemSource(host),
                    "snapshot reconciliation retains the committed source");
                AssertTrue(
                    !targetLabel.Visible,
                    "explicit condition notification updates keyed visibility");
                AssertTrue(
                    !host.IsRefreshing,
                    "snapshot reconciliation completes its reactive refresh");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void
            TestFailedDirectViewportBuildPreservesCommittedRange()
        {
            const string markup =
                "<ItemsControl Name='Rows' Width='180' Height='100' AutoScroll='true' " +
                "ItemKeyPath='Id' Virtualizing='true' VirtualizationThreshold='1' " +
                "OverscanItems='0' FixedItemSize='20' " +
                "ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label AutoSize='true' Text='{Binding Text}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ArrayList rows = CreateToggleRows(100);
                ToggleTextRow throwing =
                    (ToggleTextRow)rows[50];

                host.CreateControl();
                host.SetItems(rows);

                AssertTrue(
                    host.IsVirtualizing,
                    "stable root activates the direct viewport");
                ArrayList committedRecords = host.RenderedItems;
                Label committedFirst = GetItemLabel(host, rows[0]);
                VirtualViewportModel committedModel =
                    host.DirectVirtualViewport;
                long committedExtent = committedModel.TotalExtent;
                int committedStart = host.DirectVirtualRealizedStart;
                int committedEnd = host.DirectVirtualRealizedEnd;
                System.Drawing.Size committedScrollSize =
                    host.AutoScrollMinSize;

                throwing.ThrowOnRead = true;
                Exception surfaced = null;

                try
                {
                    host.ScrollToIndex(50);
                }
                catch (Exception ex)
                {
                    surfaced = ex;
                }

                AssertTrue(surfaced != null, "viewport binding error surfaced");
                AssertSame(rows, GetItemSource(host), "source after viewport failure");
                AssertSame(
                    committedModel,
                    host.DirectVirtualViewport,
                    "failed realization retains the committed logical model");
                AssertSame(
                    committedRecords,
                    host.RenderedItems,
                    "failed realization retains the committed record list");
                AssertSame(
                    committedFirst,
                    GetItemLabel(host, rows[0]),
                    "failed realization retains the committed control tree");
                AssertEqual(
                    committedScrollSize,
                    host.AutoScrollMinSize,
                    "scroll size after viewport failure");
                AssertEqual(
                    committedExtent,
                    host.DirectVirtualViewport.TotalExtent,
                    "logical extent after viewport failure");
                AssertEqual(
                    committedStart,
                    host.DirectVirtualRealizedStart,
                    "realized start after viewport failure");
                AssertEqual(
                    committedEnd,
                    host.DirectVirtualRealizedEnd,
                    "realized end after viewport failure");

                throwing.ThrowOnRead = false;
                host.ScrollToIndex(50);

                AssertEqual(
                    "Toggle 50",
                    GetItemLabel(host, throwing).Text,
                    "the row can be realized after the transient failure");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestFailedProgressiveBuildPreservesEmptyCommit()
        {
            XamlRuntime runtime = LoadSimpleItemsControl(null);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ArrayList committedRows = new ArrayList();
                host.SetItems(committedRows);

                host.ProgressiveRendering = true;
                host.ProgressiveInterval = 60000;
                host.ProgressiveBatchSize = 1;

                ItemRow first = new ItemRow("first", 1, "First");
                ArrayList failingRows = new ArrayList();
                failingRows.Add(first);
                failingRows.Add(new ThrowingTextRow("broken"));
                host.SetItems(failingRows);

                object state = GetPendingRefreshState(host);
                MethodInfo buildMethod = GetRuntimeMethod(
                    "BuildItemsRefreshBatch");
                MethodInfo failMethod = GetRuntimeMethod(
                    "FailItemsRefresh");

                AssertEqual(
                    0,
                    host.RealizedCount,
                    "incomplete progressive controls remain detached");

                Exception buildError = null;

                try
                {
                    buildMethod.Invoke(
                        runtime,
                        new object[] { state, 1 });
                }
                catch (TargetInvocationException ex)
                {
                    buildError = ex.InnerException == null
                        ? ex
                        : ex.InnerException;
                }

                AssertTrue(buildError != null, "later progressive build failed");

                failMethod.Invoke(
                    runtime,
                    new object[] { state, buildError, false });

                AssertSame(
                    committedRows,
                    GetItemSource(host),
                    "empty committed source after progressive failure");
                AssertEqual(0, host.Count, "empty committed count after progressive failure");
                AssertEqual(0, host.RealizedCount, "realized count after progressive failure");
                AssertEqual(0, CountItemLabels(host), "controls after progressive failure");
                AssertTrue(!host.IsRefreshing, "progressive failure refresh state");
                AssertSame(
                    buildError,
                    host.LastRefreshError,
                    "progressive failure retained error");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestFailedVirtualForcePreservesCachedTree()
        {
            const string markup =
                "<ItemsControl Name='Rows' Width='180' Height='100' AutoScroll='true' " +
                "ItemKeyPath='Id' Virtualizing='true' VirtualizationThreshold='1' " +
                "OverscanItems='1' FixedItemSize='20' VirtualizationCacheItems='64' " +
                "ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label AutoSize='true' Text='{Binding Text}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ArrayList rows = CreateToggleRows(100);
                ToggleTextRow first = (ToggleTextRow)rows[0];
                ToggleTextRow failureRow = (ToggleTextRow)rows[80];

                host.CreateControl();
                host.SetItems(rows);

                Label cachedTree = GetItemLabel(host, first);
                host.ScrollToIndex(80);

                AssertTrue(host.VirtualCacheCount > 0, "virtual cache populated");
                AssertTrue(
                    !cachedTree.Visible || cachedTree.Bounds.IsEmpty,
                    "first row moved into virtual cache");
                AssertTrue(
                    GetItemLabelOrNull(host, failureRow) != null,
                    "failing row is in force-rebuild viewport");

                failureRow.ThrowOnRead = true;
                Exception error = null;

                try
                {
                    host.ForceReloadItems();
                }
                catch (Exception ex)
                {
                    error = ex;
                }

                AssertTrue(error != null, "forced virtual build error propagated");
                AssertTrue(!cachedTree.IsDisposed, "cached tree survives failed force reload");
                AssertTrue(host.VirtualCacheCount > 0, "cache survives failed force reload");

                failureRow.ThrowOnRead = false;
                host.ScrollToIndex(0);

                AssertSame(
                    cachedTree,
                    GetItemLabel(host, first),
                    "cached tree restored after failed force reload");

                host.ForceReloadItems();

                AssertNotSame(
                    cachedTree,
                    GetItemLabel(host, first),
                    "successful force reload rebuilds cached tree");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestMixedRebuildPreservesPatchedRootVisibility()
        {
            const string markup =
                "<ItemsControl Name='Rows' ItemKeyPath='Id' Virtualizing='false' " +
                "ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label AutoSize='true' Visibility='{Binding Visibility}' " +
                "Grid.Row='{Binding GridRow}' Text='{Binding Text}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                VisibilityRow first = new VisibilityRow(
                    "first",
                    0,
                    "First",
                    "Visible");
                VisibilityRow second = new VisibilityRow(
                    "second",
                    0,
                    "Second",
                    "Visible");
                ArrayList rows = new ArrayList();
                rows.Add(first);
                rows.Add(second);

                host.SetItems(rows);
                Label firstLabel = GetItemLabel(host, first);
                Label oldSecondLabel = GetItemLabel(host, second);

                first.Visibility = "Hidden";
                second.GridRow = 1;
                host.ReloadItems();

                AssertSame(
                    firstLabel,
                    GetItemLabel(host, first),
                    "visibility row was patched in place");
                AssertTrue(!firstLabel.Visible, "patched root remains hidden");
                AssertNotSame(
                    oldSecondLabel,
                    GetItemLabel(host, second),
                    "structural sibling was rebuilt");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestRootConditionDominatesVisible()
        {
            const string markup =
                "<ItemsControl Name='Rows' ItemKeyPath='Id' Virtualizing='false' " +
                "ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label AutoSize='true' Condition='{Binding Show}' " +
                "               Visible='{Binding Visible}' Text='{Binding Text}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ConditionalVisibilityRow row =
                    new ConditionalVisibilityRow(
                        "row",
                        "Row",
                        true,
                        false);
                ArrayList rows = new ArrayList();
                rows.Add(row);
                host.SetItems(rows);

                Label label = GetItemLabel(host, row);
                AssertTrue(!label.Visible, "initial Visible=false");

                // Condition appears before Visible in XML and in the binding-slot list.
                // Its collapsed state must still dominate the later Visible=true setter.
                row.Show = false;
                row.Visible = true;
                host.ReloadItems();
                AssertTrue(!label.Visible, "Condition=false dominates later Visible=true");

                row.Show = true;
                row.Visible = false;
                host.ReloadItems();
                AssertTrue(!label.Visible, "Visible=false remains effective");

                row.Visible = true;
                host.ReloadItems();
                AssertTrue(label.Visible, "both visibility inputs allow display");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestPostCommitLayoutErrorKeepsCommittedTree()
        {
            XamlRuntime runtime = LoadSimpleItemsControl(null);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ItemRow oldRow = new ItemRow("old", 1, "Old");
                ArrayList oldRows = new ArrayList();
                oldRows.Add(oldRow);
                host.SetItems(oldRows);
                Label oldLabel = GetItemLabel(host, oldRow);

                bool throwOnLayout = false;
                int failedCount = 0;

                host.Layout +=
                    delegate
                    {
                        if (throwOnLayout)
                        {
                            throwOnLayout = false;
                            throw new InvalidOperationException(
                                "Layout callback failed after commit.");
                        }
                    };
                host.RefreshFailed +=
                    delegate
                    {
                        failedCount++;
                    };

                ArrayList newRows = new ArrayList();
                newRows.Add(new ItemRow("new-1", 1, "New 1"));
                newRows.Add(new ItemRow("new-2", 1, "New 2"));
                Exception surfaced = null;
                throwOnLayout = true;

                try
                {
                    host.SetItems(newRows);
                }
                catch (Exception ex)
                {
                    surfaced = ex;
                }

                AssertTrue(surfaced != null, "post-commit layout error surfaced");
                AssertSame(newRows, GetItemSource(host), "post-commit source");
                AssertEqual(2, host.Count, "post-commit count");
                AssertEqual(
                    "New 1",
                    GetItemLabel(host, newRows[0]).Text,
                    "post-commit first row");
                AssertTrue(oldLabel.IsDisposed, "old row disposed after layout error");
                AssertEqual(0, failedCount, "post-commit failure event count");
                AssertEqual(null, host.LastRefreshError, "post-commit LastRefreshError");
                AssertTrue(!host.IsRefreshing, "post-commit refresh state");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestThrowingCompletionKeepsCommittedTree()
        {
            XamlRuntime runtime = LoadSimpleItemsControl(null);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ArrayList committedRows = new ArrayList();
                committedRows.Add(new ItemRow("old", 1, "Old"));
                host.SetItems(committedRows);

                ArrayList requestedRows = new ArrayList();
                requestedRows.Add(new ItemRow("new", 1, "New"));
                int failedCount = 0;

                host.RefreshFailed +=
                    delegate
                    {
                        failedCount++;
                    };
                host.RefreshCompleted +=
                    delegate
                    {
                        if (Object.ReferenceEquals(
                            GetItemSource(host),
                            requestedRows))
                        {
                            throw new InvalidOperationException(
                                "Completion callback failed.");
                        }
                    };

                Exception surfaced = null;

                try
                {
                    host.SetItems(requestedRows);
                }
                catch (Exception ex)
                {
                    surfaced = ex;
                }

                AssertTrue(surfaced != null, "completion callback error surfaced");
                AssertSame(requestedRows, GetItemSource(host), "completion source");
                AssertEqual(1, host.Count, "completion committed count");
                AssertEqual("New", GetItemLabel(host, requestedRows[0]).Text, "completion row");
                AssertEqual(0, failedCount, "completion failure event count");
                AssertEqual(null, host.LastRefreshError, "completion LastRefreshError");
                AssertTrue(!host.IsRefreshing, "completion refresh state");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestReentrantCompletionKeepsNestedRefresh()
        {
            XamlRuntime runtime = LoadSimpleItemsControl(null);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ArrayList oldRows = new ArrayList();
                oldRows.Add(new ItemRow("old", 1, "Old"));
                host.SetItems(oldRows);

                ArrayList outerRows = new ArrayList();
                outerRows.Add(new ItemRow("outer", 1, "Outer"));
                ArrayList nestedRows = CreatePrefixedRows("nested", 3);
                bool handled = false;

                host.RefreshCompleted +=
                    delegate
                    {
                        if (handled ||
                            !Object.ReferenceEquals(
                                GetItemSource(host),
                                outerRows))
                        {
                            return;
                        }

                        handled = true;
                        host.ProgressiveRendering = true;
                        host.ProgressiveInterval = 60000;
                        host.ProgressiveBatchSize = 1;
                        host.SetItems(nestedRows);
                        throw new InvalidOperationException(
                            "Outer completion callback failed.");
                    };

                Exception surfaced = null;

                try
                {
                    host.SetItems(outerRows);
                }
                catch (Exception ex)
                {
                    surfaced = ex;
                }

                AssertTrue(surfaced != null, "outer completion error surfaced");
                AssertSame(nestedRows, GetItemSource(host), "nested completion source");
                AssertTrue(host.IsRefreshing, "nested completion refresh remains active");
                AssertEqual(null, host.LastRefreshError, "nested completion error state");

                host.ProgressiveRendering = false;
                host.ReloadItems();

                AssertSame(nestedRows, GetItemSource(host), "nested completion final source");
                AssertEqual(3, host.Count, "nested completion final count");
                AssertTrue(!host.IsRefreshing, "nested completion finalized");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestReentrantFailureKeepsNestedRefresh()
        {
            XamlRuntime runtime = LoadSimpleItemsControl(null);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ArrayList oldRows = new ArrayList();
                oldRows.Add(new ItemRow("old", 1, "Old"));
                host.SetItems(oldRows);

                ArrayList nestedRows = CreatePrefixedRows("recovery", 3);
                bool handled = false;

                host.RefreshFailed +=
                    delegate
                    {
                        if (handled)
                            return;

                        handled = true;
                        host.ProgressiveRendering = true;
                        host.ProgressiveInterval = 60000;
                        host.ProgressiveBatchSize = 1;
                        host.SetItems(nestedRows);
                        throw new InvalidOperationException(
                            "Failure callback failed after starting recovery.");
                    };

                ArrayList failingRows = new ArrayList();
                failingRows.Add(new ThrowingTextRow("broken"));
                Exception surfaced = null;

                try
                {
                    host.SetItems(failingRows);
                }
                catch (Exception ex)
                {
                    surfaced = ex;
                }

                AssertTrue(surfaced != null, "original item failure surfaced");
                AssertSame(nestedRows, GetItemSource(host), "nested recovery source");
                AssertTrue(host.IsRefreshing, "nested recovery remains active");
                AssertEqual(null, host.LastRefreshError, "nested recovery error state");

                host.ProgressiveRendering = false;
                host.ReloadItems();

                AssertSame(nestedRows, GetItemSource(host), "nested recovery final source");
                AssertEqual(3, host.Count, "nested recovery final count");
                AssertTrue(!host.IsRefreshing, "nested recovery finalized");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void
            TestRenderBindingDeactivationContinuesAfterFailure()
        {
            const string markup =
                "<ItemsControl Name='Rows' ItemKeyPath='Id' " +
                "Virtualizing='false' ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <StackPanel>" +
                "      <Label Condition='{Binding Show}' />" +
                "      <Label Condition='{Binding Show}' />" +
                "    </StackPanel>" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ReactiveItemRow row =
                    new ReactiveItemRow(
                        "slot-deactivation",
                        "Unused",
                        true);
                ArrayList rows = new ArrayList();
                rows.Add(row);

                CreateHandleAndDrainReactiveCallbacks(
                    runtime.RootControl);
                host.SetItems(rows);
                row.Show.Value = false;
                DrainReactiveCallbacks(runtime.RootControl);

                object record = GetOnlyRenderedItemRecord(host);
                ArrayList slots = GetRenderBindingSlots(record);
                AssertEqual(
                    2,
                    slots.Count,
                    "two independent condition slots are active");

                object successfulSlot = slots[0];
                object failingSlot = slots[slots.Count - 1];
                Control successfulTarget =
                    GetRenderBindingSlotTarget(successfulSlot);
                Control failingTarget =
                    GetRenderBindingSlotTarget(failingSlot);

                AssertTrue(
                    !successfulTarget.Visible && !failingTarget.Visible,
                    "both conditional targets start hidden");

                EventHandler throwOnRestore = null;
                throwOnRestore =
                    delegate
                    {
                        failingTarget.VisibleChanged -= throwOnRestore;
                        throw new InvalidOperationException(
                            "Injected slot visibility failure.");
                    };
                failingTarget.VisibleChanged += throwOnRestore;

                MethodInfo deactivateMethod =
                    GetRuntimeMethod("DeactivateRenderBindingSlots");
                Exception surfaced = null;

                try
                {
                    deactivateMethod.Invoke(
                        runtime,
                        new object[] { slots });
                }
                catch (TargetInvocationException ex)
                {
                    surfaced = ex.InnerException == null
                        ? ex
                        : ex.InnerException;
                }

                AssertTrue(
                    surfaced != null,
                    "temporary slot deactivation surfaces the first failure");
                AssertFieldIsNull(successfulSlot, "Host");
                AssertSame(
                    host,
                    GetInstanceFieldValue(failingSlot, "Host"),
                    "failed temporary slot retains retry metadata");
                AssertFieldIsNull(
                    successfulSlot,
                    "ObservableRegistration");
                AssertFieldIsNull(
                    failingSlot,
                    "ObservableRegistration");
                AssertEqual(
                    0,
                    GetPropertyBindingSubscriberCount(row.Show),
                    "all independent source subscriptions are detached");

                deactivateMethod.Invoke(
                    runtime,
                    new object[] { slots });

                AssertFieldIsNull(failingSlot, "Host");
                AssertFieldIsNull(failingSlot, "DataContext");
                AssertFieldIsNull(failingSlot, "PathResult");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void
            TestRenderedRecordRetirementContinuesAfterSlotFailure()
        {
            const string markup =
                "<ItemsControl Name='Rows' ItemKeyPath='Id' " +
                "Virtualizing='false' ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <StackPanel>" +
                "      <Label Condition='{Binding Show}' />" +
                "      <Label Condition='{Binding Show}' />" +
                "    </StackPanel>" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ReactiveItemRow row =
                    new ReactiveItemRow(
                        "record-retirement",
                        "Unused",
                        true);
                ArrayList rows = new ArrayList();
                rows.Add(row);

                CreateHandleAndDrainReactiveCallbacks(
                    runtime.RootControl);
                host.SetItems(rows);
                row.Show.Value = false;
                DrainReactiveCallbacks(runtime.RootControl);

                object record = GetOnlyRenderedItemRecord(host);
                ArrayList slots = GetRenderBindingSlots(record);
                AssertEqual(
                    2,
                    slots.Count,
                    "two record-retirement slots are active");

                Control recordControl =
                    GetInstanceFieldValue(record, "Control") as Control;
                Control firstTarget =
                    GetRenderBindingSlotTarget(slots[0]);
                Control failingTarget =
                    GetRenderBindingSlotTarget(
                        slots[slots.Count - 1]);
                AssertTrue(
                    recordControl != null,
                    "rendered record owns a native control");

                EventHandler throwOnRestore = null;
                throwOnRestore =
                    delegate
                    {
                        failingTarget.VisibleChanged -= throwOnRestore;
                        throw new InvalidOperationException(
                            "Injected retirement visibility failure.");
                    };
                failingTarget.VisibleChanged += throwOnRestore;

                MethodInfo disposeMethod =
                    GetRuntimeMethod("DisposeRenderedItemRecord");
                Exception surfaced = null;

                try
                {
                    disposeMethod.Invoke(
                        runtime,
                        new object[] { record });
                }
                catch (TargetInvocationException ex)
                {
                    surfaced = ex.InnerException == null
                        ? ex
                        : ex.InnerException;
                }

                AssertTrue(
                    surfaced != null,
                    "record retirement surfaces the slot cleanup failure");
                AssertTrue(
                    recordControl.IsDisposed &&
                    firstTarget.IsDisposed &&
                    failingTarget.IsDisposed,
                    "record retirement still disposes the complete native tree");
                AssertEqual(
                    0,
                    GetPropertyBindingSubscriberCount(row.Show),
                    "record retirement detaches every source subscription");
                AssertFieldIsNull(record, "BindingSlots");
                AssertFieldIsNull(record, "Control");
                AssertFieldIsNull(record, "Item");
                AssertFieldIsNull(record, "FunctionResults");
                AssertFieldIsNull(record, "VersionValue");

                int i;

                for (i = 0; i < slots.Count; i++)
                {
                    object slot = slots[i];
                    AssertFieldIsNull(slot, "ObservableRegistration");
                    AssertFieldIsNull(slot, "Host");
                    AssertFieldIsNull(slot, "DataContext");
                    AssertFieldIsNull(slot, "EventTarget");
                    AssertFieldIsNull(slot, "PathResult");
                    AssertFieldIsNull(slot, "Target");
                    AssertFieldIsNull(slot, "LastValue");
                }
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestRuntimeDisposalCancelsProgressiveRefresh()
        {
            XamlRuntime runtime = LoadSimpleItemsControl(null);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");

                host.ProgressiveRendering = true;
                host.ProgressiveInterval = 60000;
                host.ProgressiveBatchSize = 1;
                host.SetItems(CreateRows(3));

                AssertTrue(host.IsRefreshing, "progressive refresh queued before disposal");

                runtime.Dispose();

                AssertTrue(
                    !host.IsRefreshing,
                    "runtime disposal clears progressive refresh state");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestRuntimeDisposalClearsQueuedReactiveItemPatch()
        {
            const string markup =
                "<ItemsControl Name='Rows' ItemKeyPath='Id' " +
                "Virtualizing='false' ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label AutoSize='true' Text='{Binding Text}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.GetItemsControl("Rows");
                ReactiveItemRow row =
                    new ReactiveItemRow(
                        "queued-disposal",
                        "Before disposal",
                        true);
                ArrayList rows = new ArrayList();
                rows.Add(row);

                CreateHandleAndDrainReactiveCallbacks(
                    runtime.RootControl);
                host.SetItems(rows);

                row.Text.Value = "Queued before disposal";
                DrainObservableBindingDispatch(runtime);

                AssertEqual(
                    1,
                    GetPendingReactiveItemUpdateCount(runtime),
                    "reactive item patch is queued before disposal");

                runtime.Dispose();

                AssertEqual(
                    0,
                    GetPendingReactiveItemUpdateCount(runtime),
                    "runtime disposal clears queued reactive item patches");
                AssertRenderedItemBindingsReleased(host);
                AssertEqual(
                    0,
                    GetPropertyBindingSubscriberCount(row.Text),
                    "runtime disposal detaches the item source subscription");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void AdvanceProgressiveTimer(
            XamlRuntime.ItemsControl host)
        {
            object state = GetPendingRefreshState(host);
            FieldInfo timerField = state.GetType().GetField(
                "Timer",
                BindingFlags.Instance | BindingFlags.Public);

            AssertTrue(timerField != null, "progressive timer field found");

            System.Windows.Forms.Timer timer =
                timerField.GetValue(state) as System.Windows.Forms.Timer;
            AssertTrue(timer != null, "progressive timer found");

            MethodInfo tickMethod =
                typeof(System.Windows.Forms.Timer).GetMethod(
                "OnTick",
                BindingFlags.Instance | BindingFlags.NonPublic);

            AssertTrue(tickMethod != null, "progressive timer tick found");
            tickMethod.Invoke(timer, new object[] { EventArgs.Empty });
        }

        private static object GetPendingRefreshState(
            XamlRuntime.ItemsControl host)
        {
            FieldInfo pendingField = typeof(XamlRuntime.ItemsControl).GetField(
                "PendingRefresh",
                BindingFlags.Instance | BindingFlags.NonPublic);

            AssertTrue(pendingField != null, "pending refresh field found");

            object state = pendingField.GetValue(host);
            AssertTrue(state != null, "pending refresh state found");
            return state;
        }

        private static MethodInfo GetRuntimeMethod(string name)
        {
            MethodInfo method = typeof(XamlRuntime).GetMethod(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);

            AssertTrue(method != null, name + " method found");
            return method;
        }

        private static XamlRuntime LoadDirectVirtualThresholdFixture(
            int overscanItems,
            out XamlRuntime.ItemsControl host)
        {
            string markup =
                "<ItemsControl Name='Rows' Width='180' Height='100' AutoScroll='true' " +
                "ItemKeyPath='Id' Virtualizing='true' VirtualizationThreshold='32' " +
                "OverscanItems='" + overscanItems.ToString() + "' FixedItemSize='20' " +
                "ProgressiveRendering='true' ProgressiveBatchSize='1' " +
                "ProgressiveInterval='60000' ProgressiveTimeBudgetMs='1'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label AutoSize='true' Text='{Binding Text}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            XamlRuntime runtime = XamlRuntime.Load(markup);
            host = runtime.GetItemsControl("Rows");

            try
            {
                host.CreateControl();
                host.SetItems(CreateRows(32));

                AssertEqual(
                    32,
                    host.Count,
                    "fixture uses the exact virtualization threshold");
                AssertTrue(
                    host.IsVirtualizing,
                    "exact-threshold fixture enables virtualization");
                AssertTrue(
                    !host.IsRefreshing &&
                    !host.DirectVirtualRefreshRunning,
                    "direct realization completes synchronously");
                AssertTrue(
                    host.DirectVirtualViewport != null &&
                    host.DirectVirtualViewport.Count == 32,
                    "fixture publishes the complete logical model");
                AssertTrue(
                    host.DirectVirtualRealizedStart >= 0 &&
                    host.DirectVirtualRealizedEnd >=
                        host.DirectVirtualRealizedStart,
                    "fixture publishes an initial realized range");
                AssertDirectVirtualIndicesSorted(host);
                return runtime;
            }
            catch
            {
                DisposeRuntime(runtime);
                throw;
            }
        }

        private static void AssertDirectVirtualIndicesSorted(
            XamlRuntime.ItemsControl host)
        {
            AssertTrue(
                host.DirectVirtualActive &&
                host.DirectVirtualViewport != null,
                "direct viewport is active");

            int previous = -1;
            int i;

            for (i = 0; i < host.RenderedItems.Count; i++)
            {
                object record = host.RenderedItems[i];
                int logicalIndex = (int)GetInstanceFieldValue(
                    record,
                    "LogicalIndex");

                AssertTrue(
                    logicalIndex > previous,
                    "direct realized records stay sorted by logical index");
                previous = logicalIndex;
            }

            if (host.RenderedItems.Count == 0)
            {
                AssertEqual(
                    -1,
                    host.DirectVirtualRealizedStart,
                    "empty direct range start");
                AssertEqual(
                    -1,
                    host.DirectVirtualRealizedEnd,
                    "empty direct range end");
                return;
            }

            AssertEqual(
                host.DirectVirtualRealizedStart,
                (int)GetInstanceFieldValue(
                    host.RenderedItems[0],
                    "LogicalIndex"),
                "published start matches the first record");
            AssertEqual(
                host.DirectVirtualRealizedEnd,
                (int)GetInstanceFieldValue(
                    host.RenderedItems[host.RenderedItems.Count - 1],
                    "LogicalIndex"),
                "published end matches the last record");
        }

        private static void AssertDirectVirtualViewportCovered(
            XamlRuntime.ItemsControl host,
            string message)
        {
            VirtualViewportModel model = host.DirectVirtualViewport;

            AssertTrue(
                host.DirectVirtualActive && model != null,
                message + " keeps the direct viewport active");

            int viewportAxis = host.Orientation == Orientation.Vertical
                ? host.ClientSize.Height
                : host.ClientSize.Width;

            if (model.Count == 0 || viewportAxis <= 0)
                return;

            Point nativeScroll = host.AutoScrollPosition;
            int logicalScroll = host.Orientation == Orientation.Vertical
                ? Math.Max(0, -nativeScroll.Y)
                : Math.Max(0, -nativeScroll.X);
            long contentEnd = model.TotalExtent;

            AssertTrue(
                logicalScroll < contentEnd,
                message + " leaves the native origin inside content");

            long viewportEnd = Math.Min(
                contentEnd,
                (long)logicalScroll + (long)viewportAxis);
            int first = model.FindIndexAtOffset(logicalScroll);
            int last = model.FindIndexAtOffset(viewportEnd - 1L);

            AssertTrue(
                host.DirectVirtualRealizedStart <= first &&
                host.DirectVirtualRealizedEnd >= last,
                message + " realizes every final visible index");

            int coveredEnd = 0;
            int expectedIndex = first;
            int i;

            for (i = 0; i < host.RenderedItems.Count; i++)
            {
                object record = host.RenderedItems[i];
                int logicalIndex = (int)GetInstanceFieldValue(
                    record,
                    "LogicalIndex");

                if (logicalIndex < first || logicalIndex > last)
                    continue;

                AssertEqual(
                    expectedIndex,
                    logicalIndex,
                    message + " keeps visible records contiguous");

                Control control =
                    GetInstanceFieldValue(record, "Control") as Control;
                AssertTrue(
                    control != null && !control.IsDisposed,
                    message + " retains each visible control");

                int start = host.Orientation == Orientation.Vertical
                    ? control.Bounds.Top
                    : control.Bounds.Left;
                int end = host.Orientation == Orientation.Vertical
                    ? control.Bounds.Bottom
                    : control.Bounds.Right;

                AssertTrue(
                    end > 0 && start < viewportAxis,
                    message + " places each visible control in the client");

                if (logicalIndex == first)
                {
                    AssertTrue(
                        start <= 0,
                        message + " has no blank leading viewport gap");
                }
                else
                {
                    AssertTrue(
                        start <= coveredEnd,
                        message + " has no blank gap between visible controls");
                }

                coveredEnd = Math.Max(coveredEnd, end);
                expectedIndex++;
            }

            AssertEqual(
                last + 1,
                expectedIndex,
                message + " publishes a control for each visible index");
            AssertTrue(
                coveredEnd >= Math.Min(
                    viewportAxis,
                    (int)Math.Min(
                        (long)Int32.MaxValue,
                        contentEnd - (long)logicalScroll)),
                message + " covers the final viewport without a large gap");
        }

        private static ArrayList CreateRows(int count)
        {
            ArrayList rows = new ArrayList();
            int i;

            for (i = 0; i < count; i++)
            {
                rows.Add(
                    new ItemRow(
                        "row-" + i,
                        1,
                        "Row " + i));
            }

            return rows;
        }

        private static ArrayList CreateToggleRows(int count)
        {
            ArrayList rows = new ArrayList();
            int i;

            for (i = 0; i < count; i++)
            {
                rows.Add(
                    new ToggleTextRow(
                        "toggle-" + i,
                        "Toggle " + i));
            }

            return rows;
        }

        private static ArrayList CreateConditionalRows(int count)
        {
            ArrayList rows = new ArrayList();
            int i;

            for (i = 0; i < count; i++)
            {
                rows.Add(
                    new ConditionalToggleRow(
                        "conditional-" + i,
                        "Conditional " + i));
            }

            return rows;
        }

        private static ArrayList CreatePrefixedRows(
            string prefix,
            int count)
        {
            ArrayList rows = new ArrayList();
            int i;

            for (i = 0; i < count; i++)
            {
                rows.Add(
                    new ItemRow(
                        prefix + "-" + i,
                        1,
                        prefix + " " + i));
            }

            return rows;
        }

        private static object GetItemSource(
            XamlRuntime.ItemsControl host)
        {
            return host.ItemsSource;
        }

        private static void CreateHandleAndDrainReactiveCallbacks(
            Control root)
        {
            AssertTrue(root != null, "reactive root control");

            if (!root.IsHandleCreated)
                root.CreateControl();

            AssertTrue(root.IsHandleCreated, "reactive root handle created");
            DrainReactiveCallbacks(root);
        }

        private static void DrainReactiveCallbacks(Control root)
        {
            AssertTrue(root != null, "reactive dispatch root");
            AssertTrue(!root.IsDisposed, "reactive dispatch root is active");
            AssertTrue(
                root.IsHandleCreated,
                "reactive dispatch root has a handle");

            // Reactive source processing and the resulting ItemsControl reload can
            // each enqueue another pass. Sentinels preserve message ordering without
            // relying on sleeps or timing-sensitive polling.
            int round;

            for (round = 0; round < 6; round++)
            {
                bool reached = false;

                root.BeginInvoke(
                    new MethodInvoker(
                        delegate
                        {
                            reached = true;
                        }));

                int iterations = 0;

                while (!reached && iterations < 1024)
                {
                    Application.DoEvents();
                    iterations++;
                }

                AssertTrue(reached, "reactive dispatch sentinel reached");
            }
        }

        private static void DrainObservableBindingDispatch(
            XamlRuntime runtime)
        {
            MethodInfo drainMethod = typeof(XamlRuntime).GetMethod(
                "DrainObservableBindingChangesSynchronously",
                BindingFlags.Instance | BindingFlags.NonPublic);

            AssertTrue(
                drainMethod != null,
                "observable binding dispatch method found");
            drainMethod.Invoke(runtime, null);
        }

        private static int GetPendingReactiveItemUpdateCount(
            XamlRuntime runtime)
        {
            FieldInfo pendingField = typeof(XamlRuntime).GetField(
                "_pendingReactiveItemUpdates",
                BindingFlags.Instance | BindingFlags.NonPublic);

            AssertTrue(
                pendingField != null,
                "pending reactive item update field found");

            Hashtable pending =
                pendingField.GetValue(runtime) as Hashtable;

            AssertTrue(
                pending != null,
                "pending reactive item update table found");
            return pending.Count;
        }

        private static void AssertRenderedItemBindingsReleased(
            XamlRuntime.ItemsControl host)
        {
            FieldInfo renderedField = typeof(XamlRuntime.ItemsControl).GetField(
                "RenderedItems",
                BindingFlags.Instance | BindingFlags.NonPublic);

            AssertTrue(renderedField != null, "rendered item list field found");

            ArrayList records =
                renderedField.GetValue(host) as ArrayList;

            // Runtime-owned root disposal retires and clears the complete item
            // tree after deactivating its slots. A still-retained record list is
            // valid only for cleanup paths that preserve the host for retry.
            if (records == null || records.Count == 0)
                return;

            int slotCount = 0;
            int recordIndex;

            for (recordIndex = 0;
                 records != null && recordIndex < records.Count;
                 recordIndex++)
            {
                object record = records[recordIndex];

                if (record == null)
                    continue;

                FieldInfo slotsField = record.GetType().GetField(
                    "BindingSlots",
                    BindingFlags.Instance | BindingFlags.Public);

                AssertTrue(slotsField != null, "render binding slot list found");

                ArrayList slots = slotsField.GetValue(record) as ArrayList;
                int slotIndex;

                for (slotIndex = 0;
                     slots != null && slotIndex < slots.Count;
                     slotIndex++)
                {
                    object slot = slots[slotIndex];

                    if (slot == null)
                        continue;

                    slotCount++;
                    AssertFieldIsNull(slot, "Host");
                    AssertFieldIsNull(slot, "DataContext");
                    AssertFieldIsNull(slot, "PathResult");
                    AssertFieldIsNull(
                        slot,
                        "ObservableRegistration");
                }
            }

            AssertTrue(
                slotCount > 0,
                "at least one rendered item binding slot was inspected");
        }

        private static object GetOnlyRenderedItemRecord(
            XamlRuntime.ItemsControl host)
        {
            FieldInfo renderedField = typeof(XamlRuntime.ItemsControl).GetField(
                "RenderedItems",
                BindingFlags.Instance | BindingFlags.NonPublic);

            AssertTrue(renderedField != null, "rendered item list field found");

            ArrayList records =
                renderedField.GetValue(host) as ArrayList;

            AssertTrue(records != null, "rendered item list found");
            AssertEqual(1, records.Count, "one rendered item record");
            AssertTrue(records[0] != null, "rendered item record found");
            return records[0];
        }

        private static ArrayList GetRenderBindingSlots(
            object record)
        {
            ArrayList slots =
                GetInstanceFieldValue(record, "BindingSlots") as ArrayList;

            AssertTrue(slots != null, "render binding slot list found");
            return slots;
        }

        private static Control GetRenderBindingSlotTarget(
            object slot)
        {
            Control target =
                GetInstanceFieldValue(slot, "Target") as Control;

            AssertTrue(target != null, "render binding slot target found");
            return target;
        }

        private static object GetInstanceFieldValue(
            object instance,
            string fieldName)
        {
            AssertTrue(instance != null, "field owner found: " + fieldName);

            FieldInfo field = FindInstanceField(
                instance.GetType(),
                fieldName);

            AssertTrue(field != null, "instance field found: " + fieldName);
            return field.GetValue(instance);
        }

        private static void SetInstanceFieldValue(
            object instance,
            string fieldName,
            object value)
        {
            AssertTrue(instance != null, "field owner found: " + fieldName);

            FieldInfo field = FindInstanceField(
                instance.GetType(),
                fieldName);

            AssertTrue(field != null, "instance field found: " + fieldName);
            field.SetValue(instance, value);
        }

        private static FieldInfo FindInstanceField(
            Type type,
            string fieldName)
        {
            Type current = type;

            while (current != null)
            {
                FieldInfo field = current.GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

                if (field != null)
                    return field;

                current = current.BaseType;
            }

            return null;
        }

        private static void AssertFieldIsNull(
            object instance,
            string fieldName)
        {
            FieldInfo field = instance.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic);

            AssertTrue(
                field != null,
                "render binding slot field found: " + fieldName);
            AssertEqual(
                null,
                field.GetValue(instance),
                "render binding slot field released: " + fieldName);
        }

        private static int GetPropertyBindingSubscriberCount(
            object binding)
        {
            AssertTrue(binding != null, "PropertyBinding instance");

            FieldInfo handlersField = binding.GetType().GetField(
                "_valueChanged",
                BindingFlags.Instance | BindingFlags.NonPublic);

            AssertTrue(
                handlersField != null,
                "PropertyBinding ValueChanged field found");

            Delegate handlers = handlersField.GetValue(binding) as Delegate;

            return handlers == null
                ? 0
                : handlers.GetInvocationList().Length;
        }

        private static XamlRuntime LoadSimpleItemsControl(
            string versionPath)
        {
            string versionAttribute = String.IsNullOrEmpty(versionPath)
                ? String.Empty
                : " ItemVersionPath='" + versionPath + "'";

            return XamlRuntime.Load(
                "<ItemsControl Name='Rows' ItemKeyPath='Id'" +
                versionAttribute +
                " Virtualizing='false' ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label AutoSize='true' Text='{Binding Text}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>");
        }

        private static Label GetItemLabel(
            XamlRuntime.ItemsControl host,
            object item)
        {
            Label label = GetItemLabelOrNull(host, item);

            if (label == null)
            {
                throw new InvalidOperationException(
                    "No realized Label was found for the requested item.");
            }

            return label;
        }

        private static bool IsItemRealized(
            XamlRuntime.ItemsControl host,
            object item)
        {
            int i;

            for (i = 0; i < host.Controls.Count; i++)
            {
                if (Object.ReferenceEquals(
                    host.Controls[i].Tag,
                    item))
                {
                    return true;
                }
            }

            return false;
        }

        private static Label GetItemLabelOrNull(
            XamlRuntime.ItemsControl host,
            object item)
        {
            int i;

            for (i = 0; i < host.Controls.Count; i++)
            {
                Label label = FindItemLabel(
                    host.Controls[i],
                    item);

                if (label != null)
                    return label;
            }

            return null;
        }

        private static Label FindItemLabel(
            Control control,
            object item)
        {
            if (control == null)
                return null;

            Label label = control as Label;

            if (label != null && Object.ReferenceEquals(label.Tag, item))
                return label;

            int i;

            for (i = 0; i < control.Controls.Count; i++)
            {
                label = FindItemLabel(
                    control.Controls[i],
                    item);

                if (label != null)
                    return label;
            }

            return null;
        }

        private static TextBox GetItemTextBox(
            XamlRuntime.ItemsControl host,
            object item)
        {
            int i;

            for (i = 0; i < host.Controls.Count; i++)
            {
                TextBox textBox = host.Controls[i] as TextBox;

                if (textBox != null &&
                    Object.ReferenceEquals(textBox.Tag, item))
                {
                    return textBox;
                }
            }

            throw new InvalidOperationException(
                "No realized TextBox was found for the requested item.");
        }

        private static EqualLifecycleControl GetEqualLifecycleControl(
            XamlRuntime.ItemsControl host,
            object item)
        {
            int i;

            for (i = 0; i < host.Controls.Count; i++)
            {
                EqualLifecycleControl control =
                    host.Controls[i] as EqualLifecycleControl;

                if (control != null &&
                    Object.ReferenceEquals(control.Tag, item))
                {
                    return control;
                }
            }

            throw new InvalidOperationException(
                "No realized EqualLifecycleControl was found for the requested item.");
        }

        private static int CountEqualLifecycleControls(
            XamlRuntime.ItemsControl host)
        {
            int count = 0;
            int i;

            for (i = 0; i < host.Controls.Count; i++)
            {
                if (host.Controls[i] is EqualLifecycleControl)
                    count++;
            }

            return count;
        }

        private static int CountItemLabels(
            XamlRuntime.ItemsControl host)
        {
            int count = 0;
            int i;

            for (i = 0; i < host.Controls.Count; i++)
            {
                if (host.Controls[i] is Label)
                    count++;
            }

            return count;
        }

        private static void DisposeRuntime(XamlRuntime runtime)
        {
            if (runtime == null)
                return;

            Control root = runtime.RootControl;

            if (root != null && !root.IsDisposed)
                root.Dispose();

            runtime.Dispose();
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException("Assertion failed: " + message + ".");
        }

        private static void AssertEqual(
            object expected,
            object actual,
            string message)
        {
            if (!Object.Equals(expected, actual))
            {
                throw new InvalidOperationException(
                    "Assertion failed: " +
                    message +
                    ". Expected <" +
                    expected +
                    ">, actual <" +
                    actual +
                    ">.");
            }
        }

        private static void AssertSame(
            object expected,
            object actual,
            string message)
        {
            if (!Object.ReferenceEquals(expected, actual))
            {
                throw new InvalidOperationException(
                    "Assertion failed: " + message + ". Expected the same instance.");
            }
        }

        private static void AssertNotSame(
            object first,
            object second,
            string message)
        {
            if (Object.ReferenceEquals(first, second))
            {
                throw new InvalidOperationException(
                    "Assertion failed: " + message + ". Expected different instances.");
            }
        }
    }

    public sealed class EqualLifecycleControl : Control
    {
        private bool _disposeCounted;

        public static int CreatedCount;
        public static int DisposedCount;

        public EqualLifecycleControl()
        {
            CreatedCount++;
        }

        public static void ResetLifetimeCounts()
        {
            CreatedCount = 0;
            DisposedCount = 0;
        }

        public override bool Equals(object value)
        {
            return value is EqualLifecycleControl;
        }

        public override int GetHashCode()
        {
            return 1;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_disposeCounted)
            {
                _disposeCounted = true;
                DisposedCount++;
            }

            base.Dispose(disposing);
        }
    }

    public sealed class FaultingRollbackLabel : Label
    {
        public string ThrowBeforeText;
        public int PendingAssignmentCount;
        public bool SawObjectMarkerText;

        public override string Text
        {
            get { return base.Text; }
            set
            {
                if (String.Equals(
                    value,
                    "System.Object",
                    StringComparison.Ordinal))
                {
                    SawObjectMarkerText = true;
                }

                if (String.Equals(
                    value,
                    "Pending",
                    StringComparison.Ordinal))
                {
                    PendingAssignmentCount++;
                }

                if (!String.IsNullOrEmpty(ThrowBeforeText) &&
                    String.Equals(
                        value,
                        ThrowBeforeText,
                        StringComparison.Ordinal))
                {
                    ThrowBeforeText = null;
                    throw new InvalidOperationException(
                        "Text setter failed before replacing the current value.");
                }

                base.Text = value;
            }
        }
    }

    public sealed class ReentrantTextLabel : Label
    {
        public static XamlRuntime.ItemsControl Host;
        public static IEnumerable NestedItems;
        public static string RollbackText;
        public static bool ReenterOnRollback;

        public override string Text
        {
            get { return base.Text; }
            set
            {
                bool shouldReenter =
                    ReenterOnRollback &&
                    String.Equals(
                        value,
                        RollbackText,
                        StringComparison.Ordinal);

                base.Text = value;

                if (shouldReenter)
                {
                    ReenterOnRollback = false;

                    if (Host != null)
                        Host.SetItems(NestedItems);
                }
            }
        }
    }
}
