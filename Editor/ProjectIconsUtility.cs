using System;
using Packages.GradientTextureGenerator.Runtime;
using UnityEditor;
using UnityEngine;

namespace Packages.GradientTextureGenerator.Editor
{
    [InitializeOnLoad]
    public static class ProjectIconsUtility
    {
        private static readonly Type GradientTextureType = typeof(GradientTexture);

        static ProjectIconsUtility()
        {
            EditorApplication.projectWindowItemOnGUI -= ItemOnGUI;
            EditorApplication.projectWindowItemOnGUI += ItemOnGUI;
        }

        private static void ItemOnGUI(string guid, Rect rect)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);

            // Check if asset is a scriptable object and if type of asset at path is Gradient Texture without loading the asset
            if (!assetPath.EndsWith(".asset") ||
                AssetDatabase.GetMainAssetTypeAtPath(assetPath) != GradientTextureType)
            {
                return;
            }

            var asset = AssetDatabase.LoadAssetAtPath<GradientTexture>(assetPath);

            // if the rect size is less than 30 the icon is shown, if larger the project view uses the texture from RenderStaticPreview
            if (rect.height < 30)
            {
                Texture2D texture = asset.GetTexture();

                if (texture == null)
                {
                    return;
                }
                
                Rect iconRect = new Rect { x = rect.x + 3, y = rect.y, height = rect.height - 1, width = rect.height };
                GUI.DrawTexture(iconRect, AssetPreview.GetMiniThumbnail(texture));
            }
        }
    }
}