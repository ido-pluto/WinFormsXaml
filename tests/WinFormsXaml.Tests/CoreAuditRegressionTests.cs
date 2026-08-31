using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Schema;
using WinFormsXaml;

namespace WinFormsXaml.Tests.AmbiguousOne
{
    public sealed class AmbiguousAuditControl : Control
    {
    }
}

namespace WinFormsXaml.Tests.AmbiguousTwo
{
    public sealed class AmbiguousAuditControl : Control
    {
    }
}

namespace WinFormsXaml.Tests
{
    public sealed class EqualAuditControl : Control
    {
        public override bool Equals(object value)
        {
            return value is EqualAuditControl;
        }

        public override int GetHashCode()
        {
            return 1;
        }
    }

    public sealed class RecordingHyperlinkLabel : HyperlinkLabel
    {
        private string _openedUri;
        private int _openCount;

        public string OpenedUri
        {
            get { return _openedUri; }
        }

        public int OpenCount
        {
            get { return _openCount; }
        }

        public void ActivateLink()
        {
            LinkLabel.Link link =
                new LinkLabel.Link(0, Text.Length);
            OnLinkClicked(
                new LinkLabelLinkClickedEventArgs(link));
        }

        protected override void OpenNavigateUri(string navigateUri)
        {
            _openedUri = navigateUri;
            _openCount++;
        }
    }

    internal sealed class HyperlinkBindingAuditState
    {
        public readonly PropertyBinding<string> DocumentationUri =
            new PropertyBinding<string>("https://example.test/initial");
        public int RequestCount;
        public string RequestedUri;

        public void Documentation_RequestNavigate(
            object sender,
            HyperlinkNavigateEventArgs e)
        {
            RequestCount++;
            RequestedUri = e.NavigateUri;
            e.Handled = true;
        }
    }

    public sealed class ThrowingRollbackControl : Control
    {
        public string FailingProperty
        {
            get { return String.Empty; }
            set
            {
                throw new InvalidOperationException(
                    "Primary construction failure.");
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                throw new InvalidOperationException(
                    "Secondary rollback cleanup failure.");
            }
        }
    }

    public sealed class ThrowingAuditCollectionHost
    {
        private readonly ThrowingAuditCollection _items =
            new ThrowingAuditCollection();

        public ThrowingAuditCollection Items
        {
            get { return _items; }
        }
    }

    public sealed class ThrowingAuditCollectionGetterHost
    {
        public ThrowingAuditCollection Items
        {
            get
            {
                throw new InvalidOperationException(
                    "Primary collection getter failure.");
            }
        }
    }

    public sealed class CaseDistinctReflectionAuditTarget
    {
        public string Value
        {
            get { return "upper"; }
        }

        public string value
        {
            get { return "lower"; }
        }

        public event EventHandler Changed
        {
            add { }
            remove { }
        }

        public event EventHandler changed
        {
            add { }
            remove { }
        }
    }

    public sealed class BindingLookupAuditValue
    {
        private string _caption;

        public string Caption
        {
            get { return _caption; }
            set { _caption = value; }
        }

        public string this[int index]
        {
            get { return index.ToString(); }
        }

        public int Version;
    }

    public sealed class ItemRefreshAuditAddress
    {
        public string City;
    }

    public sealed class ItemRefreshAuditCustomer
    {
        public readonly ItemRefreshAuditAddress Address =
            new ItemRefreshAuditAddress();
    }

    public sealed class ItemRefreshAuditRow
    {
        private readonly ItemRefreshAuditCustomer _customer =
            new ItemRefreshAuditCustomer();

        public static int CustomerReadCount;
        public string Id;
        public int Version;
        public string Caption;

        public ItemRefreshAuditCustomer Customer
        {
            get
            {
                CustomerReadCount++;
                return _customer;
            }
        }
    }

    public sealed class ThrowingAuditCollection
    {
        public static int AddCount;

        public void Add(TrackedAuditCollectionValue value)
        {
            AddCount++;
            throw new InvalidOperationException(
                "Primary collection add failure.");
        }
    }

    public sealed class TrackedAuditCollectionValue : IDisposable
    {
        public static int DisposeCount;

        public void Dispose()
        {
            DisposeCount++;
        }
    }

    internal sealed class ThrowingAuditEnumerable : IEnumerable<int>
    {
        public IEnumerator<int> GetEnumerator()
        {
            return new ThrowingAuditEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        private sealed class ThrowingAuditEnumerator : IEnumerator<int>
        {
            private int _position = -1;

            public int Current
            {
                get { return 7; }
            }

            object IEnumerator.Current
            {
                get { return Current; }
            }

            public bool MoveNext()
            {
                _position++;

                if (_position == 0)
                    return true;

                throw new InvalidOperationException(
                    "Source enumeration failure.");
            }

            public void Reset()
            {
                _position = -1;
            }

            public void Dispose()
            {
            }
        }
    }

    internal sealed class ReplaceAuditItem : INotifyPropertyChanged
    {
        private string _text;

        public ReplaceAuditItem(string text)
        {
            _text = text;
        }

