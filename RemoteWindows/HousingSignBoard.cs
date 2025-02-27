﻿using System;
using System.Text;
using ff14bot;

namespace LlamaLibrary.RemoteWindows
{
    //TODO Move element numbers to dictionary
    public class HousingSignBoard : RemoteWindow<HousingSignBoard>
    {
        private const string WindowName = "HousingSignBoard";

        public HousingSignBoard() : base(WindowName)
        {
            _name = WindowName;
        }

        public bool IsForSale => Core.Memory.ReadString((IntPtr)Elements[1].Data, Encoding.UTF8).Contains("Sale");

        public void ClickBuy()
        {
            SendAction(1, 3, 1);
        }
    }
}