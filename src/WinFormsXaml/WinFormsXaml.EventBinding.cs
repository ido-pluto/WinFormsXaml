using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml;
using System.Windows.Forms;

namespace WinFormsXaml
{
    public sealed partial class XamlRuntime : IDisposable
    {
        private const int EventForwarderFactoryCacheLimit = 256;
        private static readonly Hashtable _eventForwarderFactoryCache =
            new Hashtable();
        private static readonly object _eventForwarderFactoryCacheSync =
            new object();

        private interface IEventHandlerForwarderFactory
        {
            Delegate Create(
                Type eventHandlerType,
                Delegate sourceHandler,
                out IEventHandlerForwarder forwarder);
        }

        private sealed class EventHandlerForwarderFactory<TEventArgs>
            : IEventHandlerForwarderFactory
            where TEventArgs : EventArgs
        {
            private static readonly MethodInfo _forwardMethod =
                typeof(EventHandlerForwarder<TEventArgs>).GetMethod(
                    "Invoke");

            public EventHandlerForwarderFactory()
            {
            }

            public Delegate Create(
                Type eventHandlerType,
                Delegate sourceHandler,
                out IEventHandlerForwarder forwarder)
            {
                EventHandlerForwarder<TEventArgs> ownedForwarder =
                    new EventHandlerForwarder<TEventArgs>(sourceHandler);

                forwarder = ownedForwarder;
                return Delegate.CreateDelegate(
                    eventHandlerType,
                    ownedForwarder,
                    _forwardMethod);
            }
        }

        // ============================================================
        // EVENTS
        // ============================================================

        private void BindEvent(
            object instance,
            EventInfo eventInfo,
            string handlerName,
            bool styleSetter)
        {
            object eventTarget =
                GetComponentEventTarget(
                    GetCurrentBuildDataContext());

            if (eventTarget == null)
            {
                throw new InvalidOperationException(
                    "XAML contains event " +
                    eventInfo.Name +
                    "=\"" +
                    handlerName +
                    "\", but no event target was supplied.");
            }

            handlerName =
                handlerName.Replace(
                    "\\_",
                    "_");

            MethodInfo[] methods =
                GetCachedEventHandlerMethods(
                    handlerName,
                    eventTarget);

            int i;

            for (i = 0;
                 i < methods.Length;
                 i++)
            {
                MethodInfo method =
                    methods[i];

                Delegate handler;

                try
                {
                    if (method.IsStatic)
                    {
                        handler =
                            Delegate.CreateDelegate(
                                eventInfo.EventHandlerType,
                                method);
                    }
                    else
                    {
                        handler =
                                Delegate.CreateDelegate(
                                    eventInfo.EventHandlerType,
                                    eventTarget,
                                    method);
                    }
                }
                catch
                {
                    continue;
                }

                // Delegate-shape mismatches may try another overload. Once a valid
                // delegate exists, accessor failures must remain observable.
                ReplaceBoundEvent(
                    instance,
                    eventInfo,
                    handler,
                    styleSetter);

                return;
            }

            throw new InvalidOperationException(
                "Could not bind " +
                instance.GetType().Name +
                "." +
                eventInfo.Name +
                " to method '" +
                handlerName +
                "'.");
        }

        private MethodInfo[] GetCachedEventHandlerMethods(
            string handlerName,
            object eventTarget)
        {
            string cacheKey = Object.ReferenceEquals(
                    eventTarget,
                    _eventTarget)
                ? handlerName
                : "@" +
                  eventTarget.GetType().AssemblyQualifiedName +
                  "\n" +
                  handlerName;
            MethodInfo[] cached =
                _eventHandlerMethodsCache == null
                    ? null
                    : _eventHandlerMethodsCache[cacheKey]
                        as MethodInfo[];

            if (cached != null)
                return cached;

            MethodInfo[] all =
                Object.ReferenceEquals(eventTarget, _eventTarget)
                    ? GetCachedEventTargetMethods()
                    : eventTarget.GetType().GetMethods(
                        BindingFlags.Instance |
                        BindingFlags.Static |
                        BindingFlags.Public |
                        BindingFlags.NonPublic);
            ArrayList matches = new ArrayList();
            int i;

            for (i = 0; i < all.Length; i++)
            {
                if (String.Equals(
                    all[i].Name,
                    handlerName,
                    StringComparison.Ordinal))
                {
                    matches.Add(all[i]);
                }
            }

