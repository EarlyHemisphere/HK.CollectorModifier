using Modding;
using Satchel.BetterMenus;
using SFCore.Utils;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;

namespace CollectorModifier {
    public class CollectorModifier : Mod, ICustomMenuMod, ILocalSettings<LocalSettings> {
        private Menu menuRef = null;
        public static CollectorModifier instance;
        private PlayMakerFSM controlFSM = null;
        private PlayMakerFSM phaseControlFSM = null;
        private PlayMakerFSM damageControlFSM = null;
        private PlayMakerFSM stunControlFSM = null;

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
                        name: "Min Minions Per Wave",
                        storeValue: val => {
                            localSettings.minNumMinionsPerWave = (int)val;
                            ApplyMaxNumMinionsPerWave();
                        },
                        loadValue: () => localSettings.minNumMinionsPerWave,
                        minValue: 2,
                        maxValue: 40,
                        wholeNumbers: true,
                        Id: "minNumMinionsPerWave"
                    ),
                    new CustomSlider(
                        name: "Max Minions Per Wave",
                        storeValue: val => {
                            localSettings.maxNumMinionsPerWave = (int)val;
                            ApplyMaxNumMinionsPerWave();
                        },
                        loadValue: () => localSettings.maxNumMinionsPerWave,
                        minValue: 3,
                        maxValue: 40,
                        wholeNumbers: true,
                        Id: "maxNumMinionsPerWave"
                    ),
                    new HorizontalOption(
                        name: "Disable Stagger",
                        description: "Prevents stagger",
                        values: new [] {"true", "false"},
                        applySetting: val => {
                            localSettings.disableStagger = val == 0;
                            ApplyDisableStagger();
                        },
                        loadSetting: () => localSettings.disableStagger ? 0 : 1,
                        Id: "disableStagger"
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
                } else if (self.FsmName == "Phase Control") {
                    phaseControlFSM = self;
                } else if (self.FsmName == "Damage Control") {
                    damageControlFSM = self;
                } else if (self.FsmName == "Stun Control") {
                    stunControlFSM = self;
                }
                ApplySettings();
            }
        }

        private void SceneChanged(UnityEngine.SceneManagement.Scene _, UnityEngine.SceneManagement.Scene to) {
            if (to.name != "GG_Collector" && to.name != "GG_Collector_V") {
                controlFSM = null;
                phaseControlFSM = null;
                damageControlFSM = null;
                stunControlFSM = null;
            }
        }

        public void ApplySettings() {
            ApplySpawnCutoff();
            ApplyMinNumMinionsPerWave();
            ApplyMaxNumMinionsPerWave();
            ApplyDisableStagger();
        }

        public void ApplySpawnCutoff() {
            if (controlFSM == null) return;

            controlFSM.GetAction<IntCompare>("Summon?", 1).integer2 = localSettings.spawnCutoff;
            controlFSM.Fsm.Variables.GetFsmInt("Enemies Max").Value = localSettings.spawnCutoff;
        }

        public void ApplyMinNumMinionsPerWave() {
            if (phaseControlFSM == null) return;

            phaseControlFSM.GetAction<SetFsmInt>("Phase 2", 0).setValue = localSettings.minNumMinionsPerWave;

            if (damageControlFSM == null) return;
            if (damageControlFSM.Fsm.Variables.GetFsmBool("Phase 2").Value) {
                controlFSM.Fsm.Variables.GetFsmInt("Spawn Min").Value = localSettings.minNumMinionsPerWave;
            }
        }

        public void ApplyMaxNumMinionsPerWave() {
            if (localSettings.maxNumMinionsPerWave < localSettings.minNumMinionsPerWave) {
                localSettings.maxNumMinionsPerWave = localSettings.minNumMinionsPerWave;
            }

            if (phaseControlFSM == null) return;

            phaseControlFSM.GetAction<SetFsmInt>("Phase 2", 1).setValue = localSettings.maxNumMinionsPerWave;

            if (damageControlFSM == null) return;
            if (damageControlFSM.Fsm.Variables.GetFsmBool("Phase 2").Value) {
                controlFSM.Fsm.Variables.GetFsmInt("Spawn Max").Value = localSettings.maxNumMinionsPerWave;
            }
        }

        public void ApplyDisableStagger() {
            if (stunControlFSM != null) {
                int numActions = stunControlFSM.GetState("Stun").Actions.Length;
                if (localSettings.disableStagger && numActions == 5) {
                    stunControlFSM.RemoveAction("Stun", 2);
                } else if (!localSettings.disableStagger && numActions == 4) {
                    FsmEventTarget target = new FsmEventTarget();
                    target.target = FsmEventTarget.EventTarget.Self;
                    stunControlFSM.GetState("Stun").InsertAction(new SendEventByName {
                        eventTarget = target,
                        sendEvent = "STUN",
                        delay = 0f,
                        everyFrame = false
                    }, 1);
                }
            }
        }

        public void ResetToDefaults() {
            localSettings.spawnCutoff = 4;
            localSettings.maxNumMinionsPerWave = 3;

            ApplySettings();

            CustomSlider spawnCutoffSlider = menuRef.Find("spawnCutoff") as CustomSlider;
            CustomSlider minNumMinionsPerWaveSlider = menuRef.Find("minNumMinionsPerWave") as CustomSlider;
            CustomSlider maxNumMinionsPerWaveSlider = menuRef.Find("maxNumMinionsPerWave") as CustomSlider;
            HorizontalOption disableStagger = menuRef.Find("disableStagger") as HorizontalOption;
            spawnCutoffSlider.Update();
            minNumMinionsPerWaveSlider.Update();
            maxNumMinionsPerWaveSlider.Update();
            disableStagger.Update();
        }
    }

    public class LocalSettings {
        public int spawnCutoff = 4;
        public int minNumMinionsPerWave = 2;
        public int maxNumMinionsPerWave = 3;
        public bool disableStagger = false;
    }
}
