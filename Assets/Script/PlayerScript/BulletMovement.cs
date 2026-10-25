using System.Collections.Generic;
using UnityEngine;

public class BulletMovement : MonoBehaviour, ITickable
{
    public float Velocity = 10;//速度
    public Vector3 Direction = Vector3.forward;//進行方向

    [SerializeField] private float Distance = 2;//進行距離

    [SerializeField] private int MaxCount = 1;//ヒット回数
    private bool isMove = false;

    private ColliderManager ownerManager;//攻撃をした人
    private ColliderManager targetManager;//攻撃される人
    private AttackFrameController targetFrameController;//攻撃される人のやられ判定を取る用

    private List<ColliderType> hitBoxList = new();//攻撃のヒットボックス

    private ActionFrameData currentAttackData;

    private int currentHitCount = 0;//現在のヒット回数
    /// <summary>
    /// 誰が攻撃して誰を狙っているのかをセットする関数
    /// </summary>
    public void SetReference(ColliderManager target, AttackFrameController targetFrameController,
        ColliderManager ownerColliderManager,ActionFrameData data)
    {
        currentAttackData = data;//攻撃データ
        ownerManager = ownerColliderManager;//攻撃をした人
        targetManager = target;//攻撃対象
        this.targetFrameController = targetFrameController;//
    }
    /// <summary>
    /// 弾自体のパラメーターをセットする関数
    /// </summary>
    public void SetBulletParameter(float velocity,Vector3 dir)
    {
        Velocity = velocity;//速度
        Direction = dir.normalized;//進む方向

        currentHitCount = MaxCount;//ヒットカウントを復活させる
        isMove = true;
    }
    private void OnEnable()
    {
        //固定ティックで移動と当たり判定をする
        BattleTicker.Register(this);

        if (hitBoxList.Count >= 1) return;

        //ヒットボックスをリストに保存
        var hitBox = GetComponent<ColliderType>();
        hitBoxList.Add(hitBox);
    }
    private void OnDisable()
    {
        BattleTicker.Unregister(this);

        InitReference();

        currentHitCount = MaxCount;

        //プールに戻ったら次にパラメーターがセットされるまで動かさない
        isMove = false;
    }
    private void InitReference()
    {
        currentAttackData = null;
        ownerManager = null;
        targetManager = null;
        targetFrameController = null;
    }
    //プレイヤーが動いた後に処理する
    public int TickOrder => BattleTickOrder.Of(TickPhase.Bullet);

    //BattleTickerから1ティックごとに呼ばれる 移動してから当たり判定をする
    public void Tick()
    {
        if (!isMove) return;

        //direction方向に進行
        transform.position +=  Direction * Velocity * BattleTicker.TickTime;

        CheckHit();
    }
    private void CheckHit()
    {

        //参照がセットされる前は判定しない
        if (targetManager == null || ownerManager == null || targetFrameController == null) return;

        //相手との方向ベクトルを取る
        var dir = targetManager.transform.position - transform.position;

        //長さを出してその長さが当たり判定に入っていたら処理開始
        if (dir.sqrMagnitude < Distance * Distance)
        {
            //ヒットカウントが０になったら判定しないようにする
            if (currentHitCount <= 0) return;

            var hurtList = targetFrameController.GetCurrentHurtBoxesList();

            //当たっていたらヒットカウントを減らす
          if (ownerManager.CheckColliderHit(targetManager,
                hitBoxList, hurtList, currentAttackData))
            {
                currentHitCount--;
            }

        }
    }

}
