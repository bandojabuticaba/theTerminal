using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGameLibrary.Audio;
using MonoGameLibrary.Input;
using MonoGameLibrary.Scenes;
using ImGuiNET;
using ImGuiNET.SampleProgram.XNA;
using MonoGameLibrary.Graphics;
using MonoGameLibrary.Content;
using MonoGameLibrary.Debug;
using System.Collections.Generic;


namespace MonoGameLibrary;

public class Core : Game
{
    /// <summary>  
    /// Gets a runtime generated 1x1 pixel texture.  
    /// </summary>  
    public static Texture2D Pixel { get; private set; }

    internal static Core s_instance;

    /// <summary>
    /// Gets a reference to the Core instance.
    /// </summary>
    public static Core Instance => s_instance;

    // The scene that is currently active.
    private static Scene s_activeScene;

    // The next scene to switch to, if there is one.
    private static Scene s_nextScene;

    /// <summary>
    /// Gets the graphics device manager to control the presentation of graphics.
    /// </summary>
    public static GraphicsDeviceManager Graphics { get; private set; }

    /// <summary>
    /// Gets the graphics device used to create graphical resources and perform primitive rendering.
    /// </summary>
    public static new GraphicsDevice GraphicsDevice { get; private set; }

    /// <summary>
    /// Gets the sprite batch used for all 2D rendering.
    /// </summary>
    public static SpriteBatch SpriteBatch { get; private set; }

    /// <summary>
    /// Gets the content manager used to load global assets.
    /// </summary>
    public static new ContentManager Content { get; private set; }

    /// <summary>
    /// Gets the content manager used to load assets owned by this library rather
    /// than by the game consuming it.
    /// </summary>
    /// <remarks>
    /// The 2D Shaders tutorial gives shared assets their own .mgcb and their own
    /// ContentManager rooted at "SharedContent". Here they are instead built by
    /// MonoGameLibrary.csproj's BuildSharedContent target and merged into the single
    /// content root, so this is an alias for <see cref="Content"/> - it exists so the
    /// tutorial's Core.SharedContent.Load/WatchMaterial calls work unchanged.
    /// </remarks>
    public static ContentManager SharedContent => Content;

    /// <summary>
    /// Gets a reference to to the input management system.
    /// </summary>
    public static InputManager Input { get; private set; }

    /// <summary>
    /// Gets or Sets a value that indicates if the game should exit when the esc key on the keyboard is pressed.
    /// </summary>
    public static bool ExitOnEscape { get; set; }

    /// <summary>
    /// Gets or Sets a value that indicates if the F key on the keyboard toggles fullscreen.
    /// </summary>
    /// <remarks>
    /// Defaults to false so existing games keep their current behavior unless they opt in.
    /// </remarks>
    public static bool ToggleFullScreenOnF { get; set; }

    /// <summary>
    /// Gets or Sets a value that indicates if the scene transition quad (the fade/wipe effect
    /// driven by <see cref="SceneTransitionMaterial"/>) should be drawn. The transition's state
    /// still progresses underneath either way; this only controls whether it's drawn.
    /// </summary>
    /// <remarks>
    /// Defaults to true so existing games keep their current behavior unless they opt out.
    /// </remarks>
    public static bool UseSceneTransition { get; set; } = true;

    /// <summary>
    /// Gets or Sets a value that indicates if the game draws at a fixed virtual resolution
    /// (the width and height given to the constructor) and scales it to fit the back buffer,
    /// with black bars where the aspect ratios differ.
    /// </summary>
    /// <remarks>
    /// Defaults to false so existing games keep their current behavior unless they opt in.
    /// Scenes and materials that set their own render targets will need to restore
    /// <see cref="VirtualTarget"/> instead of null when this is on. The debug overlay is
    /// always drawn at the real back buffer size, unscaled.
    /// </remarks>
    public static bool UseVirtualResolution { get; set; }

    /// <summary>
    /// Gets the width, in pixels, of the virtual resolution: the width given to the constructor.
    /// </summary>
    public static int VirtualWidth { get; private set; }

    /// <summary>
    /// Gets the height, in pixels, of the virtual resolution: the height given to the constructor.
    /// </summary>
    public static int VirtualHeight { get; private set; }

    /// <summary>
    /// Gets the render target the game draws into when <see cref="UseVirtualResolution"/> is on.
    /// </summary>
    public static RenderTarget2D VirtualTarget { get; private set; }

    /// <summary>
    /// Gets where the virtual resolution lands on the back buffer: as large as fits while
    /// keeping its aspect ratio, centered. The full back buffer when the feature is off.
    /// </summary>
    public static Rectangle VirtualDestination
    {
        get
        {
            var parameters = GraphicsDevice.PresentationParameters;
            int backBufferWidth = parameters.BackBufferWidth;
            int backBufferHeight = parameters.BackBufferHeight;

            if (!UseVirtualResolution)
            {
                return new Rectangle(0, 0, backBufferWidth, backBufferHeight);
            }

            float scale = Math.Min((float)backBufferWidth / VirtualWidth, (float)backBufferHeight / VirtualHeight);
            int width = (int)(VirtualWidth * scale);
            int height = (int)(VirtualHeight * scale);

            return new Rectangle((backBufferWidth - width) / 2, (backBufferHeight - height) / 2, width, height);
        }
    }

