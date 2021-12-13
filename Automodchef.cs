using I2.Loc;
using MaterialUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.Analytics;
using UnityEngine.UI;
using ZyMod;
using static ZyMod.ModHelpers;
using static I2.Loc.ScriptLocalization.Warnings;

namespace Automodchef {
   using Ex = Exception;

   public class Automodchef : ZySimpleMod {
      public static void Main () => new Automodchef().Initialize();
      protected override string GetAppDataDir () {
         var path = System.Environment.GetFolderPath( System.Environment.SpecialFolder.LocalApplicationData );
         if ( string.IsNullOrEmpty( path ) ) return null;
         return Path.Combine( Directory.GetParent( path ).FullName, "LocalLow", "HermesInteractive", "Automachef" );
      }
      protected override Type GetPatchClass () => typeof( Patches );
      protected override void OnGameAssemblyLoaded ( Assembly game ) => Patches.Apply( game );
   }

   public class Config : IniConfig {
      [ ConfigAttribute( "To change log level, create new file Automochef-log.conf with the first line saying Off, Error, Warning, Info, or Verbose.\r\n; Version of this Automodchef config file.  Don't touch!" ) ]
      public int config_version = 20211206;

      [ ConfigAttribute( "System", "Skip Unity, Hermes, Team 17, and Autosave screens.  Default true." ) ]
      public bool skip_intro = true;
      [ ConfigAttribute( "System", "Skip 'Press Spacebar to continue'.  Default true." ) ]
      public bool skip_spacebar = true;
      [ ConfigAttribute( "System", "Disable mission stats analytics.  Default true." ) ]
      public bool disable_analytics = true;

      [ ConfigAttribute( "Bug Fix", "Fix food mouseover hints not showing when game is paused.  Default true." ) ]
      public bool fix_food_hint_when_paused = true;
      [ ConfigAttribute( "Bug Fix", "Make sure all dishes that have this much efficiency quota for their ingredients.  Game is inconsistent.  Mod default 1.  0 to fix Double Bypass Meal.  -1 to disable.  When set to 1, this mod will not apply buffer to single ingredient recipes for better balance." ) ]
      public sbyte dish_ingredient_quota_buffer = 1;

      [ ConfigAttribute( "Camera", "Angle of left / right view.  Game default 35.  Set to 0 to not change." ) ]
      public float side__view_angle = 0;
      [ ConfigAttribute( "Camera", "Downward angle of close up view.  Game default 40.  Set to 0 to not change." ) ]
      public float close_view_angle = 0;
      [ ConfigAttribute( "Camera", "Downward angle of far view.  Game default 60.  Mode default 70.  Set to 0 to not change.  Set to 90 for a top-down view." ) ]
      public float far_view_angle = 70;
      [ ConfigAttribute( "Camera", "Height of far camera.  Game default 20.  Set to 0 to not change." ) ]
      public float far_view_height = 0;

      [ ConfigAttribute( "User Interface", "Suppress yes/no confirmations - save before quit, load game, delete or overwrite save / blueprint / scenario, quit level / game, reset layout.  Default true." ) ]
      public bool suppress_confirmation = true;
      [ ConfigAttribute( "User Interface", "Show load game prompt when entering a level (if any saves).  Loading a game will bypass level goal popup and roboto speech.  Default true." ) ]
      public bool ask_loadgame_on_level_start = true;
      [ ConfigAttribute( "User Interface", "Keep save dialog open after deleting a save.  Default true." ) ]
      public bool stay_open_after_delete_save = true;
      [ ConfigAttribute( "User Interface", "Max number if options to convert dropdown to toggle button.  Default 3.  0 to disable." ) ]
      public byte dropdown_toogle_threshold = 3;
      [ ConfigAttribute( "User Interface", "Hide efficiency of tutorial levels.  Default true." ) ]
      public bool hide_tutorial_efficiency = true;

      [ ConfigAttribute( "Log", "Show real-time power usage in mouseover tooltips.  Default true." ) ]
      public bool tooltip_power_usage = true;
      [ ConfigAttribute( "Log", "Show food freshness in mouseover tooltips.  Default true." ) ]
      public bool tooltip_freshness = true;
      [ ConfigAttribute( "Log", "Add efficiency calculation to kitchen log.  Default true." ) ]
      public bool efficiency_log = true;
      [ ConfigAttribute( "Log", "Breakdown efficiency quotas by dishes.  Default true." ) ]
      public bool efficiency_log_breakdown = true;
      [ ConfigAttribute( "Log", "Show top X power consuming part types in kitchen log.  Default 5.  0 to disable." ) ]
      public byte power_log_rows = 5;

      [ ConfigAttribute( "Simulation", "Change game speed instantaneously.  Default true." ) ]
      public bool instant_speed_change = true;
      [ ConfigAttribute( "Simulation", "Speed of double time (two arrows).  0-100 integer.  Game default 3.  Mod default 5." ) ]
      public byte speed2 = 5;
      [ ConfigAttribute( "Simulation", "Speed of triple time (three arrows).  0-100 integer.  Game default 5.  Mod default 20.  High speed may cause some orders to expire when they would not on slower speeds." ) ]
      public byte speed3 = 20;

      [ ConfigAttribute( "Mechanic", "Food processor use less power when not processing.  Game default 800 (no idle mode).  Mod default -1 which does not enable idle mode.  Its belt moves at half speed so 75W can be 'realistic'." ) ]
      public int food_processor_idle_power = -1;
      [ ConfigAttribute( "Mechanic", "This section changes game mechanics. They do not break or brick saves, but may allow non-vanilla solutions.\r\n; Packaging machine use less power when not packaging.  Game default 800 (no idle mode).  Mod default 60 (2x slowest belts).  Set to -1 to not change." ) ]
      public int packaging_machine_idle_power = 60;
      [ ConfigAttribute( "Mechanic", "Packaging machine will pass-through foods that it is not interested in, like assemblers.  You still need to assign at least one recipe to the machine.  Default true." ) ]
      public bool packaging_machine_passthrough = true;
      [ ConfigAttribute( "Mechanic", "Packaging machine's sub-recipes have lowest priority (Fries < Bacon Fries < Loaded Cheese Fries), last processed recipe have lower priority, and random for remaining ties.  Default true." ) ]
      public bool smart_packaging_machine = true;

