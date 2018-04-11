using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Barotrauma.Lights
{
    class LightSource
    {
        private static Texture2D lightTexture;

        private List<ConvexHullList> hullsInRange;

        private Color color;
        private float range;
        
        public Sprite overrideLightTexture;
        public Texture2D texture;
        
        //Additional sprite drawn on top of the lightsource. Ignores shadows.
        //Can be used to make lamp sprites glow for example.
        public Sprite LightSprite;

        //Override the alpha value of the light sprite (if not set, the alpha of the light color is used)
        //Can be used to make lamp sprites glow at full brightness even if the light itself is dim.
        public float? OverrideLightSpriteAlpha;

        public SpriteEffects LightSpriteEffect;

        public Submarine ParentSub;

        public bool CastShadows;

        //what was the range of the light when lightvolumes were last calculated
        private float prevCalculatedRange;
        private Vector2 prevCalculatedPosition;

        //do we need to recheck which convex hulls are within range 
        //(e.g. position or range of the lightsource has changed)
        public bool NeedsHullCheck = true;
        //do we need to recalculate the vertices of the light volume
        private bool needsRecalculation;
        public bool NeedsRecalculation
        {
            get { return needsRecalculation; }
            set
            {
                if (!needsRecalculation && value)
                {
                    foreach (ConvexHullList chList in hullsInRange)
                    {
                        chList.IsHidden.Clear();
                    }
                }
                needsRecalculation = value;
            }
        }

        //when were the vertices of the light volume last calculated
        private float lastRecalculationTime;

        private Dictionary<Submarine, Vector2> diffToSub;

        private DynamicVertexBuffer lightVolumeBuffer;
        private DynamicIndexBuffer lightVolumeIndexBuffer;
        private int vertexCount;
        private int indexCount;

        private Vector2 position;
        public Vector2 Position
        {
            get { return position; }
            set
            {
                if (position == value) return;
                position = value;

                if (Vector2.DistanceSquared(prevCalculatedPosition, position) < 5.0f * 5.0f) return;
                
                NeedsHullCheck = true;
                NeedsRecalculation = true;
                prevCalculatedPosition = position;
            }
        }

        private float rotation;
        public float Rotation
        {
            get { return rotation; }
            set
            {
                if (rotation == value) return;
                rotation = value;

                NeedsHullCheck = true;
                NeedsRecalculation = true;
            }
        }

        public Vector2 WorldPosition
        {
            get { return (ParentSub == null) ? position : position + ParentSub.Position; }
        }

        public static Texture2D LightTexture
        {
            get
            {
                if (lightTexture == null)
                {
                    lightTexture = TextureLoader.FromFile("Content/Lights/light.png");
                }

                return lightTexture;
            }
        }

        public Color Color
        {
            get { return color; }
            set { color = value; }
        }

        public float Range
        {
            get { return range; }
            set
            {

                range = MathHelper.Clamp(value, 0.0f, 2048.0f);
                if (Math.Abs(prevCalculatedRange - range) < 10.0f) return;
                
                NeedsHullCheck = true;
                NeedsRecalculation = true;
                prevCalculatedRange = range;
            }
        }

        public bool Enabled = true;

        public LightSource (XElement element)
            : this(Vector2.Zero, 100.0f, Color.White, null)
        {
            range = element.GetAttributeFloat("range", 100.0f);
            color = new Color(element.GetAttributeVector4("color", Vector4.One));

            CastShadows = element.GetAttributeBool("castshadows", true);

            
            foreach (XElement subElement in element.Elements())
            {
                switch (subElement.Name.ToString().ToLowerInvariant())
                {
                    case "sprite":
                        LightSprite = new Sprite(subElement);

                        float spriteAlpha = subElement.GetAttributeFloat("alpha", -1.0f);
                        if (spriteAlpha >= 0.0f)
                        {
                            OverrideLightSpriteAlpha = spriteAlpha;
                        }
                        break;
                    case "lighttexture":
                        overrideLightTexture = new Sprite(subElement);
                        break;
                }
            }
        }

        public LightSource(Vector2 position, float range, Color color, Submarine submarine, bool addLight=true)
        {
            hullsInRange = new List<ConvexHullList>();

            this.ParentSub = submarine;

            this.position = position;
            this.range = range;
            this.color = color;

            CastShadows = true;
            
            texture = LightTexture;

            diffToSub = new Dictionary<Submarine, Vector2>();

            if (addLight) GameMain.LightManager.AddLight(this);
        }
        
        /// <summary>
        /// Update the contents of ConvexHullList and check if we need to recalculate vertices
        /// </summary>
        private void RefreshConvexHullList(ConvexHullList chList, Vector2 lightPos, Submarine sub)
        {
            var fullChList = ConvexHull.HullLists.Find(x => x.Submarine == sub);
            if (fullChList == null) return;

            chList.List = fullChList.List.FindAll(ch => ch.Enabled && MathUtils.CircleIntersectsRectangle(lightPos, range, ch.BoundingBox));

            NeedsHullCheck = true;
        }

        /// <summary>
        /// Recheck which convex hulls are in range (if needed), 
        /// and check if we need to recalculate vertices due to changes in the convex hulls
        /// </summary>
        private void CheckHullsInRange()
        {
            List<Submarine> subs = new List<Submarine>(Submarine.Loaded);
            subs.Add(null);

            foreach (Submarine sub in subs)
            {
                //find the list of convexhulls that belong to the sub
                var chList = hullsInRange.Find(x => x.Submarine == sub);

                //not found -> create one
                if (chList == null)
                {
                    chList = new ConvexHullList(sub);
                    hullsInRange.Add(chList);
                    NeedsRecalculation = true;
                }

                if (chList.List.Any(ch => ch.LastVertexChangeTime > lastRecalculationTime && !chList.IsHidden.Contains(ch)))
                {
                    NeedsRecalculation = true;
                }

                Vector2 lightPos = position;
                if (ParentSub == null)
                {
                    //light and the convexhulls are both outside
                    if (sub == null)
                    {
                        if (NeedsHullCheck)
                        {
                            RefreshConvexHullList(chList, lightPos, null);
                        }
                    }
                    //light is outside, convexhulls inside a sub
                    else
                    {
                        lightPos -= sub.Position;

                        Rectangle subBorders = sub.Borders;
                        subBorders.Location += sub.HiddenSubPosition.ToPoint() - new Point(0, sub.Borders.Height);

                        //only draw if the light overlaps with the sub
                        if (!MathUtils.CircleIntersectsRectangle(lightPos, range, subBorders))
                        {
                            if (chList.List.Count > 0) NeedsRecalculation = true;
                            chList.List.Clear();
                            continue;
                        }
                        
                        RefreshConvexHullList(chList, lightPos, sub);
                    }
                }
                else 
                {
                    //light is inside, convexhull outside
                    if (sub == null) continue;
                
                    //light and convexhull are both inside the same sub
                    if (sub == ParentSub)
                    {
                        if (NeedsHullCheck)
                        {                            
                            RefreshConvexHullList(chList, lightPos, sub);
                        }
                    }
                    //light and convexhull are inside different subs
                    else
                    {
                        if (sub.DockedTo.Contains(ParentSub) && !NeedsHullCheck) continue;
                        
                        lightPos -= (sub.Position - ParentSub.Position);

                        Rectangle subBorders = sub.Borders;
                        subBorders.Location += sub.HiddenSubPosition.ToPoint() - new Point(0, sub.Borders.Height);

                        //don't draw any shadows if the light doesn't overlap with the borders of the sub
                        if (!MathUtils.CircleIntersectsRectangle(lightPos, range, subBorders))
                        {
                            if (chList.List.Count > 0) NeedsRecalculation = true;
                            chList.List.Clear();
                            continue;
                        }

                        //recalculate vertices if the subs have moved > 5 px relative to each other
                        Vector2 diff = ParentSub.WorldPosition - sub.WorldPosition;
                        Vector2 prevDiff;
                        if (!diffToSub.TryGetValue(sub, out prevDiff))
                        {
                            diffToSub.Add(sub, diff);
                            NeedsRecalculation = true;
                        }
                        else if (Vector2.DistanceSquared(diff, prevDiff) > 5.0f*5.0f)
                        {
                            diffToSub[sub] = diff;
                            NeedsRecalculation = true;
                        }

                        RefreshConvexHullList(chList, lightPos, sub);
                    }
                }
            }
        }

        private List<Vector2> FindRaycastHits()
        {
            if (!CastShadows)
            {
                return null;
            }
            if (range < 1.0f || color.A < 0.01f) return null;

            Vector2 drawPos = position;
            if (ParentSub != null) drawPos += ParentSub.DrawPosition;

            var hulls = new List<ConvexHull>();// ConvexHull.GetHullsInRange(position, range, ParentSub);
            foreach (ConvexHullList chList in hullsInRange)
            {
                //hulls.AddRange(chList.List);
                foreach (ConvexHull hull in chList.List)
                {
                    if (!chList.IsHidden.Contains(hull)) hulls.Add(hull);
                    //hulls.Add(hull);
                }
                foreach (ConvexHull hull in chList.List)
                {
                    chList.IsHidden.Add(hull);
                }
            }

            float bounds = range * 2;
            //find convexhull segments that are close enough and facing towards the light source
            List<Segment> visibleSegments = new List<Segment>();
            List<SegmentPoint> points = new List<SegmentPoint>();
            foreach (ConvexHull hull in hulls)
            {
                hull.RefreshWorldPositions();

                var visibleHullSegments = hull.GetVisibleSegments(drawPos);
                visibleSegments.AddRange(visibleHullSegments);
            }

            //Generate new points at the intersections between segments
            //This is necessary for the light volume to generate properly on some subs
            for (int i = 0; i < visibleSegments.Count; i++)
            {
                Vector2 p1a = visibleSegments[i].Start.WorldPos;
                Vector2 p1b = visibleSegments[i].End.WorldPos;

                for (int j = 0; j < visibleSegments.Count; j++)
                {
                    if (j == i) continue;
                    Vector2 p2a = visibleSegments[j].Start.WorldPos;
                    Vector2 p2b = visibleSegments[j].End.WorldPos;

                    if (Vector2.DistanceSquared(p1a, p2a) < 25.0f ||
                        Vector2.DistanceSquared(p1a, p2b) < 25.0f ||
                        Vector2.DistanceSquared(p1b, p2a) < 25.0f ||
                        Vector2.DistanceSquared(p1b, p2b) < 25.0f)
                    {
                        continue;
                    }

                    Vector2? intersection = MathUtils.GetLineIntersection(p1a, p1b, p2a, p2b);

                    if (intersection != null)
                    {
                        Vector2 intersectionVal = intersection.Value;

                        SegmentPoint start = visibleSegments[i].Start;
                        SegmentPoint end = visibleSegments[i].End;
                        SegmentPoint mid = new SegmentPoint(intersectionVal, null);

                        if (Vector2.DistanceSquared(start.WorldPos, mid.WorldPos) < 25.0f ||
                            Vector2.DistanceSquared(end.WorldPos, mid.WorldPos) < 25.0f)
                        {
                            continue;
                        }

                        Segment seg1 = new Segment(start, mid, visibleSegments[i].ConvexHull); seg1.IsHorizontal = visibleSegments[i].IsHorizontal;
                        Segment seg2 = new Segment(mid, end, visibleSegments[i].ConvexHull); seg2.IsHorizontal = visibleSegments[i].IsHorizontal;
                        visibleSegments[i] = seg1;
                        visibleSegments.Insert(i+1,seg2);
                        i--;
                        break;
                    }
                }
            }

            foreach (Segment s in visibleSegments)
            {
                points.Add(s.Start);
                points.Add(s.End);
                if (Math.Abs(s.Start.WorldPos.X - drawPos.X) > bounds) bounds = Math.Abs(s.Start.WorldPos.X - drawPos.X);
                if (Math.Abs(s.Start.WorldPos.Y - drawPos.Y) > bounds) bounds = Math.Abs(s.Start.WorldPos.Y - drawPos.Y);
                if (Math.Abs(s.End.WorldPos.X - drawPos.X) > bounds) bounds = Math.Abs(s.End.WorldPos.X - drawPos.X);
                if (Math.Abs(s.End.WorldPos.Y - drawPos.Y) > bounds) bounds = Math.Abs(s.End.WorldPos.Y - drawPos.Y);
            }

            //add a square-shaped boundary to make sure we've got something to construct the triangles from
            //even if there aren't enough hull segments around the light source

            //(might be more effective to calculate if we actually need these extra points)
            var boundaryCorners = new List<SegmentPoint> {
                new SegmentPoint(new Vector2(drawPos.X + bounds, drawPos.Y + bounds), null),
                new SegmentPoint(new Vector2(drawPos.X + bounds, drawPos.Y - bounds), null),
                new SegmentPoint(new Vector2(drawPos.X - bounds, drawPos.Y - bounds), null),
                new SegmentPoint(new Vector2(drawPos.X - bounds, drawPos.Y + bounds), null)
            };

            points.AddRange(boundaryCorners);

            for (int i = 0; i < 4; i++)
            {
                visibleSegments.Add(new Segment(boundaryCorners[i], boundaryCorners[(i + 1) % 4], null));
            }
            
            var compareCCW = new CompareSegmentPointCW(drawPos);
            try
            {
                points.Sort(compareCCW);
            }
            catch (Exception e)
            {
                StringBuilder sb = new StringBuilder("Constructing light volumes failed! Light pos: "+drawPos+", Hull verts:\n");
                foreach (SegmentPoint sp in points)
                {
                    sb.AppendLine(sp.Pos.ToString());
                }
                DebugConsole.ThrowError(sb.ToString(), e);
            }
            
            List<Vector2> output = new List<Vector2>();
            //List<Pair<int, Vector2>> preOutput = new List<Pair<int, Vector2>>();

            //remove points that are very close to each other
            for (int i = 0; i < points.Count - 1; i++)
            {
                if (Math.Abs(points[i].WorldPos.X - points[i + 1].WorldPos.X) < 6 &&
                    Math.Abs(points[i].WorldPos.Y - points[i + 1].WorldPos.Y) < 6)
                {
                    points.RemoveAt(i + 1);
                    i--;
                }
            }

            foreach (SegmentPoint p in points)
            {
                Vector2 dir = Vector2.Normalize(p.WorldPos - drawPos);
                Vector2 dirNormal = new Vector2(-dir.Y, dir.X) * 3;

                //do two slightly offset raycasts to hit the segment itself and whatever's behind it
                Pair<int,Vector2> intersection1 = RayCast(drawPos, drawPos + dir * bounds * 2 - dirNormal, visibleSegments);
                Pair<int,Vector2> intersection2 = RayCast(drawPos, drawPos + dir * bounds * 2 + dirNormal, visibleSegments);

                if (intersection1.First < 0) return new List<Vector2>();
                if (intersection2.First < 0) return new List<Vector2>();
                Segment seg1 = visibleSegments[intersection1.First];
                Segment seg2 = visibleSegments[intersection2.First];
                
                bool isPoint1 = MathUtils.LineToPointDistance(seg1.Start.WorldPos, seg1.End.WorldPos, p.WorldPos) < 5.0f;
                bool isPoint2 = MathUtils.LineToPointDistance(seg2.Start.WorldPos, seg2.End.WorldPos, p.WorldPos) < 5.0f;

                if (isPoint1 && isPoint2)
                {
                    //hit at the current segmentpoint -> place the segmentpoint into the list 
                    output.Add(p.WorldPos);

                    foreach (ConvexHullList hullList in hullsInRange)
                    {
                        hullList.IsHidden.Remove(p.ConvexHull);
                        hullList.IsHidden.Remove(seg1.ConvexHull);
                        hullList.IsHidden.Remove(seg2.ConvexHull);
                    }
                }
                else if (intersection1.First != intersection2.First)
                {
                    //the raycasts landed on different segments
                    //we definitely want to generate new geometry here
                    output.Add(isPoint1 ? p.WorldPos : intersection1.Second);
                    output.Add(isPoint2 ? p.WorldPos : intersection2.Second);

                    foreach (ConvexHullList hullList in hullsInRange)
                    {
                        hullList.IsHidden.Remove(p.ConvexHull);
                        hullList.IsHidden.Remove(seg1.ConvexHull);
                        hullList.IsHidden.Remove(seg2.ConvexHull);
                    }
                }
                //if neither of the conditions above are met, we just assume
                //that the raycasts both resulted on the same segment
                //and creating geometry here would be wasteful
            }

            //remove points that are very close to each other
            for (int i = 0; i < output.Count - 1; i++)
            {
                if (Math.Abs(output[i].X - output[i + 1].X) < 6 &&
                    Math.Abs(output[i].Y - output[i + 1].Y) < 6)
                {
                    output.RemoveAt(i + 1);
                    i--;
                }
            }

            return output;
        }

        private Pair<int,Vector2> RayCast(Vector2 rayStart, Vector2 rayEnd, List<Segment> segments)
        {
            float closestDist = 0.0f;
            Vector2? closestIntersection = null;
            int segment = -1;

            for (int i=0;i<segments.Count;i++)
            {
                Segment s = segments[i];
                Vector2? intersection = MathUtils.GetAxisAlignedLineIntersection(rayStart, rayEnd, s.Start.WorldPos, s.End.WorldPos, s.IsHorizontal);

                if (intersection != null)
                {
                    float dist = Vector2.DistanceSquared((Vector2)intersection, rayStart);
                    if (closestIntersection == null || dist < closestDist)
                    {
                        closestDist = dist;
                        closestIntersection = intersection;
                        segment = i;
                    }
                }
            }

            Pair<int,Vector2> retVal = new Pair<int,Vector2>();
            retVal.Second = closestIntersection == null ? rayEnd : (Vector2)closestIntersection;
            retVal.First = segment;
            return retVal;
        }

        private void CalculateLightVertices(List<Vector2> rayCastHits)
        {
            List<VertexPositionColorTexture> vertices = new List<VertexPositionColorTexture>();

            Vector2 drawPos = position;
            if (ParentSub != null) drawPos += ParentSub.DrawPosition;

            float cosAngle = (float)Math.Cos(Rotation);
            float sinAngle = -(float)Math.Sin(Rotation);
            
            Vector2 uvOffset = Vector2.Zero;
            Vector2 overrideTextureDims = Vector2.One;
            if (overrideLightTexture != null)
            {
                overrideTextureDims = new Vector2(overrideLightTexture.SourceRect.Width, overrideLightTexture.SourceRect.Height);
                uvOffset = (overrideLightTexture.Origin / overrideTextureDims) - new Vector2(0.5f, 0.5f);
            }

            // Add a vertex for the center of the mesh
            vertices.Add(new VertexPositionColorTexture(new Vector3(position.X, position.Y, 0),
                Color.White,new Vector2(0.5f, 0.5f) + uvOffset));

            // Add all the other encounter points as vertices
            // storing their world position as UV coordinates
            for (int i = 0; i < rayCastHits.Count; i++)
            {
                Vector2 vertex = rayCastHits[i];
                
                //we'll use the previous and next vertices to calculate the normals
                //of the two segments this vertex belongs to
                //so we can add new vertices based on these normals
                Vector2 prevVertex = rayCastHits[i > 0 ? i - 1 : rayCastHits.Count - 1];
                Vector2 nextVertex = rayCastHits[i < rayCastHits.Count - 1 ? i + 1 : 0];
                
                Vector2 rawDiff = vertex - drawPos;
                Vector2 diff = rawDiff;
                diff /= range * 2.0f;
                if (overrideLightTexture != null)
                {
                    //calculate texture coordinates based on the light's rotation
                    Vector2 originDiff = diff;

                    diff.X = originDiff.X * cosAngle - originDiff.Y * sinAngle;
                    diff.Y = originDiff.X * sinAngle + originDiff.Y * cosAngle;
                    diff *= (overrideTextureDims / overrideLightTexture.size) * 2.0f;

                    diff += uvOffset;
                }
                
                //calculate normal of first segment
                Vector2 nDiff1 = vertex - nextVertex;
                float tx = nDiff1.X; nDiff1.X = -nDiff1.Y; nDiff1.Y = tx;
                nDiff1 /= Math.Max(Math.Abs(nDiff1.X), Math.Abs(nDiff1.Y));
                //if the normal is pointing towards the light origin
                //rather than away from it, invert it
                if (Vector2.DistanceSquared(nDiff1, rawDiff) > Vector2.DistanceSquared(-nDiff1, rawDiff)) nDiff1 = -nDiff1;
                
                //calculate normal of second segment
                Vector2 nDiff2 = prevVertex - vertex;
                tx = nDiff2.X; nDiff2.X = -nDiff2.Y; nDiff2.Y = tx;
                nDiff2 /= Math.Max(Math.Abs(nDiff2.X),Math.Abs(nDiff2.Y));
                //if the normal is pointing towards the light origin
                //rather than away from it, invert it
                if (Vector2.DistanceSquared(nDiff2, rawDiff) > Vector2.DistanceSquared(-nDiff2, rawDiff)) nDiff2 = -nDiff2;
                
                //add the normals together and use some magic numbers to create
                //a somewhat useful/good-looking blur
                Vector2 nDiff = nDiff1 + nDiff2;
                nDiff /= Math.Max(Math.Abs(nDiff.X), Math.Abs(nDiff.Y));
                nDiff *= 50.0f;

                //finally, create the vertices
                VertexPositionColorTexture fullVert = new VertexPositionColorTexture(new Vector3(position.X + rawDiff.X, position.Y + rawDiff.Y, 0),
                   Color.White, new Vector2(0.5f, 0.5f) + diff);
                VertexPositionColorTexture fadeVert = new VertexPositionColorTexture(new Vector3(position.X + rawDiff.X + nDiff.X, position.Y + rawDiff.Y + nDiff.Y, 0),
                   Color.White * 0.0f, new Vector2(0.5f, 0.5f) + diff);

                vertices.Add(fullVert);
                vertices.Add(fadeVert);
            }

            // Compute the indices to form triangles
            List<short> indices = new List<short>();
            for (int i = 0; i < rayCastHits.Count-1; i++)
            {
                //main light body
                indices.Add(0);
                indices.Add((short)((i*2 + 3) % vertices.Count));
                indices.Add((short)((i*2 + 1) % vertices.Count));
                
                //faded light
                indices.Add((short)((i*2 + 1) % vertices.Count));
                indices.Add((short)((i*2 + 3) % vertices.Count));
                indices.Add((short)((i*2 + 4) % vertices.Count));

                indices.Add((short)((i*2 + 2) % vertices.Count));
                indices.Add((short)((i*2 + 1) % vertices.Count));
                indices.Add((short)((i*2 + 4) % vertices.Count));
            }
            
            //main light body
            indices.Add(0);
            indices.Add((short)(1));
            indices.Add((short)(vertices.Count - 2));
            
            //faded light
            indices.Add((short)(1));
            indices.Add((short)(vertices.Count-1));
            indices.Add((short)(vertices.Count-2));

            indices.Add((short)(1));
            indices.Add((short)(2));
            indices.Add((short)(vertices.Count-1));

            vertexCount = vertices.Count;
            indexCount = indices.Count;

            //TODO: a better way to determine the size of the vertex buffer and handle changes in size?
            //now we just create a buffer for 64 verts and make it larger if needed
            if (lightVolumeBuffer == null)
            {
                lightVolumeBuffer = new DynamicVertexBuffer(GameMain.Instance.GraphicsDevice, VertexPositionColorTexture.VertexDeclaration, Math.Max(64, (int)(vertexCount*1.5)), BufferUsage.None);
                lightVolumeIndexBuffer = new DynamicIndexBuffer(GameMain.Instance.GraphicsDevice, typeof(short), Math.Max(64*3, (int)(indexCount * 1.5)), BufferUsage.None);
            }
            else if (vertexCount > lightVolumeBuffer.VertexCount || indexCount > lightVolumeIndexBuffer.IndexCount)
            {
                lightVolumeBuffer.Dispose();
                lightVolumeIndexBuffer.Dispose();

                lightVolumeBuffer = new DynamicVertexBuffer(GameMain.Instance.GraphicsDevice, VertexPositionColorTexture.VertexDeclaration, (int)(vertexCount*1.5), BufferUsage.None);
                lightVolumeIndexBuffer = new DynamicIndexBuffer(GameMain.Instance.GraphicsDevice, typeof(short), (int)(indexCount * 1.5), BufferUsage.None);
            }
            
            lightVolumeBuffer.SetData<VertexPositionColorTexture>(vertices.ToArray());
            lightVolumeIndexBuffer.SetData<short>(indices.ToArray());
        }     

        public void Draw(SpriteBatch spriteBatch, BasicEffect lightEffect, Matrix transform)
        {
            if (CastShadows)
            {
                CheckHullsInRange();
            }

            Vector3 offset = ParentSub == null ? Vector3.Zero :
            new Vector3(ParentSub.DrawPosition.X, ParentSub.DrawPosition.Y, 0.0f);

            lightEffect.World = Matrix.CreateTranslation(offset) * transform;

            Vector2 drawPos = position;
            if (ParentSub != null) drawPos += ParentSub.DrawPosition;

            drawPos.Y = -drawPos.Y;
            
            if (LightSprite != null)
            {
                Vector2 origin = LightSprite.Origin;
                if (LightSpriteEffect == SpriteEffects.FlipHorizontally) origin.X = LightSprite.SourceRect.Width - origin.X;
                if (LightSpriteEffect == SpriteEffects.FlipVertically) origin.Y = LightSprite.SourceRect.Height - origin.Y;
                
                LightSprite.Draw(
                    spriteBatch, drawPos, 
                    new Color(Color, OverrideLightSpriteAlpha ?? Color.A / 255.0f),
                    origin, -Rotation, 1, LightSpriteEffect);
            }

            //if the light doesn't cast shadows, we can simply render the texture without having to calculate the light volume
            if (!CastShadows)
            {
                Texture2D currentTexture = texture ?? LightTexture;
                if (overrideLightTexture != null) currentTexture = overrideLightTexture.Texture;                

                Vector2 center = new Vector2(currentTexture.Width / 2, currentTexture.Height / 2);
                float scale = range / (currentTexture.Width / 2.0f);

                spriteBatch.Draw(currentTexture, drawPos, null, color * (color.A / 255.0f), 0, center, scale, SpriteEffects.None, 1);
                return;
            }

            if (NeedsRecalculation)
            {
                var verts = FindRaycastHits();
                CalculateLightVertices(verts);

                lastRecalculationTime = (float)Timing.TotalTime;
                NeedsRecalculation = false;
            }
            
            if (vertexCount == 0) return;

            lightEffect.DiffuseColor = (new Vector3(color.R, color.G, color.B) * (color.A / 255.0f)) / 255.0f;
            if (overrideLightTexture != null)
            {
                lightEffect.Texture = overrideLightTexture.Texture;
            }
            else
            {
                lightEffect.Texture = texture ?? LightTexture;
            }
            lightEffect.CurrentTechnique.Passes[0].Apply();

            GameMain.Instance.GraphicsDevice.SetVertexBuffer(lightVolumeBuffer);
            GameMain.Instance.GraphicsDevice.Indices = lightVolumeIndexBuffer;

            GameMain.Instance.GraphicsDevice.DrawIndexedPrimitives
            (
                PrimitiveType.TriangleList, 0, 0, indexCount / 3
            );
        }
        
        public void Reset()
        {
            hullsInRange.Clear();
            diffToSub.Clear();
            NeedsHullCheck = true;
            NeedsRecalculation = true;

            vertexCount = 0;
            if (lightVolumeBuffer != null)
            {
                lightVolumeBuffer.Dispose();
                lightVolumeBuffer = null;
            }

            indexCount = 0;
            if (lightVolumeIndexBuffer != null)
            {
                lightVolumeIndexBuffer.Dispose();
                lightVolumeIndexBuffer = null;
            }
        }

        public void Remove()
        {
            if (LightSprite != null) LightSprite.Remove();

            if (lightVolumeBuffer != null)
            {
                lightVolumeBuffer.Dispose();
                lightVolumeBuffer = null;
            }

            if (lightVolumeIndexBuffer != null)
            {
                lightVolumeIndexBuffer.Dispose();
                lightVolumeIndexBuffer = null;
            }

            GameMain.LightManager.RemoveLight(this);
        }
    }
}
