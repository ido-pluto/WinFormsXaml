using System;
using System.Reflection;

namespace WinFormsXaml
{
    // Pure-model regressions for the direct logical viewport.
    internal static class VirtualViewportModelFocusedTests
    {
        private delegate void TestAction();

        internal static void Run()
        {
            TestUniformFastPath();
            TestGeneralOffsetsAndZeroExtents();
            TestGeneralRepeatedExtentConstruction();
            TestSparseItemVersionSnapshots();
            TestGeneralUpdates();
            TestValidationAndOverflow();
        }

        private static void TestUniformFastPath()
        {
            VirtualViewportModel model =
                new VirtualViewportModel(1000000, 32);

            AssertEqual(true, model.Uniform, "uniform mode");
            AssertEqual(1000000, model.Count, "uniform count");
            AssertEqual(32000000L, model.TotalExtent, "uniform total");
            AssertEqual(0L, model.GetOffset(0), "uniform first offset");
            AssertEqual(16000000L, model.GetOffset(500000), "uniform middle offset");
            AssertEqual(32000000L, model.GetOffset(model.Count), "uniform end offset");
            AssertEqual(32L, model.GetExtent(999999), "uniform extent");
            AssertEqual(0, model.FindIndexAtOffset(0), "uniform first index");
            AssertEqual(0, model.FindIndexAtOffset(31), "uniform first boundary");
            AssertEqual(1, model.FindIndexAtOffset(32), "uniform second index");
            AssertEqual(
                999999,
                model.FindIndexAtOffset(model.TotalExtent - 1),
                "uniform last index");

            FieldInfo extents = typeof(VirtualViewportModel).GetField(
                "_extents",
                BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo tree = typeof(VirtualViewportModel).GetField(
                "_tree",
                BindingFlags.Instance | BindingFlags.NonPublic);

            AssertEqual(null, extents.GetValue(model), "uniform stores no extent array");
            AssertEqual(null, tree.GetValue(model), "uniform stores no Fenwick array");
            ExpectException(
                typeof(InvalidOperationException),
                delegate { model.SetExtent(0, 16); },
                "uniform updates are rejected");

            VirtualViewportModel empty = new VirtualViewportModel(0, 12);
            AssertEqual(0L, empty.TotalExtent, "empty uniform total");
            AssertEqual(-1, empty.FindIndexAtOffset(0), "empty uniform lookup");
        }

        private static void TestGeneralOffsetsAndZeroExtents()
        {
            VirtualViewportModel model = new VirtualViewportModel(
                new long[] { 0, 5, 0, 3, 2, 0 });

            AssertEqual(false, model.Uniform, "general mode");
            AssertEqual(6, model.Count, "general count");
            AssertEqual(10L, model.TotalExtent, "general total");
            AssertEqual(0L, model.GetOffset(0), "general offset zero");
            AssertEqual(0L, model.GetOffset(1), "leading zero offset");
            AssertEqual(5L, model.GetOffset(2), "offset after first item");
            AssertEqual(5L, model.GetOffset(3), "middle zero offset");
            AssertEqual(8L, model.GetOffset(4), "offset after third item");
            AssertEqual(10L, model.GetOffset(6), "general end offset");
            AssertEqual(1, model.FindIndexAtOffset(0), "skip leading zero");
            AssertEqual(1, model.FindIndexAtOffset(4), "first included range");
            AssertEqual(3, model.FindIndexAtOffset(5), "skip boundary zero");
            AssertEqual(3, model.FindIndexAtOffset(7), "middle included range");
            AssertEqual(4, model.FindIndexAtOffset(8), "last included range");
            AssertEqual(4, model.FindIndexAtOffset(9), "last extent boundary");

            VirtualViewportModel zeros =
                new VirtualViewportModel(new long[] { 0, 0, 0 });
            AssertEqual(-1, zeros.FindIndexAtOffset(0), "all-zero lookup");
        }

        private static void TestGeneralUpdates()
        {
            VirtualViewportModel model = new VirtualViewportModel(
                new long[] { 0, 5, 0, 3, 2, 0 });

            model.SetExtent(2, 4);
            AssertEqual(14L, model.TotalExtent, "point growth updates total");
            AssertEqual(5L, model.GetOffset(2), "point growth keeps prefix");
            AssertEqual(9L, model.GetOffset(3), "point growth updates suffix");
            AssertEqual(2, model.FindIndexAtOffset(5), "new extent is included");
            AssertEqual(2, model.FindIndexAtOffset(8), "new extent range end");

            model.SetExtent(1, 0);
            AssertEqual(9L, model.TotalExtent, "point removal updates total");
            AssertEqual(2, model.FindIndexAtOffset(0), "removed extent is skipped");
            AssertEqual(4L, model.GetOffset(3), "point removal updates suffix");

            model.SetExtent(1, 0);
            AssertEqual(9L, model.TotalExtent, "equal update is a no-op");
        }

        private static void TestSparseItemVersionSnapshots()
        {
            VirtualViewportModel model =
                new VirtualViewportModel(1000000, 20);
            object value;
            bool hasValue;

            AssertEqual(
                false,
                model.TryGetItemVersion(999999, out value, out hasValue),
                "version storage starts disabled");

            model.InitializeItemVersionSnapshot();

            AssertEqual(
                false,
                model.TryGetItemVersion(999999, out value, out hasValue),
                "uncaptured far version has no sparse entry");

            model.SetItemVersion(999999, "v1", true);
            AssertEqual(
                true,
                model.TryGetItemVersion(999999, out value, out hasValue),
                "captured sparse version exists");
            AssertEqual("v1", value, "captured sparse version value");
            AssertEqual(true, hasValue, "captured sparse version flag");

            model.SetItemVersion(5, null, false);
            AssertEqual(
                true,
                model.TryGetItemVersion(5, out value, out hasValue),
                "captured missing version remains distinguishable");
            AssertEqual(null, value, "captured missing version value");
            AssertEqual(false, hasValue, "captured missing version flag");
        }

        private static void TestGeneralRepeatedExtentConstruction()
        {
            VirtualViewportModel model =
                new VirtualViewportModel(4, 12, 9);

            AssertEqual(false, model.Uniform, "repeated general mode");
            AssertEqual(4, model.Count, "repeated general count");
            AssertEqual(45L, model.TotalExtent, "repeated general total");
            AssertEqual(12L, model.GetExtent(0), "repeated first extent");
            AssertEqual(12L, model.GetExtent(2), "repeated middle extent");
            AssertEqual(9L, model.GetExtent(3), "repeated trailing extent");
            AssertEqual(24L, model.GetOffset(2), "repeated middle offset");
            AssertEqual(36L, model.GetOffset(3), "repeated trailing offset");
            AssertEqual(3, model.FindIndexAtOffset(44), "repeated final index");

            model.SetExtent(1, 6);
            AssertEqual(39L, model.TotalExtent, "repeated model remains mutable");

            VirtualViewportModel empty =
                new VirtualViewportModel(0, 12, 9);
            AssertEqual(0L, empty.TotalExtent, "empty repeated total");
            AssertEqual(-1, empty.FindIndexAtOffset(0), "empty repeated lookup");
        }

        private static void TestValidationAndOverflow()
        {
            ExpectException(
                typeof(ArgumentOutOfRangeException),
                delegate { new VirtualViewportModel(-1, 1); },
                "negative uniform count");
            ExpectException(
                typeof(ArgumentOutOfRangeException),
                delegate { new VirtualViewportModel(1, -1); },
                "negative uniform extent");
            ExpectException(
                typeof(OverflowException),
                delegate { new VirtualViewportModel(2, Int64.MaxValue); },
                "uniform total overflow");
            ExpectException(
                typeof(ArgumentOutOfRangeException),
                delegate { new VirtualViewportModel(-1, 1, 1); },
                "negative repeated count");
            ExpectException(
                typeof(ArgumentOutOfRangeException),
                delegate { new VirtualViewportModel(1, -1, 1); },
                "negative repeated extent");
            ExpectException(
                typeof(ArgumentOutOfRangeException),
                delegate { new VirtualViewportModel(1, 1, -1); },
                "negative repeated trailing extent");
            ExpectException(
                typeof(OverflowException),
                delegate
                {
                    new VirtualViewportModel(
                        3,
                        Int64.MaxValue,
                        0);
                },
                "repeated extent multiplication overflow");
            ExpectException(
                typeof(ArgumentNullException),
                delegate { new VirtualViewportModel((long[])null); },
                "null general extents");
            ExpectException(
                typeof(ArgumentOutOfRangeException),
                delegate { new VirtualViewportModel(new long[] { 1, -1 }); },
                "negative general extent");
            ExpectException(
                typeof(OverflowException),
                delegate
                {
                    new VirtualViewportModel(
                        new long[] { Int64.MaxValue, 1 });
                },
                "general total overflow");

            VirtualViewportModel model =
                new VirtualViewportModel(new long[] { Int64.MaxValue - 1, 0 });
            ExpectException(
                typeof(OverflowException),
                delegate { model.SetExtent(1, 2); },
                "point update overflow");
            AssertEqual(
                Int64.MaxValue - 1,
                model.TotalExtent,
                "failed point update is transactional");
            AssertEqual(0L, model.GetExtent(1), "failed update retains extent");
            ExpectException(
                typeof(ArgumentOutOfRangeException),
                delegate { model.SetExtent(1, -1); },
                "negative point update");
            ExpectException(
                typeof(ArgumentOutOfRangeException),
                delegate { model.GetExtent(2); },
                "extent index validation");
            ExpectException(
                typeof(ArgumentOutOfRangeException),
                delegate { model.GetOffset(3); },
                "offset index validation");
            ExpectException(
                typeof(ArgumentOutOfRangeException),
                delegate { model.FindIndexAtOffset(-1); },
                "negative offset validation");
            ExpectException(
                typeof(ArgumentOutOfRangeException),
                delegate { model.FindIndexAtOffset(model.TotalExtent); },
                "end offset is outside half-open content");
        }

        private static void ExpectException(
            Type expectedType,
            TestAction action,
            string message)
        {
            Exception failure = null;

            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }

            if (failure == null || !expectedType.IsInstanceOfType(failure))
            {
                throw new InvalidOperationException(
                    message + ": expected " + expectedType.FullName +
                    ", actual " +
                    (failure == null
                        ? "no exception"
                        : failure.GetType().FullName) + ".");
            }
        }

        private static void AssertEqual(
            object expected,
            object actual,
            string message)
        {
            if (!Object.Equals(expected, actual))
            {
                throw new InvalidOperationException(
                    message + ": expected '" + expected +
                    "', actual '" + actual + "'.");
            }
        }
    }
}
