using System;
using System.Collections.Generic;
using UnityEngine;
using static AnimationManager;
using static UiUtility;
using State = StateMachine<PlayerController>.State;

public partial class PlayerController
{
    //行動をステートにしてステートにして管理することで視認性があがりデバックもやりやすい
    private class MoveAndIdle : State
    {
        private float _prevDir;//前フレームのプレイヤーが向いている方向

        protected override void OnEnter(State prevstate)
        {
            owner.SetCanRotationFlag(true);
           _prevDir = 0;

            InitState();
          
            InvokeEventAction();
        }
        private void InitState()
        {
            if (owner.IsCanNextAction)
                owner.ChangePlayerActionState(PlayerAction.Idle);

            owner.currentActionState = PlayerInputHandler.ActionState.None;
        }
        private void InvokeEventAction()//イベントを発火
        {
            owner.playerState.ChangeState(PlayerTransitionState.Normal);//これを登録する
            owner.OnActiveCollisionAction?.Invoke(AnimationId.Idle, true);
            owner.OnSetCanMoveFlagAction?.Invoke(false);
        }
        protected override void OnUpdate()
        {
            var Input = owner.playerInputHandler.MoveInput;
            if (owner.PlayerActionState == PlayerAction.Inoperable|| BattleStateManager.Instance == null) return;

            //ゲームが始まっていたらアニメーション開始
           if(BattleStateManager.Instance.CurrentBattleState is not BattleStateManager.BattleState.GameStating)
                owner.controller.PlayMoveAnimation(Input.x);

           //コマンドが押されていたか確認
            if (CheckBufferCommand()) return;

            //ステップできるか確認
            if (TryStep(Input)) return;

            //ガードしているか確認
            if (TryGuard()) return;

        }
        private bool CheckBufferCommand()
        {
            if (owner.IsAction) return true;
            foreach (var state in owner.commandList)
            {
                owner.CheckAction(PlayerInputHandler.InputState.Performed, state);
                return true;
            }
            return false;
        }
        //ステップするかの確認
        private bool TryStep(Vector2 moveInput)
        {
            //ステップできない状態か、しゃがんでいたらステップしない
            if (owner.simpleMovement.IsStepping()||owner.playerInputHandler.IsCrouching) return false;

            bool IsAction = false;
            int dir = Mathf.RoundToInt(moveInput.x);
            if (_prevDir == 0 && dir != 0)
            {
                //行動不可能じゃなかったらステップ
                if (owner.PlayerActionState is not PlayerAction.Inoperable)
                {
                    Vector3 playerPos = owner.transform.position;
                    bool IsBack = owner.PlayerCheckDirection();
                    int waitTime = 9;

                    if (owner.simpleMovement.CheckCanStep(dir,playerPos,IsBack,waitTime))
                    {
                        owner.stateMachine.Dispatch((int)Event.StepEvent);
                        IsAction = true;
                    }
                }
            }
            _prevDir = dir;
            return IsAction;
        }
        //ガードするかの確認
        private bool TryGuard()
        {
            //立ガードがしゃがみガードを見ている
            var currentGuard = owner.CheckGuard();
            bool isGuard = currentGuard == PlayerTransitionState.CrouchGuard ||
                           currentGuard == PlayerTransitionState.StandGuard;


            if (isGuard&&owner.PlayerActionState != PlayerAction.Attack)
            {
                stateMachine.Dispatch((int)Event.GuardEvent);
                return true;
            }
            return false;
        }
        protected override void OnFixedUpdate()
        {
            if (owner.PlayerActionState == PlayerAction.Inoperable) return;

            if (!owner.IsAction)
            owner.simpleMovement.UpdateMoving(owner.playerInputHandler.MoveInput);
        }
        protected override void OnExit(State nextstate)
        {
            RemoveDirection();
            //動きをのフラグを止める
            owner.OnSetCanMoveFlagAction?.Invoke(true);
        }
        private void RemoveDirection()
        {
            //後ろ方向の入力以外なら消す
            if (stateMachine.NextState is not (int)Event.GuardEvent)
                owner.simpleMovement.InitDirection();
        }
    }
}
