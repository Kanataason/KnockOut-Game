using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
public class AnimationManager : MonoBehaviour
{
    [Serializable]
    public class Entry<T,To>where T:Enum
    {
        public T Key;
        public To Value;
    }
    /// <summary>
    /// アニメーション種類
    /// </summary>
    public enum AnimationId
    {
        //通常攻撃
        WeakAttack,
        MiddleAttack,
        StrongAttack,
        SpecialAttack,
        TiltAttack,
        SmashAttack,

        //ノーマルモーション
        Jumping,
        Crouching,
        Stepping,

        //しゃがみ攻撃
        CrouchKickAttack,
        CrouchUpperAttack,

        //特殊攻撃
        Throw,
        Smash,
        Grabbing,

        //ジャンプ攻撃
        JumpingAttack,

        //状態
        Guarding,
        Hitting,
        Launching,
        Launch,
        WakeUp,
        Thrown,
        Down,
        Airborne,//空中
        Throwing,
        Dead,

        //エモート
        WinDance,

        None,
        Idle
    }
    /// <summary>
    /// 演出用のステート
    /// </summary>
    public enum PresentationState
    {
        None,
        Charge
    }

    /// <summary>
    /// エフェクトを出すポジション
    /// </summary>
    public enum EffectAnchor
    {
        Hand,//手
        RightLeg,//右足
        LeftLeg,//左足
        Target,//ターゲットのとこ
        Body,//体
        Root,//自分の座標
        TargetHead,//ターゲットの頭
        MyHead,//自分の頭
        None,
    }

    /// <summary>
    /// アバターマスクのステート
    /// </summary>
    public enum AvatarState
    {
        Normal,

        CrouchBody,
        CrouchHit,
        CrouchGuard,
        CrouchKick,
        CrouchUpper,

        JumpAttack,

        Smash
    }

    private static readonly int Is_NextRoundAnima = Animator.StringToHash("NextRound");
    private static readonly int Is_Dead = Animator.StringToHash("Die");
    private static readonly int Is_DeadType = Animator.StringToHash("DeadType");
    private static readonly int F_MoveInput = Animator.StringToHash("MoveInput");
    private static readonly int Is_Crouch = Animator.StringToHash("IsCrouch");
    #region
    private Animator animator;
    private AttackFrameController attackFrameController;
    private PlayableAnimationPlayer playable;
    private ColliderManager colliderManager;
    private PlayerController controller;
    private PlayerKnockBackController knockBack;
    private BaseActionController actionController;
    private PlayerInputHandler playerInputHandler;
    private PlayerState playerState;
    #endregion

    [SerializeField] private AssetReferenceGameObject createObj;

    //addresableに対応させる
    [SerializeField] private List<Entry<EffectAnchor, Transform>> rootReferences = new();
    [SerializeField] private List<Entry<AvatarState, AssetReferenceT<AvatarMask>>> avatarMaskReferences = new();
    [SerializeField] private List<Entry<AnimationId, AssetReferenceT<ActionFrameData>>> attackDataReferences = new();

    //各リストに対応したディクショナリー
    private Dictionary<EffectAnchor, Transform> rootDictionary = new();
    private Dictionary<AvatarState, AvatarMask> avatarMaskDictionary = new();
    private Dictionary<AnimationId, ActionFrameData> attackdataList = new();

    //ロードしたものを保存する用リスト
    private readonly List<AsyncOperationHandle> handles = new();

    private bool isThrowing = false;

    private PresentationState currentPresentationState;//演出用のステート

    public event System.Action<Transform,Action> OnCameraZoom;

    private ActionFrameData currentAnimationData = null;

    //データのロード処理 ロード完了を待つために保持
    private Task loadTask;

    //アニメーショングラフの初期化が終わっているか
    private bool isPlayableReady = false;

    /// <summary>
    /// 攻撃データとアバターマスクの読み込みが終わったか（失敗した場合も終わった扱い）
    /// </summary>
    public bool IsLoadFinished { get; private set; } = false;

