using System;
using System.Linq;
using System.IO;
using System.Reflection;
using HarmonyLib;
using static System.Reflection.BindingFlags;

namespace Automodchef
{
   public static class Automodchef {

      public static void Main () { try {
         Log.CreateNew();
         AppDomain.CurrentDomain.AssemblyLoad += AsmLoaded;
         AppDomain.CurrentDomain.UnhandledException += ( sender, e ) => Log.Error( e );
         Log.Info( "Mod Initiated" );
         /*
         var jsonReader = JsonReaderWriterFactory.CreateJsonReader( Encoding.UTF8.GetBytes(@"{ ""Address"": { ""City"": ""New York"" } }"), new System.Xml.XmlDictionaryReaderQuotas() );
         var root = XElement.Load(jsonReader);
         using ( TextWriter tw = File.CreateText( "mod.txt" ) ) {
             tw.WriteLine( root.XPathSelectElement("//Address/City").Value );
             tw.Flush();
         }
         */
      } catch ( Exception ex ) {
         Log.Error( ex.ToString() );
      } }

      private static void AsmLoaded ( object sender, AssemblyLoadEventArgs args ) {
         //Log.Info( args.LoadedAssembly.FullName );
         if ( args?.LoadedAssembly?.FullName?.StartsWith( "Assembly-CSharp" ) != true ) return;
         Log.Info( "Game Loaded" );
         AppDomain.CurrentDomain.AssemblyLoad -= AsmLoaded;
         try {
            Patches.Apply( args.LoadedAssembly );
         } catch ( Exception ex ) {
            Log.Error( ex.ToString() );
         }
      }

   }

   internal static class Patches {

      internal static void Apply ( Assembly game ) {
         /*
         foreach ( var cls in game.GetTypes() ) {
            if ( ! cls.IsSubclassOf( typeof( MonoBehaviour ) ) ) continue;
            if ( cls.FullName.StartsWith( "UnityEngine" ) ) continue;
            try {
               var m = cls.GetMethod( "Awake", Public | NonPublic | Instance );
               if ( m != null ) Mod.TryPatch( m, nameof( ClassLog ) );
            } catch ( AmbiguousMatchException ex ) { }
         }
         */
         Mod.TryPatch( typeof( FaderUIController ), "Awake", nameof( FaderUIController_Awake_SkipSplash ) );
         Mod.TryPatch( typeof( SplashScreen ), "Update", postfix: nameof( SplashScreen_Update_SkipSplash ) );
         Log.Info( "Game Patched." );
      }

      //private static void ClassLog ( object __instance ) { Log.Info( __instance?.GetType().FullName ); }

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
         //if ( ControllerInterface.GetInstance() == null ) { Log.Warn( "ControllerInterface not found" ); return; };
         //if ( ___m_MenuBackground == null )  { Log.Warn( "___m_MenuBackground not found" ); return; };
         //if ( ___m_MenuRootCanvasGroup == null )  { Log.Warn( "___m_MenuRootCanvasGroup not found" ); return; };
         ___m_bProcessedCloseRequest = true;
         typeof( SplashScreen ).Method( "ProceedToMainMenu" ).Invoke( __instance, Array.Empty<object>() );
         Log.Info( "Skipped Space Press Splash" );
      } catch ( Exception ex ) {
         Log.Error( ex );
         ___m_bProcessedCloseRequest = false;
      } }

      private static MethodInfo Method ( this Type type, string method ) { try {
         return Mod.GetPatchSubject( type, method );
      } catch ( Exception ex ) { Log.Warn( ex ); return null; } }
   }

   internal static class Mod {

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
      private static string LogPath = "mod.txt";
      internal static void CreateNew () {
         using ( TextWriter tw = File.CreateText( LogPath ) ) {
             tw.WriteLine( $"{DateTime.Now:u} Automodchef initiated" );
             tw.Flush();
         }
      }
      internal static void Error ( object msg ) { try { Write( Timestamp( "ERROR " + msg ) ); } catch ( Exception ) { } }
      internal static void Warn ( object msg ) => Write( Timestamp( "WARN " + msg ) );
      //internal static void Info ( object msg ) { msg = Timestamp( msg ); Task.Run( () => Write( msg ) ); }
      internal static void Info ( object msg ) => Write( Timestamp( msg ) );
      internal static void Write ( object msg ) { using ( TextWriter tw = File.AppendText( LogPath ) ) tw.WriteLine( msg ); }
      private static string Timestamp ( object msg ) => DateTime.Now.ToString( "HH:mm:ss.fff " ) + ( msg ?? "null" );
   }
}
