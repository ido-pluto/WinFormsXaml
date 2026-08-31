using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Xml;
using System.Windows.Forms;

namespace WinFormsXaml
{
    public sealed partial class XamlRuntime
    {
        internal enum LightweightNodeKind
        {
            Border,
            StackPanel,
            Label,
            CheckBox,
            HyperlinkLabel,
            Image
        }

        internal sealed class LightweightValueSlot
        {
            internal int Id;
            internal XmlElement Element;
            internal string PropertyName;
            internal string Expression;
            internal object Literal;
            internal bool Dynamic;
            internal object BindingPlan;
        }

        internal sealed class LightweightTemplateNode
        {
            internal int Id;
            internal int LinkId = -1;
            internal LightweightNodeKind Kind;
            internal XmlElement SourceElement;
            internal readonly ArrayList Children = new ArrayList();
            internal Padding Margin;
            internal Padding Padding;
            internal Padding BorderThickness;
            internal Orientation Orientation;
            internal int Spacing;
            internal int Width = -1;
            internal int Height = -1;
            internal string FontFamily;
            internal float FontSizeInPoints = -1.0f;
            internal FontStyle FontStyle;
            internal bool FontStyleSpecified;
            internal ContentAlignment TextAlign = ContentAlignment.MiddleLeft;
            internal ContentAlignment CheckAlign = ContentAlignment.MiddleLeft;
            internal bool AutoEllipsis;
            internal LightweightValueSlot Text;
            internal LightweightValueSlot ForeColor;
            internal LightweightValueSlot BackColor;
            internal LightweightValueSlot BorderColor;
            internal LightweightValueSlot Checked;
            internal LightweightValueSlot Enabled;
            internal LightweightValueSlot NavigateUri;
            internal LightweightValueSlot LinkColor;
            internal LightweightValueSlot VisitedLinkColor;
            internal LightweightValueSlot Source;
            internal ImageStretch Stretch = ImageStretch.Uniform;
            internal Font CachedFont;
            internal Font CachedBaseFont;

            internal void Dispose()
            {
                if (CachedFont != null)
                {
                    CachedFont.Dispose();
                    CachedFont = null;
                    CachedBaseFont = null;
                }

                int i;

                for (i = 0; i < Children.Count; i++)
                {
                    LightweightTemplateNode child =
                        Children[i] as LightweightTemplateNode;

                    if (child != null)
                        child.Dispose();
                }

                Children.Clear();
            }
        }

        internal sealed class LightweightTemplatePlan
        {
            internal LightweightTemplateNode Root;
            internal string TemplateXml;
            internal int NextNodeId;
            internal int NextLinkId;
            internal int NextValueSlotId;

            internal void Dispose()
            {
                if (Root != null)
                    Root.Dispose();

                Root = null;
                TemplateXml = null;
            }
        }

        private sealed class LightweightRowSnapshot
        {
            internal ItemsControl Host;
            internal int Generation;
            internal int Index;
            internal object Item;
            internal string StableItemKey;
            internal bool Prepared;
            internal bool Retired;
            internal readonly object[] Values;
            internal readonly object[] ConvertedValues;
            internal readonly object[] TextValues;
            internal readonly Hashtable FunctionResults = new Hashtable();
            internal readonly object[] Images;
            internal readonly Image[] ThumbnailSources;
            internal readonly LightweightVisitedLinkKey[] LinkKeys;

            internal LightweightRowSnapshot(
                int valueSlotCount,
                int nodeCount,
                int linkCount)
            {
                Values = new object[valueSlotCount];
                ConvertedValues = new object[valueSlotCount];
                TextValues = new object[valueSlotCount];
                Images = new object[nodeCount];
                ThumbnailSources = new Image[nodeCount];
                LinkKeys = new LightweightVisitedLinkKey[linkCount];
            }
        }

        internal sealed class LightweightHitTarget
        {
            internal int Index;
            internal LightweightTemplateNode Node;
            internal Rectangle Bounds;
        }

        private sealed class LightweightVisitedLinkKey
        {
            private readonly string _itemKey;
            private readonly int _nodeId;
            private readonly string _destination;
            private readonly int _hashCode;

            internal LightweightVisitedLinkKey(
                string itemKey,
                int nodeId,
                string destination)
            {
                _itemKey = itemKey;
                _nodeId = nodeId;
                _destination = destination;
                _hashCode =
                    (itemKey == null ? 0 : itemKey.GetHashCode()) ^
                    nodeId ^
                    (destination == null ? 0 : destination.GetHashCode());
            }

            public override bool Equals(object value)
            {
                LightweightVisitedLinkKey other =
                    value as LightweightVisitedLinkKey;

                return other != null &&
                    _nodeId == other._nodeId &&
                    String.Equals(
                        _itemKey,
                        other._itemKey,
                        StringComparison.Ordinal) &&
                    String.Equals(
                        _destination,
                        other._destination,
                        StringComparison.Ordinal);
            }

            public override int GetHashCode()
            {
                return _hashCode;
            }
        }

        internal const int LightweightVisitedLinkLimit = 256;
        private static readonly object LightweightCachedNullValue =
            new object();

        private static BindingExpressionPlan GetLightweightBindingPlan(
            LightweightValueSlot slot)
        {
            return slot == null
                ? null
                : slot.BindingPlan as BindingExpressionPlan;
        }

        internal void ValidateLightweightItemsControlConfiguration(
            ItemsControl host)
        {
            if (host == null)
                throw new ArgumentNullException("host");

            if (host.VirtualizationMode !=
                ItemsControlVirtualizationMode.Lightweight)
            {
                DisposeLightweightTemplatePlan(host);
                return;
            }

            if (host.TemplateRoot == null)
                return;

            LightweightTemplatePlan candidate =
                CompileLightweightTemplate(host);
            LightweightTemplatePlan previous = host.LightweightPlan;

            try
            {
                ClearLightweightRowCache(host);
                ClearLightweightVisitedLinks(host, false);
                DisposeLightweightBrushCache(host);
            }
            catch
            {
                candidate.Dispose();
                throw;
            }

            host.LightweightPlan = candidate;
            host.LightweightGeneration = NextLightweightGeneration(
                host.LightweightGeneration);

            if (previous != null)
                previous.Dispose();
        }

        internal void ValidateLightweightItemsControlEligibility(
            ItemsControl host)
        {
            ValidateLightweightItemsControlEligibility(host, null);
        }

        internal void ValidateLightweightItemsControlEligibility(
            ItemsControl host,
            XmlElement declarationElement)
        {
            if (host == null)
                throw new ArgumentNullException("host");

            if (host.VirtualizationMode ==
                ItemsControlVirtualizationMode.Lightweight)
            {
                EnsureLightweightHostIsEligible(
                    host,
                    declarationElement);
            }
        }

    }
}
