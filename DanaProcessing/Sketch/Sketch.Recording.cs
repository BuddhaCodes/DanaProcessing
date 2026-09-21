using System;
using SkiaSharp;

namespace DanaProcessing
{
    public abstract partial class Sketch
    {
        // =====================================================================
        // Vector export — https://processing.org/reference/beginRaw_.html and
        // https://processing.org/reference/beginRecord_.html. Real Processing
        // draws to the screen AND records simultaneously; SkiaSharp gives us
        // no built-in way to fork one set of draw calls to two canvases at
        // once, so this instead REDIRECTS every draw call between
        // BeginRaw()/EndRaw() straight to the PDF page — nothing appears on
        // the normal on-screen canvas while a recording is active. The
        // idiomatic pattern for a sketch that wants both is to factor the
        // actual drawing into its own method and call it twice: once
        // normally in Draw(), and once more wrapped in BeginRaw()/EndRaw()
        // (typically on a keypress, not every frame) — see the "Exportar a
        // PDF" sample for exactly that shape.
        //
        // Only PDF is supported (via SkiaSharp's own SKDocument) — there's
        // no SVG *writer* available in this project's SkiaSharp packages
        // (SkiaSharp.Extended.Svg, used elsewhere for LoadShape(), only
        // reads SVGs). BeginRecord()/EndRecord() are plain aliases: real
        // Processing distinguishes them (RAW is for a single 3D frame,
        // RECORD is more general 2D/3D recording), but here they're the same
        // mechanism, so `renderer` is accepted for API parity and ignored.
        // =====================================================================

        private SKDocument? _rawDocument;
        private SKWStream? _rawStream;
        private SKCanvas? _rawSavedCanvas;

        /// <summary>Whether a BeginRaw()/BeginRecord() export is currently in progress.</summary>
        public bool IsRecording => _rawDocument != null;

        /// <summary>Starts exporting every subsequent draw call to a vector PDF at `path`, like Processing's beginRaw(PDF, filename). Call EndRaw() to finish and write the file.</summary>
        public void BeginRaw(string path)
        {
            EnsureReady();
            if (_rawDocument != null)
                throw new InvalidOperationException("BeginRaw()/BeginRecord() ya está activo — llama EndRaw()/EndRecord() antes de empezar una grabación nueva.");

            _rawStream = new SKFileWStream(path);
            _rawDocument = SKDocument.CreatePdf(_rawStream);
            var pageCanvas = _rawDocument.BeginPage(Width, Height);

            _rawSavedCanvas = Canvas;
            Canvas = pageCanvas; // A partir de acá, todo lo que dibuje el sketch va a la página PDF, no a pantalla.
        }

        /// <summary>Stops the current BeginRaw() export, writes the PDF to disk, and restores normal on-screen drawing.</summary>
        public void EndRaw()
        {
            if (_rawDocument == null)
                throw new InvalidOperationException("EndRaw()/EndRecord() llamado sin un BeginRaw()/BeginRecord() activo.");

            _rawDocument.EndPage();
            _rawDocument.Close();
            _rawDocument.Dispose();
            _rawStream!.Dispose();

            Canvas = _rawSavedCanvas!;

            _rawDocument = null;
            _rawStream = null;
            _rawSavedCanvas = null;
        }

        /// <summary>Alias of BeginRaw(path), like Processing's beginRecord(renderer, filename). `renderer` is accepted for API parity but ignored — see the type-level remarks above.</summary>
        public void BeginRecord(string renderer, string path) => BeginRaw(path);

        /// <summary>Alias of EndRaw(), like Processing's endRecord().</summary>
        public void EndRecord() => EndRaw();
    }
}
