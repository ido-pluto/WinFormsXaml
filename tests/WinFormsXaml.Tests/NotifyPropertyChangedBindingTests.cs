using System;
using System.Collections;
using System.ComponentModel;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using WinFormsXaml;

namespace WinFormsXaml.Tests
{
    internal static class NotifyPropertyChangedBindingTests
    {
        private abstract class NotifyObject : INotifyPropertyChanged
        {
            private PropertyChangedEventHandler _propertyChanged;

            public event PropertyChangedEventHandler PropertyChanged
            {
                add { _propertyChanged += value; }
                remove { _propertyChanged -= value; }
            }

            public int SubscriberCount
            {
                get
                {
                    return _propertyChanged == null
                        ? 0
                        : _propertyChanged.GetInvocationList().Length;
                }
            }

            protected void RaisePropertyChanged(string propertyName)
            {
                PropertyChangedEventHandler handler = _propertyChanged;

                if (handler != null)
                {
                    handler(
                        this,
                        new PropertyChangedEventArgs(propertyName));
                }
            }
        }

        private sealed class NotifyAddress : NotifyObject
        {
            private string _city;

            public NotifyAddress(string city)
            {
                _city = city;
            }

            public string City
            {
                get { return _city; }
                set
                {
                    if (String.Equals(
                            _city,
                            value,
                            StringComparison.Ordinal))
                    {
                        return;
                    }

                    _city = value;
                    RaisePropertyChanged("City");
                }
            }
        }

        private sealed class NotifyCustomer : NotifyObject
        {
            private NotifyAddress _address;

            public NotifyCustomer(NotifyAddress address)
            {
                _address = address;
            }

            public NotifyAddress Address
            {
                get { return _address; }
                set
                {
                    if (Object.ReferenceEquals(_address, value))
                        return;

                    _address = value;
                    RaisePropertyChanged("Address");
                }
            }
        }

        private sealed class NotifyRow : NotifyObject
        {
            private string _title;

            public NotifyRow(string title)
            {
                _title = title;
            }

            public string Title
            {
                get { return _title; }
                set
                {
                    if (String.Equals(
                            _title,
                            value,
                            StringComparison.Ordinal))
                    {
                        return;
                    }

                    _title = value;
                    RaisePropertyChanged("Title");
                }
            }

            public int Version
            {
                get { return 1; }
            }
        }

        private sealed class NotifyState : NotifyObject
        {
            private string _header;
            private string _secondary;
            private string _normalized;
            private string _sharedText;
            private bool _canOpen;
            private NotifyCustomer _customer;
            private IEnumerable _rows;
            private int _secondaryReadCount;

            public NotifyState()
            {
                _header = "Initial header";
                _secondary = "Initial secondary";
                _normalized = "Initial edit";
                _sharedText = "Shared initial";
                _canOpen = true;
            }

            public string Header
            {
                get { return _header; }
                set
                {
                    if (String.Equals(
                            _header,
                            value,
                            StringComparison.Ordinal))
                    {
                        return;
                    }

                    _header = value;
                    RaisePropertyChanged("Header");
                }
            }

            public string Secondary
            {
                get
                {
                    _secondaryReadCount++;
                    return _secondary;
                }
                set
                {
                    if (String.Equals(
                            _secondary,
                            value,
                            StringComparison.Ordinal))
                    {
                        return;
                    }

                    _secondary = value;
                    RaisePropertyChanged("Secondary");
                }
            }

            public int SecondaryReadCount
            {
                get { return _secondaryReadCount; }
            }

            public string Normalized
            {
                get { return _normalized; }
                set
                {
                    string normalized = value == null
                        ? null
                        : value.Trim();

                    if (String.Equals(
                            _normalized,
                            normalized,
                            StringComparison.Ordinal))
                    {
                        return;
                    }

                    _normalized = normalized;
                    RaisePropertyChanged("Normalized");
                }
            }

            public string SharedText
            {
                get { return _sharedText; }
                set
                {
                    if (String.Equals(
                            _sharedText,
                            value,
                            StringComparison.Ordinal))
                    {
                        return;
                    }

                    _sharedText = value;
                    RaisePropertyChanged("SharedText");
                }
            }

            public bool CanOpen
            {
                get { return _canOpen; }
                set
                {
                    if (_canOpen == value)
                        return;

                    _canOpen = value;
                    RaisePropertyChanged("CanOpen");
                }
            }

