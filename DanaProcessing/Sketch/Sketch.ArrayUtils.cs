using System;

namespace DanaProcessing
{
    /// <summary>
    /// Array utility functions — https://processing.org/reference/append_.html
    /// and siblings (arrayCopy/concat/expand/reverse/shorten/sort/splice/
    /// subset). Generic over T so one implementation covers int[]/float[]/
    /// string[]/PVector[]/whatever, unlike Processing's per-type overloads
    /// (Java has no generics-over-arrays story as clean as C#'s). Every
    /// method here returns a *new* array rather than mutating in place
    /// (matching Processing's own contract: "the parameter is not modified,
    /// the result must be assigned to a new array") — the one exception is
    /// ArrayCopy(), which is a copy-into like Processing's, and Sort(),
    /// which follows Processing's own "returns a new sorted array" contract
    /// too, so nothing here mutates its input.
    /// </summary>
    public abstract partial class Sketch
    {
        /// <summary>Appends value to the end of array, returning a new, one-longer array, like Processing's append().</summary>
        public T[] Append<T>(T[] array, T value)
        {
            var result = new T[array.Length + 1];
            Array.Copy(array, result, array.Length);
            result[array.Length] = value;
            return result;
        }

        /// <summary>Copies the entire src array into dst (which must already be sized), like Processing's arrayCopy(src, dst).</summary>
        public void ArrayCopy<T>(T[] src, T[] dst) => Array.Copy(src, dst, Math.Min(src.Length, dst.Length));

        /// <summary>Copies `count` elements from src into dst, offset by srcPos/dstPos in each, like Processing's arrayCopy(src, srcPos, dst, dstPos, count).</summary>
        public void ArrayCopy<T>(T[] src, int srcPos, T[] dst, int dstPos, int count) => Array.Copy(src, srcPos, dst, dstPos, count);

        /// <summary>Concatenates two arrays into a new one, like Processing's concat().</summary>
        public T[] Concat<T>(T[] a, T[] b)
        {
            var result = new T[a.Length + b.Length];
            Array.Copy(a, result, a.Length);
            Array.Copy(b, 0, result, a.Length, b.Length);
            return result;
        }

        /// <summary>Grows array to newSize, padding new slots with default(T) (0/null/false), like Processing's expand(array, newSize). If newSize is smaller than array's current length, the array is returned unchanged (matching Processing, which only ever grows).</summary>
        public T[] Expand<T>(T[] array, int newSize)
        {
            if (newSize <= array.Length)
                return array;
            var result = new T[newSize];
            Array.Copy(array, result, array.Length);
            return result;
        }

        /// <summary>Doubles array's size (or grows a zero-length array to size 1), like Processing's one-argument expand(array).</summary>
        public T[] Expand<T>(T[] array) => Expand(array, array.Length == 0 ? 1 : array.Length * 2);

        /// <summary>Returns a new array with the elements in reverse order, like Processing's reverse().</summary>
        public T[] Reverse<T>(T[] array)
        {
            var result = new T[array.Length];
            for (int i = 0; i < array.Length; i++)
                result[i] = array[array.Length - 1 - i];
            return result;
        }

        /// <summary>Returns a new array missing the last element, like Processing's shorten().</summary>
        public T[] Shorten<T>(T[] array)
        {
            if (array.Length == 0)
                return array;
            var result = new T[array.Length - 1];
            Array.Copy(array, result, result.Length);
            return result;
        }

        /// <summary>Returns a new, ascending-sorted copy of a numeric/string array, like Processing's sort(). Uses the natural (default) comparer for T — fine for int/float/string, the types Processing's own sort() supports.</summary>
        public T[] Sort<T>(T[] array)
        {
            var result = (T[])array.Clone();
            Array.Sort(result);
            return result;
        }

        /// <summary>Sorts only the first `count` elements, leaving the rest untouched, like Processing's sort(array, count).</summary>
        public T[] Sort<T>(T[] array, int count)
        {
            var result = (T[])array.Clone();
            Array.Sort(result, 0, Math.Min(count, result.Length));
            return result;
        }

        /// <summary>Inserts value into array at index, returning a new, one-longer array, like Processing's splice(array, value, index) for a single value.</summary>
        public T[] Splice<T>(T[] array, T value, int index)
        {
            var result = new T[array.Length + 1];
            Array.Copy(array, 0, result, 0, index);
            result[index] = value;
            Array.Copy(array, index, result, index + 1, array.Length - index);
            return result;
        }

        /// <summary>Inserts every element of insertion into array starting at index, like Processing's splice(array, insertion, index) for an array insertion.</summary>
        public T[] Splice<T>(T[] array, T[] insertion, int index)
        {
            var result = new T[array.Length + insertion.Length];
            Array.Copy(array, 0, result, 0, index);
            Array.Copy(insertion, 0, result, index, insertion.Length);
            Array.Copy(array, index, result, index + insertion.Length, array.Length - index);
            return result;
        }

        /// <summary>Returns a new array containing everything from `start` to the end, like Processing's subset(array, start).</summary>
        public T[] Subset<T>(T[] array, int start) => Subset(array, start, array.Length - start);

        /// <summary>Returns a new array of `count` elements starting at `start`, like Processing's subset(array, start, count).</summary>
        public T[] Subset<T>(T[] array, int start, int count)
        {
            var result = new T[count];
            Array.Copy(array, start, result, 0, count);
            return result;
        }
    }
}
