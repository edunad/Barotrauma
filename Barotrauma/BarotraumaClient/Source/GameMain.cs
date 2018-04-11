using Barotrauma.Networking;
using Barotrauma.Particles;
using FarseerPhysics;
using FarseerPhysics.Dynamics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace Barotrauma
{
    class GameMain : Game
    {
        public static bool ShowFPS = true;
        public static bool DebugDraw;
        
        public static FrameCounter FrameCounter;

        public static readonly Version Version = Assembly.GetEntryAssembly().GetName().Version;
        
        public static GameScreen            GameScreen;
        public static MainMenuScreen        MainMenuScreen;
        public static LobbyScreen           LobbyScreen;

        public static NetLobbyScreen        NetLobbyScreen;
        public static ServerListScreen      ServerListScreen;

        public static SubEditorScreen         SubEditorScreen;
        public static CharacterEditorScreen   CharacterEditorScreen;
        public static ParticleEditorScreen  ParticleEditorScreen;

        public static Lights.LightManager LightManager;
        
        public static ContentPackage SelectedPackage
        {
            get { return Config.SelectedContentPackage; }
        }
        
        public static GameSession GameSession;

        public static NetworkMember NetworkMember;

        public static ParticleManager ParticleManager;
        public static DecalManager DecalManager;
        
        public static World World;

        public static LoadingScreen TitleScreen;
        private bool loadingScreenOpen;

        public static GameSettings Config;

        private CoroutineHandle loadingCoroutine;
        private bool hasLoaded;

        private GameTime fixedTime;

        private static SpriteBatch spriteBatch;

        private Viewport defaultViewport;

        public static GameMain Instance
        {
            get;
            private set;
        }

        public static GraphicsDeviceManager GraphicsDeviceManager
        {
            get;
            private set;
        }
        
        public static WindowMode WindowMode
        {
            get;
            private set;
        }

        public static int GraphicsWidth
        {
            get;
            private set;
        }

        public static int GraphicsHeight
        {
            get;
            private set;
        }

        public static bool WindowActive
        {
            get { return Instance == null || Instance.IsActive; }
        }

        public static GameServer Server
        {
            get { return NetworkMember as GameServer; }
        }

        public static GameClient Client
        {
            get { return NetworkMember as GameClient; }
        }
        
        public static RasterizerState ScissorTestEnable
        {
            get;
            private set;
        }

        public bool LoadingScreenOpen
        {
            get { return loadingScreenOpen; }
        }
                                
        public GameMain()
        {
            GraphicsDeviceManager = new GraphicsDeviceManager(this);

            Window.Title = "Barotrauma";
            
            Instance = this;

            Config = new GameSettings("config.xml");
            if (Config.WasGameUpdated)
            {
                UpdaterUtil.CleanOldFiles();
                Config.WasGameUpdated = false;
                Config.Save("config.xml");
            }

            ApplyGraphicsSettings();

            Content.RootDirectory = "Content";

            FrameCounter = new FrameCounter();
            
            IsFixedTimeStep = false;

            Timing.Accumulator = 0.0f;
            fixedTime = new GameTime();

            World = new World(new Vector2(0, -9.82f));
            FarseerPhysics.Settings.AllowSleep = true;
            FarseerPhysics.Settings.ContinuousPhysics = false;
            FarseerPhysics.Settings.VelocityIterations = 1;
            FarseerPhysics.Settings.PositionIterations = 1;
        }

        public void ApplyGraphicsSettings()
        {
            GraphicsWidth = Config.GraphicsWidth;
            GraphicsHeight = Config.GraphicsHeight;
            GraphicsDeviceManager.GraphicsProfile = GraphicsProfile.Reach;
            GraphicsDeviceManager.PreferredBackBufferFormat = SurfaceFormat.Color;
            GraphicsDeviceManager.PreferMultiSampling = false;
            GraphicsDeviceManager.SynchronizeWithVerticalRetrace = Config.VSyncEnabled;

            if (Config.WindowMode == WindowMode.Windowed)
            {
                //for whatever reason, window isn't centered automatically
                //since MonoGame 3.6 (nuget package might be broken), so
                //let's do it manually
                Window.Position = new Point((GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width - GraphicsWidth) / 2,
                                            (GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height - GraphicsHeight) / 2);
            }

            GraphicsDeviceManager.PreferredBackBufferWidth = GraphicsWidth;
            GraphicsDeviceManager.PreferredBackBufferHeight = GraphicsHeight;

            SetWindowMode(Config.WindowMode);

            defaultViewport = GraphicsDevice.Viewport;
        }

        public void SetWindowMode(WindowMode windowMode)
        {
            WindowMode = windowMode;
            GraphicsDeviceManager.HardwareModeSwitch = Config.WindowMode != WindowMode.BorderlessWindowed;
            GraphicsDeviceManager.IsFullScreen = Config.WindowMode == WindowMode.Fullscreen || Config.WindowMode == WindowMode.BorderlessWindowed;
            
            GraphicsDeviceManager.ApplyChanges();
        }

        public void ResetViewPort()
        {
            GraphicsDevice.Viewport = defaultViewport;
        }

        /// <summary>
        /// Allows the game to perform any initialization it needs to before starting to run.
        /// This is where it can query for any required services and load any non-graphic
        /// related content.  Calling base.Initialize will enumerate through any components
        /// and initialize them as well.
        /// </summary>
        protected override void Initialize()
        {
            base.Initialize();
            
            ScissorTestEnable = new RasterizerState() { ScissorTestEnable = true };

            Hyper.ComponentModel.HyperTypeDescriptionProvider.Add(typeof(Character));
            Hyper.ComponentModel.HyperTypeDescriptionProvider.Add(typeof(Item));
            Hyper.ComponentModel.HyperTypeDescriptionProvider.Add(typeof(Items.Components.ItemComponent));
            Hyper.ComponentModel.HyperTypeDescriptionProvider.Add(typeof(Hull));
        }

        /// <summary>
        /// LoadContent will be called once per game and is the place to load
        /// all of your content.
        /// </summary>
        protected override void LoadContent()
        {
            GraphicsWidth = GraphicsDevice.Viewport.Width;
            GraphicsHeight = GraphicsDevice.Viewport.Height;

            Sound.Init();

            ConvertUnits.SetDisplayUnitToSimUnitRatio(Physics.DisplayToSimRation);

            spriteBatch = new SpriteBatch(GraphicsDevice);
            TextureLoader.Init(GraphicsDevice);

            loadingScreenOpen = true;
            TitleScreen = new LoadingScreen(GraphicsDevice);

            loadingCoroutine = CoroutineManager.StartCoroutine(Load());
        }

        private IEnumerable<object> Load()
        {
            if (GameSettings.VerboseLogging)
            {
                DebugConsole.NewMessage("LOADING COROUTINE", Color.Lime);
            }
            GUI.GraphicsDevice = base.GraphicsDevice;
            GUI.Init(Content);

            GUIComponent.Init(Window);
            DebugConsole.Init(Window);
            DebugConsole.Log(SelectedPackage == null ? "No content package selected" : "Content package \"" + SelectedPackage.Name + "\" selected");
        yield return CoroutineStatus.Running;

            LightManager = new Lights.LightManager(base.GraphicsDevice, Content);

            Hull.renderer = new WaterRenderer(base.GraphicsDevice, Content);
            TitleScreen.LoadState = 1.0f;
        yield return CoroutineStatus.Running;

            GUI.LoadContent();
            TitleScreen.LoadState = 2.0f;
        yield return CoroutineStatus.Running;

            Mission.Init();
            MapEntityPrefab.Init();
            LevelGenerationParams.LoadPresets();
            TitleScreen.LoadState = 10.0f;
        yield return CoroutineStatus.Running;

            JobPrefab.LoadAll(SelectedPackage.GetFilesOfType(ContentType.Jobs));
            // Add any missing jobs from the prefab into Config.JobNamePreferences.
            foreach (JobPrefab job in JobPrefab.List)
            {
                if (!Config.JobNamePreferences.Contains(job.Name)) { Config.JobNamePreferences.Add(job.Name); }
            }
            StructurePrefab.LoadAll(SelectedPackage.GetFilesOfType(ContentType.Structure));
            TitleScreen.LoadState = 20.0f;
        yield return CoroutineStatus.Running;

            ItemPrefab.LoadAll(SelectedPackage.GetFilesOfType(ContentType.Item));
            TitleScreen.LoadState = 30.0f;
        yield return CoroutineStatus.Running;

            Debug.WriteLine("sounds");
            CoroutineManager.StartCoroutine(SoundPlayer.Init());

            int i = 0;
            while (!SoundPlayer.Initialized)
            {
                i++;
                TitleScreen.LoadState = SoundPlayer.SoundCount == 0 ? 
                    30.0f :
                    Math.Min(30.0f + 40.0f * i / Math.Max(SoundPlayer.SoundCount, 1), 70.0f);
                yield return CoroutineStatus.Running;
            }

            TitleScreen.LoadState = 70.0f;
        yield return CoroutineStatus.Running;

            GameModePreset.Init();

            Submarine.RefreshSavedSubs();
            TitleScreen.LoadState = 80.0f;
        yield return CoroutineStatus.Running;

            GameScreen          =   new GameScreen(GraphicsDeviceManager.GraphicsDevice, Content);
            TitleScreen.LoadState = 90.0f;
        yield return CoroutineStatus.Running;

            MainMenuScreen          =   new MainMenuScreen(this); 
            LobbyScreen             =   new LobbyScreen();
            
            ServerListScreen        =   new ServerListScreen();

            SubEditorScreen         =   new SubEditorScreen(Content);
            CharacterEditorScreen   =   new CharacterEditorScreen();
            ParticleEditorScreen    =   new ParticleEditorScreen();

        yield return CoroutineStatus.Running;

            ParticleManager = new ParticleManager(GameScreen.Cam);
            ParticleManager.LoadPrefabs();
            DecalManager = new DecalManager();
        yield return CoroutineStatus.Running;

            LocationType.Init();
            MainMenuScreen.Select();

            TitleScreen.LoadState = 100.0f;
            hasLoaded = true;
            if (GameSettings.VerboseLogging)
            {
                DebugConsole.NewMessage("LOADING COROUTINE FINISHED", Color.Lime);
            }
        yield return CoroutineStatus.Success;

        }

        /// <summary>
        /// UnloadContent will be called once per game and is the place to unload
        /// all content.
        /// </summary>
        protected override void UnloadContent()
        {
            Sound.Dispose();
        }
        
        /// <summary>
        /// Allows the game to run logic such as updating the world,
        /// checking for collisions, gathering input, and playing audio.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Update(GameTime gameTime)
        {
            Timing.TotalTime = gameTime.TotalGameTime.TotalSeconds;
            Timing.Accumulator += gameTime.ElapsedGameTime.TotalSeconds;
            PlayerInput.UpdateVariable();

            bool paused = true;

            while (Timing.Accumulator >= Timing.Step)
            {
                fixedTime.IsRunningSlowly = gameTime.IsRunningSlowly;
                TimeSpan addTime = new TimeSpan(0, 0, 0, 0, 16);
                fixedTime.ElapsedGameTime = addTime;
                fixedTime.TotalGameTime.Add(addTime);
                base.Update(fixedTime);
                
                if (WindowActive)
                {
                    PlayerInput.Update(Timing.Step);
                }

                if (loadingScreenOpen)
                {
                    //reset accumulator if loading
                    // -> less choppy loading screens because the screen is rendered after each update
                    // -> no pause caused by leftover time in the accumulator when starting a new shift
                    Timing.Accumulator = 0.0f;

                    if (TitleScreen.LoadState >= 100.0f && 
                        (!waitForKeyHit || PlayerInput.GetKeyboardState.GetPressedKeys().Length>0 || PlayerInput.LeftButtonClicked()))
                    {
                        loadingScreenOpen = false;
                    }

                    if (!hasLoaded && !CoroutineManager.IsCoroutineRunning(loadingCoroutine))
                    {
                        throw new Exception("Loading was interrupted due to an error");
                    }
                }
                else if (hasLoaded)
                {
                    SoundPlayer.Update((float)Timing.Step);

                    if (PlayerInput.KeyHit(Keys.Escape)) GUI.TogglePauseMenu();

                    GUIComponent.ClearUpdateList();
                    paused = (DebugConsole.IsOpen || GUI.PauseMenuOpen || GUI.SettingsMenuOpen) &&
                             (NetworkMember == null || !NetworkMember.GameStarted);

                    if (!paused)
                    {
                        Screen.Selected.AddToGUIUpdateList();
                    }

                    if (NetworkMember != null)
                    {
                        NetworkMember.AddToGUIUpdateList();
                    }

                    GUI.AddToGUIUpdateList();
                    DebugConsole.AddToGUIUpdateList();
                    GUIComponent.UpdateMouseOn();

                    DebugConsole.Update(this, (float)Timing.Step);
                    paused = paused || (DebugConsole.IsOpen && (NetworkMember == null || !NetworkMember.GameStarted));
                    
                    if (!paused)
                    {
                        Screen.Selected.Update(Timing.Step);
                    }

                    if (NetworkMember != null)
                    {
                        NetworkMember.Update((float)Timing.Step);
                    }

                    GUI.Update((float)Timing.Step);
                }

                CoroutineManager.Update((float)Timing.Step, paused ? 0.0f : (float)Timing.Step);

                Timing.Accumulator -= Timing.Step;
            }

            if (!paused) Timing.Alpha = Timing.Accumulator / Timing.Step;
        }


        /// <summary>
        /// This is called when the game should draw itself.
        /// </summary>
        protected override void Draw(GameTime gameTime)
        {
            double deltaTime = gameTime.ElapsedGameTime.TotalSeconds;

            FrameCounter.Update(deltaTime);

            if (loadingScreenOpen)
            {
                TitleScreen.Draw(spriteBatch, base.GraphicsDevice, (float)deltaTime);
            }
            else if (hasLoaded)
            {
                Screen.Selected.Draw(deltaTime, base.GraphicsDevice, spriteBatch);
            }

            if (!DebugDraw) return;
            if (GUIComponent.MouseOn!=null)
            {
                spriteBatch.Begin();
                GUI.DrawRectangle(spriteBatch, GUIComponent.MouseOn.MouseRect, Color.Lime);
                spriteBatch.End();
            }
        }

        static bool waitForKeyHit = true;
        public CoroutineHandle ShowLoading(IEnumerable<object> loader, bool waitKeyHit = true)
        {
            waitForKeyHit = waitKeyHit;
            loadingScreenOpen = true;
            TitleScreen.LoadState = null;
            return CoroutineManager.StartCoroutine(TitleScreen.DoLoading(loader));
        }

        protected override void OnExiting(object sender, EventArgs args)
        {
            if (NetworkMember != null) NetworkMember.Disconnect();
           
            base.OnExiting(sender, args);
        }
    }
}
