using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace WinFormsXaml
{
    public sealed partial class XamlRuntime
    {
        private sealed class NamedMemberAssignment
        {
            public string Name;
            public object Target;
            public FieldInfo Field;
            public object Value;
            public object PreviousValue;
        }

        private ArrayList _namedMemberAssignments;
        private bool _namedMemberWiringReady;

        private void InitializeNamedMemberWiring()
        {
            XmlForm target = _eventTarget as XmlForm;

            if (target == null)
                return;

            List<string> names =
                new List<string>(_namedObjects.Keys);
            names.Sort(StringComparer.Ordinal);

            ArrayList planned = new ArrayList();
            int i;

            for (i = 0; i < names.Count; i++)
            {
                string name = names[i];
                NamedMemberAssignment assignment =
                    PlanNamedMemberAssignment(
                        target,
                        name,
                        _namedObjects[name]);

                if (assignment != null)
                    planned.Add(assignment);
            }

            _namedMemberAssignments = new ArrayList();

            try
            {
                for (i = 0; i < planned.Count; i++)
                {
                    NamedMemberAssignment assignment =
                        (NamedMemberAssignment)planned[i];

                    // Publish the cleanup debt before mutating user state. If
                    // reflection fails after a partial assignment, disposal can
                    // still restore the field.
                    _namedMemberAssignments.Add(assignment);
                    assignment.Field.SetValue(
                        assignment.Target,
                        assignment.Value);
                }
            }
            catch
            {
                for (i = _namedMemberAssignments.Count - 1;
                    i >= 0;
                    i--)
                {
                    NamedMemberAssignment assignment =
                        (NamedMemberAssignment)_namedMemberAssignments[i];

                    try
                    {
                        if (Object.ReferenceEquals(
                            assignment.Field.GetValue(assignment.Target),
                            assignment.Value))
                        {
                            assignment.Field.SetValue(
                                assignment.Target,
                                assignment.PreviousValue);
                        }

                        _namedMemberAssignments.RemoveAt(i);
                    }
                    catch
                    {
                    }
                }

                if (_namedMemberAssignments.Count == 0)
                    _namedMemberAssignments = null;

                throw;
            }

            _namedMemberWiringReady = true;
        }

        private void WireRegisteredName(
            string name,
            object value)
        {
            if (!_namedMemberWiringReady)
                return;

            XmlForm target = _eventTarget as XmlForm;

            if (target == null)
                return;

            NamedMemberAssignment assignment =
                PlanNamedMemberAssignment(
                    target,
                    name,
                    value);

            if (assignment == null)
                return;

            if (_namedMemberAssignments == null)
                _namedMemberAssignments = new ArrayList();

            _namedMemberAssignments.Add(assignment);

            try
            {
                assignment.Field.SetValue(
                    assignment.Target,
                    assignment.Value);
            }
            catch
            {
                try
                {
                    object current =
                        assignment.Field.GetValue(assignment.Target);

                    if (Object.ReferenceEquals(
                        current,
                        assignment.Value))
                    {
                        assignment.Field.SetValue(
                            assignment.Target,
                            assignment.PreviousValue);
                    }

                    // A different current value belongs to application code
                    // and must be preserved; either way no framework value is
                    // left to restore.
                    _namedMemberAssignments.Remove(assignment);

                    if (_namedMemberAssignments.Count == 0)
                        _namedMemberAssignments = null;
                }
                catch
                {
                    // Keep the published assignment as retryable disposal debt.
                }

                throw;
            }
        }

        private NamedMemberAssignment PlanNamedMemberAssignment(
            XmlForm target,
            string name,
            object value)
        {
            FieldInfo field = FindNamedMemberField(
                target.GetType(),
                name,
                value);

            if (field == null)
                return null;

            object previousValue = field.GetValue(target);

            if (Object.ReferenceEquals(previousValue, value))
                return null;

            if (previousValue != null)
            {
                throw new InvalidOperationException(
                    "XmlForm field '" +
                    field.Name +
                    "' already contains a value and cannot be wired to XML Name '" +
                    name +
                    "'. Declare the field without an initializer or use a different name.");
            }

            NamedMemberAssignment assignment =
                new NamedMemberAssignment();
            assignment.Name = name;
            assignment.Target = target;
            assignment.Field = field;
            assignment.Value = value;
            assignment.PreviousValue = previousValue;
            return assignment;
        }

        private static FieldInfo FindNamedMemberField(
            Type targetType,
            string name,
            object value)
        {
            FieldInfo exactMatch = null;
            FieldInfo ignoreCaseMatch = null;
            Type type = targetType;

            // XmlForm's own lifetime fields are framework implementation state,
            // not injection targets. Intermediate application base classes above
            // XmlForm remain eligible.
            while (type != null && type != typeof(XmlForm))
            {
                FieldInfo[] fields = type.GetFields(
                    BindingFlags.Instance |
                    BindingFlags.Static |
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly);
                int i;

                for (i = 0; i < fields.Length; i++)
                {
                    FieldInfo field = fields[i];

                    if (String.Equals(
                        field.Name,
                        name,
                        StringComparison.Ordinal))
                    {
                        if (exactMatch == null)
                            exactMatch = field;

                        continue;
                    }

                    if (!String.Equals(
                        field.Name,
                        name,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!CanWireNamedMemberField(field, value))
                        continue;

                    if (ignoreCaseMatch != null)
                    {
                        throw new InvalidOperationException(
                            "XML Name '" +
                            name +
                            "' matches more than one writable XmlForm field by case. " +
                            "Use an exact field name.");
                    }

                    ignoreCaseMatch = field;
                }

                type = type.BaseType;
            }

            if (exactMatch != null)
            {
                ValidateNamedMemberField(
                    exactMatch,
                    name,
                    value);
                return exactMatch;
            }

            return ignoreCaseMatch;
        }

        private static void ValidateNamedMemberField(
            FieldInfo field,
            string name,
            object value)
        {
            if (field.IsStatic ||
                field.IsLiteral ||
                field.IsInitOnly)
            {
                throw new InvalidOperationException(
                    "XmlForm field '" +
                    field.Name +
                    "' matches XML Name '" +
                    name +
                    "' but is not a writable instance field.");
            }

            if (field.FieldType.IsValueType ||
                (value != null &&
                 !field.FieldType.IsInstanceOfType(value)))
            {
                throw new InvalidOperationException(
                    "XmlForm field '" +
                    field.Name +
                    "' cannot receive XML Name '" +
                    name +
                    "' because " +
                    field.FieldType.FullName +
                    " is not compatible with " +
                    (value == null
                        ? "null"
                        : value.GetType().FullName) +
                    ".");
            }
        }

        private static bool CanWireNamedMemberField(
            FieldInfo field,
            object value)
        {
            if (field.IsStatic ||
                field.IsLiteral ||
                field.IsInitOnly ||
                field.FieldType.IsValueType)
            {
                return false;
            }

            return value == null ||
                field.FieldType.IsInstanceOfType(value);
        }

        private void UnwireRegisteredName(
            string name,
            object value)
        {
            if (_namedMemberAssignments == null)
                return;

            int i;

            for (i = _namedMemberAssignments.Count - 1; i >= 0; i--)
            {
                NamedMemberAssignment assignment =
                    (NamedMemberAssignment)_namedMemberAssignments[i];

                if (!String.Equals(
                        assignment.Name,
                        name,
                        StringComparison.OrdinalIgnoreCase) ||
                    !Object.ReferenceEquals(assignment.Value, value))
                {
                    continue;
                }

                if (Object.ReferenceEquals(
                    assignment.Field.GetValue(assignment.Target),
                    assignment.Value))
                {
                    assignment.Field.SetValue(
                        assignment.Target,
                        assignment.PreviousValue);
                }

                _namedMemberAssignments.RemoveAt(i);
            }
        }

        private void DisposeNamedMemberWiring()
        {
            _namedMemberWiringReady = false;

            if (_namedMemberAssignments == null)
                return;

            Exception firstError = null;
            int i;

            for (i = _namedMemberAssignments.Count - 1; i >= 0; i--)
            {
                NamedMemberAssignment assignment =
                    (NamedMemberAssignment)_namedMemberAssignments[i];

                try
                {
                    if (Object.ReferenceEquals(
                        assignment.Field.GetValue(assignment.Target),
                        assignment.Value))
                    {
                        assignment.Field.SetValue(
                            assignment.Target,
                            assignment.PreviousValue);
                    }

                    _namedMemberAssignments.RemoveAt(i);
                }
                catch (Exception ex)
                {
                    if (firstError == null)
                        firstError = ex;
                }
            }

            if (_namedMemberAssignments.Count == 0)
                _namedMemberAssignments = null;

            if (firstError != null)
            {
                throw new InvalidOperationException(
                    "One or more automatically wired XmlForm fields could not be restored: " +
                    firstError.Message,
                    firstError);
            }
        }
    }
}
