using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Xml;
using System.Windows.Forms;

namespace WinFormsXaml
{
    public sealed partial class XamlRuntime
    {
        internal void HandleLightweightViewportChanged(ItemsControl host)
        {
            if (host == null || !host.LightweightActive)
                return;

            host.LightweightHotTarget = null;
            host.Cursor = Cursors.Default;
            UpdateLightweightScrollExtent(host);
            UpdateLightweightOverscanDirection(host);
            UpdateLightweightVisibleRange(host);

            if (!PrepareLightweightVisibleRows(
                    host,
                    host.RefreshGeneration))
            {
                return;
            }

            host.Invalidate(false);
        }

        private static void UpdateLightweightOverscanDirection(
            ItemsControl host)
        {
            int current = Math.Max(
                0,
                -host.AutoScrollPosition.Y);

            if (!host.LightweightHasViewportOffset)
            {
                host.LightweightOverscanDirection = 0;
                host.LightweightHasViewportOffset = true;
            }
            else if (current > host.LightweightLastViewportOffset)
            {
                host.LightweightOverscanDirection = 1;
            }
            else if (current < host.LightweightLastViewportOffset)
            {
                host.LightweightOverscanDirection = -1;
            }
            else
            {
                host.LightweightOverscanDirection = 0;
            }

            host.LightweightLastViewportOffset = current;
        }

        private void UpdateLightweightScrollExtent(ItemsControl host)
        {
            long count = host.Count;
            long extent =
                (count * (long)host.FixedItemSize) +
                (Math.Max(0L, count - 1L) * (long)host.Spacing) +
                host.Padding.Top +
                host.Padding.Bottom;
            int height = extent > Int32.MaxValue
                ? Int32.MaxValue
                : (int)Math.Max(0L, extent);
            Size desired = count == 0L
                ? Size.Empty
                : new Size(1, Math.Max(1, height));

            if (host.AutoScrollMinSize != desired)
                host.AutoScrollMinSize = desired;

            host.UpdateScrollExtentMarker(desired, Point.Empty);
        }

        private static void UpdateLightweightVisibleRange(
            ItemsControl host)
        {
            int first;
            int last;
            GetLightweightVisibleRange(host, out first, out last);
            host.LightweightRealizedStart = first;
            host.LightweightRealizedEnd = last;
            host.LightweightRealizedCount = first < 0
                ? 0
                : last - first + 1;
        }

        internal static void GetLightweightPreparedRange(
            ItemsControl host,
            int visibleStart,
            int visibleEnd,
            out int preparedStart,
            out int preparedEnd)
        {
            preparedStart = -1;
            preparedEnd = -1;

            if (host == null || host.Count <= 0 ||
                visibleStart < 0 || visibleEnd < visibleStart)
            {
                return;
            }

            long overscan = Math.Max(0, host.OverscanItems);
            long before = overscan;
            long after = overscan;

            // Keep one fixed budget (2 * OverscanItems). Stationary/initial
            // viewports split it symmetrically; a known scroll direction
            // moves the whole budget ahead of the visible rows.
            if (host.LightweightOverscanDirection > 0)
            {
                before = 0L;
                after = overscan * 2L;
            }
            else if (host.LightweightOverscanDirection < 0)
            {
                before = overscan * 2L;
                after = 0L;
            }

            long first = Math.Max(
                0L,
                (long)visibleStart - before);
            long last = Math.Min(
                (long)host.Count - 1L,
                (long)visibleEnd + after);

            preparedStart = (int)first;
            preparedEnd = (int)last;
        }

