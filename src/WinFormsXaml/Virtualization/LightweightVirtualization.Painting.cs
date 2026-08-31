using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Xml;
using System.Windows.Forms;

namespace WinFormsXaml
{
    public sealed partial class XamlRuntime
    {
        private void DrawLightweightNode(
            ItemsControl host,
            Graphics graphics,
            LightweightTemplateNode node,
            Rectangle allocation,
            LightweightRowSnapshot snapshot,
            int index)
        {
            Rectangle bounds = ApplyLightweightBox(node, allocation);

            if (bounds.Width <= 0 || bounds.Height <= 0)
                return;

            Color background = ResolveLightweightColor(
                host,
                node.BackColor,
                snapshot,
                Color.Transparent);

            if (background.A != 0)
            {
                graphics.FillRectangle(
                    GetLightweightBrush(host, background),
                    bounds);
            }

            if (node.Kind == LightweightNodeKind.Border)
            {
                DrawLightweightBorder(host, graphics, node, bounds, snapshot);
                Rectangle inner = DeflateLightweightRectangle(
                    bounds,
                    AddPadding(node.BorderThickness, node.Padding));

                if (node.Children.Count == 1)
                {
                    DrawLightweightNode(
                        host,
                        graphics,
                        node.Children[0] as LightweightTemplateNode,
                        inner,
                        snapshot,
                        index);
                }

                return;
            }

            if (node.Kind == LightweightNodeKind.StackPanel)
            {
                DrawLightweightStack(
                    host,
                    graphics,
                    node,
                    DeflateLightweightRectangle(bounds, node.Padding),
                    snapshot,
                    index);
                return;
            }

            Rectangle content = DeflateLightweightRectangle(
                bounds,
                node.Padding);

            if (node.Kind == LightweightNodeKind.Image)
            {
                DrawLightweightImage(
                    host,
                    graphics,
                    node,
                    content,
                    snapshot);
                return;
            }

            bool enabled = ResolveLightweightBoolean(
                host,
                node.Enabled,
                snapshot,
                true);

            if (node.Kind == LightweightNodeKind.CheckBox)
            {
                DrawLightweightCheckBox(
                    host,
                    graphics,
                    node,
                    content,
                    snapshot,
                    enabled);
            }
            else
            {
                DrawLightweightTextLeaf(
                    host,
                    graphics,
                    node,
                    content,
                    snapshot,
                    enabled,
                    index);
            }
        }

        private void DrawLightweightBorder(
            ItemsControl host,
            Graphics graphics,
            LightweightTemplateNode node,
            Rectangle bounds,
            LightweightRowSnapshot snapshot)
        {
            Padding thickness = node.BorderThickness;

            if (thickness == Padding.Empty)
                return;

            Color color = ResolveLightweightColor(
                host,
                node.BorderColor,
                snapshot,
                SystemColors.ControlDark);

            Brush brush = GetLightweightBrush(host, color);

            if (thickness.Top > 0)
            {
                graphics.FillRectangle(
                    brush,
                    bounds.Left,
                    bounds.Top,
                    bounds.Width,
                    Math.Min(thickness.Top, bounds.Height));
            }

            if (thickness.Bottom > 0)
            {
                int height = Math.Min(thickness.Bottom, bounds.Height);
                graphics.FillRectangle(
                    brush,
                    bounds.Left,
                    bounds.Bottom - height,
                    bounds.Width,
                    height);
            }

            if (thickness.Left > 0)
            {
                graphics.FillRectangle(
                    brush,
                    bounds.Left,
                    bounds.Top,
                    Math.Min(thickness.Left, bounds.Width),
                    bounds.Height);
            }

            if (thickness.Right > 0)
            {
                int width = Math.Min(thickness.Right, bounds.Width);
                graphics.FillRectangle(
                    brush,
                    bounds.Right - width,
                    bounds.Top,
                    width,
                    bounds.Height);
            }
        }

