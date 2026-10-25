using System;
using System.Collections.Generic;
using UnityEngine;
using static AnimationManager;
using static UiUtility;
using State = StateMachine<PlayerController>.State;

public partial class PlayerController
{
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
}
