
public interface ITickable
{
    /// <summary>
    /// 処理する順番 小さい方から先に呼ばれる
    /// </summary>
    int TickOrder { get; }

    /// <summary>
    /// 1ティック分の処理
    /// </summary>
    void Tick();
}
