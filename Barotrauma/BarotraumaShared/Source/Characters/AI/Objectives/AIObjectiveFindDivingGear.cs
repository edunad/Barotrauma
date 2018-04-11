﻿using Barotrauma.Items.Components;
using System;

namespace Barotrauma
{
    class AIObjectiveFindDivingGear : AIObjective
    {
        private AIObjective subObjective;

        private string gearName;

        public override bool IsCompleted()
        {
            for (int i = 0; i < character.Inventory.Items.Length; i++)
            {
                if (CharacterInventory.limbSlots[i] == InvSlotType.Any || character.Inventory.Items[i] == null) continue;
                if (character.Inventory.Items[i].Prefab.NameMatches(gearName) || character.Inventory.Items[i].HasTag(gearName))
                {
                    var containedItems = character.Inventory.Items[i].ContainedItems;
                    if (containedItems == null) continue;

                    var oxygenTank = Array.Find(containedItems, it => (it.Prefab.NameMatches("Oxygen Tank") || it.HasTag("oxygensource")) && it.Condition > 0.0f);
                    if (oxygenTank != null) return true;
                }
            }

            return false;
        }

        public AIObjectiveFindDivingGear(Character character, bool needDivingSuit)
            : base(character, "")
        {
            gearName = needDivingSuit ? "Diving Suit" : "diving";
        }

        protected override void Act(float deltaTime)
        {
            var item = character.Inventory.FindItem(gearName);
            if (item == null)
            {
                //get a diving mask/suit first
                if (!(subObjective is AIObjectiveGetItem))
                {
                    subObjective = new AIObjectiveGetItem(character, gearName, true);
                }
            }
            else
            {
                var containedItems = item.ContainedItems;
                if (containedItems == null) return;

                //check if there's an oxygen tank in the mask/suit
                foreach (Item containedItem in containedItems)
                {
                    if (containedItem == null) continue;
                    if (containedItem.Condition <= 0.0f)
                    {
                        containedItem.Drop();
                    }
                    else if (containedItem.Prefab.NameMatches("Oxygen Tank") || containedItem.HasTag("oxygensource"))
                    {
                        //we've got an oxygen source inside the mask/suit, all good
                        return;
                    }
                }
                
                if (!(subObjective is AIObjectiveContainItem) || subObjective.IsCompleted())
                {
                    subObjective = new AIObjectiveContainItem(character, new string[] { "Oxygen Tank", "oxygensource" }, item.GetComponent<ItemContainer>());
                }
            }

            if (subObjective != null)
            {
                subObjective.TryComplete(deltaTime);
            }
        }

        public override float GetPriority(AIObjectiveManager objectiveManager)
        {
            if (character.AnimController.CurrentHull == null) return 100.0f;

            if (objectiveManager.CurrentOrder == this)
            {
                return AIObjectiveManager.OrderPriority;
            }

            return 100.0f - character.Oxygen;
        }

        public override bool IsDuplicate(AIObjective otherObjective)
        {
            return otherObjective is AIObjectiveFindDivingGear;
        }
    }
}
