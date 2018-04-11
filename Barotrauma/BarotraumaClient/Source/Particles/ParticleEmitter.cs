﻿using Microsoft.Xna.Framework;
using System;
using System.Xml.Linq;

namespace Barotrauma.Particles
{
    class ParticleEmitter
    {
        private float emitTimer;

        public readonly ParticleEmitterPrefab Prefab;

        public ParticleEmitter(XElement element)
        {
            Prefab = new ParticleEmitterPrefab(element);
        }

        public ParticleEmitter(ParticleEmitterPrefab prefab)
        {
            System.Diagnostics.Debug.Assert(prefab != null, "The prefab of a particle emitter cannot be null");
            Prefab = prefab;
        }

        public void Emit(float deltaTime, Vector2 position, Hull hullGuess = null, float angle = 0.0f, float particleRotation = 0.0f)
        {
            emitTimer += deltaTime;

            if (Prefab.ParticlesPerSecond > 0)
            {
                float emitInterval = 1.0f / Prefab.ParticlesPerSecond;
                while (emitTimer > emitInterval)
                {
                    Emit(position, hullGuess, angle, particleRotation);
                    emitTimer -= emitInterval;
                }
            }

            for (int i = 0; i < Prefab.ParticleAmount; i++)
            {
                Emit(position, hullGuess, particleRotation);
            }
        }

        private void Emit(Vector2 position, Hull hullGuess = null, float angle = 0.0f, float particleRotation = 0.0f)
        {
            angle += Rand.Range(Prefab.AngleMin, Prefab.AngleMax);
            Vector2 velocity = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * Rand.Range(Prefab.VelocityMin, Prefab.VelocityMax);

            var particle = GameMain.ParticleManager.CreateParticle(Prefab.ParticlePrefab, position, velocity, particleRotation, hullGuess);

            if (particle != null)
            {
                particle.Size *= Rand.Range(Prefab.ScaleMin, Prefab.ScaleMax);
            }
        }

        public Rectangle CalculateParticleBounds(Vector2 startPosition)
        {
            Rectangle bounds = new Rectangle((int)startPosition.X, (int)startPosition.Y, (int)startPosition.X, (int)startPosition.Y);

            for (float angle = Prefab.AngleMin; angle <= Prefab.AngleMax; angle += 0.1f)
            {
                Vector2 velocity = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * Prefab.VelocityMax;
                Vector2 endPosition = Prefab.ParticlePrefab.CalculateEndPosition(startPosition, velocity);

                bounds = new Rectangle(
                    (int)Math.Min(bounds.X, endPosition.X),
                    (int)Math.Min(bounds.Y, endPosition.Y),
                    (int)Math.Max(bounds.X, endPosition.X),
                    (int)Math.Max(bounds.Y, endPosition.Y));
            }

            bounds = new Rectangle(bounds.X, bounds.Y, bounds.Width - bounds.X, bounds.Height - bounds.Y);

            return bounds;
        }
    }

    class ParticleEmitterPrefab
    {        
        public readonly string Name;

        public readonly ParticlePrefab ParticlePrefab;

        public readonly float AngleMin, AngleMax;

        public readonly float VelocityMin, VelocityMax;

        public readonly float ScaleMin, ScaleMax;
        
        public readonly int ParticleAmount;
        public readonly float ParticlesPerSecond;        

        public ParticleEmitterPrefab(XElement element)
        {
            Name = element.Name.ToString();

            ParticlePrefab = GameMain.ParticleManager.FindPrefab(element.GetAttributeString("particle", ""));

            if (element.Attribute("startrotation") == null)
            {
                AngleMin = element.GetAttributeFloat("anglemin", 0.0f);
                AngleMax = element.GetAttributeFloat("anglemax", 0.0f);
            }
            else
            {
                AngleMin = element.GetAttributeFloat("angle", 0.0f);
                AngleMax = AngleMin;
            }

            AngleMin = MathHelper.ToRadians(MathHelper.Clamp(AngleMin, -360.0f, 360.0f));
            AngleMax = MathHelper.ToRadians(MathHelper.Clamp(AngleMax, -360.0f, 360.0f));

            if (element.Attribute("scalemin")==null)
            {
                ScaleMin = 1.0f;
                ScaleMax = 1.0f;
            }
            else
            {
                ScaleMin = element.GetAttributeFloat("scalemin",1.0f);
                ScaleMax = Math.Max(ScaleMin, element.GetAttributeFloat("scalemax", 1.0f));
            }

            if (element.Attribute("velocity") == null)
            {
                VelocityMin = element.GetAttributeFloat("velocitymin", 0.0f);
                VelocityMax = element.GetAttributeFloat("velocitymax", 0.0f);
            }
            else
            {
                VelocityMin = element.GetAttributeFloat("velocity", 0.0f);
                VelocityMax = VelocityMin;
            }

            ParticlesPerSecond = element.GetAttributeInt("particlespersecond", 0);
            ParticleAmount = element.GetAttributeInt("particleamount", 0);
        }
    }
}
