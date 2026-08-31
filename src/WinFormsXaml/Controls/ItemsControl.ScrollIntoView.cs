using System;
using System.Drawing;
using System.Windows.Forms;

namespace WinFormsXaml
{
    public sealed partial class XamlRuntime
    {
        public partial class ItemsControl : IItemsBindingScrollObserver
        {
            private IItemsBindingScrollSource _observedItemScrollSource;
            private bool _deferredItemScrollRequest;
            private object _deferredItemScrollSource;
            private ItemsBindingScrollRequest
                _deferredItemsBindingScrollRequest;
            private int _deferredItemScrollIndex;
            private ItemScrollAlignment _deferredItemScrollAlignment;
            private bool _deferredItemScrollHasAnimationOverride;
            private bool _deferredItemScrollAnimate;
            private bool _itemScrollDispatchPosted;
            private bool _postedItemScrollFromBinding;
            private object _postedItemScrollSource;
            private ItemsBindingScrollRequest _postedItemScrollRequest;
#if !WINFORMSXAML_PACKAGE
            private int _itemScrollDispatchDrainCountForTest;
#endif
            private bool _activeItemScrollRequest;
            private object _activeItemScrollSource;
            private IItemsBindingScrollSource
                _activeItemScrollBindingSource;
            private ItemsBindingScrollRequest
                _activeItemsBindingScrollRequest;
            private int _activeItemScrollIndex;
            private ItemScrollAlignment _activeItemScrollAlignment;

            /// <summary>
            /// Brings one logical item into the viewport with the smallest
            /// necessary movement. SmoothScroll selects animated or immediate
            /// movement.
            /// </summary>
            public void ScrollIntoView(int index)
            {
                ScrollIntoViewCore(
                    index,
                    ItemScrollAlignment.Nearest,
                    false,
                    false);
            }

            /// <summary>
            /// Places one logical item at the requested viewport alignment.
            /// SmoothScroll selects animated or immediate movement.
            /// </summary>
            public void ScrollIntoView(
                int index,
                ItemScrollAlignment alignment)
            {
                ScrollIntoViewCore(
                    index,
                    alignment,
                    false,
                    false);
            }

            /// <summary>
            /// Places one logical item at the requested viewport alignment and
            /// explicitly selects animated or immediate movement.
            /// </summary>
            public void ScrollIntoView(
                int index,
                ItemScrollAlignment alignment,
                bool animate)
            {
                ScrollIntoViewCore(
                    index,
                    alignment,
                    true,
                    animate);
            }

            private void ScrollIntoViewCore(
                int index,
                ItemScrollAlignment alignment,
                bool hasAnimationOverride,
                bool animate)
            {
                ScrollIntoViewCore(
                    index,
                    alignment,
                    hasAnimationOverride,
                    animate,
                    null,
                    null);
            }

            private void ScrollIntoViewCore(
                int index,
                ItemScrollAlignment alignment,
                bool hasAnimationOverride,
                bool animate,
                IItemsBindingScrollSource bindingSource,
                ItemsBindingScrollRequest bindingRequest)
            {
                if (RequiresOwnerThreadMarshal())
                {
                    QueuePostedItemScrollRequest(
                        null,
                        new ItemsBindingScrollRequest(
                            index,
                            null,
                            false,
                            alignment,
                            hasAnimationOverride,
                            animate),
                        false);

                    return;
                }

                ThrowIfItemScrollUnavailable();
                ValidateItemScrollAlignment(alignment);

                if (index < 0 || index >= Count)
                    throw new ArgumentOutOfRangeException("index");

                if (_isRefreshing || PendingRefresh != null)
                {
                    if (bindingRequest != null &&
                        bindingSource != null)
                    {
                        DeferItemsBindingScrollRequest(
                            bindingSource,
                            bindingRequest);
                    }
                    else
                    {
                        DeferItemScrollRequest(
                            index,
                            alignment,
                            hasAnimationOverride,
                            animate);
                    }

                    return;
                }

                if (!AutoScroll)
                    return;

                if (TryScrollDirectVirtualItemIntoView(
                        index,
                        alignment,
                        hasAnimationOverride,
                        animate))
                {
                    TrackActiveItemScrollRequest(
                        index,
                        alignment,
                        bindingSource,
                        bindingRequest);
                    return;
                }

                RenderedItemRecord record =
                    FindRenderedItemRecordForScroll(index);

                if (record == null ||
                    record.Control == null ||
                    record.Control.IsDisposed)
                {
                    // A root Condition can intentionally remove one logical
                    // item. Do not accidentally scroll the following rendered
                    // record merely because its compacted array index matches.
                    return;
                }

                long itemStart;
                long itemExtent;

                if (!TryGetNormalItemScrollBounds(
                        record.Control,
                        out itemStart,
                        out itemExtent))
                {
                    return;
                }

                int target = CalculateItemScrollTarget(
                    itemStart,
                    itemExtent,
                    GetLogicalScrollOffset(),
                    GetItemScrollViewportExtent(),
                    alignment);

                ApplyItemScrollTarget(
                    target,
                    hasAnimationOverride,
                    animate);
                TrackActiveItemScrollRequest(
                    index,
                    alignment,
                    bindingSource,
                    bindingRequest);
            }

