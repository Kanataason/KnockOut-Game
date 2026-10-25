using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using static AnimationManager;
/// <summary>
/// プレイヤー現在の状態
/// </summary>
public enum PlayerTransitionState
{
    Normal,
    BeingAttacked,
    CrouchGuard,
    StandGuard,
    Jump,
    Crouch,
    Hitting,
    Flying,
    PunishCounter,
    Counter,
    PlayerDown,
    Win,
}

/// <summary>
/// プレイヤーの特殊状態
/// </summary>
[Flags]
public enum StatusFlags
{
    None =0,
    IsPowerUp = 1<<0,//パワーアップ状態
    IsPowerUpEffect = 1<<1,//パワーアップの演出中   
    IsThrowing = 1<<2,//捕まれている状態

}
public class PlayerState : MonoBehaviour, ITickable
{
    //イベント
    public event Action<PlayerState> OnHpBarChange;//Hpの減りをUiに通知するアクション
    public event Action<PlayerState> OnOffHpBarChange;//Hpの白ゲージをけすことを通知するアクション
    public event Action<PlayerState,bool,float> OnGaugeChange;//現在のゲージをUiに通知するアクション
    public event Action<PlayerState, float> OnPowerUpTimeChanged;//パワーアップの残り秒数をUiに通知するアクション 0なら終了
    public event Action<AnimationId> OnStatusEnded;//プレイヤーの状態が終わった通知するアクション
    public event Action<PlayerState> OnPlayerDeadAction;//死んだことを通知するアクション


    //現在の状態や行動状態のステート
    [FormerlySerializedAs("currentTranstionState")] public PlayerTransitionState currentTransitionState = PlayerTransitionState.Normal;
    public StatusFlags currentState = StatusFlags.None;

    [Header("Player状態")]

    ///<summary>プレイヤー番号 0:Player1 1:Player2（PlayerIdsと同じ番号）</summary>
    [FormerlySerializedAs("PlayerNumber")] public int PlayerId = PlayerIds.None;

    public float MaxHp = 1;
    public float CurrentHp = 0;
    public bool IsDead = false;

    //プレイヤーのゲージ変数
    public float CurrentPlayerGauge;
    public float PlayerMaxGauge =30000;

    ///<summary>ダメージカット率 受けるダメージをこの割合だけ減らす</summary>
    [FormerlySerializedAs("Correction")] public float DamageCutRate = 0.2f;

    ///<summary>攻撃を受けた時のゲージ増加の係数</summary>
    public float GaugeGainRate = 0.2f;

    ///<summary>プレイヤーの速度</summary>
    public float PlayerSpeed = 3;

    //ヒット時の情報変数

    ///<summary>攻撃した側のコンボカウント</summary>
    public int ReceiveHitCount = 0;

    ///<summary>攻撃された側のコンボカウント</summary>
    public int ComboCount = 0;

    ///<summary>空中でのコンボカウント</summary>
    public int FlyComboCount = 0;

    //パワーアップの残りティック数 0なら時間を数えていない
    private int powerUpRemainingTicks = 0;


    private void OnEnable()
    {
        //パワーアップの残り時間を固定ティックで数える
        BattleTicker.Register(this);
    }
    //プレイヤーの行動より前に状態の時間を進める
    public int TickOrder => BattleTickOrder.Of(TickPhase.Status, PlayerId);

    public void Tick()
    {
        if (powerUpRemainingTicks <= 0) return;

        powerUpRemainingTicks--;
        OnPowerUpTimeChanged?.Invoke(this, (float)powerUpRemainingTicks / BattleTicker.TickRate);

        //時間が来たらパワーアップを終了する
        if (powerUpRemainingTicks <= 0)
            EndPowerUp();
    }

    private void Start()
    {
        //パラメーター初期化
        Init();
    }
    public void Init()//プレイヤー情報初期化処理
    {
        CurrentHp = MaxHp;
        CurrentPlayerGauge = 0;
        IsDead = false;

        if (powerUpRemainingTicks > 0)
            EndPowerUp();

        ChangeState(PlayerTransitionState.Normal);

        //Uiの初期化
        OnHpBarChange?.Invoke(this);
        OffHpBarChange();
        AddGauge(CurrentPlayerGauge);
    }
    private void OnDisable()
    {
        BattleTicker.Unregister(this);
    }
    /// <summary>
    /// ダメージを与える関数
    /// </summary>
    public void TakeDamage(float damage,float Gauge)
    {

        //体力が０以下にならないようにする
        CurrentHp = Mathf.Max(0, CurrentHp - damage);

        //攻撃を受けた時のゲージを係数をかけて追加する
        AddGauge(Gauge*GaugeGainRate);

        //Hpが減ったことをUiに反映
        OnHpBarChange?.Invoke(this);

        //体力が０かつステートが死んでいなかったら 死ぬ
        if (CurrentHp <= 0 && !IsDead)
            Die();
    }
    /// <summary>
    /// ガードできる攻撃か確認
    /// </summary>
    public bool CanGuard(AttackType attackType)
    {
        bool isGuard = false;

        switch (currentTransitionState)
        {
            //立ガードなら下段以外の攻撃はガード可能
            case PlayerTransitionState.StandGuard:

                if (attackType == AttackType.LowerRow) return false;

                isGuard = true; break;

            //しゃがみガードなら中段以外の攻撃はガード可能
            case PlayerTransitionState.CrouchGuard:

                if (attackType is AttackType.MiddleRow) return false;

                isGuard = true; break;
            default: break;
        }
        return isGuard;
    }
    public bool IsCheckHit()
    {
        return currentTransitionState is PlayerTransitionState.Hitting or PlayerTransitionState.Flying;
    }