        private void DrawLightweightStack(
            ItemsControl host,
            Graphics graphics,
            LightweightTemplateNode node,
            Rectangle bounds,
            LightweightRowSnapshot snapshot,
            int index)
        {
            int count = node.Children.Count;

            if (count == 0)
                return;

            int available = node.Orientation == Orientation.Horizontal
                ? bounds.Width
                : bounds.Height;
            available = Math.Max(
                0,
                available - (node.Spacing * Math.Max(0, count - 1)));
            int fixedExtent = 0;
            int flexible = 0;
            int i;

            for (i = 0; i < count; i++)
            {
                LightweightTemplateNode child =
                    node.Children[i] as LightweightTemplateNode;
                int explicitExtent = node.Orientation == Orientation.Horizontal
                    ? child.Width
                    : child.Height;
                int margins = node.Orientation == Orientation.Horizontal
                    ? child.Margin.Left + child.Margin.Right
                    : child.Margin.Top + child.Margin.Bottom;

                if (explicitExtent >= 0)
                    fixedExtent += explicitExtent + margins;
                else
                    flexible++;
            }

            int remainder = Math.Max(0, available - fixedExtent);
            int cursor = node.Orientation == Orientation.Horizontal
                ? bounds.Left
                : bounds.Top;
            int flexibleSeen = 0;

            for (i = 0; i < count; i++)
            {
                LightweightTemplateNode child =
                    node.Children[i] as LightweightTemplateNode;
                int explicitExtent = node.Orientation == Orientation.Horizontal
                    ? child.Width
                    : child.Height;
                int extent;

                if (explicitExtent >= 0)
                {
                    int margins = node.Orientation == Orientation.Horizontal
                        ? child.Margin.Left + child.Margin.Right
                        : child.Margin.Top + child.Margin.Bottom;
                    extent = explicitExtent + margins;
                }
                else
                {
                    flexibleSeen++;
                    extent = flexible == 0
                        ? 0
                        : (flexibleSeen == flexible
                            ? remainder -
                                ((remainder / flexible) * (flexible - 1))
                            : remainder / flexible);
                }

                Rectangle slot = node.Orientation == Orientation.Horizontal
                    ? new Rectangle(cursor, bounds.Top, extent, bounds.Height)
                    : new Rectangle(bounds.Left, cursor, bounds.Width, extent);

                DrawLightweightNode(
                    host,
                    graphics,
                    child,
                    slot,
                    snapshot,
                    index);
                cursor += extent + node.Spacing;
            }
        }

        private void DrawLightweightTextLeaf(
            ItemsControl host,
            Graphics graphics,
            LightweightTemplateNode node,
            Rectangle bounds,
            LightweightRowSnapshot snapshot,
            bool enabled,
            int index)
        {
            string text = ResolveLightweightText(
                host,
                node.Text,
                snapshot,
                String.Empty);
            Color color;

            if (!enabled)
            {
                color = SystemColors.GrayText;
            }
            else if (node.Kind == LightweightNodeKind.HyperlinkLabel)
            {
                bool visited = IsLightweightLinkVisited(
                    host,
                    snapshot,
                    node);
                color = ResolveLightweightColor(
                    host,
                    visited ? node.VisitedLinkColor : node.LinkColor,
                    snapshot,
                    visited ? Color.Purple : Color.Blue);
            }
            else
            {
                color = ResolveLightweightColor(
                    host,
                    node.ForeColor,
                    snapshot,
                    host.ForeColor);
            }

            Font font = GetLightweightFont(host, node);

            TextRenderer.DrawText(
                graphics,
                text,
                font,
                bounds,
                color,
                GetLightweightTextFlags(
                    host,
                    node.TextAlign,
                    node.AutoEllipsis));
        }

