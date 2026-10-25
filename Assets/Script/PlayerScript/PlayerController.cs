using System;
using System.Collections.Generic;
using UnityEngine;
using static AnimationManager;
using static UiUtility;
using State = StateMachine<PlayerController>.State;

/// <summary>
/// プレイヤーがいま何の状態かのステート
/// </summary>
public enum PlayerAction
{
    Attack,
    Inoperable,
    Idle,
    Jump,
    Crouch,
    Die,
}
/// <summary>
/// プレイヤーの入力を受け取り条件を見て実行可能ならそれを通知するスクリプト
/// </summary>

[RequireComponent(typeof(PlayerEffectController))]
public partial class PlayerController : MonoBehaviour, ITickable, IPlayerCommandReceiver
{
    public enum Event : int//ステートの移行用ステート
    {
        MoveEvent, JumpEvent, CrouchEvent, AttackEvent,
        DieEvent, GuardEvent, StepEvent, IsHitEvent, WinEvent,InoperableEvent
    }
    //プレイヤー入力時のアクションの情報をセットする関数
    [Serializable]
    public class ActionsInfo
    {
        public Event ActionEvent;//イベントのタイプ
        public Action TransitionAction;//遷移後に処理する関数
        public Func<bool> IsBlocked;//特定のflagの状態をいれる
    }
    #region
    private AnimationManager controller;
    private PlayerState playerState;
    private PlayerMovement simpleMovement;
    private PlayerInputHandler playerInputHandler;
    private AttackFrameController colliderManager;
    private PlayerHitDirector hitDirector;
    private PlayerKnockBackController backMotion;
    private PlayerEffectController playerEffectController;
    #endregion
    //デバックようの管理クラス
    PlayerDebugTextManager textmanager;
    StateMachine<PlayerController> stateMachine;


    public bool IsHit = false;
    public bool IsGuarding = false;

    private bool IsAction = false;
    private bool IsCanNextAction = true;
    private bool IsCanRotation = true;
    private bool IsHitStop = false;


    private PlayerTransitionState targetCurrentState = PlayerTransitionState.Normal;
    //技がキャンセル可能かどうかを判断するためのやつ
    public AcceptInput CurrentCancelState = AcceptInput.Disable;

    public PlayerAction PlayerActionState = PlayerAction.Idle;

    //入力を受け取って、その行動が今できるかを判断する
    private PlayerCommandInput commandInput;

    //アニメーションの現在再生フレーム
    private int currentAnimationFrame;

    //ヒットストップ現在再生フレーム
    private int currentHitStopFrame;
    private int currentHitTotalFrame;

    //ジャンプ専用の再生フレームとトータルフレーム
    private int currentJumpFrame;
    private int currentJumpTotalFrame;


    //------event-------//
    public event Action OnRemovePos;//生成場所に戻すアクション
    public event Action OnStopAction;//アニメーションが終わったことを通知するアクション
    public event Action<bool> OnSetCanMoveFlagAction;//動けることを通知するアクション
    public event Action<int> OnJumpTransitionAction;//イベントのJumpの処理開始通知アクション
    public event Action<AnimationId, bool> OnActiveCollisionAction;//当たり判定のアクション

    void TryGetComponents()
    {
        simpleMovement = GetComponent<PlayerMovement>();
        controller = GetComponent<AnimationManager>();
        playerState = GetComponent<PlayerState>();
        playerInputHandler = GetComponent<PlayerInputHandler>();
        colliderManager = GetComponent<AttackFrameController>();
        hitDirector = GetComponent<PlayerHitDirector>();
        backMotion = GetComponent<PlayerKnockBackController>();
        playerEffectController = GetComponent<PlayerEffectController>();
    }

