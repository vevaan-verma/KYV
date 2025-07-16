using UnityEngine;

[CreateAssetMenu(menuName = "Shop/JunkFoodData")]
public class JunkFoodData : FoodData {

    [Header("Data")]
    [SerializeField] private AbilityType abilityType;

    public AbilityType GetAbilityType() => abilityType;

}
