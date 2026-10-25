using UnityEngine;

public class RADARActionController : BaseActionController
{
    private ColliderManager colliderManager;
    private AttackFrameController attackFrameController;

    private ColliderManager targetColliderManager;

    [SerializeField] private float bulletVelocity = 8;
    private void Start()
    {
        TryGetComponents();
    
        Initialize(controller, state, effectController, animationController);
    }
    private void TryGetComponents()
    {
        effectController = GetComponent<PlayerEffectController>();
        animationController = GetComponent<AnimationManager>();
        colliderManager = GetComponent<ColliderManager>();
        attackFrameController = GetComponent<AttackFrameController>();
    }
    //初期化
    protected override void Initialize(PlayerController controller, PlayerState state, PlayerEffectController effectController, AnimationManager animationController)
    {
        base.Initialize(controller, state, effectController, animationController);

        actionDictionary.Add(AnimationManager.AnimationId.Smash, OnSmashAttack);
    }
    //ステートにアクションが紐づいているか確認する
    public override void OnCheckAction(ActionFrameData data)
    {
        if (actionDictionary.TryGetValue(data.ActionName, out var action))
        {
            action?.Invoke(data);
            return;
        }
    }

    //攻撃初め
    public override void OnAttackStart(ActionFrameData data)
    {
        InvokeThrowSuccess(AnimationManager.AnimationId.None, false);

        OnCheckAction(data);
    }
    //攻撃終わり
    public override void OnAttackEnd()
    {
        base.OnAttackEnd();
    }

    //スマッシュ攻撃
    protected override void OnSmashAttack(ActionFrameData data)
    {
        //スポーン場所をゲット
        var root = animationController.GetAnchor(AnimationManager.EffectAnchor.Hand);

        //エフェクト再生
        effectController.PlayVfxEffectAndGetVfxEffect
            (EffectType.Smash, root, Quaternion.identity, effect => { SetEffectInfo(effect, data); });

        //Se再生
        AudioManager.Instance.PlaySe(AudioManager.SeState.RobotSA);
    }
    /// <summary>
    /// エフェクトに情報をセット
    /// </summary>
    /// <param name="effect"></param>
    private void SetEffectInfo(GameObject effect,ActionFrameData data)
    {
        //弾のスクリプトを取得
        var script = effect.GetComponent<BulletMovement>();

        //前方向に弾を出す
        effect.transform.position += transform.forward;

        Vector3 vector = transform.forward;//向き

        //相手を取得
        var opponent = attackFrameController.GetNormalOpponent();

        if (targetColliderManager == null)
        {
            targetColliderManager = opponent.GetComponent<ColliderManager>();
        }

        //弾に飛ぶ方向と,速さを与える
        script.SetBulletParameter(bulletVelocity, vector);

        //参照を渡す
        script.SetReference(targetColliderManager,opponent,colliderManager,data);
        
        float returnTime = 2;
        //エフェクト返却処理
        Delay.WaitFrame(this, TickScheduler.SecondsToTicks(returnTime), completeAction: () =>
        { effectController.ReturnEffect(EffectType.Smash, effect); });
    }
}
