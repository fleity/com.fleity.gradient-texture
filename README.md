# GradientTexture

This is based largely on https://github.com/mitay-walle/com.mitay-walle.gradient-texture/tree/main,
but with both runtime and editor code mostly rewritten to add new features, fix warnings and build errors and cleaner separate the code
(fleity).


Unity gradient texture generator,
Texture2D-Gradient generated in Editor by ScriptableObject with Gradient-properties.

[Usage example video ( Youtube )](https://youtu.be/LmBBTqhpsbw)
<br>Shader in example based on [this](https://simonschreibt.de/gat/fallout-4-the-mushroom-case/), can be downloaded [here](https://github.com/mitay-walle/GradientTexture/issues/6)

![](https://github.com/mitay-walle/com.mitay-walle.gradient-texture/blob/main/Documentation/gradientTexture_srgb_inspector_preview.png)
![alt text](https://github.com/mitay-walle/GradientTexture/blob/main/Documentation/Inspector_preview.png?raw=true)

![alt text](https://github.com/mitay-walle/GradientTexture/blob/main/Documentation/drag_drop_as_texture.gif?raw=true)

# Problem

## I. Shader Graph no Exposed Gradient
[You can't expose gradient to material inspector](https://issuetracker.unity3d.com/issues/gradient-property-cant-be-exposed-from-the-shadergraph)

You are forced to use Texture2D-based gradients

[Forum last active thread](https://forum.unity.com/threads/gradients-exposed-property-is-ignored.837970/)

## II. designing VFX with gradients
While designing VFX using gradients you need to tweak colors and positions, according to vfx timings/size etc, what makes you:
1. _optional_ pause vfx
2. _optional_ make screenshot
3. switch Photoshop or rearrange windows to have both (Photoshop and Unity) visible on screen together
4. tweak Gradient as is in Photoshop or according to screenshot, or according to Unity-view
5. save file
6. switch to Unity window 1-2-3 times to reimport Texture or reimport by hand (if Playmode is active?)
7. check visual changes
8. repeat all

# Solution
Texture2D-Gradient generated dynamically during Editor-time by a ScriptableObject with Gradient-properties
<br>I. Exposed in shader graph as Texture2D
<br>II. faster iteration with no need to switch to Photoshop, rearrange windows, save file, reimport etc.
