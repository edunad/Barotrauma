﻿using Barotrauma.Networking;
using FarseerPhysics;
using FarseerPhysics.Dynamics;
using Lidgren.Network;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Voronoi2;

namespace Barotrauma
{
    public enum Direction : byte
    {
        None = 0, Left = 1, Right = 2
    }

    [Flags]
    public enum SubmarineTag
    {
        [Description("Shuttle")]
        Shuttle = 1,
        [Description("Hide in menus")]
        HideInMenus = 2
    }

    partial class Submarine : Entity, IServerSerializable
    {
        public byte TeamID = 1;

        public static string SavePath = "Submarines";

        public static readonly Vector2 HiddenSubStartPosition = new Vector2(-50000.0f, 10000.0f);
        //position of the "actual submarine" which is rendered wherever the SubmarineBody is 
        //should be in an unreachable place
        public Vector2 HiddenSubPosition
        {
            get;
            private set;
        }

        public ushort IdOffset
        {
            get;
            private set;
        }

        public static bool LockX, LockY;

        public static List<Submarine> SavedSubmarines = new List<Submarine>();
        
        public static readonly Vector2 GridSize = new Vector2(16.0f, 16.0f);

        public static Submarine[] MainSubs = new Submarine[2];
        public static Submarine MainSub
        {
            get { return MainSubs[0]; }
            set { MainSubs[0] = value; }
        }
        private static List<Submarine> loaded = new List<Submarine>();

        private static List<MapEntity> visibleEntities;

        private SubmarineBody subBody;

        public readonly List<Submarine> DockedTo;

        private static Vector2 lastPickedPosition;
        private static float lastPickedFraction;

        private Md5Hash hash;
        
        private string filePath;
        private string name;

        private SubmarineTag tags;

        private Vector2 prevPosition;

        private float networkUpdateTimer;

        private EntityGrid entityGrid = null;

        public int RecommendedCrewSizeMin = 1, RecommendedCrewSizeMax = 2;
        public string RecommendedCrewExperience;

        public HashSet<string> CompatibleContentPackages = new HashSet<string>();
        
        //properties ----------------------------------------------------

        public string Name
        {
            get { return name; }
            set { name = value; }
        }

        public bool OnRadar = true;

        public string Description
        {
            get; 
            set; 
        }
        
        public static Vector2 LastPickedPosition
        {
            get { return lastPickedPosition; }
        }

        public static float LastPickedFraction
        {
            get { return lastPickedFraction; }
        }

        public bool Loading
        {
            get;
            private set;
        }

        public bool GodMode
        {
            get;
            set;
        }

        public Md5Hash MD5Hash
        {
            get
            {
                if (hash != null) return hash;

                XDocument doc = OpenFile(filePath);
                hash = new Md5Hash(doc);

                return hash;
            }
        }
        
        public static List<Submarine> Loaded
        {
            get { return loaded; }
        }

        public SubmarineBody SubBody
        {
            get { return subBody; }
        }

        public PhysicsBody PhysicsBody
        {
            get { return subBody.Body; }
        }

        public Rectangle Borders
        {
            get 
            {
                return subBody.Borders;
            }
        }

        public Vector2 Dimensions
        {
            get;
            private set;
        }

        public override Vector2 Position
        {
            get { return subBody == null ? Vector2.Zero : subBody.Position - HiddenSubPosition; }
        }

        public override Vector2 WorldPosition
        {
            get
            {
                return subBody == null ? Vector2.Zero : subBody.Position;
            }
        }

        public bool AtEndPosition
        {
            get 
            {
                if (Level.Loaded == null) return false;
                return (Vector2.Distance(Position + HiddenSubPosition, Level.Loaded.EndPosition) < Level.ExitDistance);
            }
        }

        public bool AtStartPosition
        {
            get
            {
                if (Level.Loaded == null) return false;
                return (Vector2.Distance(Position + HiddenSubPosition, Level.Loaded.StartPosition) < Level.ExitDistance);
            }
        }

        public new Vector2 DrawPosition
        {
            get;
            private set;
        }

        public override Vector2 SimPosition
        {
            get
            {
                return ConvertUnits.ToSimUnits(Position);
            }
        }
        
        public Vector2 Velocity
        {
            get { return subBody==null ? Vector2.Zero : subBody.Velocity; }
            set
            {
                if (subBody == null) return;
                subBody.Velocity = value;
            }
        }

        public List<Vector2> HullVertices
        {
            get { return subBody.HullVertices; }
        }


        public string FilePath
        {
            get { return filePath; }
            set { filePath = value; }
        }

        public bool AtDamageDepth
        {
            get { return subBody != null && subBody.AtDamageDepth; }
        }

