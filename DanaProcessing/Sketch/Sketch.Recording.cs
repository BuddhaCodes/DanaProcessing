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
        // The file's extension picks the format: .pdf (SkiaSharp's SKDocument)
        // or .svg (SKSvgCanvas), like Processing's PDF and SVG renderers.
        // An SVG can also come out plotter-ready (plotter: true): one group per
        // pen color and the strokes reordered so the pen travels as little as
        // possible in the air -- see PlotterSvg. BeginRecord()/EndRecord() are plain aliases: real
        // Processing distinguishes them (RAW is for a single 3D frame,
        // RECORD is more general 2D/3D recording), but here they're the same
        // mechanism, so `renderer` is accepted for API parity and ignored.
        // =====================================================================

        private SKDocument? _rawDocument;
        private SKWStream? _rawStream;
        private SKCanvas? _rawSavedCanvas;
        private SKCanvas? _rawSvgCanvas;
        private System.IO.MemoryStream? _rawSvgBuffer;
        private string? _rawPath;
        private bool _rawPlotter;

        /// <summary>Whether a BeginRaw()/BeginRecord() export is currently in progress.</summary>
        public bool IsRecording => _rawSavedCanvas != null;

        /// <summary>
        /// Starts exporting every subsequent draw call to a vector file at `path`, like Processing's
        /// beginRaw(PDF/SVG, filename): a <c>.svg</c> path writes an SVG, anything else a PDF. With
        /// <paramref name="plotter"/> (SVG only) the file comes out ready for a pen plotter: one
        /// group per stroke color (one pen each), fills as outlines, no background, and the strokes
        /// ordered so the pen barely travels in the air. Call EndRaw() to finish and write the file.
        /// </summary>
        public void BeginRaw(string path, bool plotter = false)
        {
            EnsureReady();
            if (IsRecording)
                throw new InvalidOperationException("BeginRaw()/BeginRecord() ya está activo — llama EndRaw()/EndRecord() antes de empezar una grabación nueva.");

            _rawPath = ResolveOutputPath(path);
            _rawPlotter = plotter;
            SKCanvas pageCanvas;
            if (_rawPath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
            {
                _rawSvgBuffer = new System.IO.MemoryStream();
                _rawSvgCanvas = SKSvgCanvas.Create(new SKRect(0, 0, Width, Height), _rawSvgBuffer);
                pageCanvas = _rawSvgCanvas;
            }
            else
            {
                if (plotter)
                    throw new ArgumentException("plotter: true es solo para archivos .svg.", nameof(plotter));
                _rawStream = new SKFileWStream(_rawPath);
                _rawDocument = SKDocument.CreatePdf(_rawStream);
                pageCanvas = _rawDocument.BeginPage(Width, Height);
            }

            _rawSavedCanvas = Canvas;
            Canvas = pageCanvas; // A partir de acá, todo lo que dibuje el sketch va al archivo, no a pantalla.
        }

        /// <summary>Stops the current BeginRaw() export, writes the PDF or SVG to disk, and restores normal on-screen drawing.</summary>
        public void EndRaw()
        {
            if (!IsRecording)
                throw new InvalidOperationException("EndRaw()/EndRecord() llamado sin un BeginRaw()/BeginRecord() activo.");

            if (_rawSvgCanvas != null)
            {
                _rawSvgCanvas.Dispose();                     // the SVG is only complete once its canvas is disposed
                string svg = System.Text.Encoding.UTF8.GetString(_rawSvgBuffer!.ToArray());
                if (_rawPlotter)
                    svg = PlotterSvg.Optimize(svg).Svg;
                System.IO.File.WriteAllText(_rawPath!, svg);
                _rawSvgBuffer.Dispose();
                _rawSvgCanvas = null;
                _rawSvgBuffer = null;
            }
            else
            {
                _rawDocument!.EndPage();
                _rawDocument.Close();
                _rawDocument.Dispose();
                _rawStream!.Dispose();
                _rawDocument = null;
                _rawStream = null;
            }

            Canvas = _rawSavedCanvas!;
            _rawSavedCanvas = null;
            _rawPath = null;
        }

        /// <summary>Alias of BeginRaw(path), like Processing's beginRecord(renderer, filename): <c>"svg"</c> (or a .svg path) records an SVG, anything else a PDF.</summary>
        public void BeginRecord(string renderer, string path) =>
            BeginRaw(renderer.Equals("svg", StringComparison.OrdinalIgnoreCase) && !path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) ? path + ".svg" : path);

        /// <summary>Alias of EndRaw(), like Processing's endRecord().</summary>
        public void EndRecord() => EndRaw();
    }
}
