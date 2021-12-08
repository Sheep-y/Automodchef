using HarmonyLib;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using static System.Reflection.BindingFlags;

namespace ZyMod {
   public abstract class ZySimpleMod {
      protected static object sync = new object();
      internal static ZySimpleMod instance;
      internal static string ModName { get { lock ( sync ) return instance?.GetModName() ?? "ZyMod"; } }
      internal static Type PatchClass { get { lock ( sync ) return instance?.GetPatchClass() ?? typeof( ZySimpleMod ); } }

      public void Initialize () {
         lock ( sync ) {
            if ( instance != null ) { Log.Warn( "Mod already initialized" ); return; }
            instance = this;
         }
         try {
            Log.Initialize();
            foreach ( var asm in AppDomain.CurrentDomain.GetAssemblies() ) Log.Fine( $"Already loaded: {asm.FullName}, {asm.CodeBase}" );
            AppDomain.CurrentDomain.AssemblyLoad += AsmLoaded;
            AppDomain.CurrentDomain.UnhandledException += ( _, evt ) => Log.Error( evt.ExceptionObject );
            AppDomain.CurrentDomain.AssemblyResolve += ( _, evt ) => { Log.Fine( $"Resolving {evt.Name}" ); return null; };
            Log.Info( "Mod Initiated" );
         } catch ( Exception ex ) {
            Log.Error( ex.ToString() );
         }
      }

      private void AsmLoaded ( object sender, AssemblyLoadEventArgs args ) {
         string name = args.LoadedAssembly.FullName;
         if ( args.LoadedAssembly.IsDynamic || name.StartsWith( "DMDASM." ) || name.StartsWith( "HarmonyDTFAssembly" ) ) return;
         Log.Fine( $"{name}, {args.LoadedAssembly.CodeBase}" );
         if ( name.StartsWith( "Assembly-CSharp," ) ) {
            Log.Info( "Target assembly loaded." );
            //AppDomain.CurrentDomain.AssemblyLoad -= AsmLoaded;
            try {
               OnGameAssemblyLoaded( args.LoadedAssembly );
               Log.Info( "Bootstrap complete." );
            } catch ( Exception ex ) {
               Log.Error( ex.ToString() );
            }
         }
      }

      private static string _AppDataDir;
      public static string AppDataDir { get {
         if ( _AppDataDir != null ) return _AppDataDir;
         lock ( sync ) {
            if ( instance == null ) return "";
            _AppDataDir = instance.GetAppDataDir();
         }
         try {
            if ( ! Directory.Exists( _AppDataDir ) ) {
               Directory.CreateDirectory( _AppDataDir );
               if ( ! Directory.Exists( _AppDataDir ) ) _AppDataDir = "";
            }
         } catch ( Exception _ ) { _AppDataDir = ""; }
         return _AppDataDir;
      } }

      protected abstract string GetAppDataDir ();
      protected abstract void OnGameAssemblyLoaded ( Assembly game );
      protected abstract Type GetPatchClass ();
      protected virtual string GetModName () => GetType().Name;
   }

   [ AttributeUsage( AttributeTargets.Class | AttributeTargets.Field | AttributeTargets.Property ) ]
   public class ConfigAttribute : Attribute {
      public ConfigAttribute () {}
      public ConfigAttribute ( string comment ) { Comment = comment; }
      public ConfigAttribute ( string section, string comment ) { Section = section; Comment = comment; }
      public string Section;
      public string Comment;
   }

   public class IniConfig {

      public void Load () => Load( Path.Combine( ZySimpleMod.AppDataDir, ZySimpleMod.ModName + ".ini" ) );
      public virtual void Load ( string path ) { try {
         if ( ! File.Exists( path ) ) {
            Create( path );
         } else {
            Log.Info( "Loading " + path );
            foreach ( var line in File.ReadAllLines( path ) ) {
               var split = line.Split( new char[]{ '=' }, 2 );
               if ( split.Length != 2 || line.StartsWith( ";" ) ) continue;
               var prop = GetType().GetField( split[ 0 ].Trim() );
               if ( prop == null ) { Log.Warn( "Unknown field: " + split[ 0 ] ); continue; }
               var val = split[1].Trim();
               if ( val.Length > 1 && val.StartsWith( "\"" ) && val.EndsWith( "\"" ) ) val = val.Substring( 1, val.Length - 2 );
               switch ( prop.FieldType.FullName ) {
                  case "System.SByte"  : if ( SByte .TryParse( val, out sbyte  bval ) ) prop.SetValue( this, bval ); break;
                  case "System.Int16"  : if ( Int16 .TryParse( val, out short  sval ) ) prop.SetValue( this, sval ); break;
                  case "System.Int32"  : if ( Int32 .TryParse( val, out int    ival ) ) prop.SetValue( this, ival ); break;
                  case "System.Int64"  : if ( Int64 .TryParse( val, out long   lval ) ) prop.SetValue( this, lval ); break;
                  case "System.Byte"   : if ( Byte  .TryParse( val, out byte   Bval ) ) prop.SetValue( this, Bval ); break;
                  case "System.UInt16" : if ( UInt16.TryParse( val, out ushort Sval ) ) prop.SetValue( this, Sval ); break;
                  case "System.UInt32" : if ( UInt32.TryParse( val, out uint   Ival ) ) prop.SetValue( this, Ival ); break;
                  case "System.UInt64" : if ( UInt64.TryParse( val, out ulong  Lval ) ) prop.SetValue( this, Lval ); break;
                  case "System.Single" : if ( Single.TryParse( val, out float  fval ) ) prop.SetValue( this, fval ); break;
                  case "System.Double" : if ( Double.TryParse( val, out double dval ) ) prop.SetValue( this, dval ); break;
                  case "System.String" : prop.SetValue( this, val ); break;
                  case "System.Boolean" :
                     val = val.ToLowerInvariant();
                     prop.SetValue( this, val == "yes" || val == "1" || val == "true" );
                     break;
                  default :
                     Log.Warn( $"Unexpected field type {prop.FieldType} of {split[0]}" );
                     break;
               }
            }
         }
         foreach ( var prop in GetType().GetFields() )
            Log.Info( prop.Name + " = " + prop.GetValue( this ) );
      } catch ( Exception ex ) { Log.Warn( ex ); } }

