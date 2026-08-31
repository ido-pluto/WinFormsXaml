using System;
using System.Collections;
using System.Reflection;
using System.Threading;
using System.Xml;
using System.Windows.Forms;

namespace WinFormsXaml
{
    public sealed partial class XamlRuntime
    {
        private ArrayList GetComponentInvocationContent(
            XmlElement invocation,
            RegisteredComponent component)
        {
            ArrayList children = new ArrayList();
            XmlNode node = invocation.FirstChild;

            while (node != null)
            {
                XmlElement child = node as XmlElement;

                if (child != null)
                    children.Add(child);

                if ((node.NodeType == XmlNodeType.Text ||
                     node.NodeType == XmlNodeType.CDATA) &&
                    node.Value != null &&
                    node.Value.Trim().Length != 0)
                {
                    throw CreateMarkupLoadException(
                        invocation,
                        null,
                        new InvalidOperationException(
                            "Registered XML component <" +
                            component.Name +
                            "> accepts visual child elements, not text content."));
                }

                node = node.NextSibling;
            }

            return children;
        }

        private ArrayList ApplyComponentChildrenProjection(
            XmlDocument templateDocument,
            ArrayList invocationChildren,
            RegisteredComponent component,
            ComponentChildrenHost childrenHost,
            object parentDataContext,
            object callerEventTarget,
            string callerMarkupSource,
            string callerElementPath,
            Assembly callerMarkupAssembly,
            int callerComponentBuildDepth)
        {
            XmlElement slot =
                FindComponentChildrenSlot(
                    templateDocument.DocumentElement);

            if (slot == null || slot.ParentNode == null)
            {
                throw new InvalidOperationException(
                    "Registered XML component <" +
                    component.Name +
                    "> lost its validated <Children> slot.");
            }

            XmlNode parent = slot.ParentNode;
            ArrayList projectedRoots = new ArrayList();
            int i;

            try
            {
                for (i = 0; i < invocationChildren.Count; i++)
                {
                    XmlElement invocationChild =
                        invocationChildren[i] as XmlElement;
                    XmlElement projectedRoot =
                        CloneComponentContentWithLocations(
                            templateDocument,
                            invocationChild);
                    ComponentContentProjection inherited =
                        GetComponentContentProjection(invocationChild);
                    ComponentContentProjection projection =
                        new ComponentContentProjection();

                    if (inherited == null)
                    {
                        projection.DataContext = parentDataContext;
                        projection.EventTarget = callerEventTarget;
                        projection.MarkupSource =
                            String.IsNullOrEmpty(callerMarkupSource)
                                ? _markupSource
                                : callerMarkupSource;
                        projection.ElementPathPrefix = callerElementPath;
                        projection.MarkupAssembly = callerMarkupAssembly;
                        projection.ComponentBuildDepth =
                            callerComponentBuildDepth;
                    }
                    else
                    {
                        projection.DataContext = inherited.DataContext;
                        projection.EventTarget = inherited.EventTarget;
                        projection.MarkupSource = inherited.MarkupSource;
                        projection.ElementPathPrefix =
                            inherited.ElementPathPrefix;
                        projection.MarkupAssembly =
                            inherited.MarkupAssembly;
                        projection.ComponentBuildDepth =
                            inherited.ComponentBuildDepth;
                    }

                    projection.ChildrenHost = childrenHost;
                    parent.InsertBefore(projectedRoot, slot);
                    projectedRoots.Add(projectedRoot);
                    RegisterComponentContentProjection(
                        projectedRoot,
                        projection);
                }

                RegisterComponentChildrenSlot(slot, childrenHost);
                return projectedRoots;
            }
            catch
            {
                for (i = 0; i < projectedRoots.Count; i++)
                {
                    UnregisterComponentContentProjection(
                        projectedRoots[i] as XmlElement);
                }

                UnregisterComponentChildrenSlot(childrenHost);
                throw;
            }
        }

