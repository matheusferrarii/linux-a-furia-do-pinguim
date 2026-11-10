using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LinuxFuriaPinguim.EditorTools
{
    /// <summary>
    /// A arte gerada (atlas de pranchas de madeira) veio como JPG com um fundo em
    /// xadrez cinza/branco "achatado" (sem canal alfa real). Esta ferramenta remove
    /// esse fundo por conectividade (flood-fill a partir das bordas da imagem, nao por
    /// cor global - assim a neve branca DENTRO das pranchas nunca e afetada, so o que
    /// esta conectado a borda externa) e recorta cada prancha como um sprite separado.
    /// </summary>
    public static class WoodPlankAtlasProcessor
    {
        private const string SourcePath = "Assets/UI/Sprites/WoodPlankAtlas_Source.jpg";
        private const string OutputPath = "Assets/UI/Sprites/WoodPlankAtlas.png";

        [MenuItem("Tools/Furia Pinguim/1 - Remove Checker Background")]
        public static void RemoveCheckerBackground()
        {
            TextureImporter importer = AssetImporter.GetAtPath(SourcePath) as TextureImporter;
            if (importer == null) {
                Debug.LogError("WoodPlankAtlasProcessor: fonte nao encontrada em " + SourcePath);
                return;
            }
            importer.isReadable = true;
            importer.textureType = TextureImporterType.Default;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 4096;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();

            Texture2D source = AssetDatabase.LoadAssetAtPath<Texture2D>(SourcePath);
            int width = source.width;
            int height = source.height;
            Color32[] pixels = source.GetPixels32();

            bool[] isBackground = new bool[pixels.Length];
            Queue<int> queue = new Queue<int>();

            void TrySeed(int x, int y)
            {
                int idx = y * width + x;
                if (!isBackground[idx] && IsBackgroundLike(pixels[idx])) {
                    isBackground[idx] = true;
                    queue.Enqueue(idx);
                }
            }

            for (int x = 0; x < width; x++) {
                TrySeed(x, 0);
                TrySeed(x, height - 1);
            }
            for (int y = 0; y < height; y++) {
                TrySeed(0, y);
                TrySeed(width - 1, y);
            }

            void TryExpand(int nx, int ny)
            {
                if (nx < 0 || nx >= width || ny < 0 || ny >= height) { return; }
                int nIdx = ny * width + nx;
                if (isBackground[nIdx]) { return; }
                if (IsBackgroundLike(pixels[nIdx])) {
                    isBackground[nIdx] = true;
                    queue.Enqueue(nIdx);
                }
            }

            while (queue.Count > 0) {
                int idx = queue.Dequeue();
                int x = idx % width;
                int y = idx / width;
                TryExpand(x - 1, y);
                TryExpand(x + 1, y);
                TryExpand(x, y - 1);
                TryExpand(x, y + 1);
            }

            int cleared = 0;
            for (int i = 0; i < pixels.Length; i++) {
                if (isBackground[i]) {
                    pixels[i] = new Color32(0, 0, 0, 0);
                    cleared++;
                }
            }

            Texture2D output = new Texture2D(width, height, TextureFormat.RGBA32, false);
            output.SetPixels32(pixels);
            output.Apply();

            File.WriteAllBytes(OutputPath, output.EncodeToPNG());
            AssetDatabase.ImportAsset(OutputPath, ImportAssetOptions.ForceSynchronousImport);

            Debug.Log("WoodPlankAtlasProcessor: fundo removido (" + cleared + " / " + pixels.Length +
                " pixels, " + (100f * cleared / pixels.Length).ToString("F1") + "%). PNG salvo em " + OutputPath);
        }

        private static bool IsBackgroundLike(Color32 c)
        {
            // O checker "achatado" desta arte usa cinza medio/escuro (nao branco), entao o
            // criterio e so dessaturacao (R~=G~=B). A protecao contra apagar neve/branco de
            // dentro das pranchas vem da conectividade do flood-fill (seed so pelas bordas),
            // nao deste teste de cor sozinho.
            int max = Mathf.Max(c.r, c.g, c.b);
            int min = Mathf.Min(c.r, c.g, c.b);
            return (max - min) < 20;
        }

        [MenuItem("Tools/Furia Pinguim/2 - Slice Wood Plank Sprites")]
        public static void SliceSprites()
        {
            TextureImporter importer = AssetImporter.GetAtPath(OutputPath) as TextureImporter;
            if (importer == null) {
                Debug.LogError("WoodPlankAtlasProcessor: " + OutputPath + " nao encontrado - rode 'Remove Checker Background' primeiro.");
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.isReadable = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 4096;
            // Commit base settings first and re-fetch the texture so width/height below reflect
            // the REAL reimported size, not a stale value from a previous import pass.
            importer.SaveAndReimport();

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(OutputPath);
            int w = texture.width;
            int h = texture.height;

            // Coordenadas medidas por pixel via canal alfa (Tools > Furia Pinguim > 0 - Detect
            // Row Bands), nao mais estimadas a olho - veja o log dessa ferramenta se precisar reajustar.
            List<SpriteMetaData> sprites = new List<SpriteMetaData>();
            AddSprite(sprites, "Btn_Play", FracRect(0.02f, 0.047f, 0.35f, 0.202f, w, h));
            AddSprite(sprites, "Btn_Options", FracRect(0.02f, 0.283f, 0.35f, 0.202f, w, h));
            AddSprite(sprites, "Btn_Extras", FracRect(0.02f, 0.514f, 0.35f, 0.203f, w, h));
            AddSprite(sprites, "Btn_Quit", FracRect(0.02f, 0.742f, 0.35f, 0.204f, w, h));

            AddSprite(sprites, "Panel_Blank_1", FracRect(0.37f, 0.047f, 0.35f, 0.202f, w, h));
            AddSprite(sprites, "Panel_Blank_2", FracRect(0.37f, 0.283f, 0.35f, 0.202f, w, h));
            AddSprite(sprites, "Panel_Blank_3", FracRect(0.37f, 0.514f, 0.35f, 0.203f, w, h));

            AddSprite(sprites, "Panel_Large_Plain", FracRect(0.72f, 0.063f, 0.27f, 0.4f, w, h));
            AddSprite(sprites, "Panel_Large_Icy", FracRect(0.72f, 0.4847f, 0.27f, 0.4654f, w, h));

#pragma warning disable 0618
            importer.spritesheet = sprites.ToArray();
#pragma warning restore 0618
            importer.SaveAndReimport();

            Debug.Log("WoodPlankAtlasProcessor: " + sprites.Count + " sprites recortados em " + OutputPath);
        }

        private const string IndividualSpritesDir = "Assets/UI/Sprites/Buttons";

        /// <summary>
        /// Em vez de usar o sistema multi-sprite do Unity (TextureImporter.spritesheet se
        /// comportou de forma inconsistente/com tamanho de textura "inflado" entre reimports),
        /// detecta cada prancha automaticamente por conectividade de pixels (ilhas isoladas de
        /// alfa>0, ja que o fundo xadrez foi removido) e salva cada uma como PNG individual,
        /// importado como Sprite simples (Single) - sem nenhuma coordenada manual.
        /// </summary>
        [MenuItem("Tools/Furia Pinguim/2 - Auto-Slice Into Individual Sprites")]
        public static void AutoSliceIntoIndividualSprites()
        {
            TextureImporter importer = AssetImporter.GetAtPath(OutputPath) as TextureImporter;
            if (importer == null) {
                Debug.LogError("WoodPlankAtlasProcessor: " + OutputPath + " nao encontrado - rode 'Remove Checker Background' primeiro.");
                return;
            }
            importer.textureType = TextureImporterType.Default;
            importer.isReadable = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();

            Texture2D source = AssetDatabase.LoadAssetAtPath<Texture2D>(OutputPath);
            int w = source.width;
            int h = source.height;
            Color32[] pixels = source.GetPixels32();
            Debug.Log("WoodPlankAtlasProcessor: lendo atlas como " + w + "x" + h + " (" + pixels.Length + " pixels).");

            int[] label = new int[pixels.Length];
            List<RectInt> islands = new List<RectInt>();

            for (int i = 0; i < pixels.Length; i++) {
                if (label[i] != 0 || pixels[i].a <= 10) { continue; }

                int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
                int count = 0;
                Queue<int> queue = new Queue<int>();
                label[i] = 1;
                queue.Enqueue(i);

                while (queue.Count > 0) {
                    int idx = queue.Dequeue();
                    int x = idx % w;
                    int y = idx / w;
                    if (x < minX) { minX = x; }
                    if (x > maxX) { maxX = x; }
                    if (y < minY) { minY = y; }
                    if (y > maxY) { maxY = y; }
                    count++;

                    TryAdd(x - 1, y);
                    TryAdd(x + 1, y);
                    TryAdd(x, y - 1);
                    TryAdd(x, y + 1);

                    void TryAdd(int nx, int ny)
                    {
                        if (nx < 0 || nx >= w || ny < 0 || ny >= h) { return; }
                        int nIdx = ny * w + nx;
                        if (label[nIdx] != 0 || pixels[nIdx].a <= 10) { return; }
                        label[nIdx] = 1;
                        queue.Enqueue(nIdx);
                    }
                }

                if (count > 3000) {
                    islands.Add(new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1));
                }
            }

            Debug.Log("WoodPlankAtlasProcessor: " + islands.Count + " ilhas detectadas.");

            // Agrupa por coluna (terco esquerdo/meio/direito da imagem) e ordena de cima pra
            // baixo dentro de cada coluna (y maior = mais pra cima, pois pixels[] e bottom-up).
            List<RectInt> col0 = new List<RectInt>();
            List<RectInt> col1 = new List<RectInt>();
            List<RectInt> col2 = new List<RectInt>();
            foreach (RectInt island in islands) {
                float centerXFrac = (island.x + island.width * 0.5f) / w;
                if (centerXFrac < 0.34f) { col0.Add(island); }
                else if (centerXFrac < 0.67f) { col1.Add(island); }
                else { col2.Add(island); }
            }
            col0.Sort((a, b) => b.y.CompareTo(a.y));
            col1.Sort((a, b) => b.y.CompareTo(a.y));
            col2.Sort((a, b) => b.y.CompareTo(a.y));

            Debug.Log("WoodPlankAtlasProcessor: col0=" + col0.Count + " col1=" + col1.Count + " col2=" + col2.Count);

            Directory.CreateDirectory(IndividualSpritesDir);

            string[] col0Names = { "Btn_Play", "Btn_Options", "Btn_Extras", "Btn_Quit" };
            string[] col1Names = { "Panel_Blank_1", "Panel_Blank_2", "Panel_Blank_3" };
            string[] col2Names = { "Panel_Large_Plain", "Panel_Large_Icy" };

            SaveNamedCrops(col0, col0Names, pixels, w, h);
            SaveNamedCrops(col1, col1Names, pixels, w, h);
            SaveNamedCrops(col2, col2Names, pixels, w, h);

            AssetDatabase.Refresh();

            foreach (string path in Directory.GetFiles(IndividualSpritesDir, "*.png")) {
                string assetPath = path.Replace('\\', '/');
                TextureImporter spriteImporter = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (spriteImporter == null) { continue; }
                spriteImporter.textureType = TextureImporterType.Sprite;
                spriteImporter.spriteImportMode = SpriteImportMode.Single;
                spriteImporter.alphaIsTransparency = true;
                spriteImporter.mipmapEnabled = false;
                spriteImporter.spritePixelsPerUnit = 100f;
                TextureImporterSettings settings = new TextureImporterSettings();
                spriteImporter.ReadTextureSettings(settings);
                settings.spriteBorder = EstimateBorder(assetPath);
                spriteImporter.SetTextureSettings(settings);
                spriteImporter.SaveAndReimport();
            }

            Debug.Log("WoodPlankAtlasProcessor: sprites individuais salvos em " + IndividualSpritesDir);
        }

        private static void SaveNamedCrops(List<RectInt> islandsInColumn, string[] names, Color32[] pixels, int w, int h)
        {
            if (islandsInColumn.Count != names.Length) {
                Debug.LogWarning("WoodPlankAtlasProcessor: esperava " + names.Length + " ilhas nesta coluna, encontrei " +
                    islandsInColumn.Count + " - confira " + IndividualSpritesDir + " manualmente.");
            }
            int n = Mathf.Min(islandsInColumn.Count, names.Length);
            for (int i = 0; i < n; i++) {
                RectInt r = islandsInColumn[i];
                const int pad = 4;
                int x0 = Mathf.Max(0, r.x - pad);
                int y0 = Mathf.Max(0, r.y - pad);
                int x1 = Mathf.Min(w - 1, r.x + r.width - 1 + pad);
                int y1 = Mathf.Min(h - 1, r.y + r.height - 1 + pad);
                int cw = x1 - x0 + 1;
                int ch = y1 - y0 + 1;

                Color32[] crop = new Color32[cw * ch];
                for (int y = 0; y < ch; y++) {
                    for (int x = 0; x < cw; x++) {
                        crop[y * cw + x] = pixels[(y0 + y) * w + (x0 + x)];
                    }
                }

                Texture2D tex = new Texture2D(cw, ch, TextureFormat.RGBA32, false);
                tex.SetPixels32(crop);
                tex.Apply();
                File.WriteAllBytes(IndividualSpritesDir + "/" + names[i] + ".png", tex.EncodeToPNG());
            }
        }

        private static Vector4 EstimateBorder(string assetPath)
        {
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            float w = tex != null ? tex.width : 300f;
            float h = tex != null ? tex.height : 100f;
            return new Vector4(w * 0.18f, h * 0.12f, w * 0.18f, h * 0.32f);
        }

        [MenuItem("Tools/Furia Pinguim/0 - Detect Row Bands (debug)")]
        public static void DetectRowBands()
        {
            TextureImporter importer = AssetImporter.GetAtPath(OutputPath) as TextureImporter;
            if (importer != null) {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }

            Texture2D source = AssetDatabase.LoadAssetAtPath<Texture2D>(OutputPath);
            int w = source.width;
            int h = source.height;
            Color32[] pixels = source.GetPixels32();

            void ScanColumn(string label, float xFracStart, float xFracEnd)
            {
                int xStart = Mathf.RoundToInt(xFracStart * w);
                int xEnd = Mathf.RoundToInt(xFracEnd * w);

                bool wasOpaque = false;
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                sb.AppendLine("--- " + label + " (x " + xFracStart + "-" + xFracEnd + ") ---");
                for (int yTop = 0; yTop < h; yTop++) {
                    int y = h - 1 - yTop; // pixels[] is bottom-up; walk top-to-bottom for readability
                    bool opaque = false;
                    for (int x = xStart; x < xEnd; x++) {
                        if (pixels[y * w + x].a > 10) { opaque = true; break; }
                    }
                    if (opaque != wasOpaque) {
                        float frac = (float)yTop / h;
                        sb.AppendLine((opaque ? "START" : "END") + " at yTopFrac=" + frac.ToString("F4") + " (yTop=" + yTop + ")");
                        wasOpaque = opaque;
                    }
                }
                Debug.Log(sb.ToString());
            }

            ScanColumn("Col1 (icon buttons)", 0.04f, 0.36f);
            ScanColumn("Col2 (blank plaques)", 0.395f, 0.705f);
            ScanColumn("Col3 (large panels)", 0.735f, 0.995f);
        }

        [MenuItem("Tools/Furia Pinguim/3 - Export Sliced Sprites For Review")]
        public static void ExportSlicedSpritesForReview()
        {
            Texture2D source = AssetDatabase.LoadAssetAtPath<Texture2D>(OutputPath);
            if (source == null) {
                Debug.LogError("WoodPlankAtlasProcessor: " + OutputPath + " nao encontrado.");
                return;
            }

            string reviewDir = "Assets/UI/Sprites/_SliceReview";
            Directory.CreateDirectory(reviewDir);

            Color32[] sourcePixels = source.GetPixels32();
            int srcW = source.width;
            int srcH = source.height;

            foreach (Object asset in AssetDatabase.LoadAllAssetRepresentationsAtPath(OutputPath)) {
                if (asset is not Sprite sprite) { continue; }

                Rect r = sprite.rect;
                int rx = Mathf.RoundToInt(r.x);
                int ry = Mathf.RoundToInt(r.y);
                int rw = Mathf.RoundToInt(r.width);
                int rh = Mathf.RoundToInt(r.height);

                Texture2D crop = new Texture2D(rw, rh, TextureFormat.RGBA32, false);
                Color32[] cropPixels = new Color32[rw * rh];
                for (int y = 0; y < rh; y++) {
                    for (int x = 0; x < rw; x++) {
                        int sx = rx + x;
                        int sy = ry + y;
                        cropPixels[y * rw + x] = (sx >= 0 && sx < srcW && sy >= 0 && sy < srcH)
                            ? sourcePixels[sy * srcW + sx]
                            : new Color32(255, 0, 255, 255);
                    }
                }
                crop.SetPixels32(cropPixels);
                crop.Apply();

                File.WriteAllBytes(reviewDir + "/" + sprite.name + ".png", crop.EncodeToPNG());
            }

            AssetDatabase.Refresh();
            Debug.Log("WoodPlankAtlasProcessor: previews salvos em " + reviewDir);
        }

        private static Rect FracRect(float xFrac, float yFracFromTop, float wFrac, float hFrac, int texWidth, int texHeight)
        {
            float x = xFrac * texWidth;
            float w = wFrac * texWidth;
            float yFromTop = yFracFromTop * texHeight;
            float h = hFrac * texHeight;
            float yFromBottom = texHeight - yFromTop - h;
            return new Rect(x, yFromBottom, w, h);
        }

        private static void AddSprite(List<SpriteMetaData> list, string name, Rect rect)
        {
            SpriteMetaData meta = new SpriteMetaData {
                name = name,
                rect = rect,
                alignment = (int)SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f),
                border = new Vector4(rect.width * 0.18f, rect.height * 0.12f, rect.width * 0.18f, rect.height * 0.3f),
            };
            list.Add(meta);
        }
    }
}
