global using Microsoft.Xna.Framework;
global using Terraria;
global using Terraria.ID;
global using Terraria.DataStructures;
global using Terraria.ModLoader;
global using Terraria.ObjectData;
global using Terraria.ModLoader.Config;
global using System;
global using System.Collections.Generic;
global using System.ComponentModel;
global using System.Linq;

namespace ValksStructures;

public class ValksStructures : Mod
{
    public bool IsCurrentlyBuilding { get; set; }
}