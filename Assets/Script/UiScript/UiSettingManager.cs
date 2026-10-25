using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;
using UnityEngine.UI;
using System;
using System.Collections.Generic;

public class UiSettingManager : MonoBehaviour
{
    /// <summary>
    /// 画面や音の設定をスライダーに反映させるためのスクリプト
    /// </summary>
     public enum SliderType
    {
        Bgm,
        Se,
        Voice,
        Brightness,
        ColorAdjustment,
        Bloom
    }
    //設定したステータスに合ったスライダーを設定
    [Serializable]
    public class Sliders
    {
        public SliderType SliderType;
        public UnityEngine.Events.UnityEvent<float> SliderAction;
        public Slider Slider;
    }
    [SerializeField] private List<Sliders> SliderList;//インスペクターで設定するリスト
    
     private Dictionary<SliderType, Sliders> _sliderActionList = new();

    //------------AudioSource--------------------//
     private AudioSource _bgmAudiosource;
     private AudioSource _seAudiosource;
     private AudioSource _voiceAudiosource;

    //----------------Dispray---------------//
    private Image _darknessOverlay;

    //---------------PostProcess-------------//
    private Volume _volume;
    private ColorAdjustments _colorAdjustments;
    private Bloom _bloom;

    void OnDestroy()//リストを初期化
    {
        foreach (var slider in _sliderActionList)
        {
            var value = slider.Value;

            value.Slider.onValueChanged.RemoveListener(value.SliderAction.Invoke);//登録されているリスナーを解除
        }
    }

    private void InitList()
    {
        //インスペクターで設定したリストをディクショナリーに登録
        foreach(var list in SliderList)
        {
            _sliderActionList[list.SliderType] = list;
        }
    }
    void Start()
    {
        InitList();
        DarknessInit();
        PostProcessInit();
        AudioInit();
    }
    void DarknessInit()//画面関連の初期化
    {
        _darknessOverlay = GameManager.Instance.FadeImage;
        //設定したタイプでリスナーを登録する
        SliderType[] sliderTypes = new SliderType[] {SliderType.Brightness};
        SetSliderAddListener(sliderTypes);
    }

