using System;
using System.Collections;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Xml;
using System.Windows.Forms;

namespace WinFormsXaml
{
    public sealed partial class XamlRuntime : IDisposable
    {
        // ============================================================
        // ITEMS CONTROL / DATA TEMPLATE
        // ============================================================

        /// <summary>
        /// A high-performance repeating item host for Windows Forms.
        /// Call SetItems with any IEnumerable to render the template once per
        /// item. Bind template attributes with {Binding Property}.
        /// Code-behind functions are also supported:
        /// {Function GetImage(.)}, {Function FormatTitle(Title, Count)}, or
        /// {Function GetImage(.)}. Functions may return real
        /// CLR objects such as System.Drawing.Image.
        /// </summary>
        public partial class ItemsControl : Panel
        {
            internal XamlRuntime Runtime;
            internal XmlElement TemplateRoot;
            internal string TemplateOuterXml;
            internal object TemplateEventTarget;
            private ItemTemplateDeclarationContext _templateContext;
            internal object TemplateContext
            {
                get { return _templateContext; }
            }
            internal ArrayList ItemValues;
            internal ArrayList CommittedItemValues;
            internal ArrayList RenderedItems;
            private Hashtable _renderedItemRecordsByControl;
            private long _renderedItemPublicationRevision;
            private bool _renderedItemRecordIndexDiagnosticsEnabled;
            private long _renderedItemRecordIndexLookupCount;
            private long _renderedItemRecordIndexProbeCount;
            private long _itemControlReferenceScanProbeCount;
            internal ArrayList TemplateFunctionExpressions;
            internal object PendingRefresh;
            internal int RefreshGeneration;
            internal bool ContentRightToLeft;

            private Orientation _orientation;
            private int _spacing;
            private bool _wrap;
            private FlexJustifyContent _justifyContent;
            private FlexAlignItems _alignItems;
            private bool _runtimeLayoutInProgress;
            private long _itemsMeasureEpoch;
            private long _reentrantItemsLayoutRevision;
            internal WrappedItemsLayoutPlan WrappedLayoutScratchPlan;
            internal bool WrappedLayoutScratchInUse;
            private bool _isRefreshing;
            private Exception _lastRefreshError;
            private int _itemsRollbackDepth;
            private bool _deferredItemsRequest;
            private bool _deferredItemsHasSource;
            private IEnumerable _deferredItemsSource;
            private bool _deferredItemsForceRebuild;
            private readonly int _ownerThreadId;

            private readonly object _itemSourceSync;
            private IEnumerable _itemSource;
            private IEnumerable _committedItemSource;
            private IBindingList _observedItemList;
            private ItemSourceListChangedForwarder
                _observedItemListChangedForwarder;
            private ListChangedEventHandler _observedItemListChanged;
            private IBindingList _detachedCommittedItemList;
            private int _itemSourceSubscriptionEpoch;
            private int _pendingItemSourceReloadEpoch;
            private bool _itemSourceReloadPending;
            private bool _itemSourceReloadPosted;
            private bool _itemSourceFullReloadPending;
            private readonly Hashtable _pendingItemSourceChangedIndices;
            private readonly ArrayList _pendingItemSourceChanges;
            private bool _itemSourceHandleReady;
            private bool _itemsSourceInitializationComplete;
            private bool _itemsSourceDisposed;

            private string _itemKeyPath;
            private string _itemVersionPath;
            private bool _reuseItems;
            private bool _reevaluateFunctionsOnRefresh;
            private bool _progressiveRendering;
            private int _progressiveBatchSize;
            private int _progressiveInterval;
            private int _progressiveTimeBudgetMs;
            private bool _liveScroll;
            private bool _keepScrollBarOnRight;
            private int _scrollBarGap;
            private bool _resizeReflowPending;
            private bool _resizeReflowRunning;
            private ScrollExtentMarker _scrollExtentMarker;

            // Viewport virtualization. ItemValues still holds the lightweight data list;
            // only the currently realized range owns Control trees.
            private bool _virtualizing;
            private ItemsControlVirtualizationMode _virtualizationMode;
            private int _virtualizationThreshold;
            private int _overscanItems;
            private int _estimatedItemSize;
            private int _virtualizationCacheItems;
            private int _fixedItemSize;
            private int _configurationMutationGeneration;
            private long _virtualRetainedReuseCount;
            private long _virtualCacheReuseCount;
            private long _virtualCreatedCount;
            private long _progressiveBatchCount;

            internal ArrayList DirectVirtualCacheRecords;

            private const int SB_HORZ = 0;
            private const int SB_VERT = 1;
            private const int NativeScrollInfoSize = 28;
            private const int SIF_POS = 0x0004;
            private const int SIF_TRACKPOS = 0x0010;

            private sealed class ItemSourceListChangedForwarder
            {
                private volatile ItemsControl _owner;
                private readonly int _subscriptionEpoch;

                public ItemSourceListChangedForwarder(
                    ItemsControl owner,
                    int subscriptionEpoch)
                {
                    _subscriptionEpoch = subscriptionEpoch;
                    _owner = owner;
                }

                public void OnListChanged(
                    object sender,
                    ListChangedEventArgs e)
                {
                    ItemsControl owner = _owner;

                    if (owner != null)
                    {
                        owner.OnObservedItemListChanged(
                            sender,
                            _subscriptionEpoch,
                            e);
                    }
                }

                public void Disable()
                {
                    _owner = null;
                }
            }

            internal sealed class ObservedItemListChange
            {
                internal ListChangedType Type;
                internal int NewIndex;
                internal int OldIndex;
                internal object Item;
                internal string PropertyName;
            }

            private const int MaximumPendingItemSourceChanges = 64;

            /// <summary>
            /// A lazily-created 1x1 transparent native child placed at the logical
            /// end of scrolling content. ScrollableControl reliably derives AutoScroll
            /// ranges from child bounds on every resize, even when its cached
            /// display rectangle / AutoScrollMinSize state has just collapsed.
            /// It is intentionally not part of RenderedItems.
            /// </summary>
            private sealed class ScrollExtentMarker : Control
            {
                public ScrollExtentMarker()
                {
                    SetStyle(ControlStyles.Selectable, false);
                    SetStyle(ControlStyles.SupportsTransparentBackColor, true);
                    TabStop = false;
                    Enabled = false;
                    BackColor = Color.Transparent;
                    Size = new Size(1, 1);
                }

                protected override void OnPaint(PaintEventArgs e)
                {
                    // Intentionally invisible. Its Bounds exist only so native
                    // ScrollableControl has a stable end-of-content child.
                }
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct NativeScrollInfo
            {
                public int cbSize;
                public int fMask;
                public int nMin;
                public int nMax;
                public int nPage;
                public int nPos;
                public int nTrackPos;
            }

            [DllImport("user32.dll", SetLastError = true)]
            private static extern bool GetScrollInfo(
                IntPtr hwnd,
                int bar,
                ref NativeScrollInfo info);

            [DllImport("user32.dll", SetLastError = true)]
            private static extern int SetScrollInfo(
                IntPtr hwnd,
                int bar,
                ref NativeScrollInfo info,
                bool redraw);

            /// <summary>
            /// Creates an empty repeated-content host on the current UI thread.
            /// </summary>
            public ItemsControl()
            {
                _ownerThreadId =
                    System.Threading.Thread.CurrentThread.ManagedThreadId;

                _itemSourceSync = new object();
                _itemSource = null;
                _committedItemSource = null;
                _observedItemList = null;
                _observedItemListChangedForwarder = null;
                _observedItemListChanged = null;
                _detachedCommittedItemList = null;
                _itemSourceSubscriptionEpoch = 0;
                _pendingItemSourceReloadEpoch = 0;
                _itemSourceReloadPending = false;
                _itemSourceReloadPosted = false;
                _itemSourceFullReloadPending = false;
                _pendingItemSourceChangedIndices = new Hashtable();
                _pendingItemSourceChanges = new ArrayList();
                _itemSourceHandleReady = false;
                _itemsSourceInitializationComplete = false;
                _itemsSourceDisposed = false;

                ItemValues = new ArrayList();
                CommittedItemSource = null;
                CommittedItemValues = ItemValues;
                RenderedItems = new ArrayList();
                _renderedItemRecordsByControl =
                    new Hashtable(_runtimeObjectReferenceComparer);
                _renderedItemPublicationRevision = 0L;
                _renderedItemRecordIndexDiagnosticsEnabled = false;
                _renderedItemRecordIndexLookupCount = 0L;
                _renderedItemRecordIndexProbeCount = 0L;
                _itemControlReferenceScanProbeCount = 0L;
                TemplateFunctionExpressions = new ArrayList();

                _orientation = System.Windows.Forms.Orientation.Vertical;
                _spacing = 0;
                _wrap = false;
                _justifyContent = FlexJustifyContent.Start;
                _alignItems = FlexAlignItems.Stretch;
                _logicalScrollMappingInitialized = false;
                _logicalScrollMappingOrientation = _orientation;
                _logicalScrollMappingRightToLeft = false;
                _logicalScrollMappingMaximum = 0;
                _savedLogicalScrollOffset = 0;
                _runtimeLayoutInProgress = false;
                _itemsMeasureEpoch = 0L;
                _reentrantItemsLayoutRevision = 0L;
                _isRefreshing = false;
                _lastRefreshError = null;
                _itemsRollbackDepth = 0;
                _deferredItemsRequest = false;
                _deferredItemsHasSource = false;
                _deferredItemsSource = null;
                _deferredItemsForceRebuild = false;

                _itemKeyPath = null;
                _itemVersionPath = null;
                _reuseItems = true;
                _reevaluateFunctionsOnRefresh = true;
                _progressiveRendering = true;
                _progressiveBatchSize = 8;
                _progressiveInterval = 1;
                _progressiveTimeBudgetMs = 4;
                _liveScroll = true;
                _keepScrollBarOnRight = true;
                _scrollBarGap = 0;
                _resizeReflowPending = false;
                _resizeReflowRunning = false;

                _virtualizing = false;
                _virtualizationMode =
                    ItemsControlVirtualizationMode.Controls;
                _virtualizationThreshold = 32;
                _overscanItems = 3;
                _estimatedItemSize = 96;
                _virtualizationCacheItems = 16;
                _fixedItemSize = 0;
                _configurationMutationGeneration = 0;
                _virtualRetainedReuseCount = 0L;
                _virtualCacheReuseCount = 0L;
                _virtualCreatedCount = 0L;
                _progressiveBatchCount = 0L;

                // Created on the first native-controls virtual realization.
                // Non-virtualized and lightweight hosts do not need this list.
                DirectVirtualCacheRecords = null;

                ContentRightToLeft = false;
                RefreshGeneration = 0;

                // ScrollableControl already uses ScrollWindowEx to move child
                // windows efficiently. Do not turn on whole-control double buffering
                // here: it can make thumb tracking feel delayed on older machines.
                DoubleBuffered = false;

                // Repeated content is most useful as a viewport by default. Markup
                // can still opt out explicitly with AutoScroll="false".
                base.AutoScroll = true;

                // The native scroll-origin marker is created on the first
                // nonempty scrollable layout. Empty and AutoScroll-disabled
                // hosts otherwise need neither its managed Control nor HWND.
                _scrollExtentMarker = null;
            }

            internal IEnumerable ItemSource
            {
                get
                {
                    lock (_itemSourceSync)
                        return _itemSource;
                }
                set
                {
                    ReplaceItemSource(value);
                }
            }

            internal IEnumerable CommittedItemSource
            {
                get
                {
                    lock (_itemSourceSync)
                        return _committedItemSource;
                }
                set
                {
                    lock (_itemSourceSync)
                    {
                        _committedItemSource = value;
                        _detachedCommittedItemList = null;
                    }
                }
            }

            /// <summary>
            /// The enumerable rendered by this host. An IBindingList source is observed
            /// automatically and coalesced collection changes refresh the existing template.
            /// </summary>
            public IEnumerable ItemsSource
            {
                get { return ItemSource; }
                set
                {
                    bool initialized;
                    bool disposed;

                    lock (_itemSourceSync)
                    {
                        initialized = _itemsSourceInitializationComplete;
                        disposed = _itemsSourceDisposed;
                    }

                    if (disposed)
                    {
                        throw new ObjectDisposedException(
                            GetType().FullName);
                    }

                    if (!initialized)
                    {
                        if (System.Threading.Thread.CurrentThread.ManagedThreadId !=
                            _ownerThreadId)
                        {
                            throw new InvalidOperationException(
                                "ItemsSource cannot be assigned from another thread " +
                                "before the ItemsControl handle has been created.");
                        }

                        ReplaceItemSource(value);
                        return;
                    }

                    SetItems(value);
                }
            }

