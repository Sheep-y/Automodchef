using I2.Loc;
using MaterialUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;
using UnityEngine.Analytics;
using ZyMod;
using static I2.Loc.ScriptLocalization.Warnings;

namespace Automodchef {

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

      [ ConfigAttribute( "System", "Skip Unity, Hermes, Team 17, and Autosave screens.  True or false.  Default true." ) ]
      public bool skip_intro = true;
      [ ConfigAttribute( "System", "Skip 'Press Spacebar to continue'.  True or false.  Default true." ) ]
      public bool skip_spacebar = true;
      [ ConfigAttribute( "System", "Disable mission stats analytics.  True or false.  Default true." ) ]
      public bool disable_analytics = true;

      [ ConfigAttribute( "Bug Fix", "Fix food mouseover hints not showing when game is paused." ) ]
      public bool fix_food_hint_when_paused = true;

      [ ConfigAttribute( "User Interface", "Suppress yes/no confirmations - save before quit, load game, delete or overwrite save / blueprint / scenario, quit level / game, reset layout" ) ]
      public bool suppress_confirmation = true;
      [ ConfigAttribute( "User Interface", "Show load game prompt when entering a level (if any saves).  Loading a game will bypass level goal popup and roboto speech.  True or false.  Default true." ) ]
      public bool ask_loadgame_on_level_start = true;
      [ ConfigAttribute( "User Interface", "Max number if options to convert dropdown to toggle button.  Default 3.  0 to disable." ) ]
      public byte dropdown_toogle_threshold = 3;
      [ ConfigAttribute( "User Interface", "Show real-time power usage in mouseover tooltips." ) ]
      public bool tooltip_power_usage = true;
      [ ConfigAttribute( "User Interface", "Show food freshness in mouseover tooltips." ) ]
      public bool tooltip_freshness = true;
      [ ConfigAttribute( "User Interface", "Add effiency calculation to kitchen log.  True or false.  Default true." ) ]
      public bool efficiency_log = true;
      [ ConfigAttribute( "User Interface", "Breakdown efficiency quotas by dishes.  True or false.  Default true." ) ]
      public bool efficiency_log_breakdown = true;
      [ ConfigAttribute( "User Interface", "Show top X power consuming part types in kitchen log." ) ]
      public byte power_log_rows = 5;

      [ ConfigAttribute( "Simulation", "Change game speed instantiously." ) ]
      public bool instant_speed_change = true;
      [ ConfigAttribute( "Simulation", "Speed of double time (two arrows).  0-100 integer.  Game default 3.  Mod default 5." ) ]
      public byte speed2 = 5;
      [ ConfigAttribute( "Simulation", "Speed of triple time (three arrows).  0-100 integer.  Game default 5.  Mod default 20.  High speed may cause some orders to expire when they would not on slower speeds." ) ]
      public byte speed3 = 20;

      [ ConfigAttribute( "Mechanic", "Packaging machine spend less power when not packaging.  Game default 800.  Mod default 60 (2x slowest belts)." ) ]
      public float packaging_machine_idle_power = 60;
      [ ConfigAttribute( "Mechanic", "Packaging machine's sub-recipes have lowest priority (Fries < Bacon Fries < Loaded Cheese Fries), last processed recipe have lower priority, and random for top ties." ) ]
      public bool smart_packaging_machine = true;