    async void Start()
    {
        TryGetComponents();

        SubscribeEvents();

        //バトルのシーン出なければロードしない
        if (BattleStateManager.Instance == null) return;

        InitList();


        handles.Clear();

        //async voidの例外は握りつぶされるのでここでログに出す
        try
        {
            loadTask = LoadData();
            await loadTask;
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
        finally
        {
            //失敗してもバトルの初期化が止まらないように終わったことにする
            IsLoadFinished = true;
        }
    }

    private async Task LoadData()
    {
        attackdataList ??= new();
        avatarMaskDictionary ??= new();

        attackdataList.Clear();
        avatarMaskDictionary.Clear();
        //データの生成元をリストで取得
        var attackLoads = attackDataReferences.Select(async attack =>
        {
            var handle = attack.Value.LoadAssetAsync<ActionFrameData>();
            handles.Add(handle);

            var data = await handle.Task;
            return (attack.Key, data);
        });

        var avatarLoads = avatarMaskReferences.Select(async avatar =>
        {
            var handle = avatar.Value.LoadAssetAsync<AvatarMask>();
            handles.Add(handle);

            var data = await handle.Task;
            return (avatar.Key, data);
        });
        //すべて入れ終わったらロード完了するまで待つ
        var attacksTask = Task.WhenAll(attackLoads);
        var avatarsTask = Task.WhenAll(avatarLoads);

        await Task.WhenAll(attacksTask, avatarsTask);

        //ディクショナリーに追加
        foreach (var (key, data) in attacksTask.Result)
            attackdataList[key] = data;

        foreach (var (key, data) in avatarsTask.Result)
            avatarMaskDictionary[key] = data;
    }
    private void OnDestroy()
    {
        foreach (var handle in handles)
        {
            Addressables.Release(handle);
        }
    }
    //攻撃の情報が入ったデータをディクショナリーに登録
    private void InitList()
    {
        //シーン上にあるものを入れるのでAddresable化しない
        foreach (var root in rootReferences)
        {
            rootDictionary[root.Key] = root.Value;
        }
    }
    /// <summary>
    /// 参照を取得してアニメーショングラフにアバターマスクを設定
    /// </summary>
    public async void SetReferencePlayable(PlayableAnimationPlayer referencePlayable)
    {
        playable = referencePlayable;

        //ロードが終わる前に呼ばれてもアバターマスクがnullにならないように完了を待つ
        if (loadTask != null && !loadTask.IsCompleted)
        {
            try
            {
                await loadTask;
            }
            catch (Exception)
            {
                //例外はStartでログに出しているのでここでは何もしない
            }
        }

        //待っている間に破棄されていたら何もしない
        if (this == null) return;

        playable.SetAvatarMask(GetAvatarMask(AvatarState.Normal), GetAvatarMask(AvatarState.CrouchBody));
        isPlayableReady = true;
    }
    private void TryGetComponents()//宣言したコンポーネントを取得
    {
        knockBack = GetComponent<PlayerKnockBackController>();
        attackFrameController = GetComponent<AttackFrameController>();
        playerState = GetComponent<PlayerState>();
        animator = GetComponent<Animator>();
        playerInputHandler = GetComponent<PlayerInputHandler>();
        controller = GetComponent<PlayerController>();
        colliderManager = GetComponent<ColliderManager>();
        actionController = GetComponent<BaseActionController>();
    }
    public Transform GetAnchor(EffectAnchor state)//rootを取得
    {
        if (rootDictionary.TryGetValue(state, out var root))
        {
            return root;
        }
        return null;
    }
    private AvatarMask GetAvatarMask(AvatarState state)//アバターマスクを取得
    {
        var  resultState = GetAvatarState(state);

        if (avatarMaskDictionary.TryGetValue(resultState, out var avatar))
        {
            return avatar;
        }
        return null;
    }

    /// <summary>
    /// idle状態とcrouch状態のアニメーションを分けたいとき使う
    /// </summary>
    private AvatarState GetAvatarState(AvatarState state)
    {
        if (currentAnimationData == null) return state;

        bool isHit = currentAnimationData.ActionName is AnimationId.Hitting;

        bool isGuard = currentAnimationData.ActionName is AnimationId.Guarding;
        if (isGuard || isHit)
        {
            if (playerInputHandler.IsCrouching)
            {
                if (isHit)
                    state = AvatarState.CrouchHit;

                if (isGuard)
                    state = AvatarState.CrouchGuard;
            }
        }
        return state;
    }
    /// <summary>
    /// 各状態の終わりを通知するアクションを登録している
    /// </summary>
    private void SubscribeEvents()
    {
        knockBack.OnLanded += OnInitLanding;

        controller.OnJumpTransitionAction += OnSetJumpAnimation;

        playerState.OnStatusEnded += OnStatusEnd;
    }

    /// <summary>
    /// 各状態の終わりを通知するアクションを解除している
    /// </summary>
    private void UnSubscribeEvents()
    {
        if (knockBack != null) 
        knockBack.OnLanded -= OnInitLanding;

        if(controller != null)
        controller.OnJumpTransitionAction -= OnSetJumpAnimation;

        if (playerState == null) return;

        playerState.OnStatusEnded -= OnStatusEnd;

    }
    private void OnDisable()
    {
        UnSubscribeEvents();
    }

    //プレイヤーのアニメーションを再生させる関数
    public void PlayMoveAnimation(float moveSpeed)
    {
        float dampTime = 0.03f;
        //BattleTickerから1ティックごとに呼ばれるので1ティック分の時間で補間する
        animator.SetFloat(F_MoveInput, moveSpeed, dampTime, BattleTicker.TickTime);
    }
    #region
    public void OnWeakAttack()
    {
        PlayGroundAttack(AnimationId.WeakAttack);
    }
    public void OnMiddleAttack()
    {
        PlayGroundAttack(AnimationId.MiddleAttack);
    }
    public void OnStrongAttack()
    {
        var animationId = playerInputHandler.IsCrouching ? 
            AnimationId.CrouchKickAttack : AnimationId.StrongAttack;

        PlayGroundAttack(animationId);
    }
    public void OnSpecialAttack()
    {
        var animationId = playerInputHandler.IsCrouching ?
                AnimationId.CrouchUpperAttack : AnimationId.SpecialAttack;

        PlayGroundAttack(animationId);
    }
    public void OnTiltAttack()
    {
        colliderManager.ThrowActive(false);
        PlayGroundAttack(AnimationId.TiltAttack);
    }
    public void OnSmashAttack()//必殺技を発動可能状態かを見る
    {
        var data = GetAttackData(AnimationId.Smash);
        if (!playerState.CanUseGauge(data.UseGauge))
        {
            currentAnimationData = null;
            return;//ゲージが足りなかったら返す
        }
        attackFrameController.ActiveHurtCollider(AnimationId.None);
        colliderManager.ThrowActive(false);

        PlayAnimation(AnimationId.Smash);//アニメーション情報をセット

        currentPresentationState = PresentationState.Charge;//演出用のエフェクトステートをセット
        //演出を再生
        controller.PlayerStartPresentation(OnCameraZoom, this.transform, () => { PlaySmashAttack(); });

    }
    private void PlaySmashAttack()
    {

        playerState.UseGauge(currentAnimationData.UseGauge);//ゲージを消費

        controller.ChangePlayerState(PlayerInputHandler.InputState.Performed,
            PlayerInputHandler.ActionState.SmashAttack);

        actionController.OnAttackStart(currentAnimationData);

        //時間が経ったら完了させる
        Delay.WaitFrame(this, currentAnimationData.frameData[^1].FrameNumber,
            () => controller.IsHit, () => { },
            () =>
            {
                actionController.OnAttackEnd();
            });
    }
    public void OnThrowAttack()
    {
        if (TryFlyAttack()) return;

        if (playerState.currentTransitionState is PlayerTransitionState.Hitting) return;

        SetThrowingFlag(true);

        //もし投げられている状態なら返す
        if (playerState.currentState.HasFlag(StatusFlags.IsThrowing))
        {
            //捕まれ状態を解除
            playerState.RemoveStatusFlag(StatusFlags.IsThrowing);

        }
        PlayAnimation(AnimationId.Throw);
    }
    private void PlayGroundAttack(AnimationId id)
    {
        if (TryFlyAttack())
            return;

        PlayAnimation(id);

        playerState.ChangeState(PlayerTransitionState.Counter);

    }
    private bool TryFlyAttack()
    {
        if (controller.PlayerActionState is PlayerAction.Jump)
        {
            PlayAnimation(AnimationId.JumpingAttack);
            return true;
        }
        return false;
    }
    #endregion
    //アニメーションのデータを取得してアニメーションの初期化をする
    private void SetAnime(AnimationId attackId)
    {
        currentAnimationData = GetAttackData(attackId);

        if (currentAnimationData == null) return;

        //アニメーショングラフの準備ができていなければ再生しない
        if (!isPlayableReady) return;
        //テーブルの初期化
        playable.SetPlayInfo(currentAnimationData.clip, GetAvatarMask(currentAnimationData.AvatarMask));
        //アニメーションの初期化
        playable.InitAnimaInfo(currentAnimationData.frameData[^1].FrameNumber, currentAnimationData.clip);
    }
    //アニメーションを手動で１フレームずつ流す
    public void PlayAnimation(int currentAnimationIndex)
    {

        if (currentAnimationData == null) return;
        //位置フレームずつ流す
        playable.PlayAnimation(currentAnimationIndex,
            currentAnimationData.frameData[^1].FrameNumber, currentAnimationData.clip);

    }
    //アニメーションがながれたら流す
    public void EndAnime(bool isCancelAttack = false)
    {
        if(!isCancelAttack)
        currentAnimationData = null;

        playable.EndManual();
        playable.EndPlayAnima();
    }

    public ActionFrameData GetCurrentAttackData() => currentAnimationData;//現在の攻撃データを取得
    public PresentationState GetPresentationState() => currentPresentationState;//現在の演出用ステートを取得

    public void SetThrowingFlag(bool isThrow = false) => isThrowing = isThrow;

    public bool GetThrowingFlag() => isThrowing;

    










    //キーを指定してデータを取得
    public ActionFrameData GetAttackData(AnimationId attackId)
    {
       if(attackdataList.TryGetValue(attackId,out var data))
        {
            return data;
        }
        Debug.LogWarning($"{attackId}のデータが登録されていない");
        return null;
    }
    /// <summary>
    /// アニメーションコントローラーで再生されるノーマルモーションを再生する
    /// </summary>
    /// <param name="animationId"> 何を再生するのか確認ステート</param>
    /// <param name="value"> アニメーションにセットする値</param>
    public void PlayNormalAnimation<T>(AnimationId animationId,T value)
    {
        switch (animationId)
        {
            case AnimationId.Crouching:
                if(value is bool resultValue)
                    animator.SetBool(Is_Crouch, resultValue);//アニメーションにセット
                break;
            case AnimationId.Dead:
                if(value is int resultNum)
                {
                    animator.SetInteger(Is_DeadType, resultNum);
                    animator.SetTrigger(Is_Dead);
                }
                break;
        }
    }
    /// <summary>
    /// フレーム単位で操作する必要があるアニメーションを再生
    /// </summary>
    public void PlayAnimation(AnimationId animationId)
    {
        SetAnime(animationId);
    }
    public void ThrowAnime(AnimationId animationId)
    {
        //攻撃を受けているかの確認
        if (!playerState.IsCheckHit()) return;

        playable.StopManual();
        PlayAnimation(animationId);

        attackFrameController.StopCollider();

    }
    public void PlayCheckHitOrGuard(AnimationId animationId)
    {
        //アニメーションを初期化
        playable.StopManual();
        PlayAnimation(animationId);

        attackFrameController.StopCollider();

    }

    public void PlayLaunchAnimation(AnimationId animationId)
    {
        if (playerState.currentTransitionState is PlayerTransitionState.Flying)
            attackFrameController.ActiveHurtCollider(AnimationId.Airborne);
        else
            attackFrameController.ActiveHurtCollider(AnimationId.None);

        attackFrameController.StopCollider();

        colliderManager.ThrowActive(false);
        
        PlayAnimation(animationId);
    }
    private void OnSetJumpAnimation(int none)//ジャンプのアニメーションの情報をセット
    {
        StopAnimation();
        PlayAnimation(AnimationId.Jumping);
    }
    public void SetWinAnimation()
    {
        PlayAnimation(AnimationId.WinDance);
    }
    public void NextRoundAnimation()//最初の状態のアニメーションにする
    {
        animator.SetFloat(F_MoveInput, 0);
        animator.SetTrigger(Is_NextRoundAnima);
    }

    public void PlayDieAnimation()
    {

        var deadType = playerState.currentTransitionState is PlayerTransitionState.Flying ? 1 : 0;

        PlayNormalAnimation<int>(AnimationId.Dead, deadType);
    }

    //-----------------anima--------------//

    public void StopAnimation()
    {
        playable.EndManual();
        attackFrameController.ResetColliderInfo();
    }
    public void StopClipAnimation()//アニメーションを完全に止める
    {
        playable.StopClip();
    }
    public void StartClipAnimation()//アニメーションを完全に再生
    {
        playable.StartClip();
    }
   



    //--------------ここからはイベントから呼ばれる関数


    //地面についたときに通知される
    private void OnInitLanding()
    {
        if (playerState.IsDead) return;
        PlayLaunchAnimation(AnimationId.Launching);

        playerState.OnResetComboCount();
    }

    //Playerの特殊な状態が終わったときに通知される
    private void OnStatusEnd(AnimationId animationId)
    {
        actionController.OnCheckAction(GetAttackData(animationId));
    }
}
