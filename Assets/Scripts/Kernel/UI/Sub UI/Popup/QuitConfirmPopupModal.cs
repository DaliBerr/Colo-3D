

using Lonize.Localization;
using Lonize.UI;
using UnityEngine;

[UIPrefab("Prefabs/UI/QuitPopup")]
public class QuitConfirmPopupModal : PopupModal
{
    protected override void OnInit()
    {
        closeButton.onClick.RemoveAllListeners();
        confirmButton.onClick.RemoveAllListeners();

        closeButton.onClick.AddListener(() =>
        {
            base.CancelButtonAction();
        });

        confirmButton.onClick.AddListener(() =>
        {
            ConfirmButtonAction();
        });

        SetCloseButtonText("No".Translate());
        SetConfirmButtonText("Yes".Translate());
    }
    public void ConfirmButtonAction()
    {
        Application.Quit();
    }
}