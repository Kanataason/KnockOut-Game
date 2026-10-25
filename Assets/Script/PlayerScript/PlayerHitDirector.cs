using System;
using System.Collections.Generic;
using UnityEngine;
using static AnimationManager;
using static ColliderManager;
public class PlayerHitDirector : MonoBehaviour
{
    public enum HitResult
    {
        Hit,
        Guard,
        Counter,
    }
    [SerializeField] private int MaxCombo = 4;
    //参照先
    private PlayerState state;
    private PlayerController playerController;
    private PlayerKnockBackController knockBackManager;
    private AnimationManager animationController;
    private PlayerEffectController effectController;
    private ColliderManager colliderManager;
    private AttackFrameController attackFrameController;
    private BaseActionController actionController;

    private PlayerState targetState;

    private ActionFrameData currentAttackdata;

    private Dictionary<AttackAttribute, Action<ColliderManager.HitInfo,Vector3>> actionList;
    void Start()
    {
        TryGetComponents();
        InitList();
    }
    private void InitList()
    {
        actionList = new()
        {
            { AttackAttribute.DownAttack,ApplyDownAttack},
            {AttackAttribute.SmashAttack,ApplySmashAttack },
            {AttackAttribute.LaunchAttack,ApplyLaunchAttack },
            {AttackAttribute.NormalAttack,ApplyNormalAttack}
        };
    }
    private void TryGetComponents()
    {
        attackFrameController = GetComponent<AttackFrameController>();
        colliderManager = GetComponent<ColliderManager>();
        actionController = GetComponent<BaseActionController>();
        effectController = GetComponent<PlayerEffectController>();
        knockBackManager = GetComponent<PlayerKnockBackController>();
        state = GetComponent<PlayerState>();
        playerController = GetComponent<PlayerController>();
        animationController = GetComponent<AnimationManager>();
    }
    //受けた攻撃の情報を渡す
    public ActionFrameData GetAttackData()=> currentAttackdata;

    /// <summary>
    /// この攻撃が自分に当たるか
    /// 空中コンボは指定した回数までしか入らない 必殺技は入るようにする
    /// 当たらない時はヒットストップも攻撃の停止も起こさない
    /// </summary>
    public bool CanReceiveHit(ActionFrameData data)
    {
        return state.FlyComboCount < MaxCombo ||
            data.ActionName is AnimationId.Smash or AnimationId.SmashAttack;
    }


    //攻撃をした側の処理
    public void OnAttackGuard(HitInfo hitInfo)
    {
        //nullなら相手のステータスをゲット
        if (targetState == null)
            targetState = hitInfo.targetManager.GetComponent<PlayerState>();

        var data = hitInfo.hitBoxData;
        var dir = hitInfo.targetManager.transform.position - transform.position;

        playerController.SetTargetTransitionState(targetState.currentTransitionState);
        knockBackManager.StopForwardCoroutine();

        float correction = 0.7f;//補正
        state.AddGauge(data.AddGuardGauge * correction);

        bool isCanKnockBack = knockBackManager.CheckCanKnockBack(hitInfo.targetManager.transform.position);
        //相手が壁側に居るかを確認 もし居なかったらノックバック
        if (!isCanKnockBack) return;

        //ガードバック 反対方向に飛ぶためdirを反対に
        knockBackManager.ForwardMove(-dir, data.GuardKnockBackTime, data.GuardKnockBackDistance);
    }
    public void OnAttackHit(HitInfo hitInfo)
    {
        //nullなら相手のステータスをゲット
        if (targetState == null)
            targetState = hitInfo.targetManager.GetComponent<PlayerState>();

        var data = hitInfo.hitBoxData;
        var dir = hitInfo.targetManager.transform.position - transform.position;
        dir.y = 0;

        //相手が浮いている状態でもヒットにする
        PlayerTransitionState resultState = targetState.currentTransitionState == PlayerTransitionState.Flying ?
            PlayerTransitionState.Hitting:targetState.currentTransitionState;

        playerController.SetTargetTransitionState(resultState);
        knockBackManager.StopForwardCoroutine();

        state.AddGauge(data.AddHitGauge);

        if (hitInfo.hitBoxData.AtAttri != AttackAttribute.NormalAttack) return;

        if (playerController.PlayerActionState is PlayerAction.Jump) return;

        bool isCanKnockBack = knockBackManager.CheckCanKnockBack(hitInfo.targetManager.transform.position);
        attackFrameController.SetKnockBackFlag(isCanKnockBack);
        //相手が壁側に居るかを確認 もし居なかったらノックバック
        if (!isCanKnockBack) return;
        //ガードバック 反対方向に飛ぶためdirを反対に
        knockBackManager.ForwardMove(-dir, data.HitKnockbackTime, data.HitKnockbackDistance);
    }
    //投げた側の処理
    public void OnThrowHit(AnimationManager targetAnimationController,int throwTechFrames,HitInfo hitInfo)
    {
        //nullなら相手のステータスをゲット
        if (targetState == null)
            targetState = hitInfo.targetManager.GetComponent<PlayerState>();

        //グラップ入力受付時間を決めてタイマースタート
        Delay.WaitFrame(this, throwTechFrames, 
           () => targetAnimationController.GetThrowingFlag(),
           () => //投げ抜け成立
           {
               //投げ抜け処理
               ApplyThrowTech(hitInfo);

               //相手に投げ抜けを通知
               hitInfo.hitDirector.ApplyThrowTech(hitInfo); 
           },
           () => //投げ成立
           {
               //投げ処理
               OnApplyThrowing(targetAnimationController);

               //相手に投げ成立を通知
               hitInfo.hitDirector.ApplyThrow(hitInfo); 
           });
    }
    //投げが成立したら
    private void OnApplyThrowing(AnimationManager targetAnimationController)
    {
        //アニメーション再生
        animationController.PlayAnimation(AnimationId.Throwing);

        //投げられフラグを直す
        colliderManager.SetThrowingFlag();
        animationController.SetThrowingFlag();

        //ステートを変更
        playerController.HasInoperable();

        //相手の位置を補正させる
        var targetRoot = targetAnimationController.GetAnchor(EffectAnchor.Root);
        knockBackManager.SnapOpponentInFront(targetRoot);
    }
    //--------------------アニメーションイベントから呼ばれる

