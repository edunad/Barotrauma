﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Barotrauma.Items.Components
{
    partial class PowerTransfer : Powered
    {
        public override void DrawHUD(SpriteBatch spriteBatch, Character character)
        {
            if (!canBeSelected) return;

            int x = GuiFrame.Rect.X;
            int y = GuiFrame.Rect.Y;

            GuiFrame.Draw(spriteBatch);

            GUI.Font.DrawString(spriteBatch, 
                TextManager.Get("PowerTransferPower").Replace("[power]", ((int)(-currPowerConsumption)).ToString()), 
                new Vector2(x + 30, y + 30), Color.White);

            GUI.Font.DrawString(spriteBatch,
                 TextManager.Get("PowerTransferLoad").Replace("[load]", ((int)powerLoad).ToString()),
                new Vector2(x + 30, y + 100),  Color.White);
        }

        public override void AddToGUIUpdateList()
        {
            GuiFrame.AddToGUIUpdateList();
        }

        public override void UpdateHUD(Character character)
        {
            GuiFrame.Update(1.0f / 60.0f);
        }
    }
}
