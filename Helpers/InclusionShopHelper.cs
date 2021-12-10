﻿using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using Buddy.Coroutines;
using ff14bot.Managers;
using LlamaLibrary.Logging;
using LlamaLibrary.RemoteAgents;
using LlamaLibrary.RemoteWindows;

namespace LlamaLibrary.Helpers
{
    public static class InclusionShopHelper
    {
        private static readonly LLogger Log = new LLogger("InclusionShopHelper", Colors.Moccasin);

        public static async Task<int> BuyItem(uint itemId, int qty)
        {
            if (!LlamaLibrary.RemoteWindows.InclusionShop.Instance.IsOpen)
            {
                Log.Information("InclusionShop window not open");
                return 0;
            }

            var shopItems = AgentInclusionShop.Instance.ShopItems;

            if (!shopItems.Any(i => i.RawItemIds.Contains(itemId)))
            {
                Log.Information($"Item {itemId} not found");
                return 0;
            }

            var shopItem = shopItems.First(i => i.RawItemIds.Contains(itemId));

            if (shopItem.Hidden)
            {
                Log.Information(
                    $"Item {DataManager.GetItem(shopItem.ItemId).CurrentLocaleName} is in a hidden sub category");
                return 0;
            }

            int amtToBuy;

            amtToBuy = qty == -1 ? shopItem.CanAffordQty() : Math.Min(qty, shopItem.CanAffordQty());

            if (amtToBuy == 0)
            {
                var costs = shopItem.Costs.Select(cost =>
                    $"{DataManager.GetItem(cost.ItemId).CurrentLocaleName} x {cost.Qty}");
                Log.Information($"You can't afford {string.Join(",", costs)}");
                return 0;
            }

            LlamaLibrary.RemoteWindows.InclusionShop.Instance.SetCategory(shopItem.Category);

            await Coroutine.Wait(10000, () => AgentInclusionShop.Instance.SelectedCategory == shopItem.Category);

            if (AgentInclusionShop.Instance.SelectedCategory != shopItem.Category)
            {
                Log.Information("Couldn't change category");
                return 0;
            }

            LlamaLibrary.RemoteWindows.InclusionShop.Instance.SetSubCategory(shopItem.SubCategory + 1);

            await Coroutine.Wait(10000, () => AgentInclusionShop.Instance.SelectedSubCategory == shopItem.SubCategory);

            if (AgentInclusionShop.Instance.SelectedSubCategory != shopItem.SubCategory)
            {
                Log.Information("Couldn't change subcategory");
                return 0;
            }

            await Coroutine.Wait(10000, () => AgentInclusionShop.Instance.ItemCount >= shopItem.Index);

            LlamaLibrary.RemoteWindows.InclusionShop.Instance.BuyItem(shopItem.Index, qty);

            await Coroutine.Wait(10000, () => ShopExchangeItemDialog.Instance.IsOpen);

            if (!ShopExchangeItemDialog.Instance.IsOpen)
            {
                Log.Information("ShopExchangeItemDialog didn't open");
                return 0;
            }

            var currentAmt = InventoryManager.FilledSlots.Where(i => i.RawItemId == shopItem.ItemId).Sum(i => i.Count);

            ShopExchangeItemDialog.Instance.Exchange();

            await Coroutine.Wait(10000, () => !ShopExchangeItemDialog.Instance.IsOpen);

            await Coroutine.Wait(
                10000,
                () => currentAmt != InventoryManager.FilledSlots.Where(i => i.RawItemId == shopItem.ItemId)
                    .Sum(i => i.Count));

            await Coroutine.Sleep(100);

            if (InclusionShop.Instance.IsOpen)
            {
                InclusionShop.Instance.Close();
                await Coroutine.Wait(10000, () => !ShopExchangeItemDialog.Instance.IsOpen);
            }

            return (int)(InventoryManager.FilledSlots.Where(i => i.RawItemId == shopItem.ItemId).Sum(i => i.Count) -
                          currentAmt);
        }

        public static async Task<int> BuyItemGoToNpc(uint itemId, int qty)
        {
            var shopIds = InclusionShopConstants.KnownItems.Where(i => i.Value.Contains(itemId)).Select(i => i.Key);

            var npcs = InclusionShopConstants.ShopNpcs.Where(i => shopIds.Contains(i.ShopKey)).Where(i => i.RequiredQuest == 0 || QuestLogManager.IsQuestCompleted(i.RequiredQuest));

            var npcToGoTo = npcs.OrderBy(j => WorldManager.AvailableLocations.First(i => i.ZoneId == j.ZoneId).GilCost)
                .First();

            await Navigation.GetToInteractNpcSelectString(npcToGoTo.NpcId, npcToGoTo.ZoneId, npcToGoTo.Location, 0, LlamaLibrary.RemoteWindows.InclusionShop.Instance);

            return await BuyItem(itemId, qty);
        }
    }
}