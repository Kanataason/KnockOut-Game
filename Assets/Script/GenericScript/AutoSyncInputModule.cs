using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[ExecuteAlways]
public class AutoSyncInputModule : MonoBehaviour
{
    void OnEnable()
    {
        var playerInput = GetComponent<PlayerInput>();
        var uiInputModule = GetComponent<InputSystemUIInputModule>();

        if (playerInput != null && uiInputModule != null)
        {
            if (uiInputModule.actionsAsset != playerInput.actions)
            {
                uiInputModule.actionsAsset = playerInput.actions;

#if UNITY_EDITOR
                EditorUtility.SetDirty(uiInputModule);
                EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
            }
        }
    }
}