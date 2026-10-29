using UnityEngine;
using UnityEditor;

public class SpriteFolderPostprocessor : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        // Automatically ensure any texture placed in Assets/Sprites is imported as a Single Sprite
        if (assetPath.StartsWith("Assets/Sprites/"))
        {
            TextureImporter importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
        }
    }
}
