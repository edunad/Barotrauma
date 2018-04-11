﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Barotrauma
{
    partial class Gap : MapEntity
    {
        private float particleTimer;

        public override void Draw(SpriteBatch sb, bool editing, bool back = true)
        {
            if (GameMain.DebugDraw)
            {
                Vector2 center = new Vector2(WorldRect.X + rect.Width / 2.0f, -(WorldRect.Y - rect.Height / 2.0f));

                GUI.DrawLine(sb, center, center + new Vector2(flowForce.X, -flowForce.Y) / 10.0f, Color.Red);

                GUI.DrawLine(sb, center + Vector2.One * 5.0f, center + new Vector2(lerpedFlowForce.X, -lerpedFlowForce.Y) / 10.0f + Vector2.One * 5.0f, Color.Orange);
            }

            if (!editing || !ShowGaps) return;

            Color clr = (open == 0.0f) ? Color.Red : Color.Cyan;
            if (isHighlighted) clr = Color.Gold;

            float depth = (ID % 255) * 0.000001f;

            GUI.DrawRectangle(
                sb, new Rectangle(WorldRect.X, -WorldRect.Y, rect.Width, rect.Height),
                clr * 0.5f, true,
                depth,
                (int)Math.Max((1.5f / GameScreen.Selected.Cam.Zoom), 1.0f));

            for (int i = 0; i < linkedTo.Count; i++)
            {
                Vector2 dir = IsHorizontal ?
                    new Vector2(Math.Sign(linkedTo[i].Rect.Center.X - rect.Center.X), 0.0f)
                    : new Vector2(0.0f, Math.Sign((linkedTo[i].Rect.Y - linkedTo[i].Rect.Height / 2.0f) - (rect.Y - rect.Height / 2.0f)));

                Vector2 arrowPos = new Vector2(WorldRect.Center.X, -(WorldRect.Y - WorldRect.Height / 2));
                arrowPos += new Vector2(dir.X * (WorldRect.Width / 2 + 10), dir.Y * (WorldRect.Height / 2 + 10));

                GUI.Arrow.Draw(sb,
                    arrowPos, clr * 0.8f,
                    GUI.Arrow.Origin, MathUtils.VectorToAngle(dir) + MathHelper.PiOver2,
                    IsHorizontal ? new Vector2(rect.Height / 16.0f, 1.0f) : new Vector2(rect.Width / 16.0f, 1.0f),
                    SpriteEffects.None, depth);
            }

            if (IsSelected)
            {
                GUI.DrawRectangle(sb,
                    new Vector2(WorldRect.X - 5, -WorldRect.Y - 5),
                    new Vector2(rect.Width + 10, rect.Height + 10),
                    Color.Red,
                    false,
                    depth,
                    (int)Math.Max((1.5f / GameScreen.Selected.Cam.Zoom), 1.0f));
            }
        }

        partial void EmitParticles(float deltaTime)
        {
            if (flowTargetHull == null) return;
            
            particleTimer += deltaTime;

            Vector2 pos = Position;
            if (IsHorizontal)
            {
                pos.X += Math.Sign(flowForce.X);
                pos.Y = MathHelper.Clamp((higherSurface + lowerSurface) / 2.0f, rect.Y - rect.Height, rect.Y) + 10;
            }
            else
            {
                pos.Y += Math.Sign(flowForce.Y) * rect.Height / 2.0f;
            }

            //light dripping
            if (open < 0.2f && LerpedFlowForce.LengthSquared() > 100.0f)
            {
                float particlesPerSec = open * 1000.0f;
                float emitInterval = 1.0f / particlesPerSec;
                while (particleTimer > emitInterval)
                {
                    Vector2 velocity = flowForce;
                    if (!IsHorizontal)
                    {
                        velocity.X = Rand.Range(-500.0f, 500.0f) * open;
                    }
                    else
                    {
                        velocity.X *= Rand.Range(1.0f, 3.0f);
                    }

                    if (flowTargetHull.WaterVolume < flowTargetHull.Volume)
                    {
                        GameMain.ParticleManager.CreateParticle(
                            Rand.Range(0.0f, open) < 0.05f ? "waterdrop" : "watersplash",
                            (Submarine == null ? pos : pos + Submarine.Position),
                            velocity, 0, flowTargetHull);
                    }

                    GameMain.ParticleManager.CreateParticle(
                        "bubbles",
                        (Submarine == null ? pos : pos + Submarine.Position),
                        velocity, 0, flowTargetHull);

                    particleTimer = 0.0f;
                    particleTimer -= emitInterval;
                }
            }
            //heavy flow -> strong waterfall type of particles
            else if (LerpedFlowForce.LengthSquared() > 20000.0f)
            {
                if (IsHorizontal)
                {
                    Vector2 velocity = new Vector2(
                        MathHelper.Clamp(flowForce.X, -5000.0f, 5000.0f) * Rand.Range(0.5f, 0.7f),
                        flowForce.Y * Rand.Range(0.5f, 0.7f));

                    if (flowTargetHull.WaterVolume < flowTargetHull.Volume)
                    {
                        var particle = GameMain.ParticleManager.CreateParticle(
                            "watersplash",
                            (Submarine == null ? pos : pos + Submarine.Position) - Vector2.UnitY * Rand.Range(0.0f, 10.0f),
                            velocity, 0, flowTargetHull);

                        if (particle != null)
                        {
                            particle.Size = particle.Size * Math.Min(Math.Abs(flowForce.X / 1000.0f), 5.0f);
                        }
                    }

                    if (Math.Abs(flowForce.X) > 300.0f)
                    {
                        pos.X += Math.Sign(flowForce.X) * 10.0f;
                        pos.Y = Rand.Range(lowerSurface, rect.Y - rect.Height);

                        GameMain.ParticleManager.CreateParticle(
                            "bubbles",
                            Submarine == null ? pos : pos + Submarine.Position,
                            flowForce / 10.0f, 0, flowTargetHull);
                    }
                }
                else
                {
                    if (Math.Sign(flowTargetHull.Rect.Y - rect.Y) != Math.Sign(lerpedFlowForce.Y)) return;

                    for (int i = 0; i < rect.Width; i += Rand.Range(80, 100))
                    {
                        pos.X = Rand.Range(rect.X, rect.X + rect.Width);

                        Vector2 velocity = new Vector2(
                            lerpedFlowForce.X * Rand.Range(0.5f, 0.7f),
                            MathHelper.Clamp(lerpedFlowForce.Y, -500.0f, 1000.0f) * Rand.Range(0.5f, 0.7f));

                        var splash = GameMain.ParticleManager.CreateParticle(
                            "watersplash",
                            Submarine == null ? pos : pos + Submarine.Position,
                            velocity, 0, FlowTargetHull);

                        if (splash != null) splash.Size = splash.Size * MathHelper.Clamp(rect.Width / 50.0f, 0.8f, 4.0f);

                        GameMain.ParticleManager.CreateParticle(
                            "bubbles",
                            Submarine == null ? pos : pos + Submarine.Position,
                            flowForce / 2.0f, 0, FlowTargetHull);
                    }
                }
            }            
        }
    }
}
