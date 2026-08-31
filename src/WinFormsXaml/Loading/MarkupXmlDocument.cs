using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;

namespace WinFormsXaml
{
    /// <summary>
    /// Retains parser locations on XML elements and attributes without a
    /// second pass over the document. The custom nodes also preserve their
    /// locations when item templates are cloned.
    /// </summary>
    internal sealed class MarkupXmlDocument : XmlDocument
    {
        internal const string LocationAttributeName =
            "__WfxLocation";

        private IXmlLineInfo _activeLineInfo;

        internal void LoadMarkup(string xml)
        {
            if (xml == null)
                throw new ArgumentNullException("xml");

            using (StringReader textReader = new StringReader(xml))
            using (XmlTextReader reader = new XmlTextReader(textReader))
            {
                reader.ProhibitDtd = true;
                reader.XmlResolver = null;
                LoadMarkup(reader);
            }
        }

        internal void LoadMarkup(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException("stream");

            using (XmlTextReader reader = new XmlTextReader(stream))
            {
                reader.ProhibitDtd = true;
                reader.XmlResolver = null;
                LoadMarkup(reader);
            }
        }

        internal void LoadMarkup(XmlReader reader)
        {
            if (reader == null)
                throw new ArgumentNullException("reader");

            _activeLineInfo = reader as IXmlLineInfo;

            try
            {
                base.Load(reader);
            }
            finally
            {
                _activeLineInfo = null;
            }
        }

        public override XmlElement CreateElement(
            string prefix,
            string localName,
            string namespaceUri)
        {
            MarkupXmlElement element =
                new MarkupXmlElement(
                    prefix,
                    localName,
                    namespaceUri,
                    this);

            CaptureLocation(element);
            return element;
        }

        public override XmlAttribute CreateAttribute(
            string prefix,
            string localName,
            string namespaceUri)
        {
            MarkupXmlAttribute attribute =
                new MarkupXmlAttribute(
                    prefix,
                    localName,
                    namespaceUri,
                    this);

            CaptureLocation(attribute);
            return attribute;
        }