        private bool PrepareLightweightVisibleRows(
            ItemsControl host,
            int transitionGeneration)
        {
            if (host == null || !host.LightweightActive ||
                host.LightweightPlan == null ||
                host.LightweightPlan.Root == null)
            {
                return true;
            }

            PrepareLightweightFonts(
                host,
                host.LightweightPlan.Root);

            int visibleFirst = host.LightweightRealizedStart;
            int visibleLast = host.LightweightRealizedEnd;

            if (visibleFirst < 0)
                return true;

            int first;
            int last;
            GetLightweightPreparedRange(
                host,
                visibleFirst,
                visibleLast,
                out first,
                out last);

            int i;

            for (i = first; i <= last; i++)
            {
                if (!OwnsItemsTransition(host, transitionGeneration) ||
                    host.ItemValues == null ||
                    i >= host.ItemValues.Count)
                {
                    return false;
                }

                LightweightRowSnapshot snapshot =
                    GetLightweightRowSnapshot(host, i);
                if (!EnsureLightweightRowSnapshotPrepared(host, snapshot))
                {
                    // A getter/subscription accessor may reenter viewport or
                    // item work and retire this candidate. If the refresh
                    // generation still belongs to us, that nested work owns
                    // the current range; only a superseding transition aborts.
                    return OwnsItemsTransition(
                        host,
                        transitionGeneration);
                }

                if (!OwnsItemsTransition(host, transitionGeneration))
                    return false;
            }

            TrimLightweightRowCache(
                host,
                visibleFirst,
                visibleLast);
            return true;
        }

        private bool EnsureLightweightRowSnapshotPrepared(
            ItemsControl host,
            LightweightRowSnapshot snapshot)
        {
            if (!IsLightweightRowSnapshotCurrent(host, snapshot))
                return false;

            if (snapshot.Prepared)
                return true;

            try
            {
                PrepareLightweightNodeValues(
                    host,
                    host.LightweightPlan.Root,
                    snapshot);

                if (!IsLightweightRowSnapshotCurrent(host, snapshot))
                    return false;

                AttachLightweightRowObservableBindings(host, snapshot);

                if (!IsLightweightRowSnapshotCurrent(host, snapshot))
                {
                    DetachObservableBindings(snapshot);
                    return false;
                }

                snapshot.Prepared = true;
                return true;
            }
            catch
            {
                if (host != null && host.LightweightRowCache != null &&
                    Object.ReferenceEquals(
                        host.LightweightRowCache[snapshot.Index],
                        snapshot))
                {
                    host.LightweightRowCache.Remove(snapshot.Index);
                }

                try
                {
                    ReleaseLightweightRowSnapshot(host, snapshot);
                }
                catch
                {
                    // Preserve the value/dependency preparation failure.
                }

                throw;
            }
        }

        private static bool IsLightweightRowSnapshotCurrent(
            ItemsControl host,
            LightweightRowSnapshot snapshot)
        {
            return host != null && snapshot != null &&
                !snapshot.Retired && host.LightweightActive &&
                !host.LightweightDisposed &&
                host.LightweightPlan != null &&
                host.LightweightRowCache != null &&
                snapshot.Generation == host.LightweightGeneration &&
                snapshot.Index >= 0 && host.ItemValues != null &&
                snapshot.Index < host.ItemValues.Count &&
                Object.ReferenceEquals(
                    host.LightweightRowCache[snapshot.Index],
                    snapshot) &&
                Object.ReferenceEquals(
                    host.ItemValues[snapshot.Index],
                    snapshot.Item);
        }

        private static void PrepareLightweightFonts(
            ItemsControl host,
            LightweightTemplateNode node)
        {
            if (IsLightweightTextLeaf(node.Kind))
                GetLightweightFont(host, node);

            int i;

            for (i = 0; i < node.Children.Count; i++)
            {
                PrepareLightweightFonts(
                    host,
                    node.Children[i] as LightweightTemplateNode);
            }
        }