            private void ThrowIfItemScrollUnavailable()
            {
                if (IsDisposed || Disposing || _itemsSourceDisposed)
                {
                    throw new ObjectDisposedException(
                        GetType().FullName);
                }
            }

            private static void ValidateItemScrollAlignment(
                ItemScrollAlignment alignment)
            {
                if (alignment < ItemScrollAlignment.Nearest ||
                    alignment > ItemScrollAlignment.End)
                {
                    throw new ArgumentOutOfRangeException("alignment");
                }
            }

            private RenderedItemRecord FindRenderedItemRecordForScroll(
                int logicalIndex)
            {
                if (RenderedItems == null || RenderedItems.Count == 0)
                    return null;

                int low = 0;
                int high = RenderedItems.Count - 1;

                while (low <= high)
                {
                    int middle = low + ((high - low) / 2);
                    RenderedItemRecord record =
                        RenderedItems[middle] as RenderedItemRecord;

                    if (record == null)
                        return null;

                    if (record.LogicalIndex == logicalIndex)
                        return record;

                    if (record.LogicalIndex < logicalIndex)
                        low = middle + 1;
                    else
                        high = middle - 1;
                }

                return null;
            }

            private bool TryGetNormalItemScrollBounds(
                Control control,
                out long itemStart,
                out long itemExtent)
            {
                itemStart = 0L;
                itemExtent = 0L;

                if (control == null || control.IsDisposed)
                    return false;

                Rectangle viewport = GetItemsViewportRectangle();
                int current = GetLogicalScrollOffset();

                if (_orientation == Orientation.Vertical)
                {
                    itemStart = (long)current +
                        (long)control.Top - viewport.Top;
                    itemExtent = Math.Max(0, control.Height);
                }
                else if (ContentRightToLeft)
                {
                    itemStart = (long)current +
                        (long)viewport.Right - control.Right;
                    itemExtent = Math.Max(0, control.Width);
                }
                else
                {
                    itemStart = (long)current +
                        (long)control.Left - viewport.Left;
                    itemExtent = Math.Max(0, control.Width);
                }

                if (itemStart < 0L)
                    itemStart = 0L;

                return itemExtent > 0L;
            }

            internal int GetItemScrollViewportExtent()
            {
                Rectangle viewport = GetItemsViewportRectangle();

                return Math.Max(
                    0,
                    _orientation == Orientation.Vertical
                        ? viewport.Height
                        : viewport.Width);
            }

            internal static int CalculateItemScrollTarget(
                long itemStart,
                long itemExtent,
                int currentOffset,
                int viewportExtent,
                ItemScrollAlignment alignment)
            {
                ValidateItemScrollAlignment(alignment);

                long start = Math.Max(0L, itemStart);
                long extent = Math.Max(0L, itemExtent);
                long current = Math.Max(0, currentOffset);
                long viewport = Math.Max(0, viewportExtent);
                long end = SaturatingAddItemScrollValue(start, extent);
                long viewportEnd =
                    SaturatingAddItemScrollValue(current, viewport);
                long target;

                if (alignment == ItemScrollAlignment.Start)
                {
                    target = start;
                }
                else if (alignment == ItemScrollAlignment.Center)
                {
                    target = SaturatingAddItemScrollValue(
                        start,
                        extent / 2L) - (viewport / 2L);
                }
                else if (alignment == ItemScrollAlignment.End)
                {
                    target = end - viewport;
                }
                else if (start >= current && end <= viewportEnd)
                {
                    target = current;
                }
                else if (extent > viewport &&
                         start <= current &&
                         end >= viewportEnd)
                {
                    target = current;
                }
                else
                {
                    long startTarget = start;
                    long endTarget = end - viewport;

                    if (endTarget < 0L)
                        endTarget = 0L;

                    long startDistance = AbsoluteItemScrollDistance(
                        startTarget,
                        current);
                    long endDistance = AbsoluteItemScrollDistance(
                        endTarget,
                        current);

                    target = startDistance <= endDistance
                        ? startTarget
                        : endTarget;
                }

                if (target <= 0L)
                    return 0;

                return target >= Int32.MaxValue
                    ? Int32.MaxValue
                    : (int)target;
            }

