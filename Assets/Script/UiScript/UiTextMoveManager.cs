using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// バトル中の文字演出（ラウンド開始、KO、タイムオーバー、勝敗）を表示するスクリプト
/// 表示して終わったら戻るだけで、試合をいつ進めるかはBattleStateManagerが決める
/// </summary>
public class UiTextMoveManager : MonoBehaviour
{
    public class ResultText
    {
        public string First;
        public string Second;

        public ResultText(string first, string second)
        {
            First = first;
            Second = second;
        }
    }
    //テキストの処理のステート
    private enum CoroutineState
    {
        Dead,
        TimeOver,
        Round,
        Next,
        Win
    }

    [SerializeField] TextMeshProUGUI timerText;

    //前フレームの値を保存
    private int lastTime = 0;

    //テキストの種類に合ったIEnumeratorを保存するリスト
    private Dictionary<CoroutineState, Func<UiTextMover, IEnumerator>> coroutineList = new();

    private Dictionary<CoroutineState, ResultText> resultTexts = new();

    [SerializeField] private GameObject UiPanel;

    [SerializeField] private UiTextMover mainText;
    [SerializeField] private UiTextMover subText;

    //ラウンド開始の文字を出すまでの間
    private const float RoundStartDelay = 1.2f;

    //KOやタイムオーバーの後、HUDを消すまでの間
    private const float HideHudDelay = 1f;

    //勝者の文字を出すまでの間
    private const float WinTextDelay = 1f;

    private void OnDisable()
    {
        ResetText();

        if (BattleStateManager.Instance != null)
            BattleStateManager.Instance.RemovePresenter(this);
    }
    private void Start()
    {
        //リスト初期化
        InitList();

        //テキストに出す文字をバッファーに保存
        SetTimerText();

        //進行役に文字演出の担当として登録する
        if (BattleStateManager.Instance != null)
            BattleStateManager.Instance.SetPresenter(this);
    }
    private void InitList()
    {
        //リストに追加
        coroutineList.Add(CoroutineState.Round, PlayTextAnimation);
        coroutineList.Add(CoroutineState.Dead, PlayDeadTextAnimation);
        coroutineList.Add(CoroutineState.TimeOver, PlayDeadTextAnimation);
        coroutineList.Add(CoroutineState.Next, PlayTextAnimation);

        resultTexts.Add(CoroutineState.Round, new ResultText("Round{0}", "Fight"));
        resultTexts.Add(CoroutineState.Dead, new ResultText("Knock", "Out"));
        resultTexts.Add(CoroutineState.TimeOver, new ResultText("Time", "Over"));
        resultTexts.Add(CoroutineState.Win, new ResultText("Player{0}Win", "Draw Game"));
        resultTexts.Add(CoroutineState.Next, new ResultText("Round{0}", "Fight"));
    }
    public void SetTimerText()
    {
        //GCを少なくするためにバッファーにする
        timerText.SetText("{0}", (int)BattleStateManager.Instance.RemainingTime);
    }

    //---------------ここからBattleStateManagerから呼ばれる演出 終わったら戻る---------------

    /// <summary>
    /// 「Round Fight」を表示する
    /// </summary>
    public IEnumerator PlayRoundStart(bool isFirstRound)
    {
        var state = isFirstRound ? CoroutineState.Round : CoroutineState.Next;

        //テキスト設定
        ResetText();
        SetTimerText();
        SetActiveText(subText);

        //少し待ってから開始テキストを出す
        yield return new WaitForSeconds(RoundStartDelay);

        yield return DisplayTextSequence(state, mainText, subText);
    }

    /// <summary>
    /// 「Knock Out」を表示する
    /// </summary>
    public IEnumerator PlayKnockOut()
    {
        ResetText();
        //テキスト設定
        SetActiveText(mainText, true);
        SetActiveText(subText, true);

        //ボイス再生
        AudioManager.Instance.PlayVoice(AudioManager.VoiceState.KnockOut);

        yield return DisplayTextSequence(CoroutineState.Dead, mainText, subText);

        //少し待ってからHUDを消す
        StartCoroutine(HideHudLater());
    }

    /// <summary>
    /// 「Time Over」を表示する
    /// </summary>
    public IEnumerator PlayTimeOver()
    {
        ResetText();
        //テキスト設定
        SetActiveText(mainText, true);
        SetActiveText(subText, true);

        yield return DisplayTextSequence(CoroutineState.TimeOver, mainText, subText);

        //少し待ってからHUDを消す
        StartCoroutine(HideHudLater());
    }

