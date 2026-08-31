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
        private static readonly GridDefinition _defaultStarGridDefinition =
            CreateDefaultStarDefinition();

        // Preferred-size calls repeat within Grid and Flex measure/arrange work.
        // Keep only the current outer custom-layout pass. A fixed table prevents
        // adversarial control trees from growing a runtime-owned collection.
        private const int PreferredSizeCacheCapacity = 256;

        private struct PreferredSizeCacheEntry
        {
            public Control Control;
            public int ProposedWidth;
            public int ProposedHeight;
            public Size PreferredSize;
        }

        private PreferredSizeCacheEntry[] _preferredSizeCache;
        private int[] _preferredSizeCacheUsedSlots;
        private int _preferredSizeCacheUsedCount;
        private int _preferredSizePassDepth;

        private void BeginPreferredSizePass()
        {
            _preferredSizePassDepth++;
        }

        private void EndPreferredSizePass()
        {
            if (_preferredSizePassDepth <= 0)
                return;

            _preferredSizePassDepth--;

            if (_preferredSizePassDepth == 0 &&
                _preferredSizeCache != null)
            {
                // Do not retain controls after the pass. Clearing the reusable
                // fixed table also guarantees that a later pass cannot observe
                // a result produced before a property or child-tree change.
                int i;

                for (i = 0;
                     i < _preferredSizeCacheUsedCount;
                     i++)
                {
                    int index =
                        _preferredSizeCacheUsedSlots[i];

                    _preferredSizeCache[index] =
                        new PreferredSizeCacheEntry();
                }

                _preferredSizeCacheUsedCount = 0;
            }
        }

        private bool TryGetCachedPreferredSize(
            Control control,
            Size proposed,
            out Size preferred)
        {
            preferred = Size.Empty;

            if (_preferredSizePassDepth <= 0 ||
                _preferredSizeCache == null ||
                control == null)
            {
                return false;
            }

            int index =
                GetPreferredSizeCacheIndex(
                    control,
                    proposed);

            int probe;

            for (probe = 0;
                 probe < PreferredSizeCacheCapacity;
                 probe++)
            {
                PreferredSizeCacheEntry entry =
                    _preferredSizeCache[index];

                if (entry.Control == null)
                    return false;

                if (Object.ReferenceEquals(
                        entry.Control,
                        control) &&
                    entry.ProposedWidth == proposed.Width &&
                    entry.ProposedHeight == proposed.Height)
                {
                    preferred = entry.PreferredSize;
                    return true;
                }

                index =
                    (index + 1) &
                    (PreferredSizeCacheCapacity - 1);
            }

            return false;
        }

        private void CachePreferredSize(
            Control control,
            Size proposed,
            Size preferred)
        {
            if (_preferredSizePassDepth <= 0 ||
                control == null)
            {
                return;
            }

            if (_preferredSizeCache == null)
            {
                // Allocate lazily: empty custom hosts never pay for the table.
                // Publish both fixed arrays only after both allocations succeed.
                PreferredSizeCacheEntry[] cache =
                    new PreferredSizeCacheEntry[
                        PreferredSizeCacheCapacity];

                int[] usedSlots =
                    new int[
                        PreferredSizeCacheCapacity];

                _preferredSizeCache = cache;
                _preferredSizeCacheUsedSlots = usedSlots;
            }

            int index =
                GetPreferredSizeCacheIndex(
                    control,
                    proposed);

            int probe;

            for (probe = 0;
                 probe < PreferredSizeCacheCapacity;
                 probe++)
            {
                PreferredSizeCacheEntry entry =
                    _preferredSizeCache[index];

                if (entry.Control == null ||
                    (Object.ReferenceEquals(
                         entry.Control,
                         control) &&
                     entry.ProposedWidth == proposed.Width &&
                     entry.ProposedHeight == proposed.Height))
                {
                    if (entry.Control == null)
                    {
                        _preferredSizeCacheUsedSlots[
                            _preferredSizeCacheUsedCount] = index;

                        _preferredSizeCacheUsedCount++;
                    }

                    entry.Control = control;
                    entry.ProposedWidth = proposed.Width;
                    entry.ProposedHeight = proposed.Height;
                    entry.PreferredSize = preferred;
                    _preferredSizeCache[index] = entry;
                    return;
                }

                index =
                    (index + 1) &
                    (PreferredSizeCacheCapacity - 1);
            }

            // The bounded cache is only an optimization. Once full, preserve
            // normal measurement behavior without eviction or allocation.
        }

        private static int GetPreferredSizeCacheIndex(
            Control control,
            Size proposed)
        {
            int hash;

            unchecked
            {
                hash =
                    System.Runtime.CompilerServices.RuntimeHelpers
                        .GetHashCode(control);
                hash = (hash * 397) ^ proposed.Width;
                hash = (hash * 397) ^ proposed.Height;
            }

            return hash &
                (PreferredSizeCacheCapacity - 1);
        }

        // ============================================================
        // GRID DEFINITIONS
        // ============================================================

        private void ReadRowDefinitions(
            GridHost grid,
            XmlElement propertyElement)
        {
            grid.Rows.Clear();

            XmlNode node =
                propertyElement.FirstChild;

            while (node != null)
            {
                XmlElement definition =
                    node as XmlElement;

                if (definition != null &&
                    EqualsIgnoreCase(
                        definition.LocalName,
                        "RowDefinition"))
                {
                    string height =
                        GetAttributeIgnoreNamespace(
                            definition,
                            "Height");

                    if (String.IsNullOrEmpty(
                        height))
                    {
                        height = "*";
                    }

                    grid.Rows.Add(
                        ParseGridDefinition(
                            height));
                }

                node =
                    node.NextSibling;
            }
        }

        private void ReadColumnDefinitions(
            GridHost grid,
            XmlElement propertyElement)
        {
            grid.Columns.Clear();

            XmlNode node =
                propertyElement.FirstChild;

            while (node != null)
            {
                XmlElement definition =
                    node as XmlElement;

                if (definition != null &&
                    EqualsIgnoreCase(
                        definition.LocalName,
                        "ColumnDefinition"))
                {
                    string width =
                        GetAttributeIgnoreNamespace(
                            definition,
                            "Width");

                    if (String.IsNullOrEmpty(
                        width))
                    {
                        width = "*";
                    }

                    grid.Columns.Add(
                        ParseGridDefinition(
                            width));
                }

                node =
                    node.NextSibling;
            }
        }

        private GridDefinition ParseGridDefinition(
            string value)
        {
            GridDefinition result =
                new GridDefinition();

            value =
                value.Trim();

            if (EqualsIgnoreCase(
                value,
                "Auto"))
            {
                result.Unit =
                    GridUnit.Auto;

                result.Value =
                    0;

                return result;
            }

            if (value.EndsWith("*"))
            {
                string amount =
                    value.Substring(
                        0,
                        value.Length - 1);

                if (String.IsNullOrEmpty(
                    amount))
                {
                    amount = "1";
                }

                result.Unit =
                    GridUnit.Star;

                result.Value =
                    ValidateGridDefinitionValue(
                        ParseFloat(amount),
                        value);

                return result;
            }

            result.Unit =
                GridUnit.Pixel;

            result.Value =
                ValidateGridDefinitionValue(
                    ParseFloat(value),
                    value);

            return result;
        }

        private static float ValidateGridDefinitionValue(
            float value,
            string declaration)
        {
            if (Single.IsNaN(value) ||
                Single.IsInfinity(value) ||
                value < 0.0f)
            {
                throw new FormatException(
                    "Grid length '" +
                    declaration +
                    "' must be finite and non-negative.");
            }

            return value;
        }

        // ============================================================
        // GRID LAYOUT
        // ============================================================

        private void LayoutGrid(
            GridHost grid)
        {
            Rectangle inner =
                GetInnerRectangle(
                    grid);

            bool rtl =
                IsRightToLeft(
                    grid);

            int columnCount =
                GetGridColumnCount(
                    grid);

            int rowCount =
                GetGridRowCount(
                    grid);

            int[] widths =
                CalculateGridColumns(
                    grid,
                    inner.Width,
                    columnCount);

            int[] heights =
                CalculateGridRows(
                    grid,
                    inner.Height,
                    rowCount);

            int[] x =
                new int[columnCount];

            int[] y =
                new int[rowCount];

            int current;
            int i;

            if (rtl)
            {
                current =
                    inner.Right;

                for (i = 0;
                     i < columnCount;
                     i++)
                {
                    current -=
                        widths[i];

                    x[i] =
                        current;
                }
            }
            else
            {
                current =
                    inner.Left;

                for (i = 0;
                     i < columnCount;
                     i++)
                {
                    x[i] =
                        current;

                    current +=
                        widths[i];
                }
            }

            current =
                inner.Top;

            for (i = 0;
                 i < rowCount;
                 i++)
            {
                y[i] =
                    current;

                current +=
                    heights[i];
            }

            for (i = 0;
                 i < grid.Controls.Count;
                 i++)
            {
                Control child =
                    grid.Controls[i];

                ElementInfo info =
                    GetInfo(
                        child);

                if (info.Collapsed)
                {
                    SetBoundsIfChanged(
                        child,
                        Rectangle.Empty);

                    continue;
                }

                int column =
                    Clamp(
                        info.GridColumn,
                        0,
                        columnCount - 1);

                int row =
                    Clamp(
                        info.GridRow,
                        0,
                        rowCount - 1);

                int columnSpan =
                    Math.Min(
                        Math.Max(
                            1,
                            info.GridColumnSpan),
                        columnCount -
                            column);

                int rowSpan =
                    Math.Min(
                        Math.Max(
                            1,
                            info.GridRowSpan),
                        rowCount -
                            row);

                int width =
                    0;

                int height =
                    0;

                int n;

                for (n = 0;
                     n < columnSpan;
                     n++)
                {
                    width +=
                        widths[column + n];
                }

                for (n = 0;
                     n < rowSpan;
                     n++)
                {
                    height +=
                        heights[row + n];
                }

                int slotX;

                if (rtl)
                {
                    slotX =
                        x[column];

                    for (n = 1;
                         n < columnSpan;
                         n++)
                    {
                        slotX =
                            Math.Min(
                                slotX,
                                x[column + n]);
                    }
                }
                else
                {
                    slotX =
                        x[column];
                }

                Rectangle slot =
                    new Rectangle(
                        slotX,
                        y[row],
                        width,
                        height);

                LayoutControlInSlot(
                    child,
                    slot,
                    true,
                    true);
            }
        }

        private Size GetPreferredGridSize(
            GridHost grid,
            Size proposed)
        {
            int columns =
                GetGridColumnCount(
                    grid);

            int rows =
                GetGridRowCount(
                    grid);

            int[] widths =
                CalculateGridColumns(
                    grid,
                    -1,
                    columns);

            int[] heights =
                CalculateGridRows(
                    grid,
                    -1,
                    rows);

            int width =
                grid.Padding.Left +
                grid.Padding.Right;

            int height =
                grid.Padding.Top +
                grid.Padding.Bottom;

            int i;

            for (i = 0;
                 i < widths.Length;
                 i++)
            {
                width +=
                    widths[i];
            }

            for (i = 0;
                 i < heights.Length;
                 i++)
            {
                height +=
                    heights[i];
            }

            return ApplyMinimumSize(
                grid,
                new Size(
                    width,
                    height));
        }

        private int GetGridColumnCount(
            GridHost grid)
        {
            int count =
                Math.Max(
                    1,
                    grid.Columns.Count);

            int i;

            for (i = 0;
                 i < grid.Controls.Count;
                 i++)
            {
                ElementInfo info =
                    GetInfo(
                        grid.Controls[i]);

                count =
                    Math.Max(
                        count,
                        info.GridColumn +
                        Math.Max(
                            1,
                            info.GridColumnSpan));
            }

            return count;
        }

        private int GetGridRowCount(
            GridHost grid)
        {
            int count =
                Math.Max(
                    1,
                    grid.Rows.Count);

            int i;

            for (i = 0;
                 i < grid.Controls.Count;
                 i++)
            {
                ElementInfo info =
                    GetInfo(
                        grid.Controls[i]);

                count =
                    Math.Max(
                        count,
                        info.GridRow +
                        Math.Max(
                            1,
                            info.GridRowSpan));
            }

            return count;
        }

        private int[] CalculateGridColumns(
            GridHost grid,
            int available,
            int count)
        {
            int[] sizes =
                new int[count];

            float starTotal =
                0;

            int i;

            for (i = 0;
                 i < count;
                 i++)
            {
                GridDefinition definition =
                    GetColumnDefinition(
                        grid,
                        i);

                if (definition.Unit ==
                    GridUnit.Pixel)
                {
                    sizes[i] =
                        Math.Max(
                            0,
                            (int)Math.Round(
                                definition.Value));
                }
                else if (
                    definition.Unit ==
                    GridUnit.Star)
                {
                    starTotal +=
                        Math.Max(
                            0.0001f,
                            definition.Value);
                }
            }

            for (i = 0;
                 i < grid.Controls.Count;
                 i++)
            {
                Control child =
                    grid.Controls[i];

                ElementInfo info =
                    GetInfo(
                        child);

                if (info.Collapsed)
                    continue;

                Size desired =
                    GetDesiredSize(
                        child,
                        new Size(
                            Math.Max(
                                1,
                                available),
                            10000));

                int needed =
                    desired.Width +
                    info.Margin.Left +
                    info.Margin.Right;

                int start =
                    Clamp(
                        info.GridColumn,
                        0,
                        count - 1);

                int span =
                    Math.Max(
                        1,
                        Math.Min(
                            info.GridColumnSpan,
                            count - start));

                int each =
                    (int)Math.Ceiling(
                        (double)needed /
                        (double)span);

                int n;

                for (n = 0;
                     n < span;
                     n++)
                {
                    int index =
                        start + n;

                    GridDefinition definition =
                        GetColumnDefinition(
                            grid,
                            index);

                    if (definition.Unit ==
                            GridUnit.Auto ||
                        available < 0)
                    {
                        sizes[index] =
                            Math.Max(
                                sizes[index],
                                each);
                    }
                }
            }

            if (available >= 0)
            {
                int used =
                    0;

                for (i = 0;
                     i < count;
                     i++)
                {
                    GridDefinition definition =
                        GetColumnDefinition(
                            grid,
                            i);

                    if (definition.Unit !=
                        GridUnit.Star)
                    {
                        used +=
                            sizes[i];
                    }
                }

                int remaining =
                    Math.Max(
                        0,
                        available - used);

                if (starTotal > 0)
                {
                    int assigned =
                        0;

                    int lastStar =
                        -1;

                    for (i = 0;
                         i < count;
                         i++)
                    {
                        GridDefinition definition =
                            GetColumnDefinition(
                                grid,
                                i);

                        if (definition.Unit !=
                            GridUnit.Star)
                        {
                            continue;
                        }

                        lastStar =
                            i;

                        int amount =
                            (int)Math.Floor(
                                remaining *
                                (
                                    definition.Value /
                                    starTotal
                                ));

                        sizes[i] =
                            amount;

                        assigned +=
                            amount;
                    }

                    if (lastStar >= 0)
                    {
                        sizes[lastStar] +=
                            remaining -
                            assigned;
                    }
                }
            }

            return sizes;
        }

        private int[] CalculateGridRows(
            GridHost grid,
            int available,
            int count)
        {
            int[] sizes =
                new int[count];

            float starTotal =
                0;

            int i;

            for (i = 0;
                 i < count;
                 i++)
            {
                GridDefinition definition =
                    GetRowDefinition(
                        grid,
                        i);

                if (definition.Unit ==
                    GridUnit.Pixel)
                {
                    sizes[i] =
                        Math.Max(
                            0,
                            (int)Math.Round(
                                definition.Value));
                }
                else if (
                    definition.Unit ==
                    GridUnit.Star)
                {
                    starTotal +=
                        Math.Max(
                            0.0001f,
                            definition.Value);
                }
            }

            for (i = 0;
                 i < grid.Controls.Count;
                 i++)
            {
                Control child =
                    grid.Controls[i];

                ElementInfo info =
                    GetInfo(
                        child);

                if (info.Collapsed)
                    continue;

                Size desired =
                    GetDesiredSize(
                        child,
                        new Size(
                            10000,
                            Math.Max(
                                1,
                                available)));

                int needed =
                    desired.Height +
                    info.Margin.Top +
                    info.Margin.Bottom;

                int start =
                    Clamp(
                        info.GridRow,
                        0,
                        count - 1);

                int span =
                    Math.Max(
                        1,
                        Math.Min(
                            info.GridRowSpan,
                            count - start));

                int each =
                    (int)Math.Ceiling(
                        (double)needed /
                        (double)span);

                int n;

                for (n = 0;
                     n < span;
                     n++)
                {
                    int index =
                        start + n;

                    GridDefinition definition =
                        GetRowDefinition(
                            grid,
                            index);

                    if (definition.Unit ==
                            GridUnit.Auto ||
                        available < 0)
                    {
                        sizes[index] =
                            Math.Max(
                                sizes[index],
                                each);
                    }
                }
            }

            if (available >= 0)
            {
                int used =
                    0;

                for (i = 0;
                     i < count;
                     i++)
                {
                    GridDefinition definition =
                        GetRowDefinition(
                            grid,
                            i);

                    if (definition.Unit !=
                        GridUnit.Star)
                    {
                        used +=
                            sizes[i];
                    }
                }

                int remaining =
                    Math.Max(
                        0,
                        available - used);

                if (starTotal > 0)
                {
                    int assigned =
                        0;

                    int lastStar =
                        -1;

                    for (i = 0;
                         i < count;
                         i++)
                    {
                        GridDefinition definition =
                            GetRowDefinition(
                                grid,
                                i);

                        if (definition.Unit !=
                            GridUnit.Star)
                        {
                            continue;
                        }

                        lastStar =
                            i;

                        int amount =
                            (int)Math.Floor(
                                remaining *
                                (
                                    definition.Value /
                                    starTotal
                                ));

                        sizes[i] =
                            amount;

                        assigned +=
                            amount;
                    }

                    if (lastStar >= 0)
                    {
                        sizes[lastStar] +=
                            remaining -
                            assigned;
                    }
                }
            }

            return sizes;
        }

        private GridDefinition GetColumnDefinition(
            GridHost grid,
            int index)
        {
            if (index >= 0 &&
                index <
                    grid.Columns.Count)
            {
                return grid.Columns[
                    index];
            }

            return _defaultStarGridDefinition;
        }

        private GridDefinition GetRowDefinition(
            GridHost grid,
            int index)
        {
            if (index >= 0 &&
                index <
                    grid.Rows.Count)
            {
                return grid.Rows[
                    index];
            }

            return _defaultStarGridDefinition;
        }

        private static GridDefinition CreateDefaultStarDefinition()
        {
            GridDefinition result =
                new GridDefinition();

            result.Unit =
                GridUnit.Star;

            result.Value =
                1;

            return result;
        }

        // ============================================================
        // STACK LAYOUT
        // ============================================================

        private void LayoutStack(
            StackHost stack)
        {
            Rectangle inner =
                GetInnerRectangle(
                    stack);

            bool rtl =
                IsRightToLeft(
                    stack);

            int cursor;

            if (stack.StackOrientation ==
                Orientation.Vertical)
            {
                cursor =
                    inner.Top;
            }
            else
            {
                cursor =
                    rtl
                        ? inner.Right
                        : inner.Left;
            }

            int i;
            bool hasPrevious = false;

            for (i = 0;
                 i < stack.Controls.Count;
                 i++)
            {
                Control child =
                    stack.Controls[i];

                ElementInfo info =
                    GetInfo(
                        child);

                if (info.Collapsed)
                {
                    SetBoundsIfChanged(
                        child,
                        Rectangle.Empty);

                    continue;
                }

                if (hasPrevious)
                {
                    if (stack.StackOrientation ==
                        Orientation.Vertical ||
                        !rtl)
                    {
                        cursor +=
                            stack.StackSpacing;
                    }
                    else
                    {
                        cursor -=
                            stack.StackSpacing;
                    }
                }

                Padding margin =
                    GetEffectiveMargin(
                        child,
                        info.Margin);

                Size desired =
                    GetDesiredSize(
                        child,
                        inner.Size);

                if (stack.StackOrientation ==
                    Orientation.Vertical)
                {
                    int totalHeight =
                        desired.Height +
                        margin.Top +
                        margin.Bottom;

                    Rectangle slot =
                        new Rectangle(
                            inner.Left,
                            cursor,
                            inner.Width,
                            totalHeight);

                    LayoutControlInSlot(
                        child,
                        slot,
                        true,
                        false);

                    cursor +=
                        totalHeight;
                }
                else
                {
                    int totalWidth =
                        desired.Width +
                        margin.Left +
                        margin.Right;

                    Rectangle slot;

                    if (rtl)
                    {
                        slot =
                            new Rectangle(
                                cursor -
                                totalWidth,
                                inner.Top,
                                totalWidth,
                                inner.Height);

                        cursor -=
                            totalWidth;
                    }
                    else
                    {
                        slot =
                            new Rectangle(
                                cursor,
                                inner.Top,
                                totalWidth,
                                inner.Height);

                        cursor +=
                            totalWidth;
                    }

                    LayoutControlInSlot(
                        child,
                        slot,
                        false,
                        true);
                }

                hasPrevious = true;
            }
        }

        private Size GetPreferredStackSize(
            StackHost stack,
            Size proposed)
        {
            int width =
                0;

            int height =
                0;

            int visibleCount =
                0;

            int i;

            for (i = 0;
                 i < stack.Controls.Count;
                 i++)
            {
                Control child =
                    stack.Controls[i];

                ElementInfo info =
                    GetInfo(
                        child);

                if (info.Collapsed)
                    continue;

                visibleCount++;

                Size desired =
                    GetDesiredSize(
                        child,
                        proposed);

                int childWidth =
                    desired.Width +
                    info.Margin.Left +
                    info.Margin.Right;

                int childHeight =
                    desired.Height +
                    info.Margin.Top +
                    info.Margin.Bottom;

                if (stack.StackOrientation ==
                    Orientation.Vertical)
                {
                    width =
                        Math.Max(
                            width,
                            childWidth);

                    height +=
                        childHeight;
                }
                else
                {
                    width +=
                        childWidth;

                    height =
                        Math.Max(
                            height,
                            childHeight);
                }
            }

            if (visibleCount > 1)
            {
                int spacing =
                    (visibleCount - 1) *
                    stack.StackSpacing;

                if (stack.StackOrientation ==
                    Orientation.Vertical)
                {
                    height += spacing;
                }
                else
                {
                    width += spacing;
                }
            }

            width +=
                stack.Padding.Left +
                stack.Padding.Right;

            height +=
                stack.Padding.Top +
                stack.Padding.Bottom;

            return ApplyMinimumSize(
                stack,
                new Size(
                    width,
                    height));
        }

        // ============================================================
        // DOCK LAYOUT
        // ============================================================

        private void LayoutDock(
            DockHost dock)
        {
            Rectangle remaining =
                GetInnerRectangle(
                    dock);

            int count =
                dock.Controls.Count;

            int i;

            for (i = 0;
                 i < count;
                 i++)
            {
                Control child =
                    dock.Controls[i];

                ElementInfo info =
                    GetInfo(
                        child);

                if (info.Collapsed)
                {
                    SetBoundsIfChanged(
                        child,
                        Rectangle.Empty);

                    continue;
                }

                bool isLast =
                    i ==
                    count - 1;

                if (isLast &&
                    dock.LastChildFill)
                {
                    LayoutControlInSlot(
                        child,
                        remaining,
                        true,
                        true);

                    continue;
                }

                DockStyle side =
                    info.DockExplicit
                        ? info.DockSide
                        : DockStyle.Left;

                side =
                    GetEffectiveDock(
                        dock,
                        side);

                Padding margin =
                    GetEffectiveMargin(
                        child,
                        info.Margin);

                Size desired =
                    GetDesiredSize(
                        child,
                        remaining.Size);

                int totalWidth =
                    desired.Width +
                    margin.Left +
                    margin.Right;

                int totalHeight =
                    desired.Height +
                    margin.Top +
                    margin.Bottom;

                Rectangle slot;

                if (side ==
                    DockStyle.Right)
                {
                    totalWidth =
                        Math.Min(
                            totalWidth,
                            remaining.Width);

                    slot =
                        new Rectangle(
                            remaining.Right -
                            totalWidth,
                            remaining.Top,
                            totalWidth,
                            remaining.Height);

                    remaining.Width -=
                        totalWidth;
                }
                else if (
                    side ==
                    DockStyle.Top)
                {
                    totalHeight =
                        Math.Min(
                            totalHeight,
                            remaining.Height);

                    slot =
                        new Rectangle(
                            remaining.Left,
                            remaining.Top,
                            remaining.Width,
                            totalHeight);

                    remaining.Y +=
                        totalHeight;

                    remaining.Height -=
                        totalHeight;
                }
                else if (
                    side ==
                    DockStyle.Bottom)
                {
                    totalHeight =
                        Math.Min(
                            totalHeight,
                            remaining.Height);

                    slot =
                        new Rectangle(
                            remaining.Left,
                            remaining.Bottom -
                            totalHeight,
                            remaining.Width,
                            totalHeight);

                    remaining.Height -=
                        totalHeight;
                }
                else
                {
                    totalWidth =
                        Math.Min(
                            totalWidth,
                            remaining.Width);

                    slot =
                        new Rectangle(
                            remaining.Left,
                            remaining.Top,
                            totalWidth,
                            remaining.Height);

                    remaining.X +=
                        totalWidth;

                    remaining.Width -=
                        totalWidth;
                }

                if (remaining.Width < 0)
                    remaining.Width = 0;

                if (remaining.Height < 0)
                    remaining.Height = 0;

                LayoutControlInSlot(
                    child,
                    slot,
                    true,
                    true);
            }
        }

        private Size GetPreferredDockSize(
            DockHost dock,
            Size proposed)
        {
            int horizontalWidth =
                0;

            int horizontalHeight =
                0;

            int verticalWidth =
                0;

            int verticalHeight =
                0;

            int i;

            for (i = 0;
                 i < dock.Controls.Count;
                 i++)
            {
                Control child =
                    dock.Controls[i];

                ElementInfo info =
                    GetInfo(
                        child);

                if (info.Collapsed)
                    continue;

                Size desired =
                    GetDesiredSize(
                        child,
                        proposed);

                int childWidth =
                    desired.Width +
                    info.Margin.Left +
                    info.Margin.Right;

                int childHeight =
                    desired.Height +
                    info.Margin.Top +
                    info.Margin.Bottom;

                DockStyle side =
                    info.DockExplicit
                        ? info.DockSide
                        : DockStyle.Left;

                if (side ==
                        DockStyle.Top ||
                    side ==
                        DockStyle.Bottom)
                {
                    verticalHeight +=
                        childHeight;

                    verticalWidth =
                        Math.Max(
                            verticalWidth,
                            childWidth);
                }
                else
                {
                    horizontalWidth +=
                        childWidth;

                    horizontalHeight =
                        Math.Max(
                            horizontalHeight,
                            childHeight);
                }
            }

            int width =
                Math.Max(
                    verticalWidth,
                    horizontalWidth);

            int height =
                verticalHeight +
                horizontalHeight;

            width +=
                dock.Padding.Left +
                dock.Padding.Right;

            height +=
                dock.Padding.Top +
                dock.Padding.Bottom;

            return ApplyMinimumSize(
                dock,
                new Size(
                    width,
                    height));
        }

        // ============================================================
        // CANVAS LAYOUT
        // ============================================================

        private void LayoutCanvas(
            CanvasHost canvas)
        {
            Rectangle inner =
                GetInnerRectangle(
                    canvas);

            bool rtl =
                IsRightToLeft(
                    canvas);

            int i;

            for (i = 0;
                 i < canvas.Controls.Count;
                 i++)
            {
                Control child =
                    canvas.Controls[i];

                ElementInfo info =
                    GetInfo(
                        child);

                if (info.Collapsed)
                {
                    SetBoundsIfChanged(
                        child,
                        Rectangle.Empty);

                    continue;
                }

                Padding margin =
                    GetEffectiveMargin(
                        child,
                        info.Margin);

                Size desired =
                    GetDesiredSize(
                        child,
                        inner.Size);

                int x;
                int y;

                if (rtl)
                {
                    if (info.CanvasLeftSet)
                    {
                        x =
                            inner.Right -
                            info.CanvasLeft -
                            desired.Width;
                    }
                    else if (
                        info.CanvasRightSet)
                    {
                        x =
                            inner.Left +
                            info.CanvasRight;
                    }
                    else
                    {
                        x =
                            inner.Right -
                            desired.Width;
                    }
                }
                else
                {
                    if (info.CanvasLeftSet)
                    {
                        x =
                            inner.Left +
                            info.CanvasLeft;
                    }
                    else if (
                        info.CanvasRightSet)
                    {
                        x =
                            inner.Right -
                            info.CanvasRight -
                            desired.Width;
                    }
                    else
                    {
                        x =
                            inner.Left;
                    }
                }

                if (info.CanvasTopSet)
                {
                    y =
                        inner.Top +
                        info.CanvasTop;
                }
                else if (
                    info.CanvasBottomSet)
                {
                    y =
                        inner.Bottom -
                        info.CanvasBottom -
                        desired.Height;
                }
                else
                {
                    y =
                        inner.Top;
                }

                x +=
                    margin.Left;

                y +=
                    margin.Top;

                SetBoundsIfChanged(
                    child,
                    new Rectangle(
                        x,
                        y,
                        desired.Width,
                        desired.Height));
            }
        }

        private Size GetPreferredCanvasSize(
            CanvasHost canvas,
            Size proposed)
        {
            int width =
                0;

            int height =
                0;

            int i;

            for (i = 0;
                 i < canvas.Controls.Count;
                 i++)
            {
                Control child =
                    canvas.Controls[i];

                ElementInfo info =
                    GetInfo(
                        child);

                if (info.Collapsed)
                    continue;

                Size desired =
                    GetDesiredSize(
                        child,
                        proposed);

                int x =
                    info.CanvasLeftSet
                        ? info.CanvasLeft
                        : 0;

                int y =
                    info.CanvasTopSet
                        ? info.CanvasTop
                        : 0;

                width =
                    Math.Max(
                        width,
                        x +
                        desired.Width +
                        info.Margin.Left +
                        info.Margin.Right);

                height =
                    Math.Max(
                        height,
                        y +
                        desired.Height +
                        info.Margin.Top +
                        info.Margin.Bottom);
            }

            width +=
                canvas.Padding.Left +
                canvas.Padding.Right;

            height +=
                canvas.Padding.Top +
                canvas.Padding.Bottom;

            return ApplyMinimumSize(
                canvas,
                new Size(
                    width,
                    height));
        }

        // ============================================================
        // SINGLE CHILD LAYOUT
        // ============================================================

        private void LayoutSingle(
            SingleHost host)
        {
            Rectangle inner =
                GetInnerRectangle(
                    host);

            int i;

            for (i = 0;
                 i < host.Controls.Count;
                 i++)
            {
                Control child =
                    host.Controls[i];

                ElementInfo info =
                    GetInfo(
                        child);

                if (info.Collapsed)
                {
                    SetBoundsIfChanged(
                        child,
                        Rectangle.Empty);

                    continue;
                }

                LayoutControlInSlot(
                    child,
                    inner,
                    true,
                    true);
            }
        }

        private Size GetPreferredSingleSize(
            SingleHost host,
            Size proposed)
        {
            int width =
                0;

            int height =
                0;

            int i;

            for (i = 0;
                 i < host.Controls.Count;
                 i++)
            {
                Control child =
                    host.Controls[i];

                ElementInfo info =
                    GetInfo(
                        child);

                if (info.Collapsed)
                    continue;

                Size desired =
                    GetDesiredSize(
                        child,
                        proposed);

                width =
                    Math.Max(
                        width,
                        desired.Width +
                        info.Margin.Left +
                        info.Margin.Right);

                height =
                    Math.Max(
                        height,
                        desired.Height +
                        info.Margin.Top +
                        info.Margin.Bottom);
            }

            width +=
                host.Padding.Left +
                host.Padding.Right;

            height +=
                host.Padding.Top +
                host.Padding.Bottom;

            return ApplyMinimumSize(
                host,
                new Size(
                    width,
                    height));
        }

        // ============================================================
        // SLOT LAYOUT
        // ============================================================

        private static void SetBoundsIfChanged(
            Control control,
            Rectangle bounds)
        {
            if (control != null && control.Bounds != bounds)
                control.Bounds = bounds;
        }

        private void LayoutControlInSlot(
            Control child,
            Rectangle slot,
            bool allowHorizontalStretch,
            bool allowVerticalStretch)
        {
            LayoutControlInSlotCore(
                child,
                slot,
                allowHorizontalStretch,
                allowVerticalStretch,
                Size.Empty,
                false);
        }

        private void LayoutControlInSlotWithDesired(
            Control child,
            Rectangle slot,
            bool allowHorizontalStretch,
            bool allowVerticalStretch,
            Size knownDesired)
        {
            LayoutControlInSlotCore(
                child,
                slot,
                allowHorizontalStretch,
                allowVerticalStretch,
                knownDesired,
                true);
        }

        private void LayoutControlInSlotCore(
            Control child,
            Rectangle slot,
            bool allowHorizontalStretch,
            bool allowVerticalStretch,
            Size knownDesired,
            bool hasKnownDesired)
        {
            ElementInfo info =
                GetInfo(
                    child);

            if (info.Collapsed)
            {
                SetBoundsIfChanged(
                    child,
                    Rectangle.Empty);

                return;
            }

            Padding margin =
                GetEffectiveMargin(
                    child,
                    info.Margin);

            Rectangle available =
                new Rectangle(
                    slot.Left +
                    margin.Left,
                    slot.Top +
                    margin.Top,
                    Math.Max(
                        0,
                        slot.Width -
                        margin.Left -
                        margin.Right),
                    Math.Max(
                        0,
                        slot.Height -
                        margin.Top -
                        margin.Bottom));

            Size desired =
                hasKnownDesired
                    ? knownDesired
                    : GetDesiredSize(
                        child,
                        available.Size);

            HorizontalXamlAlignment horizontal =
                GetEffectiveHorizontalAlignment(
                    child,
                    info.HorizontalAlignment);

            int width;
            int height;

            if (allowHorizontalStretch &&
                horizontal ==
                    HorizontalXamlAlignment.Stretch &&
                !info.WidthExplicit)
            {
                width =
                    available.Width;
            }
            else
            {
                width =
                    desired.Width;
            }

            if (allowVerticalStretch &&
                info.VerticalAlignment ==
                    VerticalXamlAlignment.Stretch &&
                !info.HeightExplicit)
            {
                height =
                    available.Height;
            }
            else
            {
                height =
                    desired.Height;
            }

            if (info.WidthExplicit)
            {
                width =
                    child.Width;
            }

            if (info.HeightExplicit)
            {
                height =
                    child.Height;
            }

            width =
                ApplyWidthLimits(
                    child,
                    width);

            height =
                ApplyHeightLimits(
                    child,
                    height);

            width =
                Math.Max(
                    0,
                    Math.Min(
                        width,
                        available.Width));

            height =
                Math.Max(
                    0,
                    Math.Min(
                        height,
                        available.Height));

            int x =
                available.Left;

            int y =
                available.Top;

            if (horizontal ==
                HorizontalXamlAlignment.Center)
            {
                x =
                    available.Left +
                    (
                        available.Width -
                        width
                    ) / 2;
            }
            else if (
                horizontal ==
                HorizontalXamlAlignment.Right)
            {
                x =
                    available.Right -
                    width;
            }

            if (info.VerticalAlignment ==
                VerticalXamlAlignment.Center)
            {
                y =
                    available.Top +
                    (
                        available.Height -
                        height
                    ) / 2;
            }
            else if (
                info.VerticalAlignment ==
                VerticalXamlAlignment.Bottom)
            {
                y =
                    available.Bottom -
                    height;
            }

            Rectangle newBounds =
                new Rectangle(
                    x,
                    y,
                    width,
                    height);

            SetBoundsIfChanged(child, newBounds);
        }

        private Size GetDesiredSize(
            Control control,
            Size proposed)
        {
            bool measurementFailed;

            return GetDesiredSize(
                control,
                proposed,
                out measurementFailed);
        }

        private Size GetDesiredSize(
            Control control,
            Size proposed,
            out bool measurementFailed)
        {
            measurementFailed = false;

            ElementInfo info =
                GetInfo(
                    control);

            Size preferred;

            if (TryGetCachedPreferredSize(
                    control,
                    proposed,
                    out preferred))
            {
                return GetDesiredSizeFromPreferred(
                    control,
                    info,
                    preferred);
            }

            try
            {
                preferred =
                    control.GetPreferredSize(
                        proposed);

                CachePreferredSize(
                    control,
                    proposed,
                    preferred);
            }
            catch
            {
                // A failed measurement may depend on transient control state.
                // Preserve the existing Size fallback, but never cache it.
                measurementFailed = true;
                preferred =
                    control.Size;
            }

            return GetDesiredSizeFromPreferred(
                control,
                info,
                preferred);
        }

        private Size GetDesiredSizeFromPreferred(
            Control control,
            ElementInfo info,
            Size preferred)
        {

            int width =
                info.WidthExplicit
                    ? control.Width
                    : preferred.Width;

            int height =
                info.HeightExplicit
                    ? control.Height
                    : preferred.Height;

            if (width <= 0)
                width = control.Width;

            if (height <= 0)
                height = control.Height;

            width =
                ApplyWidthLimits(
                    control,
                    width);

            height =
                ApplyHeightLimits(
                    control,
                    height);

            return new Size(
                Math.Max(
                    0,
                    width),
                Math.Max(
                    0,
                    height));
        }

        private int ApplyWidthLimits(
            Control control,
            int width)
        {
            if (control.MinimumSize.Width > 0)
            {
                width =
                    Math.Max(
                        width,
                        control.MinimumSize.Width);
            }

            if (control.MaximumSize.Width > 0)
            {
                width =
                    Math.Min(
                        width,
                        control.MaximumSize.Width);
            }

            return width;
        }

        private int ApplyHeightLimits(
            Control control,
            int height)
        {
            if (control.MinimumSize.Height > 0)
            {
                height =
                    Math.Max(
                        height,
                        control.MinimumSize.Height);
            }

            if (control.MaximumSize.Height > 0)
            {
                height =
                    Math.Min(
                        height,
                        control.MaximumSize.Height);
            }

            return height;
        }

        private Size ApplyMinimumSize(
            Control control,
            Size size)
        {
            size.Width =
                ApplyWidthLimits(
                    control,
                    size.Width);

            size.Height =
                ApplyHeightLimits(
                    control,
                    size.Height);

            return size;
        }

        private Rectangle GetInnerRectangle(
            Control control)
        {
            Rectangle result =
                control.ClientRectangle;

            result.X +=
                control.Padding.Left;

            result.Y +=
                control.Padding.Top;

            result.Width -=
                control.Padding.Left +
                control.Padding.Right;

            result.Height -=
                control.Padding.Top +
                control.Padding.Bottom;

            if (result.Width < 0)
                result.Width = 0;

            if (result.Height < 0)
                result.Height = 0;

            return result;
        }

        // ============================================================
        // LAYOUT RECURSION
        // ============================================================

        private void PerformLayoutRecursive(
            Control parent)
        {
            if (parent == null)
                return;

            int i;

            for (i = 0;
                 i < parent.Controls.Count;
                 i++)
            {
                PerformLayoutRecursive(
                    parent.Controls[i]);
            }

            // Leaf controls such as Label/PictureBox/Button do not need an explicit
            // layout pass here. Their parent will position them. Avoiding those no-op
            // PerformLayout calls substantially reduces initial/virtual item build cost.
            if (parent.Controls.Count > 0 ||
                parent is GridHost ||
                parent is StackHost ||
                parent is FlexPanel ||
                parent is DockHost ||
                parent is CanvasHost ||
                parent is SingleHost ||
                parent is ItemsControl)
            {
                parent.PerformLayout();
            }
        }

    }
}
