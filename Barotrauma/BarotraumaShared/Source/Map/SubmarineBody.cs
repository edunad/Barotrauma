﻿using FarseerPhysics;
using FarseerPhysics.Collision;
using FarseerPhysics.Common;
using FarseerPhysics.Dynamics;
using FarseerPhysics.Dynamics.Contacts;
using FarseerPhysics.Factories;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Voronoi2;

namespace Barotrauma
{
    class SubmarineBody
    {
        public const float DamageDepth = -30000.0f;
        private const float ImpactDamageMultiplier = 10.0f;

        private const float Friction = 0.2f, Restitution = 0.0f;

        public List<Vector2> HullVertices
        {
            get;
            private set;
        }

        private float depthDamageTimer;

        private readonly Submarine submarine;

        public readonly PhysicsBody Body;

        private List<PosInfo> memPos = new List<PosInfo>();

        public Rectangle Borders
        {
            get;
            private set;
        }
                
        public Vector2 Velocity
        {
            get { return Body.LinearVelocity; }
            set
            {
                if (!MathUtils.IsValid(value)) return;
                Body.LinearVelocity = value;
            }
        }

        public Vector2 Position
        {
            get { return ConvertUnits.ToDisplayUnits(Body.SimPosition); }
        }

        public List<PosInfo> MemPos
        {
            get { return memPos; }
        }
        
        public bool AtDamageDepth
        {
            get { return Position.Y < DamageDepth; }
        }

        public SubmarineBody(Submarine sub)
        {
            this.submarine = sub;

            Body farseerBody = null;

            if (!Hull.hullList.Any())
            {
                Body = new PhysicsBody(1,1,1,1);
                farseerBody = Body.FarseerBody;
                DebugConsole.ThrowError("WARNING: no hulls found, generating a physics body for the submarine failed.");
            }
            else
            {
                List<Vector2> convexHull = GenerateConvexHull();
                HullVertices = convexHull;

                for (int i = 0; i < convexHull.Count; i++)
                {
                    convexHull[i] = ConvertUnits.ToSimUnits(convexHull[i]);
                }

                convexHull.Reverse();

                //get farseer 'vertices' from vectors
                Vertices shapevertices = new Vertices(convexHull);

                AABB hullAABB = shapevertices.GetAABB();

                Borders = new Rectangle(
                    (int)ConvertUnits.ToDisplayUnits(hullAABB.LowerBound.X),
                    (int)ConvertUnits.ToDisplayUnits(hullAABB.UpperBound.Y),
                    (int)ConvertUnits.ToDisplayUnits(hullAABB.Extents.X * 2.0f),
                    (int)ConvertUnits.ToDisplayUnits(hullAABB.Extents.Y * 2.0f));

                farseerBody = BodyFactory.CreateBody(GameMain.World, this);

                foreach (Structure wall in Structure.WallList)
                {
                    if (wall.Submarine != submarine) continue;

                    Rectangle rect = wall.Rect;

                    FixtureFactory.AttachRectangle(
                          ConvertUnits.ToSimUnits(rect.Width),
                          ConvertUnits.ToSimUnits(rect.Height),
                          50.0f,
                          ConvertUnits.ToSimUnits(new Vector2(rect.X + rect.Width / 2, rect.Y - rect.Height / 2)),
                          farseerBody, this);
                }

                foreach (Hull hull in Hull.hullList)
                {
                    if (hull.Submarine != submarine) continue;

                    Rectangle rect = hull.Rect;
                    FixtureFactory.AttachRectangle(
                        ConvertUnits.ToSimUnits(rect.Width),
                        ConvertUnits.ToSimUnits(rect.Height),
                        5.0f,
                        ConvertUnits.ToSimUnits(new Vector2(rect.X + rect.Width / 2, rect.Y - rect.Height / 2)),
                        farseerBody, this);
                }
            }



            farseerBody.BodyType = BodyType.Dynamic;
            farseerBody.CollisionCategories = Physics.CollisionWall;
            farseerBody.CollidesWith = 
                Physics.CollisionItem | 
                Physics.CollisionLevel | 
                Physics.CollisionCharacter | 
                Physics.CollisionProjectile | 
                Physics.CollisionWall;

            farseerBody.Restitution = Restitution;
            farseerBody.Friction = Friction;
            farseerBody.FixedRotation = true;
            //mass = Body.Mass;
            farseerBody.Awake = true;
            farseerBody.SleepingAllowed = false;
            farseerBody.IgnoreGravity = true;
            farseerBody.OnCollision += OnCollision;
            farseerBody.UserData = submarine;

            Body = new PhysicsBody(farseerBody);
        }