    void PostProcessInit()//グラフィック関係の初期化
    {
        TryGetVolumeComponent();

        //設定したタイプでリスナーを登録する
        SliderType[] sliderTypes = new SliderType[] { SliderType.Bloom,SliderType.ColorAdjustment};
        SetSliderAddListener(sliderTypes);

        //現在のスライダーを取得
        var bloomslider = GetSlider(SliderType.Bloom);
        var coloradjusmentslider = GetSlider(SliderType.ColorAdjustment);

        // 現在値を反映（SetBloomと同じ0〜5の範囲を0〜1に変換）
        bloomslider.Slider.SetValueWithoutNotify(Mathf.InverseLerp(0f, 5f, _bloom.intensity.value));
        coloradjusmentslider.Slider.SetValueWithoutNotify(Mathf.InverseLerp(-20f, 20f, _colorAdjustments.saturation.value));
     
    }
    private void TryGetVolumeComponent()
    {
        //コンポーネントを取得する
        _volume = GameManager.Instance.Volume;
        if (_volume.profile.TryGet<ColorAdjustments>(out _colorAdjustments)) { }
        if (_volume.profile.TryGet<Bloom>(out _bloom)) { }
    }
    void AudioInit()//音関連の初期化
    {
        _bgmAudiosource = AudioManager.Instance.BGMSource;
        _seAudiosource = AudioManager.Instance.SeSource;
        _voiceAudiosource = AudioManager.Instance.VoiceSource;

        //設定したタイプでリスナーを登録する
        SliderType[] sliderTypes = new SliderType[] { SliderType.Bgm, SliderType.Se,SliderType.Voice };
        SetSliderAddListener(sliderTypes);

        //現在のスライダーを取得
        var bgmSlider = GetSlider(SliderType.Bgm);
        var seSlider = GetSlider(SliderType.Se);
        var voiceSlider = GetSlider(SliderType.Voice);

        // 現在の音量をスライダーに反映
        SetSliderInfo(bgmSlider.Slider, _bgmAudiosource);
        SetSliderInfo(seSlider.Slider, _seAudiosource);
        SetSliderInfo(voiceSlider.Slider, _voiceAudiosource);
    }
    private void SetSliderAddListener(SliderType[] types)
    {
        //新しくスライダーを生成して
        Sliders[] sliders = new Sliders[types.Length + 1];
        for(int i = 0; i < types.Length; i++)
        {
            //スライダーを取得して初期化
            sliders[i] = GetSlider(types[i]);
            InitSliderInfo(sliders[i].Slider);
            //リスナーを登録
            sliders[i].Slider.onValueChanged.AddListener(sliders[i].SliderAction.Invoke);
        }
    }
    private void InitSliderInfo(Slider targetslider)
    {
        //スライダーの範囲を初期化
        targetslider.minValue = 0f;
        targetslider.maxValue = 1f;
    }
    private void SetSliderInfo(Slider targetslider,AudioSource audioSource)
    {
        //スライダーに値をセット
        targetslider.SetValueWithoutNotify(LinearToSlider(audioSource.volume));
    }
    //スライダーの値を変えたときに呼ばれるイベント
    public void SetBrightness(float sliderValue)
    {
        float alpha = Mathf.Lerp(0f, 0.8f, sliderValue); // 0→0, 1→0.8
        Color c = _darknessOverlay.color;
        c.a = alpha;
        _darknessOverlay.color = c;//色を変える
    }
    public void SetSeVolume(float sliderValue)//
    {
        float db = Mathf.Lerp(-20f, 0f, sliderValue);//範囲を制限
        _seAudiosource.volume = DbToLinear(db);//デシベルをリニア変換
        AudioManager.Instance.PlaySe(AudioManager.SeState.Hit);//変えたときにわかるように音を鳴らす
    }
    public void SetBGMVolume(float sliderValue)
    {
        float db = Mathf.Lerp(-20f, 0f, sliderValue); // 0→-20dB, 1→0dB
        _bgmAudiosource.volume = DbToLinear(db);//デシベルをリニア変換
    }
    public void SetVoiceVolume(float sliderValue)
    {
        float db = Mathf.Lerp(-20f, 0f, sliderValue);//範囲を制限
        _voiceAudiosource.volume = DbToLinear(db);//デシベルをリニア変換
        AudioManager.Instance.PlayVoice(AudioManager.VoiceState.Setting);//変えたときにわかるように音を鳴らす
    }

    /// <summary>
    /// スライダーに合うように範囲を決める
    /// </summary>
    public void SetBloom(float sliderValue)
    {
        //ブルームの範囲に合うように制限してから設定
        _bloom.intensity.value = Mathf.Lerp(0f, 5f, sliderValue);
    }

    public void SetColorAdjustments(float sliderValue)
    {
        //スライダーの範囲に合うように制限してから設定
        _colorAdjustments.saturation.value = Mathf.Lerp(-20f, 20f, sliderValue);
    }

    float DbToLinear(float db)//デシベルをリニアに変える
    {
        return Mathf.Pow(10f, db / 20f);// 累乗の形
    }

    float LinearToSlider(float linear)//スライダー用に値を変換させる
    {
        //log10 = 10^2 = 100 = 2;
        //DbToLinear(10^(dB/20))の逆なので常用対数を使う
        float db = Mathf.Log10(Mathf.Max(linear, 0.0001f)) * 20f; // Linear→dB
        return Mathf.InverseLerp(-20f, 0f, db); // -20dB→0, 0dB→1
    }

    Sliders GetSlider(SliderType type)
    {
        //引数のタイプのスライダーを取得
        if (_sliderActionList.TryGetValue(type, out var action))
        {
            return action;
        }
        return null;
    }
}
