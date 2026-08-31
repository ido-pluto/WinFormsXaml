using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml;
using System.Windows.Forms;

namespace WinFormsXaml
{
    public sealed partial class XamlRuntime : IDisposable
    {
        // ============================================================
        // REFLECTION PROPERTY SETTING
        // ============================================================

        private bool TrySetProperty(
            object instance,
            string propertyName,
            string value)
        {
            PropertyInfo property =
                FindProperty(
                    instance.GetType(),
                    propertyName);

            if (property == null ||
                !property.CanWrite)
            {
                return false;
            }

            SetPropertyValue(
                instance,
                property,
                value);

            return true;
        }

        private void SetPropertyValue(
            object instance,
            PropertyInfo property,
            string value)
        {
            if (IsExecutingCompiledControlBlueprint)
            {
                IncrementCompiledControlBlueprintCounter(
                    ref _compiledControlBlueprintStringConversionCount);
            }

            object converted = null;
            bool ownsConverted = false;

            try
            {
                converted =
                    ConvertString(
                        value,
                        property.PropertyType);

                ownsConverted =
                    converted is IDisposable &&
                    (property.PropertyType == typeof(Image) ||
                     property.PropertyType == typeof(Bitmap) ||
                     property.PropertyType == typeof(Icon) ||
                     property.PropertyType == typeof(Font));
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Cannot convert '" +
                    value +
                    "' to " +
                    property.PropertyType.FullName +
                    " for " +
                    instance.GetType().Name +
                    "." +
                    property.Name +
                    ": " +
                    ex.Message,
                    ex);
            }

            object previousValue;
            bool previousValueKnown = TryReadPropertyValue(
                instance,
                property,
                out previousValue);
            StyleMetadataState previousMetadata =
                PublishNativePropertyMetadata(
                    instance,
                    property.Name,
                    converted);

            try
            {
                property.SetValue(
                    instance,
                    converted,
                    null);
            }
            catch (Exception ex)
            {
                object actualValue;
                bool actualValueKnown;

                ReconcileFailedPropertyAssignment(
                    instance,
                    property,
                    previousValue,
                    previousValueKnown,
                    converted,
                    previousMetadata,
                    out actualValue,
                    out actualValueKnown);

                if (ownsConverted)
                {
                    bool convertedStillInstalled =
                        !actualValueKnown ||
                        Object.ReferenceEquals(actualValue, converted);

                    if (convertedStillInstalled)
                    {
                        ReplaceOwnedPropertyValue(
                            instance,
                            property.Name,
                            converted as IDisposable);
                    }
                    else
                    {
                        IDisposable disposable = converted as IDisposable;

                        if (disposable != null)
                            disposable.Dispose();

                        if (!Object.ReferenceEquals(
                            actualValue,
                            previousValue))
                        {
                            ReleaseOwnedPropertyValue(
                                instance,
                                property.Name,
                                actualValue);
                        }
                    }
                }
                else if (!actualValueKnown ||
                    !Object.ReferenceEquals(actualValue, previousValue))
                {
                    ReleaseOwnedPropertyValue(
                        instance,
                        property.Name,
                        actualValueKnown ? actualValue : converted);
                }

                throw new InvalidOperationException(
                    "Could not set " +
                    instance.GetType().Name +
                    "." +
                    property.Name +
                    " = '" +
                    value +
                    "': " +
                    ex.Message,
                    ex);
            }

            object installedValue;
            bool installedValueKnown = TryReadPropertyValue(
                instance,
                property,
                out installedValue);

            if (ownsConverted)
            {
                ReconcileOwnedPropertyAssignment(
                    instance,
                    property.Name,
                    converted as IDisposable,
                    installedValue,
                    installedValueKnown);
            }
            else
            {
                ReleaseOwnedPropertyValue(
                    instance,
                    property.Name,
                    installedValueKnown ? installedValue : converted);
            }
        }

