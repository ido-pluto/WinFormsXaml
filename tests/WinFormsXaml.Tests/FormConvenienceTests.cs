using System;
using System.Collections;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using WinFormsXaml;

namespace WinFormsXaml.Tests
{
    public sealed class DiscoveredMainFormCodeBehind
    {
        public static int InstanceCount;
        public static int ClickCount;

        public DiscoveredMainFormCodeBehind()
        {
            InstanceCount++;
        }

        public void Action_Click(object sender, EventArgs e)
        {
            ClickCount++;
        }
    }

    public sealed class EmbeddedXmlFormHost : XmlForm
    {
        public static int LoadedCount;
        public static int StaticClickCount;
        public static int DisposeCount;
        private int _clickCount;
        // XmlForm assigns null named fields after loading markup.
        private Button hostAction = null;

        public EmbeddedXmlFormHost()
            : base(
                typeof(EmbeddedXmlFormHost).Assembly,
                "UI.XmlFormHost")
        {
        }

        public int ClickCount
        {
            get { return _clickCount; }
        }

        public Button ActionButton
        {
            get { return Get<Button>("HostAction"); }
        }

        public Button AutomaticallyWiredActionButton
        {
            get { return hostAction; }
        }

        public Thread StartTrackedWorker(
            ManualResetEvent started,
            ManualResetEvent stopped)
        {
            return RunThread(
                delegate(XmlFormThreadContext context)
                {
                    started.Set();
                    context.StopWaitHandle.WaitOne();
                    stopped.Set();
                });
        }

        public Thread StartDelayedWorker(
            ManualResetEvent started,
            ManualResetEvent release,
            ManualResetEvent stopped)
        {
            return RunThread(
                delegate(XmlFormThreadContext context)
                {
                    started.Set();
                    release.WaitOne();
                    stopped.Set();
                });
        }

        public bool PostUi(MethodInvoker callback)
        {
            return PostToUi(callback);
        }

        public Thread StartImmediateWorker()
        {
            return RunThread(
                delegate(XmlFormThreadContext context)
                {
                });
        }

        public void Action_Click(object sender, EventArgs e)
        {
            // Runtime-created XmlForm classes must adopt the active runtime;
            // touching WinForm here must not load a second XML tree.
            Form currentForm = WinForm;

            if (currentForm == null)
                throw new InvalidOperationException("The attached Form is missing.");

            _clickCount++;
            StaticClickCount++;
        }

        protected override void OnLoaded(EventArgs e)
        {
            LoadedCount++;

            if (WinForm == null)
                throw new InvalidOperationException("The loaded Form is missing.");

            if (hostAction == null)
            {
                throw new InvalidOperationException(
                    "The named HostAction field was not wired before OnLoaded.");
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                DisposeCount++;

            base.Dispose(disposing);
        }
    }

    public sealed class ConventionXmlFormHost : XmlForm
    {
        private string _message = "Loaded by convention";

        public string Message
        {
            get { return _message; }
        }

        public Label MessageLabel
        {
            get { return Get<Label>("ConventionMessage"); }
        }

        public PresetManager FormPresets
        {
            get { return Presets; }
        }

        public void SetMessage(string value)
        {
            _message = value;
        }

        public void ReloadMessageProperty()
        {
            ReloadBinding("ConventionMessage", "Text");
        }

        public void ReloadMessageElement()
        {
            ReloadBindings("ConventionMessage");
        }

        public void ReloadAllFormBindings()
        {
            ReloadBindings();
        }
    }

    internal sealed class NotifyingXmlFormHost : XmlForm
    {
        private string _title;
        private int _propertyChangedHookCount;
        private string _lastHookPropertyName;

        public NotifyingXmlFormHost()
            : base(
                typeof(NotifyingXmlFormHost).Assembly,
                "WinFormsXaml.Tests.UI.XmlFormHost.xml")
        {
        }

        public string Title
        {
            get { return _title; }
        }

        public int PropertyChangedHookCount
        {
            get { return _propertyChangedHookCount; }
        }

        public string LastHookPropertyName
        {
            get { return _lastHookPropertyName; }
        }

        public bool SetTitle(string value)
        {
            return SetProperty(
                ref _title,
                value,
                "Title");
        }

        public bool SetTitle(
            string value,
            string propertyName)
        {
            return SetProperty(
                ref _title,
                value,
                propertyName);
        }

        public void RaisePropertyChanged(
            PropertyChangedEventArgs e)
        {
            OnPropertyChanged(e);
        }

        protected override void OnPropertyChanged(
            PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            _propertyChangedHookCount++;
            _lastHookPropertyName = e.PropertyName;
        }
    }

    internal sealed class InvalidPartialXmlFormHost : XmlForm
    {
        public InvalidPartialXmlFormHost()
            : base(
                typeof(InvalidPartialXmlFormHost).Assembly,
                "Fixtures.InvalidDiagnosticForm")
        {
        }
    }

    public sealed class FailingOnLoadedWorkerXmlFormHost : XmlForm
    {
        private readonly ManualResetEvent _workerStarted;
        private readonly ManualResetEvent _workerRelease;
        private readonly ManualResetEvent _workerStopped;
        private Form _loadedForm;
        private Thread _worker;
        private int _disposeCount;

        public FailingOnLoadedWorkerXmlFormHost(
            ManualResetEvent workerStarted,
            ManualResetEvent workerRelease,
            ManualResetEvent workerStopped)
            : base(
                typeof(FailingOnLoadedWorkerXmlFormHost).Assembly,
                "UI.FailingOnLoadedWorkerForm")
        {
            if (workerStarted == null)
                throw new ArgumentNullException("workerStarted");

            if (workerRelease == null)
                throw new ArgumentNullException("workerRelease");

            if (workerStopped == null)
                throw new ArgumentNullException("workerStopped");

            _workerStarted = workerStarted;
            _workerRelease = workerRelease;
            _workerStopped = workerStopped;
        }

        public Form LoadedForm
        {
            get { return _loadedForm; }
        }

        public Thread Worker
        {
            get { return _worker; }
        }

        public int DisposeCount
        {
            get { return _disposeCount; }
        }

        protected override void OnLoaded(EventArgs e)
        {
            _loadedForm = Ui.Form;
            _worker = RunThread(
                delegate(XmlFormThreadContext context)
                {
                    _workerStarted.Set();
                    _workerRelease.WaitOne();
                    _workerStopped.Set();
                });

            if (!_workerStarted.WaitOne(2000, false))
            {
                throw new InvalidOperationException(
                    "The failing-load worker did not start.");
            }

            throw new InvalidOperationException(
                "Intentional OnLoaded failure after starting a worker.");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _disposeCount++;

            base.Dispose(disposing);
        }
    }

    public sealed class RuntimeOwnedRetryXmlFormHost : XmlForm
    {
        public static int DisposeAttempts;
        public static int SuccessfulDisposeCount;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DisposeAttempts++;

                if (DisposeAttempts == 1)
                {
                    throw new InvalidOperationException(
                        "Intentional first derived-disposal failure.");
                }

                SuccessfulDisposeCount++;
            }