        private List<Vector2> GenerateConvexHull()
        {
            List<Structure> subWalls = Structure.WallList.FindAll(wall => wall.Submarine == submarine);

            if (subWalls.Count == 0)
            {
                return new List<Vector2> { new Vector2(-1.0f, 1.0f), new Vector2(1.0f, 1.0f), new Vector2(0.0f, -1.0f) };
            }

            List<Vector2> points = new List<Vector2>();

            foreach (Structure wall in subWalls)
            {
                points.Add(new Vector2(wall.Rect.X, wall.Rect.Y));
                points.Add(new Vector2(wall.Rect.X + wall.Rect.Width, wall.Rect.Y));
                points.Add(new Vector2(wall.Rect.X, wall.Rect.Y - wall.Rect.Height));
                points.Add(new Vector2(wall.Rect.X + wall.Rect.Width, wall.Rect.Y - wall.Rect.Height));
            }

            List<Vector2> hullPoints = MathUtils.GiftWrap(points);

            return hullPoints;
        }

        public void Update(float deltaTime)
        {
            if (GameMain.Client != null)
            {
                if (memPos.Count == 0) return;
                
                Vector2 newVelocity = Body.LinearVelocity;
                Vector2 newPosition = Body.SimPosition;

                Body.CorrectPosition(memPos, deltaTime, out newVelocity, out newPosition);
                Vector2 moveAmount = ConvertUnits.ToDisplayUnits(newPosition - Body.SimPosition);

                List<Submarine> subsToMove = submarine.GetConnectedSubs();
                foreach (Submarine dockedSub in subsToMove)
                {
                    if (dockedSub == submarine) continue;
                    //clear the position buffer of the docked subs to prevent unnecessary position corrections
                    dockedSub.SubBody.memPos.Clear();
                }

                Submarine closestSub = null;
                if (Character.Controlled == null)
                {
                    closestSub = Submarine.FindClosest(GameMain.GameScreen.Cam.WorldViewCenter);
                }
                else
                {
                    closestSub = Character.Controlled.Submarine;
                }

                bool displace = moveAmount.Length() > 100.0f;
                foreach (Submarine sub in subsToMove)
                {
                    sub.PhysicsBody.SetTransform(sub.PhysicsBody.SimPosition + ConvertUnits.ToSimUnits(moveAmount), 0.0f);
                    sub.PhysicsBody.LinearVelocity = newVelocity;

                    if (displace) sub.SubBody.DisplaceCharacters(moveAmount);
                }

                if (closestSub != null && subsToMove.Contains(closestSub))                     
                {
                    GameMain.GameScreen.Cam.Position += moveAmount;
                    if (GameMain.GameScreen.Cam.TargetPos != Vector2.Zero) GameMain.GameScreen.Cam.TargetPos += moveAmount;    
            
                    if (Character.Controlled!=null) Character.Controlled.CursorPosition += moveAmount;
                }

                return;
            }
            
            //if outside left or right edge of the level
            if (Position.X < 0 || Position.X > Level.Loaded.Size.X)
            {
                Rectangle worldBorders = Borders;
                worldBorders.Location += MathUtils.ToPoint(Position);

                //push the sub back below the upper "barrier" of the level
                if (worldBorders.Y > Level.Loaded.Size.Y)
                {
                    Body.LinearVelocity = new Vector2(
                        Body.LinearVelocity.X,
                        Math.Min(Body.LinearVelocity.Y, ConvertUnits.ToSimUnits(Level.Loaded.Size.Y - worldBorders.Y)));
                }
                else if (worldBorders.Y - worldBorders.Height < Level.Loaded.BottomPos)
                {
                    Body.LinearVelocity = new Vector2(
                        Body.LinearVelocity.X,
                        Math.Max(Body.LinearVelocity.Y, ConvertUnits.ToSimUnits(Level.Loaded.BottomPos - (worldBorders.Y - worldBorders.Height))));
                }
            }

            //-------------------------

            Vector2 totalForce = CalculateBuoyancy();

            if (Body.LinearVelocity.LengthSquared() > 0.000001f)
            {
                float dragCoefficient = 0.01f;

                float speedLength = (Body.LinearVelocity == Vector2.Zero) ? 0.0f : Body.LinearVelocity.Length();
                float drag = speedLength * speedLength * dragCoefficient * Body.Mass;

                totalForce += -Vector2.Normalize(Body.LinearVelocity) * drag;                
            }

            ApplyForce(totalForce);

            UpdateDepthDamage(deltaTime);
        }
        
