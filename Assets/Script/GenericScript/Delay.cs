using System;
using UnityEngine;

/// <summary>
/// 指定した時間止めることができる関数
/// </summary>
public static class Delay
{
    /// <summary>
    /// 指定したフレームの数待ってアクションを実行する
    /// フレームは描画のフレームではなくBattleTickerのティック数（1秒間に60回）で数える
    /// コルーチンは使わずTickSchedulerで数えるので、ティックが止まっている間は待つ時間も止まる
    /// </summary>
    /// <param name="owner"></param>
    /// <param name="frame"></param>
    /// <param name="func"></param>
    /// <param name="cancelAction">
    /// 条件が合ったら実行される処理
    /// </param>
    /// <param name="completeAction">
    /// 指定されたフレーム数流れたら実行される処理
    /// </param>
    public static void WaitFrame(MonoBehaviour owner,int frame,Func<bool> func =null,
        Action cancelAction = null,Action completeAction = null)
    {
        TickScheduler.RunAfter(frame, completeAction, owner, func, cancelAction);
    }
    /// <summary>
    /// 一定時間待つ
    /// UIの演出用 試合中の処理はティックで数えるWaitFrameを使う
    /// </summary>

    public static void WaitTime(MonoBehaviour owner,float time,Action action)
    {
        owner.StartCoroutine(StartWaitTime(time,action));
    }

    private static System.Collections.IEnumerator StartWaitTime(float time,Action action)
    {
        yield return new WaitForSeconds(time);

        action?.Invoke();
    }
}
