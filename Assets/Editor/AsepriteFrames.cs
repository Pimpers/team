using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

// Turns an .aseprite file into per-animation sprite strips with ONE shared pivot.
//
// Why: the exported Meow-Knight_*.png sheets were trimmed frame by frame (and Unity's
// auto-slice split the sword FX into separate sprites), so where the body sits inside a
// frame was lost - the character slid around during attacks and needed guessed offsets.
// The .aseprite file still has every frame on the same 80x80 canvas, so here each frame is
// composited from its visible layers (sword trails included), cut to its own pixels plus a
// 1px border, and given a custom pivot at the same canvas point: the centre of Idle frame 0.
// Every frame then lines up exactly, and flipX mirrors around the body centre.
public static class AsepriteFrames
{
    public const int PixelsPerUnit = 100;

    // Writes <outDir>/<prefix>_<Tag>.png for every animation tag and returns the sprites
    // of each tag in frame order. `skipLayer(tag, frameInTag, layerName)` can leave layers
    // out of single frames (e.g. an arrow already flying off - the game spawns a real one).
    public static Dictionary<string, Sprite[]> Build(string asePath, string outDir, string prefix, string pivotTag = "Idle",
        Func<string, int, string, bool> skipLayer = null)
    {
        var ase = new AseReader(asePath);
        if (!AssetDatabase.IsValidFolder(outDir))
        {
            string parent = Path.GetDirectoryName(outDir).Replace('\\', '/');
            AssetDatabase.CreateFolder(parent, Path.GetFileName(outDir));
        }

        int pivotTagIndex = ase.TagNames.IndexOf(pivotTag);
        if (pivotTagIndex < 0) throw new Exception($"{asePath}: no '{pivotTag}' tag");
        RectInt pr = Padded(ase, AseReader.Bounds(ase.Frames[ase.TagFrom[pivotTagIndex]], ase.Width, ase.Height));
        // Canvas point (top-down pixels) every frame pivots around.
        Vector2 pivot = new Vector2(pr.x + pr.width * 0.5f, pr.y + pr.height * 0.5f);

        var result = new Dictionary<string, Sprite[]>();
        int W = ase.Width, H = ase.Height;
        for (int t = 0; t < ase.TagNames.Count; t++)
        {
            string tag = ase.TagNames[t];
            int from = ase.TagFrom[t], count = ase.TagTo[t] - from + 1;
            string path = $"{outDir}/{prefix}_{tag}.png";

            // Strip of full canvases, left to right. Texture rows are bottom-up.
            var tex = new Texture2D(W * count, H, TextureFormat.RGBA32, false);
            var px = new Color32[W * count * H];
            var rects = new RectInt[count];
            for (int i = 0; i < count; i++)
            {
                int frameInTag = i;
                byte[] f = skipLayer == null
                    ? ase.Frames[from + i]
                    : ase.Composite(from + i, name => skipLayer(tag, frameInTag, name));
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        int s = (y * W + x) * 4;
                        px[(H - 1 - y) * W * count + i * W + x] = new Color32(f[s], f[s + 1], f[s + 2], f[s + 3]);
                    }
                rects[i] = Padded(ase, AseReader.Bounds(f, W, H));
            }
            // A strip that exists is kept: some were touched up by hand after the export (Knight_Attack_1 / 4,
            // Archer_Attack_2 / 4, Archer_Tag), and writing them again from the .ase undid that on every scene build.
            // Delete the PNG to export it afresh.
            if (!File.Exists(path))
            {
                tex.SetPixels32(px);
                File.WriteAllBytes(path, tex.EncodeToPNG());
            }
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Multiple;
            ti.spritePixelsPerUnit = PixelsPerUnit;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.SaveAndReimport();

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var dp = factory.GetSpriteEditorDataProviderFromObject(ti);
            dp.InitSpriteEditorDataProvider();
            // Keep sprite IDs stable across rebuilds so references to them survive.
            var oldIds = dp.GetSpriteRects().ToDictionary(r => r.name, r => r.spriteID);
            var spriteRects = new List<SpriteRect>();
            for (int i = 0; i < count; i++)
            {
                RectInt r = rects[i];
                string name = $"{prefix}_{tag}_{i}";
                spriteRects.Add(new SpriteRect
                {
                    name = name,
                    spriteID = oldIds.TryGetValue(name, out var id) ? id : GUID.Generate(),
                    // Unity rects are bottom-up.
                    rect = new Rect(i * W + r.x, H - (r.y + r.height), r.width, r.height),
                    alignment = SpriteAlignment.Custom,
                    pivot = new Vector2((pivot.x - r.x) / r.width, (r.y + r.height - pivot.y) / r.height),
                });
            }
            dp.SetSpriteRects(spriteRects.ToArray());
            var names = dp.GetDataProvider<ISpriteNameFileIdDataProvider>();
            names?.SetNameFileIdPairs(spriteRects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
            dp.Apply();
            ti.SaveAndReimport();

            var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
                .OrderBy(s => int.Parse(s.name.Substring(s.name.LastIndexOf('_') + 1))).ToArray();
            if (sprites.Length != count) throw new Exception($"{path}: expected {count} sprites, got {sprites.Length}");
            result[tag] = sprites;
        }
        return result;
    }

