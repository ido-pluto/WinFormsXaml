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
        private bool RetainDynamicTargetDisposalHook(object target)
        {
            IComponent component = target as IComponent;

            if (component == null)
                return true;

            if (target == null ||
                _dynamicTargetDisposalHooks == null ||
                _dynamicFeaturesDisposed ||
                IsDynamicTargetDisposing(target))
            {
                return false;
            }

            DynamicTargetDisposalRegistration existing =
                _dynamicTargetDisposalHooks[target] as
                    DynamicTargetDisposalRegistration;

            if (existing != null)
            {
                return
                    !existing.Adding &&
                    !existing.DetachRequested &&
                    !existing.DisposedObserved &&
                    !existing.Detached;
            }

            DynamicTargetDisposalRegistration registration =
                new DynamicTargetDisposalRegistration();
            DynamicTargetDisposalForwarder forwarder =
                new DynamicTargetDisposalForwarder(
                    this,
                    registration,
                    target);
            EventHandler handler = new EventHandler(
                forwarder.OnDisposed);

            registration.Target = target;
            registration.Component = component;
            registration.ComponentReference = new WeakReference(component);
            registration.Forwarder = forwarder;
            registration.Handler = handler;

            // Publish before invoking user code in the custom add accessor. A
            // synchronous Disposed notification can then disable this candidate
            // and defer physical removal until the add has unwound.
            _dynamicTargetDisposalHooks.Add(target, registration);
            registration.AddAttempted = true;
            registration.Adding = true;
            bool addSucceeded = false;

            try
            {
                component.Disposed += handler;
                addSucceeded = true;
            }
            finally
            {
                registration.Adding = false;

                if (!addSucceeded)
                    registration.DetachRequested = true;

                if (registration.DetachRequested ||
                    registration.DisposedObserved ||
                    _dynamicFeaturesDisposed ||
                    !IsActiveDynamicTargetDisposalHook(registration))
                {
                    DetachDynamicTargetDisposalHook(registration);
                }
            }

            return
                addSucceeded &&
                !registration.DetachRequested &&
                !registration.DisposedObserved &&
                !_dynamicFeaturesDisposed &&
                IsActiveDynamicTargetDisposalHook(registration);
        }

        private void ReleaseDynamicTargetDisposalHook(object target)
        {
            if (target == null || _dynamicTargetDisposalHooks == null)
                return;

            DynamicTargetDisposalRegistration registration =
                _dynamicTargetDisposalHooks[target] as
                    DynamicTargetDisposalRegistration;

            if (registration != null)
                DetachDynamicTargetDisposalHook(registration);
        }

        private bool DetachDynamicTargetDisposalHook(
            DynamicTargetDisposalRegistration registration)
        {
            if (registration == null || registration.Detached)
                return true;

            registration.DetachRequested = true;
            DisableDynamicTargetDisposalHook(registration);

            if (!registration.AddAttempted)
            {
                CompleteDynamicTargetDisposalHook(registration);
                return true;
            }

            // Keep an in-flight add published, but inert, until its accessor
            // returns. Reentrant binding registration can then see that the
            // target is being detached instead of installing a second hook.
            if (registration.Adding || registration.Removing)
                return false;

            RemoveActiveDynamicTargetDisposalHook(registration);
            return TryRemoveDynamicTargetDisposalHook(registration);
        }

        private static void DisableDynamicTargetDisposalHook(
            DynamicTargetDisposalRegistration registration)
        {
            if (registration == null || registration.Forwarder == null)
                return;

            DynamicTargetDisposalForwarder forwarder =
                registration.Forwarder;
            registration.Forwarder = null;
            forwarder.Disable();
        }

        private bool TryRemoveDynamicTargetDisposalHook(
            DynamicTargetDisposalRegistration registration)
        {
            IComponent component = registration.Component;

            if (component == null &&
                registration.ComponentReference != null)
            {
                component =
                    registration.ComponentReference.Target as IComponent;
            }

            EventHandler handler = registration.Handler;

            if (component == null || handler == null)
            {
                CompleteDynamicTargetDisposalHook(registration);
                return true;
            }

            registration.Removing = true;

            try
            {
                component.Disposed -= handler;
            }
            catch
            {
                // Retry debt must not make a disposed runtime the strong owner
                // of an otherwise collectible component or Control tree. The
                // attached handler is already inert and the weak reference is
                // sufficient for a later best-effort physical removal.
                registration.Target = null;
                registration.Component = null;
                TrackDynamicTargetDisposalRetry(registration);
                return false;
            }
            finally
            {
                registration.Removing = false;
            }

            CompleteDynamicTargetDisposalHook(registration);
            return true;
        }

        private bool IsActiveDynamicTargetDisposalHook(
            DynamicTargetDisposalRegistration registration)
        {
            return
                registration != null &&
                registration.Target != null &&
                _dynamicTargetDisposalHooks != null &&
                Object.ReferenceEquals(
                    _dynamicTargetDisposalHooks[registration.Target],
                    registration);
        }

        private void RemoveActiveDynamicTargetDisposalHook(
            DynamicTargetDisposalRegistration registration)
        {
            if (IsActiveDynamicTargetDisposalHook(registration))
            {
                _dynamicTargetDisposalHooks.Remove(
                    registration.Target);
            }
        }

        private void TrackDynamicTargetDisposalRetry(
            DynamicTargetDisposalRegistration registration)
        {
            if (registration == null ||
                registration.Detached ||
                registration.RetryQueued)
            {
                return;
            }

            try
            {
                if (_dynamicTargetDisposalRetryHooks == null)
                    _dynamicTargetDisposalRetryHooks = new ArrayList();

                _dynamicTargetDisposalRetryHooks.Add(registration);
                registration.RetryQueued = true;
            }
            catch
            {
                // The forwarder is already inert. Preserve an in-flight add
                // exception even if retry bookkeeping cannot be allocated.
            }
        }

        private void RemoveDynamicTargetDisposalRetry(
            DynamicTargetDisposalRegistration registration)
        {
            if (registration == null)
                return;

            if (_dynamicTargetDisposalRetryHooks != null)
            {
                int i;

                for (i = _dynamicTargetDisposalRetryHooks.Count - 1;
                     i >= 0;
                     i--)
                {
                    if (Object.ReferenceEquals(
                            _dynamicTargetDisposalRetryHooks[i],
                            registration))
                    {
                        _dynamicTargetDisposalRetryHooks.RemoveAt(i);
                        break;
                    }
                }
            }

            registration.RetryQueued = false;
        }

        private void CompleteDynamicTargetDisposalHook(
            DynamicTargetDisposalRegistration registration)
        {
            if (registration == null || registration.Detached)
                return;

            RemoveActiveDynamicTargetDisposalHook(registration);
            RemoveDynamicTargetDisposalRetry(registration);
            registration.Detached = true;
            registration.AddAttempted = false;
            registration.Adding = false;
            registration.DetachRequested = false;
            registration.Removing = false;
            registration.Forwarder = null;
            registration.Handler = null;
            registration.Component = null;
            registration.ComponentReference = null;
            registration.Target = null;
        }

        private void RetryDynamicTargetDisposalHooks()
        {
            if (_retryingDynamicTargetDisposalHooks ||
                _dynamicTargetDisposalRetryHooks == null ||
                _dynamicTargetDisposalRetryHooks.Count == 0)
            {
                return;
            }

            _retryingDynamicTargetDisposalHooks = true;

            try
            {
                ArrayList registrations = new ArrayList(
                    _dynamicTargetDisposalRetryHooks);
                int i;

                for (i = 0; i < registrations.Count; i++)
                {
                    DetachDynamicTargetDisposalHook(
                        registrations[i] as
                            DynamicTargetDisposalRegistration);
                }
            }
            finally
            {
                _retryingDynamicTargetDisposalHooks = false;
            }
        }

        private void ReleaseAllDynamicTargetDisposalHooks()
        {
            RetryDynamicTargetDisposalHooks();

            if (_dynamicTargetDisposalHooks != null)
            {
                ArrayList registrations = new ArrayList(
                    _dynamicTargetDisposalHooks.Values);
                int i;

                for (i = 0; i < registrations.Count; i++)
                {
                    DetachDynamicTargetDisposalHook(
                        registrations[i] as
                            DynamicTargetDisposalRegistration);
                }

                _dynamicTargetDisposalHooks.Clear();
            }

            if (_disposingDynamicTargets != null)
                _disposingDynamicTargets.Clear();
        }

        private void OnDynamicTargetDisposed(
            DynamicTargetDisposalRegistration registration,
            object target)
        {
            if (registration == null ||
                target == null ||
                registration.Detached ||
                registration.DetachRequested ||
                registration.DisposedObserved ||
                !Object.ReferenceEquals(
                    registration.Target,
                    target) ||
                !IsActiveDynamicTargetDisposalHook(registration))
            {
                return;
            }

            registration.DisposedObserved = true;

            if (_disposingDynamicTargets != null)
                _disposingDynamicTargets[target] = true;

            try
            {
                Exception firstError = null;

                // Disable the target callback before source or target
                // subscription cleanup invokes user event accessors. A hostile
                // accessor can synchronously raise Disposed again; the retained
                // delegate must already be inert before that is possible.
                try
                {
                    DetachDynamicTargetDisposalHook(registration);
                }
                catch (Exception ex)
                {
                    firstError = ex;
                }

                try
                {
                    ReleaseDynamicBindings(target);
                }
                catch (Exception ex)
                {
                    if (firstError == null)
                        firstError = ex;
                }

                // Generated Image/Icon values are owned independently of the
                // binding subscriptions that produced them. An externally
                // disposed target must drop its ownership reference even when
                // a hostile event accessor made binding detachment fail. A
                // shared image remains alive until its last live target is
                // released by the same reference-counted path.
                try
                {
                    ReleaseOwnedPropertyValues(target);
                }
                catch (Exception ex)
                {
                    if (firstError == null)
                        firstError = ex;
                }

                if (firstError != null)
                    throw firstError;
            }
            finally
            {
                if (_disposingDynamicTargets != null)
                    _disposingDynamicTargets.Remove(target);
            }
        }

        private bool IsDynamicTargetDisposing(object target)
        {
            return target != null &&
                _disposingDynamicTargets != null &&
                _disposingDynamicTargets.ContainsKey(target);
        }

    }
}