      [ ConfigAttribute( "Misc", "Export foods to foods.csv on game launch.  Default false.  Neglectable speed impact, disabled because most players don't need the data." ) ]
      public bool export_food_csv = false;
      [ ConfigAttribute( "Misc", "Export hardwares to hardwares.csv on game launch.  Default false.  Ditto." ) ]
      public bool export_hardware_csv = false;
      [ ConfigAttribute( "Misc", "Export text from current language to text-0.csv on game launch.  Default false.  Ditto." ) ]
      public bool export_text_csv = false;
      [ ConfigAttribute( "Misc", "Change Simplified Chinese to Traditional Chinese with improved translations.  No effect on other langauges." ) ]
      public bool traditional_chinese = true;

      public override void Load ( string path ) {
         base.Load( path );
         if ( config_version >= 20211206 ) return;
         config_version = 20211206;
         Create( path );
      }
   }

   internal static class Patches {

      internal static Config config = new Config();

      internal static void Apply ( Assembly game ) {
         config.Load();
         ApplySystemPatches();
         ApplyUserInterfacePatches();
         ApplyLogPatches();
         ApplyMechanicPatches();
      }

      private static void ApplySystemPatches () { try {
         if ( config.skip_intro )
            Modder.TryPatch( typeof( FaderUIController ), "Awake", nameof( SkipVideoSplashes ) );
         if ( config.skip_spacebar )
            Modder.TryPatch( typeof( SplashScreen ), "Update", postfix: nameof( SkipSpacebarSplash ) );
         if ( config.disable_analytics ) {
            foreach ( var m in typeof( Analytics ).Methods().Where( e => e.Name == "CustomEvent" || e.Name == "Transaction" || e.Name.StartsWith( "Send" ) ) )
               Modder.TryPatch( m, nameof( DisableAnalytics ) );
            Modder.TryPatch( typeof( AutomachefAnalytics ), "Track", nameof( DisableAnalytics ) );
         }
         if ( config.fix_food_hint_when_paused )
            Modder.TryPatch( typeof( IngredientTooltip ), "Update", postfix: nameof( FixIngredientHintOnPause ) );
         if ( config.dish_ingredient_quota_buffer >= 0 )
            Modder.TryPatch( typeof( SplashScreen ), "Awake", postfix: nameof( FixDishIngredientQuota ) );
         if ( Non0( config.side__view_angle ) || Non0( config.close_view_angle ) || Non0( config.far_view_angle ) || Non0( config.far_view_height ) )
            Modder.TryPatch( typeof( CameraMovement ), "Awake", postfix: nameof( OverrideCameraSettings ) );
         Modder.TryPatch( typeof( ContractsLogic ), "AddNewIncomingContract", nameof( OverrideContracts ), nameof( RestoreContracts ) );
      } catch ( Ex x ) { Err( x ); } }

      private static void ApplyUserInterfacePatches () { try {
         if ( config.suppress_confirmation ) {
            var orig = typeof( DialogManager ).Methods( "ShowAlert" ).FirstOrDefault( e => e.GetParameters().Length == 7 );
            if ( orig != null ) Modder.TryPatch( orig, nameof( SuppressConfirmation ) );
         }
         if ( config.ask_loadgame_on_level_start ) {
            Modder.TryPatch( typeof( LevelManager ), "Start", postfix: nameof( SetNewLevelTrigger ) );
            Modder.TryPatch( typeof( SaveLoad ), "Close", nameof( RestorePreLevelScreen ) );
            Modder.TryPatch( typeof( SaveLoadManager ), "DeleteSave", nameof( ClearPreLevelFlag ) );
            Modder.TryPatch( typeof( SaveLoadManager ), "LoadAndBuildKitchen", nameof( ClearPreLevelFlag ) );
         }
         if ( config.stay_open_after_delete_save ) {
            Modder.TryPatch( typeof( SaveLoadManager ), "DeleteSave", nameof( DisableNextSaveLoadClose ) );
            Modder.TryPatch( typeof( SaveLoad ), "Close", nameof( CheckSaveLoadCloseDisabled ) );
         }
         if ( config.hide_tutorial_efficiency ) {
            Modder.TryPatch( typeof( LevelSelection ), "InitializeLevelList", postfix: nameof( HideTutorialMaxEfficiency ) );
            Modder.TryPatch( typeof( LevelStatus ), "RenderStats", postfix: nameof( HideTutorialEfficiencyStat ) );
         }
         if ( config.dropdown_toogle_threshold > 1 ) {
            dropdownIcon = new ConditionalWeakTable< MaterialDropdown, DropdownIcon >();
            Modder.TryPatch( typeof( PartProperties ), "PopulateDropdownForProperty", nameof( TrackDropdownIcon ) );
            Modder.TryPatch( typeof( MaterialDropdown ), "ShowDropdown", nameof( ToggleDropdown ) );
         }
      } catch ( Ex x ) { Err( x ); } }

      private static void ApplyLogPatches () { try {
         if ( config.tooltip_power_usage || config.power_log_rows > 0 ) {
            Modder.TryPatch( typeof( KitchenPart ).Methods( "ConsumePower" ).FirstOrDefault( e => e.GetParameters().Length > 0 ), nameof( LogPowerUsage ) );
            Modder.TryPatch( typeof( PowerMeter ), "Reset", nameof( ClearPowerUsage ) );
            if ( config.tooltip_power_usage )
               Modder.TryPatch( typeof( KitchenPart ), "GetTooltipText", postfix: nameof( AppendPowerToTooltip ) );
         }
         if ( config.tooltip_freshness ) {
            Modder.TryPatch( typeof( PackagingMachine ), "GetTooltipTextDetails", nameof( SuppressFreshnessTooltip ), nameof( RestoreFreshnessTooltip ) );
            Modder.TryPatch( typeof( Ingredient ), "GetTooltipText", postfix: nameof( AppendFreshnessTooltip ) );
         }
         if ( config.efficiency_log || config.power_log_rows > 0 ) {
            Modder.TryPatch( typeof( LevelStatus ), "RenderEvents", postfix: nameof( ForceShowEfficiencyLog ) );
            if ( config.efficiency_log ) {
               extraLog = new List<string>();
               if ( config.efficiency_log_breakdown )
                  if ( Modder.TryPatch( typeof( EfficiencyMeter ), "Reset", nameof( ClearEfficiencyLog ) ) != null ) {
                     orderedDish = new Dictionary<object, int>();
                     cookedDish = new Dictionary<object, int>();
                     Modder.TryPatch( typeof( EfficiencyMeter ), "AddOrder", nameof( TrackOrdersEfficiency ) );
                     Modder.TryPatch( typeof( EfficiencyMeter ), "AddDeliveredDish", nameof( TrackDeliveryEfficiency ) );
                  }
               Modder.TryPatch( typeof( EfficiencyMeter ), "GetEfficiency", postfix: nameof( CalculateEfficiency ) );
               Modder.TryPatch( typeof( KitchenEventsLog ), "ToString", postfix: nameof( AppendEfficiencyLog ) );
            }
            if ( config.power_log_rows > 0 )
               Modder.TryPatch( typeof( KitchenEventsLog ), "ToString", postfix: nameof( AppendPowerLog ) );
         }
      } catch ( Ex x ) { Err( x ); } }

