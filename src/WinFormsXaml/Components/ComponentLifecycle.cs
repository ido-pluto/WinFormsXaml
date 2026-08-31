using System;
using System.Collections;
using System.Windows.Forms;

namespace WinFormsXaml
{
    public sealed partial class XamlRuntime
    {
        private sealed class ComponentChildrenDisposalSnapshot
        {
            public ComponentChildrenHost Host;
            public ChildrenBind Owner;
            public Control[] Remaining;
        }

        private void HookComponentInstanceDisposal(
            ComponentInstanceState state)
        {
            Control root = state == null
                ? null
                : state.Root as Control;

            if (root == null || state.RootDisposedHandler != null)
                return;

            EventHandler handler = delegate(object sender, EventArgs e)
            {
                ReleaseDisposedComponentRoot(state);
            };

            state.RootDisposedHandler = handler;
            root.Disposed += handler;
        }

        private void ReleaseDisposedComponentRoot(
            ComponentInstanceState state)
        {
            object root = state == null
                ? null
                : state.Root;

            if (root == null)
            {
                ReleaseComponentInstanceState(state);
                return;
            }

            ArrayList projectedChildren =
                CaptureDisposedComponentChildren(root);
            Exception firstError = null;

            try
            {
                RemoveNamesForElementTree(root);
            }
            catch (Exception ex)
            {
                firstError = ex;
            }

            try
            {
                // The native Control is already inside Dispose. Release only
                // runtime ownership for its complete logical/native subtree;
                // ReleaseElementObjectTree also releases this component state.
                ReleaseElementObjectTree(
                    root,
                    new Hashtable(_runtimeObjectReferenceComparer));
            }
            catch (Exception ex)
            {
                if (firstError == null)
                    firstError = ex;
            }

            try
            {
                RemoveDisposedComponentLogicalOwnerReferences(root);
            }
            catch (Exception ex)
            {
                if (firstError == null)
                    firstError = ex;
            }

            int i;

            for (i = 0;
                 projectedChildren != null && i < projectedChildren.Count;
                 i++)
            {
                ComponentChildrenDisposalSnapshot snapshot =
                    projectedChildren[i] as
                        ComponentChildrenDisposalSnapshot;
                ComponentChildrenHost host = snapshot == null
                    ? null
                    : snapshot.Host;

                if (host == null ||
                    host.Retired ||
                    !host.Attached ||
                    host.Mutating ||
                    host.State == null ||
                    !Object.ReferenceEquals(
                        host.State.Children,
                        snapshot.Owner) ||
                    !ContainsComponentReference(host.ProjectedChildren, root))
                {
                    continue;
                }

                try
                {
                    PublishComponentChildren(
                        host,
                        snapshot.Owner,
                        snapshot.Remaining);
                }
                catch (Exception ex)
                {
                    if (firstError == null)
                        firstError = ex;
                }
            }

            if (firstError != null)
                throw firstError;
        }

        private ArrayList CaptureDisposedComponentChildren(object root)
        {
            Control disposedControl = root as Control;

            if (disposedControl == null || _componentInstances == null)
                return null;

            ArrayList snapshots = null;
            int i;

            for (i = 0; i < _componentInstances.Count; i++)
            {
                ComponentInstanceState candidate =
                    _componentInstances[i] as ComponentInstanceState;
                ComponentChildrenHost host = candidate == null
                    ? null
                    : candidate.ChildrenHost;

                if (host == null ||
                    host.Retired ||
                    !host.Attached ||
                    host.Mutating ||
                    candidate.Children == null ||
                    !ContainsComponentReference(
                        host.ProjectedChildren,
                        disposedControl))
                {
                    continue;
                }

                Control[] remaining =
                    new Control[host.ProjectedChildren.Count - 1];
                int sourceIndex;
                int targetIndex = 0;

                for (sourceIndex = 0;
                     sourceIndex < host.ProjectedChildren.Count;
                     sourceIndex++)
                {
                    Control child =
                        host.ProjectedChildren[sourceIndex] as Control;

                    if (!Object.ReferenceEquals(child, disposedControl))
                    {
                        remaining[targetIndex] = child;
                        targetIndex++;
                    }
                }

                ComponentChildrenDisposalSnapshot snapshot =
                    new ComponentChildrenDisposalSnapshot();
                snapshot.Host = host;
                snapshot.Owner = candidate.Children;
                snapshot.Remaining = remaining;

                if (snapshots == null)
                    snapshots = new ArrayList();

                snapshots.Add(snapshot);
            }

            return snapshots;
        }

