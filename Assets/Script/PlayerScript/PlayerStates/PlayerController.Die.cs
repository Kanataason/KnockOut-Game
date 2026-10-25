using System;
using System.Collections.Generic;
using UnityEngine;
using static AnimationManager;
using static UiUtility;
using State = StateMachine<PlayerController>.State;

public partial class PlayerController
{
    private class Die : State
    {
        ActionFrameData currentAnimationData;
        ActionFrameData NextAnimationData;

        bool isUsingAnimatorDeath;
        int animationTotalFrame;
        protected override void OnEnter(State prevstate)
        {
            owner.ReleaseHitStop();

            //同じティックで他の処理がアニメーションを設定し終わってから、次のティックでデータを取得
            owner.RunNextTick(this, () =>
            {
                currentAnimationData = owner.controller.GetCurrentAttackData();

                //データが無い時はアニメーターの死亡モーションを使う
                if (currentAnimationData == null)
                {
                    isUsingAnimatorDeath = true;
                    owner.controller.StopAnimation();
                    owner.controller.PlayDieAnimation();
                    owner.ChangePlayerActionState(PlayerAction.Die);
                    return;
                }

                //特定のアニメーションが再生されていたらDieアニメーションは再生しない
                if (!CanCurrentAnimation(currentAnimationData.ActionName))
                {
                    isUsingAnimatorDeath = true;
                    owner.controller.StopAnimation();
                    owner.controller.PlayDieAnimation();
                }
                InitFrame();

                owner.ChangePlayerActionState(PlayerAction.Die);
            });
        }
        private void InitFrame()
        {
            animationTotalFrame = currentAnimationData.frameData[^1].FrameNumber -2;
            if (owner.currentAnimationFrame >= animationTotalFrame) 
                owner.currentAnimationFrame = 1;

            owner.SetCanRotationFlag();
            owner.playerState.OffHpBarChange();
        }
        //特定のアニメーションが再生されていないか確認
        private bool CanCurrentAnimation(AnimationId id)
        {
            //空中やられなら強制的にtrueに
            if (owner.playerState.currentTransitionState is PlayerTransitionState.Flying)
            {
                NextAnimationData = owner.controller.GetAttackData(AnimationId.Launching);
                return true;
            }

            bool isPlaying = id switch
            {
                AnimationId.Down => true,
                AnimationId.Thrown =>true,
                _=>false
            };

            return isPlaying;
        }
        protected override void OnUpdate()
        {
            if (currentAnimationData == null||isUsingAnimatorDeath) return;
            //アニメーション更新
            owner.controller.PlayAnimation(owner.currentAnimationFrame);

            //再生が終了したら流す
            if (owner.currentAnimationFrame >= animationTotalFrame)
            {
                //次に再生するアニメーションがあれば情報をセット
                if (NextAnimationData != null)
                    SetAnimationInfo();
                
                return;
            }
            //フレーム更新
            owner.currentAnimationFrame++;
            base.OnUpdate();
        }
        private void SetAnimationInfo()//アニメーション再生に必要なフレームの初期化
        {
            //トータルフレーム初期化
            animationTotalFrame = NextAnimationData.frameData[^1].FrameNumber-2;

            //現在のフレームを初期化
            owner.currentAnimationFrame = 1;

            //アニメーション情報をセット
            owner.controller.PlayAnimation(NextAnimationData.ActionName);
            NextAnimationData = null;
        }
        protected override void OnExit(State nextstate)//バトル最初の状態に初期化
        {
            //フラグを戻す
            isUsingAnimatorDeath = false;
            
            //データを外す
            currentAnimationData = null;
            NextAnimationData = null;

            SetNextRoundInfo();

            base.OnExit(nextstate);
        }
        private void SetNextRoundInfo()//次の試合に行くときの初期化処理
        {
            owner.CheckSpecialPlayerState();                              // 特殊な状態か確認
            owner.SetHitStopFlag();                                       // ヒットストップを解除
            owner.CanMoveState();                                         // 行動可能な状態に戻す
            owner.SetHitOrGuardFlag(PlayerHitDirector.HitResult.Hit, false); // ヒット・ガード状態を解除
            owner.controller.EndAnime();                                 // アニメーションを終了
            owner.playerState.OnResetComboCount();                       // コンボ情報をリセット
            owner.colliderManager.ResetColliderInfo();                   // 当たり判定を初期化
            owner.commandList.Clear();                                    // 入力コマンドをクリア
            owner.simpleMovement.ResetJumpFlag(false);                   // ジャンプのフラグを直す
        }
    }
}
