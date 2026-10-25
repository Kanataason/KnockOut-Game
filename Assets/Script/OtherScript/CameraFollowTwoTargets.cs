using System;
using System.Collections;
using UnityEngine;
public class CameraFollowTwoTargets : MonoBehaviour
{
    public static bool IsZooming = false;
    private Transform target1;
    private Transform target2;

    public float minSize = 5f;
    public float maxSize = 10f;
    //ステージ幅
    [SerializeField] private float stageLeft = -8.5f;
    [SerializeField] private float stageRight = 8.5f;

    private Vector2 minPosition; // ステージ左下
    private Vector2 maxPosition; // ステージ右上
    [SerializeField] private float zoomScale = 1.3f;
    [SerializeField] private float deadZoneX = 0.5f;//カメラの動く判定X軸
    [SerializeField] private float deadZoneY = 0.3f;//カメラの動く判定Y軸
    [SerializeField] private float playerPosYOffset = 1f;
    [SerializeField] private float smoothTime = 0.2f;//カメラの滑らかに動き具合

    private Vector3 velocity;

    public float WidthOffset = 0.9f;//画面端からの距離補正

    private Camera cam;
    private Vector3 startPos = Vector3.zero;

    [Header("ズーム処理")]
    [SerializeField] float zoomFOV = 30f;       // ズーム時の視野
    [SerializeField] float normalFOV = 60f;     // 通常時の視野
    [SerializeField] float zoomDuration = 0.3f; // ズーム速度
    [SerializeField] float holdDuration = 0.5f; // ズーム維持時間
    public float lockDistance = 6f; // 最初の固定距離
    [SerializeField] Vector3 offset = new Vector3(0, 1, -5);

    Coroutine zoomCoroutine;
    void Start()
    {
        startPos = transform.position;
        cam = Camera.main;
    }
    private void OnDisable()//イベントの解除
    {
        //ズームの途中で消えても時間が止まったままにならないようにする
        if (IsZooming)
            UiUtility.PauseService.Resume(UiUtility.PauseReason.CameraZoom);

        if (target1 == null || target2 == null) return;
        UnsubscribeEvent(target1);
        UnsubscribeEvent(target2);
        IsZooming = false;
    }

    void SubscribeEvent(Transform target)//イベント登録
    {
        BattleStateManager.Instance.OnNextRoundAction -= InitCameraPos;
        BattleStateManager.Instance.OnNextRoundAction += InitCameraPos;
        BattleStateManager.Instance.OnKnockOutAction -= StartZoom;
        BattleStateManager.Instance.OnKnockOutAction += StartZoom;

        var anime = target.GetComponent<AnimationManager>();
        if (anime != null)
        {
            anime.OnCameraZoom += StartZoom;
        }
    }

