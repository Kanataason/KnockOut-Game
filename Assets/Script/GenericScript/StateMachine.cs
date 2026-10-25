using System.Collections.Generic;
using UnityEngine;

public class StateMachine<TOwner> 
{
    public abstract class State
    {

        protected StateMachine<TOwner> StateMachine => stateMachine;//こう書くことによって継承したところでも使うことができる
        internal StateMachine<TOwner> stateMachine;//stateMacineとStateでしか参照できない。

        internal Dictionary<int, State> state = new Dictionary<int, State>();
        internal Dictionary<State, int> reverseState = new Dictionary<State, int>();
        protected TOwner owner => stateMachine.owner;

        internal void Enter(State prevstate)    
        {
            OnEnter(prevstate);
        }
        protected virtual void OnEnter(State prevstate) { }

        internal void Update()
        {
            OnUpdate();
        }
        protected virtual void OnUpdate() { }

        internal void FixedUpdate()
        {
            OnFixedUpdate();
        }
        protected virtual void OnFixedUpdate() { }
        internal void Exit(State nextstate)
        {
            OnExit(nextstate);
        }
        protected virtual void OnExit(State nextstate) { }
    }
    public sealed class AnyState : State { }
    public TOwner owner { get; }
    public State CurrentState { get; private set; }
    public int NextState { get; private set; }
    /// <summary>
    /// 循環型リスト ぴんぽいんとの要素にアクセスする必要がないときだけ使う
    /// </summary>
    private LinkedList<State> stateslist = new LinkedList<State>();
    public StateMachine(TOwner My)//コンストラクタ
    {
        owner = My;
    }
    public T Add<T>() where T : State, new()
    {
        var state = new T();
        state.stateMachine = this;
        stateslist.AddLast(state);
        return state;
    }

    //遷移先を登録する
    public void AddTransition<TFrom, TTo>(int eventId) where TFrom : State, new() where TTo : State, new()
    {
        var from = GetOrAddState<TFrom>();//現在の状態が登録されているかの確認

        if (from.state.ContainsKey(eventId))
        {
            return;
        }
        var to = GetOrAddState<TTo>();//遷移先が登録されているか確認

        //現在の状態に遷移先を追加
        from.state.Add(eventId, to);
        from.reverseState.Add(to, eventId);
    }
    public void AddAnyTransition<TTo>(int eventId) where TTo : State, new()
    {
        AddTransition<AnyState, TTo>(eventId);
       
    }
    //状態を登録されているか確認してされてなかったら登録する関数
    private T GetOrAddState<T>() where T : State, new()
    {
        foreach (var state in stateslist)
        {
            if (state is T result)
            {
                return result;
            }
        }
        return Add<T>();
    }

    public void Dispatch(int eventId)
    {
        State to;
        //現在のステートで引数のidを登録していたらステート遷移
        if (!CurrentState.state.TryGetValue(eventId, out to))
        {
            //全体ステートで登録されているか確認登録されていたらステート遷移
            if (!GetOrAddState<AnyState>().state.TryGetValue(eventId, out to))
            {
                return;
            }
        }
        NextState = eventId;
        Change(to);
    }
    public void Start<Tfirst>() where Tfirst : State, new()
    {
        Starts(GetOrAddState<Tfirst>());
    }



    //ここでStart Update Exitなどの処理をしている



    public void Starts(State first)
    {
        CurrentState = first;
        CurrentState.Enter(null);
    }
    public void Update()
    {
        CurrentState.Update();
    }
    public void FixedUpdate()
    {
        CurrentState.FixedUpdate();
    }

    //状態を変えるときに前の状態を終わらせてから現在の状態を始める
    private void Change(State nextstate)
    {
        CurrentState.Exit(nextstate);
        nextstate.Enter(CurrentState);
        CurrentState = nextstate;
    }
}
