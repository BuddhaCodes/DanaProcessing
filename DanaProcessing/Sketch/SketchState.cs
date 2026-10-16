using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace DanaProcessing
{
    /// <summary>
    /// A frozen copy of a sketch's own state at one frame -- what makes rewind
    /// "real": scrub back to frame 300, press "continue from here", and the
    /// sketch's fields go back to how they were at frame 300 and it runs on
    /// from there. Built with the same field walk as hot reload
    /// (SketchHotReload), but DEEP: lists, arrays and your own objects are
    /// copied, so later frames can't change the snapshot.
    ///
    /// What's captured: every field your sketch class declares, plus the
    /// engine's FrameCount, random generator, seed and noise. What's shared,
    /// not copied: things that wrap the outside world -- images, fonts,
    /// shaders, sounds, streams, ports, threads, delegates (anything
    /// IDisposable, basically). Millis() keeps running, and the drawing state
    /// (Fill, Stroke...) stays as it is now.
    /// </summary>
    public sealed class SketchState
    {
        /// <summary>Objects copied, at most -- past this the snapshot gives up (a sketch with a huge state stays visual-only rewind).</summary>
        public const int MaxObjects = 200_000;

        private readonly List<(FieldInfo Field, object? Value)> _values;

        /// <summary>The sketch's FrameCount when this was taken.</summary>
        public int FrameCount { get; }

        /// <summary>How many objects the copy holds (a rough size).</summary>
        public int ObjectCount { get; }

        private SketchState(List<(FieldInfo, object?)> values, int frameCount, int objects)
        {
            _values = values;
            FrameCount = frameCount;
            ObjectCount = objects;
        }

        // Base-class state that IS part of "where the sketch is".
        private static readonly string[] EngineFields =
        {
            "<FrameCount>k__BackingField", "_rng", "_seed", "_seeded", "_noise", "_spareGaussian",
        };

        private static readonly ConcurrentDictionary<Type, FieldInfo[]> FieldCache = new();

        /// <summary>The fields a snapshot covers for this sketch type.</summary>
        private static FieldInfo[] FieldsOf(Type type) => FieldCache.GetOrAdd(type, t =>
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            var fields = new List<FieldInfo>();
            for (var level = t; level != null && level != typeof(Sketch); level = level.BaseType)
                fields.AddRange(level.GetFields(flags).Where(f => !typeof(Delegate).IsAssignableFrom(f.FieldType)));
            foreach (var name in EngineFields)
                if (typeof(Sketch).GetField(name, flags) is { } f)
                    fields.Add(f);
            return fields.ToArray();
        });

        /// <summary>Takes a snapshot, or returns null when the state is too big (more than <see cref="MaxObjects"/> objects).</summary>
        public static SketchState? Capture(Sketch sketch, int maxObjects = MaxObjects)
        {
            var copier = new DeepCopier(sketch, maxObjects);
            var values = new List<(FieldInfo, object?)>();
            try
            {
                foreach (var field in FieldsOf(sketch.GetType()))
                    values.Add((field, copier.Copy(field.GetValue(sketch))));
            }
            catch (StateTooLargeException)
            {
                return null;
            }
            catch (Exception ex) when (ex is ArgumentException or FieldAccessException or NotSupportedException or MemberAccessException or InvalidCastException)
            {
                // Something in the state can't be copied by reflection: rewind stays visual-only.
                DanaLogger.Warn($"Rebobinado: no se puede copiar el estado de {sketch.GetType().Name} ({ex.GetType().Name}: {ex.Message}).");
                return null;
            }
            return new SketchState(values, sketch.FrameCount, copier.Count);
        }

        /// <summary>
        /// Puts the snapshot back into <paramref name="sketch"/> -- an instance of
        /// the same class, or of its hot-reloaded successor (fields are matched
        /// by name and type). Copies again, so the same snapshot can be restored
        /// any number of times.
        /// </summary>
        public void Restore(Sketch sketch)
        {
            var copier = new DeepCopier(sketch, int.MaxValue);

            // After a Hot Reload the sketch is a NEW compiled type: match fields
            // by name, like SketchHotReload does -- so you can rewind to frame
            // 300 and continue from there with the code you just changed.
            Dictionary<string, FieldInfo>? byName = null;
            foreach (var (field, value) in _values)
            {
                var target = field;
                if (field.DeclaringType != null && !field.DeclaringType.IsInstanceOfType(sketch))
                {
                    byName ??= FieldsOf(sketch.GetType()).GroupBy(f => f.Name).ToDictionary(g => g.Key, g => g.First());
                    if (!byName.TryGetValue(field.Name, out var renamed) || renamed.FieldType != field.FieldType)
                        continue; // gone or changed type in the new code: keep the new code's value
                    target = renamed;
                }
                target.SetValue(sketch, copier.Copy(value));
            }
        }

        private sealed class StateTooLargeException : Exception { }

        /// <summary>Reflection deep copy with a visited map (shared references and cycles stay shared and cyclic).</summary>
        private sealed class DeepCopier
        {
            private readonly Sketch _sketch;
            private readonly int _limit;
            private readonly Dictionary<object, object> _copies = new(ReferenceEqualityComparer.Instance);
            public int Count { get; private set; }

            public DeepCopier(Sketch sketch, int limit)
            {
                _sketch = sketch;
                _limit = limit;
            }

            public object? Copy(object? value)
            {
                if (value == null)
                    return null;
                var type = value.GetType();
                if (IsImmutableOrShared(value, type))
                    return value;
                if (_copies.TryGetValue(value, out var done))
                    return done;
                if (++Count > _limit)
                    throw new StateTooLargeException();

                if (type.IsArray)
                {
                    var source = (Array)value;
                    var copy = (Array)source.Clone(); // right shape, rank and bounds
                    _copies[value] = copy;
                    var element = type.GetElementType()!;
                    if (!IsPlainValue(element))
                    {
                        if (source.Rank == 1)
                        {
                            for (int i = 0; i < source.Length; i++)
                                copy.SetValue(Copy(source.GetValue(i)), i);
                        }
                        else
                        {
                            foreach (var index in Indices(source))
                                copy.SetValue(Copy(source.GetValue(index)), index);
                        }
                    }
                    return copy;
                }

                if (type.IsValueType)
                {
                    // A struct holding references (a tuple with a list...): copy its fields.
                    var boxed = RuntimeHelpers.GetUninitializedObject(type);
                    CopyFields(value, boxed, type);
                    return boxed;
                }

                var clone = RuntimeHelpers.GetUninitializedObject(type);
                _copies[value] = clone;
                CopyFields(value, clone, type);
                return clone;
            }

            private void CopyFields(object from, object to, Type type)
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
                for (var t = type; t != null && t != typeof(object); t = t.BaseType)
                    foreach (var f in t.GetFields(flags))
                        f.SetValue(to, Copy(f.GetValue(from)));
            }

            private bool IsImmutableOrShared(object value, Type type)
            {
                if (ReferenceEquals(value, _sketch) || value is Sketch)
                    return true; // a field pointing at the sketch itself stays pointing at it
                if (IsPlainValue(type) || value is string || value is Delegate || value is MemberInfo
                    || value is Assembly || value is IDisposable || value is Task || value is Thread
                    || value is WaitHandle || value is CancellationTokenSource || value is Exception)
                    return true;
                // Engine wrappers around native objects, and anything SkiaSharp.
                var ns = type.Namespace ?? "";
                return ns.StartsWith("SkiaSharp", StringComparison.Ordinal)
                    || ns.StartsWith("System.Reflection", StringComparison.Ordinal)
                    || ns.StartsWith("System.Threading", StringComparison.Ordinal) && type != typeof(CancellationToken)
                    || type == typeof(PImage) || type == typeof(PFont) || type == typeof(PShape) || type == typeof(PShader2D)
                    || type == typeof(SampleBuffer);
            }

            private static readonly ConcurrentDictionary<Type, bool> PlainCache = new();

            /// <summary>Primitives, enums and structs made only of those: copying the value copies everything.</summary>
            private static bool IsPlainValue(Type type) => PlainCache.GetOrAdd(type, t =>
            {
                if (t.IsPrimitive || t.IsEnum || t == typeof(decimal) || t == typeof(DateTime) || t == typeof(TimeSpan) || t == typeof(Guid))
                    return true;
                if (!t.IsValueType)
                    return false;
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                return t.GetFields(flags).All(f => f.FieldType != t && IsPlainValue(f.FieldType));
            });

            private static IEnumerable<int[]> Indices(Array array)
            {
                var index = new int[array.Rank];
                for (int d = 0; d < array.Rank; d++)
                    index[d] = array.GetLowerBound(d);
                for (int n = 0; n < array.Length; n++)
                {
                    yield return (int[])index.Clone();
                    for (int d = array.Rank - 1; d >= 0; d--)
                    {
                        if (++index[d] <= array.GetUpperBound(d))
                            break;
                        index[d] = array.GetLowerBound(d);
                    }
                }
            }
        }
    }
}
