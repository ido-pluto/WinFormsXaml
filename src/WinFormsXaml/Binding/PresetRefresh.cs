using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Xml;
using System.Windows.Forms;

namespace WinFormsXaml
{
    public sealed partial class XamlRuntime
    {
        private void OnPresetManagerChanged(
            object sender,
            PresetChangedEventArgs e)
        {
            lock (_presetRefreshSync)
            {
                if (_dynamicFeaturesDisposed)
                    return;

                MergePendingPresetChange(e);
                _presetRefreshRetryBlocked = false;

                if (_activePresetDependencyMemo != null)
                {
                    _activePresetDependencyMemo =
                        new Hashtable(
                            StringComparer.OrdinalIgnoreCase);
                }

                if (_presetRefreshActive || _presetRefreshQueued)
                    return;
            }

            QueuePresetRefresh();
        }

        private void OnDynamicRootReady()
        {
            Control root = RootControl;

            if (_loadedPresetElements != null)
                _loadedPresetElements.Clear();

            if (root == null)
            {
                OnObservableBindingRootReady();

                bool pendingWithoutControl;

                lock (_presetRefreshSync)
                    pendingWithoutControl = _presetChangePending;

                if (_root != null && pendingWithoutControl)
                    DrainPresetChanges();

                return;
            }

            if (root.IsDisposed)
            {
                DisposeDynamicFeatures();
                return;
            }

            lock (_presetRefreshSync)
            {
                if (_dynamicFeaturesDisposed)
                    return;

                if (!_rootDisposedHooked)
                {
                    root.Disposed += OnDynamicRootDisposed;
                    _rootDisposedHooked = true;
                }
            }

            if (root.IsDisposed)
            {
                DisposeDynamicFeatures();
                return;
            }

            OnObservableBindingRootReady();

            bool pending;

            lock (_presetRefreshSync)
                pending = _presetChangePending;

            if (pending)
                QueuePresetRefresh();
        }

        private void QueuePresetRefresh()
        {
            QueuePresetRefresh(true);
        }

        private void QueuePresetRefresh(bool mayRetryDispatch)
        {
            Control root = RootControl;

            if (root == null)
            {
                bool canDrain;

                lock (_presetRefreshSync)
                {
                    canDrain =
                        !_dynamicFeaturesDisposed &&
                        _presetChangePending &&
                        !_presetRefreshRetryBlocked;
                }

                if (_root != null && canDrain)
                    DrainPresetChanges();

                return;
            }

            if (root.IsDisposed)
            {
                DisposeDynamicFeatures();
                return;
            }

            lock (_presetRefreshSync)
            {
                if (_dynamicFeaturesDisposed ||
                    !_presetChangePending ||
                    _presetRefreshRetryBlocked)
                {
                    return;
                }
            }

            if (!root.IsHandleCreated)
            {
                HookDynamicRootHandle(root);
                return;
            }

            if (root.InvokeRequired)
            {
                lock (_presetRefreshSync)
                {
                    if (_presetRefreshQueued)
                        return;

                    _presetRefreshQueued = true;
                }

                try
                {
                    root.BeginInvoke(
                        new MethodInvoker(DrainPresetChanges));
                }
                catch (InvalidOperationException)
                {
                    lock (_presetRefreshSync)
                        _presetRefreshQueued = false;

                    if (!root.IsDisposed)
                    {
                        HookDynamicRootHandle(
                            root,
                            mayRetryDispatch);
                    }
                }

                return;
            }

            DrainPresetChanges();
        }

        private void OnDynamicRootHandleCreated(
            object sender,
            EventArgs e)
        {
            Control root = sender as Control;

            if (root != null)
                root.HandleCreated -= OnDynamicRootHandleCreated;

            lock (_presetRefreshSync)
                _rootHandleHooked = false;

            QueuePresetRefresh();
        }

        private void HookDynamicRootHandle(Control root)
        {
            HookDynamicRootHandle(root, true);
        }

        private void HookDynamicRootHandle(
            Control root,
            bool retryIfHandleCreated)
        {
            if (root == null || root.IsDisposed)
                return;

            bool hook;

            lock (_presetRefreshSync)
            {
                if (_dynamicFeaturesDisposed)
                    return;

                hook = !_rootHandleHooked;

                if (hook)
                {
                    _rootHandleHooked = true;
                    root.HandleCreated += OnDynamicRootHandleCreated;
                }
            }

            if (hook)
            {
                if (root.IsDisposed)
                {
                    DisposeDynamicFeatures();
                    return;
                }

                // The handle can be created between QueuePresetRefresh checking
                // it and this subscription. Re-check so the pending change is
                // not stranded waiting for an event that already happened.
                if (retryIfHandleCreated && root.IsHandleCreated)
                {
                    root.HandleCreated -= OnDynamicRootHandleCreated;

                    lock (_presetRefreshSync)
                        _rootHandleHooked = false;

                    QueuePresetRefresh(false);
                }
            }
        }

