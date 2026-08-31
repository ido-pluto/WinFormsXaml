using System;
using System.Windows.Forms;
using WinFormsXaml;

namespace ComponentsGallery.UI.Components
{
    /// <summary>
    /// Optional per-invocation code-behind for the GalleryCard XML component.
    /// </summary>
    public sealed class GalleryCard
    {
        public readonly PropertyBinding<string> Title =
            new PropertyBinding<string>(String.Empty);

        public readonly ChildrenBind Children =
            new ChildrenBind();

        private void UpdateFromComponent_Click(
            object sender,
            EventArgs e)
        {
            TextBox editor = Children.Get<TextBox>("TitleEditor");
            string value = editor.Text;

            Title.Value = String.IsNullOrEmpty(value)
                ? "Updated by component code-behind"
                : value + " - updated by component code-behind";
        }
    }
}