            internal void CompleteXamlInitialization(
                XmlElement declarationElement)
            {
                if (System.Threading.Thread.CurrentThread.ManagedThreadId !=
                    _ownerThreadId)
                {
                    throw new InvalidOperationException(
                        "ItemsControl XAML initialization must complete on its owner thread.");
                }

                IEnumerable source;

                lock (_itemSourceSync)
                {
                    if (_itemsSourceDisposed ||
                        _itemsSourceInitializationComplete)
                    {
                        return;
                    }

                    source = _itemSource;
                }

                if (Runtime != null &&
                    VirtualizationMode ==
                        ItemsControlVirtualizationMode.Lightweight)
                {
                    // PostConfigure runs after every attribute and the complete
                    // ItemTemplate, so this is the order-independent strict
                    // eligibility boundary. Do not publish initialization or
                    // a compiled plan when the final host is ineligible.
                    Runtime.ValidateLightweightItemsControlEligibility(
                        this,
                        declarationElement);
                    Runtime.ValidateLightweightItemsControlConfiguration(
                        this);
                }

                lock (_itemSourceSync)
                {
                    if (_itemsSourceDisposed)
                        return;

                    _itemsSourceInitializationComplete = true;
                }

                // The declarative property is assigned before ItemTemplate is parsed.
                // Re-run the central replacement now to activate collection observation.
                ReplaceItemSource(source);

                if (Runtime != null)
                {
                    if (source != null)
                        ReloadItems(false);
                }
            }

            private bool CanReloadAfterConfigurationChange()
            {
                if (Runtime == null)
                    return false;

                lock (_itemSourceSync)
                {
                    return
                        _itemsSourceInitializationComplete &&
                        !_itemsSourceDisposed &&
                        _itemSource != null;
                }
            }

            private bool CanRelayoutCommittedNonVirtualGeometry()
            {
                XamlRuntime runtime = Runtime;

                if (runtime == null ||
                    runtime.IsDisposed ||
                    IsDisposed ||
                    Disposing ||
                    _virtualizing ||
                    DirectVirtualActive ||
                    LightweightActive ||
                    PendingRefresh != null ||
                    _isRefreshing ||
                    _itemsRollbackDepth != 0 ||
                    _runtimeLayoutInProgress ||
                    !Object.ReferenceEquals(
                        ItemValues,
                        CommittedItemValues))
                {
                    return false;
                }

                lock (_itemSourceSync)
                {
                    return
                        _itemsSourceInitializationComplete &&
                        !_itemsSourceDisposed &&
                        Object.ReferenceEquals(
                            _itemSource,
                            _committedItemSource);
                }
            }

            private void BestEffortRelayoutAfterConfigurationFailure()
            {
                try
                {
                    if (Runtime != null &&
                        !Runtime.IsDisposed &&
                        !IsDisposed &&
                        !Disposing)
                    {
                        PerformLayout();
                    }
                }
                catch
                {
                    // Preserve the original configuration/layout failure.
                }
            }

            private void BestEffortCompleteThemedOrientationTransition()
            {
                try
                {
                    CompleteThemedScrollBarOrientationTransition();
                }
                catch
                {
                    // Preserve the original configuration/layout failure.
                }
            }

            private void BestEffortRestoreAutoScrollConfiguration(
                bool autoScroll,
                Size extent,
                Point scroll)
            {
                try
                {
                    base.AutoScroll = autoScroll;
                }
                catch
                {
                    // Continue restoring the independent native state below.
                }

                try
                {
                    AutoScrollMinSize = extent;
                }
                catch
                {
                    // Continue restoring the logical origin below.
                }

                try
                {
                    AutoScrollPosition = new Point(
                        GetRestoredScrollOffset(scroll.X),
                        GetRestoredScrollOffset(scroll.Y));
                }
                catch
                {
                    // Preserve the original configuration/layout failure.
                }

                try
                {
                    if (HasActiveThemedScrollBar)
                        ApplyThemedScrollBarConfigurationChange();
                    else
                        PerformLayout();
                }
                catch
                {
                    BestEffortRelayoutAfterConfigurationFailure();
                }
            }

            private static int GetRestoredScrollOffset(int physicalOrigin)
            {
                if (physicalOrigin >= 0)
                    return 0;

                return physicalOrigin == Int32.MinValue
                    ? Int32.MaxValue
                    : -physicalOrigin;
            }

            internal bool IsXamlInitializationComplete
            {
                get
                {
                    lock (_itemSourceSync)
                    {
                        return
                            _itemsSourceInitializationComplete &&
                            !_itemsSourceDisposed;
                    }
                }
            }

            private void ValidateActiveLightweightConfiguration()
            {
                ValidateWrapVirtualizationConfiguration();

                bool finalized;

                lock (_itemSourceSync)
                {
                    finalized =
                        _itemsSourceInitializationComplete &&
                        !_itemsSourceDisposed;
                }

                if (finalized &&
                    VirtualizationMode ==
                        ItemsControlVirtualizationMode.Lightweight &&
                    Runtime != null)
                {
                    Runtime.ValidateLightweightItemsControlEligibility(this);
                }
            }

            private void ValidateWrapVirtualizationConfiguration()
            {
                if (_wrap && _virtualizing)
                {
                    throw new InvalidOperationException(
                        "ItemsControl.Wrap cannot be combined with " +
                        "ItemsControl.Virtualizing. Wrapped virtualization " +
                        "requires a line-based viewport model and is not " +
                        "silently downgraded to one-dimensional scrolling.");
                }
            }

            private void RestoreViewportAfterConfigurationFailure()
            {
                if (!DirectVirtualActive && !LightweightActive)
                    return;

                try
                {
                    HandleDirectVirtualViewportChanged();
                }
                catch
                {
                    // Preserve the original rejected mutation. The normal
                    // refresh transaction already retained the committed UI;
                    // a later layout/scroll pass can retry reconciliation.
                }
            }

            private int BeginConfigurationMutation()
            {
                _configurationMutationGeneration =
                    _configurationMutationGeneration == Int32.MaxValue
                        ? 1
                        : _configurationMutationGeneration + 1;
                return _configurationMutationGeneration;
            }

            private bool OwnsConfigurationMutation(int generation)
            {
                return generation == _configurationMutationGeneration;
            }

            internal void ReplaceItemSource(IEnumerable source)
            {
                bool observe;

                lock (_itemSourceSync)
                {
                    observe =
                        _itemsSourceInitializationComplete &&
                        !_itemsSourceDisposed;
                }

                IBindingList desiredList = null;
                IItemsBindingScrollSource desiredScrollSource = null;

                if (observe)
                {
                    desiredList = source as IBindingList;
                    desiredScrollSource =
                        source as IItemsBindingScrollSource;

                    if (desiredList != null &&
                        !desiredList.SupportsChangeNotification)
                    {
                        desiredList = null;
                    }
                }

                IBindingList previousList;
                ItemSourceListChangedForwarder previousForwarder;
                ListChangedEventHandler previousHandler;
                ItemSourceListChangedForwarder desiredForwarder = null;
                ListChangedEventHandler desiredHandler = null;
                int epoch;
                bool reconcileCommittedList;

                lock (_itemSourceSync)
                {
                    if (_itemsSourceDisposed)
                    {
                        _itemSource = source;
                        return;
                    }

                    if (Object.ReferenceEquals(_itemSource, source) &&
                        Object.ReferenceEquals(
                            _observedItemList,
                            desiredList) &&
                        Object.ReferenceEquals(
                            _observedItemScrollSource,
                            desiredScrollSource))
                    {
                        return;
                    }

                    previousList = _observedItemList;
                    previousForwarder =
                        _observedItemListChangedForwarder;
                    previousHandler = _observedItemListChanged;

                    if (previousList != null &&
                        !Object.ReferenceEquals(_itemSource, source) &&
                        Object.ReferenceEquals(
                            _itemSource,
                            _committedItemSource))
                    {
                        // If this request later rolls back, changes raised by the old
                        // committed list while detached may have been missed.
                        _detachedCommittedItemList = previousList;
                    }

                    reconcileCommittedList =
                        desiredList != null &&
                        Object.ReferenceEquals(
                            desiredList,
                            _detachedCommittedItemList) &&
                        Object.ReferenceEquals(
                            source,
                            _committedItemSource);

                    _itemSource = source;
                    epoch = ++_itemSourceSubscriptionEpoch;
                    _observedItemList = desiredList;
                    _observedItemListChangedForwarder = null;
                    _observedItemListChanged = null;
                    _itemSourceReloadPending = false;
                    _itemSourceFullReloadPending = false;
                    _pendingItemSourceChangedIndices.Clear();
                    _pendingItemSourceChanges.Clear();
                    _pendingItemSourceReloadEpoch = epoch;

                    if (desiredList != null)
                    {
                        desiredForwarder =
                            new ItemSourceListChangedForwarder(
                                this,
                                epoch);
                        desiredHandler =
                            new ListChangedEventHandler(
                                desiredForwarder.OnListChanged);

                        _observedItemListChangedForwarder =
                            desiredForwarder;
                        _observedItemListChanged = desiredHandler;
                    }

                    if (reconcileCommittedList)
                    {
                        _detachedCommittedItemList = null;
                        _itemSourceReloadPending = true;
                        _itemSourceFullReloadPending = true;
                        _pendingItemSourceReloadEpoch = epoch;
                    }
                }

                if (previousForwarder != null)
                    previousForwarder.Disable();

                if (previousList != null && previousHandler != null)
                {
                    try
                    {
                        previousList.ListChanged -= previousHandler;
                    }
                    catch
                    {
                        AbandonItemSourceObservation(
                            epoch,
                            desiredList,
                            desiredForwarder,
                            desiredHandler,
                            reconcileCommittedList);
                        throw;
                    }
                }

                if (desiredList != null && desiredHandler != null)
                {
                    try
                    {
                        desiredList.ListChanged += desiredHandler;
                    }
                    catch
                    {
                        AbandonItemSourceObservation(
                            epoch,
                            desiredList,
                            desiredForwarder,
                            desiredHandler,
                            reconcileCommittedList);

                        try
                        {
                            desiredList.ListChanged -= desiredHandler;
                        }
                        catch
                        {
                        }

                        throw;
                    }
                }

                ReplaceItemScrollObservation(
                    epoch,
                    desiredScrollSource);

                if (reconcileCommittedList)
                    QueuePendingItemSourceReload();
            }

            private void AbandonItemSourceObservation(
                int epoch,
                IBindingList desiredList,
                ItemSourceListChangedForwarder desiredForwarder,
                ListChangedEventHandler desiredHandler,
                bool restoreDetachedCommittedList)
            {
                if (desiredForwarder != null)
                    desiredForwarder.Disable();

                lock (_itemSourceSync)
                {
                    if (_itemSourceSubscriptionEpoch != epoch ||
                        !Object.ReferenceEquals(
                            _observedItemList,
                            desiredList) ||
                        !Object.ReferenceEquals(
                            _observedItemListChangedForwarder,
                            desiredForwarder) ||
                        !Object.ReferenceEquals(
                            _observedItemListChanged,
                            desiredHandler))
                    {
                        return;
                    }

                    int abandonedEpoch =
                        ++_itemSourceSubscriptionEpoch;
                    _observedItemList = null;
                    _observedItemListChangedForwarder = null;
                    _observedItemListChanged = null;
                    _itemSourceReloadPending = false;
                    _itemSourceFullReloadPending = false;
                    _pendingItemSourceChangedIndices.Clear();
                    _pendingItemSourceChanges.Clear();
                    _pendingItemSourceReloadEpoch = abandonedEpoch;

                    if (restoreDetachedCommittedList &&
                        Object.ReferenceEquals(
                            _itemSource,
                            _committedItemSource))
                    {
                        _detachedCommittedItemList = desiredList;
                    }
                }
            }

            private void OnObservedItemListChanged(
                object sender,
                int subscriptionEpoch,
                ListChangedEventArgs e)
            {
                ObservedItemListChange capturedChange =
                    TryCaptureObservedItemListChange(
                        sender as IBindingList,
                        e);

                lock (_itemSourceSync)
                {
                    if (_itemsSourceDisposed ||
                        !_itemsSourceInitializationComplete ||
                        subscriptionEpoch != _itemSourceSubscriptionEpoch ||
                        !Object.ReferenceEquals(sender, _observedItemList))
                    {
                        return;
                    }

                    _itemSourceReloadPending = true;
                    _pendingItemSourceReloadEpoch = subscriptionEpoch;

                    if (!_itemSourceFullReloadPending &&
                        capturedChange != null)
                    {
                        bool duplicatePropertyChange =
                            IsPendingObservedItemPropertyChangeDuplicate(
                                capturedChange);

                        if (!duplicatePropertyChange &&
                            _pendingItemSourceChanges.Count >=
                                MaximumPendingItemSourceChanges)
                        {
                            _itemSourceFullReloadPending = true;
                            _pendingItemSourceChangedIndices.Clear();
                            _pendingItemSourceChanges.Clear();
                        }
                        else if (!duplicatePropertyChange)
                        {
                            _pendingItemSourceChanges.Add(capturedChange);
                        }

                        if (!_itemSourceFullReloadPending &&
                            capturedChange.Type ==
                                ListChangedType.ItemChanged)
                        {
                            _pendingItemSourceChangedIndices[
                                capturedChange.NewIndex] = true;
                        }
                    }
                    else
                    {
                        // Reset/property-descriptor notifications and oversized or
                        // malformed batches do not carry enough information to build
                        // an exact next snapshot. Preserve the ordinary full reload.
                        _itemSourceFullReloadPending = true;
                        _pendingItemSourceChangedIndices.Clear();
                        _pendingItemSourceChanges.Clear();
                    }
                }

                QueuePendingItemSourceReload();
            }

