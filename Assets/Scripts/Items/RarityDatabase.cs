using System;
using UnityEngine;

public class RarityDatabase : MonoBehaviour {

    [Header("Data")]
    [SerializeField] private RarityData[] itemRarityData;
    [SerializeField] private ExcludedRarityColor[] exludedRarityColors;

    [Header("Settings")]
    [SerializeField] private float rarityPositiveStatIncrement;
    [SerializeField] private float rarityNegativeStatIncrement;

    private void Start() {

        #region VALIDATION
        // make sure rarity array contains one of each of the values in the rarity enum, excluding the rarities to exclude
        Rarity[] rarities = (Rarity[]) Enum.GetValues(typeof(Rarity)); // get all the rarities in the enum

        Rarity[] excludedRarities = new Rarity[GameSettings.RARITIES_TO_EXCLUDE]; // create an array to hold the excluded rarities
        Array.Copy(rarities, rarities.Length - GameSettings.RARITIES_TO_EXCLUDE, excludedRarities, 0, GameSettings.RARITIES_TO_EXCLUDE); // copy the excluded rarities from the end of the enum list to the new array (do this before resizing the array)

        Array.Resize(ref rarities, rarities.Length - GameSettings.RARITIES_TO_EXCLUDE); // resize the array to exclude the rarities to exclude from the back of the enum list

        for (int i = 0; i < rarities.Length; i++) // iterate through the rarities, excluding the rarities to exclude from the back of the enum list
            if (Array.Find(itemRarityData, r => r.GetRarity() == rarities[i]) == null)
                Debug.LogError($"Rarity {rarities[i]} is missing from the rarity data.");

        for (int i = 0; i < excludedRarities.Length; i++) // iterate through the excluded rarities and make sure they have an excluded rarity color
            if (Array.Find(exludedRarityColors, r => r.GetRarity() == excludedRarities[i]) == null)
                Debug.LogError($"Excluded rarity {excludedRarities[i]} is missing from the excluded rarity colors.");

        // make sure the spawn chances add up to 100
        float totalSpawnChance = 0f;

        foreach (RarityData rarity in itemRarityData)
            totalSpawnChance += rarity.GetSpawnChance();

        if (totalSpawnChance != 100f)
            Debug.LogError("The total spawn chance of all rarities must be 100.");
        #endregion

    }

    public Rarity GetRandomRarity() {

        float randomValue = UnityEngine.Random.Range(0f, 100f);
        float currentSpawnChance = 0f;

        // iterate through the rarities and return the rarity if the random value is less than the current spawn chance
        foreach (RarityData rarity in itemRarityData) {

            currentSpawnChance += rarity.GetSpawnChance();

            // if the random value is less than the current spawn chance, return the rarity
            if (randomValue < currentSpawnChance)
                return rarity.GetRarity();

        }

        return Rarity.Processed; // return the lowest rarity if the random value is greater than the total spawn chance

    }

    public Color GetRarityColor(Rarity rarity) {

        // check if the rarity is in the item rarity data and return the color if it is
        RarityData rarityData = Array.Find(itemRarityData, r => r.GetRarity() == rarity);

        if (rarityData != null) return rarityData.GetColor(); // return the rarity color if the rarity is in the item rarity data

        // check if the rarity is in the excluded rarities list and return the color if it is
        ExcludedRarityColor excludedRarityColor = Array.Find(exludedRarityColors, r => r.GetRarity() == rarity);

        if (excludedRarityColor != null) return excludedRarityColor.GetColor(); // return the excluded rarity color if the rarity is in the excluded rarities list

        return Color.white; // return white if the rarity is not in the item rarity data or the excluded rarities list

    }

    public int GetPositiveStatValue(int statValue, Rarity rarity) => statValue > 0 ? (int) (statValue + (rarityPositiveStatIncrement * (int) rarity)) : 0; // returns the stat value with the rarity positive stat multiplier applied linearly; the lowest rarity just returns the given value; stat value is floored/truncated; if the stat value is 0, it doesn't scale the stat value by the rarity

    public int GetNegativeStatValue(int statValue, Rarity rarity) => statValue > 0 ? (int) (statValue + (rarityNegativeStatIncrement * (int) rarity)) : 0; // returns the stat value with the rarity negative stat multiplier applied linearly; the lowest rarity just returns the given value; stat value is floored/truncated; if the stat value is 0, it doesn't scale the stat value by the rarity

    [Serializable]
    private class RarityData {

        [Header("Data")]
        [SerializeField] private Rarity rarity;
        [SerializeField] private float spawnChance;
        [SerializeField] private Color color;

        public Rarity GetRarity() => rarity;

        public float GetSpawnChance() => spawnChance;

        public Color GetColor() => color;

    }
}

public enum Rarity {

    // listed in increasing order of rarity
    Processed,
    Fresh,
    Organic,
    Homegrown,
    Special

}

[Serializable]
public class ExcludedRarityColor {

    [Header("Data")]
    [SerializeField] private Rarity rarity;
    [SerializeField] private Color color;

    public Rarity GetRarity() => rarity;

    public Color GetColor() => color;

}
