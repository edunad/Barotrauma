﻿using Barotrauma.Networking;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Barotrauma
{
    partial class FixRequirement
    {
        private static GUIFrame frame;

        public bool CanBeFixed(Character character, GUIComponent reqFrame = null)
        {
            foreach (string itemName in requiredItems)
            {
                Item item = character.Inventory.FindItem(itemName);
                bool itemFound = (item != null);

                if (reqFrame != null)
                {
                    GUIComponent component = reqFrame.children.Find(c => c.UserData as string == itemName);
                    GUITextBlock text = component as GUITextBlock;
                    if (text != null) text.TextColor = itemFound ? Color.LightGreen : Color.Red;
                }
            }

            foreach (Skill skill in requiredSkills)
            {
                float characterSkill = character.GetSkillLevel(skill.Name);
                bool sufficientSkill = characterSkill >= skill.Level;

                if (reqFrame != null)
                {
                    GUIComponent component = reqFrame.children.Find(c => c.UserData as Skill == skill);
                    GUITextBlock text = component as GUITextBlock;
                    if (text != null) text.TextColor = sufficientSkill ? Color.LightGreen : Color.Red;
                }
            }

            return CanBeFixed(character);
        }

        private static void CreateGUIFrame(Item item)
        {
            int width = 400, height = 500;
            int y = 0;

            frame = new GUIFrame(new Rectangle(0, 0, width, height), null, Alignment.Center, "");
            frame.Padding = new Vector4(20.0f, 20.0f, 20.0f, 20.0f);
            frame.UserData = item;

            new GUITextBlock(new Rectangle(0, 0, 200, 20), TextManager.Get("FixHeader").Replace("[itemname]", item.Name), "", frame);

            y = y + 40;
            foreach (FixRequirement requirement in item.FixRequirements)
            {
                GUIFrame reqFrame = new GUIFrame(
                    new Rectangle(0, y, 0, 20 + Math.Max(requirement.requiredItems.Count, requirement.requiredSkills.Count) * 15),
                    Color.Transparent, null, frame);
                reqFrame.UserData = requirement;


                var fixButton = new GUIButton(new Rectangle(0, 0, 50, 20), TextManager.Get("FixButton"), "", reqFrame);
                fixButton.OnClicked = FixButtonPressed;
                fixButton.UserData = requirement;

                var tickBox = new GUITickBox(new Rectangle(70, 0, 20, 20), requirement.name, Alignment.Left, reqFrame);
                tickBox.Enabled = false;

                int y2 = 20;
                foreach (string itemName in requirement.requiredItems)
                {
                    var itemBlock = new GUITextBlock(new Rectangle(30, y2, 200, 15), itemName, "", reqFrame);
                    itemBlock.Font = GUI.SmallFont;
                    itemBlock.UserData = itemName;

                    y2 += 15;
                }

                y2 = 20;
                foreach (Skill skill in requirement.requiredSkills)
                {
                    var skillBlock = new GUITextBlock(new Rectangle(0, y2, 200, 15), skill.Name + " - " + skill.Level, "", Alignment.Right, Alignment.TopLeft, reqFrame);
                    skillBlock.Font = GUI.SmallFont;
                    skillBlock.UserData = skill;


                    y2 += 15;
                }

                y += reqFrame.Rect.Height;
            }
        }

        private static bool FixButtonPressed(GUIButton button, object obj)
        {
            FixRequirement requirement = obj as FixRequirement;
            if (requirement == null) return true;

            Item item = frame.UserData as Item;
            if (item == null) return true;

            if (!requirement.CanBeFixed(Character.Controlled, button.Parent)) return true;

            if (GameMain.Client != null)
            {
                GameMain.Client.CreateEntityEvent(item, new object[] { NetEntityEvent.Type.Repair, item.FixRequirements.IndexOf(requirement) });
            }
            else if (GameMain.Server != null)
            {
                GameMain.Server.CreateEntityEvent(item, new object[] { NetEntityEvent.Type.Status });
                requirement.Fixed = true;
            }
            else
            {
                requirement.Fixed = true;
            }

            return true;
        }

        private static void UpdateGUIFrame(Item item, Character character)
        {
            if (frame == null) return;

            bool unfixedFound = false;
            foreach (GUIComponent child in frame.children)
            {
                FixRequirement requirement = child.UserData as FixRequirement;
                if (requirement == null) continue;

                if (requirement.Fixed)
                {
                    child.Color = Color.LightGreen * 0.3f;
                    child.GetChild<GUITickBox>().Selected = true;
                }
                else
                {
                    bool canBeFixed = requirement.CanBeFixed(character, child);
                    unfixedFound = true;
                    //child.GetChild<GUITickBox>().Selected = canBeFixed;
                    GUITickBox tickBox = child.GetChild<GUITickBox>();
                    if (tickBox.Selected)
                    {
                        tickBox.Selected = canBeFixed;
                        requirement.Fixed = canBeFixed;

                    }
                    child.Color = Color.Red * 0.2f;
                    //tickBox.State = GUIComponent.ComponentState.None;
                }
            }
            if (!unfixedFound)
            {
                item.Condition = item.Prefab.Health;
                frame = null;
            }
        }

        public static void DrawHud(SpriteBatch spriteBatch, Item item, Character character)
        {
            if (frame == null) return;

            frame.Draw(spriteBatch);
        }

        public static void AddToGUIUpdateList()
        {
            if (frame == null) return;

            frame.AddToGUIUpdateList();
        }

        public static void UpdateHud(Item item, Character character)
        {
            if (frame == null || frame.UserData != item)
            {
                CreateGUIFrame(item);
            }
            UpdateGUIFrame(item, character);

            if (frame == null) return;

            frame.Update((float)Timing.Step);
        }
    }
}
