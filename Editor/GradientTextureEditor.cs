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
    [CustomEditor(typeof(GradientTexture), true), CanEditMultipleObjects]
    public class GradientTextureEditor : UnityEditor.Editor
    {
        private GradientTexture _gradientTexture;
        private UnityEditor.Editor _editor;

        private const string EncodeToFileText = "Encode to";

        private enum EncodeFileType
        {
            Png = 0,
            Tga = 1,
            Exr = 2,
        }

        private SerializedProperty _scriptProp;
        private SerializedProperty _resolutionProp;
        private SerializedProperty _hdrProp;
        private SerializedProperty _sRGBProp;
        private SerializedProperty _generateMipmapsProp;
        private SerializedProperty _useTwoGradientsProp;
        private SerializedProperty _horizontalTopProp;
        private SerializedProperty _horizontalBottomProp;
        private SerializedProperty _verticalLerpProp;

        private static readonly string[] propertiesToExclude =
        {
            "m_Script",
            "_resolution",
            "_HDR",
            "_sRGB",
            "_generateMipmaps",
            "_useTwoGradients",
            "_horizontalTop",
            "_horizontalBottom",
            "_verticalLerp",
        };

        public override bool HasPreviewGUI() => false;

        private void OnEnable()
        {
            _gradientTexture = target as GradientTexture;
            _scriptProp = serializedObject.FindProperty("m_Script");
            _resolutionProp = serializedObject.FindProperty("_resolution");
            _hdrProp = serializedObject.FindProperty("_HDR");
            _sRGBProp = serializedObject.FindProperty("_sRGB");
            _generateMipmapsProp = serializedObject.FindProperty("_generateMipmaps");
            _useTwoGradientsProp = serializedObject.FindProperty("_useTwoGradients");
            _horizontalTopProp = serializedObject.FindProperty("_horizontalTop");
            _horizontalBottomProp = serializedObject.FindProperty("_horizontalBottom");
            _verticalLerpProp = serializedObject.FindProperty("_verticalLerp");
        }
        
        public override void OnInspectorGUI()
        {
            if (_gradientTexture.GetTexture() == null)
            {
                Texture2D newTex = (_gradientTexture as IGradientTextureForEditor).CreateTexture();

                if (newTex != null)
                {
                    // AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(newTex), ImportAssetOptions.ForceUpdate);
                    OnEnable();

                    return;
                }
            }
            
            serializedObject.Update();

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.PropertyField(_scriptProp);
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.PropertyField(_resolutionProp);
            EditorGUILayout.PropertyField(_hdrProp);
            EditorGUILayout.PropertyField(_sRGBProp);
            EditorGUILayout.PropertyField(_generateMipmapsProp);

            EditorGUILayout.PropertyField(_useTwoGradientsProp);
            EditorGUILayout.PropertyField(_horizontalTopProp);

            EditorGUI.BeginDisabledGroup(!_useTwoGradientsProp.boolValue);
            EditorGUILayout.PropertyField(_horizontalBottomProp);
            EditorGUILayout.PropertyField(_verticalLerpProp);
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
            return targets.Length > 1 ? $"{EncodeToFileText} {ft} ({targets.Length})" : $"{EncodeToFileText} {ft}";
        }

        private void EncodeToFiles(EncodeFileType fileType)
        {
            foreach (Object targetObject in targets)
            {
                GradientTexture gradientTexture = (GradientTexture)targetObject;

                string extension = fileType.ToString().ToLower();
                string path = EditorUtility.SaveFilePanelInProject(
                    "Save file",
                    $"{gradientTexture.name}_baked",
                    extension,
                    "Choose path to save file", 
                    AssetDatabase.GetAssetPath(gradientTexture));

                if (string.IsNullOrEmpty(path))
                {
                    return;
                }

                Texture2D texture2D = gradientTexture.GetTexture();
                bool wasSRGB = gradientTexture.GetSRGB();

                // set linear or gamma for export
                if (_hdrProp.boolValue)
                {
                    if (wasSRGB)
                    {
                        gradientTexture.SetSRGB(false);
                    }
                }
                else
                {
                    // non hdr has to be always srgb during encode
                    gradientTexture.SetSRGB(true);
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

                gradientTexture.SetSRGB(wasSRGB);

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
                TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
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
            Texture2D texture = _gradientTexture.GetTexture();
            bool check = !_editor || _editor.target != texture;

            if (check && texture && (_editor == null || _editor.target != texture))
            {
                try
                {
                    // _editor = CreateEditor(targets.Select(t => (t as GradientTexture)?.GetTexture()).ToArray());
                }
                catch
                {
                    _editor = null;
                    //Debug.LogException(e);
                    //throw;
                }
            }

            if (_editor && _editor.target)
            {
                try
                {
                    _editor.DrawPreview(previewArea);
                }
                catch
                {
                    //Debug.LogException(e);
                    //throw;
                }
            }
        }

        public override void OnPreviewSettings()
        {
            if (_editor && _editor.target)
            {
                try
                {
                    _editor.OnPreviewSettings();
                }
                catch
                {
                    //Debug.LogException(e);
                    //throw;
                }
            }
        }

        public override void ReloadPreviewInstances()
        {
            if (_editor && _editor.target)
            {
                try
                {
                    _editor.ReloadPreviewInstances();
                }
                catch
                {
                    //Debug.LogException(e);
                    //throw;
                }
            }
        }

        public override void OnInteractivePreviewGUI(Rect r, GUIStyle background)
        {
            if (_editor && _editor.target)
            {
                try
                {
                    _editor.OnInteractivePreviewGUI(r, background);
                }
                catch
                {
                    //Debug.LogException(e);
                    //throw;
                }
            }
        }

        public override void OnPreviewGUI(Rect r, GUIStyle background)
        {
            if (_editor && _editor.target)
            {
                try
                {
                    _editor.OnPreviewGUI(r, background);
                }
                catch
                {
                    //Debug.LogException(e);
                    //throw;
                }
            }
        }

        public override Texture2D RenderStaticPreview(string assetPath, Object[] subAssets, int width, int height)
        {
            if (_gradientTexture == null) return null;
            if (_gradientTexture.GetTexture() == null) return null;
            Texture2D tex = new Texture2D(width, height);
            EditorUtility.CopySerialized(_gradientTexture.GetTexture(), tex);

            return tex;
        }

        void OnDisable()
        {
            if (_editor)
            {
                _editor.GetType().GetMethod("OnDisable", BindingFlags.NonPublic)?.Invoke(_editor, null);
            }
        }

        void OnDestroy()
        {
            if (_editor)
            {
                DestroyImmediate(_editor);
            }
        }
    }
}