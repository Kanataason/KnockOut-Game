using System;
using UnityEngine;

public class PlayerMovement : MonoBehaviour, ITickable
{
    const float GRAVITY = 7.8f;

    private Transform targetEnemy;

    private PlayerInputHandler inputHandler;
    private PlayerController playerController;
    private PlayerState state;
    private PlayerKnockBackController backMotion;
    [SerializeField] StepInfo stepinfo = new();

    [Header("Jump")]
    [SerializeField] private AnimationCurve jumpCurve;
    [SerializeField] private float jumpHeight = 2f;
    [SerializeField] private float jumpDistanceX = 2f;
    [SerializeField] private float jumpMinDistance = 0.5f;

    private Vector3 jumpStartPos;
    private Vector3 jumpTargetPos;

    public bool IsJumping = false;
    private bool cantMove = false;

    private float VerticalSpeed = 0;
    [SerializeField] private float RayOffset;//地上判定の光線長さ
    private void Start()
    {
        TryComponents();
        SubscribeEvents();   
    }
    /// <summary>
    /// プレイヤーコントローラーからの動きの通知アクションを登録
    /// </summary>
    private void SubscribeEvents()
    {
        playerController.OnJumpTransitionAction += EnterJump;
        playerController.OnSetCanMoveFlagAction += SetMoveFlag;
    }

    /// <summary>
    /// プレイヤーコントローラーからの動きの通知アクションを解除
    /// </summary>
    private void UnSubscribeEvents()
    {
        if (playerController == null) return;

        playerController.OnJumpTransitionAction -= EnterJump;
        playerController.OnSetCanMoveFlagAction -= SetMoveFlag;
    }
    private void OnDestroy()
    {
        UnSubscribeEvents();
    }
    private void TryComponents()
    {
        inputHandler = GetComponent<PlayerInputHandler>();
        state = GetComponent<PlayerState>();
        backMotion = GetComponent<PlayerKnockBackController>();
        playerController = GetComponent<PlayerController>();
    }
    private void OnEnable()
    {
        BattleTicker.Register(this);
    }
    private void OnDisable()
    {
        BattleTicker.Unregister(this);
    }

    //プレイヤーの行動とノックバックの後に処理する
    public int TickOrder => BattleTickOrder.Of(TickPhase.Movement, state != null ? state.PlayerId : PlayerIds.None);

    //BattleTickerから1ティックごとに呼ばれる
    public void Tick()
    {
        //Startの前に呼ばれた時は何もしない
        if (playerController == null) return;

        CheckGround();

        UpdateFacing();
    }
    private void UpdateFacing()//プレイヤーが回転できるか判定
    {
        if (targetEnemy == null)
            return;

        if (!CanRotation())
            return;

        if (!playerController.GetCanRotation())
            return;

        Vector3 dir = targetEnemy.position - transform.position;
        dir.y = 0;

        if (dir.sqrMagnitude < 0.001f)
            return;

        UpdateRotation(dir);
    }
    private void CheckGround()//地面についているかどうかを判定
    {

        RaycastHit hit;
        if (Physics.Raycast(transform.position, Vector3.down, out hit, RayOffset))
        {
            VerticalSpeed = 0;
            transform.position = new Vector3(transform.position.x, hit.point.y + RayOffset, transform.position.z);
        }
        else
        {
            VerticalSpeed -= GRAVITY * BattleTicker.TickTime;
            transform.position += new Vector3(0, VerticalSpeed * BattleTicker.TickTime, 0);
        }
    }
    private void UpdateRotation(Vector3 Direction)
    {
        float rotationSpeed = 40f;

        Quaternion targetRotation = Quaternion.LookRotation(Direction);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, BattleTicker.TickTime * rotationSpeed);
    }
    private bool CanRotation()//キャラが回転可能かどうか確認
    {
        return state.currentTransitionState is not PlayerTransitionState.Counter &&
            state.currentTransitionState is not PlayerTransitionState.PunishCounter &&
            state.currentTransitionState is not PlayerTransitionState.PlayerDown && 
            state.currentTransitionState is not PlayerTransitionState.Hitting;
    }
    public void LockRotationPlayer()//キャラクターの回転を固定させる
    {
        //ベクトル獲得
        Vector3 targetDirection = targetEnemy.position - transform.position;
        targetDirection.y = 0;

        //方向を固定させる
        if (targetDirection == Vector3.zero) return;
        transform.rotation = Quaternion.LookRotation(targetDirection);
    }
    public void UpdateMoving(Vector2 moveInput)//キャラクターを動かす関数
    {
        if (inputHandler == null || playerController.PlayerActionState is PlayerAction.Attack) return;
        var Speed = cantMove ? 0 : 1;

        //パワーアップ状態ならスピードを変える
        var totalSpeed = state.PlayerSpeed * Speed;
        //BattleTickerから1ティックごとに呼ばれるので1ティック分の時間で動かす
        transform.position +=
            new Vector3(inputHandler.MoveInput.x * totalSpeed * BattleTicker.TickTime, 0, 0);
    }

    /// <summary>
    /// フレームごとに呼んでジャンプの処理をする
    /// </summary>
    /// <param name="currentFrame"></param>
    /// <param name="totalFrames"></param>
    /// <returns></returns>
    public bool UpdateJumps(int currentFrame,int totalFrames)
    {
        if (IsJumping)
        {

            float t = Mathf.Clamp01((float)currentFrame / totalFrames);

            //アニメーションカーブから軌道を取る
            float yOffset = jumpCurve.Evaluate(t) * jumpHeight;

            float xPos = Mathf.Lerp(
                jumpStartPos.x,
                jumpTargetPos.x,
                t);

            xPos = CheckScreenEdge(xPos);//画面端かどうか確認

            transform.position = new Vector3(
                xPos,
                jumpStartPos.y + yOffset,
                jumpStartPos.z);

            if (currentFrame >= totalFrames)
            {
                IsJumping = false;
                return true;
            }
            
        }
            return false;
    }
    private float  CheckScreenEdge(float xPos)
    {
        if (backMotion.CheckCanKnockBack(targetEnemy.position))
        {
            var target = targetEnemy;
            // 左向き
            if (transform.position.x < target.position.x)
            {
                xPos = Mathf.Min(xPos, target.position.x - jumpMinDistance);
            }
            // 右向き
            else
            {
                xPos = Mathf.Max(xPos, target.position.x + jumpMinDistance);
            }
        }
        return xPos;
    }

    private void EnterJump(int direction)
    {
        Jump(direction);
        LockRotationPlayer();
    }
    private void Jump(float direction)
    {
        if (IsJumping) return;

        IsJumping = true;

        jumpStartPos = transform.position;

        jumpTargetPos = jumpStartPos + new Vector3(jumpDistanceX * direction, 0, jumpStartPos.z);

    }

    //ステップできる状態か見る
    public bool CheckCanStep(int direction,Vector3 playerPos,bool IsBack,int waitTime)
    {
        return stepinfo.CheckStep(direction, playerPos, IsBack,waitTime);
    }
    //ステップの処理をフレームごとに更新
    public void UpdateStep(Vector3 playerPos,int currentFrame)
    {
        var resultPos = stepinfo.UpdateStep(playerPos, currentFrame);

        //画面は端かどうか見る
        if (backMotion.IsGoingOutOfBounds()) return;

        transform.position += resultPos;
    }
    //ステップに必要な情報をリセット
    public void InitStepInfo() { stepinfo.InitStepInfo(); }

    //入力方向をリセット
    public void InitDirection() { stepinfo.InitDir(); }

    //現在ステップ中か確認
    public bool IsStepping() { return stepinfo.IsStepping; }
    public void ResetJumpFlag(bool isJump)=> IsJumping = isJump;
    private void SetMoveFlag(bool isMove) => cantMove = isMove;
    public void SetEnemy(Transform enemy) => targetEnemy = enemy;
}
[Serializable]
public class StepInfo
{
    public float StepDistance = 1.8f;//ステップ移動距離
    public int StepDurationFrame = 23;//ステップ処理時間
    public bool IsStepping => isStep;//ステップフラグ
    