        private void ReconcileOwnedPropertyAssignment(
            object target,
            string propertyName,
            IDisposable attemptedValue,
            object actualValue,
            bool actualValueKnown)
        {
            if (!actualValueKnown ||
                Object.ReferenceEquals(actualValue, attemptedValue))
            {
                ReplaceOwnedPropertyValue(
                    target,
                    propertyName,
                    attemptedValue);
                return;
            }

            bool attemptedValueTracked =
                IsOwnedPropertyValueTracked(attemptedValue);

            // A native setter can synchronously run user code which leaves a
            // different value installed. Preserve that value's ownership entry
            // and never replace it with the superseded outer assignment.
            ReleaseOwnedPropertyValue(
                target,
                propertyName,
                actualValue);

            if (attemptedValue != null && !attemptedValueTracked)
                DisposeOwnedValueIfUnreferenced(attemptedValue);
        }

        private void SetMappedPictureBoxSource(
            PictureBox picture,
            Image image,
            string imageLocation,
            bool useImageLocation,
            bool ownsImage)
        {
            if (picture == null)
                throw new ArgumentNullException("picture");

            Image previousImage = picture.Image;
            string previousImageLocation = picture.ImageLocation;

            // Reapplying an unchanged location must not cancel a pending load,
            // discard the decoded native image, reopen a local file, or restart
            // a remote request. Call PictureBox.Load/LoadAsync explicitly when
            // the same URI intentionally needs to be refreshed.
            if (useImageLocation &&
                String.Equals(
                    previousImageLocation,
                    imageLocation,
                    StringComparison.Ordinal) &&
                (!String.IsNullOrEmpty(imageLocation) ||
                 previousImage == null))
            {
                return;
            }

            try
            {
                if (useImageLocation)
                {
                    if (!String.IsNullOrEmpty(previousImageLocation) &&
                        !String.Equals(
                            previousImageLocation,
                            imageLocation,
                            StringComparison.Ordinal))
                    {
                        picture.CancelAsync();
                    }

                    if (picture.Image != null)
                        picture.Image = null;

                    picture.ImageLocation = imageLocation;
                }
                else
                {
                    if (!String.IsNullOrEmpty(picture.ImageLocation))
                    {
                        picture.CancelAsync();
                        picture.ImageLocation = null;
                    }

                    if (!Object.ReferenceEquals(picture.Image, image))
                        picture.Image = image;
                }
            }
            catch
            {
                ReconcileMappedPictureBoxSourceOwnership(
                    picture,
                    image,
                    ownsImage);
                throw;
            }

            ReconcileMappedPictureBoxSourceOwnership(
                picture,
                image,
                ownsImage);

            NotifyMappedPictureBoxSourceChanged(
                picture,
                previousImage,
                previousImageLocation);
        }

        private void NotifyMappedPictureBoxSourceChanged(
            PictureBox picture,
            Image previousImage,
            string previousImageLocation)
        {
            ImageControl imageControl = picture as ImageControl;

            if (imageControl == null)
                return;

            Image actualImage = picture.Image;
            string actualImageLocation = picture.ImageLocation;

            if (Object.ReferenceEquals(previousImage, actualImage) &&
                String.Equals(
                    previousImageLocation,
                    actualImageLocation,
                    StringComparison.Ordinal))
            {
                return;
            }

            try
            {
                imageControl.NotifyMappedSourceChanged();
            }
            finally
            {
                // SourceChanged is application code and can replace Source
                // reentrantly. Retire the runtime-owned value only when it is no
                // longer the image that survived the callback.
                Image survivingImage = null;
                bool survivingImageKnown = false;

                try
                {
                    survivingImage = picture.Image;
                    survivingImageKnown = true;
                }
                catch
                {
                }

                if (survivingImageKnown)
                {
                    ReleaseOwnedPropertyValue(
                        picture,
                        "Image",
                        survivingImage);
                }
            }
        }

