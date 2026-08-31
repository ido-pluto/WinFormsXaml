using System;
using System.Collections;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using System.Xml;
using WinFormsXaml;

namespace WinFormsXaml.Tests
{
    internal sealed class Program
    {
        private delegate void TestMethod();

        private sealed class TestCase
        {
            private readonly string _name;
            private readonly TestMethod _method;

            public TestCase(string name, TestMethod method)
            {
                _name = name;
                _method = method;
            }

            public string Name
            {
                get { return _name; }
            }

            public TestMethod Method
            {
                get { return _method; }
            }
        }

        private sealed class BindingState
        {
            public string Text;
            public byte[] ImageBytes;
            public bool IsReady;
            public int Row;
        }

        private sealed class NativeBindingState
        {
            public Size Size;
            public Padding Margin;
            public Font Font;
            public string FontText;
            public RightToLeft Direction;
        }

        private sealed class EventState
        {
            public string CurrentStyle;
            public string HandlerName;
            public int ClickCount;
            public int AlternateClickCount;
            public int SecondaryClickCount;
            public int SecondaryAlternateClickCount;

            public void Action_Click(object sender, EventArgs e)
            {
                ClickCount++;
            }

            public void Alternate_Click(object sender, EventArgs e)
            {
                AlternateClickCount++;
            }

            public void Secondary_Click(object sender, EventArgs e)
            {
                SecondaryClickCount++;
            }

            public void SecondaryAlternate_Click(object sender, EventArgs e)
            {
                SecondaryAlternateClickCount++;
            }
        }

        private sealed class StyleState
        {
            public string CurrentStyle;
            public string SecondaryStyle;
            public string StyleBackground;
            public string LocalBackground;

            public string StackStyle;
            public string DockStyle;
            public string BorderStyle;
        }

        private sealed class ShadowStyleState
        {
            public string CurrentStyle;
            public object NullableObject;
            public Padding PaddingValue;
            public Uri NullableUri;
        }

        private sealed class ContentItemState
        {
            public string Id;
            public int Version;
            public string Text;
            public object NullableObject;
            public Padding PaddingValue;
            public Uri NullableUri;
        }

        public sealed class ReactiveChildState
        {
            public readonly PropertyBinding<string> Text;

            public ReactiveChildState(string text)
            {
                Text = new PropertyBinding<string>(text);
            }
        }

        private sealed class ReactiveBindingState
        {
            public readonly PropertyBinding<string> Text;
            public readonly PropertyBinding<string> SecondaryText;
            public readonly PropertyBinding<string> ContentText;
            public readonly PropertyBinding<string> HeaderText;
            public readonly PropertyBinding<string> TitleText;
            public readonly PropertyBinding<bool> Checked;
            public readonly PropertyBinding<bool> AliasChecked;
            public readonly PropertyBinding<bool> Enabled;
            public readonly PropertyBinding<bool> TabStop;
            public readonly PropertyBinding<bool> ReadOnly;
            public readonly PropertyBinding<Color> Foreground;
            public readonly PropertyBinding<Color> Background;
            public readonly PropertyBinding<decimal> Number;
            public readonly PropertyBinding<int> Row;
            public readonly PropertyBinding<bool> Condition;
            public readonly PropertyBinding<ReactiveChildState> Nested;
            public PropertyBinding<string> Replaceable;
            public string PlainText;

            public ReactiveBindingState()
            {
                Text = new PropertyBinding<string>("Initial");
                SecondaryText = new PropertyBinding<string>("Secondary");
                ContentText = new PropertyBinding<string>("Content initial");
                HeaderText = new PropertyBinding<string>("Header initial");
                TitleText = new PropertyBinding<string>("Title initial");
                Checked = new PropertyBinding<bool>(false);
                AliasChecked = new PropertyBinding<bool>(false);
                Enabled = new PropertyBinding<bool>(true);
                TabStop = new PropertyBinding<bool>(false);
                ReadOnly = new PropertyBinding<bool>(false);
                Foreground = new PropertyBinding<Color>(Color.Black);
                Background = new PropertyBinding<Color>(Color.White);
                Number = new PropertyBinding<decimal>(1m);
                Row = new PropertyBinding<int>(0);
                Condition = new PropertyBinding<bool>(true);
                Nested =
                    new PropertyBinding<ReactiveChildState>(
                        new ReactiveChildState("Nested initial"));
                Replaceable =
                    new PropertyBinding<string>("Replaceable initial");
                PlainText = "Plain";
            }
        }

        private sealed class AliasCollisionBindingState
        {
            public readonly PropertyBinding<bool> Checked;
            public readonly PropertyBinding<Color> Background;
            public readonly PropertyBinding<object> Content;
            public readonly PropertyBinding<string> ReadOnlyContent;

            public AliasCollisionBindingState()
            {
                Checked = new PropertyBinding<bool>(true);
                Background = new PropertyBinding<Color>(Color.Blue);
                Content = new PropertyBinding<object>("Custom content");
                ReadOnlyContent =
                    new PropertyBinding<string>("Native text content");
            }
        }

        private sealed class AlternateTargetEventBindingState
        {
            public readonly PropertyBinding<object> SelectedItem;
            public readonly PropertyBinding<int> DomainSelectedIndex;
            public readonly PropertyBinding<DateTime> CalendarStart;
            public readonly PropertyBinding<TreeNode> SelectedNode;
            public readonly PropertyBinding<TabPage> SelectedTab;
            public readonly PropertyBinding<int> RichSelectionStart;

            public AlternateTargetEventBindingState()
            {
                SelectedItem = new PropertyBinding<object>(null);
                DomainSelectedIndex = new PropertyBinding<int>(-1);
                CalendarStart =
                    new PropertyBinding<DateTime>(
                        DateTime.Today);
                SelectedNode = new PropertyBinding<TreeNode>(null);
                SelectedTab = new PropertyBinding<TabPage>(null);
                RichSelectionStart = new PropertyBinding<int>(0);
            }
        }

        private sealed class MutableTargetEventBindingState
        {
            public readonly PropertyBinding<int> Width;
            public readonly PropertyBinding<int> Height;
            public readonly PropertyBinding<int> Left;
            public readonly PropertyBinding<int> Top;
            public readonly PropertyBinding<string[]> Lines;
            public readonly PropertyBinding<string> Rtf;
            public readonly PropertyBinding<int> HorizontalOffset;
            public readonly PropertyBinding<bool> RichReadOnly;

            public MutableTargetEventBindingState()
            {
                Width = new PropertyBinding<int>(120);
                Height = new PropertyBinding<int>(40);
                Left = new PropertyBinding<int>(5);
                Top = new PropertyBinding<int>(7);
                Lines = new PropertyBinding<string[]>(
                    new string[] { "First", "Second" });
                Rtf = new PropertyBinding<string>(
                    "{\\rtf1\\ansi Initial}");
                HorizontalOffset = new PropertyBinding<int>(0);
                RichReadOnly = new PropertyBinding<bool>(false);
            }
        }

        private sealed class ComponentConditionState
        {
            public readonly PropertyBinding<bool> TemplateCondition;
            public readonly PropertyBinding<bool> InvocationCondition;

            public ComponentConditionState(
                bool templateCondition,
                bool invocationCondition)
            {
                TemplateCondition =
                    new PropertyBinding<bool>(templateCondition);
                InvocationCondition =
                    new PropertyBinding<bool>(invocationCondition);
            }
        }

        private sealed class ReactivePresetState
        {
            public readonly PropertyBinding<Color> Surface;
            public readonly PropertyBinding<string> Caption;
            public readonly PropertyBinding<string> AlternateCaption;

            public ReactivePresetState(
                Color surface,
                string caption)
            {
                Surface = new PropertyBinding<Color>(surface);
                Caption = new PropertyBinding<string>(caption);
                AlternateCaption =
                    new PropertyBinding<string>(caption + " alternate");
            }

            private string Decorate(string value)
            {
                return "[" + value + "]";
            }
        }

        private sealed class ReactiveIconState
        {
            public readonly PropertyBinding<Icon> ApplicationIcon;
            public readonly PropertyBinding<bool> UseApplicationIcon;

            public ReactiveIconState(Icon icon)
            {
                ApplicationIcon = new PropertyBinding<Icon>(icon);
                UseApplicationIcon = new PropertyBinding<bool>(true);
            }
        }

        private sealed class ReentrantEndpointState
        {
            public ReactiveChildState Endpoint;

            public ReentrantEndpointState(
                ReactiveChildState endpoint)
            {
                Endpoint = endpoint;
            }
        }

        [STAThread]
        private static int Main()
        {
            // Deliberately do not call Application.EnableVisualStyles here. This
            // general runner covers fallback selection in an isolated process;
            // WinFormsXaml.NativeMarqueeValidation owns the enabled native path.
            TestCase[] tests = new TestCase[]
            {
                new TestCase("default preset fallback", TestDefaultFallback),
                new TestCase(
                    "preset Value attribute is required",
                    TestPresetValueAttributeIsRequired),
                new TestCase(
                    "reactive typed preset values",
                    TestReactiveTypedPresetValues),
                new TestCase(
                    "nested preset dependencies refresh",
                    TestNestedPresetDependenciesRefresh),
                new TestCase(
                    "reactive preset replacement detaches old source",
                    TestReactivePresetReplacementDetachesOldSource),
                new TestCase(
                    "shared reactive presets remain runtime-local",
                    TestSharedReactivePresetRuntimeIsolation),
                new TestCase(
                    "preset value cycles are rejected",
                    TestReactivePresetCycleValidation),
                new TestCase("public mutation API", TestMutationApi),
                new TestCase("duplicate XML key rejection", TestDuplicateXmlKey),
                new TestCase("failed import is transactional", TestTransactionalImport),
                new TestCase("preserve-existing import", TestPreserveExistingImport),
                new TestCase("deferred notifications", TestDeferredNotifications),
                new TestCase("ordinary property reload", TestOrdinaryPropertyReload),
                new TestCase("explicit negative boolean binding", TestNegativeBooleanBinding),
                new TestCase("PropertyBinding value semantics", TestPropertyBindingValueSemantics),
                new TestCase("reactive one-way direct interpolated nested", TestReactiveOneWayBindings),
                new TestCase("reactive two-way native properties and aliases", TestReactiveTwoWayPropertiesAndAliases),
                new TestCase(
                    "reactive two-way update source triggers",
                    TestReactiveTwoWayUpdateSourceTriggers),
                new TestCase(
                    "reactive two-way target is current inside Click",
                    TestReactiveTwoWayTargetIsCurrentInsideClick),
                new TestCase(
                    "reactive two-way mapped aliases prefer native targets",
                    TestReactiveTwoWayMappedAliasPrecedence),
                new TestCase(
                    "reactive two-way alternate target events",
                    TestReactiveTwoWayAlternateTargetEvents),
                new TestCase(
                    "reactive two-way mutable target event coverage",
                    TestReactiveTwoWayMutableTargetEventCoverage),
                new TestCase(
                    "reactive two-way endpoint replacement preserves target edit",
                    TestReactiveTwoWayEndpointReplacementPreservesTargetEdit),
                new TestCase("reactive binding shared targets", TestReactiveSharedTargets),
                new TestCase(
                    "reactive shared newest equal edit wins",
                    TestReactiveSharedNewestEqualEditWins),
                new TestCase("reactive binding feedback keeps latest value", TestReactiveFeedbackKeepsLatestValue),
                new TestCase(
                    "reactive source target rewrite returns to source",
                    TestReactiveSourceTargetRewriteReturnsToSource),
                new TestCase(
                    "reactive condition preserves visibility state",
                    TestReactiveConditionPreservesVisibilityState),
                new TestCase("reactive binding manual rebind replaces source", TestReactiveManualRebind),
                new TestCase(
                    "reactive target commit precedes manual reload",
                    TestReactiveTargetCommitPrecedesManualReload),
                new TestCase(
                    "reactive rebind distinguishes equal source versions",
                    TestReactiveRebindDistinguishesEqualSourceVersions),
                new TestCase("reactive binding parser validation", TestReactiveParserValidation),
                new TestCase(
                    "computed binding condition expressions",
                    ConditionExpressionTests.Run),
                new TestCase(
                    "conditional styles and property objects",
                    ConditionalMarkupTests.Run),
                new TestCase(
                    "preset selected-name Boolean expressions",
                    PresetConditionExpressionTests.Run),
                new TestCase(
                    "dynamic element names are rejected clearly",
                    TestDynamicElementNameValidation),
                new TestCase("reactive binding target validation", TestReactiveTargetValidation),
                new TestCase(
                    "reactive component invocation supports two-way",
                    TestReactiveComponentInvocationTwoWay),
                new TestCase(
                    "item component invocation selects code-behind source",
                    TestItemComponentInvocationUsesCodeBehindSource),
                new TestCase("reactive binding disposal detaches", TestReactiveBindingDisposal),
                new TestCase(
                    "reactive non-root disposal detaches binding",
                    TestReactiveNonRootDisposalDetachesBinding),
                new TestCase("reactive binding pre-handle and worker dispatch", TestReactivePreHandleAndWorkerDispatch),
                new TestCase("binding audit regressions", BindingAuditRegressionTests.Run),
                new TestCase("core lifecycle and type regressions", CoreAuditRegressionTests.Run),
                new TestCase(
                    "direct viewport logical model",
                    VirtualViewportModelFocusedTests.Run),
                new TestCase(
                    "direct viewport eligibility",
                    VirtualizationEligibilityFocusedTests.Run),
                new TestCase(
                    "direct viewport realization manager",
                    VirtualRealizationManagerTests.Run),
                new TestCase("WPF-style Image control", ImageControlRegressionTests.Run),
                new TestCase("item template resource scope regressions", ItemTemplateResourceScopeTests.Run),
                new TestCase("preset refresh regressions", PresetAuditRegressionTests.Run),
                new TestCase(
                    "TabView markup, styles, selection, and collection",
                    TabViewIntegrationTests.Run),
                new TestCase(
                    "XML include composition",
                    IncludesIntegrationTests.Run),
                new TestCase("embedded XML loading", TestEmbeddedXmlLoading),
                new TestCase(
                    "embedded preset source uses markup assembly",
                    TestEmbeddedPresetUsesMarkupAssembly),
                new TestCase("embedded XML relative file base", TestEmbeddedXmlRelativeFileBase),
                new TestCase("Form convenience and discovery APIs", FormConvenienceTests.Run),
                new TestCase(
                    "XmlForm property notification helper",
                    FormConvenienceTests.RunPropertyNotificationTests),
                new TestCase(
                    "INotifyPropertyChanged binding graph",
                    NotifyPropertyChangedBindingTests.Run),
                new TestCase(
                    "structured markup load diagnostics",
                    MarkupDiagnosticsTests.Run),
                new TestCase("native WinForms type names stay native", TestNativeTypeNames),
                new TestCase("registered C# component constructor", TestRegisteredClassComponent),
                new TestCase("registered XML component reload scopes", TestRegisteredXmlComponentReloads),
                new TestCase("registered XML component code-behind and children", ComponentCodeBehindTests.Run),
                new TestCase(
                    "registered XML component content slot",
                    TestRegisteredXmlComponentContentSlot),
                new TestCase(
                    "component preset source uses component assembly",
                    TestComponentPresetUsesComponentAssembly),
                new TestCase(
                    "registered component conditions remain independent",
                    TestRegisteredComponentConditionsRemainIndependent),
                new TestCase(
                    "registered component templates parse once per runtime",
                    TestRegisteredComponentTemplateParsingCache),
                new TestCase(
                    "registered component root disposal detaches sources",
                    TestRegisteredComponentRootDisposalDetachesSources),
                new TestCase("registered XML component item reload", TestRegisteredXmlComponentItemReload),
                new TestCase("registered component validation", TestRegisteredComponentValidation),
                new TestCase("preset value in style setter", TestPresetStyleSetter),
                new TestCase("style switch removes stale setter binding", TestStyleSwitchRemovesStaleSetterBinding),
                new TestCase("style switch clears omitted property", TestStyleSwitchClearsOmittedProperty),
                new TestCase("reentrant style reload keeps newest style", TestReentrantStyleReloadKeepsNewestStyle),
                new TestCase("button visual-style background returns", TestButtonVisualStyleBackgroundReturns),
                new TestCase("implicit background returns after style switch", TestImplicitBackgroundReturnsAfterStyleSwitch),
                new TestCase("dynamic local value wins after style switch", TestLocalValueWinsAfterStyleSwitch),
                new TestCase("static local value wins after style switch", TestStaticLocalValueWinsAfterStyleSwitch),
                new TestCase("BackColor local value blocks Background style", TestBackColorLocalValueBlocksBackgroundStyle),
                new TestCase("style switch restores field-backed layout values", TestFieldBackedStyleValuesRestore),
                new TestCase("style visibility aliases share layout state", TestVisibilityAliasLayering),
                new TestCase("min and max style axes retain local values", TestSizeAxisStylePrecedence),
                new TestCase("size and orientation aliases retain local values", TestCompositeSizeAndOrientationPrecedence),
                new TestCase("font style axes retain local values", TestFontAxisStylePrecedence),
                new TestCase("ambient font inheritance returns after style", TestAmbientFontInheritanceReturns),
                new TestCase("typed composite bindings retain layout metadata", TestTypedCompositeBindings),
                new TestCase("typed RightToLeft binding stays explicit", TestTypedRightToLeftBinding),
                new TestCase("mapped aliases do not restore shadow properties", TestMappedAliasShadowProperties),
                new TestCase("dynamic Content style uses exact CLR property", TestDynamicExactContentStyle),
                new TestCase("bound Content keeps exact local precedence", TestBoundExactContentPrecedence),
                new TestCase("menu Content event does not intercept style alias", TestMenuContentEventStyle),
                new TestCase("Padding style restores native shadowed property", TestPaddingShadowStyle),
                new TestCase("mapped native property restores past a shadow", TestMappedNativeShadowRestore),
                new TestCase("WebBrowser Source typed null uses native blank state", TestWebBrowserSourceTypedNull),
                new TestCase("TextBox local Text blocks Text styles", TestTextBoxLocalTextPrecedence),
                new TestCase("failed native setters preserve coherent state", TestFailedNativeSetterState),
                new TestCase("Label AutoSize baseline returns", TestLabelAutoSizeBaselineReturns),
                new TestCase("failed style restore retries baseline", TestFailedStyleRestoreRetriesBaseline),
                new TestCase("failed dependent style restore preserves lower baseline", TestFailedDependentStyleRestore),
                new TestCase("style event reload replaces handler", TestStyleEventReplacement),
                new TestCase("dynamic style event setter reloads handler", TestDynamicStyleEventSetter),
                new TestCase("style switch detaches omitted event", TestStyleSwitchDetachesOmittedEvent),
                new TestCase("style switch preserves external event", TestStyleSwitchPreservesExternalEvent),
                new TestCase("style switch preserves same-handler local event", TestStyleSwitchPreservesSameHandlerLocalEvent),
                new TestCase("runtime owns its event registration", TestRuntimeOwnsEventRegistration),
                new TestCase(
                    "bound event target index uses reference identity",
                    TestBoundEventTargetIndexUsesReferenceIdentity),
                new TestCase("failed custom event add leaves no live handler", TestFailedCustomEventAdd),
                new TestCase("custom event accessor reentry keeps newest handlers", TestCustomEventAccessorReentry),
                new TestCase("disposing inside event add leaves no live handler", TestDisposeInsideEventAdd),
                new TestCase("failed event remove retries on dispose", TestFailedEventRemoveRetry),
                new TestCase("target release rejects reentrant event registration", TestTargetReleaseRejectsReentrantEvent),
                new TestCase("failed child attachment disposes child", TestFailedChildAttachmentDisposesChild),
                new TestCase("legacy marquee capability matrix", TestLegacyMarqueeCapabilityMatrix),
                new TestCase("legacy marquee frame mapping", TestLegacyMarqueeFrameMapping),
                new TestCase("legacy marquee native API state", TestLegacyMarqueeState),
                new TestCase("legacy marquee pause preserves phase", TestLegacyMarqueePausePreservesPhase),
                new TestCase("shared runtime preserves preset state", TestSharedRuntimePresets),
                new TestCase("application icon defaults", TestApplicationIconDefaults),
                new TestCase("shared decoded image lifetime", TestSharedImageLifetime),
                new TestCase(
                    "reentrant owned values follow installed properties",
                    TestReentrantOwnedPropertyAssignment),
                new TestCase("root disposal ends runtime", TestRootDisposal)
            };

            int failed = 0;
            int i;

            for (i = 0; i < tests.Length; i++)
            {
                try
                {
                    tests[i].Method();
                    Console.WriteLine("PASS  " + tests[i].Name);
                }
                catch (Exception ex)
                {
                    failed++;
                    Console.Error.WriteLine("FAIL  " + tests[i].Name);
                    Console.Error.WriteLine(ex.ToString());
                }
            }

            Console.WriteLine(
                "WinFormsXaml: " +
                (tests.Length - failed) +
                " passed, " +
                failed +
                " failed.");

            return failed == 0 ? 0 : 1;
        }

        private static void TestDefaultFallback()
        {
            PresetManager manager = new PresetManager();

            manager.LoadXml(
                "<Presets Name='Theme' Selected='Dark' Default='Light'>" +
                "  <Preset Name='Light'>" +
                "    <Set Key='Caption' Value='Light caption' />" +
                "    <Set Key='Surface' Value='White' />" +
                "  </Preset>" +
                "  <Preset Name='Dark'>" +
                "    <Set Key='Surface' Value='Black' />" +
                "  </Preset>" +
                "</Presets>");

            AssertEqual("Dark", manager["Theme"].SelectedName, "selected preset");
            AssertEqual("Light", manager["Theme"].DefaultName, "default preset");
            AssertEqual("Black", manager.Resolve("Theme", "Surface"), "selected value");
            AssertEqual("Light caption", manager.Resolve("Theme", "Caption"), "fallback value");
        }

        private static void TestPresetValueAttributeIsRequired()
        {
            PresetManager manager = new PresetManager();

            manager.LoadXml(
                "<Presets Name='Theme' Selected='Light'>" +
                "  <Preset Name='Light'>" +
                "    <Set Key='Canonical' Value='White' />" +
                "    <Set Key='Empty' Value='' />" +
                "  </Preset>" +
                "</Presets>");

            AssertEqual(
                "White",
                manager.Resolve("Theme", "Canonical"),
                "Value attribute supplies the preset value");
            AssertEqual(
                String.Empty,
                manager.Resolve("Theme", "Empty"),
                "empty Value attribute remains an explicit value");

            ExpectInvalidOperation(
                delegate
                {
                    manager.LoadXml(
                        "<Presets Name='Invalid'>" +
                        "<Preset Name='Only'>" +
                        "<Set Key='Legacy'>Black</Set>" +
                        "</Preset></Presets>");
                });
            ExpectInvalidOperation(
                delegate
                {
                    manager.LoadXml(
                        "<Presets Name='Invalid'>" +
                        "<Preset Name='Only'>" +
                        "<Set Key='Mixed' Value='White'>Ignored</Set>" +
                        "</Preset></Presets>");
                });
            ExpectInvalidOperation(
                delegate
                {
                    manager.LoadXml(
                        "<Preserts Name='Invalid'>" +
                        "<Preset Name='Only'>" +
                        "<Set Key='Value' Value='Rejected' />" +
                        "</Preset></Preserts>");
                });
            ExpectInvalidOperation(
                delegate
                {
                    XamlRuntime.Load(
                        "<Panel><Preserts Name='Invalid' /></Panel>");
                });
        }

        private static void TestReactiveTypedPresetValues()
        {
            ReactivePresetState state =
                new ReactivePresetState(Color.Red, "Initial");
            XamlRuntime runtime = XamlRuntime.Load(
                "<Panel>" +
                "  <Presets Name='Theme' Selected='Current'>" +
                "    <Preset Name='Current'>" +
                "      <Set Key='Surface' " +
                "           Value='{Binding Surface, Source=CodeBehind}' />" +
                "      <Set Key='Caption' " +
                "           Value='{Function Decorate(Caption)}' />" +
                "    </Preset>" +
                "  </Presets>" +
                "  <Label Name='Target' " +
                "         BackColor='{Preset Theme.Surface}' " +
                "         Text='{Preset Theme.Caption}' />" +
                "</Panel>",
                state);

            try
            {
                Label target = runtime.Get<Label>("Target");

                AssertEqual(
                    Color.Red.ToArgb(),
                    target.BackColor.ToArgb(),
                    "typed initial color");
                AssertEqual("[Initial]", target.Text, "function initial value");

                CreateHandleAndDrainReactiveCallbacks(runtime.RootControl);
                state.Surface.Value = Color.Blue;
                state.Caption.Value = "Updated";
                DrainReactiveCallbacks(runtime.RootControl);

                AssertEqual(
                    Color.Blue.ToArgb(),
                    target.BackColor.ToArgb(),
                    "typed reactive color");
                AssertEqual("[Updated]", target.Text, "reactive function argument");
            }
            finally
            {
                runtime.Dispose();
            }

            AssertEqual(
                0,
                GetPropertyBindingSubscriberCount(state.Surface),
                "typed preset source detached");
            AssertEqual(
                0,
                GetPropertyBindingSubscriberCount(state.Caption),
                "function preset source detached");
        }

