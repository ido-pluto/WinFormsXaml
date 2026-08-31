using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace WinFormsXaml
{
    /// <summary>
    /// Plans bounded, deterministic edits without touching the live list.
    /// </summary>
    internal static class ItemsBindingDiff<T>
    {
        private const int MaximumOperations = 64;
        private static readonly bool MatchByValue = typeof(T).IsValueType;
        private static readonly bool CanBeNull =
            !typeof(T).IsValueType ||
            Nullable.GetUnderlyingType(typeof(T)) != null;

        internal enum OperationType
        {
            Insert,
            Remove,
            Replace,
            Move
        }

        internal struct Operation
        {
            private readonly OperationType _type;
            private readonly int _index;
            private readonly int _oldIndex;
            private readonly T _value;

            internal Operation(
                OperationType type,
                int index,
                int oldIndex,
                T value)
            {
                _type = type;
                _index = index;
                _oldIndex = oldIndex;
                _value = value;
            }

            internal OperationType Type
            {
                get { return _type; }
            }

            internal int Index
            {
                get { return _index; }
            }

            internal int OldIndex
            {
                get { return _oldIndex; }
            }

            internal T Value
            {
                get { return _value; }
            }
        }

        private sealed class ItemComparer : IEqualityComparer<T>
        {
            public bool Equals(T left, T right)
            {
                return AreSame(left, right);
            }

            public int GetHashCode(T value)
            {
                if (MatchByValue)
                    return EqualityComparer<T>.Default.GetHashCode(value);

                return RuntimeHelpers.GetHashCode((object)value);
            }
        }

        private static readonly IEqualityComparer<T> ItemEqualityComparer =
            new ItemComparer();

        internal static bool TryPlan(
            IList<T> current,
            IList<T> replacement,
            out List<Operation> operations)
        {
            operations = new List<Operation>();

            if (SequencesMatch(current, replacement))
                return true;

            int[] replacementToOld;
            bool[] matchedOld;
            MatchOccurrences(
                current,
                replacement,
                out replacementToOld,
                out matchedOld);

            // An unmatched old occurrence and an unmatched replacement
            // occurrence can share one ItemChanged operation. Pair them in
            // source/target order so the pairing itself never reverses their
            // relative order. The later LIS pass is then free to retain the
            // largest possible ordered subset of the selected occurrences.
            List<int> unmatchedOld = new List<int>();
            List<int> unmatchedReplacement = new List<int>();
            int i;

            for (i = 0; i < matchedOld.Length; i++)
            {
                if (!matchedOld[i])
                    unmatchedOld.Add(i);
            }

            for (i = 0; i < replacementToOld.Length; i++)
            {
                if (replacementToOld[i] < 0)
                    unmatchedReplacement.Add(i);
            }

            int replacementPairCount = Math.Min(
                unmatchedOld.Count,
                unmatchedReplacement.Count);

            for (i = 0; i < replacementPairCount; i++)
            {
                int oldIndex = unmatchedOld[i];
                int replacementIndex = unmatchedReplacement[i];

                if (!Add(
                        operations,
                        OperationType.Replace,
                        oldIndex,
                        -1,
                        replacement[replacementIndex]))
                {
                    operations = null;
                    return false;
                }

                replacementToOld[replacementIndex] = oldIndex;
                matchedOld[oldIndex] = true;
            }

            List<int> currentTokens = new List<int>(current.Count);

            for (i = 0; i < current.Count; i++)
                currentTokens.Add(i);

            // Remove surplus old occurrences from the end. Descending indices
            // keep every published ItemDeleted index valid without touching
            // the live list while the plan is being built.
            for (i = currentTokens.Count - 1; i >= 0; i--)
            {
                int token = currentTokens[i];

                if (!matchedOld[token])
                {
                    if (!Add(
                            operations,
                            OperationType.Remove,
                            i,
                            -1,
                            default(T)))
                    {
                        operations = null;
                        return false;
                    }

                    currentTokens.RemoveAt(i);
                }
            }

            bool[] retainedPositions =
                FindLongestIncreasingSubsequencePositions(
                    replacementToOld);
            int[] tokenPositions = new int[current.Count];
            RebuildTokenPositions(currentTokens, tokenPositions);
            int anchorIndex = currentTokens.Count;

            // Work backwards, inserting/moving each non-retained token before
            // the already-correct suffix. The retained positions are an LIS of
            // old occurrence indices, so n-LIS is the minimum number of moves
            // for the selected identity tokens. In particular, either rotation
            // direction is one move rather than a prefix-sized series of moves.
            for (i = replacement.Count - 1; i >= 0; i--)
            {
                int desiredToken = replacementToOld[i];

                if (desiredToken < 0)
                {
                    if (!Add(
                            operations,
                            OperationType.Insert,
                            anchorIndex,
                            -1,
                            replacement[i]))
                    {
                        operations = null;
                        return false;
                    }

                    currentTokens.Insert(
                        anchorIndex,
                        EncodeReplacementToken(i));
                    RebuildTokenPositions(
                        currentTokens,
                        tokenPositions);
                    continue;
                }

                int currentIndex = tokenPositions[desiredToken];

                if (currentIndex < 0)
                {
                    operations = null;
                    return false;
                }

                if (retainedPositions[i])
                {
                    anchorIndex = currentIndex;
                    continue;
                }

                int newIndex = anchorIndex;

                if (currentIndex < newIndex)
                    newIndex--;

                if (currentIndex != newIndex)
                {
                    if (!Add(
                            operations,
                            OperationType.Move,
                            newIndex,
                            currentIndex,
                            default(T)))
                    {
                        operations = null;
                        return false;
                    }

                    currentTokens.RemoveAt(currentIndex);
                    currentTokens.Insert(newIndex, desiredToken);
                    RebuildTokenPositions(
                        currentTokens,
                        tokenPositions);
                }

                anchorIndex = newIndex;
            }

            return TokensMatchReplacement(
                currentTokens,
                replacementToOld);
        }

        private static void MatchOccurrences(
            IList<T> current,
            IList<T> replacement,
            out int[] replacementToOld,
            out bool[] matchedOld)
        {
            if (MatchByValue)
            {
                MatchValueOccurrences(
                    current,
                    replacement,
                    out replacementToOld,
                    out matchedOld);
                return;
            }

            Dictionary<T, int> oldIndexHeads =
                new Dictionary<T, int>(ItemEqualityComparer);
            int nullOldIndexHead = -1;
            int[] nextOldIndices = new int[current.Count];
            int i;

            // Reference items use runtime identity, so no application equality
            // or hashing code can run here. Build each occurrence chain
            // backwards. The encoded next index is one-based, leaving zero as
            // the allocation-free end marker. This retains ascending source
            // order without allocating one Queue and backing array for every
            // distinct item identity.
            for (i = current.Count - 1; i >= 0; i--)
            {
                T key = current[i];

                if (IsNull(key))
                {
                    nextOldIndices[i] = nullOldIndexHead + 1;
                    nullOldIndexHead = i;
                }
                else
                {
                    int head;

                    if (oldIndexHeads.TryGetValue(key, out head))
                    {
                        nextOldIndices[i] = head + 1;
                        oldIndexHeads[key] = i;
                    }
                    else
                    {
                        oldIndexHeads.Add(key, i);
                    }
                }
            }

            replacementToOld = new int[replacement.Count];
            matchedOld = new bool[current.Count];

            for (i = 0; i < replacement.Count; i++)
            {
                T key = replacement[i];
                int oldIndex = -1;

                if (IsNull(key))
                {
                    oldIndex = nullOldIndexHead;

                    if (oldIndex >= 0)
                    {
                        nullOldIndexHead =
                            nextOldIndices[oldIndex] - 1;
                    }
                }
                else
                {
                    if (oldIndexHeads.TryGetValue(key, out oldIndex) &&
                        oldIndex >= 0)
                    {
                        oldIndexHeads[key] =
                            nextOldIndices[oldIndex] - 1;
                    }
                    else
                    {
                        oldIndex = -1;
                    }
                }

                if (oldIndex >= 0)
                {
                    replacementToOld[i] = oldIndex;
                    matchedOld[oldIndex] = true;
                }
                else
                {
                    replacementToOld[i] = -1;
                }
            }
        }

        private static void MatchValueOccurrences(
            IList<T> current,
            IList<T> replacement,
            out int[] replacementToOld,
            out bool[] matchedOld)
        {
            Dictionary<T, Queue<int>> oldIndices =
                new Dictionary<T, Queue<int>>(ItemEqualityComparer);
            Queue<int> nullOldIndices = null;
            int i;

            // Preserve the established forward comparison and hashing order
            // for value types, whose custom equality can execute application
            // code and start a newer Replace request.
            for (i = 0; i < current.Count; i++)
            {
                T key = current[i];
                Queue<int> indices;

                if (IsNull(key))
                {
                    if (nullOldIndices == null)
                        nullOldIndices = new Queue<int>();

                    indices = nullOldIndices;
                }
                else if (!oldIndices.TryGetValue(key, out indices))
                {
                    indices = new Queue<int>();
                    oldIndices.Add(key, indices);
                }

                indices.Enqueue(i);
            }

            replacementToOld = new int[replacement.Count];
            matchedOld = new bool[current.Count];

            for (i = 0; i < replacement.Count; i++)
            {
                T key = replacement[i];
                Queue<int> indices = null;

                if (IsNull(key))
                    indices = nullOldIndices;
                else
                    oldIndices.TryGetValue(key, out indices);

                if (indices != null && indices.Count > 0)
                {
                    int oldIndex = indices.Dequeue();
                    replacementToOld[i] = oldIndex;
                    matchedOld[oldIndex] = true;
                }
                else
                {
                    replacementToOld[i] = -1;
                }
            }
        }

        private static bool Add(
            IList<Operation> operations,
            OperationType type,
            int index,
            int oldIndex,
            T value)
        {
            if (operations.Count >= MaximumOperations)
                return false;

            operations.Add(
                new Operation(type, index, oldIndex, value));
            return true;
        }

        private static bool[] FindLongestIncreasingSubsequencePositions(
            int[] tokens)
        {
            bool[] retained = new bool[tokens.Length];
            int[] tails = new int[tokens.Length];
            int[] predecessors = new int[tokens.Length];
            int length = 0;
            int i;

            for (i = 0; i < predecessors.Length; i++)
                predecessors[i] = -1;

            for (i = 0; i < tokens.Length; i++)
            {
                if (tokens[i] < 0)
                    continue;

                int low = 0;
                int high = length;

                while (low < high)
                {
                    int middle = low + ((high - low) / 2);

                    if (tokens[tails[middle]] < tokens[i])
                        low = middle + 1;
                    else
                        high = middle;
                }

                if (low > 0)
                    predecessors[i] = tails[low - 1];

                tails[low] = i;

                if (low == length)
                    length++;
            }

            if (length == 0)
                return retained;

            int cursor = tails[length - 1];

            while (cursor >= 0)
            {
                retained[cursor] = true;
                cursor = predecessors[cursor];
            }

            return retained;
        }

        private static int EncodeReplacementToken(int replacementIndex)
        {
            return -replacementIndex - 1;
        }

        private static void RebuildTokenPositions(
            IList<int> tokens,
            int[] positions)
        {
            int i;

            for (i = 0; i < positions.Length; i++)
                positions[i] = -1;

            for (i = 0; i < tokens.Count; i++)
            {
                if (tokens[i] >= 0)
                    positions[tokens[i]] = i;
            }
        }

        private static bool TokensMatchReplacement(
            IList<int> currentTokens,
            int[] replacementToOld)
        {
            if (currentTokens.Count != replacementToOld.Length)
                return false;

            int i;

            for (i = 0; i < currentTokens.Count; i++)
            {
                int expected = replacementToOld[i] < 0
                    ? EncodeReplacementToken(i)
                    : replacementToOld[i];

                if (currentTokens[i] != expected)
                    return false;
            }

            return true;
        }

        private static bool SequencesMatch(
            IList<T> current,
            IList<T> replacement)
        {
            if (current.Count != replacement.Count)
                return false;

            int i;

            for (i = 0; i < current.Count; i++)
            {
                if (!AreSame(current[i], replacement[i]))
                    return false;
            }

            return true;
        }

        private static bool AreSame(T left, T right)
        {
            if (MatchByValue)
                return EqualityComparer<T>.Default.Equals(left, right);

            return Object.ReferenceEquals((object)left, (object)right);
        }

        private static bool IsNull(T value)
        {
            return CanBeNull &&
                Object.ReferenceEquals((object)value, null);
        }
    }
}