        private void PrepareLightweightNodeValues(
            ItemsControl host,
            LightweightTemplateNode node,
            LightweightRowSnapshot snapshot)
        {
            if (node.BackColor != null)
            {
                Color background = ResolveLightweightColor(
                    host,
                    node.BackColor,
                    snapshot,
                    Color.Transparent);

                if (background.A != 0)
                    GetLightweightBrush(host, background);
            }

            if (node.Kind == LightweightNodeKind.Border &&
                node.BorderThickness != Padding.Empty)
            {
                Color border = ResolveLightweightColor(
                    host,
                    node.BorderColor,
                    snapshot,
                    SystemColors.ControlDark);
                GetLightweightBrush(host, border);
            }

            if (node.ForeColor != null)
            {
                ResolveLightweightColor(
                    host,
                    node.ForeColor,
                    snapshot,
                    host.ForeColor);
            }

            if (node.LinkColor != null)
            {
                ResolveLightweightColor(
                    host,
                    node.LinkColor,
                    snapshot,
                    Color.Blue);
            }

            if (node.VisitedLinkColor != null)
            {
                ResolveLightweightColor(
                    host,
                    node.VisitedLinkColor,
                    snapshot,
                    Color.Purple);
            }

            if (node.Text != null)
            {
                ResolveLightweightText(
                    host,
                    node.Text,
                    snapshot,
                    String.Empty);
            }

            if (node.NavigateUri != null)
            {
                ResolveLightweightText(
                    host,
                    node.NavigateUri,
                    snapshot,
                    null);

                if (node.Kind == LightweightNodeKind.HyperlinkLabel)
                    GetLightweightLinkKey(snapshot, node);
            }

            if (node.Enabled != null)
            {
                ResolveLightweightBoolean(
                    host,
                    node.Enabled,
                    snapshot,
                    true);
            }

            if (node.Checked != null)
            {
                ResolveLightweightBoolean(
                    host,
                    node.Checked,
                    snapshot,
                    false);
            }

            if (node.Kind == LightweightNodeKind.Image)
            {
                ValidateLightweightImage(
                    node,
                    ResolveLightweightImage(host, node, snapshot));
            }

            int i;

            for (i = 0; i < node.Children.Count; i++)
            {
                PrepareLightweightNodeValues(
                    host,
                    node.Children[i] as LightweightTemplateNode,
                    snapshot);
            }
        }

        private void AttachLightweightRowObservableBindings(
            ItemsControl host,
            LightweightRowSnapshot snapshot)
        {
            if (host == null || snapshot == null ||
                host.LightweightPlan == null ||
                host.LightweightPlan.Root == null)
            {
                return;
            }

            BindingPathResult dependencies = null;
            object previousTarget = _activeComponentEventTarget;
            ItemTemplateActiveContext previousContext =
                PushItemTemplateDeclarationContext(host);

            try
            {
                _activeComponentEventTarget = host.TemplateEventTarget;
                CollectLightweightNodeObservableDependencies(
                    host.LightweightPlan.Root,
                    GetItemDataContext(snapshot.Item),
                    ref dependencies);
            }
            finally
            {
                _activeComponentEventTarget = previousTarget;
                RestoreItemTemplateDeclarationContext(previousContext);
            }

            if (dependencies == null ||
                dependencies.Dependencies.Count == 0)
            {
                return;
            }

            AttachObservableBinding(
                snapshot,
                null,
                null,
                BindingMode.OneWay,
                BindingUpdateSourceTrigger.PropertyChanged,
                dependencies,
                OnLightweightRowObservableBindingChanged);
        }

        private void CollectLightweightNodeObservableDependencies(
            LightweightTemplateNode node,
            object item,
            ref BindingPathResult aggregate)
        {
            if (node == null)
                return;

            MergeLightweightSlotObservableDependencies(
                node.Text,
                item,
                ref aggregate);
            MergeLightweightSlotObservableDependencies(
                node.ForeColor,
                item,
                ref aggregate);
            MergeLightweightSlotObservableDependencies(
                node.BackColor,
                item,
                ref aggregate);
            MergeLightweightSlotObservableDependencies(
                node.BorderColor,
                item,
                ref aggregate);
            BindingPathResult checkedDependencies =
                MergeLightweightSlotObservableDependencies(
                node.Checked,
                item,
                ref aggregate);
            ValidateLightweightCheckBoxEndpoint(
                node,
                checkedDependencies);
            MergeLightweightSlotObservableDependencies(
                node.Enabled,
                item,
                ref aggregate);
            MergeLightweightSlotObservableDependencies(
                node.NavigateUri,
                item,
                ref aggregate);
            MergeLightweightSlotObservableDependencies(
                node.LinkColor,
                item,
                ref aggregate);
            MergeLightweightSlotObservableDependencies(
                node.VisitedLinkColor,
                item,
                ref aggregate);
            MergeLightweightSlotObservableDependencies(
                node.Source,
                item,
                ref aggregate);

            int i;

            for (i = 0; i < node.Children.Count; i++)
            {
                CollectLightweightNodeObservableDependencies(
                    node.Children[i] as LightweightTemplateNode,
                    item,
                    ref aggregate);
            }
        }

