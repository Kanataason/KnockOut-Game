using System.Collections.Generic;

/// <summary>
/// 入力の判断に必要な情報と、判断した結果を実行する側
/// </summary>
public interface IPlayerCommandReceiver
{
    //入力を受け付けられるか（ポーズ中や試合中以外は受け付けない）
    bool CanAcceptInput { get; }

    //行動中で次の行動ができないか
    bool IsBusy { get; }

    //技のキャンセルの状態
    AcceptInput CancelState { get; }

    //相手がやられ中かガード中か
    bool IsTargetHitOrGuard { get; }

    //投げられている途中か
    bool IsBeingThrown { get; }

    //行動を開始する
    void BeginAction(PlayerController.ActionsInfo action);

    //ニュートラルで保留していた行動を開始する
    void BeginNeutralAction(PlayerController.ActionsInfo action);

    //行動を終えて待機に戻る
    void EndAction();
}

/// <summary>
/// プレイヤーの入力を受け取って、その行動が今できるかを判断するクラス
/// 押されているボタンの記録、技のキャンセル、ニュートラル（反対の入力同士の解決）を担当する
/// </summary>
public class PlayerCommandInput
{
    private readonly IPlayerCommandReceiver receiver;

    //入力に紐づいている行動
    private readonly Dictionary<PlayerInputHandler.ActionState, PlayerController.ActionsInfo> actionList;

    //同時に押されたらニュートラルにする入力の組み合わせ
    private readonly Dictionary<PlayerInputHandler.ActionState, PlayerInputHandler.ActionState> neutralList;

    private readonly Queue<PlayerInputHandler.ActionState> prevCommandList = new();//ニュートラルで保留した入力
    private readonly List<PlayerInputHandler.ActionState> commandList = new();//押されている入力

    /// <summary>
    /// 押されている入力
    /// </summary>
    public IReadOnlyList<PlayerInputHandler.ActionState> PressedCommands => commandList;

    /// <summary>
    /// 現在実行している行動の入力
    /// </summary>
    public PlayerInputHandler.ActionState CurrentAction { get; private set; } = PlayerInputHandler.ActionState.None;

    public PlayerCommandInput(IPlayerCommandReceiver receiver,
        Dictionary<PlayerInputHandler.ActionState, PlayerController.ActionsInfo> actionList,
        Dictionary<PlayerInputHandler.ActionState, PlayerInputHandler.ActionState> neutralList)
    {
        this.receiver = receiver;
        this.actionList = actionList;
        this.neutralList = neutralList;
    }

    public bool IsPressed(PlayerInputHandler.ActionState state) => commandList.Contains(state);

    public void RemoveCommand(PlayerInputHandler.ActionState state) => commandList.Remove(state);

    public void ClearCommands() => commandList.Clear();

    public void ClearCurrentAction() => CurrentAction = PlayerInputHandler.ActionState.None;

    /// <summary>
    /// プレイヤーが入力した動作に紐づいているアクション実行やステートに変更する関数
    /// </summary>
    public void Receive(PlayerInputHandler.InputState inputState,
        PlayerInputHandler.ActionState actionState = PlayerInputHandler.ActionState.None)
    {
        //ポーズ中や試合中以外は何もしない
        if (!receiver.CanAcceptInput) return;

        //ボタンを離したか確認
        bool IsCancel = inputState == PlayerInputHandler.InputState.Canceled;

        //ボタンを押した瞬間か確認
        bool IsPerform = inputState == PlayerInputHandler.InputState.Performed;


        if (IsCancel)
        {
            if(commandList.Contains(actionState))
            {
                RemoveCommand(actionState);
            }
        }
        if (IsPerform)
        {
            if (!commandList.Contains(actionState))
            {
                commandList.Add(actionState);
            }

        }


        //アクションが登録されているかを確認
        if (!actionList.TryGetValue(actionState, out var currentAction))
        {
            return;
        }

        if (IsCommandBlocked(inputState, currentAction, actionState)) return;

        if (IsCancel)
        {

            RemoveCommand(actionState);

            if (CanNeutralPlay()) return;

            CurrentAction = PlayerInputHandler.ActionState.None;

            receiver.EndAction();
            return;
        }
        if (IsPerform)
        {
            //キャンセル可能の時ならアクション中でも行動可能

            //ニュートラルの行動かを見る
            if (CheckNeutral(CurrentAction, actionState)) return;

            //現在のアクションステータスを設定
            CurrentAction = actionState;

            receiver.BeginAction(currentAction);

        }

    }
    private bool IsCommandBlocked(PlayerInputHandler.InputState inputState,
      PlayerController.ActionsInfo currentAction, PlayerInputHandler.ActionState actionState = PlayerInputHandler.ActionState.None)
    {
        if (CheckCancelAttack(inputState,actionState)) return false;
        //アタックキャンセル可能期間中なら行動可能にする

        //行動禁止の条件があったらみる
        if (currentAction.IsBlocked != null && currentAction.IsBlocked())
        {
            return true;
        }
        //捕まれていたら何もできないようにする
        if (receiver.IsBeingThrown
            && actionState is not PlayerInputHandler.ActionState.Grabbing)
        {
            return true;
        }

        //アクション中で行動可能かどうかみる可能じゃなければ返す
        if (receiver.IsBusy)
        {
            return true;
        }

        //キャンセルできる状態じゃなかったら返す
        if (receiver.CancelState == AcceptInput.Disable)
        {
            return true;
        }return false;
    }
    private bool CheckCancelAttack(PlayerInputHandler.InputState inputState,PlayerInputHandler.ActionState actionState)
    {
        //アタックキャンセル可能期間中なら行動可能にする

        bool canCancelAttack = receiver.CancelState is AcceptInput.AttackCancellable or AcceptInput.SmashCancellable;

        bool inputCancel = inputState is PlayerInputHandler.InputState.Canceled;

        bool isTargetHit = receiver.IsTargetHitOrGuard;

        bool isSameAction = CurrentAction == actionState;

        // キャンセル可能期間中で、ボタンを押しており、相手にヒット中、かつ同じ技ではない
        return canCancelAttack && !inputCancel && isTargetHit &&!isSameAction;
    }

    private bool CanNeutralPlay()//これはCPT方式のための関数
    {
        if (prevCommandList.Count > 0)//ニュートラル時の物があったら再生
        {
            var eventId = prevCommandList.Dequeue();
            bool canNeutral = false;

            foreach (var state in commandList)
            {
                if (state == eventId)
                    canNeutral = true;
            }
            if (!canNeutral) return false;

            if (!actionList.TryGetValue(eventId, out var actions)) return false;

            CurrentAction = eventId;
            receiver.BeginNeutralAction(actions);
            return true;
        }
        return false;
    }

    //リストに入っているステート同士ならニュートラルにする
    private bool CheckNeutral(PlayerInputHandler.ActionState currentState, PlayerInputHandler.ActionState prevState)
    {
        if(neutralList.TryGetValue(currentState,out var resultState))
        {
            if(resultState == prevState)
            {
                if(prevCommandList.Count == 0)
                prevCommandList.Enqueue(currentState);//今のステートを入れる

                Receive(PlayerInputHandler.InputState.Canceled, currentState);
                CurrentAction = PlayerInputHandler.ActionState.None;
                return true;
            }
        }
        return false;
    }
}
