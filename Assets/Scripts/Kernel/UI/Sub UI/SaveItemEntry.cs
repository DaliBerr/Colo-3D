using System;
using Lonize.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// summary: 单个存档条目UI（用于存档列表），负责显示标题/时间并把点击事件抛给父UI（使用 Button.onClick）。
/// </summary>
public class SaveItemEntry : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI saveTitleText;
    [SerializeField] private TextMeshProUGUI saveTimeText;

    [Tooltip("条目的点击按钮。建议把整个 Entry 根节点做成 Button（带一个透明 Image 作为 Raycast 区域）。")]
    [SerializeField] private Button entryButton;

    [Header("Optional")]
    [SerializeField] private GameObject selectedIndicator;

    private Action<SaveItemEntry> _onClicked;

    /// <summary>
    /// summary: 存档名（通常等于文件名/目录名，不含扩展名）。
    /// </summary>
    public string SaveName { get; private set; }

    /// <summary>
    /// summary: Unity Awake，自动补全 Button 引用并注册点击回调。
    /// </summary>
    /// <returns>无</returns>
    private void Awake()
    {
        ResolveButtonReference();
        RegisterButtonCallback();
    }

    /// <summary>
    /// summary: Unity OnEnable，确保回调存在（避免预制体反复启用后丢失绑定）。
    /// </summary>
    /// <returns>无</returns>
    private void OnEnable()
    {
        RegisterButtonCallback();
    }

    /// <summary>
    /// summary: Unity OnDisable，取消回调，避免重复注册或潜在泄漏。
    /// </summary>
    /// <returns>无</returns>
    private void OnDisable()
    {
        UnregisterButtonCallback();
    }

    /// <summary>
    /// summary: 初始化显示内容，并记录存档名。
    /// </summary>
    /// <param name="title">存档标题/名称</param>
    /// <param name="time">存档时间字符串</param>
    /// <returns>无</returns>
    public void Initialize(string title, string time)
    {
        SaveName = title;

        if (saveTitleText != null)
            saveTitleText.text = title;

        if (saveTimeText != null)
            saveTimeText.text = time;
    }

    /// <summary>
    /// summary: 绑定点击回调（由父级 LoadMenuUI 提供）。
    /// </summary>
    /// <param name="onClicked">点击条目后的回调</param>
    /// <returns>无</returns>
    public void Bind(Action<SaveItemEntry> onClicked)
    {
        _onClicked = onClicked;
    }

    /// <summary>
    /// summary: 设置选中状态（用于高亮/勾选显示）。
    /// </summary>
    /// <param name="selected">是否选中</param>
    /// <returns>无</returns>
    public void SetSelected(bool selected)
    {
        if (selectedIndicator != null)
        {
            selectedIndicator.SetActive(selected);
            // GameDebug.Log("[SaveItemEntry] SetSelected: " + selected);
        }
        // else
        // {
        //     GameDebug.LogWarning("[SaveItemEntry] SetSelected: selectedIndicator is null.");
        // }
    }

    /// <summary>
    /// summary: 自动寻找 entryButton 引用（优先自身，其次子级）。
    /// </summary>
    /// <returns>无</returns>
    private void ResolveButtonReference()
    {
        if (entryButton != null)
            return;

        entryButton = GetComponent<Button>();
        if (entryButton == null)
            entryButton = GetComponentInChildren<Button>(true);

        if (entryButton == null)
            GameDebug.LogWarning("[SaveItemEntry] entryButton is null. 请在 Prefab 上绑定一个 Button（通常放在 Entry 根节点）。");
    }

    /// <summary>
    /// summary: 注册 Button.onClick 回调（先移除再添加，避免重复叠加）。
    /// </summary>
    /// <returns>无</returns>
    private void RegisterButtonCallback()
    {
        if (entryButton == null)
            return;

        entryButton.onClick.RemoveListener(HandleButtonClick);
        entryButton.onClick.AddListener(HandleButtonClick);
    }

    /// <summary>
    /// summary: 取消 Button.onClick 回调。
    /// </summary>
    /// <returns>无</returns>
    private void UnregisterButtonCallback()
    {
        if (entryButton == null)
            return;

        entryButton.onClick.RemoveListener(HandleButtonClick);
    }

    /// <summary>
    /// summary: Button 点击入口。
    /// </summary>
    /// <returns>无</returns>
    private void HandleButtonClick()
    {
        // GameDebug.Log("[SaveItemEntry] HandleButtonClick: " + SaveName);
        _onClicked?.Invoke(this);
    }
}