        public override string ToString()
        {
            return "Barotrauma.Submarine ("+name+")";
        }

        public override bool Removed
        {
            get
            {
                return !loaded.Contains(this);
            }
        }

        //constructors & generation ----------------------------------------------------

        public Submarine(string filePath, string hash = "", bool tryLoad = true) : base(null)
        {
            this.filePath = filePath;
            try
            {
                name = System.IO.Path.GetFileNameWithoutExtension(filePath);
            }
            catch (Exception e)
            {
                DebugConsole.ThrowError("Error loading submarine " + filePath + "!", e);
            }

            if (hash != "")
            {
                this.hash = new Md5Hash(hash);
            }

            if (tryLoad)
            {
                XDocument doc = OpenFile(filePath);

                if (doc != null && doc.Root != null)
                {
                    Description = doc.Root.GetAttributeString("description", "");
                    Enum.TryParse(doc.Root.GetAttributeString("tags", ""), out tags);
                    Dimensions = doc.Root.GetAttributeVector2("dimensions", Vector2.Zero);
                    RecommendedCrewSizeMin = doc.Root.GetAttributeInt("recommendedcrewsizemin", 0);
                    RecommendedCrewSizeMax = doc.Root.GetAttributeInt("recommendedcrewsizemax", 0);
                    RecommendedCrewExperience = doc.Root.GetAttributeString("recommendedcrewexperience", "Unknown");
                    string[] contentPackageNames = doc.Root.GetAttributeStringArray("compatiblecontentpackages", new string[0]);
                    foreach (string contentPackageName in contentPackageNames)
                    {
                        CompatibleContentPackages.Add(contentPackageName);
                    }

#if CLIENT                    
                    string previewImageData = doc.Root.GetAttributeString("previewimage", "");
                    if (!string.IsNullOrEmpty(previewImageData))
                    {
                        using (MemoryStream mem = new MemoryStream(Convert.FromBase64String(previewImageData)))
                        {
                            PreviewImage = new Sprite(TextureLoader.FromStream(mem), null, null);
                        }
                    }
#endif
                }
            }

            DockedTo = new List<Submarine>();

            ID = ushort.MaxValue;
            base.Remove();
        }

        public bool HasTag(SubmarineTag tag)
        {
            return tags.HasFlag(tag);
        }

        public void AddTag(SubmarineTag tag)
        {
            if (tags.HasFlag(tag)) return;

            tags |= tag;
        }

        public void RemoveTag(SubmarineTag tag)
        {
            if (!tags.HasFlag(tag)) return;

            tags &= ~tag;
        }

        /// <summary>
        /// Returns a rect that contains the borders of this sub and all subs docked to it
        /// </summary>
        public Rectangle GetDockedBorders()
        {
            Rectangle dockedBorders = Borders;
            dockedBorders.Y -= dockedBorders.Height;

            var connectedSubs = GetConnectedSubs();

            foreach (Submarine dockedSub in connectedSubs)
            {
                if (dockedSub == this) continue;

                Vector2 diff = dockedSub.Submarine == this ? dockedSub.WorldPosition : dockedSub.WorldPosition - WorldPosition;                    

                Rectangle dockedSubBorders = dockedSub.Borders;
                dockedSubBorders.Y -= dockedSubBorders.Height;
                dockedSubBorders.Location += MathUtils.ToPoint(diff);

                dockedBorders = Rectangle.Union(dockedBorders, dockedSubBorders);
            }

            dockedBorders.Y += dockedBorders.Height;
            return dockedBorders;
        }

        /// <summary>
        /// Returns a list of all submarines that are connected to this one via docking ports.
        /// </summary>
        public List<Submarine> GetConnectedSubs()
        {
            List<Submarine> connectedSubs = new List<Submarine>();
            connectedSubs.Add(this);
            GetConnectedSubsRecursive(connectedSubs);

            return connectedSubs;
        }

        private void GetConnectedSubsRecursive(List<Submarine> subs)
        {
            foreach (Submarine dockedSub in DockedTo)
            {
                if (subs.Contains(dockedSub)) continue;

                subs.Add(dockedSub);
                dockedSub.GetConnectedSubsRecursive(subs);
            }
        }

