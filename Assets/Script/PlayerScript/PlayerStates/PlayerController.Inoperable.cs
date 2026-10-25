using System;
using System.Collections.Generic;
using UnityEngine;
using static AnimationManager;
using static UiUtility;
using State = StateMachine<PlayerController>.State;

public partial class PlayerController
{
    private class Inoperable:State
    {
        private ActionFrameData currentAnimationData;
        private Queue<ActionFrameData> animationQueue;

        private PlayerInputHandler.ActionState prevAction;//遷移前の行動
        private int animationTotalFrame;
        protected override void OnEnter(State prevstate)
        {
            prevAction = owner.commandInput.CurrentAction;
            //当たり判定のリストをクリアにする
            ClearCollider();

            //動けないようにする
            CantMove();

            //同じティックで他の処理がアニメーションを設定し終わってから、次のティックでデータを取得
            owner.RunNextTick(this, () =>
            {
                currentAnimationData = owner.controller.GetCurrentAttackData();

                //データがなかった時ずっとこのステートになってしまうから無かったらIdleにする
                if (currentAnimationData == null)
                {
                    stateMachine.Dispatch((int)Event.MoveEvent);
                    return;
                }
                
                owner.colliderManager.InitColliderInfo(currentAnimationData);

                InitFrame();
              
                SetupAnimationQueue();

            });
            InvokeEventAction();

        }
        private void InitFrame()
        {
            animationTotalFrame = currentAnimationData.frameData[^1].FrameNumber;
            owner.currentAnimationFrame = 1;
        }
        private void ClearCollider()//当たり判定をクリアにする
        {
            owner.colliderManager.StopCollider();
            owner.colliderManager.ClearHurtBox();
        }
        private void CantMove()
        {
            owner.ChangeIsActionFlag(true);
            owner.ChangeCanNextActionFlag();
        }
        private void InvokeEventAction()//イベントを発火
        {
            owner.OnActiveCollisionAction?.Invoke(AnimationId.None, false);//当たり判定をなくす
            owner.playerState.ChangeState(PlayerTransitionState.PlayerDown);//これを登録する
            owner.ChangePlayerActionState(PlayerAction.Inoperable);
        }
        private void SetupAnimationQueue()//リストの初期化
        {
            animationQueue ??= new();

            animationQueue.Clear();
            //再生リストにアニメーションクリップが入っていたらキューに保存
            foreach (var anima in currentAnimationData.AnimationQueue)
            {
                animationQueue.Enqueue(anima);
            }
        }

        protected override void OnUpdate()
        {
            if (currentAnimationData == null||owner.playerState.IsDead) return;

            //フレーム情報更新 
            owner.colliderManager.UpdateColliders(currentAnimationData, owner.currentAnimationFrame);

            //アニメーションを更新
            owner.controller.PlayAnimation(owner.currentAnimationFrame);

            CheckEndAnimation();

            //フレームを更新
            owner.currentAnimationFrame++;
        }
        private void CheckEndAnimation()
        {
            //アニメーションが再生終了したらステートを変更
            if (owner.currentAnimationFrame > animationTotalFrame)
            {
                if (animationQueue.Count > 0)
                {
                    PlayNextAnimation();

                    return;
                }
                //ステートを変更
                owner.CanMoveState();
                stateMachine.Dispatch((int)Event.MoveEvent);

            }
        }
        //アニメーション再生に必要な情報をセット
        private void PlayNextAnimation()
        {
            var anima = animationQueue.Dequeue();

            //アニメーションの情報をセット
            animationTotalFrame = anima.frameData[^1].FrameNumber;
            owner.controller.PlayAnimation(anima.ActionName);
            owner.currentAnimationFrame = 1;
        }
        protected override void OnExit(State nextstate)
        {
            currentAnimationData = null;

            //ステートをクリアにする
            ResetHitState();

            ResetComboCount();
            owner.commandInput.RemoveCommand(prevAction);
            owner.playerState.OffHpBarChange();//Uiに反映

            //特定のアニメーションだったらアニメーションを初期化しない
            if (stateMachine.NextState is (int)Event.DieEvent) return;
            owner.controller.EndAnime();
        }
        private void ResetHitState()//状態をキレイにする
        {
            owner.SetHitOrGuardFlag(PlayerHitDirector.HitResult.Hit, false);//フラグを直す

            owner.simpleMovement.ResetJumpFlag(false);

            //投げられている状態がまだだったら消す
            if (owner.playerState.currentState.HasFlag(StatusFlags.IsThrowing))
            {
                owner.playerState.RemoveStatusFlag(StatusFlags.IsThrowing);
            }

        }
        private void ResetComboCount()//コンボカウントをリセット
        {
            owner.playerState.OnResetComboCount();
        }
    }
}