    //引数で渡されたゲージが使えるか確認
    public bool CanUseGauge(int useGauge)
    {
        return useGauge <= CurrentPlayerGauge;
    }
    //ゲージを使用してUiに反映させる
    public void UseGauge(float Gauge)
    {
        CurrentPlayerGauge -= Gauge;

        //０〜マックスの値に収まるように補間
        CurrentPlayerGauge = Mathf.Clamp(CurrentPlayerGauge, 0, PlayerMaxGauge);

        //Uiに反映させる
        OnGaugeChange?.Invoke(this,false,CurrentPlayerGauge);
    }

    //引数で渡された数ゲージを足す
   public void AddGauge(float Gauge)
    {
        CurrentPlayerGauge += Gauge;

        //０〜マックスの値に収まるように補間
        CurrentPlayerGauge = Mathf.Clamp(CurrentPlayerGauge, 0, PlayerMaxGauge);

        //Uiに反映させる
        OnGaugeChange?.Invoke(this,true,CurrentPlayerGauge);
    }

    //パラメーターを設定
    public void SetMoveParameters(float damageCutRate,float playerSpeed)
    {
        DamageCutRate = damageCutRate;//ダメージカット率
        PlayerSpeed = playerSpeed;//プレイヤーの移動スピード
    }

    public void Die()//死んだときの処理をする
    {

        IsDead = true;
        //死んだことを通知する
        OnPlayerDeadAction?.Invoke(this);

    }

    //キャラクターごとにパラメーターを設定する
    public void SetPlayerInfo(CustomCharacterData data)
    {
        //最大Hpを設定
        MaxHp = data.Hp;
        CurrentHp = MaxHp;

        bool isTankType = data.AttributeType is CustomCharacterData.PlayerType.Tank;

        bool isPowerType = data.AttributeType is CustomCharacterData.PlayerType.Power;

        //タイプに合った値を設定
        PlayerSpeed = isTankType ? 1.5f : 2;

        DamageCutRate = isPowerType ? 0.1f : 0.2f;
        GaugeGainRate = isPowerType ? 0.1f : 0.2f;//今までと同じ値を使う

    }

    /// <summary>
    /// UiにHpの減りを反映させる
    /// </summary>
    public void OffHpBarChange() => OnOffHpBarChange?.Invoke(this);

    //hadakaの必殺技の状態処理
    //isStartPowerUpがtrueなら指定した秒数のタイマーを開始（数えている途中なら上書き） falseならすぐに終了
    public void OnStartPowerUp(bool isStartPowerUp = false,float PowerUptime = 0.1f)
    {
        //パワーアップしているか確認
        if (isStartPowerUp)
        {
            //時間はUiではなくこのスクリプトがティックで数える
            powerUpRemainingTicks = Mathf.Max(1, Mathf.RoundToInt(PowerUptime * BattleTicker.TickRate));
            OnPowerUpTimeChanged?.Invoke(this, PowerUptime);
            return;
        }

        EndPowerUp();
    }
    private void EndPowerUp()//パワーアップを終了する
    {
        powerUpRemainingTicks = 0;
        OnPowerUpTimeChanged?.Invoke(this, 0f);

        //状態を解除
        RemoveStatusFlag(StatusFlags.IsPowerUp);
        RemoveStatusFlag(StatusFlags.IsPowerUpEffect);

        //終わったことを通知してリセット処理をする
        OnStatusEnded?.Invoke(AnimationId.Smash);
    }
    public void OnResetComboCount()
    {
        ComboCount = 0;
        ReceiveHitCount = 0;
        FlyComboCount = 0;
    }

    ///---------------ここからステートを変更する関数--------------

    public void ChangeState(PlayerTransitionState state)//プレイヤーの行動状態を変更
    {
        if (currentTransitionState == state) return; // 同じなら何もしない

        currentTransitionState = state;//ステートを入れ替える
    }
    public void AddStatusFlag(StatusFlags flag)//状態を追加
    {
        currentState |= flag;
    }
    public void RemoveStatusFlag(StatusFlags flag)//状態を解除
    {
        currentState &= ~flag;
    }
    public bool HasStatusFlag(StatusFlags flag)//状態を確認
    {
        return currentState.HasFlag(flag);
    }
}