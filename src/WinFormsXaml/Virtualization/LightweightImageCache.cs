using System;
using System.Collections;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace WinFormsXaml
{
    public sealed partial class XamlRuntime
    {
        // Sixteen 256-by-256 32-bit images have a four-megabyte upper bound.
        // Most lightweight rows use much smaller icons, but both limits matter
        // on machines with the memory budgets typical of Windows 98.
        internal const int LightweightThumbnailCacheLimit = 16;
        private const long LightweightThumbnailPixelLimit = 65536L;

        private sealed class LightweightThumbnailCacheEntry
        {
            internal WeakReference Source;
            internal int Generation;
            internal int DestinationWidth;
            internal int DestinationHeight;
            internal ImageStretch Stretch;
            internal CompositingQuality CompositingQuality;
            internal InterpolationMode InterpolationMode;
            internal PixelOffsetMode PixelOffsetMode;
            internal Bitmap Thumbnail;
        }

        private bool TryDrawLightweightThumbnail(
            ItemsControl host,
            Graphics graphics,
            LightweightTemplateNode node,
            Rectangle bounds,
            LightweightRowSnapshot snapshot,
            Image source)
        {
            if (host == null || graphics == null || node == null ||
                snapshot == null || source == null ||
                node.Stretch == ImageStretch.None)
            {
                return false;
            }

            Image eligibleSource =
                snapshot.ThumbnailSources[node.Id];

            // Direct Image values are application-owned and may be mutated at
            // any time. Only the runtime-created, snapshot-owned conversion of
            // an Icon or encoded byte[] is admitted to this cache.
            if (!Object.ReferenceEquals(eligibleSource, source) ||
                ImageAnimator.CanAnimate(source))
            {
                return false;
            }

            if (!host.LightweightThumbnailPaintAllowed)
                return false;

            int thumbnailWidth;
            int thumbnailHeight;

            if (!TryGetLightweightThumbnailSize(
                    source,
                    node.Stretch,
                    bounds.Size,
                    out thumbnailWidth,
                    out thumbnailHeight))
            {
                return false;
            }

            Bitmap thumbnail;

            try
            {
                thumbnail = GetLightweightThumbnail(
                    host,
                    graphics,
                    source,
                    snapshot.Generation,
                    bounds.Width,
                    bounds.Height,
                    thumbnailWidth,
                    thumbnailHeight,
                    node.Stretch);
            }
            catch (OutOfMemoryException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                return false;
            }

            if (thumbnail == null)
                return false;

            int x = bounds.X + (bounds.Width - thumbnail.Width) / 2;
            int y = bounds.Y + (bounds.Height - thumbnail.Height) / 2;
            graphics.DrawImageUnscaled(thumbnail, x, y);
            return true;
        }

        private static bool CanUseLightweightThumbnailTransform(
            Graphics graphics)
        {
            if (graphics == null)
                return false;

            try
            {
                using (Matrix transform = graphics.Transform)
                    return transform.IsIdentity;
            }
            catch (OutOfMemoryException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                return false;
            }
        }

        private static void DrawLightweightScaledImage(
            ItemsControl host,
            Graphics graphics,
            Image source,
            Rectangle destination,
            RectangleF sourceRectangle)
        {
            ImageAttributes attributes =
                host.LightweightImageDrawAttributes;

            if (attributes == null)
            {
                attributes = new ImageAttributes();

                try
                {
                    // GDI+ samples outside a one-pixel source edge while
                    // upscaling, which blends the image with the destination.
                    // TileFlipXY supplies edge pixels without changing the
                    // selected interpolation mode. Keep one native attributes
                    // object per ItemsControl rather than allocating in Paint.
                    attributes.SetWrapMode(WrapMode.TileFlipXY);
                    host.LightweightImageDrawAttributes = attributes;
                }
                catch
                {
                    attributes.Dispose();
                    throw;
                }
            }

            graphics.DrawImage(
                source,
                destination,
                sourceRectangle.X,
                sourceRectangle.Y,
                sourceRectangle.Width,
                sourceRectangle.Height,
                GraphicsUnit.Pixel,
                attributes);
        }

        private static bool TryGetLightweightThumbnailSize(
            Image source,
            ImageStretch stretch,
            Size destination,
            out int width,
            out int height)
        {
            width = 0;
            height = 0;

            if (destination.Width <= 0 || destination.Height <= 0 ||
                source.Width <= 0 || source.Height <= 0)
            {
                return false;
            }

            if (stretch == ImageStretch.Uniform)
            {
                float scale = Math.Min(
                    (float)destination.Width / (float)source.Width,
                    (float)destination.Height / (float)source.Height);

                // Upscaling is deliberately left on the direct draw path. The
                // cache is for bounded thumbnails, not arbitrary rendered sizes.
                if (scale >= 1.0f)
                    return false;

                width = Math.Max(
                    1,
                    (int)Math.Round(source.Width * scale));
                height = Math.Max(
                    1,
                    (int)Math.Round(source.Height * scale));
            }
            else if (stretch == ImageStretch.UniformToFill)
            {
                float scale = Math.Max(
                    (float)destination.Width / (float)source.Width,
                    (float)destination.Height / (float)source.Height);

                if (scale >= 1.0f)
                    return false;

                width = destination.Width;
                height = destination.Height;
            }
            else if (stretch == ImageStretch.Fill)
            {
                if (destination.Width > source.Width ||
                    destination.Height > source.Height ||
                    (destination.Width == source.Width &&
                     destination.Height == source.Height))
                {
                    return false;
                }

                width = destination.Width;
                height = destination.Height;
            }
            else
            {
                return false;
            }

            return (long)width * height <=
                LightweightThumbnailPixelLimit;
        }

        private Bitmap GetLightweightThumbnail(
            ItemsControl host,
            Graphics destinationGraphics,
            Image source,
            int generation,
            int destinationWidth,
            int destinationHeight,
            int thumbnailWidth,
            int thumbnailHeight,
            ImageStretch stretch)
        {
            ArrayList cache = host.LightweightThumbnailCache;

            if (cache == null)
            {
                cache = new ArrayList();
                host.LightweightThumbnailCache = cache;
            }

            int i;

            for (i = cache.Count - 1; i >= 0; i--)
            {
                LightweightThumbnailCacheEntry entry =
                    cache[i] as LightweightThumbnailCacheEntry;
                object cachedSource = entry == null || entry.Source == null
                    ? null
                    : entry.Source.Target;

                if (entry == null || cachedSource == null ||
                    entry.Thumbnail == null)
                {
                    cache.RemoveAt(i);
                    DisposeLightweightThumbnailEntry(entry);
                    continue;
                }

                if (!Object.ReferenceEquals(cachedSource, source) ||
                    entry.Generation != generation ||
                    entry.DestinationWidth != destinationWidth ||
                    entry.DestinationHeight != destinationHeight ||
                    entry.Stretch != stretch ||
                    entry.CompositingQuality !=
                        destinationGraphics.CompositingQuality ||
                    entry.InterpolationMode !=
                        destinationGraphics.InterpolationMode ||
                    entry.PixelOffsetMode !=
                        destinationGraphics.PixelOffsetMode)
                {
                    continue;
                }

                if (i != cache.Count - 1)
                {
                    cache.RemoveAt(i);
                    cache.Add(entry);
                }

                return entry.Thumbnail;
            }

            while (cache.Count >= LightweightThumbnailCacheLimit)
            {
                LightweightThumbnailCacheEntry oldest =
                    cache[0] as LightweightThumbnailCacheEntry;
                cache.RemoveAt(0);
                DisposeLightweightThumbnailEntry(oldest);
            }

            LightweightThumbnailCacheEntry created =
                new LightweightThumbnailCacheEntry();
            created.Source = new WeakReference(source);
            created.Generation = generation;
            created.DestinationWidth = destinationWidth;
            created.DestinationHeight = destinationHeight;
            created.Stretch = stretch;
            created.CompositingQuality =
                destinationGraphics.CompositingQuality;
            created.InterpolationMode =
                destinationGraphics.InterpolationMode;
            created.PixelOffsetMode =
                destinationGraphics.PixelOffsetMode;
            created.Thumbnail = CreateLightweightThumbnail(
                destinationGraphics,
                source,
                destinationWidth,
                destinationHeight,
                thumbnailWidth,
                thumbnailHeight,
                stretch);

            try
            {
                cache.Add(created);
            }
            catch
            {
                DisposeLightweightThumbnailEntry(created);
                throw;
            }

            return created.Thumbnail;
        }

        private static Bitmap CreateLightweightThumbnail(
            Graphics destinationGraphics,
            Image source,
            int destinationWidth,
            int destinationHeight,
            int thumbnailWidth,
            int thumbnailHeight,
            ImageStretch stretch)
        {
            Bitmap thumbnail =
                new Bitmap(thumbnailWidth, thumbnailHeight);

            try
            {
                using (Graphics graphics = Graphics.FromImage(thumbnail))
                {
                    graphics.CompositingMode = CompositingMode.SourceCopy;

                    if (destinationGraphics.CompositingQuality !=
                        CompositingQuality.Invalid)
                    {
                        graphics.CompositingQuality =
                            destinationGraphics.CompositingQuality;
                    }

                    if (destinationGraphics.InterpolationMode !=
                        InterpolationMode.Invalid)
                    {
                        graphics.InterpolationMode =
                            destinationGraphics.InterpolationMode;
                    }

                    if (destinationGraphics.PixelOffsetMode !=
                        PixelOffsetMode.Invalid)
                    {
                        graphics.PixelOffsetMode =
                            destinationGraphics.PixelOffsetMode;
                    }

                    Rectangle thumbnailBounds = new Rectangle(
                        0,
                        0,
                        thumbnailWidth,
                        thumbnailHeight);

                    if (stretch == ImageStretch.UniformToFill)
                    {
                        RectangleF sourceRectangle =
                            GetLightweightUniformToFillSource(
                                source.Width,
                                source.Height,
                                destinationWidth,
                                destinationHeight);
                        graphics.DrawImage(
                            source,
                            thumbnailBounds,
                            sourceRectangle.X,
                            sourceRectangle.Y,
                            sourceRectangle.Width,
                            sourceRectangle.Height,
                            GraphicsUnit.Pixel);
                    }
                    else
                    {
                        graphics.DrawImage(source, thumbnailBounds);
                    }
                }
            }
            catch
            {
                thumbnail.Dispose();
                throw;
            }

            return thumbnail;
        }

        private void ReleaseLightweightRowSnapshot(
            ItemsControl host,
            LightweightRowSnapshot snapshot)
        {
            if (snapshot == null)
                return;

            snapshot.Retired = true;
            Exception firstError = null;

            try
            {
                // Owner-indexed detachment also sees registrations that are
                // currently inside a user-controlled INPC event add accessor.
                DetachObservableBindings(snapshot);
            }
            catch (Exception ex)
            {
                firstError = ex;
            }

            try
            {
                RemoveLightweightThumbnailsForSnapshot(host, snapshot);
            }
            catch (Exception ex)
            {
                firstError = FirstItemsCommitError(firstError, ex);
            }

            try
            {
                ReleaseOwnedPropertyValues(snapshot);
            }
            catch (Exception ex)
            {
                firstError = FirstItemsCommitError(firstError, ex);
            }

            Array.Clear(snapshot.Values, 0, snapshot.Values.Length);
            Array.Clear(
                snapshot.ConvertedValues,
                0,
                snapshot.ConvertedValues.Length);
            Array.Clear(
                snapshot.TextValues,
                0,
                snapshot.TextValues.Length);
            snapshot.FunctionResults.Clear();
            Array.Clear(snapshot.Images, 0, snapshot.Images.Length);
            Array.Clear(
                snapshot.ThumbnailSources,
                0,
                snapshot.ThumbnailSources.Length);
            Array.Clear(snapshot.LinkKeys, 0, snapshot.LinkKeys.Length);
            snapshot.Item = null;
            snapshot.StableItemKey = null;
            snapshot.Host = null;

            if (firstError != null)
                throw firstError;
        }

        private void RemoveLightweightThumbnailsForSnapshot(
            ItemsControl host,
            LightweightRowSnapshot snapshot)
        {
            if (host == null || snapshot == null ||
                snapshot.ThumbnailSources.Length == 0)
            {
                return;
            }

            Exception firstError = null;
            int i;

            for (i = 0; i < snapshot.ThumbnailSources.Length; i++)
            {
                Image source = snapshot.ThumbnailSources[i];

                if (source == null)
                    continue;

                if (HasLiveLightweightThumbnailSource(
                        host,
                        source,
                        snapshot.Generation))
                {
                    continue;
                }

                try
                {
                    RemoveLightweightThumbnails(
                        host,
                        source,
                        snapshot.Generation);
                }
                catch (Exception ex)
                {
                    firstError = FirstItemsCommitError(firstError, ex);
                }
            }

            if (firstError != null)
                throw firstError;
        }

        private static bool HasLiveLightweightThumbnailSource(
            ItemsControl host,
            Image source,
            int generation)
        {
            if (host == null || source == null ||
                host.LightweightRowCache == null)
            {
                return false;
            }

            foreach (DictionaryEntry rowEntry in host.LightweightRowCache)
            {
                LightweightRowSnapshot live =
                    rowEntry.Value as LightweightRowSnapshot;

                if (live == null || live.Generation != generation)
                    continue;

                int i;

                for (i = 0; i < live.ThumbnailSources.Length; i++)
                {
                    if (Object.ReferenceEquals(
                            live.ThumbnailSources[i],
                            source))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void RemoveLightweightThumbnails(
            ItemsControl host,
            Image source,
            int generation)
        {
            ArrayList cache = host == null
                ? null
                : host.LightweightThumbnailCache;

            if (cache == null || source == null)
                return;

            Exception firstError = null;
            int i;

            for (i = cache.Count - 1; i >= 0; i--)
            {
                LightweightThumbnailCacheEntry entry =
                    cache[i] as LightweightThumbnailCacheEntry;
                object cachedSource = entry == null || entry.Source == null
                    ? null
                    : entry.Source.Target;

                if (entry != null &&
                    entry.Generation != generation)
                {
                    continue;
                }

                if (entry != null && cachedSource != null &&
                    !Object.ReferenceEquals(cachedSource, source))
                {
                    continue;
                }

                cache.RemoveAt(i);

                try
                {
                    DisposeLightweightThumbnailEntry(entry);
                }
                catch (Exception ex)
                {
                    firstError = FirstItemsCommitError(firstError, ex);
                }
            }

            if (firstError != null)
                throw firstError;
        }

        private void ClearLightweightThumbnailCache(ItemsControl host)
        {
            ArrayList cache = host == null
                ? null
                : host.LightweightThumbnailCache;

            if (cache == null || cache.Count == 0)
                return;

            ArrayList entries = new ArrayList(cache);
            cache.Clear();
            Exception firstError = null;
            int i;

            for (i = 0; i < entries.Count; i++)
            {
                try
                {
                    DisposeLightweightThumbnailEntry(
                        entries[i] as LightweightThumbnailCacheEntry);
                }
                catch (Exception ex)
                {
                    firstError = FirstItemsCommitError(firstError, ex);
                }
            }

            if (firstError != null)
                throw firstError;
        }

        private static void DisposeLightweightThumbnailEntry(
            LightweightThumbnailCacheEntry entry)
        {
            if (entry == null)
                return;

            Bitmap thumbnail = entry.Thumbnail;
            entry.Thumbnail = null;
            entry.Source = null;

            if (thumbnail != null)
                thumbnail.Dispose();
        }
    }
}