      private static void ApplyMechanicPatches () { try {
         if ( config.instant_speed_change )
            Modder.TryPatch( typeof( Initializer ), "Update", postfix: nameof( InstantGameSpeedUpdate ) );
         if ( config.speed2 != 3 || config.speed3 != 5 )
            Modder.TryPatch( typeof( Initializer ), "Start", nameof( AdjustGameSpeedPresets ) );
         if ( config.food_processor_idle_power >= 0 )
            Modder.TryPatch( typeof( Processor ), "FixedUpdate", nameof( SetFoodProcessorPower ) );
         if ( config.packaging_machine_idle_power >= 0 )
            Modder.TryPatch( typeof( PackagingMachine ), "FixedUpdate", nameof( SetPackagingMachinePower ) );
         if ( config.packaging_machine_passthrough )
            Modder.TryPatch( typeof( PackagingMachine ), "FixedUpdate", nameof( PackagingMachinePassThrough ) );
         if ( config.smart_packaging_machine ) {
            packMachineCanMake = typeof( PackagingMachine ).Method( "AllIngredientsReady" );
            packMachineConsume = typeof( PackagingMachine ).Method( "ConsumeIngredients" );
            packMachinePackage = typeof( PackagingMachine ).Method( "StartPackaging" );
            packMachineRandom  = new System.Random();
            packMachineLastDish = new ConditionalWeakTable<PackagingMachine, Dish>();
            Modder.TryPatch( typeof( KitchenPart ), "Reset", postfix: nameof( ClearPackagingMachineLastDish ) );
            Modder.TryPatch( typeof( PackagingMachine ), "StartPackaging", nameof( LogPackagingMachineLastDish ) );
            Modder.TryPatch( typeof( PackagingMachine ), "SeeIfSomethingCanBePackaged", nameof( OverridePackagingMachineLogic ) );
         }
         if ( config.export_food_csv )
            Modder.TryPatch( typeof( SplashScreen ), "Awake", nameof( DumpFoodCsv ) );
         if ( config.export_hardware_csv )
            Modder.TryPatch( typeof( SplashScreen ), "Awake", nameof( DumpHardwareCsv ) );
         if ( config.export_text_csv )
            Modder.TryPatch( typeof( LocalizationManager ), "LocalizeAll", postfix: nameof( DumpLanguageCsv ) );
         if ( config.traditional_chinese ) {
            Modder.TryPatch( typeof( LanguageSelectionScreen ), "OnShown", nameof( ShowZht ) );
            Modder.TryPatch( typeof( LocalizationManager ), "CreateCultureForCode", nameof( DetectZh ) );
         }
      } catch ( Ex x ) { Err( x ); } }

      #region Skip Splash
      private static void SkipVideoSplashes ( ref FaderUIController.SplashStateInfo[] ___m_SplashStates ) { try {
         if ( ___m_SplashStates == null || ___m_SplashStates.Length <= 1 ) return;
         if ( ! ___m_SplashStates.Any( e => e.m_AnimToPlay == "LoadStartScreen" ) ) return;
         ___m_SplashStates = ___m_SplashStates.Where( e => e.m_AnimToPlay == "LoadStartScreen" ).ToArray();
         ___m_SplashStates[0].m_TimeInState = 0.01f;
         Info( "Skipping Logos and Warnings." );
         // [ { "Unity, 1, False }, { "HermesInteractive, 2, False }, { "Team 17, 4, True }, { "Legal, 3, False }, { "LoadStartScreen", 2, False } ]
      } catch ( Ex x ) { Err( x ); } }

      private static void SkipSpacebarSplash ( SplashScreen __instance, ref bool ___m_bProcessedCloseRequest ) { try {
         if ( ___m_bProcessedCloseRequest || InputWrapper.GetController() == null ) return;
         ___m_bProcessedCloseRequest = true;
         typeof( SplashScreen ).TryMethod( "ProceedToMainMenu" )?.Invoke( __instance, Array.Empty<object>() );
         Info( "Skipped Press Space Splash." );
      } catch ( Ex ex ) {
         Error( ex );
         ___m_bProcessedCloseRequest = false;
      } }
      #endregion

      private static bool DisableAnalytics () { Info( "Analytics Blocked" ); return false; }

      #region Bug fixes
      private static void FixIngredientHintOnPause ( IngredientTooltip __instance, RectTransform ___ourRectTransform ) { try {
         if ( __instance.canvasGroup.alpha != 0 || ! Initializer.GetInstance().IsSimRunning() ) return;
         if ( __instance.kitchenBuilder.IsSomethingBeingPlaced() || InputWrapper.IsPointerOverUI( -1 ) ) return;
         var ray = Camera.main.ScreenPointToRay( InputWrapper.mousePosition );
         if ( ! Physics.Raycast( ray, out RaycastHit raycastHit, 50 ) ) return;
         var food = raycastHit.transform.gameObject.GetComponent<Ingredient>();
         if ( food == null ) return;
         ___ourRectTransform.anchoredPosition = Camera.main.WorldToScreenPoint( raycastHit.transform.position ) / __instance.GetComponentInParent<Canvas>().scaleFactor;
         __instance.tooltipText.text = typeof( IngredientTooltip ).Method( "FormatText" ).Invoke( __instance, new object[]{ food.GetTooltipText() } ).ToString();
         __instance.canvasGroup.alpha = 1f;
      } catch ( Ex x ) { Err( x ); } }