        public Vector2 FindSpawnPos(Vector2 spawnPos)
        {
            Rectangle dockedBorders = GetDockedBorders();
            
            int iterations = 0;
            bool wallTooClose = false;
            do
            {
                Rectangle worldBorders = new Rectangle(
                    dockedBorders.X + (int)spawnPos.X,
                    dockedBorders.Y + (int)spawnPos.Y, 
                    dockedBorders.Width, 
                    dockedBorders.Height);

                wallTooClose = false;

                var nearbyCells = Level.Loaded.GetCells(
                    spawnPos, (int)Math.Ceiling(Math.Max(dockedBorders.Width, dockedBorders.Height) / (float)Level.GridCellSize));

                foreach (VoronoiCell cell in nearbyCells)
                {
                    if (cell.CellType == CellType.Empty) continue;

                    foreach (GraphEdge e in cell.edges)
                    {
                        List<Vector2> intersections = MathUtils.GetLineRectangleIntersections(e.point1, e.point2, worldBorders);
                        foreach (Vector2 intersection in intersections)
                        {
                            wallTooClose = true;

                            if (intersection.X < spawnPos.X)
                            {
                                spawnPos.X += intersection.X - worldBorders.X;
                            }
                            else
                            {
                                spawnPos.X += intersection.X - worldBorders.Right;
                            }

                            if (intersection.Y < spawnPos.Y)
                            {
                                spawnPos.Y += intersection.Y - (worldBorders.Y - worldBorders.Height);
                            }
                            else
                            {
                                spawnPos.Y += intersection.Y - worldBorders.Y;
                            }

                            spawnPos.Y = Math.Min(spawnPos.Y, Level.Loaded.Size.Y - dockedBorders.Height / 2);
                        }
                    }
                }

                iterations++;
            } while (wallTooClose && iterations < 10);

            return spawnPos;

        
        }
        
        //drawing ----------------------------------------------------

        public static void CullEntities(Camera cam)
        {
            HashSet<Submarine> visibleSubs = new HashSet<Submarine>();
            foreach (Submarine sub in Loaded)
            {
                if (sub.WorldPosition.Y < Level.MaxEntityDepth) continue;

                Rectangle worldBorders = new Rectangle(
                    sub.Borders.X + (int)sub.WorldPosition.X - 500,
                    sub.Borders.Y + (int)sub.WorldPosition.Y + 500,
                    sub.Borders.Width + 1000,
                    sub.Borders.Height + 1000);

                if (RectsOverlap(worldBorders, cam.WorldView))
                {
                    visibleSubs.Add(sub);
                }
            }

            Rectangle worldView = cam.WorldView;

            visibleEntities = new List<MapEntity>();
            foreach (MapEntity me in MapEntity.mapEntityList)
            {
                if (me.Submarine == null || visibleSubs.Contains(me.Submarine))
                {
                    if (me.IsVisible(worldView)) visibleEntities.Add(me);
                }
            }
        }

        public void UpdateTransform()
        {
            DrawPosition = Timing.Interpolate(prevPosition, Position);
        }

        //math/physics stuff ----------------------------------------------------

        public static Vector2 MouseToWorldGrid(Camera cam, Submarine sub)
        {
            Vector2 position = PlayerInput.MousePosition;
            position = cam.ScreenToWorld(position);

            Vector2 worldGridPos = VectorToWorldGrid(position);

            if (sub != null)
            {
                worldGridPos.X += sub.Position.X % GridSize.X;
                worldGridPos.Y += sub.Position.Y % GridSize.Y;
            }

            return worldGridPos;
        }

        public static Vector2 VectorToWorldGrid(Vector2 position)
        {
            position.X = (float)Math.Floor(position.X / GridSize.X) * GridSize.X;
            position.Y = (float)Math.Ceiling(position.Y / GridSize.Y) * GridSize.Y;

            return position;
        }

        public Rectangle CalculateDimensions(bool onlyHulls = true)
        {
            List<MapEntity> entities = onlyHulls ? 
                Hull.hullList.FindAll(h => h.Submarine == this).Cast<MapEntity>().ToList() : 
                MapEntity.mapEntityList.FindAll(me => me.Submarine == this);

            if (entities.Count == 0) return Rectangle.Empty;

            float minX = entities[0].Rect.X, minY = entities[0].Rect.Y - entities[0].Rect.Height;
            float maxX = entities[0].Rect.Right, maxY = entities[0].Rect.Y;

            for (int i = 1; i < entities.Count; i++)
            {
                minX = Math.Min(minX, entities[i].Rect.X);
                minY = Math.Min(minY, entities[i].Rect.Y - entities[i].Rect.Height);
                maxX = Math.Max(maxX, entities[i].Rect.Right);
                maxY = Math.Max(maxY, entities[i].Rect.Y);
            }

            return new Rectangle((int)minX, (int)minY, (int)(maxX - minX), (int)(maxY - minY));
        }
        
