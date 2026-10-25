using System;
using System.Collections.Generic;
using UnityEngine;
using static AnimationManager;
using static UiUtility;
using State = StateMachine<PlayerController>.State;

public partial class PlayerController
{
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
            prevState = owner.commandInput.CurrentAction;

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
                    owner.commandInput.Receive(PlayerInputHandler.InputState.Canceled, prevState);
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
                owner.commandInput.Receive(PlayerInputHandler.InputState.Canceled, prevState);
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
            owner.commandInput.RemoveCommand(prevState);
        }
    }
}
