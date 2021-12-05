using System;
using System.Linq;
using System.IO;
using System.Reflection;
using HarmonyLib;
using static System.Reflection.BindingFlags;
using System.Collections.Generic;
using UnityEngine;

namespace Automodchef {
   public static class Automodchef {

      public static void Main () {
         try {
            Log.CreateNew();
            AppDomain.CurrentDomain.AssemblyLoad += AsmLoaded;
            AppDomain.CurrentDomain.UnhandledException += ( sender, e ) => Log.Error( e.ExceptionObject );
            Log.Info( "Mod Initiated" );
         } catch ( Exception ex ) {
            Log.Error( ex.ToString() );
         }
      }

      private static void AsmLoaded ( object sender, AssemblyLoadEventArgs args ) {
         //Log.Info( args.LoadedAssembly.FullName );
         if ( args?.LoadedAssembly?.FullName?.StartsWith( "Assembly-CSharp," ) != true ) return;
         Log.Info( "Game Loaded" );
         AppDomain.CurrentDomain.AssemblyLoad -= AsmLoaded;
         try {
            Config.Load( Path.Combine( SaveDir, typeof( Automodchef ).Name + ".ini" ) );
            Patches.Apply( args.LoadedAssembly );
         } catch ( Exception ex ) {
            Log.Error( ex.ToString() );
         }
      }

      private static string _SaveDir;
      internal static string SaveDir { get {
         if ( _SaveDir != null ) return _SaveDir;
         var path = System.Environment.GetFolderPath( System.Environment.SpecialFolder.LocalApplicationData );
         if ( string.IsNullOrEmpty( path ) ) return null;
         path = Path.Combine( Directory.GetParent( path ).FullName, "LocalLow", "HermesInteractive", "Automachef" );
         _SaveDir = path;
         try {
            if ( ! Directory.Exists( path ) ) {
               Directory.CreateDirectory( path );
               if ( ! Directory.Exists( path ) ) _SaveDir = "";
            }
         } catch ( Exception _ ) { _SaveDir = ""; }
         return _SaveDir;
      } }
   }

   public class Config {
      public int config_version = 20211205;
      public bool skip_intro = true;
      public bool skip_spacebar = true;
      public bool efficiency_log = true;
      public bool efficiency_log_breakdown = true;
      public bool disable_analytics = true;

      internal static void Load ( string path ) { try {
         if ( ! File.Exists( path ) ) { Create( path ); return; }
         Log.Info( "Loading " + path );
         var conf = Patches.config;
         foreach ( var line in File.ReadAllLines( path ) ) {
            var split = line.Split( new char[]{ '=' }, 2 );
            if ( split.Length != 2 ) continue;
            var prop = typeof( Config ).GetField( split[ 0 ].Trim() );
            if ( prop == null ) { Log.Warn( "Unknown field: " + split[ 0 ] ); continue; }
            var val = split[1].Trim();
            if ( val.Length > 1 && val.StartsWith( "\"" ) && val.EndsWith( "\"" ) ) val = val.Substring( 1, val.Length - 2 );
            switch ( prop.FieldType.FullName ) {
               case "System.SByte" : case "System.Int16"  : case "System.Int32"  : case "System.Int64"  :
               case "System.Byte"  : case "System.UInt16" : case "System.UInt32" : case "System.UInt64" :
                  if ( Int64.TryParse( val, out long ival ) ) prop.SetValue( conf, ival );
                  break;
               case "System.String" :
                  prop.SetValue( conf, val );
                  break;
               case "System.Boolean" :
                  val = val.ToLowerInvariant();
                  prop.SetValue( conf, val == "yes" || val == "1" || val == "true" );
                  break;
               default :
                  Log.Warn( $"Unexpected field type {prop.FieldType} of {split[0]}" );
                  break;
            }
         }
         foreach ( var prop in typeof( Config ).GetFields() )
            Log.Info( prop.Name + " = " + prop.GetValue( conf ) );
      } catch ( Exception ex ) { Log.Warn( ex ); } }

