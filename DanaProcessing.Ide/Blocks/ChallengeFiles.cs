using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using DanaProcessing.Ide.Localization;

namespace DanaProcessing.Ide.Blocks
{
    /// <summary>
    /// A challenge a teacher made, as saved in a .danareto file: their own
    /// answer (built with blocks) is the solution; the IDE works out the rest.
    /// <list type="bullet">
    /// <item>"shape": the child's turtle has to draw what the solution draws.</item>
    /// <item>"blocks": the child has to use the same kinds of blocks as the solution (music, drawing, games).</item>
    /// </list>
    /// </summary>
    public sealed class ChallengeFile
    {
        public const string Extension = ".danareto";
        public const int CurrentFormat = 1;

        public int Format { get; set; } = CurrentFormat;
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];
        public string Title { get; set; } = "";
        public string Goal { get; set; } = "";
        public string Hint { get; set; } = "";
        public string? Author { get; set; }
        public BlockView View { get; set; } = BlockView.Tiles;
        public ChallengeCheck Check { get; set; } = ChallengeCheck.Shape;

        /// <summary>Block types on the palette. Empty = the blocks the solution uses (and their start blocks).</summary>
        public List<string> Palette { get; set; } = new();

        /// <summary>The teacher's answer.</summary>
        public BlockProgram Solution { get; set; } = new();

        /// <summary>What the child starts from. Null = the solution's start blocks, empty.</summary>
        public BlockProgram? Start { get; set; }

