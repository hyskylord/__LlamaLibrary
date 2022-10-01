﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using Buddy.Coroutines;
using ff14bot;
using ff14bot.Managers;
using ff14bot.Navigation;
using ff14bot.Pathing.Service_Navigation;
using ff14bot.RemoteWindows;
using LlamaLibrary.Enums;
using LlamaLibrary.Extensions;
using LlamaLibrary.Logging;
using LlamaLibrary.Memory.Attributes;
using LlamaLibrary.RemoteAgents;
using LlamaLibrary.RemoteWindows;
using LlamaLibrary.Structs;

namespace LlamaLibrary.Helpers
{
    public static class GrandCompanyShop
    {
        private static readonly string Name = "GrandCompanyShop";
        private static readonly Color LogColor = Colors.SeaGreen;
        private static readonly LLogger Log = new LLogger(Name, LogColor);

        internal static class Offsets
        {
            [Offset("Search 0F B6 15 ? ? ? ? 8D 42 ? 3C ? 77 ? FE CA 48 8D 0D ? ? ? ? Add 3 TraceRelative")]
            internal static IntPtr CurrentGC;

            [Offset("Search 48 83 EC ? 48 8B 05 ? ? ? ? 44 8B C1 BA ? ? ? ? 48 8B 88 ? ? ? ? E8 ? ? ? ? 48 85 C0 75 ? 48 83 C4 ? C3 48 8B 00 48 83 C4 ? C3 ? ? ? ? ? ? ? ? ? ? ? ? ? ? ? 48 83 EC ? 80 F9 ?")]
            internal static IntPtr GCGetMaxSealsByRank;

            [Offset("Search 48 8D 9E ? ? ? ? 4C 89 B4 24 ? ? ? ? Add 3 Read32")]
            internal static int GCArrayStart;

            [Offset("Search 41 83 FD ? 0F 82 ? ? ? ? 41 0F B6 97 ? ? ? ? Add 3 Read8")]
            internal static int GCShopCount;

            [Offset("Search 48 8B 05 ? ? ? ? 33 C9 40 84 FF Add 3 TraceRelative")]
            internal static IntPtr GCShopPtr;
        }

        public static IntPtr ActiveShopPtr => Core.Memory.Read<IntPtr>(Offsets.GCShopPtr);

        public static IntPtr ListStart => (ActiveShopPtr + Offsets.GCArrayStart);

        public static List<GCShopItem> Items => Core.Memory.ReadArray<GCShopItem>(ActiveShopPtr + Offsets.GCArrayStart, Offsets.GCShopCount).Where(i => i.ItemID != 0).ToList();

        public static int CanAfford(GCShopItem item)
        {
            return (int) Math.Floor((double) (Core.Me.GCSeals() / item.Cost));
        }

        public static async Task<int> BuyItem(uint ItemId, int qty)
        {
            if (!await OpenShop())
            {
                return 0;
            }

            var item = Items.FirstOrDefault(i => i.ItemID == ItemId);
            Log.Information($"Want to buy {DataManager.GetItem(item.ItemID).LocaleName()}");
            if (item.ItemID == 0)
            {
                Log.Error($"Can't find item {(ActiveShopPtr + Offsets.GCArrayStart).ToString("X")}");
                foreach (var item1 in Items)
                {
                    Log.Error($"{item1}");
                }

                return 0;
            }

            if (item.Cost > Core.Me.GCSeals())
            {
                Log.Error($"Don't have enough seals for {item.Item.LocaleName()}");
                return 0;
            }

            var oldBagQty = item.InBag;
            var qtyCanBuy = Math.Min(qty, CanAfford(item));
            Log.Information($"CanBuy {qtyCanBuy}");
            AgentGrandCompanyExchange.Instance.BuyItem(item.Index, qtyCanBuy);
            await Coroutine.Wait(5000, () => SelectYesno.IsOpen);
            if (SelectYesno.IsOpen)
            {
                SelectYesno.Yes();
                Log.Information($"Clicked Yes");
                await Coroutine.Wait(5000, () => !SelectYesno.IsOpen);
            }

            await Coroutine.Wait(3000, () => Items.FirstOrDefault(i => i.ItemID == ItemId).InBag != oldBagQty);
            await Coroutine.Sleep(500);
            GrandCompanyExchange.Instance.Close();
            await Coroutine.Wait(5000, () => !GrandCompanyExchange.Instance.IsOpen);
            Core.Me.ClearTarget();
            await Coroutine.Sleep(500);
            return qtyCanBuy;
        }