            public NotifyCustomer Customer
            {
                get { return _customer; }
                set
                {
                    if (Object.ReferenceEquals(_customer, value))
                        return;

                    _customer = value;
                    RaisePropertyChanged("Customer");
                }
            }

            public IEnumerable Rows
            {
                get { return _rows; }
                set
                {
                    if (Object.ReferenceEquals(_rows, value))
                        return;

                    _rows = value;
                    RaisePropertyChanged("Rows");
                }
            }

            public void SetAllSilently(
                string header,
                string secondary)
            {
                _header = header;
                _secondary = secondary;
            }

            public void RaiseAll()
            {
                RaisePropertyChanged(String.Empty);
            }
        }

        private sealed class PlainState
        {
            private string _text;

            public PlainState()
            {
                _text = "Plain";
            }

            public string Text
            {
                get { return _text; }
                set { _text = value; }
            }
        }

        private sealed class PlainVirtualState
        {
            public bool CanOpen;
            public IEnumerable Rows;
        }

        public static void Run()
        {
            TestOneWayFilteringWildcardAndWorkerDispatch();
            TestNonReactiveNonControlRootAvoidsDispatcher();
            TestNonControlRootOneWayAndTwoWayDispatch();
            TestControlRootHandleRecreationRetainsBindingDebt();
            TestNestedReplacementAndDisposal();
            TestNestedReplacementIgnoresStaleBranchForTargetReplay();
            TestTwoWaySetterNormalization();
            TestItemAndItemsSourceNotifications();
            TestItemTemplateCodeBehindSource();
            TestRootConditionFallbackUsesCodeBehindSource();
            TestKeyedForceReloadReevaluatesCodeBehindCondition();
            TestTwoWayRequiresObservableSource();
        }

        private static void TestOneWayFilteringWildcardAndWorkerDispatch()
        {
            NotifyState state = new NotifyState();
            XamlRuntime runtime = XamlRuntime.Load(
                "<FlowLayoutPanel>" +
                "  <Label Name='Header' Text='{Binding Header}' />" +
                "  <Label Name='Secondary' Text='{Binding Secondary}' />" +
                "</FlowLayoutPanel>",
                state);

            try
            {
                Label header = runtime.Get<Label>("Header");
                Label secondary = runtime.Get<Label>("Secondary");
                CreateHandleAndDrain(runtime.RootControl);
                int secondaryReads = state.SecondaryReadCount;

                state.Header = "Changed header";
                Drain(runtime.RootControl);
                AssertEqual(
                    "Changed header",
                    header.Text,
                    "a named property notification refreshes its target");
                AssertEqual(
                    secondaryReads,
                    state.SecondaryReadCount,
                    "a named property notification does not read unrelated members");

                state.SetAllSilently("Wildcard header", "Wildcard secondary");
                state.RaiseAll();
                Drain(runtime.RootControl);
                AssertEqual(
                    "Wildcard header",
                    header.Text,
                    "an empty property name refreshes the first target");
                AssertEqual(
                    "Wildcard secondary",
                    secondary.Text,
                    "an empty property name refreshes every target");

                Thread worker = new Thread(
                    new ThreadStart(
                        delegate
                        {
                            state.Header = "Worker header";
                        }));

                worker.Start();
                worker.Join();
                AssertEqual(
                    "Wildcard header",
                    header.Text,
                    "a worker notification waits for UI dispatch");
                Drain(runtime.RootControl);
                AssertEqual(
                    "Worker header",
                    header.Text,
                    "a worker notification is applied on the UI dispatch path");
            }
            finally
            {
                runtime.Dispose();
            }

            AssertEqual(
                0,
                state.SubscriberCount,
                "disposing the runtime detaches a notifying root source");
        }

