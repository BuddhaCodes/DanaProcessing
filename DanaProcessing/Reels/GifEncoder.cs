namespace DanaProcessing.Reels
{
    /// <summary>
    /// Animated GIF writer, written from scratch so a reel can always be
    /// exported, even on a machine without ffmpeg. Each frame gets its own
    /// 256-color palette (median cut over a 15-bit histogram) -- code cards
    /// have few colors and the sketch part changes a lot, so per-frame
    /// palettes look much better than one global table.
    /// </summary>
    internal sealed class GifEncoder : IDisposable
    {
        private readonly Stream _out;
        private readonly int _w, _h;
        private readonly byte[] _indices;
        private byte[]? _prev;
        private int _rx, _ry, _rw, _rh; // region of the current frame that changed
        private readonly int[] _hist = new int[32768];
        private readonly byte[] _lut = new byte[32768];
        private readonly byte[] _palette = new byte[768];
        private bool _disposed;

        // LZW dictionary: key = (prefix << 8) | byte, direct-mapped, invalidated by generation.
        private readonly int[] _dictCode = new int[1 << 20];
        private readonly int[] _dictGen = new int[1 << 20];
        private int _gen = 1;

        public GifEncoder(Stream output, int width, int height)
        {
            _out = output;
            _w = width;
            _h = height;
            _indices = new byte[width * height];

            Write("GIF89a"u8);
            WriteShort(width);
            WriteShort(height);
            _out.WriteByte(0);   // no global color table
            _out.WriteByte(0);   // background
            _out.WriteByte(0);   // aspect
            // Loop forever (NETSCAPE2.0 application extension).
            Write(new byte[] { 0x21, 0xFF, 0x0B });
            Write("NETSCAPE2.0"u8);
            Write(new byte[] { 0x03, 0x01, 0x00, 0x00, 0x00 });
        }

        /// <summary>Adds one frame. <paramref name="rgba"/> is width*height*4 bytes, R,G,B,A; alpha is ignored. Delay is in 1/100 s.</summary>
        public void AddFrame(ReadOnlySpan<byte> rgba, int delayCentiseconds)
        {
            // Only the rectangle that changed since the last frame is stored
            // (the rest stays on screen: disposal "leave in place"). While code
            // is being typed that's one line of text -- a fraction of the frame.
            ChangedRegion(rgba);
            Quantize(rgba);

            // Graphic control extension: disposal "leave in place", delay.
            Write(new byte[] { 0x21, 0xF9, 0x04, 0x04 });
            WriteShort(Math.Max(2, delayCentiseconds));
            Write(new byte[] { 0x00, 0x00 });

            // Image descriptor with a 256-entry local color table.
            _out.WriteByte(0x2C);
            WriteShort(_rx); WriteShort(_ry); WriteShort(_rw); WriteShort(_rh);
            _out.WriteByte(0x87);
            _out.Write(_palette, 0, 768);

            _out.WriteByte(8); // LZW minimum code size
            Lzw(_rw * _rh);

            _prev ??= new byte[rgba.Length];
            rgba.CopyTo(_prev);
        }

        private void ChangedRegion(ReadOnlySpan<byte> rgba)
        {
            if (_prev == null)
            {
                _rx = 0; _ry = 0; _rw = _w; _rh = _h;
                return;
            }
            int minX = _w, minY = _h, maxX = -1, maxY = -1;
            int stride = _w * 4;
            for (int y = 0; y < _h; y++)
            {
                var a = rgba.Slice(y * stride, stride);
                var b = _prev.AsSpan(y * stride, stride);
                if (a.SequenceEqual(b))
                    continue;
                if (y < minY) minY = y;
                maxY = y;
                int first = 0;
                while (first < _w && a[first * 4] == b[first * 4] && a[first * 4 + 1] == b[first * 4 + 1] && a[first * 4 + 2] == b[first * 4 + 2]) first++;
                int last = _w - 1;
                while (last > first && a[last * 4] == b[last * 4] && a[last * 4 + 1] == b[last * 4 + 1] && a[last * 4 + 2] == b[last * 4 + 2]) last--;
                if (first < minX) minX = first;
                if (last > maxX) maxX = last;
            }
            if (maxY < 0 || maxX < minX)
            {
                // Nothing changed: a 1x1 frame just to carry the delay.
                _rx = 0; _ry = 0; _rw = 1; _rh = 1;
                return;
            }
            _rx = minX; _ry = minY; _rw = maxX - minX + 1; _rh = maxY - minY + 1;
        }

        // --- Color quantization ------------------------------------------------

        private struct Box { public int Start, End; }

        private void Quantize(ReadOnlySpan<byte> rgba)
        {
            Array.Clear(_hist);
            for (int y = 0; y < _rh; y++)
            {
                int row = ((_ry + y) * _w + _rx) * 4;
                for (int x = 0; x < _rw; x++)
                {
                    int p = row + x * 4;
                    _hist[((rgba[p] >> 3) << 10) | ((rgba[p + 1] >> 3) << 5) | (rgba[p + 2] >> 3)]++;
                }
            }

            var bins = new List<int>();
            for (int k = 0; k < 32768; k++)
                if (_hist[k] > 0) bins.Add(k);
            var arr = bins.ToArray();

            var boxes = new List<Box> { new Box { Start = 0, End = arr.Length } };
            while (boxes.Count < 256)
            {
                // Split the box with the widest channel range, weighted by how
                // many pixels it covers (so big flat areas don't hog the palette
                // but busy areas get their share).
                int best = -1;
                double bestScore = 0;
                int bestChannel = 0;
                for (int b = 0; b < boxes.Count; b++)
                {
                    var box = boxes[b];
                    if (box.End - box.Start < 2) continue;
                    Range(arr, box, out int ch, out int range, out long count);
                    double score = range * Math.Sqrt(count);
                    if (score > bestScore) { bestScore = score; best = b; bestChannel = ch; }
                }
                if (best < 0) break;

                var bx = boxes[best];
                int shift = bestChannel == 0 ? 10 : bestChannel == 1 ? 5 : 0;
                Array.Sort(arr, bx.Start, bx.End - bx.Start, Comparer<int>.Create((a, c) => ((a >> shift) & 31).CompareTo((c >> shift) & 31)));
                long total = 0;
                for (int i = bx.Start; i < bx.End; i++) total += _hist[arr[i]];
                long acc = 0;
                int split = bx.Start + 1;
                for (int i = bx.Start; i < bx.End - 1; i++)
                {
                    acc += _hist[arr[i]];
                    if (acc * 2 >= total) { split = i + 1; break; }
                    split = i + 2;
                }
                split = Math.Clamp(split, bx.Start + 1, bx.End - 1);
                boxes[best] = new Box { Start = bx.Start, End = split };
                boxes.Add(new Box { Start = split, End = bx.End });
            }

            Array.Clear(_palette);
            var pr = new int[boxes.Count];
            var pg = new int[boxes.Count];
            var pb = new int[boxes.Count];
            for (int b = 0; b < boxes.Count; b++)
            {
                long r = 0, g = 0, bl = 0, c = 0;
                for (int i = boxes[b].Start; i < boxes[b].End; i++)
                {
                    int k = arr[i];
                    long w = _hist[k];
                    r += (((k >> 10) & 31) * 255 / 31) * w;
                    g += (((k >> 5) & 31) * 255 / 31) * w;
                    bl += ((k & 31) * 255 / 31) * w;
                    c += w;
                }
                if (c == 0) c = 1;
                pr[b] = (int)(r / c); pg[b] = (int)(g / c); pb[b] = (int)(bl / c);
                _palette[b * 3] = (byte)pr[b];
                _palette[b * 3 + 1] = (byte)pg[b];
                _palette[b * 3 + 2] = (byte)pb[b];
            }

            // Nearest palette entry for every color actually present.
            foreach (int k in arr)
            {
                int r = ((k >> 10) & 31) * 255 / 31, g = ((k >> 5) & 31) * 255 / 31, bl = (k & 31) * 255 / 31;
                int bestI = 0, bestD = int.MaxValue;
                for (int p = 0; p < boxes.Count; p++)
                {
                    int dr = r - pr[p], dg = g - pg[p], db = bl - pb[p];
                    int d = dr * dr * 3 + dg * dg * 4 + db * db * 2;
                    if (d < bestD) { bestD = d; bestI = p; }
                }
                _lut[k] = (byte)bestI;
            }

            int i2 = 0;
            for (int y = 0; y < _rh; y++)
            {
                int row = ((_ry + y) * _w + _rx) * 4;
                for (int x = 0; x < _rw; x++)
                {
                    int p = row + x * 4;
                    _indices[i2++] = _lut[((rgba[p] >> 3) << 10) | ((rgba[p + 1] >> 3) << 5) | (rgba[p + 2] >> 3)];
                }
            }
        }

        private void Range(int[] arr, Box box, out int channel, out int range, out long count)
        {
            int rMin = 31, rMax = 0, gMin = 31, gMax = 0, bMin = 31, bMax = 0;
            count = 0;
            for (int i = box.Start; i < box.End; i++)
            {
                int k = arr[i];
                int r = (k >> 10) & 31, g = (k >> 5) & 31, b = k & 31;
                if (r < rMin) rMin = r; if (r > rMax) rMax = r;
                if (g < gMin) gMin = g; if (g > gMax) gMax = g;
                if (b < bMin) bMin = b; if (b > bMax) bMax = b;
                count += _hist[k];
            }
            int rr = rMax - rMin, gr = gMax - gMin, br = bMax - bMin;
            if (gr >= rr && gr >= br) { channel = 1; range = gr; }
            else if (rr >= br) { channel = 0; range = rr; }
            else { channel = 2; range = br; }
        }

        // --- LZW -----------------------------------------------------------------

        private readonly byte[] _block = new byte[256];
        private int _blockLen;
        private int _bitBuf, _bitCount;

        private void Lzw(int count)
        {
            const int clear = 256, eoi = 257;
            int codeSize = 9;
            int maxCode = eoi;
            _gen++;
            _bitBuf = 0; _bitCount = 0; _blockLen = 0;

            Emit(clear, codeSize);
            int cur = _indices[0];
            for (int i = 1; i < count; i++)
            {
                int k = _indices[i];
                int key = (cur << 8) | k;
                if (_dictGen[key] == _gen)
                {
                    cur = _dictCode[key];
                    continue;
                }

                Emit(cur, codeSize);
                maxCode++;
                _dictGen[key] = _gen;
                _dictCode[key] = maxCode;
                if (maxCode >= (1 << codeSize))
                    codeSize++;
                if (maxCode == 4095)
                {
                    Emit(clear, codeSize);
                    _gen++;
                    codeSize = 9;
                    maxCode = eoi;
                }
                cur = k;
            }
            Emit(cur, codeSize);
            Emit(eoi, codeSize);
            if (_bitCount > 0)
                PutByte((byte)(_bitBuf & 0xFF));
            FlushBlock();
            _out.WriteByte(0); // block terminator
        }

        private void Emit(int code, int size)
        {
            _bitBuf |= code << _bitCount;
            _bitCount += size;
            while (_bitCount >= 8)
            {
                PutByte((byte)(_bitBuf & 0xFF));
                _bitBuf >>= 8;
                _bitCount -= 8;
            }
        }

        private void PutByte(byte b)
        {
            _block[_blockLen++] = b;
            if (_blockLen == 255)
                FlushBlock();
        }

        private void FlushBlock()
        {
            if (_blockLen == 0) return;
            _out.WriteByte((byte)_blockLen);
            _out.Write(_block, 0, _blockLen);
            _blockLen = 0;
        }

        private void Write(ReadOnlySpan<byte> bytes) => _out.Write(bytes);

        private void WriteShort(int v)
        {
            _out.WriteByte((byte)(v & 0xFF));
            _out.WriteByte((byte)((v >> 8) & 0xFF));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _out.WriteByte(0x3B);
            _out.Flush();
        }
    }
}