            MethodInfo[] result =
                (MethodInfo[])matches.ToArray(
                    typeof(MethodInfo));

            if (_eventHandlerMethodsCache == null)
            {
                _eventHandlerMethodsCache =
                    new Hashtable(StringComparer.Ordinal);
            }

            if (_eventHandlerMethodsCache != null &&
                _eventHandlerMethodsCache.Count <
                    EventHandlerMethodCacheLimit &&
                cacheKey.Length <= RuntimeMetadataCacheKeyLengthLimit)
            {
                _eventHandlerMethodsCache[cacheKey] = result;
            }

            return result;
        }

        private MethodInfo[] GetCachedEventTargetMethods()
        {
            if (_eventTargetMethods == null)
            {
                BindingFlags flags =
                    BindingFlags.Instance |
                    BindingFlags.Static |
                    BindingFlags.Public |
                    BindingFlags.NonPublic;

                _eventTargetMethods =
                    _eventTarget.GetType().GetMethods(flags);
            }

            return _eventTargetMethods;
        }

        private void ReplaceBoundEvent(
            object target,
            EventInfo eventInfo,
            Delegate handler,
            bool styleSetter)
        {
            if (IsDisposed || _boundEvents == null)
            {
                throw new ObjectDisposedException(
                    "WinFormsXaml",
                    "The runtime was disposed while replacing an event binding.");
            }

            if (IsBoundEventTargetReleasing(target))
                return;

            BoundEventRegistration existing =
                FindCurrentBoundEventRegistration(
                    target,
                    eventInfo);

            // Reapplying the same binding is a true no-op. In particular, a
            // custom add accessor may re-enter here before the first add has
            // unwound; that must not make the in-flight candidate stale.
            if (existing != null &&
                existing.SourceHandler != null &&
                existing.SourceHandler.Equals(handler))
            {
                if (styleSetter)
                    existing.StyleOwner = true;
                else
                    existing.LocalOwner = true;

                return;
            }

            long disposalEpoch = _boundEventDisposalEpoch;
            long revision = GetNextBoundEventRevision(target, eventInfo);

            IEventHandlerForwarder forwarder;
            Delegate ownedHandler =
                CreateOwnedEventHandler(
                    eventInfo.EventHandlerType,
                    handler,
                    out forwarder);

            BoundEventRegistration registration =
                new BoundEventRegistration();

            registration.Target = target;
            registration.Event = eventInfo;
            registration.Handler = ownedHandler;
            registration.SourceHandler = handler;
            registration.Forwarder = forwarder;
            registration.Revision = revision;
            registration.DisposalEpoch = disposalEpoch;
            registration.LocalOwner = !styleSetter;
            registration.StyleOwner = styleSetter;
            registration.Disabled = false;
            registration.AddAttempted = false;
            registration.Adding = false;
            registration.DetachRequested = false;
            registration.Removing = false;
            registration.Detached = false;
            registration.Tracked = false;

            // Publish the candidate before removing the previous registration.
            // A custom remove accessor can then see this revision and either
            // accept the same handler as a no-op or supersede this exact key.
            TrackBoundEventRegistration(registration);

            try
            {
                if (existing != null)
                    DetachBoundEventRegistration(existing, true);
            }
            catch
            {
                DetachBoundEventRegistration(registration, false);
                throw;
            }

            if (IsDisposed ||
                registration.DisposalEpoch != _boundEventDisposalEpoch)
            {
                DetachBoundEventRegistration(registration, false);

                throw new ObjectDisposedException(
                    "WinFormsXaml",
                    "The runtime was disposed while replacing an event binding.");
            }

            // Only a newer request for this target and event may supersede the
            // candidate. Mutating another event has an independent revision.
            if (!IsCurrentBoundEventRegistration(registration))
            {
                DetachBoundEventRegistration(registration, false);
                return;
            }

            bool addSucceeded = false;
            registration.AddAttempted = true;
            registration.Adding = true;

            try
            {
                eventInfo.AddEventHandler(target, ownedHandler);
                addSucceeded = true;
            }
            finally
            {
                // Removal is intentionally deferred until the custom add
                // accessor has completely unwound. An accessor is allowed to
                // subscribe and then throw, so a failed add still needs one
                // best-effort physical removal.
                registration.Adding = false;

                if (!addSucceeded)
                {
                    registration.DetachRequested = true;
                    DisableBoundEventRegistration(registration);
                }

                if (registration.DetachRequested ||
                    IsDisposed ||
                    registration.DisposalEpoch != _boundEventDisposalEpoch ||
                    !IsCurrentBoundEventRegistration(registration))
                {
                    DetachBoundEventRegistration(registration, false);
                }
            }
        }