    /// <summary>
    /// 遷移パターンを登録
    /// AddTransition<AClass --> BClass>に遷移
    /// </summary>
    private void InitTransition()
    {
        stateMachine = new StateMachine<PlayerController>(this);
        stateMachine.AddTransition<MoveAndIdle, Jump>((int)Event.JumpEvent);
        stateMachine.AddTransition<MoveAndIdle, Crouch>((int)Event.CrouchEvent);
        stateMachine.AddTransition<MoveAndIdle, Guard>((int)Event.GuardEvent);
        stateMachine.AddTransition<MoveAndIdle, Attack>((int)Event.AttackEvent);
        stateMachine.AddTransition<MoveAndIdle, Step>((int)Event.StepEvent);

        stateMachine.AddTransition<Attack, Crouch>((int)Event.CrouchEvent);
        stateMachine.AddTransition<Attack, MoveAndIdle>((int)Event.MoveEvent);
        stateMachine.AddTransition<Attack, Jump>((int)Event.JumpEvent);
        stateMachine.AddTransition<Attack,Attack>((int)Event.AttackEvent);

        stateMachine.AddTransition<Crouch, Attack>((int)Event.AttackEvent);
        stateMachine.AddTransition<Crouch, Guard>((int)Event.GuardEvent);
        stateMachine.AddTransition<Crouch, Crouch>((int)Event.CrouchEvent);

        stateMachine.AddTransition<Guard, Attack>((int)Event.AttackEvent);
        stateMachine.AddTransition<Guard, Crouch>((int)Event.CrouchEvent);
        stateMachine.AddTransition<Guard, Jump>((int)Event.JumpEvent);

        stateMachine.AddTransition<Jump, Jump>((int)Event.JumpEvent);
        stateMachine.AddTransition<Jump, Attack>((int)Event.AttackEvent);

        stateMachine.AddTransition<Hitting, Crouch>((int)Event.CrouchEvent);
        stateMachine.AddTransition<Hitting, MoveAndIdle>((int)Event.MoveEvent);
        stateMachine.AddTransition<Hitting, Guard>((int)Event.GuardEvent);

        stateMachine.AddTransition<Die, MoveAndIdle>((int)Event.MoveEvent);
        stateMachine.AddTransition<Win, MoveAndIdle>((int)Event.MoveEvent);

        stateMachine.AddAnyTransition<MoveAndIdle>((int)Event.MoveEvent);
        stateMachine.AddAnyTransition<Die>((int)Event.DieEvent);
        stateMachine.AddAnyTransition<Hitting>((int)Event.IsHitEvent);
        stateMachine.AddAnyTransition<Inoperable>((int)Event.InoperableEvent);
        stateMachine.AddAnyTransition<Win>((int)Event.WinEvent);
        stateMachine.Start<MoveAndIdle>();
    }

    /// <summary>
    /// リストの初期化
    /// </summary>
    private void InitList()
    {
        //ステートによって処理するアクションを変えて登録している
        //プレイヤーの入力によって実行するアクションを設定
        var actionList = new Dictionary<PlayerInputHandler.ActionState, ActionsInfo>()
        {
            {PlayerInputHandler.ActionState.WeakAttack,new ActionsInfo()
            {ActionEvent = Event.AttackEvent,TransitionAction =controller.OnWeakAttack} },

            {PlayerInputHandler.ActionState.MiddleAttack,new ActionsInfo() 
            { ActionEvent = Event.AttackEvent, TransitionAction = controller.OnMiddleAttack }},

            {PlayerInputHandler.ActionState.StrongAttack,new ActionsInfo()
            { ActionEvent = Event.AttackEvent, TransitionAction = controller.OnStrongAttack } }, 

            {PlayerInputHandler.ActionState.SpecialAttack,new ActionsInfo()
            { ActionEvent = Event.AttackEvent, TransitionAction = controller.OnSpecialAttack } },

            {PlayerInputHandler.ActionState.TiltAttack,new ActionsInfo()
            { ActionEvent = Event.AttackEvent, TransitionAction = controller.OnTiltAttack }},

            {PlayerInputHandler.ActionState.SmashAttack,new ActionsInfo()
            { ActionEvent = Event.AttackEvent, TransitionAction = controller.OnSmashAttack 
            ,IsBlocked = ()=>simpleMovement.IsJumping}},

            {PlayerInputHandler.ActionState.Grabbing,new ActionsInfo() 
            { ActionEvent = Event.AttackEvent, TransitionAction = controller.OnThrowAttack } },

            {PlayerInputHandler.ActionState.Jumping,new ActionsInfo() 
            { ActionEvent = Event.JumpEvent,TransitionAction = null,
                IsBlocked = ()=>playerState.currentState.HasFlag(StatusFlags.IsThrowing)
                ||simpleMovement.IsJumping||PlayerActionState == PlayerAction.Attack}},

            {PlayerInputHandler.ActionState.Crouching,new ActionsInfo()
            { ActionEvent = Event.CrouchEvent, TransitionAction = null ,
                IsBlocked = ()=>simpleMovement.IsJumping||PlayerActionState == PlayerAction.Attack}},

        };

        var neutralList = new Dictionary<PlayerInputHandler.ActionState, PlayerInputHandler.ActionState>()
        {
            {PlayerInputHandler.ActionState.Crouching,PlayerInputHandler.ActionState.Jumping },
             {PlayerInputHandler.ActionState.Jumping,PlayerInputHandler.ActionState.Crouching},
        };

        commandInput = new PlayerCommandInput(this, actionList, neutralList);
    }