        private void RemoveDisposedComponentLogicalOwnerReferences(
            object root)
        {
            if (root == null || _elementInfos == null)
                return;

            ArrayList parents = null;

            foreach (System.Collections.Generic.KeyValuePair<object, ElementInfo>
                entry in _elementInfos)
            {
                ElementInfo info = entry.Value;

                if (info == null ||
                    info.LogicalChildren == null ||
                    !ContainsComponentReference(info.LogicalChildren, root))
                {
                    continue;
                }

                if (parents == null)
                    parents = new ArrayList();

                parents.Add(entry.Key);
            }

            int i;

            for (i = 0; parents != null && i < parents.Count; i++)
                UnregisterLogicalChild(parents[i], root);
        }

        private static bool ContainsComponentReference(
            ArrayList values,
            object candidate)
        {
            int i;

            for (i = 0; values != null && i < values.Count; i++)
            {
                if (Object.ReferenceEquals(values[i], candidate))
                    return true;
            }

            return false;
        }

        private void TrackComponentInstanceState(
            ComponentInstanceState state)
        {
            if (state == null || state.Tracked)
                return;

            if (_componentInstances == null)
                _componentInstances = new ArrayList();

            state.InstanceIndex = _componentInstances.Count;
            state.Tracked = true;
            _componentInstances.Add(state);

            if (state.Root == null)
                return;

            if (_componentInstancesByRoot == null)
            {
                _componentInstancesByRoot =
                    new Hashtable(_observableReferenceComparer);
            }

            _componentInstancesByRoot[state.Root] = state;
        }

        private void UntrackComponentInstanceState(
            ComponentInstanceState state)
        {
            if (state == null || !state.Tracked)
                return;

            if (_componentInstancesByRoot != null && state.Root != null &&
                Object.ReferenceEquals(
                    _componentInstancesByRoot[state.Root],
                    state))
            {
                _componentInstancesByRoot.Remove(state.Root);
            }

            int index = state.InstanceIndex;

            if (_componentInstances != null &&
                (index < 0 || index >= _componentInstances.Count ||
                 !Object.ReferenceEquals(_componentInstances[index], state)))
            {
                index = _componentInstances.IndexOf(state);
            }

            if (_componentInstances != null && index >= 0)
                RemoveComponentInstanceAt(index);

            state.Tracked = false;
            state.InstanceIndex = -1;
        }

        private void RemoveComponentInstanceAt(int index)
        {
            if (_componentInstances == null ||
                index < 0 ||
                index >= _componentInstances.Count)
            {
                return;
            }

            int lastIndex = _componentInstances.Count - 1;

            if (index != lastIndex)
            {
                ComponentInstanceState moved =
                    _componentInstances[lastIndex] as
                        ComponentInstanceState;
                _componentInstances[index] = moved;

                if (moved != null)
                    moved.InstanceIndex = index;
            }

            _componentInstances.RemoveAt(lastIndex);
        }

        private static void UnhookComponentInstanceDisposal(
            ComponentInstanceState state)
        {
            if (state == null || state.RootDisposedHandler == null)
                return;

            EventHandler handler = state.RootDisposedHandler;
            state.RootDisposedHandler = null;
            Control root = state.Root as Control;

            if (root == null)
                return;

            try
            {
                root.Disposed -= handler;
            }
            catch
            {
                // Release the state even if a custom Control has a broken
                // event accessor during deterministic cleanup.
            }
        }

