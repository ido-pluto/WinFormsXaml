using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace WinFormsXaml
{
    public sealed partial class XamlRuntime
    {
        /// <summary>
        /// Runs the normal, non-virtual item refresh immediately or in bounded
        /// UI-thread batches. Viewport realization has its own synchronous path
        /// and never enters this progressive timer.
        /// </summary>
        private void StartItemsRefreshWork(
            ItemsRefreshState state)
        {
            ItemsControl host = state.Host;
            int totalWork =
                state.PatchQueue.Count +
                state.BuildQueue.Count;

            if (!host.ProgressiveRendering || totalWork <= 1)
            {
                try
                {
                    while (state.PatchIndex < state.PatchQueue.Count &&
                           IsItemsRefreshCurrent(state))
                    {
                        ApplyItemsPatchBatch(state, state.PatchQueue.Count);
                    }

                    while (state.BuildIndex < state.BuildQueue.Count &&
                           IsItemsRefreshCurrent(state))
                    {
                        BuildItemsRefreshBatch(state, state.BuildQueue.Count);
                    }

                    if (!IsItemsRefreshCurrent(state))
                        return;

                    CommitItemsRefresh(state);
                }
                catch (Exception ex)
                {
                    if (state.Committed)
                        throw new ItemsRefreshCommittedException(ex);

                    if (host.PendingRefresh == state)
                    {
                        FailItemsRefresh(state, ex, true);
                        throw new ItemsRefreshFailedException(ex);
                    }

                    throw;
                }

                return;
            }

            state.ProgressiveBudget = new Stopwatch();

            try
            {
                bool workComplete =
                    RunProgressiveItemsRefreshBatch(state);

                if (!IsItemsRefreshCurrent(state))
                    return;

                if (workComplete)
                {
                    CommitItemsRefresh(state);
                    return;
                }
            }
            catch (Exception ex)
            {
                if (state.Committed)
                    throw new ItemsRefreshCommittedException(ex);

                if (host.PendingRefresh == state)
                {
                    FailItemsRefresh(state, ex, true);
                    throw new ItemsRefreshFailedException(ex);
                }

                throw;
            }

            Timer timer = new Timer();
            timer.Interval = host.ProgressiveInterval;
            state.Timer = timer;

            timer.Tick += delegate
            {
                if (host.IsDisposed ||
                    host.PendingRefresh != state ||
                    state.Generation != host.RefreshGeneration)
                {
                    timer.Stop();
                    return;
                }

                try
                {
                    bool workComplete =
                        RunProgressiveItemsRefreshBatch(state);

                    if (workComplete && IsItemsRefreshCurrent(state))
                    {
                        timer.Stop();
                        CommitItemsRefresh(state);
                    }
                }
                catch (Exception ex)
                {
                    timer.Stop();

                    if (state.Committed)
                        throw;

                    if (host.PendingRefresh == state)
                        FailItemsRefresh(state, ex, false);
                    else
                        throw;
                }
            };

            timer.Start();
        }

        private bool RunProgressiveItemsRefreshBatch(
            ItemsRefreshState state)
        {
            if (!IsItemsRefreshCurrent(state))
                return false;

            ItemsControl host = state.Host;
            Stopwatch budget = state.ProgressiveBudget;

            if (budget == null)
            {
                budget = new Stopwatch();
                state.ProgressiveBudget = budget;
            }

            budget.Reset();
            budget.Start();

            int processed = 0;
            int maximum = Math.Max(1, host.ProgressiveBatchSize);
            int timeBudget = Math.Max(
                1,
                host.ProgressiveTimeBudgetMs);

            try
            {
                while (processed < maximum)
                {
                    bool didWork = false;

                    if (state.PatchIndex < state.PatchQueue.Count)
                    {
                        didWork = ApplyItemsPatchBatch(state, 1) > 0;
                    }
                    else if (state.BuildIndex < state.BuildQueue.Count)
                    {
                        int before = state.BuildIndex;
                        BuildItemsRefreshBatch(state, 1);
                        didWork = state.BuildIndex > before;
                    }

                    if (!didWork)
                        break;

                    processed++;

                    if (budget.ElapsedMilliseconds >= timeBudget)
                        break;
                }
            }
            finally
            {
                budget.Stop();
            }

            if (processed > 0)
                host.RecordProgressiveBatch();

            if (!IsItemsRefreshCurrent(state))
                return false;

            bool workComplete =
                state.PatchIndex >= state.PatchQueue.Count &&
                state.BuildIndex >= state.BuildQueue.Count;

            if (state.PatchLayoutDirty)
            {
                state.PatchLayoutDirty = false;

                if (!workComplete)
                    host.PerformLayout();
            }

            return workComplete && IsItemsRefreshCurrent(state);
        }
    }
}
