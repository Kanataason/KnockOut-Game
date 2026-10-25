using System;
using System.Collections.Generic;
using UnityEngine;
using static AnimationManager;
using static UiUtility;
using State = StateMachine<PlayerController>.State;

public partial class PlayerController
{
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
}
