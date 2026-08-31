# Markup and layout

WinFormsXaml markup is XML that creates Windows Forms objects. Native control
elements use the same names as their `System.Windows.Forms` types, and native
properties use the same names you use in C#.

```xml
<Form Name="SettingsForm" Text="Settings" Width="520" Height="360">
  <Panel Name="Content" BackColor="White" Padding="12">
    <CheckBox Name="StartAutomatically"
              Text="Start automatically"
              Checked="true" />
  </Panel>
</Form>
```

The C# types are exactly what the XML says:

```csharp
Form form = ui.Form;
Panel content = ui.Get<Panel>("Content");
CheckBox checkBox = ui.Get<CheckBox>("StartAutomatically");
```

## Native WinForms controls

Any public WinForms type that can be created with a parameterless constructor
can be resolved by name. Common examples include:

| XML element | C# type |
| --- | --- |
| `Form` | `System.Windows.Forms.Form` |
| `Label` | `System.Windows.Forms.Label` |
| `TextBox` | `System.Windows.Forms.TextBox` |
| `PictureBox` | `System.Windows.Forms.PictureBox` |
| `TrackBar` | `System.Windows.Forms.TrackBar` |
| `TabControl` | `System.Windows.Forms.TabControl` |
| `TabPage` | `System.Windows.Forms.TabPage` |
| `DataGridView` | `System.Windows.Forms.DataGridView` |
| `MenuStrip` | `System.Windows.Forms.MenuStrip` |
| `ToolStripMenuItem` | `System.Windows.Forms.ToolStripMenuItem` |
| `TreeView` | `System.Windows.Forms.TreeView` |

Use native WinForms properties in markup:

```xml
<TextBox Name="Password"
         UseSystemPasswordChar="true"
         MaxLength="128" />

<PictureBox Name="Preview"
            Image="{Binding PreviewImage}"
            SizeMode="Zoom" />

<TrackBar Name="Volume"
          Minimum="0"
          Maximum="100"
          TickFrequency="10" />

<Form FormBorderStyle="FixedDialog"
      MaximizeBox="false"
      MinimizeBox="true" />
```

Binding `PreviewImage` can return a real `System.Drawing.Image` object.
Form resizing uses the native `FormBorderStyle`, `MaximizeBox`, and `MinimizeBox`
properties.

## WPF-style Image

Use `Image` when you want WPF-oriented `Source` and `Stretch` names while
retaining the native `PictureBox` implementation:

```xml
<Image Name="Preview"
       Source="{Binding PreviewImage}"
       Stretch="Uniform"
       Width="160"
       Height="90" />
```

The XML element creates the public, extensible
`WinFormsXaml.ImageControl : PictureBox`. Its default stretch is `Uniform`.
`None` centers the unscaled source, `Fill` uses native stretching, and
`Uniform` preserves the aspect ratio. `UniformToFill` preserves the aspect
ratio while filling the complete content area and cropping equally from the two
overflowing edges. Use the native control directly when its terminology is
clearer:

```xml
<PictureBox Image="{Binding PreviewImage}"
            SizeMode="Zoom"
            Width="160"
            Height="90" />
```

`PictureBox` remains exactly `System.Windows.Forms.PictureBox` and keeps its
native `SizeMode="Normal"` default. Its `Image`, `ImageLocation`, `InitialImage`,
`ErrorImage`, `WaitOnLoad`, and `SizeMode` properties can all use Binding,
Function, or Preset values. The schema also completes native
`LoadCompleted`, `LoadProgressChanged`, and `SizeModeChanged` event handlers.
The optional mapped `Source` and `Stretch` aliases work on a native
`PictureBox`, but they do not change its CLR type or default.

The same WPF-oriented names are available in C#:

```csharp
ImageControl preview = Get<ImageControl>("Preview");
preview.Source = Properties.Resources.Preview;
preview.Stretch = ImageStretch.Uniform;
```

