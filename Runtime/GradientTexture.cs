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
    /// <summary>
    /// Main Asset, holds settings, create and change Texture2D's pixels based on gradients.
    /// Use the static Create method instead of Scriptable Object CreateInstance to immediately initialize the texture.
    /// </summary>
    
    // Create menu is in the editor class, that way we can immediately initialize the texture that way too.
    public class GradientTexture : ScriptableObject, IEquatable<Texture2D>, ISerializationCallbackReceiver
    {
        [FormerlySerializedAs("_resolution")][SerializeField] private Vector2Int resolution = new Vector2Int(256, 256);
        [FormerlySerializedAs("_HDR")][SerializeField] private bool hdr = true;
        [FormerlySerializedAs("_sRGB")][SerializeField] private bool sRGB = true;
        [FormerlySerializedAs("_generateMipmaps")][SerializeField] private bool generateMipmaps;
        [FormerlySerializedAs("_useTwoGradients")][SerializeField] private bool useTwoGradients;
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
        /// Create new Gradient Texture scriptable object instance.
        /// </summary>
        /// <returns></returns>
        public static GradientTexture Create()
        {
            // Create a new instance of the texture asset ScriptableObject
            GradientTexture asset = CreateInstance<GradientTexture>();
            
            // Initialize the texture
            asset.UpdateTexture();
            
            return asset;
        }
        
        /// <summary>
        /// Create new texture and fill with current gradient colors.
        /// </summary>
        /// <returns></returns>
        private void CreateTexture()
        {
            texture = new Texture2D(resolution.x, resolution.y, hdr ? DefaultFormat.HDR : DefaultFormat.LDR,
                    generateMipmaps ? TextureCreationFlags.MipChain : TextureCreationFlags.None) { wrapMode = TextureWrapMode.Clamp };
        }
        
        /// <summary>
        /// Fill pixels of texture with colors from the gradient definition. 
        /// </summary>
        private void FillColors()
        {
            bool projectIsLinear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            bool isHDR = GraphicsFormatUtility.IsHDRFormat(texture.format);
            int width = resolution.x;
            int height = resolution.y;
            
            Color[] pixels = new Color[width * height];
            
            for (int y = 0; y < height; y++)
            {
                float tVertical = verticalLerp.Evaluate((float) y / height);
                
                for (int x = 0; x < width; x++)
                {
                    float tHorizontal = (float) x / width;
                    Color color;
                    
                    if (useTwoGradients)
                    {
                        Color bottomColor = horizontalBottom.Evaluate(tHorizontal);
                        Color topColor = horizontalTop.Evaluate(tHorizontal);
                        color = Color.Lerp(bottomColor, topColor, tVertical);
                    }
                    else
                    {
                        color = horizontalTop.Evaluate(tHorizontal);
                    }
                    
                    if (isHDR)
                    {
                        color = sRGB && projectIsLinear ? color.linear : color;
                    }
                    else
                    {
                        color = sRGB && projectIsLinear ? color : color.gamma;
                    }
                    
                    pixels[y * width + x] = color;
                }
            }
            
#if UNITY_EDITOR
            
            // must be set for the importer to correctly recognize the alpha channel
            texture.alphaIsTransparency = true;
#endif
            
            texture.SetPixels(pixels);
            texture.Apply();
        }
        
        public bool Equals(Texture2D other)
        {
            return texture.Equals(other);
        }
        
        /// <summary>
        /// Update the texture with current gradient values, call this after changing any properties.
        /// Re-initializes the texture if necessary and matches texture name to scriptable object.
        /// </summary>
        public void UpdateTexture()
        {
            if (!texture)
            {
                CreateTexture();
            }
            
            if (texture.name != name)
            {
                texture.name = name;
            }
            
            if (texture.width != resolution.x || texture.height != resolution.y || IsHDRFormat != hdr ||
                texture.mipmapCount == 1 == generateMipmaps // mip map minimum is 1
               )
            {
#if UNITY_2022_1_OR_NEWER
                texture.Reinitialize(resolution.x, resolution.y, SystemInfo.GetGraphicsFormat(hdr ? DefaultFormat.HDR : DefaultFormat.LDR),
                        generateMipmaps);
#else
                    texture.Resize(resolution.x, resolution.y);
#endif
            }
            
            FillColors();
            SetDirtyTexture();
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
            if (texture != null && texture.name != name)
            {
                texture.name = name;
            }
#endif
        }
    }
}