      private static int FindDishMinIngredient ( Dish dish ) { try {
         return dish.recipe.Select( id => Ingredient.GetByInternalName( id ) ?? Dish.GetByInternalName( id ) ).Sum( e => e is Dish d ? FindDishMinIngredient( d ) : 1 );
      } catch ( Ex x ) { return Err( x, dish?.recipe?.Count ?? 0 ); } }

      private static void FixDishIngredientQuota () { try {
         var updated = false;
         foreach ( var dish in Dish.GetAll() ) {
            var i = FindDishMinIngredient( dish ) + config.dish_ingredient_quota_buffer;
            if ( i == 2 && config.dish_ingredient_quota_buffer == 1 ) i = 1;
            if ( i > dish.expectedIngredients ) {
               Info( "Bumping {0} ingredient quota from {1} to {2}.", dish.GetFriendlyNameTranslated(), dish.expectedIngredients, i );
               dish.expectedIngredients = i;
               updated = true;
            }
         }
         if ( updated && config.dish_ingredient_quota_buffer == 1 ) Info( "Dishes made from single ingredient are not buffed for better game balance." );
      } catch ( Ex x ) { Err( x ); } }
      #endregion

      private static void OverrideCameraSettings ( ref float ___wideAngle, ref float ___wideHeight, ref float ___teleAngle, ref float ___isometricAngle ) {
         if ( Non0( config.side__view_angle ) ) ___isometricAngle = config.side__view_angle;
         if ( Non0( config.close_view_angle ) ) ___teleAngle  = config.close_view_angle;
         if ( Non0( config.far_view_angle   ) ) ___wideAngle  = config.far_view_angle;
         if ( Non0( config.far_view_height  ) ) ___wideHeight = config.far_view_height;
         Info( "Camera settigns applied." );
         // Default camera settings:
         // edgePanMarginMouse = 10
         // edgePanMarginController = 80
         // wideFoV = 32
         // teleFoV = 16
         // wideAngle = 60
         // teleAngle = 40
         // isometricAngle = 35
         // wideHeight = 20
         // zoomSpeed = 60
         // panSpeed = 5
         // modeChangeSpeed = 150
      }

      private static List<Contract> allContracts;
      private static void OverrideContracts ( ref List<Contract> ___allPossibleContracts, Company ___company ) { // Find BeachBurger contracts for bug fixing.
         if ( allContracts != null ) allContracts = ___allPossibleContracts;
         List<Contract> filteredContracts = ___allPossibleContracts
            .Where( e => e.requiredDishes.Contains( "BeachBurger" ) && e.client.minReputation <= ___company.reputation ).ToList();
         if ( filteredContracts.Count == 0 ) return;
         Info( "Filtering {0} down to {1}.", allContracts.Count, filteredContracts.Count );
         ___allPossibleContracts = filteredContracts;
         // Client's name and clientName
         // Client1 The Feedbag
         // Client2 Heartburns
         // Client3 Dine 'N Dash
         // Client4 The Happy Gorger
         // Client5 Salad Bowl
         // Client6 Cheesy Does It
         // Client7 Calorie Cabin
         // Client8 Lots O' Flavour
         // Client9 Fresh & Tasty
         // Client10 Big Taste Inc.
      }
      private static void RestoreContracts ( ref List<Contract> ___allPossibleContracts ) => ___allPossibleContracts = allContracts;

      #region Pre-level load dialogue
      private static LevelManager currentLevel;
      private static bool lastPreLevelScreenState;

      private static void SetNewLevelTrigger ( LevelManager __instance, SaveLoadManager ___m_saveLoadManager ) { try {
         if ( IsTutorial( __instance.GetLevel() ) ) return;
         Fine( "Entering new non-tutorial level." );
         currentLevel = __instance;
         ___m_saveLoadManager.OnMetadataReady += OfferToLoadGameOnEnter;
      } catch ( Ex x ) { Err( x ); } }

      private static void OfferToLoadGameOnEnter () { try {
         Fine( "New level data loaded." );
         var data = SaveLoadManager.GetInstance().getSavedKitchenData();
         if ( data == null ) { Info( "Save data not found, aborting." ); return; }
		 for ( var i = 0; i < 5; i++ )
			if ( data.Get( i ) != null ) {
               Info( "Found saved level at slot {0}, offering load dialog.", i+1 );
               lastPreLevelScreenState = currentLevel.levelStatusUI.preLevelInfoRoot.gameObject.activeSelf;
               currentLevel.levelStatusUI.preLevelScreen.gameObject.SetActive( false );
               Initializer.GetInstance().saveLoadPanel.Open( false );
               return;
            }
         Info( "No saved level found.  Skipping load dialog." );
      } catch ( Ex x ) { Err( x ); } }

      private static void RestorePreLevelScreen () { try {
         if ( ! lastPreLevelScreenState ) return;
         Fine( "Restoring level brief screen." );
         lastPreLevelScreenState = false;
         currentLevel.levelStatusUI.preLevelScreen.gameObject.SetActive( true );
      } catch ( Ex ex ) { Error( ex ); } }

      private static void ClearPreLevelFlag () {
         Fine( "Save loaded or deleted.  Suppressing pre-level screen." );
         lastPreLevelScreenState = false;
         currentLevel?.levelStatusUI.preLevelScreen.gameObject.SetActive( false );
      }
      #endregion

      #region Save/Load Deletion
      private static bool bypassNextSaveLoad;
      private static void DisableNextSaveLoadClose ( ref Action OnDeleted ) {
         if ( OnDeleted != null ) return;
         bypassNextSaveLoad = true;
         OnDeleted = () => typeof( SaveLoad ).TryMethod( "RebuildList" )?.Invoke( Initializer.GetInstance().saveLoadPanel, null );
      }
      private static bool CheckSaveLoadCloseDisabled () => ( ! bypassNextSaveLoad ) || ( bypassNextSaveLoad = false );
      #endregion

