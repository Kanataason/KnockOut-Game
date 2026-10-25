using System;
using System.Collections.Generic;
using UnityEngine;
using static AnimationManager;
using static UiUtility;
using State = StateMachine<PlayerController>.State;

public partial class PlayerController
{
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
            if(!owner.commandInput.IsPressed(PlayerInputHandler.ActionState.Crouching))
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
}