        /// <summary>
        /// Moves away any character that is inside the bounding box of the sub (but not inside the sub)
        /// </summary>
        /// <param name="subTranslation">The translation that was applied to the sub before doing the displacement 
        /// (used for determining where to push the characters)</param>
        private void DisplaceCharacters(Vector2 subTranslation)
        {
            Rectangle worldBorders = Borders;
            worldBorders.Location += MathUtils.ToPoint(ConvertUnits.ToDisplayUnits(Body.SimPosition));

            Vector2 translateDir = Vector2.Normalize(subTranslation);

            foreach (Character c in Character.CharacterList)
            {
                if (c.AnimController.CurrentHull != null && c.AnimController.CanEnterSubmarine) continue;

                foreach (Limb limb in c.AnimController.Limbs)
                {
                    //if the character isn't inside the bounding box, continue
                    if (!Submarine.RectContains(worldBorders, limb.WorldPosition)) continue;
                
                    //cast a line from the position of the character to the same direction as the translation of the sub
                    //and see where it intersects with the bounding box
                    Vector2? intersection = MathUtils.GetLineRectangleIntersection(limb.WorldPosition,
                        limb.WorldPosition + translateDir*100000.0f, worldBorders);

                    //should never be null when casting a line out from inside the bounding box
                    Debug.Assert(intersection != null);

                    //"+ translatedir" in order to move the character slightly away from the wall
                    c.AnimController.SetPosition(ConvertUnits.ToSimUnits(c.WorldPosition + ((Vector2)intersection - limb.WorldPosition)) + translateDir);

                    return;
                }                

            }
        }

        private Vector2 CalculateBuoyancy()
        {
            float waterVolume = 0.0f;
            float volume = 0.0f;
            foreach (Hull hull in Hull.hullList)
            {
                if (hull.Submarine != submarine) continue;

                waterVolume += hull.WaterVolume;
                volume += hull.Volume;
            }

            float waterPercentage = volume <= 0.0f ? 0.0f : waterVolume / volume;

            float neutralPercentage = 0.07f;

            float buoyancy = neutralPercentage - waterPercentage;

            if (buoyancy > 0.0f) buoyancy *= 2.0f;

            return new Vector2(0.0f, buoyancy * Body.Mass * 10.0f);
        }

        public void ApplyForce(Vector2 force)
        {
            Body.ApplyForce(force);
        }

        public void SetPosition(Vector2 position)
        {
            Body.SetTransform(ConvertUnits.ToSimUnits(position), 0.0f);
        }

        private void UpdateDepthDamage(float deltaTime)
        {
            if (Position.Y > DamageDepth) return;

            float depth = DamageDepth - Position.Y;

            depthDamageTimer -= deltaTime;

            if (depthDamageTimer > 0.0f) return;

            foreach (Structure wall in Structure.WallList)
            {
                if (wall.Submarine != submarine) continue;

                if (wall.Health < depth * 0.01f)
                {
                    Explosion.RangedStructureDamage(wall.WorldPosition, 100.0f, depth * 0.01f);

                    if (Character.Controlled != null && Character.Controlled.Submarine == submarine)
                    {
                        GameMain.GameScreen.Cam.Shake = Math.Max(GameMain.GameScreen.Cam.Shake, Math.Min(depth * 0.001f, 50.0f));
                    }
                }
            }

            depthDamageTimer = 10.0f;
        }

