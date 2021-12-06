using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;

namespace Automodchef {
   public static class Automodchef {
      public static void Main () {
         ZySimpleMod.Initialise();
      }
   }

   public class Config : IniConfig {
      public int config_version = 20211205;
      public bool skip_intro = true;
      public bool skip_spacebar = true;
      public bool efficiency_log = true;
      public bool efficiency_log_breakdown = true;
      public bool disable_analytics = true;
   }

   internal static class Patches {

      internal static Config config = new Config();

      internal static void Apply ( Assembly game ) {
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
         Log.Info( "Game Patched." );
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
         foreach ( var key in orderedDish.Keys ) {
            var dish = key as Dish;
            cookedDish.TryGetValue( dish, out int delivered );
            int eI = dish.expectedIngredients, eP = dish.expectedPower, ordered = orderedDish[ dish ], missed = ordered - delivered;
            int missedPowerPenalty = ( dish.expectedPower - dish.expectedPower / 3 ) * missed;
            __result += $"Quota\\{dish.friendlyName} = {eI} mats & {eP}Wh each\n";
            __result += $"  {ordered-missed}/{ordered} done = {eI*ordered-missed} mats & {eP*ordered-missedPowerPenalty}Wh\n";
         }
         __result += "\n" + string.Join( "\n", efficiencyLog.ToArray() );
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

      private static bool Analytics_Disable () { return false; }

   }
}