        public static async Task<bool> BuyKnownItems(List<(uint ItemId, int qty)> items)
        {
            if (!await OpenShop())
            {
                Log.Error("Could not open the shop");
                return false;
            }

            foreach (var itemToBuy in items)
            {
                if (!KnownItems.TryGetValue(itemToBuy.ItemId, out var itemInfo))
                {
                    continue;
                    //return false;
                }

                if (AgentGrandCompanyExchange.Instance.Rank != itemInfo.GCRankGroup)
                {
                    Log.Information($"Change GC Rank to {itemInfo.GCRankGroup}");
                    GrandCompanyExchange.Instance.ChangeRankGroup(itemInfo.GCRankGroup);
                    await Coroutine.Wait(2000, () => AgentGrandCompanyExchange.Instance.Rank == itemInfo.GCRankGroup);
                    await Coroutine.Sleep(500);
                }

                if (AgentGrandCompanyExchange.Instance.Category + 1 != (byte) itemInfo.Category)
                {
                    Log.Information($"Change ChangeItemGroup to {itemInfo.Category}");
                    GrandCompanyExchange.Instance.ChangeItemGroup((int) itemInfo.Category);
                    await Coroutine.Wait(3000, () => AgentGrandCompanyExchange.Instance.Category + 1 == (byte) itemInfo.Category);
                    await Coroutine.Sleep(500);
                }

                var item = Items.FirstOrDefault(i => i.ItemID == itemToBuy.ItemId);
                Log.Information($"Want to buy {DataManager.GetItem(item.ItemID).LocaleName()}");
                if (item.ItemID == 0)
                {
                    Log.Error($"Can't find item {(ActiveShopPtr + Offsets.GCArrayStart).ToString("X")}");
                    foreach (var item1 in Items)
                    {
                        Log.Error($"{item1}");
                    }

                    return false;
                }

                if (item.Cost > Core.Me.GCSeals())
                {
                    Log.Error($"Don't have enough seals for {item.Item.LocaleName()}");
                    continue;
                }

                var oldBagQty = item.InBag;
                var qtyCanBuy = Math.Min(itemToBuy.qty, CanAfford(item));
                Log.Information($"CanBuy {qtyCanBuy}");
                AgentGrandCompanyExchange.Instance.BuyItem(item.Index, qtyCanBuy);
                await Coroutine.Wait(5000, () => SelectYesno.IsOpen);
                if (SelectYesno.IsOpen)
                {
                    SelectYesno.Yes();
                    Log.Information($"Clicked Yes");
                    await Coroutine.Wait(5000, () => !SelectYesno.IsOpen);
                }

                await Coroutine.Wait(3000, () => Items.FirstOrDefault(i => i.ItemID == itemToBuy.ItemId).InBag != oldBagQty);
            }
            await Coroutine.Sleep(500);
            GrandCompanyExchange.Instance.Close();
            await Coroutine.Wait(5000, () => !GrandCompanyExchange.Instance.IsOpen);
            Core.Me.ClearTarget();
            await Coroutine.Sleep(500);
            return true;

            /*
            if (KnownItems.TryGetValue(ItemId, out var itemInfo2))
            {
                Log.Information($"Found Known item {ItemId}");
                return await BuyItem(ItemId, qty, itemInfo.Item1, itemInfo.Item2);
            }
            */

            return false;
        }

        public static async Task<int> BuyKnownItem(uint ItemId, int qty)
        {
            if (KnownItems.TryGetValue(ItemId, out var itemInfo))
            {
                Log.Information($"Found Known item {ItemId}");
                return await BuyItem(ItemId, qty, itemInfo.Item1, itemInfo.Item2);
            }

            return 0;
        }

