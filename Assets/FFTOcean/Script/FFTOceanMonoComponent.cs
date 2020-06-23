﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

public class FFTOceanMonoComponent : MonoBehaviour
{
    #region var
    [System.Serializable]
    public struct MatParam
    {
        public float HorizonScale;
        public float VerticalScale;
    }

    [SerializeField]
    public IFFTOceanInitConfig InitParamData;
    SpectrumUtil m_spectrum_util = new SpectrumUtil();
    IFFTUtil m_ifft_util = new IFFTUtil();
    #endregion

    #region  method
    void InitConfig()
    {
        IFFTOceanInitConfig param = CommonUtil.LoadAsset(UICommonData.IFFTOceanInitConfig, typeof(IFFTOceanInitConfig)) as IFFTOceanInitConfig;
        //主动更新一下最新的rendertexture
        param.IFFTParam.BufferFlyLutTex = AssetDatabase.LoadAssetAtPath(UICommonData.IFFTOceanLutTexPath, typeof(RenderTexture)) as RenderTexture;
        InitData(param);
    }

    void Start()
    {
        InitConfig();
    }

    void InitSpectrum()
    {
        m_spectrum_util.InitData(InitParamData.SpectrumParam);
        Debug.Log("[SpectrumUtil] init done");
    }

    void InitIFFTUtil()
    {
        m_ifft_util.InitData(InitParamData.IFFTParam);
        Debug.Log("[IFFTUtil] init done");
    }
    void InitMat()
    {
        Material mat = GetComponent<MeshRenderer>().sharedMaterial;
        float[] scales = {InitParamData.OceanMatParam.HorizonScale, InitParamData.OceanMatParam.VerticalScale, InitParamData.OceanMatParam.HorizonScale};
        mat?.SetFloatArray(CommonData.OceanMatScaleName, scales);
    }
    public void InitData(IFFTOceanInitConfig initParam)
    {
        InitParamData = initParam;

        InitSpectrum();
        InitIFFTUtil();
        InitMat();
    }

    void Update()
    {
        //1、生成spectrum
        GenSpectrum();

        //2、根据specturm生成高度图
        IFFTUpdate();

        //3、更新高度图到材质
        UpdateMatTexes();
    }

    void UpdateMatTexes()
    {
        Material mat = GetComponent<MeshRenderer>().sharedMaterial;
        //CommonUtil.SaveRenderTextureToPNG(m_ifft_util.ResTex, UICommonData.IFFTOceanHeightMapPath);
        mat?.SetTexture(Shader.PropertyToID(CommonData.OceanMatHeightTexName), m_ifft_util.ResHeightTex);
        mat?.SetTexture(Shader.PropertyToID(CommonData.OceanMatDisplaceTexName), m_ifft_util.ResDisplaceTex);
        mat?.SetTexture(Shader.PropertyToID(CommonData.OceanMatNormalTexName), m_ifft_util.ResNormalTex);
        mat?.SetTexture(Shader.PropertyToID(CommonData.OceanMatJacobTexName), m_ifft_util.ResJacobTex);
    }

    void IFFTUpdate()
    {
        //更新高度图
        m_ifft_util.SetInputRenderTexture(m_spectrum_util.ResTex);
        m_ifft_util.Update();
    }

    void GenSpectrum()
    {
        m_spectrum_util.Execute();
        //Debug.Log("[SpectrumUtil] execute done. time : " + Time.time.ToString());
    }

    void OnDestory()
    {
        m_spectrum_util.Leave();
        m_spectrum_util = null;
        m_ifft_util.Leave();
        m_ifft_util = null;
    }

    #endregion
}
