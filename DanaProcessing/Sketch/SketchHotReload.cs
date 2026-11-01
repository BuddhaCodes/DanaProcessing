using System;
using System.Collections.Generic;
using System.Reflection;

namespace DanaProcessing
{
    /// <summary>
    /// Reflection-based state transplant between an old, running Sketch instance
    /// and a freshly recompiled one of (usually) the same source, edited — the
    /// mechanism behind the IDE's "Hot Reload" button. A normal Run always
    /// produces a brand-new instance and reruns Setup() from scratch; this is
    /// what lets a hot-reloaded sketch skip Setup() entirely and keep going from
    /// wherever its state already was.
    ///
    /// Deliberately all-or-nothing rather than "copy what matches, skip the
    /// rest": if a field was added, removed, or changed type, Analyze() reports
    /// CanApply = false and the caller should fall back to an ordinary full
    /// restart (AvaloniaSketchCanvas.LoadSketch) instead of leaving the new
    /// instance half-initialized from a partial transplant. That's a deliberate
    /// first-cut scope limit, not an oversight — see ROADMAP.md's "True
    /// hot-reload" entry.
    ///
    /// Lives in the core DanaProcessing project (not DanaProcessing.Ide) on
    /// purpose: DanaProcessing.AvaloniaHost's AvaloniaSketchCanvas is what
    /// actually needs to call this (from inside its own _sketchLock, to close
    /// the race with the render thread), and AvaloniaHost only references this
    /// core project, never DanaProcessing.Ide.
    /// </summary>
    public static class SketchHotReload
    {
        /// <summary>Result of comparing an old and new sketch's field shape. Reasons is
        /// empty when CanApply is true, and holds one human-readable line per
        /// added/removed/retyped field otherwise.</summary>
        public sealed record Plan(bool CanApply, IReadOnlyList<string> Reasons);

        /// <summary>
        /// Decides whether Transplant() can safely carry every field of
        /// <paramref name="oldSketch"/> over onto <paramref name="newSketch"/>.
        /// Base-class (Sketch/GraphicsContext) fields always match trivially —
        /// every compiled sketch assembly references the one DanaProcessing.dll
        /// already loaded at IDE startup, so those fields are literally the same
        /// FieldInfo/Type across old and new. In practice this only ever finds a
        /// real mismatch in the user's own MySketch-declared fields.
        /// </summary>
        public static Plan Analyze(Sketch oldSketch, Sketch newSketch)
        {
            var oldFields = CollectFields(oldSketch.GetType());
            var newFields = CollectFields(newSketch.GetType());

            var reasons = new List<string>();

            foreach (var (name, oldField) in oldFields)
            {
                if (!newFields.TryGetValue(name, out var newField))
                {
                    reasons.Add($"'{name}' was removed");
                    continue;
                }
                if (oldField.FieldType != newField.FieldType)
                    reasons.Add($"'{name}' changed type ({oldField.FieldType.Name} -> {newField.FieldType.Name})");
            }

            foreach (var name in newFields.Keys)
            {
                if (!oldFields.ContainsKey(name))
                    reasons.Add($"'{name}' is new");
            }

            return new Plan(reasons.Count == 0, reasons);
        }

        /// <summary>
        /// Copies every matching field's value from <paramref name="oldSketch"/> onto
        /// <paramref name="newSketch"/>. Only call this after Analyze() reports
        /// CanApply == true — it doesn't re-check on its own.
        ///
        /// TRAP TO REMEMBER: this shallow-copies GraphicsContext's own paint
        /// objects (_fillPaint/_strokePaint/etc.) onto the new instance, same
        /// references as the old one. GraphicsContext.Dispose() disposes those —
        /// so nothing must ever call Dispose() on oldSketch after a hot-reload
        /// swap (nothing does today; keep it that way, or newSketch inherits
        /// disposed paint objects out from under it).
        /// </summary>
        public static void Transplant(Sketch oldSketch, Sketch newSketch)
        {
            var oldFields = CollectFields(oldSketch.GetType());
            var newFields = CollectFields(newSketch.GetType());

            foreach (var (name, oldField) in oldFields)
            {
                if (!newFields.TryGetValue(name, out var newField))
                    continue;

                newField.SetValue(newSketch, oldField.GetValue(oldSketch));
            }
        }

        /// <summary>
        /// Every non-delegate instance field across the type's full hierarchy, by
        /// name. Walks BaseType manually with DeclaredOnly at each level — plain
        /// GetFields() never returns a base type's PRIVATE fields, which is most
        /// of what Sketch/GraphicsContext actually hold (Width, MouseX, _backend,
        /// the drawing-state paint objects, ...). Delegate-typed fields (event
        /// backing fields: SizeChanged, ExitRequested, etc.) are excluded on
        /// purpose — those must stay host-managed via explicit +=/-=
        /// subscription, not raw field copying, or a host that also resubscribes
        /// itself (as AvaloniaSketchCanvas does for SizeChanged) would end up
        /// double-subscribed.
        /// </summary>
        private static Dictionary<string, FieldInfo> CollectFields(Type type)
        {
            var fields = new Dictionary<string, FieldInfo>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

            for (var t = type; t != null && t != typeof(object); t = t.BaseType)
            {
                foreach (var field in t.GetFields(flags))
                {
                    if (typeof(Delegate).IsAssignableFrom(field.FieldType))
                        continue;

                    // A derived type's field of the same name as a base type's
                    // (via `new`) would otherwise throw on the second insert --
                    // not a pattern used anywhere in this codebase, but keep the
                    // walk itself from crashing if it ever comes up; the leaf
                    // one (found first, since the walk starts at `type` and
                    // moves up through BaseType) wins, matching normal
                    // field-hiding semantics closely enough for a name-based
                    // transplant.
                    fields.TryAdd(field.Name, field);
                }
            }

            return fields;
        }
    }
}
