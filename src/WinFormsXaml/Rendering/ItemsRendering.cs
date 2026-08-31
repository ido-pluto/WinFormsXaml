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
        private const int CompiledTemplateElementMapThreshold = 8;

        private static readonly string[] CommonItemKeyPaths =
        {
            "Id",
            "ID",
            "_id",
            "Key"
        };
        private static readonly object _itemRootConditionStateKey =
            new object();

        private readonly object _reactiveItemUpdateSync = new object();
        private readonly Hashtable _pendingReactiveItemUpdates =
            new Hashtable();

        // ============================================================
        // ITEMS CONTROL RENDERING / BINDING
        // ============================================================

        private static bool IsRemovedItemsTemplateAliasElement(
            XmlElement element)
        {
            if (element == null)
                return false;

            return
                EqualsIgnoreCase(
                    element.LocalName,
                    "Template") ||
                EqualsIgnoreCase(
                    element.LocalName,
                    "DataTemplate");
        }

        private static bool IsItemsControlElement(
            XmlElement element)
        {
            if (element == null)
                return false;

            return EqualsIgnoreCase(
                element.LocalName,
                "ItemsControl");
        }

        private static bool IsNestedItemsTemplateContainer(
            XmlElement element)
        {
            if (element == null)
                return false;

            XmlElement parent = element.ParentNode as XmlElement;

            if (!IsItemsControlElement(parent))
                return false;

            if (IsRemovedItemsTemplateAliasElement(element))
                return true;

            if (!IsPropertyElement(element))
                return false;

            string propertyName = GetPropertyElementName(
                element.LocalName);

            return EqualsIgnoreCase(
                propertyName,
                "ItemTemplate");
        }

        private static XmlElement ExtractTemplateRoot(
            XmlElement container)
        {
            if (container == null)
                return null;

            XmlElement first =
                GetFirstElementChild(
                    container);

            if (first == null)
            {
                throw new InvalidOperationException(
                    "ItemsControl template contains no element.");
            }

            if (IsRemovedItemsTemplateAliasElement(first))
            {
                throw new InvalidOperationException(
                    "ItemsControl.ItemTemplate must contain its visual root " +
                    "directly. Template and DataTemplate wrapper elements " +
                    "are not supported.");
            }

            return first;
        }

        private static XmlElement GetFirstElementChild(
            XmlElement element)
        {
            XmlNode node =
                element.FirstChild;

            while (node != null)
            {
                XmlElement child =
                    node as XmlElement;

                if (child != null)
                    return child;

                node = node.NextSibling;
            }

            return null;
        }

        private enum RenderBindingSlotKind
        {
            Attribute,
            InnerText,
            Condition,
            RebuildOnChange
        }

        private enum ItemVisibilityState
        {
            Visible,
            Hidden,
            Collapsed
        }

        private sealed class RenderBindingDefinition
        {
            public XmlElement SourceElement;
            public string ElementPath;
            public int[] ElementPathIndices;
            public string TargetElementPath;
            public string AttributeName;
            public string XmlAttributeName;
            public string Expression;
            public BindingExpressionPlan DirectPlan;
            public RenderBindingSlotKind Kind;
            public bool AffectsLayout;
            public bool PropertyElementValue;
            public bool ComponentOwned;
        }

        private sealed class CompiledItemTemplate
        {
            public XmlElement AnnotatedRoot;
            public ArrayList BindingDefinitions;
            public Hashtable StyleScopesByElementPath;
            public ArrayList LoadedPresetElements;
            public CompiledControlBlueprint ControlBlueprint;
        }

        /// <summary>
        /// Captures the resource and diagnostic context in which an
        /// ItemsControl.ItemTemplate was declared. Nested item hosts can realize
        /// their rows after the parent template or component build has returned,
        /// so they cannot depend on the runtime's transient active fields.
        /// </summary>
        private sealed class ItemTemplateDeclarationContext
        {
            public Dictionary<string, StyleDefinition> NamedStyles;
            public List<StyleDefinition> ImplicitStyles;
            public string MarkupSource;
            public string ElementPathPrefix;
            public Assembly MarkupAssembly;
        }

        private struct ItemTemplateActiveContext
        {
            public Dictionary<string, StyleDefinition> NamedStyles;
            public List<StyleDefinition> ImplicitStyles;
            public string MarkupSource;
            public string ElementPathPrefix;
            public Assembly MarkupAssembly;
        }

        /// <summary>
        /// Immutable-after-compilation effective style scope for one template
        /// element. Resource-owning descendants receive derived collections;
        /// elements without local resources share their parent's collections.
        /// </summary>
        private sealed class ItemTemplateStyleScope
        {
            public Dictionary<string, StyleDefinition> NamedStyles;
            public List<StyleDefinition> ImplicitStyles;
        }

        private sealed class RenderBindingSlot
        {
            public XmlElement SourceElement;
            public string ElementPath;
            public string AttributeName;
            public string Expression;
            public BindingExpressionPlan DirectPlan;
            public object DataContext;
            public object EventTarget;
            public ItemsControl Host;
            public BindingPathResult PathResult;
            public ObservableBindingRegistration ObservableRegistration;
            public bool ObservableDependencyKnown;
            public Control Target;
            public object LastValue;
            public bool LastByteImageFingerprintKnown;
            public ulong LastByteImageFingerprint;
            public bool ForceNextApply;
            public bool ReactiveDirty;
            public bool StyleSetter;
            public RestoreStyleValue PresetBaselineRestore;
            public RenderBindingSlotKind Kind;
            public bool AffectsLayout;
            public bool ComponentOwned;
        }

        private sealed class ReactiveItemUpdateBatch
        {
            public ItemsControl Host;
            public readonly ArrayList Slots = new ArrayList();
            public readonly Hashtable SlotSet = new Hashtable();
            public bool ReloadRequired;
            public bool RaiseRefreshCompleted;
        }

        private sealed class ItemPatchChange
        {
            public RenderBindingSlot Slot;
            public object OldValue;
            public object NewValue;
        }

        private sealed class ItemReactiveBindingChange
        {
            public RenderBindingSlot Slot;
            public object OldDataContext;
            public object NewDataContext;
            public ItemsControl OldHost;
            public ItemsControl NewHost;
            public BindingPathResult OldPathResult;
            public BindingPathResult NewPathResult;
            public bool OldSubscriptionActive;
            public bool OldReactiveDirty;
        }

        private sealed class ItemPatchPlan
        {
            public RenderedItemRecord Record;
            public object OldItem;
            public object NewItem;
            public Hashtable OldFunctionResults;
            public Hashtable FunctionResults;
            public ArrayList Changes;
            public ArrayList ReactiveChanges;
            public bool RequiresRebuild;
            public bool AffectsLayout;
            public bool AffectsInheritance;
            public bool Applied;
            public int AppliedChangeCount;
            public int AppliedReactiveChangeCount;
            public bool DataContextApplied;
            public bool RootVisibilityCaptured;
            public bool RootVisibilityApplied;
            public ItemVisibilityState OldRootVisibility;
            public bool OldRootConditionVisible;
        }

        private sealed class ObservedItemPatchPlan
        {
            public int Index;
            public RenderedItemRecord Record;
            public object Item;
            public string Key;
            public ItemPatchPlan Patch;
            public bool HasVersionValue;
            public object VersionValue;
        }


        private sealed class RenderedItemRecord
        {
            public ItemsControl Owner;
            public string Key;
            public object Item;
            public Hashtable FunctionResults;
            public ArrayList BindingSlots;
            public Control Control;
            public bool IntendedVisible;
            public ItemVisibilityState RootVisibility;
            public bool RootConditionVisible;
            public bool Reused;
            public int LogicalIndex;
            public int RealizationGeneration;

            // Optional cheap application-level change token from ItemVersionPath. When it
            // is unchanged, normal data bindings do not need to be re-evaluated; Function
            // bindings may still be refreshed independently.
            public bool HasVersionValue;
            public object VersionValue;

            // Preferred-size measurement is expensive for nested labels/panels.
            // Ordinary rendering scopes reuse to one native layout epoch so direct
            // Text/Font/tree mutations are observed on the next layout. Direct
            // virtualization keeps its cache across layouts until the proposed
            // viewport or an explicit layout-affecting binding changes.
            public bool MeasureCacheValid;
            public long MeasureCacheEpoch;
            public int MeasureProposedWidth;
            public int MeasureProposedHeight;
            public Size MeasureCachedSize;
        }

        private sealed class ItemsRefreshState
        {
            public ItemsControl Host;
            public int Generation;
            public bool ForceRebuild;
            public int PreviousScrollX;
            public int PreviousScrollY;
            public int RollbackScrollX;
            public int RollbackScrollY;
            public Size RollbackAutoScrollMinSize;
            public ArrayList OldRecords;
            public ArrayList NewRecords;
            public ArrayList BuildQueue;
            public int BuildIndex;
            public ArrayList PatchQueue;
            public int PatchIndex;
            public bool PatchLayoutDirty;
            public bool AnyVisualChange;
            public bool AnyLayoutChange;
            public Timer Timer;
            public Stopwatch ProgressiveBudget;
            public bool Committed;
        }

        private sealed class ItemsRefreshCommittedException : Exception
        {
            public ItemsRefreshCommittedException(Exception innerException)
                : base(
                    "The item refresh committed, but post-commit work failed.",
                    innerException)
            {
            }
        }

        private sealed class ItemsRefreshFailedException : Exception
        {
            public ItemsRefreshFailedException(Exception innerException)
                : base(
                    "The item refresh failure was already rolled back and reported.",
                    innerException)
            {
            }
        }

        private sealed class ItemsRefreshSupersededException : Exception
        {
            public ItemsRefreshSupersededException(Exception innerException)
                : base(
                    "The item refresh was superseded by reentrant work.",
                    innerException)
            {
            }
        }

        private bool TryBeginDirectItemsRefresh(
            ItemsControl host,
            bool forceRebuild)
        {
            host.SetRefreshing(true, null);

            try
            {
                bool activated =
                    ResetDirectViewportVirtualization(
                        host,
                        forceRebuild,
                        true);

                if (!activated)
                {
                    host.SetRefreshing(false, null);
                    return false;
                }
            }
            catch (ItemsRefreshCommittedException ex)
            {
                Exception committedError = ex.InnerException == null
                    ? ex
                    : ex.InnerException;

                committedError = CompleteDirectItemsRefresh(
                    host,
                    committedError);

                throw new ItemsRefreshCommittedException(
                    committedError);
            }
            catch (Exception ex)
            {
                RestoreCommittedItemsSource(host);
                host.SetRefreshing(false, ex);

                Exception failure = ex;

                try
                {
                    host.RaiseRefreshFailed();
                }
                catch (Exception eventError)
                {
                    failure = FirstItemsCommitError(
                        failure,
                        eventError);
                    host.SetRefreshing(false, failure);
                }

                throw new ItemsRefreshFailedException(failure);
            }

            Exception completionError =
                CompleteDirectItemsRefresh(host, null);

            if (completionError != null)
            {
                throw new ItemsRefreshCommittedException(
                    completionError);
            }

            return true;
        }

        private Exception CompleteDirectItemsRefresh(
            ItemsControl host,
            Exception currentError)
        {
            try
            {
                CommitItemsSource(host);
            }
            catch (Exception ex)
            {
                currentError = FirstItemsCommitError(
                    currentError,
                    ex);
            }

            host.SetRefreshing(false, null);

            try
            {
                RefreshItemsControlPresetIndex(host, true);
            }
            catch (Exception ex)
            {
                currentError = FirstItemsCommitError(
                    currentError,
                    ex);
            }

            try
            {
                host.RaiseRefreshCompleted();
            }
            catch (Exception ex)
            {
                currentError = FirstItemsCommitError(
                    currentError,
                    ex);
            }

            return currentError;
        }

        /// <summary>
        /// Starts a keyed incremental refresh. Unchanged controls are reused. Only
        /// changed/new item templates are rebuilt, and those builds can be split
        /// across timer ticks. The currently visible tree stays intact until every
        /// replacement needed for the final commit has been prepared.
        /// </summary>
        private void BeginItemsRefresh(
            ItemsControl host,
            bool forceRebuild)
        {
            if (host == null)
                return;

            if (!CancelItemsRefresh(host, false))
                return;

            if (host.ItemValues != null &&
                host.ItemValues.Count > 0 &&
                host.TemplateRoot == null)
            {
                throw new InvalidOperationException(
                    "ItemsControl has items but no ItemsControl.ItemTemplate.");
            }

            if (TryBeginLightweightItemsRefresh(host, forceRebuild))
                return;

            if (TryBeginDirectItemsRefresh(host, forceRebuild))
                return;

            int logicalScroll = host.GetLogicalScrollOffset();
            ArrayList requestedValues = host.ItemValues;

            ItemsRefreshState state =
                new ItemsRefreshState();

            state.Host = host;
            state.Generation = ++host.RefreshGeneration;
            state.ForceRebuild = forceRebuild;
            state.PreviousScrollX =
                host.Orientation == Orientation.Horizontal
                    ? logicalScroll
                    : 0;
            state.PreviousScrollY =
                host.Orientation == Orientation.Vertical
                    ? logicalScroll
                    : 0;
            state.RollbackScrollX = state.PreviousScrollX;
            state.RollbackScrollY = state.PreviousScrollY;
            state.RollbackAutoScrollMinSize = host.AutoScrollMinSize;
            state.OldRecords = CloneArrayList(host.RenderedItems);
            state.NewRecords = new ArrayList(
                requestedValues == null
                    ? 0
                    : requestedValues.Count);
            state.BuildQueue = new ArrayList();
            state.BuildIndex = 0;
            state.PatchQueue = new ArrayList();
            state.PatchIndex = 0;
            state.PatchLayoutDirty = false;
            state.AnyVisualChange = false;
            state.AnyLayoutChange = false;

            host.PendingRefresh = state;
            host.SetRefreshing(true, null);

            try
            {

                Hashtable oldBuckets =
                    BuildOldItemBuckets(state.OldRecords);

                int i;

                for (i = 0;
                     requestedValues != null &&
                     i < requestedValues.Count &&
                     IsItemsRefreshCurrent(state);
                     i++)
                {
                    object item = requestedValues[i];
                    string key = GetStableItemKey(host, item, i);

                    if (!IsItemsRefreshCurrent(state))
                        return;

                    RenderedItemRecord oldRecord =
                        TakeOldItemRecord(oldBuckets, key);

                    RenderedItemRecord record =
                        new RenderedItemRecord();

                    record.Owner = host;
                    record.Key = key;
                    record.Item = item;
                    record.FunctionResults = null;
                    record.BindingSlots = null;
                    record.Control = null;
                    record.IntendedVisible = true;
                    record.RootVisibility = ItemVisibilityState.Visible;
                    record.RootConditionVisible = true;
                    record.Reused = false;
                    // Normal records retain their logical data index just like
                    // direct-viewport records. This makes reactive source and
                    // binding updates independent of the RenderedItems order.
                    record.LogicalIndex = i;
                    record.RealizationGeneration = 0;
                    record.MeasureCacheValid = false;
                    record.MeasureProposedWidth = 0;
                    record.MeasureProposedHeight = 0;
                    record.MeasureCachedSize = Size.Empty;

                    bool hasVersionValue;
                    object versionValue = GetItemVersionValue(
                        host,
                        item,
                        out hasVersionValue);

                    if (!IsItemsRefreshCurrent(state))
                        return;

                    record.HasVersionValue = hasVersionValue;
                    record.VersionValue = versionValue;

                    bool versionUnchanged =
                        hasVersionValue &&
                        oldRecord != null &&
                        oldRecord.HasVersionValue &&
                        AreFunctionResultsEquivalent(
                            oldRecord.VersionValue,
                            versionValue);

                    bool normalDataKnownUnchanged =
                        versionUnchanged;

                    if (!forceRebuild &&
                        host.ReuseItems &&
                        oldRecord != null &&
                        oldRecord.Control != null &&
                        !oldRecord.Control.IsDisposed &&
                        oldRecord.BindingSlots != null)
                    {
                        // ItemVersionPath is an explicit application promise that the normal
                        // rendered data did not change while this token is equal. If Function
                        // refresh is disabled too, no binding evaluation is needed at all.
                        if (normalDataKnownUnchanged &&
                            !host.ReevaluateFunctionsOnRefresh &&
                            !RenderedItemRecordRequiresReactiveValidation(
                                oldRecord,
                                item))
                        {
                            record.Control = oldRecord.Control;
                            record.BindingSlots = oldRecord.BindingSlots;
                            record.FunctionResults = oldRecord.FunctionResults;
                            record.IntendedVisible = oldRecord.IntendedVisible;
                            record.RootVisibility = oldRecord.RootVisibility;
                            record.RootConditionVisible =
                                oldRecord.RootConditionVisible;
                            record.Reused = true;
                            record.MeasureCacheValid = oldRecord.MeasureCacheValid;
                            record.MeasureCacheEpoch = oldRecord.MeasureCacheEpoch;
                            record.MeasureProposedWidth = oldRecord.MeasureProposedWidth;
                            record.MeasureProposedHeight = oldRecord.MeasureProposedHeight;
                            record.MeasureCachedSize = oldRecord.MeasureCachedSize;

                            if (!Object.ReferenceEquals(
                                    oldRecord.Item,
                                    item))
                            {
                                state.PatchQueue.Add(
                                    CreateDataContextPatchPlan(
                                        oldRecord,
                                        record,
                                        item));
                            }

                            state.NewRecords.Add(record);
                            continue;
                        }

                        ItemPatchPlan patch =
                            CreateItemPatchPlan(
                                host,
                                oldRecord,
                                item,
                                normalDataKnownUnchanged);

                        if (!IsItemsRefreshCurrent(state))
                            return;

                        record.FunctionResults =
                            patch.FunctionResults;

                        if (!patch.RequiresRebuild)
                        {
                            record.Control = oldRecord.Control;
                            record.BindingSlots = oldRecord.BindingSlots;
                            record.IntendedVisible = oldRecord.IntendedVisible;
                            record.RootVisibility = oldRecord.RootVisibility;
                            record.RootConditionVisible =
                                oldRecord.RootConditionVisible;
                            record.Reused = true;
                            record.MeasureCacheValid = oldRecord.MeasureCacheValid;
                            record.MeasureCacheEpoch = oldRecord.MeasureCacheEpoch;
                            record.MeasureProposedWidth = oldRecord.MeasureProposedWidth;
                            record.MeasureProposedHeight = oldRecord.MeasureProposedHeight;
                            record.MeasureCachedSize = oldRecord.MeasureCachedSize;

                            patch.Record = record;

                            if (patch.Changes.Count > 0 ||
                                patch.ReactiveChanges.Count > 0 ||
                                !Object.ReferenceEquals(
                                    oldRecord.Item,
                                    item))
                            {
                                // Keep the currently-visible item's DataContext stable until
                                // its progressive patch is actually applied.
                                state.PatchQueue.Add(patch);
                            }
                        }
                        else
                        {
                            state.BuildQueue.Add(record);
                        }
                    }
                    else
                    {
                        state.BuildQueue.Add(record);
                    }

                    if (record.FunctionResults == null)
                        record.FunctionResults = new Hashtable();

                    state.NewRecords.Add(record);
                }

                if (!IsItemsRefreshCurrent(state))
                    return;

                StartItemsRefreshWork(state);
            }
            catch (Exception ex)
            {
                if (ex is ItemsRefreshCommittedException ||
                    ex is ItemsRefreshFailedException)
                {
                    throw;
                }

                if (IsItemsRefreshCurrent(state))
                {
                    FailItemsRefresh(state, ex, true);
                    throw new ItemsRefreshFailedException(ex);
                }

                throw new ItemsRefreshSupersededException(ex);
            }
        }
    }
}
