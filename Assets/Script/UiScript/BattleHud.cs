using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class BattleHud : MonoBehaviour
{
    [SerializeField] private Image Player1Red;
    [SerializeField] private Image Player2Red;

    [SerializeField] private Image Player1White;
    [SerializeField] private Image Player2White;

     [SerializeField] private Image[] Player1WinMarks; // プレイヤー1の○マーク
    [SerializeField] private Image[] Player2WinMarks; // プレイヤー2の○マーク

    [FormerlySerializedAs("LevelBarsplayer1")] [SerializeField] private Image[] LevelBarsPlayer1; // 3本のゲージ
    [SerializeField] private Image[] LevelBarsPlayer2;

    [SerializeField] private TextMeshProUGUI PowerTimer1;
    [SerializeField] private TextMeshProUGUI PowerTimer2;

    [SerializeField] private TextMeshProUGUI Gauge1;
    [SerializeField] private TextMeshProUGUI Gauge2;

    public List<PlayerInfo> infosList = new List<PlayerInfo>();
    void Start()
    {
        SubscribeEvents();

        //WinUiの更新
        BattleStateManager.Instance.InitDisplayWinUiImage();
    }
    private void OnDisable()
    {
        UnSubscribeEvents();
    }
    private void SubscribeEvents()
    {
        if (BattleStateManager.Instance != null)
            BattleStateManager.Instance.OnChangeWinUiAction += OnUpdateWinUI;
    }
    private void UnSubscribeEvents()
    {
        if (BattleStateManager.Instance != null)
            BattleStateManager.Instance.OnChangeWinUiAction -= OnUpdateWinUI;
        foreach (var info in infosList) // メモリリーク防ぐ
        {
            if (info != null && info.state != null)
            {
                var Plstate = info.state;
                Plstate.OnHpBarChange -= OnHpChanged;
                Plstate.OnOffHpBarChange -= OnUpdateDelayedHpBar;
                Plstate.OnGaugeChange -= OnGaugeChanged;
                Plstate.OnPowerUpTimeChanged -= OnPowerUpTimeChanged;
            }
        }
        infosList.Clear();
    }

    public void InitializePlayerUI(PlayerState state)//生成されたタイミングで設定
    {
        var existingInfo = GetPlayerInfo(state);
        if (existingInfo != null)//event 解除
        {
            //event
            state.OnHpBarChange -= OnHpChanged;
            state.OnOffHpBarChange -= OnUpdateDelayedHpBar;
            state.OnGaugeChange -= OnGaugeChanged;
            state.OnPowerUpTimeChanged -= OnPowerUpTimeChanged;
        }
        PlayerInfo info = null;
        if (state.PlayerId == PlayerIds.Player1)
        {
            if (infosList.Count < 1)
                infosList.Add(new PlayerInfo());
            info = infosList[0];
        }
        else if (state.PlayerId == PlayerIds.Player2)
        {
            if (infosList.Count < 2)
                infosList.Add(new PlayerInfo());
            info = infosList[1];
        }
        info.NowHp = state.CurrentHp;
        info.MaxHp = state.MaxHp;
        info.NowGauge = state.CurrentPlayerGauge;
        info.MaxGauge = state.PlayerMaxGauge;
        info.state = state;

        state.OnHpBarChange += OnHpChanged;
        state.OnOffHpBarChange += OnUpdateDelayedHpBar;
        state.OnGaugeChange += OnGaugeChanged;
        state.OnPowerUpTimeChanged += OnPowerUpTimeChanged;
    }
    private void OnUpdateWinUI(int Player1Win, int Player2Win)
    {
        UpdateWinMarks(Player1WinMarks, Player1Win);
        UpdateWinMarks(Player2WinMarks, Player2Win);
    }
    private void UpdateWinMarks(Image[] marks, int winCount)
    {
        for (int i = 0; i < marks.Length; i++)
        {
            marks[i].color = i < winCount ? Color.red : Color.white;
        }
    }
    private void OnGaugeChanged(PlayerState state,bool IsAdd,float value)
    {
        UpdatePlayerGaugeUI(state, value);
    }
    private void UpdatePlayerGaugeUI(PlayerState state, float value)
    {
        var gaugeText = state.PlayerId == PlayerIds.Player1 ? Gauge1 : Gauge2;
        var gaugeBar = state.PlayerId == PlayerIds.Player1 ? LevelBarsPlayer1 : LevelBarsPlayer2;

        //gaugeText.text = ((int)value).ToString();
        UpdateGauge(value, gaugeBar);
    }
    private void RefreshGaugeUI(float value, Image[] gauge, PlayerState state)
    {
        float remaining = value;

        // UI更新
        float temp = remaining; // 残量をコピーしてUI計算用に使う
        for (int i = 0; i < gauge.Length; i++)
        {
            if (temp >= 10000f)
            {
                gauge[i].fillAmount = 1f;
                temp -= 10000f;
            }
            else if (temp > 0f)
            {
                float fill = temp / 10000f;
                gauge[i].fillAmount = fill;
                temp = 0f;
            }
            else
            {
                gauge[i].fillAmount = 0f; // 残量が無ければ0をセット
            }
        }
    }
    private void UpdateGauge(float value, Image[] gauge)
    {
        float remaining = value;

        for (int i = 0; i < gauge.Length; i++)
        {
            if (remaining >= 10000f)
            {
                gauge[i].fillAmount = 1f;
                remaining -= 10000f;
            }
            else
            {
                gauge[i].fillAmount = remaining / 10000f;
                remaining = 0f;
            }
        }
    }

    //パワーアップの残り時間を表示する 時間はPlayerStateが数えるのでここでは表示だけをする
    private void OnPowerUpTimeChanged(PlayerState state,float remainingTime)
    {
        var text = state.PlayerId == PlayerIds.Player1 ? PowerTimer1 : PowerTimer2;
        if (text == null) return;

        //0になったら消す
        text.text = remainingTime > 0 ? ((int)remainingTime).ToString() : "";
    }

    private void OnHpChanged(PlayerState state)
    {
        SetWhiteHpBarVisible(true, state);
        UpdateHpBar(state, true);
    }
    private void OnUpdateDelayedHpBar(PlayerState state)
    {
        SetWhiteHpBarVisible(false,state);
        UpdateHpBar(state, false);
    }
    private void UpdateHpBar(PlayerState state, bool isRedBar)
    {
        SetWhiteHpBarVisible(isRedBar, state);

        var info = GetPlayerInfo(state);
        if (info == null) return;

        Image target = state.PlayerId == PlayerIds.Player1
            ? (isRedBar ? Player1Red : Player1White)
            : (isRedBar ? Player2Red : Player2White);

        target.fillAmount = state.CurrentHp / info.MaxHp;
    }
    private PlayerInfo GetPlayerInfo(PlayerState state)=> infosList.Find(i => i.state == state);
    private void SetWhiteHpBarVisible(bool IsOn,PlayerState state)
    {
        Image target = state.PlayerId == PlayerIds.Player1 ? Player1White : Player2White;
        Color c = target.color;
        c.a = IsOn ? 1 : 0;
        target.color = c;
    }

}

[Serializable] 
public class PlayerInfo
{
    public float NowHp;
    public float MaxHp;
    public float NowGauge;
    public float MaxGauge;
    public PlayerState state;
}
