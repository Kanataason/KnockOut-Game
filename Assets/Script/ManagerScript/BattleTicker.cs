using System.Collections.Generic;
using UnityEngine;

public class BattleTicker : MonoBehaviour
{
    //1秒間に処理する回数
    public const int TickRate = 60;

    //1ティックの時間
    public const float TickTime = 1f / TickRate;

    //処理落ちした時に1フレームでまとめて進める上限
    private const int MaxTicksPerFrame = 5;

    //フレームの時間がティックの整数倍にこれだけ近ければ、ちょうどその時間とみなす
    //60fpsでもフレームの時間は16.6ms前後でぶれるので、そのままだと
    //「進まないフレーム」と「2ティック進むフレーム」が混ざって動きがカクつく
    private const float SnapTolerance = 0.002f;

    private static BattleTicker instance;

    /// <summary>
    /// 起動してから進んだティック数
    /// </summary>
    public static int CurrentTick { get; private set; }

    private readonly List<ITickable> tickables = new();
    private readonly List<ITickable> tickBuffer = new();//処理中に登録・解除されても壊れないようにするためのコピー

    private float accumulator;

    /// <summary>
    /// 処理を登録 シーンにTickerが無ければ生成する
    /// </summary>
    public static void Register(ITickable tickable)
    {
        var ticker = GetOrCreate();

        if (!ticker.tickables.Contains(tickable))
            ticker.tickables.Add(tickable);
    }

    private static BattleTicker GetOrCreate()
    {
        if (instance == null)
        {
            instance = new GameObject(nameof(BattleTicker)).AddComponent<BattleTicker>();
        }
        return instance;
    }

    /// <summary>
    /// 登録を解除 シーンの破棄中に新しく生成しないようにTickerがある時だけ処理する
    /// </summary>
    public static void Unregister(ITickable tickable)
    {
        if (instance == null) return;

        instance.tickables.Remove(tickable);
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Update()
    {
        //deltaTimeを使うのでtimeScaleが0の時は進まない
        accumulator += SnapToTicks(Time.deltaTime);

        int tickCount = 0;
        while (accumulator >= TickTime && tickCount < MaxTicksPerFrame)
        {
            TickAll();

            accumulator -= TickTime;
            tickCount++;
        }

        //処理が追いつかない時は溜まった時間を捨てて、まとめて進みすぎないようにする
        if (tickCount == MaxTicksPerFrame)
            accumulator = 0;
    }

    /// <summary>
    /// フレームの時間が1ティック、2ティック…に近ければ、ちょうどその時間に丸める
    /// </summary>
    private static float SnapToTicks(float deltaTime)
    {
        for (int i = 1; i <= MaxTicksPerFrame; i++)
        {
            float tickTimes = TickTime * i;
            if (Mathf.Abs(deltaTime - tickTimes) < SnapTolerance)
                return tickTimes;
        }
        return deltaTime;
    }

    private void TickAll()
    {
        //TickOrderは登録した後に変わることがあるので、毎回順番を並べ直す
        tickBuffer.Clear();
        tickBuffer.AddRange(tickables);
        SortByTickOrder(tickBuffer);

        foreach (var tickable in tickBuffer)
        {
            //処理中に破棄されたものは呼ばない
            if (tickable is Object obj && obj == null) continue;

            tickable.Tick();
        }

        CurrentTick++;
    }

    /// <summary>
    /// 登録順を保ったままTickOrderの小さい順に並べる（要素数が少ないので挿入ソート）
    /// </summary>
    private static void SortByTickOrder(List<ITickable> list)
    {
        for (int i = 1; i < list.Count; i++)
        {
            var current = list[i];
            int j = i - 1;
            while (j >= 0 && list[j].TickOrder > current.TickOrder)
            {
                list[j + 1] = list[j];
                j--;
            }
            list[j + 1] = current;
        }
    }
}
