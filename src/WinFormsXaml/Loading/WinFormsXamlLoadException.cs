using System;
using System.Runtime.Serialization;

namespace WinFormsXaml
{
    /// <summary>
    /// Describes a failure while parsing or constructing a WinFormsXaml tree,
    /// or while re-evaluating or applying one of its retained bindings.
    /// </summary>
    [Serializable]
    public sealed class WinFormsXamlLoadException : InvalidOperationException
    {
        private readonly string _markupSource;
        private readonly string _elementPath;
        private readonly string _propertyName;
        private readonly int _lineNumber;
        private readonly int _linePosition;

        internal WinFormsXamlLoadException(
            string markupSource,
            string elementPath,
            string propertyName,
            int lineNumber,
            int linePosition,
            Exception innerException)
            : base(
                BuildMessage(
                    markupSource,
                    elementPath,
                    propertyName,
                    lineNumber,
                    linePosition,
                    innerException),
                innerException)
        {
            _markupSource = markupSource;
            _elementPath = elementPath;
            _propertyName = propertyName;
            _lineNumber = lineNumber;
            _linePosition = linePosition;
        }

        private WinFormsXamlLoadException(
            SerializationInfo info,
            StreamingContext context)
            : base(info, context)
        {
            if (info == null)
                throw new ArgumentNullException("info");

            _markupSource = info.GetString("MarkupSource");
            _elementPath = info.GetString("ElementPath");
            _propertyName = info.GetString("PropertyName");
            _lineNumber = info.GetInt32("LineNumber");
            _linePosition = info.GetInt32("LinePosition");
        }

        /// <summary>
        /// Gets the embedded-resource name or inline-markup label where the
        /// failing markup originated.
        /// </summary>
        public string MarkupSource
        {
            get { return _markupSource; }
        }

        /// <summary>Gets the deepest known XML element path.</summary>
        public string ElementPath
        {
            get { return _elementPath; }
        }

        /// <summary>Gets the property or attribute being applied, when known.</summary>
        public string PropertyName
        {
            get { return _propertyName; }
        }

        /// <summary>
        /// Gets the one-based source line for parser and semantic failures, or
        /// zero when source-location information is unavailable.
        /// </summary>
        public int LineNumber
        {
            get { return _lineNumber; }
        }

        /// <summary>
        /// Gets the one-based source position. Semantic failures identify the
        /// failing attribute when retained and otherwise the opening element.
        /// </summary>
        public int LinePosition
        {
            get { return _linePosition; }
        }

        /// <summary>
        /// Adds markup source and location details to serialized exception data.
        /// </summary>
        public override void GetObjectData(
            SerializationInfo info,
            StreamingContext context)
        {
            if (info == null)
                throw new ArgumentNullException("info");

            base.GetObjectData(info, context);
            info.AddValue("MarkupSource", _markupSource);
            info.AddValue("ElementPath", _elementPath);
            info.AddValue("PropertyName", _propertyName);
            info.AddValue("LineNumber", _lineNumber);
            info.AddValue("LinePosition", _linePosition);
        }

        private static string BuildMessage(
            string markupSource,
            string elementPath,
            string propertyName,
            int lineNumber,
            int linePosition,
            Exception innerException)
        {
            string message = lineNumber > 0
                ? "Invalid WinFormsXaml XML"
                : "Could not load WinFormsXaml markup";

            if (!String.IsNullOrEmpty(markupSource))
                message += " from '" + markupSource + "'";

            if (lineNumber > 0)
            {
                message += " at line " + lineNumber.ToString();

                if (linePosition > 0)
                    message += ", position " + linePosition.ToString();
            }

            if (!String.IsNullOrEmpty(elementPath))
                message += ", element " + elementPath;

            if (!String.IsNullOrEmpty(propertyName))
                message += ", property '" + propertyName + "'";

            message += ".";

            if (innerException != null &&
                !String.IsNullOrEmpty(innerException.Message))
            {
                message += " " + innerException.Message;
            }

            return message;
        }
    }
}
