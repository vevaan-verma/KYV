using System.Collections.Generic;
using UnityEngine;

public class AbilityDatabase : MonoBehaviour {

    [Header("Data")]
    private List<AbilityData> abilityData;

    private void Awake() {

        abilityData = new List<AbilityData>(Resources.LoadAll<AbilityData>(FileConstants.ABILITY_DATA_RES_PATH));

        #region VALIDATION
        // make sure ability array contains one of each of the values in the ability enum
        AbilityType[] abilityTypes = (AbilityType[]) System.Enum.GetValues(typeof(AbilityType)); // get all the ability types in the enum

        for (int i = 0; i < abilityTypes.Length; i++) // iterate through the ability types
            if (abilityData.Find(c => c.GetAbilityType() == abilityTypes[i]) == null)
                Debug.LogError($"Ability {abilityTypes[i]} is missing from the ability data.");
        #endregion

    }

    public AbilityData GetAbilityData(AbilityType abilityType) {

        foreach (var item in abilityData)
            if (item is AbilityData abilityData && abilityData.GetAbilityType() == abilityType)
                return abilityData;

        return null;

    }
}

public enum AbilityType {

    WakeUpCall,
    NinjaDashes,
    Deflection,
    ChuckingCheese,
    Leftovers,
    Cowardice,
    StrongSweeping,
    SprainedStems,
    Whirlwind,
    Herbicide

}
