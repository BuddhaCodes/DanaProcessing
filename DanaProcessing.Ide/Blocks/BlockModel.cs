using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DanaProcessing.Ide.Blocks
{
    /// <summary>
    /// One block placed in a program: its type (see <see cref="BlockCatalog"/>),
    /// the value picked in each of its slots, and -- for "C" blocks like
    /// repetir / si -- the blocks inside it.
    /// </summary>
    public sealed class Block
    {
        /// <summary>Unique within its program; the generated code reports it back when the block runs.</summary>
        public int Id { get; set; }

        public string Type { get; set; } = "";

        /// <summary>Slot name -> chosen value (a key of that slot's choices, e.g. "100", "red", "C4").</summary>
        public Dictionary<string, string> Values { get; set; } = new();

        /// <summary>The blocks inside a C block (null for every other shape).</summary>
        public List<Block>? Body { get; set; }

        [JsonIgnore]
        public BlockDef Def => BlockCatalog.Get(Type);

        /// <summary>The value of <paramref name="slot"/>, or its default if it was never set.</summary>
        public string Value(string slot)
        {
            if (Values.TryGetValue(slot, out var v) && SlotChoices.IsValid(Def.Slot(slot).Kind, v))
                return v;
            return Def.Slot(slot).Default;
        }

        /// <summary>This block and everything inside it, depth first.</summary>
        public IEnumerable<Block> SelfAndDescendants()
        {
            yield return this;
            if (Body is null)
                yield break;
            foreach (var child in Body)
                foreach (var b in child.SelfAndDescendants())
                    yield return b;
        }
    }

    /// <summary>
    /// A column of blocks snapped together, lying somewhere on the workspace.
    /// If its first block is a hat ("al empezar", "siempre"...) the column is a
    /// script that runs; a column without a hat is just lying around and does
    /// nothing, like in Scratch.
    /// </summary>
    public sealed class BlockStack
    {
        public double X { get; set; }
        public double Y { get; set; }
        public List<Block> Blocks { get; set; } = new();

        [JsonIgnore]
        public Block? Hat => Blocks.Count > 0 && Blocks[0].Def.Shape == BlockShape.Hat ? Blocks[0] : null;
    }

    /// <summary>A whole block program: what a blocks tab holds, and what a .danablocks file stores.</summary>
    public sealed class BlockProgram
    {
        public const string FileExtension = ".danablocks";

        public int Version { get; set; } = 1;

        /// <summary>Shown on the tab, and (cleaned up) the name of the generated class.</summary>
        public string Name { get; set; } = "MiPrograma";

        public List<BlockStack> Stacks { get; set; } = new();

        /// <summary>Next id to hand out; ids are never reused within a program.</summary>
        public int NextId { get; set; }

        /// <summary>The challenge this program is the answer to, if any (see Challenges).</summary>
        public string? ChallengeId { get; set; }

        /// <summary>How the program is shown: picture tiles (6-8 years) or sentences on lines (9-12).</summary>
        public BlockView View { get; set; } = BlockView.Tiles;

        /// <summary>A new block of <paramref name="type"/> with a fresh id and every slot at its default.</summary>
        public Block NewBlock(string type)
        {
            var def = BlockCatalog.Get(type);
            var block = new Block { Id = NextId++, Type = type };
            foreach (var slot in def.Slots)
                block.Values[slot.Name] = slot.Default;
            if (def.Shape == BlockShape.CBlock)
                block.Body = new List<Block>();
            return block;
        }

        /// <summary>Adds a stack at (x, y) made of new blocks of the given types, in order.</summary>
        public BlockStack AddStack(double x, double y, params string[] types)
        {
            var stack = new BlockStack { X = x, Y = y };
            foreach (var t in types)
                stack.Blocks.Add(NewBlock(t));
            Stacks.Add(stack);
            return stack;
        }

        /// <summary>Every block in the program, depth first, stack by stack.</summary>
        public IEnumerable<Block> AllBlocks() => Stacks.SelectMany(s => s.Blocks).SelectMany(b => b.SelfAndDescendants());

        /// <summary>The stacks that run (start with a hat), in reading order: top to bottom, then left to right.</summary>
        public IEnumerable<BlockStack> Scripts() =>
            Stacks.Where(s => s.Hat != null).OrderBy(s => Math.Round(s.Y / 20)).ThenBy(s => s.X);

        public Block? Find(int id) => AllBlocks().FirstOrDefault(b => b.Id == id);

        /// <summary>
        /// The list that directly holds the block with <paramref name="id"/> (a
        /// stack's blocks or a C block's body), its index there, and the C block
        /// owning that list (null at stack level).
        /// </summary>
        public (List<Block> List, int Index, Block? Owner, BlockStack Stack)? Locate(int id)
        {
            foreach (var stack in Stacks)
            {
                var found = Locate(stack.Blocks, null, id);
                if (found is { } f)
                    return (f.List, f.Index, f.Owner, stack);
            }
            return null;
        }

        private static (List<Block> List, int Index, Block? Owner)? Locate(List<Block> list, Block? owner, int id)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Id == id)
                    return (list, i, owner);
                if (list[i].Body is { } body && Locate(body, list[i], id) is { } inner)
                    return inner;
            }
            return null;
        }

        /// <summary>The blocks that contain <paramref name="id"/> (C blocks and the hat of its script), innermost first.</summary>
        public List<int> AncestorsOf(int id)
        {
            var result = new List<int>();
            foreach (var stack in Stacks)
            {
                if (Path(stack.Blocks, id, result))
                {
                    if (stack.Hat is { } hat && hat.Id != id && !result.Contains(hat.Id))
                        result.Add(hat.Id);
                    return result;
                }
            }
            return result;
        }

        private static bool Path(List<Block> list, int id, List<int> path)
        {
            foreach (var b in list)
            {
                if (b.Id == id)
                    return true;
                if (b.Body is { } body && Path(body, id, path))
                {
                    path.Add(b.Id);
                    return true;
                }
            }
            return false;
        }

        // =================================================================
        // Files
        // =================================================================

        private static readonly JsonSerializerOptions Json = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() }, // "View": "Lines", readable in the file
        };

        public string ToJson() => JsonSerializer.Serialize(this, Json);

        /// <summary>
        /// Reads a program back. Blocks of a type this version doesn't know
        /// (a file from a newer IDE) are dropped rather than failing the whole
        /// file, and ids are repaired if two blocks share one.
        /// </summary>
        public static BlockProgram FromJson(string json)
        {
            var program = JsonSerializer.Deserialize<BlockProgram>(json, Json) ?? new BlockProgram();
            program.Stacks ??= new List<BlockStack>();
            foreach (var stack in program.Stacks)
                stack.Blocks = Clean(stack.Blocks);
            program.Stacks.RemoveAll(s => s.Blocks.Count == 0);

            var seen = new HashSet<int>();
            int max = -1;
            foreach (var b in program.AllBlocks())
                max = Math.Max(max, b.Id);
            program.NextId = Math.Max(program.NextId, max + 1);
            foreach (var b in program.AllBlocks())
                if (!seen.Add(b.Id))
                    b.Id = program.NextId++;
            return program;
        }

        private static List<Block> Clean(List<Block>? blocks)
        {
            var result = new List<Block>();
            foreach (var b in blocks ?? new List<Block>())
            {
                if (b is null || !BlockCatalog.Exists(b.Type))
                    continue;
                b.Values ??= new Dictionary<string, string>();
                if (b.Def.Shape == BlockShape.CBlock)
                    b.Body = Clean(b.Body);
                else
                    b.Body = null;
                result.Add(b);
            }
            return result;
        }

        /// <summary>A deep copy (through JSON, so it's exactly what saving would keep).</summary>
        public BlockProgram Clone() => FromJson(ToJson());
    }
}