        private BindingPathResult MergeLightweightSlotObservableDependencies(
            LightweightValueSlot slot,
            object item,
            ref BindingPathResult aggregate)
        {
            if (slot == null || !slot.Dynamic)
                return null;

            BindingExpressionPlan directPlan;
            BindingPathResult slotDependencies =
                ResolveObservableExpressionDependencies(
                    slot.Expression,
                    item,
                    out directPlan);

            if (slotDependencies == null ||
                slotDependencies.Dependencies.Count == 0)
            {
                return slotDependencies;
            }

            if (aggregate == null)
                aggregate = new BindingPathResult();

            MergeBindingPathDependencies(
                aggregate,
                slotDependencies,
                aggregate.DependencySourceIndex);
            return slotDependencies;
        }

        private void ValidateLightweightCheckBoxEndpoint(
            LightweightTemplateNode node,
            BindingPathResult path)
        {
            BindingExpressionPlan checkedPlan =
                node == null
                    ? null
                    : GetLightweightBindingPlan(node.Checked);

            if (node == null || node.Kind != LightweightNodeKind.CheckBox ||
                checkedPlan == null ||
                checkedPlan.Mode != BindingMode.TwoWay)
            {
                return;
            }

            BindingPathDependency endpoint = path == null
                ? null
                : path.TerminalDependency;

            if (endpoint == null ||
                (endpoint.RuntimeBinding == null &&
                 !IsWritableNotifyPropertyDependency(endpoint)))
            {
                throw LightweightMarkupError(
                    node.SourceElement,
                    node.Checked.PropertyName,
                    "The TwoWay lightweight CheckBox binding must end in a " +
                    "writable PropertyBinding<T> or notifying CLR property.");
            }
        }

        private void OnLightweightRowObservableBindingChanged(
            object owner,
            long revision)
        {
            LightweightRowSnapshot snapshot =
                owner as LightweightRowSnapshot;
            ItemsControl host = snapshot == null
                ? null
                : snapshot.Host;

            if (!IsLightweightRowSnapshotCurrent(host, snapshot))
                return;

            int index = snapshot.Index;
            int generation = snapshot.Generation;
            object item = snapshot.Item;

            if (host.LightweightHotTarget != null &&
                host.LightweightHotTarget.Index == index)
            {
                host.LightweightHotTarget = null;
                host.Cursor = Cursors.Default;
            }

            host.LightweightRowCache.Remove(index);
            ReleaseLightweightRowSnapshot(host, snapshot);

            if (!host.LightweightActive || host.LightweightDisposed ||
                host.LightweightGeneration != generation ||
                host.LightweightRowCache == null ||
                host.ItemValues == null || index < 0 ||
                index >= host.ItemValues.Count ||
                host.LightweightRowCache.ContainsKey(index) ||
                !Object.ReferenceEquals(host.ItemValues[index], item))
            {
                return;
            }

            int preparedStart;
            int preparedEnd;
            GetLightweightPreparedRange(
                host,
                host.LightweightRealizedStart,
                host.LightweightRealizedEnd,
                out preparedStart,
                out preparedEnd);

            if (index < preparedStart || index > preparedEnd)
                return;

            LightweightRowSnapshot replacement =
                GetLightweightRowSnapshot(host, index);

            if (EnsureLightweightRowSnapshotPrepared(host, replacement))
                host.Invalidate(GetLightweightRowBounds(host, index));
        }