        private static XmlElement CloneComponentContentWithLocations(
            XmlDocument templateDocument,
            XmlElement invocationContent)
        {
            XmlElement serializableContent =
                invocationContent.CloneNode(true) as XmlElement;

            CopyProjectionNamespaceDeclarations(
                invocationContent,
                serializableContent);

            // Importing into the component template already creates nodes owned
            // by that document. Persist parser coordinates on the clone first so
            // this path does not serialize and parse the projected subtree merely
            // to transfer those coordinates.
            MarkupXmlDocument.PersistElementLocations(
                serializableContent);

            return templateDocument.ImportNode(
                serializableContent,
                true) as XmlElement;
        }

        private static XmlElement FindComponentChildrenSlot(
            XmlElement element)
        {
            if (element == null)
                return null;

            if (EqualsIgnoreCase(
                    element.LocalName,
                    "Children"))
            {
                return element;
            }

            XmlNode node = element.FirstChild;

            while (node != null)
            {
                XmlElement child = node as XmlElement;
                XmlElement found =
                    FindComponentChildrenSlot(child);

                if (found != null)
                    return found;

                node = node.NextSibling;
            }

            return null;
        }

        private static void CopyProjectionNamespaceDeclarations(
            XmlElement source,
            XmlElement projectedRoot)
        {
            if (source == null || projectedRoot == null)
                return;

            XmlElement ancestor = source.ParentNode as XmlElement;

            while (ancestor != null)
            {
                int i;

                for (i = 0; i < ancestor.Attributes.Count; i++)
                {
                    XmlAttribute attribute = ancestor.Attributes[i];
                    bool namespaceDeclaration =
                        String.Equals(
                            attribute.Name,
                            "xmlns",
                            StringComparison.OrdinalIgnoreCase) ||
                        String.Equals(
                            attribute.Prefix,
                            "xmlns",
                            StringComparison.OrdinalIgnoreCase);

                    if (namespaceDeclaration &&
                        !projectedRoot.HasAttribute(attribute.Name))
                    {
                        projectedRoot.SetAttribute(
                            attribute.Name,
                            attribute.Value);
                    }
                }

                ancestor = ancestor.ParentNode as XmlElement;
            }
        }

        private ComponentContentProjection GetComponentContentProjection(
            XmlElement element)
        {
            if (_componentContentProjections == null || element == null)
                return null;

            return _componentContentProjections[element] as
                ComponentContentProjection;
        }

        private void RegisterComponentContentProjection(
            XmlElement element,
            ComponentContentProjection projection)
        {
            if (_componentContentProjections == null)
            {
                _componentContentProjections =
                    new Hashtable(_observableReferenceComparer);
            }

            _componentContentProjections.Add(
                element,
                projection);
        }

        private void UnregisterComponentContentProjection(
            XmlElement element)
        {
            if (_componentContentProjections == null || element == null)
                return;

            _componentContentProjections.Remove(element);

            if (_componentContentProjections.Count == 0)
                _componentContentProjections = null;
        }

        private void RegisterComponentChildrenSlot(
            XmlElement slot,
            ComponentChildrenHost host)
        {
            if (slot == null || host == null)
                throw new ArgumentNullException(slot == null ? "slot" : "host");

            if (_componentChildrenSlotMarkers == null)
            {
                _componentChildrenSlotMarkers =
                    new Hashtable(_observableReferenceComparer);
            }

            host.SlotElement = slot;
            _componentChildrenSlotMarkers.Add(slot, host);
        }

        private ComponentChildrenHost GetComponentChildrenSlotHost(
            XmlElement slot)
        {
            if (_componentChildrenSlotMarkers == null || slot == null)
                return null;

            return _componentChildrenSlotMarkers[slot] as
                ComponentChildrenHost;
        }

        private void UnregisterComponentChildrenSlot(
            ComponentChildrenHost host)
        {
            if (host == null || host.SlotElement == null)
                return;

            if (_componentChildrenSlotMarkers != null)
            {
                _componentChildrenSlotMarkers.Remove(host.SlotElement);

                if (_componentChildrenSlotMarkers.Count == 0)
                    _componentChildrenSlotMarkers = null;
            }

            host.SlotElement = null;
        }

