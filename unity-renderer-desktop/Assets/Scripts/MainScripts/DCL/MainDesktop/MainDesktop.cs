using System;
using System.Linq;
using DCL.SettingsCommon;
using DCL.Components;
using MainScripts.DCL.Controllers.HUD.Preloading;
using MainScripts.DCL.Controllers.LoadingFlow;
using MainScripts.DCL.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DCL
{
    /// <summary>
    /// This is the MainDesktop entry point.
    /// Most of the application subsystems should be initialized from this class Awake() event.
    /// </summary>
    public class MainDesktop : Main
    {
        private LoadingFlowController loadingFlowController;
        private PreloadingController preloadingController;
        private bool isConnectionLost;

        protected override void Awake()
        {
            CommandLineParserUtils.ParseArguments();
            isConnectionLost = false;

            DCLVideoTexture.videoPluginWrapperBuilder = () => new VideoPluginWrapper_Native();

            InitializeSettings();

            base.Awake();
            DataStore.i.wsCommunication.communicationEstablished.OnChange += OnCommunicationEstablished;
            DataStore.i.performance.multithreading.Set(true);
            DataStore.i.performance.maxDownloads.Set(50);
            CheckForIncorrectScreenSize();
        }

        protected override void InitializeCommunication()
        {
            // TODO(Brian): Remove this branching once we finish migrating all tests out of the
            //              IntegrationTestSuite_Legacy base class.
            if (!Configuration.EnvironmentSettings.RUNNING_TESTS)
            {
                var withSSL = true;
                int startPort = CommandLineParserUtils.startPort;

#if UNITY_EDITOR
                withSSL = DebugConfigComponent.i.webSocketSSL;
                startPort = 5000;
#endif

                int endPort = startPort + 100;
                kernelCommunication = new WebSocketCommunication(withSSL, startPort, endPort);
            }
        }
        
        protected override void SetupPlugins()
        {
            pluginSystem = PluginSystemFactoryDesktop.Create();
        }

        private void CheckForIncorrectScreenSize()
        {
            var maxRes = Screen.resolutions[Screen.resolutions.Length - 1];
            bool supports4KResolution = maxRes.width >= 3840;
            int minWidth = supports4KResolution ? maxRes.width / 2 : 1024;
            var currentWidth = Screen.currentResolution.width;

            if (currentWidth >= minWidth) return;
            
            var availableFilteredResolutions =
                Screen.resolutions.Where(r => r.width >= minWidth && r.refreshRate > 0).ToArray();
            
            var minRes = availableFilteredResolutions[0];

            Screen.SetResolution(minRes.width, minRes.height, Screen.fullScreenMode);
        }

        private void InitializeSettings()
        {
            Settings.CreateSharedInstance(new DefaultSettingsFactory()
                .WithGraphicsQualitySettingsPresetPath("DesktopGraphicsQualityPresets"));
        }

        protected override void Dispose()
        {
            try
            {
                DataStore.i.wsCommunication.communicationEstablished.OnChange -= OnCommunicationEstablished;

                base.Dispose();
                DesktopDestroy();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void DesktopDestroy()
        {
            loadingFlowController.Dispose();
            preloadingController.Dispose();
            DCLVideoPlayer.StopAllThreads();
        }

        void OnCommunicationEstablished(bool current, bool previous)
        {
            if (current == false && previous)
            {
                isConnectionLost = true;
            }
        }

        protected override void Update()
        {
            base.Update();
            loadingFlowController.Update();

            if (isConnectionLost)
            {
                DesktopUtils.Quit();
            }
        }

        protected override void Start()
        {
            loadingFlowController = new LoadingFlowController(
                DataStore.i.HUDs.loadingHUD.fatalError,
                DataStore.i.HUDs.loadingHUD.visible,
                CommonScriptableObjects.rendererState,
                DataStore.i.wsCommunication.communicationEstablished);

            base.Start();
        }

        protected override void InitializeSceneDependencies()
        {
            base.InitializeSceneDependencies();
            preloadingController = new PreloadingController();
        }

        protected override void SetupServices()
        {
            Environment.Setup(ServiceLocatorDesktopFactory.CreateDefault());
        }
    }
}
