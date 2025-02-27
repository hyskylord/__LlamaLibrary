﻿using ff14bot.Managers;

namespace LlamaLibrary.Extensions
{
    public static class RetainerTaskAskExtensions
    {


        public static bool CanAssign()
        {
            var WindowByName = RaptureAtkUnitManager.GetWindowByName("RetainerTaskAsk");
            if (WindowByName == null)
            {
                return false;
            }

            var remoteButton = WindowByName.FindButton(40);
            return remoteButton != null && remoteButton.Clickable;
        }

        public static string GetErrorReason()
        {
            var WindowByName = RaptureAtkUnitManager.GetWindowByName("RetainerTaskAsk");
            if (WindowByName == null || WindowByName.FindLabel(39) == null)
            {
                return "";
            }

            return WindowByName.FindLabel(39).Text;
        }
    }
}