﻿using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Barotrauma.Items.Components
{
    class WearableSprite
    {
        public readonly Sprite Sprite;
        public readonly LimbType Limb;
        public readonly bool HideLimb;
        public readonly bool InheritLimbDepth;
        public readonly LimbType DepthLimb;

        public LightComponent LightComponent;

        public readonly Wearable WearableComponent;
        public readonly string Sound;

        public WearableSprite(Wearable item, Sprite sprite, LimbType limb, bool hideLimb, bool inheritLimbDepth = true, LimbType depthLimb = LimbType.None, string sound = null)
        {
            WearableComponent = item;
            Sprite = sprite;
            Limb = limb;
            HideLimb = hideLimb;
            InheritLimbDepth = inheritLimbDepth;
            DepthLimb = depthLimb;
            Sound = sound;
        }
    }

    class Wearable : Pickable
    {
        private WearableSprite[] wearableSprites;
        private LimbType[] limbType;
        private Limb[] limb;

        private List<DamageModifier> damageModifiers;

        public List<DamageModifier> DamageModifiers
        {
            get { return damageModifiers; }
        }
        
        public Wearable (Item item, XElement element)
            : base(item, element)
        {
            this.item = item;

            damageModifiers = new List<DamageModifier>();
            
            int spriteCount = element.Elements().Count(x => x.Name.ToString() == "sprite");
            wearableSprites = new WearableSprite[spriteCount];
            limbType    = new LimbType[spriteCount];
            limb        = new Limb[spriteCount];

            int i = 0;
            foreach (XElement subElement in element.Elements())
            {
                switch (subElement.Name.ToString().ToLower())
                {
                    case "sprite":
                        if (subElement.Attribute("texture") == null)
                        {
                            DebugConsole.ThrowError("Item \"" + item.Name + "\" doesn't have a texture specified!");
                            return;
                        }

                        string spritePath = subElement.Attribute("texture").Value;
                        spritePath = Path.GetDirectoryName(item.Prefab.ConfigFile) + "/" + spritePath;

                        var sound = subElement.GetAttributeString("sound", "");
                        var sprite = new Sprite(subElement, "", spritePath);
                        limbType[i] = (LimbType)Enum.Parse(typeof(LimbType),
                            subElement.GetAttributeString("limb", "Head"), true);

                        wearableSprites[i] = new WearableSprite(this, sprite, limbType[i],
                            subElement.GetAttributeBool("hidelimb", false),
                            subElement.GetAttributeBool("inheritlimbdepth", true),
                            (LimbType)Enum.Parse(typeof(LimbType), subElement.GetAttributeString("depthlimb", "None"), true), sound);

                        foreach (XElement lightElement in subElement.Elements())
                        {
                            if (lightElement.Name.ToString().ToLowerInvariant() != "lightcomponent") continue;
                            wearableSprites[i].LightComponent = new LightComponent(item, lightElement);
                            wearableSprites[i].LightComponent.Parent = this;
                            item.components.Add(wearableSprites[i].LightComponent);
                        }

                        i++;
                        break;
                    case "damagemodifier":
                        damageModifiers.Add(new DamageModifier(subElement));
                        break;
                }
            }
        }

        public override void Equip(Character character)
        {
            picker = character;
            for (int i = 0; i < wearableSprites.Length; i++ )
            {
                Limb equipLimb  = character.AnimController.GetLimb(limbType[i]);
                if (equipLimb == null) continue;
                
                item.body.Enabled = false;
                IsActive = true;
                if (wearableSprites[i].LightComponent != null)
                {
                    wearableSprites[i].LightComponent.ParentBody = equipLimb.body;
                }

                limb[i] = equipLimb;
                if (!equipLimb.WearingItems.Contains(wearableSprites[i]))
                {
                    equipLimb.WearingItems.Add(wearableSprites[i]);
                }
            }
        }

        public override void Drop(Character dropper)
        {
            Unequip(picker);

            base.Drop(dropper);

            picker = null;
            IsActive = false;
        }

        public override void Unequip(Character character)
        {
            if (picker == null) return;
            for (int i = 0; i < wearableSprites.Length; i++)
            {
                Limb equipLimb = character.AnimController.GetLimb(limbType[i]);
                if (equipLimb == null) continue;

                if (wearableSprites[i].LightComponent != null)
                {
                    wearableSprites[i].LightComponent.ParentBody = null;
                }

                equipLimb.WearingItems.RemoveAll(w => w != null && w == wearableSprites[i]);

                limb[i] = null;
            }

            IsActive = false;
        }

        public override void UpdateBroken(float deltaTime, Camera cam)
        {
            Update(deltaTime, cam);
        }

        public override void Update(float deltaTime, Camera cam)
        {
            item.SetTransform(picker.SimPosition, 0.0f);
            item.SetContainedItemPositions();
            
            ApplyStatusEffects(ActionType.OnWearing, deltaTime, picker);

#if CLIENT
            PlaySound(ActionType.OnWearing, picker.WorldPosition);
#endif
        }

        protected override void RemoveComponentSpecific()
        {
            base.RemoveComponentSpecific();

            foreach (WearableSprite wearableSprite in wearableSprites)
            {
                if (wearableSprite != null && wearableSprite.Sprite != null) wearableSprite.Sprite.Remove();
            }
        }

    }
}
