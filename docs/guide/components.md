# Reusable components

Components let an application give a reusable control or XML fragment its own
element name. Registration is global to the application domain, so register each
component once during startup before loading any interface that uses it.

There are two component shapes:

- a C# control class for typed properties, constructors, events, and encapsulated
  behavior;
- an embedded XML component for reusable composition and styling. It can remain
  XML-only or name a lightweight C# code-behind class with `Component.Class`.

C# registration examples import the package namespace and use the primary
runtime type directly:

```csharp
using WinFormsXaml;
```

Code-behind snippets use the protected `Ui` runtime inherited from `XmlForm`.
Named component roots can wire to declaration-only code-behind fields; use
`Get<T>` from a method only when declaration wiring is not appropriate.

## Register a C# component

Create a normal WinForms control. Public constructor parameters are matched to
XML attributes by name and converted to their declared CLR types.

```csharp
using System;
using System.Drawing;
using System.Windows.Forms;

namespace MyProduct.UI.Components
{
    public sealed class ActionButton : Button
    {
        private Color _accentColor;

        public ActionButton(string text, Color accentColor)
        {
            Text = text;
            AccentColor = accentColor;
            FlatStyle = FlatStyle.Flat;
            Click += HandleClick;
        }

        public Color AccentColor
        {
            get { return _accentColor; }
            set
            {
                _accentColor = value;
                BackColor = value;
            }
        }

        private void HandleClick(object sender, EventArgs e)
        {
            // Behavior owned by every ActionButton instance.
        }
    }
}
```

Register it once:

```csharp
XamlRuntime.Register(
    "ActionButton",
    typeof(MyProduct.UI.Components.ActionButton));
```

It can now be used in every subsequently loaded XML interface:

```xml
<ActionButton Name="SaveButton"
              Text="{Binding SaveButtonText}"
              AccentColor="{Binding SaveAccentColor}"
              Enabled="{Binding CanSave}"
              Click="Save_Click" />
```

The created object is the registered class:

```csharp
private ActionButton SaveButton = null;
```

The declaration-only field is assigned automatically before `OnLoaded` runs.

The constructor receives the initial `Text` and `AccentColor` values. Because
both names also resolve to writable properties, they can receive later binding
updates. Keep form-owned changing values in stable wrappers:

```csharp
public readonly PropertyBinding<string> SaveButtonText =
    new PropertyBinding<string>("Save");
public readonly PropertyBinding<Color> SaveAccentColor =
    new PropertyBinding<Color>(Color.SteelBlue);

private void ApplyLiveValues()
{
    SaveButtonText.Value = "Save changes";
    SaveAccentColor.Value = Color.RoyalBlue;
}
```

Both changes refresh the component without an element lookup or explicit
reload. Use `ReloadBindings("SaveButton")` only when an argument comes from
snapshot state or a function with no discoverable dependency.

If an attribute exists only as a constructor parameter, it is initialization
only. A dynamic binding on that attribute is rejected because there is nowhere
to assign a later value. Expose a writable property with the same name when the
value must support later updates.

## Constructor selection

The runtime chooses the public constructor whose required parameter names can
be satisfied by the component's XML attributes. Values use normal binding and
type-conversion rules.

```csharp
public sealed class UserBadge : Panel
{
    private string _displayName;
    private Image _avatar;

    public UserBadge(string displayName, Image avatar)
    {
        DisplayName = displayName;
        Avatar = avatar;
    }

    public string DisplayName
    {
        get { return _displayName; }
        set { _displayName = value; }
    }

    public Image Avatar
    {
        get { return _avatar; }
        set { _avatar = value; }
    }
}
```

```xml
<UserBadge DisplayName="{Binding User.DisplayName}"
           Avatar="{Binding User.Avatar}" />
```

Avoid public constructor overloads with the same number of equally matching
parameters; an ambiguous match produces a clear load error.

## Create an embedded XML component

Create `UI/Components/StatusBadge.xml`:

```xml
<Component>
  <Component.Properties>
    <Property Name="Text" />
    <Property Name="AccentColor"
              Type="System.Drawing.Color"
              Default="SteelBlue" />
    <Property Name="Icon"
              Type="System.Drawing.Image"
              Required="false" />
    <Property Name="IconVisible"
              Type="Boolean"
              Default="false" />
  </Component.Properties>

  <Border BorderBrush="{Binding AccentColor}"
          BorderThickness="1"
          Padding="8">
    <StackPanel Orientation="Horizontal">
      <PictureBox Image="{Binding Icon}"
                  Visible="{Binding IconVisible}"
                  SizeMode="CenterImage"
                  Width="20"
                  Height="20"
                  Margin="0,0,6,0" />
      <Label Text="{Binding Text}"
             AutoSize="true" />
    </StackPanel>
  </Border>
</Component>
```

The file must have one `Component` root, an optional
`Component.Properties` section, and exactly one visual control root.

Open the application's `.csproj` file and manually add the component XML as an
embedded resource inside the root `<Project>` element:

```xml
<ItemGroup>
  <EmbeddedResource Include="UI\Components\*.xml" />
</ItemGroup>
```

The Visual Studio equivalent is setting each component XML file's **Build
Action** to **Embedded Resource**.

Register all component resources under one manifest-path fragment during
startup:

```csharp
XamlRuntime.Register("UI.Components");
```

Every embedded `.xml` resource whose manifest path contains the fragment is
inspected. A well-formed Form, preset set, or other document without a
`Component` root is ignored; a matched batch with no component roots is a no-op.
Each retained element name is the final resource-name segment without `.xml`,
so this folder registers `StatusBadge` and its sibling components in one call.
Registration is atomic: malformed XML, a malformed `Component`, or a duplicate
derived name prevents the batch from being published.

If the fragment matches nothing, the exception lists up to eight available
embedded XML resource names in deterministic order and reports any remaining
count. If two matches derive the same element name, the exception names both
resources. A conflict with an earlier global registration describes both the
existing and attempted origins, including the CLR type and assembly or the
embedded resource and assembly as applicable.

A complete manifest resource name with exact casing wins before
case-insensitive convenience matching. If a differently cased complete name
matches multiple case-only variants, registration rejects the ambiguity and
lists the candidates so one exact name and casing can be supplied.

Pass the complete manifest resource name when only one component should be
registered. This form is strict and rejects a non-`Component` root:

```csharp
XamlRuntime.Register(
    "MyProduct.UI.Components.StatusBadge.xml");
```

For a resource in another assembly, be explicit:

```csharp
XamlRuntime.Register(
    typeof(SharedControlsMarker).Assembly,
    "SharedControls.UI.StatusBadge.xml");
```

Embedded preset imports inside that component template resolve from the
component resource's assembly by default. This remains true when the consuming
Form XML lives in another assembly. An explicit `Assembly` on the `Presets`
element still overrides the default.

## Add optional component code-behind

Add `Class` only when the XML component needs behavior or a typed C# surface.
The XML resource name still supplies the invocation element name; callers use
`<StatusBadge>`, never the full CLR name.

```xml
<Component Class="MyProduct.UI.Components.StatusBadgeCodeBehind">
  <Component.Properties>
    <Property Name="Text" Type="String" />
    <Property Name="Count" Type="Int32" Default="0" />
  </Component.Properties>

  <StackPanel>
    <Label Text="{Binding Text}" />
    <Button Text="{Function FormatCount(Count)}"
            Click="Dismiss_Click" />
    <Children />
  </StackPanel>
</Component>
```

The class must be public, concrete, closed, and have a public parameterless
constructor. The runtime validates and caches that constructor and its matching
public members during registration, then creates one class instance for every
component invocation.

```csharp
using System;
using WinFormsXaml;

namespace MyProduct.UI.Components
{
    public sealed class StatusBadgeCodeBehind : IDisposable
    {
        public readonly PropertyBinding<string> Text =
            new PropertyBinding<string>(String.Empty);

        public int Count;

        public readonly ChildrenBind Children =
            new ChildrenBind();

        private string FormatCount(int count)
        {
            return count.ToString();
        }

        private void Dismiss_Click(object sender, EventArgs e)
        {
        }

        public void Dispose()
        {
        }
    }
}
```