        private static void TestNestedPresetDependenciesRefresh()
        {
            XamlRuntime runtime = XamlRuntime.Load(
                "<Panel>" +
                "  <Presets Name='Palette' Selected='Warm'>" +
                "    <Preset Name='Warm'>" +
                "      <Set Key='Accent' Value='Red' />" +
                "    </Preset>" +
                "    <Preset Name='Cool'>" +
                "      <Set Key='Accent' Value='Blue' />" +
                "    </Preset>" +
                "  </Presets>" +
                "  <Presets Name='Theme' Selected='Current'>" +
                "    <Preset Name='Current'>" +
                "      <Set Key='Accent' Value='{Preset Palette.Accent}' />" +
                "    </Preset>" +
                "  </Presets>" +
                "  <Label Name='Target' ForeColor='{Preset Theme.Accent}' />" +
                "</Panel>");

            try
            {
                Label target = runtime.Get<Label>("Target");

                AssertEqual(Color.Red, target.ForeColor, "nested initial preset");
                CreateHandleAndDrainReactiveCallbacks(runtime.RootControl);

                runtime.Presets.Select("Palette", "Cool");
                DrainReactiveCallbacks(runtime.RootControl);

                AssertEqual(Color.Blue, target.ForeColor, "nested selected preset");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestReactivePresetReplacementDetachesOldSource()
        {
            ReactivePresetState state =
                new ReactivePresetState(Color.Empty, "Primary");
            XamlRuntime runtime = XamlRuntime.Load(
                "<Panel>" +
                "  <Presets Name='Text' Selected='Current'>" +
                "    <Preset Name='Current'>" +
                "      <Set Key='Caption' Value='{Binding Caption}' />" +
                "    </Preset>" +
                "  </Presets>" +
                "  <Label Name='Target' Text='{Preset Text.Caption}' />" +
                "</Panel>",
                state);

            try
            {
                Label target = runtime.Get<Label>("Target");

                AssertEqual("Primary", target.Text, "primary preset source");
                AssertEqual(
                    1,
                    GetPropertyBindingSubscriberCount(state.Caption),
                    "primary source subscribed");

                CreateHandleAndDrainReactiveCallbacks(runtime.RootControl);
                runtime.Presets.SetValue(
                    "Text",
                    "Current",
                    "Caption",
                    "{Binding AlternateCaption}");
                DrainReactiveCallbacks(runtime.RootControl);

                AssertEqual(
                    "Primary alternate",
                    target.Text,
                    "replacement source applied");
                AssertEqual(
                    0,
                    GetPropertyBindingSubscriberCount(state.Caption),
                    "replaced source detached");
                AssertEqual(
                    1,
                    GetPropertyBindingSubscriberCount(state.AlternateCaption),
                    "replacement source subscribed");

                state.Caption.Value = "Stale";
                state.AlternateCaption.Value = "Current";
                DrainReactiveCallbacks(runtime.RootControl);
                AssertEqual("Current", target.Text, "only replacement remains live");
            }
            finally
            {
                runtime.Dispose();
            }

            AssertEqual(
                0,
                GetPropertyBindingSubscriberCount(state.AlternateCaption),
                "replacement source detached on disposal");
        }

        private static void TestSharedReactivePresetRuntimeIsolation()
        {
            PresetManager manager = new PresetManager();
            manager.LoadXml(
                "<Presets Name='Text' Selected='Current'>" +
                "  <Preset Name='Current'>" +
                "    <Set Key='Caption' Value='{Binding Caption}' />" +
                "  </Preset>" +
                "</Presets>");

            ReactivePresetState firstState =
                new ReactivePresetState(Color.Empty, "First");
            ReactivePresetState secondState =
                new ReactivePresetState(Color.Empty, "Second");
            XamlRuntime first = XamlRuntime.Load(
                "<Label Name='Target' Text='{Preset Text.Caption}' />",
                firstState,
                null,
                manager);
            XamlRuntime second = XamlRuntime.Load(
                "<Label Name='Target' Text='{Preset Text.Caption}' />",
                secondState,
                null,
                manager);

            try
            {
                Label firstTarget = first.Get<Label>("Target");
                Label secondTarget = second.Get<Label>("Target");

                AssertEqual("First", firstTarget.Text, "first runtime context");
                AssertEqual("Second", secondTarget.Text, "second runtime context");
                AssertEqual(
                    1,
                    GetPropertyBindingSubscriberCount(firstState.Caption),
                    "first runtime subscription");
                AssertEqual(
                    1,
                    GetPropertyBindingSubscriberCount(secondState.Caption),
                    "second runtime subscription");

                CreateHandleAndDrainReactiveCallbacks(first.RootControl);
                CreateHandleAndDrainReactiveCallbacks(second.RootControl);
                firstState.Caption.Value = "First updated";
                DrainReactiveCallbacks(first.RootControl);

                AssertEqual("First updated", firstTarget.Text, "first update");
                AssertEqual("Second", secondTarget.Text, "second remains isolated");

                first.Dispose();
                AssertEqual(
                    0,
                    GetPropertyBindingSubscriberCount(firstState.Caption),
                    "disposed shared runtime detached");
                AssertEqual(
                    1,
                    GetPropertyBindingSubscriberCount(secondState.Caption),
                    "remaining shared runtime stays subscribed");
            }
            finally
            {
                first.Dispose();
                second.Dispose();
            }

            AssertEqual(
                0,
                GetPropertyBindingSubscriberCount(secondState.Caption),
                "last shared runtime detached");
        }

        private static void TestReactivePresetCycleValidation()
        {
            ExpectInvalidMarkupMessage(
                "<Panel>" +
                "  <Presets Name='Cycle' Selected='Current'>" +
                "    <Preset Name='Current'>" +
                "      <Set Key='First' Value='{Preset Cycle.Second}' />" +
                "      <Set Key='Second' Value='{Preset Cycle.First}' />" +
                "    </Preset>" +
                "  </Presets>" +
                "  <Label Text='{Preset Cycle.First}' />" +
                "</Panel>",
                null,
                "Preset values contain a reference cycle");
        }

        private static void TestMutationApi()
        {
            PresetManager manager = new PresetManager();
            int changeCount = 0;

            manager.Changed +=
                delegate(object sender, PresetChangedEventArgs e)
                {
                    changeCount++;
                };

            PresetSet theme = manager.AddSet("Theme");
            Preset light = theme.AddPreset("Light");
            Preset dark = theme.AddPreset("Dark");

            light.AddValue("Caption", "Light");
            dark.AddValue("Caption", "Dark");
            theme.SetDefault("Light");
            theme.Select("Dark");

            AssertEqual("Dark", manager.Resolve("theme", "caption"), "case-insensitive lookup");

            manager.SetValue("Theme", "Dark", "Caption", "Updated");
            AssertEqual("Updated", dark["Caption"], "updated value");

            AssertTrue(manager.RemoveValue("Theme", "Dark", "Caption"), "value removal");
            AssertEqual("Light", theme.Resolve("Caption"), "fallback after removal");

            AssertTrue(theme.RemovePreset("Dark"), "preset removal");
            AssertEqual("Light", theme.SelectedName, "selection after removing selected preset");
            AssertTrue(changeCount > 0, "mutations should raise Changed");
        }

        private static void TestDuplicateXmlKey()
        {
            PresetManager manager = new PresetManager();
            int changeCount = 0;

            manager.Changed +=
                delegate(object sender, PresetChangedEventArgs e)
                {
                    changeCount++;
                };

            ExpectInvalidOperation(
                delegate
                {
                    manager.LoadXml(
                        "<Presets Name='Theme'>" +
                        "  <Preset Name='Light'>" +
                        "    <Set Key='Accent' Value='Blue' />" +
                        "    <Set Key='accent' Value='Red' />" +
                        "  </Preset>" +
                        "</Presets>");
                });

            AssertTrue(!manager.Contains("Theme"), "rejected import must not create a set");
            AssertEqual(0, changeCount, "rejected import event count");
        }

        private static void TestTransactionalImport()
        {
            PresetManager manager = new PresetManager();

            manager.LoadXml(
                "<Presets Name='Theme' Selected='Light'>" +
                "  <Preset Name='Light'><Set Key='Accent' Value='Blue' /></Preset>" +
                "</Presets>");

            int changeCount = 0;
            manager.Changed +=
                delegate(object sender, PresetChangedEventArgs e)
                {
                    changeCount++;
                };

            ExpectInvalidOperation(
                delegate
                {
                    manager.LoadXml(
                        "<PresetDocument>" +
                        "  <Presets Name='Theme'>" +
                        "    <Preset Name='Light'><Set Key='Accent' Value='Red' /></Preset>" +
                        "  </Presets>" +
                        "  <Presets Name='Broken'>" +
                        "    <Preset Name='Only'>" +
                        "      <Set Key='Duplicate' Value='One' />" +
                        "      <Set Key='duplicate' Value='Two' />" +
                        "    </Preset>" +
                        "  </Presets>" +
                        "</PresetDocument>");
                });

            AssertEqual("Blue", manager.Resolve("Theme", "Accent"), "pre-import value");
            AssertTrue(!manager.Contains("Broken"), "partially imported set");
            AssertEqual(0, changeCount, "failed import event count");
        }

        private static void TestPreserveExistingImport()
        {
            PresetManager manager = new PresetManager();

            manager.LoadXml(
                "<Presets Name='Theme' Selected='Dark' Default='Dark'>" +
                "  <Preset Name='Dark'><Set Key='Accent' Value='Runtime value' /></Preset>" +
                "</Presets>");

            int changeCount = 0;
            PresetChangedEventArgs lastChange = null;

            manager.Changed +=
                delegate(object sender, PresetChangedEventArgs e)
                {
                    changeCount++;
                    lastChange = e;
                };

            manager.LoadXml(
                "<Presets Name='Theme' Selected='Light' Default='Light'>" +
                "  <Preset Name='Dark'>" +
                "    <Set Key='Accent' Value='Declared value' />" +
                "    <Set Key='Spacing' Value='12' />" +
                "  </Preset>" +
                "  <Preset Name='Light'><Set Key='Accent' Value='Blue' /></Preset>" +
                "</Presets>",
                PresetImportMode.PreserveExisting);

            PresetSet theme = manager["Theme"];

            AssertEqual("Dark", theme.SelectedName, "preserved selection");
            AssertEqual("Dark", theme.DefaultName, "preserved default");
            AssertEqual("Runtime value", theme["Dark"]["Accent"], "preserved key");
            AssertEqual("12", theme["Dark"]["Spacing"], "added key");
            AssertEqual("Blue", theme["Light"]["Accent"], "added preset");
            AssertEqual(1, changeCount, "successful import event count");
            AssertTrue(lastChange != null, "successful import event arguments");
            AssertEqual(null, lastChange.SetName, "successful import event scope");
            AssertEqual(null, lastChange.PresetName, "successful import preset scope");
            AssertEqual(null, lastChange.Key, "successful import key scope");

            manager.LoadXml(
                "<Presets Name='Theme' Selected='Light' Default='Light'>" +
                "  <Preset Name='Dark'>" +
                "    <Set Key='Accent' Value='Declared value' />" +
                "    <Set Key='Spacing' Value='12' />" +
                "  </Preset>" +
                "  <Preset Name='Light'><Set Key='Accent' Value='Blue' /></Preset>" +
                "</Presets>",
                PresetImportMode.PreserveExisting);

            AssertEqual(1, changeCount, "no-op import event count");
        }

        private static void TestDeferredNotifications()
        {
            PresetManager manager = new PresetManager();

            manager.LoadXml(
                "<Presets Name='Theme' Selected='Light'>" +
                "  <Preset Name='Light'>" +
                "    <Set Key='Background' Value='White' />" +
                "    <Set Key='Foreground' Value='Black' />" +
                "  </Preset>" +
                "</Presets>");

            int changeCount = 0;
            PresetChangedEventArgs lastChange = null;

            manager.Changed +=
                delegate(object sender, PresetChangedEventArgs e)
                {
                    changeCount++;
                    lastChange = e;
                };

            using (manager.DeferNotifications())
            {
                manager.SetValue(
                    "Theme",
                    "Light",
                    "Background",
                    "Ivory");
                manager.SetValue(
                    "Theme",
                    "Light",
                    "Foreground",
                    "Navy");
            }

            AssertEqual(1, changeCount, "deferred event count");
            AssertTrue(lastChange != null, "deferred event arguments");
            AssertEqual(null, lastChange.SetName, "coalesced event scope");
            AssertEqual("Ivory", manager.Resolve("Theme", "Background"), "first value");
            AssertEqual("Navy", manager.Resolve("Theme", "Foreground"), "second value");
        }

        private static void TestOrdinaryPropertyReload()
        {
            BindingState state = new BindingState();
            state.Text = "Before";

            XamlRuntime runtime =
                XamlRuntime.Load(
                    "<Label Name='Caption' Text='{Binding Text}' />",
                    state);

            try
            {
                Label caption = runtime.Get<Label>("Caption");
                AssertEqual("Before", caption.Text, "initial binding");

                state.Text = "After";
                runtime.ReloadBinding("Caption", "Text");
                AssertEqual("After", caption.Text, "reloaded binding");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestNegativeBooleanBinding()
        {
            BindingState state = new BindingState();
            state.IsReady = false;

            XamlRuntime runtime = XamlRuntime.Load(
                "<Button Name='Action' Enabled='{Binding !IsReady}' />",
                state);

            try
            {
                Button action = runtime.Get<Button>("Action");
                AssertEqual(true, action.Enabled, "initial negated binding");

                state.IsReady = true;
                runtime.ReloadBinding("Action", "Enabled");
                AssertEqual(false, action.Enabled, "reloaded negated binding");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestPropertyBindingValueSemantics()
        {
            PropertyBinding<string> defaultValue =
                new PropertyBinding<string>();
            PropertyBinding<int> defaultNumber =
                new PropertyBinding<int>();
            PropertyBinding<int> initialValue =
                new PropertyBinding<int>(7);
            int changeCount = 0;
            object lastSender = null;
            EventArgs lastArgs = null;
            EventHandler handler =
                delegate(object sender, EventArgs e)
                {
                    changeCount++;
                    lastSender = sender;
                    lastArgs = e;
                };

            AssertEqual(null, defaultValue.Value, "default PropertyBinding value");
            AssertEqual(0, defaultNumber.Value, "default value-type binding value");
            AssertEqual(7, initialValue.Value, "explicit PropertyBinding value");

            defaultValue.ValueChanged += handler;
            defaultValue.Value = null;
            AssertEqual(0, changeCount, "equal null does not notify");

            defaultValue.Value = "First";
            AssertEqual(1, changeCount, "first changed value notification");
            AssertSame(defaultValue, lastSender, "ValueChanged sender");
            AssertSame(EventArgs.Empty, lastArgs, "ValueChanged arguments");

            defaultValue.Value = new string(new char[] { 'F', 'i', 'r', 's', 't' });
            AssertEqual(1, changeCount, "default equality suppresses equal string");

            defaultValue.ValueChanged -= handler;
            defaultValue.Value = "Second";
            AssertEqual(1, changeCount, "removed ValueChanged handler");
            AssertEqual("Second", defaultValue.Value, "value changes after removal");

            PropertyBinding<int> fanout =
                new PropertyBinding<int>(0);
            int laterListenerCalls = 0;
            EventHandler failingListener =
                delegate
                {
                    throw new InvalidOperationException(
                        "expected listener failure");
                };
            EventHandler laterListener =
                delegate
                {
                    laterListenerCalls++;
                };

            fanout.ValueChanged += failingListener;
            fanout.ValueChanged += laterListener;
            object fanoutSnapshot =
                GetInstanceField(
                    fanout,
                    "_valueChangedSubscribers");

            bool listenerFailureReported = false;

            try
            {
                fanout.Value = 1;
            }
            catch (InvalidOperationException ex)
            {
                listenerFailureReported =
                    ex.Message == "expected listener failure";
            }

            AssertTrue(
                listenerFailureReported,
                "PropertyBinding reports the first listener failure");
            AssertEqual(
                1,
                laterListenerCalls,
                "PropertyBinding listener failure does not starve later listeners");
            AssertEqual(
                1,
                fanout.Value,
                "PropertyBinding commits the changed value before notification");
            AssertSame(
                fanoutSnapshot,
                GetInstanceField(
                    fanout,
                    "_valueChangedSubscribers"),
                "PropertyBinding reuses its immutable dispatch snapshot");

            fanout.ValueChanged -= failingListener;
            fanout.ValueChanged -= laterListener;

            PropertyBinding<int> reentrant =
                new PropertyBinding<int>(0);
            int removedCalls = 0;
            int addedCalls = 0;
            bool subscriptionsChanged = false;
            EventHandler removed =
                delegate
                {
                    removedCalls++;
                };
            EventHandler added =
                delegate
                {
                    addedCalls++;
                };
            EventHandler mutating =
                delegate
                {
                    if (subscriptionsChanged)
                        return;

                    subscriptionsChanged = true;
                    reentrant.ValueChanged -= removed;
                    reentrant.ValueChanged += added;
                };

            reentrant.ValueChanged += mutating;
            reentrant.ValueChanged += removed;
            reentrant.Value = 1;

            AssertEqual(
                1,
                removedCalls,
                "reentrant removal does not alter the active snapshot");
            AssertEqual(
                0,
                addedCalls,
                "reentrant addition waits for the next notification");

            reentrant.Value = 2;

            AssertEqual(
                1,
                removedCalls,
                "removed listener stays absent from the next snapshot");
            AssertEqual(
                1,
                addedCalls,
                "added listener participates in the next snapshot");

            reentrant.ValueChanged -= mutating;
            reentrant.ValueChanged -= added;
        }

        private static void TestReactiveOneWayBindings()
        {
            ReactiveBindingState state = new ReactiveBindingState();
            ReactiveChildState firstChild = state.Nested.Value;
            XamlRuntime runtime = XamlRuntime.Load(
                "<Panel>" +
                "  <Label Name='Direct' Text='{Binding Text}' />" +
                "  <Label Name='Interpolated' " +
                "      Text='[{Binding Text}] {Binding Nested.Text}' />" +
                "  <Label Name='Nested' Text='{Binding Nested.Text}' />" +
                "  <Label Name='Explicit' " +
                "      Text='{Binding Path=SecondaryText, Mode=OneWay}' />" +
                "</Panel>",
                state);

            try
            {
                Label direct = runtime.Get<Label>("Direct");
                Label interpolated = runtime.Get<Label>("Interpolated");
                Label nested = runtime.Get<Label>("Nested");
                Label explicitOneWay = runtime.Get<Label>("Explicit");

                AssertEqual("Initial", direct.Text, "initial direct wrapper value");
                AssertEqual(
                    "[Initial] Nested initial",
                    interpolated.Text,
                    "initial interpolated wrapper values");
                AssertEqual(
                    "Nested initial",
                    nested.Text,
                    "initial nested wrapper value");
                AssertEqual(
                    "Secondary",
                    explicitOneWay.Text,
                    "initial explicit one-way value");
                AssertEqual(
                    1,
                    GetPropertyBindingSubscriberCount(state.Text),
                    "shared direct source subscription");
                AssertEqual(
                    1,
                    GetPropertyBindingSubscriberCount(firstChild.Text),
                    "shared nested source subscription");

                CreateHandleAndDrainReactiveCallbacks(runtime.RootControl);

                state.Text.Value = "Updated";
                state.SecondaryText.Value = "Secondary updated";
                firstChild.Text.Value = "Nested updated";
                DrainReactiveCallbacks(runtime.RootControl);

                AssertEqual("Updated", direct.Text, "automatic direct update");
                AssertEqual(
                    "[Updated] Nested updated",
                    interpolated.Text,
                    "automatic interpolated update");
                AssertEqual(
                    "Nested updated",
                    nested.Text,
                    "automatic nested update");
                AssertEqual(
                    "Secondary updated",
                    explicitOneWay.Text,
                    "automatic explicit one-way update");

                ReactiveChildState secondChild =
                    new ReactiveChildState("Replacement child");
                state.Nested.Value = secondChild;
                DrainReactiveCallbacks(runtime.RootControl);

                AssertEqual(
                    "Replacement child",
                    nested.Text,
                    "intermediate wrapper replacement");
                AssertEqual(
                    0,
                    GetPropertyBindingSubscriberCount(firstChild.Text),
                    "obsolete nested wrapper detached");
                AssertEqual(
                    1,
                    GetPropertyBindingSubscriberCount(secondChild.Text),
                    "replacement nested wrapper subscribed");

                firstChild.Text.Value = "Stale child";
                DrainReactiveCallbacks(runtime.RootControl);
                AssertEqual(
                    "Replacement child",
                    nested.Text,
                    "detached nested wrapper ignored");

                secondChild.Text.Value = "Current child";
                DrainReactiveCallbacks(runtime.RootControl);
                AssertEqual(
                    "Current child",
                    nested.Text,
                    "replacement nested wrapper remains live");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestReactiveTwoWayPropertiesAndAliases()
        {
            ReactiveBindingState state = new ReactiveBindingState();
            XamlRuntime runtime = XamlRuntime.Load(
                "<FlowLayoutPanel>" +
                "  <TextBox Name='TextTarget' " +
                "      Text='{Binding Text, Mode=TwoWay}' />" +
                "  <CheckBox Name='CheckedTarget' " +
                "      Checked='{Binding Checked, Mode=TwoWay}' />" +
                "  <NumericUpDown Name='ValueTarget' " +
                "      Value='{Binding Number, Mode=TwoWay}' />" +
                "  <Label Name='ContentTarget' " +
                "      Content='{Binding ContentText, Mode=TwoWay}' />" +
                "  <Label Name='HeaderTarget' " +
                "      Header='{Binding HeaderText, Mode=TwoWay}' />" +
                "  <Label Name='TitleTarget' " +
                "      Title='{Binding TitleText, Mode=TwoWay}' />" +
                "  <CheckBox Name='AliasCheckedTarget' " +
                "      IsChecked='{Binding AliasChecked, Mode=TwoWay}' />" +
                "  <Button Name='EnabledTarget' " +
                "      IsEnabled='{Binding Enabled, Mode=TwoWay}' />" +
                "  <Button Name='TabStopTarget' " +
                "      IsTabStop='{Binding TabStop, Mode=TwoWay}' />" +
                "  <TextBox Name='ReadOnlyTarget' " +
                "      IsReadOnly='{Binding ReadOnly, Mode=TwoWay}' />" +
                "  <Panel Name='ForegroundTarget' " +
                "      Foreground='{Binding Foreground, Mode=TwoWay}' />" +
                "  <Panel Name='BackgroundTarget' " +
                "      Background='{Binding Background, Mode=TwoWay}' />" +
                "</FlowLayoutPanel>",
                state);

            try
            {
                TextBox text = runtime.Get<TextBox>("TextTarget");
                CheckBox check = runtime.Get<CheckBox>("CheckedTarget");
                NumericUpDown number =
                    runtime.Get<NumericUpDown>("ValueTarget");
                Label content = runtime.Get<Label>("ContentTarget");
                Label header = runtime.Get<Label>("HeaderTarget");
                Label title = runtime.Get<Label>("TitleTarget");
                CheckBox aliasCheck =
                    runtime.Get<CheckBox>("AliasCheckedTarget");
                Button enabled = runtime.Get<Button>("EnabledTarget");
                Button tabStop = runtime.Get<Button>("TabStopTarget");
                TextBox readOnly = runtime.Get<TextBox>("ReadOnlyTarget");
                Panel foreground = runtime.Get<Panel>("ForegroundTarget");
                Panel background = runtime.Get<Panel>("BackgroundTarget");

                AssertEqual("Initial", text.Text, "initial Text endpoint");
                AssertEqual(false, check.Checked, "initial Checked endpoint");
                AssertEqual(1m, number.Value, "initial Value endpoint");
                AssertEqual(
                    "Content initial",
                    content.Text,
                    "initial Content alias endpoint");
                AssertEqual(
                    "Header initial",
                    header.Text,
                    "initial Header alias endpoint");
                AssertEqual(
                    "Title initial",
                    title.Text,
                    "initial Title alias endpoint");
                AssertEqual(
                    false,
                    aliasCheck.Checked,
                    "initial IsChecked alias endpoint");
                AssertEqual(true, enabled.Enabled, "initial IsEnabled endpoint");
                AssertEqual(
                    false,
                    tabStop.TabStop,
                    "initial IsTabStop endpoint");
                AssertEqual(false, readOnly.ReadOnly, "initial IsReadOnly endpoint");
                AssertEqual(
                    Color.Black.ToArgb(),
                    foreground.ForeColor.ToArgb(),
                    "initial Foreground endpoint");
                AssertEqual(
                    Color.White.ToArgb(),
                    background.BackColor.ToArgb(),
                    "initial Background endpoint");

                CreateHandleAndDrainReactiveCallbacks(runtime.RootControl);

                state.Text.Value = "Source text";
                state.Checked.Value = true;
                state.Number.Value = 4m;
                state.ContentText.Value = "Source content";
                state.HeaderText.Value = "Source header";
                state.TitleText.Value = "Source title";
                state.AliasChecked.Value = true;
                state.Enabled.Value = false;
                state.TabStop.Value = true;
                state.ReadOnly.Value = true;
                state.Foreground.Value = Color.Red;
                state.Background.Value = Color.Yellow;
                DrainReactiveCallbacks(runtime.RootControl);

                AssertEqual("Source text", text.Text, "source to Text");
                AssertEqual(true, check.Checked, "source to Checked");
                AssertEqual(4m, number.Value, "source to Value");
                AssertEqual("Source content", content.Text, "source to Content alias");
                AssertEqual("Source header", header.Text, "source to Header alias");
                AssertEqual("Source title", title.Text, "source to Title alias");
                AssertEqual(true, aliasCheck.Checked, "source to IsChecked alias");
                AssertEqual(false, enabled.Enabled, "source to IsEnabled alias");
                AssertEqual(true, tabStop.TabStop, "source to IsTabStop alias");
                AssertEqual(true, readOnly.ReadOnly, "source to IsReadOnly alias");
                AssertEqual(
                    Color.Red.ToArgb(),
                    foreground.ForeColor.ToArgb(),
                    "source to Foreground alias");
                AssertEqual(
                    Color.Yellow.ToArgb(),
                    background.BackColor.ToArgb(),
                    "source to Background alias");

                text.Text = "Target text";
                check.Checked = false;
                number.Value = 9m;
                content.Text = "Target content";
                header.Text = "Target header";
                title.Text = "Target title";
                aliasCheck.Checked = false;
                enabled.Enabled = true;
                tabStop.TabStop = false;
                readOnly.ReadOnly = false;
                foreground.ForeColor = Color.Blue;
                background.BackColor = Color.Green;
                DrainReactiveCallbacks(runtime.RootControl);

                AssertEqual("Target text", state.Text.Value, "Text to source");
                AssertEqual(false, state.Checked.Value, "Checked to source");
                AssertEqual(9m, state.Number.Value, "Value to source");
                AssertEqual(
                    "Target content",
                    state.ContentText.Value,
                    "Content alias to source");
                AssertEqual(
                    "Target header",
                    state.HeaderText.Value,
                    "Header alias to source");
                AssertEqual(
                    "Target title",
                    state.TitleText.Value,
                    "Title alias to source");
                AssertEqual(
                    false,
                    state.AliasChecked.Value,
                    "IsChecked alias to source");
                AssertEqual(true, state.Enabled.Value, "IsEnabled alias to source");
                AssertEqual(
                    false,
                    state.TabStop.Value,
                    "IsTabStop alias to source");
                AssertEqual(false, state.ReadOnly.Value, "IsReadOnly alias to source");
                AssertEqual(
                    Color.Blue,
                    state.Foreground.Value,
                    "Foreground alias to source");
                AssertEqual(
                    Color.Green,
                    state.Background.Value,
                    "Background alias to source");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestReactiveTwoWayTargetIsCurrentInsideClick()
        {
            ReactiveBindingState state = new ReactiveBindingState();
            XamlRuntime runtime = XamlRuntime.Load(
                "<TwoWayClickCheckBox Name='LaunchAtLogin' " +
                "    Checked='{Binding Checked, Mode=TwoWay}' />",
                state);

            try
            {
                TwoWayClickCheckBox target =
                    runtime.Get<TwoWayClickCheckBox>("LaunchAtLogin");
                bool clickRan = false;
                bool sourceValueInsideClick = false;

                target.Click +=
                    delegate(object sender, EventArgs e)
                    {
                        clickRan = true;
                        sourceValueInsideClick = state.Checked.Value;
                    };

                CreateHandleAndDrainReactiveCallbacks(runtime.RootControl);
                target.PerformUserClick();

                AssertEqual(true, clickRan, "CheckBox Click handler ran");
                AssertEqual(true, target.Checked, "CheckBox toggled before Click");
                AssertEqual(
                    true,
                    state.Checked.Value,
                    "CheckedChanged commits the source before Click");
                AssertEqual(
                    true,
                    sourceValueInsideClick,
                    "Click reads the current PropertyBinding.Value");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestReactiveTwoWayUpdateSourceTriggers()
        {
            ReactiveBindingState state = new ReactiveBindingState();
            XamlRuntime runtime = XamlRuntime.Load(
                "<FlowLayoutPanel>" +
                "  <TriggerTextBox Name='LostFocusTarget' " +
                "      Text='{Binding Text, Mode=TwoWay, " +
                "UpdateSourceTrigger=LostFocus}' />" +
                "  <TextBox Name='ExplicitTarget' " +
                "      Text='{Binding SecondaryText, Mode=TwoWay, " +
                "UpdateSourceTrigger=Explicit}' />" +
                "</FlowLayoutPanel>",
                state);

            try
            {
                TriggerTextBox lostFocus =
                    runtime.Get<TriggerTextBox>("LostFocusTarget");
                TextBox explicitTarget =
                    runtime.Get<TextBox>("ExplicitTarget");

                CreateHandleAndDrainReactiveCallbacks(runtime.RootControl);

                lostFocus.Text = "Pending lost focus";
                explicitTarget.Text = "Pending explicit";
                DrainReactiveCallbacks(runtime.RootControl);

                AssertEqual(
                    "Initial",
                    state.Text.Value,
                    "LostFocus defers target writeback");
                AssertEqual(
                    "Secondary",
                    state.SecondaryText.Value,
                    "Explicit defers target writeback");

                lostFocus.RaiseLostFocus();
                DrainReactiveCallbacks(runtime.RootControl);

                AssertEqual(
                    "Pending lost focus",
                    state.Text.Value,
                    "LostFocus commits the current target value");
                AssertEqual(
                    "Secondary",
                    state.SecondaryText.Value,
                    "LostFocus does not commit another target");

                runtime.UpdateBindingSource("ExplicitTarget", "Text");
                DrainReactiveCallbacks(runtime.RootControl);

                AssertEqual(
                    "Pending explicit",
                    state.SecondaryText.Value,
                    "explicit named-property update commits the source");

                state.SecondaryText.Value = "Source still reactive";
                DrainReactiveCallbacks(runtime.RootControl);

                AssertEqual(
                    "Source still reactive",
                    explicitTarget.Text,
                    "Explicit affects target-to-source only");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestReactiveTwoWayMappedAliasPrecedence()
        {
            AliasCollisionBindingState state =
                new AliasCollisionBindingState();
            XamlRuntime runtime = XamlRuntime.Load(
                "<Panel>" +
                "  <TwoWayAliasShadowCheckBox Name='Target' " +
                "      IsChecked='{Binding Checked, Mode=TwoWay}' " +
                "      Background='{Binding Background, Mode=TwoWay}' />" +
                "  <ContentShadowControl Name='WritableContent' " +
                "      Content='{Binding Content, Mode=TwoWay}' />" +
                "  <ReadOnlyContentShadowControl Name='ReadOnlyContent' " +
                "      Content='{Binding ReadOnlyContent, Mode=TwoWay}' />" +
                "</Panel>",
                state);

            try
            {
                TwoWayAliasShadowCheckBox target =
                    runtime.Get<TwoWayAliasShadowCheckBox>("Target");
                ContentShadowControl writableContent =
                    runtime.Get<ContentShadowControl>("WritableContent");
                ReadOnlyContentShadowControl readOnlyContent =
                    runtime.Get<ReadOnlyContentShadowControl>(
                        "ReadOnlyContent");

                AssertEqual(
                    true,
                    target.Checked,
                    "mapped IsChecked initializes native Checked");
                AssertEqual(
                    Color.Blue.ToArgb(),
                    target.BackColor.ToArgb(),
                    "mapped Background initializes native BackColor");
                AssertEqual(
                    0,
                    target.IsCheckedSetCount,
                    "mapped IsChecked ignores a shadow CLR alias");
                AssertEqual(
                    0,
                    target.BackgroundSetCount,
                    "mapped Background ignores a shadow CLR alias");
                AssertEqual(
                    "Custom content",
                    writableContent.Content,
                    "writable Content keeps its real CLR property");
                AssertEqual(
                    "Native text baseline",
                    writableContent.Text,
                    "writable Content does not use the Text fallback");
                AssertEqual(
                    "Native text content",
                    readOnlyContent.Text,
                    "read-only Content uses the reversible Text fallback");

                CreateHandleAndDrainReactiveCallbacks(runtime.RootControl);

                state.Checked.Value = false;
                state.Background.Value = Color.Yellow;
                state.Content.Value = "Source custom content";
                state.ReadOnlyContent.Value = "Source native text";
                DrainReactiveCallbacks(runtime.RootControl);

                AssertEqual(
                    false,
                    target.Checked,
                    "source update keeps using native Checked");
                AssertEqual(
                    Color.Yellow.ToArgb(),
                    target.BackColor.ToArgb(),
                    "source update keeps using native BackColor");
                AssertEqual(
                    0,
                    target.IsCheckedSetCount,
                    "source update leaves shadow IsChecked untouched");
                AssertEqual(
                    0,
                    target.BackgroundSetCount,
                    "source update leaves shadow Background untouched");
                AssertEqual(
                    "Source custom content",
                    writableContent.Content,
                    "source update keeps using writable Content");
                AssertEqual(
                    "Source native text",
                    readOnlyContent.Text,
                    "source update keeps using fallback Text");

                target.Checked = true;
                target.BackColor = Color.Green;
                writableContent.Content = "Target custom content";
                readOnlyContent.Text = "Target native text";
                DrainReactiveCallbacks(runtime.RootControl);

                AssertEqual(
                    true,
                    state.Checked.Value,
                    "native CheckedChanged updates the source");
                AssertEqual(
                    Color.Green,
                    state.Background.Value,
                    "native BackColorChanged updates the source");
                AssertEqual(
                    "Target custom content",
                    state.Content.Value,
                    "real ContentChanged updates the source");
                AssertEqual(
                    "Target native text",
                    state.ReadOnlyContent.Value,
                    "fallback TextChanged updates the source");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestReactiveTwoWayAlternateTargetEvents()
        {
            AlternateTargetEventBindingState state =
                new AlternateTargetEventBindingState();
            XamlRuntime runtime = XamlRuntime.Load(
                "<FlowLayoutPanel>" +
                "  <ComboBox Name='Combo' " +
                "      SelectedItem='{Binding SelectedItem, Mode=TwoWay}' />" +
                "  <DomainUpDown Name='Domain' " +
                "      SelectedIndex='{Binding DomainSelectedIndex, Mode=TwoWay}' />" +
                "  <MonthCalendar Name='Calendar' " +
                "      SelectionStart='{Binding CalendarStart, Mode=TwoWay}' />" +
                "  <TreeView Name='Tree' " +
                "      SelectedNode='{Binding SelectedNode, Mode=TwoWay}' />" +
                "  <TabControl Name='Tabs' " +
                "      SelectedTab='{Binding SelectedTab, Mode=TwoWay}' />" +
                "  <RichTextBox Name='Rich' Text='abcd' " +
                "      SelectionStart='{Binding RichSelectionStart, Mode=TwoWay}' />" +
                "</FlowLayoutPanel>",
                state);

            try
            {
                ComboBox combo = runtime.Get<ComboBox>("Combo");
                DomainUpDown domain = runtime.Get<DomainUpDown>("Domain");
                MonthCalendar calendar =
                    runtime.Get<MonthCalendar>("Calendar");
                TreeView tree = runtime.Get<TreeView>("Tree");
                TabControl tabs = runtime.Get<TabControl>("Tabs");
                RichTextBox rich = runtime.Get<RichTextBox>("Rich");

                combo.Items.Add("First");
                combo.Items.Add("Second");
                domain.Items.Add("First");
                domain.Items.Add("Second");

                TreeNode firstNode = tree.Nodes.Add("First");
                TreeNode secondNode = tree.Nodes.Add("Second");
                TabPage firstTab = new TabPage("First");
                TabPage secondTab = new TabPage("Second");
                tabs.TabPages.Add(firstTab);
                tabs.TabPages.Add(secondTab);

                CreateHandleAndDrainReactiveCallbacks(runtime.RootControl);

                DateTime secondDate = DateTime.Today.AddDays(1.0);
                combo.SelectedItem = "Second";
                domain.SelectedIndex = 1;
                calendar.SelectionStart = secondDate;
                tree.SelectedNode = secondNode;
                tabs.SelectedTab = secondTab;
                rich.SelectionStart = 2;
                DrainReactiveCallbacks(runtime.RootControl);

                AssertEqual(
                    "Second",
                    state.SelectedItem.Value,
                    "SelectedIndexChanged reports ComboBox.SelectedItem");
                AssertEqual(
                    1,
                    state.DomainSelectedIndex.Value,
                    "SelectedItemChanged reports DomainUpDown.SelectedIndex");
                AssertEqual(
                    secondDate,
                    state.CalendarStart.Value,
                    "DateChanged reports MonthCalendar.SelectionStart");
                AssertEqual(
                    secondNode,
                    state.SelectedNode.Value,
                    "AfterSelect reports TreeView.SelectedNode");
                AssertEqual(
                    secondTab,
                    state.SelectedTab.Value,
                    "SelectedIndexChanged reports TabControl.SelectedTab");
                AssertEqual(
                    2,
                    state.RichSelectionStart.Value,
                    "SelectionChanged reports RichTextBox.SelectionStart");

                long calendarVersion =
                    (long)GetInstanceField(state.CalendarStart, "_version");
                long richSelectionVersion =
                    (long)GetInstanceField(
                        state.RichSelectionStart,
                        "_version");

                calendar.SelectionEnd = secondDate.AddDays(1.0);
                rich.SelectionLength = 1;
                DrainReactiveCallbacks(runtime.RootControl);

                AssertEqual(
                    calendarVersion,
                    GetInstanceField(state.CalendarStart, "_version"),
                    "DateChanged does not claim an unchanged SelectionStart");
                AssertEqual(
                    richSelectionVersion,
                    GetInstanceField(
                        state.RichSelectionStart,
                        "_version"),
                    "SelectionChanged ignores unrelated selection properties");

                long selectedItemVersion =
                    (long)GetInstanceField(state.SelectedItem, "_version");
                combo.SelectedItem = "First";
                combo.SelectedItem = "Second";

                AssertEqual(
                    "Second",
                    state.SelectedItem.Value,
                    "changed-away-and-back SelectedItem keeps its last edit");
                AssertEqual(
                    selectedItemVersion + 2L,
                    GetInstanceField(state.SelectedItem, "_version"),
                    "each real SelectedItem transition claims source ordering");

                DateTime thirdDate = DateTime.Today.AddDays(2.0);
                state.SelectedItem.Value = "First";
                state.DomainSelectedIndex.Value = 0;
                state.CalendarStart.Value = thirdDate;
                state.SelectedNode.Value = firstNode;
                state.SelectedTab.Value = firstTab;
                state.RichSelectionStart.Value = 1;
                DrainReactiveCallbacks(runtime.RootControl);

                AssertEqual(
                    "First",
                    combo.SelectedItem,
                    "source updates ComboBox.SelectedItem");
                AssertEqual(
                    0,
                    domain.SelectedIndex,
                    "source updates DomainUpDown.SelectedIndex");
                AssertEqual(
                    thirdDate,
                    calendar.SelectionStart,
                    "source updates MonthCalendar.SelectionStart");
                AssertEqual(
                    firstNode,
                    tree.SelectedNode,
                    "source updates TreeView.SelectedNode");
                AssertEqual(
                    firstTab,
                    tabs.SelectedTab,
                    "source updates TabControl.SelectedTab");
                AssertEqual(
                    1,
                    rich.SelectionStart,
                    "source updates RichTextBox.SelectionStart");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestReactiveTwoWayMutableTargetEventCoverage()
        {
            MutableTargetEventBindingState state =
                new MutableTargetEventBindingState();
            XamlRuntime runtime = XamlRuntime.Load(
                "<Panel>" +
                "  <Panel Name='Geometry' " +
                "      Width='{Binding Width, Mode=TwoWay}' " +
                "      Height='{Binding Height, Mode=TwoWay}' " +
                "      Left='{Binding Left, Mode=TwoWay}' " +
                "      Top='{Binding Top, Mode=TwoWay}' />" +
                "  <TextBox Name='LinesEditor' Multiline='true' " +
                "      Lines='{Binding Lines, Mode=TwoWay}' />" +
                "  <RichTextBox Name='RichEditor' " +
                "      Rtf='{Binding Rtf, Mode=TwoWay}' " +
                "      IsReadOnly='{Binding RichReadOnly, Mode=TwoWay}' />" +
                "  <TwoWayScrollDataGridView Name='Grid' Width='80' " +
                "      HorizontalScrollingOffset=" +
                "'{Binding HorizontalOffset, Mode=TwoWay}' />" +
                "</Panel>",
                state);

            try
            {
                Panel geometry = runtime.Get<Panel>("Geometry");
                TextBox linesEditor = runtime.Get<TextBox>("LinesEditor");
                RichTextBox richEditor =
                    runtime.Get<RichTextBox>("RichEditor");
                TwoWayScrollDataGridView grid =
                    runtime.Get<TwoWayScrollDataGridView>("Grid");

                AssertEqual(120, geometry.Width, "initial bound Width");
                AssertEqual(40, geometry.Height, "initial bound Height");
                AssertEqual(5, geometry.Left, "initial bound Left");
                AssertEqual(7, geometry.Top, "initial bound Top");
                AssertEqual(2, linesEditor.Lines.Length, "initial bound Lines");
                AssertEqual(
                    false,
                    richEditor.ReadOnly,
                    "initial RichTextBox IsReadOnly alias");

                AssertObservableTargetRoute(
                    typeof(Panel),
                    "Width",
                    "Width",
                    "SizeChanged");
                AssertObservableTargetRoute(
                    typeof(Panel),
                    "Height",
                    "Height",
                    "SizeChanged");
                AssertObservableTargetRoute(
                    typeof(Panel),
                    "Left",
                    "Left",
                    "LocationChanged");
                AssertObservableTargetRoute(
                    typeof(Panel),
                    "Top",
                    "Top",
                    "LocationChanged");
                AssertObservableTargetRoute(
                    typeof(TextBox),
                    "Lines",
                    "Lines",
                    "TextChanged");
                AssertObservableTargetRoute(
                    typeof(MaskedTextBox),
                    "Lines",
                    "Lines",
                    "TextChanged");
                AssertObservableTargetRoute(
                    typeof(ToolStripTextBox),
                    "Lines",
                    "Lines",
                    "TextChanged");
                AssertObservableTargetRoute(
                    typeof(RichTextBox),
                    "Rtf",
                    "Rtf",
                    "TextChanged");
                AssertObservableTargetRoute(
                    typeof(DataGridView),
                    "FirstDisplayedCell",
                    "FirstDisplayedCell",
                    "Scroll");
                AssertObservableTargetRoute(
                    typeof(DataGridView),
                    "FirstDisplayedScrollingColumnIndex",
                    "FirstDisplayedScrollingColumnIndex",
                    "Scroll");
                AssertObservableTargetRoute(
                    typeof(DataGridView),
                    "FirstDisplayedScrollingRowIndex",
                    "FirstDisplayedScrollingRowIndex",
                    "Scroll");
                AssertObservableTargetRoute(
                    typeof(DataGridView),
                    "HorizontalScrollingOffset",
                    "HorizontalScrollingOffset",
                    "Scroll");
                AssertObservableTargetRoute(
                    typeof(WebBrowser),
                    "Source",
                    "Url",
                    "Navigated");
                AssertObservableTargetRoute(
                    typeof(RichTextBox),
                    "IsReadOnly",
                    "ReadOnly",
                    null);

                CreateHandleAndDrainReactiveCallbacks(runtime.RootControl);
                grid.PrepareScrollableContent();

                geometry.Size = new Size(180, 60);
                geometry.Location = new Point(17, 19);
                linesEditor.Text = "Alpha\r\nBeta\r\nGamma";
                richEditor.Text = "Changed rich text";
                richEditor.ReadOnly = true;
                grid.SimulateHorizontalScroll(25);
                DrainReactiveCallbacks(runtime.RootControl);

                AssertEqual(180, state.Width.Value, "SizeChanged reports Width");
                AssertEqual(60, state.Height.Value, "SizeChanged reports Height");
                AssertEqual(17, state.Left.Value, "LocationChanged reports Left");
                AssertEqual(19, state.Top.Value, "LocationChanged reports Top");
                AssertEqual(3, state.Lines.Value.Length, "TextChanged reports Lines");
                AssertEqual("Alpha", state.Lines.Value[0], "first edited line");
                AssertEqual("Gamma", state.Lines.Value[2], "last edited line");
                AssertEqual(
                    richEditor.Rtf,
                    state.Rtf.Value,
                    "TextChanged reports current RichTextBox.Rtf");
                AssertEqual(
                    true,
                    state.RichReadOnly.Value,
                    "ReadOnlyChanged reports IsReadOnly alias");
                AssertEqual(
                    25,
                    state.HorizontalOffset.Value,
                    "Scroll reports HorizontalScrollingOffset");

                state.Width.Value = 200;
                state.Height.Value = 70;
                state.Left.Value = 23;
                state.Top.Value = 29;
                state.Lines.Value = new string[] { "Source", "Lines" };
                state.Rtf.Value = "{\\rtf1\\ansi Source rich text}";
                state.RichReadOnly.Value = false;
                state.HorizontalOffset.Value = 10;
                DrainReactiveCallbacks(runtime.RootControl);

                AssertEqual(200, geometry.Width, "source updates Width");
                AssertEqual(70, geometry.Height, "source updates Height");
                AssertEqual(23, geometry.Left, "source updates Left");
                AssertEqual(29, geometry.Top, "source updates Top");
                AssertEqual(2, linesEditor.Lines.Length, "source updates Lines");
                AssertEqual("Source", linesEditor.Lines[0], "source first line");
                AssertEqual(
                    "Source rich text",
                    richEditor.Text,
                    "source updates Rtf");
                AssertEqual(false, richEditor.ReadOnly, "source updates IsReadOnly");
                AssertEqual(
                    10,
                    grid.HorizontalScrollingOffset,
                    "source updates HorizontalScrollingOffset");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void AssertObservableTargetRoute(
            Type targetType,
            string requestedName,
            string expectedResolvedName,
            string expectedAlternateEvent)
        {
            MethodInfo resolve = typeof(XamlRuntime).GetMethod(
                "ResolveObservableTargetProperty",
                BindingFlags.Static | BindingFlags.NonPublic);
            AssertTrue(resolve != null, "observable target resolver found");
            object route = resolve.Invoke(
                null,
                new object[] { targetType, requestedName });
            AssertEqual(
                expectedResolvedName,
                GetInstanceField(route, "ResolvedName"),
                requestedName + " resolved target property");
            System.ComponentModel.EventDescriptor alternate =
                GetInstanceField(route, "AlternateChangedEvent") as
                    System.ComponentModel.EventDescriptor;

            if (expectedAlternateEvent == null)
            {
                AssertEqual(
                    null,
                    alternate,
                    requestedName + " uses its conventional change event");
            }
            else
            {
                AssertTrue(
                    alternate != null,
                    requestedName + " alternate change event found");
                AssertEqual(
                    expectedAlternateEvent,
                    alternate.Name,
                    requestedName + " alternate change event");
            }
        }

        private static void TestReactiveSharedTargets()
        {
            ReactiveBindingState state = new ReactiveBindingState();
            XamlRuntime runtime = XamlRuntime.Load(
                "<Panel>" +
                "  <Label Name='Display' Text='{Binding Text}' />" +
                "  <TextBox Name='FirstEditor' " +
                "      Text='{Binding Text, Mode=TwoWay}' />" +
                "  <TextBox Name='SecondEditor' " +
                "      Text='{Binding Text, Mode=TwoWay}' />" +
                "</Panel>",
                state);

            try
            {
                Label display = runtime.Get<Label>("Display");
                TextBox first = runtime.Get<TextBox>("FirstEditor");
                TextBox second = runtime.Get<TextBox>("SecondEditor");

                AssertEqual(
                    1,
                    GetPropertyBindingSubscriberCount(state.Text),
                    "one shared source event subscription");
                CreateHandleAndDrainReactiveCallbacks(runtime.RootControl);

                state.Text.Value = "Shared source";
                DrainReactiveCallbacks(runtime.RootControl);
                AssertEqual("Shared source", display.Text, "shared label source update");
                AssertEqual("Shared source", first.Text, "first shared source update");
                AssertEqual("Shared source", second.Text, "second shared source update");

                first.Text = "First edit";
                DrainReactiveCallbacks(runtime.RootControl);
                AssertEqual("First edit", state.Text.Value, "first target source update");
                AssertEqual("First edit", display.Text, "first target updates label");
                AssertEqual("First edit", second.Text, "first target updates sibling");

                second.Text = "Second edit";
                DrainReactiveCallbacks(runtime.RootControl);
                AssertEqual("Second edit", state.Text.Value, "second target source update");
                AssertEqual("Second edit", display.Text, "second target updates label");
                AssertEqual("Second edit", first.Text, "second target updates sibling");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestReactiveTwoWayEndpointReplacementPreservesTargetEdit()
        {
            ReactiveBindingState state = new ReactiveBindingState();
            ReactiveChildState original = state.Nested.Value;
            XamlRuntime runtime = XamlRuntime.Load(
                "<TextBox Name='Editor' " +
                "Text='{Binding Nested.Text, Mode=TwoWay}' />",
                state);

            try
            {
                TextBox editor = runtime.Get<TextBox>("Editor");
                ReactiveChildState replacement =
                    new ReactiveChildState("Replacement endpoint");

                CreateHandleAndDrainReactiveCallbacks(runtime.RootControl);

                state.Nested.Value = replacement;
                editor.Text = "Target edit before dispatch";
                DrainReactiveCallbacks(runtime.RootControl);

                AssertEqual(
                    "Nested initial",
                    original.Text.Value,
                    "superseded ordinary endpoint is not edited");
                AssertEqual(
                    "Target edit before dispatch",
                    replacement.Text.Value,
                    "pending target edit writes replacement ordinary endpoint");
                AssertEqual(
                    "Target edit before dispatch",
                    editor.Text,
                    "ordinary target is republished after endpoint replacement");
                AssertEqual(
                    0,
                    GetPropertyBindingSubscriberCount(original.Text),
                    "superseded ordinary endpoint detaches");
                AssertEqual(
                    1,
                    GetPropertyBindingSubscriberCount(replacement.Text),
                    "replacement ordinary endpoint subscribes once");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestReactiveSharedNewestEqualEditWins()
        {
            ReactiveBindingState state = new ReactiveBindingState();
            XamlRuntime runtime = XamlRuntime.Load(
                "<Panel>" +
                "  <TextBox Name='FirstEditor' " +
                "      Text='{Binding Text, Mode=TwoWay}' />" +
                "  <TextBox Name='SecondEditor' " +
                "      Text='{Binding Text, Mode=TwoWay}' />" +
                "</Panel>",
                state);
            EventHandler sourceHandler = null;

            try
            {
                TextBox first = runtime.Get<TextBox>("FirstEditor");
                TextBox second = runtime.Get<TextBox>("SecondEditor");
                int sourceChangeCount = 0;

                sourceHandler =
                    delegate(object sender, EventArgs e)
                    {
                        sourceChangeCount++;
                    };
                state.Text.ValueChanged += sourceHandler;

                CreateHandleAndDrainReactiveCallbacks(runtime.RootControl);

                first.Text = "Older different edit";
                second.Text = "Temporary second edit";
                second.Text = "Initial";
                AssertEqual(
                    "Initial",
                    state.Text.Value,
                    "newest shared target edit commits immediately");
                AssertEqual(
                    3,
                    sourceChangeCount,
                    "each distinct owner-thread edit publishes immediately");
                DrainReactiveCallbacks(runtime.RootControl);

                AssertEqual(
                    "Initial",
                    state.Text.Value,
                    "newest equal edit wins shared source arbitration");
                AssertEqual(
                    3,
                    sourceChangeCount,
                    "queued sibling refresh adds no source notification");
                AssertEqual(
                    "Initial",
                    first.Text,
                    "older target returns to winning source value");
                AssertEqual(
                    "Initial",
                    second.Text,
                    "newest equal target remains current");
            }
            finally
            {
                if (sourceHandler != null)
                    state.Text.ValueChanged -= sourceHandler;

                runtime.Dispose();
            }
        }

        private static void TestReactiveFeedbackKeepsLatestValue()
        {
            ReactiveBindingState state = new ReactiveBindingState();
            XamlRuntime runtime = XamlRuntime.Load(
                "<TextBox Name='Editor' " +
                "Text='{Binding Text, Mode=TwoWay}' />",
                state);
            EventHandler sourceHandler = null;

            try
            {
                TextBox editor = runtime.Get<TextBox>("Editor");
                int targetChangeCount = 0;
                bool rewroteTargetValue = false;

                CreateHandleAndDrainReactiveCallbacks(runtime.RootControl);

                editor.TextChanged +=
                    delegate(object sender, EventArgs e)
                    {
                        targetChangeCount++;
                    };

                sourceHandler =
                    delegate(object sender, EventArgs e)
                    {
                        if (!rewroteTargetValue &&
                            state.Text.Value == "Target edit")
                        {
                            rewroteTargetValue = true;
                            state.Text.Value = "Reentrant latest";
                        }
                    };
                state.Text.ValueChanged += sourceHandler;

                state.Text.Value = "Source edit";
                DrainReactiveCallbacks(runtime.RootControl);
                AssertEqual("Source edit", editor.Text, "one-way leg of feedback test");
                AssertEqual(1, targetChangeCount, "source update changes target once");

                editor.Text = "Target edit";
                DrainReactiveCallbacks(runtime.RootControl);

                AssertTrue(rewroteTargetValue, "reentrant source callback ran");
                AssertEqual(
                    "Reentrant latest",
                    state.Text.Value,
                    "reentrant source keeps latest value");
                AssertEqual(
                    "Reentrant latest",
                    editor.Text,
                    "reentrant latest value returns to target");
                AssertEqual(
                    3,
                    targetChangeCount,
                    "feedback suppression prevents extra target changes");

                state.Text.Value = "Reentrant latest";
                DrainReactiveCallbacks(runtime.RootControl);
                AssertEqual(
                    3,
                    targetChangeCount,
                    "equal source assignment remains quiet");
            }
            finally
            {
                if (sourceHandler != null)
                    state.Text.ValueChanged -= sourceHandler;

                runtime.Dispose();
            }
        }

        private static void TestReactiveSourceTargetRewriteReturnsToSource()
        {
            ReactiveBindingState state = new ReactiveBindingState();
            XamlRuntime runtime = XamlRuntime.Load(
                "<TextBox Name='Editor' " +
                "Text='{Binding Text, Mode=TwoWay}' />",
                state);
            EventHandler targetHandler = null;

            try
            {
                TextBox editor = runtime.Get<TextBox>("Editor");
                bool rewroteTarget = false;

                CreateHandleAndDrainReactiveCallbacks(runtime.RootControl);

                targetHandler =
                    delegate(object sender, EventArgs e)
                    {
                        if (!rewroteTarget &&
                            String.Equals(
                                editor.Text,
                                "Source-driven value",
                                StringComparison.Ordinal))
                        {
                            rewroteTarget = true;
                            editor.Text = "Synchronous target rewrite";
                        }
                    };
                editor.TextChanged += targetHandler;

                state.Text.Value = "Source-driven value";
                DrainReactiveCallbacks(runtime.RootControl);

                AssertTrue(
                    rewroteTarget,
                    "TextChanged handler synchronously rewrote source update");
                AssertEqual(
                    "Synchronous target rewrite",
                    editor.Text,
                    "rewritten target value remains visible");
                AssertEqual(
                    "Synchronous target rewrite",
                    state.Text.Value,
                    "rewritten target value returns to PropertyBinding");
            }
            finally
            {
                if (targetHandler != null)
                {
                    TextBox editor = runtime.Get<TextBox>("Editor");
                    editor.TextChanged -= targetHandler;
                }

                runtime.Dispose();
            }
        }

        private static void TestReactiveConditionPreservesVisibilityState()
        {
            ReactiveBindingState state = new ReactiveBindingState();
            state.Condition.Value = false;

            XamlRuntime runtime = XamlRuntime.Load(
                "<Panel>" +
                "  <Label Name='CollapsedTarget' Visibility='Collapsed' " +
                "      Condition='{Binding Condition}' />" +
                "  <Label Name='HiddenTarget' Visibility='Hidden' " +
                "      Condition='{Binding Condition}' />" +
                "</Panel>",
                state);

            try
            {
                Label collapsed =
                    runtime.Get<Label>("CollapsedTarget");
                Label hidden =
                    runtime.Get<Label>("HiddenTarget");

                CreateHandleAndDrainReactiveCallbacks(runtime.RootControl);

                AssertTrue(!collapsed.Visible, "false Condition keeps collapsed target hidden");
                AssertTrue(!hidden.Visible, "false Condition keeps hidden target hidden");
                AssertEqual(
                    true,
                    GetElementInfoField(
                        runtime,
                        collapsed,
                        "VisibilityCollapsed"),
                    "collapsed visibility layer retained initially");
                AssertEqual(
                    true,
                    GetElementInfoField(runtime, hidden, "Hidden"),
                    "hidden visibility layer retained initially");

                state.Condition.Value = true;
                DrainReactiveCallbacks(runtime.RootControl);

                AssertTrue(
                    !collapsed.Visible,
                    "true Condition does not resurrect collapsed target");
                AssertTrue(
                    !hidden.Visible,
                    "true Condition does not resurrect hidden target");
                AssertEqual(
                    true,
                    GetElementInfoField(runtime, collapsed, "Collapsed"),
                    "collapsed target remains layout-collapsed");
                AssertEqual(
                    false,
                    GetElementInfoField(runtime, hidden, "Collapsed"),
                    "hidden target clears only its Condition collapse layer");
                AssertEqual(
                    true,
                    GetElementInfoField(runtime, hidden, "Hidden"),
                    "hidden target retains its visibility layer");

                state.Condition.Value = false;
                DrainReactiveCallbacks(runtime.RootControl);
                state.Condition.Value = true;
                DrainReactiveCallbacks(runtime.RootControl);

                AssertTrue(
                    !collapsed.Visible,
                    "repeated Condition toggles preserve collapsed state");
                AssertTrue(
                    !hidden.Visible,
                    "repeated Condition toggles preserve hidden state");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestReactiveManualRebind()
        {
            ReactiveBindingState state = new ReactiveBindingState();
            PropertyBinding<string> original = state.Replaceable;
            XamlRuntime runtime = XamlRuntime.Load(
                "<TextBox Name='Caption' " +
                "Text='{Binding Replaceable, Mode=TwoWay}' />",
                state);

            try
            {
                TextBox caption = runtime.Get<TextBox>("Caption");
                CreateHandleAndDrainReactiveCallbacks(runtime.RootControl);
                AssertEqual(
                    "Replaceable initial",
                    caption.Text,
                    "initial replaceable wrapper");

                PropertyBinding<string> replacement =
                    new PropertyBinding<string>("Replacement initial");
                state.Replaceable = replacement;
                runtime.ReloadBinding("Caption", "Text");

                AssertEqual(
                    "Replacement initial",
                    caption.Text,
                    "manual reload resolves replacement wrapper");
                AssertEqual(
                    0,
                    GetPropertyBindingSubscriberCount(original),
                    "manual reload detaches original wrapper");
                AssertEqual(
                    1,
                    GetPropertyBindingSubscriberCount(replacement),
                    "manual reload subscribes replacement wrapper");

                original.Value = "Stale original";
                DrainReactiveCallbacks(runtime.RootControl);
                AssertEqual(
                    "Replacement initial",
                    caption.Text,
                    "detached original wrapper is ignored");

                replacement.Value = "Replacement update";
                DrainReactiveCallbacks(runtime.RootControl);
                AssertEqual(
                    "Replacement update",
                    caption.Text,
                    "replacement wrapper updates automatically");

                caption.Text = "Replacement target edit";
                DrainReactiveCallbacks(runtime.RootControl);
                AssertEqual(
                    "Replacement target edit",
                    replacement.Value,
                    "two-way target writes replacement wrapper");
                AssertEqual(
                    "Stale original",
                    original.Value,
                    "two-way target does not write original wrapper");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestReactiveTargetCommitPrecedesManualReload()
        {
            ReactiveBindingState state = new ReactiveBindingState();
            XamlRuntime runtime = XamlRuntime.Load(
                "<TextBox Name='Editor' " +
                "Text='{Binding Text, Mode=TwoWay}' />",
                state);
            EventHandler sourceHandler = null;

            try
            {
                TextBox editor = runtime.Get<TextBox>("Editor");
                int sourceChangeCount = 0;

                sourceHandler =
                    delegate(object sender, EventArgs e)
                    {
                        sourceChangeCount++;
                    };
                state.Text.ValueChanged += sourceHandler;

                CreateHandleAndDrainReactiveCallbacks(runtime.RootControl);

                editor.Text = "Queued target edit";
                AssertEqual(
                    "Queued target edit",
                    state.Text.Value,
                    "owner-thread target edit commits synchronously");
                AssertEqual(
                    1,
                    sourceChangeCount,
                    "synchronous target commit notifies the source once");

                state.Text.Value = "Manual source value";
                runtime.ReloadBinding("Editor", "Text");

                AssertEqual(
                    "Manual source value",
                    state.Text.Value,
                    "manual reload keeps current source value");
                AssertEqual(
                    "Manual source value",
                    editor.Text,
                    "manual reload synchronously applies the source value");

                DrainReactiveCallbacks(runtime.RootControl);

                AssertEqual(
                    "Manual source value",
                    state.Text.Value,
                    "completed target commit cannot overwrite a later source edit");
                AssertEqual(
                    "Manual source value",
                    editor.Text,
                    "source and target remain aligned after dispatch");
                AssertEqual(
                    2,
                    sourceChangeCount,
                    "manual source edit is the only later notification");
            }
            finally
            {
                if (sourceHandler != null)
                    state.Text.ValueChanged -= sourceHandler;

                runtime.Dispose();
            }
        }

        private static void TestReactiveRebindDistinguishesEqualSourceVersions()
        {
            ReactiveChildState endpointA =
                new ReactiveChildState("Endpoint A initial");
            ReactiveChildState endpointB =
                new ReactiveChildState("Endpoint B initial");
            ReentrantEndpointState state =
                new ReentrantEndpointState(endpointA);
            XamlRuntime runtime = XamlRuntime.Load(
                "<TextBox Name='Editor' " +
                "Text='{Binding Endpoint.Text, Mode=TwoWay}' />",
                state);
            EventHandler endpointAHandler = null;

            try
            {
                TextBox editor = runtime.Get<TextBox>("Editor");
                bool rebound = false;

                CreateHandleAndDrainReactiveCallbacks(runtime.RootControl);
                AssertEqual(
                    0L,
                    GetInstanceField(endpointA.Text, "_version"),
                    "endpoint A starts at version zero");
                AssertEqual(
                    0L,
                    GetInstanceField(endpointB.Text, "_version"),
                    "endpoint B starts at version zero");

                endpointAHandler =
                    delegate(object sender, EventArgs e)
                    {
                        if (rebound)
                            return;

                        rebound = true;
                        state.Endpoint = endpointB;
                        runtime.ReloadBinding("Editor", "Text");
                        endpointB.Text.Value = "Endpoint B current";
                    };
                endpointA.Text.ValueChanged += endpointAHandler;

                editor.Text = "Target write to endpoint A";
                DrainReactiveCallbacks(runtime.RootControl);

                AssertTrue(
                    rebound,
                    "endpoint A handler rebound the composite path");
                AssertEqual(
                    "Target write to endpoint A",
                    endpointA.Text.Value,
                    "target write commits to original endpoint A");
                AssertEqual(
                    "Endpoint B current",
                    endpointB.Text.Value,
                    "replacement endpoint B publishes its current value");
                AssertEqual(
                    1L,
                    GetInstanceField(endpointA.Text, "_version"),
                    "endpoint A write reaches expected version one");
                AssertEqual(
                    1L,
                    GetInstanceField(endpointB.Text, "_version"),
                    "endpoint B independently reaches matching version one");
                AssertEqual(
                    "Endpoint B current",
                    editor.Text,
                    "matching B version is not suppressed as A self-notification");

                endpointA.Text.ValueChanged -= endpointAHandler;
                endpointAHandler = null;

                AssertEqual(
                    0,
                    GetPropertyBindingSubscriberCount(endpointA.Text),
                    "obsolete endpoint A is fully detached");
                AssertEqual(
                    1,
                    GetPropertyBindingSubscriberCount(endpointB.Text),
                    "replacement endpoint B remains subscribed once");
            }
            finally
            {
                if (endpointAHandler != null)
                    endpointA.Text.ValueChanged -= endpointAHandler;

                runtime.Dispose();
            }
        }

        private static void TestReactiveParserValidation()
        {
            ReactiveBindingState state = new ReactiveBindingState();

            ExpectInvalidMarkup(
                "<Label Text='{Binding Text, Mode=Sideways}' />",
                state);
            ExpectInvalidMarkup(
                "<TextBox Text='{Binding Text, Mode=TwoWay, " +
                "UpdateSourceTrigger=Sometimes}' />",
                state);
            ExpectInvalidMarkup(
                "<TextBox Text='{Binding Text, " +
                "UpdateSourceTrigger=LostFocus}' />",
                state);
            ExpectInvalidMarkup(
                "<TextBox Text='{Binding Text, Mode=TwoWay, " +
                "UpdateSourceTrigger=LostFocus, " +
                "UpdateSourceTrigger=Explicit}' />",
                state);
            ExpectInvalidMarkup(
                "<Label Text='{Binding Text, Mode=OneWay, Mode=TwoWay}' />",
                state);
            ExpectInvalidMarkup(
                "<Label Text='{Binding Text, Unknown=Value}' />",
                state);
            ExpectInvalidMarkup(
                "<Label Text='{Binding Text,, Mode=TwoWay}' />",
                state);
            ExpectInvalidMarkup(
                "<Label Text='Prefix {Binding Text, Mode=TwoWay}' />",
                state);
            ExpectInvalidMarkup(
                "<Label Text='{Binding Nested..Text}' />",
                state);
            ExpectInvalidMarkup(
                "<Label Text='{Binding .Text}' />",
                state);
            ExpectInvalidMarkup(
                "<Label Text='{Binding Text.}' />",
                state);
            ExpectInvalidMarkup(
                "<Label Text='{Binding ...}' />",
                state);

            XamlRuntime wholeContext = XamlRuntime.Load(
                "<Label Name='Whole' Tag='{Binding .}' />",
                state);

            try
            {
                AssertSame(
                    state,
                    wholeContext.Get<Label>("Whole").Tag,
                    "single-dot whole-context binding remains valid");
            }
            finally
            {
                wholeContext.Dispose();
            }
        }

        private static void TestDynamicElementNameValidation()
        {
            ReactiveBindingState state = new ReactiveBindingState();
            const string expectedMessage =
                "Name/x:Name defines element identity and cannot be dynamic";

            ExpectInvalidMarkupMessage(
                "<Label Name='{Binding Text}' />",
                state,
                expectedMessage);
            ExpectInvalidMarkupMessage(
                "<Label xmlns:x='urn:winformsxaml-test' " +
                "x:Name='{Binding Text}' />",
                state,
                expectedMessage);

            AssertEqual(
                0,
                GetPropertyBindingSubscriberCount(state.Text),
                "invalid dynamic names leave no observable subscription");
        }

        private static void TestReactiveTargetValidation()
        {
            ReactiveBindingState state = new ReactiveBindingState();

            ExpectInvalidMarkup(
                "<CheckBox Checked='{Binding !Checked, Mode=TwoWay}' />",
                state);
            ExpectInvalidMarkup(
                "<Panel>" +
                "  <Panel.Resources>" +
                "    <Style Key='InvalidStyle' TargetType='Label'>" +
                "      <Setter Property='Text' " +
                "          Value='{Binding Text, Mode=TwoWay}' />" +
                "    </Style>" +
                "  </Panel.Resources>" +
                "  <Label Style='InvalidStyle' />" +
                "</Panel>",
                state);
            ExpectInvalidMarkup(
                "<Grid><Label Grid.Row='{Binding Row, Mode=TwoWay}' /></Grid>",
                state);
            ExpectInvalidMarkup(
                "<Label Condition='{Binding Condition, Mode=TwoWay}' />",
                state);
            ExpectInvalidMarkup(
                "<TextBox Text='{Binding PlainText, Mode=TwoWay}' />",
                state);
            ExpectInvalidMarkup(
                "<DateTimePicker ShowCheckBox='true' " +
                "Checked='{Binding Checked, Mode=TwoWay}' />",
                state);

            XamlRuntime.Register(
                "ReactiveReadOnlyControl",
                typeof(ReactiveReadOnlyControl));
            XamlRuntime.Register(
                "ReactiveNoChangeControl",
                typeof(ReactiveNoChangeControl));

            ExpectInvalidMarkup(
                "<ReactiveReadOnlyControl " +
                "ReadOnlyValue='{Binding Text, Mode=TwoWay}' />",
                state);
            ExpectInvalidMarkup(
                "<ReactiveNoChangeControl " +
                "QuietValue='{Binding Text, Mode=TwoWay}' />",
                state);

            AssertEqual(
                0,
                GetPropertyBindingSubscriberCount(state.Text),
                "failed target validation leaves no Text subscription");
            AssertEqual(
                0,
                GetPropertyBindingSubscriberCount(state.Checked),
                "failed negation validation leaves no subscription");
            AssertEqual(
                0,
                GetPropertyBindingSubscriberCount(state.Row),
                "failed attached validation leaves no subscription");
            AssertEqual(
                0,
                GetPropertyBindingSubscriberCount(state.Condition),
                "failed Condition validation leaves no subscription");
        }

        private static void TestReactiveComponentInvocationTwoWay()
        {
            XamlRuntime.Register(
                Assembly.GetExecutingAssembly(),
                "WinFormsXaml.Tests.Fixtures.EditableCard.xml");

            ReactiveBindingState state = new ReactiveBindingState();
            PropertyBinding<string> original = state.Replaceable;
            XamlRuntime runtime = XamlRuntime.Load(
                "<Panel>" +
                "  <EditableCard Name='Editable' " +
                "      Value='{Binding Replaceable, Mode=TwoWay, Source=CodeBehind}' />" +
                "</Panel>",
                state);

            try
            {
                Panel card = runtime.Get<Panel>("Editable");
                TextBox editor = card.Controls["Editor"] as TextBox;
                NumericUpDown countEditor =
                    card.Controls["CountEditor"] as NumericUpDown;
                Label countLabel =
                    card.Controls["CountLabel"] as Label;

                AssertTrue(editor != null, "component two-way text editor");
                AssertTrue(
                    countEditor != null,
                    "component typed default editor");
                AssertTrue(
                    countLabel != null,
                    "component typed default observer");

                CreateHandleAndDrainReactiveCallbacks(
                    runtime.RootControl);
                AssertEqual(
                    "Replaceable initial",
                    editor.Text,
                    "component receives initial parent value");
                AssertEqual(
                    (decimal)7,
                    countEditor.Value,
                    "component preserves typed numeric default");
                AssertEqual(
                    "7",
                    countLabel.Text,
                    "component exposes typed default to inner bindings");
                AssertEqual(
                    1,
                    GetPropertyBindingSubscriberCount(original),
                    "component subscribes to original parent terminal once");

                ArrayList componentInstances =
                    GetInstanceField(
                        runtime,
                        "_componentInstances") as ArrayList;
                AssertTrue(
                    componentInstances != null &&
                    componentInstances.Count == 1,
                    "editable component instance is retained");
                object componentState = componentInstances[0];
                IDictionary componentValues =
                    GetInstanceField(
                        componentState,
                        "Values") as IDictionary;
                AssertTrue(
                    componentValues != null,
                    "editable component value context");
                object valueProxy = componentValues["Value"];
                AssertEqual(
                    2,
                    GetPropertyBindingSubscriberCount(valueProxy),
                    "component proxy has inner and outer subscribers");

                editor.Text = "Edited in component";
                DrainReactiveCallbacks(runtime.RootControl);
                AssertEqual(
                    "Edited in component",
                    original.Value,
                    "component edit reaches parent terminal");

                original.Value = "Changed by parent";
                DrainReactiveCallbacks(runtime.RootControl);
                AssertEqual(
                    "Changed by parent",
                    editor.Text,
                    "parent terminal change reaches component editor");

                countEditor.Value = 12m;
                DrainReactiveCallbacks(runtime.RootControl);
                AssertEqual(
                    "12",
                    countLabel.Text,
                    "inner two-way binding updates typed local default");

                PropertyBinding<string> replacement =
                    new PropertyBinding<string>("Replacement terminal");
                state.Replaceable = replacement;
                runtime.ReloadBinding("Editable", "Value");
                DrainReactiveCallbacks(runtime.RootControl);

                AssertEqual(
                    0,
                    GetPropertyBindingSubscriberCount(original),
                    "component detaches replaced parent terminal");
                AssertEqual(
                    1,
                    GetPropertyBindingSubscriberCount(replacement),
                    "component subscribes to replacement parent terminal");
                AssertEqual(
                    "Replacement terminal",
                    editor.Text,
                    "component refreshes from replacement terminal");

                editor.Text = "Edited after replacement";
                DrainReactiveCallbacks(runtime.RootControl);
                AssertEqual(
                    "Edited after replacement",
                    replacement.Value,
                    "component edit reaches replacement terminal");
                AssertEqual(
                    "Changed by parent",
                    original.Value,
                    "component edit does not reach detached terminal");

                card.Dispose();
                AssertEqual(
                    0,
                    GetPropertyBindingSubscriberCount(replacement),
                    "component root disposal detaches two-way parent terminal");
                AssertEqual(
                    0,
                    GetPropertyBindingSubscriberCount(valueProxy),
                    "component root disposal detaches both proxy directions");
            }
            finally
            {
                runtime.Dispose();
            }

            ExpectInvalidMarkup(
                "<EditableCard Value='{Binding PlainText, Mode=TwoWay}' />",
                state);
            AssertEqual(
                0,
                GetPropertyBindingSubscriberCount(original),
                "invalid component invocation leaves original source detached");
        }

        private static void TestItemComponentInvocationUsesCodeBehindSource()
        {
            XamlRuntime.Register(
                Assembly.GetExecutingAssembly(),
                "WinFormsXaml.Tests.Fixtures.EditableCard.xml");

            ReactiveBindingState state = new ReactiveBindingState();
            PropertyBinding<string> source = state.Replaceable;
            XamlRuntime runtime = XamlRuntime.Load(
                "<ItemsControl Name='Rows' Virtualizing='false' " +
                "    ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <EditableCard Value='{Binding Replaceable, Source=CodeBehind}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>",
                state);

            try
            {
                XamlRuntime.ItemsControl rows =
                    runtime.GetItemsControl("Rows");
                ArrayList items = new ArrayList();
                items.Add(new object());
                rows.SetItems(items);
                CreateHandleAndDrainReactiveCallbacks(runtime.RootControl);

                Panel card = null;
                int i;

                for (i = 0; i < rows.Controls.Count; i++)
                {
                    card = rows.Controls[i] as Panel;

                    if (card != null)
                        break;
                }

                AssertTrue(card != null, "item component root is realized");
                TextBox editor = card.Controls["Editor"] as TextBox;
                AssertTrue(editor != null, "item component editor is realized");
                AssertEqual(
                    "Replaceable initial",
                    editor.Text,
                    "component invocation bypasses the current item for code-behind state");

                source.Value = "Changed code-behind component value";
                DrainReactiveCallbacks(runtime.RootControl);
                card = null;

                for (i = 0; i < rows.Controls.Count; i++)
                {
                    card = rows.Controls[i] as Panel;

                    if (card != null)
                        break;
                }

                AssertTrue(card != null, "item component remains realized after refresh");
                editor = card.Controls["Editor"] as TextBox;
                AssertTrue(editor != null, "refreshed item component editor exists");
                AssertEqual(
                    "Changed code-behind component value",
                    editor.Text,
                    "code-behind source refreshes an item component invocation");
            }
            finally
            {
                runtime.Dispose();
            }

            AssertEqual(
                0,
                GetPropertyBindingSubscriberCount(source),
                "disposing an item component detaches its code-behind source");
        }

        private static void TestReactiveBindingDisposal()
        {
            ReactiveBindingState targetState = new ReactiveBindingState();
            XamlRuntime targetRuntime = XamlRuntime.Load(
                "<Panel><Label Name='Target' Text='{Binding Text}' /></Panel>",
                targetState);

            try
            {
                Label target = targetRuntime.Get<Label>("Target");
                CreateHandleAndDrainReactiveCallbacks(targetRuntime.RootControl);
                AssertEqual(
                    1,
                    GetPropertyBindingSubscriberCount(targetState.Text),
                    "target binding subscription before disposal");

                target.Dispose();
                targetState.Text.Value = "Disposed target signal";
                DrainReactiveCallbacks(targetRuntime.RootControl);
                AssertEqual(
                    0,
                    GetPropertyBindingSubscriberCount(targetState.Text),
                    "disposed target detaches on queued source signal");
            }
            finally
            {
                targetRuntime.Dispose();
            }

            ReactiveBindingState runtimeState = new ReactiveBindingState();
            XamlRuntime runtime = XamlRuntime.Load(
                "<TextBox Name='Editor' " +
                "Text='{Binding Text, Mode=TwoWay}' />",
                runtimeState);

            try
            {
                TextBox editor = runtime.Get<TextBox>("Editor");

                CreateHandleAndDrainReactiveCallbacks(runtime.RootControl);
                AssertEqual(
                    1,
                    GetPropertyBindingSubscriberCount(runtimeState.Text),
                    "runtime subscription before disposal");

                runtime.Dispose();
                AssertEqual(
                    0,
                    GetPropertyBindingSubscriberCount(runtimeState.Text),
                    "runtime disposal detaches source subscription");

                editor.Text = "Detached target edit";
                AssertEqual(
                    "Initial",
                    runtimeState.Text.Value,
                    "runtime disposal detaches target subscription");

                runtimeState.Text.Value = "Detached source edit";
                AssertEqual(
                    "Detached target edit",
                    editor.Text,
                    "runtime disposal prevents source target update");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestReactiveNonRootDisposalDetachesBinding()
        {
            ReactiveBindingState state = new ReactiveBindingState();
            XamlRuntime runtime = XamlRuntime.Load(
                "<ToolStrip Name='Strip'>" +
                "  <ToolStripButton Name='DisposableAction' " +
                "      Text='{Binding Text}' />" +
                "</ToolStrip>",
                state);

            try
            {
                ToolStripButton action =
                    runtime.Get<ToolStripButton>("DisposableAction");
                CreateHandleAndDrainReactiveCallbacks(runtime.RootControl);

                AssertEqual(
                    1,
                    GetPropertyBindingSubscriberCount(state.Text),
                    "logical child binding subscription before disposal");

                IDictionary disposalHooks =
                    GetInstanceField(
                        runtime,
                        "_dynamicTargetDisposalHooks") as IDictionary;
                AssertTrue(
                    disposalHooks != null &&
                    disposalHooks.Contains(action),
                    "logical child has a retained disposal hook");

                action.Dispose();

                AssertEqual(
                    0,
                    GetPropertyBindingSubscriberCount(state.Text),
                    "external logical-child disposal detaches source subscription");
                AssertTrue(
                    !disposalHooks.Contains(action),
                    "external logical-child disposal releases disposal hook");

                ArrayList dynamicBindings =
                    GetInstanceField(
                        runtime,
                        "_dynamicPropertyBindings") as ArrayList;
                AssertTrue(
                    dynamicBindings != null &&
                    dynamicBindings.Count == 0,
                    "external logical-child disposal releases retained binding");
                AssertTrue(
                    !runtime.IsDisposed,
                    "logical-child disposal leaves runtime active");

                state.Text.Value = "Signal after logical-child disposal";
                DrainReactiveCallbacks(runtime.RootControl);
                AssertEqual(
                    0,
                    GetPropertyBindingSubscriberCount(state.Text),
                    "disposed logical child stays detached after source signal");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestReactivePreHandleAndWorkerDispatch()
        {
            ReactiveBindingState state = new ReactiveBindingState();
            XamlRuntime runtime = XamlRuntime.Load(
                "<Label Name='Caption' Text='{Binding Text}' />",
                state);
            EventHandler workerEventHandler = null;

            try
            {
                Label caption = runtime.Get<Label>("Caption");
                AssertTrue(
                    !runtime.RootControl.IsHandleCreated,
                    "reactive pre-handle test starts without a handle");

                state.Text.Value = "Pending first";
                state.Text.Value = "Pending latest";
                AssertEqual(
                    "Initial",
                    caption.Text,
                    "pre-handle source changes remain pending");

                runtime.RootControl.CreateControl();
                DrainReactiveCallbacks(runtime.RootControl);
                AssertEqual(
                    "Pending latest",
                    caption.Text,
                    "pre-handle debt applies latest source value");

                int workerThreadId = 0;
                int eventThreadId = 0;
                Exception workerFailure = null;

                workerEventHandler =
                    delegate(object sender, EventArgs e)
                    {
                        if (state.Text.Value == "Worker update")
                        {
                            eventThreadId =
                                Thread.CurrentThread.ManagedThreadId;
                        }
                    };
                state.Text.ValueChanged += workerEventHandler;

                Thread worker =
                    new Thread(
                        delegate()
                        {
                            try
                            {
                                workerThreadId =
                                    Thread.CurrentThread.ManagedThreadId;
                                state.Text.Value = "Worker update";
                            }
                            catch (Exception ex)
                            {
                                workerFailure = ex;
                            }
                        });

                worker.Start();
                worker.Join();

                if (workerFailure != null)
                {
                    throw new InvalidOperationException(
                        "Worker PropertyBinding update failed.",
                        workerFailure);
                }

                AssertEqual(
                    workerThreadId,
                    eventThreadId,
                    "ValueChanged runs on the assigning worker thread");
                AssertEqual(
                    "Pending latest",
                    caption.Text,
                    "worker source change waits for UI dispatch");

                DrainReactiveCallbacks(runtime.RootControl);
                AssertEqual(
                    "Worker update",
                    caption.Text,
                    "worker source change marshals to owner thread");
            }
            finally
            {
                if (workerEventHandler != null)
                    state.Text.ValueChanged -= workerEventHandler;

                runtime.Dispose();
            }
        }

        private static void TestEmbeddedXmlLoading()
        {
            BindingState state = new BindingState();
            XamlRuntime runtime = XamlRuntime.LoadEmbedded(
                "WinFormsXaml.Tests.Fixtures.EmbeddedView.xml",
                state);

            try
            {
                Label label = runtime.Get<Label>("EmbeddedLabel");
                AssertEqual(
                    "Embedded resource",
                    label.Text,
                    "embedded XML label text");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestEmbeddedPresetUsesMarkupAssembly()
        {
            XamlRuntime runtime = XamlRuntime.LoadEmbedded(
                Assembly.GetExecutingAssembly(),
                "WinFormsXaml.Tests.Fixtures.EmbeddedPresetHost.xml",
                new object());

            try
            {
                Label label =
                    runtime.Get<Label>("EmbeddedPresetLabel");

                AssertEqual(
                    "Resolved from markup assembly",
                    label.Text,
                    "nested embedded preset resource assembly");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestEmbeddedXmlRelativeFileBase()
        {
            string presetPath = Path.Combine(
                Application.StartupPath,
                "WinFormsXaml.Tests.RelativePresets.xml");
            XamlRuntime runtime = null;

            File.WriteAllText(
                presetPath,
                "<Presets Name='RelativeValues' Selected='Default'>" +
                "  <Preset Name='Default'>" +
                "    <Set Key='Caption' Value='Resolved from startup path' />" +
                "  </Preset>" +
                "</Presets>");

            try
            {
                runtime = XamlRuntime.LoadEmbedded(
                    "WinFormsXaml.Tests.Fixtures.EmbeddedRelativeView.xml",
                    new BindingState());

                Label label =
                    runtime.Get<Label>("RelativePresetLabel");

                AssertEqual(
                    "Resolved from startup path",
                    label.Text,
                    "relative preset file");
            }
            finally
            {
                if (runtime != null)
                    runtime.Dispose();

                if (File.Exists(presetPath))
                    File.Delete(presetPath);
            }
        }

        private static void TestRegisteredXmlComponentReloads()
        {
            XamlRuntime.Register(
                Assembly.GetExecutingAssembly(),
                "WinFormsXaml.Tests.Fixtures.ReactiveCard.xml");
            XamlRuntime.Register(
                Assembly.GetExecutingAssembly(),
                "WinFormsXaml.Tests.Fixtures.ForwardingCard.xml");
            XamlRuntime.Register(
                Assembly.GetExecutingAssembly(),
                "WinFormsXaml.Tests.Fixtures.LogicalCard.xml");

            BindingState state = new BindingState();
            state.Text = "First";

            XamlRuntime runtime = XamlRuntime.Load(
                "<FlowLayoutPanel Name='Host'>" +
                "  <ReactiveCard Name='CardOne' Caption='{Binding Text}' />" +
                "  <ReactiveCard Name='CardTwo' Caption='{Binding Text}' />" +
                "  <ForwardingCard Name='Forwarded' OuterCaption='{Binding Text}' />" +
                "  <LogicalCard Name='Logical' Caption='{Binding Text}' />" +
                "  <Label Name='Outside' />" +
                "</FlowLayoutPanel>",
                state);

            try
            {
                Panel cardOne = runtime.Get<Panel>("CardOne");
                Panel cardTwo = runtime.Get<Panel>("CardTwo");
                Panel forwarded = runtime.Get<Panel>("Forwarded");
                ToolStrip logical = runtime.Get<ToolStrip>("Logical");
                Label firstLabel = cardOne.Controls[0] as Label;
                Label secondLabel = cardTwo.Controls[0] as Label;
                Label firstStyledLabel = cardOne.Controls[1] as Label;
                Label secondStyledLabel = cardTwo.Controls[1] as Label;
                Label forwardedLabel = forwarded.Controls[0] as Label;
                Label outside = runtime.Get<Label>("Outside");

                AssertTrue(firstLabel != null, "first component label");
                AssertTrue(secondLabel != null, "second component label");
                AssertTrue(firstStyledLabel != null, "first styled component label");
                AssertTrue(secondStyledLabel != null, "second styled component label");
                AssertTrue(forwardedLabel != null, "forwarded component label");
                AssertEqual(
                    "First",
                    logical.Items[0].Text,
                    "logical component child initial value");
                AssertEqual("First", firstLabel.Text, "initial first component value");
                AssertEqual("First", secondLabel.Text, "initial second component value");
                AssertEqual("First", firstStyledLabel.Text, "component style binding context");
                AssertEqual("First", forwardedLabel.Text, "nested component initial value");
                AssertEqual(String.Empty, outside.Text, "component implicit style does not leak");

                state.Text = "Unrelated";
                runtime.ReloadBinding("Host", "Text");
                AssertEqual(
                    "First",
                    firstLabel.Text,
                    "ancestor property reload does not cross component boundary");
                AssertEqual(
                    "First",
                    secondLabel.Text,
                    "ancestor property reload preserves sibling component");
                AssertEqual(
                    "First",
                    forwardedLabel.Text,
                    "ancestor property reload preserves nested component");

                state.Text = "Named";
                runtime.ReloadBindings("CardOne");
                AssertEqual("Named", firstLabel.Text, "named component reload");
                AssertEqual("Named", firstStyledLabel.Text, "named styled component reload");
                AssertEqual("First", secondLabel.Text, "named reload scope");
                AssertEqual("First", secondStyledLabel.Text, "named styled reload scope");
                AssertEqual("First", forwardedLabel.Text, "named nested reload scope");

                state.Text = "LogicalNamed";
                runtime.ReloadBindings("Logical");
                AssertEqual(
                    "LogicalNamed",
                    logical.Items[0].Text,
                    "logical component child named reload");

                state.Text = "Logical";
                runtime.ReloadBinding("Logical", "Caption");
                AssertEqual(
                    "Logical",
                    logical.Items[0].Text,
                    "logical component child property reload");

                state.Text = "Property";
                runtime.ReloadBinding("CardOne", "Caption");
                AssertEqual("Property", firstLabel.Text, "component property reload");
                AssertEqual("Property", firstStyledLabel.Text, "styled property reload");
                AssertEqual("First", secondLabel.Text, "property reload scope");

                firstLabel.Text = "Tampered";
                runtime.ReloadBinding("CardOne", "Caption");
                AssertEqual(
                    "Property",
                    firstLabel.Text,
                    "unchanged component property is explicitly reapplied");

                state.Text = "Forwarded";
                runtime.ReloadBinding("Forwarded", "OuterCaption");
                AssertEqual(
                    "Forwarded",
                    forwardedLabel.Text,
                    "property reload crosses a same-root component boundary");

                state.Text = "Global";
                runtime.ReloadBindings();
                AssertEqual("Global", firstLabel.Text, "global first component reload");
                AssertEqual("Global", secondLabel.Text, "global second component reload");
                AssertEqual("Global", firstStyledLabel.Text, "global styled component reload");
                AssertEqual("Global", secondStyledLabel.Text, "global second styled reload");
                AssertEqual("Global", forwardedLabel.Text, "global nested component reload");
                AssertEqual(
                    "Global",
                    logical.Items[0].Text,
                    "global logical component child reload");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestRegisteredXmlComponentContentSlot()
        {
            const string contentResource =
                "WinFormsXaml.Tests.Fixtures.ContentCard.xml";

            XamlRuntime.Register(
                Assembly.GetExecutingAssembly(),
                contentResource);

            ReactiveBindingState state = new ReactiveBindingState();
            XamlRuntime runtime = XamlRuntime.Load(
                "<ContentCard Name='Card' " +
                "Title='{Binding SecondaryText}'>" +
                "  <Panel Name='ProjectedPanel'>" +
                "    <TextBox Name='ProjectedEditor' " +
                "        Text='{Binding Text, Mode=TwoWay}' />" +
                "    <Label Name='ProjectedLabel' Text='{Binding Text}' />" +
                "  </Panel>" +
                "  <Button Name='ProjectedSecond' Text='Second' />" +
                "</ContentCard>",
                state);

            try
            {
                Panel card = runtime.Get<Panel>("Card");
                Label templateTitle = card.Controls[0] as Label;
                Panel projected =
                    runtime.Get<Panel>("ProjectedPanel");
                TextBox editor =
                    runtime.Get<TextBox>("ProjectedEditor");
                Label projectedLabel =
                    runtime.Get<Label>("ProjectedLabel");
                Button projectedSecond =
                    runtime.Get<Button>("ProjectedSecond");

                AssertTrue(
                    templateTitle != null,
                    "component template title exists");
                AssertSame(
                    projected,
                    card.Controls[1],
                    "first projected child occupies the children slot");
                AssertSame(
                    projectedSecond,
                    card.Controls[2],
                    "a children slot accepts multiple caller controls");
                AssertEqual(
                    "Secondary",
                    templateTitle.Text,
                    "component template uses component-property context");
                AssertEqual(
                    "Initial",
                    editor.Text,
                    "projected editor uses caller context");
                AssertEqual(
                    "Initial",
                    projectedLabel.Text,
                    "projected label uses caller context");

                CreateHandleAndDrainReactiveCallbacks(
                    runtime.RootControl);
                state.SecondaryText.Value = "Template changed";
                state.Text.Value = "Caller changed";
                DrainReactiveCallbacks(runtime.RootControl);

                AssertEqual(
                    "Template changed",
                    templateTitle.Text,
                    "component property remains reactive");
                AssertEqual(
                    "Caller changed",
                    editor.Text,
                    "projected two-way binding observes caller changes");
                AssertEqual(
                    "Caller changed",
                    projectedLabel.Text,
                    "projected one-way binding observes caller changes");

                editor.Text = "Edited in projected content";
                DrainReactiveCallbacks(runtime.RootControl);
                AssertEqual(
                    "Edited in projected content",
                    state.Text.Value,
                    "projected two-way binding writes to caller context");
            }
            finally
            {
                runtime.Dispose();
            }

            AssertEqual(
                0,
                GetPropertyBindingSubscriberCount(state.Text),
                "projected caller bindings detach on dispose");
            AssertEqual(
                0,
                GetPropertyBindingSubscriberCount(state.SecondaryText),
                "component property binding detaches on dispose");

            runtime = XamlRuntime.Load(
                "<ContentCard Name='EmptyCard' />",
                state);

            try
            {
                AssertEqual(
                    1,
                    runtime.Get<Panel>("EmptyCard").Controls.Count,
                    "an unused content slot is removed");
            }
            finally
            {
                runtime.Dispose();
            }

            ReactiveChildState item =
                new ReactiveChildState("Item initial");
            runtime = XamlRuntime.Load(
                "<ItemsControl Name='Rows' Virtualizing='false' " +
                "    ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <ContentCard Title='Item card'>" +
                "      <Panel>" +
                "        <TextBox Text='{Binding Text, Mode=TwoWay}' />" +
                "      </Panel>" +
                "    </ContentCard>" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>");

            try
            {
                XamlRuntime.ItemsControl rows =
                    runtime.GetItemsControl("Rows");
                ArrayList items = new ArrayList();
                items.Add(item);
                rows.SetItems(items);
                CreateHandleAndDrainReactiveCallbacks(
                    runtime.RootControl);

                Panel itemCard = null;
                int i;

                for (i = 0; i < rows.Controls.Count; i++)
                {
                    itemCard = rows.Controls[i] as Panel;

                    if (itemCard != null)
                        break;
                }

                AssertTrue(itemCard != null, "slotted item card is realized");
                Panel itemContent = itemCard.Controls[1] as Panel;
                TextBox itemEditor = itemContent == null
                    ? null
                    : itemContent.Controls[0] as TextBox;

                AssertTrue(itemEditor != null, "slotted item editor is realized");
                AssertEqual(
                    "Item initial",
                    itemEditor.Text,
                    "projected content uses the current item context");

                item.Text.Value = "Item source changed";
                DrainReactiveCallbacks(runtime.RootControl);
                AssertEqual(
                    "Item source changed",
                    itemEditor.Text,
                    "projected item binding refreshes incrementally");

                itemEditor.Text = "Item target changed";
                DrainReactiveCallbacks(runtime.RootControl);
                AssertEqual(
                    "Item target changed",
                    item.Text.Value,
                    "projected item two-way binding writes to the item");
            }
            finally
            {
                runtime.Dispose();
            }

            AssertEqual(
                0,
                GetPropertyBindingSubscriberCount(item.Text),
                "projected item binding detaches on dispose");

            ExpectInvalidMarkupMessage(
                "<ContentCard>text</ContentCard>",
                state,
                "not text content");

            XamlRuntime.Register(
                Assembly.GetExecutingAssembly(),
                "WinFormsXaml.Tests.Fixtures.LogicalCard.xml");
            ExpectInvalidMarkupMessage(
                "<LogicalCard Caption='No slot'><Label /></LogicalCard>",
                state,
                "does not declare a <Children> slot");
            ExpectInvalidMarkupMessage(
                "<Children />",
                state,
                "only valid as the single projection slot");

            bool multipleSlotsRejected = false;

            try
            {
                XamlRuntime.Register(
                    Assembly.GetExecutingAssembly(),
                    "WinFormsXaml.Tests.Fixtures.InvalidMultipleChildren.xml");
            }
            catch (InvalidOperationException ex)
            {
                multipleSlotsRejected =
                    ex.Message.IndexOf(
                        "more than one <Children>",
                        StringComparison.Ordinal) >= 0;
            }

            AssertTrue(
                multipleSlotsRejected,
                "multiple component children slots are rejected at registration");

            ProjectedDisposableComponent.CreatedCount = 0;
            ProjectedDisposableComponent.DisposedCount = 0;
            XamlRuntime.Register(
                "ProjectedDisposableComponent",
                typeof(ProjectedDisposableComponent));
            ExpectInvalidMarkupMessage(
                "<ContentCard><ProjectedDisposableComponent /></ContentCard>",
                state,
                "can project only Windows Forms Control roots");
            AssertEqual(
                1,
                ProjectedDisposableComponent.CreatedCount,
                "invalid projected object was created once");
            AssertEqual(
                1,
                ProjectedDisposableComponent.DisposedCount,
                "invalid projected object is released before the build fails");
        }

        private static void TestComponentPresetUsesComponentAssembly()
        {
            const string resourceName =
                "WinFormsXaml.Tests.Fixtures.ComponentPresetCard.xml";

            XamlRuntime.Register(
                Assembly.GetExecutingAssembly(),
                resourceName);

            MethodInfo load = typeof(XamlRuntime).GetMethod(
                "Load",
                BindingFlags.Static | BindingFlags.NonPublic,
                null,
                new Type[]
                {
                    typeof(string),
                    typeof(object),
                    typeof(string),
                    typeof(PresetManager),
                    typeof(Assembly),
                    typeof(string)
                },
                null);

            AssertTrue(
                load != null,
                "private provenance-aware Load overload found");

            Assembly rootMarkupAssembly = typeof(Form).Assembly;
            XamlRuntime runtime = null;

            try
            {
                try
                {
                    runtime = load.Invoke(
                        null,
                        new object[]
                        {
                            "<ComponentPresetCard Name='PresetCard' />",
                            null,
                            null,
                            null,
                            rootMarkupAssembly,
                            "cross-assembly test markup"
                        }) as XamlRuntime;
                }
                catch (TargetInvocationException ex)
                {
                    throw ex.InnerException;
                }

                Panel card = runtime.Get<Panel>("PresetCard");
                Label label = card.Controls[0] as Label;

                AssertTrue(label != null, "component preset label exists");
                AssertEqual(
                    "Resolved from component assembly",
                    label.Text,
                    "embedded preset import prefers component resource assembly");
                AssertSame(
                    rootMarkupAssembly,
                    GetInstanceField(runtime, "_markupAssembly"),
                    "root markup provenance is not mutated");
                AssertSame(
                    rootMarkupAssembly,
                    GetInstanceField(runtime, "_activeMarkupAssembly"),
                    "active markup assembly is restored after component build");
            }
            finally
            {
                if (runtime != null)
                    runtime.Dispose();
            }
        }

        private static void TestRegisteredComponentConditionsRemainIndependent()
        {
            XamlRuntime.Register(
                Assembly.GetExecutingAssembly(),
                "WinFormsXaml.Tests.Fixtures.ReactiveCard.xml");

            ComponentConditionState state =
                new ComponentConditionState(false, false);
            string originalTemplate = null;
            XamlRuntime runtime = null;

            try
            {
                originalTemplate =
                    ReplaceRegisteredComponentTemplateForTest(
                        "ReactiveCard",
                        "<Panel Condition='{Binding Caption}'>" +
                        "  <Label Text='Conditional component' />" +
                        "</Panel>");
                runtime = XamlRuntime.Load(
                    "<Panel>" +
                    "  <ReactiveCard Name='ConditionalCard' " +
                    "      Caption='{Binding TemplateCondition}' " +
                    "      Condition='{Binding InvocationCondition}' />" +
                    "</Panel>",
                    state);

                Panel card =
                    runtime.Get<Panel>("ConditionalCard");
                CreateHandleAndDrainReactiveCallbacks(
                    runtime.RootControl);

                IDictionary conditionStates =
                    GetElementInfoField(
                        runtime,
                        card,
                        "ConditionStates") as IDictionary;

                AssertTrue(
                    conditionStates != null,
                    "component condition state collection exists");
                AssertEqual(
                    2,
                    conditionStates.Count,
                    "template and invocation conditions use separate state keys");
                AssertTrue(!card.Visible, "both component conditions start false");

                state.InvocationCondition.Value = true;
                DrainReactiveCallbacks(runtime.RootControl);
                AssertTrue(
                    !card.Visible,
                    "invocation true cannot override false template condition");

                state.TemplateCondition.Value = true;
                DrainReactiveCallbacks(runtime.RootControl);
                AssertTrue(card.Visible, "both true component conditions show root");

                state.TemplateCondition.Value = false;
                DrainReactiveCallbacks(runtime.RootControl);
                AssertTrue(
                    !card.Visible,
                    "template false hides root while invocation remains true");

                state.InvocationCondition.Value = false;
                DrainReactiveCallbacks(runtime.RootControl);
                state.TemplateCondition.Value = true;
                DrainReactiveCallbacks(runtime.RootControl);
                AssertTrue(
                    !card.Visible,
                    "template true cannot override false invocation condition");

                state.InvocationCondition.Value = true;
                DrainReactiveCallbacks(runtime.RootControl);
                AssertTrue(
                    card.Visible,
                    "restoring both independent conditions shows root");
                AssertEqual(
                    2,
                    conditionStates.Count,
                    "component condition state keys remain stable after toggles");
            }
            finally
            {
                if (runtime != null)
                    runtime.Dispose();

                if (originalTemplate != null)
                {
                    ReplaceRegisteredComponentTemplateForTest(
                        "ReactiveCard",
                        originalTemplate);
                }
            }
        }

        private static void TestRegisteredComponentTemplateParsingCache()
        {
            XamlRuntime.Register(
                Assembly.GetExecutingAssembly(),
                "WinFormsXaml.Tests.Fixtures.ReactiveCard.xml");

            XamlRuntime runtime = XamlRuntime.Load(
                "<Panel>" +
                "  <ReactiveCard Name='FirstCard' Caption='First' />" +
                "  <ReactiveCard Name='SecondCard' Caption='Second' />" +
                "</Panel>");

            try
            {
                Panel first = runtime.Get<Panel>("FirstCard");
                Panel second = runtime.Get<Panel>("SecondCard");

                AssertTrue(
                    !Object.ReferenceEquals(first, second),
                    "component instances use isolated control trees");
                AssertEqual(
                    "First",
                    ((Label)first.Controls[0]).Text,
                    "first component receives its own values");
                AssertEqual(
                    "Second",
                    ((Label)second.Controls[0]).Text,
                    "second component receives its own values");

                IDictionary cache =
                    GetInstanceField(
                        runtime,
                        "_componentTemplateCache") as IDictionary;

                AssertTrue(cache != null, "component template cache exists");
                AssertEqual(
                    1,
                    cache.Count,
                    "two instances share one parsed component template");

                DictionaryEntry retained = new DictionaryEntry();

                foreach (DictionaryEntry entry in cache)
                {
                    retained = entry;
                    break;
                }

                FieldInfo rootField = retained.Value.GetType().GetField(
                    "Root",
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);
                XmlElement cachedRoot =
                    rootField == null
                        ? null
                        : rootField.GetValue(retained.Value) as XmlElement;

                AssertTrue(
                    cachedRoot != null &&
                    cachedRoot.OuterXml.IndexOf(
                        "{Binding Caption}",
                        StringComparison.Ordinal) >= 0,
                    "building instances does not mutate the cached source tree");

                const string replacement =
                    "<Label Text='Replacement template' />";
                string original =
                    ReplaceRegisteredComponentTemplateForTest(
                        "ReactiveCard",
                        replacement);

                try
                {
                    MethodInfo cloneMethod = typeof(XamlRuntime).GetMethod(
                        "CloneRegisteredComponentTemplateDocument",
                        BindingFlags.Instance | BindingFlags.NonPublic);

                    AssertTrue(
                        cloneMethod != null,
                        "component template clone helper found");

                    XmlDocument refreshed = cloneMethod.Invoke(
                        runtime,
                        new object[] { retained.Key }) as XmlDocument;

                    AssertTrue(
                        refreshed != null &&
                        refreshed.DocumentElement != null,
                        "replacement template produces a fresh document");
                    AssertEqual(
                        "Label",
                        refreshed.DocumentElement.LocalName,
                        "template replacement invalidates the parsed entry");
                    AssertEqual(
                        "Replacement template",
                        refreshed.DocumentElement.GetAttribute("Text"),
                        "replacement markup is reparsed before cloning");
                }
                finally
                {
                    ReplaceRegisteredComponentTemplateForTest(
                        "ReactiveCard",
                        original);
                }
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestRegisteredComponentRootDisposalDetachesSources()
        {
            XamlRuntime.Register(
                Assembly.GetExecutingAssembly(),
                "WinFormsXaml.Tests.Fixtures.ReactiveCard.xml");

            ReactiveBindingState state = new ReactiveBindingState();
            XamlRuntime runtime = XamlRuntime.Load(
                "<Panel>" +
                "  <ReactiveCard Name='DisposableCard' " +
                "      Caption='{Binding Text}-{Binding SecondaryText}' />" +
                "</Panel>",
                state);

            try
            {
                Panel card = runtime.Get<Panel>("DisposableCard");
                CreateHandleAndDrainReactiveCallbacks(runtime.RootControl);

                AssertEqual(
                    1,
                    GetPropertyBindingSubscriberCount(state.Text),
                    "first component-property source subscribed");
                AssertEqual(
                    1,
                    GetPropertyBindingSubscriberCount(state.SecondaryText),
                    "second component-property source subscribed");

                ArrayList componentInstances =
                    GetInstanceField(runtime, "_componentInstances") as ArrayList;
                AssertTrue(
                    componentInstances != null &&
                    componentInstances.Count == 1,
                    "component instance retained before root disposal");

                card.Dispose();

                AssertEqual(
                    0,
                    GetPropertyBindingSubscriberCount(state.Text),
                    "component root disposal detaches first source");
                AssertEqual(
                    0,
                    GetPropertyBindingSubscriberCount(state.SecondaryText),
                    "component root disposal detaches second source");
                AssertEqual(
                    0,
                    componentInstances.Count,
                    "component root disposal releases instance state");
                AssertTrue(
                    !runtime.IsDisposed,
                    "component child disposal leaves runtime active");

                state.Text.Value = "First signal after component disposal";
                state.SecondaryText.Value =
                    "Second signal after component disposal";
                DrainReactiveCallbacks(runtime.RootControl);

                AssertEqual(
                    0,
                    GetPropertyBindingSubscriberCount(state.Text),
                    "first component source remains detached");
                AssertEqual(
                    0,
                    GetPropertyBindingSubscriberCount(state.SecondaryText),
                    "second component source remains detached");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestRegisteredXmlComponentItemReload()
        {
            XamlRuntime.Register(
                Assembly.GetExecutingAssembly(),
                "WinFormsXaml.Tests.Fixtures.ReactiveCard.xml");

            XamlRuntime runtime = XamlRuntime.Load(
                "<Panel>" +
                "  <Presets Name='Copy' Selected='One'>" +
                "    <Preset Name='One'><Set Key='Suffix' Value='One' /></Preset>" +
                "    <Preset Name='Two'><Set Key='Suffix' Value='Two' /></Preset>" +
                "  </Presets>" +
                "  <ItemsControl Name='Rows' ItemKeyPath='Id' " +
                "    ItemVersionPath='Version' Virtualizing='false' " +
                "    ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <ReactiveCard Caption='{Binding Text}-{Preset Copy.Suffix}' />" +
                "  </ItemsControl.ItemTemplate>" +
                "  </ItemsControl>" +
                "</Panel>");

            try
            {
                XamlRuntime.ItemsControl rows =
                    runtime.GetItemsControl("Rows");
                ContentItemState item = new ContentItemState();
                item.Id = "component-item";
                item.Version = 1;
                item.Text = "Before";
                ArrayList items = new ArrayList();
                items.Add(item);
                rows.SetItems(items);

                Panel card = rows.Controls[0] as Panel;
                AssertTrue(card != null, "component item realized");
                AssertEqual(
                    "Before-One",
                    ((Label)card.Controls[0]).Text,
                    "initial component item binding");

                item.Version = 2;
                item.Text = "After";
                rows.ReloadItems();

                card = rows.Controls[0] as Panel;
                AssertTrue(card != null, "component item rebuilt");
                AssertEqual(
                    "After-One",
                    ((Label)card.Controls[0]).Text,
                    "component item binding refreshed");

                runtime.RootControl.CreateControl();
                runtime.Presets.Select("Copy", "Two");

                card = rows.Controls[0] as Panel;
                AssertTrue(card != null, "component preset item rebuilt");
                AssertEqual(
                    "After-Two",
                    ((Label)card.Controls[0]).Text,
                    "component item preset refreshed");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestRegisteredComponentValidation()
        {
            XamlRuntime.Register(
                Assembly.GetExecutingAssembly(),
                "WinFormsXaml.Tests.Fixtures.ReactiveCard.xml");

            BindingState state = new BindingState();
            state.Row = 0;
            XamlRuntime runtime = XamlRuntime.Load(
                "<Grid Width='100' Height='100'>" +
                "  <Grid.RowDefinitions>" +
                "    <RowDefinition Height='40' />" +
                "    <RowDefinition Height='*' />" +
                "  </Grid.RowDefinitions>" +
                "  <ReactiveCard Name='AttachedCard' Caption='Attached' " +
                "      Grid.Row='{Binding Row}' />" +
                "</Grid>",
                state);

            try
            {
                Panel card = runtime.Get<Panel>("AttachedCard");
                AssertEqual(0, card.Top, "initial component attached property");

                state.Row = 1;
                runtime.ReloadBinding("AttachedCard", "Grid.Row");
                AssertEqual(40, card.Top, "reloaded component attached property");
            }
            finally
            {
                runtime.Dispose();
            }

            XamlRuntime.Register(
                Assembly.GetExecutingAssembly(),
                "WinFormsXaml.Tests.Fixtures.InvalidComponentContext.xml");

            ExpectInvalidOperation(
                delegate
                {
                    XamlRuntime.Load("<InvalidComponentContext />");
                });

            bool colonRejected = false;

            try
            {
                XamlRuntime.Register(
                    "Invalid:Component",
                    typeof(RegisteredCtorLabel));
            }
            catch (ArgumentException)
            {
                colonRejected = true;
            }

            AssertTrue(colonRejected, "colon-qualified component name rejected");
        }

        private static void TestNativeTypeNames()
        {
            XamlRuntime runtime = XamlRuntime.Load(
                "<Panel>" +
                "  <DataGrid Name='Grid'>" +
                "    <DataGridTableStyle Name='LegacyTable'>" +
                "      <DataGridTextBoxColumn Name='LegacyTextColumn' />" +
                "      <DataGridBoolColumn Name='LegacyBoolColumn' />" +
                "    </DataGridTableStyle>" +
                "  </DataGrid>" +
                "  <ToolBar Name='Bar'>" +
                "    <ToolBarButton Name='BarButton' Text='Open' />" +
                "  </ToolBar>" +
                "  <StatusBar Name='LegacyStatus' ShowPanels='true'>" +
                "    <StatusBarPanel Name='LegacyPanel' Text='Ready' />" +
                "  </StatusBar>" +
                "  <ToolStripContainer Name='StripContainer' />" +
                "  <ToolStripPanel Name='StripPanel'>" +
                "    <ToolStrip Name='NestedStrip' />" +
                "  </ToolStripPanel>" +
                "  <BindingNavigator Name='Navigator'>" +
                "    <ToolStripStatusLabel Name='StatusItem' Text='Ready' />" +
                "    <ToolStripProgressBar Name='ProgressItem' Value='20' />" +
                "  </BindingNavigator>" +
                "  <TreeView Name='Tree'>" +
                "    <TreeNode Name='RootNode' Text='Root'>" +
                "      <TreeNode Name='ChildNode' Text='Child' />" +
                "    </TreeNode>" +
                "  </TreeView>" +
                "  <ListView Name='NativeList' View='Details'>" +
                "    <ColumnHeader Name='TitleColumn' Text='Title' />" +
                "    <ListViewGroup Name='PrimaryGroup' Header='Primary' />" +
                "    <ListViewItem Name='FirstItem' Text='First' />" +
                "  </ListView>" +
                "  <DataGridView Name='ModernGrid'>" +
                "    <DataGridViewTextBoxColumn HeaderText='Text' />" +
                "    <DataGridViewCheckBoxColumn HeaderText='Check' />" +
                "    <DataGridViewComboBoxColumn HeaderText='Choice' />" +
                "    <DataGridViewImageColumn HeaderText='Image' />" +
                "    <DataGridViewButtonColumn HeaderText='Action' />" +
                "    <DataGridViewLinkColumn HeaderText='Link' />" +
                "  </DataGridView>" +
                "</Panel>");

            try
            {
                AssertEqual(
                    typeof(DataGrid),
                    runtime["Grid"].GetType(),
                    "DataGrid resolves to the native WinForms type");
                AssertEqual(
                    typeof(ToolBar),
                    runtime["Bar"].GetType(),
                    "ToolBar resolves to the native WinForms type");
                DataGrid legacyGrid = runtime.Get<DataGrid>("Grid");
                AssertTrue(
                    legacyGrid.TableStyles.Count == 1 &&
                    legacyGrid.TableStyles[0].GridColumnStyles.Count == 2,
                    "DataGrid table and column styles use their native collections");
                AssertTrue(
                    runtime.Get<ToolBar>("Bar").Buttons.Count == 1,
                    "ToolBarButton attaches through ToolBar.Buttons");
                AssertTrue(
                    runtime.Get<StatusBar>("LegacyStatus").Panels.Count == 1,
                    "StatusBarPanel attaches through StatusBar.Panels");
                AssertEqual(
                    typeof(ToolStripContainer),
                    runtime["StripContainer"].GetType(),
                    "ToolStripContainer resolves to the native WinForms type");
                ToolStripPanel stripPanel =
                    runtime.Get<ToolStripPanel>("StripPanel");
                AssertTrue(
                    stripPanel.Controls.Contains(
                        runtime.Get<ToolStrip>("NestedStrip")),
                    "ToolStripPanel receives a nested ToolStrip control");

                BindingNavigator navigator =
                    runtime.Get<BindingNavigator>("Navigator");
                AssertEqual(
                    typeof(ToolStripStatusLabel),
                    runtime["StatusItem"].GetType(),
                    "StatusStrip label attaches through ToolStrip.Items");
                AssertEqual(
                    typeof(ToolStripProgressBar),
                    runtime["ProgressItem"].GetType(),
                    "ToolStrip progress attaches through ToolStrip.Items");
                AssertTrue(
                    navigator.Items.Contains(
                        runtime.Get<ToolStripStatusLabel>("StatusItem")) &&
                    navigator.Items.Contains(
                        runtime.Get<ToolStripProgressBar>("ProgressItem")),
                    "BindingNavigator receives both ToolStrip children");

                TreeView tree = runtime.Get<TreeView>("Tree");
                AssertTrue(
                    tree.Nodes.Count == 1 &&
                    tree.Nodes[0].Nodes.Count == 1,
                    "TreeNode children attach to TreeView and TreeNode collections");

                ListView list = runtime.Get<ListView>("NativeList");
                AssertTrue(
                    list.Columns.Count == 1 &&
                    list.Groups.Count == 1 &&
                    list.Items.Count == 1,
                    "ListView column, group, and item use their native collections");

                DataGridView modernGrid =
                    runtime.Get<DataGridView>("ModernGrid");
                AssertTrue(
                    modernGrid.Columns.Count == 6,
                    "all concrete .NET 2 DataGridView column types attach");
            }
            finally
            {
                runtime.Dispose();
            }

            ExpectInvalidOperation(
                delegate
                {
                    XamlRuntime.Load("<TextBlock />");
                });
        }

        private static void TestRegisteredClassComponent()
        {
            XamlRuntime.Register(
                "RegisteredCtorLabel",
                typeof(RegisteredCtorLabel));

            BindingState state = new BindingState();
            state.Text = "Constructed";
            XamlRuntime runtime = XamlRuntime.Load(
                "<Panel>" +
                "  <RegisteredCtorLabel Name='Ctor' " +
                "      Caption='{Binding Text}' ForeColor='Blue' />" +
                "  <RegisteredCtorLabel Name='SecondCtor' " +
                "      Caption='Second' />" +
                "</Panel>",
                state);

            try
            {
                RegisteredCtorLabel label =
                    runtime.Get<RegisteredCtorLabel>("Ctor");
                RegisteredCtorLabel second =
                    runtime.Get<RegisteredCtorLabel>("SecondCtor");

                AssertEqual("Constructed", label.Text, "constructor argument");
                AssertEqual(
                    "Second",
                    second.Text,
                    "cached constructor metadata keeps per-instance arguments");
                AssertEqual(0, label.CaptionSetCount, "constructor property not set twice");
                AssertEqual(Color.Blue, label.ForeColor, "writable property after constructor");

                state.Text = "Reloaded";
                runtime.ReloadBinding("Ctor", "Caption");
                AssertEqual("Reloaded", label.Text, "constructor property binding reload");
                AssertEqual(1, label.CaptionSetCount, "reload uses writable property");
            }
            finally
            {
                runtime.Dispose();
            }

            ExpectInvalidOperation(
                delegate
                {
                    XamlRuntime.Load("<RegisteredCtorLabel />");
                });
        }

        private static void TestPresetStyleSetter()
        {
            const string markup =
                "<Panel>" +
                "  <Panel.Resources>" +
                "    <Style Key='ThemeLabel' TargetType='Label'>" +
                "      <Setter Property='Foreground' Value='{Preset Theme.Foreground}' />" +
                "    </Style>" +
                "  </Panel.Resources>" +
                "  <Presets Name='Theme' Selected='Light'>" +
                "    <Preset Name='Light'><Set Key='Foreground' Value='Black' /></Preset>" +
                "    <Preset Name='Dark'><Set Key='Foreground' Value='White' /></Preset>" +
                "  </Presets>" +
                "  <Label Name='Caption' Style='ThemeLabel' />" +
                "</Panel>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                Label caption = runtime.Get<Label>("Caption");
                AssertEqual(Color.Black, caption.ForeColor, "initial style preset");

                runtime.RootControl.CreateControl();
                runtime.Presets.Select("Theme", "Dark");
                AssertEqual(Color.White, caption.ForeColor, "live style preset");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestStyleSwitchRemovesStaleSetterBinding()
        {
            StyleState state = new StyleState();
            state.CurrentStyle = "DynamicStyle";
            state.StyleBackground = "Red";

            const string markup =
                "<Panel>" +
                "  <Panel.Resources>" +
                "    <Style Key='DynamicStyle' TargetType='Label'>" +
                "      <Setter Property='Background' Value='{Binding StyleBackground}' />" +
                "    </Style>" +
                "    <Style Key='StaticStyle' TargetType='Label'>" +
                "      <Setter Property='Background' Value='Blue' />" +
                "    </Style>" +
                "  </Panel.Resources>" +
                "  <Label Name='Caption' Style='{Binding CurrentStyle}' />" +
                "</Panel>";

            XamlRuntime runtime = XamlRuntime.Load(markup, state);

            try
            {
                Label caption = runtime.Get<Label>("Caption");
                AssertEqual(Color.Red, caption.BackColor, "initial dynamic setter");

                state.CurrentStyle = "StaticStyle";
                runtime.ReloadBinding("Caption", "Style");
                AssertEqual(Color.Blue, caption.BackColor, "switched static setter");

                state.StyleBackground = "Green";
                runtime.ReloadBindings("Caption");
                AssertEqual(
                    Color.Blue,
                    caption.BackColor,
                    "setter binding from inactive style");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestStyleSwitchClearsOmittedProperty()
        {
            StyleState state = new StyleState();
            state.CurrentStyle = "BackgroundStyle";

            const string markup =
                "<Panel>" +
                "  <Panel.Resources>" +
                "    <Style Key='BackgroundStyle' TargetType='Label'>" +
                "      <Setter Property='Background' Value='Red' />" +
                "    </Style>" +
                "    <Style Key='PlainStyle' TargetType='Label'>" +
                "      <Setter Property='Foreground' Value='Blue' />" +
                "    </Style>" +
                "  </Panel.Resources>" +
                "  <Label Name='BaselineCaption' />" +
                "  <Label Name='Caption' Style='{Binding CurrentStyle}' />" +
                "</Panel>";

            XamlRuntime runtime = XamlRuntime.Load(markup, state);

            try
            {
                Label caption = runtime.Get<Label>("Caption");
                Label baseline = runtime.Get<Label>("BaselineCaption");
                AssertEqual(Color.Red, caption.BackColor, "initial styled background");

                state.CurrentStyle = "PlainStyle";
                runtime.ReloadBinding("Caption", "Style");

                AssertEqual(
                    baseline.BackColor,
                    caption.BackColor,
                    "background omitted by replacement style");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestReentrantStyleReloadKeepsNewestStyle()
        {
            StyleState state = new StyleState();
            state.CurrentStyle = "RedStyle";

            const string markup =
                "<Panel>" +
                "  <Panel.Resources>" +
                "    <Style Key='RedStyle' TargetType='Button'>" +
                "      <Setter Property='Background' Value='Red' />" +
                "    </Style>" +
                "    <Style Key='GreenStyle' TargetType='Button'>" +
                "      <Setter Property='Background' Value='Green' />" +
                "    </Style>" +
                "    <Style Key='BlueStyle' TargetType='Button'>" +
                "      <Setter Property='Background' Value='Blue' />" +
                "    </Style>" +
                "  </Panel.Resources>" +
                "  <Button Name='Action' Style='{Binding CurrentStyle}' />" +
                "</Panel>";

            XamlRuntime runtime = XamlRuntime.Load(markup, state);
            Button action = runtime.Get<Button>("Action");
            bool nestedReloadRequested = false;
            EventHandler handler =
                delegate
                {
                    if (nestedReloadRequested)
                        return;

                    nestedReloadRequested = true;
                    state.CurrentStyle = "BlueStyle";
                    runtime.ReloadBinding("Action", "Style");
                };

            action.BackColorChanged += handler;

            try
            {
                state.CurrentStyle = "GreenStyle";
                runtime.ReloadBinding("Action", "Style");

                AssertTrue(nestedReloadRequested, "nested style reload requested");
                AssertEqual(Color.Blue, action.BackColor, "newest nested style");
            }
            finally
            {
                action.BackColorChanged -= handler;
                runtime.Dispose();
            }
        }

        private static void TestButtonVisualStyleBackgroundReturns()
        {
            StyleState state = new StyleState();
            state.CurrentStyle = "ColoredStyle";

            const string markup =
                "<Panel>" +
                "  <Panel.Resources>" +
                "    <Style Key='ColoredStyle' TargetType='Button'>" +
                "      <Setter Property='Background' Value='Red' />" +
                "    </Style>" +
                "    <Style Key='PlainStyle' TargetType='Button'>" +
                "      <Setter Property='Text' Value='Plain' />" +
                "    </Style>" +
                "  </Panel.Resources>" +
                "  <Button Name='Action' Style='{Binding CurrentStyle}' />" +
                "</Panel>";

            XamlRuntime runtime = XamlRuntime.Load(markup, state);

            try
            {
                Button action = runtime.Get<Button>("Action");
                AssertTrue(
                    !action.UseVisualStyleBackColor,
                    "custom background disables visual-style painting");

                state.CurrentStyle = "PlainStyle";
                runtime.ReloadBinding("Action", "Style");

                AssertTrue(
                    action.UseVisualStyleBackColor,
                    "visual-style painting restored after background omission");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestImplicitBackgroundReturnsAfterStyleSwitch()
        {
            StyleState state = new StyleState();
            state.CurrentStyle = "OverrideStyle";

            const string markup =
                "<Panel>" +
                "  <Panel.Resources>" +
                "    <Style TargetType='Label'>" +
                "      <Setter Property='Background' Value='Green' />" +
                "    </Style>" +
                "    <Style Key='OverrideStyle' TargetType='Label'>" +
                "      <Setter Property='Background' Value='Red' />" +
                "    </Style>" +
                "    <Style Key='PlainStyle' TargetType='Label'>" +
                "      <Setter Property='Foreground' Value='Blue' />" +
                "    </Style>" +
                "  </Panel.Resources>" +
                "  <Label Name='Caption' Style='{Binding CurrentStyle}' />" +
                "</Panel>";

            XamlRuntime runtime = XamlRuntime.Load(markup, state);

            try
            {
                Label caption = runtime.Get<Label>("Caption");
                AssertEqual(Color.Red, caption.BackColor, "named style override");

                state.CurrentStyle = "PlainStyle";
                runtime.ReloadBinding("Caption", "Style");

                AssertEqual(
                    Color.Green,
                    caption.BackColor,
                    "implicit background after named style omission");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestLocalValueWinsAfterStyleSwitch()
        {
            StyleState state = new StyleState();
            state.CurrentStyle = "FirstStyle";
            state.StyleBackground = "Red";
            state.LocalBackground = "Lime";

            const string markup =
                "<Panel>" +
                "  <Panel.Resources>" +
                "    <Style Key='FirstStyle' TargetType='Label'>" +
                "      <Setter Property='Background' Value='{Binding StyleBackground}' />" +
                "    </Style>" +
                "    <Style Key='SecondStyle' TargetType='Label'>" +
                "      <Setter Property='Background' Value='Blue' />" +
                "    </Style>" +
                "  </Panel.Resources>" +
                "  <Label Name='DynamicLocal' Style='{Binding CurrentStyle}' Background='{Binding LocalBackground}' />" +
                "</Panel>";

            XamlRuntime runtime = XamlRuntime.Load(markup, state);

            try
            {
                Label dynamicLocal = runtime.Get<Label>("DynamicLocal");

                AssertEqual(Color.Lime, dynamicLocal.BackColor, "initial dynamic local value");

                state.CurrentStyle = "SecondStyle";
                runtime.ReloadBinding("DynamicLocal", "Style");

                AssertEqual(
                    Color.Lime,
                    dynamicLocal.BackColor,
                    "dynamic local value after style switch");

                state.LocalBackground = "Orange";
                runtime.ReloadBinding("DynamicLocal", "Background");
                AssertEqual(
                    Color.Orange,
                    dynamicLocal.BackColor,
                    "reloaded dynamic local value");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestStaticLocalValueWinsAfterStyleSwitch()
        {
            StyleState state = new StyleState();
            state.CurrentStyle = "FirstStyle";

            const string markup =
                "<Panel>" +
                "  <Panel.Resources>" +
                "    <Style Key='FirstStyle' TargetType='Label'>" +
                "      <Setter Property='Background' Value='Red' />" +
                "    </Style>" +
                "    <Style Key='SecondStyle' TargetType='Label'>" +
                "      <Setter Property='Background' Value='Blue' />" +
                "    </Style>" +
                "  </Panel.Resources>" +
                "  <Label Name='StaticLocal' Style='{Binding CurrentStyle}' Background='Purple' />" +
                "</Panel>";

            XamlRuntime runtime = XamlRuntime.Load(markup, state);

            try
            {
                Label staticLocal = runtime.Get<Label>("StaticLocal");
                AssertEqual(Color.Purple, staticLocal.BackColor, "initial static local value");

                state.CurrentStyle = "SecondStyle";
                runtime.ReloadBinding("StaticLocal", "Style");
                AssertEqual(
                    Color.Purple,
                    staticLocal.BackColor,
                    "static local value after style switch");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestBackColorLocalValueBlocksBackgroundStyle()
        {
            StyleState state = new StyleState();
            state.CurrentStyle = "FirstStyle";

            const string markup =
                "<Panel>" +
                "  <Panel.Resources>" +
                "    <Style Key='FirstStyle' TargetType='Label'>" +
                "      <Setter Property='Background' Value='Red' />" +
                "    </Style>" +
                "    <Style Key='SecondStyle' TargetType='Label'>" +
                "      <Setter Property='Background' Value='Blue' />" +
                "    </Style>" +
                "  </Panel.Resources>" +
                "  <Label Name='Caption' Style='{Binding CurrentStyle}' BackColor='Purple' />" +
                "</Panel>";

            XamlRuntime runtime = XamlRuntime.Load(markup, state);

            try
            {
                Label caption = runtime.Get<Label>("Caption");
                AssertEqual(
                    Color.Purple,
                    caption.BackColor,
                    "BackColor local value before style switch");

                state.CurrentStyle = "SecondStyle";
                runtime.ReloadBinding("Caption", "Style");

                AssertEqual(
                    Color.Purple,
                    caption.BackColor,
                    "BackColor local value after Background style switch");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestLabelAutoSizeBaselineReturns()
        {
            StyleState state = new StyleState();
            state.CurrentStyle = "FixedSizeStyle";

            const string markup =
                "<Panel>" +
                "  <Panel.Resources>" +
                "    <Style Key='FixedSizeStyle' TargetType='Label'>" +
                "      <Setter Property='AutoSize' Value='true' />" +
                "    </Style>" +
                "    <Style Key='PlainStyle' TargetType='Label'>" +
                "      <Setter Property='Foreground' Value='Blue' />" +
                "    </Style>" +
                "  </Panel.Resources>" +
                "  <Label Name='Caption' Style='{Binding CurrentStyle}' />" +
                "</Panel>";

            XamlRuntime runtime = XamlRuntime.Load(markup, state);

            try
            {
                Label caption = runtime.Get<Label>("Caption");
                AssertTrue(caption.AutoSize, "styled Label AutoSize");

                state.CurrentStyle = "PlainStyle";
                runtime.ReloadBinding("Caption", "Style");

                AssertTrue(
                    !caption.AutoSize,
                    "Label AutoSize baseline after style omission");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestFontAxisStylePrecedence()
        {
            StyleState state = new StyleState();
            state.CurrentStyle = "FirstFont";

            const string markup =
                "<Panel>" +
                "  <Panel.Resources>" +
                "    <Style Key='FirstFont' TargetType='Label'>" +
                "      <Setter Property='FontFamily' Value='Arial' />" +
                "    </Style>" +
                "    <Style Key='SecondFont' TargetType='Label'>" +
                "      <Setter Property='FontFamily' Value='Tahoma' />" +
                "    </Style>" +
                "  </Panel.Resources>" +
                "  <Label Name='Caption' Style='{Binding CurrentStyle}' FontSize='20' />" +
                "</Panel>";

            XamlRuntime runtime = XamlRuntime.Load(markup, state);

            try
            {
                Label caption = runtime.Get<Label>("Caption");
                float localSize = caption.Font.SizeInPoints;

                state.CurrentStyle = "SecondFont";
                runtime.ReloadBinding("Caption", "Style");

                AssertEqual(
                    localSize,
                    caption.Font.SizeInPoints,
                    "local font size after family style switch");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestAmbientFontInheritanceReturns()
        {
            StyleState state = new StyleState();
            state.CurrentStyle = "FamilyStyle";

            const string markup =
                "<Panel Name='Parent'>" +
                "  <Panel.Resources>" +
                "    <Style Key='FamilyStyle' TargetType='Label'>" +
                "      <Setter Property='FontFamily' Value='Arial' />" +
                "    </Style>" +
                "    <Style Key='PlainStyle' TargetType='Label'>" +
                "      <Setter Property='Foreground' Value='Blue' />" +
                "    </Style>" +
                "  </Panel.Resources>" +
                "  <Label Name='Caption' Style='{Binding CurrentStyle}' />" +
                "</Panel>";

            XamlRuntime runtime = XamlRuntime.Load(markup, state);

            try
            {
                Panel parent = runtime.Get<Panel>("Parent");
                Label caption = runtime.Get<Label>("Caption");

                state.CurrentStyle = "PlainStyle";
                runtime.ReloadBinding("Caption", "Style");

                using (Font changedParentFont = new Font(
                    parent.Font.FontFamily,
                    parent.Font.SizeInPoints + 3.0f,
                    parent.Font.Style,
                    GraphicsUnit.Point))
                {
                    parent.Font = changedParentFont;

                    AssertEqual(
                        changedParentFont.SizeInPoints,
                        caption.Font.SizeInPoints,
                        "child follows parent font after style removal");
                }
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestTypedCompositeBindings()
        {
            NativeBindingState state = new NativeBindingState();
            Font initialFont = new Font(
                FontFamily.GenericSansSerif,
                11.0f,
                FontStyle.Regular,
                GraphicsUnit.Point);
            Font updatedFont = new Font(
                FontFamily.GenericSansSerif,
                18.0f,
                FontStyle.Bold,
                GraphicsUnit.Point);

            state.Size = new Size(80, 30);
            state.Margin = new Padding(2);
            state.Font = initialFont;

            const string markup =
                "<StackPanel Width='300' Height='160'>" +
                "  <Label Name='Bound' HorizontalAlignment='Left' " +
                "         VerticalAlignment='Top' Size='{Binding Size}' " +
                "         Margin='{Binding Margin}' Font='{Binding Font}' />" +
                "</StackPanel>";

            XamlRuntime runtime = XamlRuntime.Load(markup, state);

            try
            {
                Label bound = runtime.Get<Label>("Bound");

                AssertEqual(new Size(80, 30), bound.Size, "initial bound Size");
                AssertEqual(new Padding(2), bound.Margin, "initial bound Margin");

                state.Size = new Size(120, 45);
                state.Margin = new Padding(7, 5, 9, 6);
                state.Font = updatedFont;
                runtime.ReloadBindings("Bound");
                runtime.RootControl.PerformLayout();

                AssertEqual(new Size(120, 45), bound.Size, "reloaded bound Size");
                AssertEqual(
                    new Padding(7, 5, 9, 6),
                    bound.Margin,
                    "reloaded bound Margin");
                AssertEqual(
                    updatedFont.SizeInPoints,
                    bound.Font.SizeInPoints,
                    "reloaded bound Font size");
                AssertEqual(
                    updatedFont.Style,
                    bound.Font.Style,
                    "reloaded bound Font style");
            }
            finally
            {
                runtime.RootControl.Dispose();
                runtime.Dispose();

                // Bound Font objects are caller-owned, even after replacement/disposal.
                AssertTrue(initialFont.Height > 0, "initial caller-owned Font remains usable");
                AssertTrue(updatedFont.Height > 0, "updated caller-owned Font remains usable");
                initialFont.Dispose();
                updatedFont.Dispose();
            }
        }

        private static void TestTypedRightToLeftBinding()
        {
            NativeBindingState state = new NativeBindingState();
            state.Direction = RightToLeft.Yes;

            const string markup =
                "<Panel Name='Parent' RightToLeft='No'>" +
                "  <Label Name='Child' RightToLeft='{Binding Direction}' />" +
                "  <ItemsControl Name='Rows' RightToLeft='{Binding Direction}' " +
                "                Virtualizing='false'>" +
                "    <ItemsControl.ItemTemplate>" +
                "      <Label AutoSize='true' Text='{Binding}' />" +
                "    </ItemsControl.ItemTemplate>" +
                "  </ItemsControl>" +
                "</Panel>";

            XamlRuntime runtime = XamlRuntime.Load(markup, state);

            try
            {
                Label child = runtime.Get<Label>("Child");
                XamlRuntime.ItemsControl rows =
                    runtime.GetItemsControl("Rows");

                AssertEqual(
                    RightToLeft.Yes,
                    child.RightToLeft,
                    "typed child direction after parent inheritance");
                AssertEqual(
                    RightToLeft.No,
                    rows.RightToLeft,
                    "ItemsControl keeps its native scrollbar direction");
                AssertEqual(
                    true,
                    GetInstanceField(rows, "ContentRightToLeft"),
                    "ItemsControl retains semantic RTL content direction");

                // Each reload performs the inheritance pass after the binding. A
                // missing explicit marker would let the LTR parent overwrite it.
                runtime.ReloadBinding("Child", "RightToLeft");
                runtime.ReloadBinding("Rows", "RightToLeft");

                AssertEqual(
                    RightToLeft.Yes,
                    child.RightToLeft,
                    "reloaded child direction remains explicit");
                AssertEqual(
                    true,
                    GetInstanceField(rows, "ContentRightToLeft"),
                    "reloaded ItemsControl direction remains explicit");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestMappedAliasShadowProperties()
        {
            StyleState state = new StyleState();
            state.CurrentStyle = "AliasStyle";
            state.SecondaryStyle = "PictureStyle";
            string imagePath = Path.Combine(
                Path.GetTempPath(),
                "wfx-style-shadow-" + Guid.NewGuid().ToString("N") + ".png");

            using (Bitmap bitmap = new Bitmap(1, 1))
                bitmap.Save(imagePath, ImageFormat.Png);

            string markup =
                "<Panel>" +
                "  <Panel.Resources>" +
                "    <Style Key='AliasStyle' TargetType='MappedAliasShadowControl'>" +
                "      <Setter Property='Background' Value='Red' />" +
                "      <Setter Property='FontFamily' Value='Arial' />" +
                "      <Setter Property='HorizontalAlignment' Value='Left' />" +
                "      <Setter Property='IsChecked' Value='true' />" +
                "    </Style>" +
                "    <Style Key='PlainStyle' TargetType='MappedAliasShadowControl'>" +
                "      <Setter Property='Width' Value='40' />" +
                "    </Style>" +
                "    <Style Key='PictureStyle' TargetType='SourceShadowPictureBox'>" +
                "      <Setter Property='Source' Value='" + imagePath + "' />" +
                "    </Style>" +
                "    <Style Key='PlainPictureStyle' TargetType='SourceShadowPictureBox'>" +
                "      <Setter Property='Width' Value='20' />" +
                "    </Style>" +
                "  </Panel.Resources>" +
                "  <MappedAliasShadowControl Name='Target' " +
                "      Style='{Binding CurrentStyle}' />" +
                "  <SourceShadowPictureBox Name='Picture' " +
                "      Style='{Binding SecondaryStyle}' />" +
                "</Panel>";

            XamlRuntime runtime = null;

            try
            {
                runtime = XamlRuntime.Load(markup, state);
                MappedAliasShadowControl target =
                    runtime.Get<MappedAliasShadowControl>("Target");
                SourceShadowPictureBox picture =
                    runtime.Get<SourceShadowPictureBox>("Picture");

                AssertEqual(Color.Red, target.BackColor, "mapped Background applied");
                AssertEqual(0, target.BackgroundSetCount, "custom Background untouched");
                AssertEqual(0, target.FontFamilySetCount, "custom FontFamily untouched");
                AssertEqual(
                    "Left",
                    GetElementInfoField(
                        runtime,
                        target,
                        "HorizontalAlignment").ToString(),
                    "style HorizontalAlignment metadata");
                AssertEqual(
                    0,
                    target.HorizontalAlignmentSetCount,
                    "custom HorizontalAlignment untouched");
                AssertEqual(true, target.IsChecked, "ordinary custom IsChecked applied");
                AssertEqual(1, target.IsCheckedSetCount, "custom IsChecked setter count");
                AssertTrue(
                    !String.IsNullOrEmpty(picture.ImageLocation),
                    "mapped PictureBox Source applied");
                AssertEqual(0, picture.SourceSetCount, "custom Source untouched");

                state.CurrentStyle = "PlainStyle";
                state.SecondaryStyle = "PlainPictureStyle";
                runtime.ReloadBinding("Target", "Style");
                runtime.ReloadBinding("Picture", "Style");

                AssertEqual(
                    "Custom background baseline",
                    target.Background,
                    "custom Background baseline remains untouched");
                AssertEqual(
                    "Custom font baseline",
                    target.FontFamily,
                    "custom FontFamily baseline remains untouched");
                AssertEqual(0, target.BackgroundSetCount, "Background not restored spuriously");
                AssertEqual(0, target.FontFamilySetCount, "FontFamily not restored spuriously");
                AssertEqual(
                    "Custom alignment baseline",
                    target.HorizontalAlignment,
                    "custom HorizontalAlignment baseline remains untouched");
                AssertEqual(
                    0,
                    target.HorizontalAlignmentSetCount,
                    "HorizontalAlignment not restored spuriously");
                AssertEqual(
                    "Stretch",
                    GetElementInfoField(
                        runtime,
                        target,
                        "HorizontalAlignment").ToString(),
                    "HorizontalAlignment metadata baseline restored");
                AssertEqual(false, target.IsChecked, "custom IsChecked baseline restored");
                AssertEqual(2, target.IsCheckedSetCount, "custom IsChecked restored once");
                AssertTrue(
                    String.IsNullOrEmpty(picture.ImageLocation),
                    "PictureBox Source location baseline restored");
                AssertEqual(null, picture.Image, "PictureBox image baseline restored");
                AssertEqual(
                    "Custom source baseline",
                    picture.Source,
                    "custom Source baseline remains untouched");
                AssertEqual(0, picture.SourceSetCount, "Source not restored spuriously");
            }
            finally
            {
                if (runtime != null)
                    runtime.Dispose();

                if (File.Exists(imagePath))
                    File.Delete(imagePath);
            }
        }

        private static void TestDynamicExactContentStyle()
        {
            ShadowStyleState state = new ShadowStyleState();
            state.CurrentStyle = "ContentStyle";
            state.NullableObject = "Styled content";

            const string markup =
                "<Panel>" +
                "  <Panel.Resources>" +
                "    <Style Key='ContentStyle' TargetType='ContentShadowControl'>" +
                "      <Setter Property='Content' Value='{Binding NullableObject}' />" +
                "    </Style>" +
                "    <Style Key='PlainContentStyle' TargetType='ContentShadowControl'>" +
                "      <Setter Property='Width' Value='40' />" +
                "    </Style>" +
                "  </Panel.Resources>" +
                "  <ContentShadowControl Name='Target' " +
                "      Style='{Binding CurrentStyle}' />" +
                "  <ItemsControl Name='Rows' ItemKeyPath='Id' " +
                "      ItemVersionPath='Version' Virtualizing='false' " +
                "      ProgressiveRendering='false'>" +
                "    <ItemsControl.ItemTemplate>" +
                "      <ContentShadowControl Style='ContentStyle' />" +
                "    </ItemsControl.ItemTemplate>" +
                "  </ItemsControl>" +
                "</Panel>";

            XamlRuntime runtime = XamlRuntime.Load(markup, state);

            try
            {
                ContentShadowControl target =
                    runtime.Get<ContentShadowControl>("Target");

                AssertEqual(
                    "Native text baseline",
                    target.Text,
                    "exact Content does not overwrite native Text");
                AssertEqual(
                    "Styled content",
                    target.Content,
                    "the exact Content property receives the style value");
                AssertEqual(1, target.ContentSetCount, "initial Content setter count");

                state.NullableObject = null;
                runtime.ReloadBinding("Target", "Content");

                AssertEqual(
                    "Native text baseline",
                    target.Text,
                    "null exact Content leaves native Text unchanged");
                AssertEqual(null, target.Content, "typed null reaches exact Content");
                AssertEqual(2, target.ContentSetCount, "typed null Content setter count");

                state.CurrentStyle = "PlainContentStyle";
                runtime.ReloadBinding("Target", "Style");

                AssertEqual(
                    "Native text baseline",
                    target.Text,
                    "native Text baseline restored after style removal");
                AssertSame(
                    target.BaselineContent,
                    target.Content,
                    "custom Content baseline after style removal");
                AssertEqual(3, target.ContentSetCount, "Content baseline restored once");

                XamlRuntime.ItemsControl rows =
                    runtime.GetItemsControl("Rows");
                ContentItemState item = new ContentItemState();
                item.Id = "item";
                item.Version = 1;
                item.NullableObject = null;
                ArrayList items = new ArrayList();
                items.Add(item);
                rows.SetItems(items);

                ContentShadowControl itemTarget = null;
                int i;

                for (i = 0; i < rows.Controls.Count; i++)
                {
                    itemTarget = rows.Controls[i] as ContentShadowControl;

                    if (itemTarget != null)
                        break;
                }

                AssertTrue(itemTarget != null, "item Content shadow control realized");
                AssertEqual(
                    "Native text baseline",
                    itemTarget.Text,
                    "item exact Content leaves native Text unchanged");
                AssertEqual(
                    null,
                    itemTarget.Content,
                    "item style typed null reaches exact Content");
                AssertEqual(
                    1,
                    itemTarget.ContentSetCount,
                    "item exact Content setter count");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestBoundExactContentPrecedence()
        {
            ShadowStyleState state = new ShadowStyleState();
            state.NullableObject = new Version(1, 2);

            const string markup =
                "<Panel>" +
                "  <Panel.Resources>" +
                "    <Style TargetType='ContentShadowControl'>" +
                "      <Setter Property='Text' Value='Styled text' />" +
                "    </Style>" +
                "  </Panel.Resources>" +
                "  <ContentShadowControl Name='Target' " +
                "      Content='{Binding NullableObject}' />" +
                "</Panel>";

            XamlRuntime runtime = XamlRuntime.Load(markup, state);

            try
            {
                ContentShadowControl target =
                    runtime.Get<ContentShadowControl>("Target");

                AssertEqual(
                    "Styled text",
                    target.Text,
                    "a local exact Content value does not block Text style");
                AssertSame(
                    state.NullableObject,
                    target.Content,
                    "bound Content uses the exact CLR property");
                AssertEqual(
                    1,
                    target.ContentSetCount,
                    "bound exact Content setter count");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestMappedNativeShadowRestore()
        {
            ShadowStyleState state = new ShadowStyleState();
            state.CurrentStyle = "AlignedStyle";

            const string markup =
                "<Panel>" +
                "  <Panel.Resources>" +
                "    <Style Key='AlignedStyle' TargetType='TextAlignShadowTextBox'>" +
                "      <Setter Property='TextAlignment' Value='Right' />" +
                "    </Style>" +
                "    <Style Key='PlainStyle' TargetType='TextAlignShadowTextBox' />" +
                "  </Panel.Resources>" +
                "  <TextAlignShadowTextBox Name='Target' " +
                "      Style='{Binding CurrentStyle}' />" +
                "</Panel>";

            XamlRuntime runtime = XamlRuntime.Load(markup, state);

            try
            {
                TextAlignShadowTextBox target =
                    runtime.Get<TextAlignShadowTextBox>("Target");

                AssertEqual(
                    HorizontalAlignment.Right,
                    ((TextBox)target).TextAlign,
                    "style changes native TextAlign");
                AssertEqual(
                    "Custom alignment baseline",
                    target.TextAlign,
                    "style leaves the shadow property untouched");
                AssertEqual(0, target.TextAlignSetCount, "shadow setter count");

                state.CurrentStyle = "PlainStyle";
                runtime.ReloadBinding("Target", "Style");

                AssertEqual(
                    HorizontalAlignment.Left,
                    ((TextBox)target).TextAlign,
                    "native TextAlign baseline restored");
                AssertEqual(
                    "Custom alignment baseline",
                    target.TextAlign,
                    "shadow property remains untouched after restore");
                AssertEqual(
                    0,
                    target.TextAlignSetCount,
                    "shadow property is not restored spuriously");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestWebBrowserSourceTypedNull()
        {
            ShadowStyleState state = new ShadowStyleState();
            state.NullableUri = new Uri("about:blank");

            const string markup =
                "<Panel RightToLeft='Yes'>" +
                "  <Panel.Resources>" +
                "    <Style Key='BrowserStyle' TargetType='WebBrowser'>" +
                "      <Setter Property='Source' Value='{Binding NullableUri}' />" +
                "    </Style>" +
                "  </Panel.Resources>" +
                "  <Label Name='InheritedDirectionTarget' />" +
                "  <WebBrowser Name='Target' Style='BrowserStyle' />" +
                "  <ItemsControl Name='Rows' ItemKeyPath='Id' " +
                "      ItemVersionPath='Version' Virtualizing='false' " +
                "      ProgressiveRendering='false'>" +
                "    <ItemsControl.ItemTemplate>" +
                "      <WebBrowser Style='BrowserStyle' />" +
                "    </ItemsControl.ItemTemplate>" +
                "  </ItemsControl>" +
                "</Panel>";

            XamlRuntime runtime = XamlRuntime.Load(markup, state);

            try
            {
                Label inheritedDirectionTarget =
                    runtime.Get<Label>("InheritedDirectionTarget");
                WebBrowser target = runtime.Get<WebBrowser>("Target");
                AssertEqual(
                    RightToLeft.Yes,
                    inheritedDirectionTarget.RightToLeft,
                    "normal controls still inherit RightToLeft");
                AssertEqual(
                    state.NullableUri,
                    target.Url,
                    "initial typed browser Source");

                state.NullableUri = null;
                runtime.ReloadBinding("Target", "Source");
                AssertWebBrowserSourceCleared(
                    target,
                    "typed null clears browser Source");

                XamlRuntime.ItemsControl rows =
                    runtime.GetItemsControl("Rows");
                ContentItemState item = new ContentItemState();
                item.Id = "browser-item";
                item.Version = 1;
                item.NullableUri = new Uri("about:blank");
                ArrayList items = new ArrayList();
                items.Add(item);
                rows.SetItems(items);

                WebBrowser itemTarget = null;
                int i;

                for (i = 0; i < rows.Controls.Count; i++)
                {
                    itemTarget = rows.Controls[i] as WebBrowser;

                    if (itemTarget != null)
                        break;
                }

                AssertTrue(itemTarget != null, "item browser realized");
                AssertEqual(
                    item.NullableUri,
                    itemTarget.Url,
                    "item initial typed Source");

                item.Version = 2;
                item.NullableUri = null;
                rows.ReloadItems();

                AssertWebBrowserSourceCleared(
                    itemTarget,
                    "retained item style clears typed null Source");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestPaddingShadowStyle()
        {
            ShadowStyleState state = new ShadowStyleState();
            state.CurrentStyle = "PaddingStyle";
            state.PaddingValue = new Padding(10, 11, 12, 13);

            const string markup =
                "<Panel>" +
                "  <Panel.Resources>" +
                "    <Style Key='PaddingStyle' TargetType='PaddingShadowControl'>" +
                "      <Setter Property='Padding' Value='{Binding PaddingValue}' />" +
                "    </Style>" +
                "    <Style Key='PlainPaddingStyle' TargetType='PaddingShadowControl'>" +
                "      <Setter Property='Width' Value='40' />" +
                "    </Style>" +
                "  </Panel.Resources>" +
                "  <PaddingShadowControl Name='Target' " +
                "      Style='{Binding CurrentStyle}' />" +
                "  <ItemsControl Name='Rows' ItemKeyPath='Id' " +
                "      ItemVersionPath='Version' Virtualizing='false' " +
                "      ProgressiveRendering='false'>" +
                "    <ItemsControl.ItemTemplate>" +
                "      <PaddingShadowControl Style='PaddingStyle' />" +
                "    </ItemsControl.ItemTemplate>" +
                "  </ItemsControl>" +
                "</Panel>";

            XamlRuntime runtime = XamlRuntime.Load(markup, state);

            try
            {
                PaddingShadowControl target =
                    runtime.Get<PaddingShadowControl>("Target");

                AssertEqual(
                    new Padding(10, 11, 12, 13),
                    ((Control)target).Padding,
                    "style changes native base Padding");
                AssertEqual(
                    "Custom padding baseline",
                    target.Padding,
                    "custom Padding remains unchanged");
                AssertEqual(0, target.PaddingSetCount, "custom Padding setter count");

                XamlRuntime.ItemsControl rows =
                    runtime.GetItemsControl("Rows");
                ContentItemState item = new ContentItemState();
                item.Id = "padding-item";
                item.Version = 1;
                item.PaddingValue = new Padding(20, 21, 22, 23);
                ArrayList items = new ArrayList();
                items.Add(item);
                rows.SetItems(items);

                PaddingShadowControl itemTarget = null;
                int i;

                for (i = 0; i < rows.Controls.Count; i++)
                {
                    itemTarget = rows.Controls[i] as PaddingShadowControl;

                    if (itemTarget != null)
                        break;
                }

                AssertTrue(itemTarget != null, "item Padding shadow control realized");
                AssertEqual(
                    new Padding(20, 21, 22, 23),
                    ((Control)itemTarget).Padding,
                    "item typed Padding applied to native base property");
                AssertEqual(0, itemTarget.PaddingSetCount, "item shadow setter count");

                item.Version = 2;
                item.PaddingValue = new Padding(24, 25, 26, 27);
                rows.ReloadItems();

                AssertEqual(
                    new Padding(24, 25, 26, 27),
                    ((Control)itemTarget).Padding,
                    "item retained style binding updates native Padding");
                AssertEqual(0, itemTarget.PaddingSetCount, "item retained shadow setter count");

                state.CurrentStyle = "PlainPaddingStyle";
                runtime.ReloadBinding("Target", "Style");

                AssertEqual(
                    new Padding(3, 4, 5, 6),
                    ((Control)target).Padding,
                    "native base Padding baseline restored");
                AssertEqual(
                    "Custom padding baseline",
                    target.Padding,
                    "custom Padding baseline remains untouched");
                AssertEqual(
                    0,
                    target.PaddingSetCount,
                    "custom Padding not restored spuriously");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestMenuContentEventStyle()
        {
            ShadowStyleState state = new ShadowStyleState();
            state.CurrentStyle = "MenuContentStyle";

            const string markup =
                "<Panel>" +
                "  <Panel.Resources>" +
                "    <Style Key='MenuContentStyle' TargetType='ContentEventMenuItem'>" +
                "      <Setter Property='Content' Value='Styled menu text' />" +
                "    </Style>" +
                "    <Style Key='PlainMenuStyle' TargetType='ContentEventMenuItem' />" +
                "  </Panel.Resources>" +
                "  <MenuStrip>" +
                "    <ContentEventMenuItem Name='MenuTarget' " +
                "        Style='{Binding CurrentStyle}' />" +
                "  </MenuStrip>" +
                "</Panel>";

            XamlRuntime runtime = XamlRuntime.Load(markup, state);

            try
            {
                ContentEventMenuItem target =
                    runtime.Get<ContentEventMenuItem>("MenuTarget");

                AssertEqual("Styled menu text", target.Text, "menu Content alias applied");

                state.CurrentStyle = "PlainMenuStyle";
                runtime.ReloadBinding("MenuTarget", "Style");

                AssertEqual(
                    "Native menu baseline",
                    target.Text,
                    "menu Text baseline restored after style removal");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestTextBoxLocalTextPrecedence()
        {
            ShadowStyleState state = new ShadowStyleState();
            state.CurrentStyle = "NamedPasswordStyle";

            const string markup =
                "<Panel>" +
                "  <Panel.Resources>" +
                "    <Style TargetType='TextBox'>" +
                "      <Setter Property='Text' Value='Implicit text' />" +
                "    </Style>" +
                "    <Style Key='NamedPasswordStyle' TargetType='TextBox'>" +
                "      <Setter Property='Text' Value='Named text' />" +
                "    </Style>" +
                "    <Style Key='OtherPasswordStyle' TargetType='TextBox'>" +
                "      <Setter Property='Text' Value='Other text' />" +
                "    </Style>" +
                "  </Panel.Resources>" +
                "  <TextBox Name='Password' Style='{Binding CurrentStyle}' " +
                "      Text='secret' UseSystemPasswordChar='true' />" +
                "</Panel>";

            XamlRuntime runtime = XamlRuntime.Load(markup, state);

            try
            {
                TextBox password = runtime.Get<TextBox>("Password");

                AssertEqual("secret", password.Text, "initial local Text value");

                state.CurrentStyle = "OtherPasswordStyle";
                runtime.ReloadBinding("Password", "Style");
                AssertEqual(
                    "secret",
                    password.Text,
                    "local Text blocks named Text style switch");

                state.CurrentStyle = String.Empty;
                runtime.ReloadBinding("Password", "Style");
                AssertEqual(
                    "secret",
                    password.Text,
                    "local Text blocks implicit Text style");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestFailedNativeSetterState()
        {
            NativeBindingState state = new NativeBindingState();
            state.Size = new Size(80, 30);
            state.FontText = "Arial, 10pt";

            const string markup =
                "<Panel Width='400' Height='200'>" +
                "  <PostCommitSetterControl Name='SizeTarget' " +
                "      Size='{Binding Size}' />" +
                "  <PostCommitSetterControl Name='FontTarget' " +
                "      Font='{Binding FontText}' />" +
                "</Panel>";

            XamlRuntime runtime = XamlRuntime.Load(markup, state);

            try
            {
                PostCommitSetterControl sizeTarget =
                    runtime.Get<PostCommitSetterControl>("SizeTarget");
                PostCommitSetterControl fontTarget =
                    runtime.Get<PostCommitSetterControl>("FontTarget");

                int sizeSetterBaseline = sizeTarget.SizeSetCount;
                sizeTarget.ThrowAfterNextSizeCommit = true;
                state.Size = new Size(120, 45);

                ExpectInvalidOperation(
                    delegate
                    {
                        runtime.ReloadBinding("SizeTarget", "Size");
                    });

                AssertEqual(new Size(80, 30), sizeTarget.Size, "failed Size rollback");
                AssertEqual(
                    sizeSetterBaseline + 2,
                    sizeTarget.SizeSetCount,
                    "failed Size assignment and compensation setter count");
                AssertEqual(
                    true,
                    GetElementInfoField(runtime, sizeTarget, "WidthExplicit"),
                    "failed Size width metadata");
                AssertEqual(
                    true,
                    GetElementInfoField(runtime, sizeTarget, "HeightExplicit"),
                    "failed Size height metadata");

                runtime.ReloadBinding("SizeTarget", "Size");
                AssertEqual(new Size(120, 45), sizeTarget.Size, "retried Size value");
                AssertEqual(
                    sizeSetterBaseline + 3,
                    sizeTarget.SizeSetCount,
                    "same Size request was retried");

                Font initialOwnedFont = fontTarget.Font;
                int fontSetterBaseline = fontTarget.FontSetCount;
                AssertSame(
                    initialOwnedFont,
                    GetOwnedPropertyValue(runtime, fontTarget, "Font"),
                    "initial converted Font ownership");

                fontTarget.ThrowAfterNextFontCommit = true;
                state.FontText = "Arial, 16pt";

                ExpectInvalidOperation(
                    delegate
                    {
                        runtime.ReloadBinding("FontTarget", "Font");
                    });

                AssertSame(initialOwnedFont, fontTarget.Font, "failed Font rollback");
                AssertSame(
                    initialOwnedFont,
                    GetOwnedPropertyValue(runtime, fontTarget, "Font"),
                    "failed Font keeps baseline ownership");
                AssertEqual(
                    fontSetterBaseline + 2,
                    fontTarget.FontSetCount,
                    "failed Font assignment and compensation setter count");
                AssertFontMetadataExplicit(runtime, fontTarget, "failed Font metadata");

                runtime.ReloadBinding("FontTarget", "Font");

                AssertEqual(16.0f, fontTarget.Font.SizeInPoints, "retried Font size");
                AssertTrue(
                    !Object.ReferenceEquals(initialOwnedFont, fontTarget.Font),
                    "retried Font replaces the converted baseline");
                AssertSame(
                    fontTarget.Font,
                    GetOwnedPropertyValue(runtime, fontTarget, "Font"),
                    "retried Font ownership follows native value");
                AssertEqual(
                    fontSetterBaseline + 3,
                    fontTarget.FontSetCount,
                    "same Font request was retried");
                AssertFontMetadataExplicit(runtime, fontTarget, "retried Font metadata");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestFailedStyleRestoreRetriesBaseline()
        {
            StyleState state = new StyleState();
            state.CurrentStyle = "RiskyStyle";
            ThrowOnceStyleControl.ThrowOnNextBaseline = false;

            const string markup =
                "<Panel>" +
                "  <Panel.Resources>" +
                "    <Style Key='RiskyStyle' TargetType='ThrowOnceStyleControl'>" +
                "      <Setter Property='RiskyValue' Value='Styled' />" +
                "    </Style>" +
                "    <Style Key='PlainStyle' TargetType='ThrowOnceStyleControl'>" +
                "      <Setter Property='BackColor' Value='White' />" +
                "    </Style>" +
                "  </Panel.Resources>" +
                "  <ThrowOnceStyleControl Name='Target' Style='{Binding CurrentStyle}' />" +
                "</Panel>";

            XamlRuntime runtime = XamlRuntime.Load(markup, state);

            try
            {
                ThrowOnceStyleControl target =
                    runtime.Get<ThrowOnceStyleControl>("Target");
                AssertEqual("Styled", target.RiskyValue, "initial styled value");

                state.CurrentStyle = "PlainStyle";
                ThrowOnceStyleControl.ThrowOnNextBaseline = true;
                ExpectInvalidOperation(
                    delegate
                    {
                        runtime.ReloadBinding("Target", "Style");
                    });

                AssertEqual("Styled", target.RiskyValue, "failed restore leaves style value");

                runtime.ReloadBinding("Target", "Style");
                AssertEqual("Baseline", target.RiskyValue, "second restore retries baseline");
            }
            finally
            {
                ThrowOnceStyleControl.ThrowOnNextBaseline = false;
                runtime.Dispose();
            }
        }

        private static void TestFailedDependentStyleRestore()
        {
            StyleState state = new StyleState();
            state.CurrentStyle = "LayeredSize";
            ThrowOnceSizeControl.ThrowOnNextBaseline = false;

            const string markup =
                "<Panel>" +
                "  <Panel.Resources>" +
                "    <Style Key='LayeredSize' TargetType='ThrowOnceSizeControl'>" +
                "      <Setter Property='Width' Value='100' />" +
                "      <Setter Property='Size' Value='200, 40' />" +
                "    </Style>" +
                "    <Style Key='PlainSize' TargetType='ThrowOnceSizeControl'>" +
                "      <Setter Property='BackColor' Value='White' />" +
                "    </Style>" +
                "  </Panel.Resources>" +
                "  <ThrowOnceSizeControl Name='Target' Style='{Binding CurrentStyle}' />" +
                "</Panel>";

            XamlRuntime runtime = XamlRuntime.Load(markup, state);

            try
            {
                ThrowOnceSizeControl target =
                    runtime.Get<ThrowOnceSizeControl>("Target");
                AssertEqual(200, target.Width, "active composite style width");

                state.CurrentStyle = "PlainSize";
                ThrowOnceSizeControl.ThrowOnNextBaseline = true;
                ExpectInvalidOperation(
                    delegate
                    {
                        runtime.ReloadBinding("Target", "Style");
                    });

                runtime.ReloadBinding("Target", "Style");
                AssertEqual(
                    50,
                    target.Width,
                    "retry restores the true pre-style width");
            }
            finally
            {
                ThrowOnceSizeControl.ThrowOnNextBaseline = false;
                runtime.Dispose();
            }
        }

        private static void TestFieldBackedStyleValuesRestore()
        {
            StyleState state = new StyleState();
            state.StackStyle = "HorizontalStack";
            state.DockStyle = "NoFillDock";
            state.BorderStyle = "ThickBorder";

            const string markup =
                "<Panel>" +
                "  <Panel.Resources>" +
                "    <Style Key='HorizontalStack' TargetType='StackPanel'>" +
                "      <Setter Property='Orientation' Value='Horizontal' />" +
                "    </Style>" +
                "    <Style Key='PlainStack' TargetType='StackPanel'>" +
                "      <Setter Property='Background' Value='White' />" +
                "    </Style>" +
                "    <Style Key='NoFillDock' TargetType='DockPanel'>" +
                "      <Setter Property='LastChildFill' Value='false' />" +
                "    </Style>" +
                "    <Style Key='PlainDock' TargetType='DockPanel'>" +
                "      <Setter Property='Background' Value='White' />" +
                "    </Style>" +
                "    <Style Key='ThickBorder' TargetType='Border'>" +
                "      <Setter Property='BorderThickness' Value='5' />" +
                "      <Setter Property='BorderBrush' Value='Red' />" +
                "    </Style>" +
                "    <Style Key='PlainBorder' TargetType='Border'>" +
                "      <Setter Property='Background' Value='White' />" +
                "    </Style>" +
                "  </Panel.Resources>" +
                "  <StackPanel Name='Stack' Style='{Binding StackStyle}' />" +
                "  <DockPanel Name='Dock' Style='{Binding DockStyle}' />" +
                "  <Border Name='Border' Style='{Binding BorderStyle}' />" +
                "</Panel>";

            XamlRuntime runtime = XamlRuntime.Load(markup, state);

            try
            {
                Control stack = runtime.Get<Control>("Stack");
                Control dock = runtime.Get<Control>("Dock");
                Control border = runtime.Get<Control>("Border");

                AssertEqual(
                    Orientation.Horizontal,
                    GetInstanceField(stack, "StackOrientation"),
                    "initial stack orientation");
                AssertEqual(false, GetInstanceField(dock, "LastChildFill"), "initial dock fill");
                AssertEqual(new Padding(5), GetInstanceField(border, "BorderThickness"), "initial border thickness");
                AssertEqual(Color.Red, GetInstanceField(border, "BorderColor"), "initial border color");

                state.StackStyle = "PlainStack";
                state.DockStyle = "PlainDock";
                state.BorderStyle = "PlainBorder";
                runtime.ReloadBinding("Stack", "Style");
                runtime.ReloadBinding("Dock", "Style");
                runtime.ReloadBinding("Border", "Style");

                AssertEqual(
                    Orientation.Vertical,
                    GetInstanceField(stack, "StackOrientation"),
                    "restored stack orientation");
                AssertEqual(true, GetInstanceField(dock, "LastChildFill"), "restored dock fill");
                AssertEqual(new Padding(1), GetInstanceField(border, "BorderThickness"), "restored border thickness");
                AssertEqual(SystemColors.ControlDark, GetInstanceField(border, "BorderColor"), "restored border color");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestVisibilityAliasLayering()
        {
            const string markup =
                "<StackPanel Width='120' Height='80'>" +
                "  <StackPanel.Resources>" +
                "    <Style TargetType='Label'>" +
                "      <Setter Property='Visibility' Value='Collapsed' />" +
                "    </Style>" +
                "    <Style Key='VisibleLabel' TargetType='Label'>" +
                "      <Setter Property='Visible' Value='true' />" +
                "    </Style>" +
                "  </StackPanel.Resources>" +
                "  <Label Name='Caption' Style='VisibleLabel' Width='60' Height='20' />" +
                "</StackPanel>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                runtime.RootControl.PerformLayout();
                Label caption = runtime.Get<Label>("Caption");

                AssertTrue(caption.Visible, "derived Visible setter");
                AssertTrue(!caption.Bounds.IsEmpty, "visible alias participates in layout");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestSizeAxisStylePrecedence()
        {
            StyleState state = new StyleState();
            state.CurrentStyle = "FirstSize";

            const string markup =
                "<Panel>" +
                "  <Panel.Resources>" +
                "    <Style Key='FirstSize' TargetType='Label'>" +
                "      <Setter Property='MinWidth' Value='10' />" +
                "      <Setter Property='MinHeight' Value='20' />" +
                "    </Style>" +
                "    <Style Key='SecondSize' TargetType='Label'>" +
                "      <Setter Property='MinWidth' Value='15' />" +
                "      <Setter Property='MinHeight' Value='30' />" +
                "    </Style>" +
                "  </Panel.Resources>" +
                "  <Label Name='AxisLocal' Style='{Binding CurrentStyle}' MinWidth='100' />" +
                "  <Label Name='CompositeLocal' Style='{Binding CurrentStyle}' MinimumSize='90,40' />" +
                "</Panel>";

            XamlRuntime runtime = XamlRuntime.Load(markup, state);

            try
            {
                Label axisLocal = runtime.Get<Label>("AxisLocal");
                Label compositeLocal = runtime.Get<Label>("CompositeLocal");

                AssertEqual(new Size(100, 20), axisLocal.MinimumSize, "initial independent axes");
                AssertEqual(new Size(90, 40), compositeLocal.MinimumSize, "initial composite local value");

                state.CurrentStyle = "SecondSize";
                runtime.ReloadBinding("AxisLocal", "Style");
                runtime.ReloadBinding("CompositeLocal", "Style");

                AssertEqual(new Size(100, 30), axisLocal.MinimumSize, "local width after style switch");
                AssertEqual(new Size(90, 40), compositeLocal.MinimumSize, "composite local after style switch");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestCompositeSizeAndOrientationPrecedence()
        {
            StyleState state = new StyleState();
            state.CurrentStyle = "CompositeSize";
            state.SecondaryStyle = "WidthOnly";
            state.StackStyle = "XamlOrientation";
            state.DockStyle = "NativeOrientation";

            const string markup =
                "<Panel>" +
                "  <Panel.Resources>" +
                "    <Style Key='CompositeSize' TargetType='Label'>" +
                "      <Setter Property='Size' Value='200,50' />" +
                "    </Style>" +
                "    <Style Key='WidthOnly' TargetType='Label'>" +
                "      <Setter Property='Width' Value='250' />" +
                "    </Style>" +
                "    <Style Key='XamlOrientation' TargetType='FlexPanel'>" +
                "      <Setter Property='Orientation' Value='Horizontal' />" +
                "    </Style>" +
                "    <Style Key='NativeOrientation' TargetType='FlexPanel'>" +
                "      <Setter Property='Direction' Value='Row' />" +
                "    </Style>" +
                "  </Panel.Resources>" +
                "  <Label Name='WidthLocal' Style='{Binding CurrentStyle}' Width='123' />" +
                "  <Label Name='SizeLocal' Style='{Binding SecondaryStyle}' Size='90,40' />" +
                "  <FlexPanel Name='NativeLocal' Style='{Binding StackStyle}' Direction='Column' />" +
                "  <FlexPanel Name='XamlLocal' Style='{Binding DockStyle}' Orientation='Vertical' />" +
                "</Panel>";

            XamlRuntime runtime = XamlRuntime.Load(markup, state);

            try
            {
                Label widthLocal = runtime.Get<Label>("WidthLocal");
                Label sizeLocal = runtime.Get<Label>("SizeLocal");
                XamlRuntime.FlexPanel nativeLocal =
                    runtime.Get<XamlRuntime.FlexPanel>("NativeLocal");
                XamlRuntime.FlexPanel xamlLocal =
                    runtime.Get<XamlRuntime.FlexPanel>("XamlLocal");

                runtime.ReloadBinding("WidthLocal", "Style");
                runtime.ReloadBinding("SizeLocal", "Style");
                runtime.ReloadBinding("NativeLocal", "Style");
                runtime.ReloadBinding("XamlLocal", "Style");

                AssertEqual(123, widthLocal.Width, "local Width against style Size");
                AssertEqual(new Size(90, 40), sizeLocal.Size, "local Size against style Width");
                AssertEqual(
                    XamlRuntime.FlexDirection.Column,
                    nativeLocal.Direction,
                    "local Direction against style Orientation");
                AssertEqual(
                    XamlRuntime.FlexDirection.Column,
                    xamlLocal.Direction,
                    "local Orientation against style Direction");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestStyleEventReplacement()
        {
            EventState state = new EventState();
            state.CurrentStyle = "ActionStyle";

            const string markup =
                "<Panel>" +
                "  <Panel.Resources>" +
                "    <Style Key='ActionStyle' TargetType='Button'>" +
                "      <Setter Property='Click' Value='Action_Click' />" +
                "    </Style>" +
                "  </Panel.Resources>" +
                "  <Button Name='Action' Style='{Binding CurrentStyle}' />" +
                "</Panel>";

            XamlRuntime runtime = XamlRuntime.Load(markup, state);
            Button action = runtime.Get<Button>("Action");

            runtime.ReloadBinding("Action", "Style");
            runtime.ReloadBinding("Action", "Style");
            action.PerformClick();

            AssertEqual(1, state.ClickCount, "single attached event handler");

            runtime.Dispose();
            action.PerformClick();
            AssertEqual(1, state.ClickCount, "handler detached on dispose");
        }

        private static void TestStyleSwitchDetachesOmittedEvent()
        {
            EventState state = new EventState();
            state.CurrentStyle = "ActionStyle";

            const string markup =
                "<Panel>" +
                "  <Panel.Resources>" +
                "    <Style Key='ActionStyle' TargetType='Button'>" +
                "      <Setter Property='Click' Value='Action_Click' />" +
                "    </Style>" +
                "    <Style Key='PlainStyle' TargetType='Button'>" +
                "      <Setter Property='Text' Value='Plain' />" +
                "    </Style>" +
                "  </Panel.Resources>" +
                "  <Button Name='Action' Style='{Binding CurrentStyle}' />" +
                "</Panel>";

            XamlRuntime runtime = XamlRuntime.Load(markup, state);

            try
            {
                Button action = runtime.Get<Button>("Action");
                action.PerformClick();
                AssertEqual(1, state.ClickCount, "initial styled event handler");

                state.CurrentStyle = "PlainStyle";
                runtime.ReloadBinding("Action", "Style");
                action.PerformClick();

                AssertEqual(
                    1,
                    state.ClickCount,
                    "event omitted by replacement style");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestDynamicStyleEventSetter()
        {
            EventState state = new EventState();
            state.HandlerName = "Action_Click";

            const string markup =
                "<Panel>" +
                "  <Panel.Resources>" +
                "    <Style Key='ActionStyle' TargetType='Button'>" +
                "      <Setter Property='Click' Value='{Binding HandlerName}' />" +
                "    </Style>" +
                "  </Panel.Resources>" +
                "  <Button Name='Action' Style='ActionStyle' />" +
                "</Panel>";

            XamlRuntime runtime = XamlRuntime.Load(markup, state);

            try
            {
                Button action = runtime.Get<Button>("Action");
                action.PerformClick();
                AssertEqual(1, state.ClickCount, "initial dynamic event handler");

                state.HandlerName = "Alternate_Click";
                runtime.ReloadBinding("Action", "Click");
                action.PerformClick();

                AssertEqual(1, state.ClickCount, "old dynamic event detached");
                AssertEqual(1, state.AlternateClickCount, "replacement dynamic event attached");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestStyleSwitchPreservesExternalEvent()
        {
            EventState state = new EventState();
            state.CurrentStyle = "ActionStyle";

            const string markup =
                "<Panel>" +
                "  <Panel.Resources>" +
                "    <Style Key='ActionStyle' TargetType='Button'>" +
                "      <Setter Property='Click' Value='Action_Click' />" +
                "    </Style>" +
                "    <Style Key='PlainStyle' TargetType='Button'>" +
                "      <Setter Property='Text' Value='Plain' />" +
                "    </Style>" +
                "  </Panel.Resources>" +
                "  <Button Name='Action' Style='{Binding CurrentStyle}' />" +
                "</Panel>";

            XamlRuntime runtime = XamlRuntime.Load(markup, state);
            Button action = runtime.Get<Button>("Action");
            int externalClickCount = 0;
            EventHandler externalHandler =
                delegate
                {
                    externalClickCount++;
                };

            action.Click += externalHandler;

            try
            {
                action.PerformClick();
                AssertEqual(1, state.ClickCount, "initial style Click handler");
                AssertEqual(1, externalClickCount, "initial external Click handler");

                state.CurrentStyle = "PlainStyle";
                runtime.ReloadBinding("Action", "Style");
                action.PerformClick();

                AssertEqual(
                    1,
                    state.ClickCount,
                    "removed style Click handler");
                AssertEqual(
                    2,
                    externalClickCount,
                    "preserved external Click handler");
            }
            finally
            {
                action.Click -= externalHandler;
                runtime.Dispose();
            }
        }

        private static void TestStyleSwitchPreservesSameHandlerLocalEvent()
        {
            EventState state = new EventState();
            state.CurrentStyle = "ActionStyle";

            const string markup =
                "<Panel>" +
                "  <Panel.Resources>" +
                "    <Style Key='ActionStyle' TargetType='Button'>" +
                "      <Setter Property='Click' Value='Action_Click' />" +
                "    </Style>" +
                "    <Style Key='PlainStyle' TargetType='Button'>" +
                "      <Setter Property='Text' Value='Plain' />" +
                "    </Style>" +
                "  </Panel.Resources>" +
                "  <Button Name='Action' Style='{Binding CurrentStyle}' " +
                "          Click='Action_Click' />" +
                "</Panel>";

            XamlRuntime runtime = XamlRuntime.Load(markup, state);

            try
            {
                Button action = runtime.Get<Button>("Action");
                ReplaceBoundEventForTest(
                    runtime,
                    action,
                    "Click",
                    new EventHandler(state.Action_Click),
                    true);
                ArrayList registrations =
                    GetInstanceField(runtime, "_boundEvents") as ArrayList;
                AssertTrue(
                    registrations != null && registrations.Count == 1,
                    "matching local and style event share one registration");
                AssertEqual(
                    true,
                    GetInstanceField(registrations[0], "LocalOwner"),
                    "matching event keeps local ownership");
                AssertEqual(
                    true,
                    GetInstanceField(registrations[0], "StyleOwner"),
                    "matching event keeps style ownership");
                action.PerformClick();
                AssertEqual(1, state.ClickCount, "shared handler attached once");

                state.CurrentStyle = "PlainStyle";
                runtime.ReloadBinding("Action", "Style");
                action.PerformClick();

                AssertEqual(
                    2,
                    state.ClickCount,
                    "local event survives removal of its matching style claim");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestRuntimeOwnsEventRegistration()
        {
            EventState state = new EventState();

            XamlRuntime runtime = XamlRuntime.Load(
                "<Button Name='Action' Click='Action_Click' />",
                state);

            try
            {
                FieldInfo eventsField = typeof(XamlRuntime).GetField(
                    "_boundEvents",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                AssertTrue(eventsField != null, "bound event list field found");

                System.Collections.ArrayList registrations =
                    eventsField.GetValue(runtime) as System.Collections.ArrayList;

                AssertTrue(
                    registrations != null && registrations.Count == 1,
                    "one runtime event registration");

                object registration = registrations[0];
                Type registrationType = registration.GetType();
                Delegate ownedHandler = registrationType
                    .GetField("Handler")
                    .GetValue(registration) as Delegate;
                Delegate sourceHandler = registrationType
                    .GetField("SourceHandler")
                    .GetValue(registration) as Delegate;
                EventHandler expectedSource = state.Action_Click;

                AssertTrue(
                    sourceHandler != null && sourceHandler.Equals(expectedSource),
                    "source handler retained for reload comparison");
                AssertTrue(
                    ownedHandler != null && !ownedHandler.Equals(sourceHandler),
                    "runtime registration has unique delegate ownership");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestBoundEventTargetIndexUsesReferenceIdentity()
        {
            EventState state = new EventState();
            XamlRuntime runtime = XamlRuntime.Load("<Panel />", state);
            ReferenceIdentityEventControl first =
                new ReferenceIdentityEventControl();
            ReferenceIdentityEventControl second =
                new ReferenceIdentityEventControl();

            try
            {
                ReplaceBoundEventForTest(
                    runtime,
                    first,
                    "Triggered",
                    new EventHandler(state.Action_Click));
                ReplaceBoundEventForTest(
                    runtime,
                    second,
                    "Triggered",
                    new EventHandler(state.Alternate_Click));

                IDictionary targetIndex =
                    GetInstanceField(runtime, "_boundEventsByTarget")
                        as IDictionary;

                AssertTrue(targetIndex != null, "bound event target index found");
                AssertEqual(2, targetIndex.Count, "equal targets have distinct buckets");
                AssertTrue(
                    !Object.ReferenceEquals(targetIndex[first], targetIndex[second]),
                    "target buckets use reference identity");

                ArrayList firstRegistrations =
                    GetInstanceField(targetIndex[first], "Registrations")
                        as ArrayList;
                ArrayList secondRegistrations =
                    GetInstanceField(targetIndex[second], "Registrations")
                        as ArrayList;

                AssertEqual(1, firstRegistrations.Count, "first target bucket count");
                AssertEqual(1, secondRegistrations.Count, "second target bucket count");

                first.RaiseTriggered();
                second.RaiseTriggered();
                AssertEqual(1, state.ClickCount, "first target handler isolated");
                AssertEqual(1, state.AlternateClickCount, "second target handler isolated");

                ReplaceBoundEventForTest(
                    runtime,
                    first,
                    "Triggered",
                    new EventHandler(state.Alternate_Click));
                first.RaiseTriggered();

                AssertEqual(1, state.ClickCount, "replaced first handler detached");
                AssertEqual(2, state.AlternateClickCount, "first replacement attached");
                AssertEqual(2, targetIndex.Count, "replacement reuses target bucket");

                ReleaseBoundEventsForTest(runtime, first);
                AssertEqual(1, targetIndex.Count, "released target bucket removed");
                AssertEqual(null, targetIndex[first], "released target no longer indexed");
                AssertTrue(targetIndex[second] != null, "other target remains indexed");

                first.RaiseTriggered();
                second.RaiseTriggered();
                AssertEqual(1, state.ClickCount, "released first target stays detached");
                AssertEqual(3, state.AlternateClickCount, "second target remains attached");
            }
            finally
            {
                runtime.Dispose();
                first.Dispose();
                second.Dispose();
            }
        }

        private static void TestFailedCustomEventAdd()
        {
            EventState state = new EventState();
            ThrowingEventControl.LastInstance = null;
            ThrowingEventControl.ThrowAfterAdd = true;
            ThrowingEventControl.ThrowOnRemove = true;

            try
            {
                Exception addError = null;

                try
                {
                    XamlRuntime.Load(
                        "<ThrowingEventControl Triggered='Action_Click' />",
                        state);
                }
                catch (Exception ex)
                {
                    addError = ex;
                }

                AssertTrue(addError != null, "custom event add failure surfaced");

                AssertTrue(
                    ThrowingEventControl.LastInstance != null,
                    "custom event control was created");
                ThrowingEventControl.LastInstance.RaiseTriggered();
                AssertEqual(
                    0,
                    state.ClickCount,
                    "failed registration forwarder is disabled");
            }
            finally
            {
                ThrowingEventControl.ThrowAfterAdd = false;
                ThrowingEventControl.ThrowOnRemove = false;
                ThrowingEventControl.LastInstance = null;
            }
        }

        private static void TestCustomEventAccessorReentry()
        {
            EventState state = new EventState();
            XamlRuntime runtime = XamlRuntime.Load(
                "<AccessorEventControl Name='Target' " +
                "Primary='Action_Click' Secondary='Secondary_Click' />",
                state);

            try
            {
                AccessorEventControl target =
                    runtime.Get<AccessorEventControl>("Target");
                EventHandler action = new EventHandler(state.Action_Click);
                EventHandler alternate =
                    new EventHandler(state.Alternate_Click);
                EventHandler secondary =
                    new EventHandler(state.Secondary_Click);
                EventHandler secondaryAlternate =
                    new EventHandler(state.SecondaryAlternate_Click);

                // The in-flight candidate is already published when its custom
                // add accessor runs. Re-adding the same source handler is a no-op.
                target.PrimaryAddCallback =
                    delegate
                    {
                        ReplaceBoundEventForTest(
                            runtime,
                            target,
                            "Primary",
                            alternate);
                    };

                ReplaceBoundEventForTest(
                    runtime,
                    target,
                    "Primary",
                    alternate);

                target.RaisePrimary();
                AssertEqual(0, state.ClickCount, "same-add old handler count");
                AssertEqual(1, state.AlternateClickCount, "same-add handler count");
                AssertEqual(2, target.PrimaryAddCount, "same add did not duplicate");

                // A request made by the old remove accessor is newer than the
                // outer candidate and must remain installed after the outer call.
                target.PrimaryRemoveCallback =
                    delegate
                    {
                        ReplaceBoundEventForTest(
                            runtime,
                            target,
                            "Primary",
                            alternate);
                    };

                ReplaceBoundEventForTest(
                    runtime,
                    target,
                    "Primary",
                    action);

                target.RaisePrimary();
                AssertEqual(0, state.ClickCount, "stale outer handler was not installed");
                AssertEqual(2, state.AlternateClickCount, "newer remove handler wins");

                // Mutating another event during both remove and add must not
                // invalidate the independent Primary candidate.
                target.PrimaryRemoveCallback =
                    delegate
                    {
                        ReplaceBoundEventForTest(
                            runtime,
                            target,
                            "Secondary",
                            secondaryAlternate);
                    };
                target.PrimaryAddCallback =
                    delegate
                    {
                        ReplaceBoundEventForTest(
                            runtime,
                            target,
                            "Secondary",
                            secondary);
                    };

                ReplaceBoundEventForTest(
                    runtime,
                    target,
                    "Primary",
                    action);

                target.RaisePrimary();
                target.RaiseSecondary();

                AssertEqual(1, state.ClickCount, "Primary survives unrelated mutation");
                AssertEqual(
                    2,
                    state.AlternateClickCount,
                    "replaced Primary handler remains detached");
                AssertEqual(
                    1,
                    state.SecondaryClickCount,
                    "latest unrelated handler installed");
                AssertEqual(
                    0,
                    state.SecondaryAlternateClickCount,
                    "intermediate unrelated handler detached");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestDisposeInsideEventAdd()
        {
            EventState state = new EventState();
            XamlRuntime runtime = XamlRuntime.Load(
                "<AccessorEventControl Name='Target' Primary='Action_Click' />",
                state);
            AccessorEventControl target =
                runtime.Get<AccessorEventControl>("Target");

            target.PrimaryAddCallback =
                delegate
                {
                    runtime.Dispose();
                };

            try
            {
                ReplaceBoundEventForTest(
                    runtime,
                    target,
                    "Primary",
                    new EventHandler(state.Alternate_Click));
            }
            catch (ObjectDisposedException)
            {
                // Either completion behavior is acceptable; the registration
                // must be inactive once the accessor-triggered Dispose returns.
            }

            AssertTrue(runtime.IsDisposed, "runtime disposed inside add accessor");

            target.RaisePrimary();
            AssertEqual(0, state.ClickCount, "old handler detached by disposal");
            AssertEqual(0, state.AlternateClickCount, "in-flight handler detached by disposal");

            runtime.Dispose();
        }

        private static void TestFailedEventRemoveRetry()
        {
            EventState state = new EventState();
            XamlRuntime runtime = XamlRuntime.Load(
                "<AccessorEventControl Name='Target' " +
                "Secondary='Secondary_Click' Primary='Action_Click' />",
                state);
            AccessorEventControl target =
                runtime.Get<AccessorEventControl>("Target");

            target.PrimaryRemoveFailuresRemaining = 1;
            runtime.Dispose();

            AssertEqual(1, target.PrimaryRemoveCount, "first remove was attempted");
            AssertEqual(
                1,
                target.SecondaryRemoveCount,
                "later event removals continue after one remover fails");
            target.RaisePrimary();
            AssertEqual(0, state.ClickCount, "failed removal forwarder is disabled");
            target.RaiseSecondary();
            AssertEqual(
                0,
                state.SecondaryClickCount,
                "later event registration was detached");

            ArrayList retryDebt =
                GetInstanceField(runtime, "_boundEvents") as ArrayList;
            AssertTrue(
                retryDebt != null && retryDebt.Count == 1,
                "failed removal remains tracked as retry debt");
            object registration = retryDebt[0];
            AssertEqual(
                null,
                GetInstanceField(registration, "SourceHandler"),
                "disabled debt releases its source handler");
            AssertEqual(
                null,
                GetInstanceField(registration, "Forwarder"),
                "disabled debt releases its forwarder field");
            Delegate removalHandler =
                GetInstanceField(registration, "Handler") as Delegate;
            AssertTrue(removalHandler != null, "removal delegate retained");
            AssertEqual(
                null,
                GetInstanceField(removalHandler.Target, "_handler"),
                "retained removal delegate releases code-behind target");

            Exception crossThreadRetryFailure = null;
            Thread crossThreadRetry =
                new Thread(
                    delegate()
                    {
                        try
                        {
                            runtime.Dispose();
                        }
                        catch (Exception ex)
                        {
                            crossThreadRetryFailure = ex;
                        }
                    });
            crossThreadRetry.Start();
            crossThreadRetry.Join();

            AssertTrue(
                crossThreadRetryFailure is InvalidOperationException,
                "retained event-removal debt stays owner-thread-affine");
            AssertEqual(
                1,
                target.PrimaryRemoveCount,
                "a rejected cross-thread retry does not consume removal debt");

            runtime.Dispose();

            AssertEqual(2, target.PrimaryRemoveCount, "second Dispose retries remove");
            target.RaisePrimary();
            AssertEqual(0, state.ClickCount, "retried registration remains inactive");

            ArrayList registrations =
                GetInstanceField(runtime, "_boundEvents") as ArrayList;
            AssertTrue(
                registrations == null || registrations.Count == 0,
                "successful retry clears event registration debt");
        }

        private static void TestTargetReleaseRejectsReentrantEvent()
        {
            EventState state = new EventState();
            XamlRuntime runtime = XamlRuntime.Load(
                "<AccessorEventControl Name='Target' Primary='Action_Click' />",
                state);
            AccessorEventControl target =
                runtime.Get<AccessorEventControl>("Target");

            try
            {
                target.PrimaryRemoveCallback =
                    delegate
                    {
                        ReplaceBoundEventForTest(
                            runtime,
                            target,
                            "Primary",
                            new EventHandler(state.Alternate_Click));
                    };

                ReleaseBoundEventsForTest(runtime, target);
                target.RaisePrimary();

                AssertEqual(0, state.ClickCount, "released local handler detached");
                AssertEqual(
                    0,
                    state.AlternateClickCount,
                    "remove callback could not publish a replacement");

                ArrayList registrations =
                    GetInstanceField(runtime, "_boundEvents") as ArrayList;

                if (registrations != null)
                {
                    int i;

                    for (i = 0; i < registrations.Count; i++)
                    {
                        object registration = registrations[i];

                        AssertTrue(
                            !Object.ReferenceEquals(
                                GetInstanceField(registration, "Target"),
                                target),
                            "released target has no retained registration");
                    }
                }
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestFailedChildAttachmentDisposesChild()
        {
            TrackedAttachmentChild.DisposeCount = 0;

            ExpectInvalidOperation(
                delegate
                {
                    XamlRuntime.Load(
                        "<ThrowingAttachmentHost>" +
                        "  <TrackedAttachmentChild />" +
                        "</ThrowingAttachmentHost>");
                });

            AssertEqual(
                1,
                TrackedAttachmentChild.DisposeCount,
                "unattached child disposal count");
        }

        private static void TestLegacyMarqueeCapabilityMatrix()
        {
            AssertTrue(
                typeof(CompatibleProgressBar).GetProperty(
                    "Style",
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.DeclaredOnly) == null,
                "fallback does not hide ProgressBar.Style");
            AssertTrue(
                typeof(CompatibleProgressBar).GetProperty(
                    "MarqueeAnimationSpeed",
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.DeclaredOnly) == null,
                "fallback does not hide ProgressBar.MarqueeAnimationSpeed");
            PropertyInfo forceFallback =
                typeof(CompatibleProgressBar).GetProperty(
                    "PreferMarqueeFallback",
                    BindingFlags.Instance | BindingFlags.Public);
            AssertTrue(
                forceFallback != null &&
                forceFallback.PropertyType == typeof(bool),
                "fallback exposes the canonical force switch");
            AssertTrue(
                typeof(CompatibleProgressBar).GetProperty(
                    "LegacyMode",
                    BindingFlags.Instance | BindingFlags.Public) == null,
                "fallback does not retain the removed LegacyMode syntax");
            AssertTrue(
                typeof(CompatibleProgressBar).GetProperty(
                    "PrefectMarqueeFallback",
                    BindingFlags.Instance | BindingFlags.Public) == null,
                "fallback does not retain the misspelled force switch");

            MethodInfo requiresFallback =
                typeof(CompatibleProgressBar).GetMethod(
                    "RequiresLegacyRenderer",
                    BindingFlags.Static | BindingFlags.NonPublic);

            AssertTrue(requiresFallback != null, "capability helper found");

            AssertEqual(
                true,
                (bool)requiresFallback.Invoke(
                    null,
                    new object[] { PlatformID.Win32Windows, 4, 10, true }),
                "Windows 98 fallback");
            AssertEqual(
                true,
                (bool)requiresFallback.Invoke(
                    null,
                    new object[] { PlatformID.Win32Windows, 4, 90, true }),
                "Windows Me fallback");
            AssertEqual(
                true,
                (bool)requiresFallback.Invoke(
                    null,
                    new object[] { PlatformID.Win32NT, 5, 0, true }),
                "Windows 2000 fallback");
            AssertEqual(
                true,
                (bool)requiresFallback.Invoke(
                    null,
                    new object[] { PlatformID.Win32NT, 5, 1, false }),
                "XP without client visual styles fallback");
            AssertEqual(
                false,
                (bool)requiresFallback.Invoke(
                    null,
                    new object[] { PlatformID.Win32NT, 5, 1, true }),
                "XP rendering with visual styles uses native marquee");
            AssertEqual(
                true,
                (bool)requiresFallback.Invoke(
                    null,
                    new object[] { PlatformID.Win32NT, 10, 0, false }),
                "modern Windows without client visual styles fallback");
            AssertEqual(
                false,
                (bool)requiresFallback.Invoke(
                    null,
                    new object[] { PlatformID.Win32NT, 10, 0, true }),
                "modern Windows rendering with visual styles uses native marquee");
        }

        private static void TestLegacyMarqueeFrameMapping()
        {
            AssertLegacyMarqueeFrame(0, false, 0, 0, 0);
            AssertLegacyMarqueeFrame(10, false, 0, 0, 116);
            AssertLegacyMarqueeFrame(11, false, 0, 12, 104);
            AssertLegacyMarqueeFrame(20, false, 0, 0, 0);
            AssertLegacyMarqueeFrame(21, false, 0, 0, 0);

            AssertLegacyMarqueeFrame(0, true, 0, 0, 0);
            AssertLegacyMarqueeFrame(10, true, 0, 0, 116);
            AssertLegacyMarqueeFrame(11, true, 0, 0, 104);
            AssertLegacyMarqueeFrame(20, true, 0, 0, 0);

            AssertLegacyMarqueeFrame(
                1,
                false,
                1000004,
                20,
                0,
                0,
                10000);
            AssertLegacyMarqueeFrame(
                101,
                false,
                1000004,
                20,
                0,
                10000,
                990000);
            AssertLegacyMarqueeFrame(
                199,
                false,
                1000004,
                20,
                0,
                990000,
                10000);
            AssertLegacyMarqueeFrame(
                200,
                false,
                1000004,
                20,
                0,
                0,
                0);

            MethodInfo nextFrame =
                typeof(CompatibleProgressBar).GetMethod(
                    "GetNextLegacyFrame",
                    BindingFlags.Static | BindingFlags.NonPublic);
            AssertTrue(nextFrame != null, "legacy frame advance helper found");
            AssertEqual(
                20,
                nextFrame.Invoke(null, new object[] { 19, 20 }),
                "last drain frame remains visible for one interval");
            AssertEqual(
                1,
                nextFrame.Invoke(null, new object[] { 20, 20 }),
                "repeated cycle skips the duplicate empty frame");

            MethodInfo getTimerInterval =
                typeof(CompatibleProgressBar).GetMethod(
                    "GetLegacyTimerInterval",
                    BindingFlags.Static | BindingFlags.NonPublic);
            AssertTrue(
                getTimerInterval != null,
                "legacy timer interval helper found");
            AssertEqual(
                105,
                getTimerInterval.Invoke(null, new object[] { 35 }),
                "fallback advances at one third requested cadence");
            AssertEqual(
                250,
                getTimerInterval.Invoke(null, new object[] { 0 }),
                "paused fallback retains a valid disabled timer interval");
            AssertEqual(
                Int32.MaxValue,
                getTimerInterval.Invoke(
                    null,
                    new object[] { Int32.MaxValue }),
                "fallback interval multiplication cannot overflow");

            MethodInfo createMaskRegion =
                typeof(CompatibleProgressBar).GetMethod(
                    "CreateLegacyMaskRegion",
                    BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo deleteObject =
                typeof(CompatibleProgressBar).GetMethod(
                    "DeleteObject",
                    BindingFlags.Static | BindingFlags.NonPublic);
            AssertTrue(
                createMaskRegion != null && deleteObject != null,
                "legacy mask region helpers found");

            IntPtr maskRegion =
                (IntPtr)createMaskRegion.Invoke(
                    null,
                    new object[] { 12, 30, 120, 20 });

            try
            {
                using (Region region = Region.FromHrgn(maskRegion))
                {
                    AssertEqual(
                        false,
                        region.IsVisible(60, 0),
                        "filled overlay excludes the native top border");
                    AssertEqual(
                        false,
                        region.IsVisible(60, 19),
                        "filled overlay excludes the native bottom border");
                    AssertEqual(
                        false,
                        region.IsVisible(0, 10),
                        "filled overlay excludes the native leading border");
                    AssertEqual(
                        false,
                        region.IsVisible(119, 10),
                        "filled overlay excludes the native trailing border");
                    AssertEqual(
                        false,
                        region.IsVisible(13, 10),
                        "filled overlay excludes pixels before its range");
                    AssertEqual(
                        true,
                        region.IsVisible(20, 10),
                        "filled overlay includes its requested track range");
                    AssertEqual(
                        false,
                        region.IsVisible(44, 10),
                        "filled overlay excludes pixels after its range");
                }
            }
            finally
            {
                deleteObject.Invoke(null, new object[] { maskRegion });
            }
        }

        private static void AssertLegacyMarqueeFrame(
            int frame,
            bool rightToLeft,
            int expectedParentPosition,
            int expectedMaskOffset,
            int expectedMaskWidth)
        {
            AssertLegacyMarqueeFrame(
                frame,
                rightToLeft,
                120,
                20,
                expectedParentPosition,
                expectedMaskOffset,
                expectedMaskWidth);
        }

        private static void AssertLegacyMarqueeFrame(
            int frame,
            bool rightToLeft,
            int clientWidth,
            int clientHeight,
            int expectedParentPosition,
            int expectedMaskOffset,
            int expectedMaskWidth)
        {
            MethodInfo calculate =
                typeof(CompatibleProgressBar).GetMethod(
                    "CalculateLegacyFrame",
                    BindingFlags.Static | BindingFlags.NonPublic);
            AssertTrue(calculate != null, "legacy frame calculator found");

            object[] arguments =
                new object[]
                {
                    frame,
                    rightToLeft,
                    clientWidth,
                    clientHeight,
                    0,
                    0,
                    0
                };
            calculate.Invoke(null, arguments);

            AssertEqual(
                expectedParentPosition,
                arguments[4],
                "legacy frame parent position");
            AssertEqual(
                expectedMaskOffset,
                arguments[5],
                "legacy frame mask offset");
            AssertEqual(
                expectedMaskWidth,
                arguments[6],
                "legacy frame mask width");
        }

        private static void TestLegacyMarqueeState()
        {
            using (CompatibleProgressBar progress =
                new CompatibleProgressBar())
            {
                AssertEqual(
                    null,
                    GetInstanceField(progress, "_animationTimer"),
                    "unused compatibility progress bar allocates no timer");
                ProgressBar nativeProgress = progress;
                progress.PreferMarqueeFallback = false;
                nativeProgress.Minimum = 10;
                nativeProgress.Maximum = 300;
                nativeProgress.Value = 77;
                nativeProgress.Style = ProgressBarStyle.Marquee;
                nativeProgress.MarqueeAnimationSpeed = 47;

                MethodInfo detectAutomaticFallback =
                    typeof(CompatibleProgressBar).GetMethod(
                        "DetectNativeMarqueeUnavailable",
                        BindingFlags.Static | BindingFlags.NonPublic);
                AssertTrue(
                    detectAutomaticFallback != null,
                    "automatic fallback capability method found");
                bool automaticFallback =
                    (bool)detectAutomaticFallback.Invoke(null, null);

                PropertyInfo createParamsProperty =
                    typeof(CompatibleProgressBar).GetProperty(
                        "CreateParams",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                AssertTrue(
                    createParamsProperty != null,
                    "compatibility control exposes CreateParams");
                CreateParams automaticParams =
                    (CreateParams)createParamsProperty.GetValue(
                        progress,
                        null);

                if (!automaticFallback)
                {
                    AssertEqual(
                        0x00000008,
                        automaticParams.Style & 0x00000008,
                        "automatic mode leaves supported native marquee intact");
                }

                progress.PreferMarqueeFallback = true;

                AssertEqual(
                    ProgressBarStyle.Marquee,
                    nativeProgress.Style,
                    "base-typed requested style");
                AssertEqual(
                    47,
                    nativeProgress.MarqueeAnimationSpeed,
                    "base-typed requested speed");

                nativeProgress.Style = ProgressBarStyle.Marquee;
                nativeProgress.MarqueeAnimationSpeed = 0;

                AssertEqual(
                    ProgressBarStyle.Marquee,
                    nativeProgress.Style,
                    "fallback preserves the native Style property");

                nativeProgress.MarqueeAnimationSpeed = 35;
                AssertEqual(
                    35,
                    nativeProgress.MarqueeAnimationSpeed,
                    "fallback preserves the native speed property");

                bool invalidStyleRejected = false;

                try
                {
                    nativeProgress.Style = (ProgressBarStyle)999;
                }
                catch (ArgumentException)
                {
                    invalidStyleRejected = true;
                }

                AssertTrue(
                    invalidStyleRejected,
                    "invalid requested style is rejected");
                AssertEqual(
                    ProgressBarStyle.Marquee,
                    nativeProgress.Style,
                    "invalid style does not corrupt requested state");

                AssertEqual(
                    true,
                    progress.PreferMarqueeFallback,
                    "forced fallback switch remains enabled");

                CreateParams fallbackParams =
                    (CreateParams)createParamsProperty.GetValue(
                        progress,
                        null);
                AssertEqual(
                    0,
                    fallbackParams.Style & 0x00000008,
                    "fallback strips only the native marquee style bit");
                AssertEqual(
                    automaticParams.Style & ~0x00000008,
                    fallbackParams.Style & ~0x00000008,
                    "fallback preserves every unrelated native style bit");

                progress.Size = new Size(120, 20);
                progress.CreateControl();
                System.Windows.Forms.Timer animationTimer =
                    GetInstanceField(progress, "_animationTimer") as
                        System.Windows.Forms.Timer;
                AssertTrue(animationTimer != null, "marquee timer exists");
                AssertEqual(
                    true,
                    animationTimer.Enabled,
                    "active fallback marquee runs its timer");
                AssertEqual(
                    105,
                    animationTimer.Interval,
                    "active fallback uses one-third frame cadence");
                AssertTrue(
                    (IntPtr)GetInstanceField(progress, "_maskHandle") !=
                        IntPtr.Zero,
                    "fallback creates its private native mask");
                AssertEqual(
                    0,
                    progress.Controls.Count,
                    "unmanaged mask does not pollute the public Controls tree");
                SetInstanceField(
                    progress,
                    "_preferMarqueeFallback",
                    false);
                bool snapshotFallback = !automaticFallback;
                SetInstanceField(
                    progress,
                    "_useLegacyRendererForHandle",
                    snapshotFallback);
                CreateParams existingHandleParams =
                    (CreateParams)createParamsProperty.GetValue(
                        progress,
                        null);
                AssertEqual(
                    snapshotFallback,
                    GetInstanceField(
                        progress,
                        "_useLegacyRendererForHandle"),
                    "CreateParams queries preserve the handle renderer snapshot");
                AssertEqual(
                    snapshotFallback ? 0 : 0x00000008,
                    existingHandleParams.Style & 0x00000008,
                    "existing handle keeps its renderer snapshot style");
                SetInstanceField(
                    progress,
                    "_preferMarqueeFallback",
                    true);
                SetInstanceField(
                    progress,
                    "_useLegacyRendererForHandle",
                    true);
                AssertEqual(10, nativeProgress.Minimum, "logical minimum");
                AssertEqual(300, nativeProgress.Maximum, "logical maximum");
                AssertEqual(77, nativeProgress.Value, "logical value");

                nativeProgress.Style = ProgressBarStyle.Blocks;
                AssertEqual(
                    ProgressBarStyle.Blocks,
                    nativeProgress.Style,
                    "compatibility-aware style switches to determinate mode");
                AssertEqual(
                    false,
                    GetInstanceField(progress, "_legacyMarqueeActive"),
                    "base-typed style stops the fallback immediately");
                AssertEqual(
                    false,
                    animationTimer.Enabled,
                    "determinate fallback does not poll");
                AssertEqual(10, nativeProgress.Minimum, "restored minimum");
                AssertEqual(300, nativeProgress.Maximum, "restored maximum");
                AssertEqual(77, nativeProgress.Value, "restored value");
                nativeProgress.Style = ProgressBarStyle.Marquee;
                nativeProgress.MarqueeAnimationSpeed = 0;
                AssertEqual(
                    false,
                    animationTimer.Enabled,
                    "paused fallback marquee does not poll");
                AssertEqual(
                    250,
                    animationTimer.Interval,
                    "paused fallback retains a valid timer interval");
                nativeProgress.MarqueeAnimationSpeed = 35;
                AssertEqual(
                    true,
                    animationTimer.Enabled,
                    "base-typed speed change resumes the fallback immediately");
                AssertEqual(
                    105,
                    animationTimer.Interval,
                    "base-typed speed change applies fallback cadence only");
                progress.Enabled = false;
                AssertEqual(
                    false,
                    animationTimer.Enabled,
                    "disabled fallback marquee does not poll");
                progress.Enabled = true;

                progress.PreferMarqueeFallback = false;
                AssertEqual(
                    ProgressBarStyle.Marquee,
                    nativeProgress.Style,
                    "renderer switch preserves native Style readback");
                AssertEqual(
                    35,
                    nativeProgress.MarqueeAnimationSpeed,
                    "renderer switch preserves native speed readback");
                AssertEqual(
                    automaticFallback,
                    animationTimer.Enabled,
                    "automatic mode follows the detected native capability");
                progress.PreferMarqueeFallback = true;
                AssertEqual(
                    true,
                    animationTimer.Enabled,
                    "fallback renderer switch restarts from native state");

                using (Bitmap rendered =
                    new Bitmap(progress.Width, progress.Height))
                {
                    progress.DrawToBitmap(
                        rendered,
                        new Rectangle(Point.Empty, progress.Size));
                    AssertEqual(120, rendered.Width, "marquee paint width");
                }
            }

            XamlRuntime runtime = XamlRuntime.Load(
                "<ProgressBar Name='Loading' Style='Marquee' " +
                "    MarqueeAnimationSpeed='35' " +
                "    PreferMarqueeFallback='true' />");
            ProgressBar markupProgress = null;

            try
            {
                markupProgress = runtime.Get<ProgressBar>("Loading");
                AssertTrue(
                    markupProgress is CompatibleProgressBar,
                    "native ProgressBar lookup retains the legacy fallback");
                AssertEqual(
                    ProgressBarStyle.Marquee,
                    markupProgress.Style,
                    "native Style markup requests marquee");
                AssertEqual(
                    35,
                    markupProgress.MarqueeAnimationSpeed,
                    "native marquee speed markup");
            }
            finally
            {
                runtime.Dispose();

                if (markupProgress != null && !markupProgress.IsDisposed)
                    markupProgress.Dispose();
            }
        }

        private static void TestLegacyMarqueePausePreservesPhase()
        {
            using (CompatibleProgressBar progress =
                new CompatibleProgressBar())
            {
                ProgressBar nativeProgress = progress;
                progress.PreferMarqueeFallback = true;
                nativeProgress.Style = ProgressBarStyle.Marquee;
                nativeProgress.MarqueeAnimationSpeed = 35;
                progress.Size = new Size(120, 20);
                progress.CreateControl();

                System.Windows.Forms.Timer animationTimer =
                    GetInstanceField(progress, "_animationTimer") as
                        System.Windows.Forms.Timer;
                AssertTrue(
                    animationTimer != null && animationTimer.Enabled,
                    "pause regression starts with an active timer");

                const int originalFrame = 17;
                SetInstanceField(progress, "_marqueeFrame", originalFrame);

                nativeProgress.MarqueeAnimationSpeed = 0;
                AssertEqual(
                    false,
                    animationTimer.Enabled,
                    "speed zero pauses the fallback timer");
                AssertEqual(
                    originalFrame,
                    GetInstanceField(progress, "_marqueeFrame"),
                    "pausing preserves the current visual phase");
                nativeProgress.MarqueeAnimationSpeed = 35;

                AssertEqual(
                    true,
                    animationTimer.Enabled,
                    "positive speed resumes the fallback timer");
                AssertEqual(
                    originalFrame,
                    GetInstanceField(progress, "_marqueeFrame"),
                    "resuming has no elapsed-time catch-up jump");

                MethodInfo getLastFrame =
                    typeof(CompatibleProgressBar).GetMethod(
                        "GetLegacyLastFrame",
                        BindingFlags.Static | BindingFlags.NonPublic);
                AssertTrue(
                    getLastFrame != null,
                    "legacy last-frame helper found");
                int lastFrame =
                    (int)getLastFrame.Invoke(
                        null,
                        new object[]
                        {
                            progress.ClientSize.Width,
                            progress.ClientSize.Height
                        });
                MethodInfo applyVisual =
                    typeof(CompatibleProgressBar).GetMethod(
                        "ApplyLegacyNativeVisual",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                AssertTrue(applyVisual != null, "native fallback visual found");

                SetInstanceField(progress, "_marqueeFrame", 1);
                applyVisual.Invoke(progress, null);
                IntPtr growingMask =
                    (IntPtr)GetInstanceField(progress, "_maskHandle");
                AssertEqual(
                    true,
                    GetInstanceField(progress, "_maskVisible"),
                    "growing phase reveals the filled native overlay");

                int firstDrainFrame = lastFrame / 2 + 1;
                SetInstanceField(
                    progress,
                    "_marqueeFrame",
                    firstDrainFrame);
                applyVisual.Invoke(progress, null);
                AssertEqual(
                    true,
                    GetInstanceField(progress, "_maskVisible"),
                    "reverse phase reveals the clipped filled native overlay");
                AssertEqual(
                    growingMask,
                    GetInstanceField(progress, "_maskHandle"),
                    "both phases reuse one native block raster");
                AssertEqual(
                    0,
                    progress.Controls.Count,
                    "reverse mask remains outside the managed Controls tree");
            }
        }

        private static void TestSharedRuntimePresets()
        {
            PresetManager manager = new PresetManager();

            manager.LoadXml(
                "<Presets Name='Theme' Selected='Dark'>" +
                "  <Preset Name='Dark'><Set Key='Accent' Value='Runtime' /></Preset>" +
                "</Presets>");

            const string markup =
                "<Panel>" +
                "  <Presets Name='Theme' Selected='Light'>" +
                "    <Preset Name='Dark'>" +
                "      <Set Key='Accent' Value='Declared' />" +
                "      <Set Key='Spacing' Value='12' />" +
                "    </Preset>" +
                "    <Preset Name='Light'><Set Key='Accent' Value='Blue' /></Preset>" +
                "  </Presets>" +
                "</Panel>";

            XamlRuntime runtime =
                XamlRuntime.Load(markup, null, null, manager);

            try
            {
                AssertEqual("Dark", manager["Theme"].SelectedName, "shared selection");
                AssertEqual("Runtime", manager.Resolve("Theme", "Accent"), "shared value");
                AssertEqual("12", manager["Theme"]["Dark"]["Spacing"], "missing declaration");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestApplicationIconDefaults()
        {
            string iconPath = Path.Combine(
                Path.GetTempPath(),
                "wfx-form-icon-" + Guid.NewGuid().ToString("N") + ".ico");

            using (FileStream stream = new FileStream(
                iconPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None))
            {
                SystemIcons.Warning.Save(stream);
            }

            string iconMarkupPath = EscapeXmlAttributeValue(iconPath);

            XamlRuntime first = XamlRuntime.Load("<Form />");
            XamlRuntime second = XamlRuntime.Load("<Form />");
            XamlRuntime optedOut =
                XamlRuntime.Load("<Form UseApplicationIcon='false' />");
            XamlRuntime literalDirectiveFirst = XamlRuntime.Load(
                "<Form UseApplicationIcon='true' Icon='" +
                iconMarkupPath + "' />");
            XamlRuntime literalIconFirst = XamlRuntime.Load(
                "<Form Icon='" + iconMarkupPath +
                "' UseApplicationIcon='true' />");
            ReactiveIconState iconState =
                new ReactiveIconState(SystemIcons.Warning);
            XamlRuntime boundDirectiveFirst = XamlRuntime.Load(
                "<Form UseApplicationIcon='{Binding UseApplicationIcon}' " +
                "Icon='{Binding ApplicationIcon}' />",
                iconState);
            XamlRuntime boundIconFirst = XamlRuntime.Load(
                "<Form Icon='{Binding ApplicationIcon}' " +
                "UseApplicationIcon='{Binding UseApplicationIcon}' />",
                iconState);
            XamlRuntime styled = XamlRuntime.Load(
                "<Form ResourceStyle='IconStyle' " +
                "UseApplicationIcon='{Binding UseApplicationIcon}'>" +
                "  <Form.Resources>" +
                "    <Style Key='IconStyle' TargetType='Form'>" +
                "      <Setter Property='Icon' " +
                "          Value='{Binding ApplicationIcon}' />" +
                "    </Style>" +
                "  </Form.Resources>" +
                "</Form>",
                iconState);
            Form firstForm = first.Root as Form;
            Form secondForm = second.Root as Form;
            Form optedOutForm = optedOut.Root as Form;
            Form literalDirectiveFirstForm =
                literalDirectiveFirst.Root as Form;
            Form literalIconFirstForm = literalIconFirst.Root as Form;
            Form boundDirectiveFirstForm =
                boundDirectiveFirst.Root as Form;
            Form boundIconFirstForm = boundIconFirst.Root as Form;
            Form styledForm = styled.Root as Form;

            try
            {
                AssertTrue(firstForm != null, "first root is a Form");
                AssertTrue(secondForm != null, "second root is a Form");
                AssertTrue(optedOutForm != null, "icon opt-out root is a Form");
                AssertTrue(firstForm.Icon != null, "first Form icon");
                AssertTrue(secondForm.Icon != null, "Form icon");
                AssertTrue(
                    !Object.ReferenceEquals(firstForm.Icon, secondForm.Icon),
                    "independent icon instances");
                AssertTrue(optedOutForm != null, "opt-out root is a Form");
                AssertEqual(
                    false,
                    GetFormIconStateField(
                        optedOut,
                        optedOutForm,
                        "FallbackApplied"),
                    "opt-out does not install the executable icon fallback");
                AssertEqual(
                    null,
                    GetOwnedPropertyValue(optedOut, optedOutForm, "Icon"),
                    "opt-out does not create a runtime-owned icon");
                AssertTrue(
                    literalDirectiveFirstForm != null,
                    "directive-first literal icon root is a Form");
                AssertTrue(
                    literalIconFirstForm != null,
                    "icon-first literal icon root is a Form");
                AssertIconsEqual(
                    SystemIcons.Warning,
                    literalDirectiveFirstForm.Icon,
                    "literal Icon wins when UseApplicationIcon comes first");
                AssertIconsEqual(
                    SystemIcons.Warning,
                    literalIconFirstForm.Icon,
                    "literal Icon wins when UseApplicationIcon comes last");
                AssertTrue(
                    boundDirectiveFirstForm != null,
                    "directive-first bound icon root is a Form");
                AssertTrue(
                    boundIconFirstForm != null,
                    "icon-first bound icon root is a Form");
                AssertTrue(styledForm != null, "styled icon root is a Form");
                AssertSame(
                    iconState.ApplicationIcon.Value,
                    boundDirectiveFirstForm.Icon,
                    "bound Icon wins when UseApplicationIcon comes first");
                AssertSame(
                    iconState.ApplicationIcon.Value,
                    boundIconFirstForm.Icon,
                    "bound Icon wins when UseApplicationIcon comes last");
                AssertSame(
                    iconState.ApplicationIcon.Value,
                    styledForm.Icon,
                    "an Icon style owns the property above the default directive");

                CreateHandleAndDrainReactiveCallbacks(
                    boundDirectiveFirst.RootControl);
                CreateHandleAndDrainReactiveCallbacks(
                    boundIconFirst.RootControl);
                CreateHandleAndDrainReactiveCallbacks(styled.RootControl);

                iconState.UseApplicationIcon.Value = false;
                DrainReactiveCallbacks(boundDirectiveFirst.RootControl);
                DrainReactiveCallbacks(boundIconFirst.RootControl);
                DrainReactiveCallbacks(styled.RootControl);
                AssertSame(
                    iconState.ApplicationIcon.Value,
                    boundDirectiveFirstForm.Icon,
                    "reactive opt-out cannot overwrite a bound Icon");
                AssertSame(
                    iconState.ApplicationIcon.Value,
                    boundIconFirstForm.Icon,
                    "reactive opt-out is independent of attribute order");
                AssertSame(
                    iconState.ApplicationIcon.Value,
                    styledForm.Icon,
                    "reactive opt-out cannot overwrite an Icon style");

                iconState.UseApplicationIcon.Value = true;
                DrainReactiveCallbacks(boundDirectiveFirst.RootControl);
                DrainReactiveCallbacks(boundIconFirst.RootControl);
                DrainReactiveCallbacks(styled.RootControl);
                Icon replacementIcon = SystemIcons.Information;
                iconState.ApplicationIcon.Value = replacementIcon;
                DrainReactiveCallbacks(boundDirectiveFirst.RootControl);
                DrainReactiveCallbacks(boundIconFirst.RootControl);
                DrainReactiveCallbacks(styled.RootControl);
                AssertSame(
                    replacementIcon,
                    boundDirectiveFirstForm.Icon,
                    "the normal Icon property remains reactively bindable");
                AssertSame(
                    replacementIcon,
                    boundIconFirstForm.Icon,
                    "the reverse-order Icon binding remains reactive");
                AssertSame(
                    replacementIcon,
                    styledForm.Icon,
                    "the styled Icon binding remains reactive");
            }
            finally
            {
                if (firstForm != null)
                    firstForm.Dispose();

                if (secondForm != null)
                    secondForm.Dispose();

                if (optedOutForm != null)
                    optedOutForm.Dispose();

                if (literalDirectiveFirstForm != null)
                    literalDirectiveFirstForm.Dispose();

                if (literalIconFirstForm != null)
                    literalIconFirstForm.Dispose();

                if (boundDirectiveFirstForm != null)
                    boundDirectiveFirstForm.Dispose();

                if (boundIconFirstForm != null)
                    boundIconFirstForm.Dispose();

                if (styledForm != null)
                    styledForm.Dispose();

                first.Dispose();
                second.Dispose();
                optedOut.Dispose();
                literalDirectiveFirst.Dispose();
                literalIconFirst.Dispose();
                boundDirectiveFirst.Dispose();
                boundIconFirst.Dispose();
                styled.Dispose();

                if (File.Exists(iconPath))
                    File.Delete(iconPath);
            }
        }

        private static void TestSharedImageLifetime()
        {
            BindingState state = new BindingState();

            using (MemoryStream stream = new MemoryStream())
            using (Bitmap bitmap = new Bitmap(1, 1))
            {
                bitmap.Save(stream, ImageFormat.Png);
                state.ImageBytes = stream.ToArray();
            }

            byte[] imageBytes = state.ImageBytes;

            XamlRuntime runtime =
                XamlRuntime.Load(
                    "<Panel>" +
                    "  <ReferenceIdentityPictureBox Name='First' Source='{Binding ImageBytes}' />" +
                    "  <ReferenceIdentityPictureBox Name='Second' Source='{Binding ImageBytes}' />" +
                    "</Panel>",
                    state);

            try
            {
                PictureBox first = runtime.Get<PictureBox>("First");
                PictureBox second = runtime.Get<PictureBox>("Second");
                Image sharedImage = first.Image;

                AssertTrue(
                    Object.ReferenceEquals(sharedImage, second.Image),
                    "identity decode cache");
                AssertSame(
                    sharedImage,
                    GetOwnedPropertyValue(runtime, first, "image"),
                    "owned property keys are case-insensitive");
                AssertEqual(
                    2,
                    GetOwnedPropertyValueReferenceCount(
                        runtime,
                        sharedImage),
                    "shared image starts with two owned references");

                state.ImageBytes = null;
                runtime.ReloadBinding("First", "Source");

                AssertEqual(null, first.Image, "first image cleared");
                AssertEqual(1, second.Image.Width, "shared image remains usable");
                AssertEqual(
                    1,
                    GetOwnedPropertyValueReferenceCount(
                        runtime,
                        sharedImage),
                    "first release decrements the shared reference count");

                runtime.ReloadBinding("Second", "Source");
                AssertEqual(null, second.Image, "second image cleared");
                AssertEqual(
                    0,
                    GetOwnedPropertyValueReferenceCount(
                        runtime,
                        sharedImage),
                    "last release removes the shared reference count");

                state.ImageBytes = imageBytes;
                runtime.ReloadBinding("First", "Source");

                AssertTrue(
                    !Object.ReferenceEquals(sharedImage, first.Image),
                    "last release invalidates the decoded-image cache");
                AssertEqual(
                    1,
                    first.Image.Width,
                    "image is decoded again after cache invalidation");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestReentrantOwnedPropertyAssignment()
        {
            BindingState state = new BindingState();
            state.Text = "Arial, 8pt";

            using (MemoryStream stream = new MemoryStream())
            using (Bitmap bitmap = new Bitmap(1, 1))
            {
                bitmap.Save(stream, ImageFormat.Png);
                state.ImageBytes = stream.ToArray();
            }

            XamlRuntime runtime = XamlRuntime.Load(
                "<Panel>" +
                "  <PictureBox Name='Picture' Source='{Binding ImageBytes}' />" +
                "  <Label Name='Text' Font='{Binding Text}' />" +
                "</Panel>",
                state);
            PictureBox picture = null;
            Label label = null;
            InvalidateEventHandler imageInvalidated = null;
            EventHandler fontChanged = null;

            try
            {
                picture = runtime.Get<PictureBox>("Picture");
                label = runtime.Get<Label>("Text");
                Image initialImage = picture.Image;
                Font initialFont = label.Font;

                picture.CreateControl();

                AssertSame(
                    initialImage,
                    GetOwnedPropertyValue(runtime, picture, "Image"),
                    "initial decoded image ownership");
                AssertSame(
                    initialFont,
                    GetOwnedPropertyValue(runtime, label, "Font"),
                    "initial converted Font ownership");

                bool restoringImage = false;
                imageInvalidated = delegate
                {
                    if (!restoringImage && picture.Image == null)
                    {
                        restoringImage = true;

                        try
                        {
                            picture.Image = initialImage;
                        }
                        finally
                        {
                            restoringImage = false;
                        }
                    }
                };
                picture.Invalidated += imageInvalidated;

                bool restoringFont = false;
                fontChanged = delegate
                {
                    if (!restoringFont &&
                        !Object.ReferenceEquals(label.Font, initialFont))
                    {
                        restoringFont = true;

                        try
                        {
                            label.Font = initialFont;
                        }
                        finally
                        {
                            restoringFont = false;
                        }
                    }
                };
                label.FontChanged += fontChanged;

                state.ImageBytes = null;
                runtime.ReloadBinding("Picture", "Source");

                AssertSame(
                    initialImage,
                    picture.Image,
                    "Invalidated callback keeps the restored image installed");
                AssertSame(
                    initialImage,
                    GetOwnedPropertyValue(runtime, picture, "Image"),
                    "image ownership follows the reentrant installed value");
                AssertEqual(
                    1,
                    picture.Image.Width,
                    "reentrant installed image remains usable");

                state.Text = "Arial, 12pt";
                runtime.ReloadBinding("Text", "Font");

                AssertSame(
                    initialFont,
                    label.Font,
                    "FontChanged callback keeps the restored Font installed");
                AssertSame(
                    initialFont,
                    GetOwnedPropertyValue(runtime, label, "Font"),
                    "Font ownership follows the reentrant installed value");
            }
            finally
            {
                if (picture != null && imageInvalidated != null)
                    picture.Invalidated -= imageInvalidated;

                if (label != null && fontChanged != null)
                    label.FontChanged -= fontChanged;

                runtime.Dispose();
            }
        }

        private static void TestRootDisposal()
        {
            PresetManager manager = new PresetManager();
            XamlRuntime runtime =
                XamlRuntime.Load("<Panel />", null, null, manager);

            AssertTrue(!runtime.IsDisposed, "runtime starts active");
            runtime.RootControl.Dispose();
            AssertTrue(runtime.IsDisposed, "root disposal propagates");

            // Must be detached from the manager and remain safe to dispose again.
            manager.AddSet("AfterDispose");
            runtime.Dispose();
        }

        private static void CreateHandleAndDrainReactiveCallbacks(
            Control root)
        {
            AssertTrue(root != null, "reactive root control");

            if (!root.IsHandleCreated)
                root.CreateControl();

            if (!root.IsHandleCreated)
            {
                IntPtr handle = root.Handle;

                AssertTrue(
                    handle != IntPtr.Zero,
                    "reactive root native handle");
            }

            AssertTrue(
                root.IsHandleCreated,
                "reactive root handle created");
            DrainReactiveCallbacks(root);
        }

        private static void DrainReactiveCallbacks(Control root)
        {
            AssertTrue(root != null, "reactive dispatch root");
            AssertTrue(!root.IsDisposed, "reactive dispatch root is active");
            AssertTrue(
                root.IsHandleCreated,
                "reactive dispatch root has a handle");

            // A callback can enqueue a second pass while the first pass is being
            // drained. Repeated sentinels preserve message order without relying
            // on timing or Thread.Sleep.
            int round;

            for (round = 0; round < 6; round++)
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

                AssertTrue(
                    reached,
                    "reactive dispatch sentinel reached");
            }
        }

        private static int GetPropertyBindingSubscriberCount(object binding)
        {
            Delegate handlers =
                GetInstanceField(binding, "_valueChanged") as Delegate;

            return handlers == null
                ? 0
                : handlers.GetInvocationList().Length;
        }

        private static void ExpectInvalidMarkup(
            string markup,
            object eventTarget)
        {
            XamlRuntime runtime = null;
            bool rejected = false;

            try
            {
                runtime = XamlRuntime.Load(markup, eventTarget);
            }
            catch (InvalidOperationException)
            {
                rejected = true;
            }
            finally
            {
                if (runtime != null)
                    runtime.Dispose();
            }

            if (!rejected)
            {
                throw new InvalidOperationException(
                    "Expected invalid reactive markup was accepted: " +
                    markup);
            }
        }

        private static void ExpectInvalidMarkupMessage(
            string markup,
            object eventTarget,
            string expectedMessage)
        {
            XamlRuntime runtime = null;
            bool rejected = false;

            try
            {
                runtime = XamlRuntime.Load(markup, eventTarget);
            }
            catch (InvalidOperationException ex)
            {
                if (ex.Message == null ||
                    ex.Message.IndexOf(
                        expectedMessage,
                        StringComparison.Ordinal) < 0)
                {
                    throw new InvalidOperationException(
                        "Invalid markup produced an unclear error. Expected '" +
                        expectedMessage +
                        "', actual '" +
                        ex.Message +
                        "'.",
                        ex);
                }

                rejected = true;
            }
            finally
            {
                if (runtime != null)
                    runtime.Dispose();
            }

            if (!rejected)
            {
                throw new InvalidOperationException(
                    "Expected invalid markup was accepted: " +
                    markup);
            }
        }

        private static void ExpectInvalidOperation(TestMethod method)
        {
            try
            {
                method();
            }
            catch (InvalidOperationException)
            {
                return;
            }

            throw new InvalidOperationException(
                "Expected InvalidOperationException was not thrown.");
        }

        private static object GetInstanceField(
            object target,
            string name)
        {
            FieldInfo field = target.GetType().GetField(
                name,
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);

            AssertTrue(field != null, name + " field found");
            return field.GetValue(target);
        }

        private static void SetInstanceField(
            object target,
            string name,
            object value)
        {
            FieldInfo field = target.GetType().GetField(
                name,
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);

            AssertTrue(field != null, name + " field found");
            field.SetValue(target, value);
        }

        private static string ReplaceRegisteredComponentTemplateForTest(
            string componentName,
            string templateXml)
        {
            FieldInfo registryField = typeof(XamlRuntime).GetField(
                "_registeredComponents",
                BindingFlags.Static | BindingFlags.NonPublic);
            FieldInfo syncField = typeof(XamlRuntime).GetField(
                "_componentRegistrySync",
                BindingFlags.Static | BindingFlags.NonPublic);

            AssertTrue(registryField != null, "component registry field found");
            AssertTrue(syncField != null, "component registry sync field found");

            IDictionary registry =
                registryField.GetValue(null) as IDictionary;
            object registrySync = syncField.GetValue(null);

            AssertTrue(registry != null, "component registry found");
            AssertTrue(registrySync != null, "component registry sync found");

            lock (registrySync)
            {
                object component = registry[componentName];

                AssertTrue(component != null, "registered component found");

                FieldInfo templateField = component.GetType().GetField(
                    "TemplateXml",
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);

                AssertTrue(
                    templateField != null,
                    "registered component template field found");

                string previous =
                    templateField.GetValue(component) as string;
                templateField.SetValue(component, templateXml);
                return previous;
            }
        }

        private static void ReplaceBoundEventForTest(
            XamlRuntime runtime,
            object target,
            string eventName,
            Delegate handler)
        {
            ReplaceBoundEventForTest(
                runtime,
                target,
                eventName,
                handler,
                false);
        }

        private static void ReplaceBoundEventForTest(
            XamlRuntime runtime,
            object target,
            string eventName,
            Delegate handler,
            bool styleSetter)
        {
            EventInfo eventInfo = target.GetType().GetEvent(eventName);
            MethodInfo replace = typeof(XamlRuntime).GetMethod(
                "ReplaceBoundEvent",
                BindingFlags.Instance | BindingFlags.NonPublic);

            AssertTrue(eventInfo != null, eventName + " event found");
            AssertTrue(replace != null, "event replacement method found");

            try
            {
                replace.Invoke(
                    runtime,
                    new object[] { target, eventInfo, handler, styleSetter });
            }
            catch (TargetInvocationException ex)
            {
                if (ex.InnerException != null)
                    throw ex.InnerException;

                throw;
            }
        }

        private static void ReleaseBoundEventsForTest(
            XamlRuntime runtime,
            object target)
        {
            MethodInfo release = typeof(XamlRuntime).GetMethod(
                "ReleaseBoundEvents",
                BindingFlags.Instance | BindingFlags.NonPublic);

            AssertTrue(release != null, "event release method found");

            try
            {
                release.Invoke(runtime, new object[] { target });
            }
            catch (TargetInvocationException ex)
            {
                if (ex.InnerException != null)
                    throw ex.InnerException;

                throw;
            }
        }

        private static object GetElementInfoField(
            XamlRuntime runtime,
            object target,
            string name)
        {
            IDictionary elementInfos =
                GetInstanceField(runtime, "_elementInfos") as IDictionary;

            AssertTrue(elementInfos != null, "element metadata dictionary found");
            object info = elementInfos[target];
            AssertTrue(info != null, "target element metadata found");
            return GetInstanceField(info, name);
        }

        private static object GetFormIconStateField(
            XamlRuntime runtime,
            Form form,
            string name)
        {
            object state = GetElementInfoField(runtime, form, "FormIcon");
            AssertTrue(state != null, "Form icon metadata found");
            return GetInstanceField(state, name);
        }

        private static object GetOwnedPropertyValue(
            XamlRuntime runtime,
            object target,
            string propertyName)
        {
            IDictionary ownedValues =
                GetInstanceField(runtime, "_ownedPropertyValues")
                as IDictionary;

            AssertTrue(ownedValues != null, "owned property index found");

            IDictionary propertyValues =
                ownedValues[target] as IDictionary;

            if (propertyValues == null)
                return null;

            object owned = propertyValues[propertyName];

            if (owned == null)
                return null;

            AssertSame(
                target,
                GetInstanceField(owned, "Target"),
                "owned property target identity");

            return GetInstanceField(owned, "Value");
        }

        private static int GetOwnedPropertyValueReferenceCount(
            XamlRuntime runtime,
            object value)
        {
            IDictionary referenceCounts =
                GetInstanceField(
                    runtime,
                    "_ownedPropertyValueReferenceCounts")
                as IDictionary;

            AssertTrue(
                referenceCounts != null,
                "owned value reference-count index found");

            object reference = referenceCounts[value];

            return reference == null
                ? 0
                : (int)GetInstanceField(reference, "Count");
        }

        private static void AssertFontMetadataExplicit(
            XamlRuntime runtime,
            object target,
            string message)
        {
            string[] fields = new string[]
            {
                "FontFamilyExplicit",
                "FontFamilySet",
                "FontSizeExplicit",
                "FontSizeSet",
                "FontWeightExplicit",
                "FontWeightSet",
                "FontStyleExplicit",
                "FontStyleSet",
                "TextDecorationsExplicit",
                "TextDecorationsSet"
            };
            int i;

            for (i = 0; i < fields.Length; i++)
            {
                AssertEqual(
                    true,
                    GetElementInfoField(runtime, target, fields[i]),
                    message + " " + fields[i]);
            }
        }

        private static void AssertSame(
            object expected,
            object actual,
            string message)
        {
            if (!Object.ReferenceEquals(expected, actual))
            {
                throw new InvalidOperationException(
                    "Assertion failed: " + message + ". Expected the same instance.");
            }
        }

        private static string EscapeXmlAttributeValue(string value)
        {
            if (value == null)
                return String.Empty;

            return value
                .Replace("&", "&amp;")
                .Replace("'", "&apos;")
                .Replace("<", "&lt;");
        }

        private static void AssertIconsEqual(
            Icon expected,
            Icon actual,
            string message)
        {
            AssertTrue(expected != null, message + " expected icon");
            AssertTrue(actual != null, message + " actual icon");

            using (Bitmap expectedBitmap = expected.ToBitmap())
            using (Bitmap actualBitmap = actual.ToBitmap())
            {
                AssertEqual(
                    expectedBitmap.Size,
                    actualBitmap.Size,
                    message + " size");

                int y;

                for (y = 0; y < expectedBitmap.Height; y++)
                {
                    int x;

                    for (x = 0; x < expectedBitmap.Width; x++)
                    {
                        if (expectedBitmap.GetPixel(x, y) !=
                            actualBitmap.GetPixel(x, y))
                        {
                            throw new InvalidOperationException(
                                "Assertion failed: " + message +
                                ". Icon pixels differ at " + x + "," + y + ".");
                        }
                    }
                }
            }
        }

        private static void AssertWebBrowserSourceCleared(
            WebBrowser browser,
            string message)
        {
            Uri source = browser.Url;

            if (source == null)
                return;

            // Native WebBrowser implementations expose an empty document as
            // either null or the canonical about:blank URI. In particular,
            // Wine retains about:blank when Url is assigned null before a
            // browser message loop exists. No previous or arbitrary URI is a
            // valid cleared state.
            if (source.IsAbsoluteUri &&
                String.Equals(
                    source.AbsoluteUri,
                    "about:blank",
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            throw new InvalidOperationException(
                "Assertion failed: " +
                message +
                ". Expected <null> or <about:blank>, actual <" +
                source +
                ">.");
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException("Assertion failed: " + message + ".");
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
                    ". Expected <" +
                    expected +
                    ">, actual <" +
                    actual +
                    ">.");
            }
        }
    }

    public sealed class ThrowingAttachmentHost : Panel
    {
        protected override void OnControlAdded(ControlEventArgs e)
        {
            base.OnControlAdded(e);
            throw new InvalidOperationException("Attachment failed after parenting.");
        }
    }

    public sealed class TrackedAttachmentChild : Button
    {
        public static int DisposeCount;

        protected override void Dispose(bool disposing)
        {
            if (disposing && !IsDisposed)
                DisposeCount++;

            base.Dispose(disposing);
        }
    }

    public sealed class TwoWayScrollDataGridView : DataGridView
    {
        public void PrepareScrollableContent()
        {
            if (Columns.Count != 0)
                return;

            DataGridViewTextBoxColumn first =
                new DataGridViewTextBoxColumn();
            DataGridViewTextBoxColumn second =
                new DataGridViewTextBoxColumn();
            first.Width = 100;
            second.Width = 100;
            Columns.Add(first);
            Columns.Add(second);
            Rows.Add();
        }

        public void SimulateHorizontalScroll(int offset)
        {
            HorizontalScrollingOffset = offset;
            OnScroll(
                new ScrollEventArgs(
                    ScrollEventType.ThumbPosition,
                    offset,
                    ScrollOrientation.HorizontalScroll));
        }
    }

    public sealed class ThrowOnceStyleControl : Control
    {
        public static bool ThrowOnNextBaseline;

        private string _riskyValue = "Baseline";

        public string RiskyValue
        {
            get { return _riskyValue; }
            set
            {
                if (ThrowOnNextBaseline && value == "Baseline")
                {
                    ThrowOnNextBaseline = false;
                    throw new InvalidOperationException(
                        "Baseline restore failed once.");
                }

                _riskyValue = value;
            }
        }
    }

    public sealed class ThrowOnceSizeControl : Control
    {
        public static bool ThrowOnNextBaseline;

        public ThrowOnceSizeControl()
        {
            base.Size = new Size(50, 30);
        }

        public new Size Size
        {
            get { return base.Size; }
            set
            {
                if (ThrowOnNextBaseline && value.Width == 100)
                {
                    ThrowOnNextBaseline = false;
                    throw new InvalidOperationException(
                        "Composite size baseline restore failed once.");
                }

                base.Size = value;
            }
        }
    }

    public sealed class MappedAliasShadowControl : Control
    {
        private string _background = "Custom background baseline";
        private string _fontFamily = "Custom font baseline";
        private string _horizontalAlignment = "Custom alignment baseline";
        private bool _isChecked;

        public int BackgroundSetCount;
        public int FontFamilySetCount;
        public int HorizontalAlignmentSetCount;
        public int IsCheckedSetCount;

        public string Background
        {
            get { return _background; }
            set
            {
                BackgroundSetCount++;
                _background = value;
            }
        }

        public string FontFamily
        {
            get { return _fontFamily; }
            set
            {
                FontFamilySetCount++;
                _fontFamily = value;
            }
        }

        public string HorizontalAlignment
        {
            get { return _horizontalAlignment; }
            set
            {
                HorizontalAlignmentSetCount++;
                _horizontalAlignment = value;
            }
        }

        public bool IsChecked
        {
            get { return _isChecked; }
            set
            {
                IsCheckedSetCount++;
                _isChecked = value;
            }
        }
    }

    public sealed class TwoWayAliasShadowCheckBox : CheckBox
    {
        private bool _isChecked;
        private Color _background;

        public int IsCheckedSetCount;
        public int BackgroundSetCount;

        public bool IsChecked
        {
            get { return _isChecked; }
            set
            {
                if (_isChecked == value)
                    return;

                _isChecked = value;
                IsCheckedSetCount++;

                EventHandler handler = IsCheckedChanged;

                if (handler != null)
                    handler(this, EventArgs.Empty);
            }
        }

        public Color Background
        {
            get { return _background; }
            set
            {
                if (_background == value)
                    return;

                _background = value;
                BackgroundSetCount++;

                EventHandler handler = BackgroundChanged;

                if (handler != null)
                    handler(this, EventArgs.Empty);
            }
        }

        public event EventHandler IsCheckedChanged;
        public event EventHandler BackgroundChanged;
    }

    public sealed class TwoWayClickCheckBox : CheckBox
    {
        public void PerformUserClick()
        {
            // CheckBox does not expose PerformClick in .NET Framework 2.0.
            // Calling its protected click path preserves the native event
            // ordering: CheckedChanged is raised before Click returns.
            OnClick(EventArgs.Empty);
        }
    }

    public sealed class TriggerTextBox : TextBox
    {
        public void RaiseLostFocus()
        {
            OnLostFocus(EventArgs.Empty);
        }
    }

    public sealed class SourceShadowPictureBox : PictureBox
    {
        private string _source = "Custom source baseline";

        public int SourceSetCount;

        public string Source
        {
            get { return _source; }
            set
            {
                SourceSetCount++;
                _source = value;
            }
        }
    }

    public sealed class ContentShadowControl : Control
    {
        private object _content;

        public readonly object BaselineContent;
        public int ContentSetCount;

        public ContentShadowControl()
        {
            BaselineContent = new object();
            _content = BaselineContent;
            base.Text = "Native text baseline";
        }

        public object Content
        {
            get { return _content; }
            set
            {
                if (Object.Equals(_content, value))
                    return;

                ContentSetCount++;
                _content = value;

                EventHandler handler = ContentChanged;

                if (handler != null)
                    handler(this, EventArgs.Empty);
            }
        }

        public event EventHandler ContentChanged;
    }

    public sealed class ReadOnlyContentShadowControl : Control
    {
        public string Content
        {
            get { return "Read-only content"; }
        }
    }

    public sealed class PaddingShadowControl : Control
    {
        private string _padding = "Custom padding baseline";

        public int PaddingSetCount;

        public PaddingShadowControl()
        {
            base.Padding = new Padding(3, 4, 5, 6);
        }

        public new string Padding
        {
            get { return _padding; }
            set
            {
                PaddingSetCount++;
                _padding = value;
            }
        }
    }

    public sealed class TextAlignShadowTextBox : TextBox
    {
        private string _textAlign = "Custom alignment baseline";

        public int TextAlignSetCount;

        public TextAlignShadowTextBox()
        {
            base.TextAlign = HorizontalAlignment.Left;
        }

        public new string TextAlign
        {
            get { return _textAlign; }
            set
            {
                TextAlignSetCount++;
                _textAlign = value;
            }
        }
    }

    public sealed class ContentEventMenuItem : ToolStripMenuItem
    {
        public event EventHandler Content;

        public ContentEventMenuItem()
        {
            Text = "Native menu baseline";
        }

        public void RaiseContent()
        {
            EventHandler handler = Content;

            if (handler != null)
                handler(this, EventArgs.Empty);
        }
    }

    public sealed class PostCommitSetterControl : Control
    {
        public bool ThrowAfterNextSizeCommit;
        public bool ThrowAfterNextFontCommit;
        public int SizeSetCount;
        public int FontSetCount;

        public new Size Size
        {
            get { return base.Size; }
            set
            {
                SizeSetCount++;
                base.Size = value;

                if (ThrowAfterNextSizeCommit)
                {
                    ThrowAfterNextSizeCommit = false;
                    throw new InvalidOperationException(
                        "Size setter failed after committing the value.");
                }
            }
        }

        public new Font Font
        {
            get { return base.Font; }
            set
            {
                FontSetCount++;
                base.Font = value;

                if (ThrowAfterNextFontCommit)
                {
                    ThrowAfterNextFontCommit = false;
                    throw new InvalidOperationException(
                        "Font setter failed after committing the value.");
                }
            }
        }
    }

    public sealed class ReferenceIdentityPictureBox : PictureBox
    {
        public override bool Equals(object value)
        {
            return value is ReferenceIdentityPictureBox;
        }

        public override int GetHashCode()
        {
            return 1;
        }
    }

    public sealed class ReferenceIdentityEventControl : Control
    {
        private EventHandler _triggered;

        public event EventHandler Triggered
        {
            add { _triggered += value; }
            remove { _triggered -= value; }
        }

        public void RaiseTriggered()
        {
            EventHandler handler = _triggered;

            if (handler != null)
                handler(this, EventArgs.Empty);
        }

        public override bool Equals(object value)
        {
            return value is ReferenceIdentityEventControl;
        }

        public override int GetHashCode()
        {
            return 1;
        }
    }

    public delegate void EventAccessorCallback();

    public sealed class AccessorEventControl : Control
    {
        private EventHandler _primary;
        private EventHandler _secondary;

        public EventAccessorCallback PrimaryAddCallback;
        public EventAccessorCallback PrimaryRemoveCallback;
        public int PrimaryAddCount;
        public int PrimaryRemoveCount;
        public int PrimaryRemoveFailuresRemaining;
        public int SecondaryRemoveCount;

        public event EventHandler Primary
        {
            add
            {
                PrimaryAddCount++;
                _primary += value;

                EventAccessorCallback callback = PrimaryAddCallback;
                PrimaryAddCallback = null;

                if (callback != null)
                    callback();
            }
            remove
            {
                PrimaryRemoveCount++;

                EventAccessorCallback callback = PrimaryRemoveCallback;
                PrimaryRemoveCallback = null;

                if (callback != null)
                    callback();

                if (PrimaryRemoveFailuresRemaining > 0)
                {
                    PrimaryRemoveFailuresRemaining--;
                    throw new InvalidOperationException(
                        "Primary remove failed before detaching.");
                }

                _primary -= value;
            }
        }

        public event EventHandler Secondary
        {
            add { _secondary += value; }
            remove
            {
                SecondaryRemoveCount++;
                _secondary -= value;
            }
        }

        public void RaisePrimary()
        {
            EventHandler handler = _primary;

            if (handler != null)
                handler(this, EventArgs.Empty);
        }

        public void RaiseSecondary()
        {
            EventHandler handler = _secondary;

            if (handler != null)
                handler(this, EventArgs.Empty);
        }
    }

    public sealed class ProjectedDisposableComponent : IDisposable
    {
        public static int CreatedCount;
        public static int DisposedCount;

        public ProjectedDisposableComponent()
        {
            CreatedCount++;
        }

        public void Dispose()
        {
            DisposedCount++;
        }
    }

    public sealed class RegisteredCtorLabel : Label
    {
        private string _caption;

        public RegisteredCtorLabel(string caption)
        {
            _caption = caption;
            Text = _caption;
        }

        public int CaptionSetCount;

        public string Caption
        {
            get { return _caption; }
            set
            {
                CaptionSetCount++;
                _caption = value;
                Text = value;
            }
        }
    }

    public sealed class ReactiveReadOnlyControl : Control
    {
        private string _readOnlyValue;

        [System.ComponentModel.ReadOnly(true)]
        public string ReadOnlyValue
        {
            get { return _readOnlyValue; }
            set { _readOnlyValue = value; }
        }
    }

    public sealed class ReactiveNoChangeControl : Control
    {
        private string _quietValue;

        public string QuietValue
        {
            get { return _quietValue; }
            set { _quietValue = value; }
        }
    }

    public sealed class ThrowingEventControl : Control
    {
        public static ThrowingEventControl LastInstance;
        public static bool ThrowAfterAdd;
        public static bool ThrowOnRemove;

        private EventHandler _triggered;

        public ThrowingEventControl()
        {
            LastInstance = this;
        }

        public event EventHandler Triggered
        {
            add
            {
                _triggered += value;

                if (ThrowAfterAdd)
                    throw new InvalidOperationException("Custom add failed after subscribing.");
            }
            remove
            {
                if (ThrowOnRemove)
                    throw new InvalidOperationException("Custom remove failed.");

                _triggered -= value;
            }
        }

        public void RaiseTriggered()
        {
            EventHandler handler = _triggered;

            if (handler != null)
                handler(this, EventArgs.Empty);
        }
    }
}
