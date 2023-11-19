using System.Collections.Generic;
using System;
using Modding;
using Satchel.BetterMenus;
using SFCore.Utils;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using System.Linq;

namespace CollectorModifier {
    public class CollectorModifier : Mod, ICustomMenuMod, ILocalSettings<LocalSettings> {
        private Menu menuRef = null;
        public static CollectorModifier instance;
        private PlayMakerFSM controlFSM = null;
        private PlayMakerFSM phaseControlFSM = null;
        private PlayMakerFSM damageControlFSM = null;

        public CollectorModifier() : base("Collector Modifier") => instance = this;

        public static LocalSettings localSettings { get; private set; } = new();
        public void OnLoadLocal(LocalSettings s) => localSettings = s;
        public LocalSettings OnSaveLocal() => localSettings;

        public override string GetVersion() => GetType().Assembly.GetName().Version.ToString();

        public bool ToggleButtonInsideMenu => false;

        public override void Initialize() {
            Log("Initializing");

            On.PlayMakerFSM.OnEnable += OnFsmEnable;
            UnityEngine.SceneManagement.SceneManager.activeSceneChanged += SceneChanged;

            Log("Initialized");
        }

        public MenuScreen GetMenuScreen(MenuScreen modListMenu, ModToggleDelegates? toggleDelegates) {    
            menuRef ??= new Menu(
                name: "Collector Modifier",
                elements: new Element[] {
                    new CustomSlider(
                        name: "Minion Spawn Cutoff",
                        storeValue: val => {
                            localSettings.spawnCutoff = (int)val;
                            ApplySpawnCutoff();
                        },
                        loadValue: () => localSettings.spawnCutoff,
                        minValue: 4,
                        maxValue: 20,
                        wholeNumbers: true,
                        Id: "spawnCutoff"
                    ),
                    new CustomSlider(
                        name: "Max Number of Minions Per Wave",
                        storeValue: val => {
                            localSettings.maxNumMinionsPerWave = (int)val;
                            ApplyMaxNumMinionsPerWave();
                        },
                        loadValue: () => localSettings.maxNumMinionsPerWave,
                        minValue: 3,
                        maxValue: 20,
                        wholeNumbers: true,
                        Id: "maxNumMinionsPerWave"
                    ),
                    new MenuButton(
                        name: "Reset To Defaults",
                        description: "",
                        submitAction: _ => ResetToDefaults()
                    )
                }
            );
            
            return menuRef.GetMenuScreen(modListMenu);
        }

        private void OnFsmEnable(On.PlayMakerFSM.orig_OnEnable orig, PlayMakerFSM self) {
            orig(self);

            if (self.gameObject.name == "Jar Collector") {
                if (self.FsmName == "Control") {
                    controlFSM = self;
                    ApplySettings();
                } else if (self.FsmName == "Phase Control") {
                    phaseControlFSM = self;
                    ApplySettings();
                } else if (self.FsmName == "Damage Control") {
                    damageControlFSM = self;
                }
            }
        }

        private void SceneChanged(UnityEngine.SceneManagement.Scene _, UnityEngine.SceneManagement.Scene to) {
            if (to.name != "GG_Collector" && to.name != "GG_Collector_V") {
                controlFSM = null;
            }
        }

        public void ApplySettings() {
            ApplySpawnCutoff();
            ApplyMaxNumMinionsPerWave();
        }

        public void ApplySpawnCutoff() {
            if (controlFSM == null) return;

            controlFSM.GetAction<IntCompare>("Summon?", 1).integer2 = localSettings.spawnCutoff;
            controlFSM.Fsm.Variables.GetFsmInt("Enemies Max").Value = localSettings.spawnCutoff;
        }

        public void ApplyMaxNumMinionsPerWave() {
            if (phaseControlFSM == null) return;

            phaseControlFSM.GetAction<SetFsmInt>("Phase 2", 0).setValue = (int)Math.Ceiling((double)(localSettings.maxNumMinionsPerWave / 2));
            phaseControlFSM.GetAction<SetFsmInt>("Phase 2", 1).setValue = localSettings.maxNumMinionsPerWave;

            if (damageControlFSM == null) return;
            if (damageControlFSM.Fsm.Variables.GetFsmBool("Phase 2").Value) {
                controlFSM.Fsm.Variables.GetFsmInt("Spawn Min").Value = (int)Math.Ceiling((double)(localSettings.maxNumMinionsPerWave / 2));
                controlFSM.Fsm.Variables.GetFsmInt("Spawn Max").Value = localSettings.maxNumMinionsPerWave;
            }
        }

        public void ResetToDefaults() {
            localSettings.spawnCutoff = 4;
            localSettings.maxNumMinionsPerWave = 3;

            ApplySettings();

            CustomSlider spawnCutoffSlider = menuRef.Find("spawnCutoff") as CustomSlider;
            CustomSlider maxNumMinionsPerWaveSlider = menuRef.Find("maxNumMinionsPerWave") as CustomSlider;
            spawnCutoffSlider.Update();
            maxNumMinionsPerWaveSlider.Update();
        }
    }

    public class LocalSettings {
        public int spawnCutoff = 4;
        public int maxNumMinionsPerWave = 3;
    }
}