      #region Dropdown Toggle
      private static ConditionalWeakTable< MaterialDropdown, DropdownIcon > dropdownIcon;

      private static void TrackDropdownIcon ( MaterialDropdown dropdown, KitchenPartProperty prop, DropdownIcon icon ) { try {
         Fine( "Tracking dropdown {0} icon for kitchen part prop {1}", dropdown.GetHashCode(), prop.name );
         dropdownIcon.Remove( dropdown ); if ( icon != null ) dropdownIcon.Add( dropdown, icon );
      } catch ( Ex x ) { Err( x ); } }

      private static bool ToggleDropdown ( MaterialDropdown __instance, ref int ___m_CurrentlySelected, OptionDataList ___m_OptionDataList ) { try {
         //if ( IsTutorial() ) return true;
         int max_options = ___m_OptionDataList?.options.Count ?? 0, new_selection = ___m_CurrentlySelected + 1;
         if ( max_options <= 1 || max_options > config.dropdown_toogle_threshold ) return true;
         Fine( "Toggle dropdown {0} of from {1} to {2} (or 0 if {3})", __instance.GetHashCode(), ___m_CurrentlySelected, new_selection, max_options );
         __instance.Select( new_selection >= max_options ? 0 : new_selection );
         if ( dropdownIcon.TryGetValue( __instance, out DropdownIcon icon ) ) icon?.UpdateIcon();
         return false;
      } catch ( Ex x ) { return Err( x, true ); } }
      #endregion

      #region Power
      private class PowerLog { internal float power; }
      private static ConditionalWeakTable< KitchenPart, PowerLog > powerLog;

      private static void LogPowerUsage ( KitchenPart __instance, float multiplier ) { try {
         if ( ! Initializer.GetInstance().IsSimRunning() || powerLog == null ) return;
         powerLog.GetOrCreateValue( __instance ).power += __instance.powerInWatts * multiplier;
      } catch ( Ex x ) { Err( x ); } }

      private static void ClearPowerUsage () { Fine( "Reset power log" ); powerLog = new ConditionalWeakTable<KitchenPart, PowerLog>(); }

      private static void AppendPowerToTooltip ( KitchenPart __instance, ref string __result ) { try {
         if ( ! Initializer.GetInstance().IsSimRunning() || __instance.powerInWatts <= 0 || powerLog == null ) return;
         powerLog.TryGetValue( __instance, out PowerLog log );
         __result += $"\n{PowerMeter.GetInstance().GetLastPowerUsage( __instance )}W >> {Wh(log?.power??0)}";
      } catch ( Ex x ) { Err( x ); } }

      private static void AppendPowerLog ( ref string __result ) { try {
         if ( powerLog == null || ( config.hide_tutorial_efficiency && IsTutorial() ) ) return;
         Info( "Appending power log (up to {0} lines) to kitchen log.", config.power_log_rows );
         float total = 0;
         Dictionary< string, PowerLog > byType = new Dictionary<string, PowerLog>();
         HashSet<KitchenPart> allParts = Initializer.GetInstance().kitchen.GetAllKitchenParts();
         foreach ( var part in allParts ) {
            if ( ! powerLog.TryGetValue( part, out PowerLog partPower ) || partPower.power <= 0 ) continue;
            var key = part.internalName;
            switch ( key ) {
               case "AdvancedAssembler"   : key = "Assembler"; break;
               case "AdvancedComputer"    : key = "Computer"; break;
               case "AdvancedOrderReader" : key = "OrderReader"; break;
               case "BeltBridge" : case "HighSpeedBelt" : case "Gate" : key = "Belt"; break;
               case "ConvectionFryer"     : key = "Fryer"; break;
               case "ConveyorGrill"       : key = "Grill"; break;
               case "HighSpeedDispenser"  : key = "Dispenser"; break;
               case "LargeStorageUnit"    : key = "StackingRobotArm"; break;
               case "LongRobotArm" : case "RobotArm" : case "StackingRobotArm" : key = "DumbRobotArm"; break;
            }
            if ( byType.TryGetValue( key, out PowerLog log ) ) log.power += partPower.power;
            else byType.Add( key, new PowerLog { power = partPower.power } );
            total += partPower.power;
         }
         Info( "Found {0} parts in {1} groups totaling {2}", allParts.Count, byType.Count, Wh( total ) );
         __result += $"\n\nTop {Math.Min(allParts.Count,config.power_log_rows)} power using equipment groups:";
         __result += "\n" + string.Join( "\n", byType.OrderBy( e => e.Value.power ).Reverse().Take( config.power_log_rows ).Select( e =>
            $"{AutomachefResources.KitchenParts.CreateNewInstance(e.Key).partName} ... {Wh(e.Value.power,false)} ({e.Value.power*100/total:0.0}%)" ) );
         __result = __result.Trim();
         Fine( __result );
      } catch ( Ex x ) { Err( x ); } }
      #endregion

      private static void HideTutorialMaxEfficiency ( List<Level> ___levelsList, Dictionary<string, GameObject> ___levelObjectMapping ) { try {
         foreach ( var level in ___levelsList.Where( IsTutorial ) ) {
            var text = ___levelObjectMapping[ level.number ].GetComponent<LevelSelectionItem>()?.maxEfficiency;
            if ( text?.gameObject.activeSelf != true ) return;
            text.text = ScriptLocalization.Main_UI.Success;
            Fine( $"Hiding efficiency of tutorial level {0}", level.number );
         }
      } catch ( Ex x ) { Err( x ); } }

      private static void HideTutorialEfficiencyStat ( Text ___statsEfficiencyValueUI ) { try {
         if ( IsTutorial() ) ___statsEfficiencyValueUI.text = "n/a";
      } catch ( Ex x ) { Err( x ); } }

      #region Freshness
      private static bool simpleTooltip;
      private static void SuppressFreshnessTooltip () => simpleTooltip = true;
      private static void RestoreFreshnessTooltip  () => simpleTooltip = false;

