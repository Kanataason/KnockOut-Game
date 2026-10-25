/// <summary>
/// プレイヤーの番号をまとめたクラス 番号は全体で0始まりに統一する（0:Player1 1:Player2）
/// </summary>
public static class PlayerIds
{
    public const int None = -1;//未割り当て
    public const int Player1 = 0;
    public const int Player2 = 1;

    //画面に表示する番号（1P,2P）に変換する
    public static int ToDisplayNumber(int playerId) => playerId + 1;
}