        internal static void GetLightweightVisibleRange(
            ItemsControl host,
            out int first,
            out int last)
        {
            first = -1;
            last = -1;

            if (host == null || host.Count <= 0 ||
                host.FixedItemSize <= 0 || host.ClientSize.Height <= 0)
            {
                return;
            }

            long stride = Math.Max(
                1L,
                (long)host.FixedItemSize + host.Spacing);
            int offset = Math.Max(0, -host.AutoScrollPosition.Y);
            long visibleTop = Math.Max(
                0L,
                (long)offset - host.Padding.Top);
            long visibleBottom =
                (long)offset +
                Math.Max(0, host.ClientSize.Height - 1) -
                host.Padding.Top;

            if (visibleBottom < 0L)
                return;

            first = (int)Math.Min(
                host.Count - 1L,
                visibleTop / stride);

            if ((visibleTop % stride) >= host.FixedItemSize)
                first++;

            last = (int)Math.Min(
                host.Count - 1L,
                Math.Max(0L, visibleBottom) / stride);

            if (first >= host.Count || last < first)
            {
                first = -1;
                last = -1;
            }
        }

        internal void ScrollLightweightItemIntoView(
            ItemsControl host,
            int index)
        {
            ScrollLightweightItemIntoView(
                host,
                index,
                ItemScrollAlignment.Nearest,
                false,
                false);
        }

        internal void ScrollLightweightItemIntoView(
            ItemsControl host,
            int index,
            ItemScrollAlignment alignment,
            bool hasAnimationOverride,
            bool animate)
        {
            if (host == null || !host.LightweightActive)
                return;

            if (index < 0 || index >= host.Count)
                throw new ArgumentOutOfRangeException("index");

            long stride =
                (long)host.FixedItemSize + host.Spacing;
            long logicalStart = stride * (long)index;
            int current = host.GetLogicalScrollOffset();
            int target = ItemsControl.CalculateItemScrollTarget(
                logicalStart,
                host.FixedItemSize,
                current,
                host.GetItemScrollViewportExtent(),
                alignment);

            host.ApplyItemScrollTarget(
                target,
                hasAnimationOverride,
                animate);

            if (!host._smoothScrollActive)
            {
                HandleLightweightViewportChanged(host);
            }
        }

        internal void PaintLightweightItems(
            ItemsControl host,
            PaintEventArgs e)
        {
            if (host == null || e == null ||
                !host.LightweightActive || host.LightweightPlan == null ||
                host.LightweightPlan.Root == null)
            {
                return;
            }

            UpdateLightweightVisibleRange(host);
            int first = host.LightweightRealizedStart;
            int last = host.LightweightRealizedEnd;

            if (first < 0)
                return;

            bool previousThumbnailPaintAllowed =
                host.LightweightThumbnailPaintAllowed;
            host.LightweightThumbnailPaintAllowed =
                CanUseLightweightThumbnailTransform(e.Graphics);

            try
            {
                int i;

                for (i = first; i <= last; i++)
                {
                    Rectangle rowBounds = GetLightweightRowBounds(host, i);

                    if (!rowBounds.IntersectsWith(e.ClipRectangle))
                        continue;

                    LightweightRowSnapshot snapshot =
                        GetLightweightRowSnapshot(host, i);

                    if (!EnsureLightweightRowSnapshotPrepared(host, snapshot))
                        return;

                    DrawLightweightNode(
                        host,
                        e.Graphics,
                        host.LightweightPlan.Root,
                        rowBounds,
                        snapshot,
                        i);
                }

                TrimLightweightRowCache(host, first, last);
            }
            finally
            {
                host.LightweightThumbnailPaintAllowed =
                    previousThumbnailPaintAllowed;
            }
        }