        private static void TestNonControlRootOneWayAndTwoWayDispatch()
        {
            int ownerThreadId = Thread.CurrentThread.ManagedThreadId;
            XamlRuntime.Register(
                "NotifyNonControlRoot",
                typeof(NotifyNonControlRoot));

            NotifyState state = new NotifyState();
            XamlRuntime runtime = XamlRuntime.Load(
                "<NotifyNonControlRoot " +
                "Text='{Binding Header, Mode=TwoWay}' />",
                state);
            NotifyNonControlRoot root =
                runtime.Root as NotifyNonControlRoot;

            try
            {
                AssertTrue(root != null, "reactive non-Control root loads");
                AssertTrue(
                    runtime.RootControl == null,
                    "reactive root stays outside the Control hierarchy");
                AssertEqual(
                    "Initial header",
                    root.Text,
                    "non-Control root receives its initial binding value");

                state.Header = "Same-thread source";
                AssertEqual(
                    "Same-thread source",
                    root.Text,
                    "non-Control source changes drain synchronously on the owner thread");

                root.Text = "Two-way target";
                AssertEqual(
                    "Two-way target",
                    state.Header,
                    "non-Control target changes update the source");

                Thread worker = new Thread(
                    new ThreadStart(
                        delegate
                        {
                            state.Header = "Worker source";
                        }));

                worker.Start();
                worker.Join();
                AssertEqual(
                    "Two-way target",
                    root.Text,
                    "worker changes wait for the owner-thread dispatcher");

                int iterations = 0;

                while (!String.Equals(
                           root.Text,
                           "Worker source",
                           StringComparison.Ordinal) &&
                       iterations < 1024)
                {
                    Application.DoEvents();
                    iterations++;
                }

                AssertEqual(
                    "Worker source",
                    root.Text,
                    "worker changes reach a non-Control root on its owner thread");
                AssertEqual(
                    ownerThreadId,
                    root.LastSetThreadId,
                    "a rootless worker update runs on the load thread");

                EventHandler cascade = null;
                cascade = delegate
                {
                    if (String.Equals(
                            root.Text,
                            "First cascade",
                            StringComparison.Ordinal))
                    {
                        state.Header = "Second cascade";
                    }
                };
                root.TextSet += cascade;
                int setCountBeforeCascade = root.SetCount;
                state.Header = "First cascade";
                root.TextSet -= cascade;

                AssertEqual(
                    "Second cascade",
                    root.Text,
                    "rootless reentrant source work drains to a stable value");
                AssertEqual(
                    setCountBeforeCascade + 2,
                    root.SetCount,
                    "rootless reentrant work uses two batches");
                AssertEqual(
                    1,
                    root.MaxSetDepth,
                    "rootless reentrant work is pumped without recursive setters");

                Exception workerDisposeFailure = null;
                Thread disposeWorker = new Thread(
                    new ThreadStart(
                        delegate
                        {
                            try
                            {
                                runtime.Dispose();
                            }
                            catch (Exception ex)
                            {
                                workerDisposeFailure = ex;
                            }
                        }));

                disposeWorker.Start();
                disposeWorker.Join();
                AssertTrue(
                    workerDisposeFailure is InvalidOperationException,
                    "wrong-thread rootless disposal is rejected");
                AssertEqual(
                    false,
                    runtime.IsDisposed,
                    "rejected disposal does not partially dispose the runtime");

                state.Header = "After rejected dispose";
                AssertEqual(
                    "After rejected dispose",
                    root.Text,
                    "owner-thread use continues after rejected disposal");
            }
            finally
            {
                runtime.Dispose();
            }

            AssertEqual(
                0,
                state.SubscriberCount,
                "disposing a non-Control runtime detaches its source");

            FieldInfo dispatcherField =
                typeof(XamlRuntime).GetField(
                    "_observableRootlessDispatcher",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            AssertTrue(
                dispatcherField != null,
                "rootless dispatcher field remains discoverable");
            AssertEqual(
                null,
                dispatcherField.GetValue(runtime),
                "owner-thread disposal releases the private dispatcher");

            int detachedSetCount = root.SetCount;
            state.Header = "Detached source";
            AssertEqual(
                detachedSetCount,
                root.SetCount,
                "disposing a non-Control runtime stops source updates");

            root.Text = "Detached target";
            AssertEqual(
                "Detached source",
                state.Header,
                "disposing a non-Control runtime detaches its target event");
        }

        private static void TestNonReactiveNonControlRootAvoidsDispatcher()
        {
            XamlRuntime.Register(
                "NotifyNonReactiveRoot",
                typeof(NotifyNonControlRoot));
            XamlRuntime runtime = XamlRuntime.Load(
                "<NotifyNonReactiveRoot Text='Static' />");

            try
            {
                FieldInfo dispatcherField =
                    typeof(XamlRuntime).GetField(
                        "_observableRootlessDispatcher",
                        BindingFlags.Instance | BindingFlags.NonPublic);

                AssertTrue(
                    dispatcherField != null,
                    "rootless dispatcher field exists");
                AssertEqual(
                    null,
                    dispatcherField.GetValue(runtime),
                    "a nonreactive non-Control root allocates no dispatcher");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestControlRootHandleRecreationRetainsBindingDebt()
        {
            XamlRuntime.Register(
                "NotifyRecreatingControl",
                typeof(NotifyRecreatingControl));
            NotifyState state = new NotifyState();
            XamlRuntime runtime = XamlRuntime.Load(
                "<NotifyRecreatingControl Text='{Binding Header}' />",
                state);

            try
            {
                NotifyRecreatingControl root =
                    runtime.RootControl as NotifyRecreatingControl;

                AssertTrue(root != null, "recreating Control root loads");
                CreateHandleAndDrain(root);
                int changesBefore = root.TextChangeCount;

                state.Header = "Queued across recreation";
                root.RecreateNativeHandle();
                Drain(root);

                AssertEqual(
                    "Queued across recreation",
                    root.Text,
                    "binding debt survives root handle recreation");
                AssertEqual(
                    changesBefore + 1,
                    root.TextChangeCount,
                    "a replacement handle applies pending binding work once");

                Exception workerDisposeFailure = null;
                Thread disposeWorker = new Thread(
                    new ThreadStart(
                        delegate
                        {
                            try
                            {
                                runtime.Dispose();
                            }
                            catch (Exception ex)
                            {
                                workerDisposeFailure = ex;
                            }
                        }));

                disposeWorker.Start();
                disposeWorker.Join();
                AssertTrue(
                    workerDisposeFailure is InvalidOperationException,
                    "wrong-thread Control-root disposal is rejected");
                AssertEqual(
                    false,
                    runtime.IsDisposed,
                    "rejected Control-root disposal leaves the runtime usable");

                state.Header = "After rejected Control disposal";
                Drain(root);
                AssertEqual(
                    "After rejected Control disposal",
                    root.Text,
                    "Control-root bindings survive rejected disposal");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestNestedReplacementAndDisposal()
        {
            NotifyAddress firstAddress = new NotifyAddress("First city");
            NotifyCustomer firstCustomer =
                new NotifyCustomer(firstAddress);
            NotifyState state = new NotifyState();
            state.Customer = firstCustomer;
            XamlRuntime runtime = XamlRuntime.Load(
                "<Label Name='City' Text='{Binding Customer.Address.City}' />",
                state);
            NotifyAddress secondAddress =
                new NotifyAddress("Second city");
            NotifyAddress thirdAddress =
                new NotifyAddress("Third city");
            NotifyCustomer secondCustomer =
                new NotifyCustomer(thirdAddress);

            try
            {
                Label city = runtime.Get<Label>("City");
                CreateHandleAndDrain(runtime.RootControl);
                AssertEqual(1, state.SubscriberCount, "root path subscription pooled");
                AssertEqual(1, firstCustomer.SubscriberCount, "nested owner subscribed");
                AssertEqual(1, firstAddress.SubscriberCount, "terminal owner subscribed");

                firstCustomer.Address = secondAddress;
                Drain(runtime.RootControl);
                AssertEqual("Second city", city.Text, "an intermediate property rebinds");
                AssertEqual(0, firstAddress.SubscriberCount, "old terminal detached");
                AssertEqual(1, secondAddress.SubscriberCount, "new terminal attached");

                state.Customer = secondCustomer;
                Drain(runtime.RootControl);
                AssertEqual("Third city", city.Text, "the root endpoint rebinds");
                AssertEqual(0, firstCustomer.SubscriberCount, "old owner detached");
                AssertEqual(0, secondAddress.SubscriberCount, "old branch detached");
                AssertEqual(1, secondCustomer.SubscriberCount, "new owner attached");
                AssertEqual(1, thirdAddress.SubscriberCount, "new branch attached");
            }
            finally
            {
                runtime.Dispose();
            }

            AssertEqual(0, state.SubscriberCount, "root detached on disposal");
            AssertEqual(0, secondCustomer.SubscriberCount, "owner detached on disposal");
            AssertEqual(0, thirdAddress.SubscriberCount, "terminal detached on disposal");
        }

        private static void TestTwoWaySetterNormalization()
        {
            NotifyState state = new NotifyState();
            XamlRuntime runtime = XamlRuntime.Load(
                "<TextBox Name='Editor' " +
                "Text='{Binding Normalized, Mode=TwoWay}' />",
                state);

            try
            {
                TextBox editor = runtime.Get<TextBox>("Editor");
                CreateHandleAndDrain(runtime.RootControl);

                state.Normalized = "Source edit";
                Drain(runtime.RootControl);
                AssertEqual(
                    "Source edit",
                    editor.Text,
                    "a normal source property updates a two-way target");

                editor.Text = "  Target edit  ";
                Drain(runtime.RootControl);
                AssertEqual(
                    "Target edit",
                    state.Normalized,
                    "a target edit writes the normal source property");
                AssertEqual(
                    "Target edit",
                    editor.Text,
                    "a normalizing setter reconciles the target");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void
            TestNestedReplacementIgnoresStaleBranchForTargetReplay()
        {
            NotifyAddress firstAddress =
                new NotifyAddress("First city");
            NotifyCustomer firstCustomer =
                new NotifyCustomer(firstAddress);
            NotifyAddress replacementAddress =
                new NotifyAddress("Replacement city");
            NotifyCustomer replacementCustomer =
                new NotifyCustomer(replacementAddress);
            NotifyState state = new NotifyState();
            state.Customer = firstCustomer;
            XamlRuntime runtime = XamlRuntime.Load(
                "<TextBox Name='CityEditor' " +
                "Text='{Binding Customer.Address.City, Mode=TwoWay}' />",
                state);

            try
            {
                TextBox editor = runtime.Get<TextBox>("CityEditor");
                CreateHandleAndDrain(runtime.RootControl);

                state.Customer = replacementCustomer;
                editor.Text = "Edit for replacement";
                firstAddress.City = "Stale old-branch city";
                Drain(runtime.RootControl);

                AssertEqual(
                    "Stale old-branch city",
                    firstAddress.City,
                    "the stale endpoint keeps its own later source edit");
                AssertEqual(
                    "Edit for replacement",
                    replacementAddress.City,
                    "a stale old-branch signal cannot discard a newer target edit");
                AssertEqual(
                    "Edit for replacement",
                    editor.Text,
                    "the replacement endpoint republishes the preserved target edit");
                AssertEqual(
                    0,
                    firstCustomer.SubscriberCount,
                    "the replaced path owner detaches after replay");
                AssertEqual(
                    0,
                    firstAddress.SubscriberCount,
                    "the stale endpoint detaches after replay");
                AssertEqual(
                    1,
                    replacementCustomer.SubscriberCount,
                    "the replacement path owner subscribes once");
                AssertEqual(
                    1,
                    replacementAddress.SubscriberCount,
                    "the replacement endpoint subscribes once");
            }
            finally
            {
                runtime.Dispose();
            }

            AssertEqual(0, state.SubscriberCount, "replay root detached on disposal");
            AssertEqual(
                0,
                replacementCustomer.SubscriberCount,
                "replay owner detached on disposal");
            AssertEqual(
                0,
                replacementAddress.SubscriberCount,
                "replay endpoint detached on disposal");
        }

        private static void TestItemAndItemsSourceNotifications()
        {
            NotifyRow first = new NotifyRow("First row");
            NotifyRow second = new NotifyRow("Second row");
            NotifyState state = new NotifyState();
            state.Rows = new object[] { first };
            XamlRuntime runtime = XamlRuntime.Load(
                "<ItemsControl Name='Rows' ItemsSource='{Binding Rows}'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Button Text='{Binding Title}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>",
                state);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.Get<XamlRuntime.ItemsControl>("Rows");
                CreateHandleAndDrain(runtime.RootControl);
                Button row = GetOnlyButton(host);
                AssertEqual("First row", row.Text, "initial notifying item value");

                first.Title = "Updated first row";
                Drain(runtime.RootControl);
                row = GetOnlyButton(host);
                AssertEqual(
                    "Updated first row",
                    row.Text,
                    "a notifying item patches its realized control");

                state.Rows = new object[] { second };
                Drain(runtime.RootControl);
                row = GetOnlyButton(host);
                AssertEqual(
                    "Second row",
                    row.Text,
                    "a notifying ItemsSource property replaces an array");
                AssertEqual(0, first.SubscriberCount, "removed item detached");
                AssertEqual(1, second.SubscriberCount, "replacement item attached");
            }
            finally
            {
                runtime.Dispose();
            }

            AssertEqual(0, state.SubscriberCount, "items source owner detached");
            AssertEqual(0, second.SubscriberCount, "realized item detached");
        }

        private static void TestTwoWayRequiresObservableSource()
        {
            bool rejected = false;

            try
            {
                XamlRuntime runtime = XamlRuntime.Load(
                    "<TextBox Text='{Binding Text, Mode=TwoWay}' />",
                    new PlainState());
                runtime.Dispose();
            }
            catch (InvalidOperationException ex)
            {
                rejected =
                    ex.Message.IndexOf(
                        "notifying CLR property",
                        StringComparison.Ordinal) >= 0;
            }

            AssertTrue(
                rejected,
                "two-way normal properties require INotifyPropertyChanged");
        }

        private static void TestItemTemplateCodeBehindSource()
        {
            NotifyRow first = new NotifyRow("First");
            NotifyRow second = new NotifyRow("Second");
            NotifyRow replacement = new NotifyRow("Replacement");
            NotifyState state = new NotifyState();
            state.Rows = new object[] { first, second };
            XamlRuntime runtime = XamlRuntime.Load(
                "<ItemsControl Name='Rows' ItemsSource='{Binding Rows}' " +
                "    Virtualizing='false' ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <FlowLayoutPanel>" +
                "      <Label Name='CurrentTitle' " +
                "          Text='{Binding Title, Source=cUrReNt}' />" +
                "      <Label Name='Summary' " +
                "          Text='{Binding Title}: {Binding Header, Source=CodeBehind}' />" +
                "      <Button Name='OpenButton' Text='{Binding Title}' " +
                "          Enabled='{Binding CanOpen, Source=codebehind}' />" +
                "      <TextBox Name='SharedEditor' " +
                "          Text='{Binding SharedText, Mode=TwoWay, Source=CodeBehind}' />" +
                "    </FlowLayoutPanel>" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>",
                state);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.Get<XamlRuntime.ItemsControl>("Rows");
                CreateHandleAndDrain(runtime.RootControl);
                FlowLayoutPanel firstRow = GetItemRow(host, 0);
                FlowLayoutPanel secondRow = GetItemRow(host, 1);
                Label firstCurrent = GetChild<Label>(firstRow, "CurrentTitle");
                Label firstSummary = GetChild<Label>(firstRow, "Summary");
                Button firstButton = GetChild<Button>(firstRow, "OpenButton");
                Button secondButton = GetChild<Button>(secondRow, "OpenButton");
                TextBox firstEditor = GetChild<TextBox>(firstRow, "SharedEditor");
                TextBox secondEditor = GetChild<TextBox>(secondRow, "SharedEditor");

                AssertEqual("First", firstCurrent.Text, "explicit Current source uses the item");
                AssertEqual(
                    "First: Initial header",
                    firstSummary.Text,
                    "interpolation can combine item and code-behind sources");
                AssertEqual(true, firstButton.Enabled, "initial code-behind boolean");
                AssertEqual(
                    "Shared initial",
                    secondEditor.Text,
                    "initial shared two-way code-behind value");
                AssertEqual(
                    1,
                    state.SubscriberCount,
                    "item and code-behind paths pool one source handler");
                AssertEqual(1, first.SubscriberCount, "first item source is pooled");
                AssertEqual(1, second.SubscriberCount, "second item source is pooled");

                state.CanOpen = false;
                state.Header = "Changed header";
                Drain(runtime.RootControl);
                AssertEqual(false, firstButton.Enabled, "first row observes code-behind state");
                AssertEqual(false, secondButton.Enabled, "second row observes code-behind state");
                AssertEqual(
                    "First: Changed header",
                    firstSummary.Text,
                    "code-behind interpolation refreshes without an item change");
                AssertEqual("First", firstCurrent.Text, "unrelated item binding remains stable");

                firstEditor.Text = "Edited in first row";
                Drain(runtime.RootControl);
                AssertEqual(
                    "Edited in first row",
                    state.SharedText,
                    "item target writes a two-way code-behind property");
                AssertEqual(
                    "Edited in first row",
                    secondEditor.Text,
                    "shared code-behind edit reaches sibling item targets");

                first.Title = "Updated first";
                Drain(runtime.RootControl);
                AssertEqual(
                    "Updated first: Changed header",
                    firstSummary.Text,
                    "item changes retain the selected code-behind segment");

                state.Rows = new object[] { replacement };
                Drain(runtime.RootControl);
                FlowLayoutPanel replacementRow = GetItemRow(host, 0);
                Button replacementButton =
                    GetChild<Button>(replacementRow, "OpenButton");
                AssertEqual(false, replacementButton.Enabled, "replacement row uses current code-behind state");
                AssertEqual(0, first.SubscriberCount, "replaced first item detaches");
                AssertEqual(0, second.SubscriberCount, "replaced second item detaches");
                AssertEqual(1, replacement.SubscriberCount, "replacement item attaches once");

                state.CanOpen = true;
                Drain(runtime.RootControl);
                AssertEqual(true, replacementButton.Enabled, "replacement row remains reactive");
            }
            finally
            {
                runtime.Dispose();
            }

            AssertEqual(0, state.SubscriberCount, "code-behind source detaches on disposal");
            AssertEqual(0, replacement.SubscriberCount, "replacement item detaches on disposal");
        }

        private static void TestRootConditionFallbackUsesCodeBehindSource()
        {
            NotifyState state = new NotifyState();
            NotifyRow[] rows = new NotifyRow[]
            {
                new NotifyRow("One"),
                new NotifyRow("Two"),
                new NotifyRow("Three"),
                new NotifyRow("Four"),
                new NotifyRow("Five"),
                new NotifyRow("Six")
            };
            state.Rows = rows;
            XamlRuntime runtime = XamlRuntime.Load(
                "<ItemsControl Name='Rows' ItemsSource='{Binding Rows}' " +
                "    Width='180' Height='60' AutoScroll='true' " +
                "    Virtualizing='true' VirtualizationThreshold='1' " +
                "    OverscanItems='0' FixedItemSize='20' " +
                "    ItemVersionPath='Version' " +
                "    ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label Condition='{Binding CanOpen, Source=CodeBehind}' " +
                "        Text='{Binding Title}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>",
                state);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.Get<XamlRuntime.ItemsControl>("Rows");
                CreateHandleAndDrain(runtime.RootControl);
                AssertTrue(
                    !host.IsVirtualizing,
                    "root Condition selects the normal keyed renderer");
                AssertTrue(
                    host.RealizedCount == rows.Length,
                    "keyed fallback realizes every row");
                AssertEqual(
                    1,
                    state.SubscriberCount,
                    "keyed item and ItemsSource bindings pool the code-behind source");
                Label original = GetFirstRealizedLabel(host);
                AssertTrue(original.Visible, "true shared condition shows rows");

                state.CanOpen = false;
                Drain(runtime.RootControl);
                AssertEqual(
                    rows.Length,
                    host.RealizedCount,
                    "a shared false condition retains keyed row controls");
                AssertTrue(
                    !original.IsDisposed && !original.Visible,
                    "a shared false condition hides the retained row");
                AssertEqual(
                    1,
                    state.SubscriberCount,
                    "the keyed condition stays subscribed while rows are hidden");

                state.CanOpen = true;
                Drain(runtime.RootControl);
                AssertTrue(
                    host.RealizedCount == rows.Length,
                    "a shared true condition keeps all keyed rows");
                AssertEqual(
                    "One",
                    GetFirstRealizedLabel(host).Text,
                    "re-realized virtual rows retain their current-item source");
                AssertTrue(
                    Object.ReferenceEquals(
                        original,
                        GetFirstRealizedLabel(host)),
                    "condition invalidation reuses the keyed control");
                AssertTrue(original.Visible, "true shared condition shows the row again");

                state.CanOpen = false;
                state.CanOpen = true;
                Drain(runtime.RootControl);
                AssertTrue(
                    original.Visible,
                    "coalesced condition notifications retain the newest visibility state");
            }
            finally
            {
                runtime.Dispose();
            }

            AssertEqual(
                0,
                state.SubscriberCount,
                "keyed code-behind condition detaches on disposal");
        }

        private static void
            TestKeyedForceReloadReevaluatesCodeBehindCondition()
        {
            PlainVirtualState state = new PlainVirtualState();
            state.CanOpen = false;
            state.Rows = new NotifyRow[]
            {
                new NotifyRow("One"),
                new NotifyRow("Two")
            };
            XamlRuntime runtime = XamlRuntime.Load(
                "<ItemsControl Name='Rows' ItemsSource='{Binding Rows}' " +
                "    Width='180' Height='60' AutoScroll='true' " +
                "    Virtualizing='true' VirtualizationThreshold='1' " +
                "    ItemVersionPath='Version' OverscanItems='0' " +
                "    FixedItemSize='20' ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label Condition='{Binding CanOpen, Source=CodeBehind}' " +
                "        Text='{Binding Title}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>",
                state);

            try
            {
                XamlRuntime.ItemsControl host =
                    runtime.Get<XamlRuntime.ItemsControl>("Rows");
                CreateHandleAndDrain(runtime.RootControl);
                AssertTrue(
                    !host.IsVirtualizing,
                    "plain root Condition selects keyed fallback");
                AssertEqual(
                    2,
                    host.RealizedCount,
                    "keyed fallback retains both rows");
                Label initial = GetFirstRealizedLabel(host);
                AssertTrue(!initial.Visible, "initial plain condition is false");

                state.CanOpen = true;
                host.ReloadItems();
                AssertTrue(
                    !initial.Visible,
                    "ordinary reload honors the unchanged item version token");

                host.ForceReloadItems();
                AssertTrue(
                    GetFirstRealizedLabel(host).Visible,
                    "forced reload reevaluates keyed root visibility");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static Button GetOnlyButton(XamlRuntime.ItemsControl host)
        {
            AssertTrue(host != null, "items host exists");
            AssertEqual(1, host.RealizedCount, "items host has one row");
            int i;

            for (i = 0; i < host.Controls.Count; i++)
            {
                Button button = host.Controls[i] as Button;

                if (button != null)
                    return button;
            }

            throw new InvalidOperationException(
                "The realized Button item was not found.");
        }

        private static FlowLayoutPanel GetItemRow(
            XamlRuntime.ItemsControl host,
            int index)
        {
            AssertTrue(host != null, "items host exists");
            AssertTrue(
                index >= 0 && index < host.RealizedCount,
                "requested item row exists");
            int found = 0;
            int i;

            for (i = 0; i < host.Controls.Count; i++)
            {
                FlowLayoutPanel row =
                    host.Controls[i] as FlowLayoutPanel;

                if (row == null)
                    continue;

                if (found == index)
                    return row;

                found++;
            }

            throw new InvalidOperationException(
                "The requested realized FlowLayoutPanel row was not found.");
        }

        private static Label GetFirstRealizedLabel(
            XamlRuntime.ItemsControl host)
        {
            AssertTrue(host != null, "items host exists");
            int i;

            for (i = 0; i < host.Controls.Count; i++)
            {
                Label label = host.Controls[i] as Label;

                if (label != null)
                    return label;
            }

            throw new InvalidOperationException(
                "No realized Label item was found.");
        }

        private static T GetChild<T>(
            Control parent,
            string name)
            where T : Control
        {
            AssertTrue(parent != null, "child parent exists");
            T child = parent.Controls[name] as T;
            AssertTrue(child != null, "named child '" + name + "' exists");
            return child;
        }

        private static void CreateHandleAndDrain(Control root)
        {
            AssertTrue(root != null, "dispatch root exists");

            if (!root.IsHandleCreated)
                root.CreateControl();

            if (!root.IsHandleCreated)
            {
                IntPtr handle = root.Handle;
                AssertTrue(handle != IntPtr.Zero, "dispatch root handle exists");
            }

            Drain(root);
        }

        private static void Drain(Control root)
        {
            int round;

            for (round = 0; round < 8; round++)
            {
                bool reached = false;

                root.BeginInvoke(
                    new MethodInvoker(
                        delegate
                        {
                            reached = true;
                        }));

                int iterations = 0;

                while (!reached && iterations < 1024)
                {
                    Application.DoEvents();
                    iterations++;
                }

                AssertTrue(reached, "dispatch sentinel reached");
            }
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private static void AssertEqual(
            object expected,
            object actual,
            string message)
        {
            if (!Object.Equals(expected, actual))
            {
                throw new InvalidOperationException(
                    message +
                    ": expected '" +
                    Convert.ToString(expected) +
                    "', actual '" +
                    Convert.ToString(actual) +
                    "'.");
            }
        }
    }

    public sealed class NotifyNonControlRoot
    {
        private string _text;
        private int _setDepth;

        public event EventHandler TextChanged;
        public event EventHandler TextSet;

        public int SetCount;
        public int LastSetThreadId;
        public int MaxSetDepth;

        public string Text
        {
            get { return _text; }
            set
            {
                if (String.Equals(
                        _text,
                        value,
                        StringComparison.Ordinal))
                {
                    return;
                }

                _setDepth++;

                try
                {
                    _text = value;
                    SetCount++;
                    LastSetThreadId = Thread.CurrentThread.ManagedThreadId;

                    if (_setDepth > MaxSetDepth)
                        MaxSetDepth = _setDepth;

                    EventHandler changed = TextChanged;

                    if (changed != null)
                        changed(this, EventArgs.Empty);

                    EventHandler set = TextSet;

                    if (set != null)
                        set(this, EventArgs.Empty);
                }
                finally
                {
                    _setDepth--;
                }
            }
        }
    }

    public sealed class NotifyRecreatingControl : Control
    {
        public int TextChangeCount;

        public void RecreateNativeHandle()
        {
            RecreateHandle();
        }

        protected override void OnTextChanged(EventArgs e)
        {
            TextChangeCount++;
            base.OnTextChanged(e);
        }
    }
}
