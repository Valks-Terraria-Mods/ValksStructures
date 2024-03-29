﻿using ValksStructures.Content.Items;

namespace ValksStructures;

public partial class Schematic
{
    static readonly List<TileInfo> solidTiles = new();
    static bool containsFallingTiles;

    public static void Paste(Schematic schematic, Vector2I mPos, 
        int vOffset = 0)
    {
        // Do not let the player build structures concurrently
        if (ModContent.GetInstance<ValksStructures>().IsCurrentlyBuilding)
        {
            Main.NewText("Please wait for the current house to " +
                "finish building", Colors.RarityPink);

            return;
        }

        // Setup all variables
        containsFallingTiles = false;
        ModContent.GetInstance<ValksStructures>().IsCurrentlyBuilding = true;

        // The size of the schematic
        Vector2I size = schematic.Size;

        // Mouse Position (mPos) is the start position
        mPos += new Vector2I(0, -size.Y + vOffset);

        // All furniture tiles will be stored in here
        Dictionary<int, List<TileInfo>> furniture = 
            PrepareFurnitureDictionary(schematic, size);

        // Destroy the area where the structure will be placed
        DestroyArea(mPos, schematic);

        // Place walls and tiles
        PlaceWallsAndTiles(mPos, schematic, furniture);

        // Place tiles as reset tiles
        // Doing it this way will not disturb falling tiles like sand
        if (containsFallingTiles)
            ResetAllTiles();

        // Place tiles
        PlaceAllSolidTiles();

        // Place liquids all in one go
        VModSystem.AddAction(() =>
            PlaceLiquids(mPos, schematic));

        // Ensure all tiles are sloped correctly
        SlopeAllTiles();

        // Add all the furniture
        AddFurnitureTiles(furniture);

        // Clear liquids
        VModSystem.AddAction(() => 
            ClearAllLiquids(mPos, schematic));

        if (ModContent.GetInstance<Config>().BuildInstantly)
            // Construction will be built instantly
            VModSystem.ExecuteAllActions();
        else
            // Construction will be built by one task at a time every frame
            VModSystem.StartActions();
    }

    static void ClearAllLiquids(Vector2I mPos, Schematic schematic)
    {
        foreach (TileInfo tileInfo in schematic.Tiles)
        {
            Vector2I pos = mPos + tileInfo.Position;
            int x = pos.X;
            int y = pos.Y;

            if (IsLiquid(tileInfo))
                continue;

            Tile tile = Main.tile[x, y];
            tile.Clear(TileDataType.Liquid);

            if (Main.netMode == NetmodeID.MultiplayerClient)
                NetMessage.SendTileSquare(Main.myPlayer, x, y);
        }
    }

    static void PlaceAllSolidTiles()
    {
        foreach (TileInfo solidTile in solidTiles)
        {
            VModSystem.AddAction(() =>
            {
                PlaceTile(
                    solidTile.Position.X,
                    solidTile.Position.Y,
                    solidTile);
            });
        }
    }

    static void ResetAllTiles()
    {
        foreach (TileInfo solidTile in solidTiles)
        {
            VModSystem.AddAction(() =>
            {
                Vector2I pos = solidTile.Position;

                //WorldGen.KillTile(pos.X, pos.Y, noItem: true);

                ResetTileToType(
                    solidTile.Position.X,
                    solidTile.Position.Y,
                    solidTile);
            });
        }
    }

    static void PlaceLiquids(Vector2I mPos, Schematic schematic)
    {
        foreach (TileInfo tileInfo in schematic.Tiles)
        {
            Vector2I pos = mPos + tileInfo.Position;
            int x = pos.X;
            int y = pos.Y;

            if (IsLiquid(tileInfo))
            {
                WorldGen.PlaceLiquid(x, y, (byte)tileInfo.LiquidType,
                    tileInfo.LiquidAmount);

                if (Main.netMode == NetmodeID.MultiplayerClient)
                {
                    NetMessage.sendWater(x, y);
                    NetMessage.SendTileSquare(Main.myPlayer, x, y, TileChangeType.LavaWater);
                }
            };
        }
    }