        private void CaptureComponentChildrenSlot(
            object parent,
            ComponentChildrenMarker marker)
        {
            ComponentChildrenHost host = marker == null
                ? null
                : marker.Host;

            if (host == null || host.Retired)
            {
                throw new InvalidOperationException(
                    "The component <Children> slot is no longer active.");
            }

            if (host.Attached)
            {
                throw new InvalidOperationException(
                    "A component <Children> slot was attached more than once.");
            }

            Control parentControl = parent as Control;

            if (parentControl == null)
            {
                throw new InvalidOperationException(
                    "A component <Children> slot must be an ordinary child of " +
                    "a Windows Forms Control.");
            }

            Control[] projected =
                (Control[])host.ProjectedChildren.ToArray(typeof(Control));
            int i;

            for (i = 0; i < projected.Length; i++)
            {
                if (!Object.ReferenceEquals(
                        projected[i].Parent,
                        parentControl))
                {
                    throw new InvalidOperationException(
                        "Projected child " +
                        projected[i].GetType().FullName +
                        " was not attached directly at its component <Children> slot.");
                }
            }

            host.Parent = parentControl;
            host.SlotIndex = Math.Max(
                0,
                parentControl.Controls.Count - projected.Length);
            host.Attached = true;
            host.State.Children.Attach(host, projected);
        }

        private void ReplaceComponentChildren(
            ComponentChildrenHost host,
            ChildrenBind owner,
            Control[] replacement)
        {
            Control[] previous = ValidateComponentChildrenMutation(
                host,
                owner,
                replacement);
            Control parent = host.Parent;

            if (ControlSequencesMatch(previous, replacement) &&
                ComponentSlotMatches(
                    parent,
                    host.SlotIndex,
                    replacement))
            {
                return;
            }

            Control[] originalOrder =
                SnapshotControlOrder(parent);
            ArrayList added = new ArrayList();
            ArrayList newlyTracked = new ArrayList();
            ArrayList removed = new ArrayList();
            int i;
            Exception mutationFailure = null;
            Exception resumeFailure = null;

            host.Mutating = true;

            try
            {
                parent.SuspendLayout();

                for (i = 0; i < previous.Length; i++)
                {
                    if (ContainsControl(replacement, previous[i]))
                        continue;

                    parent.Controls.Remove(previous[i]);
                    UnregisterLogicalChild(parent, previous[i]);
                    removed.Add(previous[i]);
                }

                for (i = 0; i < replacement.Length; i++)
                {
                    Control child = replacement[i];

                    if (ContainsControl(previous, child))
                        continue;

                    ElementInfo ignored;
                    bool tracked = _elementInfos.TryGetValue(child, out ignored);

                    if (!tracked)
                    {
                        GetInfo(child);
                        newlyTracked.Add(child);
                    }

                    added.Add(child);
                    RegisterLogicalChild(parent, child);
                    parent.Controls.Add(child);
                }

                SetComponentSlotOrder(
                    parent,
                    host.SlotIndex,
                    replacement);
            }
            catch (Exception ex)
            {
                mutationFailure = ex;
                RollbackComponentChildrenReplace(
                    parent,
                    previous,
                    originalOrder,
                    added,
                    newlyTracked);
            }
            finally
            {
                host.Mutating = false;

                try
                {
                    parent.ResumeLayout(false);
                }
                catch (Exception ex)
                {
                    resumeFailure = ex;
                }
            }

            if (mutationFailure != null)
                throw mutationFailure;

            Exception notificationFailure = null;
            Exception releaseFailure = null;
            Exception layoutFailure = null;

            try
            {
                PublishComponentChildren(host, owner, replacement);
            }
            catch (Exception ex)
            {
                // The tree and published snapshot already committed. Finish
                // releasing removed ownership before reporting a listener.
                notificationFailure = ex;
            }

            for (i = 0; i < removed.Count; i++)
            {
                try
                {
                    ReleaseCreatedElement(removed[i]);
                }
                catch (Exception ex)
                {
                    if (releaseFailure == null)
                        releaseFailure = ex;
                }
            }

            try
            {
                parent.PerformLayout();
                parent.Invalidate(true);
            }
            catch (Exception ex)
            {
                layoutFailure = ex;
            }

            if (notificationFailure != null)
                throw notificationFailure;

            if (resumeFailure != null)
                throw resumeFailure;

            if (releaseFailure != null)
            {
                throw new InvalidOperationException(
                    "Projected children were replaced, but one removed Control " +
                    "could not be released: " +
                    releaseFailure.Message,
                    releaseFailure);
            }

            if (layoutFailure != null)
            {
                throw new InvalidOperationException(
                    "Projected children were replaced, but their parent " +
                    "could not complete layout: " +
                    layoutFailure.Message,
                    layoutFailure);
            }
        }