        private void DrawLightweightCheckBox(
            ItemsControl host,
            Graphics graphics,
            LightweightTemplateNode node,
            Rectangle bounds,
            LightweightRowSnapshot snapshot,
            bool enabled)
        {
            bool isChecked = ResolveLightweightBoolean(
                host,
                node.Checked,
                snapshot,
                false);
            Size glyph = SystemInformation.MenuCheckSize;
            int glyphWidth = Math.Max(13, glyph.Width);
            int glyphHeight = Math.Max(13, glyph.Height);
            bool right = IsRightAligned(node.CheckAlign) ||
                host.ContentRightToLeft;
            Rectangle glyphBounds = new Rectangle(
                right
                    ? Math.Max(bounds.Left, bounds.Right - glyphWidth)
                    : bounds.Left,
                bounds.Top + Math.Max(0, (bounds.Height - glyphHeight) / 2),
                Math.Min(glyphWidth, bounds.Width),
                Math.Min(glyphHeight, bounds.Height));
            ButtonState state = isChecked
                ? ButtonState.Checked
                : ButtonState.Normal;

            if (!enabled)
                state |= ButtonState.Inactive;

            ControlPaint.DrawCheckBox(graphics, glyphBounds, state);
            Rectangle textBounds = bounds;
            int gap = 3;

            if (right)
                textBounds.Width = Math.Max(0, glyphBounds.Left - gap - bounds.Left);
            else
            {
                int left = glyphBounds.Right + gap;
                textBounds.X = left;
                textBounds.Width = Math.Max(0, bounds.Right - left);
            }

            string text = ResolveLightweightText(
                host,
                node.Text,
                snapshot,
                String.Empty);
            Color color = enabled
                ? ResolveLightweightColor(
                    host,
                    node.ForeColor,
                    snapshot,
                    host.ForeColor)
                : SystemColors.GrayText;
            TextRenderer.DrawText(
                graphics,
                text,
                GetLightweightFont(host, node),
                textBounds,
                color,
                GetLightweightTextFlags(host, node.TextAlign, node.AutoEllipsis));
        }

        private void DrawLightweightImage(
            ItemsControl host,
            Graphics graphics,
            LightweightTemplateNode node,
            Rectangle bounds,
            LightweightRowSnapshot snapshot)
        {
            Image image = ResolveLightweightImage(
                host,
                node,
                snapshot);

            ValidateLightweightImage(node, image);

            if (image == null || bounds.Width <= 0 || bounds.Height <= 0 ||
                image.Width <= 0 || image.Height <= 0)
            {
                return;
            }

            System.Drawing.Drawing2D.GraphicsState state = graphics.Save();

            try
            {
                graphics.SetClip(
                    bounds,
                    System.Drawing.Drawing2D.CombineMode.Intersect);

                if (TryDrawLightweightThumbnail(
                        host,
                        graphics,
                        node,
                        bounds,
                        snapshot,
                        image))
                {
                    return;
                }

                if (node.Stretch == ImageStretch.Fill)
                {
                    DrawLightweightScaledImage(
                        host,
                        graphics,
                        image,
                        bounds,
                        new RectangleF(
                            0.0f,
                            0.0f,
                            image.Width,
                            image.Height));
                }
                else if (node.Stretch == ImageStretch.None)
                {
                    Rectangle destination = new Rectangle(
                        bounds.X + (bounds.Width - image.Width) / 2,
                        bounds.Y + (bounds.Height - image.Height) / 2,
                        image.Width,
                        image.Height);
                    graphics.DrawImage(image, destination);
                }
                else if (node.Stretch == ImageStretch.UniformToFill)
                {
                    RectangleF source = GetLightweightUniformToFillSource(
                        image.Width,
                        image.Height,
                        bounds.Width,
                        bounds.Height);
                    DrawLightweightScaledImage(
                        host,
                        graphics,
                        image,
                        bounds,
                        source);
                }
                else
                {
                    float scale = Math.Min(
                        (float)bounds.Width / (float)image.Width,
                        (float)bounds.Height / (float)image.Height);
                    int width = Math.Max(
                        1,
                        (int)Math.Round(image.Width * scale));
                    int height = Math.Max(
                        1,
                        (int)Math.Round(image.Height * scale));
                    Rectangle destination = new Rectangle(
                        bounds.X + (bounds.Width - width) / 2,
                        bounds.Y + (bounds.Height - height) / 2,
                        width,
                        height);
                    DrawLightweightScaledImage(
                        host,
                        graphics,
                        image,
                        destination,
                        new RectangleF(
                            0.0f,
                            0.0f,
                            image.Width,
                            image.Height));
                }
            }
            finally
            {
                graphics.Restore(state);
            }
        }

