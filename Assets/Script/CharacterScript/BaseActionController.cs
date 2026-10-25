using UnityEngine;
using System.Collections.Generic;
using System;
public abstract class BaseActionController : MonoBehaviour
{
    /// <summary>
    /// キャラ特有の技をここに書く
    /// </summary>
    public enum SpecialActionState
    {
        None,
        Smash,
    }
    protected Dictionary<AnimationManager.AnimationId, Action<ActionFrameData>> actionDictionary;

    protected PlayerController controller;
    protected PlayerState state;
    protected PlayerEffectController effectController;
    protected AnimationManager animationController;

    public event Action<AnimationManager.AnimationId, bool> OnActiveCollisionAction;//当たり判定のアクション
    /// <summary>
    /// 参照をセット
    /// </summary>
    protected virtual void Initialize(PlayerController controller,
        PlayerState state, PlayerEffectController effectController,
       AnimationManager animationController)
    {
        this.controller = controller;
        this.state = state;
        this.animationController = animationController;
        this.effectController = effectController;

        InitList();
    }
    protected void InvokeThrowSuccess(AnimationManager.AnimationId collName,bool isActive)
    {
        OnActiveCollisionAction?.Invoke(collName,isActive);
    }

    protected virtual void InitList()
    {
        actionDictionary = new();
    }
    /// <summary>
    /// 指定したステートにアクションが登録されているか確認する関数
    /// </summary>
    /// <param name="data"></param>
    public virtual void OnCheckAction(ActionFrameData data) { }
    /// <summary>
    /// 攻撃初めの処理を書く関数
    /// </summary>
    /// <param name="data"></param>
    public virtual void OnAttackStart(ActionFrameData data) { }
    /// <summary>
    /// 攻撃終わりの処理を書く関数
    /// </summary>
    /// <param name="data"></param>
    public virtual void OnAttackEnd() { }

    //ここにキャラ特有の物が増えたら書く
    protected virtual void OnSmashAttack(ActionFrameData data) { }
}
