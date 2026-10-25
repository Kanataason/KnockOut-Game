
public enum TickPhase
{
    Status = -10,//パワーアップなどの状態の時間（プレイヤーの行動より前）
    Player = 0,//プレイヤーの行動
    KnockBack = 40,//ノックバックと吹っ飛び
    Movement = 50,//重力と向き
    Bullet = 60,//弾の移動と当たり判定
    Push = 100,//押し出し（両方のプレイヤーが動いた後に処理する）
    Battle = 200,//バトルの進行
}

/// <summary>
/// ITickable.TickOrderに渡す値を作る
/// </summary>
public static class BattleTickOrder
{
    /// <summary>
    /// 段階の中ではPlayer1、Player2の順に処理する 番号が決まる前は段階の先頭で処理する
    /// </summary>
    public static int Of(TickPhase phase, int playerId = PlayerIds.None)
    {
        return (int)phase + (playerId > PlayerIds.None ? playerId : 0);
    }
}