      [ ConfigAttribute( "Tools", "Export foods to foods.csv on game launch.  True or false.  Default false.  Neglectable impact, disabled only because most won't need these." ) ]
      public bool export_food_csv = false;
      [ ConfigAttribute( "Tools", "Export hardwares to hardwares.csv on game launch.  True or false.  Default false.  Ditto." ) ]
      public bool export_hardware_csv = false;

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
         ApplyMechanicPatches();
      }

      private static void ApplySystemPatches () { try {
         if ( config.skip_intro )
            Modder.TryPatch( typeof( FaderUIController ), "Awake", nameof( SkipVideoSplashes ) );
         if ( config.skip_spacebar )
            Modder.TryPatch( typeof( SplashScreen ), "Update", postfix: nameof( SkipSpacebarSplash ) );
         if ( config.disable_analytics ) {
            foreach ( var m in typeof( Analytics ).AllMethods().Where( e => e.Name == "CustomEvent" || e.Name == "Transaction" || e.Name.StartsWith( "Send" ) ) )
               Modder.TryPatch( m, nameof( DisableAnalytics ) );
            Modder.TryPatch( typeof( AutomachefAnalytics ), "Track", nameof( DisableAnalytics ) );
         }
         if ( config.fix_food_hint_when_paused )
            Modder.TryPatch( typeof( IngredientTooltip ), "Update", postfix: nameof( FixIngredientHintOnPause ) );
      } catch ( Exception ex ) { Err( ex ); } }

      private static void ApplyUserInterfacePatches () { try {
         if ( config.suppress_confirmation ) {
            var orig = typeof( DialogManager ).AllMethods( "ShowAlert" ).FirstOrDefault( e => e.GetParameters().Length == 7 );
            if ( orig != null ) Modder.TryPatch( orig, nameof( SuppressConfirmation ) );
         }
         if ( config.ask_loadgame_on_level_start ) {
            Modder.TryPatch( typeof( LevelManager ), "Start", postfix: nameof( SetNewLevelTrigger ) );
            Modder.TryPatch( typeof( SaveLoad ), "Close", nameof( RestorePreLevelScreen ) );
            Modder.TryPatch( typeof( SaveLoadManager ), "DeleteSave", nameof( ClearPreLevelFlag ) );
            Modder.TryPatch( typeof( SaveLoadManager ), "LoadAndBuildKitchen", nameof( ClearPreLevelFlag ) );
         }
         if ( config.dropdown_toogle_threshold > 0 ) {
            dropdownProp = new ConditionalWeakTable< MaterialDropdown, KitchenPartProperty >();
            dropdownIcon = new ConditionalWeakTable< MaterialDropdown, DropdownIcon >();
            Modder.TryPatch( typeof( PartProperties ), "PopulateDropdownForProperty", nameof( TrackDropdown ) );
            Modder.TryPatch( typeof( MaterialDropdown ), "ShowDropdown", nameof( ToggleDropdown ) );
         }
         if ( config.tooltip_power_usage || config.power_log_rows > 0 ) {
            Modder.TryPatch( typeof( KitchenPart ).AllMethods( "ConsumePower" ).FirstOrDefault( e => e.GetParameters().Length > 0 ), nameof( LogPowerUsage ) );
            Modder.TryPatch( typeof( PowerMeter ), "Reset", nameof( ClearPowerUsage ) );
            if ( config.tooltip_power_usage )
               Modder.TryPatch( typeof( KitchenPart ), "GetTooltipText", postfix: nameof( AppendPowerToTooltip ) );
         }
         if ( config.tooltip_freshness )
            Modder.TryPatch( typeof( Ingredient ), "GetTooltipText", postfix: nameof( AppendFreshnessToTooltip ) );
         if ( config.efficiency_log || config.power_log_rows > 0 ) {
            Modder.TryPatch( typeof( LevelStatus ), "RenderEvents", postfix: nameof( ForceShowEfficiencyLog ) );
            if ( config.efficiency_log ) {
               extraLog = new List<string>();
               if ( config.efficiency_log_breakdown )
                  if ( Modder.TryPatch( typeof( EfficiencyMeter ), "Reset", nameof( ClearEfficiencyLog ) ) ) {
                     orderedDish = new Dictionary<object, int>();
                     cookedDish = new Dictionary<object, int>();
                     Modder.TryPatch( typeof( EfficiencyMeter ), "AddOrder", nameof( TrackOrdersEfficiency ) );
                     Modder.TryPatch( typeof( EfficiencyMeter ), "AddDeliveredDish", nameof( TrackDeliveryEfficiency ) );
                  }
               if ( Modder.TryPatch( typeof( EfficiencyMeter ), "GetEfficiency", postfix: nameof( CalculateEfficiency ) ) ) {
                  Modder.TryPatch( typeof( LevelManager ), "DetermineLevelOutcome", nameof( SuppressEfficiencyLog ), nameof( ResumeEfficiencyLog ) );
                  Modder.TryPatch( typeof( KitchenEventsLog ), "ToString", postfix: nameof( AppendEfficiencyLog ) );
               }
            }
            if ( config.power_log_rows > 0 )
               Modder.TryPatch( typeof( KitchenEventsLog ), "ToString", postfix: nameof( AppendPowerLog ) );
         }
      } catch ( Exception ex ) { Err( ex ); } }

      private static void ApplyMechanicPatches () { try {
         if ( config.instant_speed_change )
            Modder.TryPatch( typeof( Initializer ), "Update", postfix: nameof( InstantGameSpeedUpdate ) );
         if ( config.speed2 != 3 || config.speed3 != 5 )
            Modder.TryPatch( typeof( Initializer ), "Start", nameof( AdjustGameSpeedPresets ) );
         if ( config.packaging_machine_idle_power != 800 && config.packaging_machine_idle_power >= 0 )
            Modder.TryPatch( typeof( PackagingMachine ), "FixedUpdate", nameof( SetPackagingMachinePower ) );
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
      } catch ( Exception ex ) { Err( ex ); } }

      #region Skip Splash
      private static void SkipVideoSplashes ( ref FaderUIController.SplashStateInfo[] ___m_SplashStates ) { try {
         if ( ___m_SplashStates == null || ___m_SplashStates.Length <= 1 || ___m_SplashStates[0].m_AnimToPlay != "Unity" ) return;
         if ( ! ___m_SplashStates.Any( e => e.m_AnimToPlay == "LoadStartScreen" ) ) return;
         ___m_SplashStates = ___m_SplashStates.Where( e => e.m_AnimToPlay == "LoadStartScreen" ).ToArray();
         ___m_SplashStates[0].m_TimeInState = 0.01f;
         Log.Info( $"Skipping Logos and Warnings." );
         // [ { "Unity, 1, False }, { "HermesInteractive, 2, False }, { "Team 17, 4, True }, { "Legal, 3, False }, { "LoadStartScreen", 2, False } ]
      } catch ( Exception ex ) { Err( ex ); } }

      private static void SkipSpacebarSplash ( SplashScreen __instance, ref bool ___m_bProcessedCloseRequest ) { try {
         if ( ___m_bProcessedCloseRequest || InputWrapper.GetController() == null ) return;
         ___m_bProcessedCloseRequest = true;
         Modder.TryMethod( typeof( SplashScreen ), "ProceedToMainMenu" ).Invoke( __instance, Array.Empty<object>() );
         Log.Info( "Skipped Press Space Splash." );
      } catch ( Exception ex ) {
         Log.Error( ex );
         ___m_bProcessedCloseRequest = false;
      } }
      #endregion

      private static bool DisableAnalytics () { Log.Info( "Analytics Blocked" ); return false; }

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
      } catch ( Exception ex ) { Err( ex ); } }

      #region Pre-level load dialogue
      private static LevelManager currentLevel;
      private static bool lastPreLevelScreenState;

      private static void SetNewLevelTrigger ( LevelManager __instance, SaveLoadManager ___m_saveLoadManager ) { try {
         if ( __instance.GetLevel().IsTutorial() ) return;
         Log.Fine( "Entering new non-tutorial level." );
         currentLevel = __instance;
         ___m_saveLoadManager.OnMetadataReady += OfferToLoadGameOnEnter;
      } catch ( Exception ex ) { Err( ex ); } }

      private static void OfferToLoadGameOnEnter () { try {
         Log.Fine( "New level data loaded." );
         var data = SaveLoadManager.GetInstance().getSavedKitchenData();
         if ( data == null ) { Log.Info( "Save data not found, aborting." ); return; }
		 for ( var i = 0; i < 5; i++ )
			if ( data.Get( i ) != null ) {
               Log.Info( $"Found saved level at slot {i+1}, offering load dialog." );
               lastPreLevelScreenState = currentLevel.levelStatusUI.preLevelInfoRoot.gameObject.activeSelf;
               currentLevel.levelStatusUI.preLevelScreen.gameObject.SetActive( false );
               Initializer.GetInstance().saveLoadPanel.Open( false );
               return;
            }
         Log.Info( "No saved level found.  Skipping load dialog." );
      } catch ( Exception ex ) { Err( ex ); } }

      private static void RestorePreLevelScreen () { try {
         if ( ! lastPreLevelScreenState ) return;
         Log.Fine( "Restoring level brief screen." );
         lastPreLevelScreenState = false;
         currentLevel.levelStatusUI.preLevelScreen.gameObject.SetActive( true );
      } catch ( Exception ex ) { Log.Error( ex ); } }

      private static void ClearPreLevelFlag () {
         Log.Fine( "Save loaded or deleted.  Suppressing pre-level screen." );
         lastPreLevelScreenState = false;
         currentLevel?.levelStatusUI.preLevelScreen.gameObject.SetActive( false );
      }
      #endregion

      #region Dropdown Toggle
      private static ConditionalWeakTable< MaterialDropdown, KitchenPartProperty > dropdownProp;
      private static ConditionalWeakTable< MaterialDropdown, DropdownIcon > dropdownIcon;

      private static void TrackDropdown ( MaterialDropdown dropdown, KitchenPartProperty prop, DropdownIcon icon ) { try {
         dropdownProp.Remove( dropdown ); dropdownProp.Add( dropdown, prop );
         dropdownIcon.Remove( dropdown ); dropdownIcon.Add( dropdown, icon );
      } catch ( Exception ex ) { Err( ex ); } }

      private static bool ToggleDropdown ( MaterialDropdown __instance, ref int ___m_CurrentlySelected ) { try {
         if ( ! dropdownProp.TryGetValue( __instance, out KitchenPartProperty prop ) ) return true;
         if ( ( prop.friendlyValues?.Count ?? 0 ) > 3 ) return true;
         if ( Initializer.GetInstance().levelManager.GetLevel().IsTutorial() ) return true;
         var new_selection = ___m_CurrentlySelected + 1;
         if ( new_selection >= prop.friendlyValues.Count ) new_selection = 0;
         __instance.Select( new_selection );
         if ( dropdownIcon.TryGetValue( __instance, out DropdownIcon icon ) ) icon.UpdateIcon();
         return false;
      } catch ( Exception ex ) { Err( ex ); return true; } }
      #endregion

      #region Power
      private class PowerLog { internal float power; }
      private static ConditionalWeakTable< KitchenPart, PowerLog > powerLog;

      private static void LogPowerUsage ( KitchenPart __instance, float multiplier ) { try {
         if ( ! Initializer.GetInstance().IsSimRunning() || powerLog == null ) return;
         powerLog.GetOrCreateValue( __instance ).power += __instance.powerInWatts * multiplier;
      } catch ( Exception ex ) { Err( ex ); } }

      private static void ClearPowerUsage () {
         Log.Fine( "Reset power log" );
         powerLog = new ConditionalWeakTable<KitchenPart, PowerLog>();
      }

      private static void AppendPowerToTooltip ( KitchenPart __instance, ref string __result ) { try {
         if ( ! Initializer.GetInstance().IsSimRunning() || __instance.powerInWatts <= 0 || powerLog == null ) return;
         powerLog.TryGetValue( __instance, out PowerLog log );
         __result += $"\n{PowerMeter.GetInstance().GetLastPowerUsage( __instance )}W >> {Wh(log?.power??0)}";
      } catch ( Exception ex ) { Err( ex ); } }

      private static void AppendPowerLog ( ref string __result ) { try {
         if ( powerLog == null ) return;
         Log.Info( $"Appending power log (up to {config.power_log_rows} lines) to kitchen log." );
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
         Log.Info( $"Found {allParts.Count} parts in {byType.Count} groups totaling {Wh(total)}" );
         __result += $"\n\nTop {Math.Min(allParts.Count,config.power_log_rows)} power using equipment groups:";
         __result += "\n" + string.Join( "\n", byType.OrderBy( e => e.Value.power ).Reverse().Take( config.power_log_rows ).Select( e =>
            $"{AutomachefResources.KitchenParts.CreateNewInstance(e.Key).partName} ... {Wh(e.Value.power,false)} ({e.Value.power*100/total:0.0}%)" ) );
         __result = __result.Trim();
         Log.Fine( __result );
      } catch ( Exception ex ) { Err( ex ); } }
      #endregion

      private static void AppendFreshnessToTooltip ( Ingredient __instance, ref string __result ) { try {
            Initializer init = Initializer.GetInstance();
            if ( !init.IsSimRunning() || __instance.HasGoneBad() || __instance.name.StartsWith( "Insects" ) ) return;
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
      } catch ( Exception ex ) { Err( ex ); } }

      #region Efficiency Log
      private static Dictionary< object, int > orderedDish, cookedDish;
      private static void ClearEfficiencyLog () { orderedDish.Clear(); cookedDish.Clear(); }
      private static void TrackOrdersEfficiency ( Dish dish ) => EfficiencyMeter_Dish_Log( orderedDish, dish );
      private static void TrackDeliveryEfficiency ( Dish dish ) => EfficiencyMeter_Dish_Log( cookedDish, dish );
      private static void EfficiencyMeter_Dish_Log ( Dictionary< object, int > log, Dish dish ) {
         log.TryGetValue( dish, out int i );
         log[ dish ] = i + 1;
      }

      private static int outcome;
      private static List<string> extraLog;
      private static bool ShowEfficiencyLog => outcome != (int) LevelOutcome.Failure && outcome != (int) LevelOutcome.InProgress;

      private static void SuppressEfficiencyLog () => outcome = (int) LevelOutcome.InProgress;
      private static void ResumeEfficiencyLog ( LevelOutcome __result ) => outcome = (int) __result;
      private static void CalculateEfficiency ( bool allGoalsFulfilled, int __result, int ___expectedIngredientsUsage, int ___expectedPowerUsage ) { try {
         if ( ! ShowEfficiencyLog ) return;
         float iUsed = IngredientsCounter.GetInstance().GetUsedIngredients(), pUsed = PowerMeter.GetInstance().GetWattsHour();
         float iMark = Mathf.Clamp01( ___expectedIngredientsUsage / iUsed ), pMark = Mathf.Clamp01( ___expectedPowerUsage / pUsed );
         float mark = ( iMark + pMark ) / 2f;
         extraLog.Clear();
         extraLog.Add( $"Ingredients Quota {___expectedIngredientsUsage} / {iUsed} Spent = {iMark:0.00}" );
         extraLog.Add( $"Power Quota {___expectedPowerUsage}Wh / {pUsed}Wh Spent = {pMark:0.00}" );
         extraLog.Add( $"( Average {mark:0.00}" + ( allGoalsFulfilled ? "" : " - 0.1 goal failed" ) + $" )² = Final {__result/100f:0.00}" );
      } catch ( Exception ex ) { Err( ex ); } }

      // Show modded logs even when kitchen has no events
      private static void ForceShowEfficiencyLog ( LevelStatus __instance, KitchenEventsLog log ) { try {
         if ( ( ShowEfficiencyLog && log.GetEventsCount() <= 0 )  || config.power_log_rows > 0 ) __instance.eventsLogTextField.text = log.ToString();
      } catch ( Exception ex ) { Err( ex ); } }

      private static void AppendEfficiencyLog ( ref string __result ) { try {
         if ( ! ShowEfficiencyLog || extraLog.Count == 0 ) return;
         Log.Info( $"Appending efficiency log ({extraLog.Count+orderedDish.Count} lines) to kitchen log." );
         __result += "\n\n";
         if ( orderedDish.Count > 0 ) __result += "Delivered / Ordered Dish ... Quota Contribution\n";
         foreach ( var key in orderedDish.Keys ) {
            var dish = key as Dish;
            cookedDish.TryGetValue( dish, out int delivered );
            int eI = dish.expectedIngredients, eP = dish.expectedPower, ordered = orderedDish[ dish ], missed = ordered - delivered;
            int missedPowerPenalty = ( dish.expectedPower - dish.expectedPower / 3 ) * missed;
            //__result += $"Quota\\{dish.friendlyName} = {eI} mats & {eP}Wh each\n";
            //__result += $"  {ordered-missed}/{ordered} done = {eI*ordered-missed} mats & {eP*ordered-missedPowerPenalty}Wh\n";
            __result += $"{ordered-missed}/{ordered} {dish.friendlyName} ... {eI*ordered-missed} mats & {eP*ordered-missedPowerPenalty}Wh\n";
         }
         __result += "\n" + string.Join( "\n", extraLog.ToArray() );
         __result = __result.Trim();
         Log.Fine( __result );
      } catch ( Exception ex ) { Err( ex ); } }
      #endregion

      private static bool SuppressConfirmation ( string bodyText, Action onAffirmativeButtonClicked, Action onDismissiveButtonClicked ) { try {
         foreach ( var msg in new string[] { About_To_Load_Game, Bonus_Level, Delete_Blueprint_Confirmation, Delete_Game,
                  Overwrite_Game, Overwrite_Blueprint, Quit_Confirmation, Quit_Confirmation_In_Game, Reset_Kitchen } )
            if ( bodyText == msg ) {
               Log.Info( $"Assuming yes for {bodyText}" );
               onAffirmativeButtonClicked();
               return false;
            }
         if ( bodyText == Save_Before_Quitting ) {
            Log.Info( $"Assuming no for {bodyText}" );
            onDismissiveButtonClicked();
            return false;
         }
         return true;
      } catch ( Exception ex ) { return Err( ex, true ); } }

      private static void InstantGameSpeedUpdate ( float ___targetTimeScale ) { try {
         if ( Time.timeScale != ___targetTimeScale ) Time.timeScale = ___targetTimeScale;
      } catch ( Exception ex ) { Err( ex ); } }

      private static void AdjustGameSpeedPresets ( Initializer __instance ) { try {
         if ( __instance == null || __instance.speeds == null || __instance.speeds.Count < 4 ) return;
         Log.Info( $"Setting game speeds to [ {__instance.speeds[0]}x, {__instance.speeds[1]}x, {config.speed2}x, {config.speed3}x ]" );
         __instance.speeds[2] = config.speed2;
         __instance.speeds[3] = config.speed3;
      } catch ( Exception ex ) { Err( ex ); } }

      #region Packaging Machine mechanics
      private static float fullPMpower;
      private static void SetPackagingMachinePower ( PackagingMachine __instance, bool ___packaging ) { try {
         if ( fullPMpower == 0 ) {
            fullPMpower = __instance.powerInWatts;
            Log.Info( $"Packaging machine power: {config.packaging_machine_idle_power}W when idle, {fullPMpower}W when packaging." );
         }
         __instance.powerInWatts = ___packaging ? fullPMpower : config.packaging_machine_idle_power;
      } catch ( Exception ex ) { Err( ex ); } }

      private static MethodInfo packMachineCanMake, packMachineConsume, packMachinePackage;
      private static System.Random packMachineRandom;
      private static ConditionalWeakTable< PackagingMachine, Dish > packMachineLastDish;

      private static void ClearPackagingMachineLastDish ( PackagingMachine __instance ) => packMachineLastDish?.Remove( __instance );
      private static void LogPackagingMachineLastDish ( PackagingMachine __instance, Dish dishToPrepare ) { try {
         packMachineLastDish.Remove( __instance );
         packMachineLastDish.Add( __instance, dishToPrepare );
      } catch ( Exception ex ) { Err( ex ); } }

      private static bool OverridePackagingMachineLogic ( PackagingMachine __instance, List<Ingredient> ___ingredientsInside ) { try {
         if ( ___ingredientsInside.Count == 0 ) return false;
         HashSet<Dish> canMake = new HashSet<Dish>( __instance.dishesToAssemble.Select( e => Dish.GetByInternalNameHash( e.GetStableHashCode() ) ).Where( e =>
            (bool) packMachineCanMake.Invoke( __instance, new object[]{ e } ) ) );
         if ( canMake.Count == 0 ) return false;
         if ( canMake.Count > 1 ) Log.Info( "Packaging options: " + string.Join( ", ", canMake.Select( e => e.GetFriendlyNameTranslated() ) ) );
         foreach ( var dishA in canMake.ToArray() ) foreach ( var dishB in canMake ) {
            if ( dishA == dishB || dishA.recipe.Count >= dishB.recipe.Count ) continue;
            if ( dishA.recipe.Any( i => ! dishB.recipe.Contains( i ) ) ) continue;
            Log.Fine( $"Delisting {dishA.GetFriendlyNameTranslated()} as a sub-recipe of {dishB.GetFriendlyNameTranslated()}" );
            canMake.Remove( dishA );
            break;
         }
         if ( canMake.Count > 1 && packMachineLastDish.TryGetValue( __instance, out Dish lastDish ) && canMake.Contains( lastDish ) ) {
            Log.Fine( $"Delisting last dish {lastDish.GetFriendlyNameTranslated()}" );
            canMake.Remove( lastDish );
         }
         if ( canMake.Count > 1 ) Log.Fine( "Randomly pick one" );
         var dish = canMake.ElementAt( canMake.Count == 1 ? 9 : packMachineRandom.Next( canMake.Count ) );
         if ( (bool) packMachineConsume.Invoke( __instance, new object[] { dish } ) ) {
            packMachinePackage.Invoke( __instance, new object[] { dish } );
            Log.Info( $"Winner: {dish.GetFriendlyNameTranslated()}" );
         }
         return false;
      } catch ( Exception ex ) { return Err( ex, true ); } }
      #endregion

      #region Csv dump
      private static bool foodDumped, hardwareDumped;
      private static readonly StringBuilder line = new StringBuilder();

      private static void DumpFoodCsv () { try {
         if ( foodDumped ) return;
         string file = Path.Combine( ZySimpleMod.AppDataDir, "foods.csv" );
         Log.Info( $"Exporting food list to {file}" );
         using ( TextWriter f = File.CreateText( file ) ) {
            f.Csv( "Id", "Name", "Translated", "Process", "Seconds", "Recipe", "Liquids",
                   "Processed", "Grilled", "Fried", "Steamed", "Baked", "Wet", "Baterias",
                   "Spoil", "Ingredients Quota", "Power Quota" );
            foreach ( var mat in Ingredient.GetAll().Union( Dish.GetAll() ) ) {
               Log.Fine( $"#{mat.internalName} = {mat.friendlyName}" );
               float spoil = 0, iQ = 0, pQ = 0;
               if ( mat is Dish dish ) { spoil = dish.timeToBeSpoiled;  iQ = dish.expectedIngredients;  pQ = dish.expectedPower; }
               f.Csv( mat.internalName, mat.friendlyName, mat.GetFriendlyNameTranslated(), mat.technique.ToString(), mat.timeToBeAssembled + "",
                  mat.recipe == null ? "" : string.Join( " + ", mat.recipe ),
                  mat.recipeLiquidIngredients == null ? "" : string.Join( " + ", mat.recipeLiquidIngredients ),
                  mat.resultProcess, mat.resultGrill, mat.resultFry, mat.resultSteam, mat.resultBake, mat.resultWet,
                  mat.bacteria == null ? "" : string.Join( " & ", mat.bacteria.Select( e => e.friendlyName ) ),
                  spoil == 0 ? "" : spoil.ToString(), iQ == 0 ? "" : iQ.ToString(), pQ == 0 ? "" : pQ.ToString()
                  );
            }
            f.Flush();
         }
         foodDumped = true;
         Log.Info( "Food list exported" );
      } catch ( Exception ex ) { Err( ex ); } }

      private static void DumpHardwareCsv () { try {
         if ( hardwareDumped ) return;
         string file = Path.Combine( ZySimpleMod.AppDataDir, "hardwares.csv" );
         Log.Info( $"Exporting hardware list to {file}" );
         using ( TextWriter f = File.CreateText( file ) ) {
            f.Csv( "Id", "Name", "Description", "Category", "Price", "Power", "Reliability", "Variant", "Code Class" );
            foreach ( var part in AutomachefResources.KitchenParts.GetList_ReadOnly() ) {
               Log.Fine( $"#{part.internalName} = {part.partName}" );
               f.Csv( part.internalName, part.partName, part.description, part.category, part.cost, part.powerInWatts, part.reliabilityPercentage,
                  part.nextVariantInternalName, part.GetType().FullName );
            }
            f.Flush();
         }
         hardwareDumped = true;
         Log.Info( "Hardware list exported" );
      } catch ( Exception ex ) { Err( ex ); } }

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


      private static void Err ( object msg ) => Log.Error( msg );
      private static T Err < T > ( object msg, T val ) { Log.Error( msg ); return val; }

      private static string Wh ( float wh, bool prefix = true ) {
         var power = CachedData.fixedDeltaTime * wh / 3600f;
         var unit = "Wh";
         if ( prefix && power >= 1000 ) { power /= 1000f; unit = "kWh"; }
         if ( prefix && power >= 1000 ) { power /= 1000f; unit = "MWh"; }
         return prefix ? $"{power:0.00}{unit}" : $"{power:0}Wh";
      }
   }
}
