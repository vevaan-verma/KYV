using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class AbilityButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler {

    [Header("References")]
    [SerializeField] private AbilityType abilityType;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private AbilityInfoWidget abilityInfoWidgetPrefab;
    private ShopManager shopManager;
    private AbilityDatabase abilityDatabase;
    private AbilityData abilityData;
    private Button button;
    private AbilityInfoWidget currAbilityInfoWidget;
    private Transform rootTransform; // this is the transform of the root object that the ability info widget will be parented to; this is used to ensure that the ability info widget is always in the same space as the root object and not in world space
    private Coroutine costLerpCoroutine;

    [Header("Settings")]
    [SerializeField] private float costLerpDuration;

    public void Initialize(Transform rootTransform) {

        this.rootTransform = rootTransform; // set the root transform to the transform of the root object that the ability info widget will be parented to

        shopManager = FindFirstObjectByType<ShopManager>();
        abilityDatabase = FindFirstObjectByType<AbilityDatabase>();
        button = GetComponent<Button>();

        costText.text = "0"; // set to 0 to ensure the text is not null and is parseable when lerping

        abilityData = abilityDatabase.GetAbilityData(abilityType);

        button.onClick.AddListener(() => shopManager.OnAbilityButtonClicked(this, abilityData));

    }

    public void SetAbility(int cost) {

        if (costLerpCoroutine != null) StopCoroutine(costLerpCoroutine);
        costLerpCoroutine = StartCoroutine(HandleCostLerp(cost));

        RefreshLayout(transform as RectTransform); // refresh the layout of the button to make sure the text is laid out correctly; this is needed because the text is lerped and the layout may not be updated until the next frame, which can cause the text to be cut off if it is too long; this is a workaround for a bug in Unity where the layout is not updated immediately after changing the text

    }

    public void OnPointerEnter(PointerEventData eventData) {

        // destroy the ability info widget if it exists
        if (currAbilityInfoWidget != null) Destroy(currAbilityInfoWidget.gameObject);

        currAbilityInfoWidget = Instantiate(abilityInfoWidgetPrefab, Input.mousePosition, Quaternion.identity, rootTransform); // instantiate the ability info widget at the mouse position and set its parent to the root transform
        currAbilityInfoWidget.transform.SetAsLastSibling(); // set the widget to the front
        currAbilityInfoWidget.Initialize(abilityData); // initialize the widget with the ability data

    }

    public void OnPointerExit(PointerEventData eventData) {

        // destroy the ability info widget if it exists
        if (currAbilityInfoWidget != null) Destroy(currAbilityInfoWidget.gameObject);

    }

    private IEnumerator HandleCostLerp(int targetCost) {

        float currentTime = 0f;
        float startCost = int.Parse(costText.text);

        while (currentTime < costLerpDuration) {

            costText.text = Mathf.FloorToInt(Mathf.Lerp(startCost, targetCost, currentTime / costLerpDuration)).ToString();
            currentTime += Time.deltaTime;
            yield return null;

        }

        costText.text = targetCost.ToString();

        RefreshLayout(transform as RectTransform); // refresh the layout of the button to make sure the text is laid out correctly; this is needed because the text is lerped and the layout may not be updated until the next frame, which can cause the text to be cut off if it is too long; this is a workaround for a bug in Unity where the layout is not updated immediately after changing the text

        costLerpCoroutine = null;

    }

    private void RefreshLayout(RectTransform root) {

        foreach (var layoutGroup in root.GetComponentsInChildren<LayoutGroup>())
            LayoutRebuilder.ForceRebuildLayoutImmediate(layoutGroup.GetComponent<RectTransform>());

    }

    public void SetInteractable(bool interactable) => button.interactable = interactable; // set the interactable state of the button

    public AbilityData GetAbilityData() => abilityData;

    public AbilityType GetAbilityType() => abilityType;

}
