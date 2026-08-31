using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Schema;

namespace WinFormsXaml.BuildTools
{
    public static class SchemaContractValidator
    {
        public static int Main(string[] args)
        {
            if (args == null || args.Length < 2)
            {
                Console.Error.WriteLine(
                    "Usage: SchemaContractValidator.exe <schema-path> <fixture-path> [fixture-path ...]");
                return 2;
            }

            string[] fixturePaths = new string[args.Length - 1];
            Array.Copy(args, 1, fixturePaths, 0, fixturePaths.Length);

            try
            {
                Validate(args[0], fixturePaths);
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(
                    "Schema contract validation failed: {0}",
                    exception.Message);
                return 1;
            }
        }

        public static void Validate(string schemaPath, string[] fixturePaths)
        {
            string resolvedSchemaPath = ResolveRequiredPath(
                schemaPath,
                "schema");
            if (new FileInfo(resolvedSchemaPath).Length == 0)
            {
                throw new InvalidOperationException(
                    "The WinFormsXaml schema is empty: '" +
                    resolvedSchemaPath + "'.");
            }

            string[] resolvedFixturePaths = ResolveFixturePaths(fixturePaths);
            XmlSchemaSet schemaSet = CompileSchema(resolvedSchemaPath);

            for (int index = 0; index < resolvedFixturePaths.Length; index++)
            {
                ValidateFixture(
                    schemaSet,
                    resolvedSchemaPath,
                    resolvedFixturePaths[index]);
            }

            Console.WriteLine(
                "Compiled '{0}' and validated {1} associated XML fixtures.",
                resolvedSchemaPath,
                resolvedFixturePaths.Length.ToString(
                    CultureInfo.InvariantCulture));
        }

        private static XmlSchemaSet CompileSchema(string schemaPath)
        {
            ValidationCollector schemaDiagnostics = new ValidationCollector();
            XmlSchemaSet schemaSet = new XmlSchemaSet();
            schemaSet.XmlResolver = null;
            schemaSet.ValidationEventHandler +=
                schemaDiagnostics.HandleValidationEvent;

            XmlReaderSettings schemaSettings = CreateSecureReaderSettings();
            using (XmlReader schemaReader = XmlReader.Create(
                schemaPath,
                schemaSettings))
            {
                schemaSet.Add(null, schemaReader);
            }

            ThrowForSchemaDiagnostics(
                schemaDiagnostics,
                "Schema read failed",
                schemaPath);

            schemaSet.Compile();
            ThrowForSchemaDiagnostics(
                schemaDiagnostics,
                "Schema compilation failed",
                schemaPath);

            return schemaSet;
        }

        private static void ValidateFixture(
            XmlSchemaSet schemaSet,
            string schemaPath,
            string fixturePath)
        {
            XmlQualifiedName rootName = ReadRootName(fixturePath);
            if (!schemaSet.GlobalElements.Contains(rootName))
            {
                throw new InvalidOperationException(
                    "The fixture root '" + FormatQualifiedName(rootName) +
                    "' is not declared as a global element in '" +
                    schemaPath + "': '" + fixturePath + "'.");
            }

            ValidationCollector fixtureDiagnostics = new ValidationCollector();
            XmlReaderSettings fixtureSettings = CreateSecureReaderSettings();
            fixtureSettings.ValidationType = ValidationType.Schema;
            fixtureSettings.Schemas = schemaSet;
            fixtureSettings.ValidationFlags =
                XmlSchemaValidationFlags.ProcessIdentityConstraints |
                XmlSchemaValidationFlags.ReportValidationWarnings;
            fixtureSettings.ValidationEventHandler +=
                fixtureDiagnostics.HandleValidationEvent;

            using (XmlReader fixtureReader = XmlReader.Create(
                fixturePath,
                fixtureSettings))
            {
                while (fixtureReader.Read())
                {
                }
            }

            if (fixtureDiagnostics.HasErrors)
            {
                throw new InvalidOperationException(
                    "Schema validation failed for '" + fixturePath +
                    "' against '" + schemaPath + "':" +
                    Environment.NewLine + fixtureDiagnostics.FormatErrors());
            }

            if (fixtureDiagnostics.HasWarnings)
            {
                Console.Error.WriteLine(
                    "Allowed schema validation warnings for '{0}':{1}{2}",
                    fixturePath,
                    Environment.NewLine,
                    fixtureDiagnostics.FormatWarnings());
            }
        }

        private static XmlQualifiedName ReadRootName(string fixturePath)
        {
            XmlReaderSettings rootSettings = CreateSecureReaderSettings();
            using (XmlReader rootReader = XmlReader.Create(
                fixturePath,
                rootSettings))
            {
                while (rootReader.Read())
                {
                    if (rootReader.NodeType == XmlNodeType.Element)
                    {
                        return new XmlQualifiedName(
                            rootReader.LocalName,
                            rootReader.NamespaceURI);
                    }
                }
            }

            throw new InvalidOperationException(
                "The schema-validation fixture has no root element: '" +
                fixturePath + "'.");
        }

