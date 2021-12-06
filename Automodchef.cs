using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using ZyMod;
using System.Text;
using MaterialUI;
using System.Runtime.CompilerServices;

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
      [ ConfigAttribute( "Version of this Automodchef config file.  Don't touch!" ) ]
      public int config_version = 20211206;

      [ ConfigAttribute( "System", "Skip Unity, Hermes, Team 17, and Autosave screens.  True or false.  Default true." ) ]
      public bool skip_intro = true;
      [ ConfigAttribute( "System", "Skip 'Press Spacebar to continue'.  True or false.  Default true." ) ]
      public bool skip_spacebar = true;
      [ ConfigAttribute( "System", "Disable mission stats analytics.  True or false.  Default true." ) ]
      public bool disable_analytics = true;

      [ ConfigAttribute( "User Interface", "Show load game prompt when entering a level (if any saves).  Loading a game will bypass level goal popup and roboto speech.  True or false.  Default true." ) ]
      public bool load_prompt_on_enter = true;
      [ ConfigAttribute( "User Interface", "Speed of double time (two arrows).  0-100 integer.  Game default 3.  Mod default 5." ) ]
      public byte speed2 = 5;
      [ ConfigAttribute( "User Interface", "Speed of triple time (three arrows).  0-100 integer.  Game default 5.  Mod default 20." ) ]
      public byte speed3 = 20;
      [ ConfigAttribute( "User Interface", "Max number if options to convert dropdown to toggle button.  Default 3.  0 to disable." ) ]
      public byte dropdown_toogle_threshold = 3;
      [ ConfigAttribute( "User Interface", "Add effiency calculation to kitchen log.  True or false.  Default true." ) ]
      public bool efficiency_log = true;
      [ ConfigAttribute( "User Interface", "Breakdown efficiency quotas by dishes.  True or false.  Default true." ) ]
      public bool efficiency_log_breakdown = true;

      [ ConfigAttribute( "Tools", "Export ingrediants to ingredients.csv on game launch.  True or false.  Default false." ) ]
      public bool export_ingrediants = false;

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
         if ( config.skip_intro )
            Modder.TryPatch( typeof( FaderUIController ), "Awake", nameof( FaderUIController_Awake_SkipSplash ) );
         if ( config.skip_spacebar )
            Modder.TryPatch( typeof( SplashScreen ), "Update", postfix: nameof( SplashScreen_Update_SkipSplash ) );
         if ( config.disable_analytics )
            if ( Modder.TryPatch( typeof( AutomachefAnalytics ), "Track", nameof( Analytics_Disable ) ) )
               Log.Info( "Mission Analytics Disabled" );
         if ( config.load_prompt_on_enter ) {
            Modder.TryPatch( typeof( LevelStatus ), "InitUI", nameof( OfferToLoadGameOnEnter ) );
         }
         if ( config.speed2 != 3 || config.speed3 != 5 )
            Modder.TryPatch( typeof( Initializer ), "Start", nameof( Initializer_Start_AdjustSpeed ) );
         if ( config.dropdown_toogle_threshold > 0 ) {
            dropdownProp = new ConditionalWeakTable< MaterialDropdown, KitchenPartProperty >();
            dropdownIcon = new ConditionalWeakTable< MaterialDropdown, DropdownIcon >();
            Modder.TryPatch( typeof( PartProperties ), "PopulateDropdownForProperty", nameof( TrackDropdown ) );
            Modder.TryPatch( typeof( MaterialDropdown ), "ShowDropdown", nameof( ToggleDropdown ) );
         }
         if ( config.efficiency_log ) {
            efficiencyLog = new List<string>();
            if ( config.efficiency_log_breakdown )
               if ( Modder.TryPatch( typeof( EfficiencyMeter ), "Reset", nameof( EfficiencyMeter_Reset_ClearLog ) ) ) {
                  orderedDish = new Dictionary<object, int>();
                  cookedDish = new Dictionary<object, int>();
                  Modder.TryPatch( typeof( EfficiencyMeter ), "AddOrder", nameof( EfficiencyMeter_AddOrder_Log ) );
                  Modder.TryPatch( typeof( EfficiencyMeter ), "AddDeliveredDish", nameof( EfficiencyMeter_AddDeliveredDish_Log ) );
               }
            if ( Modder.TryPatch( typeof( EfficiencyMeter ), "GetEfficiency", postfix: nameof( EfficiencyMeter_Calc_Log ) ) ) {
               Modder.TryPatch( typeof( LevelManager ), "DetermineLevelOutcome", nameof( LevelManger_Outcome_StopLog ), nameof( LevelManger_Outcome_ResumeLog ) );
               Modder.TryPatch( typeof( LevelStatus ), "RenderEvents", postfix: nameof( LevelStatus_RenderEvents_ShowLog ) );
               Modder.TryPatch( typeof( KitchenEventsLog ), "ToString", postfix: nameof( KitchenLog_ToString_Append ) );
            }
         }
         if ( config.export_ingrediants )
            Modder.TryPatch( typeof( SplashScreen ), "Awake", nameof( SplashScreen_Awake_DumpCsv ) );
      }

      #region Skip Splash
      private static void FaderUIController_Awake_SkipSplash ( ref FaderUIController.SplashStateInfo[] ___m_SplashStates ) { try {
         if ( ___m_SplashStates == null || ___m_SplashStates.Length < 1 || ___m_SplashStates[0].m_AnimToPlay != "Unity" ) return;
         if ( ! ___m_SplashStates.Any( e => e.m_AnimToPlay == "LoadStartScreen" ) ) return;
         ___m_SplashStates = ___m_SplashStates.Where( e => e.m_AnimToPlay == "LoadStartScreen" ).ToArray();
         ___m_SplashStates[0].m_TimeInState = 0.01f;
         Log.Info( $"Skipping Splash Screens ({___m_SplashStates.Length} scene left)" );
         // [ { "Unity, 1, False }, { "HermesInteractive, 2, False }, { "Team 17, 4, True }, { "Legal, 3, False }, { "LoadStartScreen", 2, False } ]
      } catch ( Exception ex ) { Log.Error( ex ); } }

      private static void SplashScreen_Update_SkipSplash ( SplashScreen __instance, ref bool ___m_bProcessedCloseRequest ) { try {
         if ( ___m_bProcessedCloseRequest || InputWrapper.GetController() == null ) return;
         ___m_bProcessedCloseRequest = true;
         Modder.TryMethod( typeof( SplashScreen ), "ProceedToMainMenu" ).Invoke( __instance, Array.Empty<object>() );
         Log.Info( "Skipped Space Press Splash" );
      } catch ( Exception ex ) {
         Log.Error( ex );
         ___m_bProcessedCloseRequest = false;
      } }
      #endregion

      private static bool Analytics_Disable () => false;

      private static bool isInitialLoad = false;

      private static void OfferToLoadGameOnEnter ( LevelStatus __instance ) { try {
         isInitialLoad = false;
         var data = SaveLoadManager.GetInstance().getSavedKitchenData();
         if ( data == null ) return;
		 for ( var i = 0; i < 5; i++ )
			if ( data.Get( i ) != null ) {
               __instance.preLevelScreen.gameObject.SetActive( false );
               isInitialLoad = true;
               Initializer.GetInstance().saveLoadPanel.Open( false );
               return;
            }
      } catch ( Exception ex ) { Log.Error( ex ); } }

      private static void Initializer_Start_AdjustSpeed ( Initializer __instance ) {
         if ( __instance == null || __instance.speeds == null || __instance.speeds.Count < 4 ) return;
         Log.Info( $"Setting game speeds to [ 0x, 1x, {config.speed2}x, {config.speed3}x ]" );
         __instance.speeds[2] = config.speed2;
         __instance.speeds[3] = config.speed3;
      }

      #region Dropdown Toggle
      private static ConditionalWeakTable< MaterialDropdown, KitchenPartProperty > dropdownProp;
      private static ConditionalWeakTable< MaterialDropdown, DropdownIcon > dropdownIcon;

      private static void TrackDropdown ( MaterialDropdown dropdown, KitchenPartProperty prop, DropdownIcon icon ) { try {
         dropdownProp.Remove( dropdown );
         dropdownProp.Add( dropdown, prop );
         dropdownIcon.Remove( dropdown );
         dropdownIcon.Add( dropdown, icon );
      } catch ( Exception ex ) { Log.Error( ex ); } }

      private static bool ToggleDropdown ( MaterialDropdown __instance, ref int ___m_CurrentlySelected ) { try {
         if ( ! dropdownProp.TryGetValue( __instance, out KitchenPartProperty prop ) ) return true;
         if ( ( prop.friendlyValues?.Count ?? 0 ) > 3 ) return true;
         if ( Initializer.GetInstance().levelManager.GetLevel().IsTutorial() ) return true;
         var new_selection = ___m_CurrentlySelected + 1;
         if ( new_selection >= prop.friendlyValues.Count ) new_selection = 0;
         __instance.Select( new_selection );
         if ( dropdownIcon.TryGetValue( __instance, out DropdownIcon icon ) ) icon.UpdateIcon();
         return false;
      } catch ( Exception ex ) { Log.Error( ex ); return true; } }
      #endregion

      #region Efficiency Log
      private static Dictionary< object, int > orderedDish, cookedDish;
      private static void EfficiencyMeter_Reset_ClearLog () { orderedDish.Clear(); cookedDish.Clear(); }
      private static void EfficiencyMeter_AddOrder_Log ( Dish dish ) => EfficiencyMeter_Dish_Log( orderedDish, dish );
      private static void EfficiencyMeter_AddDeliveredDish_Log ( Dish dish ) => EfficiencyMeter_Dish_Log( cookedDish, dish );
      private static void EfficiencyMeter_Dish_Log ( Dictionary< object, int > log, Dish dish ) {
         log.TryGetValue( dish, out int i );
         log[ dish ] = i + 1;
      }

      private static int outcome;
      private static List<string> efficiencyLog;
      private static bool ShowEfficiencyLog => outcome != (int) LevelOutcome.Failure && outcome != (int) LevelOutcome.InProgress;

      private static void LevelManger_Outcome_StopLog () => outcome = (int) LevelOutcome.InProgress;
      private static void LevelManger_Outcome_ResumeLog ( LevelOutcome __result ) => outcome = (int) __result;
      private static void EfficiencyMeter_Calc_Log ( bool allGoalsFulfilled, int __result, int ___expectedIngredientsUsage, int ___expectedPowerUsage ) { try {
         if ( ! ShowEfficiencyLog ) return;
         float iUsed = IngredientsCounter.GetInstance().GetUsedIngredients(), pUsed = PowerMeter.GetInstance().GetWattsHour();
         float iMark = Mathf.Clamp01( ___expectedIngredientsUsage / iUsed ), pMark = Mathf.Clamp01( ___expectedPowerUsage / pUsed );
         float mark = ( iMark + pMark ) / 2f;
         efficiencyLog.Clear();
         efficiencyLog.Add( $"Ingredients Quota {___expectedIngredientsUsage} / {iUsed} Spent = {iMark:0.00}" );
         efficiencyLog.Add( $"Power Quota {___expectedPowerUsage}Wh / {pUsed}Wh Spent = {pMark:0.00}" );
         efficiencyLog.Add( $"( Average {mark:0.00}" + ( allGoalsFulfilled ? "" : " - 0.1 goal failed" ) + $" )² = Final {__result/100f:0.00}" );
      } catch ( Exception ex ) { Log.Error( ex ); } }

      // Show modded logs even when kitchen has no events
      private static void LevelStatus_RenderEvents_ShowLog ( LevelStatus __instance, KitchenEventsLog log ) { try {
         if ( ShowEfficiencyLog && log.GetEventsCount() <= 0 ) __instance.eventsLogTextField.text = log.ToString();
      } catch ( Exception ex ) { Log.Error( ex ); } }

      private static void KitchenLog_ToString_Append ( ref string __result ) { try {
         if ( ! ShowEfficiencyLog || efficiencyLog.Count <= 0 ) return;
         Log.Info( $"Appending efficiency log ({efficiencyLog.Count+orderedDish.Count} lines) to kitchen log." );
         __result += "\n\n";
         if ( orderedDish.Count > 0 ) __result += "Delivered / Ordered Dish ... Quota Contribution";
         foreach ( var key in orderedDish.Keys ) {
            var dish = key as Dish;
            cookedDish.TryGetValue( dish, out int delivered );
            int eI = dish.expectedIngredients, eP = dish.expectedPower, ordered = orderedDish[ dish ], missed = ordered - delivered;
            int missedPowerPenalty = ( dish.expectedPower - dish.expectedPower / 3 ) * missed;
            //__result += $"Quota\\{dish.friendlyName} = {eI} mats & {eP}Wh each\n";
            //__result += $"  {ordered-missed}/{ordered} done = {eI*ordered-missed} mats & {eP*ordered-missedPowerPenalty}Wh\n";
            __result += $"{ordered-missed}/{ordered} {dish.friendlyName} ... {eI*ordered-missed} mats & {eP*ordered-missedPowerPenalty}Wh\n";
         }
         __result += "\n" + string.Join( "\n", efficiencyLog.ToArray() );
         __result = __result.Trim();
      } catch ( Exception ex ) { Log.Error( ex ); } }
      #endregion

      #region Tools (CSV)
      private static readonly StringBuilder line = new StringBuilder();

      private static void SplashScreen_Awake_DumpCsv () { try {
         string file = Path.Combine( ZySimpleMod.AppDataDir, "ingredients.csv" );
         Log.Info( $"Exporting ingredient list to {file}" );
         using ( TextWriter f = File.CreateText( file ) ) {
            f.Csv( "Id", "Name", "Translated", "Process", "Seconds", "Recipe", "Liquids",
                   "Processed", "Grilled", "Fried", "Steamed", "Baked", "Wet", "Baterias",
                   "Spoil", "Ingredients Quota", "Power Quota" );
            foreach ( var mat in Ingredient.GetAll().Union( Dish.GetAll() ) ) {
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
         Log.Info( "Export done" );
      } catch ( Exception ex ) { Log.Warn( ex ); } }

      private static void Csv ( this TextWriter f, params string[] values ) {
         foreach ( var v in values ) {
            if ( v.Contains( "," ) || v.Contains( "\"" ) || v.Contains( "\n" ) ) line.Append( '"' ).Append( v.Replace( "\"", "\"\"" ) ).Append( "\"," );
            else line.Append( v ).Append( ',' );
         }
         --line.Length;
         f.Write( line.Append( "\r\n" ) );
         line.Length = 0;
      }
      #endregion
   }
}
