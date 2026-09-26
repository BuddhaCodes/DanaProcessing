using System.Collections.Generic;

namespace DanaProcessing.Ide.Agent
{
    /// <summary>The fixed set of providers Agent mode ships with for v1 --
    /// SettingsWindow's provider dropdown lists these by Name; adding a third
    /// provider later means adding one more ILlmProvider implementation and
    /// one more entry here, nothing else.</summary>
    public static class AgentProviders
    {
        public static readonly IReadOnlyList<ILlmProvider> All = new ILlmProvider[]
        {
            new AnthropicProvider(),
            new OpenAiProvider(),
        };

        /// <summary>Looks up a provider by its Name (== AgentSettings.Provider),
        /// falling back to the first provider if the saved name doesn't match
        /// any known one (e.g. an older settings file, or a provider that got
        /// renamed/removed) rather than throwing.</summary>
        public static ILlmProvider Resolve(string? providerName)
        {
            foreach (var provider in All)
            {
                if (provider.Name == providerName)
                    return provider;
            }
            return All[0];
        }
    }
}
