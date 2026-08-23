using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class sh_PCPathSelectionUI : MonoBehaviour
{
    [Header("컨트롤러")]
    [SerializeField] private sh_MarkerRouteController markerRouteController;

    [Header("1번 PC UI")]
    [SerializeField] private Button pc1ToggleButton;
    [SerializeField] private GameObject pc1SelectedIndicator;
    [SerializeField] private Button pc1ConfirmButton;

    [Header("2번 PC UI")]
    [SerializeField] private Button pc2ToggleButton;
    [SerializeField] private GameObject pc2SelectedIndicator;
    [SerializeField] private Button pc2ConfirmButton;

    [Header("안내 텍스트")]
    [SerializeField] private TMP_Text selectedPathText;
    [SerializeField] private string noSelectionMessage = "PC를 선택해 주세요";
    [SerializeField] private string pc1SelectedMessage = "1번 PC가 선택되었습니다";
    [SerializeField] private string pc2SelectedMessage = "2번 PC가 선택되었습니다";

    [Header("버튼 색상")]
    [SerializeField] private Color selectedButtonColor = new Color(1f, 1f, 1f, 1f);
    [SerializeField] private Color unselectedButtonColor = new Color(0.55f, 0.55f, 0.55f, 1f);
    [SerializeField] private Color selectedTextColor = new Color(0f, 0f, 0f, 1f);
    [SerializeField] private Color unselectedTextColor = new Color(1f, 1f, 1f, 1f);

    private void Awake()
    {
        RefreshUI();
    }

    private void OnEnable()
    {
        RefreshUI();
    }

    public void OnClickPC1Toggle()
    {
        if (markerRouteController == null)
            return;

        markerRouteController.SelectPC1();
        RefreshUI();
    }

    public void OnClickPC2Toggle()
    {
        if (markerRouteController == null)
            return;

        markerRouteController.SelectPC2();
        RefreshUI();
    }

    public void OnClickPC1Confirm()
    {
        if (markerRouteController == null)
            return;

        markerRouteController.SelectPC1();
        markerRouteController.ConfirmPCSelection();
        RefreshUI();
    }

    public void OnClickPC2Confirm()
    {
        if (markerRouteController == null)
            return;

        markerRouteController.SelectPC2();
        markerRouteController.ConfirmPCSelection();
        RefreshUI();
    }

    public void RefreshUI()
    {
        sh_PCPathOption currentPathOption = markerRouteController != null
            ? markerRouteController.CurrentPathOption
            : sh_PCPathOption.None;

        bool isPC1Selected = currentPathOption == sh_PCPathOption.PC1;
        bool isPC2Selected = currentPathOption == sh_PCPathOption.PC2;

        if (selectedPathText != null)
        {
            if (isPC1Selected)
                selectedPathText.text = pc1SelectedMessage;
            else if (isPC2Selected)
                selectedPathText.text = pc2SelectedMessage;
            else
                selectedPathText.text = noSelectionMessage;
        }

        if (pc1SelectedIndicator != null)
            pc1SelectedIndicator.SetActive(isPC1Selected);

        if (pc2SelectedIndicator != null)
            pc2SelectedIndicator.SetActive(isPC2Selected);

        if (pc1ConfirmButton != null)
            pc1ConfirmButton.gameObject.SetActive(isPC1Selected);

        if (pc2ConfirmButton != null)
            pc2ConfirmButton.gameObject.SetActive(isPC2Selected);

        ApplyButtonColor(pc1ToggleButton, isPC1Selected);
        ApplyButtonColor(pc2ToggleButton, isPC2Selected);
    }

    private void ApplyButtonColor(Button targetButton, bool isSelected)
    {
        if (targetButton == null)
            return;

        targetButton.interactable = true;

        if (targetButton.targetGraphic != null)
            targetButton.targetGraphic.color = isSelected ? selectedButtonColor : unselectedButtonColor;

        ColorBlock colors = targetButton.colors;
        Color buttonColor = isSelected ? selectedButtonColor : unselectedButtonColor;
        colors.normalColor = buttonColor;
        colors.highlightedColor = buttonColor;
        colors.selectedColor = buttonColor;
        colors.pressedColor = buttonColor;
        colors.disabledColor = buttonColor;
        targetButton.colors = colors;

        TMP_Text buttonLabel = targetButton.GetComponentInChildren<TMP_Text>(true);
        if (buttonLabel != null)
            buttonLabel.color = isSelected ? selectedTextColor : unselectedTextColor;
    }
}
