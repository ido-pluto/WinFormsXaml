# BindingPlayground sample

This application puts the binding features in one small, editable screen:

- stable public readonly `PropertyBinding<T>` fields for reactive and two-way
  values, updated through `.Value`;
- the public `PropertyBinding<string>.ValueChanged` event;
- one-way and two-way native control properties;
- immediate, lost-focus, and explicit TwoWay source-update triggers, including
  the `XmlForm.UpdateBindingSource(name, property)` commit shortcut;
- a nested path ending in another stable `PropertyBinding<string>`;
- a function whose explicit arguments are reactive;
- `{Binding !IsReady}` negation and a dynamic `Condition`;
- a plain public snapshot field with an explicit `ReloadBinding` after mutation.

The XML is embedded as `BindingPlayground.UI.MainForm.xml`. `MainForm` uses the
parameterless `XmlForm` convention, so application startup only needs:

```csharp
new MainForm().Start();
```

Open `UI/MainForm.xml` in Visual Studio to use the linked
`WinFormsXaml.xsd` IntelliSense schema.
