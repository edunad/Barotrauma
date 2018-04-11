﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace Barotrauma
{
    abstract class AIObjective
    {
        protected readonly List<AIObjective> subObjectives;
        protected float priority;
        protected readonly Character character;
        protected string option;

        public virtual bool CanBeCompleted
        {
            get { return true; }
        }

        public string Option
        {
            get { return option; }
        }            

        public AIObjective(Character character, string option)
        {
            subObjectives = new List<AIObjective>();
            this.character = character;
            this.option = option;

#if DEBUG
            IsDuplicate(null);
#endif
        }

        /// <summary>
        /// makes the character act according to the objective, or according to any subobjectives that
        /// need to be completed before this one
        /// </summary>
        public void TryComplete(float deltaTime)
        {
            subObjectives.RemoveAll(s => s.IsCompleted() || !s.CanBeCompleted);

            foreach (AIObjective objective in subObjectives)
            {
                objective.TryComplete(deltaTime);
                return;
            }

            Act(deltaTime);
        }

        public void AddSubObjective(AIObjective objective)
        {
            if (subObjectives.Any(o => o.IsDuplicate(objective))) return;

            subObjectives.Add(objective);
        }

        public AIObjective GetCurrentSubObjective()
        {
            AIObjective currentSubObjective = this;
            while (currentSubObjective.subObjectives.Count > 0)
            {
                currentSubObjective = subObjectives[0];
            }
            return currentSubObjective;
        }

        protected abstract void Act(float deltaTime);
        
        public abstract bool IsCompleted();
        public abstract float GetPriority(AIObjectiveManager objectiveManager);
        public abstract bool IsDuplicate(AIObjective otherObjective);
    }
}