    #region Debug
    //デバッグ用のスクリプトを取得
    public void SetDebugTextManager(PlayerDebugTextManager manager)
    {
        textmanager = manager;
        SetUpDebugInfo();
    }

    //debagのためにテキストマネージャーに渡す処理
    private void SetUpDebugInfo()
    {
        textmanager.Register("IsCanNext", () => IsCanNextAction.ToString());
        textmanager.Register("IsAction", () => IsAction.ToString());
        textmanager.Register("CurrentCancel", () => CurrentCancelState.ToString());
        textmanager.Register("Jyoutai",()=> playerState.currentState.ToString());
        textmanager.Register("PlayerCurrentAction",()=>PlayerActionState.ToString());
        textmanager.Register("StateMachine.CurrentState", () => BattleStateManager.Instance.CurrentBattleState.ToString());
        textmanager.Register("PlayerState", () => playerState.currentTransitionState.ToString());
        textmanager.Register("frameRate", () => (1f / Time.unscaledDeltaTime).ToString());
        textmanager.SetEndText();
    }
    #endregion
    private void Start()
    {
        //コンポーネントを取得
        TryGetComponents();

        //現在のHpをUiに反映させる
        playerState.OffHpBarChange();

        //イベントの登録
        SubscribeEvents();

        //リストの初期化
        InitList();

        //ステートマシンは他のコンポーネントのイベント登録が終わってから
        //PlayerInitializeManagerがInitializeで初期化する
    }

    /// <summary>
    /// ステートマシンを初期化する 生成したキャラクターの準備ができてからPlayerInitializeManagerが呼ぶ
    /// </summary>
    public void Initialize()
    {
        //二重に初期化しない
        if (stateMachine != null) return;

        InitTransition();
    }

    /// <summary>
    /// 次のティックで実行する 実行する時にステートが変わっていたら実行しない
    /// </summary>
    private void RunNextTick(State state, Action action)
    {
        TickScheduler.RunNextTick(() =>
        {
            if (this == null || stateMachine == null || stateMachine.CurrentState != state) return;

            action?.Invoke();
        });
    }
    private void OnEnable()
    {
        //固定ティックで行動を更新する
        BattleTicker.Register(this);
    }
    private void OnDisable()
    {
        BattleTicker.Unregister(this);
        UnSubscribeEvents();
    }
    private void SubscribeEvents()//イベント登録
    {
        //入力を受け取って処理するイベントを登録
        if (playerInputHandler != null)
        playerInputHandler.OnAction += OnCommandInput;

        //地面についた通知をするイベントを登録
        if (backMotion != null)
            backMotion.OnLanded += HasInoperable;


        if (BattleStateManager.Instance == null) return;

        //プレイヤーが死んだときのイベントを登録
        BattleStateManager.Instance.OnGameEndAction += OnPlayGameEndAnimation;

        //プレイヤーの状態を初期化のイベントを登録
        BattleStateManager.Instance.OnNextRoundAction += InitPlayerState;
    }
    private void UnSubscribeEvents()//イベント解除
    {
        //入力を受け取って処理するイベントを解除
        if (playerInputHandler != null)
        playerInputHandler.OnAction -= OnCommandInput;

        //地面についた通知をするイベントを解除
        if (backMotion != null)
            backMotion.OnLanded -= HasInoperable;

        if (BattleStateManager.Instance == null) return;

        //プレイヤーが死んだときのイベントを解除
        BattleStateManager.Instance.OnGameEndAction -= OnPlayGameEndAnimation;

        //プレイヤーの状態を初期化のイベントを解除
        BattleStateManager.Instance.OnNextRoundAction -= InitPlayerState;
    }