    public bool IsBack => isBack;//バックステップをするかどうかのフラグ

    private bool isBack = false;
    private bool isStep = false;

    private Vector3 StartStepPos;//処理開始地点
    private Vector3 TargetStepPos;//目的地

    private float BackStepScale;//バックステップの移動距離
    private int inputTimeFrame;//ステップ猶予時間
    private int lastDir = 0;//最後に入力した方向

    //ステップできるかチェックする
    public bool CheckStep(int dir, Vector3 currentPos, bool dirFlag, int waitTime)
    {

        float backStepDistance = 0.5f;
        float forwardStepDistance = 0.8f;

        //猶予時間は描画のフレームではなくティック数で数える
        int currentTick = BattleTicker.CurrentTick;

        //前の向きと違ったら今の向きを保存する
        if (dir != lastDir)
        {
            lastDir = dir;
            inputTimeFrame = currentTick + waitTime -1;
            return false;
        }

        // ステップ猶予時間にステップしていたらステップ
        if (currentTick <= inputTimeFrame&&!isStep)
        {
            isBack = dirFlag;
            BackStepScale = dirFlag ? backStepDistance : forwardStepDistance;

            //ステップを始める
            StartStep(dir, currentPos, BackStepScale);

            //古い方向を０に初期化
            lastDir = 0;

            return true;
        }

        // 今の向きを保存　猶予時間を更新
        lastDir = dir;
        inputTimeFrame = currentTick + waitTime;

        return false;
    }
    public void InitStepInfo()//ステップ情報の初期化
    {
        lastDir = 0;
        inputTimeFrame = BattleTicker.CurrentTick;
        BackStepScale = 0;
        isBack = false;
        StopStep();
    }
    public void InitDir() => lastDir = 0;
    public Vector3 UpdateStep(Vector3 CurrentPos,int currentFrame)//ステップをフレームごとに更新
    {
        if (!isStep) return Vector3.zero;

        var t = (float)currentFrame / StepDurationFrame;
        var newPos = Vector3.Lerp(StartStepPos, TargetStepPos, t);//場所を滑らかに更新

        //処理終了時間が来たらステップ終了
        if (t >= 1) isStep = false;

        return newPos - CurrentPos;
    }
    //ステップを開始
    private void StartStep(int dir, Vector3 CurrentPos, float stepScale)
    {
        isStep = true;
        StartStepPos = CurrentPos;
        TargetStepPos = CurrentPos + new Vector3(dir * StepDistance * stepScale, 0, 0);
    }
    public void StopStep()
    {
        if (isStep) isStep = false;
    }
}