        public static async Task<int> BuyItem(uint ItemId, int qty, int GCRankGroup, GCShopCategory Category)
        {
            if (!await OpenShop())
            {
                return 0;
            }

            if (AgentGrandCompanyExchange.Instance.Rank != GCRankGroup)
            {
                Log.Information($"Change GC Rank to {GCRankGroup}");
                GrandCompanyExchange.Instance.ChangeRankGroup(GCRankGroup);
                await Coroutine.Wait(2000, () => AgentGrandCompanyExchange.Instance.Rank == GCRankGroup);
                await Coroutine.Sleep(500);
            }

            if (AgentGrandCompanyExchange.Instance.Category + 1 != (byte) Category)
            {
                Log.Information($"Change ChangeItemGroup to {Category}");
                GrandCompanyExchange.Instance.ChangeItemGroup((int) Category);
                await Coroutine.Wait(3000, () => AgentGrandCompanyExchange.Instance.Category + 1 == (byte) Category);
                await Coroutine.Sleep(500);
            }

            return await BuyItem(ItemId, qty);
        }

        public static async Task<bool> OpenShop()
        {
            if (!GrandCompanyExchange.Instance.IsOpen)
            {
                Navigator.PlayerMover = new SlideMover();
                Navigator.NavigationProvider = new ServiceNavigationProvider();

                Log.Information("Calling interact with npc");
                await GrandCompanyHelper.InteractWithNpc(GCNpc.Quartermaster);

                if (!await Coroutine.Wait(5000, () => GrandCompanyExchange.Instance.IsOpen))
                {
                    await GrandCompanyHelper.InteractWithNpc(GCNpc.Quartermaster);
                    await Coroutine.Wait(5000, () => GrandCompanyExchange.Instance.IsOpen);
                }
            }

            return GrandCompanyExchange.Instance.IsOpen;
        }