        public bool OnCollision(Fixture f1, Fixture f2, Contact contact)
        {
            Limb limb = f2.Body.UserData as Limb;
            if (limb != null)
            {
                bool collision = CheckLimbCollision(contact, limb);
                if (collision) HandleLimbCollision(contact, limb);
                return collision;
            }

            VoronoiCell cell = f2.Body.UserData as VoronoiCell;
            if (cell != null)
            {
                HandleLevelCollision(contact, Vector2.Normalize(ConvertUnits.ToDisplayUnits(Body.SimPosition) - cell.Center));
                return true;
            }

            Structure structure = f2.Body.UserData as Structure;
            if (structure != null)
            {
                Vector2 normal;
                FixedArray2<Vector2> points;
                contact.GetWorldManifold(out normal, out points);
                if (contact.FixtureA.Body == f1.Body)
                {
                    normal = -normal;
                }

                HandleLevelCollision(contact, normal);
                return true;
            }

            Submarine otherSub = f2.Body.UserData as Submarine;
            if (otherSub != null)
            {
                HandleSubCollision(contact, otherSub);
                return true;
            }

            return true;
        }

        private bool CheckLimbCollision(Contact contact, Limb limb)
        {
            if (limb.character.Submarine != null) return false;

            Vector2 contactNormal;
            FixedArray2<Vector2> points;
            contact.GetWorldManifold(out contactNormal, out points);

            Vector2 normalizedVel = limb.character.AnimController.Collider.LinearVelocity == Vector2.Zero ?
                Vector2.Zero : Vector2.Normalize(limb.character.AnimController.Collider.LinearVelocity);

            Vector2 targetPos = ConvertUnits.ToDisplayUnits(points[0] - contactNormal);
            Hull newHull = Hull.FindHull(targetPos, null);

            if (newHull == null)
            {
                targetPos = ConvertUnits.ToDisplayUnits(points[0] + normalizedVel);
                newHull = Hull.FindHull(targetPos, null);
                if (newHull == null) return true;
            }

            var gaps = newHull.ConnectedGaps;
            targetPos = limb.character.WorldPosition;
            Gap adjacentGap = Gap.FindAdjacent(gaps, targetPos, 200.0f);
            if (adjacentGap == null) return true;

            var ragdoll = limb.character.AnimController;
            ragdoll.FindHull(newHull.WorldPosition, true);

            return false;
        }

        private void HandleLimbCollision(Contact contact, Limb limb)
        {
            if (limb.Mass > 100.0f)
            {
                Vector2 normal = Vector2.Normalize(Body.SimPosition - limb.SimPosition);

                float impact = Math.Min(Vector2.Dot(Velocity - limb.LinearVelocity, -normal), 50.0f) / 5.0f * Math.Min(limb.Mass / 200.0f, 1);

                ApplyImpact(impact, -normal, contact);
                foreach (Submarine dockedSub in submarine.DockedTo)
                {
                    dockedSub.SubBody.ApplyImpact(impact, -normal, contact);
                }
            }

            //find all contacts between the limb and level walls
            List<Contact> levelContacts = new List<Contact>();
            ContactEdge contactEdge = limb.body.FarseerBody.ContactList;
            while (contactEdge.Next != null)
            {
                if (contactEdge.Contact.Enabled &&
                    contactEdge.Other.UserData is VoronoiCell &&
                    contactEdge.Contact.IsTouching)
                {
                    levelContacts.Add(contactEdge.Contact);
                }
                contactEdge = contactEdge.Next;
            }

            if (levelContacts.Count == 0) return;

            //if the limb is in contact with the level, apply an artifical impact to prevent the sub from bouncing on top of it
            //not a very realistic way to handle the collisions (makes it seem as if the characters were made of reinforced concrete),
            //but more realistic than bouncing and prevents using characters as "bumpers" that prevent all collision damage

            //TODO: apply impact damage and/or gib the character that got crushed between the sub and the level?
            Vector2 avgContactNormal = Vector2.Zero;
            foreach (Contact levelContact in levelContacts)
            {
                Vector2 contactNormal;
                FixedArray2<Vector2> temp;
                levelContact.GetWorldManifold(out contactNormal, out temp);

                //if the contact normal is pointing from the limb towards the level cell it's touching, flip the normal
                VoronoiCell cell = levelContact.FixtureB.Body.UserData is VoronoiCell ?
                    ((VoronoiCell)levelContact.FixtureB.Body.UserData) : ((VoronoiCell)levelContact.FixtureA.Body.UserData);

                var cellDiff = ConvertUnits.ToDisplayUnits(limb.body.SimPosition) - cell.Center;
                if (Vector2.Dot(contactNormal, cellDiff) < 0)
                {
                    contactNormal = -contactNormal;
                }

                avgContactNormal += contactNormal;

                //apply impacts at the positions where this sub is touching the limb
                ApplyImpact((Vector2.Dot(-Velocity, contactNormal) / 2.0f) / levelContacts.Count, contactNormal, levelContact);
            }
            avgContactNormal /= levelContacts.Count;
            
            float contactDot = Vector2.Dot(Body.LinearVelocity, -avgContactNormal);
            if (contactDot > 0.0f)
            {
                Body.LinearVelocity -= Vector2.Normalize(Body.LinearVelocity) * contactDot;

                float damageAmount = contactDot * Body.Mass / limb.character.Mass;

                Vector2 n;
                FixedArray2<Vector2> contactPos;
                contact.GetWorldManifold(out n, out contactPos);
                limb.character.DamageLimb(ConvertUnits.ToDisplayUnits(contactPos[0]), limb, DamageType.Blunt, damageAmount, 0.0f, 0.0f, true, 0.0f);

                if (limb.character.IsDead)
                {
                    foreach (LimbJoint limbJoint in limb.character.AnimController.LimbJoints)
                    {
                        if (limbJoint.IsSevered || (limbJoint.LimbA != limb && limbJoint.LimbB != limb)) continue;
                        limb.character.AnimController.SeverLimbJoint(limbJoint);
                    }
                }
            }
        }

