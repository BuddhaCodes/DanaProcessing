using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DanaProcessing.Ide.Compilation.PackageManagement;

namespace DanaProcessing.Ide.Agent
{
    /// <summary>
    /// Per-conversation state the agent's tools share: which `// nuget:`
    /// packages the user has already allowed to run, and how to ask about new
    /// ones.
    ///
    /// Why this exists: run_sketch downloads every `// nuget:` package in the
    /// sketch from nuget.org and loads it into the IDE process. The user's own
    /// packages are theirs to choose, but a package the MODEL wrote into the
    /// sketch must not be fetched and executed without the user saying yes --
    /// otherwise a confused (or prompt-injected) model can pull arbitrary code
    /// onto the machine with no human in the loop.
    ///
    /// The rule: directives present in a source the USER wrote (anything that
    /// isn't byte-for-byte the last text the agent itself put in the editor)
    /// are approved implicitly; anything else is asked about once, right
    /// before the run that would restore it.
    /// </summary>
    public sealed class AgentToolContext
    {
        private readonly HashSet<string> _approved = new(StringComparer.Ordinal);
        private string? _lastAgentWrittenSource;

        /// <summary>Asks the user whether these packages may be downloaded and
        /// run; true = allow. Null means "no way to ask": everything new is refused.</summary>
        public Func<IReadOnlyList<PackageDirective>, Task<bool>>? ConfirmPackages { get; init; }

        /// <summary>Call with the editor's text right BEFORE the agent replaces it.
        /// If the user wrote that text (it isn't what the agent last wrote), its
        /// packages are the user's own choice and count as approved.</summary>
        public void NoteSourceBeforeAgentEdit(string? currentSource)
        {
            if (currentSource is null || currentSource == _lastAgentWrittenSource)
                return;
            Approve(PackageDirectiveParser.Parse(currentSource));
        }

        /// <summary>Call with the text the agent just put in the editor.</summary>
        public void NoteAgentWrote(string newSource) => _lastAgentWrittenSource = newSource;

        /// <summary>The packages in <paramref name="source"/> that still need the user's OK.
        /// A source the agent never touched is entirely the user's, so nothing in it does.</summary>
        public IReadOnlyList<PackageDirective> Unapproved(string? source)
        {
            if (source is null || source != _lastAgentWrittenSource)
                return Array.Empty<PackageDirective>();
            return PackageDirectiveParser.Parse(source).Where(d => !_approved.Contains(Key(d))).ToList();
        }

        public void Approve(IEnumerable<PackageDirective> directives)
        {
            foreach (var d in directives)
                _approved.Add(Key(d));
        }

        /// <summary>Id + exact version: approving "Foo, 1.0" does not approve "Foo" (latest) or "Foo, 2.0".</summary>
        private static string Key(PackageDirective d) =>
            d.Id.ToLowerInvariant() + "|" + (d.Version?.ToLowerInvariant() ?? "*");
    }
}
