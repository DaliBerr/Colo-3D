#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 强制在进入 Play Mode 前切换到指定启动场景。
/// 放在 Assets/Editor/ 下。
/// </summary>
[InitializeOnLoad]
public static class PlayFromSpecificScene
{
    private const string StartScenePath = "Assets/Scenes/StartPage.unity"; // ←改成你的Scene路径
    private static string _previousScenePath;

    static PlayFromSpecificScene()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    /// <summary>
    /// 监听 PlayMode 状态变化，在进入播放前切到指定 Scene，在退出后切回原 Scene。
    /// </summary>
    /// <param name="state">Unity 编辑器当前的播放状态</param>
    /// <returns>无</returns>
    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            // 记录当前场景（只记录 active scene，按需你也可以扩展为多场景）
            _previousScenePath = SceneManager.GetActiveScene().path;

            if (!string.IsNullOrEmpty(StartScenePath) && _previousScenePath != StartScenePath)
            {
                // 如果有未保存修改，弹窗询问
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    // 用户取消保存 -> 取消进入 Play
                    EditorApplication.isPlaying = false;
                    return;
                }

                EditorSceneManager.OpenScene(StartScenePath, OpenSceneMode.Single);
            }
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            // 从 Play 返回编辑器后，切回原场景（如果你希望这样）
            if (!string.IsNullOrEmpty(_previousScenePath) && _previousScenePath != StartScenePath)
            {
                EditorSceneManager.OpenScene(_previousScenePath, OpenSceneMode.Single);
            }
        }
    }

    /// <summary>
    /// 菜单：快速打开启动 Scene，方便检查路径是否正确。
    /// </summary>
    /// <returns>无</returns>
    [MenuItem("Tools/Play Mode/Open Start Scene")]
    private static void OpenStartScene()
    {
        EditorSceneManager.OpenScene(StartScenePath, OpenSceneMode.Single);
    }
}
#endif
