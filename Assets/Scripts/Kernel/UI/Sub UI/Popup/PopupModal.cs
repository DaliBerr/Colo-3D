
using Kernel.GameState;
using Lonize.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// [UIPrefab("Prefabs/UI/PopupPrefab")]
public class PopupModal : UIScreen
{
    public override Status currentStatus  => StatusList.PopUpStatus;

    public Button closeButton;
    public Button confirmButton;
    public TextMeshProUGUI messageText;


    // protected override void OnInit()
    // {
    // }
    // public virtual void Start()
    // {
    //     StatusController.AddStatus(StatusList.PopUpStatus);
    // }

    public void SetMessage(string message)
    {
        messageText.text = message;
    }

    public void SetConfirmButtonActive(bool isActive)
    {
        confirmButton.gameObject.SetActive(isActive);
    }

    public void SetCloseButtonActive(bool isActive)
    {
        closeButton.gameObject.SetActive(isActive);
    }

    public void SetConfirmButtonText(string text)
    {
        confirmButton.GetComponentInChildren<TextMeshProUGUI>().text = text;
    }

    public void SetCloseButtonText(string text)
    {
        closeButton.GetComponentInChildren<TextMeshProUGUI>().text = text;
    }
    public virtual void CancelButtonAction()
    {
        UIManager.Instance.CloseTopModal();
    }

}