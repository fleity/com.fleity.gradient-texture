using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Packages.GradientTextureGenerator.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Packages.GradientTextureGenerator.Editor
{
    /// <summary>
    /// Editor class for <see cref="GradientTexture"/>. Implements custom inspector, preview and create menu.
    /// </summary>
    [CustomEditor(typeof(GradientTexture), true)][CanEditMultipleObjects]
    public class GradientTextureEditor : UnityEditor.Editor
    {
        private UnityEditor.Editor editor;
        private readonly List<GradientTexture> gradientTextures = new List<GradientTexture>();
        private Object[] previewObjects;
        private const string ENCODE_TO_FILE_TEXT = "Encode to";
        
        private enum EncodeFileType
        {
            Png = 0,
            Tga = 1,
            Exr = 2
        }
        
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
                "m_Script", GradientTexture.RESOLUTION_PROP_NAME, GradientTexture.HDR_PROP_NAME, GradientTexture.S_RGB_PROP_NAME,
                GradientTexture.GENERATE_MIPMAPS_PROP_NAME, GradientTexture.USE_TWO_GRADIENTS_PROP_NAME,
                GradientTexture.HORIZONTAL_TOP_PROP_NAME, GradientTexture.HORIZONTAL_BOTTOM_PROP_NAME, GradientTexture.VERTICAL_LERP_PROP_NAME
        };
        
#region Scriptable Object Create Menu Item
        [MenuItem("Assets/Create/Texture/GradientTextureAsset", false, 0)]
        public static void CreateGradientTexture()
        {
            // Create and initialize a new instance of the texture asset ScriptableObject
            GradientTexture asset = GradientTexture.Create();
            
            // Save the ScriptableObject as a new asset
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            
            if (string.IsNullOrEmpty(path))
            {
                path = "Assets";
            }
            else if (Path.GetExtension(path) != "")
            {
                path = path.Replace(Path.GetFileName(AssetDatabase.GetAssetPath(Selection.activeObject)), "");
            }
            
            string assetPathAndName = AssetDatabase.GenerateUniqueAssetPath(path + "/NewGradientTexture.asset");
            
            AssetDatabase.CreateAsset(asset, assetPathAndName);
            
            // Add the texture as a sub-asset
            AssetDatabase.AddObjectToAsset(asset.Texture, asset);
            AssetDatabase.SaveAssetIfDirty(asset);
            
            // Select the newly created asset
            Selection.activeObject = asset;
        }
#endregion
        
#region Unity Mono Events
        private void OnEnable()
        {
            // Cache inspected gradient textures
            previewObjects = new Object[targets.Length];
            
            for (int index = 0; index < targets.Length; index++)
            {
                Object targetObject = targets[index];
                
                if (targetObject is GradientTexture gradientTexture)
                {
                    gradientTextures.Add(gradientTexture);
                    previewObjects[index] = gradientTexture.Texture;
                }
            }
            
            // Create preview editor
            CreateCachedEditor(previewObjects, null, ref editor);
            
            // Get serialized properties
            resolutionProp = serializedObject.FindProperty(GradientTexture.RESOLUTION_PROP_NAME);
            hdrProp = serializedObject.FindProperty(GradientTexture.HDR_PROP_NAME);
            sRGBProp = serializedObject.FindProperty(GradientTexture.S_RGB_PROP_NAME);
            generateMipmapsProp = serializedObject.FindProperty(GradientTexture.GENERATE_MIPMAPS_PROP_NAME);
            useTwoGradientsProp = serializedObject.FindProperty(GradientTexture.USE_TWO_GRADIENTS_PROP_NAME);
            horizontalTopProp = serializedObject.FindProperty(GradientTexture.HORIZONTAL_TOP_PROP_NAME);
            horizontalBottomProp = serializedObject.FindProperty(GradientTexture.HORIZONTAL_BOTTOM_PROP_NAME);
            verticalLerpProp = serializedObject.FindProperty(GradientTexture.VERTICAL_LERP_PROP_NAME);
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
#endregion
        
#region Custom Inspector
        public override VisualElement CreateInspectorGUI()
        {
            VisualElement container = new VisualElement();
            
            // Add main inspector
            VisualElement mainInspector = new VisualElement();
            container.RegisterCallback<SerializedPropertyChangeEvent>(_ => UpdateTextures()); // update textures on any changed property
            container.Add(mainInspector);
            
            // Setup two gradients field and disabling the connected fields
            PropertyField horizontalBottomField = new PropertyField(horizontalBottomProp);
            PropertyField verticalLerpField = new PropertyField(verticalLerpProp);
            PropertyField twoGradientsField = new PropertyField(useTwoGradientsProp);
            twoGradientsField.RegisterCallback<ChangeEvent<bool>>(x =>
            {
                horizontalBottomField.SetEnabled(x.newValue);
                verticalLerpField.SetEnabled(x.newValue);
            });
            
            // Add fields to inspector
            mainInspector.Add(new PropertyField(resolutionProp));
            mainInspector.Add(new PropertyField(hdrProp));
            mainInspector.Add(new PropertyField(sRGBProp));
            mainInspector.Add(new PropertyField(generateMipmapsProp));
            mainInspector.Add(twoGradientsField);
            mainInspector.Add(new PropertyField(horizontalTopProp));
            mainInspector.Add(horizontalBottomField);
            mainInspector.Add(verticalLerpField);
            
            // Add encode buttons
            VisualElement buttonGroup = new VisualElement { style = { marginTop = 10 } };
            container.Add(buttonGroup);
            
            foreach (EncodeFileType fileType in Enum.GetValues(typeof(EncodeFileType)))
            {
                buttonGroup.Add(new Button(() => { EncodeToFiles(fileType); }) { text = GetEncodeButtonText(fileType) });
            }
            
            return container;
        }
        
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            EditorGUI.BeginChangeCheck();
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
            
            if (EditorGUI.EndChangeCheck())
            {
                UpdateTextures();
            }
            
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
        
        private void UpdateTextures()
        {
            foreach (GradientTexture gradientTexture in gradientTextures)
            {
                gradientTexture.UpdateTexture();
            }
        }
#endregion
        
#region Encode to File
        private string GetEncodeButtonText(EncodeFileType fileType)
        {
            string ft = fileType.ToString().ToUpper();
            
            return targets.Length > 1 ? $"{ENCODE_TO_FILE_TEXT} {ft} ({targets.Length})" : $"{ENCODE_TO_FILE_TEXT} {ft}";
        }
        
        private void EncodeToFiles(EncodeFileType fileType)
        {
            foreach (GradientTexture targetGradientTexture in gradientTextures)
            {
                Texture2D texture = targetGradientTexture.Texture;
                string extension = fileType.ToString().ToLower();
                string path = EditorUtility.SaveFilePanelInProject("Save file", $"{targetGradientTexture.name}_baked", extension,
                        "Choose path to save file", AssetDatabase.GetAssetPath(targetGradientTexture));
                
                if (string.IsNullOrEmpty(path))
                {
                    return;
                }
                
                // Get byte array for file type
                byte[] bytes;
                
                switch (fileType)
                {
                    case EncodeFileType.Png:
                        // The encoded PNG data will be either 8bit grayscale, RGB or RGBA (depending on the passed in format)
                        bytes = texture.EncodeToPNG();
                        
                        break;
                    case EncodeFileType.Tga:
                        bytes = texture.EncodeToTGA();
                        
                        break;
                    case EncodeFileType.Exr:
                        bytes = texture.EncodeToEXR();
                        
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(fileType), fileType, null);
                }
                
                // Get output path
                string dataPath = Path.Combine(Application.dataPath, "..", path);
                bool fileExistedBefore = File.Exists(dataPath);
                File.WriteAllBytes(dataPath, bytes);
                
                if (!fileExistedBefore)
                {
                    AssetDatabase.ImportAsset(path); // Import asset to create importer
                }
                
                // Set importer settings of exported file
                TextureImporter importer = (TextureImporter) AssetImporter.GetAtPath(path);
                importer.sRGBTexture = texture.isDataSRGB;
                importer.wrapMode = texture.wrapMode;
                importer.mipmapEnabled = texture.mipmapCount > 1;
                
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
#endregion
        
#region Preview GUI
        public override bool HasPreviewGUI()
        {
            return true;
        }
        
        public override void DrawPreview(Rect previewArea)
        {
            Texture2D texture = gradientTextures[0].Texture;
            
            if (texture == null)
            {
                return;
            }
            
            bool needNewEditor = editor.target != texture;
            
            if (needNewEditor)
            {
                for (int i = 0; i < targets.Length; i++)
                {
                    if (targets[i] is GradientTexture gradientTex)
                    {
                        previewObjects[i] = gradientTex.Texture;
                    }
                }
                
                CreateCachedEditor(previewObjects, null, ref editor);
            }
            
            editor.DrawPreview(previewArea);
        }
        
        public override void OnPreviewSettings()
        {
            editor.OnPreviewSettings();
        }
        
        public override void ReloadPreviewInstances()
        {
            editor.ReloadPreviewInstances();
        }
        
        public override void OnInteractivePreviewGUI(Rect r, GUIStyle background)
        {
            editor.OnInteractivePreviewGUI(r, background);
        }
        
        public override void OnPreviewGUI(Rect r, GUIStyle background)
        {
            editor.OnPreviewGUI(r, background);
        }
        
        public override Texture2D RenderStaticPreview(string assetPath, Object[] subAssets, int width, int height)
        {
            GradientTexture gradientTexture = (GradientTexture) target;
            Texture2D tex = new Texture2D(width, height);
            EditorUtility.CopySerialized(gradientTexture.Texture, tex);
            
            return tex;
        }
#endregion
    }
}
