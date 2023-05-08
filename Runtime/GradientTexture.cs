using System;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Packages.GradientTextureGenerator.Runtime
{
    public interface IGradientTextureForEditor
    {
        void CreateTexture();

        Texture2D GetTexture();

        void LoadExistingTexture();
    }

    /// <summary>
    /// Main Asset, holds settings, create, hold and change Texture2D's pixels, name
    /// </summary>
    [CreateAssetMenu(fileName = "NewGradientTexture", menuName = "Texture/Gradient")]
    public class GradientTexture : ScriptableObject, IEquatable<Texture2D>, ISerializationCallbackReceiver,
        IGradientTextureForEditor
    {
        [SerializeField] Vector2Int _resolution = new(256, 256);
        [SerializeField] bool _HDR = true;
        [SerializeField] bool _sRGB = true;
        [SerializeField] bool _generateMipmaps;
        
        [SerializeField] bool _useTwoGradients = true;
        [SerializeField, GradientUsage(true)] Gradient _horizontalTop = GetDefaultGradient();
        [SerializeField, GradientUsage(true)] Gradient _horizontalBottom = GetDefaultGradient();
        [SerializeField] AnimationCurve _verticalLerp = AnimationCurve.Linear(0, 0, 1, 1);
        [SerializeField, HideInInspector] Texture2D _texture;

        public Texture2D GetTexture() => _texture;
        
        public bool GetSRGB() => _sRGB;

        public void SetSRGB(bool value)
        {
            _sRGB = value;
            OnValidate();
        }

        public static implicit operator Texture2D(GradientTexture asset) => asset.GetTexture();

        private bool IsHDRFormat => GraphicsFormatUtility.IsHDRFormat(_texture.format);
        
        static Gradient GetDefaultGradient() => new Gradient
        {
            alphaKeys = new[] { new GradientAlphaKey(1, 1) },
            colorKeys = new[]
            {
                new GradientColorKey(Color.black, 0),
                new GradientColorKey(Color.white, 1)
            }
        };

        public void FillColors()
        {
            bool isLinear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            
            for (int y = 0; y < _resolution.y; y++)
            {
                float tVertical = _verticalLerp.Evaluate((float) y / _resolution.y);

                for (int x = 0; x < _resolution.x; x++)
                {
                    float tHorizontal = (float) x / _resolution.x;

                    Color color = _useTwoGradients
                            ? Color.Lerp(_horizontalBottom.Evaluate(tHorizontal),
                                    _horizontalTop.Evaluate(tHorizontal),
                                    tVertical)
                            : _horizontalTop.Evaluate(tHorizontal);

                    
                    if (GraphicsFormatUtility.IsHDRFormat(_texture.format))
                    {
                        color = _sRGB && isLinear ? color.linear : color;
                    }
                    else
                    {
                        color = _sRGB && isLinear ? color : color.gamma;
                    }

                    _texture.SetPixel(x, y, color);
                }
            }

            _texture.Apply();
        }

        public bool Equals(Texture2D other)
        {
            return _texture.Equals(other);
        }

        void OnValidate() => ValidateTextureValues();

        void IGradientTextureForEditor.LoadExistingTexture()
        {
            #if UNITY_EDITOR
            if (!_texture)
            {
                string assetPath = AssetDatabase.GetAssetPath(this);
                _texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            }
            #endif
        }

        void IGradientTextureForEditor.CreateTexture()
        {
            #if UNITY_EDITOR

            string assetPath = AssetDatabase.GetAssetPath(this);
            if (string.IsNullOrEmpty(assetPath)) return;

            if (!_texture && this != null && !EditorApplication.isUpdating)
            {
                AssetDatabase.ImportAsset(assetPath);
                _texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            }

            if (!_texture)
            {
#if UNITY_2018
                _texture = new Texture2D(_resolution.x, _resolution.y);
#else

                _texture = new Texture2D(
                    _resolution.x, 
                    _resolution.y, 
                    _HDR ? DefaultFormat.HDR : DefaultFormat.LDR, 
                    _generateMipmaps ? TextureCreationFlags.MipChain : TextureCreationFlags.None)
                {
                    wrapMode = TextureWrapMode.Clamp
                };
#endif
                if (_texture.name != name) _texture.name = name;
            }

            if (!_texture) return;

            ValidateTextureValues();

            if (!EditorUtility.IsPersistent(this)) return;
            if (AssetDatabase.IsSubAsset(_texture)) return;
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath)) return;

            #if UNITY_2020_1_OR_NEWER
            if (AssetDatabase.IsAssetImportWorkerProcess()) return;
            #endif
            AssetDatabase.AddObjectToAsset(_texture, this);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
#endif
        }

        private void ValidateTextureValues()
        {
            if (!_texture) return;
            if (_texture.name != name)
            {
                _texture.name = name;
            }
            else
            {
                if (_texture.width != _resolution.x
                    || _texture.height != _resolution.y 
                    || IsHDRFormat != _HDR
                    || _texture.mipmapCount == 1 == _generateMipmaps // mip map minimum is 1
                   )
                {
#if UNITY_2022_1_OR_NEWER
                    _texture.Reinitialize(
                        _resolution.x,
                        _resolution.y,
                        SystemInfo.GetGraphicsFormat(_HDR ? DefaultFormat.HDR : DefaultFormat.LDR),
                        _generateMipmaps);
#else
                    _texture.Resize(_resolution.x, _resolution.y);
#endif
                }

#if UNITY_EDITOR
                _texture.alphaIsTransparency = true;
#endif
                FillColors();

                SetDirtyTexture();
            }
        }

        #region Editor

        [Conditional("UNITY_EDITOR")]
        void SetDirtyTexture()
        {
#if UNITY_EDITOR
            if (!_texture) return;

            EditorUtility.SetDirty(_texture);
#endif
        }
        
        #endregion

        public void OnAfterDeserialize()
        {
        }

        public void OnBeforeSerialize()
        {
#if UNITY_EDITOR
            if (!_texture || _texture.name == name) return;

            _texture.name = name;

            //AssetDatabase.SaveAssets();
  #endif
        }
    }
}
