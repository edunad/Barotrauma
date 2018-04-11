using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Barotrauma
{
    class WaterRenderer : IDisposable
    {
        const int DefaultBufferSize = 1500;

        private Vector2 wavePos;

        public VertexPositionTexture[] vertices = new VertexPositionTexture[DefaultBufferSize];

        public Effect waterEffect
        {
            get;
            private set;
        }
        private BasicEffect basicEffect;

        public int PositionInBuffer = 0;

        private Texture2D waterTexture;

        public Texture2D WaterTexture
        {
            get { return waterTexture; }
        }

        public WaterRenderer(GraphicsDevice graphicsDevice, ContentManager content)
        {
#if WINDOWS
            waterEffect = content.Load<Effect>("watershader");
#endif
#if LINUX

            waterEffect = content.Load<Effect>("watershader_opengl");
#endif

            waterTexture = TextureLoader.FromFile("Content/waterbump.png");
            waterEffect.Parameters["xWaveWidth"].SetValue(0.05f);
            waterEffect.Parameters["xWaveHeight"].SetValue(0.05f);

            waterEffect.Parameters["xWaterBumpMap"].SetValue(waterTexture);

            if (basicEffect == null)
            {
                basicEffect = new BasicEffect(GameMain.Instance.GraphicsDevice);
                basicEffect.VertexColorEnabled = false;

                basicEffect.TextureEnabled = true;
            }
        }

        public void RenderBack(SpriteBatch spriteBatch, RenderTarget2D texture, float blurAmount = 0.0f)
        {
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.LinearWrap, null, null, waterEffect);

            waterEffect.CurrentTechnique = waterEffect.Techniques["WaterShader"];
            waterEffect.Parameters["xWavePos"].SetValue(wavePos);
            waterEffect.Parameters["xBlurDistance"].SetValue(blurAmount);
            //waterEffect.CurrentTechnique.Passes[0].Apply();
            
//#if WINDOWS
            waterEffect.Parameters["xTexture"].SetValue(texture);
            spriteBatch.Draw(texture, new Rectangle(0, 0, GameMain.GraphicsWidth, GameMain.GraphicsHeight), Color.White);
//#elif LINUX

//            spriteBatch.Draw(texture, new Rectangle(0, 0, GameMain.GraphicsWidth, GameMain.GraphicsHeight), Color.White);
//#endif
            spriteBatch.End();
        }

        public void ScrollWater(float deltaTime)
        {
            wavePos.X += 0.006f * deltaTime;
            wavePos.Y += 0.006f * deltaTime;
        }

        public void Render(GraphicsDevice graphicsDevice, Camera cam, RenderTarget2D texture, Matrix transform)
        {
            if (vertices == null) return;
            if (vertices.Length < 0) return;

            basicEffect.Texture = texture;

            basicEffect.View = Matrix.Identity;
            basicEffect.World = transform
                * Matrix.CreateOrthographic(GameMain.GraphicsWidth, GameMain.GraphicsHeight, -1, 1) * 0.5f;

            basicEffect.CurrentTechnique.Passes[0].Apply();

            graphicsDevice.SamplerStates[0] = SamplerState.PointWrap;
            graphicsDevice.DrawUserPrimitives<VertexPositionTexture>(PrimitiveType.TriangleList, vertices, 0, vertices.Length / 3);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) return;

            if (waterEffect != null)
            {
                waterEffect.Dispose();
                waterEffect = null;
            }

            if (basicEffect != null)
            {
                basicEffect.Dispose();
                basicEffect = null;
            }
        }

    }
}
