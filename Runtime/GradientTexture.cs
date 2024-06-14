using System;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Packages.GradientTextureGenerator.Runtime
{
    public interface IGradientTextureForEditor
    {
        Texture2D CreateTexture();
        
        Texture2D Texture { get; }
        
        void LoadExistingTexture();
    }
    
    /// <summary>
    /// Main Asset, holds settings, create, hold and change Texture2D's pixels, name
    /// </summary>
    [CreateAssetMenu(fileName = "NewGradientTexture", menuName = "Texture/Gradient")]
    public class GradientTexture : ScriptableObject, IEquatable<Texture2D>, ISerializationCallbackReceiver, IGradientTextureForEditor
    {
        [FormerlySerializedAs("_resolution")][SerializeField] private Vector2Int resolution = new Vector2Int(256, 256);
        [FormerlySerializedAs("_HDR")][SerializeField] private bool hdr = true;
        [FormerlySerializedAs("_sRGB")][SerializeField] private bool sRGB = true;
        [FormerlySerializedAs("_generateMipmaps")][SerializeField] private bool generateMipmaps;
        [FormerlySerializedAs("_useTwoGradients")][SerializeField] private bool useTwoGradients = true;
        [FormerlySerializedAs("_horizontalTop")][SerializeField, GradientUsage(true)] private Gradient horizontalTop = GetDefaultGradient();
        [FormerlySerializedAs("_horizontalBottom")][SerializeField, GradientUsage(true)] private Gradient horizontalBottom = GetDefaultGradient();
        [FormerlySerializedAs("_verticalLerp")][SerializeField] private AnimationCurve verticalLerp = AnimationCurve.Linear(0, 0, 1, 1);
        [FormerlySerializedAs("_texture")][SerializeField, HideInInspector] private Texture2D texture;
        
#if UNITY_EDITOR
        public const string RESOLUTION_PROP_NAME = nameof(resolution);
        public const string HDR_PROP_NAME = nameof(hdr);
        public const string S_RGB_PROP_NAME = nameof(sRGB);
        public const string GENERATE_MIPMAPS_PROP_NAME = nameof(generateMipmaps);
        public const string USE_TWO_GRADIENTS_PROP_NAME = nameof(useTwoGradients);
        public const string HORIZONTAL_TOP_PROP_NAME = nameof(horizontalTop);
        public const string HORIZONTAL_BOTTOM_PROP_NAME = nameof(horizontalBottom);
        public const string VERTICAL_LERP_PROP_NAME = nameof(verticalLerp);
#endif
        
        public Texture2D Texture => texture;
        public bool SRGB => sRGB;
        
        public void SetSRGB(bool value)
        {
            sRGB = value;
            OnValidate();
        }
        
        public static implicit operator Texture2D(GradientTexture asset)
        {
            return asset.Texture;
        }
        
        private bool IsHDRFormat => GraphicsFormatUtility.IsHDRFormat(texture.format);
        
        private static Gradient GetDefaultGradient()
        {
            return new Gradient
            {
                    alphaKeys = new[] { new GradientAlphaKey(1, 1) },
                    colorKeys = new[] { new GradientColorKey(Color.black, 0), new GradientColorKey(Color.white, 1) }
            };
        }
        
        /// <summary>
        /// Fill pixels of texture with colors from the gradient definition. 
        /// </summary>
        public void FillColors()
        {
            bool isLinear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            
            for (int y = 0; y < resolution.y; y++)
            {
                float tVertical = verticalLerp.Evaluate((float) y / resolution.y);
                
                for (int x = 0; x < resolution.x; x++)
                {
                    float tHorizontal = (float) x / resolution.x;
                    
                    Color color = useTwoGradients
                            ? Color.Lerp(horizontalBottom.Evaluate(tHorizontal), horizontalTop.Evaluate(tHorizontal), tVertical)
                            : horizontalTop.Evaluate(tHorizontal);
                    
                    if (GraphicsFormatUtility.IsHDRFormat(texture.format))
                    {
                        color = sRGB && isLinear ? color.linear : color;
                    }
                    else
                    {
                        color = sRGB && isLinear ? color : color.gamma;
                    }
                    
                    texture.SetPixel(x, y, color);
                }
            }
            
            texture.Apply();
        }
        
        public bool Equals(Texture2D other)
        {
            return texture.Equals(other);
        }
        
        private void OnValidate()
        {
            ValidateTextureValues();
        }
        
        void IGradientTextureForEditor.LoadExistingTexture()
        {
#if UNITY_EDITOR
            if (texture)
            {
                return;
            }
            
            string assetPath = AssetDatabase.GetAssetPath(this);
            texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
#endif
        }
        
        Texture2D IGradientTextureForEditor.CreateTexture()
        {
#if UNITY_EDITOR
            
            string assetPath = AssetDatabase.GetAssetPath(this);
            
            if (string.IsNullOrEmpty(assetPath) || !EditorApplication.isUpdating)
            {
                return null;
            }
            
            // Load texture from asset
            if (texture == null)
            {
                texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            }
            
            // If asset had no texture create a new one
            if (texture != null)
            {
#if UNITY_2018
                texture = new Texture2D(_resolution.x, _resolution.y);
#else
                
                texture = new Texture2D(resolution.x, resolution.y, hdr ? DefaultFormat.HDR : DefaultFormat.LDR,
                        generateMipmaps ? TextureCreationFlags.MipChain : TextureCreationFlags.None) { wrapMode = TextureWrapMode.Clamp };
#endif
                if (texture.name != name)
                {
                    texture.name = name;
                }
            }
            
            // If texture could not be created return
            if (!texture)
            {
                return null;
            }
            
            // Validate Texture Values and sub asset state
            ValidateTextureValues();
            
            if (!EditorUtility.IsPersistent(this) || AssetDatabase.IsSubAsset(texture) || AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath))
            {
                return null;
            }
            
#if UNITY_2020_1_OR_NEWER
            if (AssetDatabase.IsAssetImportWorkerProcess())
            {
                return null;
            }
#endif
            AssetDatabase.AddObjectToAsset(texture, this);
            
            return texture;
#endif
        }
        
        private void ValidateTextureValues()
        {
            if (!texture)
            {
                return;
            }
            
            if (texture.name != name)
            {
                texture.name = name;
            }
            else
            {
                if (texture.width != resolution.x || texture.height != resolution.y || IsHDRFormat != hdr ||
                    texture.mipmapCount == 1 == generateMipmaps // mip map minimum is 1
                   )
                {
#if UNITY_2022_1_OR_NEWER
                    texture.Reinitialize(resolution.x, resolution.y, SystemInfo.GetGraphicsFormat(hdr ? DefaultFormat.HDR : DefaultFormat.LDR),
                            generateMipmaps);
#else
                    texture.Resize(_resolution.x, _resolution.y);
#endif
                }
                
#if UNITY_EDITOR
                texture.alphaIsTransparency = true;
#endif
                FillColors();
                SetDirtyTexture();
            }
        }
        
#region Editor
        [Conditional("UNITY_EDITOR")]
        private void SetDirtyTexture()
        {
#if UNITY_EDITOR
            if (!texture)
            {
                return;
            }
            
            EditorUtility.SetDirty(texture);
#endif
        }
#endregion
        
        public void OnAfterDeserialize() {}
        
        public void OnBeforeSerialize()
        {
#if UNITY_EDITOR
            if (!texture || texture.name == name)
            {
                return;
            }
            
            texture.name = name;
#endif
        }
    }
}