        public string Text
        {
            get { return _text; }
            set
            {
                if (String.Equals(_text, value, StringComparison.Ordinal))
                    return;

                _text = value;
                PropertyChangedEventHandler handler = PropertyChanged;

                if (handler != null)
                {
                    handler(
                        this,
                        new PropertyChangedEventArgs("Text"));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    internal static class CoreAuditRegressionTests
    {
        public static void Run()
        {
            TestMarkupAndPresetsRejectDtds();
            TestSchemaContracts();
            TestCanonicalItemsControlType();
            TestHyperlinkLabelNavigation();
            TestItemsBindingAddRangeSnapshotsInput();
            TestItemsBindingReplacePublishesMinimalDiffs();
            TestItemsBindingReentrantReplaceKeepsNewestResult();
            TestItemsBindingFailedReentrantReplaceKeepsOuterRequest();
            TestItemsBindingReplaceMinimizesLargeRotations();
            TestItemsBindingReplaceHandlesDuplicateNullOccurrences();
            TestItemsBindingReplacePreservesItemNotifications();
            TestValueParseCachesAreBounded();
            TestOptionalRuntimeMetadataCachesAreLazyAndReleased();
            TestConvertedMarkupValuesAreReusedAndReleased();
            TestRuntimeMetadataCachesAreBoundedAndReleased();
            TestReflectionCachesUseTypeBuckets();
            TestReflectionCachesAreBounded();
            TestBindingLookupCachesAreBounded();
            TestObservableTargetPropertyCacheIsBounded();
            TestStaticTextIsNotADynamicExpression();
            TestStyleBasedOnChainRemainsOrdered();
            TestImplicitStyleMatchPlansAreReusedAndReleased();
            TestItemChangesUseCompiledSlotsWithoutVersionContract();
            TestVirtualItemChangesUseCompiledSlotsWithoutVersionContract();
            TestObservedItemChangesUseCompiledSlotsWithoutVersionContract();
            TestEqualItemVersionRetainsFastContract();
            TestDirectViewportIsSynchronousAndSchedulerFree();
            TestElementMetadataUsesReferenceIdentity();
            TestRollbackPreservesConstructionFailure();
            TestAmbiguousSimpleTypeIsRejected();
            TestDisposedLayoutHostReleasesRuntime();
            TestCollectionAddFailureIsNotRetried();
            TestPropertyElementGetterFailureIsPreserved();
            RuntimeOwnershipRegressionTests.Run();
        }

        private static void TestCanonicalItemsControlType()
        {
            XamlRuntime runtime = XamlRuntime.Load(
                "<ItemsControl Name='Rows' />");

            try
            {
                ItemsControl canonical =
                    runtime.Get<ItemsControl>("Rows");
                XamlRuntime.ItemsControl established =
                    runtime.GetItemsControl("Rows");

                AssertTrue(
                    canonical.GetType() == typeof(ItemsControl),
                    "XML ItemsControl uses the canonical top-level public type");
                AssertTrue(
                    Object.ReferenceEquals(canonical, established),
                    "canonical ItemsControl preserves the established nested API");
                AssertTrue(
                    !typeof(ItemsControl).IsSealed,
                    "canonical ItemsControl remains extensible");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestHyperlinkLabelNavigation()
        {
            XamlRuntime runtime = XamlRuntime.Load(
                "<Panel>" +
                " <HyperlinkLabel Name='DocsLink' " +
                "   Content='Open documentation' " +
                "   NavigateUri='https://example.test/docs' />" +
                " <LinkLabel Name='NativeLinks' Text='Docs and support'>" +
                "  <LinkLabel.Links>" +
                "   <Link Start='0' Length='4' " +
                "    LinkData='https://example.test/docs' />" +
                "   <Link Start='9' Length='7' Enabled='false' />" +
                "  </LinkLabel.Links>" +
                " </LinkLabel>" +
                "</Panel>");

            try
            {
                HyperlinkLabel link =
                    runtime.Get<HyperlinkLabel>("DocsLink");

                AssertTrue(
                    link.GetType() == typeof(HyperlinkLabel),
                    "XML HyperlinkLabel resolves to the public shortcut type");
                AssertTrue(
                    link.Text == "Open documentation",
                    "HyperlinkLabel Content maps to native LinkLabel text");
                AssertTrue(
                    link.NavigateUri == "https://example.test/docs",
                    "HyperlinkLabel receives NavigateUri from XML");
                AssertTrue(
                    !typeof(HyperlinkLabel).IsSealed,
                    "HyperlinkLabel remains extensible");

                LinkLabel nativeLinks =
                    runtime.Get<LinkLabel>("NativeLinks");
                AssertTrue(
                    nativeLinks.Links.Count == 2 &&
                    nativeLinks.Links[0].Start == 0 &&
                    nativeLinks.Links[0].Length == 4 &&
                    String.Equals(
                        nativeLinks.Links[0].LinkData as string,
                        "https://example.test/docs",
                        StringComparison.Ordinal) &&
                    !nativeLinks.Links[1].Enabled,
                    "LinkLabel.Links builds native Link entries from XML");
            }
            finally
            {
                DisposeRuntime(runtime);
            }

            RecordingHyperlinkLabel recording =
                new RecordingHyperlinkLabel();
            int changedCount = 0;
            int clickedCount = 0;
            int requestCount = 0;
            recording.Text = "Open";
            recording.NavigateUriChanged +=
                delegate(object sender, EventArgs e)
                {
                    changedCount++;
                };
            recording.LinkClicked +=
                delegate(
                    object sender,
                    LinkLabelLinkClickedEventArgs e)
                {
                    clickedCount++;
                };
            recording.RequestNavigate +=
                delegate(
                    object sender,
                    HyperlinkNavigateEventArgs e)
                {
                    requestCount++;
                };

            recording.NavigateUri = "https://example.test/first";
            recording.NavigateUri = "https://example.test/first";
            recording.ActivateLink();

            AssertTrue(
                changedCount == 1,
                "NavigateUriChanged ignores equal assignments");
            AssertTrue(
                clickedCount == 1,
                "HyperlinkLabel preserves the native LinkClicked event");
            AssertTrue(
                requestCount == 1,
                "HyperlinkLabel raises one WPF-style navigation request");
            AssertTrue(
                recording.OpenCount == 1 &&
                recording.OpenedUri == "https://example.test/first",
                "link activation opens NavigateUri exactly once");
            AssertTrue(
                recording.LinkVisited,
                "successful navigation marks the link as visited");

            recording.NavigateUri = " ";
            recording.ActivateLink();
            AssertTrue(
                recording.OpenCount == 1,
                "blank NavigateUri does not launch the system shell");
            recording.Dispose();

            RecordingHyperlinkLabel canceled =
                new RecordingHyperlinkLabel();
            string requestedUri = null;
            canceled.Text = "Open";
            canceled.NavigateUri = "https://example.test/captured";
            canceled.LinkClicked +=
                delegate(
                    object sender,
                    LinkLabelLinkClickedEventArgs e)
                {
                    canceled.NavigateUri =
                        "https://example.test/mutated";
                };
            canceled.RequestNavigate +=
                delegate(
                    object sender,
                    HyperlinkNavigateEventArgs e)
                {
                    requestedUri = e.NavigateUri;
                    e.Handled = true;
                };
            canceled.ActivateLink();

            AssertTrue(
                requestedUri == "https://example.test/captured",
                "navigation captures the activation URI before callbacks");
            AssertTrue(
                canceled.OpenCount == 0 && !canceled.LinkVisited,
                "handled RequestNavigate suppresses automatic navigation");
            canceled.Dispose();

            RecordingHyperlinkLabel captured =
                new RecordingHyperlinkLabel();
            captured.Text = "Open";
            captured.NavigateUri = "https://example.test/original";
            captured.LinkClicked +=
                delegate(
                    object sender,
                    LinkLabelLinkClickedEventArgs e)
                {
                    captured.NavigateUri =
                        "https://example.test/replacement";
                };
            captured.ActivateLink();
            AssertTrue(
                captured.OpenedUri == "https://example.test/original",
                "unhandled activation opens its captured URI");
            captured.Dispose();

            HyperlinkBindingAuditState bindingState =
                new HyperlinkBindingAuditState();
            XamlRuntime bindingRuntime = XamlRuntime.Load(
                "<HyperlinkLabel Name='BoundLink' " +
                " NavigateUri='{Binding DocumentationUri}' />",
                bindingState);

            try
            {
                HyperlinkLabel boundLink =
                    bindingRuntime.Get<HyperlinkLabel>("BoundLink");
                AssertTrue(
                    boundLink.NavigateUri ==
                        "https://example.test/initial",
                    "NavigateUri accepts PropertyBinding source values");

                bindingRuntime.RootControl.CreateControl();
                bindingState.DocumentationUri.Value =
                    "https://example.test/updated";
                Application.DoEvents();

                AssertTrue(
                    boundLink.NavigateUri ==
                        "https://example.test/updated",
                    "NavigateUri refreshes when its binding source changes");
            }
            finally
            {
                DisposeRuntime(bindingRuntime);
            }

            HyperlinkBindingAuditState eventState =
                new HyperlinkBindingAuditState();
            XamlRuntime eventRuntime = XamlRuntime.Load(
                "<Object " +
                " Type='WinFormsXaml.Tests.RecordingHyperlinkLabel' " +
                " Name='MarkupLink' Text='Open' " +
                " NavigateUri='https://example.test/markup' " +
                " RequestNavigate='Documentation_RequestNavigate' />",
                eventState);

            try
            {
                RecordingHyperlinkLabel markupLink =
                    eventRuntime.Get<RecordingHyperlinkLabel>(
                        "MarkupLink");
                markupLink.ActivateLink();

                AssertTrue(
                    eventState.RequestCount == 1 &&
                    eventState.RequestedUri ==
                        "https://example.test/markup",
                    "XML binds the custom RequestNavigate delegate");
                AssertTrue(
                    markupLink.OpenCount == 0,
                    "XML RequestNavigate handlers can suppress navigation");

                bool opened = markupLink.Navigate();
                AssertTrue(
                    !opened && eventState.RequestCount == 2,
                    "programmatic Navigate uses the same cancellable request");
            }
            finally
            {
                DisposeRuntime(eventRuntime);
            }
        }

        private static void TestStyleBasedOnChainRemainsOrdered()
        {
            XamlRuntime runtime = XamlRuntime.Load(
                "<Panel>" +
                " <Panel.Resources>" +
                "  <Style Key='Base' TargetType='Label'>" +
                "   <Setter Property='Foreground' Value='Red' />" +
                "  </Style>" +
                "  <Style Key='Derived' TargetType='Label' " +
                "      BasedOn='{StaticResource Base}'>" +
                "   <Setter Property='Background' Value='Blue' />" +
                "  </Style>" +
                " </Panel.Resources>" +
                " <Label Name='Styled' ResourceStyle='Derived' />" +
                " <Label Name='StyledAgain' ResourceStyle='Derived' />" +
                "</Panel>");

            try
            {
                Label styled = runtime.Get<Label>("Styled");
                Label styledAgain = runtime.Get<Label>("StyledAgain");
                AssertTrue(
                    styled.ForeColor == Color.Red &&
                    styledAgain.ForeColor == Color.Red,
                    "a derived style retains its base setters");
                AssertTrue(
                    styled.BackColor == Color.Blue &&
                    styledAgain.BackColor == Color.Blue,
                    "a derived style applies its own setters after the base");

                BindingFlags flags =
                    BindingFlags.Instance | BindingFlags.NonPublic;
                FieldInfo countField = typeof(XamlRuntime).GetField(
                    "_resolvedStyleChainCacheEntryCount",
                    flags);
                FieldInfo hitsField = typeof(XamlRuntime).GetField(
                    "_resolvedStyleChainCacheHitCount",
                    flags);
                FieldInfo cachesField = typeof(XamlRuntime).GetField(
                    "_resolvedStyleChainCaches",
                    flags);
                FieldInfo limitField = typeof(XamlRuntime).GetField(
                    "ResolvedStyleChainCacheLimit",
                    BindingFlags.Static | BindingFlags.NonPublic);
                FieldInfo perScopeLimitField = typeof(XamlRuntime).GetField(
                    "ResolvedStyleChainCachePerScopeLimit",
                    BindingFlags.Static | BindingFlags.NonPublic);
                MethodInfo getChain = typeof(XamlRuntime).GetMethod(
                    "GetResolvedStyleChain",
                    flags);

                AssertTrue(
                    countField != null && hitsField != null &&
                    cachesField != null && limitField != null &&
                    perScopeLimitField != null && getChain != null,
                    "resolved style chain diagnostics remain inspectable");
                AssertEqual(
                    1,
                    (int)countField.GetValue(runtime),
                    "one named style creates one flattened chain");
                AssertTrue(
                    (long)hitsField.GetValue(runtime) >= 1L,
                    "a repeated named style reuses its flattened chain");

                Type styleType = typeof(XamlRuntime).GetNestedType(
                    "StyleDefinition",
                    BindingFlags.NonPublic);
                Type dictionaryType = typeof(Dictionary<,>).MakeGenericType(
                    typeof(string),
                    styleType);
                FieldInfo styleKeyField = styleType.GetField("Key");
                Hashtable caches =
                    cachesField.GetValue(runtime) as Hashtable;
                caches.Clear();
                countField.SetValue(runtime, 0);
                int limit = (int)limitField.GetValue(null);
                int perScope = (int)perScopeLimitField.GetValue(null);
                int scopeIndex;

                for (scopeIndex = 0;
                     scopeIndex < (limit / perScope) + 1;
                     scopeIndex++)
                {
                    IDictionary scope =
                        Activator.CreateInstance(dictionaryType)
                            as IDictionary;
                    int entryIndex;

                    for (entryIndex = 0;
                         entryIndex < perScope + 2;
                         entryIndex++)
                    {
                        object style = Activator.CreateInstance(
                            styleType,
                            true);
                        string key = "Style" + scopeIndex.ToString() +
                            "_" + entryIndex.ToString();
                        styleKeyField.SetValue(style, key);
                        scope.Add(key, style);
                        getChain.Invoke(
                            runtime,
                            new object[] { scope, style, null });
                    }
                }

                AssertEqual(
                    limit,
                    (int)countField.GetValue(runtime),
                    "lazy resolved-style admission preserves the global cap");
                AssertEqual(
                    limit / perScope,
                    caches.Count,
                    "resolved-style scopes preserve their per-scope cap");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestOptionalRuntimeMetadataCachesAreLazyAndReleased()
        {
            Type runtimeType = typeof(XamlRuntime);
            BindingFlags flags =
                BindingFlags.Instance | BindingFlags.NonPublic;
            FieldInfo expressionField = runtimeType.GetField(
                "_templateExpressionPlanCache",
                flags);
            FieldInfo implicitField = runtimeType.GetField(
                "_implicitStyleMatchCaches",
                flags);
            FieldInfo resolvedField = runtimeType.GetField(
                "_resolvedStyleChainCaches",
                flags);
            FieldInfo convertedField = runtimeType.GetField(
                "_convertedStringValueCaches",
                flags);
            FieldInfo compiledField = runtimeType.GetField(
                "_compiledItemTemplates",
                flags);
            MethodInfo getExpressionPlan = runtimeType.GetMethod(
                "GetTemplateExpressionPlan",
                flags);
            MethodInfo convertString = runtimeType.GetMethod(
                "ConvertString",
                flags,
                null,
                new Type[] { typeof(string), typeof(Type) },
                null);

            AssertTrue(
                expressionField != null && implicitField != null &&
                resolvedField != null && convertedField != null &&
                compiledField != null && getExpressionPlan != null &&
                convertString != null,
                "optional runtime metadata caches remain inspectable");

            XamlRuntime minimal = XamlRuntime.Load("<Panel />");

            try
            {
                AssertTrue(
                    expressionField.GetValue(minimal) == null &&
                    implicitField.GetValue(minimal) == null &&
                    resolvedField.GetValue(minimal) == null &&
                    convertedField.GetValue(minimal) == null &&
                    compiledField.GetValue(minimal) == null,
                    "a minimal runtime allocates no optional metadata cache");

                object firstPlan = getExpressionPlan.Invoke(
                    minimal,
                    new object[] { "literal-cache-probe" });
                object secondPlan = getExpressionPlan.Invoke(
                    minimal,
                    new object[] { "literal-cache-probe" });
                IDictionary expressionCache =
                    expressionField.GetValue(minimal) as IDictionary;

                AssertTrue(
                    expressionCache != null && expressionCache.Count == 1 &&
                    Object.ReferenceEquals(firstPlan, secondPlan),
                    "first expression use allocates the cache and the next use hits");

                object firstConverted = convertString.Invoke(
                    minimal,
                    new object[] { "2147483002", typeof(int) });
                object secondConverted = convertString.Invoke(
                    minimal,
                    new object[] { "2147483002", typeof(int) });
                IDictionary convertedCaches =
                    convertedField.GetValue(minimal) as IDictionary;

                AssertTrue(
                    convertedCaches != null && convertedCaches.Count == 1 &&
                    Object.ReferenceEquals(firstConverted, secondConverted),
                    "first invariant conversion allocates its type cache and then hits");

                minimal.Dispose();
                AssertTrue(
                    expressionField.GetValue(minimal) == null &&
                    convertedField.GetValue(minimal) == null,
                    "minimal runtime disposal releases lazily allocated caches");
            }
            finally
            {
                if (!minimal.IsDisposed)
                    minimal.Dispose();
            }

            XamlRuntime styled = XamlRuntime.Load(
                "<Panel>" +
                " <Panel.Resources>" +
                "  <Style TargetType='Label'>" +
                "   <Setter Property='Foreground' Value='Red' />" +
                "  </Style>" +
                "  <Style Key='Base' TargetType='Label'>" +
                "   <Setter Property='Text' Value='Base' />" +
                "  </Style>" +
                "  <Style Key='Derived' TargetType='Label' " +
                "      BasedOn='{StaticResource Base}' />" +
                " </Panel.Resources>" +
                " <Label ResourceStyle='Derived' />" +
                " <Label ResourceStyle='Derived' />" +
                "</Panel>");

            try
            {
                AssertTrue(
                    implicitField.GetValue(styled) != null &&
                    resolvedField.GetValue(styled) != null,
                    "first implicit and named-style use allocates both style caches");
                AssertTrue(
                    (long)runtimeType.GetField(
                        "_implicitStyleMatchCacheHitCount",
                        flags).GetValue(styled) > 0L &&
                    (long)runtimeType.GetField(
                        "_resolvedStyleChainCacheHitCount",
                        flags).GetValue(styled) > 0L,
                    "repeated style use takes both cache hit paths");

                styled.Dispose();
                AssertTrue(
                    implicitField.GetValue(styled) == null &&
                    resolvedField.GetValue(styled) == null,
                    "style runtime disposal releases both lazy caches");
            }
            finally
            {
                if (!styled.IsDisposed)
                    styled.Dispose();
            }

            XamlRuntime templated = XamlRuntime.Load(
                "<ItemsControl Name='Rows' Virtualizing='false' " +
                "ProgressiveRendering='false'>" +
                " <ItemsControl.ItemTemplate>" +
                "  <Label Text='{Binding}' />" +
                " </ItemsControl.ItemTemplate>" +
                "</ItemsControl>");

            try
            {
                ItemsControl rows = templated.Get<ItemsControl>("Rows");
                AssertTrue(
                    compiledField.GetValue(templated) == null,
                    "declaring an unused item template allocates no compiled cache");
                ArrayList items = new ArrayList();
                items.Add("One");
                rows.SetItems(items);
                IDictionary compiled =
                    compiledField.GetValue(templated) as IDictionary;

                AssertTrue(
                    compiled != null && compiled.Count == 1,
                    "first item realization allocates one compiled template");
                rows.ReloadItems();
                AssertTrue(
                    Object.ReferenceEquals(
                        compiled,
                        compiledField.GetValue(templated)) &&
                    compiled.Count == 1,
                    "template reload hits the existing compiled entry");

                templated.Dispose();
                AssertTrue(
                    compiledField.GetValue(templated) == null,
                    "template runtime disposal releases its lazy cache");
            }
            finally
            {
                if (!templated.IsDisposed)
                    templated.Dispose();
            }
        }

        private static void TestConvertedMarkupValuesAreReusedAndReleased()
        {
            XamlRuntime runtime = XamlRuntime.Load("<Panel />");
            BindingFlags flags =
                BindingFlags.Instance | BindingFlags.NonPublic;
            Type runtimeType = typeof(XamlRuntime);
            MethodInfo convert = runtimeType.GetMethod(
                "ConvertString",
                flags,
                null,
                new Type[] { typeof(string), typeof(Type) },
                null);
            FieldInfo cachesField = runtimeType.GetField(
                "_convertedStringValueCaches",
                flags);
            FieldInfo countField = runtimeType.GetField(
                "_convertedStringValueCacheEntryCount",
                flags);
            FieldInfo hitsField = runtimeType.GetField(
                "_convertedStringValueCacheHitCount",
                flags);
            FieldInfo limitField = runtimeType.GetField(
                "ConvertedStringValueCacheLimit",
                BindingFlags.Static | BindingFlags.NonPublic);
            FieldInfo perTypeLimitField = runtimeType.GetField(
                "ConvertedStringValueCachePerTypeLimit",
                BindingFlags.Static | BindingFlags.NonPublic);

            try
            {
                AssertTrue(
                    convert != null && cachesField != null &&
                    countField != null && hitsField != null &&
                    limitField != null && perTypeLimitField != null,
                    "converted markup value cache remains inspectable");

                int originalCount = (int)countField.GetValue(runtime);
                long originalHits = (long)hitsField.GetValue(runtime);
                object first = convert.Invoke(
                    runtime,
                    new object[] { "2147483001", typeof(int) });
                object second = convert.Invoke(
                    runtime,
                    new object[] { "2147483001", typeof(int) });

                AssertTrue(
                    Object.ReferenceEquals(first, second) &&
                    (int)first == 2147483001,
                    "repeated invariant value conversion reuses one boxed value");
                AssertEqual(
                    originalCount + 1,
                    (int)countField.GetValue(runtime),
                    "one novel value creates one bounded conversion entry");
                AssertTrue(
                    (long)hitsField.GetValue(runtime) == originalHits + 1L,
                    "the repeated conversion takes the cache hit path");

                Hashtable caches =
                    cachesField.GetValue(runtime) as Hashtable;
                caches.Clear();
                countField.SetValue(runtime, 0);
                Type[] boundedTypes =
                {
                    typeof(int),
                    typeof(long),
                    typeof(short),
                    typeof(decimal),
                    typeof(byte)
                };
                int perTypeLimit =
                    (int)perTypeLimitField.GetValue(null);
                int totalLimit = (int)limitField.GetValue(null);
                int typeIndex;

                for (typeIndex = 0;
                     typeIndex < boundedTypes.Length;
                     typeIndex++)
                {
                    int valueIndex;

                    for (valueIndex = 0;
                         valueIndex < perTypeLimit + 8;
                         valueIndex++)
                    {
                        convert.Invoke(
                            runtime,
                            new object[]
                            {
                                valueIndex.ToString(),
                                boundedTypes[typeIndex]
                            });
                    }
                }

                AssertEqual(
                    totalLimit,
                    (int)countField.GetValue(runtime),
                    "lazy converted-value admission preserves the global cap");
                AssertEqual(
                    totalLimit / perTypeLimit,
                    caches.Count,
                    "per-type admission stops before a fifth cache is retained");

                foreach (DictionaryEntry entry in caches)
                {
                    AssertEqual(
                        perTypeLimit,
                        ((Hashtable)entry.Value).Count,
                        "each converted-value type cache preserves its cap");
                }

                runtime.Dispose();

                AssertTrue(
                    cachesField.GetValue(runtime) == null,
                    "disposing releases converted markup cache values");
            }
            finally
            {
                if (!runtime.IsDisposed)
                    runtime.Dispose();
            }
        }

        private static void TestRuntimeMetadataCachesAreBoundedAndReleased()
        {
            XamlRuntime runtime = XamlRuntime.Load("<Panel />");
            Type runtimeType = typeof(XamlRuntime);
            BindingFlags flags =
                BindingFlags.Instance | BindingFlags.NonPublic;
            BindingFlags staticFlags =
                BindingFlags.Static | BindingFlags.NonPublic;
            MethodInfo getExpressionPlan = runtimeType.GetMethod(
                "GetTemplateExpressionPlan",
                flags);
            MethodInfo getArgumentParts = runtimeType.GetMethod(
                "GetCachedFunctionArgumentParts",
                flags);
            FieldInfo expressionCacheField = runtimeType.GetField(
                "_templateExpressionPlanCache",
                flags);
            FieldInfo argumentCacheField = runtimeType.GetField(
                "_functionArgumentPartsCache",
                flags);
            FieldInfo functionMethodsCacheField = runtimeType.GetField(
                "_bindingFunctionMethodsCache",
                flags);
            FieldInfo functionParametersCacheField = runtimeType.GetField(
                "_bindingFunctionParametersCache",
                flags);
            FieldInfo functionPlansCacheField = runtimeType.GetField(
                "_bindingFunctionInvocationPlans",
                flags);
            FieldInfo eventMethodsCacheField = runtimeType.GetField(
                "_eventHandlerMethodsCache",
                flags);
            FieldInfo expressionLimitField = runtimeType.GetField(
                "TemplateExpressionPlanCacheLimit",
                staticFlags);
            FieldInfo argumentLimitField = runtimeType.GetField(
                "FunctionArgumentPartsCacheLimit",
                staticFlags);
            FieldInfo keyLimitField = runtimeType.GetField(
                "RuntimeMetadataCacheKeyLengthLimit",
                staticFlags);

            try
            {
                AssertTrue(
                    getExpressionPlan != null && getArgumentParts != null &&
                    expressionCacheField != null &&
                    argumentCacheField != null &&
                    functionMethodsCacheField != null &&
                    functionParametersCacheField != null &&
                    functionPlansCacheField != null &&
                    eventMethodsCacheField != null &&
                    expressionLimitField != null &&
                    argumentLimitField != null && keyLimitField != null,
                    "bounded runtime metadata caches remain inspectable");

                AssertTrue(
                    expressionCacheField.GetValue(runtime) == null &&
                    argumentCacheField.GetValue(runtime) == null,
                    "a runtime without templates or Function bindings allocates no parser cache");
                AssertTrue(
                    functionMethodsCacheField.GetValue(runtime) == null &&
                    functionParametersCacheField.GetValue(runtime) == null &&
                    functionPlansCacheField.GetValue(runtime) == null &&
                    eventMethodsCacheField.GetValue(runtime) == null,
                    "a runtime without Function or event bindings allocates none of their reflection caches");

                getExpressionPlan.Invoke(
                    runtime,
                    new object[] { "warmup" });
                getArgumentParts.Invoke(
                    runtime,
                    new object[] { "warmup" });

                Hashtable expressionCache =
                    expressionCacheField.GetValue(runtime) as Hashtable;
                Hashtable argumentCache =
                    argumentCacheField.GetValue(runtime) as Hashtable;
                int expressionLimit =
                    (int)expressionLimitField.GetValue(null);
                int argumentLimit =
                    (int)argumentLimitField.GetValue(null);
                int keyLimit = (int)keyLimitField.GetValue(null);
                expressionCache.Clear();
                argumentCache.Clear();
                int i;

                for (i = 0; i < expressionLimit + 32; i++)
                {
                    getExpressionPlan.Invoke(
                        runtime,
                        new object[] { "literal-" + i.ToString() });
                }

                for (i = 0; i < argumentLimit + 32; i++)
                {
                    getArgumentParts.Invoke(
                        runtime,
                        new object[]
                        {
                            "Argument" + i.ToString() + ", Other"
                        });
                }

                AssertEqual(
                    expressionLimit,
                    expressionCache.Count,
                    "expression-plan admission stops at its bound");
                AssertEqual(
                    argumentLimit,
                    argumentCache.Count,
                    "Function argument-plan admission stops at its bound");

                int expressionCount = expressionCache.Count;
                object overflowOne = getExpressionPlan.Invoke(
                    runtime,
                    new object[] { "uncached-overflow-expression" });
                object overflowTwo = getExpressionPlan.Invoke(
                    runtime,
                    new object[] { "uncached-overflow-expression" });

                AssertTrue(
                    overflowOne != null && overflowTwo != null &&
                    !Object.ReferenceEquals(overflowOne, overflowTwo) &&
                    expressionCache.Count == expressionCount,
                    "post-cap expressions still compile without being retained");

                expressionCache.Clear();
                argumentCache.Clear();
                string oversized = new string('x', keyLimit + 1);
                getExpressionPlan.Invoke(
                    runtime,
                    new object[] { oversized });
                getArgumentParts.Invoke(
                    runtime,
                    new object[] { oversized });

                AssertTrue(
                    expressionCache.Count == 0 && argumentCache.Count == 0,
                    "oversized metadata keys are evaluated but not retained");

                runtime.Dispose();

                AssertTrue(
                    expressionCacheField.GetValue(runtime) == null &&
                    argumentCacheField.GetValue(runtime) == null,
                    "disposing releases parser metadata caches");
            }
            finally
            {
                if (!runtime.IsDisposed)
                    runtime.Dispose();
            }
        }

        private static void TestImplicitStyleMatchPlansAreReusedAndReleased()
        {
            XamlRuntime runtime = XamlRuntime.Load(
                "<Panel>" +
                " <Panel.Resources>" +
                "  <Style TargetType='Label'>" +
                "   <Setter Property='Foreground' Value='Red' />" +
                "  </Style>" +
                " </Panel.Resources>" +
                " <Label Name='FirstImplicitLabel' />" +
                " <Label Name='SecondImplicitLabel' />" +
                " <Label Name='ThirdImplicitLabel' />" +
                "</Panel>");

            Type runtimeType = typeof(XamlRuntime);
            BindingFlags flags =
                BindingFlags.Instance | BindingFlags.NonPublic;
            FieldInfo cachesField = runtimeType.GetField(
                "_implicitStyleMatchCaches",
                flags);
            FieldInfo countField = runtimeType.GetField(
                "_implicitStyleMatchCacheEntryCount",
                flags);
            FieldInfo hitsField = runtimeType.GetField(
                "_implicitStyleMatchCacheHitCount",
                flags);
            FieldInfo limitField = runtimeType.GetField(
                "ImplicitStyleMatchCacheLimit",
                BindingFlags.Static | BindingFlags.NonPublic);
            FieldInfo perScopeLimitField = runtimeType.GetField(
                "ImplicitStyleMatchCachePerScopeLimit",
                BindingFlags.Static | BindingFlags.NonPublic);
            FieldInfo implicitStylesField = runtimeType.GetField(
                "_implicitStyles",
                flags);
            MethodInfo getMatches = runtimeType.GetMethod(
                "GetMatchingImplicitStyles",
                flags);

            try
            {
                AssertTrue(
                    cachesField != null &&
                    countField != null &&
                    hitsField != null && limitField != null &&
                    perScopeLimitField != null &&
                    implicitStylesField != null && getMatches != null,
                    "implicit style match plan diagnostics remain inspectable");
                AssertTrue(
                    runtime.Get<Label>("FirstImplicitLabel").ForeColor ==
                        Color.Red &&
                    runtime.Get<Label>("SecondImplicitLabel").ForeColor ==
                        Color.Red &&
                    runtime.Get<Label>("ThirdImplicitLabel").ForeColor ==
                        Color.Red,
                    "cached implicit style matches preserve normal output");
                AssertEqual(
                    2,
                    (int)countField.GetValue(runtime),
                    "the Panel and Label types each produce one match plan");
                AssertTrue(
                    (long)hitsField.GetValue(runtime) >= 2L,
                    "later controls reuse the implicit style match plan");

                Type styleType = runtimeType.GetNestedType(
                    "StyleDefinition",
                    BindingFlags.NonPublic);
                Type listType = typeof(List<>).MakeGenericType(styleType);
                IList loadedStyles =
                    implicitStylesField.GetValue(runtime) as IList;
                object style = loadedStyles[0];
                Hashtable caches =
                    cachesField.GetValue(runtime) as Hashtable;
                caches.Clear();
                countField.SetValue(runtime, 0);
                int limit = (int)limitField.GetValue(null);
                int perScope = (int)perScopeLimitField.GetValue(null);
                int scopeIndex;

                using (Label probe = new Label())
                {
                    for (scopeIndex = 0;
                         scopeIndex < (limit / perScope) + 1;
                         scopeIndex++)
                    {
                        IList scope =
                            Activator.CreateInstance(listType) as IList;
                        scope.Add(style);
                        int entryIndex;

                        for (entryIndex = 0;
                             entryIndex < perScope + 2;
                             entryIndex++)
                        {
                            getMatches.Invoke(
                                runtime,
                                new object[]
                                {
                                    scope,
                                    probe,
                                    "LabelAlias" + entryIndex.ToString()
                                });
                        }
                    }
                }

                AssertEqual(
                    limit,
                    (int)countField.GetValue(runtime),
                    "lazy implicit-style admission preserves the global cap");
                AssertEqual(
                    limit / perScope,
                    caches.Count,
                    "implicit-style scopes stop at their per-scope cap");

                runtime.Dispose();

                AssertTrue(
                    cachesField.GetValue(runtime) == null,
                    "disposing releases implicit style scope references");
                AssertEqual(
                    0,
                    (int)countField.GetValue(runtime),
                    "disposing resets the implicit style plan count");
            }
            finally
            {
                if (!runtime.IsDisposed)
                    runtime.Dispose();
            }
        }

        private static void TestItemsBindingAddRangeSnapshotsInput()
        {
            ItemsBinding<int> items = new ItemsBinding<int>();
            int resetCount = 0;
            items.Add(1);
            items.Add(2);
            items.ListChanged +=
                delegate(object sender, ListChangedEventArgs e)
                {
                    if (e.ListChangedType == ListChangedType.Reset)
                        resetCount++;
                };

            items.AddRange(items);

            AssertTrue(items.Count == 4, "AddRange accepts its own list");
            AssertTrue(
                items[0] == 1 && items[1] == 2 &&
                items[2] == 1 && items[3] == 2,
                "self AddRange appends one stable snapshot");
            AssertTrue(
                resetCount == 1,
                "self AddRange publishes one reset notification");

            bool failed = false;

            try
            {
                items.AddRange(new ThrowingAuditEnumerable());
            }
            catch (InvalidOperationException)
            {
                failed = true;
            }

            AssertTrue(failed, "source enumeration failure is preserved");
            AssertTrue(
                items.Count == 4,
                "failed source enumeration cannot partially mutate the list");
            AssertTrue(
                resetCount == 1,
                "failed source enumeration cannot publish a reset");
        }

        private static void TestItemsBindingReplacePublishesMinimalDiffs()
        {
            ReplaceAuditItem first = new ReplaceAuditItem("first");
            ReplaceAuditItem second = new ReplaceAuditItem("second");
            ReplaceAuditItem third = new ReplaceAuditItem("third");
            ReplaceAuditItem replacement =
                new ReplaceAuditItem("replacement");
            ItemsBinding<ReplaceAuditItem> items =
                new ItemsBinding<ReplaceAuditItem>();
            items.Add(first);
            items.Add(second);

            List<ListChangedEventArgs> changes =
                new List<ListChangedEventArgs>();
            items.ListChanged +=
                delegate(object sender, ListChangedEventArgs e)
                {
                    changes.Add(e);
                };

            items.Replace(items);
            AssertTrue(
                changes.Count == 0,
                "self Replace does not publish unchanged work");

            items.Replace(
                new ReplaceAuditItem[] { first, second, third });
            AssertTrue(
                changes.Count == 1 &&
                changes[0].ListChangedType == ListChangedType.ItemAdded &&
                changes[0].NewIndex == 2,
                "Replace publishes one precise insertion");
            changes.Clear();

            items.Replace(
                new ReplaceAuditItem[] { first, third });
            AssertTrue(
                changes.Count == 1 &&
                changes[0].ListChangedType == ListChangedType.ItemDeleted &&
                changes[0].NewIndex == 1,
                "Replace publishes one precise removal");
            changes.Clear();

            items.Replace(
                new ReplaceAuditItem[] { replacement, third });
            AssertTrue(
                changes.Count == 1 &&
                changes[0].ListChangedType == ListChangedType.ItemChanged &&
                changes[0].NewIndex == 0,
                "Replace publishes one precise item replacement");
            changes.Clear();

            first.Text = "detached";
            AssertTrue(
                changes.Count == 0,
                "Replace detaches notifications from the displaced item");
            replacement.Text = "replacement-updated";
            AssertTrue(
                changes.Count == 1 &&
                changes[0].ListChangedType == ListChangedType.ItemChanged &&
                changes[0].NewIndex == 0,
                "Replace subscribes to the replacement item");
            changes.Clear();

            items.Replace(
                new ReplaceAuditItem[] { third, replacement });
            AssertTrue(
                changes.Count == 1 &&
                changes[0].ListChangedType == ListChangedType.ItemMoved &&
                changes[0].OldIndex == 1 &&
                changes[0].NewIndex == 0,
                "Replace publishes one precise move");
            AssertTrue(
                Object.ReferenceEquals(items[0], third) &&
                Object.ReferenceEquals(items[1], replacement),
                "Replace applies the requested reference sequence");
            changes.Clear();
            third.Text = "third-updated";
            AssertTrue(
                changes.Count == 1 &&
                changes[0].ListChangedType == ListChangedType.ItemChanged &&
                changes[0].NewIndex == 0,
                "Replace preserves notifications when moving an item");

            ItemsBinding<ReplaceAuditItem> duplicates =
                new ItemsBinding<ReplaceAuditItem>();
            duplicates.Add(first);
            duplicates.Add(second);
            duplicates.Add(first);
            changes.Clear();
            duplicates.ListChanged +=
                delegate(object sender, ListChangedEventArgs e)
                {
                    changes.Add(e);
                };
            duplicates.Replace(
                new ReplaceAuditItem[] { first, first, second });
            AssertTrue(
                changes.Count == 1 &&
                changes[0].ListChangedType == ListChangedType.ItemMoved,
                "Replace retains duplicate occurrences with one move");
            AssertTrue(
                Object.ReferenceEquals(duplicates[0], first) &&
                Object.ReferenceEquals(duplicates[1], first) &&
                Object.ReferenceEquals(duplicates[2], second),
                "Replace orders duplicate references deterministically");

            ItemsBinding<int> values = new ItemsBinding<int>();
            values.Add(10);
            values.Add(20);
            int valueChangeCount = 0;
            values.ListChanged +=
                delegate(object sender, ListChangedEventArgs e)
                {
                    valueChangeCount++;
                };
            values.Replace(new int[] { 10, 20 });
            AssertTrue(
                valueChangeCount == 0,
                "Replace compares value-type items by value");

            bool failed = false;

            try
            {
                values.Replace(new ThrowingAuditEnumerable());
            }
            catch (InvalidOperationException)
            {
                failed = true;
            }

            AssertTrue(
                failed,
                "Replace preserves a source enumeration failure");
            AssertTrue(
                values.Count == 2 && values[0] == 10 && values[1] == 20,
                "failed Replace enumeration leaves the list unchanged");
            AssertTrue(
                valueChangeCount == 0,
                "failed Replace enumeration publishes no notification");
        }

        private static void TestItemsBindingReplacePreservesItemNotifications()
        {
            ReplaceAuditItem item = new ReplaceAuditItem("before");
            ItemsBinding<ReplaceAuditItem> items =
                new ItemsBinding<ReplaceAuditItem>();
            items.Add(item);
            List<ListChangedEventArgs> changes =
                new List<ListChangedEventArgs>();
            items.ListChanged +=
                delegate(object sender, ListChangedEventArgs e)
                {
                    changes.Add(e);
                };

            items.Replace(new ReplaceAuditItem[] { item });
            AssertTrue(
                changes.Count == 0,
                "same-instance Replace keeps the item without list churn");

            item.Text = "after";
            AssertTrue(
                changes.Count == 1 &&
                changes[0].ListChangedType == ListChangedType.ItemChanged &&
                changes[0].NewIndex == 0,
                "same-instance notifying changes remain subscribed after Replace");

            changes.Clear();
            items.ResetItem(0);
            AssertTrue(
                changes.Count == 1 &&
                changes[0].ListChangedType == ListChangedType.ItemChanged &&
                changes[0].NewIndex == 0,
                "ResetItem remains available for non-notifying internal changes");
        }

        private static void TestItemsBindingReentrantReplaceKeepsNewestResult()
        {
            ReplaceAuditItem first = new ReplaceAuditItem("first");
            ReplaceAuditItem second = new ReplaceAuditItem("second");
            ReplaceAuditItem third = new ReplaceAuditItem("third");
            ItemsBinding<ReplaceAuditItem> items =
                new ItemsBinding<ReplaceAuditItem>();
            items.Add(first);
            items.Add(second);
            items.Add(third);

            ReplaceAuditItem newestFirst =
                new ReplaceAuditItem("newest-first");
            ReplaceAuditItem newestSecond =
                new ReplaceAuditItem("newest-second");
            ReplaceAuditItem newestThird =
                new ReplaceAuditItem("newest-third");
            bool replacedReentrantly = false;
            int resetCount = 0;

            items.ListChanged +=
                delegate(object sender, ListChangedEventArgs e)
                {
                    if (e.ListChangedType == ListChangedType.Reset)
                        resetCount++;

                    if (replacedReentrantly)
                        return;

                    replacedReentrantly = true;
                    items.Replace(
                        new ReplaceAuditItem[]
                        {
                            newestFirst,
                            newestSecond,
                            newestThird
                        });
                };

            items.Replace(
                new ReplaceAuditItem[]
                {
                    new ReplaceAuditItem("obsolete-first"),
                    new ReplaceAuditItem("obsolete-second"),
                    new ReplaceAuditItem("obsolete-third")
                });

            AssertTrue(
                replacedReentrantly,
                "Replace permits a synchronous newer replacement");
            AssertTrue(
                Object.ReferenceEquals(items[0], newestFirst) &&
                Object.ReferenceEquals(items[1], newestSecond) &&
                Object.ReferenceEquals(items[2], newestThird),
                "a stale Replace plan cannot overwrite a reentrant newer result");
            AssertTrue(
                resetCount == 0,
                "reentrant Replace keeps precise notifications without a reset");
        }

        private static void TestItemsBindingFailedReentrantReplaceKeepsOuterRequest()
        {
            ItemsBinding<int> items = new ItemsBinding<int>();
            items.Add(10);
            items.Add(20);
            items.Add(30);
            bool attemptedReentrantReplace = false;
            bool reentrantFailureObserved = false;

            items.ListChanged +=
                delegate(object sender, ListChangedEventArgs e)
                {
                    if (attemptedReentrantReplace)
                        return;

                    attemptedReentrantReplace = true;

                    try
                    {
                        items.Replace(new ThrowingAuditEnumerable());
                    }
                    catch (InvalidOperationException)
                    {
                        reentrantFailureObserved = true;
                    }
                };

            items.Replace(new int[] { 40, 50, 60 });

            AssertTrue(
                attemptedReentrantReplace && reentrantFailureObserved,
                "reentrant Replace surfaces its source-enumeration failure");
            AssertTrue(
                items.Count == 3 &&
                items[0] == 40 &&
                items[1] == 50 &&
                items[2] == 60,
                "a failed reentrant Replace does not cancel the valid outer request");
        }

        private static void TestItemsBindingReplaceMinimizesLargeRotations()
        {
            List<ReplaceAuditItem> original =
                new List<ReplaceAuditItem>();
            int i;

            for (i = 0; i < 130; i++)
            {
                original.Add(
                    new ReplaceAuditItem(
                        "rotation-" + i.ToString()));
            }

            ItemsBinding<ReplaceAuditItem> items =
                new ItemsBinding<ReplaceAuditItem>(original);
            List<ListChangedEventArgs> changes =
                new List<ListChangedEventArgs>();
            items.ListChanged +=
                delegate(object sender, ListChangedEventArgs e)
                {
                    changes.Add(e);
                };

            List<ReplaceAuditItem> left =
                new List<ReplaceAuditItem>(original.Count);

            for (i = 1; i < original.Count; i++)
                left.Add(original[i]);

            left.Add(original[0]);
            items.Replace(left);

            AssertTrue(
                changes.Count == 1 &&
                changes[0].ListChangedType ==
                    ListChangedType.ItemMoved &&
                changes[0].OldIndex == 0 &&
                changes[0].NewIndex == original.Count - 1,
                "a large left rotation publishes one move without reset");
            AssertTrue(
                Object.ReferenceEquals(
                    items[items.Count - 1],
                    original[0]),
                "the large left rotation preserves item identity");

            changes.Clear();
            items.Replace(original);

            AssertTrue(
                changes.Count == 1 &&
                changes[0].ListChangedType ==
                    ListChangedType.ItemMoved &&
                changes[0].OldIndex == original.Count - 1 &&
                changes[0].NewIndex == 0,
                "a large right rotation publishes one move without reset");

            for (i = 0; i < original.Count; i++)
            {
                AssertTrue(
                    Object.ReferenceEquals(items[i], original[i]),
                    "the right rotation restores every reference occurrence");
            }
        }

        private static void TestItemsBindingReplaceHandlesDuplicateNullOccurrences()
        {
            ReplaceAuditItem first =
                new ReplaceAuditItem("duplicate-first");
            ReplaceAuditItem second =
                new ReplaceAuditItem("duplicate-second");
            List<ReplaceAuditItem> original =
                new List<ReplaceAuditItem>();
            original.Add(null);
            original.Add(first);
            original.Add(second);
            original.Add(null);
            original.Add(first);

            ItemsBinding<ReplaceAuditItem> items =
                new ItemsBinding<ReplaceAuditItem>(original);
            List<ListChangedEventArgs> changes =
                new List<ListChangedEventArgs>();
            items.ListChanged +=
                delegate(object sender, ListChangedEventArgs e)
                {
                    changes.Add(e);
                };

            ReplaceAuditItem[] replacement =
                new ReplaceAuditItem[]
                {
                    first,
                    null,
                    first,
                    null,
                    second
                };
            items.Replace(replacement);

            AssertTrue(
                changes.Count == 3,
                "duplicate/null permutation uses three deterministic moves");

            int i;

            for (i = 0; i < changes.Count; i++)
            {
                AssertTrue(
                    changes[i].ListChangedType ==
                        ListChangedType.ItemMoved,
                    "duplicate/null permutation avoids replacement and reset");
            }

            for (i = 0; i < replacement.Length; i++)
            {
                AssertTrue(
                    Object.ReferenceEquals(items[i], replacement[i]),
                    "duplicate/null occurrences retain deterministic identity order");
            }
        }

        private static void TestValueParseCachesAreBounded()
        {
            Type runtimeType = typeof(XamlRuntime);
            BindingFlags staticPrivate =
                BindingFlags.Static | BindingFlags.NonPublic;
            FieldInfo colorField = runtimeType.GetField(
                "_colorParseCache",
                staticPrivate);
            FieldInfo thicknessField = runtimeType.GetField(
                "_thicknessParseCache",
                staticPrivate);
            FieldInfo syncField = runtimeType.GetField(
                "_valueParseCacheLock",
                staticPrivate);
            FieldInfo limitField = runtimeType.GetField(
                "ValueParseCacheLimit",
                staticPrivate);
            MethodInfo parseColor = runtimeType.GetMethod(
                "ParseColor",
                staticPrivate);
            MethodInfo parseThickness = runtimeType.GetMethod(
                "ParseThickness",
                staticPrivate);

            AssertTrue(
                colorField != null && thicknessField != null &&
                syncField != null && limitField != null &&
                parseColor != null && parseThickness != null,
                "bounded value parse cache internals are available");

            IDictionary colors = colorField.GetValue(null) as IDictionary;
            IDictionary thicknesses =
                thicknessField.GetValue(null) as IDictionary;
            object cacheSync = syncField.GetValue(null);
            int limit = (int)limitField.GetValue(null);
            Hashtable savedColors;
            Hashtable savedThicknesses;

            lock (cacheSync)
            {
                savedColors = new Hashtable(colors);
                savedThicknesses = new Hashtable(thicknesses);
                colors.Clear();
                thicknesses.Clear();
            }

            try
            {
                Color qualifiedSystemColor =
                    (Color)parseColor.Invoke(
                        null,
                        new object[] { "SystemColors.Control" });
                Color fullyQualifiedSystemColor =
                    (Color)parseColor.Invoke(
                        null,
                        new object[] {
                            "System.Drawing.SystemColors.ControlText"
                        });
                Color qualifiedNamedColor =
                    (Color)parseColor.Invoke(
                        null,
                        new object[] { "Color.Red" });
                Color fullyQualifiedNamedColor =
                    (Color)parseColor.Invoke(
                        null,
                        new object[] {
                            "System.Drawing.Color.Transparent"
                        });

                AssertTrue(
                    qualifiedSystemColor.ToArgb() ==
                        SystemColors.Control.ToArgb() &&
                    fullyQualifiedSystemColor.ToArgb() ==
                        SystemColors.ControlText.ToArgb(),
                    "qualified SystemColors values resolve through the framework palette");
                AssertTrue(
                    qualifiedNamedColor.ToArgb() == Color.Red.ToArgb() &&
                    fullyQualifiedNamedColor.ToArgb() ==
                        Color.Transparent.ToArgb(),
                    "qualified Color values resolve through the named palette");

                int i;

                for (i = 0; i < limit + 64; i++)
                {
                    parseColor.Invoke(
                        null,
                        new object[] { "#" + i.ToString("X6") });
                    parseThickness.Invoke(
                        null,
                        new object[] { i + " " + (i + 1) });
                }

                AssertTrue(
                    colors.Count == limit,
                    "dynamic color values cannot grow the static cache past its cap");
                AssertTrue(
                    thicknesses.Count == limit,
                    "dynamic thickness values cannot grow the static cache past its cap");

                int colorCount = colors.Count;
                int thicknessCount = thicknesses.Count;
                Color uncachedColor = (Color)parseColor.Invoke(
                    null,
                    new object[] { "#FFFFFF" });
                Padding uncachedThickness = (Padding)parseThickness.Invoke(
                    null,
                    new object[] { "999 1000" });

                AssertTrue(
                    uncachedColor.ToArgb() == Color.White.ToArgb(),
                    "colors still parse after cache admission stops");
                AssertTrue(
                    uncachedThickness.Left == 999 &&
                    uncachedThickness.Top == 1000 &&
                    uncachedThickness.Right == 999 &&
                    uncachedThickness.Bottom == 1000,
                    "thickness still parses after cache admission stops");
                AssertTrue(
                    colors.Count == colorCount &&
                    thicknesses.Count == thicknessCount,
                    "post-cap values are not retained");
            }
            finally
            {
                lock (cacheSync)
                {
                    colors.Clear();
                    thicknesses.Clear();

                    foreach (DictionaryEntry entry in savedColors)
                        colors.Add(entry.Key, entry.Value);

                    foreach (DictionaryEntry entry in savedThicknesses)
                        thicknesses.Add(entry.Key, entry.Value);
                }
            }
        }

        private static void TestReflectionCachesUseTypeBuckets()
        {
            Type runtimeType = typeof(XamlRuntime);
            BindingFlags staticPrivate =
                BindingFlags.Static | BindingFlags.NonPublic;
            MethodInfo findProperty = runtimeType.GetMethod(
                "FindProperty",
                staticPrivate);
            MethodInfo findEvent = runtimeType.GetMethod(
                "FindEvent",
                staticPrivate);
            FieldInfo propertyCacheField = runtimeType.GetField(
                "_propertyInfoCache",
                staticPrivate);
            FieldInfo eventCacheField = runtimeType.GetField(
                "_eventInfoCache",
                staticPrivate);
            FieldInfo syncField = runtimeType.GetField(
                "_reflectionInfoCacheLock",
                staticPrivate);

            AssertTrue(
                findProperty != null && findEvent != null &&
                propertyCacheField != null && eventCacheField != null &&
                syncField != null,
                "reflection lookup helpers and caches are available");

            IDictionary propertyCache =
                propertyCacheField.GetValue(null) as IDictionary;
            IDictionary eventCache =
                eventCacheField.GetValue(null) as IDictionary;
            object cacheSync = syncField.GetValue(null);
            Hashtable savedProperties;
            Hashtable savedEvents;

            lock (cacheSync)
            {
                savedProperties = new Hashtable(propertyCache);
                savedEvents = new Hashtable(eventCache);
                propertyCache.Clear();
                eventCache.Clear();
            }

            try
            {
                Type targetType =
                    typeof(CaseDistinctReflectionAuditTarget);
                PropertyInfo upperProperty = findProperty.Invoke(
                    null,
                    new object[] { targetType, "Value" }) as PropertyInfo;
                PropertyInfo lowerProperty = findProperty.Invoke(
                    null,
                    new object[] { targetType, "value" }) as PropertyInfo;
                EventInfo upperEvent = findEvent.Invoke(
                    null,
                    new object[] { targetType, "Changed" }) as EventInfo;
                EventInfo lowerEvent = findEvent.Invoke(
                    null,
                    new object[] { targetType, "changed" }) as EventInfo;

                AssertTrue(
                    upperProperty != null && upperProperty.Name == "Value" &&
                    lowerProperty != null && lowerProperty.Name == "value",
                    "property cache preserves exact-case lookup semantics");
                AssertTrue(
                    upperEvent != null && upperEvent.Name == "Changed" &&
                    lowerEvent != null && lowerEvent.Name == "changed",
                    "event cache preserves exact-case lookup semantics");

                object propertyBucket = propertyCache[targetType];
                object eventBucket = eventCache[targetType];
                FieldInfo propertyMembersField =
                    propertyBucket == null
                        ? null
                        : propertyBucket.GetType().GetField(
                            "Members",
                            BindingFlags.Instance |
                            BindingFlags.Public |
                            BindingFlags.NonPublic);
                FieldInfo eventMembersField =
                    eventBucket == null
                        ? null
                        : eventBucket.GetType().GetField(
                            "Members",
                            BindingFlags.Instance |
                            BindingFlags.Public |
                            BindingFlags.NonPublic);
                IDictionary properties =
                    propertyMembersField == null
                        ? null
                        : propertyMembersField.GetValue(propertyBucket)
                            as IDictionary;
                IDictionary events =
                    eventMembersField == null
                        ? null
                        : eventMembersField.GetValue(eventBucket)
                            as IDictionary;

                AssertTrue(
                    properties != null &&
                    properties.Contains("Value") &&
                    properties.Contains("value"),
                    "properties are bucketed by Type without a composite string key");
                AssertTrue(
                    events != null &&
                    events.Contains("Changed") &&
                    events.Contains("changed"),
                    "events are bucketed by Type without a composite string key");
            }
            finally
            {
                lock (cacheSync)
                {
                    propertyCache.Clear();
                    eventCache.Clear();

                    foreach (DictionaryEntry entry in savedProperties)
                        propertyCache.Add(entry.Key, entry.Value);

                    foreach (DictionaryEntry entry in savedEvents)
                        eventCache.Add(entry.Key, entry.Value);
                }
            }
        }

        private static void TestReflectionCachesAreBounded()
        {
            Type runtimeType = typeof(XamlRuntime);
            BindingFlags staticPrivate =
                BindingFlags.Static | BindingFlags.NonPublic;
            FieldInfo propertyCacheField = runtimeType.GetField(
                "_propertyInfoCache",
                staticPrivate);
            FieldInfo eventCacheField = runtimeType.GetField(
                "_eventInfoCache",
                staticPrivate);
            FieldInfo syncField = runtimeType.GetField(
                "_reflectionInfoCacheLock",
                staticPrivate);
            FieldInfo typeLimitField = runtimeType.GetField(
                "ReflectionTypeCacheLimit",
                staticPrivate);
            FieldInfo memberLimitField = runtimeType.GetField(
                "ReflectionMemberNameCacheLimit",
                staticPrivate);
            FieldInfo missingField = runtimeType.GetField(
                "_missingReflectionInfo",
                staticPrivate);
            MethodInfo findProperty = runtimeType.GetMethod(
                "FindProperty",
                staticPrivate);
            MethodInfo findEvent = runtimeType.GetMethod(
                "FindEvent",
                staticPrivate);

            AssertTrue(
                propertyCacheField != null && eventCacheField != null &&
                syncField != null && typeLimitField != null &&
                memberLimitField != null && missingField != null &&
                findProperty != null && findEvent != null,
                "bounded reflection cache internals are available");

            IDictionary propertyCache =
                propertyCacheField.GetValue(null) as IDictionary;
            IDictionary eventCache =
                eventCacheField.GetValue(null) as IDictionary;
            object cacheSync = syncField.GetValue(null);
            object missing = missingField.GetValue(null);
            int typeLimit = (int)typeLimitField.GetValue(null);
            int memberLimit = (int)memberLimitField.GetValue(null);
            Hashtable savedProperties;
            Hashtable savedEvents;

            lock (cacheSync)
            {
                savedProperties = new Hashtable(propertyCache);
                savedEvents = new Hashtable(eventCache);
                propertyCache.Clear();
                eventCache.Clear();
            }

            try
            {
                Type targetType =
                    typeof(CaseDistinctReflectionAuditTarget);
                findProperty.Invoke(
                    null,
                    new object[] { targetType, "Value" });
                findEvent.Invoke(
                    null,
                    new object[] { targetType, "Changed" });

                object propertyBucket = propertyCache[targetType];
                object eventBucket = eventCache[targetType];

                AssertTrue(
                    propertyBucket != null && eventBucket != null,
                    "reflection lookups create bounded type buckets");

                FieldInfo propertyMembersField =
                    propertyBucket.GetType().GetField(
                        "Members",
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic);
                FieldInfo eventMembersField =
                    eventBucket.GetType().GetField(
                        "Members",
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic);
                Hashtable properties =
                    propertyMembersField.GetValue(propertyBucket)
                        as Hashtable;
                Hashtable events =
                    eventMembersField.GetValue(eventBucket)
                        as Hashtable;

                AssertTrue(
                    properties != null && events != null,
                    "bounded reflection tests use production cache buckets");

                properties.Clear();
                events.Clear();

                FillAuditCache(
                    properties,
                    memberLimit - 1,
                    "PropertySlot",
                    missing);
                FillAuditCache(
                    events,
                    memberLimit - 1,
                    "EventSlot",
                    missing);

                PropertyInfo property = findProperty.Invoke(
                    null,
                    new object[] { targetType, "Value" }) as PropertyInfo;
                EventInfo eventInfo = findEvent.Invoke(
                    null,
                    new object[] { targetType, "Changed" }) as EventInfo;

                AssertTrue(
                    property != null && eventInfo != null &&
                    properties.Count == memberLimit &&
                    events.Count == memberLimit,
                    "reflection member buckets admit entries up to their cap");

                findProperty.Invoke(
                    null,
                    new object[] { targetType, "OverflowProperty" });
                findEvent.Invoke(
                    null,
                    new object[] { targetType, "OverflowEvent" });

                AssertTrue(
                    properties.Count == memberLimit &&
                    events.Count == memberLimit &&
                    !properties.Contains("OverflowProperty") &&
                    !events.Contains("OverflowEvent"),
                    "post-cap reflection member names are not retained");

                lock (cacheSync)
                {
                    propertyCache.Clear();
                    eventCache.Clear();
                    FillAuditCache(
                        propertyCache,
                        typeLimit,
                        "PropertyTypeSlot",
                        new Hashtable());
                    FillAuditCache(
                        eventCache,
                        typeLimit,
                        "EventTypeSlot",
                        new Hashtable());
                }

                property = findProperty.Invoke(
                    null,
                    new object[] { targetType, "Value" }) as PropertyInfo;
                eventInfo = findEvent.Invoke(
                    null,
                    new object[] { targetType, "Changed" }) as EventInfo;

                AssertTrue(
                    property != null && eventInfo != null,
                    "uncached reflection lookups still resolve after the type cap");
                AssertTrue(
                    propertyCache.Count == typeLimit &&
                    eventCache.Count == typeLimit &&
                    !propertyCache.Contains(targetType) &&
                    !eventCache.Contains(targetType),
                    "post-cap reflection types are not retained");
            }
            finally
            {
                lock (cacheSync)
                {
                    propertyCache.Clear();
                    eventCache.Clear();

                    foreach (DictionaryEntry entry in savedProperties)
                        propertyCache.Add(entry.Key, entry.Value);

                    foreach (DictionaryEntry entry in savedEvents)
                        eventCache.Add(entry.Key, entry.Value);
                }
            }
        }

        private static void TestBindingLookupCachesAreBounded()
        {
            Type runtimeType = typeof(XamlRuntime);
            BindingFlags staticPrivate =
                BindingFlags.Static | BindingFlags.NonPublic;
            FieldInfo memberCacheField = runtimeType.GetField(
                "_bindingMemberLookupCache",
                staticPrivate);
            FieldInfo memberSyncField = runtimeType.GetField(
                "_bindingMemberLookupCacheLock",
                staticPrivate);
            FieldInfo memberTypeLimitField = runtimeType.GetField(
                "BindingMemberTypeCacheLimit",
                staticPrivate);
            FieldInfo memberNameLimitField = runtimeType.GetField(
                "BindingMemberNameCacheLimit",
                staticPrivate);
            FieldInfo pathCacheField = runtimeType.GetField(
                "_bindingPathPartsCache",
                staticPrivate);
            FieldInfo pathSyncField = runtimeType.GetField(
                "_bindingPathPartsCacheLock",
                staticPrivate);
            FieldInfo pathLimitField = runtimeType.GetField(
                "BindingPathPartsCacheLimit",
                staticPrivate);
            MethodInfo getMember = runtimeType.GetMethod(
                "GetCachedBindingMember",
                staticPrivate);
            MethodInfo getPathParts = runtimeType.GetMethod(
                "GetCachedBindingPathParts",
                staticPrivate);

            AssertTrue(
                memberCacheField != null && memberSyncField != null &&
                memberTypeLimitField != null &&
                memberNameLimitField != null && pathCacheField != null &&
                pathSyncField != null && pathLimitField != null &&
                getMember != null && getPathParts != null,
                "bounded binding lookup cache internals are available");

            IDictionary memberCache =
                memberCacheField.GetValue(null) as IDictionary;
            IDictionary pathCache =
                pathCacheField.GetValue(null) as IDictionary;
            object memberSync = memberSyncField.GetValue(null);
            object pathSync = pathSyncField.GetValue(null);
            int memberTypeLimit =
                (int)memberTypeLimitField.GetValue(null);
            int memberNameLimit =
                (int)memberNameLimitField.GetValue(null);
            int pathLimit = (int)pathLimitField.GetValue(null);
            Hashtable savedMembers;
            Hashtable savedPaths;

            lock (memberSync)
            {
                savedMembers = new Hashtable(memberCache);
                memberCache.Clear();
            }

            lock (pathSync)
            {
                savedPaths = new Hashtable(pathCache);
                pathCache.Clear();
            }

            try
            {
                Type targetType = typeof(BindingLookupAuditValue);
                Hashtable members = new Hashtable(
                    StringComparer.OrdinalIgnoreCase);
                FillAuditCache(
                    members,
                    memberNameLimit - 1,
                    "MemberSlot",
                    new object());

                lock (memberSync)
                    memberCache.Add(targetType, members);

                object lookup = getMember.Invoke(
                    null,
                    new object[] { targetType, "Caption" });

                AssertTrue(
                    lookup != null && members.Count == memberNameLimit &&
                    members.Contains("Caption"),
                    "binding member buckets admit entries up to their cap");

                getMember.Invoke(
                    null,
                    new object[] { targetType, "OverflowMember" });

                AssertTrue(
                    members.Count == memberNameLimit &&
                    !members.Contains("OverflowMember"),
                    "post-cap binding member names are not retained");

                lock (memberSync)
                {
                    memberCache.Clear();
                    FillAuditCache(
                        memberCache,
                        memberTypeLimit,
                        "BindingTypeSlot",
                        new Hashtable());
                }

                lookup = getMember.Invoke(
                    null,
                    new object[] { targetType, "Caption" });

                AssertTrue(
                    lookup != null &&
                    memberCache.Count == memberTypeLimit &&
                    !memberCache.Contains(targetType),
                    "post-cap binding types resolve without being retained");

                string[] cachedParts = new string[] { "Retained" };

                lock (pathSync)
                {
                    FillAuditCache(
                        pathCache,
                        pathLimit - 1,
                        "PathSlot",
                        cachedParts);
                }

                string[] admitted = getPathParts.Invoke(
                    null,
                    new object[] { "State.Caption" }) as string[];
                string[] overflow = getPathParts.Invoke(
                    null,
                    new object[] { "State.Overflow.Caption" }) as string[];

                AssertTrue(
                    admitted != null && admitted.Length == 2 &&
                    overflow != null && overflow.Length == 3,
                    "binding paths still parse at and after the cache cap");
                AssertTrue(
                    pathCache.Count == pathLimit &&
                    pathCache.Contains("State.Caption") &&
                    !pathCache.Contains("State.Overflow.Caption") &&
                    Object.ReferenceEquals(
                        pathCache["PathSlot0"],
                        cachedParts),
                    "binding path admission stops without clearing the hot set");
            }
            finally
            {
                lock (memberSync)
                {
                    memberCache.Clear();

                    foreach (DictionaryEntry entry in savedMembers)
                        memberCache.Add(entry.Key, entry.Value);
                }

                lock (pathSync)
                {
                    pathCache.Clear();

                    foreach (DictionaryEntry entry in savedPaths)
                        pathCache.Add(entry.Key, entry.Value);
                }
            }
        }

        private static void TestObservableTargetPropertyCacheIsBounded()
        {
            Type runtimeType = typeof(XamlRuntime);
            BindingFlags staticPrivate =
                BindingFlags.Static | BindingFlags.NonPublic;
            FieldInfo cacheField = runtimeType.GetField(
                "_observableTargetPropertyCache",
                staticPrivate);
            FieldInfo syncField = runtimeType.GetField(
                "_observableTargetPropertyCacheSync",
                staticPrivate);
            FieldInfo limitField = runtimeType.GetField(
                "ObservableTargetPropertyCacheLimit",
                staticPrivate);
            FieldInfo missingField = runtimeType.GetField(
                "_missingObservableTargetProperty",
                staticPrivate);
            Type keyType = runtimeType.GetNestedType(
                "ObservableTargetPropertyCacheKey",
                BindingFlags.NonPublic);
            MethodInfo resolve = runtimeType.GetMethod(
                "ResolveObservableTargetProperty",
                staticPrivate);

            AssertTrue(
                cacheField != null && syncField != null &&
                limitField != null && missingField != null &&
                keyType != null && resolve != null,
                "bounded observable target-property cache internals are available");

            ConstructorInfo keyConstructor = keyType.GetConstructor(
                BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic,
                null,
                new Type[] { typeof(Type), typeof(string) },
                null);
            AssertTrue(
                keyConstructor != null,
                "observable target-property cache key can be inspected");

            IDictionary cache = cacheField.GetValue(null) as IDictionary;
            object cacheSync = syncField.GetValue(null);
            object missing = missingField.GetValue(null);
            int limit = (int)limitField.GetValue(null);
            Hashtable saved;

            lock (cacheSync)
            {
                saved = new Hashtable(cache);
                cache.Clear();
            }

            try
            {
                object retained = resolve.Invoke(
                    null,
                    new object[] { typeof(Label), "Text" });
                object retainedKey = keyConstructor.Invoke(
                    new object[] { typeof(Label), "Text" });
                int i;

                lock (cacheSync)
                {
                    for (i = 0; i < limit - 1; i++)
                    {
                        object key = keyConstructor.Invoke(
                            new object[]
                            {
                                typeof(Label),
                                "TargetPropertySlot" + i
                            });
                        cache.Add(key, missing);
                    }
                }

                object overflow = resolve.Invoke(
                    null,
                    new object[] { typeof(Label), "Content" });
                object overflowKey = keyConstructor.Invoke(
                    new object[] { typeof(Label), "Content" });
                object repeated = resolve.Invoke(
                    null,
                    new object[] { typeof(Label), "Text" });

                AssertTrue(
                    overflow != null &&
                    !Object.ReferenceEquals(overflow, missing),
                    "uncached target-property aliases still resolve after the cap");
                AssertTrue(
                    cache.Count == limit &&
                    cache.Contains(retainedKey) &&
                    !cache.Contains(overflowKey) &&
                    Object.ReferenceEquals(retained, repeated),
                    "target-property admission stops without evicting hot descriptors");
            }
            finally
            {
                lock (cacheSync)
                {
                    cache.Clear();

                    foreach (DictionaryEntry entry in saved)
                        cache.Add(entry.Key, entry.Value);
                }
            }
        }

        private static void FillAuditCache(
            IDictionary cache,
            int count,
            string keyPrefix,
            object value)
        {
            int i;

            for (i = 0; i < count; i++)
                cache.Add(keyPrefix + i, value);
        }

        private static void
            TestStaticTextIsNotADynamicExpression()
        {
            MethodInfo containsDynamic =
                typeof(XamlRuntime).GetMethod(
                    "ContainsDynamicExpression",
                    BindingFlags.Static | BindingFlags.NonPublic);

            AssertTrue(
                containsDynamic != null,
                "dynamic-expression classifier is available");
            AssertTrue(
                !(bool)containsDynamic.Invoke(
                    null,
                    new object[] { "ordinary static text" }),
                "ordinary static text is rejected before dynamic parsing");
            AssertTrue(
                !(bool)containsDynamic.Invoke(
                    null,
                    new object[] { "ordinary static text }" }),
                "a closing brace alone does not enter dynamic parsing");
            AssertTrue(
                (bool)containsDynamic.Invoke(
                    null,
                    new object[] { "{Binding Caption}" }) &&
                (bool)containsDynamic.Invoke(
                    null,
                    new object[] { "{Function Format()}" }) &&
                (bool)containsDynamic.Invoke(
                    null,
                    new object[] { "{Preset Theme.Surface}" }),
                "the brace fast path preserves every dynamic expression family");
        }

        private static void
            TestItemChangesUseCompiledSlotsWithoutVersionContract()
        {
            const string markup =
                "<ItemsControl Name='Rows' ItemKeyPath='Id' " +
                "    ReuseItems='true' ReevaluateFunctionsOnRefresh='false' " +
                "    Virtualizing='false' ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Panel>" +
                "      <Label Name='CaptionLabel' Text='{Binding Caption}' />" +
                "      <Label Name='CityLabel' " +
                "          Text='{Binding Customer.Address.City}' />" +
                "    </Panel>" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";
            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl rows =
                    runtime.GetItemsControl("Rows");
                ItemRefreshAuditRow row = new ItemRefreshAuditRow();
                row.Id = "normal-row";
                row.Caption = "Before caption";
                row.Customer.Address.City = "Before city";
                ArrayList items = new ArrayList();
                items.Add(row);
                rows.SetItems(items);

                Panel panel = rows.Controls[0] as Panel;
                Label caption = panel == null
                    ? null
                    : panel.Controls["CaptionLabel"] as Label;
                Label city = panel == null
                    ? null
                    : panel.Controls["CityLabel"] as Label;

                AssertTrue(
                    panel != null && caption != null && city != null,
                    "ordinary item bindings are realized");
                AssertTrue(
                    String.Equals(
                        city.Text,
                        "Before city",
                        StringComparison.Ordinal),
                    "nested item binding has its initial value");

                row.Customer.Address.City = "After city";
                ItemRefreshAuditRow.CustomerReadCount = 0;
                rows.ReloadItems();

                AssertTrue(
                    Object.ReferenceEquals(panel, rows.Controls[0]) &&
                    Object.ReferenceEquals(
                        city,
                        panel.Controls["CityLabel"]),
                    "nested item refresh reuses the compiled control tree");
                AssertTrue(
                    ItemRefreshAuditRow.CustomerReadCount > 0 &&
                    String.Equals(
                        city.Text,
                        "After city",
                        StringComparison.Ordinal),
                    "nested item changes reach the compiled-slot planner");

                row.Caption = "After caption";
                rows.ReloadItems();

                AssertTrue(
                    Object.ReferenceEquals(
                        caption,
                        panel.Controls["CaptionLabel"]) &&
                    String.Equals(
                        caption.Text,
                        "After caption",
                        StringComparison.Ordinal),
                    "ordinary member changes reach the compiled-slot planner");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void
            TestVirtualItemChangesUseCompiledSlotsWithoutVersionContract()
        {
            const string markup =
                "<ItemsControl Name='Rows' Width='180' Height='60' " +
                "    AutoScroll='true' ItemKeyPath='Id' ReuseItems='true' " +
                "    ReevaluateFunctionsOnRefresh='false' Virtualizing='true' " +
                "    VirtualizationThreshold='1' OverscanItems='0' " +
                "    FixedItemSize='20' ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label Text='{Binding Customer.Address.City}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";
            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl rows =
                    runtime.GetItemsControl("Rows");
                ItemRefreshAuditRow row = new ItemRefreshAuditRow();
                row.Id = "virtual-row";
                row.Customer.Address.City = "Before virtual city";
                ArrayList items = new ArrayList();
                items.Add(row);
                rows.CreateControl();
                rows.SetItems(items);

                Label label = null;
                int i;

                for (i = 0; i < rows.Controls.Count; i++)
                {
                    label = rows.Controls[i] as Label;

                    if (label != null)
                        break;
                }

                AssertTrue(
                    rows.IsVirtualizing &&
                    label != null &&
                    String.Equals(
                        label.Text,
                        "Before virtual city",
                        StringComparison.Ordinal),
                    "virtual item binding has its initial value");

                row.Customer.Address.City = "After virtual city";
                ItemRefreshAuditRow.CustomerReadCount = 0;
                rows.ReloadItems();

                AssertTrue(
                    Object.ReferenceEquals(label, rows.Controls[i]) &&
                    ItemRefreshAuditRow.CustomerReadCount > 0 &&
                    String.Equals(
                        label.Text,
                        "After virtual city",
                        StringComparison.Ordinal),
                    "virtual item changes reach the compiled-slot planner");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void
            TestObservedItemChangesUseCompiledSlotsWithoutVersionContract()
        {
            const string markup =
                "<ItemsControl Name='Rows' ItemKeyPath='Id' " +
                "    ReuseItems='true' ReevaluateFunctionsOnRefresh='false' " +
                "    Virtualizing='false' ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label Text='{Binding Customer.Address.City}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";
            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl rows =
                    runtime.GetItemsControl("Rows");
                ItemRefreshAuditRow row = new ItemRefreshAuditRow();
                row.Id = "observed-row";
                row.Customer.Address.City = "Before observed city";
                BindingList<ItemRefreshAuditRow> items =
                    new BindingList<ItemRefreshAuditRow>();
                items.Add(row);
                rows.SetItems(items);

                Label label = rows.Controls[0] as Label;
                AssertTrue(
                    label != null &&
                    String.Equals(
                        label.Text,
                        "Before observed city",
                        StringComparison.Ordinal),
                    "observed item binding has its initial value");

                row.Customer.Address.City = "After observed city";
                ItemRefreshAuditRow.CustomerReadCount = 0;
                MethodInfo applyObservedChanges =
                    typeof(XamlRuntime).GetMethod(
                        "TryApplyObservedItemChanges",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                ArrayList changedIndices = new ArrayList();
                changedIndices.Add(0);

                AssertTrue(
                    applyObservedChanges != null,
                    "observed-item compiled-slot planner is available");

                bool applied = (bool)applyObservedChanges.Invoke(
                    runtime,
                    new object[]
                    {
                        rows,
                        (IBindingList)items,
                        null,
                        changedIndices
                    });

                AssertTrue(
                    applied &&
                    Object.ReferenceEquals(label, rows.Controls[0]) &&
                    ItemRefreshAuditRow.CustomerReadCount > 0 &&
                    String.Equals(
                        label.Text,
                        "After observed city",
                        StringComparison.Ordinal),
                    "BindingList item changes reach the compiled-slot planner");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestEqualItemVersionRetainsFastContract()
        {
            const string markup =
                "<ItemsControl Name='Rows' ItemKeyPath='Id' " +
                "    ItemVersionPath='Version' ReuseItems='true' " +
                "    ReevaluateFunctionsOnRefresh='false' " +
                "    Virtualizing='false' ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Label Text='{Binding Customer.Address.City}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";
            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl rows =
                    runtime.GetItemsControl("Rows");
                ItemRefreshAuditRow row = new ItemRefreshAuditRow();
                row.Id = "versioned-row";
                row.Version = 7;
                row.Customer.Address.City = "Before versioned city";
                ArrayList items = new ArrayList();
                items.Add(row);
                rows.SetItems(items);

                Label label = rows.Controls[0] as Label;
                AssertTrue(
                    label != null &&
                    String.Equals(
                        label.Text,
                        "Before versioned city",
                        StringComparison.Ordinal),
                    "versioned item binding has its initial value");

                row.Customer.Address.City = "After versioned city";
                ItemRefreshAuditRow.CustomerReadCount = 0;
                rows.ReloadItems();

                AssertTrue(
                    Object.ReferenceEquals(label, rows.Controls[0]) &&
                    ItemRefreshAuditRow.CustomerReadCount == 0 &&
                    String.Equals(
                        label.Text,
                        "Before versioned city",
                        StringComparison.Ordinal),
                    "equal ItemVersionPath token retains the explicit fast contract");

                row.Version = 8;
                rows.ReloadItems();

                AssertTrue(
                    Object.ReferenceEquals(label, rows.Controls[0]) &&
                    ItemRefreshAuditRow.CustomerReadCount > 0 &&
                    String.Equals(
                        label.Text,
                        "After versioned city",
                        StringComparison.Ordinal),
                    "changed ItemVersionPath token resumes compiled-slot planning");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void
            TestDirectViewportIsSynchronousAndSchedulerFree()
        {
            const string markup =
                "<ItemsControl Name='Rows' Width='180' Height='100' " +
                " AutoScroll='true' ItemKeyPath='Id' " +
                " Virtualizing='true' VirtualizationThreshold='1' " +
                " OverscanItems='1' FixedItemSize='20' " +
                " ProgressiveRendering='true' ProgressiveBatchSize='1' " +
                " ProgressiveInterval='60000'>" +
                " <ItemsControl.ItemTemplate>" +
                "  <Label Text='{Binding Caption}' />" +
                " </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";
            XamlRuntime runtime = XamlRuntime.Load(markup);
            XamlRuntime.ItemsControl host =
                runtime.GetItemsControl("Rows");
            Type hostType = typeof(XamlRuntime.ItemsControl);
            BindingFlags instanceFields =
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic;
            MethodInfo directHook = hostType.GetMethod(
                "HandleDirectVirtualViewportChanged",
                instanceFields);
            MethodInfo directRefresh = typeof(XamlRuntime).GetMethod(
                "RefreshDirectVirtualViewportSynchronously",
                instanceFields);

            AssertTrue(
                directHook != null && directRefresh != null,
                "direct synchronous viewport hooks are available");

            FieldInfo[] fields = hostType.GetFields(instanceFields);
            int i;

            for (i = 0; i < fields.Length; i++)
            {
                if (typeof(System.Windows.Forms.Timer).IsAssignableFrom(
                    fields[i].FieldType))
                {
                    AssertTrue(
                        String.Equals(
                            fields[i].Name,
                            "_smoothScrollTimer",
                            StringComparison.Ordinal) ||
                        String.Equals(
                            fields[i].Name,
                            "_scrollBitmapImmediateCommitTimer",
                            StringComparison.Ordinal),
                        "item scrolling owns only its two lazy gesture timers");
                }
            }

            try
            {
                ArrayList rows = new ArrayList();

                for (i = 0; i < 100; i++)
                {
                    ItemRefreshAuditRow row =
                        new ItemRefreshAuditRow();

                    row.Id = "direct-" + i.ToString();
                    row.Caption = "Direct " + i.ToString();
                    rows.Add(row);
                }

                host.CreateControl();
                host.SetItems(rows);

                AssertTrue(
                    host.IsVirtualizing &&
                    host.DirectVirtualViewport != null &&
                    host.DirectVirtualViewport.Count == rows.Count,
                    "stable rows publish one direct logical viewport");
                AssertTrue(
                    !host.IsRefreshing &&
                    !host.DirectVirtualRefreshRunning,
                    "progressive settings do not defer direct realization");

                int generation = host.DirectVirtualGeneration;
                host.ScrollToIndex(70);

                AssertTrue(
                    host.DirectVirtualRealizedStart <= 70 &&
                    host.DirectVirtualRealizedEnd >= 70,
                    "ScrollToIndex publishes its target range before returning");
                AssertTrue(
                    !host.IsRefreshing &&
                    !host.DirectVirtualRefreshRunning,
                    "scroll realization leaves no pending scheduler state");
                AssertTrue(
                    host.DirectVirtualGeneration == generation,
                    "scroll realization stays in the committed generation");

                int previous = -1;

                for (i = 0; i < host.RenderedItems.Count; i++)
                {
                    object record = host.RenderedItems[i];
                    FieldInfo logicalIndex = record.GetType().GetField(
                        "LogicalIndex",
                        instanceFields);

                    AssertTrue(
                        logicalIndex != null,
                        "direct record exposes its logical index");
                    int current = (int)logicalIndex.GetValue(record);
                    AssertTrue(
                        current > previous,
                        "direct records publish in logical-index order");
                    previous = current;
                }

                ArrayList committedRecords = host.RenderedItems;
                directHook.Invoke(host, null);
                AssertTrue(
                    Object.ReferenceEquals(
                        committedRecords,
                        host.RenderedItems),
                    "an equivalent viewport request is an immediate no-op");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestMarkupAndPresetsRejectDtds()
        {
            const string markup =
                "<!DOCTYPE Form [<!ENTITY caption 'expanded'>]>" +
                "<Form Text='&caption;' />";
            bool markupRejected = false;

            try
            {
                XamlRuntime.Load(markup);
            }
            catch (WinFormsXamlLoadException)
            {
                markupRejected = true;
            }

            AssertTrue(
                markupRejected,
                "runtime markup rejects DTD declarations");

            PresetManager presets = new PresetManager();
            bool presetRejected = false;

            try
            {
                presets.LoadXml(
                    "<!DOCTYPE Presets [<!ENTITY color 'White'>]>" +
                    "<Presets Name='Theme' Selected='Light'>" +
                    "<Preset Name='Light'>" +
                    "<Set Key='Surface' Value='&color;' />" +
                    "</Preset></Presets>");
            }
            catch (System.Xml.XmlException)
            {
                presetRejected = true;
            }

            AssertTrue(
                presetRejected,
                "preset XML rejects DTD declarations");
            AssertTrue(
                !presets.Contains("Theme"),
                "rejected preset XML cannot mutate the manager");
        }

        private static void TestSchemaContracts()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            Stream schemaStream = assembly.GetManifestResourceStream(
                "WinFormsXaml.Tests.Schema.WinFormsXaml.xsd");

            AssertTrue(schemaStream != null, "schema fixture is embedded");

            XmlSchemaSet schemas = new XmlSchemaSet();

            using (schemaStream)
            using (XmlReader schemaReader = XmlReader.Create(schemaStream))
            {
                schemas.Add(null, schemaReader);
            }

            schemas.Compile();

            AssertSchemaBindableValueTypesAcceptExpressions(schemas);
            AssertCustomControlSchemaContracts(schemas);

            AssertTrue(
                IsSchemaValid(
                    schemas,
                    "<Form WindowState='{Binding WindowState}' " +
                    " FlowDirection='{Binding FlowDirection}' " +
                    " Width='{Binding FormWidth}'>" +
                    " <CheckBox Checked='{Binding Accepted, Mode=TwoWay}' " +
                    "  CheckState='{Binding AcceptedState, Mode=TwoWay}' />" +
                    " <ComboBox SelectedIndex='{Binding Selection, Mode=TwoWay}' " +
                    "  SelectedItem='{Binding SelectedItem, Mode=TwoWay}' " +
                    "  DropDownStyle='{Binding DropDownStyle}' />" +
                    " <TreeView SelectedNode='{Binding SelectedNode, Mode=TwoWay}' />" +
                    " <TabControl SelectedTab='{Binding SelectedTab, Mode=TwoWay}' />" +
                    " <RichTextBox SelectionStart=" +
                    "'{Binding SelectionStart, Mode=TwoWay}' " +
                    "  Lines='{Binding Lines, Mode=TwoWay}' " +
                    "  Rtf='{Binding Rtf, Mode=TwoWay}' />" +
                    " <HyperlinkLabel NavigateUri='{Binding HelpUri}' " +
                    "  LinkBehavior='{Binding LinkBehavior}' " +
                    "  LinkVisited='{Binding HelpVisited}' />" +
                    " <ProgressBar Style='{Binding ProgressStyle}' " +
                    "  PreferMarqueeFallback='{Binding PreferFallback}' " +
                    "  Value='{Binding ProgressValue}' />" +
                    " <TrackBar Value='{Function CurrentVolume}' />" +
                    " <NumericUpDown Value='{Preset Editor.Step}' />" +
                    " <TextBox TextAlign='{Binding TextAlignment}' " +
                    "  ScrollBars='{Preset Editor.ScrollBars}' />" +
                    " <RichTextBox ScrollBars='{Function RichScrollBars}' />" +
                    " <ListBox SelectionMode='{Binding ListSelection}' />" +
                    " <DataGridView SelectionMode=" +
                    "'{Function GridSelection}' " +
                    "  FirstDisplayedCell='{Binding FirstCell, Mode=TwoWay}' " +
                    "  FirstDisplayedScrollingColumnIndex=" +
                    "'{Binding FirstColumn, Mode=TwoWay}' " +
                    "  FirstDisplayedScrollingRowIndex=" +
                    "'{Binding FirstRow, Mode=TwoWay}' " +
                    "  HorizontalScrollingOffset=" +
                    "'{Binding HorizontalOffset, Mode=TwoWay}' />" +
                    " <WebBrowser Source=" +
                    "'{Binding BrowserUri, Mode=TwoWay}' />" +
                    "</Form>"),
                "schema accepts bindings for boolean, enum, numeric, and aliasable control values");

            AssertTrue(
                IsSchemaValid(
                    schemas,
                    "<Panel>" +
                    " <Label Condition=\"{Binding NumCount > 10}\" />" +
                    " <Label Condition=\"{Binding NumCount &lt;= 2}\" />" +
                    " <Label Condition=\"{Binding NumCount &lt; 2 " +
                    "&amp;&amp; NumCount > 0}\" />" +
                    " <Label Condition='{Binding TextContent === " +
                    "\"Text\" || TextContent == \"\"}' />" +
                    " <Label Condition=\"{Binding doubleNum == 2.6}\" />" +
                    " <Button Enabled=\"{Binding NumCount > 10}\" />" +
                    "</Panel>"),
                "schema accepts XML-safe computed Bindings for Condition and " +
                "general one-way targets");

            AssertTrue(
                IsSchemaValid(
                    schemas,
                    "<Panel>" +
                    " <Includes Source='SharedTheme' Condition='true' />" +
                    " <Includes Source='DarkTheme' " +
                    "  Condition='{Preset Theme == Dark}' />" +
                    " <Includes Source='OptionalCommands' " +
                    "  Condition='{Binding EnableCommands}' />" +
                    "</Panel>"),
                "schema accepts static and one-way conditional include directives");

            AssertTrue(
                IsSchemaValid(
                    schemas,
                    "<CheckBox Checked=' {Binding Accepted, Mode=TwoWay} ' />"),
                "schema accepts surrounding whitespace on a complete binding expression");
            AssertTrue(
                !IsSchemaValid(
                    schemas,
                    "<CheckBox Checked='{Bogus Accepted}' />") &&
                !IsSchemaValid(
                    schemas,
                    "<CheckBox Checked='{StaticResource Accepted}' />") &&
                !IsSchemaValid(
                    schemas,
                    "<CheckBox Checked='{Binding A}{Preset Theme.Flag}' />"),
                "schema rejects unknown, static-resource, and concatenated expressions for typed values");
            AssertTrue(
                !IsSchemaValid(
                    schemas,
                    "<TextBox Lines='First,Second' />") &&
                !IsSchemaValid(
                    schemas,
                    "<DataGridView FirstDisplayedCell='0,0' />"),
                "object and array-only targets require dynamic expressions rather than fake literals");
            AssertTrue(
                !IsSchemaValid(
                    schemas,
                    "<DateTimePicker Format='N2' />") &&
                !IsSchemaValid(
                    schemas,
                    "<Button AutoSizeMode='Fill' />") &&
                !IsSchemaValid(
                    schemas,
                    "<DataGridViewTextBoxColumn AutoSizeMode='GrowOnly' />") &&
                !IsSchemaValid(
                    schemas,
                    "<ProgressBar Minimum='0.5' />") &&
                !IsSchemaValid(
                    schemas,
                    "<TrackBar Maximum='10.5' />"),
                "element-specific built-ins reject literals for the wrong native property type");
            AssertTrue(
                !IsSchemaValid(
                    schemas,
                    "<ListBox SelectionMode='FullRowSelect' />") &&
                !IsSchemaValid(
                    schemas,
                    "<DataGridView SelectionMode='MultiExtended' />") &&
                !IsSchemaValid(
                    schemas,
                    "<TextBox ScrollBars='ForcedBoth' />") &&
                !IsSchemaValid(
                    schemas,
                    "<TextBox TextAlign='BottomRight' />") &&
                !IsSchemaValid(
                    schemas,
                    "<Label TextAlign='Left' />") &&
                !IsSchemaValid(
                    schemas,
                    "<Panel BorderStyle='SunkenOuter' />") &&
                !IsSchemaValid(
                    schemas,
                    "<ToolStripButton DisplayStyle='DropDownButton' />"),
                "same-named properties reject literals from unrelated native enum types");
            AssertTrue(
                !IsSchemaValid(
                    schemas,
                    "<ProgressBar Value='not-an-integer' />") &&
                !IsSchemaValid(
                    schemas,
                    "<TrackBar Value='0.5' />") &&
                IsSchemaValid(
                    schemas,
                    "<NumericUpDown Value='0.5' />"),
                "range-control Value literals retain their native numeric type");
            AssertTrue(
                IsSchemaValid(
                    schemas,
                    "<Form Opacity='1e0'>" +
                    " <DataGridView><DataGridView.Columns>" +
                    "  <DataGridViewTextBoxColumn FillWeight='1.25e2' />" +
                    " </DataGridView.Columns></DataGridView>" +
                    " <PrintPreviewControl Zoom='1e0' />" +
                    "</Form>"),
                "floating-point built-ins accept exponent notation used by CLR converters");

            AssertTrue(
                IsSchemaValid(
                    schemas,
                    "<Component>" +
                    "<Component.Properties>" +
                    "<Property Name='Caption' Required='false' />" +
                    "</Component.Properties>" +
                    "<Label />" +
                    "</Component>"),
                "component Required accepts a literal Boolean");

            AssertTrue(
                !IsSchemaValid(
                    schemas,
                    "<Component>" +
                    "<Component.Properties>" +
                    "<Property Name='Caption' " +
                    "Required='{Binding IsRequired}' />" +
                    "</Component.Properties>" +
                    "<Label />" +
                    "</Component>"),
                "component Required rejects dynamic expressions in the schema");

            AssertTrue(
                IsSchemaValid(
                    schemas,
                    "<Presets Name='Theme'>" +
                    "<Preset Name='Light'>" +
                    "<Set Key='Surface' Value='White' />" +
                    "</Preset></Presets>"),
                "preset Set accepts the required Value attribute");
            AssertTrue(
                !IsSchemaValid(
                    schemas,
                    "<Presets Name='Theme'>" +
                    "<Preset Name='Light'>" +
                    "<Set Key='Surface'>White</Set>" +
                    "</Preset></Presets>"),
                "preset Set rejects legacy inner-text values");
            AssertTrue(
                !IsSchemaValid(
                    schemas,
                    "<Preserts Name='Theme' />"),
                "schema rejects non-canonical preset container names");
            AssertTrue(
                IsSchemaValid(
                    schemas,
                    "<ItemsControl>" +
                    "<ItemsControl.ItemTemplate>" +
                    "<Label />" +
                    "</ItemsControl.ItemTemplate>" +
                    "</ItemsControl>"),
                "schema accepts the direct-root ItemsControl.ItemTemplate form");

            // XSD 1.0 cannot blacklist same-namespace names beneath the lax
            // wildcard that preserves arbitrary registered-control/property
            // extensibility. Runtime regressions are the rejection gate for
            // removed item-template aliases and nested wrappers.

            string[] collectionElements =
            {
                "BindingNavigator",
                "ToolStripContainer",
                "ToolStripPanel",
                "DataGrid",
                "DataGridTableStyle",
                "DataGridTextBoxColumn",
                "DataGridBoolColumn",
                "ToolBar",
                "ToolBarButton",
                "StatusBar",
                "StatusBarPanel",
                "ToolStripStatusLabel",
                "ToolStripProgressBar",
                "HyperlinkLabel",
                "Image",
                "PictureBox",
                "Link",
                "TreeNode",
                "ListViewItem",
                "ListViewGroup",
                "ColumnHeader",
                "DataGridViewTextBoxColumn",
                "DataGridViewCheckBoxColumn",
                "DataGridViewComboBoxColumn",
                "DataGridViewImageColumn",
                "DataGridViewButtonColumn",
                "DataGridViewLinkColumn",
                "DataGridViewRow",
                "DataGridViewTextBoxCell",
                "DataGridViewCellStyle",
                "RowStyle",
                "ColumnStyle",
                "MainMenu",
                "ContextMenu",
                "MenuItem",
                "Label.Text",
                "ListView.Columns",
                "LinkLabel.Links",
                "DataGridView.Columns",
                "TreeView.Nodes",
                "ToolStrip.Items",
                "TabControl.TabPages",
                "UserControl.Resources"
            };
            int i;

            for (i = 0; i < collectionElements.Length; i++)
            {
                AssertTrue(
                    schemas.GlobalElements.Contains(
                        new XmlQualifiedName(
                            collectionElements[i],
                            String.Empty)),
                    collectionElements[i] +
                    " is globally declared by the schema");
            }

            AssertSchemaElementDeclaresAttribute(
                schemas,
                "Image",
                "Source");
            AssertSchemaElementDeclaresAttribute(
                schemas,
                "Image",
                "Stretch");
            AssertSchemaElementDeclaresAttribute(
                schemas,
                "Image",
                "SourceChanged");
            AssertSchemaElementDeclaresAttribute(
                schemas,
                "Image",
                "StretchChanged");
            AssertSchemaElementDeclaresAttribute(
                schemas,
                "PictureBox",
                "ImageLocation");
            AssertSchemaElementDeclaresAttribute(
                schemas,
                "PictureBox",
                "InitialImage");
            AssertSchemaElementDeclaresAttribute(
                schemas,
                "PictureBox",
                "ErrorImage");
            AssertSchemaElementDeclaresAttribute(
                schemas,
                "PictureBox",
                "WaitOnLoad");
            AssertSchemaElementDeclaresAttribute(
                schemas,
                "PictureBox",
                "SizeModeChanged");
            AssertSchemaElementDeclaresAttribute(
                schemas,
                "PictureBox",
                "LoadCompleted");
            AssertSchemaElementDeclaresAttribute(
                schemas,
                "PictureBox",
                "LoadProgressChanged");
            AssertSchemaElementDeclaresAttribute(
                schemas,
                "DomainUpDown",
                "SelectedItemChanged");
            AssertSchemaElementDeclaresAttribute(
                schemas,
                "SplitContainer",
                "SplitterMoved");
            AssertSchemaElementDeclaresAttribute(
                schemas,
                "PropertyGrid",
                "SelectedObjectsChanged");
            AssertSchemaElementDeclaresAttribute(
                schemas,
                "TextBox",
                "Lines");
            AssertSchemaElementDeclaresAttribute(
                schemas,
                "MaskedTextBox",
                "Lines");
            AssertSchemaElementDeclaresAttribute(
                schemas,
                "ToolStripTextBox",
                "Lines");
            AssertSchemaElementDeclaresAttribute(
                schemas,
                "RichTextBox",
                "Lines");
            AssertSchemaElementDeclaresAttribute(
                schemas,
                "RichTextBox",
                "Rtf");
            AssertSchemaElementDeclaresAttribute(
                schemas,
                "DataGridView",
                "FirstDisplayedCell");
            AssertSchemaElementDeclaresAttribute(
                schemas,
                "DataGridView",
                "FirstDisplayedScrollingColumnIndex");
            AssertSchemaElementDeclaresAttribute(
                schemas,
                "DataGridView",
                "FirstDisplayedScrollingRowIndex");
            AssertSchemaElementDeclaresAttribute(
                schemas,
                "DataGridView",
                "HorizontalScrollingOffset");
            AssertSchemaElementDeclaresAttribute(
                schemas,
                "ItemsControl",
                "Virtualizing");
            AssertSchemaElementDeclaresAttribute(
                schemas,
                "ItemsControl",
                "VirtualizationMode");
            AssertSchemaElementDeclaresAttribute(
                schemas,
                "ItemsControl",
                "ItemRecycling");
            AssertSchemaElementDeclaresAttribute(
                schemas,
                "ItemsControl",
                "VirtualizationThreshold");
            AssertSchemaElementDeclaresAttribute(
                schemas,
                "ItemsControl",
                "OverscanItems");
            AssertSchemaElementDeclaresAttribute(
                schemas,
                "ItemsControl",
                "EstimatedItemSize");
            AssertSchemaElementDeclaresAttribute(
                schemas,
                "ItemsControl",
                "VirtualizationCacheItems");
            AssertSchemaElementDeclaresAttribute(
                schemas,
                "ItemsControl",
                "FixedItemSize");
            AssertSchemaElementDeclaresAttribute(
                schemas,
                "ItemsControl",
                "Wrap");
            AssertSchemaElementDeclaresAttribute(
                schemas,
                "ItemsControl",
                "JustifyContent");
            AssertSchemaElementDeclaresAttribute(
                schemas,
                "ItemsControl",
                "AlignItems");
            AssertSchemaElementDeclaresAttribute(
                schemas,
                "FlexPanel",
                "Wrap");
            AssertSchemaElementDeclaresAttribute(
                schemas,
                "FlexPanel",
                "JustifyContent");
            AssertSchemaElementDeclaresAttribute(
                schemas,
                "FlexPanel",
                "AlignItems");
            AssertSchemaElementDeclaresAttribute(
                schemas,
                "FlexPanel",
                "Direction");
            AssertSchemaElementDeclaresAttribute(
                schemas,
                "FlexPanel",
                "Gap");
            AssertSchemaElementDeclaresAttribute(
                schemas,
                "ItemsControl",
                "ScrollBarGap");
            AssertSchemaElementDoesNotDeclareAttribute(
                schemas,
                "ScrollBarStyle",
                "Gap");
            AssertTrue(
                IsSchemaValid(
                    schemas,
                    "<ItemsControl ScrollBarGap='8'>" +
                    " <ItemsControl.VerticalScrollStyle>" +
                    "  <ScrollBarStyle />" +
                    " </ItemsControl.VerticalScrollStyle>" +
                    "</ItemsControl>") &&
                IsSchemaValid(
                    schemas,
                    "<ItemsControl ScrollBarGap='{Binding ScrollGap}'>" +
                    " <ItemsControl.HorizontalScrollStyle>" +
                    "  <ScrollBarStyle />" +
                    " </ItemsControl.HorizontalScrollStyle>" +
                    "</ItemsControl>"),
                "ItemsControl ScrollBarGap accepts literal and dynamic values for either renderer");
            AssertSchemaElementDoesNotDeclareAttribute(
                schemas,
                "Panel",
                "Wrap");
            AssertSchemaElementDoesNotDeclareAttribute(
                schemas,
                "Panel",
                "JustifyContent");
            AssertSchemaElementDoesNotDeclareAttribute(
                schemas,
                "Button",
                "AlignItems");
            AssertSchemaElementDoesNotDeclareAttribute(
                schemas,
                "ItemsControl",
                "RecycleVirtualItems");
            AssertSchemaElementDoesNotDeclareAttribute(
                schemas,
                "ItemsControl",
                "DirectionalOverscan");
            AssertSchemaElementDoesNotDeclareAttribute(
                schemas,
                "ItemsControl",
                "CriticalViewportBufferItems");
            AssertSchemaElementDoesNotDeclareAttribute(
                schemas,
                "ItemsControl",
                "CriticalViewportMaxPasses");

            AssertTrue(
                !schemas.GlobalElements.Contains(
                    new XmlQualifiedName(
                        "WrapPanel",
                        String.Empty)),
                "WrapPanel is not globally declared by the schema");

            AssertTrue(
                IsSchemaValid(
                    schemas,
                    "<Form WindowState='Maximized' " +
                    " FormBorderStyle='FixedDialog' " +
                    " MaximizeBox='false' MinimizeBox='false'>" +
                    "  <Panel>" +
                    "    <Button ToolTip='Save the current record' " +
                    "      Panel.ZIndex='2' />" +
                    "  </Panel>" +
                    "  <ProgressBar Style='Marquee' " +
                    "    PreferMarqueeFallback='false' />" +
                    "  <ListView View='Details' />" +
                    "  <ListBox SelectionMode='One' />" +
                    "  <RichTextBox ScrollBars='ForcedBoth' />" +
                    "  <BindingNavigator>" +
                    "    <ToolStripStatusLabel DisplayStyle='Text' " +
                    "      BorderStyle='SunkenOuter' Spring='True' />" +
                    "    <ToolStripProgressBar Overflow='Never' />" +
                    "  </BindingNavigator>" +
                    "  <DataGridView SelectionMode='FullRowSelect'>" +
                    "    <DataGridViewTextBoxColumn HeaderText='Name' " +
                    "      DataPropertyName='Name' SortMode='Automatic' " +
                    "      Resizable='True' AutoSizeMode='Fill' />" +
                    "    <DataGridViewComboBoxColumn " +
                    "      DisplayStyle='DropDownButton' />" +
                    "    <DataGridViewImageColumn ImageLayout='Zoom' />" +
                    "  </DataGridView>" +
                    "</Form>"),
                "common runtime enum values validate in the schema");
            AssertTrue(
                IsSchemaValid(
                    schemas,
                    "<Form>" +
                    " <HyperlinkLabel Text='Documentation' " +
                    "  NavigateUri='https://example.invalid/docs' " +
                    "  LinkArea='0,13' LinkBehavior='HoverUnderline' " +
                    "  LinkColor='Blue' VisitedLinkColor='Purple' " +
                    "  NavigateUriChanged='DocumentationUri_Changed' " +
                    "  RequestNavigate='Documentation_RequestNavigate' />" +
                    " <Image Source='preview.png' Stretch='Uniform' " +
                    "  SourceChanged='Preview_SourceChanged' " +
                    "  StretchChanged='Preview_StretchChanged' />" +
                    " <DomainUpDown " +
                    "  SelectedItemChanged='Domain_SelectedItemChanged' />" +
                    " <SplitContainer SplitterMoved='Layout_SplitterMoved' " +
                    "  SplitterMoving='Layout_SplitterMoving' />" +
                    " <PropertyGrid " +
                    "  SelectedObjectsChanged='Inspector_SelectionChanged' />" +
                    " <MaskedTextBox Mask='000-000' PromptChar='_' />" +
                    " <FlowLayoutPanel WrapContents='{Binding ShouldWrap}' />" +
                    " <ListBox MultiColumn='true' ScrollAlwaysVisible='false' />" +
                    " <CheckedListBox CheckOnClick='true' />" +
                    " <TrackBar TickStyle='{Binding TickStyle}' />" +
                    " <NumericUpDown Minimum='0.5' Maximum='99.5' />" +
                    " <Button AutoSizeMode='GrowAndShrink' />" +
                    " <DataGridView><DataGridView.DefaultCellStyle>" +
                    "  <DataGridViewCellStyle Format='N2' />" +
                    " </DataGridView.DefaultCellStyle></DataGridView>" +
                    "</Form>"),
                "schema covers useful LinkLabel, input, list, flow, track, and decimal numeric properties");
            AssertTrue(
                !IsSchemaValid(
                    schemas,
                    "<HyperlinkLabel LinkBehavior='UnderlineSometimes' />"),
                "invalid LinkBehavior is rejected by the schema");
            AssertTrue(
                IsSchemaValid(
                    schemas,
                    "<Form>" +
                    " <Image Source='{Binding Preview}' " +
                    "  Stretch='{Binding PreviewStretch}' " +
                    "  SourceChanged='Preview_SourceChanged' " +
                    "  StretchChanged='Preview_StretchChanged' />" +
                    " <PictureBox Image='{Binding NativePreview}' " +
                    "  ImageLocation='{Function PreviewPath}' " +
                    "  InitialImage='{Binding Placeholder}' " +
                    "  ErrorImage='{Preset Media.ErrorImage}' " +
                    "  WaitOnLoad='{Binding LoadSynchronously}' " +
                    "  SizeMode='{Preset Media.PictureMode}' " +
                    "  SizeModeChanged='Picture_SizeModeChanged' " +
                    "  LoadCompleted='Picture_LoadCompleted' " +
                    "  LoadProgressChanged='Picture_LoadProgressChanged' />" +
                    "</Form>") &&
                !IsSchemaValid(
                    schemas,
                    "<Image Stretch='Crop' />") &&
                !IsSchemaValid(
                    schemas,
                    "<PictureBox SizeMode='Uniform' />") &&
                !IsSchemaValid(
                    schemas,
                    "<PictureBox WaitOnLoad='sometimes' />"),
                "Image and native PictureBox expose distinct bindable image contracts");
            AssertTrue(
                !IsSchemaValid(
                    schemas,
                    "<Form WindowState='FullScreen' />"),
                "invalid Form window state is rejected by the schema");
            AssertTrue(
                !IsSchemaValid(
                    schemas,
                    "<Form FormBorderStyle='Resizable' />"),
                "invalid native Form border style is rejected by the schema");
            AssertTrue(
                !IsSchemaValid(
                    schemas,
                    "<Panel><Button Panel.ZIndex='front' /></Panel>"),
                "Panel.ZIndex requires an integer or dynamic expression");
            AssertTrue(
                !IsSchemaValid(
                    schemas,
                    "<DataGridView SelectionMode='Rows' />"),
                "invalid DataGridView selection mode is rejected by the schema");
            AssertTrue(
                IsSchemaValid(
                    schemas,
                    "<Panel>" +
                    "<FlexPanel Direction='{Binding PanelDirection}' " +
                    " JustifyContent='SpaceAround' AlignItems='Stretch' " +
                    " Wrap='true' Gap='{Preset Layout.PanelGap}'>" +
                    " <TextBox FlexGrow='1' />" +
                    "</FlexPanel>" +
                    "<ItemsControl ItemsSource='{Binding Items}' " +
                    " Orientation='Vertical' Spacing='4' ItemKeyPath='Id' " +
                    " ItemVersionPath='Version' ReuseItems='true' " +
                    " ReevaluateFunctionsOnRefresh='true' " +
                    " ProgressiveRendering='true' ProgressiveBatchSize='8' " +
                    " ProgressiveInterval='1' ProgressiveTimeBudgetMs='4' " +
                    " LiveScroll='true' KeepScrollBarOnRight='true' " +
                    " Virtualizing='true' VirtualizationMode='Controls' " +
                    " ItemRecycling='Explicit' " +
                    " VirtualizationThreshold='32' " +
                    " OverscanItems='3' EstimatedItemSize='24' " +
                    " VirtualizationCacheItems='16' " +
                    " FixedItemSize='24' " +
                    " RefreshCompleted='Items_RefreshCompleted' " +
                    " RefreshFailed='Items_RefreshFailed'>" +
                    " <ItemsControl.ItemTemplate><Label /></ItemsControl.ItemTemplate>" +
                    "</ItemsControl>" +
                    "<ItemsControl ItemsSource='{Binding Cards}' " +
                    " Orientation='{Binding CardOrientation}' " +
                    " Spacing='{Function ResolveItemSpacing}' " +
                    " Wrap='{Binding UseWrap}' " +
                    " JustifyContent='{Preset Layout.ItemJustify}' " +
                    " AlignItems='{Function ResolveItemAlignment}'>" +
                    " <ItemsControl.ItemTemplate><Panel /></ItemsControl.ItemTemplate>" +
                    "</ItemsControl>" +
                    "</Panel>"),
                "Flex and wrapped ItemsControl surfaces validate in the schema");
            AssertTrue(
                !IsSchemaValid(
                    schemas,
                    "<FlexPanel JustifyContent='Evenly' />") &&
                !IsSchemaValid(
                    schemas,
                    "<ItemsControl AlignItems='Baseline' />") &&
                !IsSchemaValid(
                    schemas,
                    "<ItemsControl Wrap='sometimes' />"),
                "flex-line attributes reject unknown literal values while " +
                "retaining dynamic expression support");
            AssertTrue(
                IsSchemaValid(
                    schemas,
                    "<ItemsControl Virtualizing='true' AutoScroll='true' " +
                    " VirtualizationMode='{Binding ListMode}' " +
                    " ItemRecycling='{Preset Lists.Recycling}' " +
                    " FixedItemSize='24'>" +
                    " <ItemsControl.ItemTemplate>" +
                    "  <Border><Label Text='{Binding Name}' /></Border>" +
                    " </ItemsControl.ItemTemplate>" +
                    "</ItemsControl>"),
                "virtualization mode and recycling accept dynamic expressions");
            AssertTrue(
                !IsSchemaValid(
                    schemas,
                    "<ItemsControl VirtualizationMode='Automatic' />"),
                "unknown virtualization modes are rejected by the schema");
            AssertTrue(
                !IsSchemaValid(
                    schemas,
                    "<ItemsControl ItemRecycling='Unsafe' />"),
                "unknown recycling modes are rejected by the schema");
            AssertTrue(
                IsSchemaValid(
                    schemas,
                    "<Form Class='Example.UI.MainForm' " +
                    " Width='Auto' Height='24.5px' FontSize='10pt' " +
                    " Condition='{Binding IsVisible}' " +
                    " FlowDirection='RightToLeft' RightToLeft='Inherit'>" +
                    " <Grid>" +
                    "  <Grid.RowDefinitions>" +
                    "   <RowDefinition Height='Auto' />" +
                    "   <RowDefinition Height='0.5*' />" +
                    "  </Grid.RowDefinitions>" +
                    "  <Grid.ColumnDefinitions>" +
                    "   <ColumnDefinition Width='12.5pt' />" +
                    "  </Grid.ColumnDefinitions>" +
                    "  <Canvas><Button Canvas.Left='-1.5pt' /></Canvas>" +
                    " </Grid>" +
                    " <ProgressBar Style='Marquee' " +
                    "  MarqueeAnimationSpeed='0' " +
                    "  PreferMarqueeFallback='true' />" +
                    " <Label><Label.Text>{Binding Status}</Label.Text></Label>" +
                    "</Form>"),
                "canonical dimensions, directions, grid lengths, progress, and leaf property elements validate");
            AssertMarkupRejected(
                "<ProgressBar Style='Marquee' LegacyMode='Enabled' />",
                "removed progress fallback syntax is rejected by the runtime");
            AssertMarkupRejected(
                "<ProgressBar PrefectMarqueeFallback='true' />",
                "misspelled progress fallback syntax is rejected by the runtime");
            AssertMarkupRejected(
                "<Button PreferMarqueeFallback='true' />",
                "progress fallback switch is scoped to ProgressBar at runtime");
            AssertTrue(
                IsSchemaValid(
                    schemas,
                    "<Form>" +
                    " <ListView><ListView.Columns>" +
                    "  <ColumnHeader Text='Name' />" +
                    " </ListView.Columns></ListView>" +
                    " <DataGridView><DataGridView.Columns>" +
                    "  <DataGridViewTextBoxColumn HeaderText='Name' />" +
                    " </DataGridView.Columns></DataGridView>" +
                    " <TableLayoutPanel><TableLayoutPanel.RowStyles>" +
                    "  <RowStyle SizeType='Percent' Height='100' />" +
                    " </TableLayoutPanel.RowStyles></TableLayoutPanel>" +
                    " <Form.Resources><Style Key='Base' TargetType='Button' />" +
                    "  <Style Key='Child' TargetType='{x:Type Button}' " +
                    "   BasedOn='{StaticResource Base}' />" +
                    " </Form.Resources>" +
                    "</Form>"),
                "curated collection property elements and canonical style inheritance validate");
            AssertTrue(
                !IsSchemaValid(
                    schemas,
                    "<FlexPanel Direction='Horizontal' />"),
                "invalid Flex direction is rejected by the schema");
            AssertTrue(
                !IsSchemaValid(
                    schemas,
                    "<Form Condition='sometimes' />"),
                "Condition requires a boolean literal or expression");
            AssertTrue(
                !IsSchemaValid(
                    schemas,
                    "<Form FlowDirection='Yes' />"),
                "FlowDirection rejects native RightToLeft values");
            AssertTrue(
                !IsSchemaValid(
                    schemas,
                    "<Form RightToLeft='LeftToRight' />"),
                "RightToLeft rejects FlowDirection values");
            AssertTrue(
                !IsSchemaValid(
                    schemas,
                    "<ProgressBar Style='Pulse' />"),
                "ProgressBar Style rejects values outside the native enum");
            AssertTrue(
                !IsSchemaValid(
                    schemas,
                    "<ProgressBar MarqueeAnimationSpeed='-1' />"),
                "progress marquee speed must be non-negative");
            AssertTrue(
                !IsSchemaValid(
                    schemas,
                    "<Grid><Grid.RowDefinitions>" +
                    "<ColumnDefinition Width='*' />" +
                    "</Grid.RowDefinitions></Grid>"),
                "row definitions reject column definition elements");
            AssertTrue(
                !IsSchemaValid(
                    schemas,
                    "<Grid><Grid.RowDefinitions>" +
                    "<RowDefinition Width='20' />" +
                    "</Grid.RowDefinitions></Grid>"),
                "RowDefinition exposes Height rather than Width");
            AssertTrue(
                !IsSchemaValid(
                    schemas,
                    "<Presets Name='Theme'><Preset Name='Light'>" +
                    "<Set Key='Surface' Value='White'>ignored</Set>" +
                    "</Preset></Presets>"),
                "preset Set rejects inner text even when Value is present");
            AssertTrue(
                !IsSchemaValid(
                    schemas,
                    "<Style Key='Child' TargetType='Button' BasedOn='Base' />"),
                "Style BasedOn requires the canonical StaticResource expression");
            AssertTrue(
                !IsSchemaValid(
                    schemas,
                    "<Item Content='value' Condition='{Binding Visible}' />"),
                "value-only Item Condition remains static");
            AssertTrue(
                !IsSchemaValid(
                    schemas,
                    "<RowStyle SizeType='Fill' />"),
                "table-layout styles reject invalid SizeType values");
        }

        private static bool IsSchemaValid(
            XmlSchemaSet schemas,
            string xml)
        {
            bool valid = true;
            XmlReaderSettings settings = new XmlReaderSettings();
            settings.ValidationType = ValidationType.Schema;
            settings.Schemas = schemas;
            settings.ValidationEventHandler +=
                delegate(object sender, ValidationEventArgs e)
                {
                    valid = false;
                };

            using (StringReader text = new StringReader(xml))
            using (XmlReader reader = XmlReader.Create(text, settings))
            {
                while (reader.Read())
                {
                }
            }

            return valid;
        }

        private static void AssertSchemaElementDeclaresAttribute(
            XmlSchemaSet schemas,
            string elementName,
            string attributeName)
        {
            XmlSchemaElement element = schemas.GlobalElements[
                new XmlQualifiedName(
                    elementName,
                    String.Empty)] as XmlSchemaElement;
            XmlSchemaComplexType complexType = element == null
                ? null
                : element.ElementSchemaType as XmlSchemaComplexType;

            AssertTrue(
                complexType != null &&
                complexType.AttributeUses.Contains(
                    new XmlQualifiedName(
                        attributeName,
                        String.Empty)),
                elementName + "." + attributeName +
                " is explicitly declared for IntelliSense");
        }

        private static void AssertSchemaElementDoesNotDeclareAttribute(
            XmlSchemaSet schemas,
            string elementName,
            string attributeName)
        {
            XmlSchemaElement element = schemas.GlobalElements[
                new XmlQualifiedName(
                    elementName,
                    String.Empty)] as XmlSchemaElement;
            XmlSchemaComplexType complexType = element == null
                ? null
                : element.ElementSchemaType as XmlSchemaComplexType;

            AssertTrue(
                complexType != null &&
                !complexType.AttributeUses.Contains(
                    new XmlQualifiedName(
                        attributeName,
                        String.Empty)),
                elementName + "." + attributeName +
                " is not declared for IntelliSense");
        }

        private static void AssertCustomControlSchemaContracts(
            XmlSchemaSet schemas)
        {
            string[] scrollBarElements =
            {
                "VerticalScrollBar",
                "HorizontalScrollBar"
            };
            string[] rangeAttributes =
            {
                "Minimum",
                "Maximum",
                "Value"
            };
            string[] tabViewAppearanceEvents =
            {
                "TabBackgroundChanged",
                "SelectedTabBackgroundChanged",
                "TabForegroundChanged",
                "SelectedTabForegroundChanged",
                "TabBorderBrushChanged",
                "TabBorderThicknessChanged",
                "TabPaddingChanged",
                "HeaderSpacingChanged",
                "ContentBackgroundChanged",
                "ContentBorderBrushChanged",
                "ContentBorderThicknessChanged",
                "ContentPaddingChanged"
            };
            int i;
            int n;

            for (i = 0; i < scrollBarElements.Length; i++)
            {
                for (n = 0; n < rangeAttributes.Length; n++)
                {
                    AssertSchemaElementDeclaresAttribute(
                        schemas,
                        scrollBarElements[i],
                        rangeAttributes[n]);
                }
            }

            for (i = 0; i < tabViewAppearanceEvents.Length; i++)
            {
                AssertSchemaElementDeclaresAttribute(
                    schemas,
                    "TabView",
                    tabViewAppearanceEvents[i]);
            }

            AssertTrue(
                IsSchemaValid(
                    schemas,
                    "<Panel>" +
                    " <VerticalScrollBar Minimum='{Binding Minimum}' " +
                    "  Maximum='1000' " +
                    "  Value='{Binding Offset, Mode=TwoWay}' />" +
                    " <HorizontalScrollBar Minimum='0' " +
                    "  Maximum='{Function MaximumOffset}' " +
                    "  Value='{Preset Timeline.Offset}' />" +
                    "</Panel>"),
                "framework scrollbar ranges accept integer literals and " +
                "dynamic expressions");
            AssertTrue(
                !IsSchemaValid(
                    schemas,
                    "<VerticalScrollBar Value='0.5' />") &&
                !IsSchemaValid(
                    schemas,
                    "<HorizontalScrollBar Maximum='100.25' />"),
                "framework scrollbar ranges reject fractional literals");
            AssertTrue(
                IsSchemaValid(
                    schemas,
                    "<TabView " +
                    " TabBackgroundChanged='Tabs_TabBackgroundChanged' " +
                    " SelectedTabBackgroundChanged=" +
                    "'Tabs_SelectedTabBackgroundChanged' " +
                    " TabForegroundChanged='Tabs_TabForegroundChanged' " +
                    " SelectedTabForegroundChanged=" +
                    "'Tabs_SelectedTabForegroundChanged' " +
                    " TabBorderBrushChanged='Tabs_TabBorderBrushChanged' " +
                    " TabBorderThicknessChanged=" +
                    "'Tabs_TabBorderThicknessChanged' " +
                    " TabPaddingChanged='Tabs_TabPaddingChanged' " +
                    " HeaderSpacingChanged='Tabs_HeaderSpacingChanged' " +
                    " ContentBackgroundChanged=" +
                    "'Tabs_ContentBackgroundChanged' " +
                    " ContentBorderBrushChanged=" +
                    "'Tabs_ContentBorderBrushChanged' " +
                    " ContentBorderThicknessChanged=" +
                    "'Tabs_ContentBorderThicknessChanged' " +
                    " ContentPaddingChanged='Tabs_ContentPaddingChanged'>" +
                    " <TabViewItem Header='General'><Panel /></TabViewItem>" +
                    "</TabView>"),
                "TabView appearance change events validate as markup events");
        }

        private static void AssertSchemaBindableValueTypesAcceptExpressions(
            XmlSchemaSet schemas)
        {
            int bindableValueTypeCount = 0;

            foreach (DictionaryEntry entry in schemas.GlobalTypes)
            {
                XmlQualifiedName qualifiedName =
                    entry.Key as XmlQualifiedName;

                if (qualifiedName == null ||
                    qualifiedName.Namespace.Length != 0 ||
                    !qualifiedName.Name.EndsWith(
                        "Value",
                        StringComparison.Ordinal) ||
                    String.Equals(
                        qualifiedName.Name,
                        "GridLengthValue",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                XmlSchemaSimpleType simpleType =
                    entry.Value as XmlSchemaSimpleType;
                XmlSchemaSimpleTypeUnion union =
                    simpleType == null
                        ? null
                        : simpleType.Content as XmlSchemaSimpleTypeUnion;
                bool acceptsExpression = false;
                int i;

                if (union != null)
                {
                    for (i = 0; i < union.MemberTypes.Length; i++)
                    {
                        XmlQualifiedName member = union.MemberTypes[i];

                        if ((member.Namespace.Length == 0 &&
                             String.Equals(
                                 member.Name,
                                 "DynamicExpression",
                                 StringComparison.Ordinal)) ||
                            (String.Equals(
                                 member.Namespace,
                                 XmlSchema.Namespace,
                                 StringComparison.Ordinal) &&
                             String.Equals(
                                 member.Name,
                                 "string",
                                 StringComparison.Ordinal)))
                        {
                            acceptsExpression = true;
                            break;
                        }
                    }
                }

                AssertTrue(
                    acceptsExpression,
                    qualifiedName.Name +
                    " keeps literal IntelliSense while accepting binding strings");
                bindableValueTypeCount++;
            }

            AssertTrue(
                bindableValueTypeCount >= 40,
                "schema audits the complete family of bindable value types");
        }

        private static void AssertMarkupRejected(
            string xml,
            string message)
        {
            XamlRuntime unexpectedRuntime = null;
            Exception failure = null;

            try
            {
                unexpectedRuntime = XamlRuntime.Load(xml);
            }
            catch (Exception ex)
            {
                failure = ex;
            }
            finally
            {
                DisposeRuntime(unexpectedRuntime);
            }

            AssertTrue(failure != null, message);
        }

        private static void TestElementMetadataUsesReferenceIdentity()
        {
            XamlRuntime runtime = XamlRuntime.Load(
                "<FlowLayoutPanel>" +
                "  <EqualAuditControl Name='First' />" +
                "  <EqualAuditControl Name='Second' />" +
                "</FlowLayoutPanel>");

            try
            {
                EqualAuditControl first =
                    runtime.Get<EqualAuditControl>("First");
                EqualAuditControl second =
                    runtime.Get<EqualAuditControl>("Second");

                AssertTrue(
                    !Object.ReferenceEquals(first, second),
                    "equal custom controls retain independent metadata");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestRollbackPreservesConstructionFailure()
        {
            Exception failure = null;
            XamlRuntime unexpectedRuntime = null;

            try
            {
                unexpectedRuntime = XamlRuntime.Load(
                    "<ThrowingRollbackControl FailingProperty='value' />");
            }
            catch (Exception ex)
            {
                failure = ex;
            }
            finally
            {
                if (unexpectedRuntime != null)
                {
                    try
                    {
                        DisposeRuntime(unexpectedRuntime);
                    }
                    catch
                    {
                    }
                }
            }

            AssertTrue(failure != null, "the invalid element is rejected");
            AssertTrue(
                ExceptionContains(
                    failure,
                    "Primary construction failure"),
                "rollback preserves the construction failure");
            AssertTrue(
                !ExceptionContains(
                    failure,
                    "Secondary rollback cleanup failure"),
                "rollback cleanup does not replace the construction failure");
        }

        private static void TestAmbiguousSimpleTypeIsRejected()
        {
            Exception failure = null;
            XamlRuntime unexpectedRuntime = null;

            try
            {
                unexpectedRuntime =
                    XamlRuntime.Load("<AmbiguousAuditControl />");
            }
            catch (Exception ex)
            {
                failure = ex;
            }
            finally
            {
                DisposeRuntime(unexpectedRuntime);
            }

            AssertTrue(failure != null, "an ambiguous element is rejected");
            AssertTrue(
                ExceptionContains(failure, "ambiguous"),
                "the ambiguous type error explains how to disambiguate it");
        }

        private static void TestDisposedLayoutHostReleasesRuntime()
        {
            XamlRuntime runtime = XamlRuntime.Load("<Grid />");
            Control root = runtime.RootControl;

            try
            {
                FieldInfo runtimeField = root.GetType().GetField(
                    "Runtime",
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);

                AssertTrue(runtimeField != null, "layout runtime field is available");
                AssertTrue(
                    Object.ReferenceEquals(
                        runtime,
                        runtimeField.GetValue(root)),
                    "an active layout host retains its runtime");

                root.Dispose();

                AssertTrue(
                    runtimeField.GetValue(root) == null,
                    "a disposed layout host releases its runtime");
            }
            finally
            {
                DisposeRuntime(runtime);
            }
        }

        private static void TestCollectionAddFailureIsNotRetried()
        {
            ThrowingAuditCollection.AddCount = 0;
            TrackedAuditCollectionValue.DisposeCount = 0;
            Exception failure = null;

            try
            {
                XamlRuntime.Load(
                    "<ThrowingAuditCollectionHost>" +
                    "  <TrackedAuditCollectionValue />" +
                    "</ThrowingAuditCollectionHost>");
            }
            catch (Exception ex)
            {
                failure = ex;
            }

            AssertTrue(failure != null, "a failed collection add is reported");
            AssertTrue(
                ExceptionContains(failure, "Primary collection add failure"),
                "the collection's failure remains observable");
            AssertTrue(
                ThrowingAuditCollection.AddCount == 1,
                "a matching Add method is invoked once");
            AssertTrue(
                TrackedAuditCollectionValue.DisposeCount == 1,
                "the rejected child is released once");
        }

        private static void TestPropertyElementGetterFailureIsPreserved()
        {
            Exception failure = null;
            XamlRuntime unexpectedRuntime = null;

            try
            {
                unexpectedRuntime =
                    XamlRuntime.Load(
                        "<ThrowingAuditCollectionGetterHost>" +
                        "  <ThrowingAuditCollectionGetterHost.Items>" +
                        "    <TrackedAuditCollectionValue />" +
                        "  </ThrowingAuditCollectionGetterHost.Items>" +
                        "</ThrowingAuditCollectionGetterHost>");
            }
            catch (Exception ex)
            {
                failure = ex;
            }
            finally
            {
                DisposeRuntime(unexpectedRuntime);
            }

            AssertTrue(
                failure != null,
                "a failed explicit collection getter is reported");
            AssertTrue(
                ExceptionContains(
                    failure,
                    "Primary collection getter failure"),
                "the explicit collection getter failure is preserved");
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

        private static void DisposeRuntime(XamlRuntime runtime)
        {
            if (runtime == null)
                return;

            Control root = runtime.RootControl;

            if (root != null && !root.IsDisposed)
                root.Dispose();

            runtime.Dispose();
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