      private string lastSection = "";

      public virtual void Create ( string ini ) { try {
         Log.Info( "Creating " + ini );
         using ( TextWriter tw = File.CreateText( ini ) ) {
            var attr = GetType().GetCustomAttribute<ConfigAttribute>();
            if ( ! string.IsNullOrWhiteSpace( attr?.Comment ) ) tw.Write( $"{attr.Comment}\r\n" );
            var fields = GetType().GetFields();
            if ( fields.Any( e => e.GetCustomAttribute<ConfigAttribute>() != null ) )
               fields = fields.Where( e => e.GetCustomAttribute<ConfigAttribute>() != null ).ToArray();
            foreach ( var f in fields )
               if ( f.IsPublic && ! f.IsStatic ) {
                  attr = f.GetCustomAttribute<ConfigAttribute>();
                  if ( attr != null ) {
                     if ( ! string.IsNullOrWhiteSpace( attr.Section ) && attr.Section != lastSection ) {
                        lastSection = attr.Section;
                        tw.Write( $"\r\n[{attr.Section}]\r\n" );
                     }
                     if ( ! string.IsNullOrWhiteSpace( attr.Comment ) ) tw.Write( $"; {attr.Comment}\r\n" );
                  }
                  tw.Write( f.Name + " = " + f.GetValue( this ) + "\r\n" );
               }
            tw.Flush();
            }
            if ( File.Exists( ini ) ) Log.Fine( $"{new FileInfo( ini ).Length} bytes written" );
            else Log.Warn( "Config file not written." );
      } catch ( Exception ex ) { Log.Warn( "Cannot create config file: " + ex ); } }
   }

   public static class Modder {

      private static Type Code;
      private static Harmony harmony;

      public static MethodInfo TryMethod ( this Type type, string method ) { try { return Method( type, method ); } catch ( ApplicationException ) { return null; } }

      public static MethodInfo Method ( this Type type, string method ) { try {
         var result = type?.GetMethod( method, Public | NonPublic | Instance | Static );
         if ( result == null ) throw new ApplicationException( $"Not found: {type}.{method}" );
         return result;
      } catch ( AmbiguousMatchException ex ) { throw new ApplicationException( $"Multiple: {type}.{method}", ex ); } }

      internal static void Patch ( Type type, string method, string prefix = null, string postfix = null, string transpiler = null ) =>
         Patch( Method( type, method ), prefix, postfix, transpiler );

      internal static void Patch ( MethodBase method, string prefix = null, string postfix = null, string transpiler = null ) {
         if ( harmony == null ) harmony = new Harmony( ZySimpleMod.ModName );
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
         if ( Code == null ) Code = ZySimpleMod.PatchClass;
         return new HarmonyMethod( Code.GetMethod( name, Public | NonPublic | Static ) ?? throw new NullReferenceException( name + " not found" ) );
      }
   }

   public static class Log {
      public static TraceLevel LogLevel = TraceLevel.Info;
      internal static string LogPath = Path.Combine( ZySimpleMod.AppDataDir, ZySimpleMod.ModName + ".log" );
      internal static void Initialize () { try {
         var level = System.Environment.GetEnvironmentVariable( $"{ZySimpleMod.ModName}_LOG_LEVEL".Replace( ' ', '_' ).ToUpperInvariant() ) ?? "";
         switch ( ( level.Trim().ToUpperInvariant() + " " )[0] ) {
            case 'O' : LogLevel = TraceLevel.Off; break;
            case 'E' : LogLevel = TraceLevel.Verbose; break;
            case 'W' : LogLevel = TraceLevel.Warning; break;
            case 'V' : LogLevel = TraceLevel.Verbose; break;
            default  : LogLevel = TraceLevel.Info; break;
         }
         if ( LogLevel == TraceLevel.Off ) File.Delete( LogPath );
         else using ( TextWriter f = File.CreateText( LogPath ) )
             f.WriteLine( $"{DateTime.Now:u} {ZySimpleMod.ModName} initiated" );
      } catch ( Exception ) { } }
      public static void Error ( object msg ) => Write( LogLevel >= TraceLevel.Error   ? Timestamp( "ERROR ", msg ) : null );
      public static void Warn  ( object msg ) => Write( LogLevel >= TraceLevel.Warning ? Timestamp( "WARN ", msg ) : null );
      public static void Info  ( object msg ) => Write( LogLevel >= TraceLevel.Info    ? Timestamp( "INFO ", msg ) : null );
      public static void Fine  ( object msg ) => Write( LogLevel >= TraceLevel.Verbose ? Timestamp( "FINE ", msg ) : null );
      public static void Write ( object msg ) { if ( msg != null ) try {
         using ( TextWriter f = File.AppendText( LogPath ) ) f.WriteLine( msg );
      } catch ( Exception ) { } }
      private static string Timestamp ( string level, object msg ) => DateTime.Now.ToString( "HH:mm:ss.fff " ) + level + ( msg ?? "null" );
   }
}