      private static void AppendFreshnessTooltip ( Ingredient __instance, ref string __result ) { try {
            Initializer init = Initializer.GetInstance();
         if ( simpleTooltip ) { __result = __instance.GetFriendlyNameTranslated(); return; }
         if ( ! init.IsSimRunning() || __instance.HasGoneBad() || __instance.name.StartsWith( "Insects" ) ) return;
         if ( init.levelManager.GetLevel().hasInsectsDisaster && __instance.GetInsectTime() > 0 ) {
            var part = __instance.currentNode?.kitchenPart;
            if ( part != null ) {
               if ( ! init.levelManager.PartsWithInsects.Contains( part ) ) {
                  float timer = __instance.GetInsectTime(), spawn = part.GetComponent< InsectsSpawner >()?.stationaryTimeBeforeSpawning ?? 0;
                  if ( timer != 0 && spawn != 0 ) __result += $"\nGreen for {spawn-timer:0.0}s";
               } else
                  __result += "\n(Insects nearby)";
            }
         }
         if ( __instance is Dish dish ) {
            float spoil = dish.timeToBeSpoiled - __instance.GetAge();
            if ( spoil > 0 ) __result += $"\nFresh for {spoil:0.0}s";
         }
      } catch ( Ex x ) { Err( x ); } }
      #endregion

      #region Efficiency Log
      private static Dictionary< object, int > orderedDish, cookedDish;
      private static void ClearEfficiencyLog () { orderedDish.Clear(); cookedDish.Clear(); }
      private static void TrackOrdersEfficiency ( Dish dish ) => EfficiencyMeter_Dish_Log( orderedDish, dish );
      private static void TrackDeliveryEfficiency ( Dish dish ) => EfficiencyMeter_Dish_Log( cookedDish, dish );
      private static void EfficiencyMeter_Dish_Log ( Dictionary< object, int > log, Dish dish ) {
         log.TryGetValue( dish, out int i );
         log[ dish ] = i + 1;
      }

      private static List<string> extraLog;

      private static void CalculateEfficiency ( bool allGoalsFulfilled, int __result, int ___expectedIngredientsUsage, int ___expectedPowerUsage ) { try {
         float iUsed = IngredientsCounter.GetInstance().GetUsedIngredients(), pUsed = PowerMeter.GetInstance().GetWattsHour();
         float iMark = Mathf.Clamp01( ___expectedIngredientsUsage / iUsed ), pMark = Mathf.Clamp01( ___expectedPowerUsage / pUsed );
         float mark = ( iMark + pMark ) / 2f;
         extraLog.Clear();
         extraLog.Add( $"Ingredients Quota {___expectedIngredientsUsage} / {iUsed} Spent = {iMark:0.00}" );
         extraLog.Add( $"Power Quota {___expectedPowerUsage}Wh / {pUsed}Wh Spent = {pMark:0.00}" );
         extraLog.Add( $"( Average {mark:0.00}" + ( allGoalsFulfilled ? "" : " - 0.1 goal failed" ) + $" )² = Final {__result/100f:0.00}" );
      } catch ( Ex x ) { Err( x ); } }

      // Show modded logs even when kitchen has no events
      private static void ForceShowEfficiencyLog ( LevelStatus __instance, KitchenEventsLog log ) { try {
         if ( log.GetEventsCount() <= 0 && ( ! config.hide_tutorial_efficiency || ! IsTutorial() ) )
            __instance.eventsLogTextField.text = log.ToString();
      } catch ( Ex x ) { Err( x ); } }

      private static void AppendEfficiencyLog ( ref string __result ) { try {
         if ( extraLog.Count == 0 || ( config.hide_tutorial_efficiency && IsTutorial() ) ) return;
         Info( "Appending efficiency log ({0} lines) to kitchen log.", extraLog.Count + orderedDish?.Count );
         __result += "\n\n";
         if ( orderedDish?.Count > 0 ) {
            __result += "Delivered / Ordered Dish ... Quota Contribution\n";
            foreach ( var key in orderedDish.Keys ) {
               var dish = key as Dish;
               cookedDish.TryGetValue( dish, out int delivered );
               int eI = dish.expectedIngredients, eP = dish.expectedPower, ordered = orderedDish[ dish ], missed = ordered - delivered;
               int missedPowerPenalty = ( dish.expectedPower - dish.expectedPower / 3 ) * missed;
               //__result += $"Quota\\{dish.friendlyName} = {eI} mats & {eP}Wh each\n";
               //__result += $"  {ordered-missed}/{ordered} done = {eI*ordered-missed} mats & {eP*ordered-missedPowerPenalty}Wh\n";
               __result += $"{ordered-missed}/{ordered} {dish.friendlyName} ... {eI*ordered-missed} mats & {eP*ordered-missedPowerPenalty}Wh\n";
            }
         }
         __result += "\n" + string.Join( "\n", extraLog.ToArray() );
         __result = __result.Trim();
         Fine( __result );
      } catch ( Ex x ) { Err( x ); } }

      private static string Wh ( float wh, bool prefix = true ) {
         var power = CachedData.fixedDeltaTime * wh / 3600f;
         var unit = "Wh";
         if ( prefix && power >= 1000 ) { power /= 1000f; unit = "kWh"; }
         if ( prefix && power >= 1000 ) { power /= 1000f; unit = "MWh"; }
         return prefix ? $"{power:0.00}{unit}" : $"{power:0}Wh";
      }
      #endregion

      private static bool SuppressConfirmation ( string bodyText, Action onAffirmativeButtonClicked, Action onDismissiveButtonClicked ) { try {
         foreach ( var msg in new string[] { About_To_Load_Game, Bonus_Level, Delete_Blueprint_Confirmation, Delete_Game,
                  Overwrite_Game, Overwrite_Blueprint, Quit_Confirmation, Quit_Confirmation_In_Game, Reset_Kitchen } )
            if ( bodyText == msg ) {
               Info( "Assuming yes for {0}", bodyText );
               onAffirmativeButtonClicked();
               return false;
            }
         if ( bodyText == Save_Before_Quitting ) {
            Info( "Assuming no for {0}", bodyText );
            onDismissiveButtonClicked();
            return false;
         }
         return true;
      } catch ( Ex x ) { return Err( x, true ); } }

      private static void InstantGameSpeedUpdate ( float ___targetTimeScale ) { try {
         if ( Time.timeScale != ___targetTimeScale ) Time.timeScale = ___targetTimeScale;
      } catch ( Ex x ) { Err( x ); } }