`ImageControl` adds `SourceChanged` and `StretchChanged`; it otherwise retains
the complete native `PictureBox` API. Effective native `SizeMode` changes keep
`Stretch` coherent. Set `Stretch` itself to switch between `Uniform` and
`UniformToFill`, because both deliberately retain `Zoom` as the underlying
PictureBox loading/animation mode.

Both forms use one runtime image pipeline. An existing `System.Drawing.Image`
is assigned by reference, while controls bound to the same `Icon` or encoded
`byte[]` instance share one weakly cached bitmap conversion. An
explicit binding reload fingerprints that array without allocating a copy, so
an in-place byte mutation is decoded instead of returning stale pixels. Stretch
changes do not create resized image copies. The exact `UniformToFill` path
computes a source rectangle for each paint and draws the existing image
directly, so resizing does not grow a derived-image cache. Runtime-created
converted and decoded images are reference-counted and released when their last
assignment is replaced, its bound control is disposed, or the runtime is
disposed. Disposing one of several controls sharing a conversion keeps it alive
for the remaining controls. Application-provided `Image` objects remain owned
by the application.

Reloading a mapped `Source` that resolves to the same file path or URI is a
no-op and preserves a pending or completed native load. To deliberately reread
changed content at that same location, call the inherited `Load()` or
`LoadAsync()` method.

## Layout containers

WinFormsXaml adds layout containers for arrangements that native WinForms does
not provide directly:

| Element | Purpose |
| --- | --- |
| `StackPanel` | Places children vertically or horizontally. |
| `Grid` | Rows and columns with fixed, automatic, or proportional sizes. |
| `DockPanel` | Docks children to an edge and optionally fills the remainder. |
| `Canvas` | Places children at explicit coordinates. |
| `FlexPanel` | Flexible row or column layout with alignment and optional wrapping. |
| `Border` | Hosts one child and paints a border. |
| `ScrollViewer` | Hosts one scrollable child. |
| `Viewbox` | Hosts one child using single-content layout. |
| `ItemsControl` | Repeats a template for a data source. |

### StackPanel

```xml
<StackPanel Orientation="Vertical" Margin="16">
  <Label Text="Email address" AutoSize="true" />
  <TextBox Name="Email" Margin="0,4,0,12" />
  <Button Text="Save" Click="Save_Click" />
</StackPanel>
```

For a horizontal row:

```xml
<StackPanel Orientation="Horizontal">
  <Button Text="Back" Margin="0,0,8,0" />
  <Button Text="Next" />
</StackPanel>
```

### Grid

`Auto` sizes a row or column to its content. `*` receives a proportional share
of the remaining space. A number is a fixed pixel size.

```xml
<Grid Margin="12">
  <Grid.RowDefinitions>
    <RowDefinition Height="Auto" />
    <RowDefinition Height="*" />
    <RowDefinition Height="Auto" />
  </Grid.RowDefinitions>
  <Grid.ColumnDefinitions>
    <ColumnDefinition Width="120" />
    <ColumnDefinition Width="*" />
  </Grid.ColumnDefinitions>

  <Label Grid.Row="0" Grid.Column="0"
         Text="Customer" AutoSize="true" />
  <TextBox Grid.Row="0" Grid.Column="1"
           Name="CustomerName" />

  <ListView Grid.Row="1" Grid.Column="0" Grid.ColumnSpan="2"
            Name="Orders" View="Details" Margin="0,8,0,8" />

  <Button Grid.Row="2" Grid.Column="1"
          Text="Save" Click="Save_Click" />
</Grid>
```

### DockPanel

```xml
<DockPanel LastChildFill="true">
  <MenuStrip DockPanel.Dock="Top" />
  <StatusStrip DockPanel.Dock="Bottom" />
  <Panel Name="Workspace" />
</DockPanel>
```

### Canvas

```xml
<Canvas>
  <Button Text="Top left"
          Canvas.Left="12"
          Canvas.Top="12" />
  <Button Text="Bottom right"
          Canvas.Right="12"
          Canvas.Bottom="12" />
</Canvas>
```

### Tooltips and sibling stacking

`ToolTip` creates text in the runtime-owned WinForms tooltip component.
`Panel.ZIndex` accepts an integer; a higher value places an overlapping sibling
nearer the front:

