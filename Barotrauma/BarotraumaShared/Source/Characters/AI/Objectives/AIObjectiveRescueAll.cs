﻿using System.Collections.Generic;
using System.Linq;

namespace Barotrauma
{
    /*class AIObjectiveRescueAll : AIObjective
    {
        private List<Character> rescueTargets;
        
        public AIObjectiveRescueAll(Character character)
            : base (character, "")
        {
            rescueTargets = new List<Character>();
        }

        public override bool IsDuplicate(AIObjective otherObjective)
        {
            return true;
        }

        public override float GetPriority(Character character)
        {
            GetRescueTargets();

            //if there are targets to rescue, the priority is slightly less 
            //than the priority of explicit orders given to the character
            return rescueTargets.Any() ? AIObjectiveManager.OrderPriority - 5.0f : 0.0f;
        }

        private void GetRescueTargets()
        {
            rescueTargets = Character.CharacterList.FindAll(c => 
                c.AIController is HumanAIController &&
                c != character &&
                (c.IsDead || c.IsUnconscious) &&
                c.AnimController.CurrentHull != null &&
                AIObjectiveFindSafety.GetHullSafety(c.AnimController.CurrentHull, c) < 50.0f);
        }

        protected override void Act(float deltaTime)
        {
            foreach (Character target in rescueTargets)
            {
                AddSubObjective(new AIObjectiveRescue(character, target));
            }
        }
    }*/
}