            private static long SaturatingAddItemScrollValue(
                long left,
                long right)
            {
                if (right > 0L && left > Int64.MaxValue - right)
                    return Int64.MaxValue;

                return left + right;
            }

            private static long AbsoluteItemScrollDistance(
                long left,
                long right)
            {
                if (left >= right)
                    return left - right;

                return right - left;
            }

            internal bool ShouldAnimateItemScroll(
                bool hasAnimationOverride,
                bool animate)
            {
                return hasAnimationOverride
                    ? animate
                    : SmoothScroll;
            }

            internal bool ApplyItemScrollTarget(
                int target,
                bool hasAnimationOverride,
                bool animate)
            {
                if (hasAnimationOverride)
                {
                    return animate
                        ? BeginSmoothScrollAnimation(target, true)
                        : SetLogicalScrollOffset(target);
                }

                return ApplyLogicalScrollTarget(target, true);
            }

            private void TrackActiveItemScrollRequest(
                int index,
                ItemScrollAlignment alignment,
                IItemsBindingScrollSource bindingSource,
                ItemsBindingScrollRequest bindingRequest)
            {
                if (!_smoothScrollActive)
                {
                    ClearActiveItemScrollRequest();
                    return;
                }

                _activeItemScrollRequest = true;
                _activeItemScrollSource = ItemSource;
                _activeItemScrollBindingSource = bindingSource;
                _activeItemsBindingScrollRequest = bindingRequest;
                _activeItemScrollIndex = index;
                _activeItemScrollAlignment = alignment;
            }

            private void ClearActiveItemScrollRequest()
            {
                _activeItemScrollRequest = false;
                _activeItemScrollSource = null;
                _activeItemScrollBindingSource = null;
                _activeItemsBindingScrollRequest = null;
                _activeItemScrollIndex = 0;
                _activeItemScrollAlignment =
                    ItemScrollAlignment.Nearest;
            }

            /// <summary>
            /// Recomputes an active item-aware animation after layout or
            /// virtualization measures different geometry. Relative wheel and
            /// scrollbar animations intentionally keep their pixel target.
            /// </summary>
            internal void RetargetActiveItemScrollAfterLayout()
            {
                if (!_activeItemScrollRequest || !_smoothScrollActive)
                    return;

                if (IsDisposed || Disposing || _itemsSourceDisposed ||
                    !Object.ReferenceEquals(
                        _activeItemScrollSource,
                        ItemSource))
                {
                    StopSmoothScrollAnimation();
                    return;
                }

                int index = _activeItemScrollIndex;

                if (_activeItemsBindingScrollRequest != null)
                {
                    IItemsBindingScrollSource bindingSource =
                        _activeItemScrollBindingSource;

                    lock (_itemSourceSync)
                    {
                        if (bindingSource == null ||
                            !Object.ReferenceEquals(
                                bindingSource,
                                _observedItemScrollSource))
                        {
                            StopSmoothScrollAnimation();
                            return;
                        }
                    }

                    index = bindingSource.ResolveScrollIndex(
                        _activeItemsBindingScrollRequest);
                }

                if (index < 0 || index >= Count)
                {
                    StopSmoothScrollAnimation();
                    return;
                }

                long itemStart;
                long itemExtent;

                if (DirectVirtualActive &&
                    !DirectVirtualDisposed &&
                    DirectVirtualViewport != null)
                {
                    itemStart =
                        DirectVirtualViewport.GetOffset(index);
                    itemExtent = GetDirectVirtualItemContentExtent(
                        this,
                        DirectVirtualViewport,
                        index);
                }
                else if (LightweightActive &&
                         !LightweightDisposed)
                {
                    long stride =
                        (long)FixedItemSize + Spacing;
                    itemStart = stride * (long)index;
                    itemExtent = FixedItemSize;
                }
                else
                {
                    RenderedItemRecord record =
                        FindRenderedItemRecordForScroll(index);

                    if (record == null ||
                        !TryGetNormalItemScrollBounds(
                            record.Control,
                            out itemStart,
                            out itemExtent))
                    {
                        StopSmoothScrollAnimation();
                        return;
                    }
                }

                _activeItemScrollIndex = index;

                int target = CalculateItemScrollTarget(
                    itemStart,
                    itemExtent,
                    GetLogicalScrollOffset(),
                    GetItemScrollViewportExtent(),
                    _activeItemScrollAlignment);

                RetargetSmoothScrollAnimation(target);
            }

