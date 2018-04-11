﻿using Barotrauma.Items.Components;
using FarseerPhysics;
using Microsoft.Xna.Framework;
using System;

namespace Barotrauma
{
    class AIObjectiveFixLeak : AIObjective
    {
        private readonly Gap leak;

        private AIObjectiveGoTo gotoObjective;

        public Gap Leak
        {
            get { return leak; }
        }

        public AIObjectiveFixLeak(Gap leak, Character character)
            : base (character, "")
        {
            this.leak = leak;
        }

        public override bool IsCompleted()
        {
            return leak.Open <= 0.0f || leak.Removed;
        }

        public override float GetPriority(AIObjectiveManager objectiveManager)
        {
            if (leak.Open == 0.0f) return 0.0f;

            float leakSize = (leak.IsHorizontal ? leak.Rect.Height : leak.Rect.Width) * Math.Max(leak.Open, 0.1f);

            float dist = Vector2.DistanceSquared(character.SimPosition, leak.SimPosition);
            dist = Math.Max(dist / 100.0f, 1.0f);
            return Math.Min(leakSize / dist, 40.0f);
        }

        public override bool IsDuplicate(AIObjective otherObjective)
        {
            AIObjectiveFixLeak fixLeak = otherObjective as AIObjectiveFixLeak;
            if (fixLeak == null) return false;
            return fixLeak.leak == leak;
        }

        protected override void Act(float deltaTime)
        {
            var weldingTool = character.Inventory.FindItem("Welding Tool");

            if (weldingTool == null)
            {
                AddSubObjective(new AIObjectiveGetItem(character, "Welding Tool", true));
                return;
            }
            else
            {
                var containedItems = weldingTool.ContainedItems;
                if (containedItems == null) return;
                
                var fuelTank = Array.Find(containedItems, i => i.Prefab.NameMatches("Welding Fuel Tank") && i.Condition > 0.0f);

                if (fuelTank == null)
                {
                    AddSubObjective(new AIObjectiveContainItem(character, "Welding Fuel Tank", weldingTool.GetComponent<ItemContainer>()));
                    return;
                }
            }

            var repairTool = weldingTool.GetComponent<RepairTool>();
            if (repairTool == null) return;

            Vector2 standPosition = GetStandPosition();

            if (Vector2.DistanceSquared(character.WorldPosition, leak.WorldPosition) > 100.0f * 100.0f)
            {
                var gotoObjective = new AIObjectiveGoTo(ConvertUnits.ToSimUnits(standPosition), character);
                if (!gotoObjective.IsCompleted())
                {
                    AddSubObjective(gotoObjective);
                    return;
                }
            }

            AddSubObjective(new AIObjectiveOperateItem(repairTool, character, "", true, leak));                       
        }

        private Vector2 GetStandPosition()
        {
            Vector2 standPos = leak.Position;
            var hull = leak.FlowTargetHull;

            if (hull == null) return standPos;
            
            if (leak.IsHorizontal)
            {
                standPos += Vector2.UnitX * Math.Sign(hull.Position.X - leak.Position.X) * leak.Rect.Width;
            }
            else
            {
                standPos += Vector2.UnitY * Math.Sign(hull.Position.Y - leak.Position.Y) * leak.Rect.Height;
            }

            return standPos;            
        }
    }
}