        private static XmlReaderSettings CreateSecureReaderSettings()
        {
            XmlReaderSettings settings = new XmlReaderSettings();
#pragma warning disable 618
            settings.ProhibitDtd = true;
#pragma warning restore 618
            settings.XmlResolver = null;
            return settings;
        }

        private static string[] ResolveFixturePaths(string[] fixturePaths)
        {
            if (fixturePaths == null || fixturePaths.Length == 0)
            {
                throw new ArgumentException(
                    "At least one XML fixture is required for schema validation.",
                    "fixturePaths");
            }

            ArrayList sortedPaths = new ArrayList(fixturePaths.Length);
            for (int index = 0; index < fixturePaths.Length; index++)
            {
                sortedPaths.Add(ResolveRequiredPath(
                    fixturePaths[index],
                    "schema-validation fixture"));
            }

            sortedPaths.Sort(StringComparer.Ordinal);
            ArrayList uniquePaths = new ArrayList(sortedPaths.Count);
            string previousPath = null;
            for (int index = 0; index < sortedPaths.Count; index++)
            {
                string currentPath = (string)sortedPaths[index];
                if (previousPath == null ||
                    !StringComparer.Ordinal.Equals(previousPath, currentPath))
                {
                    uniquePaths.Add(currentPath);
                    previousPath = currentPath;
                }
            }

            return (string[])uniquePaths.ToArray(typeof(string));
        }

        private static string ResolveRequiredPath(
            string path,
            string description)
        {
            if (path == null || path.Trim().Length == 0)
            {
                throw new ArgumentException(
                    "A " + description + " path is required.",
                    "path");
            }

            string resolvedPath = Path.GetFullPath(path);
            if (!File.Exists(resolvedPath))
            {
                throw new FileNotFoundException(
                    "The " + description + " was not found: '" +
                    resolvedPath + "'.",
                    resolvedPath);
            }

            return resolvedPath;
        }

        private static void ThrowForSchemaDiagnostics(
            ValidationCollector diagnostics,
            string phase,
            string schemaPath)
        {
            if (!diagnostics.HasErrors && !diagnostics.HasWarnings)
            {
                return;
            }

            throw new InvalidOperationException(
                phase + " for '" + schemaPath + "':" +
                Environment.NewLine + diagnostics.FormatAll());
        }

        private static string FormatQualifiedName(XmlQualifiedName name)
        {
            if (name.Namespace == null || name.Namespace.Length == 0)
            {
                return name.Name;
            }

            return "{" + name.Namespace + "}" + name.Name;
        }

        private sealed class ValidationCollector
        {
            private readonly ArrayList errors = new ArrayList();
            private readonly ArrayList warnings = new ArrayList();

            public bool HasErrors
            {
                get { return errors.Count != 0; }
            }

            public bool HasWarnings
            {
                get { return warnings.Count != 0; }
            }

            public void HandleValidationEvent(
                object sender,
                ValidationEventArgs eventArgs)
            {
                string diagnostic = FormatDiagnostic(eventArgs);
                if (eventArgs.Severity == XmlSeverityType.Warning)
                {
                    warnings.Add(diagnostic);
                }
                else
                {
                    errors.Add(diagnostic);
                }
            }

            public string FormatErrors()
            {
                return FormatMessages(errors);
            }

            public string FormatWarnings()
            {
                return FormatMessages(warnings);
            }

            public string FormatAll()
            {
                ArrayList all = new ArrayList(errors.Count + warnings.Count);
                all.AddRange(errors);
                all.AddRange(warnings);
                return FormatMessages(all);
            }

            private static string FormatDiagnostic(
                ValidationEventArgs eventArgs)
            {
                StringBuilder diagnostic = new StringBuilder();
                diagnostic.Append(eventArgs.Severity.ToString());
                diagnostic.Append(": ");

                XmlSchemaException exception = eventArgs.Exception;
                if (exception != null)
                {
                    if (!String.IsNullOrEmpty(exception.SourceUri))
                    {
                        diagnostic.Append(exception.SourceUri);
                        diagnostic.Append(" ");
                    }

                    if (exception.LineNumber > 0)
                    {
                        diagnostic.Append("(line ");
                        diagnostic.Append(exception.LineNumber.ToString(
                            CultureInfo.InvariantCulture));
                        if (exception.LinePosition > 0)
                        {
                            diagnostic.Append(", position ");
                            diagnostic.Append(exception.LinePosition.ToString(
                                CultureInfo.InvariantCulture));
                        }
                        diagnostic.Append(") ");
                    }
                }

                diagnostic.Append(eventArgs.Message);
                return diagnostic.ToString();
            }

            private static string FormatMessages(ArrayList messages)
            {
                StringBuilder formatted = new StringBuilder();
                for (int index = 0; index < messages.Count; index++)
                {
                    if (index != 0)
                    {
                        formatted.Append(Environment.NewLine);
                    }

                    formatted.Append((string)messages[index]);
                }

                return formatted.ToString();
            }
        }
    }
}