        private Control WrapComponentChildren(
            ComponentChildrenHost host,
            ChildrenBind owner,
            Control wrapper)
        {
            if (wrapper == null)
                throw new ArgumentNullException("wrapper");

            Control[] previous = ValidateComponentChildrenMutation(
                host,
                owner,
                new Control[] { wrapper });

            if (wrapper.Parent != null || wrapper.Controls.Count != 0)
            {
                throw new InvalidOperationException(
                    "A projected-children wrapper must be an unparented, empty Control.");
            }

            Control parent = host.Parent;

            if (ContainsControl(previous, wrapper))
            {
                throw new InvalidOperationException(
                    "A projected child cannot wrap itself. Supply a new, " +
                    "unparented, empty wrapper Control.");
            }

            Control[] originalOrder = SnapshotControlOrder(parent);
            ElementInfo ignored;
            bool wrapperWasTracked =
                _elementInfos.TryGetValue(wrapper, out ignored);
            int i;
            Exception mutationFailure = null;
            Exception resumeFailure = null;

            host.Mutating = true;

            try
            {
                parent.SuspendLayout();
                wrapper.SuspendLayout();

                if (!wrapperWasTracked)
                    GetInfo(wrapper);

                for (i = 0; i < previous.Length; i++)
                {
                    parent.Controls.Remove(previous[i]);
                    UnregisterLogicalChild(parent, previous[i]);
                }

                RegisterLogicalChild(parent, wrapper);
                parent.Controls.Add(wrapper);
                SetComponentSlotOrder(
                    parent,
                    host.SlotIndex,
                    new Control[] { wrapper });

                for (i = 0; i < previous.Length; i++)
                {
                    RegisterLogicalChild(wrapper, previous[i]);
                    wrapper.Controls.Add(previous[i]);
                }
            }
            catch (Exception ex)
            {
                mutationFailure = ex;
                RollbackComponentChildrenWrap(
                    parent,
                    wrapper,
                    previous,
                    originalOrder,
                    wrapperWasTracked);
            }
            finally
            {
                host.Mutating = false;

                try
                {
                    wrapper.ResumeLayout(false);
                }
                catch (Exception ex)
                {
                    resumeFailure = ex;
                }

                try
                {
                    parent.ResumeLayout(false);
                }
                catch (Exception ex)
                {
                    if (resumeFailure == null)
                        resumeFailure = ex;
                }
            }

            if (mutationFailure != null)
                throw mutationFailure;

            Exception notificationFailure = null;
            Exception layoutFailure = null;

            try
            {
                PublishComponentChildren(
                    host,
                    owner,
                    new Control[] { wrapper });
            }
            catch (Exception ex)
            {
                notificationFailure = ex;
            }

            try
            {
                wrapper.PerformLayout();
                parent.PerformLayout();
                parent.Invalidate(true);
            }
            catch (Exception ex)
            {
                layoutFailure = ex;
            }

            if (notificationFailure != null)
                throw notificationFailure;

            if (resumeFailure != null)
                throw resumeFailure;

            if (layoutFailure != null)
            {
                throw new InvalidOperationException(
                    "Projected children were wrapped, but their parent " +
                    "could not complete layout: " +
                    layoutFailure.Message,
                    layoutFailure);
            }

            return wrapper;
        }

