using System;
using UnityEngine;

public class PlayerKnockBackController : MonoBehaviour, ITickable
{
    const float GROUND_Y = -4.8f;

    public float gravity = -9.8f;     // 重力（マイナス値）

    private Camera cam;
    private PlayerState playerState;

    //場所を入れる変数
    private Vector3 moveStartPos;
    private Vector3 moveEndPos;

    //地面についたことを通知するためのイベント
    public event Action OnLanded;

    private int currentMoveFrame = 0;

    //吹っ飛びの情報 BattleTickerから1ティックごとに進める
    private bool isLaunching = false;
    private Vector3 launchPos;
    private Vector3 launchHorizontalVelocity;
    private float launchVerticalVelocity;
    private float launchElapsed;
    private float launchDuration;

    //ノックバックの情報 BattleTickerから1ティックごとに進める
    private bool isKnockBack = false;
    private Vector3 knockBackStartPos;
    private Vector3 knockBackEndPos;
    private float knockBackElapsed;
    private float knockBackDuration;

    private void Start()
    {
         cam = Camera.main;
         playerState = GetComponent<PlayerState>();
    }
    private void OnEnable()
    {
        BattleTicker.Register(this);
    }
    private void OnDisable()
    {
        BattleTicker.Unregister(this);
    }

    //両方のプレイヤーの行動の後に動かす
    public int TickOrder => BattleTickOrder.Of(TickPhase.KnockBack, playerState != null ? playerState.PlayerId : PlayerIds.None);

    public void Tick()
    {
        if (isLaunching) TickLaunch();
        if (isKnockBack) TickKnockBack();
    }


    /// <summary>
    /// 引数のキャラクターが自分が画面はしかを見ている
    /// </summary>
    /// <param name="character"></param>
    /// <returns></returns>
    public bool CheckCanKnockBack(Vector3 character)
    {
        float margin = 0.5f;
        //カメラの奥行きを取得
        float zDist = character.z - cam.transform.position.z;

        (float left, float right) = GetCamerawidth(zDist, margin);

        float charX = character.x;

        bool isScreenEdge = false;

        //キャラが画面はしかどうか確認
        if (charX <= left)
        {
            isScreenEdge = true;
        }
        else if (charX >= right)
        {
            isScreenEdge = true;
        }

        return isScreenEdge;

    }
    public bool IsGoingOutOfBounds(float margin =1f)
    {
        //カメラの奥行きを取得
        float zDist = transform.position.z - cam.transform.position.z;

        (float left,float right) = GetCamerawidth(zDist, margin);

        //キャラがどっちを向いている)か確認
        float dot = Vector3.Dot(
            transform.forward.normalized,
            cam.transform.right.normalized);

        //左端に近くて左を向ていたらtrue
        if (transform.position.x <= left && dot < 0)
        {
            return true;
        }

        //右端に近くて右を向いていたらtrue
        if (transform.position.x >= right && dot > 0)
        {
            return true;
        }
           


        return false;
    }
    private (float left, float right) GetCamerawidth(float zDist,float margin)
    {
        //画面端の座標を取得
        Vector3 bottomLeft = cam.ViewportToWorldPoint(new Vector3(0, 0, zDist));
        Vector3 topRight = cam.ViewportToWorldPoint(new Vector3(1, 1, zDist));

        //マージン分端からの距離を開ける
        float left = bottomLeft.x + margin;
        float right = topRight.x - margin;

        return (left, right);
    }
    public void Launch(Vector3 direction, float power,float Height,float Duration)
    {
        direction.Normalize();//単位ベクトルにする

        //再生中の吹っ飛びがあれば上書きする
        launchPos = transform.position;

        //縦方向のベクトルは他の変数で管理するから０に設定
        launchHorizontalVelocity = new Vector3(direction.x, 0f, direction.z) * (power / Duration);
        launchVerticalVelocity = Height;

        launchElapsed = 0f;
        launchDuration = Duration;

        //時間が0なら飛ばずに地面に戻す
        if (launchDuration <= 0f)
        {
            EndLaunch();
            return;
        }
        isLaunching = true;
    }

