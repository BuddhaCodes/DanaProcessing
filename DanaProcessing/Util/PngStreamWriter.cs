using System.Buffers.Binary;
using System.IO.Compression;

namespace DanaProcessing
{
    /// <summary>
    /// Writes a PNG row band by row band, so an image far bigger than memory
    /// would allow in one piece (a 16000-pixel poster) streams straight to disk:
    /// only one band of pixels exists at a time. RGBA, 8 bits per channel,
    /// non-premultiplied rows; each row uses PNG's "Sub" filter.
    /// </summary>
    internal sealed class PngStreamWriter : IDisposable
    {
        private readonly Stream _output;
        private readonly int _width;
        private readonly int _height;
        private readonly IdatStream _idat;
        private readonly ZLibStream _zlib;
        private readonly byte[] _row;
        private int _rowsWritten;
        private bool _finished;

        public PngStreamWriter(Stream output, int width, int height, IEnumerable<KeyValuePair<string, string>>? text = null)
        {
            if (width <= 0 || height <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), "PNG size must be positive.");
            _output = output;
            _width = width;
            _height = height;
            _row = new byte[1 + width * 4];

            output.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
            Span<byte> ihdr = stackalloc byte[13];
            BinaryPrimitives.WriteInt32BigEndian(ihdr, width);
            BinaryPrimitives.WriteInt32BigEndian(ihdr[4..], height);
            ihdr[8] = 8;  // bit depth
            ihdr[9] = 6;  // colour type: RGBA
            ihdr[10] = 0; // deflate
            ihdr[11] = 0; // adaptive filtering
            ihdr[12] = 0; // no interlace
            PngText.WriteChunk(output, "IHDR", ihdr);
            if (text != null)
                foreach (var (key, value) in text)
                    PngText.WriteChunk(output, "iTXt", PngText.ITxtData(key, value));

            _idat = new IdatStream(output);
            _zlib = new ZLibStream(_idat, CompressionLevel.Optimal, leaveOpen: true);
        }

        /// <summary>Appends <paramref name="rows"/> rows of non-premultiplied RGBA pixels (width * 4 bytes each).</summary>
        public void WriteRows(ReadOnlySpan<byte> rgba, int rows)
        {
            int stride = _width * 4;
            if (rgba.Length < rows * stride)
                throw new ArgumentException("Not enough pixel data for that many rows.", nameof(rgba));
            if (_rowsWritten + rows > _height)
                throw new InvalidOperationException("More rows than the image is tall.");

            for (int r = 0; r < rows; r++)
            {
                var src = rgba.Slice(r * stride, stride);
                _row[0] = 1; // Sub: each byte minus the same channel of the pixel to its left
                for (int i = 0; i < stride; i++)
                    _row[1 + i] = (byte)(src[i] - (i >= 4 ? src[i - 4] : 0));
                _zlib.Write(_row);
            }
            _rowsWritten += rows;
        }

        public void Dispose()
        {
            if (_finished)
                return;
            _finished = true;
            if (_rowsWritten != _height)
                throw new InvalidOperationException($"PNG is {_height} rows tall but only {_rowsWritten} were written.");
            _zlib.Dispose();
            _idat.Flush();
            PngText.WriteChunk(_output, "IEND", ReadOnlySpan<byte>.Empty);
            _output.Flush();
        }

        /// <summary>Collects compressed bytes and writes them out as IDAT chunks of up to 64 KB.</summary>
        private sealed class IdatStream : Stream
        {
            private readonly Stream _output;
            private readonly byte[] _buffer = new byte[64 * 1024];
            private int _used;

            public IdatStream(Stream output) => _output = output;

            public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

            public override void Write(ReadOnlySpan<byte> data)
            {
                while (data.Length > 0)
                {
                    int n = Math.Min(data.Length, _buffer.Length - _used);
                    data[..n].CopyTo(_buffer.AsSpan(_used));
                    _used += n;
                    data = data[n..];
                    if (_used == _buffer.Length)
                        Flush();
                }
            }

            public override void Flush()
            {
                if (_used == 0)
                    return;
                PngText.WriteChunk(_output, "IDAT", _buffer.AsSpan(0, _used));
                _used = 0;
            }

            public override bool CanRead => false;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
        }
    }
}