    // Tight bounds plus a 1px transparent border (matches the old sheets' frame size).
    static RectInt Padded(AseReader ase, RectInt b)
    {
        if (b.width == 0) return new RectInt(0, 0, 1, 1);
        int x0 = Mathf.Max(0, b.x - 1), y0 = Mathf.Max(0, b.y - 1);
        int x1 = Mathf.Min(ase.Width, b.xMax + 1), y1 = Mathf.Min(ase.Height, b.yMax + 1);
        return new RectInt(x0, y0, x1 - x0, y1 - y0);
    }

    // Distance in sprite units from the pivot to the frame's far edge in front (the sword
    // tip / effect edge when facing right), for sizing attack reach from the art itself.
    public static float ForwardReach(Sprite s) => (s.rect.width - 1f - s.pivot.x) / s.pixelsPerUnit;

    // ------------------------------------------------------------------ .aseprite reader
    // RGBA files, normal blend mode; hidden layers and the background layer are skipped.
    class AseReader
    {
        public int Width, Height, FrameCount;
        public readonly List<string> TagNames = new List<string>();
        public readonly List<int> TagFrom = new List<int>(), TagTo = new List<int>();
        public byte[][] Frames; // RGBA, top-down, Width*Height*4

        readonly List<int> layerFlags = new List<int>(), layerOpacity = new List<int>();
        readonly List<string> layerNames = new List<string>();

        class Cel { public int Layer, X, Y, Opacity, W, H, LinkFrame = -1; public byte[] Px; }

