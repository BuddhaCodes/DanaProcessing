using System;

namespace DanaProcessing
{
    /// <summary>Payload for a SelectFileRequested event — what kind of native dialog to show, the prompt to display, and the callback to invoke with the chosen path (or null if the user cancelled).</summary>
    public sealed class SelectFileRequest
    {
        public enum DialogKind { Input, Output, Folder }

        public DialogKind Kind { get; }
        public string Prompt { get; }
        internal Action<string?> Callback { get; }

        internal SelectFileRequest(DialogKind kind, string prompt, Action<string?> callback)
        {
            Kind = kind;
            Prompt = prompt;
            Callback = callback;
        }

        /// <summary>The host calls this once the user has picked a path (or cancelled, passing null) from its native dialog.</summary>
        public void Respond(string? path) => Callback(path);
    }

    public abstract partial class Sketch
    {
        // =====================================================================
        // File dialogs — https://processing.org/reference/selectInput_.html
        // and siblings (selectOutput/selectFolder). DanaProcessing doesn't own
        // a window, so it can't pop a native file dialog itself — like
        // Cursor()/NoCursor() in Sketch.Input.cs, this just raises an event
        // with the details a host (e.g. AvaloniaSketchCanvas) needs to show
        // its own native dialog, then calls back into the sketch with the
        // result via SelectFileRequest.Respond().
        // =====================================================================

        /// <summary>Raised when the sketch calls SelectInput/SelectOutput/SelectFolder. The host should show the matching native dialog and call Respond(path) (or Respond(null) if the user cancels) on the payload once the user has answered.</summary>
        public event Action<SelectFileRequest>? SelectFileRequested;

        /// <summary>Asks the host to show an "open file" dialog, like Processing's selectInput(prompt, callback). `callback` receives the chosen path, or null if the user cancelled.</summary>
        public void SelectInput(string prompt, Action<string?> callback) =>
            SelectFileRequested?.Invoke(new SelectFileRequest(SelectFileRequest.DialogKind.Input, prompt, callback));

        /// <summary>Asks the host to show a "save file" dialog, like Processing's selectOutput(prompt, callback).</summary>
        public void SelectOutput(string prompt, Action<string?> callback) =>
            SelectFileRequested?.Invoke(new SelectFileRequest(SelectFileRequest.DialogKind.Output, prompt, callback));

        /// <summary>Asks the host to show a "choose folder" dialog, like Processing's selectFolder(prompt, callback).</summary>
        public void SelectFolder(string prompt, Action<string?> callback) =>
            SelectFileRequested?.Invoke(new SelectFileRequest(SelectFileRequest.DialogKind.Folder, prompt, callback));
    }
}
