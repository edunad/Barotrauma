﻿using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Barotrauma
{
    class AIObjectiveCombat : AIObjective
    {
        const float CoolDown = 10.0f;

        //the largest amount of damage the enemy has inflicted on this character
        //(may be higher than enemyStrength if the enemy is e.g. a human using items)
        public float MaxEnemyDamage;

        private Character enemy;

        private AIObjectiveFindSafety escapeObjective;

        float coolDownTimer;

        private readonly float enemyStrength;

        public AIObjectiveCombat(Character character, Character enemy)
            : base(character, "")
        {
            this.enemy = enemy;

            foreach (Limb limb in enemy.AnimController.Limbs)
            {
                if (limb.attack == null) continue;
                enemyStrength += limb.attack.GetDamage(1.0f);
            }

            coolDownTimer = CoolDown;
        }

        protected override void Act(float deltaTime)
        {
            coolDownTimer -= deltaTime;

            var weapon = character.Inventory.FindItem("weapon");

            if (weapon == null)
            {
                Escape(deltaTime);
            }
            else
            {
                //TODO: make sure the weapon is ready to use (projectiles/batteries loaded)
                if (!character.SelectedItems.Contains(weapon))
                {
                    if (character.Inventory.TryPutItem(weapon, 3, false, false, character))
                    {
                        weapon.Equip(character);
                    }
                    else
                    {
                        return;
                    }
                }
                character.CursorPosition = enemy.Position;
                character.SetInput(InputType.Aim, false, true);

                Vector2 enemyDiff = Vector2.Normalize(enemy.Position - character.Position);
                float weaponAngle = ((weapon.body.Dir == 1.0f) ? weapon.body.Rotation : weapon.body.Rotation - MathHelper.Pi);
                Vector2 weaponDir = new Vector2((float)Math.Cos(weaponAngle), (float)Math.Sin(weaponAngle));

                if (Vector2.Dot(enemyDiff, weaponDir) < 0.9f) return;

                List<FarseerPhysics.Dynamics.Body> ignoredBodies = new List<FarseerPhysics.Dynamics.Body>();
                foreach (Limb limb in character.AnimController.Limbs)
                {
                    ignoredBodies.Add(limb.body.FarseerBody);
                }

                var pickedBody = Submarine.PickBody(character.SimPosition, enemy.SimPosition, ignoredBodies);
                if (pickedBody != null && !(pickedBody.UserData is Limb)) return;

               weapon.Use(deltaTime, character);
            }
        }

        private void Escape(float deltaTime)
        {
            if (escapeObjective == null)
            {
                escapeObjective = new AIObjectiveFindSafety(character);
            }
            
            if (enemy.AnimController.CurrentHull == character.AnimController.CurrentHull)
            {
                escapeObjective.OverrideCurrentHullSafety = 0.0f;
            }
            else
            {
                escapeObjective.OverrideCurrentHullSafety = null;
            }

            escapeObjective.TryComplete(deltaTime);
        }

        public override bool IsCompleted()
        {
            return enemy.IsDead || coolDownTimer <= 0.0f;
        }

        public override float GetPriority(AIObjectiveManager objectiveManager)
        {
            if (objectiveManager.CurrentOrder == this)
            {
                return AIObjectiveManager.OrderPriority;
            }

            //clamp the strength to the health of this character
            //(it doesn't make a difference whether the enemy does 200 or 600 damage, it's one hit kill anyway)

            float enemyDanger = Math.Min(Math.Max(enemyStrength, MaxEnemyDamage), character.Health) + enemy.Health / 10.0f;

            EnemyAIController enemyAI = enemy.AIController as EnemyAIController;
            if (enemyAI != null)
            {
                if (enemyAI.SelectedAiTarget == character.AiTarget) enemyDanger *= 2.0f;
            }

            return Math.Max(enemyDanger, AIObjectiveManager.OrderPriority);
        }

        public override bool IsDuplicate(AIObjective otherObjective)
        {
            AIObjectiveCombat objective = otherObjective as AIObjectiveCombat;
            if (objective == null) return false;

            return objective.enemy == enemy;
        }
    }
}