      private static void Create ( string ini ) { try {
         Log.Info( "Not found, creating " + ini );
         using ( TextWriter tw = File.CreateText( ini ) ) {
            tw.Write( "[System]config_version = 20211205\r\nskip_intro = yes\r\nskip_spacebar = yes\r\ndisable_analytics\r\n\r\n" );
            tw.Write( "[User Interface]efficiency_log = yes\r\nefficiency_log_breakdown = yes\r\n\r\n" );
         }
      } catch ( Exception ) { } }
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
         efficiencyLog.Add( $"( Average {mark:0.00}" + ( allGoalsFulfilled ? "" : " - 0.1 goal failed" ) + " )² = Final {__result/100f:0.00}" );
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
            __result += $"Quota/{dish.friendlyName} = {eI} mats & {eP}Wh each\n";
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
         typeof( SplashScreen ).Method( "ProceedToMainMenu" ).Invoke( __instance, Array.Empty<object>() );
         Log.Info( "Skipped Space Press Splash" );
      } catch ( Exception ex ) {
         Log.Error( ex );
         ___m_bProcessedCloseRequest = false;
      } }
      #endregion

      private static bool Analytics_Disable () { return false; }

      private static MethodInfo Method ( this Type type, string method ) { try {
         return Modder.GetPatchSubject( type, method );
      } catch ( Exception ex ) { Log.Warn( ex ); return null; } }
   }

   internal static class Modder {

      internal static Type Code = typeof( Patches );
      internal static Harmony harmony = new Harmony( "Automodchef" );

      internal static MethodInfo GetPatchSubject ( Type type, string method ) { try {
         var result = type.GetMethod( method, Public | NonPublic | Instance | Static );
         if ( result == null ) throw new ApplicationException( $"Not found: {type}.{method}" );
         return result;
      } catch ( AmbiguousMatchException ex ) { throw new ApplicationException( $"Multiple: {type}.{method}", ex ); } }

      internal static void Patch ( Type type, string method, string prefix = null, string postfix = null, string transpiler = null ) =>
         Patch( GetPatchSubject( type, method ), prefix, postfix, transpiler );

      internal static void Patch ( MethodBase method, string prefix = null, string postfix = null, string transpiler = null ) {
         harmony.Patch( method, ToHarmony( prefix ), ToHarmony( postfix ), ToHarmony( transpiler ) );
         Log.Info( $"Patched {method.DeclaringType} {method} | Pre: {prefix} | Post: {postfix} | Trans: {transpiler}" );
      }

      internal static bool TryPatch ( Type type, string method, string prefix = null, string postfix = null, string transpiler = null ) =>
         TryPatch( GetPatchSubject( type, method ), prefix, postfix, transpiler );

      internal static bool TryPatch ( MethodBase method, string prefix = null, string postfix = null, string transpiler = null ) { try {
         Patch( method, prefix, postfix, transpiler );
         return true;
      } catch ( Exception ex ) {
         Log.Warn( ex );
         return false;
      } }

      private static HarmonyMethod ToHarmony ( string name ) {
         if ( string.IsNullOrWhiteSpace( name ) ) return null;
         return new HarmonyMethod( Code.GetMethod( name, Public | NonPublic | Static ) ?? throw new NullReferenceException( name + " not found" ) );
      }
   }

   // TODO: Replace with HarmonyX logger
   internal static class Log {
      private static string LogPath = Path.Combine( Automodchef.SaveDir, typeof( Automodchef ).Name + ".log" );
      internal static void CreateNew () { try {
         using ( TextWriter tw = File.CreateText( LogPath ) ) {
             tw.WriteLine( $"{DateTime.Now:u} Automodchef initiated" );
             tw.Flush();
         }
      } catch ( Exception ) { } }
      internal static void Error ( Exception msg ) => Write( Timestamp( "ERROR " + msg ) );
      internal static void Error ( object msg ) => Write( Timestamp( "ERROR " + msg ) );
      internal static void Warn ( object msg ) => Write( Timestamp( "WARN " + msg ) );
      //internal static void Info ( object msg ) { msg = Timestamp( msg ); Task.Run( () => Write( msg ) ); }
      internal static void Info ( object msg ) => Write( Timestamp( msg ) );
      internal static void Write ( object msg ) { try { using ( TextWriter tw = File.AppendText( LogPath ) ) tw.WriteLine( msg ); } catch ( Exception ) { } }
      private static string Timestamp ( object msg ) => DateTime.Now.ToString( "HH:mm:ss.fff " ) + ( msg ?? "null" );
   }
}
