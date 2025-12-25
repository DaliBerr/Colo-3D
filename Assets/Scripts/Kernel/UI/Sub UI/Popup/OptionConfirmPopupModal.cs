using System.Collections;
using Kernel;
using Kernel.GameState;
using Lonize.Events;
using Lonize.Localization;
using Lonize.Logging;
using Lonize.UI;
using UnityEngine;

[UIPrefab("Prefabs/UI/OptionPopup")]
public class OptionConfirmPopupModal : PopupModal
{
    public int CountDownSeconds = 10;
    private float countdownTimer;
    private bool _closing;
    private int _lastShownSecond = -1;
    protected override void OnInit()
    {

        closeButton.onClick.RemoveAllListeners();
        confirmButton.onClick.RemoveAllListeners();

        closeButton.onClick.AddListener(() =>
        {
            // GameDebug.Log("[OptionConfirmPopupModal] Cancel Button Clicked");
            CancelButtonAction();
        });

        confirmButton.onClick.AddListener(() =>
        {
            // GameDebug.Log("[OptionConfirmPopupModal] Confirm Button Clicked");
            ConfirmButtonAction();
        });
        SetCloseButtonText("Cancel_changes".Translate());
        SetConfirmButtonText("Apply_changes".Translate());
        countdownTimer = CountDownSeconds;
    }

    public void Update()
    {
        if (_closing) return;
        countdownTimer -= Time.deltaTime;
        if (countdownTimer <= 0f)
        {
            CancelButtonAction();
            return;
        }
        RefreshCountdownText(false);
        // else
        // {
        //     SetMessage($"Cancel  changes in ({Mathf.CeilToInt(countdownTimer)})s");
        // }
    }
    public  override void CancelButtonAction()
    {
        base.CancelButtonAction();
        // UIManager.Instance.CloseTopModal();
        StartCoroutine(CoCancelNextFrame());
        // StartCoroutine(CoCloseNextFrame());
        
        // OptionsManager.Instance.CancelChanges();
        // StartCoroutine(CoApplyNextFrame());
    }
    private IEnumerator CoCloseNextFrame()
    {
        yield return null;
        UIManager.Instance.CloseTopModal();
    }
    private IEnumerator CoApplyNextFrame()
    {
        yield return null;
        OptionsManager.Instance.ApplySettings();
    }
    private IEnumerator CoCancelNextFrame()
    {
        yield return null;
        OptionsManager.Instance.CancelChanges();
        // GameDebug.Log("[OptionConfirmPopupModal] Changes Canceled");
    }
    public void ConfirmButtonAction()
    {
        UIManager.Instance.CloseTopModal();
        OptionsManager.Instance.ApplySettings();

    }

    private void OnEnable()
    {
        _closing = false;
        countdownTimer = CountDownSeconds;
        _lastShownSecond = -1;
        RefreshCountdownText(true);
    }
    private void RefreshCountdownText(bool force)
    {
        int sec = Mathf.CeilToInt(countdownTimer);
        if (!force && sec == _lastShownSecond) return;
        _lastShownSecond = sec;

        // 建议也本地化
        SetMessage($"Cancel changes in ({sec})s");
    }
}