            private bool
                IsPendingObservedItemPropertyChangeDuplicate(
                    ObservedItemListChange change)
            {
                if (change == null ||
                    change.Type != ListChangedType.ItemChanged)
                {
                    return false;
                }

                int i;

                for (i = _pendingItemSourceChanges.Count - 1;
                     i >= 0;
                     i--)
                {
                    ObservedItemListChange pending =
                        _pendingItemSourceChanges[i] as
                            ObservedItemListChange;

                    if (pending == null ||
                        pending.Type != ListChangedType.ItemChanged)
                    {
                        // A structural edit changes the meaning of later logical
                        // indices, so coalescing never crosses that boundary.
                        break;
                    }

                    if (pending.NewIndex == change.NewIndex &&
                        Object.ReferenceEquals(
                            pending.Item,
                            change.Item) &&
                        String.Equals(
                            pending.PropertyName,
                            change.PropertyName,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            }

            private static ObservedItemListChange
                TryCaptureObservedItemListChange(
                    IBindingList source,
                    ListChangedEventArgs e)
            {
                if (source == null || e == null)
                    return null;

                ListChangedType type = e.ListChangedType;

                if (type != ListChangedType.ItemAdded &&
                    type != ListChangedType.ItemDeleted &&
                    type != ListChangedType.ItemMoved &&
                    type != ListChangedType.ItemChanged)
                {
                    return null;
                }

                if (e.NewIndex < 0 ||
                    (type == ListChangedType.ItemMoved &&
                     e.OldIndex < 0))
                {
                    return null;
                }

                object item = null;

                if (type != ListChangedType.ItemDeleted)
                {
                    try
                    {
                        if (e.NewIndex >= source.Count)
                            return null;

                        // ListChanged is synchronous. Capture the affected value
                        // while this individual mutation is still the source's
                        // current state; a later coalesced move/delete may make the
                        // final source index refer to a different occurrence.
                        item = source[e.NewIndex];
                    }
                    catch
                    {
                        return null;
                    }
                }

                ObservedItemListChange change =
                    new ObservedItemListChange();
                change.Type = type;
                change.NewIndex = e.NewIndex;
                change.OldIndex = e.OldIndex;
                change.Item = item;
                change.PropertyName =
                    type == ListChangedType.ItemChanged &&
                    e.PropertyDescriptor != null
                        ? e.PropertyDescriptor.Name
                        : null;
                return change;
            }

            private void QueuePendingItemSourceReload()
            {
                lock (_itemSourceSync)
                {
                    if (_itemsSourceDisposed ||
                        !_itemsSourceInitializationComplete ||
                        !_itemSourceReloadPending ||
                        _pendingItemSourceReloadEpoch !=
                            _itemSourceSubscriptionEpoch ||
                        _itemSourceReloadPosted ||
                        !_itemSourceHandleReady)
                    {
                        return;
                    }

                    _itemSourceReloadPosted = true;
                }

                try
                {
                    BeginInvoke(
                        (MethodInvoker)delegate
                        {
                            DrainPendingItemSourceReload();
                        });
#if !WINFORMSXAML_PACKAGE
                    RecordItemSourceReloadPostForTest();
#endif
                }
                catch (InvalidOperationException)
                {
                    lock (_itemSourceSync)
                        _itemSourceReloadPosted = false;
                }
            }

            private void DrainPendingItemSourceReload()
            {
                IBindingList observedList;
                ArrayList changedIndices = null;
                ArrayList changes = null;
                bool fullReload;

                lock (_itemSourceSync)
                {
                    _itemSourceReloadPosted = false;

                    if (_itemsSourceDisposed ||
                        !_itemsSourceInitializationComplete ||
                        !_itemSourceReloadPending ||
                        _pendingItemSourceReloadEpoch !=
                            _itemSourceSubscriptionEpoch)
                    {
                        return;
                    }

                    _itemSourceReloadPending = false;
                    observedList = _observedItemList;
                    fullReload =
                        _itemSourceFullReloadPending ||
                        _pendingItemSourceChanges.Count == 0;

                    if (!fullReload)
                    {
                        changedIndices = new ArrayList(
                            _pendingItemSourceChangedIndices.Count);

                        foreach (object index in
                            _pendingItemSourceChangedIndices.Keys)
                        {
                            changedIndices.Add(index);
                        }

                        changes = new ArrayList(
                            _pendingItemSourceChanges);
                    }

                    _itemSourceFullReloadPending = false;
                    _pendingItemSourceChangedIndices.Clear();
                    _pendingItemSourceChanges.Clear();
                }

                if (Runtime == null || IsDisposed || Disposing)
                    return;

                if (!fullReload &&
                    Runtime.TryApplyObservedItemListChanges(
                        this,
                        observedList,
                        changes,
                        changedIndices))
                {
                    return;
                }

                // A whole-source refresh covers every queued realized-slot update.
                // Detach that redundant batch before enumeration; the already-posted
                // dispatcher will observe that it no longer owns the host entry.
                Runtime.DiscardPendingReactiveItemUpdate(this);
                ReloadItems(false);
            }

            private void DisposeItemSourceObservation()
            {
                IBindingList observedList;
                ItemSourceListChangedForwarder observedForwarder;
                ListChangedEventHandler observedHandler;
                IItemsBindingScrollSource observedScrollSource;

                lock (_itemSourceSync)
                {
                    if (_itemsSourceDisposed)
                        return;

                    _itemsSourceDisposed = true;
                    _itemSourceSubscriptionEpoch++;
                    _itemSourceReloadPending = false;
                    _itemSourceReloadPosted = false;
                    _itemSourceFullReloadPending = false;
                    _pendingItemSourceChangedIndices.Clear();
                    _pendingItemSourceChanges.Clear();
                    _itemSourceHandleReady = false;
                    _detachedCommittedItemList = null;
                    observedList = _observedItemList;
                    observedForwarder =
                        _observedItemListChangedForwarder;
                    observedHandler = _observedItemListChanged;
                    _observedItemList = null;
                    _observedItemListChangedForwarder = null;
                    _observedItemListChanged = null;
                    observedScrollSource =
                        DetachItemScrollObservationForDisposal();
                }

                if (observedForwarder != null)
                    observedForwarder.Disable();

                if (observedScrollSource != null)
                    observedScrollSource.RemoveScrollObserver(this);

                if (observedList != null && observedHandler != null)
                    observedList.ListChanged -= observedHandler;
            }

            internal void ReleaseRuntimeItemSourceObservation()
            {
                DisposeItemSourceObservation();
            }

            /// <summary>
            /// Gets or sets the scrolling axis. Without wrapping, items are
            /// stacked on this axis. With wrapping, Vertical creates rows and
            /// Horizontal creates columns while retaining this scroll axis.
            /// </summary>
            public Orientation Orientation
            {
                get { return _orientation; }
                set
                {
                    if (_orientation == value)
                        return;

                    StopSmoothScrollAnimation();

                    Orientation previous = _orientation;
                    int previousLogicalScroll =
                        CaptureLogicalScrollOffsetForTransition();
                    int mutation = BeginConfigurationMutation();
                    bool layoutOnly = false;
                    _orientation = value;
                    BeginThemedScrollBarOrientationTransition();

                    try
                    {
                        ValidateActiveLightweightConfiguration();

                        if (Runtime != null)
                        {
                            SetFlowDirection(
                                this,
                                ContentRightToLeft);
                        }

                        layoutOnly =
                            CanRelayoutCommittedNonVirtualGeometry();

                        if (layoutOnly)
                            PerformLayout();
                        else if (CanReloadAfterConfigurationChange())
                            ReloadItemsForConfigurationChange();
                        else
                            PerformLayout();

                        CompleteThemedScrollBarOrientationTransition();
                        RestoreSavedLogicalScrollOffset(
                            previousLogicalScroll);
                    }
                    catch (ItemsRefreshCommittedException ex)
                    {
                        BestEffortCompleteThemedOrientationTransition();
                        throw ex.InnerException == null
                            ? ex
                            : ex.InnerException;
                    }
                    catch
                    {
                        if (OwnsConfigurationMutation(mutation))
                        {
                            _orientation = previous;
                            _savedLogicalScrollOffset =
                                previousLogicalScroll;
                            _logicalScrollMappingInitialized = false;

                            if (Runtime != null)
                            {
                                try
                                {
                                    SetFlowDirection(
                                        this,
                                        ContentRightToLeft);
                                }
                                catch
                                {
                                    // Preserve the rejected configuration error.
                                }
                            }

                            BestEffortCompleteThemedOrientationTransition();

                            if (layoutOnly)
                            {
                                BestEffortRelayoutAfterConfigurationFailure();
                            }

                            RestoreViewportAfterConfigurationFailure();
                            RestoreSavedLogicalScrollOffset(
                                previousLogicalScroll);
                        }
                        else
                        {
                            BestEffortCompleteThemedOrientationTransition();
                        }

                        throw;
                    }
                }
            }

            /// <summary>Gets or sets non-negative pixels between repeated items.</summary>
            public int Spacing
            {
                get { return _spacing; }
                set
                {
                    int normalized = Math.Max(0, value);

                    if (_spacing == normalized)
                        return;

                    int previous = _spacing;
                    int previousLogicalScroll =
                        CaptureLogicalScrollOffsetForTransition();
                    int mutation = BeginConfigurationMutation();
                    bool layoutOnly = false;
                    _spacing = normalized;

                    try
                    {
                        ValidateActiveLightweightConfiguration();

                        layoutOnly =
                            CanRelayoutCommittedNonVirtualGeometry();

                        if (layoutOnly)
                            PerformLayout();
                        else if (CanReloadAfterConfigurationChange())
                            ReloadItemsForConfigurationChange();
                        else
                            PerformLayout();

                        RestoreSavedLogicalScrollOffset(
                            previousLogicalScroll);
                    }
                    catch (ItemsRefreshCommittedException ex)
                    {
                        throw ex.InnerException == null
                            ? ex
                            : ex.InnerException;
                    }
                    catch
                    {
                        if (OwnsConfigurationMutation(mutation))
                        {
                            _spacing = previous;
                            _savedLogicalScrollOffset =
                                previousLogicalScroll;
                            _logicalScrollMappingInitialized = false;

                            if (layoutOnly)
                            {
                                BestEffortRelayoutAfterConfigurationFailure();
                            }

                            RestoreViewportAfterConfigurationFailure();
                            RestoreSavedLogicalScrollOffset(
                                previousLogicalScroll);
                        }

                        throw;
                    }
                }
            }

            /// <summary>
            /// Gets or sets whether non-virtual repeated items wrap into rows
            /// or columns. Wrapped virtualization is rejected explicitly.
            /// </summary>
            [DefaultValue(false)]
            public bool Wrap
            {
                get { return _wrap; }
                set
                {
                    if (_wrap == value)
                        return;

                    bool previous = _wrap;
                    _wrap = value;

                    try
                    {
                        ValidateWrapVirtualizationConfiguration();
                        StopSmoothScrollAnimation();
                        PerformLayout();
                    }
                    catch
                    {
                        _wrap = previous;

                        try
                        {
                            PerformLayout();
                        }
                        catch
                        {
                            // Preserve the rejected layout configuration error.
                        }

                        throw;
                    }
                }
            }

            /// <summary>
            /// Gets or sets free-space distribution within each wrapped line.
            /// </summary>
            [DefaultValue(FlexJustifyContent.Start)]
            public FlexJustifyContent JustifyContent
            {
                get { return _justifyContent; }
                set
                {
                    if (_justifyContent == value)
                        return;

                    FlexJustifyContent previous = _justifyContent;
                    _justifyContent = value;

                    try
                    {
                        StopSmoothScrollAnimation();
                        PerformLayout();
                    }
                    catch
                    {
                        _justifyContent = previous;
                        throw;
                    }
                }
            }

            /// <summary>
            /// Gets or sets repeated-item alignment across each wrapped line.
            /// </summary>
            [DefaultValue(FlexAlignItems.Stretch)]
            public FlexAlignItems AlignItems
            {
                get { return _alignItems; }
                set
                {
                    if (_alignItems == value)
                        return;

                    FlexAlignItems previous = _alignItems;
                    _alignItems = value;

                    try
                    {
                        StopSmoothScrollAnimation();
                        PerformLayout();
                    }
                    catch
                    {
                        _alignItems = previous;
                        throw;
                    }
                }
            }

            /// <summary>
            /// Property/field path used as the stable identity of each repeated
            /// item, for example ItemKeyPath="Id" or ItemKeyPath="User.Id".
            /// With a key, unchanged controls are reused even if items move.
            /// If omitted, Id/ID/_id/Key is auto-detected and index is the fallback.
            /// </summary>
            public string ItemKeyPath
            {
                get { return _itemKeyPath; }
                set { _itemKeyPath = value; }
            }

            /// <summary>
            /// Optional cheap application-level change token/version path. When the
            /// token is unchanged, ordinary data-binding evaluation is skipped entirely;
            /// Function bindings may still be re-evaluated when
            /// ReevaluateFunctionsOnRefresh=true. Without a version token the renderer
            /// compares the actual dynamic values used by the realized template.
            /// </summary>
            public string ItemVersionPath
            {
                get { return _itemVersionPath; }
                set { _itemVersionPath = value; }
            }

            /// <summary>Reuse unchanged keyed controls instead of rebuilding them.</summary>
            public bool ReuseItems
            {
                get { return _reuseItems; }
                set { _reuseItems = value; }
            }

            /// <summary>
            /// When true (default), every ReloadItems() re-invokes Function bindings
            /// found in this ItemsControl's ItemTemplate and includes their returned
            /// values in the keyed diff. This is required when a Function depends on
            /// external state such as an image cache, network state, or code-behind
            /// field that is not part of the item/version itself.
            /// </summary>
            public bool ReevaluateFunctionsOnRefresh
            {
                get { return _reevaluateFunctionsOnRefresh; }
                set { _reevaluateFunctionsOnRefresh = value; }
            }

            /// <summary>
            /// Build changed/new controls in small timer batches so a large refresh
            /// yields back to the WinForms message loop between batches. The old UI
            /// remains visible until the new/changed controls are ready.
            /// </summary>
            public bool ProgressiveRendering
            {
                get { return _progressiveRendering; }
                set { _progressiveRendering = value; }
            }

            /// <summary>
            /// Gets or sets the maximum item count attempted by one progressive tick.
            /// </summary>
            public int ProgressiveBatchSize
            {
                get { return _progressiveBatchSize; }
                set { _progressiveBatchSize = Math.Max(1, value); }
            }

            /// <summary>
            /// Gets or sets the minimum timer interval, in milliseconds, between
            /// progressive build ticks.
            /// </summary>
            public int ProgressiveInterval
            {
                get { return _progressiveInterval; }
                set { _progressiveInterval = Math.Max(1, value); }
            }

            /// <summary>
            /// Maximum UI-thread work budget per progressive tick. The renderer processes
            /// up to ProgressiveBatchSize items, but yields earlier when this time budget
            /// is reached so paint/input/scroll messages remain responsive.
            /// </summary>
            public int ProgressiveTimeBudgetMs
            {
                get { return _progressiveTimeBudgetMs; }
                set { _progressiveTimeBudgetMs = Math.Max(1, value); }
            }

            /// <summary>
            /// Forces content to follow the scroll thumb while it is being dragged,
            /// even when Windows' "show contents while dragging" preference is off.
            /// </summary>
            public bool LiveScroll
            {
                get { return _liveScroll; }
                set { _liveScroll = value; }
            }

            /// <summary>
            /// WinForms has a documented RightToLeft+AutoScroll limitation. Keeping
            /// the scrollable host LTR leaves the native vertical scrollbar on the
            /// right while repeated children still inherit RTL text/layout. This is
            /// enabled by default because it prevents the scrollbar from covering
            /// RTL content on legacy WinForms.
            /// </summary>
            public bool KeepScrollBarOnRight
            {
                get { return _keepScrollBarOnRight; }
                set
                {
                    if (_keepScrollBarOnRight == value)
                        return;

                    int previousLogicalScroll =
                        CaptureLogicalScrollOffsetForTransition();
                    bool previous = _keepScrollBarOnRight;
                    RightToLeft previousNativeDirection = RightToLeft;
                    bool previousContentRightToLeft =
                        ContentRightToLeft;
                    Size previousExtent = AutoScrollMinSize;
                    Point previousScroll = AutoScrollPosition;
                    int mutation = BeginConfigurationMutation();
                    _keepScrollBarOnRight = value;

                    try
                    {
                        if (Runtime != null)
                        {
                            SetFlowDirection(
                                this,
                                ContentRightToLeft);
                        }

                        // RightToLeft raises OnRightToLeftChanged synchronously.
                        // That hook already performs the themed/layout response;
                        // call it explicitly only when the native value stayed the
                        // same (the common horizontal/native-LTR path).
                        if (previousNativeDirection == RightToLeft)
                            OnItemsControlFlowDirectionChanged();

                        RestoreSavedLogicalScrollOffset(
                            previousLogicalScroll);
                    }
                    catch
                    {
                        if (OwnsConfigurationMutation(mutation))
                        {
                            _keepScrollBarOnRight = previous;
                            ContentRightToLeft =
                                previousContentRightToLeft;
                            _savedLogicalScrollOffset =
                                previousLogicalScroll;
                            _logicalScrollMappingInitialized = false;

                            try
                            {
                                if (Runtime != null)
                                {
                                    SetFlowDirection(
                                        this,
                                        previousContentRightToLeft);
                                }
                                else
                                {
                                    RightToLeft = previousNativeDirection;
                                }
                            }
                            catch
                            {
                                // Preserve the original direction/layout error.
                            }

                            if (OwnsConfigurationMutation(mutation))
                            {
                                BestEffortRestoreAutoScrollConfiguration(
                                    AutoScroll,
                                    previousExtent,
                                    previousScroll);
                            }

                            if (OwnsConfigurationMutation(mutation))
                            {
                                try
                                {
                                    RestoreSavedLogicalScrollOffset(
                                        previousLogicalScroll);
                                }
                                catch
                                {
                                    // Preserve the original direction/layout error.
                                }
                            }
                        }

                        throw;
                    }
                }
            }

            /// <summary>
            /// Gets or sets native scrolling. Active Lightweight rendering
            /// requires this to remain enabled.
            /// </summary>
            public new bool AutoScroll
            {
                get { return base.AutoScroll; }
                set
                {
                    if (base.AutoScroll == value)
                        return;

                    if (!value)
                        StopSmoothScrollAnimation();

                    bool previous = base.AutoScroll;
                    Size previousExtent = AutoScrollMinSize;
                    Point previousScroll = AutoScrollPosition;
                    int previousLogicalScroll =
                        CaptureLogicalScrollOffsetForTransition();
                    int mutation = BeginConfigurationMutation();
                    bool layoutOnly = false;

                    try
                    {
                        base.AutoScroll = value;
                        ValidateActiveLightweightConfiguration();

                        layoutOnly =
                            CanRelayoutCommittedNonVirtualGeometry();

                        if (!layoutOnly &&
                            CanReloadAfterConfigurationChange())
                        {
                            ReloadItemsForConfigurationChange();
                        }

                        if (layoutOnly && !HasActiveThemedScrollBar)
                        {
                            PerformLayout();
                        }
                        else
                        {
                            ApplyThemedScrollBarConfigurationChange();
                        }

                        RestoreSavedLogicalScrollOffset(
                            previousLogicalScroll);
                    }
                    catch (ItemsRefreshCommittedException ex)
                    {
                        throw ex.InnerException == null
                            ? ex
                            : ex.InnerException;
                    }
                    catch
                    {
                        if (OwnsConfigurationMutation(mutation))
                        {
                            BestEffortRestoreAutoScrollConfiguration(
                                previous,
                                previousExtent,
                                previousScroll);
                            RestoreSavedLogicalScrollOffset(
                                previousLogicalScroll);
                            RestoreViewportAfterConfigurationFailure();
                        }

                        throw;
                    }
                }
            }

            /// <summary>
            /// Enables opt-in viewport virtualization. The default is false.
            /// Controls mode activates at
            /// VirtualizationThreshold and realizes the visible range plus
            /// OverscanItems. Explicit Lightweight mode requires this property
            /// and paints every list size without native row controls.
            /// </summary>
            public bool Virtualizing
            {
                get { return _virtualizing; }
                set
                {
                    if (_virtualizing == value)
                        return;

                    bool previous = _virtualizing;
                    int mutation = BeginConfigurationMutation();
                    _virtualizing = value;

                    try
                    {
                        ValidateWrapVirtualizationConfiguration();
                        ValidateActiveLightweightConfiguration();

                        if (CanReloadAfterConfigurationChange())
                            ReloadItemsForConfigurationChange();
                    }
                    catch (ItemsRefreshCommittedException ex)
                    {
                        throw ex.InnerException == null
                            ? ex
                            : ex.InnerException;
                    }
                    catch
                    {
                        if (OwnsConfigurationMutation(mutation))
                        {
                            _virtualizing = previous;
                            RestoreViewportAfterConfigurationFailure();
                        }

                        throw;
                    }
                }
            }

            /// <summary>
            /// Selects the row representation used when virtualization is
            /// enabled. Controls is the full-fidelity default. Lightweight is
            /// an explicit fixed-size owner-drawn mode with a restricted
            /// template vocabulary; invalid templates fail with a markup
            /// diagnostic and never fall back to Control trees.
            /// </summary>
            public ItemsControlVirtualizationMode VirtualizationMode
            {
                get { return _virtualizationMode; }
                set
                {
                    if (value != ItemsControlVirtualizationMode.Controls &&
                        value != ItemsControlVirtualizationMode.Lightweight)
                    {
                        throw new ArgumentOutOfRangeException(
                            "value",
                            "Unknown ItemsControl virtualization mode.");
                    }

                    if (_virtualizationMode == value)
                        return;

                    ItemsControlVirtualizationMode previous =
                        _virtualizationMode;
                    bool restorePreviousLightweight =
                        LightweightActive &&
                        previous ==
                            ItemsControlVirtualizationMode.Lightweight;
                    int mutation = BeginConfigurationMutation();
                    _virtualizationMode = value;

                    try
                    {
                        if (Runtime != null)
                        {
                            Runtime.ValidateLightweightItemsControlConfiguration(
                                this);
                        }

                        ValidateActiveLightweightConfiguration();

                        if (CanReloadAfterConfigurationChange())
                            ReloadItemsForConfigurationChange();
                    }
                    catch (ItemsRefreshCommittedException ex)
                    {
                        throw ex.InnerException == null
                            ? ex
                            : ex.InnerException;
                    }
                    catch
                    {
                        if (!OwnsConfigurationMutation(mutation))
                            throw;

                        _virtualizationMode = previous;

                        if (Runtime != null)
                        {
                            try
                            {
                                Runtime.
                                    ValidateLightweightItemsControlConfiguration(
                                        this);
                            }
                            catch
                            {
                                // Preserve the original rejected transition.
                            }

                            if (restorePreviousLightweight)
                            {
                                try
                                {
                                    Runtime.
                                        RestoreLightweightItemsControlAfterConfigurationFailure(
                                            this);
                                }
                                catch
                                {
                                    // Preserve the rejected target transition.
                                }
                            }
                        }

                        RestoreViewportAfterConfigurationFailure();
                        throw;
                    }
                }
            }

            /// <summary>
            /// Minimum item count before Controls-mode viewport virtualization
            /// activates. Explicit Lightweight mode does not use this threshold.
            /// </summary>
            public int VirtualizationThreshold
            {
                get { return _virtualizationThreshold; }
                set
                {
                    int normalized = Math.Max(1, value);

                    if (_virtualizationThreshold == normalized)
                        return;

                    int previous = _virtualizationThreshold;
                    int mutation = BeginConfigurationMutation();
                    _virtualizationThreshold = normalized;

                    try
                    {
                        ValidateActiveLightweightConfiguration();

                        if (CanReloadAfterConfigurationChange())
                            ReloadItemsForConfigurationChange();
                    }
                    catch (ItemsRefreshCommittedException ex)
                    {
                        throw ex.InnerException == null
                            ? ex
                            : ex.InnerException;
                    }
                    catch
                    {
                        if (OwnsConfigurationMutation(mutation))
                        {
                            _virtualizationThreshold = previous;
                            RestoreViewportAfterConfigurationFailure();
                        }

                        throw;
                    }
                }
            }

            /// <summary>
            /// Per-side overscan used to define one fixed two-sided budget.
            /// Initial viewports split it symmetrically; scrolling moves the
            /// same total budget ahead of travel. Controls retains that bias for
            /// duplicate callbacks at the settled offset, while Lightweight
            /// returns a stationary viewport to the symmetric split. Controls
            /// realizes Control trees; Lightweight keeps bounded row values.
            /// </summary>
            public int OverscanItems
            {
                get { return _overscanItems; }
                set
                {
                    int normalized = Math.Max(0, value);

                    if (_overscanItems == normalized)
                        return;

                    int previous = _overscanItems;
                    int mutation = BeginConfigurationMutation();
                    _overscanItems = normalized;

                    try
                    {
                        ValidateActiveLightweightConfiguration();

                        if (DirectVirtualActive || LightweightActive)
                            HandleDirectVirtualViewportChanged();
                    }
                    catch
                    {
                        if (OwnsConfigurationMutation(mutation))
                        {
                            _overscanItems = previous;
                            RestoreViewportAfterConfigurationFailure();
                        }

                        throw;
                    }
                }
            }

            /// <summary>
            /// Estimated main-axis item size used before an item has been measured.
            /// This is height for Vertical orientation and width for Horizontal.
            /// </summary>
            public int EstimatedItemSize
            {
                get { return _estimatedItemSize; }
                set
                {
                    int normalized = Math.Max(1, value);

                    if (_estimatedItemSize == normalized)
                        return;

                    int previous = _estimatedItemSize;
                    int mutation = BeginConfigurationMutation();
                    _estimatedItemSize = normalized;

                    try
                    {
                        ValidateActiveLightweightConfiguration();

                        if ((DirectVirtualActive || LightweightActive) &&
                            CanReloadAfterConfigurationChange())
                        {
                            ReloadItemsForConfigurationChange();
                        }
                    }
                    catch (ItemsRefreshCommittedException ex)
                    {
                        throw ex.InnerException == null
                            ? ex
                            : ex.InnerException;
                    }
                    catch
                    {
                        if (OwnsConfigurationMutation(mutation))
                        {
                            _estimatedItemSize = previous;
                            RestoreViewportAfterConfigurationFailure();
                        }

                        throw;
                    }
                }
            }

            /// <summary>
            /// Maximum number of detached item Control trees kept for Controls-mode
            /// reuse. This setting has no effect in Lightweight mode.
            /// </summary>
            public int VirtualizationCacheItems
            {
                get { return _virtualizationCacheItems; }
                set
                {
                    _virtualizationCacheItems = Math.Max(0, value);

                    if (Runtime != null)
                        Runtime.TrimDirectVirtualizationCache(this);
                }
            }

            /// <summary>
            /// Optional virtualization model size. For Vertical lists this is row
            /// height; for Horizontal lists it is row width. Active Controls-mode
            /// virtualization uses zero for variable-size measurement, while a
            /// positive value is required by Lightweight mode. Ordinary nonvirtual
            /// controls always retain their native desired sizes and ignore this hint.
            /// </summary>
            public int FixedItemSize
            {
                get { return _fixedItemSize; }
                set
                {
                    int normalized = Math.Max(0, value);

                    if (_fixedItemSize == normalized)
                        return;

                    int previous = _fixedItemSize;
                    int mutation = BeginConfigurationMutation();
                    _fixedItemSize = normalized;

                    try
                    {
                        ValidateActiveLightweightConfiguration();

                        if ((DirectVirtualActive || LightweightActive) &&
                            CanReloadAfterConfigurationChange())
                        {
                            ReloadItemsForConfigurationChange();
                        }
                    }
                    catch (ItemsRefreshCommittedException ex)
                    {
                        throw ex.InnerException == null
                            ? ex
                            : ex.InnerException;
                    }
                    catch
                    {
                        if (OwnsConfigurationMutation(mutation))
                        {
                            _fixedItemSize = previous;
                            RestoreViewportAfterConfigurationFailure();
                        }

                        throw;
                    }
                }
            }

            /// <summary>True while the viewport-virtualized renderer is active.</summary>
            public bool IsVirtualizing
            {
                get
                {
                    return DirectVirtualActive || LightweightActive;
                }
            }

            /// <summary>
            /// Number of logical rows represented in the current viewport range.
            /// Lightweight rows are painted and do not own Control trees.
            /// </summary>
            public int RealizedCount
            {
                get
                {
                    return LightweightActive
                        ? LightweightRealizedCount
                        : (RenderedItems == null ? 0 : RenderedItems.Count);
                }
            }

            /// <summary>Number of detached Control trees retained for fast virtual reuse.</summary>
            public int VirtualCacheCount
            {
                get
                {
                    return LightweightActive
                        ? 0
                        : (DirectVirtualCacheRecords == null
                            ? 0
                            : DirectVirtualCacheRecords.Count);
                }
            }

            /// <summary>
            /// Gets the first logical index in the current Controls realization
            /// or Lightweight painted range, or -1 when virtualization is inactive
            /// or its range is empty.
            /// </summary>
            public int VirtualRealizedStartIndex
            {
                get
                {
                    return DirectVirtualActive
                        ? DirectVirtualRealizedStart
                        : (LightweightActive
                            ? LightweightRealizedStart
                            : -1);
                }
            }

            /// <summary>
            /// Gets the last logical index in the current Controls realization
            /// or Lightweight painted range, or -1 when virtualization is inactive
            /// or its range is empty.
            /// </summary>
            public int VirtualRealizedEndIndex
            {
                get
                {
                    return DirectVirtualActive
                        ? DirectVirtualRealizedEnd
                        : (LightweightActive
                            ? LightweightRealizedEnd
                            : -1);
                }
            }

            /// <summary>
            /// Gets the lifetime number of direct-viewport rows retained from
            /// the previously realized range without rebuilding their Control
            /// trees.
            /// </summary>
            public long VirtualRetainedReuseCount
            {
                get { return _virtualRetainedReuseCount; }
            }

            /// <summary>
            /// Gets the lifetime number of detached virtual cache rows reused
            /// by this host.
            /// </summary>
            public long VirtualCacheReuseCount
            {
                get { return _virtualCacheReuseCount; }
            }

            /// <summary>
            /// Gets the lifetime number of new Control trees successfully
            /// published by the direct virtual renderer for this host.
            /// </summary>
            public long VirtualCreatedCount
            {
                get { return _virtualCreatedCount; }
            }

            /// <summary>
            /// Gets the lifetime number of normal-renderer timer batches that
            /// completed at least one item patch or build operation.
            /// Direct viewport realization is synchronous and does not change
            /// this value.
            /// </summary>
            public long ProgressiveBatchCount
            {
                get { return _progressiveBatchCount; }
            }

            /// <summary>Gets whether an item refresh transaction is in progress.</summary>
            public bool IsRefreshing
            {
                get { return _isRefreshing; }
            }

            /// <summary>Gets the most recent refresh failure, or null.</summary>
            public Exception LastRefreshError
            {
                get { return _lastRefreshError; }
            }

            /// <summary>Gets the logical data-item count.</summary>
            public int Count
            {
                get
                {
                    return ItemValues == null
                        ? 0
                        : ItemValues.Count;
                }
            }

            /// <summary>Occurs after an item refresh commits successfully.</summary>
            public event EventHandler RefreshCompleted;

            /// <summary>Occurs after an item refresh fails and rolls back.</summary>
            public event EventHandler RefreshFailed;

            internal void SetRefreshing(
                bool refreshing,
                Exception error)
            {
                _isRefreshing = refreshing;
                _lastRefreshError = error;
            }

            internal void RecordVirtualRealization(
                int retainedReuseCount,
                int cacheReuseCount,
                int createdCount)
            {
                _virtualRetainedReuseCount = AddDiagnosticCount(
                    _virtualRetainedReuseCount,
                    retainedReuseCount);
                _virtualCacheReuseCount = AddDiagnosticCount(
                    _virtualCacheReuseCount,
                    cacheReuseCount);
                _virtualCreatedCount = AddDiagnosticCount(
                    _virtualCreatedCount,
                    createdCount);
            }

            internal void RecordProgressiveBatch()
            {
                _progressiveBatchCount = AddDiagnosticCount(
                    _progressiveBatchCount,
                    1);
            }

            private static long AddDiagnosticCount(
                long current,
                int increment)
            {
                if (increment <= 0 || current == Int64.MaxValue)
                    return current;

                if (current > Int64.MaxValue - increment)
                    return Int64.MaxValue;

                return current + increment;
            }

            /// <summary>
            /// Publishes a complete rendered-record snapshot together with its
            /// reference-identity root lookup. Building the lookup first keeps
            /// allocation or duplicate-root failures on the staging side of the
            /// publication boundary.
            /// </summary>
            internal void PublishRenderedItemRecords(ArrayList records)
            {
                Hashtable nextIndex =
                    new Hashtable(_runtimeObjectReferenceComparer);
                int i;

                for (i = 0; records != null && i < records.Count; i++)
                {
                    RenderedItemRecord record =
                        records[i] as RenderedItemRecord;

                    if (record == null || record.Control == null)
                        continue;

                    // Hashtable.Add deliberately rejects one native root being
                    // published for two records. The identity comparer keeps
                    // equal-but-distinct custom Controls independent.
                    nextIndex.Add(record.Control, record);
                }

                RenderedItems = records;
                _renderedItemRecordsByControl = nextIndex;
                AdvanceRenderedItemPublicationRevision();
            }

            /// <summary>
            /// Starts a progressive publication. Each later append updates the
            /// visible list and lookup before callback-capable Controls.Add work.
            /// </summary>
            internal void BeginRenderedItemRecordPublication(
                ArrayList records)
            {
                if (records == null || records.Count != 0)
                {
                    throw new ArgumentException(
                        "A progressive rendered-record publication must start " +
                        "with an empty list.",
                        "records");
                }

                PublishRenderedItemRecords(records);
            }

            internal void AppendPublishedRenderedItemRecord(
                ArrayList records,
                object value)
            {
                RenderedItemRecord record =
                    value as RenderedItemRecord;

                if (!Object.ReferenceEquals(RenderedItems, records))
                {
                    throw new InvalidOperationException(
                        "The progressive rendered-record publication was " +
                        "superseded.");
                }

                if (record == null || record.Control == null)
                    return;

                if (_renderedItemRecordsByControl == null)
                {
                    throw new InvalidOperationException(
                        "The rendered-record lookup is not initialized.");
                }

                if (_renderedItemRecordsByControl.ContainsKey(record.Control))
                {
                    throw new InvalidOperationException(
                        "One item Control cannot belong to two rendered records.");
                }

                int appendedIndex = records.Add(record);

                try
                {
                    _renderedItemRecordsByControl.Add(
                        record.Control,
                        record);
                    AdvanceRenderedItemPublicationRevision();
                }
                catch
                {
                    records.RemoveAt(appendedIndex);
                    throw;
                }
            }

            internal object FindRenderedItemRecordByRoot(
                Control control)
            {
                if (control == null ||
                    _renderedItemRecordsByControl == null)
                {
                    return null;
                }

                if (_renderedItemRecordIndexDiagnosticsEnabled)
                {
                    _renderedItemRecordIndexLookupCount = AddDiagnosticCount(
                        _renderedItemRecordIndexLookupCount,
                        1);
                    _renderedItemRecordIndexProbeCount = AddDiagnosticCount(
                        _renderedItemRecordIndexProbeCount,
                        1);
                }

                return _renderedItemRecordsByControl[control]
                    as RenderedItemRecord;
            }

            internal void UnindexRenderedItemRecord(
                object value,
                Control control)
            {
                RenderedItemRecord record =
                    value as RenderedItemRecord;

                if (record == null || control == null ||
                    _renderedItemRecordsByControl == null)
                {
                    return;
                }

                RenderedItemRecord indexed =
                    _renderedItemRecordsByControl[control]
                        as RenderedItemRecord;

                // A normal keyed refresh creates a replacement record around a
                // retained Control. Disposing the superseded record must never
                // remove the replacement's index entry.
                if (Object.ReferenceEquals(indexed, record))
                    _renderedItemRecordsByControl.Remove(control);
            }

            internal void ClearRenderedItemRecords()
            {
                RenderedItems = null;
                _renderedItemRecordsByControl = null;
                AdvanceRenderedItemPublicationRevision();
            }

            internal long RenderedItemPublicationRevision
            {
                get { return _renderedItemPublicationRevision; }
            }

            private void AdvanceRenderedItemPublicationRevision()
            {
                _renderedItemPublicationRevision =
                    _renderedItemPublicationRevision == Int64.MaxValue
                        ? 1L
                        : _renderedItemPublicationRevision + 1L;
            }

            internal void RecordItemControlReferenceScanProbe()
            {
                _itemControlReferenceScanProbeCount = AddDiagnosticCount(
                    _itemControlReferenceScanProbeCount,
                    1);
            }

            internal void BeginItemsRollback()
            {
                _itemsRollbackDepth++;
            }

            internal bool EndItemsRollback(bool disposing)
            {
                if (_itemsRollbackDepth <= 0)
                    return false;

                _itemsRollbackDepth--;

                if (_itemsRollbackDepth != 0 || !_deferredItemsRequest)
                    return false;

                bool hasSource = _deferredItemsHasSource;
                IEnumerable source = _deferredItemsSource;
                bool forceRebuild = _deferredItemsForceRebuild;

                _deferredItemsRequest = false;
                _deferredItemsHasSource = false;
                _deferredItemsSource = null;
                _deferredItemsForceRebuild = false;

                if (disposing || IsDisposed || Runtime == null)
                    return false;

                if (hasSource)
                    ReplaceItemSource(source);

                ReloadItems(forceRebuild);
                return true;
            }

            private bool DeferItemsRequest(
                IEnumerable source,
                bool hasSource,
                bool forceRebuild)
            {
                if (_itemsRollbackDepth <= 0)
                    return false;

                _deferredItemsRequest = true;

                if (hasSource)
                {
                    _deferredItemsHasSource = true;
                    _deferredItemsSource = source;
                }

                _deferredItemsForceRebuild = forceRebuild;
                return true;
            }

            internal void RaiseRefreshCompleted()
            {
                ApplyDeferredItemScrollRequest();

                EventHandler handler = RefreshCompleted;

                if (handler != null)
                    handler(this, EventArgs.Empty);
            }

            internal void RaiseRefreshFailed()
            {
                ClearDeferredItemScrollRequest();

                EventHandler handler = RefreshFailed;

                if (handler != null)
                    handler(this, EventArgs.Empty);
            }

            private bool RequiresOwnerThreadMarshal()
            {
                if (InvokeRequired)
                    return true;

                if (!IsHandleCreated &&
                    System.Threading.Thread.CurrentThread.ManagedThreadId !=
                    _ownerThreadId)
                {
                    throw new InvalidOperationException(
                        "ItemsControl cannot marshal a cross-thread operation " +
                        "until the control handle has been created.");
                }

                return false;
            }

            /// <summary>
            /// Replaces the current enumerable source and refreshes repeated content.
            /// Calls made after handle creation are marshalled to the owner thread.
            /// </summary>
            public void SetItems(IEnumerable items)
            {
                if (RequiresOwnerThreadMarshal())
                {
                    IEnumerable source = items;

                    BeginInvoke(
                        (MethodInvoker)delegate
                        {
                            SetItems(source);
                        });

                    return;
                }

                if (Runtime == null)
                {
                    throw new InvalidOperationException(
                        "ItemsControl is not attached to a WinFormsXaml runtime.");
                }

                if (DeferItemsRequest(items, true, false))
                    return;

                if (!Runtime.CancelItemsRefresh(this, false))
                    return;

                ReplaceItemSource(items);
                ReloadItems();
            }

            /// <summary>
            /// Re-evaluates the current source while retaining compatible keyed controls.
            /// </summary>
            public void ReloadItems()
            {
                ReloadItems(false);
            }

            /// <summary>
            /// Re-enumerates the existing source. forceRebuild=true bypasses keyed
            /// control reuse, useful when a Function binding depends on external state
            /// that is not represented in the item object/version.
            /// </summary>
            public void ReloadItems(bool forceRebuild)
            {
                ReloadItemsCore(forceRebuild, false);
            }

            private void ReloadItemsForConfigurationChange()
            {
                ReloadItemsCore(false, true);
            }

            private void ReloadItemsCore(
                bool forceRebuild,
                bool preserveRefreshOutcome)
            {
                if (RequiresOwnerThreadMarshal())
                {
                    bool force = forceRebuild;

                    BeginInvoke(
                        (MethodInvoker)delegate
                        {
                            ReloadItems(force);
                        });

                    return;
                }

                if (Runtime == null)
                {
                    throw new InvalidOperationException(
                        "ItemsControl is not attached to a WinFormsXaml runtime.");
                }

                if (DeferItemsRequest(null, false, forceRebuild))
                    return;

                // Cancellation restores the committed UI and source. Preserve the
                // latest requested source so ReloadItems during progressive work
                // still re-enumerates that request rather than the previous commit.
                IEnumerable requestedSource = ItemSource;

                if (!Runtime.CancelItemsRefresh(this, false))
                    return;

                ReplaceItemSource(requestedSource);

                ArrayList snapshot = new ArrayList();
                int enumerationGeneration = ++RefreshGeneration;
                IEnumerator enumerator = null;
                Exception enumerationError = null;

                try
                {
                    if (requestedSource != null)
                    {
                        enumerator = requestedSource.GetEnumerator();

                        while (XamlRuntime.OwnsItemsTransition(
                            this,
                            enumerationGeneration))
                        {
                            bool hasItem = enumerator.MoveNext();

                            if (!XamlRuntime.OwnsItemsTransition(
                                this,
                                enumerationGeneration))
                            {
                                break;
                            }

                            if (!hasItem)
                                break;

                            object item = enumerator.Current;

                            if (!XamlRuntime.OwnsItemsTransition(
                                this,
                                enumerationGeneration))
                            {
                                break;
                            }

                            snapshot.Add(item);
                        }
                    }
                }
                catch (Exception ex)
                {
                    enumerationError = ex;
                }

                IDisposable disposableEnumerator =
                    enumerator as IDisposable;

                if (disposableEnumerator != null)
                {
                    try
                    {
                        disposableEnumerator.Dispose();
                    }
                    catch (Exception ex)
                    {
                        if (enumerationError == null)
                            enumerationError = ex;
                    }
                }

                if (!XamlRuntime.OwnsItemsTransition(
                    this,
                    enumerationGeneration))
                {
                    // Reentrant SetItems/ReloadItems owns the host now. Do not let
                    // this older enumeration publish or roll back over it.
                    if (enumerationError != null)
                        throw enumerationError;

                    return;
                }

                if (enumerationError != null)
                {
                    // The current committed controls remain valid when a source cannot
                    // be enumerated, but callers still need the same observable failure
                    // contract as a template/build error.
                    ReplaceItemSource(CommittedItemSource);
                    ItemValues = CommittedItemValues;
                    _lastRefreshError = enumerationError;
                    RaiseRefreshFailed();
                    throw enumerationError;
                }

                ItemValues = snapshot;
                _lastRefreshError = null;

                try
                {
                    Runtime.BeginItemsRefresh(this, forceRebuild);
                }
                catch (Exception ex)
                {
                    ItemsRefreshCommittedException committedError =
                        ex as ItemsRefreshCommittedException;

                    if (committedError != null)
                    {
                        if (preserveRefreshOutcome)
                            throw;

                        throw committedError.InnerException == null
                            ? committedError
                            : committedError.InnerException;
                    }

                    ItemsRefreshFailedException failedError =
                        ex as ItemsRefreshFailedException;

                    if (failedError != null)
                    {
                        if (preserveRefreshOutcome)
                            throw;

                        throw failedError.InnerException == null
                            ? failedError
                            : failedError.InnerException;
                    }

                    ItemsRefreshSupersededException supersededError =
                        ex as ItemsRefreshSupersededException;

                    if (supersededError != null)
                    {
                        if (preserveRefreshOutcome)
                            throw;

                        throw supersededError.InnerException == null
                            ? supersededError
                            : supersededError.InnerException;
                    }

                    ReplaceItemSource(CommittedItemSource);
                    ItemValues = CommittedItemValues;

                    // Synchronous build failures report themselves through
                    // FailItemsRefresh. Planning failures happen before a refresh state
                    // exists, so report them here without raising the event twice.
                    if (_lastRefreshError == null)
                    {
                        SetRefreshing(false, ex);
                        RaiseRefreshFailed();
                    }

                    throw;
                }
            }

            /// <summary>Rebuilds every repeated control from the current source.</summary>
            public void ForceReloadItems()
            {
                ReloadItems(true);
            }

            /// <summary>Clears the current source and all repeated controls.</summary>
            public void ClearItems()
            {
                if (RequiresOwnerThreadMarshal())
                {
                    BeginInvoke(
                        (MethodInvoker)delegate
                        {
                            ClearItems();
                        });

                    return;
                }

                if (DeferItemsRequest(null, true, false))
                    return;

                if (Runtime != null &&
                    !Runtime.CancelItemsRefresh(this, false))
                {
                    return;
                }

                ReplaceItemSource(null);
                ItemValues = new ArrayList();

                if (Runtime != null)
                    Runtime.BeginItemsRefresh(this, false);
            }

            /// <summary>Scrolls the repeated content to its leading edge.</summary>
            public void ScrollToStart()
            {
                if (RequiresOwnerThreadMarshal())
                {
                    BeginInvoke(
                        (MethodInvoker)delegate
                        {
                            ScrollToStart();
                        });

                    return;
                }

                ThrowIfItemScrollUnavailable();

                if (!AutoScroll)
                    return;

                SetLogicalScrollOffset(0);
            }

            /// <summary>
            /// Efficiently scrolls a virtualized list to the requested data index without
            /// materializing the items before it.
            /// </summary>
            public void ScrollToIndex(int index)
            {
                ScrollIntoView(
                    index,
                    ItemScrollAlignment.Start,
                    false);
            }

            internal void SetTemplate(
                XmlElement templateRoot,
                object eventTarget)
            {
                XmlElement nextTemplateRoot =
                    templateRoot == null
                        ? null
                        : (XmlElement)templateRoot.CloneNode(true);
                string nextTemplateOuterXml =
                    nextTemplateRoot == null
                        ? null
                        : nextTemplateRoot.OuterXml;

                ArrayList nextFunctionExpressions =
                    Runtime == null
                        ? new ArrayList()
                        : Runtime.CollectTemplateFunctionExpressions(
                            nextTemplateRoot);
                ItemTemplateDeclarationContext nextTemplateContext =
                    Runtime == null || nextTemplateRoot == null
                        ? null
                        : Runtime.CaptureItemTemplateDeclarationContext();

                XmlElement previousTemplateRoot = TemplateRoot;
                string previousTemplateOuterXml = TemplateOuterXml;
                object previousTemplateEventTarget = TemplateEventTarget;
                ArrayList previousFunctionExpressions =
                    TemplateFunctionExpressions;
                ItemTemplateDeclarationContext previousTemplateContext =
                    _templateContext;

                TemplateRoot = nextTemplateRoot;
                TemplateOuterXml = nextTemplateOuterXml;
                TemplateEventTarget = eventTarget;
                TemplateFunctionExpressions = nextFunctionExpressions;
                _templateContext = nextTemplateContext;

                if (Runtime != null)
                {
                    try
                    {
                        Runtime.OnItemsControlTemplateChanged(this);

                        if (previousTemplateRoot != null)
                        {
                            Runtime.ReleaseCompiledItemTemplate(
                                previousTemplateRoot);
                        }
                    }
                    catch
                    {
                        // A future template-change hook may compile the new
                        // snapshot before discovering an indexing error. Retire
                        // only that new cache entry before restoring the previous
                        // declaration context and preset index.
                        if (nextTemplateRoot != null)
                        {
                            try
                            {
                                Runtime.ReleaseCompiledItemTemplate(
                                    nextTemplateRoot);
                            }
                            catch
                            {
                                // Preserve the template replacement failure.
                            }
                        }

                        TemplateRoot = previousTemplateRoot;
                        TemplateOuterXml = previousTemplateOuterXml;
                        TemplateEventTarget = previousTemplateEventTarget;
                        TemplateFunctionExpressions =
                            previousFunctionExpressions;
                        _templateContext = previousTemplateContext;

                        // Restore the filtered preset index to the same
                        // template snapshot before reporting replacement
                        // failure. Index repair has no user callbacks.
                        Runtime.OnItemsControlTemplateChanged(this);
                        throw;
                    }
                }
            }

            internal void ReleaseTemplateDeclarationContext()
            {
                _templateContext = null;
            }

            /// <summary>Reopens item-source dispatch for a new handle.</summary>
            protected override void OnHandleCreated(EventArgs e)
            {
                base.OnHandleCreated(e);

                _secondaryNativeChromeHidden = false;
                EnsureVirtualScrollOriginObserverHandle();
                RegisterLegacyMouseWheelRouting();
                EnsureThemedScrollBar();
                PositionThemedScrollBar();
                SynchronizeThemedScrollBar();
                InvalidateThemedNativeChromeState();
                ReconcileThemedNativeChrome();

                lock (_itemSourceSync)
                    _itemSourceHandleReady = true;

                QueuePendingItemSourceReload();
            }

            /// <summary>Revokes native-handle dispatch while retaining logical work.</summary>
            protected override void OnHandleDestroyed(EventArgs e)
            {
                StopSmoothScrollAnimation();
                DisposeScrollBitmapCache();
                UnregisterLegacyMouseWheelRouting();
                HideThemedScrollBarOverlay();
                InvalidateThemedNativeChromeState();
                _secondaryNativeChromeHidden = false;

                // An item-source delegate posted to the previous native handle is not
                // a reliable owner for pending work. Keep its request bit and post
                // again if the control later creates another handle.
                lock (_itemSourceSync)
                {
                    _itemSourceHandleReady = false;
                    _itemSourceReloadPosted = false;
                }

                base.OnHandleDestroyed(e);
            }

            /// <summary>Runs native layout followed by repeated-content layout.</summary>
            protected override void OnLayout(LayoutEventArgs e)
            {
                if (ShouldSuppressThemedInfrastructureLayout(e))
                    return;

                if (_suppressOwnedInfrastructureLayout)
                    return;

                // Final virtual bounds are already the result of a complete
                // measurement pass. Publishing each child rectangle can ask
                // ScrollableControl to relayout the parent once per child;
                // suppress only that positioning phase. Candidate attachment
                // and measurement layouts retain their established semantics.
                if (DirectVirtualPositioningControls)
                {
                    return;
                }

                if (_runtimeLayoutInProgress)
                {
                    AdvanceItemsMeasureEpoch();
                    AdvanceReentrantItemsLayoutRevision();
                }
                else
                {
                    AdvanceItemsMeasureEpoch();
                }

                // Let ScrollableControl do its normal first pass. Its first pass may be
                // based on child bounds from the previous window size, so it is NOT the
                // final authority for our custom ItemsControl.
                _secondaryNativeChromeHidden = false;
                base.OnLayout(e);

                // A user/layout mutation invalidates the captured control
                // geometry. Publish the cached logical position once before
                // the runtime lays out the replacement tree.
                CommitScrollBitmapCache();
                HideSecondaryNativeScrollBar();

                if (_runtimeLayoutInProgress)
                    return;

                if (Runtime == null)
                {
                    ReconcileLogicalScrollOffsetAfterRangeChange();
                    SynchronizeThemedScrollBar();

                    if (!_applyingLogicalScrollCommand &&
                        !_applyingSmoothScrollFrame)
                    {
                        ReconcileThemedNativeChrome();
                    }

                    return;
                }

                RunRuntimeScrollLayout(e);
                HideSecondaryNativeScrollBar();
            }

            internal long ItemsMeasureEpoch
            {
                get { return _itemsMeasureEpoch; }
            }

            internal void AdvanceItemsMeasureEpoch()
            {
                if (_itemsMeasureEpoch == Int64.MaxValue)
                {
                    // An epoch collision is practically unreachable, but keep
                    // the cache contract exact instead of allowing an ancient
                    // record to become current after rollover.
                    InvalidateCurrentItemsMeasureCaches();
                    _itemsMeasureEpoch = 1L;
                    return;
                }

                _itemsMeasureEpoch++;
            }

            private void AdvanceReentrantItemsLayoutRevision()
            {
                _reentrantItemsLayoutRevision =
                    _reentrantItemsLayoutRevision == Int64.MaxValue
                        ? 1L
                        : _reentrantItemsLayoutRevision + 1L;
            }

            private void InvalidateCurrentItemsMeasureCaches()
            {
                ArrayList records = RenderedItems;

                if (records == null)
                    return;

                int i;

                for (i = 0; i < records.Count; i++)
                {
                    RenderedItemRecord record =
                        records[i] as RenderedItemRecord;

                    if (record != null)
                        record.MeasureCacheValid = false;
                }
            }

            /// <summary>
            /// A resize can make a native scrollbar disappear. On legacy WinForms the
            /// opposite transition (grow -> no bar -> shrink -> bar) is not always
            /// recalculated during the same resize/layout message. Queue one extra layout
            /// after the parent/window resize has fully settled. This is deliberately
            /// deferred with BeginInvoke so it observes the final ClientSize.
            /// </summary>
            protected override void OnSizeChanged(EventArgs e)
            {
                base.OnSizeChanged(e);

                CommitScrollBitmapCache();

                ReconcileLogicalScrollOffsetAfterRangeChange();
                PositionThemedScrollBar();
                SynchronizeThemedScrollBar();

                if (Runtime == null || !AutoScroll)
                    return;

                QueueDeferredResizeReflow();

                HandleDirectVirtualViewportChanged();
            }

            private void QueueDeferredResizeReflow()
            {
                if (_resizeReflowPending ||
                    _resizeReflowRunning ||
                    !IsHandleCreated ||
                    IsDisposed ||
                    Disposing)
                {
                    return;
                }

                _resizeReflowPending = true;

                try
                {
                    BeginInvoke(
                        (MethodInvoker)delegate
                        {
                            _resizeReflowPending = false;

                            if (IsDisposed ||
                                Disposing ||
                                Runtime == null ||
                                !AutoScroll)
                            {
                                return;
                            }

                            if (_runtimeLayoutInProgress)
                            {
                                QueueDeferredResizeReflow();
                                return;
                            }

                            _resizeReflowRunning = true;

                            try
                            {
                                // Re-run the immediate parent layout first. This matters when
                                // ItemsControl is a FlexGrow/Grid child: after a grow/shrink cycle
                                // the native child can otherwise retain the old tall Bounds and
                                // get clipped by the Form instead of becoming a smaller scroll host.
                                Control parent = Parent;

                                if (parent != null && !parent.IsDisposed)
                                    parent.PerformLayout();

                                // PerformLayout causes our OnLayout override to run again
                                // with the final size after the resize event chain.
                                PerformLayout();

                                // Force a final native visibility/range synchronization.
                                ReconcileRuntimeScrollbars();
                                Invalidate();
                            }
                            finally
                            {
                                _resizeReflowRunning = false;
                            }
                        });
                }
                catch (InvalidOperationException)
                {
                    _resizeReflowPending = false;
                }
            }

            private void RunRuntimeScrollLayout(LayoutEventArgs e)
            {
                if (Runtime == null || _runtimeLayoutInProgress)
                    return;

                _runtimeLayoutInProgress = true;

                try
                {
                    if (DirectVirtualActive || LightweightActive)
                    {
                        // Direct virtualization owns an explicit native extent
                        // and synchronously reconciles its logical viewport. It
                        // needs one native scrollbar/layout response, not the
                        // normal renderer's up-to-eight convergence loop.
                        XamlRuntime directRuntime = Runtime;

                        if (directRuntime == null ||
                            directRuntime.IsDisposed ||
                            IsDisposed ||
                            Disposing ||
                            !directRuntime.LayoutItemsControl(this) ||
                            !Object.ReferenceEquals(
                                directRuntime,
                                Runtime))
                        {
                            return;
                        }

                        ReconcileRuntimeScrollbars();

                        if (Runtime == null ||
                            Runtime.IsDisposed ||
                            IsDisposed ||
                            Disposing)
                        {
                            return;
                        }

                        base.OnLayout(e);

                        if (Runtime == null ||
                            Runtime.IsDisposed ||
                            IsDisposed ||
                            Disposing)
                        {
                            return;
                        }

                        if (DirectVirtualActive || LightweightActive)
                        {
                            directRuntime = Runtime;

                            if (directRuntime == null ||
                                directRuntime.IsDisposed ||
                                !directRuntime.LayoutItemsControl(this) ||
                                !Object.ReferenceEquals(
                                    directRuntime,
                                    Runtime) ||
                                IsDisposed ||
                                Disposing)
                            {
                                return;
                            }

                            ReconcileRuntimeScrollbars();

                            if (DirectVirtualActive || LightweightActive)
                                return;
                        }

                        // A callback in the direct/base passes can commit the
                        // normal renderer. Let that renderer complete this same
                        // layout event instead of returning with unlaid records.
                    }

                    int pass;
                    bool finalRuntimeLayoutRequired = true;

                    for (pass = 0; pass < 8; pass++)
                    {
                        XamlRuntime runtime = Runtime;

                        if (runtime == null ||
                            runtime.IsDisposed ||
                            IsDisposed ||
                            Disposing)
                        {
                            return;
                        }

                        long runtimeReentrantLayoutRevision =
                            _reentrantItemsLayoutRevision;

                        if (!runtime.LayoutItemsControl(this) ||
                            runtimeReentrantLayoutRevision !=
                                _reentrantItemsLayoutRevision)
                        {
                            // A callback replaced/disposed the measured tree.
                            // Do not reconcile or raise base Layout against the
                            // stale pass; retry the newly committed snapshot.
                            continue;
                        }

                        ArrayList runtimeRecords = RenderedItems;
                        int runtimeRecordCount = runtimeRecords == null
                            ? 0
                            : runtimeRecords.Count;
                        int runtimeRefreshGeneration = RefreshGeneration;
                        long runtimePublicationRevision =
                            RenderedItemPublicationRevision;

                        // Capture the complete set of inputs consumed by the
                        // ordinary runtime layout. Reconcile/base.OnLayout can
                        // clamp the native display origin or synchronously run
                        // application Layout handlers that mutate framework
                        // geometry. Only a byte-for-byte stable response makes
                        // the historical final runtime pass redundant.
                        Size runtimeClient = ClientSize;
                        bool runtimeHorizontal = HorizontalScroll.Visible;
                        bool runtimeVertical = VerticalScroll.Visible;
                        Point runtimeScroll = AutoScrollPosition;
                        Rectangle runtimeViewport =
                            GetItemsViewportRectangle();
                        Rectangle runtimeDisplay = DisplayRectangle;
                        Orientation runtimeOrientation = _orientation;
                        int runtimeSpacing = _spacing;
                        Padding runtimePadding = Padding;
                        bool runtimeAutoScroll = AutoScroll;
                        bool runtimeContentRightToLeft =
                            ContentRightToLeft;
                        int runtimeConfigurationMutation =
                            _configurationMutationGeneration;
                        long runtimeCollapseRevision =
                            runtime.CaptureElementCollapseRevision();

                        // Repeated-row Bounds are owned by ItemsControl. Direct
                        // application mutation from a Layout handler is not a
                        // supported layout input, so do not reintroduce an O(N)
                        // bounds verification scan here.

                        ReconcileRuntimeScrollbars();

                        // ScrollableControl itself normally performs two layout passes
                        // around scrollbar changes. Give the native control a second base
                        // layout AFTER our new child bounds/minimum extent are known.
                        base.OnLayout(e);

                        if (Runtime == null ||
                            Runtime.IsDisposed ||
                            IsDisposed ||
                            Disposing)
                        {
                            return;
                        }

                        if (!DirectVirtualActive &&
                            !LightweightActive &&
                            Object.ReferenceEquals(runtime, Runtime) &&
                            Object.ReferenceEquals(
                                runtimeRecords,
                                RenderedItems) &&
                            runtimeRecordCount ==
                                (RenderedItems == null
                                    ? 0
                                    : RenderedItems.Count) &&
                            runtimeRefreshGeneration ==
                                RefreshGeneration &&
                            runtimePublicationRevision ==
                                RenderedItemPublicationRevision &&
                            runtimeReentrantLayoutRevision ==
                                _reentrantItemsLayoutRevision &&
                            runtimeClient == ClientSize &&
                            runtimeHorizontal == HorizontalScroll.Visible &&
                            runtimeVertical == VerticalScroll.Visible &&
                            runtimeScroll == AutoScrollPosition &&
                            runtimeViewport == GetItemsViewportRectangle() &&
                            runtimeDisplay == DisplayRectangle &&
                            runtimeOrientation == _orientation &&
                            runtimeSpacing == _spacing &&
                            runtimePadding == Padding &&
                            runtimeAutoScroll == AutoScroll &&
                            runtimeContentRightToLeft ==
                                ContentRightToLeft &&
                            runtimeConfigurationMutation ==
                                _configurationMutationGeneration &&
                            !runtime.ElementCollapseRevisionChanged(
                                runtimeCollapseRevision))
                        {
                            finalRuntimeLayoutRequired = false;
                            break;
                        }
                    }

                    if (finalRuntimeLayoutRequired &&
                        Runtime != null &&
                        !Runtime.IsDisposed &&
                        !IsDisposed &&
                        !Disposing)
                    {
                        if (Runtime.LayoutItemsControl(this))
                            ReconcileRuntimeScrollbars();
                    }
                }
                finally
                {
                    _runtimeLayoutInProgress = false;
                    RetargetActiveItemScrollAfterLayout();
                }
            }

            internal void UpdateScrollExtentMarker(
                Size contentSize,
                Point displayOrigin)
            {
                bool needsMarker =
                    DirectVirtualActive ||
                    LightweightActive ||
                    (AutoScroll &&
                     Runtime != null &&
                     GetRenderedItemCountForMarker() != 0);

                if (!needsMarker &&
                    (_scrollExtentMarker == null ||
                     _scrollExtentMarker.IsDisposed))
                {
                    return;
                }

                EnsureScrollOriginObserverMarker();

                // Virtualized lists already publish an explicit non-zero AutoScrollMinSize.
                // A huge end-marker child is redundant and can make legacy ScrollableControl
                // re-derive a stale range while the thumb is moving quickly.
                if (DirectVirtualActive || LightweightActive)
                {
                    _scrollExtentMarker.Visible = false;

                    // Initialize the hidden marker once when entering a
                    // virtual mode. After that, native ScrollableControl moves
                    // its location with the display origin; that movement is
                    // the O(1) signal used by ScrollOriginMarkerLocationChanged.
                    // Resetting an already-zero-size marker on every viewport
                    // pass would move it back to (0, 0), synchronously reenter
                    // Layout, and erase the observed scroll direction.
                    if (_scrollExtentMarker.Size != Size.Empty)
                    {
                        SetBoundsIfChanged(
                            _scrollExtentMarker,
                            Rectangle.Empty);
                    }

                    return;
                }

                _scrollExtentMarker.Visible = true;

                if (!AutoScroll ||
                    Runtime == null ||
                    GetRenderedItemCountForMarker() == 0)
                {
                    _scrollExtentMarker.Bounds = new Rectangle(0, 0, 1, 1);
                    return;
                }

                int width = Math.Max(1, contentSize.Width);
                int height = Math.Max(1, contentSize.Height);

                Rectangle markerBounds;

                if (_orientation == Orientation.Vertical)
                {
                    markerBounds = new Rectangle(
                        displayOrigin.X,
                        displayOrigin.Y + height - 1,
                        1,
                        1);
                }
                else
                {
                    markerBounds = new Rectangle(
                        displayOrigin.X + width - 1,
                        displayOrigin.Y,
                        1,
                        1);
                }

                if (_scrollExtentMarker.Bounds != markerBounds)
                    _scrollExtentMarker.Bounds = markerBounds;

                // Keep it behind real item controls without a linear
                // ControlCollection identity lookup on every layout pass.
                MoveScrollExtentMarkerBehindItems();
            }

            private void EnsureScrollOriginObserverMarker()
            {
                if (_scrollExtentMarker != null &&
                    !_scrollExtentMarker.IsDisposed)
                {
                    return;
                }

                _scrollExtentMarker = new ScrollExtentMarker();
                AttachScrollOriginObserver(_scrollExtentMarker);
                Controls.Add(_scrollExtentMarker);
                EnsureVirtualScrollOriginObserverHandle();
            }

            private void EnsureVirtualScrollOriginObserverHandle()
            {
                if ((!DirectVirtualActive && !LightweightActive) ||
                    !IsHandleCreated ||
                    IsDisposed ||
                    Disposing ||
                    _scrollExtentMarker == null ||
                    _scrollExtentMarker.IsDisposed ||
                    _scrollExtentMarker.Parent != this ||
                    _scrollExtentMarker.IsHandleCreated)
                {
                    return;
                }

                // A hidden child is skipped by Control.CreateControl on the
                // .NET 2.0/legacy path. Reading Handle explicitly creates its
                // zero-sized infrastructure HWND without making it visible or
                // contributing to the native scroll extent.
                if (_scrollExtentMarker.Handle == IntPtr.Zero)
                    return;
            }

            internal void MoveScrollExtentMarkerBehindItems()
            {
                if (_scrollExtentMarker == null ||
                    _scrollExtentMarker.IsDisposed ||
                    _scrollExtentMarker.Parent != this ||
                    Controls.Count == 0)
                {
                    return;
                }

                int lastIndex = Controls.Count - 1;
                bool previousSuppression =
                    _suppressOwnedInfrastructureLayout;

                if (DirectVirtualActive || LightweightActive)
                    _suppressOwnedInfrastructureLayout = true;

                try
                {
                    // ControlCollection.Add appends newly built item controls after
                    // the marker. Move the marker behind the completed tail once so
                    // the transaction can verify the normal item order in O(1) per
                    // row without weakening reference-identity correctness.
                    if (!Object.ReferenceEquals(
                            Controls[lastIndex],
                            _scrollExtentMarker))
                    {
                        Controls.SetChildIndex(
                            _scrollExtentMarker,
                            lastIndex);
                    }

                }
                finally
                {
                    _suppressOwnedInfrastructureLayout =
                        previousSuppression;
                }
            }

            private int GetRenderedItemCountForMarker()
            {
                if (LightweightActive)
                    return Count;

                if (DirectVirtualActive && DirectVirtualViewport != null)
                    return DirectVirtualViewport.Count;

                return RenderedItems == null
                    ? 0
                    : RenderedItems.Count;
            }

            private void ReconcileRuntimeScrollbars()
            {
                if (!AutoScroll)
                {
                    SynchronizeThemedScrollBar();
                    return;
                }

                // Preserve framework logical L while native range creation,
                // resize, and shrink replace M. This also clears the secondary
                // axis through the single physical publication path.
                ReconcileLogicalScrollOffsetAfterRangeChange();

                AdjustFormScrollbars(true);

                HideSecondaryNativeScrollBar();

                // AdjustFormScrollbars can change Maximum/LargeChange and can
                // clamp the display rectangle. Publish P from the final range.
                ReconcileLogicalScrollOffsetAfterRangeChange();
                SynchronizeThemedScrollBar();

                if (!_applyingLogicalScrollCommand &&
                    !_applyingSmoothScrollFrame)
                {
                    ReconcileThemedNativeChrome();
                }
            }

            /// <summary>Constrains scrolling to the configured item axis.</summary>
            protected override void OnScroll(ScrollEventArgs se)
            {
                if (_interceptedNativeScrollDispatchDepth > 0)
                {
                    base.OnScroll(se);

                    if (se != null)
                        se.NewValue = se.OldValue;

                    SynchronizeThemedScrollBar();
                    return;
                }

                // Never allow a transient scrollbar on the non-scrolling axis to move
                // the repeated content. The next layout reconciliation removes that bar.
                if (AutoScroll && se != null)
                {
                    bool forbiddenHorizontal =
                        _orientation == Orientation.Vertical &&
                        se.ScrollOrientation == ScrollOrientation.HorizontalScroll;

                    bool forbiddenVertical =
                        _orientation == Orientation.Horizontal &&
                        se.ScrollOrientation == ScrollOrientation.VerticalScroll;

                    if (forbiddenHorizontal || forbiddenVertical)
                    {
                        ReconcileLogicalScrollOffsetAfterRangeChange();
                        se.NewValue = 0;
                        base.OnScroll(se);
                        HideSecondaryNativeScrollBar();
                        SynchronizeThemedScrollBar();
                        return;
                    }
                }

                if (!_applyingLogicalScrollCommand &&
                    se != null &&
                    se.Type != ScrollEventType.EndScroll &&
                    AutoScroll)
                {
                    int target = GetNativeScrollEventTarget(se);

                    if (_smoothScroll &&
                        IsSmoothScrollRelativeCommand(se.Type))
                    {
                        int start = ClampLogicalScrollOffset(
                            NativePhysicalToLogicalScrollOffset(
                                Math.Max(0, se.OldValue)));

                        // Some ScrollableControl implementations move before
                        // OnScroll while others consume NewValue afterward.
                        // Restore the old logical position now, then pin
                        // NewValue after subscribers run so both paths animate
                        // from the same visible origin.
                        SetLogicalScrollOffset(start);
                        BeginSmoothScrollAnimation(target);
                        base.OnScroll(se);
                        se.NewValue =
                            LogicalToNativePhysicalScrollOffset(start);
                        SynchronizeThemedScrollBar();
                        return;
                    }

                    SetLogicalScrollOffset(target);
                    se.NewValue =
                        LogicalToNativePhysicalScrollOffset(
                            GetLogicalScrollOffset());
                }

                base.OnScroll(se);
                SynchronizeThemedScrollBar();
            }

            private int GetNativeTrackPosition(
                ScrollOrientation orientation,
                int fallback)
            {
                if (!IsHandleCreated)
                    return fallback;

                NativeScrollInfo info =
                    new NativeScrollInfo();

                info.cbSize = NativeScrollInfoSize;

                info.fMask = SIF_TRACKPOS;

                int bar =
                    orientation == ScrollOrientation.VerticalScroll
                        ? SB_VERT
                        : SB_HORZ;

                try
                {
                    if (GetScrollInfo(Handle, bar, ref info))
                        return info.nTrackPos;
                }
                catch
                {
                }

                return fallback;
            }

            /// <summary>Calculates preferred size from the current item model.</summary>
            public override Size GetPreferredSize(Size proposedSize)
            {
                if (Runtime == null)
                {
                    return base.GetPreferredSize(proposedSize);
                }

                return Runtime.GetPreferredItemsControlSize(
                    this,
                    proposedSize);
            }

            /// <summary>
            /// Cancels progressive work and releases source, cache, and runtime state.
            /// </summary>
            protected override void Dispose(bool disposing)
            {
                if (!disposing)
                {
                    base.Dispose(false);
                    return;
                }

                Exception cleanupError = null;
                Exception reportedCleanupError = null;

                try
                {
                    DetachScrollOriginObserver(_scrollExtentMarker);
                    DisposeSmoothScrollAnimation();
                    DisposeScrollBitmapCache();
                }
                catch (Exception ex)
                {
                    cleanupError = FirstItemsCommitError(
                        cleanupError,
                        ex);
                }

                try
                {
                    UnregisterLegacyMouseWheelRouting();
                }
                catch (Exception ex)
                {
                    cleanupError = FirstItemsCommitError(
                        cleanupError,
                        ex);
                }

                try
                {
                    DisposeItemSourceObservation();
                }
                catch (Exception ex)
                {
                    // State and forwarders are disabled before a custom source
                    // remove accessor runs. Report its error, but do not treat
                    // the inert physical handler as live ownership debt.
                    reportedCleanupError = ex;
                }

                try
                {
                    DisposeThemedScrollBarIntegration();
                }
                catch (Exception ex)
                {
                    cleanupError = FirstItemsCommitError(
                        cleanupError,
                        ex);
                }

                if (Runtime != null && !Runtime.IsDisposed)
                {
                    try
                    {
                        Runtime.CancelItemsRefresh(this, true);
                    }
                    catch (Exception ex)
                    {
                        cleanupError = FirstItemsCommitError(
                            cleanupError,
                            ex);
                    }

                    try
                    {
                        Runtime.DisposeDirectViewportVirtualization(this);
                    }
                    catch (Exception ex)
                    {
                        cleanupError = FirstItemsCommitError(
                            cleanupError,
                            ex);
                    }

                    try
                    {
                        Runtime.DisposeLightweightItemsControl(this);
                    }
                    catch (Exception ex)
                    {
                        cleanupError = FirstItemsCommitError(
                            cleanupError,
                            ex);
                    }

                    try
                    {
                        Runtime.DeactivateItemsControlBindingSlots(this);
                    }
                    catch (Exception ex)
                    {
                        cleanupError = FirstItemsCommitError(
                            cleanupError,
                            ex);
                    }

                    try
                    {
                        Runtime.UnregisterItemsControl(this);
                    }
                    catch (Exception ex)
                    {
                        cleanupError = FirstItemsCommitError(
                            cleanupError,
                            ex);
                    }
                }

                try
                {
                    base.Dispose(true);
                }
                catch (Exception ex)
                {
                    cleanupError = FirstItemsCommitError(
                        cleanupError,
                        ex);
                }

                if (cleanupError == null || IsDisposed)
                {
                    // Once native disposal is terminal, any retryable runtime
                    // ownership lives in runtime registries, not this disposed
                    // host. Sever its template, source, and item graphs even
                    // when a cleanup callback reported an error.
                    if (Runtime != null && !Runtime.IsDisposed)
                    {
                        try
                        {
                            Runtime.ReleaseCompiledItemTemplate(
                                TemplateRoot);
                        }
                        catch (Exception ex)
                        {
                            cleanupError = FirstItemsCommitError(
                                cleanupError,
                                ex);
                        }
                    }

                    TemplateEventTarget = null;
                    _templateContext = null;
                    TemplateRoot = null;
                    TemplateOuterXml = null;
                    TemplateFunctionExpressions = null;
                    ItemValues = null;
                    CommittedItemValues = null;

                    lock (_itemSourceSync)
                    {
                        _itemSource = null;
                        _committedItemSource = null;
                        _deferredItemsSource = null;
                        _deferredItemsHasSource = false;
                    }

                    ClearRenderedItemRecords();
                    DirectVirtualCacheRecords = null;
                    LightweightRowCache = null;
                    LightweightCacheEvictionKeys = null;
                    LightweightThumbnailCache = null;
                    LightweightVisitedLinks = null;
                    LightweightHotTarget = null;
                    WrappedLayoutScratchPlan = null;
                    WrappedLayoutScratchInUse = false;
                    PendingRefresh = null;
                    SetRefreshing(false, null);
                    Runtime = null;
                }

                if (cleanupError != null)
                {
                    if (reportedCleanupError != null)
                    {
                        throw new InvalidOperationException(
                            reportedCleanupError.Message +
                            " A later item cleanup also failed: " +
                            cleanupError.Message,
                            reportedCleanupError);
                    }

                    throw cleanupError;
                }

                if (reportedCleanupError != null)
                    throw reportedCleanupError;
            }
        }
    }
}