```xml
<Panel>
  <PictureBox Name="Preview"
              Image="{Binding PreviewImage}"
              Panel.ZIndex="0" />
  <Button Text="Edit"
          ToolTip="Open the image editor"
          Panel.ZIndex="2" />
</Panel>
```

Both values may use retained binding, function, or preset expressions. A stacking
value outside the current sibling range is clamped safely.

### FlexPanel

For a complete property guide, responsive patterns, wrapping behavior, and
container-selection advice, see [Flex layout](./flex-layout).

```xml
<FlexPanel Direction="Row"
           JustifyContent="Start"
           AlignItems="Center"
           Wrap="true"
           Gap="8">
  <Label Text="Customer" AutoSize="true" />
  <TextBox Name="CustomerQuery"
           MinimumSize="180,0"
           FlexGrow="1" />
  <Button Text="Search" Click="Search_Click" />
</FlexPanel>
```

Use a column for a vertically stretched editor and a right-aligned command row:

```xml
<FlexPanel Direction="Column"
           AlignItems="Stretch"
           Gap="12">
  <Label Text="Notes" AutoSize="true" />
  <TextBox Multiline="true" Height="120" />
  <FlexPanel Direction="Row"
             JustifyContent="End"
             AlignItems="Center"
             Gap="8">
    <Button Text="Cancel" DialogResult="Cancel" />
    <Button Text="Save" Click="Save_Click" />
  </FlexPanel>
</FlexPanel>
```

`Direction` accepts `Row` or `Column`. `JustifyContent` accepts `Start`,
`Center`, `End`, `SpaceBetween`, or `SpaceAround`; `AlignItems` accepts `Start`,
`Center`, `End`, or `Stretch`. Set `Wrap="true"` when rows may break across
lines. Set `FlexGrow="1"` on a child to let it consume remaining space.

## Names and direct access

Use `Name` only when C# needs to find an object:

```xml
<ComboBox Name="Country" DropDownStyle="DropDownList" />
```

```csharp
ComboBox country = ui.Get<ComboBox>("Country");
country.Items.Add("Poland");
country.Items.Add("Germany");
country.SelectedIndex = 0;
```

Names are case-insensitive. Names inside an `ItemsControl.ItemTemplate` belong
to each repeated item and are intentionally not added to the global name map.

## Events

An event attribute names a method on the object passed to `Load` or
`LoadEmbedded`:

```xml
<Button Text="Delete" Click="Delete_Click" />
<TextBox Name="Search" TextChanged="Search_TextChanged" />
<Form Name="MainForm" FormClosing="MainForm_FormClosing" />
```

Use the normal WinForms signatures:

```csharp
private void Delete_Click(object sender, EventArgs e)
{
    Button button = (Button)sender;
    button.Enabled = false;
}

private void Search_TextChanged(object sender, EventArgs e)
{
    TextBox search = (TextBox)sender;
    RunSearch(search.Text);
}

private void MainForm_FormClosing(
    object sender,
    FormClosingEventArgs e)
{
    if (HasUnsavedChanges())
        e.Cancel = true;
}
```

## Styles

Put reusable setters in a `Resources` property element. A style with only a
`TargetType` applies automatically. A style with a `Key` is selected explicitly.

```xml
<Form Name="MainForm" Text="Styled form">
  <Form.Resources>
    <Style TargetType="Button">
      <Setter Property="Padding" Value="8,4" />
      <Setter Property="Margin" Value="0,0,0,8" />
    </Style>

    <Style Key="PrimaryButton" TargetType="Button">
      <Setter Property="BackColor" Value="#2563EB" />
      <Setter Property="ForeColor" Value="White" />
      <Setter Property="FlatStyle" Value="Flat" />
    </Style>
  </Form.Resources>

  <StackPanel Margin="16">
    <Button Text="Normal" />
    <Button Text="Save" Style="PrimaryButton" />
  </StackPanel>
</Form>
```

Style values can use bindings and presets:

```xml
<Style Key="StatusStyle" TargetType="Label">
  <Setter Property="Text" Value="{Binding StatusText}" />
  <Setter Property="ForeColor" Value="{Preset Theme.StatusColor}" />
</Style>
```

A property written directly on the element wins over the same property from a
style:

```xml
<Label Style="StatusStyle" ForeColor="DarkRed" />
```

Native CLR properties take priority over markup conveniences. `ProgressBar`
already owns a WinForms `Style` property, so its native syntax is:

```xml
<ProgressBar Style="Marquee"
             MarqueeAnimationSpeed="35" />
```

If a control with a native `Style` property also needs a named resource style,
select that resource with `ResourceStyle="ProgressBarTheme"`.

## Conditional elements

`Condition` includes an element only when its expression evaluates to true:

```xml
<Label Text="Connected"
       Condition="{Binding IsConnected}" />

<Button Text="Connect"
        Condition="{Binding !IsConnected}"
        Click="Connect_Click" />
```

A dynamic condition that initially resolves to false is retained as a collapsed
element. A `PropertyBinding<bool>` re-evaluates it automatically, including a
false-to-true change. A function condition also observes explicit reactive path
arguments. Snapshot fields and functions without such an argument still need an
explicit binding reload.

`Condition` is one-way and rejects `Mode=TwoWay`. It combines with `Visibility`
and with conditions contributed by component invocations or templates: every
constraint must permit display. `Name` is a static identity and cannot contain
a binding, function, or preset expression. For repeated data, see
the [ItemsControl guide](./items-and-virtualization).

### Conditional styles, setters, and object properties

`Condition` is spelled exactly as shown. It can also select whole resource
styles, individual setters, and writable single-object property elements:

```xml
<Panel.Resources>
  <Style TargetType="Button" Condition="{Binding UseDarkControls}">
    <Setter Property="BackColor" Value="#23272E" />
    <Setter Property="ForeColor" Value="White"
            Condition="{Binding UseHighContrastText}" />
  </Style>
</Panel.Resources>

<ItemsControl.VerticalScrollStyle Condition="{Binding UseCustomScrollBar}">
  <ScrollBarStyle TrackColor="#171A1F"
                  ThumbColor="#596273" />
</ItemsControl.VerticalScrollStyle>
```

When a style or setter condition becomes false, WinFormsXaml removes that
layer and restores the next active style, a local XML value, or the original
WinForms value. A conditional object property restores the value that existed
below it. For the scrollbar property above that value is normally `null`, so
the native scrollbar returns without recreating the `ItemsControl` or its
items. Multiple style and setter conditions for one target are reapplied in one
batched style transition.

An unresolved conditional metadata expression is inactive; it does not apply a
style, setter, or object value. Put `Condition` on the dotted property element,
not directly on `<ScrollBarStyle>`. `<Includes>` also accepts `Condition`, but
its `Source`, `SourceKind`, and `Assembly` remain static composition metadata.
The source is composed once; dynamic transitions retain controls and registered
components in place, gate included styles, and combine nested include
conditions with AND. Included preset declarations remain available as catalogs.

## Unsupported properties fail early

WinFormsXaml reports `WinFormsXamlLoadException` when an element, property,
event, conversion, or binding cannot be resolved. Its `MarkupSource`,
`ElementPath`, and `PropertyName` identify a semantic property failure, for
example `/Form#MainForm/StackPanel/Button#Save` and `Text`. This catches spelling
mistakes and framework-version mismatches during development instead of
silently producing a partly configured interface.

Malformed XML comes directly from the XML parser and includes one-based
`LineNumber` and `LinePosition`; parser behavior is unchanged. Semantic loading
reports the original one-based position of the exact failing attribute when it
is retained, otherwise the position of the deepest opening element. That source
mapping survives item-template clones and registered-component `TemplateXml`
round trips. The original failure remains available through `InnerException`.
See the
[runtime diagnostics reference](/reference/runtime#inspect-load-failures) for
the complete property table and a catch example.

Markup is application code: it can create types, set properties, open local
resources, and connect methods. Do not load untrusted uploaded or network XML
directly.