        public static Rectangle AbsRect(Vector2 pos, Vector2 size)
        {
            if (size.X < 0.0f)
            {
                pos.X += size.X;
                size.X = -size.X;
            }
            if (size.Y < 0.0f)
            {
                pos.Y -= size.Y;
                size.Y = -size.Y;
            }
            
            return new Rectangle((int)pos.X, (int)pos.Y, (int)size.X, (int)size.Y);
        }

        public static bool RectContains(Rectangle rect, Vector2 pos, bool inclusive = false)
        {
            if (inclusive)
            {
                return (pos.X >= rect.X && pos.X <= rect.X + rect.Width
                    && pos.Y <= rect.Y && pos.Y >= rect.Y - rect.Height);
            }
            else
            {
                return (pos.X > rect.X && pos.X < rect.X + rect.Width
                    && pos.Y < rect.Y && pos.Y > rect.Y - rect.Height);
            }
        }

        public static bool RectsOverlap(Rectangle rect1, Rectangle rect2, bool inclusive=true)
        {
            if (inclusive)
            {
                return !(rect1.X > rect2.X + rect2.Width || rect1.X + rect1.Width < rect2.X ||
                    rect1.Y < rect2.Y - rect2.Height || rect1.Y - rect1.Height > rect2.Y);
            }
            else
            {
                return !(rect1.X >= rect2.X + rect2.Width || rect1.X + rect1.Width <= rect2.X ||
                    rect1.Y <= rect2.Y - rect2.Height || rect1.Y - rect1.Height >= rect2.Y);
            }
        }

        public static Body PickBody(Vector2 rayStart, Vector2 rayEnd, List<Body> ignoredBodies = null, Category? collisionCategory = null, bool ignoreSensors = true)
        {
            if (Vector2.DistanceSquared(rayStart, rayEnd) < 0.00001f)
            {
                rayEnd += Vector2.UnitX * 0.001f;
            }

            float closestFraction = 1.0f;
            Body closestBody = null;
            GameMain.World.RayCast((fixture, point, normal, fraction) =>
            {
                if (fixture == null ||
                    (ignoreSensors && fixture.IsSensor) ||
                    fixture.CollisionCategories == Category.None || 
                    fixture.CollisionCategories == Physics.CollisionItem) return -1;
                
                if (collisionCategory != null && 
                    !fixture.CollisionCategories.HasFlag((Category)collisionCategory) &&
                    !((Category)collisionCategory).HasFlag(fixture.CollisionCategories)) return -1;
      
                if (ignoredBodies != null && ignoredBodies.Contains(fixture.Body)) return -1;
                
                Structure structure = fixture.Body.UserData as Structure;                
                if (structure != null)
                {
                    if (structure.IsPlatform && collisionCategory != null && !((Category)collisionCategory).HasFlag(Physics.CollisionPlatform)) return -1;
                }                    

                if (fraction < closestFraction)
                {
                    closestFraction = fraction;
                    if (fixture.Body!=null) closestBody = fixture.Body;
                }
                return fraction;
            }
            , rayStart, rayEnd);

            lastPickedPosition = rayStart + (rayEnd - rayStart) * closestFraction;
            lastPickedFraction = closestFraction;
            
            return closestBody;
        }
        
        /// <summary>
        /// check visibility between two points (in sim units)
        /// </summary>
        /// <returns>a physics body that was between the points (or null)</returns>
        public static Body CheckVisibility(Vector2 rayStart, Vector2 rayEnd, bool ignoreLevel = false, bool ignoreSubs = false, bool ignoreSensors = true)
        {
            Body closestBody = null;
            float closestFraction = 1.0f;

            if (Vector2.Distance(rayStart, rayEnd) < 0.01f)
            {
                lastPickedPosition = rayEnd;
                return null;
            }
            
            GameMain.World.RayCast((fixture, point, normal, fraction) =>
            {
                if (fixture == null ||
                    (ignoreSensors && fixture.IsSensor) ||
                    (!fixture.CollisionCategories.HasFlag(Physics.CollisionWall) && !fixture.CollisionCategories.HasFlag(Physics.CollisionLevel))) return -1;

                if (ignoreLevel && fixture.CollisionCategories == Physics.CollisionLevel) return -1;
                if (ignoreSubs && fixture.Body.UserData is Submarine) return -1;

                Structure structure = fixture.Body.UserData as Structure;
                if (structure != null)
                {
                    if (structure.IsPlatform || structure.StairDirection != Direction.None) return -1;
                    int sectionIndex = structure.FindSectionIndex(ConvertUnits.ToDisplayUnits(point));
                    if (sectionIndex > -1 && structure.SectionBodyDisabled(sectionIndex)) return -1;
                }

                if (fraction < closestFraction)
                {
                    closestBody = fixture.Body;
                    closestFraction = fraction;
                }
                return closestFraction;
            }
            , rayStart, rayEnd);


            lastPickedPosition = rayStart + (rayEnd - rayStart) * closestFraction;
            lastPickedFraction = closestFraction;
            return closestBody;
        }