    //ラウンドが終わったら流れる 
    private void OnPlayGameEndAnimation()
    {
        //自分が死んでいたらDie 死んでなかったらWinに遷移
        var currentEvent = playerState.IsDead ? Event.DieEvent : Event.WinEvent;

        stateMachine.Dispatch((int)currentEvent);
    }
    private void CheckSpecialPlayerState()
    {
        //パワーアップ状態ならノーマル状態に戻す
        if (playerState.currentState.HasFlag(StatusFlags.IsPowerUp))
        {
            //タイマーをリセットさせるために０を送る
            playerState.OnStartPowerUp(true);
        }
    }

    //ネクストラウンドに移るときに呼ばれる
    private void InitPlayerState()
    {
        //位置を生成元に戻す
        OnRemovePos?.Invoke();

        //体力やゲージ残量を初期化
        playerState.Init();

        //アニメーションを最初のアニメーションにする
        controller.NextRoundAnimation();

        //状態をIdleに
        stateMachine.Dispatch((int)Event.MoveEvent);
    }
    //Player1→Player2の順番で処理する
    public int TickOrder => BattleTickOrder.Of(TickPhase.Player, playerState != null ? playerState.PlayerId : PlayerIds.None);

    //BattleTickerから1ティックごとに呼ばれる 描画のフレームレートに関係なく1秒間に60回進む
    public void Tick()
    {
        if (BattleStateManager.Instance == null) return;


        //ゲームの進行が不可能にならないようにするため
        if(transform.position.y < -10)
        {
            //めり込んで下に落ちたときに元の場所に戻るよう
            OnRemovePos?.Invoke();
        }

        //時間が止まっている時は更新をしない
        if (PauseService.IsPaused) return;

        //行動の更新をしてから移動を更新する
        stateMachine?.Update();
        stateMachine?.FixedUpdate();
    }
    //プレイヤーの向きを取る処理 バックならtrueがかえる
    private bool PlayerCheckDirection()//自分の向きを取得 フラグで取得
    {
        int right = 1;
        int left = -1;
        float backBorder = -0.5f;

        Vector2 input = playerInputHandler.MoveInput;

        int facingDir = transform.forward.x > 0 ? right : left;

        return input.x * facingDir < backBorder;
    }
    private int GetDirection()//数値で取得
    {
        int right = 1;
        int left = -1;
        float backBorder = 0.5f;

        float moveInputX = playerInputHandler.MoveInput.x;

        // ニュートラル
        if (Mathf.Abs(moveInputX) < backBorder)
            return 0;

        // キャラの向き
        int facingDir = transform.forward.x > 0 ? right :left;

        // 入力方向
        int inputDir = moveInputX > 0 ? right : left;

        // 向き考慮
        return inputDir * facingDir;
    }
    private PlayerTransitionState CheckGuard()//ガードしているか確認
    {
        var dir = GetDirection();

        bool isBack = dir == -1;
        bool isCrouching = playerInputHandler.IsCrouching;
        PlayerTransitionState nextState;

        if (!isBack)
        {
            return isCrouching ? PlayerTransitionState.Crouch :
                                 PlayerTransitionState.Normal;
        }

        nextState = isCrouching
    ? PlayerTransitionState.CrouchGuard
    : PlayerTransitionState.StandGuard;

        return nextState;
    }

    /// <summary>
    /// 現在の行動ステートに変更
    /// </summary>
    public void ChangePlayerActionState(PlayerAction state)
    {
        PlayerActionState = state;
    }
    /// <summary>
    /// 技のキャンセルステートを変更
    /// </summary>
    public void ChangeCancelActionState(AcceptInput state)
    {
        CurrentCancelState = state;
    }
    /// <summary>
    /// 行動可能フラグを変更
    /// </summary>
    public void ChangeCanNextActionFlag(bool isNextAction = false)
    {
        IsCanNextAction = isNextAction;
    }
    /// <summary>
    /// アクション実行フラグを変更
    /// </summary>
    private void ChangeIsActionFlag(bool StartAction = false)
    {
        IsAction = StartAction;
    }
    /// <summary>
    /// プレイヤーの回転を止めるフラグを変更
    /// </summary>
    public void SetCanRotationFlag(bool isRotation = false)
    {
        IsCanRotation = isRotation;
    }
    /// <summary>
    /// ヒットorガードのフラグを変更
    /// </summary>
    public void SetHitOrGuardFlag(PlayerHitDirector.HitResult hitState,bool isTrue = true)
    {
        switch (hitState)
        {
            case PlayerHitDirector.HitResult.Hit:
                IsHit = isTrue; break;
            case PlayerHitDirector.HitResult.Guard:
                IsGuarding = isTrue; break;
            default: break;
        }
    }
    /// <summary>
    /// 引数で与えられたステートが遷移可能かチェックしに行く
    /// </summary>
    public void ChangePlayerState(PlayerInputHandler.InputState inputState, PlayerInputHandler.ActionState actionState)
    {
        commandInput.Receive(inputState, actionState);
    }