            private void DeferItemScrollRequest(
                int index,
                ItemScrollAlignment alignment,
                bool hasAnimationOverride,
                bool animate)
            {
                _deferredItemScrollRequest = true;
                _deferredItemScrollSource = ItemSource;
                _deferredItemsBindingScrollRequest = null;
                _deferredItemScrollIndex = index;
                _deferredItemScrollAlignment = alignment;
                _deferredItemScrollHasAnimationOverride =
                    hasAnimationOverride;
                _deferredItemScrollAnimate = animate;
            }

            internal void ApplyDeferredItemScrollRequest()
            {
                if (!_deferredItemScrollRequest)
                    return;

                int index = _deferredItemScrollIndex;
                object source = _deferredItemScrollSource;
                ItemScrollAlignment alignment =
                    _deferredItemScrollAlignment;
                bool hasAnimationOverride =
                    _deferredItemScrollHasAnimationOverride;
                bool animate = _deferredItemScrollAnimate;
                ItemsBindingScrollRequest bindingRequest =
                    _deferredItemsBindingScrollRequest;

                ClearDeferredItemScrollRequest();

                if (!Object.ReferenceEquals(source, ItemSource) ||
                    IsDisposed || Disposing || _itemsSourceDisposed)
                {
                    return;
                }

                if (bindingRequest != null)
                {
                    IItemsBindingScrollSource bindingSource =
                        source as IItemsBindingScrollSource;

                    lock (_itemSourceSync)
                    {
                        if (!Object.ReferenceEquals(
                                bindingSource,
                                _observedItemScrollSource))
                        {
                            return;
                        }
                    }

                    if (bindingSource == null)
                        return;

                    index = bindingSource.ResolveScrollIndex(
                        bindingRequest);
                    alignment = bindingRequest.Alignment;
                    hasAnimationOverride =
                        bindingRequest.HasAnimationOverride;
                    animate = bindingRequest.Animate;
                }

                if (index < 0 || index >= Count)
                    return;

                ScrollIntoViewCore(
                    index,
                    alignment,
                    hasAnimationOverride,
                    animate,
                    source as IItemsBindingScrollSource,
                    bindingRequest);
            }

            internal void ClearDeferredItemScrollRequest()
            {
                _deferredItemScrollRequest = false;
                _deferredItemScrollSource = null;
                _deferredItemsBindingScrollRequest = null;
                _deferredItemScrollIndex = 0;
                _deferredItemScrollAlignment =
                    ItemScrollAlignment.Nearest;
                _deferredItemScrollHasAnimationOverride = false;
                _deferredItemScrollAnimate = false;
            }

            void IItemsBindingScrollObserver.OnItemsBindingScrollRequested(
                object source,
                ItemsBindingScrollRequest request)
            {
                if (request == null)
                    return;

                lock (_itemSourceSync)
                {
                    if (_itemsSourceDisposed ||
                        !_itemsSourceInitializationComplete ||
                        !Object.ReferenceEquals(
                            source,
                            _observedItemScrollSource) ||
                        !Object.ReferenceEquals(source, _itemSource))
                    {
                        return;
                    }
                }

                if (RequiresOwnerThreadMarshal())
                {
                    QueuePostedItemScrollRequest(
                        source,
                        request,
                        true);

                    return;
                }

                HandleItemsBindingScrollRequest(
                    source,
                    request);
            }

            private void HandleItemsBindingScrollRequest(
                object source,
                ItemsBindingScrollRequest request)
            {
                bool sourceReloadPending;

                lock (_itemSourceSync)
                {
                    if (_itemsSourceDisposed ||
                        request == null ||
                        !Object.ReferenceEquals(
                            source,
                            _observedItemScrollSource) ||
                        !Object.ReferenceEquals(source, _itemSource))
                    {
                        return;
                    }

                    sourceReloadPending =
                        _itemSourceReloadPending ||
                        _itemSourceReloadPosted;
                }

                if (sourceReloadPending ||
                    _isRefreshing || PendingRefresh != null)
                {
                    DeferItemsBindingScrollRequest(
                        source,
                        request);
                    return;
                }

                IItemsBindingScrollSource bindingSource =
                    source as IItemsBindingScrollSource;

                if (bindingSource == null)
                    return;

                int index = bindingSource.ResolveScrollIndex(request);

                if (index < 0 || index >= Count)
                    return;

                ScrollIntoViewCore(
                    index,
                    request.Alignment,
                    request.HasAnimationOverride,
                    request.Animate,
                    bindingSource,
                    request);
            }

