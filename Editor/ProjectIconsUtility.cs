using Packages.GradientTextureGenerator.Runtime;
using UnityEditor;
using UnityEngine;

namespace Packages.GradientTextureGenerator.Editor
{
    /// <summary>
    /// Handles drawing the gradient preview in the project tab. Only updates when the project tab updates too (i.e. on mouse over).
    /// </summary>
    [InitializeOnLoad]
    public class ProjectIconsUtility : MonoBehaviour
    {
        static ProjectIconsUtility()
        {
            EditorApplication.projectWindowItemOnGUI -= ItemOnGUI;
            EditorApplication.projectWindowItemOnGUI += ItemOnGUI;
        }
        
        private static void ItemOnGUI(string guid, Rect rect)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            
            // Return if not correct asset type
            if (!assetPath.EndsWith(".asset") || 
                AssetDatabase.GetMainAssetTypeAtPath(assetPath) != typeof(GradientTexture))
            {
                return;
            }
            
            // Get gradient asset
            GradientTexture asset = AssetDatabase.LoadAssetAtPath<GradientTexture>(assetPath);

            // Return if gradient or texture not valid
            if (asset == null || !asset.Texture)
            {
                return;
            }
            
            // Draw small icon preview
            if (rect.height <= 30)
            {
                rect.width = rect.height; // draw the preview in square aspect
                GUI.DrawTexture(rect, asset.Texture);
            }
        }
    }
}