        private static readonly JsonSerializerOptions Json = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        };

        public string ToJson() => JsonSerializer.Serialize(this, Json);

        /// <summary>Reads a challenge file; throws <see cref="InvalidDataException"/> with a readable message if it isn't one.</summary>
        public static ChallengeFile FromJson(string json)
        {
            ChallengeFile? file;
            try
            {
                file = JsonSerializer.Deserialize<ChallengeFile>(json, Json);
            }
            catch (JsonException e)
            {
                throw new InvalidDataException(Loc.Tr("No es un archivo de reto válido.", "That isn't a valid challenge file.") + " " + e.Message);
            }
            if (file is null || file.Solution is null)
                throw new InvalidDataException(Loc.Tr("No es un archivo de reto válido.", "That isn't a valid challenge file."));
            if (file.Format > CurrentFormat)
                throw new InvalidDataException(Loc.Tr("Este reto se hizo con una versión más nueva de DanaProcessing.", "This challenge was made with a newer DanaProcessing."));
            // Unknown block types (a typo, a newer version) would break the
            // palette: keep only the ones this IDE has; the programs go
            // through BlockProgram's own repair.
            file.Palette = file.Palette.Where(BlockCatalog.Exists).Distinct().ToList();
            file.Solution = BlockProgram.FromJson(file.Solution.ToJson());
            if (file.Start is not null)
                file.Start = BlockProgram.FromJson(file.Start.ToJson());
            if (string.IsNullOrWhiteSpace(file.Id))
                file.Id = Guid.NewGuid().ToString("N")[..12];
            return file;
        }

        /// <summary>Makes a challenge from a teacher's finished program, ready to save.</summary>
        public static ChallengeFile FromSolution(BlockProgram solution, string title, string goal, string hint, ChallengeCheck check, BlockView view, string? author = null)
        {
            var copy = solution.Clone();
            copy.ChallengeId = null;
            return new ChallengeFile
            {
                Title = title.Trim(), Goal = goal.Trim(), Hint = hint.Trim(), Author = author,
                Check = check, View = view, Solution = copy,
            };
        }

        /// <summary>The block types the solution actually runs (hats included).</summary>
        public IReadOnlyList<string> UsedTypes() =>
            Solution.Scripts().SelectMany(s => s.Blocks).SelectMany(b => b.SelfAndDescendants()).Select(b => b.Type).Distinct().ToList();

        /// <summary>Blocks in the solution, not counting start blocks: the three-star count.</summary>
        public int SolutionBlockCount() =>
            Solution.Scripts().SelectMany(s => s.Blocks).SelectMany(b => b.SelfAndDescendants()).Count(b => b.Def.Shape != BlockShape.Hat);

        /// <summary>The child's starting program: <see cref="Start"/>, or the solution's start blocks with nothing under them.</summary>
        public BlockProgram StartProgram()
        {
            if (Start is not null)
                return Start.Clone();
            var p = new BlockProgram();
            foreach (var script in Solution.Scripts())
            {
                var stack = p.AddStack(script.X, script.Y, script.Hat!.Type);
                foreach (var (k, v) in script.Hat!.Values)
                    stack.Blocks[0].Values[k] = v;
            }
            if (p.Stacks.Count == 0)
                p.AddStack(40, 40, BlockCatalog.Start);
            return p;
        }

        /// <summary>What's wrong with it as a challenge (empty title, nothing to check...), or null if it's fine.</summary>
        public string? Problem()
        {
            if (string.IsNullOrWhiteSpace(Title))
                return Loc.Tr("Ponele un nombre al reto.", "Give the challenge a name.");
            if (string.IsNullOrWhiteSpace(Goal))
                return Loc.Tr("Escribí qué tienen que hacer.", "Write what they have to do.");
            if (SolutionBlockCount() == 0)
                return Loc.Tr("La solución no tiene bloques debajo de un comienzo.", "The solution has no blocks under a start block.");
            if (Check == ChallengeCheck.Shape && !UsedTypes().Any(t => BlockCatalog.Get(t).Category == BlockCategory.Turtle))
                return Loc.Tr("Para comparar dibujos, la solución tiene que usar la tortuga.", "To compare drawings, the solution has to use the turtle.");
            return null;
        }

        /// <summary>The challenge a child plays.</summary>
        public Challenge ToChallenge(string? sourceFile = null)
        {
            var used = UsedTypes();
            var palette = (Palette.Count > 0 ? Palette : used).ToList();
            foreach (var hat in StartProgram().Stacks.Select(s => s.Hat?.Type).OfType<string>())
                if (!palette.Contains(hat))
                    palette.Insert(0, hat);
            var required = Check == ChallengeCheck.Blocks
                ? used.Where(t => BlockCatalog.Get(t).Shape != BlockShape.Hat).ToList()
                : new List<string>();
            var solution = Solution;
            var start = this;
            return new Challenge
            {
                Id = ChallengeFiles.IdPrefix + Id,
                TitleEs = Title, TitleEn = Title,
                GoalEs = Goal, GoalEn = Goal,
                HintEs = string.IsNullOrWhiteSpace(Hint) ? Loc.Tr("Pedile una pista a tu docente.", "Ask your teacher for a hint.") : Hint,
                HintEn = string.IsNullOrWhiteSpace(Hint) ? "Ask your teacher for a hint." : Hint,
                Icon = "📝",
                View = View,
                Palette = palette,
                Required = required,
                BestBlockCount = Math.Max(1, SolutionBlockCount()),
                Start = () => start.StartProgram(),
                Target = Check == ChallengeCheck.Shape ? () => HeadlessBlockRunner.Run(solution).TurtleLines : null,
                SourceFile = sourceFile,
            };
        }
    }

    public enum ChallengeCheck
    {
        /// <summary>The turtle has to draw the same as the solution.</summary>
        Shape,
        /// <summary>The program has to use the same kinds of blocks as the solution.</summary>
        Blocks,
    }

    /// <summary>Where teachers' challenges live, and loading/saving them.</summary>
    public static class ChallengeFiles
    {
        public const string IdPrefix = "file:";

        /// <summary>Documents/DanaProcessing/Retos (a teacher can also open one from anywhere: it's copied here).</summary>
        internal static string Folder { get; set; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "DanaProcessing", "Retos");

        /// <summary>Every readable challenge in the folder, by file name. Broken files are skipped.</summary>
        public static IReadOnlyList<Challenge> LoadFolder()
        {
            var list = new List<Challenge>();
            try
            {
                if (!Directory.Exists(Folder))
                    return list;
                foreach (var path in Directory.GetFiles(Folder, "*" + ChallengeFile.Extension).OrderBy(p => Path.GetFileName(p), StringComparer.OrdinalIgnoreCase))
                {
                    try
                    {
                        var challenge = ChallengeFile.FromJson(File.ReadAllText(path)).ToChallenge(path);
                        if (list.All(c => c.Id != challenge.Id))
                            list.Add(challenge);
                    }
                    catch (Exception)
                    {
                        // one bad file shouldn't hide the others
                    }
                }
            }
            catch (Exception)
            {
                // no access to Documents: no teacher challenges
            }
            return list;
        }

        /// <summary>Saves <paramref name="file"/> into the folder (name from its title) and returns the path.</summary>
        public static string SaveToFolder(ChallengeFile file)
        {
            Directory.CreateDirectory(Folder);
            var path = Path.Combine(Folder, SafeName(file.Title) + "-" + file.Id + ChallengeFile.Extension);
            File.WriteAllText(path, file.ToJson());
            return path;
        }

        /// <summary>Copies a challenge file someone shared into the folder (checking it first). Returns the challenge.</summary>
        public static Challenge Import(string path)
        {
            var file = ChallengeFile.FromJson(File.ReadAllText(path));
            var saved = SaveToFolder(file);
            ChallengeCatalog.ReloadTeacher();
            return ChallengeCatalog.Get(IdPrefix + file.Id) ?? file.ToChallenge(saved);
        }

        internal static string SafeName(string title)
        {
            // Windows' forbidden characters too, so a file made on Linux or a
            // Mac can be copied to a school's Windows computers.
            var invalid = Path.GetInvalidFileNameChars().Concat("<>:\"/\\|?*").ToHashSet();
            var name = new string(title.Trim().Select(c => invalid.Contains(c) || char.IsWhiteSpace(c) ? '-' : c).ToArray()).Trim('-');
            return name.Length == 0 ? "reto" : name.Length > 40 ? name[..40] : name;
        }
    }
}
