using UnityEngine;
using UnityEditor;

public class PixelArtImporter : AssetPostprocessor
{
    private const int TARGET_PPU = 8;

    void OnPreprocessTexture()
    {
        if (!assetPath.Contains("Art/Sprites")) return;

        TextureImporter importer = (TextureImporter)assetImporter;

        importer.textureType = TextureImporterType.Sprite;
        importer.filterMode = FilterMode.Point;
        importer.spritePixelsPerUnit = TARGET_PPU;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.sRGBTexture = true;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;

        Debug.Log($"[PixelArtImporter] 已自动规范化图片: {assetPath}");
    }
}