        //movement ----------------------------------------------------

        private bool flippedX;
        public bool FlippedX
        {
            get { return flippedX; }
        }

        public void FlipX(List<Submarine> parents=null)
        {
            if (parents == null) parents = new List<Submarine>();
            parents.Add(this);

            flippedX = !flippedX;

            Item.UpdateHulls();

            List<Item> bodyItems = Item.ItemList.FindAll(it => it.Submarine == this && it.body != null);

            List<MapEntity> subEntities = MapEntity.mapEntityList.FindAll(me => me.Submarine == this);

            foreach (MapEntity e in subEntities)
            {
                if (e.MoveWithLevel || e is Item) continue;
                
                if (e is LinkedSubmarine)
                {
                    Submarine sub = ((LinkedSubmarine)e).Sub;
                    if (!parents.Contains(sub))
                    {
                        Vector2 relative1 = sub.SubBody.Position - SubBody.Position;
                        relative1.X = -relative1.X;
                        sub.SetPosition(relative1 + SubBody.Position);
                        sub.FlipX(parents);
                    }
                }
                else
                {
                    e.FlipX();
                }
            }

            foreach (MapEntity mapEntity in subEntities)
            {
                mapEntity.Move(-HiddenSubPosition);
            }

            Vector2 pos = new Vector2(subBody.Position.X, subBody.Position.Y);
            subBody.Body.Remove();
            subBody = new SubmarineBody(this);
            SetPosition(pos);

            if (entityGrid != null)
            {
                Hull.EntityGrids.Remove(entityGrid);
                entityGrid = null;
            }
            entityGrid = Hull.GenerateEntityGrid(this);

            foreach (MapEntity mapEntity in subEntities)
            {
                mapEntity.Move(HiddenSubPosition);
            }

            foreach (Item item in Item.ItemList)
            {
                if (bodyItems.Contains(item))
                {
                    item.Submarine = this;             
                    if (Position == Vector2.Zero) item.Move(-HiddenSubPosition);
                }
                else if (item.Submarine != this)
                {
                    continue;
                }

                item.FlipX();
            }

            Item.UpdateHulls();
            Gap.UpdateHulls();
        }

        public void Update(float deltaTime)
        {
            //if (PlayerInput.KeyHit(InputType.Crouch) && (this == MainSub)) FlipX();

            if (Level.Loaded == null || subBody == null) return;

            if (WorldPosition.Y < Level.MaxEntityDepth &&
                subBody.Body.Enabled && 
                (GameMain.NetworkMember?.RespawnManager == null || this != GameMain.NetworkMember.RespawnManager.RespawnShuttle))
            {
                subBody.Body.ResetDynamics();
                subBody.Body.Enabled = false;

                foreach (MapEntity e in MapEntity.mapEntityList)
                {
                    if (e.Submarine == this)
                    {
                        Spawner.AddToRemoveQueue(e);
                    }
                }

                foreach (Character c in Character.CharacterList)
                {
                    if (c.Submarine == this)
                    {
                        c.Kill(CauseOfDeath.Pressure);
                        c.Enabled = false;
                    }
                }

                return;
            }

            subBody.Body.LinearVelocity = new Vector2(
                LockX ? 0.0f : subBody.Body.LinearVelocity.X, 
                LockY ? 0.0f : subBody.Body.LinearVelocity.Y);
                
            
            subBody.Update(deltaTime);

            for (int i = 0; i < 2; i++ )
            {
                if (MainSubs[i] == null) continue;
                if (this != MainSubs[i] && MainSubs[i].DockedTo.Contains(this)) return;
            }

            //send updates more frequently if moving fast
            networkUpdateTimer -= MathHelper.Clamp(Velocity.Length()*10.0f, 0.1f, 5.0f) * deltaTime;

            if (networkUpdateTimer < 0.0f)
            {
                networkUpdateTimer = 1.0f;
            }
            
        }

        public void ApplyForce(Vector2 force)
        {
            if (subBody != null) subBody.ApplyForce(force);
        }

        public void SetPrevTransform(Vector2 position)
        {
            prevPosition = position;
        }

        public void SetPosition(Vector2 position)
        {
            if (!MathUtils.IsValid(position)) return;
            
            subBody.SetPosition(position);

            foreach (Submarine sub in loaded)
            {
                if (sub != this && sub.Submarine == this)
                {
                    sub.SetPosition(position + sub.WorldPosition);
                    sub.Submarine = null;
                }

            }
            //Level.Loaded.SetPosition(-position);
            //prevPosition = position;
        }