        private void ReleaseComponentInstanceState(
            ComponentInstanceState state)
        {
            if (state == null || state.Releasing)
                return;

            state.Releasing = true;

            try
            {
                UnhookComponentInstanceDisposal(state);
                DetachComponentObservableBindings(state);

                if (state.ChildrenHost != null)
                {
                    UnregisterComponentChildrenSlot(state.ChildrenHost);
                    state.ChildrenHost.Retired = true;
                    state.ChildrenHost.Attached = false;
                    state.ChildrenHost.Parent = null;
                }

                if (state.Children != null &&
                    state.ChildrenHost != null)
                {
                    state.Children.Retire(state.ChildrenHost);
                }

                if (!state.CodeBehindDisposed)
                {
                    IDisposable disposable =
                        state.CodeBehind as IDisposable;

                    if (disposable != null)
                        disposable.Dispose();

                    state.CodeBehindDisposed = true;
                }

                UntrackComponentInstanceState(state);

                state.ParentDataContext = null;
                state.ParentEventTarget = null;
                state.Root = null;

                if (state.Values != null)
                {
                    state.Values.CodeBehind = null;
                    state.Values.Clear();
                }

                if (state.Properties != null)
                {
                    int i;

                    for (i = 0; i < state.Properties.Count; i++)
                    {
                        ComponentPropertyValue property =
                            state.Properties[i] as ComponentPropertyValue;

                        if (property != null)
                        {
                            property.CodeBehind = null;
                            property.CodeMember = null;
                            property.OwnerState = null;
                        }
                    }

                    state.Properties.Clear();
                }

                if (state.ChildrenHost != null)
                {
                    state.ChildrenHost.ProjectedChildren.Clear();
                    state.ChildrenHost.Runtime = null;
                    state.ChildrenHost.State = null;
                }

                state.CodeBehind = null;
                state.Children = null;
                state.ChildrenHost = null;
            }
            catch
            {
                // Construction/virtual-condition rollback states are not yet in
                // the normal instance list. Retain failed cleanup as retry debt
                // so the code-behind target remains reachable until a later
                // runtime disposal pass succeeds.
                if (!state.Tracked)
                {
                    try
                    {
                        TrackComponentInstanceState(state);
                    }
                    catch
                    {
                    }
                }

                throw;
            }
            finally
            {
                state.Releasing = false;
            }
        }

        private void ReleaseAllComponentInstances()
        {
            if (_componentInstances == null)
                return;

            int cleanupIndex;

            for (cleanupIndex = _componentInstances.Count - 1;
                 cleanupIndex >= 0;
                 cleanupIndex--)
            {
                if (_componentInstances[cleanupIndex] == null)
                    RemoveComponentInstanceAt(cleanupIndex);
            }

            if (_componentInstances.Count == 0)
            {
                _componentInstances = null;

                if (_componentInstancesByRoot != null)
                    _componentInstancesByRoot.Clear();

                return;
            }

            ArrayList states = new ArrayList(_componentInstances);
            Exception firstError = null;
            int i;

            for (i = 0; i < states.Count; i++)
            {
                try
                {
                    ReleaseComponentInstanceState(
                        states[i] as ComponentInstanceState);
                }
                catch (Exception ex)
                {
                    if (firstError == null)
                        firstError = ex;
                }
            }

            if (firstError != null)
            {
                throw new InvalidOperationException(
                    "One or more registered component instances could not " +
                    "be released: " + firstError.Message,
                    firstError);
            }

            if (_componentInstances.Count == 0)
            {
                _componentInstances = null;

                if (_componentInstancesByRoot != null)
                    _componentInstancesByRoot.Clear();
            }
        }

        private void OnComponentObservableBindingChanged(
            object owner,
            long revision)
        {
            ComponentPropertyValue property =
                owner as ComponentPropertyValue;
            ComponentInstanceState state =
                property == null
                    ? null
                    : property.OwnerState;

            if (property == null ||
                state == null ||
                state.Root == null ||
                IsDisposedTarget(state.Root))
            {
                if (state != null)
                    ReleaseComponentInstanceState(state);
                else if (property != null)
                    DetachComponentObservableBinding(property);

                return;
            }

            IPropertyBindingRuntime runtimeBinding =
                property.ValueProxy as IPropertyBindingRuntime;

            if (runtimeBinding != null)
            {
                long ignoredVersion;
                SynchronizeComponentCodeBehindMember(
                    property,
                    GetPropertyBindingSnapshot(
                        runtimeBinding,
                        out ignoredVersion));
            }

            ReloadDynamicBindings(
                state.Root,
                property.Definition.Name,
                false,
                null);
        }

        private bool IsComponentInsideTarget(
            object componentRoot,
            object target)
        {
            return IsTargetOrElementDescendant(
                componentRoot,
                target);
        }

        private bool IsInsideChangedComponent(
            object target,
            ArrayList changedRoots)
        {
            if (changedRoots == null || changedRoots.Count == 0)
                return false;

            int i;

            for (i = 0; i < changedRoots.Count; i++)
            {
                if (IsTargetOrElementDescendant(
                    target,
                    changedRoots[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private void ReleaseComponentInstance(object root)
        {
            if (_componentInstancesByRoot == null || root == null)
                return;

            ComponentInstanceState state =
                _componentInstancesByRoot[root] as ComponentInstanceState;

            if (state != null)
                ReleaseComponentInstanceState(state);
        }
    }
}