        private BoundEventTargetBucket GetBoundEventTargetBucket(
            object target,
            bool create)
        {
            if (target == null)
                return null;

            if (_boundEventsByTarget == null)
            {
                if (!create)
                    return null;

                _boundEventsByTarget =
                    new Hashtable(_runtimeObjectReferenceComparer);
            }

            BoundEventTargetBucket bucket =
                _boundEventsByTarget[target] as BoundEventTargetBucket;

            if (bucket == null && create)
            {
                bucket = new BoundEventTargetBucket();
                _boundEventsByTarget[target] = bucket;
            }

            return bucket;
        }

        private ArrayList GetBoundEventTargetRegistrations(object target)
        {
            BoundEventTargetBucket bucket =
                GetBoundEventTargetBucket(target, false);

            return bucket == null ? null : bucket.Registrations;
        }

        private void TrackBoundEventRegistration(
            BoundEventRegistration registration)
        {
            if (registration == null ||
                registration.Detached ||
                registration.Tracked ||
                registration.Target == null)
            {
                return;
            }

            if (_boundEvents == null)
                _boundEvents = new ArrayList();

            BoundEventTargetBucket bucket =
                GetBoundEventTargetBucket(registration.Target, true);

            _boundEvents.Add(registration);

            try
            {
                bucket.Registrations.Add(registration);
                registration.Tracked = true;
            }
            catch
            {
                _boundEvents.RemoveAt(_boundEvents.Count - 1);
                throw;
            }
        }

        private BoundEventRegistration FindCurrentBoundEventRegistration(
            object target,
            EventInfo eventInfo)
        {
            ArrayList registrations =
                GetBoundEventTargetRegistrations(target);

            if (registrations == null)
                return null;

            BoundEventRegistration current = null;
            int i;

            for (i = 0; i < registrations.Count; i++)
            {
                BoundEventRegistration registration =
                    registrations[i] as BoundEventRegistration;

                if (!MatchesBoundEventKey(registration, target, eventInfo) ||
                    registration.Disabled ||
                    registration.DetachRequested ||
                    registration.Detached ||
                    registration.SourceHandler == null)
                {
                    continue;
                }

                if (current == null ||
                    registration.Revision > current.Revision)
                {
                    current = registration;
                }
            }

            return current;
        }

        private bool IsCurrentBoundEventRegistration(
            BoundEventRegistration registration)
        {
            if (registration == null ||
                registration.Disabled ||
                registration.DetachRequested ||
                registration.Detached ||
                registration.Target == null ||
                registration.Event == null)
            {
                return false;
            }

            return Object.ReferenceEquals(
                FindCurrentBoundEventRegistration(
                    registration.Target,
                    registration.Event),
                registration);
        }

        private long GetNextBoundEventRevision(
            object target,
            EventInfo eventInfo)
        {
            long revision = 0;
            ArrayList registrations =
                GetBoundEventTargetRegistrations(target);

            if (registrations == null)
                return 1;

            int i;

            for (i = 0; i < registrations.Count; i++)
            {
                BoundEventRegistration registration =
                    registrations[i] as BoundEventRegistration;

                if (MatchesBoundEventKey(registration, target, eventInfo) &&
                    registration.Revision > revision)
                {
                    revision = registration.Revision;
                }
            }

            return revision + 1;
        }

        private static bool MatchesBoundEventKey(
            BoundEventRegistration registration,
            object target,
            EventInfo eventInfo)
        {
            return registration != null &&
                Object.ReferenceEquals(registration.Target, target) &&
                BoundEventInfosMatch(registration.Event, eventInfo);
        }

        private static bool BoundEventInfosMatch(
            EventInfo first,
            EventInfo second)
        {
            if (Object.ReferenceEquals(first, second) ||
                Object.Equals(first, second))
            {
                return true;
            }

            // Reflection may return distinct EventInfo objects for the same
            // inherited event depending on the reflected type. Ownership is
            // keyed by the declared event, not by a particular wrapper object.
            return first != null &&
                second != null &&
                first.DeclaringType == second.DeclaringType &&
                first.EventHandlerType == second.EventHandlerType &&
                String.Equals(
                    first.Name,
                    second.Name,
                    StringComparison.Ordinal);
        }