      private static void AdjustGameSpeedPresets ( Initializer __instance ) { try {
         if ( __instance == null || __instance.speeds == null || __instance.speeds.Count < 4 ) return;
         Info( "Setting game speeds to [ {0}x, {1}x, {2}x, {3}x ]", __instance.speeds[0], __instance.speeds[1], __instance.speeds[2], __instance.speeds[3] );
         __instance.speeds[2] = config.speed2;
         __instance.speeds[3] = config.speed3;
      } catch ( Ex x ) { Err( x ); } }

      #region Packaging Machine and Food Processor
      private static void SetIdlePower ( KitchenPart part, string name, bool isBusy, ref float fullPower, float idlePower ) { try {
         if ( fullPower == 0 ) {
            fullPower = part.powerInWatts;
            Info( "{0} power: {1}W when idle, {2}W when busy.", name, idlePower, fullPower );
         }
         part.powerInWatts = isBusy ? fullPower : idlePower;
      } catch ( Ex x ) { Err( x ); } }

      private static float fullFPpower, fullPMpower;
      private static void SetFoodProcessorPower ( Processor __instance, float ___processingTime ) =>
         SetIdlePower( __instance, "Food Processor", ___processingTime > 0, ref fullFPpower, config.food_processor_idle_power );
      private static void SetPackagingMachinePower ( PackagingMachine __instance, bool ___packaging ) =>
         SetIdlePower( __instance, "Packaging Machine", ___packaging, ref fullPMpower, config.packaging_machine_idle_power );

      private static void PackagingMachinePassThrough ( PackagingMachine __instance, List<Ingredient> ___ingredientsInside, KitchenPart.NodeData[] ___ourIngredientNodes ) { try {
         KitchenPart.NodeData exitNode = ___ourIngredientNodes[ 3 ]; // 3 is hardcoded in the game.
         if ( exitNode.ingredientExists ) return;
         PartStatus status = __instance.GetPartStatus();
         if ( status == PartStatus.Off || status == PartStatus.Working ) return;
         foreach ( var i in ___ingredientsInside ) {
            var id = i.internalName;
            foreach ( var d in __instance.dishesToAssemble )
               if ( Dish.GetByInternalName( d ).recipe.Contains( id ) ) return;
            Fine( "Packaging Machine is passing through {0}", i.GetFriendlyNameTranslated() );
            ___ingredientsInside.Remove( i );
            i.SetPosition( exitNode.position );
            __instance.TransferToNode( i, exitNode );
            return;
         }
      } catch ( Ex x ) { Err( x ); } }

      private static MethodInfo packMachineCanMake, packMachineConsume, packMachinePackage;
      private static System.Random packMachineRandom;
      private static ConditionalWeakTable< PackagingMachine, Dish > packMachineLastDish;

      private static void ClearPackagingMachineLastDish ( PackagingMachine __instance ) => packMachineLastDish?.Remove( __instance );
      private static void LogPackagingMachineLastDish ( PackagingMachine __instance, Dish dishToPrepare ) { try {
         packMachineLastDish.Remove( __instance );
         packMachineLastDish.Add( __instance, dishToPrepare );
      } catch ( Ex x ) { Err( x ); } }

      private static bool OverridePackagingMachineLogic ( PackagingMachine __instance, List<Ingredient> ___ingredientsInside ) { try {
         if ( ( ___ingredientsInside?.Count ?? 0 ) == 0 ) return false;
         HashSet<Dish> canMake = new HashSet<Dish>( __instance.dishesToAssemble.Select( Dish.GetByInternalName )
            .Where( e => (bool) packMachineCanMake.Invoke( __instance, new object[]{ e } ) ) );
         if ( canMake.Count == 0 ) return false;
         if ( canMake.Count > 1 ) {
            Info( "Packaging options: " + string.Join( ", ", canMake.Select( e => e.GetFriendlyNameTranslated() ) ) );
            foreach ( var dishA in canMake.ToArray() ) foreach ( var dishB in canMake ) {
               if ( dishA == dishB || dishA.recipe.Count >= dishB.recipe.Count ) continue;
               if ( dishA.recipe.Any( i => ! dishB.recipe.Contains( i ) ) ) continue;
               Fine( "Delisting {0} as a sub-recipe of {1}", dishA.GetFriendlyNameTranslated(), dishB.GetFriendlyNameTranslated() );
               canMake.Remove( dishA );
               break;
            }
            if ( canMake.Count > 1 && packMachineLastDish.TryGetValue( __instance, out Dish lastDish ) && canMake.Contains( lastDish ) ) {
               Fine( "Delisting last dish {0}", lastDish.GetFriendlyNameTranslated() );
               canMake.Remove( lastDish );
            }
            Info( canMake.Count > 1 ? "Randomly pick from reminders" : $"Winner: {canMake.ElementAt(0).GetFriendlyNameTranslated()}" );
         }
         var dish = canMake.ElementAt( canMake.Count == 1 ? 0 : packMachineRandom.Next( canMake.Count ) );
         if ( (bool) packMachineConsume.Invoke( __instance, new object[] { dish } ) ) packMachinePackage.Invoke( __instance, new object[] { dish } );
         return false;
      } catch ( Ex x ) { return Err( x, true ); } }
      #endregion

      #region Csv dump
      private static bool foodDumped, hardwareDumped, textDumped;
      private static readonly StringBuilder line = new StringBuilder();

