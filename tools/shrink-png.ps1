# Makes screenshots smaller: each PNG becomes a 256-color palette PNG (median cut, no dithering),
# which suits interface screenshots with flat colors and text. A file is replaced only when the result
# is smaller and no pixel moves further than -MaxError (RGB distance) from its original color.
param([Parameter(Mandatory = $true)][string[]]$Path, [int]$MaxError = 72)
$ErrorActionPreference = 'Stop'
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System; using System.Collections.Generic; using System.Drawing; using System.Drawing.Imaging; using System.IO; using System.Linq; using System.Runtime.InteropServices;
public static class PaletteShrink
{
    sealed class Box { public List<KeyValuePair<int, int>> Colors; }
    static int R(int c) { return (c >> 16) & 255; } static int G(int c) { return (c >> 8) & 255; } static int B(int c) { return c & 255; }
    static int Channel(int c, int axis) { return axis == 0 ? R(c) : axis == 1 ? G(c) : B(c); }
    static int Range(Box b, int axis) { int lo = 255, hi = 0; foreach (var p in b.Colors) { int v = Channel(p.Key, axis); if (v < lo) lo = v; if (v > hi) hi = v; } return hi - lo; }

    // Returns the new size in bytes and the largest color error, or -1 when the file is left alone.
    public static long[] Shrink(string path, int maxError)
    {
        long before = new FileInfo(path).Length;
        int width, height; int[] pixels;
        using (var source = new Bitmap(path))
        using (var argb = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb))
        {
            using (var g = Graphics.FromImage(argb)) g.DrawImage(source, 0, 0, source.Width, source.Height);
            width = argb.Width; height = argb.Height; pixels = new int[width * height];
            var data = argb.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            for (int y = 0; y < height; y++) Marshal.Copy(data.Scan0 + y * data.Stride, pixels, y * width, width);
            argb.UnlockBits(data);
        }
        if (pixels.Any(p => ((p >> 24) & 255) != 255)) return new long[] { -1, 0 };   // keep transparent images as they are
        var counts = new Dictionary<int, int>();
        foreach (int p in pixels) { int c = p & 0xFFFFFF; int n; counts.TryGetValue(c, out n); counts[c] = n + 1; }
        var boxes = new List<Box> { new Box { Colors = counts.ToList() } };
        while (boxes.Count < 256)
        {
            Box widest = null; int axis = 0; long score = -1;
            foreach (var b in boxes)
            {
                if (b.Colors.Count < 2) continue;
                for (int a = 0; a < 3; a++) { long s = (long)Range(b, a) * b.Colors.Sum(p => (long)p.Value); if (s > score) { score = s; widest = b; axis = a; } }
            }
            if (widest == null || score <= 0) break;
            var sorted = widest.Colors.OrderBy(p => Channel(p.Key, axis)).ToList();
            long total = sorted.Sum(p => (long)p.Value), half = 0; int cut = 0;
            while (cut < sorted.Count - 1 && half + sorted[cut].Value <= total / 2) { half += sorted[cut].Value; cut++; }
            if (cut == 0) cut = 1;
            boxes.Remove(widest);
            boxes.Add(new Box { Colors = sorted.Take(cut).ToList() }); boxes.Add(new Box { Colors = sorted.Skip(cut).ToList() });
        }
        var palette = boxes.Select(b => { long n = b.Colors.Sum(p => (long)p.Value); return Color.FromArgb((int)(b.Colors.Sum(p => (long)R(p.Key) * p.Value) / n), (int)(b.Colors.Sum(p => (long)G(p.Key) * p.Value) / n), (int)(b.Colors.Sum(p => (long)B(p.Key) * p.Value) / n)); }).ToList();
        var index = new Dictionary<int, byte>(); int worst = 0;
        foreach (int c in counts.Keys)
        {
            int best = 0, bestDistance = int.MaxValue;
            for (int i = 0; i < palette.Count; i++) { int dr = R(c) - palette[i].R, dg = G(c) - palette[i].G, db = B(c) - palette[i].B; int d = dr * dr + dg * dg + db * db; if (d < bestDistance) { bestDistance = d; best = i; } }
            index[c] = (byte)best; worst = Math.Max(worst, (int)Math.Sqrt(bestDistance));
        }
        if (worst > maxError) return new long[] { -1, worst };
        string temp = path + ".tmp.png";
        using (var indexed = new Bitmap(width, height, PixelFormat.Format8bppIndexed))
        {
            var pal = indexed.Palette; for (int i = 0; i < 256; i++) pal.Entries[i] = i < palette.Count ? palette[i] : Color.Black; indexed.Palette = pal;
            var data = indexed.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format8bppIndexed);
            var row = new byte[data.Stride];
            for (int y = 0; y < height; y++) { for (int x = 0; x < width; x++) row[x] = index[pixels[y * width + x] & 0xFFFFFF]; Marshal.Copy(row, 0, data.Scan0 + y * data.Stride, data.Stride); }
            indexed.UnlockBits(data);
            indexed.Save(temp, ImageFormat.Png);
        }
        long after = new FileInfo(temp).Length;
        if (after >= before) { File.Delete(temp); return new long[] { -1, worst }; }
        File.Copy(temp, path, true); File.Delete(temp);
        return new long[] { after, worst };
    }
}
'@
$files = foreach ($p in $Path) { if (Test-Path -LiteralPath $p -PathType Container) { Get-ChildItem -LiteralPath $p -Filter *.png -Recurse | ForEach-Object FullName } else { (Resolve-Path -LiteralPath $p).Path } }
$saved = 0L
foreach ($file in $files) {
    $before = (Get-Item -LiteralPath $file).Length
    $result = [PaletteShrink]::Shrink($file, $MaxError)
    if ($result[0] -lt 0) { Write-Output ('kept    {0}  (largest error {1})' -f (Split-Path -Leaf $file), $result[1]); continue }
    $saved += $before - $result[0]
    Write-Output ('{0,5:N0} -> {1,5:N0} KB  {2}  (largest error {3})' -f ($before / 1KB), ($result[0] / 1KB), (Split-Path -Leaf $file), $result[1])
}
Write-Output ('saved {0:N0} KB' -f ($saved / 1KB))
