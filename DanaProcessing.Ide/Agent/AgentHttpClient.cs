using System.Net.Http;

namespace DanaProcessing.Ide.Agent
{
    /// <summary>
    /// One shared, long-lived HttpClient for every provider call. The only
    /// other HttpClient in this codebase (Updates/UpdateChecker.cs) creates a
    /// fresh `using var http = new HttpClient()` per call -- fine for one
    /// startup check, but a real anti-pattern (socket exhaustion under load)
    /// for a chat feature that can make many calls in a session. A single
    /// static instance, never disposed, is the standard .NET guidance here.
    /// </summary>
    internal static class AgentHttpClient
    {
        public static readonly HttpClient Shared = new();
    }
}
