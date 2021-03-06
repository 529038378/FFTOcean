﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class IFFTUtil
{
    #region var
    [System.Serializable]
    public struct InitParam
    {
        public ComputeShader ComputeShader; //用来并行计算的shader
        public int Size; //输入变量大小
        public float Length; //patch尺寸大小
        public RenderTexture BufferFlyLutTex;
        public float JacobScale;
    }

    public bool Done
    {
        get;
        private set;
    }

    public RenderTexture ResHeightTex
    {
        get;
        private set;
    }

    public RenderTexture ResDisplaceTex
    {
        get;
        private set;
    }

    public RenderTexture ResNormalTex
    {
        get
        {
            return m_normal_res_tex;
        }
    }
    public RenderTexture ResJacobTex
    {
        get
        {
            return m_jacob_Res_tex;
        }
    }
    InitParam m_param;
    int m_stage_count = 0;

    int m_kernel;
    int m_post_kernel;
    RenderTexture m_height_ping_tex;
    RenderTexture m_height_pong_tex;
    RenderTexture m_displace_ping_tex;
    RenderTexture m_displace_pong_tex;
    RenderTexture m_normal_res_tex;
    RenderTexture m_debug_tex;

    RenderTexture m_jacob_Res_tex;
    #endregion

    #region  method
    public void InitData(in InitParam param)
    {
        m_param = param;
        InitComputeShaderData();
        OnInit();
    }

    void CloneRenderTexture(RenderTexture source, ref RenderTexture dest)
    {
        if (null == dest)
        {
            InitTex(ref dest);
        }

        Graphics.CopyTexture(source, dest);
    }

    public void SetInputRenderTexture(RenderTexture rt)
    {
        CloneRenderTexture(rt, ref m_height_ping_tex);
        CloneRenderTexture(rt, ref m_displace_ping_tex);
        m_height_ping_tex.name = "IFFTHeight";
        m_displace_ping_tex.name = "IFFTDisplace";
    }

    public void UpdateUI()
    {
        GameObject canvas = GameObject.Find("Canvas");
        GameObject spectrum_obj = canvas?.transform.GetChild(1).gameObject;
        RawImage spectrum_image = spectrum_obj?.GetComponent<RawImage>();
        /*float scale = m_param.Size * 1.0f / 100;
        m_raw_image.rectTransform.localScale = new Vector3(scale, scale, scale);*/
        spectrum_image.texture = ResHeightTex;

        GameObject displace_obj = canvas?.transform.GetChild(2).gameObject;
        RawImage displace_image = displace_obj?.GetComponent<RawImage>();
        displace_image.texture = ResDisplaceTex;

        GameObject normal_obj = canvas?.transform.GetChild(3).gameObject;
        RawImage normal_image = normal_obj?.GetComponent<RawImage>();
        normal_image.texture = ResNormalTex;

        GameObject jacob_obj = canvas?.transform.GetChild(4).gameObject;
        RawImage jacob_image = jacob_obj?.GetComponent<RawImage>();
        jacob_image.texture = ResJacobTex;
        #if _DEBUG_
        GameObject debug_obj = canvas?.transform.GetChild(5).gameObject;
        RawImage debug_image = debug_obj?.GetComponent<RawImage>();
        debug_image.texture = m_debug_tex;
        #endif
    }

    void InitComputeShaderData()
    {
        m_kernel = m_param.ComputeShader.FindKernel(CommonData.IFFTComputeKernelName);
        m_post_kernel = m_param.ComputeShader.FindKernel(CommonData.IFFTPostKernelName);

        m_param.ComputeShader.SetInt(CommonData.IFFTComputeSizeName, m_param.Size);
        m_param.ComputeShader.SetFloat(CommonData.IFFTComputeLenghtName, m_param.Length);
        if (null != m_param.BufferFlyLutTex)
        {
            m_param.ComputeShader.SetTexture(m_kernel, CommonData.IFFTLutTexName, m_param.BufferFlyLutTex);
        }
    }
    
    void InitPingTex()
    {
        //设置ping pong buff
        m_param.ComputeShader.SetTexture(m_kernel, CommonData.IFFTComputeHeightPingBufferName, m_height_ping_tex);
        m_param.ComputeShader.SetTexture(m_kernel, CommonData.IFFTComputeDisplacePingBufferName, m_displace_ping_tex);
    }

    void NormalPost(bool ping)
    {
        //梯度转normal
        m_param.ComputeShader.SetTexture(m_post_kernel, CommonData.IFFTComputeNormalResBufferName, m_normal_res_tex);
    }
    void JacobPost(bool ping)
    {
        //尖浪使用jacoba行列式进行判断
        m_param.ComputeShader.SetTexture(m_post_kernel, CommonData.IFFTComputeJacobInputBufferName, ping ? m_displace_ping_tex : m_displace_pong_tex);
    }
    void PostHandle(bool ping)
    {
        m_param.ComputeShader.SetInt(CommonData.IFFTComputeStagePingName, ping ? 1 : 0);
        //后处理的一些操作
        NormalPost(ping);
        JacobPost(ping);

        m_param.ComputeShader.Dispatch(m_post_kernel, m_param.Size / 8, m_param.Size / 8, 1);
    }

    public void Begin()
    {
        Done = false;
    }
    public void Update()
    {
        InitPingTex();
        int i = 0;
        //计算行
        m_param.ComputeShader.SetInt(CommonData.IFFTComputeCalLineName, 1);
        for (; i < m_stage_count; ++i)
        {
            CalStageOutput(i);
        }
        
        //重新对齐一下输入
        bool even = i % 2 != 0;
        //计算列
        m_param.ComputeShader.SetInt(CommonData.IFFTComputeCalLineName, 0);
        for (i = 0; i < m_stage_count; ++i)
        {
            CalStageOutput(i, even);
        }
        bool ping = false;
        if ((i - 1) % 2 != 0)
        {
            ResHeightTex = even ? m_height_pong_tex : m_height_ping_tex;
            ResDisplaceTex = even ? m_displace_pong_tex : m_displace_ping_tex;
            ping = even ? false : true;
        }
        else
        {
            ResHeightTex = even ? m_height_ping_tex : m_height_pong_tex;
            ResDisplaceTex = even ? m_displace_ping_tex : m_displace_pong_tex;
            ping = even ? true : false;
        }
        PostHandle(ping);
        OnDone();
    }

    void CalStageOutput(int stage, bool reverse = false)
    {
        m_param.ComputeShader.SetInt(CommonData.IFFTComputeStageName, stage);
        int GroupSize = (int)Mathf.Pow(2, stage + 1);
        m_param.ComputeShader.SetInt(CommonData.IFFTComputeStageGroupName, GroupSize);
        bool even = stage % 2 != 0;
        int ping = !even ? (reverse ? 0 : 1) : (reverse ? 1 : 0);
        m_param.ComputeShader.SetInt(CommonData.IFFTComputeStagePingName, ping);
        m_param.ComputeShader.Dispatch(m_kernel, m_param.Size / 8, m_param.Size / 8, 1);
        //Debug.Log("[IFFTStageCal] frame : " + Time.frameCount.ToString() + " input : " + ping.ToString());
    }

    void OnDone()
    {
        Done = true;
        UpdateUI();
    }

    void InitTex(ref RenderTexture rt)
    {
        if (null == rt || rt.width != m_param.Size || rt.height != m_param.Size)
        {
            if (null != rt)
            {
                RenderTexture.DestroyImmediate(rt);
            }
            rt = new RenderTexture(m_param.Size, m_param.Size, 32);
            rt.enableRandomWrite = true;
            rt.format = RenderTextureFormat.ARGBFloat;
            rt.wrapMode = TextureWrapMode.Repeat;
            rt.filterMode = FilterMode.Trilinear;
            rt.Create();
        }
    }

    void OnInit()
    {
        m_stage_count = (int)(Mathf.Log(m_param.Size, 2));
        m_param.ComputeShader.SetInt(CommonData.IFFTComputeTotalStageCountName, m_stage_count);

        InitTex(ref m_height_pong_tex);
        InitTex(ref m_displace_pong_tex);
        InitTex(ref m_jacob_Res_tex);
        InitTex(ref m_normal_res_tex);
        #if _DEBUG_
                InitTex(ref m_debug_tex);
                m_param.ComputeShader.SetTexture(m_kernel, CommonData.IFFTDebugTexName, m_debug_tex);
                m_param.ComputeShader.SetTexture(m_post_kernel, CommonData.IFFTDebugTexName, m_debug_tex);
        #endif
        m_param.ComputeShader.SetTexture(m_kernel, CommonData.IFFTComputeHeightPongBufferName, m_height_pong_tex);
        m_param.ComputeShader.SetTexture(m_kernel, CommonData.IFFTComputeDisplacePongBufferName, m_displace_pong_tex);
        m_param.ComputeShader.SetTexture(m_post_kernel, CommonData.IFFTComputeJacobResBufferName, m_jacob_Res_tex);
    }

    public void Leave()
    {
        RenderTexture.DestroyImmediate(m_height_ping_tex);
        RenderTexture.DestroyImmediate(m_height_pong_tex);
        RenderTexture.DestroyImmediate(m_displace_ping_tex);
        RenderTexture.DestroyImmediate(m_displace_pong_tex);
    }
    #endregion

}