﻿using Microsoft.Xna.Framework;

namespace Barotrauma
{
    partial class AIController : ISteerable
    {
        public enum AIState { None, Attack, GoTo, Escape, Eat }

        public bool Enabled;

        public readonly Character Character;
        
        protected AIState state;

        protected SteeringManager steeringManager;

        public SteeringManager SteeringManager
        {
            get { return steeringManager; }
        }

        public Vector2 Steering
        {
            get { return Character.AnimController.TargetMovement; }
            set { Character.AnimController.TargetMovement = value; }
        }
        
        public Vector2 SimPosition
        {
            get { return Character.SimPosition; }
        }

        public Vector2 WorldPosition
        {
            get { return Character.WorldPosition; }
        }

        public Vector2 Velocity
        {
            get { return Character.AnimController.Collider.LinearVelocity; }
        }

        public AIState State
        {
            get { return state; }
            set { state = value; }
        }

        public AIController (Character c)
        {
            Character = c;

            Enabled = true;
        }

        public virtual void OnAttacked(Character attacker, float amount) { }

        public virtual void SelectTarget(AITarget target) { }

        public virtual void Update(float deltaTime) { }

        //protected Structure lastStructurePicked;
        
    }
}
