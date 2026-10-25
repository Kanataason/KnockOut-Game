using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 指定したティック数だけ待ってから処理を実行する
/// 試合中の「待つ」はコルーチンではなくここでティックを数える
/// BattleTickerに登録して毎ティックの最初に数えるので、ティックが止まっている間は待つ時間も止まる
/// </summary>
public static class TickScheduler
{
    private class Entry
    {
        public int RemainingTicks;
        public Action OnComplete;
        public Func<bool> CancelWhen;
        public Action OnCancel;
        public MonoBehaviour Owner;
        public bool HasOwner;
    }

    //BattleTickerに登録するための本体 どの処理よりも先に呼ばれるようにする
    private class Runner : ITickable
    {
        public int TickOrder => int.MinValue;
        public void Tick() => Advance();
    }

    private static readonly Runner runner = new();

    private static List<Entry> entries = new();
    private static List<Entry> running = new();

    /// <summary>
    /// 次のティックの最初に実行する
    /// </summary>
    public static void RunNextTick(Action action, MonoBehaviour owner = null)
    {
        RunAfter(1, action, owner);
    }

    /// <summary>
    /// ticks回ティックが進んだら実行する
    /// cancelWhenがtrueになったら待つのをやめてonCancelを実行する
    /// ownerが破棄されるか非アクティブになったら何もせずに捨てる（コルーチンが止まるのと同じ）
    /// </summary>
    public static void RunAfter(int ticks, Action onComplete, MonoBehaviour owner = null,
        Func<bool> cancelWhen = null, Action onCancel = null)
    {
        //待つティックが無ければすぐ実行する
        if (ticks <= 0)
        {
            onComplete?.Invoke();
            return;
        }

        //シーンが変わってTickerが作り直されても数えられるように毎回登録する（登録済みなら何もしない）
        BattleTicker.Register(runner);

        entries.Add(new Entry
        {
            RemainingTicks = ticks,
            OnComplete = onComplete,
            CancelWhen = cancelWhen,
            OnCancel = onCancel,
            Owner = owner,
            HasOwner = owner != null,
        });
    }

    /// <summary>
    /// 秒をティック数に変換する
    /// </summary>
    public static int SecondsToTicks(float seconds)
    {
        return Mathf.Max(1, Mathf.RoundToInt(seconds * BattleTicker.TickRate));
    }

    private static void Advance()
    {
        //実行中に予約されたものは次のティックから数えるためリストを入れ替える
        (running, entries) = (entries, running);

        foreach (var entry in running)
        {
            if (entry.HasOwner && (entry.Owner == null || !entry.Owner.gameObject.activeInHierarchy))
                continue;

            //1つが失敗しても他の処理とティックが止まらないようにする
            try
            {
                if (entry.CancelWhen != null && entry.CancelWhen())
                {
                    entry.OnCancel?.Invoke();
                    continue;
                }

                entry.RemainingTicks--;
                if (entry.RemainingTicks > 0)
                {
                    entries.Add(entry);
                    continue;
                }

                entry.OnComplete?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
        running.Clear();
    }

    //ドメインのリロードを切っていても前回のプレイの予約が残らないようにする
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        entries.Clear();
        running.Clear();
    }
}