    /// <summary>
    /// Gets the matrix that maps a back buffer position (such as the raw mouse position)
    /// into virtual resolution coordinates. Identity when the feature is off.
    /// </summary>
    public static Matrix ScreenToVirtual
    {
        get
        {
            if (!UseVirtualResolution)
            {
                return Matrix.Identity;
            }

            Rectangle destination = VirtualDestination;
            return Matrix.CreateTranslation(-destination.X, -destination.Y, 0f)
                 * Matrix.CreateScale((float)VirtualWidth / destination.Width, (float)VirtualHeight / destination.Height, 1f);
        }
    }

    /// <summary>
    /// Gets a reference to the audio control system.
    /// </summary>
    public static AudioController Audio { get; private set; }

    /// <summary>  
    /// Gets the ImGui renderer used for debug UIs.  
    /// </summary>  
    public static ImGuiRenderer ImGuiRenderer { get; private set; }

    /// <summary>  
    /// The material that is used when changing scenes  
    /// </summary>  
    public static Material SceneTransitionMaterial { get; private set; }

    /// <summary>  
    /// The material that draws point lights  
    /// </summary>  
    public static Material PointLightMaterial { get; private set; }

    /// <summary>  
    /// The material that combines the various off screen textures  
    /// </summary>  
    public static Material DeferredCompositeMaterial { get; private set; }

    /// <summary>  
    /// The  material that draws shadow hulls  
    /// </summary>  
    public static Material ShadowHullMaterial { get; private set; }


    /// <summary>  
    /// A set of grayscale gradient textures to use as transition guides  
    /// </summary>  
    
    public static List<Texture2D> SceneTransitionTextures { get; private set; }

    /// <summary>  
    /// The current transition between scenes  
    /// </summary>  
    public static SceneTransition SceneTransition { get; protected set; } = SceneTransition.Open(1000);

    /// <summary>
    /// Creates a new Core instance.
    /// </summary>
    /// <param name="title">The title to display in the title bar of the game window.</param>
    /// <param name="width">The initial width, in pixels, of the game window.</param>
    /// <param name="height">The initial height, in pixels, of the game window.</param>
    /// <param name="fullScreen">Indicates if the game should start in fullscreen mode.</param>
    public Core(string title, int width, int height, bool fullScreen)
    {
        // Ensure that multiple cores are not created.
        if (s_instance != null)
        {
            throw new InvalidOperationException($"Only a single Core instance can be created");
        }

        // Store reference to engine for global member access.
        s_instance = this;

        // Create a new graphics device manager.
        Graphics = new GraphicsDeviceManager(this);


        // Set the graphics defaults
        Graphics.PreferredBackBufferWidth = width;
        Graphics.PreferredBackBufferHeight = height;
        VirtualWidth = width;
        VirtualHeight = height;
        Graphics.IsFullScreen = fullScreen;

        // Apply the graphic presentation changes.
        Graphics.ApplyChanges();

        // Set the window title
        Window.Title = title;

        // Set the core's content manager to a reference of the base Game's
        // content manager.
        Content = base.Content;

        // Set the root directory for content.
        Content.RootDirectory = "Content";

        // Mouse is visible by default.
        IsMouseVisible = true;

        // Exit on escape is true by default
        ExitOnEscape = true;        
    }

    protected override void Initialize()
    {
        base.Initialize();



        // Set the core's graphics device to a reference of the base Game's
        // graphics device.
        GraphicsDevice = base.GraphicsDevice;

        // Create a 1x1 white pixel texture for drawing quads.
        Pixel = new Texture2D(GraphicsDevice, 1, 1);
        Pixel.SetData(new Color[]{ Color.White });

        // Create the sprite batch instance.
        SpriteBatch = new SpriteBatch(GraphicsDevice);

        // Create a new input manager.
        Input = new InputManager();

        // Create a new audio controller.
        Audio = new AudioController();

        // Create the ImGui renderer.
        ImGuiRenderer = new ImGuiRenderer(this);
        ImGuiRenderer.RebuildFontAtlas();

        // Optional: Scale text and widgets for easier readability.
        var io = ImGui.GetIO();
        io.FontGlobalScale = 1.75f;
        ImGui.GetStyle().ScaleAllSizes(1.5f);
    }
    protected override void LoadContent()  
    {  
        base.LoadContent();  
        SceneTransitionMaterial = Content.WatchMaterial("effects/sceneTransitionEffect"); 
        SceneTransitionMaterial.SetParameter("EdgeWidth", .05f);
        SceneTransitionMaterial.IsDebugVisible = false; 

        SceneTransitionTextures = new List<Texture2D>();
        SceneTransitionTextures.Add(Content.Load<Texture2D>("images/angled"));
        SceneTransitionTextures.Add(Content.Load<Texture2D>("images/concave"));
        SceneTransitionTextures.Add(Content.Load<Texture2D>("images/radial"));
        SceneTransitionTextures.Add(Content.Load<Texture2D>("images/ripple"));

        PointLightMaterial = SharedContent.WatchMaterial("effects/pointLightEffect");
        PointLightMaterial.IsDebugVisible = false;
        PointLightMaterial.SetParameter("LightBrightness", .25f);
        PointLightMaterial.SetParameter("LightSharpness", .1f);

        DeferredCompositeMaterial = SharedContent.WatchMaterial("effects/deferredCompositeEffect");
        DeferredCompositeMaterial.IsDebugVisible = false;

        ShadowHullMaterial = SharedContent.WatchMaterial("effects/shadowHullEffect");
        ShadowHullMaterial.IsDebugVisible = false;
    }

