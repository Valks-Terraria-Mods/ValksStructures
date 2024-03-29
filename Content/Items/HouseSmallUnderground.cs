﻿namespace ValksStructures.Content.Items;

public class HouseSmallUnderground : SchematicItem
{
    protected override string SchematicName => "UndergroundHouse1";
    protected override Ingredient[] Ingredients => new Ingredient[]
    {
        new(ItemID.Wood, 50)
    };
    protected override int ItemRarity => ItemRarityID.LightRed;
}
