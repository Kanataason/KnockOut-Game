using System;
using System.Collections.Generic;
using UnityEditor;
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
public class PlayerController : MonoBehaviour, ITickable
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

    private PlayerInputHandler.ActionState currentActionState = PlayerInputHandler.ActionState.None;
    public PlayerAction PlayerActionState = PlayerAction.Idle;

    private Dictionary<PlayerInputHandler.ActionState, ActionsInfo> ActionList;
    private Dictionary<PlayerInputHandler.ActionState, PlayerInputHandler.ActionState> neutralList;

    private Queue<PlayerInputHandler.ActionState> prevCommandList = new();//
    private List<PlayerInputHandler.ActionState> commandList = new();//プレイヤーの入力を保管するリスト

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
        ActionList = new()
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

            //{PlayerInputHandler.ActionState.CancelAction,new ActionsInfo()
            //{
            //    ActionEvent = Event.AttackEvent,TransitionAction = controller.OnCancelAction,
            //} },
        };

        neutralList = new()
        {
            {PlayerInputHandler.ActionState.Crouching,PlayerInputHandler.ActionState.Jumping },
             {PlayerInputHandler.ActionState.Jumping,PlayerInputHandler.ActionState.Crouching},
        };
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
        playerInputHandler.OnAction += CheckAction;

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
        playerInputHandler.OnAction -= CheckAction;

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
    /// <summary>
    /// プレイヤーが入力した動作に紐づいているアクション実行やステートに変更する関数
    /// </summary>
    /// <param name="inputState"></param>
    /// <param name="actionState"></param>
    private void CheckAction(PlayerInputHandler.InputState inputState, 
        PlayerInputHandler.ActionState actionState = PlayerInputHandler.ActionState.None)
    {
        //ポーズ中なら何もしない
        if (PauseService.IsPaused) return;

        //ゲーム中でしか動けないようにする
        if (BattleStateManager.Instance.CurrentBattleState is not BattleStateManager.BattleState.GamePlaying)
            return;

        //ボタンを離したか確認
        bool IsCancel = inputState == PlayerInputHandler.InputState.Canceled;

        //ボタンを押した瞬間か確認
        bool IsPerform = inputState == PlayerInputHandler.InputState.Performed;


        if (IsCancel)
        {
            if(commandList.Contains(actionState))
            {
                RemoveCommandList(actionState);
            }
        }
        if (IsPerform)
        {
            if (!commandList.Contains(actionState))
            {
                commandList.Add(actionState);
            }

        }


        //アクションが登録されているかを確認
        if (!ActionList.TryGetValue(actionState, out var currentAction))
        {
            return;
        }

        if (CanPlayCommand(inputState, currentAction, actionState)) return;

        if (IsCancel)
        {
            
            RemoveCommandList(actionState);

            if (CanNeutralPlay()) return;
            
            currentActionState = PlayerInputHandler.ActionState.None;

            CanMoveState();

            stateMachine.Dispatch((int)Event.MoveEvent);
            return;
        }
        if (IsPerform)
        {
            //キャンセル可能の時ならアクション中でも行動可能

            //ニュートラルの行動かを見る
            if (CheckNeutral(currentActionState, actionState)) return;

            ChangeIsActionFlag(true);
            ChangeCanNextActionFlag();
            //現在のアクションステータスを設定
            currentActionState = actionState;

            currentAction.TransitionAction?.Invoke();

            stateMachine.Dispatch((int)currentAction.ActionEvent);

        }

    }
    private bool CanPlayCommand(PlayerInputHandler.InputState inputState,
      ActionsInfo currentAction, PlayerInputHandler.ActionState actionState = PlayerInputHandler.ActionState.None)
    {
        if (CheckCancelAttack(inputState,actionState)) return false;
        //アタックキャンセル可能期間中なら行動可能にする   

        //行動禁止の条件があったらみる
        if (currentAction.IsBlocked != null && currentAction.IsBlocked())
        {
            return true;
        }
        //捕まれていたら何もできないようにする
        if (playerState.currentState.HasFlag(StatusFlags.IsThrowing)
            && actionState is not PlayerInputHandler.ActionState.Grabbing)
        {
            return true;
        }

        //アクション中で行動可能かどうかみる可能じゃなければ返す
        if ((!IsCanNextAction && IsAction) || PlayerActionState == PlayerAction.Inoperable)
        {
            return true;
        }

        //キャンセルできる状態じゃなかったら返す
        if (CurrentCancelState == AcceptInput.Disable)
        {
            return true;
        }return false;
    }
    private bool CheckCancelAttack(PlayerInputHandler.InputState inputState,PlayerInputHandler.ActionState actionState)
    {
        //アタックキャンセル可能期間中なら行動可能にする

        bool canCancelAttack = CurrentCancelState is AcceptInput.AttackCancellable or AcceptInput.SmashCancellable;

        bool inputCancel = inputState is PlayerInputHandler.InputState.Canceled;

        bool isTargetHit = targetCurrentState is PlayerTransitionState.Hitting or
            ( PlayerTransitionState.CrouchGuard or PlayerTransitionState.StandGuard);

        bool isSameAction = currentActionState == actionState;

        // キャンセル可能期間中で、ボタンを押しており、相手にヒット中、かつ同じ技ではない
        return canCancelAttack && !inputCancel && isTargetHit &&!isSameAction;
    }





    private bool CanNeutralPlay()//これはCPT方式のための関数
    {
        if (prevCommandList.Count > 0)//ニュートラル時の物があったら再生
        {
            var eventId = prevCommandList.Dequeue();
            bool canNeutral = false;

            foreach (var state in commandList)
            {
                if (state == eventId)
                    canNeutral = true;
            }
            if (!canNeutral) return false;

            if (!ActionList.TryGetValue(eventId, out var actions)) return false;

            ChangeIsActionFlag(true);
            currentActionState = eventId;
            stateMachine.Dispatch((int)actions.ActionEvent);
            actions.TransitionAction?.Invoke();
            return true;
        }
        return false;
    }

    //リストに入っているステート同士ならニュートラルにする
    private bool CheckNeutral(PlayerInputHandler.ActionState currentState, PlayerInputHandler.ActionState prevState)
    {
        if(neutralList.TryGetValue(currentState,out var resultState))
        {
            if(resultState == prevState)
            {
                if(prevCommandList.Count == 0)
                prevCommandList.Enqueue(currentState);//今のステートを入れる

                CheckAction(PlayerInputHandler.InputState.Canceled, currentState);
                currentActionState = PlayerInputHandler.ActionState.None;
                return true;
            }
        }
        return false;
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
    /// <param name="state"></param>
    public void ChangePlayerActionState(PlayerAction state)
    {
        PlayerActionState = state;
    }
    /// <summary>
    /// 技のキャンセルステートを変更
    /// </summary>
    /// <param name="state"></param>
    public void ChangeCancelActionState(AcceptInput state)
    {
        CurrentCancelState = state;
    }
    /// <summary>
    /// 行動可能フラグを変更
    /// </summary>
    /// <param name="isNextAction"></param>
    public void ChangeCanNextActionFlag(bool isNextAction = false)
    {
        IsCanNextAction = isNextAction;
    }
    /// <summary>
    /// アクション実行フラグを変更
    /// </summary>
    /// <param name="StartAction"></param>
    private void ChangeIsActionFlag(bool StartAction = false)
    {
        IsAction = StartAction;
    }
    /// <summary>
    /// 入力履歴を消す
    /// </summary>
    /// <param name="state"></param>
    private void RemoveCommandList(PlayerInputHandler.ActionState state)
    {
      commandList.Remove(state);
    }

    /// <summary>
    /// プレイヤーの回転を止めるフラグを変更
    /// </summary>
    /// <param name="isRotation"></param>
    public void SetCanRotationFlag(bool isRotation = false)
    {
        IsCanRotation = isRotation;
    }
    /// <summary>
    /// ヒットorガードのフラグを変更
    /// </summary>
    /// <param name="hitState"></param>
    /// <param name="isTrue"></param>
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
    /// <param name="actionState"></param>
    public void ChangePlayerState(PlayerInputHandler.InputState inputState, PlayerInputHandler.ActionState actionState)
    {
        CheckAction(inputState, actionState);
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
    //行動をステートにしてステートにして管理することで視認性があがりデバックもやりやすい
    private class MoveAndIdle : State
    {
        private float _prevDir;//前フレームのプレイヤーが向いている方向

        protected override void OnEnter(State prevstate)
        {
            owner.SetCanRotationFlag(true);
           _prevDir = 0;

            InitState();
          
            InvokeEventAction();
        }
        private void InitState()
        {
            if (owner.IsCanNextAction)
                owner.ChangePlayerActionState(PlayerAction.Idle);

            owner.currentActionState = PlayerInputHandler.ActionState.None;
        }
        private void InvokeEventAction()//イベントを発火
        {
            owner.playerState.ChangeState(PlayerTransitionState.Normal);//これを登録する
            owner.OnActiveCollisionAction?.Invoke(AnimationId.Idle, true);
            owner.OnSetCanMoveFlagAction?.Invoke(false);
        }
        protected override void OnUpdate()
        {
            var Input = owner.playerInputHandler.MoveInput;
            if (owner.PlayerActionState == PlayerAction.Inoperable|| BattleStateManager.Instance == null) return;

            //ゲームが始まっていたらアニメーション開始
           if(BattleStateManager.Instance.CurrentBattleState is not BattleStateManager.BattleState.GameStating)
                owner.controller.PlayMoveAnimation(Input.x);

           //コマンドが押されていたか確認
            if (CheckBufferCommand()) return;

            //ステップできるか確認
            if (TryStep(Input)) return;

            //ガードしているか確認
            if (TryGuard()) return;

        }
        private bool CheckBufferCommand()
        {
            if (owner.IsAction) return true;
            foreach (var state in owner.commandList)
            {
                owner.CheckAction(PlayerInputHandler.InputState.Performed, state);
                return true;
            }
            return false;
        }
        //ステップするかの確認
        private bool TryStep(Vector2 moveInput)
        {
            //ステップできない状態か、しゃがんでいたらステップしない
            if (owner.simpleMovement.IsStepping()||owner.playerInputHandler.IsCrouching) return false;

            bool IsAction = false;
            int dir = Mathf.RoundToInt(moveInput.x);
            if (_prevDir == 0 && dir != 0)
            {
                //行動不可能じゃなかったらステップ
                if (owner.PlayerActionState is not PlayerAction.Inoperable)
                {
                    Vector3 playerPos = owner.transform.position;
                    bool IsBack = owner.PlayerCheckDirection();
                    int waitTime = 9;

                    if (owner.simpleMovement.CheckCanStep(dir,playerPos,IsBack,waitTime))
                    {
                        owner.stateMachine.Dispatch((int)Event.StepEvent);
                        IsAction = true;
                    }
                }
            }
            _prevDir = dir;
            return IsAction;
        }
        //ガードするかの確認
        private bool TryGuard()
        {
            //立ガードがしゃがみガードを見ている
            var currentGuard = owner.CheckGuard();
            bool isGuard = currentGuard == PlayerTransitionState.CrouchGuard ||
                           currentGuard == PlayerTransitionState.StandGuard;


            if (isGuard&&owner.PlayerActionState != PlayerAction.Attack)
            {
                stateMachine.Dispatch((int)Event.GuardEvent);
                return true;
            }
            return false;
        }
        protected override void OnFixedUpdate()
        {
            if (owner.PlayerActionState == PlayerAction.Inoperable) return;

            if (!owner.IsAction)
            owner.simpleMovement.UpdateMoving(owner.playerInputHandler.MoveInput);
        }
        protected override void OnExit(State nextstate)
        {
            RemoveDirection();
            //動きをのフラグを止める
            owner.OnSetCanMoveFlagAction?.Invoke(true);
        }
        private void RemoveDirection()
        {
            //後ろ方向の入力以外なら消す
            if (stateMachine.NextState is not (int)Event.GuardEvent)
                owner.simpleMovement.InitDirection();
        }
    }
    private class Jump : State
    {
        protected override void OnEnter(State prevstate)
        {
            EnterJump();
            owner.colliderManager.ClearThrowBox();

            //ノックバック中ならノックバックを取りけす
            if (owner.backMotion.CheckIsKnockBack())
                owner.backMotion.RemoveKnockBackCoroutine();
        }

        private void EnterJump()//ジャンプの処理を通知
        {
            SetUpJump();

            owner.SetCanRotationFlag();
            owner.ChangePlayerActionState(PlayerAction.Jump);
            owner.ChangeCanNextActionFlag(true);//キャンセル可能な状態にする

            InitFrame();

            InvokeEventAction();
        }

        private void InitFrame()//アニメーションやジャンプに必要なフレームの初期化
        {
            var currentData = owner.controller.GetCurrentAttackData();
            owner.currentJumpTotalFrame = currentData.frameData[^1].FrameNumber;
            owner.currentJumpFrame = 0;
            owner.currentAnimationFrame = 1;
        }
        private void SetUpJump()//ジャンプの処理を準備
        {
            //方向を0,1.-1で取得してジャンプ方向を変える
            var Movex = owner.playerInputHandler.MoveInput.x;
            var dir = Mathf.RoundToInt(Movex);

            owner.OnJumpTransitionAction?.Invoke(dir);
        }
        private void InvokeEventAction()//イベントを発火
        {
            owner.playerState.ChangeState(PlayerTransitionState.Jump);//ステートを変更
            owner.OnActiveCollisionAction?.Invoke(AnimationId.Jumping, false);//コライダーをアップデート
        }
        protected override void OnUpdate()
        {
            if (owner.PlayerActionState is not PlayerAction.Jump)
                return;

            //ヒットストップがまだ続いていれば止める再生
            if (owner.GetHitStopFlag())
            {
                UpdateHitStopFrame();
                return;
            }
            //アニメーション更新
            owner.controller.PlayAnimation(owner.currentAnimationFrame);

            //ジャンプの動作更新
            owner.simpleMovement.UpdateJumps(owner.currentJumpFrame,owner.currentJumpTotalFrame);

            //フレームを更新
            owner.currentAnimationFrame++;
            owner.currentJumpFrame++;

            //ジャンプが終わったらステートを遷移
            if (!owner.simpleMovement.IsJumping)
                CheckCommand();

            
        }
        private void UpdateHitStopFrame()//ヒットストップの処理
        {
            if (owner.currentHitStopFrame >= owner.currentHitTotalFrame)
            {
                if (owner.GetHitStopFlag()) owner.controller.StartClipAnimation();
                owner.SetHitStopFlag();
            }
            owner.currentHitStopFrame++;
        }
        private void CheckCommand()
        {
            if (CheckCancelAction()) return;

            owner.CheckAction(PlayerInputHandler.InputState.Canceled,PlayerInputHandler.ActionState.Jumping);
        }
        private bool CheckCancelAction()//キャンセルが押されていたかどうかを判断
        {
            if(owner.commandList.Count > 0)
            {
                foreach (var cancel in owner.commandList)
                {
                    if (cancel != PlayerInputHandler.ActionState.Jumping) continue;

                    owner.CheckAction(PlayerInputHandler.InputState.Performed,
                       cancel);
                    return true;

                }
            }
            return false;
        }
        protected override void OnExit(State nextstate)
        {
            //遷移先がアタックイベントならアニメーションを初期化しない
            if (stateMachine.NextState is (int)Event.AttackEvent) return;

            owner.simpleMovement.ResetJumpFlag(false);

            if (stateMachine.NextState is (int)Event.IsHitEvent) return;

            owner.controller.EndAnime();
        }
    }
    private class Crouch : State//ガード時はステートだけ変わり、クラスは変わらない
    {
        float firstDir = 0f;
        protected override void OnEnter(State prevstate)
        {
            //ステップの方向を初期化
            owner.simpleMovement.InitDirection();

            //すべての行動制限フラグを行動可能にする
            owner.CanMoveState();

            //遷移直後の入力方向を取得
            firstDir = owner.GetDirection();

            //しゃがみアニメーションを再生
            owner.controller.PlayNormalAnimation<bool>(AnimationManager.AnimationId.Crouching, true);

            UpdateCollider();

            var state = owner.CheckGuard();
            InvokeUpdateStateAction(state);
        }
        private void InvokeUpdateStateAction(PlayerTransitionState state)//イベントを発火
        {
            owner.ChangePlayerActionState(PlayerAction.Crouch);
            owner.playerState.ChangeState(state);
        }
        private void UpdateCollider()//コライダーをアップデート
        {
            owner.OnActiveCollisionAction?.Invoke(AnimationId.Crouching, true);
        }
        protected override void OnUpdate()
        {
            //入力方向と見ている方向が違ったらステータスを変える
            CheckCrouching();

            //遷移直後の入力方向と現在の入力方向を見てガードしているかを確認
            CheckDirection();

        }
        private void CheckCrouching()
        {
            //しゃがんでなかったらidleに戻す
            if(!owner.commandList.Contains(PlayerInputHandler.ActionState.Crouching))
            {
                stateMachine.Dispatch((int)Event.MoveEvent);
            }
        }

        private void CheckDirection()//入力方向を見てガードしているかをチェック
        {
            //現在方向を入力方向を取得
            var dir = owner.GetDirection();
            
            //遷移直後と現在の入力方向が違ったら戻す
            if (dir == firstDir)
                return;

            bool isBack = dir == -1;
            bool isCrouching = owner.playerInputHandler.IsCrouching;

            PlayerTransitionState nextState;

            if (isBack)
            {
                nextState = isCrouching
                    ? PlayerTransitionState.CrouchGuard
                    : PlayerTransitionState.StandGuard;

            }
            else
            {
                nextState = isCrouching
                    ? PlayerTransitionState.Crouch
                    : PlayerTransitionState.Normal;
            }

            //変えようとしているステートが現在のステートなら変更しない
            if (owner.playerState.currentTransitionState == nextState)
                return;

            //入力方向の変更
            firstDir = dir;

            //
            InvokeUpdateStateAction(nextState);
        }
        protected override void OnExit(State nextstate)
        {
            //遷移先がガードならしゃがみ状態のまま遷移
            if (reverseState.TryGetValue(nextstate, out var state)
                && state == (int)Event.GuardEvent) return;

            //しゃがみアニメーション解除
            owner.controller.PlayNormalAnimation<bool>
                (AnimationId.Crouching, false);

        }
    }
    private class Guard : State
    {
        PlayerTransitionState currentState;
        bool isCrouching;
        protected override void OnEnter(State prevstate)
        {
            isCrouching = owner.commandList.Contains(PlayerInputHandler.ActionState.Crouching);
            //しゃがんでいなかったらアニメーション再生終了
            if(!isCrouching)
                owner.controller.PlayNormalAnimation<bool>(AnimationId.Crouching,false);

            InvokeEventAction(owner.CheckGuard());
        }
        private void InvokeEventAction(PlayerTransitionState state)//イベントを発火
        {
            owner.OnSetCanMoveFlagAction?.Invoke(false);
            owner.playerState.ChangeState(state);
        }

        protected override void OnUpdate()
        {
            if (!isCrouching)
            {
                var moveInput = owner.playerInputHandler.MoveInput;
                owner.controller.PlayMoveAnimation(moveInput.x);
            }
            //ガードをしているかの確認
            CheckGuarding();
        }


        private void CheckGuarding()
        {
            // ガード維持条件 ガードボタン + 逆方向入力
            bool isStillGuarding = owner.PlayerCheckDirection();

            if (!isStillGuarding)
            {
                stateMachine.Dispatch((int)Event.MoveEvent);
                return;
            }
            if (isStillGuarding)
            {

                var IsChangeState = currentState != owner.CheckGuard();
                if(IsChangeState)
                {
                    var state = owner.CheckGuard();
                    InvokeEventAction(state);
                    currentState = state;
                }
            }


        }
        protected override void OnFixedUpdate()
        {
            if (!owner.IsAction&&!isCrouching)
                owner.simpleMovement.UpdateMoving(owner.playerInputHandler.MoveInput);
        }
        protected override void OnExit(State nextstate)
        {
            owner.OnSetCanMoveFlagAction?.Invoke(true);
        }
    }
    private class Attack : State
    {
        private int prevEvent;//この前のイベントをintで管理

        private PlayerAction currentState;//現在のプレイヤーの行動
        private PlayerInputHandler.ActionState prevState;

        private ActionFrameData currentAttackData;//現在の技データ

        protected override void OnEnter(State prevstate)
        {

            SetState(prevstate);
            currentAttackData = owner.controller.GetCurrentAttackData();
            if (currentAttackData == null)
            {
                //無限にこのステートにならないように次のティックでIdleにする
                owner.RunNextTick(this, () => { stateMachine.Dispatch((int)Event.MoveEvent); });
                return;
            }
            //debagのために現在のフレームをセットしている
            owner.textmanager.SetAttackDataInfo(currentAttackData);

            //ヒットストップ中は動けないしアニメーションは止める
            if (owner.GetHitStopFlag())
            {
                owner.controller.StopClipAnimation();
            }

            owner.SetCanRotationFlag();
            InitFrame();
            owner.colliderManager.InitColliderInfo(currentAttackData);

            //ノックバック中ならノックバックを取りけす
            if (owner.backMotion.CheckIsKnockBack())
                owner.backMotion.RemoveKnockBackCoroutine();

            if (prevEvent != (int)Event.JumpEvent)
                owner.simpleMovement.LockRotationPlayer();//向きを固定
        }
        private void InitFrame()
        {
            owner.currentAnimationFrame = 1;
            owner.currentHitStopFrame = 1;
        }

        private void SetState(State state)//ステートをセットする
        {
            //現在のステートを取得する
            prevState = owner.currentActionState;

            //ジャンプしていたらActionStateをジャンプにする
            reverseState.TryGetValue(state, out prevEvent);
            currentState = prevEvent is (int)Event.JumpEvent ? PlayerAction.Jump : PlayerAction.Attack;

            if (currentState is not PlayerAction.Jump)
                owner.simpleMovement.ResetJumpFlag(false);

            owner.ChangePlayerActionState(currentState);
            owner.ChangeCancelActionState(AcceptInput.Disable);
        }

        /// <summary>(
        /// 攻撃のフレームが終わるもしくは、敵もしくは自分が当たると次の処理開始
        /// </summary>
        protected override void OnUpdate()//スーパークラスからのUpdate関数
        {
            if (currentAttackData == null) return;

            if (owner.GetHitStopFlag())
            {
                UpdateHitStopFrame();
                return;
            }
            // ジャンプ攻撃中はジャンプ処理を優先し、終了したら通常遷移を行う
            if (CurrentPlayingJump()) return;

            //debagのために現在のフレームを渡している
            owner.textmanager.UpdateFrame(owner.currentAnimationFrame);
            //当たり判定更新
            owner.colliderManager.UpdateColliders(currentAttackData, owner.currentAnimationFrame);

            //アニメーション更新
            owner.controller.PlayAnimation(owner.currentAnimationFrame);

            CheckEndAnimation();

            //フレーム更新
            owner.currentAnimationFrame++;
            owner.currentJumpFrame++;

        }
        private void CheckEndAnimation()
        {
            if (currentAttackData == null) return;

            if (owner.currentAnimationFrame >= currentAttackData.frameData[^1].FrameNumber)
            {
                if (currentState == PlayerAction.Jump)
                    owner.playerState.ChangeState(PlayerTransitionState.Flying);
                //ジャンプ攻撃中ならジャンプが終わるまで終了しない
                if (!owner.simpleMovement.IsJumping)
                {
                    owner.CanMoveState();
                    owner.CheckAction(PlayerInputHandler.InputState.Canceled, prevState);
                }
            }
        }
        private void UpdateHitStopFrame()//ヒットストップの処理
        {
            owner.currentHitTotalFrame = currentAttackData.HitStopFrame;

            //相手がガードしていたらガード用のフレームにする
            if (owner.targetCurrentState is PlayerTransitionState.StandGuard 
                or PlayerTransitionState.CrouchGuard)
                owner.currentHitTotalFrame = currentAttackData.GuardStopFrame;

            if (owner.currentHitStopFrame >= owner.currentHitTotalFrame)
            {
                if (owner.GetHitStopFlag()) owner.controller.StartClipAnimation();
                owner.SetHitStopFlag();
            }
            owner.currentHitStopFrame++;
        }
        private bool CurrentPlayingJump()//ジャンプしているかを見ている
        {
            if (currentState != PlayerAction.Jump) return false;

          if(owner.simpleMovement.UpdateJumps(owner.currentJumpFrame, owner.currentJumpTotalFrame))
            {
                owner.CanMoveState();
                owner.CheckAction(PlayerInputHandler.InputState.Canceled, prevState);
                return true;
            }
            return false;
        }


        protected override void OnExit(State nextstate)
        {
            currentAttackData = null;
            RemoveCommandList();
            owner.controller.SetThrowingFlag();//投げフラグを解除
            owner.SetTargetTransitionState(PlayerTransitionState.Normal);
            owner.OnStopAction?.Invoke();//攻撃判定を戻すため

            //次の遷移ステートがヒットか行動不可能ステートなら以降の処理をしない
            if (CanInitState()) return;

            owner.controller.EndAnime(true);

            owner.CanMoveState();

        }
        private bool CanInitState()
        {
            return stateMachine.NextState is (int)Event.IsHitEvent or (int)Event.InoperableEvent or (int)Event.AttackEvent;
        }
        private void RemoveCommandList()//実行したコマンドを消去
        {
            owner.RemoveCommandList(prevState);
        }
    }
    private class Step: State
    {
        private bool isBack;
        protected override void OnEnter(State prevstate)
        {

            owner.SetCanRotationFlag();
            owner.ChangePlayerActionState(PlayerAction.Inoperable);

            //バックならtrueが返ってくる
            isBack = owner.PlayerCheckDirection();

            InitializeStep();
            DisableCollision();
            owner.currentAnimationFrame = 1;
        }
        private void InitializeStep()//エフェクト再生
        {
            //アニメーション再生箇所を取得
            Transform leftLeg = owner.controller.GetAnchor(AnimationManager.EffectAnchor.LeftLeg);
            Transform rightLeg = owner.controller.GetAnchor(AnimationManager.EffectAnchor.RightLeg);

            //エフェクト消滅フレームを設定
            int returnTime = 20;
            owner.playerEffectController.PlayEffect
              (EffectType.Step, leftLeg.position, Quaternion.identity, returnTime);

            owner.playerEffectController.PlayEffect
              (EffectType.Step, rightLeg.position, Quaternion.identity, returnTime);

            

            //アニメーションを初期化
            if (isBack) return;//バックなら返す

            owner.controller.PlayAnimation(AnimationManager.AnimationId.Stepping);
        }
        private void DisableCollision()//イベントを発火
        {
            owner.OnActiveCollisionAction?.Invoke(AnimationId.None, false);
        }
        protected override void OnUpdate()
        {
            //プレイヤーの位置を更新
            owner.simpleMovement.UpdateStep(owner.transform.position,owner.currentAnimationFrame);

            //アニメーションを更新
            if(!isBack)
                owner.controller.PlayAnimation(owner.currentAnimationFrame);

            owner.currentAnimationFrame++;

            //ステップが終わったらステートを遷移
            if (!owner.simpleMovement.IsStepping())
                owner.stateMachine.Dispatch((int)Event.MoveEvent);
        }
      
        protected override void OnExit(State nextstate)
        {
            owner.simpleMovement.InitStepInfo();//ステップのステータスを初期化

            owner.controller.EndAnime();
        }
    }
    private class Die : State
    {
        ActionFrameData currentAnimationData;
        ActionFrameData NextAnimationData;

        bool isUsingAnimatorDeath;
        int animationTotalFrame;
        protected override void OnEnter(State prevstate)
        {
            owner.ReleaseHitStop();

            //同じティックで他の処理がアニメーションを設定し終わってから、次のティックでデータを取得
            owner.RunNextTick(this, () =>
            {
                currentAnimationData = owner.controller.GetCurrentAttackData();

                //データが無い時はアニメーターの死亡モーションを使う
                if (currentAnimationData == null)
                {
                    isUsingAnimatorDeath = true;
                    owner.controller.StopAnimation();
                    owner.controller.PlayDieAnimation();
                    owner.ChangePlayerActionState(PlayerAction.Die);
                    return;
                }

                //特定のアニメーションが再生されていたらDieアニメーションは再生しない
                if (!CanCurrentAnimation(currentAnimationData.ActionName))
                {
                    isUsingAnimatorDeath = true;
                    owner.controller.StopAnimation();
                    owner.controller.PlayDieAnimation();
                }
                InitFrame();

                owner.ChangePlayerActionState(PlayerAction.Die);
            });
        }
        private void InitFrame()
        {
            animationTotalFrame = currentAnimationData.frameData[^1].FrameNumber -2;
            if (owner.currentAnimationFrame >= animationTotalFrame) 
                owner.currentAnimationFrame = 1;

            owner.SetCanRotationFlag();
            owner.playerState.OffHpBarChange();
        }
        //特定のアニメーションが再生されていないか確認
        private bool CanCurrentAnimation(AnimationId id)
        {
            //空中やられなら強制的にtrueに
            if (owner.playerState.currentTransitionState is PlayerTransitionState.Flying)
            {
                NextAnimationData = owner.controller.GetAttackData(AnimationId.Launching);
                return true;
            }

            bool isPlaying = id switch
            {
                AnimationId.Down => true,
                AnimationId.Thrown =>true,
                _=>false
            };

            return isPlaying;
        }
        protected override void OnUpdate()
        {
            if (currentAnimationData == null||isUsingAnimatorDeath) return;
            //アニメーション更新
            owner.controller.PlayAnimation(owner.currentAnimationFrame);

            //再生が終了したら流す
            if (owner.currentAnimationFrame >= animationTotalFrame)
            {
                //次に再生するアニメーションがあれば情報をセット
                if (NextAnimationData != null)
                    SetAnimationInfo();
                
                return;
            }
            //フレーム更新
            owner.currentAnimationFrame++;
            base.OnUpdate();
        }
        private void SetAnimationInfo()//アニメーション再生に必要なフレームの初期化
        {
            //トータルフレーム初期化
            animationTotalFrame = NextAnimationData.frameData[^1].FrameNumber-2;

            //現在のフレームを初期化
            owner.currentAnimationFrame = 1;

            //アニメーション情報をセット
            owner.controller.PlayAnimation(NextAnimationData.ActionName);
            NextAnimationData = null;
        }
        protected override void OnExit(State nextstate)//バトル最初の状態に初期化
        {
            //フラグを戻す
            isUsingAnimatorDeath = false;
            
            //データを外す
            currentAnimationData = null;
            NextAnimationData = null;

            SetNextRoundInfo();

            base.OnExit(nextstate);
        }
        private void SetNextRoundInfo()//次の試合に行くときの初期化処理
        {
            owner.CheckSpecialPlayerState();                              // 特殊な状態か確認
            owner.SetHitStopFlag();                                       // ヒットストップを解除
            owner.CanMoveState();                                         // 行動可能な状態に戻す
            owner.SetHitOrGuardFlag(PlayerHitDirector.HitResult.Hit, false); // ヒット・ガード状態を解除
            owner.controller.EndAnime();                                 // アニメーションを終了
            owner.playerState.OnResetComboCount();                       // コンボ情報をリセット
            owner.colliderManager.ResetColliderInfo();                   // 当たり判定を初期化
            owner.commandList.Clear();                                    // 入力コマンドをクリア
            owner.simpleMovement.ResetJumpFlag(false);                   // ジャンプのフラグを直す
        }
    }
    private class Hitting : State
    {

        private PlayerTransitionState currentState;
        private bool isGuarding;

        private ActionFrameData currentHitAttackData;//ヒットしたときの攻撃データ
        private ActionFrameData currentAnimationData;//再生中の攻撃データ

        private Queue<ActionFrameData> animationQueue;//他に再生するアニメーションを補間

        private int stiffnessFrame;
        private int currentTotalAnimationFrame;
        private int currentHitStopFrame;
        protected override void OnEnter(State prevstate)
        {
            //初期化
            Init();

            SetStunFrameTime();
            //イベント発火
            InvokeEventAction();
        }
        private void Init()
        {
            currentState = owner.playerState.currentTransitionState;

            //ガード状態か確認
            isGuarding = currentState is
                PlayerTransitionState.CrouchGuard or PlayerTransitionState.StandGuard;

            InitState();

            if (owner.GetHitStopFlag()) owner.controller.StopClipAnimation();

            //受けた攻撃の情報を取得
            currentHitAttackData = owner.hitDirector.GetAttackData();

            //やられアニメーションの情報を取得
            currentAnimationData = owner.controller.GetCurrentAttackData();


            owner.textmanager.SetAttackDataInfo(currentHitAttackData);
            SetupAnimationQueue();

            InitFrame();

        }
        private void InitState()
        {
            //行動不能状態にする
            owner.ChangePlayerActionState(PlayerAction.Inoperable);
            owner.ChangeCanNextActionFlag();
            owner.ChangeIsActionFlag(true);
            owner.SetCanRotationFlag();
        }
        private void InitFrame()
        {
            //やられアニメーションの全体フレームを取得
            currentTotalAnimationFrame = currentAnimationData.frameData[^1].FrameNumber - 2;
            //現在のフレームを初期化
            owner.currentAnimationFrame = 1;
            currentHitStopFrame = 1;
        }
        private void SetupAnimationQueue()//リストの初期化
        {
            animationQueue ??= new();

            animationQueue.Clear();

            //再生リストにアニメーションクリップが入っていたらキューに保存
            foreach (var animation in currentAnimationData.AnimationQueue)
            {
                animationQueue.Enqueue(animation);
            }
        }
        private void SetStunFrameTime()
        {
            int normalFrame = currentHitAttackData.HitStunTime;

            if (isGuarding) normalFrame = currentHitAttackData.BlockStunTime;

            stiffnessFrame = normalFrame;

        }
        private void InvokeEventAction()//イベントを発火させる
        {
            owner.OnActiveCollisionAction?.Invoke(AnimationId.Idle, false);
            owner.OnSetCanMoveFlagAction?.Invoke(false);
        }

        protected override void OnUpdate()
        {
            if (currentAnimationData == null) return;

            //ヒットストップフラグが経っていたら処理する
            if (owner.GetHitStopFlag())
            {
                UpdateHitStopFrame();
                return;
            }
            //でバック
            owner.textmanager.HitUpdateFrame(owner.currentAnimationFrame);
            //アニメーションを再生
            owner.controller.PlayAnimation(owner.currentAnimationFrame);

            //硬直
            UpdateStunTime();

            //フレームを更新
            owner.currentAnimationFrame++;

        }
        private void UpdateHitStopFrame()//ヒットストップの処理
        {
            int hitStopFrame = currentHitAttackData.HitStopFrame;

            if (isGuarding) hitStopFrame = currentHitAttackData.GuardStopFrame;

            if (currentHitStopFrame >= hitStopFrame)
            {
                if (owner.GetHitStopFlag()) owner.controller.StartClipAnimation();
                owner.SetHitStopFlag();
            }
            currentHitStopFrame++;
        }

        private void UpdateStunTime()
        {
            //動けるかどうかを確認
            if (CanMove())
            {
                //再生するアニメーションが合ったら再生
                if (animationQueue.Count <= 0) return;

                //終わりのフレームになったら０に戻してアニメーションを再生
                if (owner.currentAnimationFrame >= currentTotalAnimationFrame)
                {
                    SetAnimationInfo();//アニメーションを再セット
                }
                return;
            }
            //硬直が終わったら遷移前のステートに戻す
            if (owner.currentAnimationFrame >=  stiffnessFrame)
            {
                owner.currentAnimationFrame = 1;

                //やられフラグを初期化
                owner.SetHitOrGuardFlag(PlayerHitDirector.HitResult.Guard,false);
                owner.SetHitOrGuardFlag(PlayerHitDirector.HitResult.Hit, false);

                owner.CanMoveState();
                //行動開始
                stateMachine.Dispatch((int)Event.MoveEvent);
            }
        }
        private void SetAnimationInfo()
        {
            var anima = animationQueue.Dequeue();

            //アニメーションの情報をセット
            owner.controller.PlayAnimation(anima.ActionName);
            owner.currentAnimationFrame = 1;
            currentTotalAnimationFrame = anima.frameData[^1].FrameNumber - 2;
        }
        private bool CanMove()//ここに条件を書く
        {
            return owner.playerState.currentTransitionState is PlayerTransitionState.Flying;
        }
        protected override void OnExit(State nextstate)
        {
            currentAnimationData = null;

            //ヒットかダウンステートに遷移するならアニメーションの初期化はしない
            if (ShouldSkipAnimationReset(stateMachine.NextState))
                return;

            EndHitState();

            ResetComboCount();
        }
        private void EndHitState()
        {
            owner.controller.EndAnime();// アニメーションを終了
            owner.controller.StopAnimation();
            owner.colliderManager.ResetColliderInfo();// 当たり判定を初期化
            owner.playerState.OffHpBarChange();//Ui更新
        }
        //特殊なアニメーションかどうか確認
        private bool ShouldSkipAnimationReset(int nextState)
        {
            return nextState is
                (int)Event.IsHitEvent or
                (int)Event.InoperableEvent or
                (int)Event.DieEvent;
        }
        private void ResetComboCount()
        {
            owner.playerState.OnResetComboCount();
        }
    }
    private class Win : State
    {
        private const float WinDelay = 1f;

        private ActionFrameData currentAnimationData;
        private int winAnimationTotalFrame;

        private bool isPlayPreviousAnimation = false;

        protected override void OnEnter(State prevstate)
        {
            owner.ReleaseHitStop();

            owner.playerState.ChangeState(PlayerTransitionState.Win);

            currentAnimationData = owner.controller.GetCurrentAttackData();
            if (currentAnimationData != null)
            {
                isPlayPreviousAnimation = true;

                winAnimationTotalFrame =
                    currentAnimationData.frameData[^1].FrameNumber;

                return;
            }

            Delay.WaitFrame(owner, TickScheduler.SecondsToTicks(WinDelay), completeAction: ()=>
            {
                owner.controller.SetWinAnimation();
                SetPlayAnimation();
            });
           
        }
        private void SetPlayAnimation()
        {
            currentAnimationData = owner.controller.GetCurrentAttackData();

            winAnimationTotalFrame =
                currentAnimationData.frameData[^1].FrameNumber;

            owner.currentAnimationFrame = 1;
        }
        protected override void OnUpdate()
        {
            if (currentAnimationData == null) return;

            //特定のアニメーションまで再生したら止める
            if (owner.currentAnimationFrame >= winAnimationTotalFrame)
            {
                if (isPlayPreviousAnimation)
                {
                    ChangeAnimation();
                }
                return;
            }

            //アニメーション更新
            owner.controller.PlayAnimation(owner.currentAnimationFrame);

            //フレーム更新
            owner.currentAnimationFrame++;
        }
        private void ChangeAnimation()//アニメーションを勝利モーションに変える
        {
            owner.controller.SetWinAnimation();

            SetPlayAnimation();
            isPlayPreviousAnimation = false;
        }
        protected override void OnExit(State nextstate)//初期化処理
        {
            currentAnimationData = null;

            SetNextRoundInfo();

            base.OnExit(nextstate);
        }
        private void SetNextRoundInfo()//次の試合に行くときの初期化処理
        {
            owner.CheckSpecialPlayerState();// 特殊な状態か確認
            owner.SetHitStopFlag();// ヒットストップを解除
            owner.CanMoveState();// 行動可能な状態に戻す
            owner.controller.EndAnime();// アニメーションを終了
            owner.colliderManager.ResetColliderInfo();// 当たり判定を初期化
            owner.commandList.Clear(); // 入力コマンドをクリア
            owner.simpleMovement.ResetJumpFlag(false);// ジャンプのフラグを直す
        }
    }

    private class Inoperable:State
    {
        private ActionFrameData currentAnimationData;
        private Queue<ActionFrameData> animationQueue;

        private PlayerInputHandler.ActionState prevAction;//遷移前の行動
        private int animationTotalFrame;
        protected override void OnEnter(State prevstate)
        {
            prevAction = owner.currentActionState;
            //当たり判定のリストをクリアにする
            ClearCollider();

            //動けないようにする
            CantMove();

            //同じティックで他の処理がアニメーションを設定し終わってから、次のティックでデータを取得
            owner.RunNextTick(this, () =>
            {
                currentAnimationData = owner.controller.GetCurrentAttackData();

                //データがなかった時ずっとこのステートになってしまうから無かったらIdleにする
                if (currentAnimationData == null)
                {
                    stateMachine.Dispatch((int)Event.MoveEvent);
                    return;
                }
                
                owner.colliderManager.InitColliderInfo(currentAnimationData);

                InitFrame();
              
                SetupAnimationQueue();

            });
            InvokeEventAction();

        }
        private void InitFrame()
        {
            animationTotalFrame = currentAnimationData.frameData[^1].FrameNumber;
            owner.currentAnimationFrame = 1;
        }
        private void ClearCollider()//当たり判定をクリアにする
        {
            owner.colliderManager.StopCollider();
            owner.colliderManager.ClearHurtBox();
        }
        private void CantMove()
        {
            owner.ChangeIsActionFlag(true);
            owner.ChangeCanNextActionFlag();
        }
        private void InvokeEventAction()//イベントを発火
        {
            owner.OnActiveCollisionAction?.Invoke(AnimationId.None, false);//当たり判定をなくす
            owner.playerState.ChangeState(PlayerTransitionState.PlayerDown);//これを登録する
            owner.ChangePlayerActionState(PlayerAction.Inoperable);
        }
        private void SetupAnimationQueue()//リストの初期化
        {
            animationQueue ??= new();

            animationQueue.Clear();
            //再生リストにアニメーションクリップが入っていたらキューに保存
            foreach (var anima in currentAnimationData.AnimationQueue)
            {
                animationQueue.Enqueue(anima);
            }
        }

        protected override void OnUpdate()
        {
            if (currentAnimationData == null||owner.playerState.IsDead) return;

            //フレーム情報更新 
            owner.colliderManager.UpdateColliders(currentAnimationData, owner.currentAnimationFrame);

            //アニメーションを更新
            owner.controller.PlayAnimation(owner.currentAnimationFrame);

            CheckEndAnimation();

            //フレームを更新
            owner.currentAnimationFrame++;
        }
        private void CheckEndAnimation()
        {
            //アニメーションが再生終了したらステートを変更
            if (owner.currentAnimationFrame > animationTotalFrame)
            {
                if (animationQueue.Count > 0)
                {
                    PlayNextAnimation();

                    return;
                }
                //ステートを変更
                owner.CanMoveState();
                stateMachine.Dispatch((int)Event.MoveEvent);

            }
        }
        //アニメーション再生に必要な情報をセット
        private void PlayNextAnimation()
        {
            var anima = animationQueue.Dequeue();

            //アニメーションの情報をセット
            animationTotalFrame = anima.frameData[^1].FrameNumber;
            owner.controller.PlayAnimation(anima.ActionName);
            owner.currentAnimationFrame = 1;
        }
        protected override void OnExit(State nextstate)
        {
            currentAnimationData = null;

            //ステートをクリアにする
            ResetHitState();

            ResetComboCount();
            owner.RemoveCommandList(prevAction);
            owner.playerState.OffHpBarChange();//Uiに反映

            //特定のアニメーションだったらアニメーションを初期化しない
            if (stateMachine.NextState is (int)Event.DieEvent) return;
            owner.controller.EndAnime();
        }
        private void ResetHitState()//状態をキレイにする
        {
            owner.SetHitOrGuardFlag(PlayerHitDirector.HitResult.Hit, false);//フラグを直す

            owner.simpleMovement.ResetJumpFlag(false);

            //投げられている状態がまだだったら消す
            if (owner.playerState.currentState.HasFlag(StatusFlags.IsThrowing))
            {
                owner.playerState.RemoveStatusFlag(StatusFlags.IsThrowing);
            }

        }
        private void ResetComboCount()//コンボカウントをリセット
        {
            owner.playerState.OnResetComboCount();
        }
    }
}