        public void Translate(Vector2 amount)
        {
            if (amount == Vector2.Zero || !MathUtils.IsValid(amount)) return;

            subBody.SetPosition(subBody.Position + amount);

            //Level.Loaded.Move(-amount);
        }

        public static Submarine FindClosest(Vector2 worldPosition)
        {
            Submarine closest = null;
            float closestDist = 0.0f;
            foreach (Submarine sub in loaded)
            {
                float dist = Vector2.Distance(worldPosition, sub.WorldPosition);
                if (closest == null || dist < closestDist)
                {
                    closest = sub;
                    closestDist = dist;
                }
            }

            return closest;
        }

        /// <summary>
        /// Finds the sub whose borders contain the position
        /// </summary>
        public static Submarine FindContaining(Vector2 position)
        {
            foreach (Submarine sub in Submarine.Loaded)
            {
                Rectangle subBorders = sub.Borders;
                subBorders.Location += MathUtils.ToPoint(sub.HiddenSubPosition) - new Microsoft.Xna.Framework.Point(0, sub.Borders.Height);

                subBorders.Inflate(500.0f, 500.0f);

                if (subBorders.Contains(position)) return sub;                
            }

            return null;
        }

        //saving/loading ----------------------------------------------------

        public static void RefreshSavedSubs()
        {
            SavedSubmarines.Clear();

            if (!Directory.Exists(SavePath))
            {
                try
                {
                    Directory.CreateDirectory(SavePath);
                }
                catch (Exception e)
                {
                    DebugConsole.ThrowError("Directory \"" + SavePath + "\" not found and creating the directory failed.", e);
                    return;
                }
            }

            List<string> filePaths;
            string[] subDirectories;

            try
            {
                filePaths = Directory.GetFiles(SavePath).ToList();
                subDirectories = Directory.GetDirectories(SavePath);
            }
            catch (Exception e)
            {
                DebugConsole.ThrowError("Couldn't open directory \"" + SavePath + "\"!", e);
                return;
            }

            foreach (string subDirectory in subDirectories)
            {
                try
                {
                    filePaths.AddRange(Directory.GetFiles(subDirectory).ToList());
                }
                catch (Exception e)
                {
                    DebugConsole.ThrowError("Couldn't open subdirectory \"" + subDirectory + "\"!", e);
                    return;
                }
            }

            foreach (string path in filePaths)
            {
                SavedSubmarines.Add(new Submarine(path));
            }
        }

        static readonly string TempFolder = Path.Combine("Submarine", "Temp");

        public static XDocument OpenFile(string file)
        {
            XDocument doc = null;
            string extension = "";

            try
            {
                extension = System.IO.Path.GetExtension(file);
            }
            catch
            {
                //no file extension specified: try using the default one
                file += ".sub";
            }

            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".sub";
                file += ".sub";
            }

            if (extension == ".sub")
            {
                Stream stream = null;
                try
                {
                    stream = SaveUtil.DecompressFiletoStream(file);
                }
                catch (Exception e) 
                {
                    DebugConsole.ThrowError("Loading submarine \"" + file + "\" failed!", e);
                    return null;
                }                

                try
                {
                    stream.Position = 0;
                    doc = XDocument.Load(stream); //ToolBox.TryLoadXml(file);
                    stream.Close();
                    stream.Dispose();
                }

                catch (Exception e)
                {
                    DebugConsole.ThrowError("Loading submarine \"" + file + "\" failed! ("+e.Message+")");
                    return null;
                }
            }
            else if (extension == ".xml")
            {
                try
                {
                    ToolBox.IsProperFilenameCase(file);
                    doc = XDocument.Load(file);
                }

                catch (Exception e)
                {
                    DebugConsole.ThrowError("Loading submarine \"" + file + "\" failed! (" + e.Message + ")");
                    return null;
                }
            }
            else
            {
                DebugConsole.ThrowError("Couldn't load submarine \"" + file + "! (Unrecognized file extension)");
                return null;
            }

            return doc;
        }