        private void OnDynamicRootDisposed(
            object sender,
            EventArgs e)
        {
            Dispose();
        }

        private void DrainPresetChanges()
        {
            lock (_presetRefreshSync)
            {
                _presetRefreshQueued = false;

                if (_dynamicFeaturesDisposed ||
                    _presetRefreshActive ||
                    _presetRefreshRetryBlocked)
                {
                    return;
                }

                _presetRefreshActive = true;
            }

            bool refreshFailed = false;

            try
            {
                while (true)
                {
                    PresetChangedEventArgs change;

                    lock (_presetRefreshSync)
                    {
                        if (_dynamicFeaturesDisposed ||
                            !_presetChangePending)
                        {
                            return;
                        }

                        change = _pendingPresetChange;
                        _pendingPresetChange = null;
                        _presetChangePending = false;
                    }

                    try
                    {
                        ReloadPresetDependents(change);
                    }
                    catch
                    {
                        refreshFailed = true;

                        lock (_presetRefreshSync)
                        {
                            if (!_dynamicFeaturesDisposed)
                            {
                                MergePendingPresetChange(change);
                                _presetRefreshRetryBlocked = true;
                            }
                        }

                        throw;
                    }
                }
            }
            finally
            {
                bool retry;

                lock (_presetRefreshSync)
                {
                    _presetRefreshActive = false;

                    if (refreshFailed && !_dynamicFeaturesDisposed)
                        _presetRefreshRetryBlocked = true;

                    retry =
                        !_dynamicFeaturesDisposed &&
                        _presetChangePending &&
                        !_presetRefreshQueued &&
                        !_presetRefreshRetryBlocked;
                }

                if (retry)
                    QueuePresetRefresh();
            }
        }

        private void RetryFailedPresetRefresh()
        {
            bool retry;

            lock (_presetRefreshSync)
            {
                retry =
                    !_dynamicFeaturesDisposed &&
                    _presetRefreshRetryBlocked &&
                    _presetChangePending &&
                    !_presetRefreshActive;

                if (retry)
                    _presetRefreshRetryBlocked = false;
            }

            if (retry)
                DrainPresetChanges();
        }

        private void ReloadPresetDependents(
            PresetChangedEventArgs change)
        {
            if (_dynamicFeaturesDisposed)
                return;

            Hashtable previousMemo =
                _activePresetDependencyMemo;
            _activePresetDependencyMemo =
                new Hashtable(
                    StringComparer.OrdinalIgnoreCase);

            try
            {
                ReloadDynamicBindings(
                    null,
                    null,
                    true,
                    change);

                if (_dynamicFeaturesDisposed ||
                    _presetItemsControls == null ||
                    _presetItemsControls.Count == 0)
                    return;

                ArrayList presetItems =
                    new ArrayList(_presetItemsControls);
                int i;

                for (i = presetItems.Count - 1; i >= 0; i--)
                {
                    if (_dynamicFeaturesDisposed)
                        break;

                    ItemsControl items =
                        presetItems[i] as ItemsControl;

                    if (items == null || items.IsDisposed)
                    {
                        if (items != null)
                            UnregisterItemsControl(items);
                        continue;
                    }

                    if (_itemsControlSet == null ||
                        !_itemsControlSet.ContainsKey(items))
                    {
                        continue;
                    }

                    if (!ItemTemplateDependsOnPreset(items, change))
                        continue;

                    ReloadItemPresetBindings(items, change);
                }
            }
            finally
            {
                _activePresetDependencyMemo = previousMemo;
            }
        }

        private void MergePendingPresetChange(
            PresetChangedEventArgs change)
        {
            if (!_presetChangePending)
            {
                _presetChangePending = true;
                _pendingPresetChange = change;
                return;
            }

            if (!HaveSamePresetChangeScope(
                _pendingPresetChange,
                change))
            {
                // Null scope means that the next pass must refresh every preset
                // dependency. This preserves correctness when changes coalesce.
                _pendingPresetChange = null;
            }
        }

        private static bool HaveSamePresetChangeScope(
            PresetChangedEventArgs left,
            PresetChangedEventArgs right)
        {
            if (left == null || right == null)
                return left == null && right == null;

            return
                EqualsIgnoreCase(left.SetName, right.SetName) &&
                EqualsIgnoreCase(left.PresetName, right.PresetName) &&
                EqualsIgnoreCase(left.Key, right.Key);
        }

    }
}
