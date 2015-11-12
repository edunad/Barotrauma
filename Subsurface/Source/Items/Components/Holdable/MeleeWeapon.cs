﻿using FarseerPhysics;
using FarseerPhysics.Dynamics;
using FarseerPhysics.Dynamics.Contacts;
using Microsoft.Xna.Framework;
using System;
using System.Xml.Linq;

namespace Barotrauma.Items.Components
{
    class MeleeWeapon : Holdable
    {
        private float hitPos;

        private bool hitting;

        private Attack attack;

        private float range;

        private Character user;

        private float reload;

        private float reloadTimer;

        [HasDefaultValue(0.0f, false)]
        public float Range
        {
            get { return ConvertUnits.ToDisplayUnits(range); }
            set { range = ConvertUnits.ToSimUnits(value); }
        }

        [HasDefaultValue(0.5f, false)]
        public float Reload
        {
            get { return reload; }
            set { reload = Math.Max(0.0f, value); }
        }

        public MeleeWeapon(Item item, XElement element)
            : base(item, element)
        {
            //throwForce = ToolBox.GetAttributeFloat(element, "throwforce", 1.0f);

            foreach (XElement subElement in element.Elements())
            {
                if (subElement.Name.ToString().ToLower() != "attack") continue;
                attack = new Attack(subElement);
            }
        }

        public override bool Use(float deltaTime, Character character = null)
        {
            if (character == null || reloadTimer>0.0f) return false;
            if (!character.IsKeyDown(InputType.Aim) || hitting) return false;

            user = character;

            if (hitPos < MathHelper.Pi * 0.69f) return false;

            reloadTimer = reload;

            item.body.FarseerBody.CollisionCategories = Physics.CollisionProjectile;
            item.body.FarseerBody.CollidesWith = Physics.CollisionCharacter | Physics.CollisionWall;
            item.body.FarseerBody.OnCollision += OnCollision;

            foreach (Limb l in character.AnimController.Limbs)
            {
                //item.body.FarseerBody.IgnoreCollisionWith(l.body.FarseerBody);

                if (character.AnimController.InWater) continue;
                if (l.type == LimbType.LeftFoot || l.type == LimbType.LeftThigh || l.type == LimbType.LeftLeg) continue;

                if (l.type == LimbType.Head || l.type == LimbType.Torso)
                {
                    l.body.ApplyLinearImpulse(new Vector2(character.AnimController.Dir * 7.0f, -4.0f));                   
                }
                else
                {
                    l.body.ApplyLinearImpulse(new Vector2(character.AnimController.Dir * 5.0f, -2.0f));
                }                
            }
            
            hitting = true;

            IsActive = true;
            return false;
        }
        
        public override void Drop(Character dropper)
        {
            base.Drop(dropper);

            hitting = false;
            hitPos = 0.0f;
        }

        public override void UpdateBroken(float deltaTime, Camera cam)
        {
            Update(deltaTime, cam);
        }
        
        public override void Update(float deltaTime, Camera cam)
        {
            if (!item.body.Enabled) return;
            if (!picker.HasSelectedItem(item)) IsActive = false;

            reloadTimer -= deltaTime;

            if (!picker.IsKeyDown(InputType.Aim) && !hitting) hitPos = 0.0f;

            ApplyStatusEffects(ActionType.OnActive, deltaTime, picker);

            if (item.body.Dir != picker.AnimController.Dir) Flip(item);

            AnimController ac = picker.AnimController;

            if (!hitting)
            {
                if (picker.IsKeyDown(InputType.Aim))
                {
                    hitPos = Math.Min(hitPos+deltaTime*5.0f, MathHelper.Pi*0.7f);

                    ac.HoldItem(deltaTime, item, handlePos, new Vector2(0.6f, -0.1f), new Vector2(-0.3f, 0.2f), false, hitPos);
                }
                else
                {
                    ac.HoldItem(deltaTime, item, handlePos, new Vector2(hitPos, 0.0f), aimPos, false, holdAngle);
                }
            }
            else
            {
                //Vector2 diff = Vector2.Normalize(picker.CursorPosition - ac.RefLimb.Position);
                //diff.X = diff.X * ac.Dir;

                hitPos -= deltaTime*15.0f;

                //angl = -hitPos * 2.0f;
                //    System.Diagnostics.Debug.WriteLine("<1.0f "+hitPos);



                ac.HoldItem(deltaTime, item, handlePos, new Vector2(0.6f, -0.1f), new Vector2(-0.3f, 0.2f), false, hitPos);
                //}
                //else
                //{
                //    System.Diagnostics.Debug.WriteLine(">1.0f " + hitPos);
                //    ac.HoldItem(deltaTime, item, handlePos, new Vector2(0.5f, 0.2f), new Vector2(1.0f, 0.2f), false, 0.0f);
                //}

                if (hitPos < -MathHelper.PiOver4*1.2f)
                {
                    RestoreCollision();
                    hitting = false;
                }
            }
             

        }    

        private void RestoreCollision()
        {
            item.body.FarseerBody.OnCollision -= OnCollision;

            item.body.CollisionCategories = Physics.CollisionMisc;
            item.body.CollidesWith = Physics.CollisionWall;

            //foreach (Limb l in picker.AnimController.Limbs)
            //{
            //    item.body.FarseerBody.RestoreCollisionWith(l.body.FarseerBody);
            //}
        }


        private bool OnCollision(Fixture f1, Fixture f2, Contact contact)
        {
            IDamageable target = null;

            Limb limb = f2.Body.UserData as Limb;
            if (limb != null)
            {
                if (limb.character == picker) return false;
                target = limb.character;
            }
            else
            {
                return false;
            }

            if (target == null)
            {
                target = f2.Body.UserData as IDamageable;
            }

            if (target == null) return false;

            if (attack!=null) attack.DoDamage(user, target, item.Position, 1.0f);

            RestoreCollision();
            hitting = false;

            ApplyStatusEffects(ActionType.OnUse, 1.0f, limb.character);

            return true;
        }
    }
}