    //投げた側の処理
    public void OnApplyThrowingDamage(ActionFrameData data)
    {
        //ゲージを増やす
        state.AddGauge(data.AddHitGauge);
    }





    /// <summary>
    /// ここから下はアタックされた方の処理







    //ガード時の処理
    public void ReceiveGuard(HitInfo hitInfo)
    {
        //受けた攻撃のデータ
        var data = hitInfo.hitBoxData;
        currentAttackdata = data;

        //敵方向のベクトルを獲得
        var dir = hitInfo.targetManager.transform.position - transform.position;
        dir.y = 0;

        knockBackManager.StopForwardCoroutine();

        //ゲージを増やす
        state.AddGauge(data.AddGuardGauge);

        //nullなら相手のステータスをゲット
        if (targetState == null)
            targetState = hitInfo.targetManager.GetComponent<PlayerState>();

        //エフェクトを再生
        int returnTime = 30;
        effectController.PlayEffect(EffectType.Guard, hitInfo.hitPos, Quaternion.identity, returnTime);

        //アニメーション再生
        animationController.PlayCheckHitOrGuard(AnimationId.Guarding);
        PlayAnimation(HitResult.Guard);

        //画面端でも鳴るようにノックバックの判定より先に再生
        AudioManager.Instance.PlaySe(AudioManager.SeState.Guard);//Se再生

        //自分が壁側に居るかを確認 もしいたらノックバックさせない
        if (knockBackManager.CheckCanKnockBack(transform.position)) return;
        //ガードバック
        knockBackManager.ForwardMove(-dir, data.GuardKnockBackTime, data.GuardKnockBackDistance);
    }
    //ヒット時の処理
    public void ReceiveHit(HitInfo hitInfo)
    {
        var data = hitInfo.hitBoxData;
        currentAttackdata = hitInfo.hitBoxData;

        knockBackManager.StopForwardCoroutine();

        //nullなら相手のステータスをゲット
        if (targetState == null)
            targetState = hitInfo.targetManager.GetComponent<PlayerState>();
        
        //ダメージを与える処理
        state.TakeDamage(ApplyDamage(data,targetState),data.AddHitGauge);

        
        CheckAttackAttribute(hitInfo);
    }
    //投げられた時の処理
    public void ReceiveThrow(HitInfo hitInfo)
    {
        //投げられ状態にする
        state.AddStatusFlag(StatusFlags.IsThrowing);

        currentAttackdata = hitInfo.hitBoxData;

        //nullなら相手のステータスをゲット
        if (targetState == null)
            targetState = hitInfo.targetManager.GetComponent<PlayerState>();

    }