        private void HandleLevelCollision(Contact contact, Vector2 collisionNormal)
        {
            float wallImpact = Vector2.Dot(Velocity, -collisionNormal);

            ApplyImpact(wallImpact, -collisionNormal, contact);
            foreach (Submarine dockedSub in submarine.DockedTo)
            {
                dockedSub.SubBody.ApplyImpact(wallImpact, -collisionNormal, contact);
            }

#if CLIENT
            Vector2 n;
            FixedArray2<Vector2> particlePos;
            contact.GetWorldManifold(out n, out particlePos);
            
            int particleAmount = (int)Math.Min(wallImpact * 10.0f, 50);
            for (int i = 0; i < particleAmount; i++)
            {
                GameMain.ParticleManager.CreateParticle("iceshards",
                    ConvertUnits.ToDisplayUnits(particlePos[0]) + Rand.Vector(Rand.Range(1.0f, 50.0f)),
                    Rand.Vector(Rand.Range(50.0f, 500.0f)) + Velocity);
            }
#endif
        }

        private void HandleSubCollision(Contact contact, Submarine otherSub)
        {
            Debug.Assert(otherSub != submarine);

            Vector2 normal;
            FixedArray2<Vector2> points;
            contact.GetWorldManifold(out normal, out points);
            if (contact.FixtureA.Body == otherSub.SubBody.Body.FarseerBody)
            {
                normal = -normal;
            }

            float thisMass = Body.Mass + submarine.DockedTo.Sum(s => s.PhysicsBody.Mass);
            float otherMass = otherSub.PhysicsBody.Mass + otherSub.DockedTo.Sum(s => s.PhysicsBody.Mass);
            float massRatio = otherMass / (thisMass + otherMass);

            float impact = (Vector2.Dot(Velocity - otherSub.Velocity, normal) / 2.0f) * massRatio;

            //apply impact to this sub (the other sub takes care of this in its own collision callback)
            ApplyImpact(impact, normal, contact);
            foreach (Submarine dockedSub in submarine.DockedTo)
            {
                dockedSub.SubBody.ApplyImpact(impact, normal, contact);
            }

            //find all contacts between this sub and level walls
            List<Contact> levelContacts = new List<Contact>();
            ContactEdge contactEdge = Body.FarseerBody.ContactList;
            while (contactEdge.Next != null)
            {
                if (contactEdge.Contact.Enabled &&
                    contactEdge.Other.UserData is VoronoiCell &&
                    contactEdge.Contact.IsTouching)
                {
                    levelContacts.Add(contactEdge.Contact);
                }

                contactEdge = contactEdge.Next;
            }

            if (levelContacts.Count == 0) return;
            
            //if this sub is in contact with the level, apply artifical impacts
            //to both subs to prevent the other sub from bouncing on top of this one 
            //and to fake the other sub "crushing" this one against a wall
            Vector2 avgContactNormal = Vector2.Zero;
            foreach (Contact levelContact in levelContacts)
            {
                Vector2 contactNormal;
                FixedArray2<Vector2> temp;
                levelContact.GetWorldManifold(out contactNormal, out temp);

                //if the contact normal is pointing from the sub towards the level cell we collided with, flip the normal
                VoronoiCell cell = levelContact.FixtureB.Body.UserData is VoronoiCell ? 
                    ((VoronoiCell)levelContact.FixtureB.Body.UserData) : ((VoronoiCell)levelContact.FixtureA.Body.UserData);

                var cellDiff = ConvertUnits.ToDisplayUnits(Body.SimPosition) - cell.Center;
                if (Vector2.Dot(contactNormal, cellDiff) < 0)
                {
                    contactNormal = -contactNormal;
                }

                avgContactNormal += contactNormal;

                //apply impacts at the positions where this sub is touching the level
                ApplyImpact((Vector2.Dot(Velocity - otherSub.Velocity, contactNormal) / 2.0f) * massRatio / levelContacts.Count, contactNormal, levelContact);
            }
            avgContactNormal /= levelContacts.Count;

            //apply an impact to the other sub
            float contactDot = Vector2.Dot(otherSub.PhysicsBody.LinearVelocity, -avgContactNormal);
            if (contactDot > 0.0f)
            {
                otherSub.PhysicsBody.LinearVelocity -= Vector2.Normalize(otherSub.PhysicsBody.LinearVelocity) * contactDot;

                impact = Vector2.Dot(otherSub.Velocity, normal);
                otherSub.SubBody.ApplyImpact(impact, normal, contact);
                foreach (Submarine dockedSub in otherSub.DockedTo)
                {
                    dockedSub.SubBody.ApplyImpact(impact, normal, contact);
                }
            }            
        }

