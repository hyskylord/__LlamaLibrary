﻿using System;
using ff14bot.Managers;
using LlamaLibrary.Memory.Attributes;

namespace LlamaLibrary.RemoteAgents
{
    public class AgentHousing : AgentInterface<AgentHousing>, IAgent
    {
        public IntPtr RegisteredVtable => Offsets.VTable;

        private static class Offsets
        {
            [Offset("48 8D 05 ? ? ? ? 48 89 07 48 8D 4F ? 48 8D 05 ? ? ? ? 48 8B D3 Add 3 TraceRelative")]
            internal static IntPtr VTable;
        }

        protected AgentHousing(IntPtr pointer) : base(pointer)
        {
        }
    }
}