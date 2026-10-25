using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
public class UiButtonAnimation : MonoBehaviour
{
    [Header("Refarence")]
    //ボタンの色
    public Color NormalColor = Color.white;
    public Color SelectedColor = Color.yellow;
    //ボタンの情報
    public float SelectedScale = 1.1f;
    public float PressScale = 0.9f;
    public float AnimationSpeed = 10f;

    public UiInputManager CurrentPlayerInput;
    private Image _image;
    private Vector3 _baseScale;

    private void SubscribeEvents()//イベントを登録する
    {
        if (InputUserMakeManager.Instance != null)
            InputUserMakeManager.Instance.OnCreateCharacter += SetAndSubscribePlayerInput;
    }

    private void OnDisable()//いべんとの解除
    {
        if (InputUserMakeManager.Instance != null)
            InputUserMakeManager.Instance.OnCreateCharacter -= SetAndSubscribePlayerInput;

        if (CurrentPlayerInput != null)
            CurrentPlayerInput.OnSubmitAction -= UpdateSubmit;
    }

    /// <summary>
    ///キャラクターが生成されたら呼ばれて　イベントを登録
    /// </summary>
    private void SetAndSubscribePlayerInput(PlayerInput input)
    {
        //もし前の操作デバイスがあればイベントを解除
        if (CurrentPlayerInput != null)
            CurrentPlayerInput.OnSubmitAction -= UpdateSubmit;

        //イベントを登録
        CurrentPlayerInput = input.GetComponent<UiInputManager>();
        CurrentPlayerInput.OnSubmitAction += UpdateSubmit;
    }
    void Start()
    {
        SubscribeEvents();

        TryGetComponents();
        Init();
    }
    private void Init()
    {
        //もし入っていなかったら現在のUiの操作デバイスを取得して決定ボタンの通知も登録する
        if (InputUserMakeManager.Instance != null&&CurrentPlayerInput == null)
        {
            var current = InputUserMakeManager.Instance.GetCurrentUiPlayerInput();
            if (current != null) SetAndSubscribePlayerInput(current);
        }

        _baseScale = transform.localScale;//大きさを初期化
        if (_image != null)
            _image.color = NormalColor;//色を初期化
    }
    private void TryGetComponents()//コンポーネントを取得
    {
        _image = GetComponent<Image>();
    }
    void Update()
    {
        //操作キャラがいなかったら返す
        if (CurrentPlayerInput == null) return;
        // 選択されているか
        bool isSelected = EventSystem.current.currentSelectedGameObject == gameObject;

        // 色の更新
        if (_image != null)
        {
            _image.color = (isSelected) ? SelectedColor : NormalColor;
        }

        // スケールの更新
        Vector3 targetScale = _baseScale;

        if (isSelected)//選ばれているとき大きさを変える
        {
            targetScale = _baseScale * SelectedScale;
        }
        //スケールの更新
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.unscaledDeltaTime * AnimationSpeed);
    }
    private void UpdateSubmit()//決定ボタンを押したら呼ばれる
    { 
        if (_image != null)
        {
            _image.color = Color.white;
        }
    }
}