    /// <summary>
    /// 勝者か引き分けを表示する displayIdは1:Player1 2:Player2 3:引き分け
    /// </summary>
    public IEnumerator PlayWinner(int displayId)
    {
        var textClass = GetTextInfo(CoroutineState.Win);
        bool isDraw = displayId == BattleStateManager.DrawDisplayId;

        //テキスト設定
        SetActiveText(mainText, true);
        SetActiveText(subText);
        string text = isDraw ? textClass.Second : textClass.First;

        //指定した時間待つ
        yield return new WaitForSeconds(WinTextDelay);

        //BGM再生
        AudioManager.Instance.PlayBGM(AudioManager.BGMState.Win);

        if (!isDraw)
        {
            AudioManager.Instance.PlayVoice(AudioManager.VoiceState.Win);
            text = string.Format(text, displayId);
        }

        mainText.text.text = text;

        //再生が終わるまで待つ
        yield return mainText.PlayWinText();
    }

    /// <summary>
    /// HUD（体力ゲージなど）の表示を切り替える
    /// </summary>
    public void ShowHud(bool isActive) => ActiveUiPanel(isActive);

    //------------------------------------------------------------------------------

    /// <summary>
    /// 順番にテキストを表示しては消す処理
    /// </summary>
    /// <param name="state"></param>
    /// <param name="texts"></param>
    /// <returns></returns>
    private IEnumerator DisplayTextSequence(CoroutineState state, params UiTextMover[] texts)
    {

        //存在してなかったら返す
        if (!coroutineList.TryGetValue(state, out var coroutine))
            yield break;

        //引数で渡された数だけ回す
        foreach (var text in texts)
        {
            SetActiveText(text,true);
            //ここでテキストをセット
            UpdateText(text, state, text.GetTextPosition());

            //処理
            yield return StartCoroutine(coroutine(text));

            //ここでオブジェクトの非、表示
            if (state != CoroutineState.TimeOver &&  state != CoroutineState.Dead)
            {
                SetActiveText(text);
            }
        }

        //透明にする
        foreach (var text in texts)
        {
            text.InitTextColor();
        }
    }
    private IEnumerator HideHudLater()
    {
        yield return new WaitForSeconds(HideHudDelay);

        ActiveUiPanel();
    }
    /// <summary>
    /// テキストを表示させてから消す処理
    /// </summary>
    /// <param name="currentText"></param>
    /// <returns></returns>
    private IEnumerator PlayTextAnimation(UiTextMover currentText)
    {
        CheckFirstRoundText(currentText);
        //表示処理
        yield return StartCoroutine(currentText.FadeText());

        //消す処理
        yield return StartCoroutine(currentText.StartFade());


    }
    //表示テキストの更新
    private void UpdateText(UiTextMover currentText,CoroutineState state,UiTextMover.TextPosition textPosition)
    {
        if (resultTexts.TryGetValue(state, out var textInfo))
        {
            string text = textPosition is UiTextMover.TextPosition.First
                ? textInfo.First : textInfo.Second;

            if (state is CoroutineState.Round or CoroutineState.Next)
            {
                text = string.Format(text, BattleStateManager.Instance.CurrentRound);
            }
            currentText.text.text = text;
        }
    }
    private void ResetText()
    {
        mainText.text.text = "";
        subText.text.text = "";
    }
    private void ActiveUiPanel(bool isActive = false)
    {
        UiPanel.SetActive(isActive);
    }
    private void CheckFirstRoundText(UiTextMover currentText)//ラウンドテキストを表示しているか確認
    {
        if(currentText.GetTextPosition() is UiTextMover.TextPosition.First)
            AudioManager.Instance.PlayRoundVoice(BattleStateManager.Instance.CurrentRound);
    }
    private IEnumerator PlayDeadTextAnimation(UiTextMover currentText)
    {
        IEnumerator animation = currentText.AnimationStartDeadText();

        //もし対象のテキストならIEnumeratorを変える
        if (currentText.GetTextPosition() is UiTextMover.TextPosition.Second)
        {
            animation = currentText.AnimationEndDeadText();
        }
        //再生
        yield return StartCoroutine(animation);
    }
    private void Update()
    {
        //残り時間をUiに反映
        if (timerText != null && BattleStateManager.Instance != null)
        {
            int time = (int)BattleStateManager.Instance.RemainingTime;
            if (time != lastTime) // 値が変わったときだけ反映
            {
                timerText.SetText("{0}", time);
                lastTime = time;
            }
        }
    }

    private void SetActiveText(UiTextMover text,bool isActive = false)
    {
        if (text == null) return;

        text.gameObject.SetActive(isActive);
    }
    private ResultText GetTextInfo(CoroutineState state)
    {
        if (resultTexts.TryGetValue(state, out var textClass))
        {
            return textClass;
        }
        return null;

    }
}