        public AseReader(string path)
        {
            byte[] b = File.ReadAllBytes(path);
            FrameCount = BitConverter.ToUInt16(b, 6);
            Width = BitConverter.ToUInt16(b, 8);
            Height = BitConverter.ToUInt16(b, 10);
            if (BitConverter.ToUInt16(b, 12) != 32) throw new Exception($"{path}: only RGBA .aseprite files are supported");

            var cels = new List<Cel>[FrameCount];
            int off = 128;
            for (int f = 0; f < FrameCount; f++)
            {
                cels[f] = new List<Cel>();
                int size = (int)BitConverter.ToUInt32(b, off);
                int oldN = BitConverter.ToUInt16(b, off + 6);
                int newN = (int)BitConverter.ToUInt32(b, off + 12);
                int n = newN != 0 ? newN : oldN;
                int c = off + 16;
                for (int k = 0; k < n; k++)
                {
                    int cs = (int)BitConverter.ToUInt32(b, c);
                    int type = BitConverter.ToUInt16(b, c + 4);
                    int d = c + 6;
                    if (type == 0x2004) // layer
                    {
                        layerFlags.Add(BitConverter.ToUInt16(b, d));
                        layerOpacity.Add(b[d + 12]);
                        layerNames.Add(Encoding.UTF8.GetString(b, d + 18, BitConverter.ToUInt16(b, d + 16)));
                    }
                    else if (type == 0x2005) // cel
                    {
                        var cel = new Cel
                        {
                            Layer = BitConverter.ToUInt16(b, d),
                            X = BitConverter.ToInt16(b, d + 2),
                            Y = BitConverter.ToInt16(b, d + 4),
                            Opacity = b[d + 6],
                        };
                        int celType = BitConverter.ToUInt16(b, d + 7);
                        int p = d + 16;
                        if (celType == 1) cel.LinkFrame = BitConverter.ToUInt16(b, p);
                        else if (celType == 0 || celType == 2)
                        {
                            cel.W = BitConverter.ToUInt16(b, p);
                            cel.H = BitConverter.ToUInt16(b, p + 2);
                            int start = p + 4, len = c + cs - start;
                            cel.Px = new byte[cel.W * cel.H * 4];
                            if (celType == 0) Array.Copy(b, start, cel.Px, 0, cel.Px.Length);
                            else
                            {
                                // zlib stream: skip the 2-byte header, then raw deflate.
                                using (var ms = new MemoryStream(b, start + 2, len - 2))
                                using (var ds = new DeflateStream(ms, CompressionMode.Decompress))
                                {
                                    int read = 0;
                                    while (read < cel.Px.Length)
                                    {
                                        int r = ds.Read(cel.Px, read, cel.Px.Length - read);
                                        if (r <= 0) break;
                                        read += r;
                                    }
                                }
                            }
                        }
                        else { c += cs; continue; }
                        cels[f].Add(cel);
                    }
                    else if (type == 0x2018) // tags
                    {
                        int nt = BitConverter.ToUInt16(b, d);
                        int t = d + 10;
                        for (int j = 0; j < nt; j++)
                        {
                            TagFrom.Add(BitConverter.ToUInt16(b, t));
                            TagTo.Add(BitConverter.ToUInt16(b, t + 2));
                            int sl = BitConverter.ToUInt16(b, t + 17);
                            TagNames.Add(Encoding.UTF8.GetString(b, t + 19, sl));
                            t += 19 + sl;
                        }
                    }
                    c += cs;
                }
                off += size;
            }

            this.cels = cels;
            Frames = new byte[FrameCount][];
            for (int f = 0; f < FrameCount; f++) Frames[f] = Composite(f, null);
        }

        List<Cel>[] cels;

        // All visible, non-background layers of a frame blended onto the canvas, minus any
        // layer `skip` returns true for (by layer name).
        public byte[] Composite(int f, Func<string, bool> skip)
        {
            var dst = new byte[Width * Height * 4];
            foreach (var cel0 in cels[f].OrderBy(c => c.Layer))
            {
                var cel = cel0;
                if (cel.LinkFrame >= 0)
                {
                    cel = cels[cel0.LinkFrame].FirstOrDefault(lc => lc.Layer == cel0.Layer && lc.LinkFrame < 0);
                    if (cel == null) continue;
                }
                int L = cel.Layer;
                if ((layerFlags[L] & 1) == 0 || (layerFlags[L] & 16) != 0) continue; // hidden / background
                if (skip != null && skip(layerNames[L])) continue;
                float op = layerOpacity[L] / 255f * (cel.Opacity / 255f);
                for (int y = 0; y < cel.H; y++)
                {
                    int cy = cel.Y + y;
                    if (cy < 0 || cy >= Height) continue;
                    for (int x = 0; x < cel.W; x++)
                    {
                        int cx = cel.X + x;
                        if (cx < 0 || cx >= Width) continue;
                        int si = (y * cel.W + x) * 4, di = (cy * Width + cx) * 4;
                        float sa = cel.Px[si + 3] / 255f * op;
                        if (sa <= 0f) continue;
                        float da = dst[di + 3] / 255f;
                        float oa = sa + da * (1f - sa);
                        for (int ch = 0; ch < 3; ch++)
                            dst[di + ch] = (byte)Mathf.RoundToInt((cel.Px[si + ch] * sa + dst[di + ch] * da * (1f - sa)) / oa);
                        dst[di + 3] = (byte)Mathf.RoundToInt(oa * 255f);
                    }
                }
            }
            return dst;
        }

        // Tight bounds of a frame's visible pixels, top-down canvas coordinates.
        public static RectInt Bounds(byte[] px, int width, int height)
        {
            int minX = width, minY = height, maxX = -1, maxY = -1;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    if (px[(y * width + x) * 4 + 3] > 0)
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
            return maxX < 0 ? new RectInt(0, 0, 0, 0) : new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }
    }
}