Matching public fields or non-indexed properties are optional:

- a matching `PropertyBinding<T>` must use the exact declared property type.
  Its stable instance is reused by XML, code-behind, one-way updates, and
  two-way writes. A readonly initialized field is the recommended form;
- a matching plain field/property receives converted initial and later outer
  values, but changing that plain member in C# is not observable. It must be
  writable; use `PropertyBinding<T>` for values changed by code-behind. On an
  outer update, the member is assigned before the observable component value
  publishes, so synchronous functions and events see the new value;
- an omitted matching member is valid. The declared property remains available
  to the XML template through its internal typed observable proxy;
- when the template contains `<Children />`, a matching public `Children`
  member must be exactly `ChildrenBind`. An initialized readonly field keeps a
  stable identity; a null writable member is assigned by the runtime. Without a
  slot, `Children` is not reserved and an unrelated domain member is ignored.

For a plain matching member, conversion and assignment must succeed before the
component proxy publishes. If either fails, the proxy remains unchanged. Once
proxy notification begins, both values already contain the new value; a
listener exception is reported without rolling either value back.

Construction happens before declared values are injected and before the visual
tree is built. `Children.Replace(...)` may stage a replacement in the
constructor, but `Get` and `Wrap` require the `<Children />` slot to be attached.
Template events and functions run on this component instance. A nested
`ItemsControl.ItemTemplate` retains the same instance through refresh,
virtualization, and bounded cache reuse while its ordinary binding context remains the
row item. `Source=CodeBehind` inside that item template explicitly selects the
component instance.

If the class implements `IDisposable`, it is disposed once the component root
or owning runtime is released. A construction, property-injection, or visual
build failure rolls back the partial instance and control tree. Leaving off
`Class` preserves the XML-only behavior and allocation path. Disposing a nested
component root directly also releases its runtime names, bindings, event
delegates, owned values, projected children, and code-behind immediately; later
runtime disposal does not release that component twice.

## Use an XML component

```xml
<StatusBadge Name="ConnectionBadge"
             Text="{Binding ConnectionText}"
             AccentColor="{Preset Theme.AccentColor}"
             Icon="{Binding ConnectionIcon}"
             IconVisible="{Binding HasConnectionIcon}" />
```

Each invocation receives its own property values and control tree. `Name`
refers to the component's visual root:

```csharp
private Control ConnectionBadge = null;
```

The declaration-only field is filled before `OnLoaded`. An argument backed by a
`PropertyBinding<T>` refreshes the component subtree automatically:

```csharp
ConnectionText.Value = "Connected";
ConnectionIcon.Value = _connectedImage;
HasConnectionIcon.Value = true;
```

For snapshot fields or functions without an explicit reactive path argument,
reload the component explicitly:

```csharp
ReloadBindings("ConnectionBadge");
```

The runtime first refreshes the invocation arguments from the parent form,
then refreshes bindings inside the component against those new values. A global
`ReloadBindings()` updates every component as part of the same pass. Preset
changes update matching component arguments and inner values automatically.

### Project caller children

An embedded XML component may declare one empty `<Children />` slot below its
visual root:

```xml
<Component>
  <Component.Properties>
    <Property Name="Title" />
  </Component.Properties>

  <StackPanel>
    <Label Text="{Binding Title}" />
    <Children />
  </StackPanel>
</Component>
```

The invocation may supply zero or more visual Controls:

```xml
<Card Title="{Binding Header}">
  <Button Name="PrimaryAction"
          Text="{Binding PrimaryActionText}"
          Click="PrimaryAction_Click" />
  <TextBox Name="CardSearch"
           Text="{Binding SearchText, Mode=TwoWay}" />
</Card>
```

The two contexts remain separate:

- markup around `<Children />` uses the component's declared local
  properties, so the first `Title` reads the `Card.Title` value;
- the projected child and its descendants use the caller's context. In Form
  markup they bind to the Form, and in an item template they bind to the current
  item. Their event target is also the caller, not the component class;
