using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class MaterialBlockControlObject : MonoBehaviour
{
    private static int _mainColorID = UnityEngine.Shader.PropertyToID("_MainColor");
    private static int _cutOffID = UnityEngine.Shader.PropertyToID("_CutOff");
    private static int _metallicID = UnityEngine.Shader.PropertyToID("_Metallic");
    private static int _smoothnessID = UnityEngine.Shader.PropertyToID("_Smoothness");
    private static int _occlusionID = UnityEngine.Shader.PropertyToID("_Occlusion");
    private static int _emissionColorID = UnityEngine.Shader.PropertyToID("_EmissionColor");
    private static int _detailAlbedoID = UnityEngine.Shader.PropertyToID("_DetailAlbedo");
    private static int _detailSmoothnessID = UnityEngine.Shader.PropertyToID("_DetailSmoothness");

    [SerializeField]
    private Color mainColor = Color.white;

    [SerializeField]
    [Range(0, 1)]
    private float cutOff = 0.5f;

    [SerializeField]
    [Range(0, 1)]
    private float metallic = 0.5f;

    [SerializeField]
    [Range(0, 1)]
    private float smoothness = 0.5f;

    [SerializeField]
    [Range(0, 1)]
    private float occlusion = 0.5f;

    [SerializeField, ColorUsage(false, true)]
    private Color emissionColor = Color.black;

    [SerializeField]
    [Range(0, 1)]
    private float detailAlbedo = 0.2f;

    [SerializeField]
    [Range(0, 1)]
    private float detailSmoothness = 0.5f;

    private Renderer _renderer;

    private static MaterialPropertyBlock _block;

    private void Awake()
    {
        OnValidate();
    }

    //只会在Editor下自动运行的代码 在unity加载脚本或检查器中的值被更改时调用
    private void OnValidate()
    {
        if (_block == null)
        {
            _block = new MaterialPropertyBlock();
        }

        _block.SetColor(_mainColorID, mainColor);
        _block.SetFloat(_cutOffID, cutOff);
        _block.SetFloat(_metallicID, metallic);
        _block.SetFloat(_smoothnessID, smoothness);
        _block.SetFloat(_occlusionID, occlusion);
        _block.SetColor(_emissionColorID, emissionColor);
        _block.SetFloat(_detailAlbedoID, detailAlbedo);
        _block.SetFloat(_detailSmoothnessID, detailSmoothness);
        if (_renderer == null)
        {
            _renderer = GetComponent<Renderer>();
        }

        _renderer.SetPropertyBlock(_block);
    }
}