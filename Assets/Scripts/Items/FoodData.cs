using UnityEngine;

[CreateAssetMenu(menuName = "Shop/FoodData")]
public class FoodData : ItemData {

    [Header("Data")]
    [SerializeField] private StatValue positiveBaseStat;
    [SerializeField] private StatValue negativeBaseStat;
    private bool hasPositiveStat;
    private bool hasNegativeStat;

    [Header("Settings")]
    [SerializeField] private bool grantByDefault;

    private void OnEnable() {

        hasPositiveStat = positiveBaseStat.GetValue() > 0; // check if the positive base stat is greater than 0 to determine if it has a positive stat
        hasNegativeStat = negativeBaseStat.GetValue() > 0; // check if the negative base stat is greater than 0 to determine if it has a negative stat

        // check if the positive and negative stat types are not the same if it has both a positive and negative stat
        if (hasPositiveStat && hasNegativeStat && positiveBaseStat.GetStatType() == negativeBaseStat.GetStatType())
            Debug.LogWarning($"The positive and negative stat types are the same for {name}. This will result in a stat being added and subtracted from the same stat type.");

    }

    public bool HasPositiveStat() => hasPositiveStat;

    public StatValue GetPositiveStat(CategoryDatabase categoryDatabase) => hasPositiveStat ? new StatValue(positiveBaseStat.GetStatType(), (int) (positiveBaseStat.GetValue() + categoryDatabase.GetCategoryData(category).GetRoundPositiveStatIncrement() * (GameData.GetRoundNumber() - 1))) : new StatValue(positiveBaseStat.GetStatType(), 0); // return the positive stat value with the round stat increment and round number applied if the food has a positive stat, otherwise return 0 to indicate that there is no positive stat

    public bool HasNegativeStat() => hasNegativeStat;

    public StatValue GetNegativeStat(CategoryDatabase categoryDatabase) => hasNegativeStat ? new StatValue(negativeBaseStat.GetStatType(), (int) (negativeBaseStat.GetValue() + categoryDatabase.GetCategoryData(category).GetRoundNegativeStatIncrement() * (GameData.GetRoundNumber() - 1))) : new StatValue(negativeBaseStat.GetStatType(), 0); // return the negative stat value with the round stat increment and round number applied if the food has a negative stat, otherwise return 0 to indicate that there is no negative stat

    public bool IsGrantedByDefault() => grantByDefault;

}
