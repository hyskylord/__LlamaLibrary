﻿using System.Linq;
using System.Threading.Tasks;
using Buddy.Coroutines;
using ff14bot.Managers;
using ff14bot.RemoteWindows;
using LlamaLibrary.RemoteAgents;

namespace LlamaLibrary.RemoteWindows
{
    //TODO Move element numbers to dictionary
    public class HugeCraftworksSupply : RemoteWindow<HugeCraftworksSupply>
    {
        private const string WindowName = "HugeCraftworksSupply";

        public HugeCraftworksSupply() : base(WindowName)
        {
            _name = WindowName;
        }

        public int TurnInItemId => Elements[9].TrimmedData;

        public void Deliver()
        {
            SendAction(1, 3, 0);
        }

        public async Task HandOverItems()
        {
            var slots = InventoryManager.FilledSlots.Where(i => i.RawItemId == TurnInItemId).OrderByDescending(i => i.HqFlag);

            foreach (var slot in slots)
            {
                if (slot != null)
                {
                    AgentHandIn.Instance.HandIn(slot);

                    //await Coroutine.Sleep(500);
                    await Coroutine.Wait(5000, () => InputNumeric.IsOpen);
                    if (InputNumeric.IsOpen)
                    {
                        InputNumeric.Ok((uint)InputNumeric.Field.CurrentValue);
                    }

                    await Coroutine.Sleep(700);
                }
            }

            await Coroutine.Sleep(500);

            Deliver();

            await Coroutine.Wait(5000, () => Talk.DialogOpen || SelectYesno.IsOpen);

            while (Talk.DialogOpen)
            {
                Talk.Next();
                await Coroutine.Sleep(1000);
            }

            if (SelectYesno.IsOpen)
            {
                SelectYesno.Yes();
            }

            await Coroutine.Wait(5000, () => Talk.DialogOpen || !IsOpen);

            while (Talk.DialogOpen)
            {
                Talk.Next();
                await Coroutine.Sleep(1000);
            }

            await Coroutine.Wait(5000, () => !IsOpen);
        }
    }
}