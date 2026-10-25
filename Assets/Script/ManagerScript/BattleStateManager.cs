using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using State = StateMachine<BattleStateManager>.State;

public class BattleStateManager : MonoBehaviour, ITickable
{
    /// <summary>
    /// バトルの進行を管理するスクリプト
    /// </summary>
    private StateMachine<BattleStateManager> stateMachine;
    public static BattleStateManager Instance { get; private set; }
    //バトルの状態
    public enum BattleState : int {GameStating,GameEnd,GamePlaying }

    [Header("バトル情報")]
    public int TotalRound = 1;//すべてのラウンド
    public  int CurrentRound = 1;//現在のラウンド数
    public float TimeLimit = 99;//最大バトル時間
    public float RemainingTime =0;//現在のバトル時間
    public int Player1Wins = 0;//プレイヤー１の勝利数
    public int Player2Wins = 0;//プレイヤー2の勝利数
    public bool IsGameEnd = false;//ゲーム終了フラグ
    public BattleState CurrentBattleState = BattleState.GameStating;

    //処理通知アクション
    public event Action<int,int> OnChangeWinUiAction;//現在の勝利数をUiに反映させるためのアクション
    public event Action OnUnPairUserAction;//接続されているデバイスの解除アクション
    public event Action OnGameEndAction;//ラウンド終了アクション
    public event Action OnTimeOutGameAction;//タイムアップされたら呼ばれるアクション
    public event Action OnNextRoundAction;//次のラウンド遷移時のアクション
    public event Action<Transform, Action> OnKnockOutAction;

    private readonly List<PlayerState> watchedPlayers = new();


    //文字演出を表示する担当 演出が終わるのを待ってから試合を進める
    private UiTextMoveManager presenter;

    //実行中のラウンド開始処理
    private Coroutine roundStartCoroutine;

    //KOかタイムオーバーの処理中か（同時に起きても一回だけ処理する）
    private bool isFinishing = false;

    // 表示用ID (1:Player1, 2:Player2, 3:Draw)
    public const int Player1DisplayId = 1;
    public const int Player2DisplayId = 2;
    public const int DrawDisplayId = 3;

    public int TickOrder => BattleTickOrder.Of(TickPhase.Battle);