        public void Load(bool unloadPrevious, XElement submarineElement = null)
        {
            if (unloadPrevious) Unload();

            Loading = true;

            if (submarineElement == null)
            {
                XDocument doc = OpenFile(filePath);
                if (doc == null || doc.Root == null) return;

                submarineElement = doc.Root;
            }

            Description = submarineElement.GetAttributeString("description", "");
            Enum.TryParse(submarineElement.GetAttributeString("tags", ""), out tags);
            
            //place the sub above the top of the level
            HiddenSubPosition = HiddenSubStartPosition;
            if (GameMain.GameSession != null && GameMain.GameSession.Level != null)
            {
                HiddenSubPosition += Vector2.UnitY * GameMain.GameSession.Level.Size.Y;
            }

            foreach (Submarine sub in Submarine.loaded)
            {
                HiddenSubPosition += Vector2.UnitY * (sub.Borders.Height + 5000.0f);
            }

            IdOffset = 0;
            foreach (MapEntity me in MapEntity.mapEntityList)
            {
                IdOffset = Math.Max(IdOffset, me.ID);
            }

            foreach (XElement element in submarineElement.Elements())
            {
                string typeName = element.Name.ToString();

                Type t;
                try
                {
                    t = Type.GetType("Barotrauma." + typeName, true, true);
                    if (t == null)
                    {
                        DebugConsole.ThrowError("Error in " + filePath + "! Could not find a entity of the type \"" + typeName + "\".");
                        continue;
                    }
                }
                catch (Exception e)
                {
                    DebugConsole.ThrowError("Error in " + filePath + "! Could not find a entity of the type \"" + typeName + "\".", e);
                    continue;
                }

                try
                {
                    MethodInfo loadMethod = t.GetMethod("Load");
                    loadMethod.Invoke(t, new object[] { element, this });
                }
                catch (Exception e)
                {
                    DebugConsole.ThrowError("Could not find the method \"Load\" in " + t + ".", e);
                }
            }

            Vector2 center = Vector2.Zero;

            var matchingHulls = Hull.hullList.FindAll(h => h.Submarine == this);

            if (matchingHulls.Any())
            {
                Vector2 topLeft = new Vector2(matchingHulls[0].Rect.X, matchingHulls[0].Rect.Y);
                Vector2 bottomRight = new Vector2(matchingHulls[0].Rect.X, matchingHulls[0].Rect.Y);
                foreach (Hull hull in matchingHulls)
                {
                    if (hull.Rect.X < topLeft.X) topLeft.X = hull.Rect.X;
                    if (hull.Rect.Y > topLeft.Y) topLeft.Y = hull.Rect.Y;

                    if (hull.Rect.Right > bottomRight.X) bottomRight.X = hull.Rect.Right;
                    if (hull.Rect.Y - hull.Rect.Height < bottomRight.Y) bottomRight.Y = hull.Rect.Y - hull.Rect.Height;
                }

                center = (topLeft + bottomRight) / 2.0f;
                center.X -= center.X % GridSize.X;
                center.Y -= center.Y % GridSize.Y;

                if (center != Vector2.Zero)
                {
                    foreach (Item item in Item.ItemList)
                    {
                        if (item.Submarine != this) continue;

                        var wire = item.GetComponent<Items.Components.Wire>();
                        if (wire != null)
                        {
                            wire.MoveNodes(-center);
                        }
                    }

                    for (int i = 0; i < MapEntity.mapEntityList.Count; i++)
                    {
                        if (MapEntity.mapEntityList[i].Submarine != this) continue;

                        MapEntity.mapEntityList[i].Move(-center);
                    }
                }
            }

            subBody = new SubmarineBody(this);
            subBody.SetPosition(HiddenSubPosition);

            loaded.Add(this);

            if (entityGrid != null)
            {
                Hull.EntityGrids.Remove(entityGrid);
                entityGrid = null;
            }
            entityGrid = Hull.GenerateEntityGrid(this);

            for (int i = 0; i < MapEntity.mapEntityList.Count; i++)
            {
                if (MapEntity.mapEntityList[i].Submarine != this) continue;
                MapEntity.mapEntityList[i].Move(HiddenSubPosition);
            }

            Loading = false;

            MapEntity.MapLoaded(this);

            //WayPoint.GenerateSubWaypoints();

#if CLIENT
            GameMain.LightManager.OnMapLoaded();
#endif

            ID = (ushort)(ushort.MaxValue - Submarine.loaded.IndexOf(this));
        }

        public static Submarine Load(XElement element, bool unloadPrevious)
        {
            if (unloadPrevious) Unload();

            //tryload -> false

            Submarine sub = new Submarine(element.GetAttributeString("name", ""), "", false);
            sub.Load(unloadPrevious, element);

            return sub; 
        }

        public static Submarine Load(string fileName, bool unloadPrevious)
        {
           return Load(fileName, SavePath, unloadPrevious);
        }

