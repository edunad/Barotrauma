﻿using Barotrauma.Items.Components;
using Barotrauma.Networking;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Barotrauma
{
    class CharacterHUD
    {
        private static Sprite statusIcons;

        private static Sprite noiseOverlay, damageOverlay;

        private static GUIButton cprButton;
        
        private static GUIButton grabHoldButton;

        private static GUIButton suicideButton;

        private static GUIProgressBar oxygenBar, healthBar;

        private static bool oxyMsgShown, pressureMsgShown;
        private static float pressureMsgTimer;

        public static float damageOverlayTimer { get; private set; }

        public static void Reset()
        {
            damageOverlayTimer = 0.0f;
        }

        public static void TakeDamage(float amount)
        {
            healthBar.Flash();

            damageOverlayTimer = MathHelper.Clamp(amount * 0.1f, 0.2f, 1.0f);
        }

        public static void AddToGUIUpdateList(Character character)
        {
            if (GUI.DisableHUD) return;

            if (cprButton != null && cprButton.Visible) cprButton.AddToGUIUpdateList();

            if (grabHoldButton != null && cprButton.Visible) grabHoldButton.AddToGUIUpdateList();

            if (suicideButton != null && suicideButton.Visible) suicideButton.AddToGUIUpdateList();
            
            if (!character.IsUnconscious && character.Stun <= 0.0f)
            {

                if (character.Inventory != null)
                {
                    for (int i = 0; i < character.Inventory.Items.Length - 1; i++)
                    {
                        var item = character.Inventory.Items[i];
                        if (item == null || CharacterInventory.limbSlots[i] == InvSlotType.Any) continue;

                        foreach (ItemComponent ic in item.components)
                        {
                            if (ic.DrawHudWhenEquipped) ic.AddToGUIUpdateList();
                        }
                    }
                }
            }
        }

        public static void Update(float deltaTime, Character character)
        {
            if (!pressureMsgShown)
            {
                float pressureFactor = (character.AnimController.CurrentHull == null) ?
                    100.0f : Math.Min(character.AnimController.CurrentHull.LethalPressure, 100.0f);
                if (character.PressureProtection > 0.0f) pressureFactor = 0.0f;

                if (pressureFactor > 0.0f)
                {
                    pressureMsgTimer += deltaTime;
                }
                else
                {
                    pressureMsgTimer = 0.0f;
                }
            }

            if (oxygenBar != null)
            {
                oxygenBar.Update(deltaTime);
                if (character.Oxygen < 10.0f) oxygenBar.Flash();
            }
            if (healthBar != null) healthBar.Update(deltaTime);
            
            if (Inventory.SelectedSlot == null)
            {
                if (cprButton != null && cprButton.Visible) cprButton.Update(deltaTime);
                if (grabHoldButton != null && grabHoldButton.Visible) grabHoldButton.Update(deltaTime);
            }

            if (suicideButton != null && suicideButton.Visible) suicideButton.Update(deltaTime);

            if (damageOverlayTimer > 0.0f) damageOverlayTimer -= deltaTime;

            if (!character.IsUnconscious && character.Stun <= 0.0f)
            {
                if (character.Inventory != null)
                {
                    if (!character.LockHands && character.Stun >= -0.1f)
                    {
                        character.Inventory.Update(deltaTime);
                    }

                    for (int i = 0; i < character.Inventory.Items.Length - 1; i++)
                    {
                        var item = character.Inventory.Items[i];
                        if (item == null || CharacterInventory.limbSlots[i] == InvSlotType.Any) continue;

                        foreach (ItemComponent ic in item.components)
                        {
                            if (ic.DrawHudWhenEquipped) ic.UpdateHUD(character);
                        }
                    }
                }

                if (character.IsHumanoid && character.SelectedCharacter != null && character.SelectedCharacter.Inventory != null)
                {
                    character.SelectedCharacter.Inventory.Update(deltaTime);
                }

                Inventory.UpdateDragging();
            }
        }

        public static void Draw(SpriteBatch spriteBatch, Character character, Camera cam)
        {
            if (statusIcons == null)
            {
                statusIcons = new Sprite("Content/UI/statusIcons.png", Vector2.Zero);
            }

            if (noiseOverlay == null)
            {
                noiseOverlay = new Sprite("Content/UI/noise.png", Vector2.Zero);
            }

            if (damageOverlay == null)
            {
                damageOverlay = new Sprite("Content/UI/damageOverlay.png", Vector2.Zero);
            }

            if (GUI.DisableHUD) return;

            if (character.Inventory != null)
            {
                for (int i = 0; i < character.Inventory.Items.Length - 1; i++)
                {
                    var item = character.Inventory.Items[i];
                    if (item == null || CharacterInventory.limbSlots[i] == InvSlotType.Any) continue;

                    foreach (ItemComponent ic in item.components)
                    {
                        if (ic.DrawHudWhenEquipped) ic.DrawHUD(spriteBatch, character);
                    }
                }
            }

            DrawStatusIcons(spriteBatch, character);

            if (!character.IsUnconscious && character.Stun <= 0.0f)
            {
                if (character.IsHumanoid && character.SelectedCharacter != null && character.SelectedCharacter.Inventory != null)
                {
                    if (cprButton == null)
                    {
                        cprButton = new GUIButton(
                            new Rectangle(character.SelectedCharacter.Inventory.SlotPositions[0].ToPoint() + new Point(320, -30), new Point(130, 20)), "Perform CPR", "");

                        cprButton.OnClicked = (button, userData) =>
                        {
                            if (Character.Controlled == null || Character.Controlled.SelectedCharacter == null) return false;

                            Character.Controlled.AnimController.Anim = (Character.Controlled.AnimController.Anim == AnimController.Animation.CPR) ?
                                AnimController.Animation.None : AnimController.Animation.CPR;

                            foreach (Limb limb in Character.Controlled.SelectedCharacter.AnimController.Limbs)
                            {
                                limb.pullJoint.Enabled = false;
                            }
                            
                            if (GameMain.Client != null)
                            {
                                GameMain.Client.CreateEntityEvent(Character.Controlled, new object[] { NetEntityEvent.Type.Repair });
                            }
                            
                            return true;
                        };
                    }

                    if (grabHoldButton == null)
                    {
                        grabHoldButton = new GUIButton(
                            new Rectangle(character.SelectedCharacter.Inventory.SlotPositions[0].ToPoint() + new Point(320, -60), new Point(130, 20)),
                                TextManager.Get("Grabbing") + ": " + TextManager.Get(character.AnimController.GrabLimb == LimbType.None ? "Hands" : character.AnimController.GrabLimb.ToString()), "");

                        grabHoldButton.OnClicked = (button, userData) =>
                        {
                            if (Character.Controlled == null || Character.Controlled.SelectedCharacter == null) return false;

                            Character.Controlled.AnimController.GrabLimb = Character.Controlled.AnimController.GrabLimb == LimbType.None ? LimbType.Torso : LimbType.None;

                            foreach (Limb limb in Character.Controlled.SelectedCharacter.AnimController.Limbs)
                            {
                                limb.pullJoint.Enabled = false;
                            }

                            if (GameMain.Client != null)
                            {
                                GameMain.Client.CreateEntityEvent(Character.Controlled, new object[] { NetEntityEvent.Type.Control });
                            }

                            grabHoldButton.Text = TextManager.Get("Grabbing") + ": " + TextManager.Get(character.AnimController.GrabLimb == LimbType.None ? "Hands" : character.AnimController.GrabLimb.ToString());
                            return true;
                        };
                    }
                    
                    if (cprButton.Visible) cprButton.Draw(spriteBatch);
                    if (grabHoldButton.Visible) grabHoldButton.Draw(spriteBatch);

                    character.SelectedCharacter.Inventory.DrawOffset = new Vector2(320.0f, 0.0f);
                    character.SelectedCharacter.Inventory.DrawOwn(spriteBatch);
                }

                if (character.Inventory != null && !character.LockHands && character.Stun >= -0.1f)
                {
                    character.Inventory.DrawOffset = Vector2.Zero;
                    character.Inventory.DrawOwn(spriteBatch);
                }
                if (character.Inventory != null && !character.LockHands && character.Stun >= -0.1f)
                {
                    Inventory.DrawDragging(spriteBatch);
                }

                if (character.FocusedCharacter != null && character.FocusedCharacter.CanBeSelected)
                {
                    Vector2 startPos = character.DrawPosition + (character.FocusedCharacter.DrawPosition - character.DrawPosition) * 0.7f;
                    startPos = cam.WorldToScreen(startPos);

                    string focusName = character.FocusedCharacter.SpeciesName;
                    if (character.FocusedCharacter.Info != null)
                    {
                        focusName = character.FocusedCharacter.Info.DisplayName;
                    }
                    Vector2 textPos = startPos;
                    textPos -= new Vector2(GUI.Font.MeasureString(focusName).X / 2, 20);

                    GUI.DrawString(spriteBatch, textPos, focusName, Color.White, Color.Black, 2);
                }
                else if (character.SelectedCharacter == null && character.FocusedItem != null && character.SelectedConstruction == null)
                {
                    var hudTexts = character.FocusedItem.GetHUDTexts(character);

                    Vector2 startPos = new Vector2((int)(GameMain.GraphicsWidth / 2.0f), GameMain.GraphicsHeight);
                    startPos.Y -= 50 + hudTexts.Count * 25;

                    Vector2 textPos = startPos;
                    textPos -= new Vector2((int)GUI.Font.MeasureString(character.FocusedItem.Name).X / 2, 20);

                    GUI.DrawString(spriteBatch, textPos, character.FocusedItem.Name, Color.White, Color.Black * 0.7f, 2);

                    textPos.Y += 30.0f;
                    foreach (ColoredText coloredText in hudTexts)
                    {
                        textPos.X = (int)(startPos.X - GUI.SmallFont.MeasureString(coloredText.Text).X / 2);

                        GUI.DrawString(spriteBatch, textPos, coloredText.Text, coloredText.Color, Color.Black * 0.7f, 2, GUI.SmallFont);

                        textPos.Y += 25;
                    }
                }
                
                foreach (HUDProgressBar progressBar in character.HUDProgressBars.Values)
                {
                    progressBar.Draw(spriteBatch, cam);
                }
            }

            if (Screen.Selected == GameMain.SubEditorScreen) return;

            if (character.IsUnconscious || (character.Oxygen < 80.0f && !character.IsDead))
            {
                Vector2 offset = Rand.Vector(noiseOverlay.size.X);
                offset.X = Math.Abs(offset.X);
                offset.Y = Math.Abs(offset.Y);

                float alpha = character.IsUnconscious ? 1.0f : Math.Min((80.0f - character.Oxygen)/50.0f, 0.8f);

                noiseOverlay.DrawTiled(spriteBatch, Vector2.Zero - offset, new Vector2(GameMain.GraphicsWidth, GameMain.GraphicsHeight) + offset,
                    color: Color.White * alpha);

            }
            else
            {
                if (suicideButton != null) suicideButton.Visible = false;
            }

            if (damageOverlayTimer>0.0f)
            {
                damageOverlay.Draw(spriteBatch, Vector2.Zero, Color.White * damageOverlayTimer, Vector2.Zero, 0.0f,
                    new Vector2(GameMain.GraphicsWidth / damageOverlay.size.X, GameMain.GraphicsHeight / damageOverlay.size.Y));
            }

            if (character.IsUnconscious && !character.IsDead)
            {
                if (suicideButton == null)
                {
                    suicideButton = new GUIButton(
                        new Rectangle(new Point(GameMain.GraphicsWidth / 2 - 60, 20), new Point(120, 20)), TextManager.Get("GiveInButton"), "");


                    suicideButton.ToolTip = TextManager.Get(GameMain.NetworkMember == null ? "GiveInHelpSingleplayer" : "GiveInHelpMultiplayer");

                    suicideButton.OnClicked = (button, userData) =>
                    {
                        GUIComponent.ForceMouseOn(null);
                        if (Character.Controlled != null)
                        {
                            if (GameMain.Client != null)
                            {
                                GameMain.Client.CreateEntityEvent(Character.Controlled, new object[] { NetEntityEvent.Type.Status });
                            }
                            else
                            {
                                Character.Controlled.Kill(Character.Controlled.CauseOfDeath);
                                Character.Controlled = null;
                            }
                        }
                        return true;
                    };
                }

                suicideButton.Visible = true;
                suicideButton.Draw(spriteBatch);                
            }
        }

        private static void DrawStatusIcons(SpriteBatch spriteBatch, Character character)
        {
            if (GameMain.DebugDraw)
            {
                GUI.DrawString(spriteBatch, new Vector2(30, GameMain.GraphicsHeight - 260), TextManager.Get("Stun") + ": " + character.Stun, Color.White);
            }

            if (oxygenBar == null)
            {
                int width = 100, height = 20;

                oxygenBar = new GUIProgressBar(new Rectangle(30, GameMain.GraphicsHeight - 200, width, height), Color.Blue, "", 1.0f, Alignment.TopLeft);
                new GUIImage(new Rectangle(-27, -7, 20, 20), new Rectangle(17, 0, 20, 24), statusIcons, Alignment.TopLeft, oxygenBar);

                healthBar = new GUIProgressBar(new Rectangle(30, GameMain.GraphicsHeight - 230, width, height), Color.Red, "", 1.0f, Alignment.TopLeft);
                new GUIImage(new Rectangle(-26, -7, 20, 20), new Rectangle(0, 0, 13, 24), statusIcons, Alignment.TopLeft, healthBar);
            }

            oxygenBar.BarSize = character.Oxygen / 100.0f;
            if (oxygenBar.BarSize < 0.99f)
            {
                oxygenBar.Draw(spriteBatch);
                if (!oxyMsgShown)
                {
                    GUI.AddMessage(TextManager.Get("OxygenBarInfo"), new Vector2(oxygenBar.Rect.Right + 10, oxygenBar.Rect.Y), Alignment.Left, Color.White, 5.0f);
                    oxyMsgShown = true;
                }
            }

            healthBar.BarSize = character.Health / character.MaxHealth;
            if (healthBar.BarSize < 1.0f)
            {
                healthBar.Draw(spriteBatch);
            }

            float bloodDropCount = character.Bleeding;
            bloodDropCount = MathHelper.Clamp(bloodDropCount, 0.0f, 5.0f);
            for (int i = 0; i < Math.Ceiling(bloodDropCount); i++)
            {
                float alpha = MathHelper.Clamp(bloodDropCount-i, 0.2f, 1.0f);
                spriteBatch.Draw(statusIcons.Texture, new Vector2(25.0f + 20 * i, healthBar.Rect.Y - 20.0f), new Rectangle(39, 3, 15, 19), Color.White * alpha);
            }

            float pressureFactor = (character.AnimController.CurrentHull == null) ?
                100.0f : Math.Min(character.AnimController.CurrentHull.LethalPressure, 100.0f);
            if (character.PressureProtection > 0.0f) pressureFactor = 0.0f;

            if (pressureFactor > 0.0f)
            {
                float indicatorAlpha = ((float)Math.Sin(character.PressureTimer * 0.1f) + 1.0f) * 0.5f;
                indicatorAlpha = MathHelper.Clamp(indicatorAlpha, 0.1f, pressureFactor / 100.0f);
                
                if (pressureMsgTimer > 0.5f && !pressureMsgShown)
                {
                    GUI.AddMessage(TextManager.Get("PressureInfo"), new Vector2(40.0f, healthBar.Rect.Y - 60.0f), Alignment.Left, Color.White, 5.0f);
                    pressureMsgShown = true;                    
                }

                spriteBatch.Draw(statusIcons.Texture, new Vector2(10.0f, healthBar.Rect.Y - 60.0f), new Rectangle(0, 24, 24, 25), Color.White * indicatorAlpha);            
            }
        }
    }
}