    static Dictionary<int, List<TileInfo>> PrepareFurnitureDictionary(
        Schematic schematic, 
        Vector2I size)
    {
        Dictionary<int, List<TileInfo>> furniture = new();

        for (int i = 0; i < size.X * size.Y; i++)
        {
            TileInfo tileInfo = schematic.Tiles[i];
            int tileId = tileInfo.TileType;

            if (Utils.IsFurnitureTile(tileId) && !furniture.ContainsKey(tileId))
                furniture[tileInfo.TileType] = new();
        }

        return furniture;
    }

    static void DestroyArea(Vector2I startPos, Schematic schematic)
    {
        foreach (TileInfo tileInfo in schematic.Tiles)
        {
            Vector2I pos = startPos + tileInfo.Position;
            int x = pos.X;
            int y = pos.Y;
            Tile tile = Main.tile[x, y];

            if (TileID.Sets.Falling[tile.TileType])
                containsFallingTiles = true;

            // Do not kill tile if it is a replace tile
            if (IsReplaceTile(tileInfo))
                continue;

            // Do not destroy a non-existent tile
            if (!tile.HasTile)
                continue;

            VModSystem.AddAction(() =>
            {
                if (containsFallingTiles)
                    tile.ClearTile();
                else
                    Utils.KillTile(pos);

                if (Main.netMode == NetmodeID.MultiplayerClient)
                    NetMessage.SendTileSquare(Main.myPlayer, x, y);
            });
        }
    }

    static bool IsReplaceTile(TileInfo tileInfo) =>
        tileInfo.TileType == 
        ModContent.TileType<Content.Tiles.SchematicReplace>();

    static void PlaceWallsAndTiles(Vector2I startPos, Schematic schematic,
        Dictionary<int, List<TileInfo>> furniture)
    {
        // Reset solid tiles dictionary
        solidTiles.Clear();

        foreach (TileInfo tileInfo in schematic.Tiles)
        {
            Vector2I pos = startPos + tileInfo.Position;
            int x = pos.X;
            int y = pos.Y;
                
            // This is a replace tile, don't place anything here
            if (IsReplaceTile(tileInfo))
                continue;

            // Only place walls if wall exists in this tileInfo
            if (tileInfo.WallType != 0)
            {
                VModSystem.AddAction(() =>
                {
                    if (Main.tile[x, y].WallType == 0)
                    {
                        // No wall here, so place one
                        WorldGen.PlaceWall(x, y, tileInfo.WallType,
                            mute: true);
                    }
                    else
                    {
                        // Wall exists here, replace it
                        WorldGen.ReplaceWall(x, y, (ushort)tileInfo.WallType);
                    }

                    WorldGen.paintWall(x, y, tileInfo.WallColor);

                    if (Main.netMode == NetmodeID.MultiplayerClient)
                        NetMessage.SendTileSquare(Main.myPlayer, x, y);
                });
            }

            // Do not add furniture tiles right now
            if (Utils.IsFurnitureTile(tileInfo.TileType))
            {
                // Pass over the position
                tileInfo.Position = new Vector2I(x, y);

                // Keep track of the furniture tile to be added later
                furniture[tileInfo.TileType].Add(tileInfo);

                // This is a furniture tile so skip it
                continue;
            }

            // Place solid tiles
            if (tileInfo.HasTile)
            {
                // Pass over the position
                tileInfo.Position = new Vector2I(x, y);

                // Keep track of solid tiles for later use with slope
                solidTiles.Add(tileInfo);
            }
        }
    }