    private void CheckCancelState()//特定の状態か見る
    {
        //パワーアップの演出の最中ならキャンセルする
        if (state.HasStatusFlag(StatusFlags.IsPowerUpEffect))
        {
            //パワーアップ状態&パワーアップ演出中を解除
            state.RemoveStatusFlag(StatusFlags.IsPowerUp);
            state.RemoveStatusFlag(StatusFlags.IsPowerUpEffect);

            var data = animationController.GetCurrentAttackData();
            if (data == null) return;

            //登録しているアクションがあるか確認
            actionController.OnCheckAction(data);
            return;
        }
        return;
    }
    //受けた攻撃がどんな属性を持っているか
    private void CheckAttackAttribute(HitInfo hitInfo)
    {
        var data = hitInfo.hitBoxData;
        var attributeState = data.AtAttri;
        var dir = hitInfo.targetManager.transform.position - transform.position;//ベクトルを取得
        dir.y = 0;

        //パワーアップ状態だったらエフェクトを変える
        var effectState = targetState.currentState.HasFlag(StatusFlags.IsPowerUp)
          ? EffectType.PowerUp : EffectType.Hit;

        //エフェクトを再生
        int returnTime = 30;
        effectController.PlayEffect(effectState, hitInfo.hitPos, Quaternion.identity, returnTime);

        //ジャンプ状態じゃなければ
        if (state.currentTransitionState is not (PlayerTransitionState.Jump or PlayerTransitionState.Flying))
        {
            //特別な状態か確認
            CheckCancelState();
        }

        //プレイヤーが現在飛んでいるか、確認飛んでいたら属性を変更
        var resultState = CheckPlayerFlying(attributeState);

        //アクションが登録されているかを確認
        if(actionList.TryGetValue(resultState,out var action))
        {
            action?.Invoke(hitInfo,dir);
        }
        return;

    }
    private AttackAttribute CheckPlayerFlying(AttackAttribute attackState)
    {
        //特別の攻撃だったら属性を変更しない
        if (attackState is AttackAttribute.SmashAttack) 
            return attackState;

        //自分が飛ばされているもしくはジャンプをしていたら
        bool isFly = state.currentTransitionState is PlayerTransitionState.Flying or PlayerTransitionState.Jump;

        //ふっとび属性に変更
        return isFly ? AttackAttribute.LaunchAttack : attackState;
    }

    //アニメーションを再生してプレイヤーの行動ステートを変える
    private void PlayAnimation(HitResult hitResult)
    {
        playerController.SetHitOrGuardFlag(hitResult);
        playerController.ProcessHit();
    }
    //ダメージ計算
    //attackerState:攻撃した側の状態 ダメージカット率は攻撃を受けた自分(state)の値を使う
    private float ApplyDamage(ActionFrameData data,PlayerState attackerState)
    {
        float powerUpOffset = 1.5f;
        float normalOffset = 1;
        float damage = data.Damage;
        float minimumDamage = data.Damage * 0.1f;//最低保証ダメージは元のダメージの10/1

        //他のフラグが立っていても判定できるようにHasStatusFlagで確認
        bool isPowerUp = attackerState.HasStatusFlag(StatusFlags.IsPowerUp);

        damage = isPowerUp ? damage * powerUpOffset : damage * normalOffset;
        damage = OnDamage(damage, data.damageReductionPerHit);
        damage *= Mathf.Max(0f, 1f - state.DamageCutRate);//範囲を超えないようにするため補間

        // 最低ダメージ保証
        damage = Mathf.Max(damage,minimumDamage);

        return damage;
    }
    float OnDamage(float damage, float damageReductionPerHit)
    {
        //800damage * Pow(0.6^2....3...4)= 0.36 .. 0.216..0.1296
        //こういう漢字で補正を入れて計算をしていく
        float newDamage = Mathf.Pow(damageReductionPerHit,state.ReceiveHitCount -1);
        float finalDamage = damage * newDamage;
        return finalDamage;
    }