      private static void DumpFoodCsv () { if ( foodDumped ) return; try {
         string file = Path.Combine( ZySimpleMod.AppDataDir, "foods.csv" );
         Info( "Exporting food list to {0}", file, foodDumped = true );
         using ( TextWriter f = File.CreateText( file ) ) {
            f.Csv( "Id", "Name", "Translated", "Process", "Seconds", "Recipe", "Liquids",
                   "Processed", "Grilled", "Fried", "Steamed", "Baked", "Wet", //"Bacterias",
                   "Spoil (sec)", "Ingredients Quota", "Power Quota" );
            foreach ( var mat in Ingredient.GetAll().Union( Dish.GetAll() ) ) {
               Fine( $"#{mat.internalName} = {mat.friendlyName}" );
               float spoil = 0, iQ = 0, pQ = 0;
               if ( mat is Dish dish ) { spoil = dish.timeToBeSpoiled;  iQ = dish.expectedIngredients;  pQ = dish.expectedPower; }
               f.Csv( mat.internalName, mat.friendlyName, mat.GetFriendlyNameTranslated(), mat.technique.ToString(), mat.timeToBeAssembled + "",
                  mat.recipe == null ? "" : string.Join( " + ", mat.recipe ),
                  mat.recipeLiquidIngredients == null ? "" : string.Join( " + ", mat.recipeLiquidIngredients ),
                  mat.resultProcess, mat.resultGrill, mat.resultFry, mat.resultSteam, mat.resultBake, mat.resultWet,
                  //mat.bacteria == null ? "" : string.Join( " & ", mat.bacteria.Select( e => e.friendlyName ) ),
                  spoil == 0 ? "" : spoil.ToString(), iQ == 0 ? "" : iQ.ToString(), pQ == 0 ? "" : pQ.ToString()
                  );
            }
            f.Flush();
         }
         Info( "Food list exported" );
      } catch ( Ex x ) { Err( x ); } }

      private static void DumpHardwareCsv () { if ( hardwareDumped ) return; try {
         string file = Path.Combine( ZySimpleMod.AppDataDir, "hardwares.csv" );
         Info( "Exporting hardware list to {0}", file, hardwareDumped = true );
         using ( TextWriter f = File.CreateText( file ) ) {
            f.Csv( "Id", "Name", "Description", "Category", "Price", "Power", "Speed", "Time", "Variant", "Code Class" );
            foreach ( var part in AutomachefResources.KitchenParts.GetList_ReadOnly() ) {
               Fine( "#{0} = {1}", part.internalName, part.partName );
               var speed  = part.GetType().Field( "speed" )?.GetValue( part );
               var rspeed = part.GetType().Field( "rotationSpeed" )?.GetValue( part ) ??  part.GetType().Field( "armRotationSpeed" )?.GetValue( part );
               var pTime  = part.GetType().Field( "timeToProcess" )?.GetValue( part );
               f.Csv( part.internalName, part.partName, part.description, part.category, part.cost, part.powerInWatts, speed ?? rspeed ?? "", pTime ?? "",
                      part.nextVariantInternalName, part.GetType().FullName );
            }
            f.Flush();
         }
         Info( "Hardware list exported" );
      } catch ( Ex x ) { Err( x ); } }

      private static void DumpLanguageCsv ( List<LanguageSource> ___Sources ) { if ( textDumped || ___Sources == null ) return; try {
         for ( var i = 0 ; i < ___Sources.Count ; i++ ) {
            var file = Path.Combine( ZySimpleMod.AppDataDir, $"text-{i}.csv" );
            Info( "Exporting game text to {0}", file, textDumped = true );
            using ( var fw = File.CreateText( file ) ) fw.Write( ___Sources[ i ].Export_CSV( null ) );
         }
         Info( "{0} game text exported", ___Sources.Count );
      } catch ( Ex x ) { Err( x ); } }

      private static void Csv ( this TextWriter f, params object[] values ) {
         foreach ( var val in values ) {
            string v = val?.ToString() ?? "null";
            if ( v.Contains( "," ) || v.Contains( "\"" ) || v.Contains( "\n" ) ) line.Append( '"' ).Append( v.Replace( "\"", "\"\"" ) ).Append( "\"," );
            else line.Append( v ).Append( ',' );
         }
         --line.Length;
         f.Write( line.Append( "\r\n" ) );
         line.Length = 0;
      }
      #endregion

      #region Traditional Chinese.  Hooray for Taiwan, Hong Kong, Macau!
      private static void ShowZht ( List<string> ___languageNames ) { try {
         for ( var i = ___languageNames.Count - 1 ; i >= 0 ; i-- )
            if ( ___languageNames[ i ] == "简体中文" ) {
               ___languageNames[ i ] = "中文";
               return;
            }
      } catch ( Ex x ) { Err( x ); } }

      private static void DetectZh ( string code ) { try {
         Info( "Game language set to {0}", code );
         if ( code != "zh" ) return;
         zhs2zht = new Dictionary< string, string >();
         Modder.TryPatch( typeof( LanguageSource ), "TryGetTranslation", postfix: nameof( ToZht ) );
      } catch ( Ex x ) { Err( x ); } }

      private static Dictionary< string, string > zhs2zht;

      private static void ToZht ( string term, ref string Translation, bool __result ) { if ( ! __result ) return; try {
         if ( zhs2zht.TryGetValue( term, out string zht ) ) { Translation = zht; return; }
         zht = new String( ' ', Translation.Length );
         LCMapString( LOCALE_SYSTEM_DEFAULT, LCMAP_TRADITIONAL_CHINESE, Translation, Translation.Length, zht, zht.Length );
         Fine( "ZH {0} ({1} chars)", term, ( zht = FixZht( zht ) ).Length );
         zhs2zht.Add( term, Translation = zht );
      } catch ( Ex x ) { Err( x ); } }

      private static string FixZht ( string zht ) {
         zht = zht.Replace( "任務目標", "任務" ).Replace( "菜肴", "餐點" ).Replace( "美食評論家", "食評家" )
            .Replace( "已上餐點", "上菜" ).Replace( "電力消耗", "耗電" ).Replace( "使用的食材", "食材" );
         return zht;
      }

      [ DllImport( "kernel32", CharSet = CharSet.Unicode, SetLastError = true ) ]
      private static extern int LCMapString ( int Locale, int dwMapFlags, string lpSrcStr, int cchSrc, [Out] string lpDestStr, int cchDest );
      private const int LOCALE_SYSTEM_DEFAULT = 0x0800;
      private const int LCMAP_SIMPLIFIED_CHINESE = 0x02000000;
      private const int LCMAP_TRADITIONAL_CHINESE = 0x04000000;
      #endregion

      private static bool IsTutorial () => IsTutorial( Initializer.GetInstance().levelManager?.GetLevel() );
      private static bool IsTutorial ( Level lv ) => lv == null || lv.IsTutorial() || lv.IsOptionalTutorial();
   }
}