            private void DeferItemsBindingScrollRequest(
                object source,
                ItemsBindingScrollRequest request)
            {
                _deferredItemScrollRequest = true;
                _deferredItemScrollSource = source;
                _deferredItemsBindingScrollRequest = request;
                _deferredItemScrollIndex = 0;
                _deferredItemScrollAlignment = request.Alignment;
                _deferredItemScrollHasAnimationOverride =
                    request.HasAnimationOverride;
                _deferredItemScrollAnimate = request.Animate;
            }

            private void QueuePostedItemScrollRequest(
                object source,
                ItemsBindingScrollRequest request,
                bool fromBinding)
            {
                bool post;

                lock (_itemSourceSync)
                {
                    if (_itemsSourceDisposed)
                        return;

                    _postedItemScrollSource = source;
                    _postedItemScrollRequest = request;
                    _postedItemScrollFromBinding = fromBinding;
                    post = !_itemScrollDispatchPosted;
                    _itemScrollDispatchPosted = true;
                }

                if (!post)
                    return;

                try
                {
                    BeginInvoke(
                        (MethodInvoker)delegate
                        {
                            DrainPostedItemScrollRequest();
                        });
                }
                catch
                {
                    lock (_itemSourceSync)
                    {
                        _itemScrollDispatchPosted = false;
                        _postedItemScrollFromBinding = false;
                        _postedItemScrollSource = null;
                        _postedItemScrollRequest = null;
                    }

                    throw;
                }
            }

            private void DrainPostedItemScrollRequest()
            {
                object source;
                ItemsBindingScrollRequest request;
                bool fromBinding;

                lock (_itemSourceSync)
                {
#if !WINFORMSXAML_PACKAGE
                    _itemScrollDispatchDrainCountForTest++;
#endif
                    _itemScrollDispatchPosted = false;
                    source = _postedItemScrollSource;
                    request = _postedItemScrollRequest;
                    fromBinding = _postedItemScrollFromBinding;
                    _postedItemScrollSource = null;
                    _postedItemScrollRequest = null;
                    _postedItemScrollFromBinding = false;
                }

                if (request == null ||
                    IsDisposed || Disposing || _itemsSourceDisposed)
                {
                    return;
                }

                if (fromBinding)
                {
                    HandleItemsBindingScrollRequest(
                        source,
                        request);
                    return;
                }

                ScrollIntoViewCore(
                    request.Index,
                    request.Alignment,
                    request.HasAnimationOverride,
                    request.Animate);
            }

#if !WINFORMSXAML_PACKAGE
            internal int ItemScrollDispatchDrainCountForTest
            {
                get
                {
                    lock (_itemSourceSync)
                        return _itemScrollDispatchDrainCountForTest;
                }
            }
#endif

            private void ReplaceItemScrollObservation(
                int subscriptionEpoch,
                IItemsBindingScrollSource desiredSource)
            {
                IItemsBindingScrollSource previousSource;

                lock (_itemSourceSync)
                {
                    if (_itemsSourceDisposed ||
                        subscriptionEpoch != _itemSourceSubscriptionEpoch)
                    {
                        return;
                    }

                    previousSource = _observedItemScrollSource;

                    if (Object.ReferenceEquals(
                            previousSource,
                            desiredSource))
                    {
                        return;
                    }

                    _observedItemScrollSource = desiredSource;
                }

                if (previousSource != null)
                {
                    previousSource.RemoveScrollObserver(this);
                }

                if (desiredSource == null)
                    return;

                desiredSource.AddScrollObserver(this);

                lock (_itemSourceSync)
                {
                    if (!_itemsSourceDisposed &&
                        subscriptionEpoch == _itemSourceSubscriptionEpoch &&
                        Object.ReferenceEquals(
                            desiredSource,
                            _observedItemScrollSource))
                    {
                        return;
                    }
                }

                desiredSource.RemoveScrollObserver(this);
            }

            private IItemsBindingScrollSource
                DetachItemScrollObservationForDisposal()
            {
                IItemsBindingScrollSource source =
                    _observedItemScrollSource;
                _observedItemScrollSource = null;
                _itemScrollDispatchPosted = false;
                _postedItemScrollFromBinding = false;
                _postedItemScrollSource = null;
                _postedItemScrollRequest = null;
                ClearDeferredItemScrollRequest();
                ClearActiveItemScrollRequest();
                return source;
                    }
                }
            }
}