            base.Dispose(disposing);
        }
    }

    public sealed class ConstructorLoadingXmlFormHost : XmlForm
    {
        private readonly Form _constructorForm;

        public ConstructorLoadingXmlFormHost()
            : base(
                typeof(ConstructorLoadingXmlFormHost).Assembly,
                "UI.ConstructorLoadingForm")
        {
            _constructorForm = WinForm;
        }

        public Form ConstructorForm
        {
            get { return _constructorForm; }
        }
    }

    internal sealed class IncludeQueueXmlFormHost : XmlForm
    {
        public IncludeQueueXmlFormHost()
            : base(
                typeof(IncludeQueueXmlFormHost).Assembly,
                "UI.XmlFormHost")
        {
        }

        public void QueueInclude(string source)
        {
            Include(source);
        }

        public void QueueInclude(
            string source,
            IncludeSourceKind sourceKind)
        {
            Include(source, sourceKind);
        }
    }

    public sealed class VirtualCloseXmlFormHost : XmlForm
    {
        public static int DisposeCount;
        public readonly ItemsBinding<string> Rows;
        private ItemsControl VirtualRows = null;

        public VirtualCloseXmlFormHost()
            : base(
                typeof(VirtualCloseXmlFormHost).Assembly,
                "UI.VirtualCloseForm")
        {
            Rows = new ItemsBinding<string>();
            int i;

            for (i = 0; i < 64; i++)
                Rows.Add("Row " + i.ToString());
        }

        public ItemsControl RowsControl
        {
            get { return VirtualRows; }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                DisposeCount++;

            base.Dispose(disposing);
        }
    }

    internal static class FormConvenienceTests
    {
        public static void Run()
        {
            TestCanonicalRuntimeApi();
            TestXmlFormIncludeQueueApi();
            TestFormPropertyAndClassDiscovery();
            TestXmlFormResourceConvention();
            TestEmbeddedStreamLoadingPreservesEncodingAndSource();
            TestEmbeddedPartialResolutionUsesBoundedCache();
            TestXmlFormBaseClass();
            TestXmlFormConstructorLoadingAndStart();
            TestVirtualItemsFormCloseKeepsRuntimeAliveForNativeCleanup();
            TestRuntimeOwnedVirtualFormDisposesNativeTreeFirst();
            TestXmlFormUiPost();
            TestXmlFormOwnedThreadShutdown();
            TestXmlFormOwnedThreadTimeoutIsRetryable();
            TestRejectedSecondRuntimeAttachPreservesHost();
            TestFailedOnLoadedWorkerRollbackIsRetryable();
            TestPendingXmlFormCleanupRemainsOwnerThreadBound();
            TestRecursiveRuntimeOwnedXmlFormDisposalIsRetryable();
            TestExplicitDisposalInvalidatesDeferredUserClose();
            TestLaterNonUserCloseCancellationRestoresWorkerAdmission();
            TestCompletedWorkerRetiresAfterThreadTermination();
            TestXmlFormPartialResourceDiagnostics();
            TestBulkComponentRegistration();
            TestRegistrationDiagnostics();
        }

        private static void TestCanonicalRuntimeApi()
        {
            Type runtimeType = typeof(XamlRuntime);
            MethodInfo[] publicStaticMethods = runtimeType.GetMethods(
                BindingFlags.Public | BindingFlags.Static);
            bool exposesNewForm = false;
            int methodIndex;

            for (methodIndex = 0;
                 methodIndex < publicStaticMethods.Length;
                 methodIndex++)
            {
                if (String.Equals(
                        publicStaticMethods[methodIndex].Name,
                        "NewForm",
                        StringComparison.Ordinal))
                {
                    exposesNewForm = true;
                    break;
                }
            }

            PropertyInfo nativeForm = typeof(XmlForm).GetProperty(
                "WinForm",
                BindingFlags.Instance | BindingFlags.Public);

            AssertTrue(
                runtimeType.FullName == "WinFormsXaml.XamlRuntime",
                "XamlRuntime is the canonical public runtime type");
            AssertTrue(
                runtimeType.Assembly.GetType(
                    "WinFormsXaml.WinFormsXaml",
                    false) == null,
                "the legacy public runtime type is not emitted");
            AssertTrue(
                !exposesNewForm,
                "XamlRuntime does not expose the removed NewForm API");
            AssertTrue(
                runtimeType.GetMethod(
                    "GetProgressBar",
                    BindingFlags.Instance | BindingFlags.Public) == null,
                "XamlRuntime keeps progress compatibility behind Get<ProgressBar>");
            AssertTrue(
                nativeForm != null && nativeForm.PropertyType == typeof(Form),
                "XmlForm exposes its native form as WinForm");
            AssertTrue(
                typeof(XmlForm).GetProperty(
                    "Form",
                    BindingFlags.Instance | BindingFlags.Public) == null,
                "XmlForm does not expose the legacy Form property");
            AssertTrue(
                typeof(XmlForm).GetMethod(
                    "GetProgressBar",
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic) == null,
                "XmlForm keeps progress compatibility behind Get<ProgressBar>");
        }

        private static void TestXmlFormIncludeQueueApi()
        {
            MethodInfo simpleInclude = typeof(XmlForm).GetMethod(
                "Include",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new Type[] { typeof(string) },
                null);
            MethodInfo explicitInclude = typeof(XmlForm).GetMethod(
                "Include",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new Type[]
                {
                    typeof(string),
                    typeof(IncludeSourceKind)
                },
                null);

            AssertTrue(
                simpleInclude != null && simpleInclude.IsFamily,
                "XmlForm exposes the protected Include(string) sugar API");
            AssertTrue(
                explicitInclude != null && explicitInclude.IsFamily,
                "XmlForm exposes the protected explicit include-source API");
            AssertEqual(
                0,
                (int)IncludeSourceKind.Registered,
                "Registered is the default include source kind");

            IncludeQueueXmlFormHost host =
                new IncludeQueueXmlFormHost();

            host.QueueInclude("  Shared.Foundation  ");
            host.QueueInclude(
                "WinFormsXaml.Tests.UI.SharedTheme.xml",
                IncludeSourceKind.EmbeddedResource);
            host.QueueInclude(
                "Themes/Local.xml",
                IncludeSourceKind.File);

            MethodInfo snapshotMethod = typeof(XmlForm).GetMethod(
                "SnapshotIncludeRequestsForLoad",
                BindingFlags.Instance | BindingFlags.NonPublic);

            AssertTrue(
                snapshotMethod != null,
                "XmlForm exposes its internal include-composition snapshot");

            IList firstSnapshot = (IList)snapshotMethod.Invoke(
                host,
                null);
            IList secondSnapshot = (IList)snapshotMethod.Invoke(
                host,
                null);

            AssertEqual(3, firstSnapshot.Count, "all includes are retained");
            AssertEqual(
                3,
                secondSnapshot.Count,
                "include snapshots are stable and non-consuming");
            AssertTrue(
                !Object.ReferenceEquals(firstSnapshot, secondSnapshot),
                "callers receive independent include snapshot arrays");

            AssertIncludeRequest(
                firstSnapshot[0],
                "Shared.Foundation",
                IncludeSourceKind.Registered,
                typeof(IncludeQueueXmlFormHost).Assembly,
                "default include keeps first-call order");
            AssertIncludeRequest(
                firstSnapshot[1],
                "WinFormsXaml.Tests.UI.SharedTheme.xml",
                IncludeSourceKind.EmbeddedResource,
                typeof(IncludeQueueXmlFormHost).Assembly,
                "embedded include keeps second-call order");
            AssertIncludeRequest(
                firstSnapshot[2],
                "Themes/Local.xml",
                IncludeSourceKind.File,
                typeof(IncludeQueueXmlFormHost).Assembly,
                "file include keeps third-call order");

            bool rejectedAfterSnapshot = false;

            try
            {
                host.QueueInclude("TooLate");
            }
            catch (InvalidOperationException ex)
            {
                rejectedAfterSnapshot = true;
                AssertContains(
                    ex.Message,
                    "before this XML Form starts loading",
                    "late include explains its pre-load contract");
            }
            finally
            {
                host.Dispose();
            }

            AssertTrue(
                rejectedAfterSnapshot,
                "snapshotting closes the programmatic include queue");

            IncludeQueueXmlFormHost invalid =
                new IncludeQueueXmlFormHost();
            bool nullRejected = false;
            bool whitespaceRejected = false;
            bool kindRejected = false;

            try
            {
                invalid.QueueInclude(null);
            }
            catch (ArgumentNullException)
            {
                nullRejected = true;
            }

            try
            {
                invalid.QueueInclude("   ");
            }
            catch (ArgumentException)
            {
                whitespaceRejected = true;
            }

            try
            {
                invalid.QueueInclude(
                    "Theme",
                    (IncludeSourceKind)Int32.MaxValue);
            }
            catch (ArgumentOutOfRangeException)
            {
                kindRejected = true;
            }
            finally
            {
                invalid.Dispose();
            }

            AssertTrue(nullRejected, "null include source is rejected");
            AssertTrue(
                whitespaceRejected,
                "whitespace include source is rejected");
            AssertTrue(kindRejected, "unknown include source kind is rejected");

            IncludeQueueXmlFormHost disposed =
                new IncludeQueueXmlFormHost();
            disposed.Dispose();
            bool disposedRejected = false;

            try
            {
                disposed.QueueInclude("Theme");
            }
            catch (ObjectDisposedException)
            {
                disposedRejected = true;
            }

            AssertTrue(
                disposedRejected,
                "a disposed XmlForm cannot queue includes");
        }

        private static void AssertIncludeRequest(
            object request,
            string expectedSource,
            IncludeSourceKind expectedKind,
            Assembly expectedAssembly,
            string message)
        {
            AssertTrue(request != null, message + " request exists");

            Type requestType = request.GetType();
            PropertyInfo source = requestType.GetProperty(
                "Source",
                BindingFlags.Instance | BindingFlags.NonPublic);
            PropertyInfo sourceKind = requestType.GetProperty(
                "SourceKind",
                BindingFlags.Instance | BindingFlags.NonPublic);
            PropertyInfo assembly = requestType.GetProperty(
                "Assembly",
                BindingFlags.Instance | BindingFlags.NonPublic);

            AssertTrue(
                source != null && sourceKind != null && assembly != null,
                message + " request exposes composition inputs");
            AssertEqual(
                expectedSource,
                source.GetValue(request, null),
                message + " source");
            AssertEqual(
                expectedKind,
                sourceKind.GetValue(request, null),
                message + " source kind");
            AssertTrue(
                Object.ReferenceEquals(
                    expectedAssembly,
                    assembly.GetValue(request, null)),
                message + " assembly");
        }

        private static void TestXmlFormResourceConvention()
        {
            ConventionXmlFormHost host =
                new ConventionXmlFormHost();
            Form form = null;

            AssertTrue(
                !host.IsLoaded,
                "convention XmlForm remains lazy");

            try
            {
                form = host.WinForm;

                AssertEqual(
                    "ConventionXmlForm",
                    form.Name,
                    "parameterless XmlForm resolves Type.FullName.xml");
                AssertEqual(
                    "Loaded by convention",
                    host.MessageLabel.Text,
                    "convention XmlForm uses the derived object as binding source");
                AssertTrue(
                    host.FormPresets != null,
                    "XmlForm exposes its active preset manager directly to code-behind");

                host.SetMessage("One property");
                host.ReloadMessageProperty();
                AssertEqual(
                    "One property",
                    host.MessageLabel.Text,
                    "XmlForm reloads one named property without Ui ceremony");

                host.SetMessage("One element");
                host.ReloadMessageElement();
                AssertEqual(
                    "One element",
                    host.MessageLabel.Text,
                    "XmlForm reloads one named subtree without Ui ceremony");

                host.SetMessage("Everything");
                host.ReloadAllFormBindings();
                AssertEqual(
                    "Everything",
                    host.MessageLabel.Text,
                    "XmlForm reloads all retained bindings without Ui ceremony");
            }
            finally
            {
                host.Dispose();
            }

            AssertTrue(
                form != null && form.IsDisposed,
                "convention XmlForm owns the loaded native Form");
        }

        public static void RunPropertyNotificationTests()
        {
            TestXmlFormPropertyNotifications();
        }

        private static void
            TestEmbeddedStreamLoadingPreservesEncodingAndSource()
        {
            MethodInfo loadStream = typeof(XamlRuntime).GetMethod(
                "Load",
                BindingFlags.Static | BindingFlags.NonPublic,
                null,
                new Type[]
                {
                    typeof(Stream),
                    typeof(object),
                    typeof(string),
                    typeof(PresetManager),
                    typeof(Assembly),
                    typeof(string)
                },
                null);
            AssertTrue(
                loadStream != null,
                "the embedded loader exposes its direct stream parser");

            const string expected = "Zażółć gęślą jaźń";
            const string markupSource = "embedded-stream-regression.xml";
            string markup =
                "<?xml version='1.0' encoding='utf-16'?>" +
                "<Label Name='EncodedLabel' Text='" + expected + "' />";
            byte[] preamble = Encoding.Unicode.GetPreamble();
            byte[] content = Encoding.Unicode.GetBytes(markup);
            byte[] encoded = new byte[preamble.Length + content.Length];
            Buffer.BlockCopy(
                preamble,
                0,
                encoded,
                0,
                preamble.Length);
            Buffer.BlockCopy(
                content,
                0,
                encoded,
                preamble.Length,
                content.Length);
            XamlRuntime runtime = null;

            using (MemoryStream stream = new MemoryStream(encoded, false))
            {
                runtime = (XamlRuntime)loadStream.Invoke(
                    null,
                    new object[]
                    {
                        stream,
                        null,
                        null,
                        null,
                        Assembly.GetExecutingAssembly(),
                        markupSource
                    });
            }

            try
            {
                AssertEqual(
                    expected,
                    runtime.Get<Label>("EncodedLabel").Text,
                    "direct stream parsing honors the XML byte encoding");
                AssertEqual(
                    markupSource,
                    ReadPrivateField(
                        typeof(XamlRuntime),
                        runtime,
                        "_markupSource"),
                    "direct stream parsing retains embedded source metadata");
            }
            finally
            {
                if (runtime != null)
                    runtime.Dispose();
            }
        }

        private static void
            TestEmbeddedPartialResolutionUsesBoundedCache()
        {
            Type runtimeType = typeof(XamlRuntime);
            MethodInfo find = runtimeType.GetMethod(
                "FindEmbeddedXmlResource",
                BindingFlags.Static | BindingFlags.NonPublic);
            AssertTrue(
                find != null,
                "embedded partial-resource resolution is available internally");

            Assembly assembly = Assembly.GetExecutingAssembly();
            string first = (string)find.Invoke(
                null,
                new object[]
                {
                    assembly,
                    "Ambiguity/East/SharedForm"
                });
            AssertEqual(
                "WinFormsXaml.Tests.UI.Ambiguity.East.SharedForm.xml",
                first,
                "partial embedded paths retain deterministic selection");

            object sync = ReadPrivateStaticField(
                runtimeType,
                "_embeddedResourceNamesSync");
            Hashtable caches = (Hashtable)ReadPrivateStaticField(
                runtimeType,
                "_embeddedResourceResolutionsByAssembly");
            int assemblyLimit = Convert.ToInt32(
                ReadPrivateStaticField(
                    runtimeType,
                    "EmbeddedResourceAssemblyCacheLimit"));
            int perAssemblyLimit = Convert.ToInt32(
                ReadPrivateStaticField(
                    runtimeType,
                    "EmbeddedResourceResolutionPerAssemblyLimit"));
            int globalLimit = Convert.ToInt32(
                ReadPrivateStaticField(
                    runtimeType,
                    "EmbeddedResourceResolutionCacheLimit"));
            int countAfterFirst;

            lock (sync)
            {
                Hashtable assemblyCache = caches[assembly] as Hashtable;
                AssertTrue(
                    assemblyCache != null &&
                    Object.Equals(
                        first,
                        assemblyCache["Ambiguity.East.SharedForm"]),
                    "a successful normalized partial lookup is cached");
                AssertTrue(
                    assemblyCache.Count <= perAssemblyLimit &&
                    caches.Count <= assemblyLimit,
                    "partial-resource cache admission remains bounded");
                countAfterFirst = Convert.ToInt32(
                    ReadPrivateStaticField(
                        runtimeType,
                        "_embeddedResourceResolutionCount"));
                AssertTrue(
                    countAfterFirst <= globalLimit,
                    "the global partial-resource cache remains bounded");
            }

            string repeated = (string)find.Invoke(
                null,
                new object[]
                {
                    assembly,
                    "Ambiguity\\East\\SharedForm"
                });

            lock (sync)
            {
                AssertEqual(
                    countAfterFirst,
                    Convert.ToInt32(
                        ReadPrivateStaticField(
                            runtimeType,
                            "_embeddedResourceResolutionCount")),
                    "an equivalent repeated partial path reuses its cache entry");
            }

            AssertEqual(
                first,
                repeated,
                "cached partial resolution retains the selected manifest name");

            const string upperCaseResource =
                "WinFormsXaml.Tests.UI.CaseVariants.SharedCaseCard.xml";
            const string lowerCaseResource =
                "winformsxaml.tests.ui.casevariants.sharedcasecard.xml";
            string upperCaseResult = (string)find.Invoke(
                null,
                new object[] { assembly, upperCaseResource });
            string lowerCaseResult = (string)find.Invoke(
                null,
                new object[] { assembly, lowerCaseResource });

            AssertEqual(
                upperCaseResource,
                upperCaseResult,
                "the resolution cache preserves an exact manifest-name case");
            AssertEqual(
                lowerCaseResource,
                lowerCaseResult,
                "case-only manifest siblings retain distinct cache entries");
        }

        private static void TestFormPropertyAndClassDiscovery()
        {
            DiscoveredMainFormCodeBehind.InstanceCount = 0;
            DiscoveredMainFormCodeBehind.ClickCount = 0;

            XamlRuntime runtime = XamlRuntime.LoadEmbedded(
                Assembly.GetExecutingAssembly(),
                "WinFormsXaml.Tests.UI.DiscoveredMainForm.xml",
                null);

            try
            {
                AssertTrue(
                    Object.ReferenceEquals(runtime.Root, runtime.Form),
                    "Form returns the native root instance");
                AssertEqual(
                    "DiscoveredMainForm",
                    runtime.Form.Name,
                    "LoadEmbedded creates the exact embedded Form resource");
                AssertEqual(
                    1,
                    DiscoveredMainFormCodeBehind.InstanceCount,
                    "Form Class creates one code-behind object");

                RaiseClick(runtime.Get<Button>("DiscoveryAction"));
                AssertEqual(
                    1,
                    DiscoveredMainFormCodeBehind.ClickCount,
                    "the discovered Class receives events");
            }
            finally
            {
                runtime.Dispose();
            }

            DiscoveredMainFormCodeBehind supplied =
                new DiscoveredMainFormCodeBehind();
            runtime = XamlRuntime.LoadEmbedded(
                Assembly.GetExecutingAssembly(),
                "WinFormsXaml.Tests.UI.DiscoveredMainForm.xml",
                supplied);

            try
            {
                RaiseClick(runtime.Get<Button>("DiscoveryAction"));
                AssertEqual(
                    2,
                    DiscoveredMainFormCodeBehind.ClickCount,
                    "a matching supplied Class target is reused");
            }
            finally
            {
                runtime.Dispose();
            }

            XamlRuntime nonForm = XamlRuntime.Load("<Panel />");

            try
            {
                bool rejected = false;

                try
                {
                    AssertTrue(
                        nonForm.Form == null,
                        "a non-Form root cannot expose Form");
                }
                catch (InvalidOperationException)
                {
                    rejected = true;
                }

                AssertTrue(rejected, "Form rejects a non-Form root");
            }
            finally
            {
                nonForm.Dispose();
            }
        }

        private static void TestXmlFormBaseClass()
        {
            EmbeddedXmlFormHost.LoadedCount = 0;
            EmbeddedXmlFormHost.StaticClickCount = 0;
            EmbeddedXmlFormHost.DisposeCount = 0;
            EmbeddedXmlFormHost host = new EmbeddedXmlFormHost();
            Form hostedForm = null;

            AssertTrue(!host.IsLoaded, "XmlForm loads lazily");

            try
            {
                AssertEqual(
                    "EmbeddedXmlForm",
                    host.WinForm.Name,
                    "XmlForm exposes the native Form");
                hostedForm = host.WinForm;
                AssertTrue(host.IsLoaded, "XmlForm retains its runtime");
                AssertTrue(
                    Object.ReferenceEquals(
                        host.ActionButton,
                        host.AutomaticallyWiredActionButton),
                    "XmlForm wires a compatible field from XML Name before OnLoaded");

                RaiseClick(host.ActionButton);
                AssertEqual(1, host.ClickCount, "XmlForm is its event target");
                AssertEqual(
                    1,
                    EmbeddedXmlFormHost.LoadedCount,
                    "XmlForm raises OnLoaded exactly once");
            }
            finally
            {
                host.Dispose();
            }

            AssertEqual(
                1,
                EmbeddedXmlFormHost.DisposeCount,
                "XmlForm releases derived state exactly once");
            AssertTrue(
                hostedForm != null && hostedForm.IsDisposed,
                "XmlForm disposal releases its native Form");

            bool rejected = false;

            try
            {
                AssertTrue(
                    host.WinForm == null,
                    "a disposed XmlForm cannot expose Form");
            }
            catch (ObjectDisposedException)
            {
                rejected = true;
            }

            AssertTrue(rejected, "disposed XmlForm cannot reload itself");

            EmbeddedXmlFormHost.LoadedCount = 0;
            EmbeddedXmlFormHost.StaticClickCount = 0;
            EmbeddedXmlFormHost.DisposeCount = 0;
            XamlRuntime runtime = XamlRuntime.LoadEmbedded(
                Assembly.GetExecutingAssembly(),
                "WinFormsXaml.Tests.UI.XmlFormHost.xml",
                null);
            Form autoForm = runtime.Form;

            try
            {
                AssertEqual(
                    1,
                    EmbeddedXmlFormHost.LoadedCount,
                    "LoadEmbedded attaches an XmlForm Class before OnLoaded");
                RaiseClick(runtime.Get<Button>("HostAction"));
                AssertEqual(
                    1,
                    EmbeddedXmlFormHost.StaticClickCount,
                    "an auto-created XmlForm Class reuses the active runtime");
                AssertEqual(
                    1,
                    EmbeddedXmlFormHost.LoadedCount,
                    "event access does not load a second runtime");
            }
            finally
            {
                runtime.Dispose();
            }

            AssertEqual(
                1,
                EmbeddedXmlFormHost.DisposeCount,
                "runtime-owned XmlForm code-behind is disposed exactly once");
            AssertTrue(
                autoForm.IsDisposed,
                "runtime-owned XmlForm disposal releases the native Form");
        }

        private static void TestXmlFormConstructorLoadingAndStart()
        {
            ConstructorLoadingXmlFormHost host =
                new ConstructorLoadingXmlFormHost();
            System.Windows.Forms.Timer closeTimer =
                new System.Windows.Forms.Timer();

            try
            {
                AssertTrue(
                    host.IsLoaded &&
                    host.ConstructorForm != null &&
                    Object.ReferenceEquals(
                        host.ConstructorForm,
                        host.WinForm),
                    "derived constructors may explicitly load WinForm");

                closeTimer.Interval = 1;
                closeTimer.Tick +=
                    delegate(object sender, EventArgs e)
                    {
                        closeTimer.Stop();
                        host.WinForm.Close();
                    };
                closeTimer.Start();

                host.Start();

                AssertTrue(
                    host.ConstructorForm.IsDisposed &&
                    !host.IsLoaded,
                    "XmlForm.Start runs the loaded Form until it closes");
            }
            finally
            {
                closeTimer.Dispose();
                host.Dispose();
            }
        }

        private static void
            TestVirtualItemsFormCloseKeepsRuntimeAliveForNativeCleanup()
        {
            VirtualCloseXmlFormHost.DisposeCount = 0;
            VirtualCloseXmlFormHost host =
                new VirtualCloseXmlFormHost();
            System.Windows.Forms.Timer closeTimer =
                new System.Windows.Forms.Timer();
            Form form = null;
            ItemsControl rows = null;

            try
            {
                form = host.WinForm;
                rows = host.RowsControl;
                form.CreateControl();
                rows.CreateControl();
                rows.PerformLayout();

                AssertTrue(
                    rows.IsVirtualizing,
                    "the close-order regression keeps a direct virtual " +
                    "ItemsControl active");

                closeTimer.Interval = 1;
                closeTimer.Tick +=
                    delegate(object sender, EventArgs e)
                    {
                        closeTimer.Stop();
                        form.Close();
                    };
                closeTimer.Start();

                host.Start();

                AssertTrue(
                    form.IsDisposed && rows.IsDisposed && !host.IsLoaded,
                    "closing a virtual-items Form releases its native tree " +
                    "and XmlForm lifetime");
                AssertEqual(
                    1,
                    VirtualCloseXmlFormHost.DisposeCount,
                    "closing a virtual-items Form releases code-behind exactly once");

                host.Dispose();
                AssertEqual(
                    1,
                    VirtualCloseXmlFormHost.DisposeCount,
                    "completed virtual-items Form cleanup remains idempotent");
            }
            finally
            {
                closeTimer.Dispose();
                host.Dispose();
            }
        }

        private static void
            TestRuntimeOwnedVirtualFormDisposesNativeTreeFirst()
        {
            VirtualCloseXmlFormHost.DisposeCount = 0;
            XamlRuntime runtime = XamlRuntime.LoadEmbedded(
                Assembly.GetExecutingAssembly(),
                "WinFormsXaml.Tests.UI.VirtualCloseForm.xml",
                null);
            Form form = runtime.Form;
            XamlRuntime.ItemsControl rows =
                runtime.GetItemsControl("VirtualRows");

            try
            {
                form.CreateControl();
                rows.CreateControl();
                rows.PerformLayout();

                AssertTrue(
                    rows.IsVirtualizing,
                    "the runtime-owned disposal regression keeps direct " +
                    "virtualization active");

                runtime.Dispose();

                AssertTrue(
                    runtime.IsDisposed &&
                    form.IsDisposed &&
                    rows.IsDisposed,
                    "runtime disposal releases its XmlForm native tree " +
                    "before retained runtime state");
                AssertEqual(
                    1,
                    VirtualCloseXmlFormHost.DisposeCount,
                    "runtime-owned virtual Form code-behind is released once");

                runtime.Dispose();
                AssertEqual(
                    1,
                    VirtualCloseXmlFormHost.DisposeCount,
                    "completed runtime-owned virtual cleanup is idempotent");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestXmlFormUiPost()
        {
            EmbeddedXmlFormHost host =
                new EmbeddedXmlFormHost();
            ManualResetEvent callbackRan =
                new ManualResetEvent(false);
            Thread poster = null;

            try
            {
                AssertTrue(
                    !host.PostUi(delegate() { }),
                    "PostToUi rejects work before the Form is loaded");

                Form form = host.WinForm;
                AssertTrue(
                    form.Handle != IntPtr.Zero,
                    "the PostToUi test creates a native Form handle");
                int uiThreadId =
                    Thread.CurrentThread.ManagedThreadId;
                int callbackThreadId = -1;
                bool accepted = false;

                poster = new Thread(
                    delegate()
                    {
                        accepted = host.PostUi(
                            delegate()
                            {
                                callbackThreadId =
                                    Thread.CurrentThread.ManagedThreadId;
                                callbackRan.Set();
                            });
                    });
                poster.IsBackground = true;
                poster.Start();

                AssertTrue(
                    poster.Join(2000),
                    "PostToUi returns to the worker without waiting for UI work");
                AssertTrue(
                    accepted && !callbackRan.WaitOne(0, false),
                    "PostToUi queues rather than executing on the worker");

                int attempts;

                for (attempts = 0;
                     attempts < 2000 &&
                     !callbackRan.WaitOne(0, false);
                     attempts++)
                {
                    Application.DoEvents();
                    Thread.Sleep(1);
                }

                AssertTrue(
                    callbackRan.WaitOne(0, false),
                    "PostToUi dispatches through the Form message queue");
                AssertEqual(
                    uiThreadId,
                    callbackThreadId,
                    "PostToUi runs on the Form owner thread");

                bool acceptedDuringClosing = true;
                bool workerRejectedDuringClosing = false;
                FormClosingEventHandler cancelClose =
                    delegate(object sender, FormClosingEventArgs e)
                    {
                        acceptedDuringClosing =
                            host.PostUi(delegate() { });

                        try
                        {
                            host.StartImmediateWorker();
                        }
                        catch (ObjectDisposedException)
                        {
                            workerRejectedDuringClosing = true;
                        }

                        e.Cancel = true;
                    };
                form.FormClosing += cancelClose;

                form.Close();

                AssertTrue(
                    !acceptedDuringClosing &&
                    workerRejectedDuringClosing,
                    "FormClosing rejects new UI posts and owned workers");

                form.FormClosing -= cancelClose;
                Application.DoEvents();

                AssertTrue(
                    host.PostUi(delegate() { }),
                    "PostToUi recovers after a canceled close unwinds");
                Application.DoEvents();

                bool disposedCallbackRan = false;

                AssertTrue(
                    host.PostUi(
                        delegate()
                        {
                            disposedCallbackRan = true;
                        }),
                    "PostToUi accepts work before disposal begins");

                form.Dispose();

                Application.DoEvents();

                AssertTrue(
                    !disposedCallbackRan,
                    "PostToUi suppresses queued work when disposal wins");

                AssertTrue(
                    !host.PostUi(delegate() { }),
                    "PostToUi rejects work after Form disposal starts");

                bool nullRejected = false;

                try
                {
                    host.PostUi(null);
                }
                catch (ArgumentNullException)
                {
                    nullRejected = true;
                }

                AssertTrue(
                    nullRejected,
                    "PostToUi validates a null callback consistently");
            }
            finally
            {
                if (poster != null)
                    poster.Join(2000);

                host.Dispose();
                callbackRan.Close();
            }
        }

        private static void TestXmlFormOwnedThreadShutdown()
        {
            EmbeddedXmlFormHost host =
                new EmbeddedXmlFormHost();
            ManualResetEvent started =
                new ManualResetEvent(false);
            ManualResetEvent stopped =
                new ManualResetEvent(false);
            Thread worker = null;

            try
            {
                AssertTrue(
                    host.WinForm != null,
                    "the tracked-worker XmlForm loads");
                worker = host.StartTrackedWorker(
                    started,
                    stopped);
                AssertTrue(
                    started.WaitOne(2000, false),
                    "RunThread starts its delegate immediately");

                host.WinForm.Dispose();

                AssertTrue(
                    stopped.WaitOne(0, false),
                    "native Form disposal signals cooperative worker shutdown");
                AssertTrue(
                    worker.Join(0),
                    "native Form disposal joins the XmlForm-owned worker");
                AssertTrue(
                    !host.IsLoaded,
                    "native Form disposal disposes its XmlForm lifetime");
                AssertTrue(
                    host.AutomaticallyWiredActionButton == null,
                    "disposed XML names release automatically wired fields");

                bool rejected = false;

                try
                {
                    host.StartTrackedWorker(
                        started,
                        stopped);
                }
                catch (ObjectDisposedException)
                {
                    rejected = true;
                }

                AssertTrue(
                    rejected,
                    "a disposed XmlForm cannot start another owned worker");
            }
            finally
            {
                host.Dispose();
                started.Close();
                stopped.Close();
            }
        }

        private static void TestXmlFormOwnedThreadTimeoutIsRetryable()
        {
            EmbeddedXmlFormHost host =
                new EmbeddedXmlFormHost();
            ManualResetEvent started =
                new ManualResetEvent(false);
            ManualResetEvent release =
                new ManualResetEvent(false);
            ManualResetEvent stopped =
                new ManualResetEvent(false);
            Thread worker = null;
            Form form = null;

            try
            {
                form = host.WinForm;
                worker = host.StartDelayedWorker(
                    started,
                    release,
                    stopped);
                AssertTrue(
                    started.WaitOne(2000, false),
                    "the delayed RunThread delegate starts");

                bool timedOut = false;

                try
                {
                    host.Dispose();
                }
                catch (InvalidOperationException)
                {
                    timedOut = true;
                }

                AssertTrue(
                    timedOut,
                    "a non-returning delegate makes bounded disposal fail");
                AssertTrue(
                    host.IsLoaded && !form.IsDisposed,
                    "timed-out disposal leaves the wrapper and Form retryable");
                AssertTrue(
                    host.AutomaticallyWiredActionButton != null,
                    "timed-out disposal does not clear named-field wiring");

                release.Set();
                AssertTrue(
                    stopped.WaitOne(2000, false),
                    "the delayed delegate can return after the timeout");
                AssertTrue(
                    worker.Join(2000),
                    "the delayed worker exits before disposal retry");

                host.Dispose();

                AssertTrue(
                    form.IsDisposed && !host.IsLoaded,
                    "disposal succeeds when retried after the worker returns");
            }
            finally
            {
                release.Set();

                if (worker != null)
                    worker.Join(2000);

                host.Dispose();
                started.Close();
                release.Close();
                stopped.Close();
            }
        }

        private static void TestRejectedSecondRuntimeAttachPreservesHost()
        {
            EmbeddedXmlFormHost host =
                new EmbeddedXmlFormHost();
            Form originalForm = null;
            Button originalAction = null;
            XamlRuntime unexpectedRuntime = null;

            try
            {
                originalForm = host.WinForm;
                originalAction =
                    host.AutomaticallyWiredActionButton;
                Exception failure = null;

                try
                {
                    unexpectedRuntime = XamlRuntime.LoadEmbedded(
                        Assembly.GetExecutingAssembly(),
                        "WinFormsXaml.Tests.UI.XmlFormHost.xml",
                        host);
                }
                catch (Exception ex)
                {
                    failure = ex;
                }

                AssertTrue(
                    failure != null,
                    "a loaded XmlForm rejects attachment to a second runtime");
                AssertTrue(
                    host.IsLoaded &&
                    !originalForm.IsDisposed &&
                    Object.ReferenceEquals(host.WinForm, originalForm),
                    "a rejected second-runtime attach preserves the existing Form");
                AssertTrue(
                    Object.ReferenceEquals(
                        host.AutomaticallyWiredActionButton,
                        originalAction),
                    "a rejected second-runtime attach preserves existing name wiring");

                int clickCount = host.ClickCount;
                RaiseClick(originalAction);
                AssertEqual(
                    clickCount + 1,
                    host.ClickCount,
                    "the original runtime remains operational after attach rejection");
            }
            finally
            {
                if (unexpectedRuntime != null)
                    unexpectedRuntime.Dispose();

                host.Dispose();
            }
        }

        private static void TestFailedOnLoadedWorkerRollbackIsRetryable()
        {
            ManualResetEvent started =
                new ManualResetEvent(false);
            ManualResetEvent release =
                new ManualResetEvent(false);
            ManualResetEvent stopped =
                new ManualResetEvent(false);
            FailingOnLoadedWorkerXmlFormHost host =
                new FailingOnLoadedWorkerXmlFormHost(
                    started,
                    release,
                    stopped);
            Exception failure = null;

            try
            {
                try
                {
                    AssertTrue(
                        host.WinForm != null,
                        "the intentionally failing XmlForm unexpectedly loaded");
                }
                catch (Exception ex)
                {
                    failure = ex;
                }

                AssertTrue(
                    failure != null,
                    "an OnLoaded failure remains the primary load error");
                AssertTrue(
                    started.WaitOne(0, false) &&
                    host.Worker != null &&
                    host.Worker.IsAlive,
                    "the failed OnLoaded worker is still live at rollback timeout");
                AssertTrue(
                    host.LoadedForm != null &&
                    !host.LoadedForm.IsDisposed,
                    "failed-load rollback retains the root while its worker is live");
                AssertEqual(
                    0,
                    host.DisposeCount,
                    "timed-out failed-load rollback defers derived cleanup");

                release.Set();
                AssertTrue(
                    stopped.WaitOne(2000, false),
                    "the failed-load worker can finish after rollback timeout");
                AssertTrue(
                    host.Worker.Join(2000),
                    "the failed-load worker terminates before cleanup retry");

                host.Dispose();

                AssertTrue(
                    host.LoadedForm.IsDisposed,
                    "retry cleanup releases the retained failed-load root");
                AssertEqual(
                    1,
                    host.DisposeCount,
                    "retry cleanup releases failed-load derived state exactly once");
            }
            finally
            {
                release.Set();

                if (host.Worker != null)
                    host.Worker.Join(2000);

                host.Dispose();
                started.Close();
                release.Close();
                stopped.Close();
            }
        }

        private static void TestPendingXmlFormCleanupRemainsOwnerThreadBound()
        {
            EmbeddedXmlFormHost host =
                new EmbeddedXmlFormHost();
            Form form = host.WinForm;
            XamlRuntime runtime = (XamlRuntime)ReadPrivateField(
                typeof(XmlForm),
                host,
                "_ui");
            FieldInfo rootField = typeof(XamlRuntime).GetField(
                "_root",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Exception offThreadFailure = null;
            Thread disposer = null;

            AssertTrue(
                rootField != null,
                "XamlRuntime exposes its retained root lifecycle field");

            try
            {
                // Reproduce the narrow failed-load state in which the partial
                // root has been released but the paired XmlForm still owns
                // retryable cleanup debt.
                rootField.SetValue(runtime, null);
                disposer = new Thread(
                    new ThreadStart(
                        delegate
                        {
                            try
                            {
                                runtime.Dispose();
                            }
                            catch (Exception ex)
                            {
                                offThreadFailure = ex;
                            }
                        }));
                disposer.IsBackground = true;
                disposer.Start();
                AssertTrue(
                    disposer.Join(2000),
                    "the off-thread disposal preflight returns promptly");
                AssertTrue(
                    offThreadFailure is InvalidOperationException,
                    "pending XmlForm cleanup remains bound to the load thread");
                AssertTrue(
                    host.IsLoaded &&
                    !runtime.IsDisposed &&
                    !form.IsDisposed,
                    "rejected off-thread cleanup does not mutate the paired lifetime");
            }
            finally
            {
                rootField.SetValue(runtime, form);
                host.Dispose();

                if (!form.IsDisposed)
                    form.Dispose();
            }
        }

        private static void TestRecursiveRuntimeOwnedXmlFormDisposalIsRetryable()
        {
            RuntimeOwnedRetryXmlFormHost.DisposeAttempts = 0;
            RuntimeOwnedRetryXmlFormHost.SuccessfulDisposeCount = 0;
            XamlRuntime runtime = XamlRuntime.LoadEmbedded(
                Assembly.GetExecutingAssembly(),
                "WinFormsXaml.Tests.UI.RuntimeOwnedRetryForm.xml",
                null);
            Form form = runtime.Form;

            try
            {
                Exception failure = null;

                try
                {
                    form.Dispose();
                }
                catch (Exception ex)
                {
                    failure = ex;
                }

                AssertTrue(
                    failure != null,
                    "the first runtime-owned derived-disposal failure is observable");
                AssertEqual(
                    1,
                    RuntimeOwnedRetryXmlFormHost.DisposeAttempts,
                    "recursive native Form disposal attempts derived cleanup once");
                AssertTrue(
                    ReadPrivateField(
                        typeof(XamlRuntime),
                        runtime,
                        "_ownedMarkupClassTarget") != null,
                    "recursive failure retains the runtime-owned Class target");

                runtime.Dispose();

                AssertEqual(
                    2,
                    RuntimeOwnedRetryXmlFormHost.DisposeAttempts,
                    "runtime disposal retries the retained Class target");
                AssertEqual(
                    1,
                    RuntimeOwnedRetryXmlFormHost.SuccessfulDisposeCount,
                    "the retained Class target completes cleanup once");
                AssertTrue(
                    ReadPrivateField(
                        typeof(XamlRuntime),
                        runtime,
                        "_ownedMarkupClassTarget") == null,
                    "successful retry releases runtime ownership of the Class target");

                runtime.Dispose();
                AssertEqual(
                    2,
                    RuntimeOwnedRetryXmlFormHost.DisposeAttempts,
                    "completed runtime-owned cleanup is idempotent");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestExplicitDisposalInvalidatesDeferredUserClose()
        {
            EmbeddedXmlFormHost host =
                new EmbeddedXmlFormHost();
            ManualResetEvent started =
                new ManualResetEvent(false);
            ManualResetEvent release =
                new ManualResetEvent(false);
            ManualResetEvent stopped =
                new ManualResetEvent(false);
            Thread worker = null;

            try
            {
                Form form = host.WinForm;
                AssertTrue(
                    form.Handle != IntPtr.Zero,
                    "the deferred-close Form owns a native handle");
                worker = host.StartDelayedWorker(
                    started,
                    release,
                    stopped);
                AssertTrue(
                    started.WaitOne(2000, false),
                    "the deferred-close worker starts");

                FormClosingEventArgs closing =
                    RaiseFormClosing(
                        form,
                        CloseReason.UserClosing);
                AssertTrue(
                    closing.Cancel,
                    "UserClosing is deferred while an owned worker is live");

                release.Set();
                AssertTrue(
                    stopped.WaitOne(2000, false),
                    "the deferred-close worker returns");
                AssertTrue(
                    worker.Join(2000),
                    "the deferred-close worker terminates");

                int postStartedAt = Environment.TickCount;

                while (!ReadPrivateBoolean(
                        host,
                        "_deferredClosePosted") &&
                    unchecked(
                        Environment.TickCount - postStartedAt) < 2000)
                {
                    Thread.Sleep(1);
                }

                AssertTrue(
                    ReadPrivateBoolean(
                        host,
                        "_deferredClosePosted"),
                    "worker completion queues the deferred UserClosing replay");

                int queuedEpoch = ReadPrivateInt32(
                    host,
                    "_deferredCloseEpoch");

                host.Dispose();

                AssertTrue(
                    !ReadPrivateBoolean(
                        host,
                        "_deferredClosePosted") &&
                    !ReadPrivateBoolean(
                        host,
                        "_closeWhenThreadsStop"),
                    "explicit disposal clears queued close state");
                AssertTrue(
                    ReadPrivateInt32(
                        host,
                        "_deferredCloseEpoch") != queuedEpoch,
                    "explicit disposal invalidates the queued close epoch");

                Application.DoEvents();
            }
            finally
            {
                release.Set();

                if (worker != null)
                    worker.Join(2000);

                host.Dispose();
                started.Close();
                release.Close();
                stopped.Close();
            }
        }

        private static void TestLaterNonUserCloseCancellationRestoresWorkerAdmission()
        {
            EmbeddedXmlFormHost host =
                new EmbeddedXmlFormHost();
            ManualResetEvent firstStarted =
                new ManualResetEvent(false);
            ManualResetEvent firstStopped =
                new ManualResetEvent(false);
            ManualResetEvent replacementStarted =
                new ManualResetEvent(false);
            ManualResetEvent replacementStopped =
                new ManualResetEvent(false);
            Thread firstWorker = null;
            Thread replacementWorker = null;
            FormClosingEventHandler cancelLater = null;

            try
            {
                Form form = host.WinForm;
                AssertTrue(
                    form.Handle != IntPtr.Zero,
                    "the cancellation observer Form owns a native handle");
                firstWorker = host.StartTrackedWorker(
                    firstStarted,
                    firstStopped);
                AssertTrue(
                    firstStarted.WaitOne(2000, false),
                    "the non-UserClosing worker starts");

                cancelLater =
                    delegate(
                        object sender,
                        FormClosingEventArgs e)
                    {
                        if (e.CloseReason ==
                            CloseReason.ApplicationExitCall)
                        {
                            e.Cancel = true;
                        }
                    };
                form.FormClosing += cancelLater;

                FormClosingEventArgs closing =
                    RaiseFormClosing(
                        form,
                        CloseReason.ApplicationExitCall);
                AssertTrue(
                    closing.Cancel,
                    "a later FormClosing subscriber cancels the non-user close");
                AssertTrue(
                    firstStopped.WaitOne(2000, false),
                    "the canceled close still stops the current worker");
                AssertTrue(
                    firstWorker.Join(2000),
                    "the canceled-close worker terminates");

                int startedAt = Environment.TickCount;

                do
                {
                    Application.DoEvents();

                    try
                    {
                        replacementWorker =
                            host.StartTrackedWorker(
                                replacementStarted,
                                replacementStopped);
                        break;
                    }
                    catch (ObjectDisposedException)
                    {
                        Thread.Sleep(1);
                    }
                }
                while (unchecked(
                    Environment.TickCount - startedAt) < 2000);

                AssertTrue(
                    replacementWorker != null,
                    "later non-user cancellation restores RunThread admission");
                AssertTrue(
                    replacementStarted.WaitOne(2000, false),
                    "the replacement worker starts after cancellation");
            }
            finally
            {
                if (cancelLater != null && host.IsLoaded)
                    host.WinForm.FormClosing -= cancelLater;

                host.Dispose();

                if (firstWorker != null)
                    firstWorker.Join(2000);

                if (replacementWorker != null)
                {
                    AssertTrue(
                        replacementStopped.WaitOne(2000, false),
                        "XmlForm disposal stops the replacement worker");
                    replacementWorker.Join(2000);
                }

                firstStarted.Close();
                firstStopped.Close();
                replacementStarted.Close();
                replacementStopped.Close();
            }
        }

        private static void TestCompletedWorkerRetiresAfterThreadTermination()
        {
            EmbeddedXmlFormHost host =
                new EmbeddedXmlFormHost();
            ManualResetEvent started =
                new ManualResetEvent(false);
            ManualResetEvent release =
                new ManualResetEvent(false);
            ManualResetEvent stopped =
                new ManualResetEvent(false);
            Thread worker = null;

            try
            {
                AssertTrue(
                    host.WinForm != null,
                    "the retirement-probe XmlForm loads");
                worker = host.StartDelayedWorker(
                    started,
                    release,
                    stopped);
                AssertTrue(
                    started.WaitOne(2000, false),
                    "the retirement-probe worker starts");

                object owned = GetTrackedOwnedThread(
                    host,
                    worker);
                AssertTrue(
                    owned != null,
                    "a live RunThread worker is tracked");
                FieldInfo retiredField = owned.GetType().GetField(
                    "Retired",
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);
                AssertTrue(
                    retiredField != null,
                    "owned workers expose explicit retirement state");
                AssertTrue(
                    !Convert.ToBoolean(
                        retiredField.GetValue(owned),
                        System.Globalization.CultureInfo.InvariantCulture),
                    "a live worker is not retired");

                release.Set();
                AssertTrue(
                    stopped.WaitOne(2000, false),
                    "the retirement-probe delegate completes");

                object completionSnapshot =
                    GetTrackedOwnedThread(
                        host,
                        worker);
                bool alreadyTerminated = worker.Join(0);
                AssertTrue(
                    completionSnapshot != null || alreadyTerminated,
                    "a delegate-complete worker remains tracked until thread termination");
                AssertTrue(
                    worker.Join(2000),
                    "the retirement-probe thread terminates");

                int startedAt = Environment.TickCount;

                while (GetTrackedOwnedThread(host, worker) != null &&
                    unchecked(
                        Environment.TickCount - startedAt) < 2000)
                {
                    Thread.Sleep(1);
                }

                AssertTrue(
                    GetTrackedOwnedThread(host, worker) == null,
                    "a physically terminated worker is removed from tracking");
                AssertTrue(
                    Convert.ToBoolean(
                        retiredField.GetValue(owned),
                        System.Globalization.CultureInfo.InvariantCulture),
                    "worker retirement is recorded before tracking releases it");
            }
            finally
            {
                release.Set();

                if (worker != null)
                    worker.Join(2000);

                host.Dispose();
                started.Close();
                release.Close();
                stopped.Close();
            }
        }

        private static void TestXmlFormPartialResourceDiagnostics()
        {
            InvalidPartialXmlFormHost host =
                new InvalidPartialXmlFormHost();
            WinFormsXamlLoadException failure = null;

            try
            {
                AssertTrue(
                    host.WinForm != null,
                    "the invalid partial XmlForm unexpectedly loaded");
            }
            catch (WinFormsXamlLoadException ex)
            {
                failure = ex;
            }
            finally
            {
                host.Dispose();
            }

            AssertTrue(
                failure != null,
                "an invalid partial XmlForm resource reports its load failure");
            AssertEqual(
                "WinFormsXaml.Tests.Fixtures.InvalidDiagnosticForm.xml",
                failure.MarkupSource,
                "partial XmlForm diagnostics use the resolved manifest name");
        }

        private static void TestXmlFormPropertyNotifications()
        {
            NotifyingXmlFormHost host =
                new NotifyingXmlFormHost();
            INotifyPropertyChanged notifier = host;
            int eventCount = 0;
            object lastSender = null;
            string lastPropertyName = null;
            string valueObservedByHandler = null;
            PropertyChangedEventHandler handler =
                delegate(
                    object sender,
                    PropertyChangedEventArgs e)
                {
                    eventCount++;
                    lastSender = sender;
                    lastPropertyName = e.PropertyName;
                    valueObservedByHandler = host.Title;
                };

            notifier.PropertyChanged += handler;

            try
            {
                AssertTrue(
                    !host.SetTitle(null),
                    "SetProperty ignores an equal default value");
                AssertEqual(
                    0,
                    eventCount,
                    "an equal value raises no notification");

                AssertTrue(
                    host.SetTitle("Ready"),
                    "SetProperty reports a changed value");
                AssertEqual(
                    "Ready",
                    host.Title,
                    "SetProperty stores the new value before notification");
                AssertEqual(
                    1,
                    eventCount,
                    "a changed value raises exactly one notification");
                AssertTrue(
                    Object.ReferenceEquals(host, lastSender),
                    "PropertyChanged uses the XmlForm as its sender");
                AssertEqual(
                    "Title",
                    lastPropertyName,
                    "PropertyChanged preserves the supplied property name");
                AssertEqual(
                    "Ready",
                    valueObservedByHandler,
                    "the event observes the committed field value");
                AssertEqual(
                    1,
                    host.PropertyChangedHookCount,
                    "SetProperty dispatches through the virtual hook");

                string equalButDistinct =
                    new string(
                        new char[]
                        {
                            'R', 'e', 'a', 'd', 'y'
                        });

                AssertTrue(
                    !Object.ReferenceEquals(
                        host.Title,
                        equalButDistinct),
                    "the equality fixture uses distinct string instances");
                AssertTrue(
                    !host.SetTitle(equalButDistinct),
                    "SetProperty uses EqualityComparer<T>.Default");
                AssertEqual(
                    1,
                    eventCount,
                    "a semantically equal value raises no notification");

                AssertTrue(
                    host.SetTitle(null),
                    "SetProperty supports a null transition");
                AssertEqual(
                    2,
                    eventCount,
                    "a null transition raises one notification");

                notifier.PropertyChanged -= handler;
                AssertTrue(
                    host.SetTitle("Detached"),
                    "SetProperty still works without subscribers");
                AssertEqual(
                    2,
                    eventCount,
                    "removed handlers receive no notification");
                AssertEqual(
                    3,
                    host.PropertyChangedHookCount,
                    "the virtual hook runs without public subscribers");

                notifier.PropertyChanged += handler;
                host.RaisePropertyChanged(
                    new PropertyChangedEventArgs(String.Empty));
                AssertEqual(
                    3,
                    eventCount,
                    "OnPropertyChanged supports an all-properties notification");
                AssertEqual(
                    String.Empty,
                    lastPropertyName,
                    "the virtual hook preserves an empty all-properties name");
                AssertEqual(
                    String.Empty,
                    host.LastHookPropertyName,
                    "derived hooks receive the original event arguments");

                string unchanged = host.Title;
                bool nullNameRejected = false;

                try
                {
                    host.SetTitle("Invalid", null);
                }
                catch (ArgumentNullException ex)
                {
                    nullNameRejected =
                        ex.ParamName == "propertyName";
                }

                AssertTrue(
                    nullNameRejected,
                    "SetProperty rejects a null property name");
                AssertEqual(
                    unchanged,
                    host.Title,
                    "invalid metadata cannot mutate the field");

                bool emptyNameRejected = false;

                try
                {
                    host.SetTitle("Invalid", String.Empty);
                }
                catch (ArgumentException ex)
                {
                    emptyNameRejected =
                        ex.ParamName == "propertyName";
                }

                AssertTrue(
                    emptyNameRejected,
                    "SetProperty rejects an empty property name");
                AssertEqual(
                    unchanged,
                    host.Title,
                    "an empty property name cannot mutate the field");

                bool whitespaceNameRejected = false;

                try
                {
                    host.SetTitle("Invalid", "   ");
                }
                catch (ArgumentException ex)
                {
                    whitespaceNameRejected =
                        ex.ParamName == "propertyName";
                }

                AssertTrue(
                    whitespaceNameRejected,
                    "SetProperty rejects a whitespace property name");
                AssertEqual(
                    unchanged,
                    host.Title,
                    "a whitespace property name cannot mutate the field");

                bool nullArgumentsRejected = false;

                try
                {
                    host.RaisePropertyChanged(null);
                }
                catch (ArgumentNullException ex)
                {
                    nullArgumentsRejected = ex.ParamName == "e";
                }

                AssertTrue(
                    nullArgumentsRejected,
                    "OnPropertyChanged rejects null event arguments");
                AssertEqual(
                    4,
                    host.PropertyChangedHookCount,
                    "rejected event arguments do not complete the virtual hook");
            }
            finally
            {
                notifier.PropertyChanged -= handler;
                host.Dispose();
            }
        }

        private static void TestBulkComponentRegistration()
        {
            XamlRuntime.Register(
                Assembly.GetExecutingAssembly(),
                "UI.Components");

            // A broad component folder may also contain Forms, preset sets, or
            // other well-formed XML documents. They are not component failures.
            XamlRuntime.Register(
                Assembly.GetExecutingAssembly(),
                "UI.Components.NonComponents");

            XamlRuntime runtime = XamlRuntime.Load(
                "<FlowLayoutPanel>" +
                "  <GlobAlpha Name='Alpha' Caption='First' />" +
                "  <GlobBeta Name='Beta' Caption='Second' />" +
                "</FlowLayoutPanel>");

            try
            {
                AssertEqual(
                    "First",
                    runtime.Get<Label>("Alpha").Text,
                    "bulk registration loads the first component");
                AssertEqual(
                    "Second",
                    runtime.Get<Label>("Beta").Text,
                    "bulk registration loads the second component");
            }
            finally
            {
                runtime.Dispose();
            }

            // Register is idempotent for the same resource batch.
            XamlRuntime.Register(
                Assembly.GetExecutingAssembly(),
                "UI/Components");
        }

        private static void TestRegistrationDiagnostics()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            InvalidOperationException failure = null;

            try
            {
                XamlRuntime.Register();
            }
            catch (InvalidOperationException ex)
            {
                failure = ex;
            }

            AssertTrue(
                failure != null,
                "parameterless Register scans the calling assembly");
            AssertContains(
                failure.Message,
                "WinFormsXaml.Tests.Fixtures.InvalidMultipleChildren.xml",
                "scan-all Register validates discovered Component documents");
            AssertContains(
                failure.Message,
                assembly.FullName,
                "scan-all Register reports the calling assembly");

            string[] scanAllFragments =
                new string[] { String.Empty, "   \t" };
            int scanIndex;

            for (scanIndex = 0;
                 scanIndex < scanAllFragments.Length;
                 scanIndex++)
            {
                failure = null;

                try
                {
                    XamlRuntime.Register(
                        assembly,
                        scanAllFragments[scanIndex]);
                }
                catch (InvalidOperationException ex)
                {
                    failure = ex;
                }

                AssertTrue(
                    failure != null,
                    "empty and whitespace fragments scan the whole assembly");
                AssertContains(
                    failure.Message,
                    "WinFormsXaml.Tests.Fixtures.InvalidMultipleChildren.xml",
                    "explicit scan-all registration validates Component documents");
            }

            failure = null;

            try
            {
                XamlRuntime.Register(
                    assembly,
                    "Definitely.Missing.Component.Fragment");
            }
            catch (InvalidOperationException ex)
            {
                failure = ex;
            }

            AssertTrue(failure != null, "a missing component fragment fails");
            AssertContains(
                failure.Message,
                "Available embedded XML resources:",
                "component discovery reports candidate resources");
            AssertContains(
                failure.Message,
                "WinFormsXaml.Tests.Fixtures.DiagnosticCard.xml",
                "component discovery reports a deterministic candidate");
            AssertContains(
                failure.Message,
                "in assembly '" + assembly.FullName + "'",
                "component discovery reports its assembly provenance");

            failure = null;

            try
            {
                XamlRuntime.Register(
                    assembly,
                    "WinFormsXaml.Tests.UI.Components.NonComponents." +
                    "IgnoredForm.xml");
            }
            catch (InvalidOperationException ex)
            {
                failure = ex;
            }

            AssertTrue(
                failure != null,
                "an exact non-Component resource remains strict");
            AssertContains(
                failure.Message,
                "must have a <Component> or <Includes> root",
                "exact registration reports the supported reusable roots");

            failure = null;

            try
            {
                XamlRuntime.Register(
                    assembly,
                    "InvalidMultipleChildren");
            }
            catch (InvalidOperationException ex)
            {
                failure = ex;
            }

            AssertTrue(
                failure != null,
                "a malformed Component in a fragment remains an error");
            AssertContains(
                failure.Message,
                "more than one <Children>",
                "fragment registration validates Component roots strictly");

            XamlRuntime exactFormMatch =
                XamlRuntime.LoadEmbedded(
                    assembly,
                    "WinFormsXaml.Tests.UI.Ambiguity.West.SharedForm.xml",
                    null);

            try
            {
                AssertEqual(
                    "RightSharedForm",
                    exactFormMatch.Form.Name,
                    "an exact embedded Form resource loads directly");
            }
            finally
            {
                exactFormMatch.Dispose();
            }

            failure = null;

            string upperCaseResource =
                "WinFormsXaml.Tests.UI.CaseVariants.SharedCaseCard.xml";
            string lowerCaseResource =
                "winformsxaml.tests.ui.casevariants.sharedcasecard.xml";

            try
            {
                XamlRuntime.Register(
                    assembly,
                    "WINFORMSXAML.TESTS.UI.CASEVARIANTS.SHAREDCASECARD.XML");
            }
            catch (InvalidOperationException ex)
            {
                failure = ex;
            }

            AssertTrue(
                failure != null,
                "case-only non-exact component resource names are ambiguous");
            AssertContains(
                failure.Message,
                upperCaseResource,
                "case-only ambiguity reports the upper-case candidate");
            AssertContains(
                failure.Message,
                lowerCaseResource,
                "case-only ambiguity reports the lower-case candidate");
            AssertTrue(
                failure.Message.IndexOf(
                    upperCaseResource,
                    StringComparison.Ordinal) <
                failure.Message.IndexOf(
                    lowerCaseResource,
                    StringComparison.Ordinal),
                "case-only component candidates use an ordinal tie-break");

            // A truly exact manifest name wins even when a case-only sibling
            // exists, so selection never depends on manifest enumeration.
            XamlRuntime.Register(
                assembly,
                upperCaseResource);

            failure = null;

            try
            {
                XamlRuntime.Register(
                    assembly,
                    "UI.Collision");
            }
            catch (InvalidOperationException ex)
            {
                failure = ex;
            }

            AssertTrue(
                failure != null,
                "a duplicate derived component name fails the batch");
            AssertContains(
                failure.Message,
                "WinFormsXaml.Tests.UI.Collision.One.SharedCard.xml",
                "a duplicate component name reports its first resource");
            AssertContains(
                failure.Message,
                "WinFormsXaml.Tests.UI.Collision.Two.SharedCard.xml",
                "a duplicate component name reports its second resource");
            AssertTrue(
                failure.Message.IndexOf(
                    "WinFormsXaml.Tests.UI.Collision.One.SharedCard.xml",
                    StringComparison.Ordinal) <
                failure.Message.IndexOf(
                    "WinFormsXaml.Tests.UI.Collision.Two.SharedCard.xml",
                    StringComparison.Ordinal),
                "duplicate component resources use deterministic ordering");
            AssertContains(
                failure.Message,
                assembly.FullName,
                "a duplicate component name reports its assembly provenance");

            failure = null;

            try
            {
                XamlRuntime.Register(
                    "GlobAlpha",
                    typeof(Button));
            }
            catch (InvalidOperationException ex)
            {
                failure = ex;
            }

            AssertTrue(
                failure != null,
                "a conflicting component registration fails");
            AssertContains(
                failure.Message,
                "WinFormsXaml.Tests.UI.Components.GlobAlpha.xml",
                "a conflict reports the existing resource provenance");
            AssertContains(
                failure.Message,
                "System.Windows.Forms.Button",
                "a conflict reports the attempted CLR provenance");
            AssertContains(
                failure.Message,
                assembly.FullName,
                "a conflict reports the existing resource assembly");
            AssertContains(
                failure.Message,
                typeof(Button).Assembly.FullName,
                "a conflict reports the attempted CLR assembly");
        }

        private static FormClosingEventArgs RaiseFormClosing(
            Form form,
            CloseReason closeReason)
        {
            MethodInfo onFormClosing = typeof(Form).GetMethod(
                "OnFormClosing",
                BindingFlags.Instance |
                BindingFlags.NonPublic,
                null,
                new Type[] { typeof(FormClosingEventArgs) },
                null);
            AssertTrue(
                onFormClosing != null,
                "Form.OnFormClosing is available");

            FormClosingEventArgs e =
                new FormClosingEventArgs(
                    closeReason,
                    false);
            onFormClosing.Invoke(
                form,
                new object[] { e });
            return e;
        }

        private static object ReadPrivateField(
            Type declaringType,
            object target,
            string fieldName)
        {
            FieldInfo field = declaringType.GetField(
                fieldName,
                BindingFlags.Instance |
                BindingFlags.NonPublic);
            AssertTrue(
                field != null,
                declaringType.FullName +
                " exposes the expected " +
                fieldName +
                " lifecycle field");
            return field.GetValue(target);
        }

        private static object ReadPrivateStaticField(
            Type declaringType,
            string fieldName)
        {
            FieldInfo field = declaringType.GetField(
                fieldName,
                BindingFlags.Static |
                BindingFlags.NonPublic);
            AssertTrue(
                field != null,
                declaringType.FullName +
                " exposes the expected " +
                fieldName +
                " static field");
            return field.GetValue(null);
        }

        private static bool ReadPrivateBoolean(
            XmlForm host,
            string fieldName)
        {
            object sync = ReadPrivateField(
                typeof(XmlForm),
                host,
                "_ownedThreadsSync");

            lock (sync)
            {
                return Convert.ToBoolean(
                    ReadPrivateField(
                        typeof(XmlForm),
                        host,
                        fieldName),
                    System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        private static int ReadPrivateInt32(
            XmlForm host,
            string fieldName)
        {
            object sync = ReadPrivateField(
                typeof(XmlForm),
                host,
                "_ownedThreadsSync");

            lock (sync)
            {
                return Convert.ToInt32(
                    ReadPrivateField(
                        typeof(XmlForm),
                        host,
                        fieldName),
                    System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        private static object GetTrackedOwnedThread(
            XmlForm host,
            Thread worker)
        {
            object sync = ReadPrivateField(
                typeof(XmlForm),
                host,
                "_ownedThreadsSync");
            IList ownedThreads = (IList)ReadPrivateField(
                typeof(XmlForm),
                host,
                "_ownedThreads");

            lock (sync)
            {
                int i;

                for (i = 0; i < ownedThreads.Count; i++)
                {
                    object owned = ownedThreads[i];
                    FieldInfo threadField = owned.GetType().GetField(
                        "Thread",
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic);

                    if (threadField != null &&
                        Object.ReferenceEquals(
                            threadField.GetValue(owned),
                            worker))
                    {
                        return owned;
                    }
                }
            }

            return null;
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private static void AssertContains(
            string value,
            string expected,
            string message)
        {
            AssertTrue(
                value != null &&
                value.IndexOf(
                    expected,
                    StringComparison.Ordinal) >= 0,
                message);
        }

        private static void RaiseClick(Button button)
        {
            AssertTrue(button != null, "the event source button exists");

            MethodInfo onClick = typeof(Button).GetMethod(
                "OnClick",
                BindingFlags.Instance |
                BindingFlags.NonPublic);

            AssertTrue(onClick != null, "Button.OnClick is available");
            onClick.Invoke(
                button,
                new object[] { EventArgs.Empty });
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
                    ". Expected: " +
                    (expected == null ? "<null>" : expected.ToString()) +
                    "; actual: " +
                    (actual == null ? "<null>" : actual.ToString()) +
                    ".");
            }
        }
    }
}
