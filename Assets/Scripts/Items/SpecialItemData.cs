using UnityEngine;

[CreateAssetMenu(menuName = "Shop/SpecialItemData")]
public class SpecialItemData : ItemData {

    [Header("Data")]
    [SerializeField] private int baseCost;
    [SerializeField] private SpecialItemType specialItemType;
    [SerializeField] private float roundCostIncrement;

    public int GetCurrentCost() => (int) (baseCost + (roundCostIncrement * (GameData.GetRoundNumber() - 1))); // get the current cost of the item; uses linear cost scaling

    public SpecialItemType GetSpecialItemType() => specialItemType;

}
