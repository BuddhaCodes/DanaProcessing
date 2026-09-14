using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace DanaProcessing
{
    /// <summary>
    /// Processing-flavored typed lists — https://processing.org/reference/FloatList.html
    /// and its IntList/StringList siblings. Thin wrappers around List&lt;T&gt;
    /// with the handful of methods sketches actually reach for (Append,
    /// Remove, Sort, Shuffle, Reverse, Contains) rather than a 1:1 port of
    /// Java's much larger java.util-backed surface. Standalone value-holder
    /// types — like PVector, you just `new FloatList()` directly, no Sketch
    /// factory method needed.
    ///
    /// Shuffle() is independent of Sketch's own Random()/RandomSeed() state
    /// by default (it uses its own internal Random); pass your own
    /// System.Random to Shuffle(rng) if you need a sketch's seed to also
    /// control shuffling, for reproducible results.
    /// </summary>
    public sealed class FloatList : IEnumerable<float>
    {
        private readonly List<float> _items;
        private static readonly Random SharedRandom = new Random();

        public FloatList() => _items = new List<float>();
        public FloatList(IEnumerable<float> values) => _items = new List<float>(values);

        public int Size => _items.Count;
        public float this[int index] { get => _items[index]; set => _items[index] = value; }

        public void Append(float value) => _items.Add(value);
        public float Get(int index) => _items[index];
        public void Set(int index, float value) => _items[index] = value;
        public void Remove(int index) => _items.RemoveAt(index);
        public void Clear() => _items.Clear();
        public bool Contains(float value) => _items.Contains(value);
        public int IndexOf(float value) => _items.IndexOf(value);

        public void Sort() => _items.Sort();
        public void SortReverse() { _items.Sort(); _items.Reverse(); }
        public void Reverse() => _items.Reverse();

        public void Shuffle(Random? rng = null)
        {
            rng ??= SharedRandom;
            for (int i = _items.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (_items[i], _items[j]) = (_items[j], _items[i]);
            }
        }

        public float Min() => _items.Count == 0 ? 0f : _items.Min();
        public float Max() => _items.Count == 0 ? 0f : _items.Max();
        public float Sum() => _items.Sum();
        public float Average() => _items.Count == 0 ? 0f : _items.Average();

        public float[] ToArray() => _items.ToArray();

        public IEnumerator<float> GetEnumerator() => _items.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>See FloatList — the int-valued equivalent, matching Processing's IntList.</summary>
    public sealed class IntList : IEnumerable<int>
    {
        private readonly List<int> _items;
        private static readonly Random SharedRandom = new Random();

        public IntList() => _items = new List<int>();
        public IntList(IEnumerable<int> values) => _items = new List<int>(values);

        public int Size => _items.Count;
        public int this[int index] { get => _items[index]; set => _items[index] = value; }

        public void Append(int value) => _items.Add(value);
        public int Get(int index) => _items[index];
        public void Set(int index, int value) => _items[index] = value;
        public void Remove(int index) => _items.RemoveAt(index);
        public void Clear() => _items.Clear();
        public bool Contains(int value) => _items.Contains(value);
        public int IndexOf(int value) => _items.IndexOf(value);

        public void Sort() => _items.Sort();
        public void SortReverse() { _items.Sort(); _items.Reverse(); }
        public void Reverse() => _items.Reverse();

        public void Shuffle(Random? rng = null)
        {
            rng ??= SharedRandom;
            for (int i = _items.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (_items[i], _items[j]) = (_items[j], _items[i]);
            }
        }

        public int Min() => _items.Count == 0 ? 0 : _items.Min();
        public int Max() => _items.Count == 0 ? 0 : _items.Max();
        public int Sum() => _items.Sum();

        /// <summary>Fills this list with `count` sequential ints starting at 0, like Processing's IntList.fromRange() shorthand some sketches roll by hand — handy for building index lists to shuffle/sort alongside parallel data.</summary>
        public static IntList Range(int count)
        {
            var list = new IntList();
            for (int i = 0; i < count; i++)
                list.Append(i);
            return list;
        }

        public int[] ToArray() => _items.ToArray();

        public IEnumerator<int> GetEnumerator() => _items.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>See FloatList — the string-valued equivalent, matching Processing's StringList. No Min/Max/Sum (they don't make sense for strings); adds Join() instead.</summary>
    public sealed class StringList : IEnumerable<string>
    {
        private readonly List<string> _items;
        private static readonly Random SharedRandom = new Random();

        public StringList() => _items = new List<string>();
        public StringList(IEnumerable<string> values) => _items = new List<string>(values);

        public int Size => _items.Count;
        public string this[int index] { get => _items[index]; set => _items[index] = value; }

        public void Append(string value) => _items.Add(value);
        public string Get(int index) => _items[index];
        public void Set(int index, string value) => _items[index] = value;
        public void Remove(int index) => _items.RemoveAt(index);
        public void Clear() => _items.Clear();
        public bool Contains(string value) => _items.Contains(value);
        public int IndexOf(string value) => _items.IndexOf(value);

        public void Sort() => _items.Sort(StringComparer.Ordinal);
        public void SortReverse() { Sort(); _items.Reverse(); }
        public void Reverse() => _items.Reverse();

        public void Shuffle(Random? rng = null)
        {
            rng ??= SharedRandom;
            for (int i = _items.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (_items[i], _items[j]) = (_items[j], _items[i]);
            }
        }

        /// <summary>Concatenates every element with `separator` between them, like Processing's join(stringList.array(), separator).</summary>
        public string Join(string separator) => string.Join(separator, _items);

        public string[] ToArray() => _items.ToArray();

        public IEnumerator<string> GetEnumerator() => _items.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