        private void ValidateLightweightImage(
            LightweightTemplateNode node,
            Image image)
        {
            if (image == null)
                return;

            try
            {
                // Accessing dimensions also detects an application-provided
                // Image that was disposed before this paint generation.
                int width = image.Width;
                int height = image.Height;

                if (width < 0 || height < 0)
                {
                    throw new InvalidOperationException(
                        "Image dimensions cannot be negative.");
                }

                if (ImageAnimator.CanAnimate(image))
                {
                    throw new InvalidOperationException(
                        "Animated images require the Controls backend. The " +
                        "lightweight surface intentionally has no per-image " +
                        "animation registration or timer.");
                }
            }
            catch (WinFormsXamlLoadException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw CreateMarkupLoadException(
                    node.SourceElement,
                    "Source",
                    ex);
            }
        }

        private Image ResolveLightweightImage(
            ItemsControl host,
            LightweightTemplateNode node,
            LightweightRowSnapshot snapshot)
        {
            object cached = snapshot.Images[node.Id];

            if (cached != null)
            {
                return Object.ReferenceEquals(
                        cached,
                        LightweightCachedNullValue)
                    ? null
                    : cached as Image;
            }

            object source = ResolveLightweightValue(
                host,
                node.Source,
                snapshot);

            if (source == null || IsUnsetPresetValue(source))
            {
                snapshot.Images[node.Id] = LightweightCachedNullValue;
                return null;
            }

            Image image = source as Image;
            bool ownsImage = false;

            if (image == null)
            {
                Icon icon = source as Icon;

                if (icon != null)
                {
                    image = GetDecodedImageFromIcon(icon);
                    ownsImage = true;
                }
                else
                {
                    byte[] bytes = source as byte[];

                    if (bytes != null)
                    {
                        _decodedImageCacheForcedValidationDepth++;

                        try
                        {
                            image = GetDecodedImageFromBytes(bytes);
                        }
                        finally
                        {
                            _decodedImageCacheForcedValidationDepth--;
                        }

                        ownsImage = true;
                    }
                }
            }

            if (image == null)
            {
                throw LightweightMarkupError(
                    node.SourceElement,
                    "Source",
                    "Lightweight Image.Source must resolve to Image, Icon, or " +
                    "encoded byte[]. URI/file strings require the Controls backend.");
            }

            if (ownsImage)
            {
                ReplaceOwnedPropertyValue(
                    snapshot,
                    "LightweightImage." +
                        node.Id.ToString(CultureInfo.InvariantCulture),
                    image);
                snapshot.ThumbnailSources[node.Id] = image;
            }

            snapshot.Images[node.Id] = image;
            return image;
        }

        private static RectangleF GetLightweightUniformToFillSource(
            int imageWidth,
            int imageHeight,
            int destinationWidth,
            int destinationHeight)
        {
            long scaledWidth =
                (long)imageWidth * destinationHeight;
            long scaledHeight =
                (long)imageHeight * destinationWidth;

            if (scaledWidth > scaledHeight)
            {
                float sourceWidth =
                    ((float)imageHeight * destinationWidth) /
                    destinationHeight;
                return new RectangleF(
                    (imageWidth - sourceWidth) / 2.0f,
                    0.0f,
                    sourceWidth,
                    imageHeight);
            }

            if (scaledWidth < scaledHeight)
            {
                float sourceHeight =
                    ((float)imageWidth * destinationHeight) /
                    destinationWidth;
                return new RectangleF(
                    0.0f,
                    (imageHeight - sourceHeight) / 2.0f,
                    imageWidth,
                    sourceHeight);
            }

            return new RectangleF(0.0f, 0.0f, imageWidth, imageHeight);
        }

        private static bool IsRightAligned(ContentAlignment alignment)
        {
            return alignment == ContentAlignment.TopRight ||
                   alignment == ContentAlignment.MiddleRight ||
                   alignment == ContentAlignment.BottomRight;
        }