        public static Submarine Load(string fileName, string folder, bool unloadPrevious)
        {
            if (unloadPrevious) Unload();

            string path = string.IsNullOrWhiteSpace(folder) ? fileName : System.IO.Path.Combine(SavePath, fileName);

            Submarine sub = new Submarine(path);
            sub.Load(unloadPrevious);
            
            return sub;            
        }
        
        public bool SaveAs(string filePath, MemoryStream previewImage = null)
        {
            name = Path.GetFileNameWithoutExtension(filePath);

            XDocument doc = new XDocument(new XElement("Submarine"));
            SaveToXElement(doc.Root);

            hash = new Md5Hash(doc);
            doc.Root.Add(new XAttribute("md5hash", hash.Hash));
            if (previewImage != null)
            {
                doc.Root.Add(new XAttribute("previewimage", Convert.ToBase64String(previewImage.ToArray())));
            }

            try
            {
                SaveUtil.CompressStringToFile(filePath, doc.ToString());
            }
            catch (Exception e)
            {
                DebugConsole.ThrowError("Saving submarine \"" + filePath + "\" failed!", e);
                return false;
            }

            return true;
        }

        public void SaveToXElement(XElement element)
        {
            element.Add(new XAttribute("name", name));
            element.Add(new XAttribute("description", Description ?? ""));
            element.Add(new XAttribute("tags", tags.ToString()));

            Rectangle dimensions = CalculateDimensions();
            element.Add(new XAttribute("dimensions", XMLExtensions.Vector2ToString(dimensions.Size.ToVector2())));
            element.Add(new XAttribute("recommendedcrewsizemin", RecommendedCrewSizeMin));
            element.Add(new XAttribute("recommendedcrewsizemax", RecommendedCrewSizeMax));
            element.Add(new XAttribute("recommendedcrewexperience", RecommendedCrewExperience ?? ""));
            element.Add(new XAttribute("compatiblecontentpackages", string.Join(", ", CompatibleContentPackages)));

            foreach (MapEntity e in MapEntity.mapEntityList)
            {
                if (e.MoveWithLevel || e.Submarine != this) continue;
                e.Save(element);
            }
        }


        public static bool Unloading
        {
            get;
            private set;
        }

        public static void Unload()
        {
            Unloading = true;

#if CLIENT
            Sound.OnGameEnd();

            if (GameMain.LightManager != null) GameMain.LightManager.ClearLights();
#endif

            foreach (Submarine sub in loaded)
            {
                sub.Remove();
            }

            loaded.Clear();

            visibleEntities = null;

            if (GameMain.GameScreen.Cam != null) GameMain.GameScreen.Cam.TargetPos = Vector2.Zero;

            RemoveAll();

            if (Item.ItemList.Count > 0)
            {
                List<Item> items = new List<Item>(Item.ItemList);
                foreach (Item item in items)
                {
                    DebugConsole.ThrowError("Error while unloading submarines - item \""+item.Name+"\" not removed");
                    try
                    {
                        item.Remove();
                    }
                    catch (Exception e)
                    {
                        DebugConsole.ThrowError("Error while removing \"" + item.Name + "\"!", e);
                    }
                }
                Item.ItemList.Clear();
            }

            Ragdoll.RemoveAll();

            PhysicsBody.RemoveAll();

            GameMain.World.Clear();

            Unloading = false;
        }

        public override void Remove()
        {
            base.Remove();

            subBody = null;

            visibleEntities = null;

            if (MainSub == this) MainSub = null;
            if (MainSubs[1] == this) MainSubs[1] = null;

            DockedTo.Clear();
        }

        public void ServerWrite(NetBuffer msg, Client c, object[] extraData = null)
        {
            msg.Write(ID);
            //length in bytes
            msg.Write((byte)(4 + 4));

            msg.Write(PhysicsBody.SimPosition.X);
            msg.Write(PhysicsBody.SimPosition.Y);
        }

        public void ClientRead(ServerNetObject type, NetBuffer msg, float sendingTime)
        {
            var newTargetPosition = new Vector2(
                msg.ReadFloat(),
                msg.ReadFloat());            

            //already interpolating with more up-to-date data -> ignore
            if (subBody.MemPos.Count > 1 && subBody.MemPos[0].Timestamp > sendingTime)
            {
                return;
            }

            int index = 0;
            while (index < subBody.MemPos.Count && sendingTime > subBody.MemPos[index].Timestamp)
            {
                index++;
            }

            //position with the same timestamp already in the buffer (duplicate packet?)
            //  -> no need to add again
            if (index < subBody.MemPos.Count && sendingTime == subBody.MemPos[index].Timestamp)
            {
                return;
            }
            
            subBody.MemPos.Insert(index, new PosInfo(newTargetPosition, 0.0f, sendingTime));
        }
    }

}