- projected names belong to the caller's normal namescope. `PrimaryAction` and
  `CardSearch` can fill declaration-only fields on the Form when they are not
  inside an item template;
- one-way and two-way notifications, targeted reloads, item-template patching,
  event binding, ownership, and disposal use the same runtime paths as ordinary
  caller markup.

If the invocation omits children, the slot adds no native placeholder Control.
A component without `<Children />` rejects caller children. Registration rejects
the slot as the visual root, multiple slots, a non-empty slot, or a slot inside
a property element or item template. An invocation rejects non-whitespace text.
This is one unnamed insertion point that can hold multiple controls, not a set
of named or repeated slots.

### Inspect or replace projected children in C#

Component code-behind can expose a stable projected-child handle:

```csharp
public readonly ChildrenBind Children = new ChildrenBind();
```

`Count`, the indexer, enumeration, and `ToArray()` operate on a snapshot of the
direct controls at the slot. `Get<T>(name)` searches those direct controls and
their descendants; missing, ambiguous, and wrong-type names fail clearly.
This scoped lookup does not expose the component template's private named
controls.

Replace the direct controls on the owning WinForms UI thread:

```csharp
Children.Replace(
    new Label { Name = "Status", Text = "Ready" },
    new Button { Name = "Retry", Text = "Retry" });
```

A successful attached replacement transfers ownership of supplied controls to
the component. Controls retained by reference keep their identity; removed
XML-created subtrees release their bindings, nested components, events, and
owned values before disposal. Replacing with the same control references in the
same order is a no-op: it does not republish `Changed`, relayout, or invalidate
the component tree. A failed validation or native reparent operation rolls back
the old parentage, ordering, logical ownership, and published snapshot. A
replacement staged before attachment remains caller-owned until attachment
succeeds; replacing that staged request does not dispose either caller-owned
set.

Wrap all current direct children without rebuilding them:

```csharp
FlowLayoutPanel row = new FlowLayoutPanel();
Children.Wrap(row);
```

The wrapper must be live, empty, and unparented. After success it becomes the
one direct slot child and the previous controls become its children. `Clear()`
disposes attached component-owned controls after commit; before attachment it
only clears the caller-owned staged request. `Changed` runs after a published
change and notifies every listener, reporting the first listener exception
after all listeners were called. A listener cannot recursively mutate the same
`ChildrenBind`; `Replace`, `Clear`, or `Wrap` fails clearly until notification
finishes. A listener may still mutate an independent component's children.

Controls created directly in C# are ordinary native controls: their `Name`
values are available through `Children.Get`, but they are not retroactively
added to the caller XML namescope or parsed for XML bindings. `ChildrenBind`
retires with its component; later mutation attempts throw
`ObjectDisposedException`.

### Edit a component property in both directions

An XML-only component can expose an editable property without a C# control
class. Bind the editor inside the component to the declared property:

```xml
<Component>
  <Component.Properties>
    <Property Name="Value" Type="String" />
  </Component.Properties>

  <TextBox Text="{Binding Value, Mode=TwoWay}" />
</Component>
```

Then make the component invocation's link to the form two-way too:

```xml
<Editor Value="{Binding Header, Mode=TwoWay}" />
```

`Header` should be a stable readonly `PropertyBinding<string>` field. Update
`Header.Value` so the subscribed wrapper identity remains stable. An edit inside
the component writes through that same wrapper and raises its `ValueChanged`
event. Existing writable notification-based properties remain supported for
compatibility.

Every declared XML-component property has a component-local, strongly typed
observable value. This lets the inner template use `Mode=TwoWay` even when the
invocation supplies a literal or uses the property's default. The invocation
controls the outer direction:

- `Value="literal"` creates an editable local value;
- `Value="{Binding Header}"` observes the form but does not write edits back;
- `Value="{Binding Header, Mode=TwoWay}"` observes and writes the terminal
  `PropertyBinding<T>`.

Inside an item template, a component invocation can bypass the current item
when its declared property intentionally comes from the Form:

```xml
<Editor Value="{Binding Header, Source=CodeBehind}" />
```

