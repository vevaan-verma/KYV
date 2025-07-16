public class SpecialItem : Item {

    public SpecialItem(SpecialItemData specialItemData) : base(specialItemData, Rarity.Special) { }

    public new SpecialItemData GetItemData() => (SpecialItemData) itemData;

    public override bool Equals(object obj) => obj is SpecialItem item && itemData.Equals(item.itemData) && rarity == item.rarity; // check if the item data and rarity are equal

    public override int GetHashCode() => itemData.GetHashCode() ^ rarity.GetHashCode(); // use the item data's hash code and rarity as the hash code; this ensures that the hash code is unique for each item, even if they have the same data but different rarities

}
