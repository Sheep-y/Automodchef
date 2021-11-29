using System;
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
         // Log.Info( args.LoadedAssembly.FullName );
         if ( args?.LoadedAssembly?.FullName?.StartsWith( "Assembly-CSharp," ) != true ) return;
         Log.Info( "Game Loaded" );
         AppDomain.CurrentDomain.AssemblyLoad -= AsmLoaded;
         try {
            PatchGame();
         } catch ( Exception ex ) {
            Log.Error( ex.ToString() );
         }
      }

      private static void PatchGame () {
         Mod.TryPatch( typeof( Mod ), "ToHarmony", nameof( test ) );
         Mod.TryPatch( typeof( SplashScreen ), "Start", nameof( splash_start_pre ) );
         Mod.TryPatch( typeof( MainMenu ), "SetScreen", nameof( main_set_pre ) );
         Log.Info( "Game Patched." );
      }

      private static void test () { Log.Info( "ToHarmony" ); }

      private static void splash_start_pre () {
         Log.Info( "Splash Start" );
      }

      private static void main_set_pre () {
         Log.Info( "Main SecScreen" );
      }
   }

   
   internal static class Mod {

      internal static Type Code = typeof( Automodchef );
      private static Harmony harmony = new Harmony( "Automodchef" );

      private static MethodInfo GetPatchSubject ( Type type, string method ) { try {
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
      private static string Timestamp ( object msg ) => DateTime.Now.ToString( "HH:mm:ss.fff " ) + msg;
   }
}