        private bool DetachBoundEventRegistration(
            BoundEventRegistration registration,
            bool throwOnFailure)
        {
            if (registration == null)
                return true;

            if (registration.Detached)
                return true;

            registration.DetachRequested = true;
            DisableBoundEventRegistration(registration);

            if (!registration.AddAttempted)
            {
                RemoveBoundEventTracking(registration);
                CompleteBoundEventDetachment(registration);
                return true;
            }

            if (registration.Adding || registration.Removing)
                return true;

            return RemoveBoundEventHandler(
                registration,
                throwOnFailure);
        }

        private static void DisableBoundEventRegistration(
            BoundEventRegistration registration)
        {
            if (registration == null || registration.Disabled)
                return;

            registration.Disabled = true;
            registration.SourceHandler = null;

            IEventHandlerForwarder forwarder = registration.Forwarder;
            registration.Forwarder = null;

            if (forwarder != null)
                forwarder.Disable();
        }

        private bool RemoveBoundEventHandler(
            BoundEventRegistration registration,
            bool throwOnFailure)
        {
            registration.Removing = true;

            try
            {
                registration.Event.RemoveEventHandler(
                    registration.Target,
                    registration.Handler);
            }
            catch
            {
                TrackBoundEventForRetry(registration);

                if (throwOnFailure)
                    throw;

                return false;
            }
            finally
            {
                registration.Removing = false;
            }

            RemoveBoundEventTracking(registration);
            CompleteBoundEventDetachment(registration);
            return true;
        }

        private static void CompleteBoundEventDetachment(
            BoundEventRegistration registration)
        {
            registration.Detached = true;
            registration.AddAttempted = false;
            registration.Adding = false;
            registration.DetachRequested = false;
            registration.Removing = false;
            registration.SourceHandler = null;
            registration.Forwarder = null;
            registration.Target = null;
            registration.Event = null;
            registration.Handler = null;
        }

        private bool IsBoundEventTracked(
            BoundEventRegistration registration)
        {
            return registration != null && registration.Tracked;
        }

        private void RemoveBoundEventTracking(
            BoundEventRegistration registration)
        {
            if (registration == null || !registration.Tracked)
                return;

            object target = registration.Target;
            int i;

            if (_boundEvents != null)
            {
                for (i = _boundEvents.Count - 1; i >= 0; i--)
                {
                    if (Object.ReferenceEquals(
                        _boundEvents[i],
                        registration))
                    {
                        _boundEvents.RemoveAt(i);
                        break;
                    }
                }
            }

            BoundEventTargetBucket bucket =
                GetBoundEventTargetBucket(target, false);

            if (bucket != null)
            {
                for (i = bucket.Registrations.Count - 1; i >= 0; i--)
                {
                    if (Object.ReferenceEquals(
                        bucket.Registrations[i],
                        registration))
                    {
                        bucket.Registrations.RemoveAt(i);
                        break;
                    }
                }

                if (bucket.Registrations.Count == 0 &&
                    _boundEventsByTarget != null)
                {
                    _boundEventsByTarget.Remove(target);
                }
            }

            registration.Tracked = false;
        }

        private void TrackBoundEventForRetry(
            BoundEventRegistration registration)
        {
            if (registration == null ||
                registration.Detached ||
                IsBoundEventTracked(registration))
            {
                return;
            }

            TrackBoundEventRegistration(registration);
        }

        private static Delegate CreateOwnedEventHandler(
            Type eventHandlerType,
            Delegate sourceHandler,
            out IEventHandlerForwarder forwarder)
        {
            IEventHandlerForwarderFactory factory =
                GetEventHandlerForwarderFactory(eventHandlerType);

            return factory.Create(
                eventHandlerType,
                sourceHandler,
                out forwarder);
        }

