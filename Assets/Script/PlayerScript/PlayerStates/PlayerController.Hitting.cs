using System;
using System.Collections.Generic;
using UnityEngine;
using static AnimationManager;
using static UiUtility;
using State = StateMachine<PlayerController>.State;

public partial class PlayerController
{
    private class Hitting : State
    {

        private PlayerTransitionState currentState;
        private bool isGuarding;

        private ActionFrameData currentHitAttackData;//ヒットしたときの攻撃データ
        private ActionFrameData currentAnimationData;//再生中の攻撃データ

        private Queue<ActionFrameData> animationQueue;//他に再生するアニメーションを補間

        private int stiffnessFrame;
        private int currentTotalAnimationFrame;
        private int currentHitStopFrame;
        protected override void OnEnter(State prevstate)
        {
            //初期化
            Init();

            SetStunFrameTime();
            //イベント発火
            InvokeEventAction();
        }
        private void Init()
        {
            currentState = owner.playerState.currentTransitionState;

            //ガード状態か確認
            isGuarding = currentState is
                PlayerTransitionState.CrouchGuard or PlayerTransitionState.StandGuard;

            InitState();

            if (owner.GetHitStopFlag()) owner.controller.StopClipAnimation();

            //受けた攻撃の情報を取得
            currentHitAttackData = owner.hitDirector.GetAttackData();

            //やられアニメーションの情報を取得
            currentAnimationData = owner.controller.GetCurrentAttackData();


            owner.textmanager.SetAttackDataInfo(currentHitAttackData);
            SetupAnimationQueue();

            InitFrame();

        }
        private void InitState()
        {
            //行動不能状態にする
            owner.ChangePlayerActionState(PlayerAction.Inoperable);
            owner.ChangeCanNextActionFlag();
            owner.ChangeIsActionFlag(true);
            owner.SetCanRotationFlag();
        }
        private void InitFrame()
        {
            //やられアニメーションの全体フレームを取得
            currentTotalAnimationFrame = currentAnimationData.frameData[^1].FrameNumber - 2;
            //現在のフレームを初期化
            owner.currentAnimationFrame = 1;
            currentHitStopFrame = 1;
        }
        private void SetupAnimationQueue()//リストの初期化
        {
            animationQueue ??= new();

            animationQueue.Clear();

            //再生リストにアニメーションクリップが入っていたらキューに保存
            foreach (var animation in currentAnimationData.AnimationQueue)
            {
                animationQueue.Enqueue(animation);
            }
        }
        private void SetStunFrameTime()
        {
            int normalFrame = currentHitAttackData.HitStunTime;

            if (isGuarding) normalFrame = currentHitAttackData.BlockStunTime;

            stiffnessFrame = normalFrame;

        }
        private void InvokeEventAction()//イベントを発火させる
        {
            owner.OnActiveCollisionAction?.Invoke(AnimationId.Idle, false);
            owner.OnSetCanMoveFlagAction?.Invoke(false);
        }

        protected override void OnUpdate()
        {
            if (currentAnimationData == null) return;

            //ヒットストップフラグが経っていたら処理する
            if (owner.GetHitStopFlag())
            {
                UpdateHitStopFrame();
                return;
            }
            //でバック
            owner.textmanager.HitUpdateFrame(owner.currentAnimationFrame);
            //アニメーションを再生
            owner.controller.PlayAnimation(owner.currentAnimationFrame);

            //硬直
            UpdateStunTime();

            //フレームを更新
            owner.currentAnimationFrame++;

        }
        private void UpdateHitStopFrame()//ヒットストップの処理
        {
            int hitStopFrame = currentHitAttackData.HitStopFrame;

            if (isGuarding) hitStopFrame = currentHitAttackData.GuardStopFrame;

            if (currentHitStopFrame >= hitStopFrame)
            {
                if (owner.GetHitStopFlag()) owner.controller.StartClipAnimation();
                owner.SetHitStopFlag();
            }
            currentHitStopFrame++;
        }

        private void UpdateStunTime()
        {
            //動けるかどうかを確認
            if (CanMove())
            {
                //再生するアニメーションが合ったら再生
                if (animationQueue.Count <= 0) return;

                //終わりのフレームになったら０に戻してアニメーションを再生
                if (owner.currentAnimationFrame >= currentTotalAnimationFrame)
                {
                    SetAnimationInfo();//アニメーションを再セット
                }
                return;
            }
            //硬直が終わったら遷移前のステートに戻す
            if (owner.currentAnimationFrame >=  stiffnessFrame)
            {
                owner.currentAnimationFrame = 1;

                //やられフラグを初期化
                owner.SetHitOrGuardFlag(PlayerHitDirector.HitResult.Guard,false);
                owner.SetHitOrGuardFlag(PlayerHitDirector.HitResult.Hit, false);

                owner.CanMoveState();
                //行動開始
                stateMachine.Dispatch((int)Event.MoveEvent);
            }
        }
        private void SetAnimationInfo()
        {
            var anima = animationQueue.Dequeue();

            //アニメーションの情報をセット
            owner.controller.PlayAnimation(anima.ActionName);
            owner.currentAnimationFrame = 1;
            currentTotalAnimationFrame = anima.frameData[^1].FrameNumber - 2;
        }
        private bool CanMove()//ここに条件を書く
        {
            return owner.playerState.currentTransitionState is PlayerTransitionState.Flying;
        }
        protected override void OnExit(State nextstate)
        {
            currentAnimationData = null;

            //ヒットかダウンステートに遷移するならアニメーションの初期化はしない
            if (ShouldSkipAnimationReset(stateMachine.NextState))
                return;

            EndHitState();

            ResetComboCount();
        }
        private void EndHitState()
        {
            owner.controller.EndAnime();// アニメーションを終了
            owner.controller.StopAnimation();
            owner.colliderManager.ResetColliderInfo();// 当たり判定を初期化
            owner.playerState.OffHpBarChange();//Ui更新
        }
        //特殊なアニメーションかどうか確認
        private bool ShouldSkipAnimationReset(int nextState)
        {
            return nextState is
                (int)Event.IsHitEvent or
                (int)Event.InoperableEvent or
                (int)Event.DieEvent;
        }
        private void ResetComboCount()
        {
            owner.playerState.OnResetComboCount();
        }
    }
}