        private void ReconcileMappedPictureBoxSourceOwnership(
            PictureBox picture,
            Image attemptedImage,
            bool ownsAttemptedImage)
        {
            Image actualImage = null;
            bool actualImageKnown = false;

            try
            {
                actualImage = picture.Image;
                actualImageKnown = true;
            }
            catch
            {
                // A custom PictureBox can make its getter fail during teardown.
                // Generated values remain tracked conservatively in that case.
            }

            if (ownsAttemptedImage)
            {
                ReconcileOwnedPropertyAssignment(
                    picture,
                    "Image",
                    attemptedImage,
                    actualImage,
                    actualImageKnown);
            }
            else if (actualImageKnown)
            {
                ReleaseOwnedPropertyValue(
                    picture,
                    "Image",
                    actualImage);
            }
        }

        private bool IsOwnedPropertyValueTracked(IDisposable value)
        {
            if (value == null ||
                _ownedPropertyValueReferenceCounts == null)
            {
                return false;
            }

            OwnedPropertyValueReferenceCount reference =
                _ownedPropertyValueReferenceCounts[value]
                as OwnedPropertyValueReferenceCount;

            return reference != null && reference.Count > 0;
        }

        private OwnedPropertyValue FindOwnedPropertyValue(
            object target,
            string propertyName)
        {
            if (_ownedPropertyValues == null ||
                target == null ||
                propertyName == null)
            {
                return null;
            }

            Hashtable propertyValues =
                _ownedPropertyValues[target] as Hashtable;

            return propertyValues == null
                ? null
                : propertyValues[propertyName] as OwnedPropertyValue;
        }

        private Hashtable GetOwnedPropertyValues(
            object target,
            bool create)
        {
            if (_ownedPropertyValues == null || target == null)
                return null;

            Hashtable propertyValues =
                _ownedPropertyValues[target] as Hashtable;

            if (propertyValues == null && create)
            {
                propertyValues =
                    new Hashtable(StringComparer.OrdinalIgnoreCase);
                _ownedPropertyValues[target] = propertyValues;
            }

            return propertyValues;
        }

        private void ReplaceOwnedPropertyValue(
            object target,
            string propertyName,
            IDisposable value)
        {
            if (_ownedPropertyValues == null ||
                target == null ||
                propertyName == null)
            {
                return;
            }

            OwnedPropertyValue existing =
                FindOwnedPropertyValue(target, propertyName);

            if (existing != null &&
                Object.ReferenceEquals(existing.Value, value))
            {
                return;
            }

            Hashtable propertyValues =
                GetOwnedPropertyValues(target, value != null);

            if (value != null)
            {
                OwnedPropertyValue replacement =
                    new OwnedPropertyValue();
                replacement.Target = target;
                replacement.PropertyName = propertyName;
                replacement.Value = value;

                AddOwnedPropertyValueReference(value);
                propertyValues[propertyName] = replacement;
            }
            else if (propertyValues != null)
            {
                propertyValues.Remove(propertyName);

                if (propertyValues.Count == 0)
                    _ownedPropertyValues.Remove(target);
            }

            // Publish the replacement before disposing the previous value so
            // synchronous disposal callbacks always observe coherent ownership.
            if (existing != null)
                ReleaseOwnedPropertyValueReference(existing.Value);
        }

        private void ReleaseOwnedPropertyValue(
            object target,
            string propertyName,
            object replacement)
        {
            if (_ownedPropertyValues == null ||
                target == null ||
                propertyName == null)
            {
                return;
            }

            OwnedPropertyValue existing =
                FindOwnedPropertyValue(target, propertyName);

            if (existing == null ||
                Object.ReferenceEquals(existing.Value, replacement))
            {
                return;
            }

            Hashtable propertyValues =
                GetOwnedPropertyValues(target, false);

            propertyValues.Remove(propertyName);