        private static Font GetLightweightFont(
            ItemsControl host,
            LightweightTemplateNode node)
        {
            Font baseFont = host.Font;
            bool custom =
                !String.IsNullOrEmpty(node.FontFamily) ||
                node.FontSizeInPoints > 0.0f ||
                node.FontStyleSpecified ||
                node.Kind == LightweightNodeKind.HyperlinkLabel;

            if (!custom)
                return baseFont;

            if (node.CachedFont != null &&
                Object.ReferenceEquals(node.CachedBaseFont, baseFont))
            {
                return node.CachedFont;
            }

            if (node.CachedFont != null)
                node.CachedFont.Dispose();

            string family = String.IsNullOrEmpty(node.FontFamily)
                ? baseFont.FontFamily.Name
                : node.FontFamily;
            float size = node.FontSizeInPoints > 0.0f
                ? node.FontSizeInPoints
                : baseFont.SizeInPoints;
            FontStyle style = node.FontStyleSpecified
                ? node.FontStyle
                : baseFont.Style;

            if (node.Kind == LightweightNodeKind.HyperlinkLabel)
                style |= FontStyle.Underline;

            node.CachedFont = new Font(
                family,
                size,
                style,
                GraphicsUnit.Point);
            node.CachedBaseFont = baseFont;
            return node.CachedFont;
        }

        private static TextFormatFlags GetLightweightTextFlags(
            ItemsControl host,
            ContentAlignment alignment,
            bool ellipsis)
        {
            TextFormatFlags flags =
                TextFormatFlags.NoPrefix |
                TextFormatFlags.SingleLine |
                TextFormatFlags.PreserveGraphicsClipping;

            if (ellipsis)
                flags |= TextFormatFlags.EndEllipsis;

            if (alignment == ContentAlignment.TopCenter ||
                alignment == ContentAlignment.MiddleCenter ||
                alignment == ContentAlignment.BottomCenter)
            {
                flags |= TextFormatFlags.HorizontalCenter;
            }
            else if (alignment == ContentAlignment.TopRight ||
                     alignment == ContentAlignment.MiddleRight ||
                     alignment == ContentAlignment.BottomRight)
            {
                flags |= TextFormatFlags.Right;
            }
            else
            {
                flags |= TextFormatFlags.Left;
            }

            if (alignment == ContentAlignment.MiddleLeft ||
                alignment == ContentAlignment.MiddleCenter ||
                alignment == ContentAlignment.MiddleRight)
            {
                flags |= TextFormatFlags.VerticalCenter;
            }
            else if (alignment == ContentAlignment.BottomLeft ||
                     alignment == ContentAlignment.BottomCenter ||
                     alignment == ContentAlignment.BottomRight)
            {
                flags |= TextFormatFlags.Bottom;
            }

            if (host.ContentRightToLeft)
                flags |= TextFormatFlags.RightToLeft;

            return flags;
        }

        private static Rectangle ApplyLightweightBox(
            LightweightTemplateNode node,
            Rectangle allocation)
        {
            Rectangle bounds = DeflateLightweightRectangle(
                allocation,
                node.Margin);

            if (node.Width >= 0)
                bounds.Width = Math.Min(bounds.Width, node.Width);

            if (node.Height >= 0)
                bounds.Height = Math.Min(bounds.Height, node.Height);

            return bounds;
        }

        private static Rectangle DeflateLightweightRectangle(
            Rectangle rectangle,
            Padding padding)
        {
            int left = Math.Max(0, padding.Left);
            int top = Math.Max(0, padding.Top);
            int right = Math.Max(0, padding.Right);
            int bottom = Math.Max(0, padding.Bottom);
            return new Rectangle(
                rectangle.X + left,
                rectangle.Y + top,
                Math.Max(0, rectangle.Width - left - right),
                Math.Max(0, rectangle.Height - top - bottom));
        }

        private static Padding AddPadding(Padding first, Padding second)
        {
            return new Padding(
                first.Left + second.Left,
                first.Top + second.Top,
                first.Right + second.Right,
                first.Bottom + second.Bottom);
        }

    }
}
