using System;
using System.Linq;
using System.IO;
using System.Reflection;
using HarmonyLib;
using static System.Reflection.BindingFlags;

namespace Automodchef {
   public static class ZySimpleMod {

      public static void Initialise () {
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
            Patches.config.Load( Path.Combine( SaveDir, typeof( Automodchef ).Name + ".ini" ) );
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

   public class IniConfig {

      public void Load ( string path ) { try {
         if ( ! File.Exists( path ) ) { Create( path ); return; }
         Log.Info( "Loading " + path );
         foreach ( var line in File.ReadAllLines( path ) ) {
            var split = line.Split( new char[]{ '=' }, 2 );
            if ( split.Length != 2 ) continue;
            var prop = GetType().GetField( split[ 0 ].Trim() );
            if ( prop == null ) { Log.Warn( "Unknown field: " + split[ 0 ] ); continue; }
            var val = split[1].Trim();
            if ( val.Length > 1 && val.StartsWith( "\"" ) && val.EndsWith( "\"" ) ) val = val.Substring( 1, val.Length - 2 );
            switch ( prop.FieldType.FullName ) {
               case "System.SByte" : case "System.Int16"  : case "System.Int32"  : case "System.Int64"  :
               case "System.Byte"  : case "System.UInt16" : case "System.UInt32" : case "System.UInt64" :
                  if ( Int64.TryParse( val, out long ival ) ) prop.SetValue( this, ival );
                  break;
               case "System.String" :
                  prop.SetValue( this, val );
                  break;
               case "System.Boolean" :
                  val = val.ToLowerInvariant();
                  prop.SetValue( this, val == "yes" || val == "1" || val == "true" );
                  break;
               default :
                  Log.Warn( $"Unexpected field type {prop.FieldType} of {split[0]}" );
                  break;
            }
         }
         foreach ( var prop in typeof( Config ).GetFields() )
            Log.Info( prop.Name + " = " + prop.GetValue( this ) );
      } catch ( Exception ex ) { Log.Warn( ex ); } }

      public virtual void Create ( string ini ) { try {
         Log.Info( "Not found, creating " + ini );
         using ( TextWriter tw = File.CreateText( ini ) ) {
            tw.Write( "[System]config_version = 20211205\r\nskip_intro = yes\r\nskip_spacebar = yes\r\ndisable_analytics\r\n\r\n" );
            tw.Write( "[User Interface]efficiency_log = yes\r\nefficiency_log_breakdown = yes\r\n\r\n" );
         }
      } catch ( Exception ) { } }
   }

   public static class Modder {

      internal static Type Code = typeof( Patches );
      internal static Harmony harmony = new Harmony( "Automodchef" );

      public static MethodInfo TryMethod ( this Type type, string method ) { try { return Method( type, method ); } catch ( ApplicationException ) { return null; } }

      public static MethodInfo Method ( this Type type, string method ) { try {
         var result = type?.GetMethod( method, Public | NonPublic | Instance | Static );
         if ( result == null ) throw new ApplicationException( $"Not found: {type}.{method}" );
         return result;
      } catch ( AmbiguousMatchException ex ) { throw new ApplicationException( $"Multiple: {type}.{method}", ex ); } }

      internal static void Patch ( Type type, string method, string prefix = null, string postfix = null, string transpiler = null ) =>
         Patch( Method( type, method ), prefix, postfix, transpiler );

      internal static void Patch ( MethodBase method, string prefix = null, string postfix = null, string transpiler = null ) {
         harmony.Patch( method, ToHarmony( prefix ), ToHarmony( postfix ), ToHarmony( transpiler ) );
         Log.Info( $"Patched {method.DeclaringType} {method} | Pre: {prefix} | Post: {postfix} | Trans: {transpiler}" );
      }

      internal static bool TryPatch ( Type type, string method, string prefix = null, string postfix = null, string transpiler = null ) =>
         TryPatch( Method( type, method ), prefix, postfix, transpiler );

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
      internal static string LogPath = Path.Combine( ZySimpleMod.SaveDir, typeof( Automodchef ).Name + ".log" );
      internal static void CreateNew () { try {
         using ( TextWriter tw = File.CreateText( LogPath ) ) {
             tw.WriteLine( $"{DateTime.Now:u} Automodchef initiated" );
             tw.Flush();
         }
      } catch ( Exception ) { } }
      internal static void Error ( object msg ) => Write( Timestamp( "ERROR " + msg ) );
      internal static void Warn ( object msg ) => Write( Timestamp( "WARN " + msg ) );
      //internal static void Info ( object msg ) { msg = Timestamp( msg ); Task.Run( () => Write( msg ) ); }
      internal static void Info ( object msg ) => Write( Timestamp( msg ) );
      internal static void Write ( object msg ) { try { using ( TextWriter tw = File.AppendText( LogPath ) ) tw.WriteLine( msg ); } catch ( Exception ) { } }
      private static string Timestamp ( object msg ) => DateTime.Now.ToString( "HH:mm:ss.fff " ) + ( msg ?? "null" );
   }
}
