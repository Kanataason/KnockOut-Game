using System;
using System.Collections.Generic;
using UnityEngine;
using static AnimationManager;
using static UiUtility;
using State = StateMachine<PlayerController>.State;

public partial class PlayerController
{
    private class Guard : State
    {
        PlayerTransitionState currentState;
        bool isCrouching;
        protected override void OnEnter(State prevstate)
        {
            isCrouching = owner.commandList.Contains(PlayerInputHandler.ActionState.Crouching);
            //しゃがんでいなかったらアニメーション再生終了
            if(!isCrouching)
                owner.controller.PlayNormalAnimation<bool>(AnimationId.Crouching,false);

            InvokeEventAction(owner.CheckGuard());
        }
        private void InvokeEventAction(PlayerTransitionState state)//イベントを発火
        {
            owner.OnSetCanMoveFlagAction?.Invoke(false);
            owner.playerState.ChangeState(state);
        }

        protected override void OnUpdate()
        {
            if (!isCrouching)
            {
                var moveInput = owner.playerInputHandler.MoveInput;
                owner.controller.PlayMoveAnimation(moveInput.x);
            }
            //ガードをしているかの確認
            CheckGuarding();
        }


        private void CheckGuarding()
        {
            // ガード維持条件 ガードボタン + 逆方向入力
            bool isStillGuarding = owner.PlayerCheckDirection();

            if (!isStillGuarding)
            {
                stateMachine.Dispatch((int)Event.MoveEvent);
                return;
            }
            if (isStillGuarding)
            {

                var IsChangeState = currentState != owner.CheckGuard();
                if(IsChangeState)
                {
                    var state = owner.CheckGuard();
                    InvokeEventAction(state);
                    currentState = state;
                }
            }


        }
        protected override void OnFixedUpdate()
        {
            if (!owner.IsAction&&!isCrouching)
                owner.simpleMovement.UpdateMoving(owner.playerInputHandler.MoveInput);
        }
        protected override void OnExit(State nextstate)
        {
            owner.OnSetCanMoveFlagAction?.Invoke(true);
        }
    }
}