        public static readonly Dictionary<uint, (byte GCRankGroup, GCShopCategory Category)> KnownItems = new Dictionary<uint, (byte GCRankGroup, GCShopCategory Category)>()
        {
            { 21072, (0, GCShopCategory.Materiel) },
            { 4564, (0, GCShopCategory.Materiel) },
            { 4566, (0, GCShopCategory.Materiel) },
            { 5594, (0, GCShopCategory.Materiel) },
            { 5595, (0, GCShopCategory.Materiel) },
            { 5596, (0, GCShopCategory.Materiel) },
            { 6017, (0, GCShopCategory.Materiel) },
            { 6018, (0, GCShopCategory.Materiel) },
            { 6019, (0, GCShopCategory.Materiel) },
            { 15855, (0, GCShopCategory.Materiel) },
            { 15856, (0, GCShopCategory.Materiel) },
            { 4567, (0, GCShopCategory.Materiel) },
            { 4568, (0, GCShopCategory.Materiel) },
            { 4632, (0, GCShopCategory.Materiel) },
            { 4633, (0, GCShopCategory.Materiel) },
            { 5597, (0, GCShopCategory.Materiel) },
            { 5598, (0, GCShopCategory.Materiel) },
            { 5357, (0, GCShopCategory.Materials) },
            { 5358, (0, GCShopCategory.Materials) },
            { 4563, (1, GCShopCategory.Materiel) },
            { 6026, (1, GCShopCategory.Materiel) },
            { 6027, (1, GCShopCategory.Materiel) },
            { 4634, (1, GCShopCategory.Materiel) },
            { 4635, (1, GCShopCategory.Materiel) },
            { 6141, (1, GCShopCategory.Materiel) },
            { 7059, (1, GCShopCategory.Materiel) },
            { 7060, (1, GCShopCategory.Materiel) },
            { 7621, (1, GCShopCategory.Materiel) },
            { 21800, (1, GCShopCategory.Materiel) },
            { 4636, (1, GCShopCategory.Materiel) },
            { 6028, (1, GCShopCategory.Materiel) },
            { 6471, (1, GCShopCategory.Materiel) },
            { 6555, (1, GCShopCategory.Materiel) },
            { 6527, (1, GCShopCategory.Materiel) },
            { 6547, (1, GCShopCategory.Materiel) },
            { 6540, (1, GCShopCategory.Materiel) },
            { 6558, (1, GCShopCategory.Materiel) },
            { 6658, (1, GCShopCategory.Materiel) },
            { 6662, (1, GCShopCategory.Materiel) },
            { 10386, (2, GCShopCategory.Materiel) },
            { 17837, (2, GCShopCategory.Materiel) },
            { 12858, (2, GCShopCategory.Materiel) },
            { 12854, (2, GCShopCategory.Materiel) },
            { 12849, (2, GCShopCategory.Materiel) },
            { 13743, (2, GCShopCategory.Materiel) },
            { 4715, (2, GCShopCategory.Materiel) },
            { 4720, (2, GCShopCategory.Materiel) },
            { 12847, (2, GCShopCategory.Materiel) },
            { 12844, (2, GCShopCategory.Materiel) },
            { 13595, (2, GCShopCategory.Materiel) },
            { 13591, (2, GCShopCategory.Materiel) },
            { 13589, (2, GCShopCategory.Materiel) },
            { 13593, (2, GCShopCategory.Materiel) },
            { 9539, (2, GCShopCategory.Materiel) },
            { 10124, (2, GCShopCategory.Materiel) },
            { 10121, (2, GCShopCategory.Materiel) },
            { 10120, (2, GCShopCategory.Materiel) },
            { 10122, (2, GCShopCategory.Materiel) },
            { 7799, (2, GCShopCategory.Materiel) },
            { 6172, (2, GCShopCategory.Materiel) },
            { 7135, (2, GCShopCategory.Materiel) },
            { 7145, (2, GCShopCategory.Materiel) },
            { 6600, (2, GCShopCategory.Materiel) },
            { 7096, (2, GCShopCategory.Materiel) },
            { 7152, (2, GCShopCategory.Materiel) },
            { 21319, (2, GCShopCategory.Materiel) },
            { 21320, (2, GCShopCategory.Materiel) },
            { 22498, (2, GCShopCategory.Materiel) },
            { 21071, (2, GCShopCategory.Materiel) },
            { 20790, (2, GCShopCategory.Materiel) },
            { 15772, (2, GCShopCategory.Materiel) },
            { 15773, (2, GCShopCategory.Materiel) },
            { 15774, (2, GCShopCategory.Materiel) },
            { 14945, (2, GCShopCategory.Materiel) },
            { 16933, (2, GCShopCategory.Materials) },
            { 15649, (2, GCShopCategory.Materials) },
            { 9356, (2, GCShopCategory.Materials) },
            { 9371, (2, GCShopCategory.Materials) },
            { 9372, (2, GCShopCategory.Materials) },
            { 9368, (2, GCShopCategory.Materials) },
            { 9367, (2, GCShopCategory.Materials) },
            { 9369, (2, GCShopCategory.Materials) },
            { 9370, (2, GCShopCategory.Materials) },
            { 9366, (2, GCShopCategory.Materials) },
            { 7605, (2, GCShopCategory.Materials) },
            { 7603, (2, GCShopCategory.Materials) },
            { 7604, (2, GCShopCategory.Materials) },
            { 7602, (2, GCShopCategory.Materials) },
            { 7597, (2, GCShopCategory.Materials) },
            { 7601, (2, GCShopCategory.Materials) },
            { 7596, (2, GCShopCategory.Materials) },
            { 7600, (2, GCShopCategory.Materials) },
            { 7599, (2, GCShopCategory.Materials) },
            { 7806, (2, GCShopCategory.Materials) },
            { 7598, (2, GCShopCategory.Materials) },
            { 6151, (2, GCShopCategory.Materials) },
            { 5119, (2, GCShopCategory.Materials) },
            { 5261, (2, GCShopCategory.Materials) },
            { 5274, (2, GCShopCategory.Materials) },
            { 6153, (2, GCShopCategory.Materials) },
            { 5501, (2, GCShopCategory.Materials) },
            { 5502, (2, GCShopCategory.Materials) },
            { 6154, (2, GCShopCategory.Materials) },
            { 5531, (2, GCShopCategory.Materials) },
            { 5532, (2, GCShopCategory.Materials) },
            { 5530, (2, GCShopCategory.Materials) },
            { 10112, (2, GCShopCategory.Materials) },
            { 10113, (2, GCShopCategory.Materials) },
            { 10114, (2, GCShopCategory.Materials) },
            { 10115, (2, GCShopCategory.Materials) },
            { 10116, (2, GCShopCategory.Materials) },
            { 10117, (2, GCShopCategory.Materials) },
            { 10118, (2, GCShopCategory.Materials) },
            { 10119, (2, GCShopCategory.Materials) },
            { 9357, (2, GCShopCategory.Materials) },
            { 36635, (2, GCShopCategory.Materiel) },
            { 36636, (2, GCShopCategory.Materiel) }
        };

        public static bool IsBuyableItem(uint itemId) => KnownItems.ContainsKey(itemId);
    }

    public enum GCShopCategory
    {
        Materiel = 1,
        Weapons = 2,
        Armor = 3,
        Materials = 4
    }
}
