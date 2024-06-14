#if UNITY_2021_2_OR_NEWER

using Packages.GradientTextureGenerator.Runtime;
using UnityEditor;
using UnityEngine;

namespace Packages.GradientTextureGenerator.Editor
{
    public static class DragAndDropUtility
    {
        private static DragAndDrop.ProjectBrowserDropHandler projectHandler;
        
        [InitializeOnLoadMethod]
        public static void Init()
        {
            projectHandler = ProjectDropHandler;
            DragAndDrop.RemoveDropHandler(projectHandler);
            DragAndDrop.AddDropHandler(projectHandler);
        }
        
        private static DragAndDropVisualMode ProjectDropHandler(int dragInstanceId, string dropUponPath, bool perform)
        {
            if (!perform)
            {
                Object[] dragged = DragAndDrop.objectReferences;
                bool found = false;
                
                for (int i = 0; i < dragged.Length; i++)
                {
                    if (dragged[i] is GradientTexture gradient)
                    {
                        dragged[i] = gradient.Texture;
                        found = true;
                    }
                }
                
                if (!found)
                {
                    return default;
                }
                
                DragAndDrop.objectReferences = dragged;
                GUI.changed = true;
                
                return default;
            }
            
            return default;
        }
    }
}

#endif
