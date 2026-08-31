using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Windows.Forms;
using WinFormsXaml;

namespace WinFormsXaml.Tests
{
    public sealed class CodeBehindCard : IDisposable
    {
        private static readonly ArrayList _instances = new ArrayList();

        public readonly PropertyBinding<string> Title =
            new PropertyBinding<string>(String.Empty);
        public readonly ChildrenBind Children = new ChildrenBind();
        public int Count;
        public int ClickCount;
        public int DisposeCount;
        public string Snapshot = "Snapshot one";
        public bool Disposed;

        public CodeBehindCard()
        {
            _instances.Add(this);
        }

        public static void Reset()
        {
            _instances.Clear();
        }

        public static CodeBehindCard GetInstance(int index)
        {
            return (CodeBehindCard)_instances[index];
        }

        public static int InstanceCount
        {
            get { return _instances.Count; }
        }

        private string FormatTitle(string title, int count)
        {
            return title + ":" + count.ToString();
        }

        private string FormatItem(string item)
        {
            return Title.Value + ":" + item;
        }

        private void HandleClick(object sender, EventArgs e)
        {
            ClickCount++;
        }

        public void Dispose()
        {
            DisposeCount++;
            Disposed = true;
        }
    }

    public sealed class NoSlotChildrenCodeBehind
    {
        public static NoSlotChildrenCodeBehind LastInstance;
        public string Children = "domain children";

        public NoSlotChildrenCodeBehind()
        {
            LastInstance = this;
        }
    }

    public sealed class ThrowingChildrenCodeBehind
    {
        public static ThrowingChildrenCodeBehind LastInstance;
        public readonly ChildrenBind Children = new ChildrenBind();

        public ThrowingChildrenCodeBehind()
        {
            LastInstance = this;
        }
    }

    public sealed class ThrowingAddPanel : Panel
    {
        protected override void OnControlAdded(ControlEventArgs e)
        {
            base.OnControlAdded(e);

            if (e.Control != null && e.Control.Name == "RejectAfterAdd")
            {
                throw new InvalidOperationException(
                    "Rejected test child after native attachment.");
            }
        }

        protected override void OnControlRemoved(ControlEventArgs e)
        {
            base.OnControlRemoved(e);

            if (e.Control != null && e.Control.Name == "RejectAfterAdd")
            {
                throw new InvalidOperationException(
                    "Rejected test child rollback removal notification.");
            }
        }

        protected override Control.ControlCollection CreateControlsInstance()
        {
            return new ThrowingControlCollection(this);
        }

        private sealed class ThrowingControlCollection :
            Control.ControlCollection
        {
            public ThrowingControlCollection(Control owner)
                : base(owner)
            {
            }

            public override void Add(Control value)
            {
                if (value != null && value.Name == "RejectAdd")
                {
                    throw new InvalidOperationException(
                        "Rejected test child attachment.");
                }

                base.Add(value);
            }
        }
    }

    public sealed class RollbackProbePanel : Panel
    {
        public static int CreatedCount;
        public static int DisposedCount;

        public RollbackProbePanel()
        {
            CreatedCount++;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !IsDisposed)
                DisposedCount++;

