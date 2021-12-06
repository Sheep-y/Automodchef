using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using ZyMod;
using System.Text;

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

      [ ConfigAttribute( "User Interface", "Add effiency calculation to kitchen log.  True or false.  Default true." ) ]
      public bool efficiency_log = true;
      [ ConfigAttribute( "User Interface", "Breakdown efficiency quotas by dishes.  True or false.  Default true." ) ]
      public bool efficiency_log_breakdown = true;

      [ ConfigAttribute( "Tools", "Export ingrediants to ingredients.csv on game launch.  True or false.  Default false." ) ]
      public bool export_ingrediants = false;
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
            Modder.TryPatch( typeof( AutomachefAnalytics ), "Track", nameof( Analytics_Disable ) );
         if ( config.efficiency_log ) {
            if ( config.efficiency_log_breakdown )
               if ( Modder.TryPatch( typeof( EfficiencyMeter ), "Reset", nameof( EfficiencyMeter_Reset_ClearLog ) ) ) {
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

      #region Efficiency Log
      private static readonly Dictionary< object, int > orderedDish = new Dictionary< object, int >();
      private static readonly Dictionary< object, int > cookedDish  = new Dictionary< object, int >();

      private static void EfficiencyMeter_Reset_ClearLog () { orderedDish.Clear(); cookedDish.Clear(); }
      private static void EfficiencyMeter_AddOrder_Log ( Dish dish ) => EfficiencyMeter_Dish_Log( orderedDish, dish );
      private static void EfficiencyMeter_AddDeliveredDish_Log ( Dish dish ) => EfficiencyMeter_Dish_Log( cookedDish, dish );
      private static void EfficiencyMeter_Dish_Log ( Dictionary< object, int > log, Dish dish ) {
         log.TryGetValue( dish, out int i );
         log[ dish ] = i + 1;
      }

      private static int outcome;
      private static readonly List<string> efficiencyLog = new List<string>();
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

      #region Skip Splash
      private static void FaderUIController_Awake_SkipSplash ( ref FaderUIController.SplashStateInfo[] ___m_SplashStates ) { try {
         if ( ___m_SplashStates == null || ___m_SplashStates.Length < 1 || ___m_SplashStates[0].m_AnimToPlay != "Unity" ) return;
         if ( ! ___m_SplashStates.Any( e => e.m_AnimToPlay == "LoadStartScreen" ) ) return;
         Log.Info( "Skipping Splash Screens" );
         ___m_SplashStates = ___m_SplashStates.Where( e => e.m_AnimToPlay == "LoadStartScreen" ).ToArray();
         ___m_SplashStates[0].m_TimeInState = 0.01f;
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

      private static bool Analytics_Disable () { return false; }

   }
}