    //技の属性に合った処理をする
    private void ApplySmashAttack(HitInfo hitInfo,Vector3 dir)//特別な技（必殺技じゃない）
    {
        var data = hitInfo.hitBoxData;

        state.ChangeState(PlayerTransitionState.Flying);
        animationController.PlayLaunchAnimation(AnimationId.Launch);
        //アニメーションを再生
        PlayAnimation(HitResult.Hit);

        //ノックバック処理
        var knockInfo = data.knockInfos[0];
        knockBackManager.Launch(-dir, knockInfo.Power, knockInfo.Height, knockInfo.Duration);

        //Se再生
        AudioManager.Instance.PlaySe(AudioManager.SeState.Hit);
    }
    private void ApplyDownAttack(HitInfo hitInfo,Vector3 dir)//ダウン攻撃
    {
        state.ReceiveHitCount++;

        var data = hitInfo.hitBoxData;
        //アニメーションを再生
        animationController.PlayLaunchAnimation(AnimationId.Down);

        //ステートを変更
        playerController.HasInoperable();

        //Se再生
        AudioManager.Instance.PlaySe(AudioManager.SeState.Hit);
    }
    private void ApplyLaunchAttack(HitInfo hitInfo,Vector3 dir)//ふっとばし攻撃
    {
        state.ReceiveHitCount++;//攻撃を何かい当てたかのコンボカウント補正に使われる

        state.FlyComboCount++;//空中コンボカウント

        var data = hitInfo.hitBoxData;

        //アニメーションやステートを更新
        state.ChangeState(PlayerTransitionState.Flying);
        animationController.PlayLaunchAnimation(AnimationId.Launch);

        //アニメーションを再生
        PlayAnimation(HitResult.Hit);

        //ノックバックの処理をする
        var KnockInfo = data.knockInfos[0];
        float height = state.ReceiveHitCount > 0f ? KnockInfo.Height * 1.5f : KnockInfo.Height + 1;
        knockBackManager.Launch(-dir, KnockInfo.Power, height, KnockInfo.Duration);

        //Se再生
        AudioManager.Instance.PlaySe(AudioManager.SeState.Hit);
    }
    private void ApplyNormalAttack(HitInfo hitInfo,Vector3 dir)//通常攻撃の処理
    {

        state.ReceiveHitCount++;//攻撃を何回当てたかのコンボカウント補正に使われる

        var data = hitInfo.hitBoxData;
        state.ChangeState(PlayerTransitionState.Hitting);

        //アニメーションを再生
        animationController.PlayCheckHitOrGuard(AnimationId.Hitting);
        PlayAnimation(HitResult.Hit);

        //Se再生
        AudioManager.Instance.PlaySe(AudioManager.SeState.Hit);

        //自分が壁側に居るかを確認 もしいたらノックバックさせない
        if (knockBackManager.CheckCanKnockBack(transform.position)) return;
        //自分をバック
        knockBackManager.ForwardMove(-dir, data.HitKnockbackTime, data.HitKnockbackDistance);

    }

    //投げ成立
    private void ApplyThrow(HitInfo hitInfo)
    {
        //現在再生中のコライダー、アニメーションを停止
        animationController.EndAnime();
        attackFrameController.ResetColliderInfo();

        state.RemoveStatusFlag(StatusFlags.IsThrowing);
        //アニメーション再生
        state.ChangeState(PlayerTransitionState.Hitting);
        animationController.ThrowAnime(AnimationId.Thrown);

        //投げられフラグを直す
        colliderManager.SetThrowingFlag();
        animationController.SetThrowingFlag();

        colliderManager.ThrowActive(false);
        playerController.HasInoperable();

        knockBackManager.StopForwardCoroutine();
    }

    //投げ抜け成立
    private void ApplyThrowTech(HitInfo hitInfo)
    {
        //状態を解除
        state.RemoveStatusFlag(StatusFlags.IsThrowing);

        //相手の方向ベクトルを取得
        var dir = hitInfo.targetManager.transform.position - transform.position;
        var data = hitInfo.hitBoxData.knockInfos[0];

        //投げられフラグを次のティックで直す
        TickScheduler.RunNextTick(() =>
        {
            if (this != null) colliderManager.SetThrowingFlag();
        });
        animationController.SetThrowingFlag();

        //アニメーション再生
        state.ChangeState(PlayerTransitionState.Hitting);
        animationController.ThrowAnime(AnimationId.Grabbing);

        //ステートを変更
        playerController.HasInoperable();
        colliderManager.ThrowActive(false);

        //ノックバック
        knockBackManager.ForwardMove(-dir,data.Duration,data.Power);
    }



    ///-----------ここからはアニメーションイベントから呼ばれる



    public void ReceiveThrowDamage()//投げられている最中に呼ばれる
    {
        if (state.CurrentHp <= 0) return;

        var data = currentAttackdata;
        float correction = 0.6f;//カット率

        //Se再生
        AudioManager.Instance.PlaySe(AudioManager.SeState.Hit);

        //ダメージを反映
        float addGauge = data.AddGuardGauge * correction;
        state.TakeDamage(ApplyDamage(data, targetState), addGauge);
    }
}