    //このスクリプトが一つしかないことを圃場
    private void Awake()
    {
        if(Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }//ここはラウンドごとにシーンを変えなかったらできる

    private void ResetBattleInfo()//ラウンドごとに初期化
    {
        Player1Wins = 0;
        Player2Wins = 0;
        CurrentRound = 1;
        IsGameEnd = false;
    }
    private void Init()
    {
        RemainingTime = TimeLimit;

        //バトルの情報をリセット
        if (IsGameEnd)
        ResetBattleInfo();

        //WinUi更新
        InitDisplayWinUiImage();
    }
    public void InitDisplayWinUiImage()
    {
        //現在の勝利数をUiに反映
        OnChangeWinUiAction?.Invoke(Player1Wins, Player2Wins);
    }

    private void OnEnable() => BattleTicker.Register(this);
    private void OnDisable() => BattleTicker.Unregister(this);
    private void OnDestroy() => UnwatchPlayers();

    private void WatchPlayers()
    {
        UnwatchPlayers();

        foreach (var input in PlayerInitializeManager.Instance.GetPlayersList())
        {
            if (input == null || !input.TryGetComponent<PlayerState>(out var state)) continue;

            state.OnPlayerDeadAction += OnPlayerDead;
            watchedPlayers.Add(state);
        }
    }
    private void UnwatchPlayers()
    {
        foreach (var state in watchedPlayers)
        {
            if (state != null) state.OnPlayerDeadAction -= OnPlayerDead;
        }
        watchedPlayers.Clear();
    }
    private void OnPlayerDead(PlayerState deadPlayer)
    {
        if (CurrentBattleState is not BattleState.GamePlaying) return;

        UnPairUser();
        ChangeBattleState(BattleState.GameEnd);

        if (OnKnockOutAction == null)
        {
            GameEnd();
            return;
        }
        OnKnockOutAction.Invoke(deadPlayer.transform, GameEnd);
    }
    void Start()
    {
        InitTransition();//バトルの状態をセット

        //最初のラウンドを開始
        StartRound(true);
    }

    /// <summary>
    /// 文字演出を表示する担当を登録 UiTextMoveManagerのStartから呼ばれる
    /// </summary>
    public void SetPresenter(UiTextMoveManager textPresenter) => presenter = textPresenter;
    public void RemovePresenter(UiTextMoveManager textPresenter)
    {
        if (presenter == textPresenter) presenter = null;
    }

    //---------------試合の流れ ここで順番を決めて、UIは表示だけをする---------------

    private void StartRound(bool isFirstRound)//ラウンド開始の流れを開始
    {
        //二重に呼ばれたら最初からやり直す
        if (roundStartCoroutine != null) StopCoroutine(roundStartCoroutine);

        roundStartCoroutine = StartCoroutine(RoundStartRoutine(isFirstRound));
    }
    private IEnumerator RoundStartRoutine(bool isFirstRound)
    {
        //演出を表示する担当が登録されるまで待つ
        yield return new WaitUntil(() => presenter != null);

        //最初のラウンドはプレイヤーの生成と読み込みが終わるまで待つ
        if (isFirstRound && PlayerInitializeManager.Instance != null)
            yield return new WaitUntil(() => PlayerInitializeManager.Instance.IsPlayersReady);

        if (isFirstRound && PlayerInitializeManager.Instance != null)
            WatchPlayers();

        //「Round Fight」の演出が終わるまで待つ
        yield return presenter.PlayRoundStart(isFirstRound);

        presenter.ShowHud(true);

        //最初のラウンドは対戦の準備をしてから開始
        if (isFirstRound)
            PlayerInitializeManager.Instance.StartSet();

        ToPlay();

        //2ラウンド目以降は止めていた入力を戻す
        if (!isFirstRound)
            PlayerInitializeManager.Instance.EnablePlayerInputList();

        roundStartCoroutine = null;
    }

    private void StartFinish(bool isTimeOver)//KOかタイムオーバーの流れを開始
    {
        //すでに処理中なら何もしない
        if (isFinishing) return;
        isFinishing = true;

        StartCoroutine(FinishRoutine(isTimeOver));
    }
    private IEnumerator FinishRoutine(bool isTimeOver)
    {
        yield return new WaitUntil(() => presenter != null);

        //KOかタイムオーバーの演出が終わるまで待つ
        yield return isTimeOver ? presenter.PlayTimeOver() : presenter.PlayKnockOut();

        //ラウンド終了へ
        ChangeTransition(BattleState.GameEnd);
    }

    private IEnumerator WinnerRoutine(int displayId)//勝者の演出を見せてからリザルトを表示
    {
        yield return new WaitUntil(() => presenter != null);

        yield return presenter.PlayWinner(displayId);

        //リザルト画面を表示
        if (EventManager.Instance != null)
            EventManager.Instance.BattleResult();
    }

    private void InitTransition()//遷移のペアを登録
    {
        stateMachine = new StateMachine<BattleStateManager>(this);

        stateMachine.AddTransition<MatchStart, MatchPlaying>((int)BattleState.GamePlaying);
        stateMachine.AddTransition<MatchEnd, MatchStart>((int)BattleState.GameStating);
        stateMachine.AddTransition<MatchPlaying, MatchEnd>((int)BattleState.GameEnd);
        stateMachine.Start<MatchStart>();
    }

    void Update()
    {
        stateMachine?.Update();
    }
    public void Tick()
    {
        if (CurrentBattleState is not BattleState.GamePlaying) return;
        
        RemainingTime -= BattleTicker.TickTime;
        RemainingTime = Mathf.Clamp(RemainingTime, 0, TimeLimit);

        // 時間切れで試合終了へ（KOの処理中ならそちらを優先する）
        if (RemainingTime <= 0 && !isFinishing)
        {
            OnTimeOutGameAction?.Invoke();

            //タイムオーバーの演出を見せてからラウンド終了へ
            StartFinish(true);
        }
    }
    //試合開始のステータスに変更
    public void ToPlay()
    {
        stateMachine.Dispatch((int)BattleState.GamePlaying);
    }
    public void ToEnd(int winnerId) //試合終了のステータスに変更 winnerIdはPlayerIdsの番号 引き分けはPlayerIds.None
    {

        (int p1, int p2) = winnerId switch
        {
            PlayerIds.Player1 => (1, 0),
            PlayerIds.Player2 => (0, 1),
            _ => (1, 1)//引き分け
        };

        Player1Wins += p1;
        Player2Wins += p2;

    }
    private void ChangeTransition(BattleState state)//バトルの処理状態を変える
    {
        stateMachine?.Dispatch((int)state);
    }
    private void ChangeBattleState(BattleState state)//バトルの状態を変える
    {
        CurrentBattleState = state;
    }
    /// <summary>
    /// ラウンド終了時少しヒットストップをさせる
    /// </summary>
    private void GameEnd()
    {
        OnGameEndAction?.Invoke();

        //KOの演出を見せてからラウンド終了へ
        StartFinish(false);
    }
    private void UnPairUser()
    {
        OnUnPairUserAction?.Invoke();
    }
    public void OnDestroys()
    {
        Instance = null;      // シングルトン解除
        stateMachine = null;  // ステートマシン参照解除

        Destroy(gameObject);
    }
    public void NextRound()
    {

        ////次のラウンド処理
        OnNextRoundAction?.Invoke();

        ChangeTransition((int)BattleState.GameStating);

        //「Round Fight」の演出を見せてから試合開始
        StartRound(false);
    }
    private class MatchStart : State
    {
        protected override void OnEnter(State prevstate)
        {
            owner.Init();
            owner.ChangeBattleState(BattleState.GameStating);
        }
        protected override void OnExit(State nextstate)
        {
            base.OnExit(nextstate);
        }
    }
    /// <summary>
    /// 試合中ステート
    /// 制限時間管理や勝者判定を行う
    /// </summary>
    private class MatchPlaying : State
    {

        protected override void OnEnter(State prevstate)
        {
            owner.isFinishing = false;
            // バトル状態を試合中へ変更
            owner.ChangeBattleState(BattleState.GamePlaying);

            // 制限時間初期化
            owner.RemainingTime = owner.TimeLimit;
        }

        /// <summary>
        /// 勝者判定
        /// HPが一番多いプレイヤーを勝者にする
        /// 同値なら引き分け
        /// </summary>
        private void CheckWinner()
        {
            // 現在参加しているプレイヤー一覧取得
            var playersInput = PlayerInitializeManager.Instance.GetPlayersList();

            // 引き分けはPlayerIds.None
            int winnerId = PlayerIds.None;

            // 最も高いHP 初期値は絶対に値が入るように-1に
            float maxHp = -1;

            foreach (var inp in playersInput)
            {
                // プレイヤー状態取得
                var state = inp.GetComponent<PlayerState>();

                // 現在の最大HPを超えた場合
                if (state.CurrentHp > maxHp)
                {
                    maxHp = state.CurrentHp;

                    // 勝者更新
                    winnerId = state.PlayerId;
                }
                // 同じHPなら引き分け
                else if (state.CurrentHp == maxHp)
                {
                    winnerId = PlayerIds.None;
                }
            }

            owner.ChangeBattleState(BattleState.GameEnd);
            owner.UnPairUser();
            owner.ToEnd(winnerId);//勝利数を設定
        }

        protected override void OnExit(State nextstate)
        {
            // 勝者判定
            CheckWinner();

            base.OnExit(nextstate);
        }
    }

    /// <summary>
    /// ラウンド終了ステート
    /// 続行かゲーム終了かを判定する
    /// </summary>
    private class MatchEnd : State
    {

        float timer = 0;

        float interval = 1f;


        protected override void OnEnter(State prevstate)
        {
            // 現在時刻保存
            timer = Time.time;

            // 試合終了判定
            CheckEndRound();
        }

        /// <summary>
        /// ラウンド継続かゲーム終了か判定
        /// </summary>
        private void CheckEndRound()
        {
            // どちらも規定勝利数に到達していない
            if (owner.Player1Wins < owner.TotalRound &&
                owner.Player2Wins < owner.TotalRound)
            {
                // 次ラウンドへ
                owner.CurrentRound++;
            }
            else
            {
                // ゲーム終了
                owner.IsGameEnd = true;

                // 勝利UI更新
                owner.OnChangeWinUiAction?.Invoke(
                    owner.Player1Wins,
                    owner.Player2Wins
                );

                int winnerId;
                if (owner.Player1Wins == owner.Player2Wins)
                    winnerId = DrawDisplayId;
                else
                    winnerId = owner.Player1Wins > owner.Player2Wins ? Player1DisplayId : Player2DisplayId;

                // 勝者の演出を見せてからリザルトを表示（一回だけ）
                owner.StartCoroutine(owner.WinnerRoutine(winnerId));
            }
        }

        protected override void OnUpdate()
        {
            // 一定時間後に次ステートへ
            if (Time.time >= timer + interval)
            {
                timer = 0;

                stateMachine.Dispatch((int)BattleState.GameStating);
            }
        }


        protected override void OnExit(State nextstate)
        {

            //どちらか勝っていたらNext処理はしない
            if (owner.IsGameEnd) return;

            // 次ラウンドへ
            Next();
        }

        /// <summary>
        /// 次ラウンド用シーン遷移
        /// </summary>
        private void Next()
        {
            // フェード開始
            float duration = 0.5f;
            float startAlpha = 0;
            float endAlpha = 1;

            EventManager.Instance.Startfade(duration, startAlpha, endAlpha);

            // 少し待ってから次のラウンドへ
            float waitTime = 1.5f;

            Delay.WaitTime(owner, waitTime, owner.NextRound);
        }
    }
}