    static void SlopeAllTiles()
    {
        // Final pass to ensure all tiles are sloped correctly
        foreach (TileInfo solidTile in solidTiles)
        {
            VModSystem.AddAction(() =>
            {
                Vector2I pos = solidTile.Position;
                SlopeTile(pos.X, pos.Y, solidTile);
            });
        }
    }

    static void AddFurnitureTiles(Dictionary<int, List<TileInfo>> furniture)
    {
        // Otherwise chairs will not be placed properly
        if (furniture.ContainsKey(TileID.Chairs))
            furniture[TileID.Chairs].Reverse();

        foreach (List<TileInfo> furnitureList in furniture.Values)
            VModSystem.AddAction(() =>
            {
                foreach (TileInfo tileInfo in furnitureList)
                    AddFurnitureTile(tileInfo);
            });
    }

    static void AddFurnitureTile(TileInfo tileInfo)
    {
        // Open doors break surrounding tiles when placed in the world
        ReplaceTile(tileInfo, TileID.OpenDoor, TileID.ClosedDoor);
        ReplaceTile(tileInfo, TileID.TallGateOpen, TileID.TallGateClosed);
        ReplaceTile(tileInfo, TileID.TrapdoorOpen, TileID.TrapdoorClosed);

        int x = tileInfo.Position.X;
        int y = tileInfo.Position.Y;

        PlaceTile(x, y, tileInfo);
    }

    static void ReplaceWall(TileInfo tileInfo, int oldWall, int newWall)
    {
        if (tileInfo.WallType == oldWall)
            tileInfo.WallType = newWall;
    }

    static void ReplaceTile(TileInfo tileInfo, int oldTile, int newTile)
    {
        if (tileInfo.TileType == oldTile)
            tileInfo.TileType = newTile;
    }

    static void SlopeTile(int x, int y, TileInfo tileInfo)
    {
        WorldGen.SlopeTile(x, y, tileInfo.Slope);

        if (Main.netMode == NetmodeID.MultiplayerClient)
            NetMessage.SendTileSquare(Main.myPlayer, x, y);
    }

    static void ResetTileToType(int x, int y, TileInfo tileInfo)
    {
        Tile tile = Main.tile[x, y];
        tile.ResetToType((ushort)tileInfo.TileType);

        // Paint the tile with the appropriate color
        tile.TileColor = tileInfo.TileColor;

        // Helps with visuals
        tile.TileFrameX = (short)tileInfo.TileFrameX;
        tile.TileFrameY = (short)tileInfo.TileFrameY;

        if (Main.netMode == NetmodeID.MultiplayerClient)
            NetMessage.SendTileSquare(Main.myPlayer, x, y);
    }

    static void PlaceTile(int x, int y, TileInfo tileInfo)
    {
        if (IsReplaceTile(tileInfo))
            return;

        Tile tile = Main.tile[x, y];

        // Empty tile so clear this tile
        if (!tileInfo.HasTile)
        {
            tile.ClearTile();
            return;
        }

        // Get the style of this tile
        int style = TileStyle.CalculateStyle(tileInfo);

        // Place tile (no effect for liquids)
        WorldGen.PlaceTile(x, y, tileInfo.TileType,
            mute: true,
            forced: true,
            plr: Main.myPlayer,
            style: style);

        // Paint the tile with the appropriate color
        tile.TileColor = tileInfo.TileColor;

        if (tileInfo.TileType is TileID.Chairs)
        {
            // WorldGen.PlaceTile(...) overwrites the TileFrameX so that is why
            // this is set after

            // TileFrameX and TileFrameY seem to break workbenches
            // and other pieces of furniture
            tile.TileFrameX = (short)tileInfo.TileFrameX;
            //tile.TileFrameY = (short)tileInfo.TileFrameY;
        }

        if (Main.netMode == NetmodeID.MultiplayerClient)
            NetMessage.SendTileSquare(Main.myPlayer, x, y);
    }

    static bool IsLiquid(TileInfo tileInfo) => tileInfo.LiquidAmount > 0;
}
