using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using WinFormsXaml;

namespace WinFormsXaml.Tests
{
    public sealed class RuntimeOwnedAuditPanel : Panel
    {
        public static int DisposeCallCount;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                DisposeCallCount++;

            base.Dispose(disposing);
        }
    }

    public sealed class RuntimeOwnedAuditChild : Label
    {
        public static int DisposeCallCount;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                DisposeCallCount++;

            base.Dispose(disposing);
        }
    }

    public sealed class RetryingRuntimeOwnedAuditRoot : IDisposable
    {
        public static int DisposeCallCount;

        public void Dispose()
        {
            DisposeCallCount++;

            if (DisposeCallCount == 1)
            {
                throw new InvalidOperationException(
                    "First root disposal attempt failed.");
            }
        }
    }

    public sealed class FailedLoadRuntimeOwnedAuditPanel : Panel
    {
        public static int DisposeCallCount;

        protected override void OnLayout(LayoutEventArgs e)
        {
            throw new InvalidOperationException(
                "Late root layout failure.");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                DisposeCallCount++;

            base.Dispose(disposing);
        }
    }

    internal static class RuntimeOwnershipRegressionTests
    {
        public static void Run()
        {
            TestDirectRuntimeDisposesControlTreeAndCompactsState();
            TestRootCleanupFailureRemainsRetryable();
            TestFailedLoadUsesOnlyRollbackRootOwnership();
        }

        private static void
            TestDirectRuntimeDisposesControlTreeAndCompactsState()
        {
            RuntimeOwnedAuditPanel.DisposeCallCount = 0;
            RuntimeOwnedAuditChild.DisposeCallCount = 0;
            PresetManager sharedPresets = new PresetManager();
            XamlRuntime runtime = XamlRuntime.Load(
                "<RuntimeOwnedAuditPanel Name='AuditRoot'>" +
                "  <RuntimeOwnedAuditChild Name='AuditChild' />" +
                "</RuntimeOwnedAuditPanel>",
                null,
                "retained-base-path",
                sharedPresets);
            RuntimeOwnedAuditPanel root =
                runtime.Get<RuntimeOwnedAuditPanel>("AuditRoot");
            RuntimeOwnedAuditChild child =
                runtime.Get<RuntimeOwnedAuditChild>("AuditChild");
            IDictionary<string, object> retainedNames =
                runtime.NamedObjects;

            runtime.Dispose();

            AssertTrue(
                runtime.IsDisposed,
                "direct runtime disposal reaches terminal completion");
            AssertTrue(
                root.IsDisposed && child.IsDisposed,
                "direct runtime disposal releases the complete native tree");
            AssertEqual(
                1,
                RuntimeOwnedAuditPanel.DisposeCallCount,
                "the reentrant root Disposed callback does not dispose the root twice");
            AssertEqual(
                1,
                RuntimeOwnedAuditChild.DisposeCallCount,
                "native child ownership disposes each child once");
            AssertTrue(
                runtime.Root == null && runtime.RootControl == null,
                "terminal disposal releases the root reference");
            AssertTrue(
                runtime.Names.Count == 0 &&
                runtime.NamedObjects.Count == 0 &&
                retainedNames.Count == 0,
                "terminal disposal clears current and previously exposed name maps");
            AssertRetainedRuntimeGraphsAreCompacted(runtime);

            // A completed IDisposable remains harmless from any thread. This
            // also proves the completion guard runs before owner-thread checks.
            Exception repeatedDisposeFailure = null;
            Thread repeatedDisposeThread =
                new Thread(
                    delegate()
                    {
                        try
                        {
                            runtime.Dispose();
                        }
                        catch (Exception ex)
                        {
                            repeatedDisposeFailure = ex;
                        }
                    });
            repeatedDisposeThread.Start();
            repeatedDisposeThread.Join();

            AssertTrue(
                repeatedDisposeFailure == null,
                "completed disposal is idempotent across threads");
            AssertEqual(
                1,
                RuntimeOwnedAuditPanel.DisposeCallCount,
                "repeated disposal does not revisit the root");
        }

        private static void TestRootCleanupFailureRemainsRetryable()
        {
            RetryingRuntimeOwnedAuditRoot.DisposeCallCount = 0;
            XamlRuntime runtime = XamlRuntime.Load(
                "<RetryingRuntimeOwnedAuditRoot />");
            object root = runtime.Root;
            Exception firstFailure = null;

            try
            {
                runtime.Dispose();
            }
            catch (Exception ex)
            {
                firstFailure = ex;
            }

            AssertTrue(
                firstFailure != null &&
                ExceptionContains(
                    firstFailure,
                    "First root disposal attempt failed"),
                "a root cleanup failure remains observable");
            AssertTrue(
                !runtime.IsDisposed,
                "a partial cleanup is not reported as terminal disposal");
            AssertTrue(
                Object.ReferenceEquals(root, runtime.Root),
                "a failed root cleanup retains the root for retry");
            AssertEqual(
                1,
                RetryingRuntimeOwnedAuditRoot.DisposeCallCount,
                "the failed attempt is recorded once");

            runtime.Dispose();

            AssertTrue(
                runtime.IsDisposed && runtime.Root == null,
                "a successful retry reaches terminal compaction");
            AssertEqual(
                2,
                RetryingRuntimeOwnedAuditRoot.DisposeCallCount,
                "the retained non-Control root is retried exactly once");
        }

        private static void TestFailedLoadUsesOnlyRollbackRootOwnership()
        {
            FailedLoadRuntimeOwnedAuditPanel.DisposeCallCount = 0;
            Exception failure = null;

            try
            {
                XamlRuntime.Load(
                    "<FailedLoadRuntimeOwnedAuditPanel>" +
                    "  <Label Text='child' />" +
                    "</FailedLoadRuntimeOwnedAuditPanel>");
            }
            catch (Exception ex)
            {
                failure = ex;
            }

            AssertTrue(
                failure != null &&
                ExceptionContains(failure, "Late root layout failure"),
                "a post-construction load failure remains primary");
            AssertEqual(
                1,
                FailedLoadRuntimeOwnedAuditPanel.DisposeCallCount,
                "failed-load rollback, not normal root disposal, owns the partial tree");
        }

        private static void AssertRetainedRuntimeGraphsAreCompacted(
            XamlRuntime runtime)
        {
            BindingFlags flags =
                BindingFlags.Instance | BindingFlags.NonPublic;
            Type runtimeType = typeof(XamlRuntime);
            FieldInfo elementInfosField = runtimeType.GetField(
                "_elementInfos",
                flags);
            FieldInfo basePathField = runtimeType.GetField(
                "_basePath",
                flags);
            FieldInfo presetManagerField = runtimeType.GetField(
                "_presetManager",
                flags);
            IDictionary elementInfos = elementInfosField == null
                ? null
                : elementInfosField.GetValue(runtime) as IDictionary;

            AssertTrue(
                elementInfosField != null &&
                basePathField != null &&
                presetManagerField != null &&
                elementInfos != null,
                "runtime compaction fields remain inspectable");
            AssertEqual(
                0,
                elementInfos.Count,
                "terminal disposal clears element metadata");
            AssertTrue(
                basePathField.GetValue(runtime) == null,
                "terminal disposal releases retained source paths");
            AssertTrue(
                presetManagerField.GetValue(runtime) == null,
                "terminal disposal releases its shared preset-manager reference");
        }

        private static bool ExceptionContains(
            Exception error,
            string text)
        {
            while (error != null)
            {
                if (error.Message != null &&
                    error.Message.IndexOf(
                        text,
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }

                error = error.InnerException;
            }

            return false;
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private static void AssertEqual(
            int expected,
            int actual,
            string message)
        {
            if (expected != actual)
            {
                throw new InvalidOperationException(
                    message + ": expected " + expected +
                    ", actual " + actual + ".");
            }
        }
    }
}
