# Flex layout

`FlexPanel` arranges normal WinForms controls in a row or column. It is useful
for command bars, responsive forms, groups of cards, and layouts where one
control should consume the space left by its siblings.

## A responsive search row

```xml
<FlexPanel Direction="Row"
           AlignItems="Center"
           Gap="8"
           Padding="12">
  <Label Text="Customer" AutoSize="true" />
  <TextBox Name="CustomerQuery"
           MinWidth="160"
           FlexGrow="1" />
  <Button Text="Search" Click="Search_Click" />
</FlexPanel>
```

The label and button use their preferred widths. `FlexGrow="1"` gives the text
box the remaining horizontal space. A growing child without an explicit
`Width` starts from a zero basis, constrained by `MinWidth`; it is recalculated
from the current available space instead of retaining an earlier arranged
width. An explicit `Width` remains the stable basis across later layout passes.

`FlexPanel` creates and positions ordinary `System.Windows.Forms.Control`
objects. Code-behind still uses their normal WinForms types and events.

## Panel properties

| Property | Values | Meaning |
| --- | --- | --- |
| `Direction` | `Row`, `Column` | Selects the main layout axis. The default is `Row`. |
| `JustifyContent` | `Start`, `Center`, `End`, `SpaceBetween`, `SpaceAround` | Places unused space on the main axis. The default is `Start`. |
| `AlignItems` | `Start`, `Center`, `End`, `Stretch` | Aligns children on the cross axis. The default is `Stretch`. |
| `Wrap` | `true`, `false` | Starts another line when the next child does not fit. The default is `false`. |
| `Gap` | non-negative integer | Adds the same pixel gap between visible children and wrapped lines. |
| `Padding` | WinForms padding | Insets the complete layout area. |

Set `FlexGrow` on a direct child. It is a non-negative relative weight, not a
property of the native child control:

```xml
<FlexPanel Direction="Row" Gap="8">
  <TextBox FlexGrow="1" />
  <TextBox FlexGrow="2" />
</FlexPanel>
```

After bases, margins, and gaps are accounted for, the second text box receives
twice the remaining space of the first. A growing child without an explicit
main-axis size has a zero basis; its applicable minimum can raise that basis.
An explicit main-axis size is the child's stable basis: in a row that is
`Width`, and in a column it is `Height`. `MinWidth`, `MaxWidth`, `MinHeight`,
and `MaxHeight` constrain allocation. When a growing child reaches its maximum,
the unused share is redistributed among the other growing children.

When at least one child grows, `FlexGrow` consumes the available main-axis
space before `JustifyContent` is applied. Therefore `SpaceBetween` normally
matters on a line whose children do not grow.

## Wrapping cards

```xml
<FlexPanel Direction="Row"
           Wrap="true"
           AlignItems="Stretch"
           Gap="12">
  <Border Width="210" Padding="12" BorderThickness="1">
    <Label Text="Open orders" AutoSize="true" />
  </Border>
  <Border Width="210" Padding="12" BorderThickness="1">
    <Label Text="Ready to ship" AutoSize="true" />
  </Border>
  <Border Width="210" Padding="12" BorderThickness="1">
    <Label Text="Delayed" AutoSize="true" />
  </Border>
</FlexPanel>
```

For a row, wrapping compares each child's preferred width, horizontal margin,
and `Gap` with the available width. A child wider than the complete line stays
on its own line. Wrapped lines are separated by the same `Gap` value.

`GetPreferredSize` uses a finite proposed main-axis size as the wrap boundary,
so measurement and arrangement create the same lines. A collapsed child does
not reserve a gap or create an empty line.

For predictable cards, give each card a width or minimum width. For a flexible
editor row, leave the growing editor's width unset and use `FlexGrow`.

## A column with a command row

```xml
<FlexPanel Direction="Column"
           AlignItems="Stretch"
           Gap="12"
           Padding="16">
  <Label Text="Notes" AutoSize="true" />
  <TextBox Multiline="true" FlexGrow="1" MinHeight="100" />

  <FlexPanel Direction="Row"
             JustifyContent="End"
             AlignItems="Center"
             Gap="8">
    <Button Text="Cancel" DialogResult="Cancel" />
    <Button Text="Save" Click="Save_Click" />
  </FlexPanel>
</FlexPanel>
```

In a column, `FlexGrow` distributes remaining height. With
`AlignItems="Stretch"`, a child without an explicit width fills the available
cross-axis width. An explicit width remains authoritative.

## Right-to-left layout

Flex order is always logical and the `Controls` collection is never reversed.
With `FlowDirection="RightToLeft"`, a row starts at the right edge and advances
toward the left. Wrapped rows still progress from top to bottom. A wrapping
column starts at the right edge and each additional column progresses toward
the left. `Start` and `End` cross-axis alignment follow the same logical
direction.

The same markup can therefore be inherited by an LTR or RTL Form without
reordering children in code-behind.

## CSS-like subset

`FlexPanel` intentionally implements a compact CSS-like subset for native
WinForms controls: row/column direction, wrapping, gap, main-axis
justification, cross-axis alignment, and positive `FlexGrow`. It does not
implement the complete browser Flexbox algorithm. There is currently no
`flex-shrink`, `flex-basis`, `order`, `align-content`, or reverse-direction
property. When fixed bases are wider or taller than a line, controls keep their
bounded sizes and may overflow; enable wrapping or choose smaller/minimum sizes
when the layout must fit.

## Dynamic values

Flex properties use the same retained expressions as other markup properties:

```xml
<FlexPanel Direction="{Binding ToolbarDirection}"
           Wrap="{Preset Density.WrapCommands}"
           Gap="{Preset Density.ControlGap}">
  <Button Text="Back" />
  <TextBox FlexGrow="{Binding SearchWeight}" />
  <Button Text="Search" />
</FlexPanel>
```

Bindings and preset changes relayout the panel when a retained value changes.
Use values that convert to the documented enum, boolean, integer, or numeric
property type.

## Choosing flex or another container

- Use `FlexPanel` for logical rows or columns that need positive growth,
  alignment, or wrapping.
- Use `StackPanel` for a simple sequence that does not distribute remaining
  space.
- Use `Grid` when rows and columns must line up across the complete layout.
- Use `DockPanel` for fixed edge regions and one remaining content region.
- Use `ItemsControl` when a data source repeats one template; its template can
  contain a `FlexPanel` for each row.

Keep deeply nested panels only when each level expresses a real layout rule.
One panel with `Gap`, alignment, and growth is usually clearer than several
single-purpose wrapper panels.