        private Control[] ValidateComponentChildrenMutation(
            ComponentChildrenHost host,
            ChildrenBind owner,
            Control[] replacement)
        {
            if (host == null ||
                host.Retired ||
                !host.Attached ||
                host.Parent == null ||
                host.Parent.IsDisposed)
            {
                throw new ObjectDisposedException(
                    typeof(ChildrenBind).FullName,
                    "The component children slot is not attached.");
            }

            if (!Object.ReferenceEquals(host.State.Children, owner))
            {
                throw new InvalidOperationException(
                    "The ChildrenBind does not own this component slot.");
            }

            if (host.Mutating)
            {
                throw new InvalidOperationException(
                    "Projected children cannot be changed reentrantly.");
            }

            if (Thread.CurrentThread.ManagedThreadId !=
                _observableOwnerThreadId)
            {
                throw new InvalidOperationException(
                    "Projected children must be changed on the XAML runtime's " +
                    "owner UI thread.");
            }

            Control[] current =
                (Control[])host.ProjectedChildren.ToArray(typeof(Control));
            int i;

            for (i = 0; i < replacement.Length; i++)
            {
                Control child = replacement[i];

                if (child == null || child.IsDisposed)
                {
                    throw new ArgumentException(
                        "Projected replacement controls must be live, non-null Controls.",
                        "replacement");
                }

                if (Object.ReferenceEquals(child, host.Parent) ||
                    child.Contains(host.Parent))
                {
                    throw new InvalidOperationException(
                        "A projected child cannot contain its component slot parent.");
                }

                if (child.Parent != null &&
                    (!Object.ReferenceEquals(child.Parent, host.Parent) ||
                     !ContainsControl(current, child)))
                {
                    throw new InvalidOperationException(
                        "Projected replacement Control " +
                        child.GetType().FullName +
                        " already belongs to another parent or to a non-slot " +
                        "part of the component template.");
                }
            }

            return current;
        }

        private void PublishComponentChildren(
            ComponentChildrenHost host,
            ChildrenBind owner,
            Control[] controls)
        {
            host.ProjectedChildren.Clear();
            host.ProjectedChildren.AddRange(controls);
            owner.Publish(host, controls);
        }

        private Exception RollbackComponentChildrenReplace(
            Control parent,
            Control[] previous,
            Control[] originalOrder,
            ArrayList added,
            ArrayList newlyTracked)
        {
            Exception firstError = null;
            int i;

            for (i = 0; i < added.Count; i++)
            {
                Control child = added[i] as Control;

                if (child != null && Object.ReferenceEquals(child.Parent, parent))
                {
                    try
                    {
                        parent.Controls.Remove(child);
                    }
                    catch (Exception ex)
                    {
                        if (firstError == null)
                            firstError = ex;
                    }
                }

                try
                {
                    UnregisterLogicalChild(parent, child);
                }
                catch (Exception ex)
                {
                    if (firstError == null)
                        firstError = ex;
                }
            }

            for (i = 0; i < previous.Length; i++)
            {
                if (!Object.ReferenceEquals(previous[i].Parent, parent))
                {
                    try
                    {
                        RegisterLogicalChild(parent, previous[i]);
                    }
                    catch (Exception ex)
                    {
                        if (firstError == null)
                            firstError = ex;
                    }

                    try
                    {
                        parent.Controls.Add(previous[i]);
                    }
                    catch (Exception ex)
                    {
                        if (firstError == null)
                            firstError = ex;
                    }
                }
            }

            try
            {
                RestoreControlOrder(parent, originalOrder);
            }
            catch (Exception ex)
            {
                if (firstError == null)
                    firstError = ex;
            }

            for (i = 0; i < newlyTracked.Count; i++)
                _elementInfos.Remove(newlyTracked[i]);

            return firstError;
        }