    protected override void UnloadContent()
    {
        // Dispose of the audio controller.
        Audio.Dispose();

        base.UnloadContent();
    }

    protected override void Update(GameTime gameTime)
    {
        // Update the input manager.
        Input.Update(gameTime);

        // Update the audio controller.
        Audio.Update();

        if (ExitOnEscape && Input.Keyboard.WasKeyJustPressed(Keys.Escape))
        {
            Exit();
        }

        if (ToggleFullScreenOnF && Input.Keyboard.WasKeyJustPressed(Keys.F))
        {
            Graphics.ToggleFullScreen();
        }

        // if there is a next scene waiting to be switch to, then transition
        // to that scene.
        if (s_nextScene != null && SceneTransition.IsComplete)
        {
            TransitionScene();
        }

        // If there is an active scene, update it.
        if (s_activeScene != null)
        {
            s_activeScene.Update(gameTime);
        }
        
        // Check if the scene transition material needs to be reloaded.
        SceneTransitionMaterial.SetParameter("Progress", SceneTransition.DirectionalRatio);
        SceneTransitionMaterial.Update();
        PointLightMaterial.Update();
        DeferredCompositeMaterial.SetParameter("ScreenSize", new Vector2(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height));
        DeferredCompositeMaterial.SetParameter("BoxBlurStride", .18f);
        DeferredCompositeMaterial.Update();
        ShadowHullMaterial.SetParameter("ShadowFadeStartDistance", .013f);  
        ShadowHullMaterial.SetParameter("ShadowFadeEndDistance", .13f);
        ShadowHullMaterial.SetParameter("ShadowIntensity", .85f);
        ShadowHullMaterial.Update();
        
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        if (UseVirtualResolution)
        {
            VirtualTarget ??= new RenderTarget2D(GraphicsDevice, VirtualWidth, VirtualHeight);
            GraphicsDevice.SetRenderTarget(VirtualTarget);
        }

        GraphicsDevice.Clear(Color.Black);

        // If there is an active scene, draw it.
        if (s_activeScene != null)
        {
            s_activeScene.Draw(gameTime);
        }

        // Draw the scene transition quad
        if (UseSceneTransition)
        {
            SpriteBatch.Begin(effect: SceneTransitionMaterial.Effect);
            SpriteBatch.Draw(SceneTransitionTextures[SceneTransition.TextureIndex % SceneTransitionTextures.Count], GraphicsDevice.Viewport.Bounds, Color.White);
            SpriteBatch.End();
        }

        if (UseVirtualResolution)
        {
            // Scale the finished frame onto the real back buffer. Linear filtering,
            // because the scale is rarely a whole number and point sampling would
            // make some pixel rows and columns visibly thicker than others.
            GraphicsDevice.SetRenderTarget(null);
            GraphicsDevice.Clear(Color.Black);
            SpriteBatch.Begin(samplerState: SamplerState.LinearClamp);
            SpriteBatch.Draw(VirtualTarget, VirtualDestination, Color.White);
            SpriteBatch.End();
        }

        DebugOverlay.Draw(gameTime);

        base.Draw(gameTime);
    }

    public static void ChangeScene(Scene next)
    {
        // Only set the next scene value if it is not the same
        // instance as the currently active scene.
        if (s_activeScene != next)
        {
            s_nextScene = next;
            SceneTransition = SceneTransition.Close(250);
        }
    }

    private static void TransitionScene()
    {
        SceneTransition = SceneTransition.Open(500);
        // If there is an active scene, dispose of it.
        if (s_activeScene != null)
        {
            s_activeScene.Dispose();
        }

        // Force the garbage collector to collect to ensure memory is cleared.
        GC.Collect();

        // Change the currently active scene to the new scene.
        s_activeScene = s_nextScene;

        // Null out the next scene value so it does not trigger a change over and over.
        s_nextScene = null;

        // If the active scene now is not null, initialize it.
        // Remember, just like with Game, the Initialize call also calls the
        // Scene.LoadContent
        if (s_activeScene != null)
        {
            s_activeScene.Initialize();
        }
    }
}
