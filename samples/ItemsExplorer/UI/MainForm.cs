using System;
using System.Collections.Generic;
using System.Windows.Forms;
using WinFormsXaml;

namespace ItemsExplorer.UI
{
    /// <summary>
    /// Demonstrates the explicit contract required before a native row tree
    /// may be rebound to a different item.
    /// </summary>
    public sealed class RecyclableRowPanel : Panel,
        IRecyclableItemControl
    {
        private int _recycleCount;

        /// <summary>Gets the number of accepted cross-item resets.</summary>
        public int RecycleCount
        {
            get { return _recycleCount; }
        }

        /// <summary>Resets transient state before dynamic XAML values are reapplied.</summary>
        public bool TryPrepareForRecycle(ItemRecycleContext context)
        {
            // This sample owns no editor state. A real row would reset hover,
            // validation, animation, and uncommitted input here. The runtime
            // reapplies every dynamic XAML value after this method succeeds.
            _recycleCount++;
            Cursor = Cursors.Default;
            Tag = null;
            return true;
        }
    }

    public sealed class MainForm : XmlForm
    {
        public readonly ItemsBinding<Row> RecentRows;
        public readonly ItemsBinding<Row> LargeRows;
        private ItemsControl LargeResults = null;
        private ItemsControl LightweightResults = null;
        private ItemsControl RecyclingResults = null;
        private int _nextId;

        public MainForm()
        {
            RecentRows = new ItemsBinding<Row>();
            _nextId = 1;

            RecentRows.Add(CreateRow("First result"));
            RecentRows.Add(CreateRow("Second result"));
            RecentRows.Add(CreateRow("Edit a row in place"));

            List<Row> largeRows = new List<Row>(2500);
            int i;

            for (i = 0; i < 2500; i++)
            {
                largeRows.Add(
                    CreateRow(
                        "Virtual row " + (i + 1).ToString()));
            }

            LargeRows = new ItemsBinding<Row>(largeRows);
        }

        private Row CreateRow(string title)
        {
            Row row = new Row(_nextId, title);
            _nextId++;
            return row;
        }

        private void AddRow_Click(
            object sender,
            EventArgs e)
        {
            RecentRows.Add(
                CreateRow(
                    "Added at " + DateTime.Now.ToLongTimeString()));
        }

        private void RenameFirst_Click(
            object sender,
            EventArgs e)
        {
            if (RecentRows.Count == 0)
                return;

            RecentRows[0].Title.Value =
                "Patched at " + DateTime.Now.ToLongTimeString();
        }

        private void ReplaceRows_Click(
            object sender,
            EventArgs e)
        {
            List<Row> replacement =
                new List<Row>(RecentRows.Count + 1);
            int i;

            for (i = 0; i < RecentRows.Count; i++)
            {
                Row current = RecentRows[i];

                if (i == 0)
                {
                    replacement.Add(
                        new Row(
                            current.Id,
                            current.Title.Value + " (new snapshot)"));
                }
                else if (i != 1)
                {
                    replacement.Add(current);
                }
            }

            replacement.Add(
                CreateRow(
                    "Added by Replace at " +
                    DateTime.Now.ToLongTimeString()));
            RecentRows.Replace(replacement);
        }

        private void ToggleRow_Click(
            object sender,
            EventArgs e)
        {
            Button button = (Button)sender;
            Row row = (Row)button.Tag;
            row.IsVisible.Value = !row.IsVisible.Value;
        }

        private void JumpToMiddle_Click(
            object sender,
            EventArgs e)
        {
            LargeResults.ScrollToIndex(LargeRows.Count / 2);
        }

        private void JumpLightweightToMiddle_Click(
            object sender,
            EventArgs e)
        {
            LightweightResults.ScrollToIndex(LargeRows.Count / 2);
        }

        private void JumpRecyclingToMiddle_Click(
            object sender,
            EventArgs e)
        {
            RecyclingResults.ScrollToIndex(LargeRows.Count / 2);
        }

        public sealed class Row
        {
            public readonly int Id;
            public readonly PropertyBinding<string> Title;
            public readonly PropertyBinding<bool> IsVisible;
            public readonly PropertyBinding<int> Version;
            public readonly string Url;

            public Row(int id, string title)
            {
                Id = id;
                Title = new PropertyBinding<string>(title);
                IsVisible = new PropertyBinding<bool>(true);
                Version = new PropertyBinding<int>(1);
                Url = "https://github.com/";

                Title.ValueChanged += OnValueChanged;
                IsVisible.ValueChanged += OnValueChanged;
            }

            private void OnValueChanged(object sender, EventArgs e)
            {
                Version.Value = Version.Value + 1;
            }
        }
    }
}