        private Exception RollbackComponentChildrenWrap(
            Control parent,
            Control wrapper,
            Control[] previous,
            Control[] originalOrder,
            bool wrapperWasTracked)
        {
            Exception firstError = null;
            int i;

            for (i = 0; i < previous.Length; i++)
            {
                if (Object.ReferenceEquals(previous[i].Parent, wrapper))
                {
                    try
                    {
                        wrapper.Controls.Remove(previous[i]);
                    }
                    catch (Exception ex)
                    {
                        if (firstError == null)
                            firstError = ex;
                    }
                }

                try
                {
                    UnregisterLogicalChild(wrapper, previous[i]);
                }
                catch (Exception ex)
                {
                    if (firstError == null)
                        firstError = ex;
                }
            }

            if (Object.ReferenceEquals(wrapper.Parent, parent))
            {
                try
                {
                    parent.Controls.Remove(wrapper);
                }
                catch (Exception ex)
                {
                    if (firstError == null)
                        firstError = ex;
                }
            }

            try
            {
                UnregisterLogicalChild(parent, wrapper);
            }
            catch (Exception ex)
            {
                if (firstError == null)
                    firstError = ex;
            }

            for (i = 0; i < previous.Length; i++)
            {
                try
                {
                    RegisterLogicalChild(parent, previous[i]);
                }
                catch (Exception ex)
                {
                    if (firstError == null)
                        firstError = ex;
                }

                if (!Object.ReferenceEquals(previous[i].Parent, parent))
                {
                    try
                    {
                        parent.Controls.Add(previous[i]);
                    }
                    catch (Exception ex)
                    {
                        if (firstError == null)
                            firstError = ex;
                    }
                }
            }

            try
            {
                RestoreControlOrder(parent, originalOrder);
            }
            catch (Exception ex)
            {
                if (firstError == null)
                    firstError = ex;
            }

            if (!wrapperWasTracked)
                _elementInfos.Remove(wrapper);

            return firstError;
        }

        private static Control[] SnapshotControlOrder(Control parent)
        {
            Control[] controls = new Control[parent.Controls.Count];
            parent.Controls.CopyTo(controls, 0);
            return controls;
        }

        private static void RestoreControlOrder(
            Control parent,
            Control[] controls)
        {
            int i;

            for (i = 0; i < controls.Length; i++)
            {
                if (Object.ReferenceEquals(controls[i].Parent, parent))
                    parent.Controls.SetChildIndex(controls[i], i);
            }
        }

        private static void SetComponentSlotOrder(
            Control parent,
            int slotIndex,
            Control[] controls)
        {
            int i;

            for (i = controls.Length - 1; i >= 0; i--)
            {
                int index = Math.Min(
                    Math.Max(0, slotIndex),
                    parent.Controls.Count - 1);
                parent.Controls.SetChildIndex(controls[i], index);
            }
        }

        private static bool ContainsControl(
            Control[] controls,
            Control candidate)
        {
            int i;

            for (i = 0; i < controls.Length; i++)
            {
                if (Object.ReferenceEquals(controls[i], candidate))
                    return true;
            }

            return false;
        }

        private static bool ControlSequencesMatch(
            Control[] left,
            Control[] right)
        {
            if (left.Length != right.Length)
                return false;

            int i;

            for (i = 0; i < left.Length; i++)
            {
                if (!Object.ReferenceEquals(left[i], right[i]))
                    return false;
            }

            return true;
        }

        private static bool ComponentSlotMatches(
            Control parent,
            int slotIndex,
            Control[] controls)
        {
            if (controls.Length == 0)
                return true;

            if (slotIndex < 0 ||
                slotIndex + controls.Length > parent.Controls.Count)
            {
                return false;
            }

            int i;

            for (i = 0; i < controls.Length; i++)
            {
                if (!Object.ReferenceEquals(
                        parent.Controls[slotIndex + i],
                        controls[i]))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
