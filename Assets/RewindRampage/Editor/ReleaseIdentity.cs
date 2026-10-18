using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RewindRampage.Editor
{
    public static class ReleaseIdentity
    {
        private const string SourcePath = "Assets/RewindRampage/Art/ApplicationIconSource.png";
        private const string IconPath = "Assets/RewindRampage/Art/ApplicationIcon.png";
        private const string FaviconPath = "Assets/WebGLTemplates/Rewind/TemplateData/icon.png";

        [MenuItem("Rewind Rampage/Apply release version and cassette icon")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before applying release identity.");
            AssetDatabase.ImportAsset(SourcePath, ImportAssetOptions.ForceSynchronousImport);
            ImportTexture(SourcePath, true, 4096);
            Texture2D source = AssetDatabase.LoadAssetAtPath<Texture2D>(SourcePath);
            if (source == null) throw new InvalidOperationException("The supplied cassette icon source is missing.");
            // Standard icon preparation only: retain the supplied picture and its aspect ratio.
            // Fit it into a transparent square instead of stretching the horizontal cassette.
            Texture2D square = FitSquare(source, 1024, 0.94f);
            Texture2D favicon = null;
            try
            {
                File.WriteAllBytes(IconPath, square.EncodeToPNG());
                favicon = FitSquare(square, 256, 1f);
                File.WriteAllBytes(FaviconPath, favicon.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(square);
                if (favicon != null) UnityEngine.Object.DestroyImmediate(favicon);
            }
            AssetDatabase.ImportAsset(IconPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(FaviconPath, ImportAssetOptions.ForceSynchronousImport);
            ImportTexture(IconPath, false, 1024);
            // WebGLTemplates contains raw web files, imported by Unity's DefaultImporter.
            // The favicon is copied as a PNG by the web template pipeline.
            Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, new[] { icon });
            int[] sizes = PlayerSettings.GetIconSizesForTargetGroup(BuildTargetGroup.Standalone);
            var icons = new Texture2D[sizes.Length];
            for (int i = 0; i < icons.Length; i++) icons[i] = icon;
            PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Standalone, icons);
            PlayerSettings.bundleVersion = WebGLBuilder.ReleaseVersion;
            AssetDatabase.SaveAssets();
            Texture2D[] defaults = PlayerSettings.GetIconsForTargetGroup(BuildTargetGroup.Unknown);
            if (defaults.Length != 1) throw new InvalidOperationException("Default icon slot was not populated.");
            foreach (Texture2D assigned in defaults)
                if (assigned != icon) throw new InvalidOperationException("Default icon assignment failed.");
            Texture2D[] standalone = PlayerSettings.GetIconsForTargetGroup(BuildTargetGroup.Standalone);
            if (standalone.Length == 0 || standalone.Length != sizes.Length) throw new InvalidOperationException("Standalone icon slots were not populated.");
            foreach (Texture2D assigned in standalone)
                if (assigned != icon) throw new InvalidOperationException("Standalone icon assignment failed.");
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/V1000-IconSettings.txt", "Version: " + PlayerSettings.bundleVersion + "\nSource: " + SourcePath +
                "\nIcon: " + AssetDatabase.GetAssetPath(icon) + " (" + icon.width + "x" + icon.height + ")\nStandalone sizes: " + string.Join(",", sizes) + "\nWeb favicon: " + FaviconPath + " (256x256)\n");
            Debug.Log("RELEASE IDENTITY PASS: " + PlayerSettings.bundleVersion + "; supplied cassette icon set for default/Standalone; favicon generated; aspect ratio retained.");
        }

        private static Texture2D FitSquare(Texture2D source, int size, float fill)
        {
            var result = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            float scale = size * fill / Mathf.Max(source.width, source.height);
            float width = source.width * scale, height = source.height * scale;
            float left = (size - width) * 0.5f, bottom = (size - height) * 0.5f;
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f - left) / width, v = (y + 0.5f - bottom) / height;
                pixels[y * size + x] = u < 0f || u > 1f || v < 0f || v > 1f ? new Color32(0, 0, 0, 0) : (Color32)source.GetPixelBilinear(u, v);
            }
            result.SetPixels32(pixels); result.Apply(); return result;
        }

        private static void ImportTexture(string path, bool readable, int maxSize)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
            importer.isReadable = readable; importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = maxSize; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear; importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }
    }
}
