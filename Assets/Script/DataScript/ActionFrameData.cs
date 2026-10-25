using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
[CreateAssetMenu(fileName = "AttackData",menuName = "Frames")]
public class ActionFrameData : ScriptableObject
{
  [Header("攻撃の種類")] public AttackType attackType;
    [Header("攻撃属性")] public AttackAttribute AtAttri = AttackAttribute.NormalAttack;
    [Header("AvaterMask")] public AnimationManager.AvatarState AvatarMask;
    [Header("攻撃の種類")] public AnimationManager.AnimationId ActionName;
    [Header("アニメーションクリップ")] public AnimationClip clip;
    [Header("メインのアニメーションを再生してから再生するアニメーション")] public List<ActionFrameData> AnimationQueue;
    [Header("ガードスタン時間")] public int BlockStunTime;
    [Header("ヒットスタン時間")] public int HitStunTime;
    [Header("攻撃ダメージ")] public float Damage;
    [Header("ゲージHit増加量")] public float AddHitGauge;
    [Header("ゲージGuard増加量")] public float AddGuardGauge;
    [Header("使う増加量")] public int UseGauge;
    [Header("ヒットふっとばし")] public float HitKnockbackDistance;
    [Header("ヒット時間")] public float HitKnockbackTime;
    [Header("ガードふっとばし")] public float GuardKnockBackDistance;
    [Header("ガード時間")] public float GuardKnockBackTime;
    [Header("ヒット時のヒットストップ時間")] public int HitStopFrame;
    [Header("ヒット時のガードストップ時間")] public int GuardStopFrame;
    [Header("継続ダメージ")] public float ContinuousDamage;
    [Header("攻撃補正")] public float damageReductionPerHit;//補正
    [Header("ノックバックInfo")] public List<KnockInfo> knockInfos;
    [Header("攻撃の全体フレームInfo")] public List<FrameData> frameData;
}

[Serializable]
public class FrameData
{
    public int FrameNumber;//処理するフレームの番号
    public AcceptInput InputState;//キャンセル可能かどうかのステート
    public ActionFlags flags;//どんな処理をするかのフラグステート

    public MoveFrameDataList MoveFrameData;//攻撃中に動く時のデータ
    public EffectFrameDataList EffectFrameData;//攻撃中に出すエフェクトのデータ

    public List<ColliderTypes> enableHitBoxes;   // 有効にするHitBox名
    public List<ColliderTypes> disableHitBoxes;  // 無効にするHitBox名

    public List<ColliderTypes> enableHurtBoxes;  // 有効にするHurtBox名
    public List<ColliderTypes> disableHurtBoxes; // 無効にするHurtBox名

    public List<ColliderTypes> enableThrowBoxes; // 有効にするThrowBox名
    public List<ColliderTypes> disableThrowBoxes;// 無効にするThrowBox名
}
[Flags]
public enum ActionFlags
{
    None = 0,
    Hit = 1 << 0,//当たり判定処理
    Throw = 1 << 1,//投げ処理
    Invincible = 1 << 2,//無敵化処理
    HitBoxActive = 1 << 3,//当たり判定復活処理
    PunishCounter = 1<<4,//パニッシュカウンター処理
}

[Serializable]
public enum AcceptInput
{
    Normal,
    Disable,
    SmashCancellable,
    AttackCancellable
}
[Serializable]
public enum AttackType
{
    UpperRow,
    LowerRow,
    MiddleRow,
    Normal
}
[Serializable] 
public enum AttackAttribute
{
    NormalAttack,
    SmashAttack,//ふっとばし
    DownAttack,//ダウン
    LaunchAttack,//打ち上げ攻撃
    Presentation//演出用ステート
}
[Serializable]
public class KnockInfo
{
    public float Height = 3.5f;//ふっとばしの距離
    public float Power = 3f;//ふっとばしの力
    public float Duration = 2f;//ふっとばしの距離
}

[Serializable]
public class MoveFrameDataList
{
    [FormerlySerializedAs("IsStartMoveing")] public bool IsStartMoving;//始まりのフレーム
    [FormerlySerializedAs("IsEndMoveing")] public bool IsEndMoving;//終わりのフレーム
    public bool IsBack;//うしろ向きに移動
    public int MoveFrame;//移動フレーム
    [FormerlySerializedAs("Movetorque")] public float MoveDistance = 3f;//移動量
    public AnimationCurve Curve;//動きの変化
}

[Serializable]
public class EffectFrameDataList
{
    [FormerlySerializedAs("IsSpownEffect")] public bool IsSpawnEffect;//いつスポーンするか
    [FormerlySerializedAs("SpownFrame")] public int SpawnFrame;//スポーン時間
    public EffectType Type;//どんなエフェクトタイプか
    public AnimationManager.EffectAnchor RootType;//発生場所のステート
    public Vector3 Offset;//場所の補正
    public int TotalLifeFrame;//生存時間
}