        internal static void GetLocation(
            XmlElement element,
            string propertyName,
            out int lineNumber,
            out int linePosition)
        {
            lineNumber = 0;
            linePosition = 0;

            if (element == null)
                return;

            if (!String.IsNullOrEmpty(propertyName))
            {
                int i;

                for (i = 0; i < element.Attributes.Count; i++)
                {
                    XmlAttribute attribute = element.Attributes[i];

                    if (!String.Equals(
                            attribute.Name,
                            propertyName,
                            StringComparison.OrdinalIgnoreCase) &&
                        !String.Equals(
                            attribute.LocalName,
                            propertyName,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    MarkupXmlAttribute locatedAttribute =
                        attribute as MarkupXmlAttribute;

                    if (locatedAttribute != null &&
                        locatedAttribute.LineNumber > 0)
                    {
                        lineNumber = locatedAttribute.LineNumber;
                        linePosition = locatedAttribute.LinePosition;
                        return;
                    }
                }
            }

            MarkupXmlElement locatedElement =
                element as MarkupXmlElement;

            if (locatedElement != null &&
                locatedElement.LineNumber > 0)
            {
                lineNumber = locatedElement.LineNumber;
                linePosition = locatedElement.LinePosition;
                return;
            }

            TryGetSerializedLocation(
                element.GetAttribute(LocationAttributeName),
                propertyName,
                out lineNumber,
                out linePosition);

        }

        internal static string SerializeElementWithLocations(
            XmlElement element)
        {
            if (element == null)
                throw new ArgumentNullException("element");

            XmlElement copy =
                (XmlElement)element.CloneNode(true);

            PersistLocations(copy);
            return copy.OuterXml;
        }

        internal static void PersistElementLocations(
            XmlElement element)
        {
            if (element == null)
                throw new ArgumentNullException("element");

            PersistLocations(element);
        }

        internal static void RestoreSerializedMetadata(
            XmlElement element)
        {
            if (element == null)
                return;

            string serialized = element.GetAttribute(
                LocationAttributeName);
            int lineNumber;
            int linePosition;
            MarkupXmlElement locatedElement =
                element as MarkupXmlElement;

            if (locatedElement != null &&
                TryGetSerializedLocation(
                    serialized,
                    null,
                    out lineNumber,
                    out linePosition))
            {
                locatedElement.SetLocation(
                    lineNumber,
                    linePosition);
            }

            string markupSource;
            string elementPathPrefix;
            ReadSerializedOrigin(
                element,
                out markupSource,
                out elementPathPrefix);

            if (locatedElement != null &&
                (!String.IsNullOrEmpty(markupSource) ||
                 !String.IsNullOrEmpty(elementPathPrefix)))
            {
                locatedElement.SetOrigin(
                    markupSource,
                    elementPathPrefix);
            }

            int i;

            for (i = 0; i < element.Attributes.Count; i++)
            {
                MarkupXmlAttribute locatedAttribute =
                    element.Attributes[i] as MarkupXmlAttribute;

                if (locatedAttribute != null &&
                    TryGetSerializedLocation(
                        serialized,
                        element.Attributes[i].LocalName,
                        out lineNumber,
                        out linePosition))
                {
                    locatedAttribute.SetLocation(
                        lineNumber,
                        linePosition);
                }
            }

            XmlNode child = element.FirstChild;

            while (child != null)
            {
                XmlElement childElement = child as XmlElement;

                if (childElement != null)
                    RestoreSerializedMetadata(childElement);

                child = child.NextSibling;
            }
        }

        internal static void SetOrigin(
            XmlElement element,
            string markupSource,
            string elementPathPrefix)
        {
            if (element == null)
                return;

            MarkupXmlElement locatedElement =
                element as MarkupXmlElement;

            if (locatedElement != null)
            {
                locatedElement.SetOrigin(
                    markupSource,
                    elementPathPrefix);
            }

            XmlNode child = element.FirstChild;

            while (child != null)
            {
                XmlElement childElement = child as XmlElement;

                if (childElement != null)
                {
                    SetOrigin(
                        childElement,
                        markupSource,
                        elementPathPrefix);
                }

                child = child.NextSibling;
            }
        }

        internal static string GetMarkupSource(XmlElement element)
        {
            MarkupXmlElement locatedElement =
                element as MarkupXmlElement;

            if (locatedElement != null &&
                !String.IsNullOrEmpty(locatedElement.MarkupSource))
            {
                return locatedElement.MarkupSource;
            }

            string markupSource;
            string elementPathPrefix;

            ReadSerializedOrigin(
                element,
                out markupSource,
                out elementPathPrefix);

            return markupSource;
        }

        internal static string GetElementPathPrefix(XmlElement element)
        {
            MarkupXmlElement locatedElement =
                element as MarkupXmlElement;

            if (locatedElement != null &&
                !String.IsNullOrEmpty(locatedElement.ElementPathPrefix))
            {
                return locatedElement.ElementPathPrefix;
            }

            string markupSource;
            string elementPathPrefix;

            ReadSerializedOrigin(
                element,
                out markupSource,
                out elementPathPrefix);

            return elementPathPrefix;
        }

        private void CaptureLocation(MarkupXmlElement element)
        {
            if (_activeLineInfo != null &&
                _activeLineInfo.HasLineInfo())
            {
                // XmlTextReader reports an element at the first character of
                // its name. Diagnostics use the opening '<' as the stable
                // source coordinate, matching attribute coordinates which
                // point at the beginning of their complete token.
                int linePosition = _activeLineInfo.LinePosition;

                if (linePosition > 1)
                    linePosition--;

                element.SetLocation(
                    _activeLineInfo.LineNumber,
                    linePosition);
            }
        }

        private void CaptureLocation(MarkupXmlAttribute attribute)
        {
            if (_activeLineInfo != null &&
                _activeLineInfo.HasLineInfo())
            {
                attribute.SetLocation(
                    _activeLineInfo.LineNumber,
                    _activeLineInfo.LinePosition);
            }
        }

        private static void PersistLocations(XmlElement element)
        {
            int lineNumber;
            int linePosition;

            GetLocation(
                element,
                null,
                out lineNumber,
                out linePosition);

            string markupSource = GetMarkupSource(element);
            string elementPathPrefix = GetElementPathPrefix(element);

            if (lineNumber > 0 ||
                !String.IsNullOrEmpty(markupSource) ||
                !String.IsNullOrEmpty(elementPathPrefix))
            {
                StringBuilder location = new StringBuilder();

                if (lineNumber > 0)
                {
                    location.Append("E,");
                    location.Append(
                        lineNumber.ToString(CultureInfo.InvariantCulture));
                    location.Append(',');
                    location.Append(
                        linePosition.ToString(CultureInfo.InvariantCulture));
                }

                AppendEncodedOriginRecord(
                    location,
                    "S",
                    markupSource);
                AppendEncodedOriginRecord(
                    location,
                    "P",
                    elementPathPrefix);

                int i;

                for (i = 0; i < element.Attributes.Count; i++)
                {
                    XmlAttribute attribute = element.Attributes[i];

                    if (String.Equals(
                            attribute.LocalName,
                            LocationAttributeName,
                            StringComparison.OrdinalIgnoreCase) ||
                        String.Equals(
                            attribute.Name,
                            "xmlns",
                            StringComparison.OrdinalIgnoreCase) ||
                        String.Equals(
                            attribute.Prefix,
                            "xmlns",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    MarkupXmlAttribute locatedAttribute =
                        attribute as MarkupXmlAttribute;

                    if (locatedAttribute == null ||
                        locatedAttribute.LineNumber <= 0)
                    {
                        continue;
                    }

                    location.Append("|A,");
                    location.Append(attribute.LocalName);
                    location.Append(',');
                    location.Append(
                        locatedAttribute.LineNumber.ToString(
                            CultureInfo.InvariantCulture));
                    location.Append(',');
                    location.Append(
                        locatedAttribute.LinePosition.ToString(
                            CultureInfo.InvariantCulture));
                }

                element.SetAttribute(
                    LocationAttributeName,
                    location.ToString());
            }

            XmlNode child = element.FirstChild;

            while (child != null)
            {
                XmlElement childElement = child as XmlElement;

                if (childElement != null)
                    PersistLocations(childElement);

                child = child.NextSibling;
            }
        }

        private static void AppendEncodedOriginRecord(
            StringBuilder value,
            string recordType,
            string text)
        {
            if (String.IsNullOrEmpty(text))
                return;

            if (value.Length != 0)
                value.Append('|');

            value.Append(recordType);
            value.Append(',');
            value.Append(
                Convert.ToBase64String(
                    Encoding.UTF8.GetBytes(text)));
        }

        private static void ReadSerializedOrigin(
            XmlElement element,
            out string markupSource,
            out string elementPathPrefix)
        {
            markupSource = null;
            elementPathPrefix = null;

            if (element == null)
                return;

            string value = element.GetAttribute(LocationAttributeName);

            if (String.IsNullOrEmpty(value))
                return;

            string[] records = value.Split(new char[] { '|' });
            int i;

            for (i = 0; i < records.Length; i++)
            {
                int separator = records[i].IndexOf(',');

                if (separator != 1 || records[i].Length <= 2)
                    continue;

                string recordType = records[i].Substring(0, 1);

                if (!String.Equals(recordType, "S", StringComparison.Ordinal) &&
                    !String.Equals(recordType, "P", StringComparison.Ordinal))
                {
                    continue;
                }

                try
                {
                    string decoded = Encoding.UTF8.GetString(
                        Convert.FromBase64String(
                            records[i].Substring(separator + 1)));

                    if (String.Equals(recordType, "S", StringComparison.Ordinal))
                        markupSource = decoded;
                    else
                        elementPathPrefix = decoded;
                }
                catch (FormatException)
                {
                    // Invalid private metadata is ignored here. The normal
                    // semantic loader will still report the element location.
                }
            }
        }

        private static bool TryGetSerializedLocation(
            string value,
            string propertyName,
            out int lineNumber,
            out int linePosition)
        {
            lineNumber = 0;
            linePosition = 0;

            if (String.IsNullOrEmpty(value))
                return false;

            int legacySeparator = value.IndexOf(':');

            if (legacySeparator > 0 &&
                legacySeparator < value.Length - 1 &&
                value.IndexOf('|') < 0 &&
                value.IndexOf(',') < 0)
            {
                return TryParseLocationPair(
                    value.Substring(0, legacySeparator),
                    value.Substring(legacySeparator + 1),
                    out lineNumber,
                    out linePosition);
            }

            string[] records = value.Split(new char[] { '|' });
            int elementLine = 0;
            int elementPosition = 0;
            int i;

            for (i = 0; i < records.Length; i++)
            {
                string[] fields =
                    records[i].Split(new char[] { ',' });

                if (fields.Length == 3 &&
                    String.Equals(
                        fields[0],
                        "E",
                        StringComparison.Ordinal))
                {
                    TryParseLocationPair(
                        fields[1],
                        fields[2],
                        out elementLine,
                        out elementPosition);
                    continue;
                }

                if (fields.Length != 4 ||
                    String.IsNullOrEmpty(propertyName) ||
                    !String.Equals(
                        fields[0],
                        "A",
                        StringComparison.Ordinal) ||
                    !String.Equals(
                        fields[1],
                        propertyName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (TryParseLocationPair(
                        fields[2],
                        fields[3],
                        out lineNumber,
                        out linePosition))
                {
                    return true;
                }
            }

            lineNumber = elementLine;
            linePosition = elementPosition;
            return lineNumber > 0;
        }

        private static bool TryParseLocationPair(
            string lineText,
            string positionText,
            out int lineNumber,
            out int linePosition)
        {
            int parsedLine;
            int parsedPosition;

            if (!Int32.TryParse(
                    lineText,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out parsedLine) ||
                !Int32.TryParse(
                    positionText,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out parsedPosition) ||
                parsedLine <= 0 ||
                parsedPosition <= 0)
            {
                lineNumber = 0;
                linePosition = 0;
                return false;
            }

            lineNumber = parsedLine;
            linePosition = parsedPosition;
            return true;
        }
    }

    internal sealed class MarkupXmlElement : XmlElement
    {
        private int _lineNumber;
        private int _linePosition;
        private string _markupSource;
        private string _elementPathPrefix;

        internal MarkupXmlElement(
            string prefix,
            string localName,
            string namespaceUri,
            XmlDocument document)
            : base(prefix, localName, namespaceUri, document)
        {
        }

        internal int LineNumber
        {
            get { return _lineNumber; }
        }

        internal int LinePosition
        {
            get { return _linePosition; }
        }

        internal string MarkupSource
        {
            get { return _markupSource; }
        }

        internal string ElementPathPrefix
        {
            get { return _elementPathPrefix; }
        }

        internal void SetLocation(
            int lineNumber,
            int linePosition)
        {
            _lineNumber = lineNumber;
            _linePosition = linePosition;
        }

        internal void SetOrigin(
            string markupSource,
            string elementPathPrefix)
        {
            _markupSource = markupSource;
            _elementPathPrefix = elementPathPrefix;
        }

        public override XmlNode CloneNode(bool deep)
        {
            XmlNode clone = base.CloneNode(deep);
            MarkupXmlElement locatedClone =
                clone as MarkupXmlElement;

            if (locatedClone != null)
            {
                locatedClone.SetLocation(
                    _lineNumber,
                    _linePosition);
                locatedClone.SetOrigin(
                    _markupSource,
                    _elementPathPrefix);
            }

            return clone;
        }
    }

    internal sealed class MarkupXmlAttribute : XmlAttribute
    {
        private int _lineNumber;
        private int _linePosition;

        internal MarkupXmlAttribute(
            string prefix,
            string localName,
            string namespaceUri,
            XmlDocument document)
            : base(prefix, localName, namespaceUri, document)
        {
        }

        internal int LineNumber
        {
            get { return _lineNumber; }
        }

        internal int LinePosition
        {
            get { return _linePosition; }
        }

        internal void SetLocation(
            int lineNumber,
            int linePosition)
        {
            _lineNumber = lineNumber;
            _linePosition = linePosition;
        }

        public override XmlNode CloneNode(bool deep)
        {
            XmlNode clone = base.CloneNode(deep);
            MarkupXmlAttribute locatedClone =
                clone as MarkupXmlAttribute;

            if (locatedClone != null)
            {
                locatedClone.SetLocation(
                    _lineNumber,
                    _linePosition);
            }

            return clone;
        }
    }
}