            if (propertyValues.Count == 0)
                _ownedPropertyValues.Remove(target);

            ReleaseOwnedPropertyValueReference(existing.Value);
        }

        private void ReleaseOwnedPropertyValues(object target)
        {
            if (_ownedPropertyValues == null || target == null)
                return;

            while (true)
            {
                Hashtable propertyValues =
                    GetOwnedPropertyValues(target, false);

                if (propertyValues == null || propertyValues.Count == 0)
                {
                    if (propertyValues != null)
                        _ownedPropertyValues.Remove(target);

                    return;
                }

                IDictionaryEnumerator iterator =
                    propertyValues.GetEnumerator();
                iterator.MoveNext();

                OwnedPropertyValue existing =
                    iterator.Value as OwnedPropertyValue;

                if (existing == null)
                {
                    propertyValues.Remove(iterator.Key);
                    continue;
                }

                ReleaseOwnedPropertyValue(
                    target,
                    existing.PropertyName,
                    null);
            }
        }

        private void DisposeOwnedPropertyValues()
        {
            if (_ownedPropertyValues == null)
                return;

            while (_ownedPropertyValues != null &&
                _ownedPropertyValues.Count > 0)
            {
                IDictionaryEnumerator iterator =
                    _ownedPropertyValues.GetEnumerator();
                iterator.MoveNext();
                ReleaseOwnedPropertyValues(iterator.Key);
            }

            _ownedPropertyValues = null;
            _ownedPropertyValueReferenceCounts = null;
        }

        private void AddOwnedPropertyValueReference(IDisposable value)
        {
            if (value == null ||
                _ownedPropertyValueReferenceCounts == null)
            {
                return;
            }

            OwnedPropertyValueReferenceCount reference =
                _ownedPropertyValueReferenceCounts[value]
                as OwnedPropertyValueReferenceCount;

            if (reference == null)
            {
                reference = new OwnedPropertyValueReferenceCount();
                reference.Count = 1;
                _ownedPropertyValueReferenceCounts[value] = reference;
                return;
            }

            if (reference.Count == Int32.MaxValue)
                throw new InvalidOperationException(
                    "Too many owned references to the same value.");

            reference.Count++;
        }

        private void ReleaseOwnedPropertyValueReference(
            IDisposable value)
        {
            if (value == null)
                return;

            OwnedPropertyValueReferenceCount reference =
                _ownedPropertyValueReferenceCounts == null
                    ? null
                    : _ownedPropertyValueReferenceCounts[value]
                        as OwnedPropertyValueReferenceCount;

            if (reference != null && reference.Count > 1)
            {
                reference.Count--;
                return;
            }

            if (reference != null)
                _ownedPropertyValueReferenceCounts.Remove(value);

            DisposeOwnedValueIfUnreferenced(value);
        }

        private void DisposeOwnedValueIfUnreferenced(
            IDisposable value)
        {
            if (value == null)
                return;

            if (IsOwnedPropertyValueTracked(value))
                return;

            Image image = value as Image;

            if (image != null && _decodedImageCache != null)
            {
                int i;

                for (i = _decodedImageCache.Count - 1; i >= 0; i--)
                {
                    WeakDecodedImageCacheEntry entry =
                        _decodedImageCache[i] as WeakDecodedImageCacheEntry;

                    if (entry != null && entry.Image != null &&
                        Object.ReferenceEquals(entry.Image.Target, image))
                    {
                        _decodedImageCache.RemoveAt(i);
                    }
                }
            }

            value.Dispose();
        }

        private object ConvertString(
            string value,
            Type targetType)
        {
            Type underlying =
                Nullable.GetUnderlyingType(
                    targetType);

            if (underlying != null)
            {
                if (String.IsNullOrEmpty(
                    value))
                {
                    return null;
                }

                targetType =
                    underlying;
            }

            object cached;

            if (TryGetConvertedStringValue(
                    targetType,
                    value,
                    out cached))
            {
                _convertedStringValueCacheHitCount++;
                return cached;
            }

            object converted = ConvertStringUncached(
                value,
                targetType);

            CacheConvertedStringValue(
                targetType,
                value,
                converted);
            return converted;
        }

