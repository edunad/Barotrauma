﻿using System.Xml.Linq;
using Microsoft.Xna.Framework;
#if CLIENT
using Microsoft.Xna.Framework.Graphics;
using Barotrauma.Particles;
#endif

namespace Barotrauma.Items.Components
{
    class Propulsion : ItemComponent
    {
        enum UsableIn
        {
            Air,Water,Both
        };

        private float force;

        private string particles;

        private float useState;
        
        private UsableIn usableIn;
                
        [Serialize(0.0f, false)]
        public float Force
        {
            get { return force; }
            set { force = value; }
        }

#if CLIENT
        [Serialize("", false)]
        public string Particles
        {
            get { return particles; }
            set { particles = value; }
        }
#endif

        public Propulsion(Item item, XElement element)
            : base(item,element)
        {
            switch (element.GetAttributeString("usablein", "both").ToLowerInvariant())
            {
                case "air":
                    usableIn = UsableIn.Air;
                    break;
                case "water":
                    usableIn = UsableIn.Water;
                    break;
                case "both":
                default:
                    usableIn = UsableIn.Both;
                    break;
            }
        }
        
        public override bool Use(float deltaTime, Character character = null)
        {
            if (character == null || character.Removed) return false;
            if (!character.IsKeyDown(InputType.Aim) || character.Stun > 0.0f) return false;

            IsActive = true;
            useState = 0.1f;

            if (character.AnimController.InWater)
            {
                if (usableIn == UsableIn.Air) return true;
            }
            else
            {
                if (usableIn == UsableIn.Water) return true;
            }

            Vector2 dir = Vector2.Normalize(character.CursorPosition - character.Position);

            Vector2 propulsion = dir * force;

            if (character.AnimController.InWater) character.AnimController.TargetMovement = dir;

            foreach (Limb limb in character.AnimController.Limbs)
            {
                if (limb.WearingItems.Find(w => w.WearableComponent.Item == this.item)==null) continue;

                limb.body.ApplyForce(propulsion);
            }

            character.AnimController.Collider.ApplyForce(propulsion);

            if (character.SelectedItems[0] == item) character.AnimController.GetLimb(LimbType.RightHand).body.ApplyForce(propulsion);
            if (character.SelectedItems[1] == item) character.AnimController.GetLimb(LimbType.LeftHand).body.ApplyForce(propulsion);

#if CLIENT
            if (!string.IsNullOrWhiteSpace(particles))
            {
                GameMain.ParticleManager.CreateParticle(particles, item.WorldPosition,
                    item.body.Rotation + ((item.body.Dir > 0.0f) ? 0.0f : MathHelper.Pi), 0.0f, item.CurrentHull);
            }
#endif

            return true;
        }

        public override void Update(float deltaTime, Camera cam)
        {
            useState -= deltaTime;

            if (useState <= 0.0f) IsActive = false;
        }
        
    }
}