        private void ApplyImpact(float impact, Vector2 direction, Contact contact)
        {
            if (impact < 3.0f) return;

            Vector2 tempNormal;
            FixedArray2<Vector2> worldPoints;
            contact.GetWorldManifold(out tempNormal, out worldPoints);

            Vector2 lastContactPoint = worldPoints[0];

            if (Character.Controlled != null && Character.Controlled.Submarine == submarine)
            {
                GameMain.GameScreen.Cam.Shake = impact * 2.0f;
            }

            Vector2 impulse = direction * impact * 0.5f;

            float length = impulse.Length();
            if (length > 5.0f) impulse = (impulse / length) * 5.0f;

            foreach (Character c in Character.CharacterList)
            {
                if (c.Submarine != submarine) continue;

                if (impact > 2.0f) c.SetStun((impact - 2.0f) * 0.1f);

                foreach (Limb limb in c.AnimController.Limbs)
                {
                    limb.body.ApplyLinearImpulse(limb.Mass * impulse);
                }

                c.AnimController.Collider.ApplyLinearImpulse(c.AnimController.Collider.Mass * impulse);
            }

            foreach (Item item in Item.ItemList)
            {
                if (item.Submarine != submarine || item.CurrentHull == null || 
                    item.body == null || !item.body.Enabled) continue;

                item.body.ApplyLinearImpulse(item.body.Mass * impulse);                
            }

            var damagedStructures = Explosion.RangedStructureDamage(ConvertUnits.ToDisplayUnits(lastContactPoint), impact * 50.0f, impact * ImpactDamageMultiplier);

#if CLIENT
            //play a damage sound for the structure that took the most damage
            float maxDamage = 0.0f;
            Structure maxDamageStructure = null;
            foreach (KeyValuePair<Structure,float> structureDamage in damagedStructures)
            {
                if (maxDamageStructure == null || structureDamage.Value > maxDamage)
                {
                    maxDamage = structureDamage.Value;
                    maxDamageStructure = structureDamage.Key;
                }
            }

            if (maxDamageStructure != null)
            {
                SoundPlayer.PlayDamageSound(
                    "StructureBlunt",
                    impact * 10.0f,
                    ConvertUnits.ToDisplayUnits(lastContactPoint),
                    MathHelper.Clamp(maxDamage * 4.0f, 1000.0f, 4000.0f),
                    maxDamageStructure.Tags);            
            }
#endif
        }

    }
}