        private static Rectangle GetLightweightRowBounds(
            ItemsControl host,
            int index)
        {
            long stride =
                (long)host.FixedItemSize + host.Spacing;
            long logicalY =
                host.Padding.Top + (stride * index);
            int offset = Math.Max(0, -host.AutoScrollPosition.Y);
            long screenY = logicalY - offset;
            int y = screenY < Int32.MinValue
                ? Int32.MinValue
                : (screenY > Int32.MaxValue
                    ? Int32.MaxValue
                    : (int)screenY);

            Rectangle viewport = host.GetItemsViewportRectangle();

            return new Rectangle(
                viewport.X,
                y,
                Math.Max(0, viewport.Width),
                host.FixedItemSize);
        }

        private LightweightRowSnapshot GetLightweightRowSnapshot(
            ItemsControl host,
            int index)
        {
            if (host.LightweightRowCache == null)
                host.LightweightRowCache = new Hashtable();

            LightweightRowSnapshot snapshot =
                host.LightweightRowCache[index]
                    as LightweightRowSnapshot;
            object item = host.ItemValues[index];

            if (snapshot != null &&
                snapshot.Generation == host.LightweightGeneration &&
                Object.ReferenceEquals(snapshot.Item, item))
            {
                return snapshot;
            }

            if (snapshot != null)
            {
                // Remove the stale strong cache reference before releasing
                // runtime-owned decoded images. If a custom IDisposable fails,
                // the stale snapshot is still no longer retained and a later
                // paint can retry with a clean snapshot.
                host.LightweightRowCache.Remove(index);
                ReleaseLightweightRowSnapshot(host, snapshot);
            }

            LightweightTemplatePlan plan = host.LightweightPlan;
            snapshot = new LightweightRowSnapshot(
                plan == null ? 0 : plan.NextValueSlotId,
                plan == null ? 0 : plan.NextNodeId,
                plan == null ? 0 : plan.NextLinkId);
            snapshot.Host = host;
            snapshot.Generation = host.LightweightGeneration;
            snapshot.Index = index;
            snapshot.Item = item;
            host.LightweightRowCache[index] = snapshot;
            return snapshot;
        }

        private void TrimLightweightRowCache(
            ItemsControl host,
            int visibleStart,
            int visibleEnd)
        {
            if (host.LightweightRowCache == null ||
                host.LightweightRowCache.Count == 0)
            {
                return;
            }

            int retainStart;
            int retainEnd;
            GetLightweightPreparedRange(
                host,
                visibleStart,
                visibleEnd,
                out retainStart,
                out retainEnd);

            if (host.LightweightCacheEvictionKeys == null)
            {
                host.LightweightCacheEvictionKeys = new ArrayList();
            }

            ArrayList remove = host.LightweightCacheEvictionKeys;
            remove.Clear();

            foreach (DictionaryEntry entry in host.LightweightRowCache)
            {
                int index = (int)entry.Key;

                if (index < retainStart || index > retainEnd)
                    remove.Add(entry.Key);
            }

            int i;
            Exception firstError = null;

            for (i = 0; i < remove.Count; i++)
            {
                object key = remove[i];
                LightweightRowSnapshot snapshot =
                    host.LightweightRowCache[key]
                        as LightweightRowSnapshot;
                host.LightweightRowCache.Remove(key);

                if (snapshot != null)
                {
                    try
                    {
                        ReleaseLightweightRowSnapshot(host, snapshot);
                    }
                    catch (Exception ex)
                    {
                        firstError = FirstItemsCommitError(
                            firstError,
                            ex);
                    }
                }
            }

            remove.Clear();

            if (firstError != null)
                throw firstError;
        }

