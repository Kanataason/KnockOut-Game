using System;
using System.Collections.Generic;
using UnityEngine;
using static AnimationManager;
using static UiUtility;
using State = StateMachine<PlayerController>.State;

public partial class PlayerController
{
    private class Win : State
    {
        private const float WinDelay = 1f;

        private ActionFrameData currentAnimationData;
        private int winAnimationTotalFrame;

        private bool isPlayPreviousAnimation = false;

        protected override void OnEnter(State prevstate)
        {
            owner.ReleaseHitStop();

            owner.playerState.ChangeState(PlayerTransitionState.Win);

            currentAnimationData = owner.controller.GetCurrentAttackData();
            if (currentAnimationData != null)
            {
                isPlayPreviousAnimation = true;

                winAnimationTotalFrame =
                    currentAnimationData.frameData[^1].FrameNumber;

                return;
            }

            Delay.WaitFrame(owner, TickScheduler.SecondsToTicks(WinDelay), completeAction: ()=>
            {
                owner.controller.SetWinAnimation();
                SetPlayAnimation();
            });
           
        }
        private void SetPlayAnimation()
        {
            currentAnimationData = owner.controller.GetCurrentAttackData();

            winAnimationTotalFrame =
                currentAnimationData.frameData[^1].FrameNumber;

            owner.currentAnimationFrame = 1;
        }
        protected override void OnUpdate()
        {
            if (currentAnimationData == null) return;

            //特定のアニメーションまで再生したら止める
            if (owner.currentAnimationFrame >= winAnimationTotalFrame)
            {
                if (isPlayPreviousAnimation)
                {
                    ChangeAnimation();
                }
                return;
            }

            //アニメーション更新
            owner.controller.PlayAnimation(owner.currentAnimationFrame);

            //フレーム更新
            owner.currentAnimationFrame++;
        }
        private void ChangeAnimation()//アニメーションを勝利モーションに変える
        {
            owner.controller.SetWinAnimation();

            SetPlayAnimation();
            isPlayPreviousAnimation = false;
        }
        protected override void OnExit(State nextstate)//初期化処理
        {
            currentAnimationData = null;

            SetNextRoundInfo();

            base.OnExit(nextstate);
        }
        private void SetNextRoundInfo()//次の試合に行くときの初期化処理
        {
            owner.CheckSpecialPlayerState();// 特殊な状態か確認
            owner.SetHitStopFlag();// ヒットストップを解除
            owner.CanMoveState();// 行動可能な状態に戻す
            owner.controller.EndAnime();// アニメーションを終了
            owner.colliderManager.ResetColliderInfo();// 当たり判定を初期化
            owner.commandInput.ClearCommands(); // 入力コマンドをクリア
            owner.simpleMovement.ResetJumpFlag(false);// ジャンプのフラグを直す
        }
    }
}