            base.Dispose(disposing);
        }
    }

    public sealed class RollbackCodeBehind : IDisposable
    {
        private bool _failFirstDispose = true;

        public void Dispose()
        {
            if (_failFirstDispose)
            {
                _failFirstDispose = false;
                throw new InvalidOperationException(
                    "Injected component cleanup failure.");
            }
        }
    }

    internal static class ComponentCodeBehindTests
    {
        public static void Run()
        {
            const string resourceName =
                "WinFormsXaml.Tests.Fixtures.CodeBehindCard.xml";

            XamlRuntime.Register(
                Assembly.GetExecutingAssembly(),
                "WinFormsXaml.Tests.Fixtures.XmlOnlyBridge.xml");
            XamlRuntime.Register(
                Assembly.GetExecutingAssembly(),
                resourceName);

            TestComponentPropertyMetadataIndex();
            CodeBehindCard.Reset();
            ComponentHostState state = new ComponentHostState();
            XamlRuntime runtime = XamlRuntime.Load(
                "<Panel>" +
                "  <CodeBehindCard Name='FirstCard' " +
                "      Title='{Binding Title, Mode=TwoWay}' " +
                "      Count='{Binding Count}'>" +
                "    <Label Name='CallerFirst' Text='{Binding Title}' />" +
                "    <Label Name='CallerSecond' Text='Second' />" +
                "  </CodeBehindCard>" +
                "  <CodeBehindCard Name='SecondCard' Title='Other' Count='2' />" +
                "</Panel>",
                state);

            CodeBehindCard first = null;
            CodeBehindCard second = null;

            try
            {
                AssertEqual(2, CodeBehindCard.InstanceCount, "one class instance per invocation");
                first = CodeBehindCard.GetInstance(0);
                second = CodeBehindCard.GetInstance(1);

                Panel firstRoot = runtime.Get<Panel>("FirstCard");
                Label classTitle =
                    FindControl<Label>(firstRoot, "ClassTitle");
                Button classButton =
                    FindControl<Button>(firstRoot, "ClassButton");
                XamlRuntime.ItemsControl classRows =
                    FindControl<XamlRuntime.ItemsControl>(
                        firstRoot,
                        "ClassRows");
                Label bridgeValue =
                    FindControl<Label>(firstRoot, "BridgeValue");
                Label bridgeOwnerTitle =
                    FindControl<Label>(firstRoot, "BridgeOwnerTitle");
                Button bridgeButton =
                    FindControl<Button>(firstRoot, "BridgeButton");

                AssertTrue(classTitle != null, "component template label exists");
                AssertTrue(classButton != null, "component template event button exists");
                AssertTrue(classRows != null, "component item host exists");
                AssertTrue(bridgeValue != null, "nested XML-only component exists");
                AssertTrue(bridgeOwnerTitle != null, "nested inherited binding exists");
                AssertTrue(bridgeButton != null, "nested inherited event exists");

                AssertEqual("Initial", first.Title.Value, "stable property proxy receives invocation value");
                AssertEqual(1, first.Count, "plain public field receives invocation value");
                AssertEqual("Initial", classTitle.Text, "template binds to shared code-behind proxy");
                AssertEqual("Initial:1", classButton.Text, "function resolves on component code-behind");
                AssertEqual(
                    "Snapshot one",
                    bridgeValue.Text,
                    "nested invocation Source=CodeBehind uses the outer component");
                AssertEqual(
                    "Initial",
                    bridgeOwnerTitle.Text,
                    "XML-only template inherits the nearest component target");
                AssertEqual(2, first.Children.Count, "multiple caller children are projected");
                AssertSame(
                    runtime.Get<Label>("CallerFirst"),
                    first.Children.Get<Label>("CallerFirst"),
                    "ChildrenBind keeps the caller namescope and scoped lookup");

                ArrayList firstItems = new ArrayList();
                firstItems.Add("One");
                classRows.SetItems(firstItems);
                Application.DoEvents();

                Label classRow =
                    FindControl<Label>(classRows, "ClassRow");
                Label classRowTitle =
                    FindControl<Label>(classRows, "ClassRowTitle");
                Button classRowButton =
                    FindControl<Button>(classRows, "ClassRowButton");

                AssertTrue(classRow != null, "component item row is realized");
                AssertTrue(classRowTitle != null, "component item binding is realized");
                AssertTrue(classRowButton != null, "component item event is realized");
                AssertEqual(
                    "Initial:One",
                    classRow.Text,
                    "item function uses the owning component code-behind");
                AssertEqual(
                    "Initial",
                    classRowTitle.Text,
                    "item Source=CodeBehind uses the owning component instance");

                state.Title.Value = "Outer update";
                state.Count.Value = 7;
                Application.DoEvents();
                AssertEqual("Outer update", first.Title.Value, "outer binding updates stable proxy");
                AssertEqual(7, first.Count, "outer binding synchronizes plain member");
                AssertEqual("Outer update", classTitle.Text, "proxy update patches component template");
                AssertEqual(
                    "Outer update:7",
                    classButton.Text,
                    "synchronous refresh sees the updated plain component member");
                AssertEqual(
                    "Outer update",
                    classRowTitle.Text,
                    "retained item binding keeps its component target");
                AssertEqual(
                    "Outer update",
                    bridgeOwnerTitle.Text,
                    "nested XML-only retained binding keeps the outer target");

                first.Snapshot = "Snapshot two";
                runtime.ReloadBindings();
                AssertEqual(
                    "Snapshot two",
                    bridgeValue.Text,
                    "nested invocation reload restores its caller event target");

                ArrayList secondItems = new ArrayList();
                secondItems.Add("Two");
                classRows.SetItems(secondItems);
                Application.DoEvents();
                classRow = FindControl<Label>(classRows, "ClassRow");
                AssertEqual(
                    "Outer update:Two",
                    classRow.Text,
                    "reused item function keeps its component target");

                first.Title.Value = "Class update";
                Application.DoEvents();
                AssertEqual("Class update", state.Title.Value, "two-way component proxy writes outward");

                classButton.PerformClick();
                bridgeButton.PerformClick();
                classRowButton =
                    FindControl<Button>(classRows, "ClassRowButton");
                classRowButton.PerformClick();
                AssertEqual(3, first.ClickCount, "template events use component code-behind");

                Label oldFirst = first.Children.Get<Label>("CallerFirst");
                Label replacementOne = new Label();
                replacementOne.Name = "ReplacementOne";
                Label replacementTwo = new Label();
                replacementTwo.Name = "ReplacementTwo";
                first.Children.Replace(replacementOne, replacementTwo);
                AssertEqual(2, first.Children.Count, "Replace publishes direct children");
                AssertTrue(oldFirst.IsDisposed, "Replace releases removed projected controls");

                int unchangedNotifications = 0;
                EventHandler unchanged =
                    delegate(object sender, EventArgs e)
                    {
                        unchangedNotifications++;
                    };
                first.Children.Changed += unchanged;

                try
                {
                    first.Children.Replace(first.Children.ToArray());
                }
                finally
                {
                    first.Children.Changed -= unchanged;
                }

                AssertEqual(
                    0,
                    unchangedNotifications,
                    "identical Replace skips publication and Changed notification");
                AssertSame(
                    replacementOne,
                    first.Children[0],
                    "identical Replace preserves the first projected Control");
                AssertSame(
                    replacementTwo,
                    first.Children[1],
                    "identical Replace preserves the second projected Control");

                Panel wrapper = new Panel();
                wrapper.Name = "ReplacementWrapper";
                first.Children.Wrap(wrapper);
                AssertEqual(1, first.Children.Count, "Wrap creates one direct projected root");
                AssertEqual(2, wrapper.Controls.Count, "Wrap preserves previous projected controls");
                AssertSame(
                    replacementOne,
                    first.Children.Get<Label>("ReplacementOne"),
                    "ChildrenBind.Get searches the wrapped tree");

                bool selfWrapRejected = false;

                try
                {
                    first.Children.Wrap(first.Children[0]);
                }
                catch (InvalidOperationException)
                {
                    selfWrapRejected = true;
                }

                AssertTrue(
                    selfWrapRejected,
                    "a projected child cannot wrap itself");

                Control removedWrapper = first.Children[0];
                Label finalChild = new Label();
                finalChild.Name = "FinalChild";
                bool reentrantMutationRejected = false;
                EventHandler throwingChanged =
                    delegate(object sender, EventArgs e)
                    {
                        try
                        {
                            first.Children.Clear();
                        }
                        catch (InvalidOperationException)
                        {
                            reentrantMutationRejected = true;
                            throw;
                        }
                    };
                bool listenerFailureReported = false;
                first.Children.Changed += throwingChanged;

                try
                {
                    first.Children.Replace(finalChild);
                }
                catch (InvalidOperationException)
                {
                    listenerFailureReported = true;
                }
                finally
                {
                    first.Children.Changed -= throwingChanged;
                }

                AssertTrue(
                    listenerFailureReported,
                    "Changed listener failure is reported after commit");
                AssertTrue(
                    reentrantMutationRejected,
                    "Changed rejects recursive mutation of the same slot");
                AssertTrue(
                    removedWrapper.IsDisposed,
                    "Changed listener failure does not skip removed-child cleanup");
                AssertSame(
                    finalChild,
                    first.Children[0],
                    "listener failure leaves the committed snapshot published");

                Label afterNotification = new Label();
                afterNotification.Name = "AfterNotification";
                first.Children.Replace(afterNotification);
                AssertSame(
                    afterNotification,
                    first.Children[0],
                    "Changed reentrancy guard clears after listener failure");
            }
            finally
            {
                runtime.Dispose();
            }

            AssertTrue(first != null && first.Disposed, "first component code-behind is disposed");
            AssertTrue(second != null && second.Disposed, "second component code-behind is disposed");

            bool retired = false;

            try
            {
                first.Children.Clear();
            }
            catch (ObjectDisposedException)
            {
                retired = true;
            }

            AssertTrue(retired, "ChildrenBind retires with its component instance");

            TestFailedReplacementRollsBackOwnership();
            TestBuildRollbackStillDisposesNativeRoot();
            TestExternalRootDisposalReleasesRuntimeTree();
            TestProjectedNestedRootDisposalUpdatesOwners();
            TestNoSlotAllowsUnrelatedChildrenMember();
        }

        private static void TestComponentPropertyMetadataIndex()
        {
            FieldInfo registryField = typeof(XamlRuntime).GetField(
                "_registeredComponents",
                BindingFlags.Static |
                BindingFlags.NonPublic);
            IDictionary registry = registryField == null
                ? null
                : registryField.GetValue(null) as IDictionary;
            object registration = registry == null
                ? null
                : registry["CodeBehindCard"];
            FieldInfo indexField = registration == null
                ? null
                : registration.GetType().GetField(
                    "PropertiesByName",
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);
            IDictionary propertyIndex = indexField == null
                ? null
                : indexField.GetValue(registration) as IDictionary;

            AssertTrue(
                propertyIndex != null,
                "registered component caches its property-name index");
            AssertEqual(
                2,
                propertyIndex.Count,
                "component property index contains every declaration once");
            AssertTrue(
                propertyIndex.Contains("title") &&
                propertyIndex.Contains("COUNT"),
                "component property index preserves case-insensitive lookup");

            CodeBehindCard.Reset();
            XamlRuntime runtime = XamlRuntime.Load(
                "<CodeBehindCard tItLe='Indexed' cOuNt='4' />");

            try
            {
                CodeBehindCard instance = CodeBehindCard.GetInstance(0);

                AssertEqual(
                    "Indexed",
                    instance.Title.Value,
                    "indexed invocation lookup preserves mixed-case property values");
                AssertEqual(
                    4,
                    instance.Count,
                    "indexed invocation lookup maps every supplied property");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestFailedReplacementRollsBackOwnership()
        {
            XamlRuntime.Register(
                "ThrowingAddPanel",
                typeof(ThrowingAddPanel));
            XamlRuntime.Register(
                Assembly.GetExecutingAssembly(),
                "WinFormsXaml.Tests.Fixtures.ThrowingChildrenCard.xml");

            ThrowingChildrenCodeBehind.LastInstance = null;
            XamlRuntime runtime = XamlRuntime.Load(
                "<ThrowingChildrenCard><Label Name='Initial' /></ThrowingChildrenCard>");
            Label rejected = new Label();
            rejected.Name = "RejectAdd";
            Label rejectedAfterAdd = new Label();
            rejectedAfterAdd.Name = "RejectAfterAdd";
            bool failed = false;
            bool failedAfterAdd = false;

            try
            {
                Label initial =
                    ThrowingChildrenCodeBehind.LastInstance.Children.Get<Label>(
                        "Initial");

                try
                {
                    ThrowingChildrenCodeBehind.LastInstance.Children.Replace(
                        rejected);
                }
                catch (InvalidOperationException)
                {
                    failed = true;
                }

                AssertTrue(failed, "injected native child attachment fails");
                AssertTrue(
                    rejected.Parent == null,
                    "failed replacement restores native parentage");

                try
                {
                    ThrowingChildrenCodeBehind.LastInstance.Children.Replace(
                        rejectedAfterAdd);
                }
                catch (InvalidOperationException)
                {
                    failedAfterAdd = true;
                }

                AssertTrue(
                    failedAfterAdd,
                    "post-attach listener failure is reported");
                AssertTrue(
                    rejectedAfterAdd.Parent == null,
                    "rollback continues after ControlRemoved listener failure");
                AssertSame(
                    initial,
                    ThrowingChildrenCodeBehind.LastInstance.Children[0],
                    "rollback keeps the previously published child snapshot");
                AssertTrue(
                    initial.Parent != null,
                    "rollback restores the previous native child");
            }
            finally
            {
                runtime.Dispose();
            }

            AssertTrue(
                !rejected.IsDisposed,
                "failed replacement leaves the candidate caller-owned");
            AssertTrue(
                !rejectedAfterAdd.IsDisposed,
                "post-attach failure leaves the candidate caller-owned");
            rejected.Dispose();
            rejectedAfterAdd.Dispose();
        }

        private static void TestBuildRollbackStillDisposesNativeRoot()
        {
            XamlRuntime.Register(
                "RollbackProbePanel",
                typeof(RollbackProbePanel));
            XamlRuntime.Register(
                Assembly.GetExecutingAssembly(),
                "WinFormsXaml.Tests.Fixtures.RollbackCard.xml");

            RollbackProbePanel.CreatedCount = 0;
            RollbackProbePanel.DisposedCount = 0;
            Exception failure = null;

            try
            {
                XamlRuntime.Load(
                    "<Panel>" +
                    "  <RollbackCard Name='Duplicate' />" +
                    "  <RollbackCard Name='Duplicate' />" +
                    "</Panel>");
            }
            catch (Exception ex)
            {
                failure = ex;
            }

            AssertTrue(failure != null, "duplicate component name fails load");
            AssertTrue(
                failure.ToString().IndexOf(
                    "Duplicate XAML name",
                    StringComparison.Ordinal) >= 0,
                "primary component load failure is preserved");
            AssertEqual(
                RollbackProbePanel.CreatedCount,
                RollbackProbePanel.DisposedCount,
                "native component roots dispose despite code-behind cleanup failure");
        }

        private static void TestExternalRootDisposalReleasesRuntimeTree()
        {
            CodeBehindCard.Reset();
            XamlRuntime runtime = XamlRuntime.Load(
                "<Panel>" +
                "  <CodeBehindCard Name='ExternalCard' Title='External' Count='1'>" +
                "    <Label Name='ExternalCaller' Text='Caller' />" +
                "  </CodeBehindCard>" +
                "</Panel>");
            CodeBehindCard instance = CodeBehindCard.GetInstance(0);

            try
            {
                Panel root = runtime.Get<Panel>("ExternalCard");
                Button eventTarget =
                    FindControl<Button>(root, "ClassButton");

                AssertTrue(
                    HasBoundEventTarget(runtime, eventTarget),
                    "component event is tracked before external disposal");

                root.Dispose();

                AssertTrue(
                    instance.Disposed,
                    "external root disposal releases code-behind");
                AssertEqual(
                    1,
                    instance.DisposeCount,
                    "external root releases code-behind once");
                AssertTrue(
                    !runtime.NamedObjects.ContainsKey("ExternalCard"),
                    "external root disposal removes its registered name");
                AssertTrue(
                    !runtime.NamedObjects.ContainsKey("ExternalCaller"),
                    "external root disposal removes descendant names");
                AssertTrue(
                    !HasBoundEventTarget(runtime, eventTarget),
                    "external root disposal removes bound event tracking");
                AssertEqual(
                    0,
                    GetBoundEventCount(runtime),
                    "external root disposal releases event delegates");
                AssertEqual(
                    0,
                    GetPrivateCollectionCount(
                        runtime,
                        "_componentInstances"),
                    "external root disposal removes the component state");
                AssertEqual(
                    0,
                    GetPrivateCollectionCount(
                        runtime,
                        "_componentInstancesByRoot"),
                    "external root disposal removes the root identity index");
            }
            finally
            {
                runtime.Dispose();
            }

            AssertEqual(
                1,
                instance.DisposeCount,
                "runtime disposal does not release an external root twice");
        }

        private static void TestNoSlotAllowsUnrelatedChildrenMember()
        {
            XamlRuntime.Register(
                Assembly.GetExecutingAssembly(),
                "WinFormsXaml.Tests.Fixtures.NoSlotChildrenCard.xml");
            NoSlotChildrenCodeBehind.LastInstance = null;
            XamlRuntime runtime = XamlRuntime.Load(
                "<NoSlotChildrenCard Name='NoSlotCard' />");

            try
            {
                AssertTrue(
                    NoSlotChildrenCodeBehind.LastInstance != null,
                    "no-slot component creates its code-behind");
                AssertEqual(
                    "domain children",
                    NoSlotChildrenCodeBehind.LastInstance.Children,
                    "no-slot component leaves an unrelated Children member alone");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestProjectedNestedRootDisposalUpdatesOwners()
        {
            CodeBehindCard.Reset();
            XamlRuntime runtime = XamlRuntime.Load(
                "<CodeBehindCard Name='OuterCard' Title='Outer' Count='1'>" +
                "  <CodeBehindCard Name='InnerCard' Title='Inner' Count='2' />" +
                "</CodeBehindCard>");
            CodeBehindCard outer = CodeBehindCard.GetInstance(0);
            CodeBehindCard inner = CodeBehindCard.GetInstance(1);
            int changedCount = 0;
            EventHandler changed = delegate(object sender, EventArgs e)
            {
                changedCount++;
            };

            try
            {
                Panel outerRoot = runtime.Get<Panel>("OuterCard");
                Panel innerRoot = runtime.Get<Panel>("InnerCard");
                outer.Children.Changed += changed;

                AssertEqual(
                    1,
                    outer.Children.Count,
                    "outer ChildrenBind starts with the nested component root");
                AssertTrue(
                    HasLogicalChild(runtime, outerRoot, innerRoot),
                    "nested projected component starts in logical ownership");

                innerRoot.Dispose();

                AssertEqual(
                    1,
                    inner.DisposeCount,
                    "direct nested-root disposal releases its code-behind once");
                AssertTrue(
                    !outer.Disposed,
                    "direct nested-root disposal keeps the outer component alive");
                AssertEqual(
                    0,
                    outer.Children.Count,
                    "direct nested-root disposal updates ChildrenBind");
                AssertEqual(
                    1,
                    changedCount,
                    "direct nested-root disposal publishes one children change");
                AssertTrue(
                    !HasLogicalChild(runtime, outerRoot, innerRoot),
                    "direct nested-root disposal removes stale logical ownership");
            }
            finally
            {
                outer.Children.Changed -= changed;
                runtime.Dispose();
            }

            AssertEqual(
                1,
                inner.DisposeCount,
                "runtime disposal does not release the nested code-behind twice");
            AssertEqual(
                1,
                outer.DisposeCount,
                "runtime disposal still releases the outer code-behind");
        }

        private sealed class ComponentHostState
        {
            public readonly PropertyBinding<string> Title =
                new PropertyBinding<string>("Initial");
            public readonly PropertyBinding<int> Count =
                new PropertyBinding<int>(1);
        }

        private static T FindControl<T>(
            Control root,
            string name) where T : Control
        {
            if (root != null &&
                String.Equals(
                    root.Name,
                    name,
                    StringComparison.OrdinalIgnoreCase))
            {
                T rootMatch = root as T;

                if (rootMatch != null)
                    return rootMatch;
            }

            int i;

            for (i = 0; root != null && i < root.Controls.Count; i++)
            {
                T match = FindControl<T>(root.Controls[i], name);

                if (match != null)
                    return match;
            }

            return null;
        }

        private static bool HasBoundEventTarget(
            XamlRuntime runtime,
            object target)
        {
            FieldInfo field = typeof(XamlRuntime).GetField(
                "_boundEventsByTarget",
                BindingFlags.Instance | BindingFlags.NonPublic);
            IDictionary targets = field == null
                ? null
                : field.GetValue(runtime) as IDictionary;

            return targets != null && targets.Contains(target);
        }

        private static int GetBoundEventCount(XamlRuntime runtime)
        {
            FieldInfo field = typeof(XamlRuntime).GetField(
                "_boundEvents",
                BindingFlags.Instance | BindingFlags.NonPublic);
            ArrayList events = field == null
                ? null
                : field.GetValue(runtime) as ArrayList;

            return events == null ? 0 : events.Count;
        }

        private static int GetPrivateCollectionCount(
            XamlRuntime runtime,
            string fieldName)
        {
            FieldInfo field = typeof(XamlRuntime).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            ICollection collection = field == null
                ? null
                : field.GetValue(runtime) as ICollection;

            return collection == null ? 0 : collection.Count;
        }

        private static bool HasLogicalChild(
            XamlRuntime runtime,
            object parent,
            object child)
        {
            FieldInfo infosField = typeof(XamlRuntime).GetField(
                "_elementInfos",
                BindingFlags.Instance | BindingFlags.NonPublic);
            IDictionary infos = infosField == null
                ? null
                : infosField.GetValue(runtime) as IDictionary;
            object info = infos == null ? null : infos[parent];
            FieldInfo childrenField = info == null
                ? null
                : info.GetType().GetField(
                    "LogicalChildren",
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);
            ArrayList children = childrenField == null
                ? null
                : childrenField.GetValue(info) as ArrayList;
            int i;

            for (i = 0; children != null && i < children.Count; i++)
            {
                if (Object.ReferenceEquals(children[i], child))
                    return true;
            }

            return false;
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException("Assertion failed: " + message);
        }

        private static void AssertSame(
            object expected,
            object actual,
            string message)
        {
            if (!Object.ReferenceEquals(expected, actual))
            {
                throw new InvalidOperationException(
                    "Assertion failed: " + message + ".");
            }
        }

        private static void AssertEqual(
            object expected,
            object actual,
            string message)
        {
            if (!Object.Equals(expected, actual))
            {
                throw new InvalidOperationException(
                    "Assertion failed: " +
                    message +
                    ". Expected '" +
                    expected +
                    "', actual '" +
                    actual +
                    "'.");
            }
        }
    }
}