        private object ResolveLightweightValue(
            ItemsControl host,
            LightweightValueSlot slot,
            LightweightRowSnapshot snapshot)
        {
            if (slot == null)
                return null;

            if (!slot.Dynamic)
                return slot.Literal;

            object cached = snapshot.Values[slot.Id];

            if (cached != null)
            {
                return Object.ReferenceEquals(
                        cached,
                        LightweightCachedNullValue)
                    ? null
                    : cached;
            }

            object previousTarget = _activeComponentEventTarget;
            Hashtable previousFunctions = _activeFunctionResultCache;
            ItemTemplateActiveContext previousContext =
                PushItemTemplateDeclarationContext(host);

            try
            {
                _activeComponentEventTarget = host.TemplateEventTarget;
                _activeFunctionResultCache = snapshot.FunctionResults;
                object value = EvaluateTemplateExpressionValue(
                    slot.Expression,
                    snapshot.Item);
                snapshot.Values[slot.Id] = value == null
                    ? LightweightCachedNullValue
                    : value;
                return value;
            }
            catch (WinFormsXamlLoadException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw CreateMarkupLoadException(
                    slot.Element,
                    slot.PropertyName,
                    ex);
            }
            finally
            {
                _activeFunctionResultCache = previousFunctions;
                _activeComponentEventTarget = previousTarget;
                RestoreItemTemplateDeclarationContext(previousContext);
            }
        }

        private string ResolveLightweightText(
            ItemsControl host,
            LightweightValueSlot slot,
            LightweightRowSnapshot snapshot,
            string fallback)
        {
            if (slot == null)
                return fallback;

            object cached = snapshot.TextValues[slot.Id];

            if (cached != null)
            {
                return Object.ReferenceEquals(
                        cached,
                        LightweightCachedNullValue)
                    ? null
                    : cached as string;
            }

            object value = ResolveLightweightValue(host, slot, snapshot);
            string resolved = value == null ||
                IsUnsetPresetValue(value)
                ? fallback
                : BindingValueToString(value);
            snapshot.TextValues[slot.Id] = resolved == null
                ? LightweightCachedNullValue
                : (object)resolved;
            return resolved;
        }

        private bool ResolveLightweightBoolean(
            ItemsControl host,
            LightweightValueSlot slot,
            LightweightRowSnapshot snapshot,
            bool fallback)
        {
            if (slot == null)
                return fallback;

            if (snapshot.ConvertedValues[slot.Id] != null)
                return (bool)snapshot.ConvertedValues[slot.Id];

            object value = ResolveLightweightValue(host, slot, snapshot);
            object converted;

            if (IsUnsetPresetValue(value))
            {
                snapshot.ConvertedValues[slot.Id] = fallback;
                return fallback;
            }

            if (!TryConvertObjectValue(value, typeof(bool), out converted))
            {
                throw LightweightMarkupError(
                    slot.Element,
                    slot.PropertyName,
                    "The lightweight value is not boolean-compatible.");
            }

            bool result = (bool)converted;
            snapshot.ConvertedValues[slot.Id] = result;
            return result;
        }

        private Color ResolveLightweightColor(
            ItemsControl host,
            LightweightValueSlot slot,
            LightweightRowSnapshot snapshot,
            Color fallback)
        {
            if (slot == null)
                return fallback;

            if (snapshot.ConvertedValues[slot.Id] != null)
                return (Color)snapshot.ConvertedValues[slot.Id];

            object value = ResolveLightweightValue(host, slot, snapshot);

            if (IsUnsetPresetValue(value))
            {
                snapshot.ConvertedValues[slot.Id] = fallback;
                return fallback;
            }

            if (value is Color)
            {
                Color colorValue = (Color)value;
                snapshot.ConvertedValues[slot.Id] = colorValue;
                return colorValue;
            }

            try
            {
                Color result = ParseColor(BindingValueToString(value));
                snapshot.ConvertedValues[slot.Id] = result;
                return result;
            }
            catch (Exception ex)
            {
                throw CreateMarkupLoadException(
                    slot.Element,
                    slot.PropertyName,
                    ex);
            }
        }

        private Brush GetLightweightBrush(
            ItemsControl host,
            Color color)
        {
            if (host.LightweightBrushCache == null)
                host.LightweightBrushCache = new Hashtable();

            int key = color.ToArgb();
            Brush brush = host.LightweightBrushCache[key] as Brush;

            if (brush != null)
                return brush;

            brush = new SolidBrush(color);
            host.LightweightBrushCache[key] = brush;
#if !WINFORMSXAML_PACKAGE
            host.LightweightBrushCreateCountForTest++;
#endif
            return brush;
        }

    }
}
