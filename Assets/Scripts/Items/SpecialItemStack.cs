public class SpecialItemStack : ItemStack {

    public SpecialItemStack(SpecialItem specialItem, int count) : base(specialItem, count) { } // constructor that takes a Food object and an amount

    public override Item GetItem() => (SpecialItem) item; // this can be casted to Food since we know it is a Food object (look at constructor)

}
