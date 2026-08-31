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
        internal void UpdateLightweightHotTarget(
            ItemsControl host,
            Point location)
        {
            LightweightHitTarget previous = host.LightweightHotTarget;

            if (previous != null && previous.Bounds.Contains(location))
                return;

            LightweightHitTarget next = HitTestLightweightItems(
                host,
                location);
            bool same = previous != null && next != null &&
                previous.Index == next.Index &&
                Object.ReferenceEquals(previous.Node, next.Node);

            if (same || (previous == null && next == null))
                return;

            host.LightweightHotTarget = next;
            host.Cursor = next != null &&
                next.Node.Kind == LightweightNodeKind.HyperlinkLabel
                    ? Cursors.Hand
                    : Cursors.Default;

            if (previous != null)
                host.Invalidate(GetLightweightRowBounds(host, previous.Index));
            if (next != null)
                host.Invalidate(GetLightweightRowBounds(host, next.Index));
        }

        internal void ActivateLightweightHitTarget(
            ItemsControl host,
            Point location)
        {
            LightweightHitTarget hit = HitTestLightweightItems(
                host,
                location);

            if (hit == null)
                return;

            LightweightRowSnapshot snapshot =
                GetLightweightRowSnapshot(host, hit.Index);

            if (!EnsureLightweightRowSnapshotPrepared(host, snapshot))
                return;

            bool enabled = ResolveLightweightBoolean(
                host,
                hit.Node.Enabled,
                snapshot,
                true);

            if (!enabled)
                return;

            if (hit.Node.Kind == LightweightNodeKind.CheckBox)
            {
                LightweightRowSnapshot editedSnapshot = snapshot;
                object editedItem = snapshot.Item;
                int editedGeneration = snapshot.Generation;
                WriteLightweightCheckBoxValue(
                    host,
                    hit.Node,
                    snapshot.Item);

                if (!host.LightweightActive ||
                    host.LightweightDisposed ||
                    host.LightweightGeneration != editedGeneration ||
                    host.LightweightRowCache == null ||
                    host.ItemValues == null ||
                    hit.Index < 0 || hit.Index >= host.ItemValues.Count ||
                    !Object.ReferenceEquals(
                        host.ItemValues[hit.Index],
                        editedItem))
                {
                    return;
                }

                LightweightRowSnapshot oldSnapshot =
                    host.LightweightRowCache[hit.Index]
                        as LightweightRowSnapshot;

                // PropertyBinding and INPC notifications may synchronously
                // rebuild this row during the write. Only perform the explicit
                // fallback when the observable path did not already do so.
                if (Object.ReferenceEquals(
                        oldSnapshot,
                        editedSnapshot))
                {
                    host.LightweightRowCache.Remove(hit.Index);
                    ReleaseLightweightRowSnapshot(host, oldSnapshot);
                }
                else
                {
                    // The synchronous observable callback already rebuilt or
                    // a reentrant transition now owns this row.
                    return;
                }

                if (!host.LightweightActive ||
                    host.LightweightDisposed ||
                    host.LightweightGeneration != editedGeneration ||
                    host.ItemValues == null ||
                    hit.Index < 0 || hit.Index >= host.ItemValues.Count ||
                    !Object.ReferenceEquals(
                        host.ItemValues[hit.Index],
                        editedItem))
                {
                    return;
                }

                LightweightRowSnapshot refreshedSnapshot =
                    GetLightweightRowSnapshot(host, hit.Index);

                if (EnsureLightweightRowSnapshotPrepared(
                        host,
                        refreshedSnapshot))
                {
                    host.Invalidate(
                        GetLightweightRowBounds(host, hit.Index));
                }
            }
            else if (hit.Node.Kind == LightweightNodeKind.HyperlinkLabel)
            {
                string destination = ResolveLightweightText(
                    host,
                    hit.Node.NavigateUri,
                    snapshot,
                    null);

                if (String.IsNullOrEmpty(destination) ||
                    destination.Trim().Length == 0)
                {
                    return;
                }

                ProcessStartInfo startInfo =
                    new ProcessStartInfo(destination);
                startInfo.UseShellExecute = true;
                Process process = Process.Start(startInfo);

                if (process != null)
                    process.Dispose();

                MarkLightweightLinkVisited(
                    host,
                    snapshot,
                    hit.Node);
                host.Invalidate(GetLightweightRowBounds(host, hit.Index));
            }
        }

        private LightweightHitTarget HitTestLightweightItems(
            ItemsControl host,
            Point location)
        {
            if (host == null || !host.LightweightActive ||
                host.LightweightPlan == null ||
                host.LightweightPlan.Root == null)
            {
                return null;
            }

            long stride = Math.Max(
                1L,
                (long)host.FixedItemSize + host.Spacing);
            int offset = Math.Max(0, -host.AutoScrollPosition.Y);
            long logical = (long)location.Y + offset - host.Padding.Top;

            if (logical < 0L)
                return null;

            int index = (int)Math.Min(
                Int32.MaxValue,
                logical / stride);

            if (index < 0 || index >= host.Count ||
                (logical % stride) >= host.FixedItemSize)
            {
                return null;
            }

            Rectangle row = GetLightweightRowBounds(host, index);

            if (!row.Contains(location))
                return null;

            return HitTestLightweightNode(
                host.LightweightPlan.Root,
                row,
                location,
                index);
        }

        private LightweightHitTarget HitTestLightweightNode(
            LightweightTemplateNode node,
            Rectangle allocation,
            Point location,
            int index)
        {
            Rectangle bounds = ApplyLightweightBox(node, allocation);

            if (!bounds.Contains(location))
                return null;

            if (node.Kind == LightweightNodeKind.CheckBox ||
                node.Kind == LightweightNodeKind.HyperlinkLabel)
            {
                LightweightHitTarget hit = new LightweightHitTarget();
                hit.Index = index;
                hit.Node = node;
                hit.Bounds = bounds;
                return hit;
            }

            if (node.Kind == LightweightNodeKind.Border)
            {
                if (node.Children.Count == 0)
                    return null;

                Rectangle inner = DeflateLightweightRectangle(
                    bounds,
                    AddPadding(node.BorderThickness, node.Padding));
                return HitTestLightweightNode(
                    node.Children[0] as LightweightTemplateNode,
                    inner,
                    location,
                    index);
            }

            if (node.Kind != LightweightNodeKind.StackPanel)
                return null;

            Rectangle stackBounds = DeflateLightweightRectangle(
                bounds,
                node.Padding);
            int count = node.Children.Count;
            int available = node.Orientation == Orientation.Horizontal
                ? stackBounds.Width
                : stackBounds.Height;
            available = Math.Max(
                0,
                available - (node.Spacing * Math.Max(0, count - 1)));
            int fixedExtent = 0;
            int flexible = 0;
            int i;

            for (i = 0; i < count; i++)
            {
                LightweightTemplateNode child =
                    node.Children[i] as LightweightTemplateNode;
                int explicitExtent = node.Orientation == Orientation.Horizontal
                    ? child.Width
                    : child.Height;
                int margins = node.Orientation == Orientation.Horizontal
                    ? child.Margin.Left + child.Margin.Right
                    : child.Margin.Top + child.Margin.Bottom;

                if (explicitExtent >= 0)
                    fixedExtent += explicitExtent + margins;
                else
                    flexible++;
            }

            int remainder = Math.Max(0, available - fixedExtent);
            int cursor = node.Orientation == Orientation.Horizontal
                ? stackBounds.Left
                : stackBounds.Top;
            int flexibleSeen = 0;

            for (i = 0; i < count; i++)
            {
                LightweightTemplateNode child =
                    node.Children[i] as LightweightTemplateNode;
                int explicitExtent = node.Orientation == Orientation.Horizontal
                    ? child.Width
                    : child.Height;
                int extent;

                if (explicitExtent >= 0)
                {
                    int margins = node.Orientation == Orientation.Horizontal
                        ? child.Margin.Left + child.Margin.Right
                        : child.Margin.Top + child.Margin.Bottom;
                    extent = explicitExtent + margins;
                }
                else
                {
                    flexibleSeen++;
                    extent = flexible == 0
                        ? 0
                        : (flexibleSeen == flexible
                            ? remainder -
                                ((remainder / flexible) * (flexible - 1))
                            : remainder / flexible);
                }

                Rectangle slot = node.Orientation == Orientation.Horizontal
                    ? new Rectangle(cursor, stackBounds.Top, extent, stackBounds.Height)
                    : new Rectangle(stackBounds.Left, cursor, stackBounds.Width, extent);
                LightweightHitTarget hit = HitTestLightweightNode(
                    child,
                    slot,
                    location,
                    index);

                if (hit != null)
                    return hit;

                cursor += extent + node.Spacing;
            }

            return null;
        }

        private void WriteLightweightCheckBoxValue(
            ItemsControl host,
            LightweightTemplateNode node,
            object item)
        {
            BindingExpressionPlan plan =
                GetLightweightBindingPlan(node.Checked);

            if (plan == null ||
                plan.Mode != BindingMode.TwoWay ||
                plan.HasComputedExpression)
            {
                throw LightweightMarkupError(
                    node.SourceElement,
                    "Checked",
                    "The lightweight CheckBox requires a direct-path " +
                    "Mode=TwoWay Binding.");
            }

            object previousTarget = _activeComponentEventTarget;
            ItemTemplateActiveContext previousContext =
                PushItemTemplateDeclarationContext(host);

            try
            {
                _activeComponentEventTarget = host.TemplateEventTarget;
                object source = ResolveBindingSource(
                    GetItemDataContext(item),
                    plan);
                BindingPathResult path = ResolveBindingPathResult(
                    source,
                    plan.Path);
                BindingPathDependency endpoint = path.TerminalDependency;
                object converted;
                bool versionConflict = false;

                if (!TryConvertObjectValue(
                        path.Value,
                        typeof(bool),
                        out converted) ||
                    endpoint == null ||
                    !TrySetObservableSourceValue(
                        endpoint,
                        !(bool)converted,
                        endpoint.Version,
                        out versionConflict))
                {
                    throw new InvalidOperationException(
                        versionConflict
                            ? "The two-way CheckBox source changed concurrently; " +
                              "reload and retry the edit."
                            : "The two-way CheckBox binding must end in a writable " +
                              "PropertyBinding<T> or notifying CLR property.");
                }
            }
            catch (WinFormsXamlLoadException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw CreateMarkupLoadException(
                    node.SourceElement,
                    node.Checked.PropertyName,
                    ex);
            }
            finally
            {
                _activeComponentEventTarget = previousTarget;
                RestoreItemTemplateDeclarationContext(previousContext);
            }
        }

        private bool IsLightweightLinkVisited(
            ItemsControl host,
            LightweightRowSnapshot snapshot,
            LightweightTemplateNode node)
        {
            return host.LightweightVisitedLinks != null &&
                host.LightweightVisitedLinks.ContainsKey(
                    GetLightweightLinkKey(snapshot, node));
        }

        private void MarkLightweightLinkVisited(
            ItemsControl host,
            LightweightRowSnapshot snapshot,
            LightweightTemplateNode node)
        {
            if (host.LightweightVisitedLinks == null)
                host.LightweightVisitedLinks = new Hashtable();

            if (host.LightweightVisitedLinkOrder == null)
                host.LightweightVisitedLinkOrder = new ArrayList();

            LightweightVisitedLinkKey key =
                GetLightweightLinkKey(snapshot, node);

            if (host.LightweightVisitedLinks.ContainsKey(key))
                return;

            host.LightweightVisitedLinks.Add(key, true);
            host.LightweightVisitedLinkOrder.Add(key);

            while (host.LightweightVisitedLinkOrder.Count >
                LightweightVisitedLinkLimit)
            {
                object oldest = host.LightweightVisitedLinkOrder[0];
                host.LightweightVisitedLinkOrder.RemoveAt(0);
                host.LightweightVisitedLinks.Remove(oldest);
            }
        }

        private LightweightVisitedLinkKey GetLightweightLinkKey(
            LightweightRowSnapshot snapshot,
            LightweightTemplateNode node)
        {
            if (snapshot == null || node == null)
                return null;

            LightweightVisitedLinkKey key =
                snapshot.LinkKeys[node.LinkId]
                    as LightweightVisitedLinkKey;

            if (key == null)
            {
                if (snapshot.StableItemKey == null)
                {
                    snapshot.StableItemKey = GetStableItemKey(
                        snapshot.Host,
                        snapshot.Item,
                        snapshot.Index);
                }

                key = new LightweightVisitedLinkKey(
                    snapshot.StableItemKey,
                    node.LinkId,
                    node.NavigateUri == null
                        ? null
                        : snapshot.TextValues[node.NavigateUri.Id] as string);
                snapshot.LinkKeys[node.LinkId] = key;
            }

            return key;
        }
    }
}
