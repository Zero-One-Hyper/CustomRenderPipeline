using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class CustomRenderLitGUI : ShaderGUI
{
    private MaterialEditor _editor;
    private Object[] _materials;
    private MaterialProperty[] _materialProperties;

    private bool Clipping
    {
        set => SetProperty("_Clipping", "_CLIPPING", value);
    }

    private bool PremultiplyAlpha
    {
        set => SetProperty("_PREMULTIPLYALPHA", "_PREMULTIPLYALPHA", value);
    }

    private UnityEngine.Rendering.BlendMode SrcBlend
    {
        set => SetProperty("_SrcBlend", (float)value);
    }

    private UnityEngine.Rendering.BlendMode DstBlend
    {
        set => SetProperty("_DstBlend", (float)value);
    }

    private bool ZWrite
    {
        set => SetProperty("_ZWrite", value ? 1f : 0f);
    }

    private RenderQueue RenderQueue
    {
        set
        {
            foreach (Material m in _materials)
            {
                m.renderQueue = (int)value;
            }
        }
    }

    private bool HasProperty(string name) =>
        FindProperty(name, _materialProperties, false) != null;

    //检查预乘Alpha是否存在
    private bool HasPremultiplyAlpha => HasProperty("_PREMULTIPLYALPHA");

    private bool _showPresets = false;

    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        base.OnGUI(materialEditor, properties);
        _editor = materialEditor;
        _materials = materialEditor.targets;
        _materialProperties = properties;

        EditorGUI.BeginChangeCheck();
        if (EditorGUI.EndChangeCheck())
        {
            BakenEmission();
            CopyLightMappingProperties();
        }
        /*
        EditorGUILayout.Space();
        _showPresets = EditorGUILayout.Foldout(_showPresets, "Presets", true);
        if (_showPresets)
        {
            OpaquePreset();
            ClipPreset();
            FadePreset();
            TransparentPreset();
        }
        */
    }

    private void BakenEmission()
    {
        EditorGUI.BeginChangeCheck();
        //开启烘培emission到光照贴图中（必须手动设置）
        _editor.LightmapEmissionProperty();
        //Unity会积极尝试避免在烘焙时使用单独的emission通道。
        //如果材质的emission 设置为零的话，还会直接将其忽略。
        //但是，它没有限制单个对象的材质属性。
        //通过更改emission mode，被选定的材质的globalIlluminationFlags属性的默MaterialGlobalIlluminationFlags.EmissiveIsBlack标志，
        //可以覆盖该结果。这意味着你仅应在需要时才启用“Baked ”选项。
        if (EditorGUI.EndChangeCheck())
        {
            foreach (Material material in _editor.targets)
            {
                material.globalIlluminationFlags &= ~MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }
        }
    }

    private void CopyLightMappingProperties()
    {
        MaterialProperty mainTex = FindProperty("_MainTex", _materialProperties);
        MaterialProperty baseTex = FindProperty("_BaseMap", _materialProperties);
        if (mainTex != null && baseTex != null)
        {
            mainTex.textureValue = baseTex.textureValue;
            mainTex.textureScaleAndOffset = baseTex.textureScaleAndOffset;
        }

        MaterialProperty mainColor = FindProperty("_MainColor", _materialProperties);
        MaterialProperty baseColor = FindProperty("_BaseColor", _materialProperties);
        if (mainColor != null && baseColor != null)
        {
            mainColor.colorValue = baseColor.colorValue;
        }
    }

    private bool PresetButton(string name)
    {
        if (GUILayout.Button(name))
        {
            _editor.RegisterPropertyChangeUndo(name);
            return true;
        }

        return false;
    }

    private void OpaquePreset()
    {
        if (PresetButton("Opaque"))
        {
            Clipping = false;
            PremultiplyAlpha = false;
            SrcBlend = UnityEngine.Rendering.BlendMode.One;
            DstBlend = UnityEngine.Rendering.BlendMode.Zero;
            ZWrite = true;
            RenderQueue = RenderQueue.Geometry;
        }
    }

    private void ClipPreset()
    {
        if (PresetButton("Clip"))
        {
            Clipping = true;
            PremultiplyAlpha = false;
            SrcBlend = UnityEngine.Rendering.BlendMode.One;
            DstBlend = UnityEngine.Rendering.BlendMode.Zero;
            ZWrite = true;
            RenderQueue = RenderQueue.AlphaTest;
        }
    }

    private void FadePreset()
    {
        if (PresetButton("Fade"))
        {
            Clipping = false;
            PremultiplyAlpha = false;
            SrcBlend = UnityEngine.Rendering.BlendMode.SrcAlpha;
            DstBlend = UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha;
            ZWrite = false;
            RenderQueue = RenderQueue.Transparent;
        }
    }

    private void TransparentPreset()
    {
        if (HasPremultiplyAlpha && PresetButton("Transparent"))
            if (PresetButton("Transparent"))
            {
                Clipping = false;
                PremultiplyAlpha = true;
                SrcBlend = UnityEngine.Rendering.BlendMode.One;
                DstBlend = UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha;
                ZWrite = false;
                RenderQueue = RenderQueue.Transparent;
            }
    }

    private bool SetProperty(string name, float value)
    {
        MaterialProperty property = FindProperty(name, _materialProperties, false);
        if (property != null)
        {
            property.floatValue = value;
            return true;
        }

        return false;
    }

    private void SetKeyword(string keyWorld, bool enabled)
    {
        if (enabled)
        {
            foreach (Material material in _materials)
            {
                material.EnableKeyword(keyWorld);
            }
        }
        else
        {
            foreach (Material material in _materials)
            {
                material.DisableKeyword(keyWorld);
            }
        }
    }

    //keyword版本的设置property
    private void SetProperty(string name, string keyword, bool value)
    {
        if (SetProperty(name, value ? 1f : 0f))
        {
            SetKeyword(keyword, value);
        }
    }
}