        private object ConvertStringUncached(
            string value,
            Type targetType)
        {

            if (targetType ==
                typeof(string))
            {
                return value;
            }

            if (targetType ==
                typeof(object))
            {
                return value;
            }

            if (targetType ==
                typeof(bool))
            {
                return ParseBoolean(
                    value);
            }

            if (targetType ==
                typeof(char))
            {
                if (String.IsNullOrEmpty(
                    value))
                {
                    return '\0';
                }

                return value[0];
            }

            if (targetType.IsEnum)
            {
                return Enum.Parse(
                    targetType,
                    value,
                    true);
            }

            if (targetType ==
                typeof(Color))
            {
                return ParseColor(
                    value);
            }

            if (targetType ==
                typeof(Padding))
            {
                return ParseThickness(
                    value);
            }

            if (targetType ==
                typeof(Image))
            {
                return Image.FromFile(
                    ResolvePath(
                        value));
            }

            if (targetType ==
                typeof(Bitmap))
            {
                return new Bitmap(
                    ResolvePath(
                        value));
            }

            if (targetType ==
                typeof(Icon))
            {
                return new Icon(
                    ResolvePath(
                        value));
            }

            if (targetType ==
                typeof(Uri))
            {
                return new Uri(
                    value,
                    UriKind.RelativeOrAbsolute);
            }

            TypeConverter converter =
                TypeDescriptor.GetConverter(
                    targetType);

            if (converter != null &&
                converter.CanConvertFrom(
                    typeof(string)))
            {
                return converter
                    .ConvertFromInvariantString(
                        value);
            }

            return Convert.ChangeType(
                value,
                targetType,
                CultureInfo.InvariantCulture);
        }

        private bool TryGetConvertedStringValue(
            Type targetType,
            string value,
            out object converted)
        {
            converted = null;

            if (_convertedStringValueCaches == null ||
                value == null ||
                !CanCacheConvertedStringValue(targetType))
            {
                return false;
            }

            Hashtable values =
                _convertedStringValueCaches[targetType]
                    as Hashtable;

            if (values == null || !values.ContainsKey(value))
                return false;

            converted = values[value];
            return true;
        }

        private void CacheConvertedStringValue(
            Type targetType,
            string value,
            object converted)
        {
            if (value == null ||
                converted == null ||
                _convertedStringValueCacheEntryCount >=
                    ConvertedStringValueCacheLimit ||
                !CanCacheConvertedStringValue(targetType))
            {
                return;
            }

            if (_convertedStringValueCaches == null)
                _convertedStringValueCaches = new Hashtable();

            Hashtable values =
                _convertedStringValueCaches[targetType]
                    as Hashtable;

            if (values == null)
            {
                values = new Hashtable(StringComparer.Ordinal);
                _convertedStringValueCaches[targetType] = values;
            }

            if (values.ContainsKey(value) ||
                values.Count >= ConvertedStringValueCachePerTypeLimit)
            {
                return;
            }

            values.Add(value, converted);
            _convertedStringValueCacheEntryCount++;
        }

        private static bool CanCacheConvertedStringValue(Type targetType)
        {
            if (targetType == null)
                return false;

            if (targetType.IsEnum)
                return true;

            if (!targetType.IsValueType)
                return false;

            Assembly assembly = targetType.Assembly;

            return Object.ReferenceEquals(
                       assembly,
                       typeof(int).Assembly) ||
                   Object.ReferenceEquals(
                       assembly,
                       typeof(Color).Assembly) ||
                   Object.ReferenceEquals(
                       assembly,
                       typeof(Padding).Assembly);
        }
    }
}