    private void TickLaunch()//吹っ飛びを1ティック分進める
    {
        float deltaTime = BattleTicker.TickTime;
        float groundOffset = 0.3f;

        launchElapsed += deltaTime;

        // 水平方向
        launchPos += launchHorizontalVelocity * deltaTime;

        // 垂直方向
        launchVerticalVelocity += gravity * deltaTime;

        //最初は勢い良く飛んで重力で落ちる
        launchPos.y += launchVerticalVelocity * deltaTime;

        // 地面に着いたら終了
        if (launchPos.y <= GROUND_Y - groundOffset)
        {
            launchPos.y = GROUND_Y;
            transform.position = launchPos;
            OnLanding(); // 空中コンボ終了
            EndLaunch();
            return;
        }

        transform.position = launchPos;

        //時間が来たら地面の高さに戻して終了
        if (launchElapsed >= launchDuration)
            EndLaunch();
    }
    private void EndLaunch()
    {
        transform.position = new Vector3(launchPos.x, GROUND_Y, launchPos.z);
        isLaunching = false;
    }
    private void OnLanding()//ここの処理は終わったという通知にしてあっちで処理をさせる
    {
        OnLanded?.Invoke();//起き上がりモーションを再生
    }
    public void ForwardMove(Vector3 dir,float Duration,float Power)
    {
        //ベクトルを単位ベクトルに変換
        dir.Normalize();

        //再生中のノックバックがあれば上書きする
        knockBackStartPos = transform.position;

        // 地面の高さに揃えてノックバック方向に飛ばす
        knockBackEndPos = new Vector3(knockBackStartPos.x, GROUND_Y, knockBackStartPos.z) + dir * Power;

        knockBackElapsed = 0f;
        knockBackDuration = Duration;

        //時間が0ならすぐに最終位置へ
        if (knockBackDuration <= 0f)
        {
            EndKnockBack();
            return;
        }
        isKnockBack = true;
    }
    private void TickKnockBack()//ノックバックを1ティック分進める
    {
        knockBackElapsed += BattleTicker.TickTime;

        //時間が来たら最終位置に合わせて終了
        if (knockBackElapsed >= knockBackDuration)
        {
            EndKnockBack();
            return;
        }

        float t = knockBackElapsed / knockBackDuration;

        // 最初速く、最後遅くするイーズアウトを採用
        t = 1f - (1f - t) * (1f - t) * (1f - t);
        //位置を滑らかに更新
        transform.position = Vector3.Lerp(knockBackStartPos, knockBackEndPos, t);
    }
    private void EndKnockBack()
    {
        float OffsetY = 0.01f;

        // 最終位置を微調整
        transform.position = new Vector3(knockBackEndPos.x, knockBackEndPos.y + OffsetY, knockBackEndPos.z);

        isKnockBack = false;
    }

    /// <summary>
    /// 最初の地点と最後の地点を設定
    /// </summary>
    /// <param name="direction"></param>
    /// <param name="power"></param>
    public void SetMoveInfo(Transform opponent,Vector3 direction, float power)
    {
        //相手がまだ端にいる時は解除しない 画面端でおかしくなるから

        moveStartPos = transform.position;

        //単位ベクトルをパワー分動かす
        moveEndPos = moveStartPos + direction.normalized * power;
    }
    public void ResetMoveInfo()//初期化処理
    {
        moveEndPos = Vector3.zero;
        moveStartPos = transform.position;
        currentMoveFrame = 0;
    }

    /// <summary>
    /// 技モーション中の移動方法イーズアウトを使って出を早くしている
    /// </summary>
    /// <param name="totalFrame"></param>
    /// <returns></returns>
    public Vector3 OnStraightMove(int totalFrame)
    {
        var t = (float)currentMoveFrame / totalFrame;

        if (t >= 1f)
            return moveEndPos;

        // イーズアウト
        t = 1f - (1f - t) * (1f - t) * (1f - t);

        Vector3 pos = transform.position;

        // 画面端でなくノックバック中じゃなければ動かす
        if (!IsGoingOutOfBounds()&&!isKnockBack)
        {
            pos = Vector3.Lerp(moveStartPos, moveEndPos, t);
        }
        
        // フレームは必ず進める1
        currentMoveFrame++;

        return pos;
    }
    public void StopForwardCoroutine()//ノックバックと吹っ飛びをその場で止める
    {
        isKnockBack = false;
        isLaunching = false;
    }

    public bool CheckIsKnockBack()//ノックバック中かどうかを見る
    {
        return isKnockBack;
    }
    public void RemoveKnockBackCoroutine()//ノックバックだけをその場で止める
    {
        isKnockBack = false;
    }


    //投げの際に相手の位置を補正
    public void SnapOpponentInFront(Transform targetRoot)
    {
        if (targetRoot == null) return;

        float posOffset = 0.8f;

        //相手の座標を自分の前まで持ってくる
        var pos = transform.position + transform.forward * posOffset;
        targetRoot.position = pos;


        // 自分の方を向かせる（Y軸回転だけ）
        Vector3 dir = (transform.position - targetRoot.position).normalized;
        dir.y = 0;

        if (dir != Vector3.zero)
            targetRoot.rotation = Quaternion.LookRotation(dir);
    }

}
