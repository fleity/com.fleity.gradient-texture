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
                "_hdr",
                "_sRGB",
                "_generateMipmaps",
                "_useTwoGradients",
                "_horizontalTop",
                "_horizontalBottom",
                "_verticalLerp",
        };

        public override bool HasPreviewGUI() => true;

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
                (_gradientTexture as IGradientTextureForEditor).CreateTexture();
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

            string buttonText = "Encode to PNG" + (targets.Length > 1 ? $" ({targets.Length})" : "");

            if (GUILayout.Button(buttonText))
            {
                foreach (Object targetObject in targets)
                {
                    GradientTexture gradientTexture = (GradientTexture) targetObject;

                    string path = EditorUtility.SaveFilePanelInProject("Save file",
                        $"{gradientTexture.name}_baked",
                        "png",
                        "Choose path to save file");

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
                    
                    byte[] bytes = gradientTexture.GetTexture().EncodeToPNG();
                    gradientTexture.SetSRGB(wasSRGB);

                    int length = "Assets".Length;
                    string dataPath = Application.dataPath;
                    dataPath = dataPath.Remove(dataPath.Length - length, length);
                    dataPath += path;
                    File.WriteAllBytes(dataPath, bytes);

                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    AssetDatabase.ImportAsset(path);
                    Texture2D image = AssetDatabase.LoadAssetAtPath<Texture2D>(path);

                    TextureImporter importer = (TextureImporter) AssetImporter.GetAtPath(path);
                    importer.sRGBTexture = wasSRGB;
                    importer.wrapMode = texture2D.wrapMode;
                    importer.mipmapEnabled = texture2D.mipmapCount > 1;
                    importer.SaveAndReimport();

                    if (importer.importSettingsMissing)
                    {
                        importer.textureCompression = TextureImporterCompression.CompressedHQ;
                    }
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();

                    Debug.Log($"[ GradientTextureEditor ] EncodeToPNG() Success! png-gradient saved at '{path}'", image);

                    EditorGUIUtility.PingObject(image);
                    Selection.activeObject = image;
                }
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
                    _editor = CreateEditor(targets.Select(t => (t as GradientTexture)?.GetTexture()).ToArray());
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