    void UnsubscribeEvent(Transform target)//イベント解除
    {
        if (BattleStateManager.Instance != null)
        {
            BattleStateManager.Instance.OnNextRoundAction -= InitCameraPos;
            BattleStateManager.Instance.OnKnockOutAction -= StartZoom;
        }

        var anime = target.GetComponent<AnimationManager>();
        if (anime != null)
        {
            anime.OnCameraZoom -= StartZoom;
        }
    }
    public void SetTarget(Transform target,int playerNum)
    {
        if (playerNum == PlayerIds.Player1)
            target1 = target;
        else
            target2 = target;

        SubscribeEvent(target);
    }
    private void InitCameraPos()
    {
        transform.position = startPos;
    }
    void LateUpdate()
    {
        if (IsZooming) return;
        if (target1 == null || target2 == null) return;

        Vector3 p1 = target1.position;
        Vector3 p2 = target2.position;

        float zDistance = Mathf.Abs(transform.position.z - p1.z);
        Vector3 bottomLeft = cam.ViewportToWorldPoint(new Vector3(0, 0, zDistance * WidthOffset));
        Vector3 topRight = cam.ViewportToWorldPoint(new Vector3(1, 1, zDistance * WidthOffset));

        // min/maxを更新
        minPosition = new Vector2(bottomLeft.x, bottomLeft.y);
        maxPosition = new Vector2(topRight.x, topRight.y);

        // プレイヤーの位置を制限
        p1.x = Mathf.Clamp(p1.x, minPosition.x, maxPosition.x);
        p2.x = Mathf.Clamp(p2.x, minPosition.x, maxPosition.x);

        target1.position = p1;
        target2.position = p2;

        float width = Mathf.Abs(p1.x - p2.x);

        // FOVを考慮して距離を計算
        float halfFOV = cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
        float targetDistance = ((width *zoomScale) / 2f) / Mathf.Tan(halfFOV);

        // 必要に応じてmin/max距離で制限
        targetDistance = Mathf.Clamp(targetDistance, minSize, maxSize);

        // カメラのzを調整
        Vector3 camPos = transform.position;
        camPos.z = -targetDistance; // -Z方向に移動して調整
        transform.position = camPos;


        //プレイヤー同士の中間座標を取得
        float centerX = (p1.x + p2.x) * 0.5f;
        float centerY = (p1.y + p2.y) * 0.5f + playerPosYOffset;

        float offsetX = centerX - transform.position.x;
        float offsetY = centerY - transform.position.y;

        Vector3 targetPos = transform.position;

        // X軸のカメラ移動
        if (Mathf.Abs(offsetX) > deadZoneX)
        {
            targetPos.x = centerX - Mathf.Sign(offsetX) * deadZoneX;
        }

        // Y軸のカメラ移動
        if (Mathf.Abs(offsetY) > deadZoneY)
        {
            targetPos.y = centerY - Mathf.Sign(offsetY) * deadZoneY;
        }

        targetPos.x = Mathf.Clamp(targetPos.x, stageLeft, stageRight);
        //スムーズに移動
        transform.position = Vector3.SmoothDamp(
            transform.position,
            targetPos,
            ref velocity,
            smoothTime);

    }









    //イベントから呼ばれる
    public void StartZoom(Transform opponentPos,Action receiver)
    {
        var dir = GetDirection(opponentPos);

        if (zoomCoroutine != null) StopCoroutine(zoomCoroutine);

        zoomCoroutine = StartCoroutine(ZoomSequence(dir,opponentPos,receiver));
    }
    private Vector3 GetDirection(Transform targets)
    {
        //渡されたターゲットじゃない方を相手とする
        Transform other = targets == target1 ? target2 : target1;
        if (targets == null || other == null) return Vector3.zero;

        return other.position - targets.position;
    }

    IEnumerator ZoomSequence(Vector3 dir,Transform targets,Action Receiver)
    {
        IsZooming = true;
        offset.x = dir.x > 0 ? lockDistance :-lockDistance; 

        float time = 0f;
        float startFOV = cam.fieldOfView;
        Vector3 startPos = cam.transform.position;
        Vector3 targetPos = targets.position + offset;
        float cameraPosY = -3.5f;

        //ゲーム時間を止める
        UiUtility.PauseService.Pause(UiUtility.PauseReason.CameraZoom);

        //ズーム
        while (time < zoomDuration)
        {
            time += Time.unscaledDeltaTime;
            
            float p = time / zoomDuration;
            cam.fieldOfView = Mathf.Lerp(startFOV, zoomFOV, p);

            if (targets != null)
            {
                cam.transform.position = Vector3.Lerp(startPos, targetPos, p);
            }

            yield return null;
        }


        yield return new WaitForSecondsRealtime(holdDuration);//指定時間まで待つ
        //元に戻す
        time = 0f;
        while (time < zoomDuration)
        {
            time += Time.unscaledDeltaTime;
            float p = time / zoomDuration;

            cam.fieldOfView = Mathf.Lerp(zoomFOV, normalFOV, p);
            cam.transform.position = Vector3.Lerp(cam.transform.position, startPos, p);

            yield return null;
        }
        cam.transform.position = new Vector3(startPos.x,cameraPosY, startPos.z);

        //時間停止解除
        IsZooming = false;
        UiUtility.PauseService.Resume(UiUtility.PauseReason.CameraZoom);
        Receiver?.Invoke();
    }

}
