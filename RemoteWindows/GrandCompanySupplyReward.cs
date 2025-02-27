﻿namespace LlamaLibrary.RemoteWindows
{
    public class GrandCompanySupplyReward : RemoteWindow<GrandCompanySupplyReward>
    {
        private const string WindowName = "GrandCompanySupplyReward";

        public int SealReward => Elements[9].TrimmedData;

        public GrandCompanySupplyReward() : base(WindowName)
        {
            _name = WindowName;
        }

        public void Confirm()
        {
            SendAction(1, 3, 0);
        }
    }
}