Within the component template, omitted `Source` continues to use declared local
properties. `Source=CodeBehind` is also available there, but declared component
properties are preferred for reusable components because they keep dependencies
explicit at the invocation.

The two-way invocation must be one complete binding expression; interpolation,
negation, snapshot endpoints, and plain fields are rejected. Component and
parent subscriptions are detached when the component root or runtime is
disposed.

`Condition` can appear on a component invocation and on the component template's
visual root. Dynamic false conditions retain that root as collapsed, and
`PropertyBinding<T>` changes re-evaluate them automatically. Layered conditions
combine: the root is shown only when every
invocation/template condition is true and its `Visibility` also permits
display. Conditions are one-way and reject `Mode=TwoWay`.

When registered XML-component loading fails, `WinFormsXamlLoadException`
distinguishes the invocation from the reusable template. Invocation-property
failures report the consuming markup as `MarkupSource`; failures inside the
template report the registered component resource, and `ElementPath` includes
both sides. The line and position identify the original failing attribute when
retained, otherwise the deepest opening element. Those coordinates survive the
component's `TemplateXml` serialization and parse round trip.

A failure inside invocation-supplied projected content reports the consuming
Form/component resource and its original line and position. It does not pretend
that the projected child came from the reusable component template.

## Declare component properties

```xml
<Component.Properties>
  <Property Name="RequiredText" />
  <Property Name="Count" Type="Int32" Default="0" />
  <Property Name="Enabled" Type="Boolean" Default="true" />
  <Property Name="Image"
            Type="System.Drawing.Image"
            Required="false" />
  <Property Name="Padding"
            Type="System.Windows.Forms.Padding"
            Default="8" />
</Component.Properties>
```

| Attribute | Meaning |
| --- | --- |
| `Name` | Attribute name used by component invocations and inner bindings. |
| `Type` | CLR type; omitted properties are strings. |
| `Default` | Literal used when the caller omits the property. |
| `Required` | Literal `true` or `false`; controls whether omission is an error and defaults to true when no default exists. |

The runtime preserves the declared type in the component-local observable
value. For example, an `Int32` default remains an integer while an inner
`NumericUpDown` binding converts its decimal target value in both directions.

Defaults are literals. Put bindings, functions, and presets on the component
invocation, where they have access to the consuming form's state:

```xml
<StatusBadge Text="{Function FormatConnection(State)}"
             AccentColor="{Preset Theme.AccentColor}" />
```

## Forward values through nested components

Registered XML components can use other registered components. Inner bindings
forward declared values naturally:

```xml
<Component>
  <Component.Properties>
    <Property Name="Title" />
    <Property Name="StatusText" />
  </Component.Properties>

  <StackPanel>
    <Label Text="{Binding Title}" />
    <StatusBadge Text="{Binding StatusText}" />
  </StackPanel>
</Component>
```

Register dependencies before loading a form that uses the outer component.

## Registration rules

- Registration is global and case-insensitive within the current application
  domain.
- Parameterless `XamlRuntime.Register()` scans every embedded `.xml` in the
  calling assembly and retains `Component` and reusable `Includes` roots. An
  empty or whitespace fragment on the assembly overload has the same scan-all
  behavior.
- Register before calling `Load` or `LoadEmbedded`.
- A complete resource name with exact casing strictly registers a `Component`
  or `Includes` document. Case-only non-exact ambiguity lists its candidates;
  otherwise a fragment inspects every matching embedded XML document, registers
  both reusable root types, and skips other well-formed documents.
- Registering the same type or the same resource again is harmless.
- A different component cannot replace an existing registered name; the error
  reports both registration origins.
- Native WinForms type names and built-in WinFormsXaml element names are
  reserved.
- An embedded component may expose one empty `<Children />` insertion point and
  accept zero or more visual Controls. Components without that slot reject
  caller children.
- Embedded components must produce one WinForms `Control` root.
- `Component.Class` is optional. When present, registration validates and
  caches its public parameterless constructor and matching public declared-
  property/`Children` members; each invocation receives a separate instance.

C# Control components are the right choice for a custom native control.
Embedded components are the lightest choice for repeated composition and may
add code-behind without turning the visual root into a custom Control class.
