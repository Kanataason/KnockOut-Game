using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UiScrollPanel : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private RectTransform content;

    private void Update()
    {
        GameObject selected = EventSystem.current.currentSelectedGameObject;
        if (selected == null || !selected.transform.IsChildOf(content))
            return;

        RectTransform selectedRect = selected.GetComponent<RectTransform>();
        ScrollToElement(selectedRect);
    }

    void ScrollToElement(RectTransform target)
    {
        Canvas.ForceUpdateCanvases(); // 更新を反映

        // 表示領域とターゲットの相対Y位置を計算
        RectTransform viewport = scrollRect.viewport;
        Vector2 localPos = content.InverseTransformPoint(target.position);
        Vector2 viewportLocal = content.InverseTransformPoint(viewport.position);

        float contentHeight = content.rect.height;
        float viewportHeight = viewport.rect.height;

        float offset = localPos.y - viewportLocal.y;

        float normalizedPos = scrollRect.verticalNormalizedPosition + offset / (contentHeight - viewportHeight);

        scrollRect.verticalNormalizedPosition = Mathf.Clamp01(normalizedPos);
    }
}
