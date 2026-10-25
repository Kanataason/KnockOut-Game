using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using static UiUtility;

[RequireComponent(typeof(UiGimmickManager))]
[RequireComponent(typeof(SwitchActionMap))]
public class EventManager : MonoBehaviour
{
    public static EventManager Instance { get; private set; }

    [Header("Reference")]
     private UiGimmickManager gimmickManager;
     private SwitchActionMap switchActionMap;

    private PlayerInput currentUiPlayer;//現在のUI操作デバイス

    private Dictionary<UiState, Action> ActionList;//Uiのステートに紐づいたアクションリスト

    /* Setplayer 用*/

    public event Action<StateClass.UiEventType> OnUpdatePanelAction;//現在のUiパネルを更新するためのアクション
    public event Action OnProcessedAction;//デバイスの際ペアリングをする際のアクション
    public event Action OnSelectCancelAction;//プレイヤーがキャンセルボタンを押した時のアクション

    //シーン上に一つしかないことを保証
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            //後から生成された方を破棄する
            Destroy(gameObject);
        }
    }
    private void OnDestroy()
    {
        //破棄された重複側が本物の参照を消さないようにする
        if (Instance == this)
            Instance = null;
    }
    private void OnDisable()//イベントの解除
    {
        if (InputUserMakeManager.Instance != null)
            InputUserMakeManager.Instance.OnCreateCharacter -= OnSetCharacter;
    }
    private void SubscribeEvents()//イベントの登録
    {
        if (InputUserMakeManager.Instance != null)
        {
            InputUserMakeManager.Instance.OnCreateCharacter += OnSetCharacter;

            //先に生成されていた時のために今の操作デバイスも受け取る
            var current = InputUserMakeManager.Instance.GetCurrentUiPlayerInput();
            if (current != null) OnSetCharacter(current);
        }
    }
    private void OnSetCharacter(PlayerInput input)//現在Uiを操作しているUserをイベントでセット
    {
        currentUiPlayer = input;
    }
    private void Start()
    {
        TryGetScriptComponent();
        SubscribeEvents();
        InitList();
    }
    private void InitList()
    {
        //リストの初期化 ステータスを設定、アクションを設定
        ActionList = new Dictionary<UiState, Action>()
        {
            {UiState.GameStart,CancelGameStart},
            {UiState.SetPlayer,CancelSetPlayer},
            {UiState.Setting,CancelSetting},
            {UiState.Manual,CancelManual},
            {UiState.SelectCharacter,CancelSelectCharacter },
            {UiState.Battle, PlayPause},
            {UiState.Pause,CancelPause },
            {UiState.SettingInfo,CancelSettingInfo},
            {UiState.ManualInfo,CancelManualInfo }
        };
    }
    private void TryGetScriptComponent()//コンポーネントを取得
    {
        TryGetComponent<UiGimmickManager>(out gimmickManager);
        TryGetComponent<SwitchActionMap>(out switchActionMap);
    }
    public void GameStart() => UpdatePanel(StateClass.UiEventType.GameStart);//ゲームメニュー用のパネルを表示
    public void BattleStart() => UpdatePanel(StateClass.UiEventType.BattleStart);//セレクトパネルを非表示にしてバトルシーンに遷移
    public void BattlePauseCancel()//ポーズ中にプレイヤーがキャンセルボタンを押したら流れる
    {
        UpdatePanel(StateClass.UiEventType.BattlePauseExit);//ポーズパネルを消す
    }
    public void BattleResult() => UpdatePanel(StateClass.UiEventType.Result);//リザルト用のパネルに変更
    public void BattlePauseExit()//ポーズ画面からホーム画面に遷移ボタンを押したら流れる
    {
        //プレイヤーの入力をUiに変える
        if (BattleStateManager.Instance != null) BattleStateManager.Instance.OnDestroys();
        gimmickManager.OnSetActive(StateClass.UiEventType.BattlePauseToMenu);

    }
    public void NextRound()//バトル終了時に継続か終わるかを判断
    {
        BattleStateManager.Instance.NextRound();
        gimmickManager.SetNextRoundUiInfo();
        AudioManager.Instance.RandomBGM();//ランダムなBGMを流す
    }
    public void RaiseEvent()
    {
        OnProcessedAction?.Invoke();//Ui操作用のデバイスを生成
    }
    public void CharacterSelect()//ここらの引数すべてにintがたを設定する
    {
        if (BattleStateManager.Instance != null) 
            BattleStateManager.Instance.OnDestroys();//ゴミをなくすためにインスタンスを消す

        PlayerDataManager.Instance.CharacterReset();//キャラクターの情報だけを消す

        AudioManager.Instance.PlayBGM(AudioManager.BGMState.Select);//セレクト用のBGMを流す

        GameManager.Instance.ChangeState(UiState.SelectCharacter);//ステータスを変える

        GameManager.Instance.ChangeScene(StageInfo.SelectCharacter);//シーンを変更
    }
    public void SetPlayer()//1p,2p選択時に呼ばれる
    {
        UpdatePanel(StateClass.UiEventType.SetPlayerStart);//プレイヤー選択用のパネルに変更
        PlayerDataManager.Instance.ClearPlayerData();//データに保存されている情報をすべて消す
    }

    public void SettingReturn() => EscCancel();//セッティングの際プレイヤーがキャンセルを押したら流れる
    public void MainGameVsStart()//セレクトシーンに遷移時に呼ばれる
    {
        InputUserMakeManager.Instance.RemoveUser();

        InputUserMakeManager.Instance.BreakAndRemoveUser();//リストに入っているプレイヤーを破棄

        TimeLineManager.Instance.PlayTimeLine(TimeLineManager.TimeLineState.MainGame);

        UpdatePanel(StateClass.UiEventType.MainGameStart);//メインゲーム用のパネルを消す

    }
    public void EscCancel()//Escをおしたら呼ばれる
    {
        //現在のUiステータスに紐づいているアクションがあれば取得して実行
        if(!ActionList.TryGetValue(GameManager.Instance.CurrentUiState,out var action))
        {
            return;
        }
        action.Invoke();//実行
    }
    #region
    private void CancelGameStart()
    {
        UpdatePanel(StateClass.UiEventType.GameStartCancel);
        AudioManager.Instance.PlaySe(AudioManager.SeState.Return);
    }
    private void CancelSetPlayer()//1p,2p選択画面キャンセル時呼ばれる
    {
        PlayerDataManager.Instance.ClearPlayerData();
        InputUserMakeManager.Instance.UiSetUser();
        UpdatePanel(StateClass.UiEventType.GameStart);
        AudioManager.Instance.PlaySe(AudioManager.SeState.Return);
    }
    private void CancelSetting()
    {
        switchActionMap.PlayerInputUiModuleDisable(currentUiPlayer);

        UpdatePanel(StateClass.UiEventType.SettingCancel);
        UpdatePanel(StateClass.UiEventType.SettingPanelCancel);
        AudioManager.Instance.PlaySe(AudioManager.SeState.Return);
    }
    private void CancelManual()
    {
        switchActionMap.PlayerInputUiModuleDisable(currentUiPlayer);

        UpdatePanel(StateClass.UiEventType.ManualCancel);
        AudioManager.Instance.PlaySe(AudioManager.SeState.Return);
    }
    private void CancelSelectCharacter()
    {
        OnSelectCancelAction?.Invoke();
        AudioManager.Instance.PlaySe(AudioManager.SeState.Return);
    }
    private void CancelPause() => UpdatePanel(StateClass.UiEventType.BattlePauseExit);//ポーズパネルを閉じるとき呼ばれる
    private void CancelSettingInfo() => UpdatePanel(StateClass.UiEventType.SettingPanelCancel);//セッティングの各パネルを閉じる際に呼ばれる
    private void CancelManualInfo() => UpdatePanel(StateClass.UiEventType.ManualPanelCancel);//マニュアルの各パネルを閉じる際に流れる
    private void PlayPause()//バトル中にポーズ画面を開く際呼ばれる
    {
        if (BattleStateManager.Instance == null) return;

        if (BattleStateManager.Instance.CurrentBattleState is not BattleStateManager.BattleState.GamePlaying) return;

        UpdatePanel(StateClass.UiEventType.BattlePause);
    }
    #endregion
    //-------------Menu表示ボタンアクション------//
    public void MenuMove()=> UpdatePanel(StateClass.UiEventType.ManualMove);//マニュアルのMove操作パネルを表示
    public void MenuAttack() => UpdatePanel(StateClass.UiEventType.ManualAttack);//マニュアルのAttack操作パネルを表示
    public void MenuUi() => UpdatePanel(StateClass.UiEventType.ManualUi);//マニュアルのUi操作パネルを表示

    //マニュアル時にプレイヤーがキャンセルを押したら流れる
    public void CancelMenu() => UpdatePanel(StateClass.UiEventType.ManualPanelCancel);//マニュアルのパネルを非表示にする

    //--------------------Setting------------------------
    public void SettingOnClick(int type)//セッティング時に決定ボタンキャンセルボタンを押したら流れる
    {
        //セッティング時のステータスによって処理を変える
            switch (type)
        {
            case 0:gimmickManager.OnSetActive(StateClass.UiEventType.SettingDisplay);break;
            case 1:gimmickManager.OnSetActive(StateClass.UiEventType.SettingVolume);break;
            case 2:gimmickManager.OnSetActive(StateClass.UiEventType.SettingController);break;
            case 3:gimmickManager.OnSetActive(StateClass.UiEventType.SettingPanelCancel);break;
        }
    }
    public void Staffroll() => GameManager.Instance.ChangeScene(StageInfo.Staffroll);//スタッフロール遷移時に呼ばれる
    public void SetSoloKeyBoard()//ソロキーボードボタンを押したら流れる
    {
        GameManager.Instance.ChangeSoloKeyBoard();//ソロキーボードフラグを変更

        //スプライトをソロキーボードフラグによって変更
        gimmickManager.ChangeButtonSprite(StateClass.ButtonState.SettingSoloKeyBoard);
    }
    public void SettingStart()//セッティングボタンを押したら呼ばれる
    {
        PlayerInput input = currentUiPlayer;
        switchActionMap.PlayerInputUiModuleDisable(input);//Uiを無効にしてプレイヤーの入力を不可にする

        TimeLineManager.Instance.PlayTimeLine(TimeLineManager.TimeLineState.Setting);//セッティング用のtimelineを流す

        UpdatePanel(StateClass.UiEventType.SettingStart);//セッティング用のパネルを出す
    }
    //--------------------

    //----------------Manual-----------------------
    public void ManualStart()//マニュアルボタンを押したら呼ばれる
    {
        PlayerInput input = currentUiPlayer;
        //Uiを無効にしてプレイヤーの入力を不可にする
        if (input != null) switchActionMap.PlayerInputUiModuleDisable(input);

        UpdatePanel(StateClass.UiEventType.ManualStart);//マニュアル用のパネルを出す
    }
    public void OnManualTimelineFinished()//マニュアル用のたいむらいんを再生終えたら呼ばれる
    {
        PlayerInput input = currentUiPlayer;
        //Uiを有効にしてプレイヤーの入力を可能にする
        if (input != null) switchActionMap.PlayerInputUiModuleEnable(input);
    }
    //--------------------------------
    public void Startfade(float duration, float startAlpha,float endAlpha)
    {
        //引数によってフェードアウトかフェードインかを分ける
        gimmickManager.StartFade(duration,startAlpha,endAlpha);
    }
    public void IdleVideoPlayer(int IsStop)//ビデオ再生時、終了時に呼ばれる
    {
        gimmickManager.IdleVideoPlay(IsStop);//再生、ストップかを変更
    }
    public void ResetFirstSelectButton()
    {
        //最初に選ぶボタンの初期化
        gimmickManager.CheckSelectButton(StateClass.ButtonState.SelectCharacterStart);
    }

    public void UpdatePanel(StateClass.UiEventType state)//GimickManagerにイベントタイプを送って処理する関数
    {
        OnUpdatePanelAction?.Invoke(state);
    }
    public void ExitGame() => GameManager.Instance.ExitGame();//ゲームを終了時に呼ばれる関数

    /// <summary>
    /// Uiの入力を切断
    /// </summary>
    /// <param name="playerinput"></param>
    public void UiModuleDisable(PlayerInput playerinput)
    {
        switchActionMap.PlayerInputUiModuleDisable(playerinput);//UiModuleのOff
    }
    /// <summary>
    /// Uiの入力を復活
    /// </summary>
    /// <param name="playerinput"></param>
    public void UiModuleEnable(PlayerInput playerinput)
    {
        switchActionMap.PlayerInputUiModuleEnable(playerinput);//UiModuleのOn
    }
    /// <summary>
    /// Uiの入力を有効にする関数 UnityEventから呼ばれる
    /// </summary>
    public void UiModuleEnableEvent()
    {
        var playerinput = currentUiPlayer;
        switchActionMap.PlayerInputUiModuleEnable(playerinput);//UiModuleのOn
    }
    /// <summary>
    /// プレイヤーの入力コンポーネントを有効にするか無効にするかを確認して変更する関数
    /// </summary>
    public void EnableOrDisablePlayerInput(PlayerInput input,bool IsEnable = true)
    {
        switchActionMap.EnableOrDisablePlayerInput(input,IsEnable);
    }
    /// <summary>
    ///プレイヤーの入力だけを有効か無効かを確認してから変換する関数s
    /// </summary>
    public void ChangeActivePlayerInput(PlayerInput input,bool IsActive = false)
    {
        switchActionMap.ActivePlayerInput(input, IsActive);
    }
}