        private static IEventHandlerForwarderFactory
            GetEventHandlerForwarderFactory(Type eventHandlerType)
        {
            if (eventHandlerType == null)
                throw new ArgumentNullException("eventHandlerType");

            lock (_eventForwarderFactoryCacheSync)
            {
                IEventHandlerForwarderFactory cached =
                    _eventForwarderFactoryCache[eventHandlerType]
                        as IEventHandlerForwarderFactory;

                if (cached != null)
                    return cached;
            }

            MethodInfo invoke = eventHandlerType.GetMethod("Invoke");

            if (invoke == null || invoke.ReturnType != typeof(void))
            {
                throw new NotSupportedException(
                    "XAML event binding requires a void event delegate.");
            }

            ParameterInfo[] parameters = invoke.GetParameters();

            if (parameters.Length != 2 ||
                parameters[0].ParameterType != typeof(object) ||
                !typeof(EventArgs).IsAssignableFrom(
                    parameters[1].ParameterType))
            {
                throw new NotSupportedException(
                    "XAML event binding requires the standard " +
                    "void(object, EventArgs-derived) event signature.");
            }

            Type forwarderType =
                typeof(EventHandlerForwarderFactory<>).MakeGenericType(
                    new Type[] { parameters[1].ParameterType });
            IEventHandlerForwarderFactory created =
                (IEventHandlerForwarderFactory)Activator.CreateInstance(
                    forwarderType);

            lock (_eventForwarderFactoryCacheSync)
            {
                IEventHandlerForwarderFactory cached =
                    _eventForwarderFactoryCache[eventHandlerType]
                        as IEventHandlerForwarderFactory;

                if (cached != null)
                    return cached;

                // Delegate types are AppDomain-lifetime metadata. Keep the hot
                // set bounded so dynamically generated delegate types cannot
                // turn this optimization into unbounded retention.
                if (_eventForwarderFactoryCache.Count <
                    EventForwarderFactoryCacheLimit)
                {
                    _eventForwarderFactoryCache[eventHandlerType] = created;
                }
            }

            return created;
        }

        private void ReleaseBoundEvents(object target)
        {
            if (_boundEvents == null || target == null)
                return;

            if (IsBoundEventTargetReleasing(target))
                return;

            ArrayList targetRegistrations =
                GetBoundEventTargetRegistrations(target);

            if (targetRegistrations == null ||
                targetRegistrations.Count == 0)
            {
                return;
            }

            _boundEventReleaseTargets.Add(target);
            ArrayList registrations =
                new ArrayList(targetRegistrations);

            try
            {
                int i;

                for (i = registrations.Count - 1; i >= 0; i--)
                {
                    BoundEventRegistration registration =
                        registrations[i] as BoundEventRegistration;

                    if (registration == null ||
                        !registration.Tracked ||
                        !Object.ReferenceEquals(registration.Target, target))
                    {
                        continue;
                    }

                    // The snapshot makes every registration eligible at most
                    // once. Failed removals remain disabled retry debt without
                    // spinning this release pass.
                    DetachBoundEventRegistration(registration, false);
                }
            }
            finally
            {
                RemoveBoundEventReleaseTarget(target);
            }
        }

        private bool IsBoundEventTargetReleasing(object target)
        {
            if (_boundEventReleaseTargets == null || target == null)
                return false;

            int i;

            for (i = 0; i < _boundEventReleaseTargets.Count; i++)
            {
                if (Object.ReferenceEquals(
                    _boundEventReleaseTargets[i],
                    target))
                {
                    return true;
                }
            }

            return false;
        }

        private void RemoveBoundEventReleaseTarget(object target)
        {
            if (_boundEventReleaseTargets == null)
                return;

            int i;

            for (i = _boundEventReleaseTargets.Count - 1; i >= 0; i--)
            {
                if (Object.ReferenceEquals(
                    _boundEventReleaseTargets[i],
                    target))
                {
                    _boundEventReleaseTargets.RemoveAt(i);
                    return;
                }
            }
        }

        private void DisposeBoundEvents()
        {
            ++_boundEventDisposalEpoch;

            if (_boundEvents == null)
            {
                _boundEventsByTarget = null;
                return;
            }

            ArrayList registrations =
                new ArrayList(_boundEvents);
            Exception firstError = null;
            int i;

            for (i = registrations.Count - 1; i >= 0; i--)
            {
                BoundEventRegistration registration =
                    registrations[i] as BoundEventRegistration;

                if (registration == null)
                    continue;

                try
                {
                    // A custom remove accessor failure is intentionally
                    // converted into disabled retry debt by this call. Only an
                    // unexpected bookkeeping failure reaches this catch.
                    DetachBoundEventRegistration(registration, false);
                }
                catch (Exception ex)
                {
                    if (firstError == null)
                        firstError = ex;
                }
            }

            if (_boundEvents != null && _boundEvents.Count == 0)
            {
                _boundEvents = null;
                _boundEventsByTarget = null;
            }

            if (firstError != null)
            {
                throw new InvalidOperationException(
                    "One or more bound events could not be released: " +
                    firstError.Message,
                    firstError);
            }
        }
    }
}
