using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Packages.GradientTextureGenerator.Runtime;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Packages.GradientTextureGenerator.Editor
{
    [CustomEditor(typeof(GradientTexture), true)][CanEditMultipleObjects]
    public class GradientTextureEditor : UnityEditor.Editor
    {
        private GradientTexture gradientTexture;
        private UnityEditor.Editor editor;
        
        private const string ENCODE_TO_FILE_TEXT = "Encode to";
        
        private enum EncodeFileType
        {
            Png = 0,
            Tga = 1,
            Exr = 2
        }
        
        private SerializedProperty scriptProp;
        private SerializedProperty resolutionProp;
        private SerializedProperty hdrProp;
        private SerializedProperty sRGBProp;
        private SerializedProperty generateMipmapsProp;
        private SerializedProperty useTwoGradientsProp;
        private SerializedProperty horizontalTopProp;
        private SerializedProperty horizontalBottomProp;
        private SerializedProperty verticalLerpProp;
        
        private readonly static string[] propertiesToExclude =
        {
                "m_Script", GradientTexture.RESOLUTION_PROP_NAME, GradientTexture.HDR_PROP_NAME,
                GradientTexture.S_RGB_PROP_NAME, GradientTexture.GENERATE_MIPMAPS_PROP_NAME,
                GradientTexture.USE_TWO_GRADIENTS_PROP_NAME, GradientTexture.HORIZONTAL_TOP_PROP_NAME,
                GradientTexture.HORIZONTAL_BOTTOM_PROP_NAME, GradientTexture.VERTICAL_LERP_PROP_NAME
        };
        
        public override bool HasPreviewGUI()
        {
            return false;
        }
        
        private void OnEnable()
        {
            gradientTexture = target as GradientTexture;
            scriptProp = serializedObject.FindProperty("m_Script");
            resolutionProp = serializedObject.FindProperty(GradientTexture.RESOLUTION_PROP_NAME);
            hdrProp = serializedObject.FindProperty(GradientTexture.HDR_PROP_NAME);
            sRGBProp = serializedObject.FindProperty(GradientTexture.S_RGB_PROP_NAME);
            generateMipmapsProp = serializedObject.FindProperty(GradientTexture.GENERATE_MIPMAPS_PROP_NAME);
            useTwoGradientsProp = serializedObject.FindProperty(GradientTexture.USE_TWO_GRADIENTS_PROP_NAME);
            horizontalTopProp = serializedObject.FindProperty(GradientTexture.HORIZONTAL_TOP_PROP_NAME);
            horizontalBottomProp = serializedObject.FindProperty(GradientTexture.HORIZONTAL_BOTTOM_PROP_NAME);
            verticalLerpProp = serializedObject.FindProperty(GradientTexture.VERTICAL_LERP_PROP_NAME);
        }
        
        public override void OnInspectorGUI()
        {
            // Generate new texture if none could be found yet
            if (gradientTexture.Texture == null)
            {
                Texture2D newTex = (gradientTexture as IGradientTextureForEditor).CreateTexture();
                
                if (newTex != null)
                {
                    OnEnable();
                    
                    return;
                }
            }
            
            serializedObject.Update();
            
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.PropertyField(scriptProp);
            EditorGUI.EndDisabledGroup();
            
            EditorGUILayout.PropertyField(resolutionProp);
            EditorGUILayout.PropertyField(hdrProp);
            EditorGUILayout.PropertyField(sRGBProp);
            EditorGUILayout.PropertyField(generateMipmapsProp);
            
            EditorGUILayout.PropertyField(useTwoGradientsProp);
            EditorGUILayout.PropertyField(horizontalTopProp);
            
            EditorGUI.BeginDisabledGroup(!useTwoGradientsProp.boolValue);
            EditorGUILayout.PropertyField(horizontalBottomProp);
            EditorGUILayout.PropertyField(verticalLerpProp);
            EditorGUI.EndDisabledGroup();
            
            DrawPropertiesExcluding(serializedObject, propertiesToExclude);
            serializedObject.ApplyModifiedProperties();
            
            EditorGUILayout.Space();
            
            // Draw export buttons
            foreach (EncodeFileType fileType in Enum.GetValues(typeof(EncodeFileType)))
            {
                if (GUILayout.Button(GetEncodeButtonText(fileType)))
                {
                    EncodeToFiles(fileType);
                }
            }
        }
        
        private string GetEncodeButtonText(EncodeFileType fileType)
        {
            string ft = fileType.ToString().ToUpper();
            
            return targets.Length > 1
                    ? $"{ENCODE_TO_FILE_TEXT} {ft} ({targets.Length})"
                    : $"{ENCODE_TO_FILE_TEXT} {ft}";
        }
        
        private void EncodeToFiles(EncodeFileType fileType)
        {
            foreach (Object targetObject in targets)
            {
                GradientTexture targetGradientTexture = (GradientTexture) targetObject;
                
                string extension = fileType.ToString().ToLower();
                string path = EditorUtility.SaveFilePanelInProject("Save file", $"{targetGradientTexture.name}_baked",
                        extension, "Choose path to save file", AssetDatabase.GetAssetPath(targetGradientTexture));
                
                if (string.IsNullOrEmpty(path))
                {
                    return;
                }
                
                Texture2D texture2D = targetGradientTexture.Texture;
                bool wasSRGB = targetGradientTexture.SRGB;
                
                // set linear or gamma for export
                if (hdrProp.boolValue)
                {
                    if (wasSRGB)
                    {
                        targetGradientTexture.SetSRGB(false);
                    }
                }
                else
                {
                    // non hdr has to be always srgb during encode
                    targetGradientTexture.SetSRGB(true);
                }
                
                // Get byte array for file type
                byte[] bytes;
                
                switch (fileType)
                {
                    case EncodeFileType.Png:
                        // The encoded PNG data will be either 8bit grayscale, RGB or RGBA (depending on the passed in format)
                        bytes = texture2D.EncodeToPNG();
                        
                        break;
                    case EncodeFileType.Tga:
                        bytes = texture2D.EncodeToTGA();
                        
                        break;
                    case EncodeFileType.Exr:
                        bytes = texture2D.EncodeToEXR();
                        
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(fileType), fileType, null);
                }
                
                targetGradientTexture.SetSRGB(wasSRGB);
                
                // Get output path
                int length = "Assets".Length;
                string dataPath = $"{Application.dataPath[..^length]}{path}";
                bool fileExistedBefore = File.Exists(dataPath);
                File.WriteAllBytes(dataPath, bytes);
                
                if (!fileExistedBefore)
                {
                    AssetDatabase.ImportAsset(path); // Import asset to create importer
                }
                
                // Set importer settings of exported file
                TextureImporter importer = (TextureImporter) AssetImporter.GetAtPath(path);
                importer.sRGBTexture = wasSRGB;
                importer.wrapMode = texture2D.wrapMode;
                importer.mipmapEnabled = texture2D.mipmapCount > 1;
                
                if (importer.importSettingsMissing)
                {
                    importer.textureCompression = TextureImporterCompression.CompressedHQ;
                }
                
                // Import asset to update importer
                AssetDatabase.ImportAsset(path);
                
                Debug.Log($"[GradientTextureEditor] Saved gradient image at '{path}'", importer);
                EditorGUIUtility.PingObject(importer);
                Selection.activeObject = importer;
            }
        }
        
        public override void DrawPreview(Rect previewArea)
        {
            Texture2D texture = gradientTexture.Texture;
            
            if (texture == null)
            {
                return;
            }
            
            bool needNewEditor = editor == null || editor.target != texture;
            
            if (needNewEditor)
            {
                int targetCount = targets.Length;
                Object[] textures = new Object[targetCount];
                
                for (int i = 0; i < targetCount; i++)
                {
                    GradientTexture gradientTex = targets[i] as GradientTexture;
                    textures[i] = gradientTex != null ? gradientTex.Texture : null;
                }
                
                editor = CreateEditor(textures);
            }
            
            if (editor!= null && editor.target != null)
            {
                editor.DrawPreview(previewArea);
            }
        }
        
        public override void OnPreviewSettings()
        {
            if (editor && editor.target)
            {
                editor.OnPreviewSettings();
                
                try {}
                catch
                {
                    //Debug.LogException(e);
                    //throw;
                }
            }
        }
        
        public override void ReloadPreviewInstances()
        {
            if (editor && editor.target)
            {
                editor.ReloadPreviewInstances();
                
                try {}
                catch
                {
                    //Debug.LogException(e);
                    //throw;
                }
            }
        }
        
        public override void OnInteractivePreviewGUI(Rect r, GUIStyle background)
        {
            if (editor && editor.target)
            {
                editor.OnInteractivePreviewGUI(r, background);
                
                try {}
                catch
                {
                    //Debug.LogException(e);
                    //throw;
                }
            }
        }
        
        public override void OnPreviewGUI(Rect r, GUIStyle background)
        {
            if (editor && editor.target)
            {
                editor.OnPreviewGUI(r, background);
                
                try {}
                catch
                {
                    //Debug.LogException(e);
                    //throw;
                }
            }
        }
        
        public override Texture2D RenderStaticPreview(string assetPath, Object[] subAssets, int width, int height)
        {
            if (gradientTexture == null)
            {
                return null;
            }
            
            if (gradientTexture.Texture == null)
            {
                return null;
            }
            
            Texture2D tex = new Texture2D(width, height);
            EditorUtility.CopySerialized(gradientTexture.Texture, tex);
            
            return tex;
        }
        
        private void OnDisable()
        {
            if (editor)
            {
                editor.GetType().GetMethod("OnDisable", BindingFlags.NonPublic)?.Invoke(editor, null);
            }
        }
        
        private void OnDestroy()
        {
            if (editor)
            {
                DestroyImmediate(editor);
            }
        }
    }
}