    //入力の通知を受け取って判断を任せる
    private void OnCommandInput(PlayerInputHandler.InputState inputState, PlayerInputHandler.ActionState actionState)
    {
        commandInput.Receive(inputState, actionState);
    }

    bool IPlayerCommandReceiver.CanAcceptInput =>
        !PauseService.IsPaused &&
        BattleStateManager.Instance.CurrentBattleState is BattleStateManager.BattleState.GamePlaying;

    bool IPlayerCommandReceiver.IsBusy =>
        (!IsCanNextAction && IsAction) || PlayerActionState == PlayerAction.Inoperable;

    AcceptInput IPlayerCommandReceiver.CancelState => CurrentCancelState;

    bool IPlayerCommandReceiver.IsTargetHitOrGuard => targetCurrentState is PlayerTransitionState.Hitting or
            ( PlayerTransitionState.CrouchGuard or PlayerTransitionState.StandGuard);

    bool IPlayerCommandReceiver.IsBeingThrown => playerState.currentState.HasFlag(StatusFlags.IsThrowing);

    void IPlayerCommandReceiver.BeginAction(ActionsInfo action)
    {
        ChangeIsActionFlag(true);
        ChangeCanNextActionFlag();

        action.TransitionAction?.Invoke();

        stateMachine.Dispatch((int)action.ActionEvent);
    }

    void IPlayerCommandReceiver.BeginNeutralAction(ActionsInfo action)
    {
        ChangeIsActionFlag(true);
        stateMachine.Dispatch((int)action.ActionEvent);
        action.TransitionAction?.Invoke();
    }

    void IPlayerCommandReceiver.EndAction()
    {
        CanMoveState();

        stateMachine.Dispatch((int)Event.MoveEvent);
    }
    /// <summary>
    /// ヒットステートに強制遷移
    /// </summary>
    public void ProcessHit()
    {
        //死亡アニメーション中に残っていた攻撃や弾が当たっても、やられに戻らないようにする
        if (stateMachine.CurrentState is Die) return;

        stateMachine.Dispatch((int)Event.IsHitEvent);
    }
    /// <summary>
    /// 行動不能ステートに強制遷移
    /// </summary>
    public void HasInoperable()
    {
        if (playerState.IsDead) return;
        stateMachine.Dispatch((int)Event.InoperableEvent);
    }

    //演出用の関数
    public void PlayerStartPresentation(Action<Transform,Action> presentationEvent, Transform targetPos,Action receiver)
    {
        PlayPresentation();

        //演出を再生するためのイベント
        presentationEvent?.Invoke(targetPos,receiver);
    }
    private void PlayPresentation()
    {
        //ここが依存しまくっているから変える
        var effectType = playerEffectController.CanPlayPresentationEffect(controller.GetPresentationState());

        if (effectType is not EffectType.None)
        {
            Vector3 root = controller.GetAnchor(AnimationManager.EffectAnchor.Root).position + Vector3.up;
            int returnTime = 60;

            playerEffectController.PlayEffect(effectType, root, Quaternion.identity, returnTime);
        }
    }
    /// <summary>
    /// 行動可能にするためのフラグやステートを変更
    /// </summary>
    public void CanMoveState()
    {
        ChangeCanNextActionFlag(true);
        ChangeIsActionFlag();
        ChangePlayerActionState(PlayerAction.Idle);
        ChangeCancelActionState(AcceptInput.Normal);
    }
    public bool GetCanRotation() { return IsCanRotation; }


    public void SetHitStopFlag(bool isStop = false) => IsHitStop = isStop;
    public bool GetHitStopFlag() => IsHitStop;

    /// <summary>
    /// ヒットストップを解除して、止めていたアニメーションを動かす
    /// KOの一撃のヒットストップ中にズームが始まると止まったままになるので、勝敗の演出の前に呼ぶ
    /// </summary>
    private void ReleaseHitStop()
    {
        SetHitStopFlag();
        controller.StartClipAnimation();
    }
    public void SetTargetTransitionState(PlayerTransitionState state) => targetCurrentState = state;
}

