using UnityEngine;

[CreateAssetMenu(menuName = "Shop/AbilityData")]
public class AbilityData : ScriptableObject {

    [Header("Data")]
    [SerializeField] private new string name;
    [SerializeField] private AbilityType abilityType;
    [SerializeField] private string description;
    [SerializeField] private Sprite icon;
    [SerializeField] private int baseCost;
    [SerializeField] private float roundCostIncrement;

    public string GetName() => name;

    public AbilityType GetAbilityType() => abilityType;

    public string GetDescription() => description;

    public Sprite GetIcon() => icon;

    public int GetCurrentCost() => (int) (baseCost + (roundCostIncrement * (GameData.GetRoundNumber() - 1))); // get the current cost of the ability; uses linear cost scaling

}
