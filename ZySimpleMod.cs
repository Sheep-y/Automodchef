using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using static System.Reflection.BindingFlags;

namespace ZyMod {
   public abstract class ZySimpleMod {
      protected static object sync = new object();
      internal static ZySimpleMod instance;
      public static ZyLogger Log { get; private set; }
      internal static string ModName { get { lock ( sync ) return instance?.GetModName() ?? "ZyMod"; } }
      internal static Type PatchClass { get { lock ( sync ) return instance?.GetPatchClass() ?? typeof( ZySimpleMod ); } }

      private static bool ignoreAssembly ( Assembly asm ) => asm.IsDynamic || asm.FullName.StartsWith( "DMDASM." ) || asm.FullName.StartsWith( "HarmonyDTFAssembly" );
      private static bool isTargetAssembly ( Assembly asm ) => asm.FullName.StartsWith( "Assembly-CSharp," );

      public void Initialize () {
         lock ( sync ) {
            if ( instance != null ) { Log.Warn( "Mod already initialized" ); return; }
            instance = this;
         }
         try {
            Log = new ZyLogger( Path.Combine( AppDataDir, ModName + ".log" ) );
            AppDomain.CurrentDomain.UnhandledException += ( _, evt ) => Log.Error( evt.ExceptionObject );
            AppDomain.CurrentDomain.AssemblyResolve += ( _, evt ) => { Log.Fine( "Resolving {0}", evt.Name ); return null; };
            foreach ( var asm in AppDomain.CurrentDomain.GetAssemblies() ) {
               if ( ignoreAssembly( asm ) ) continue;
               Log.Fine( "DLL {0}, {1}", asm.FullName, asm.CodeBase );
               if ( isTargetAssembly( asm ) ) { GameLoaded( asm ); return; }
            }
            AppDomain.CurrentDomain.AssemblyLoad += AsmLoaded;
            Log.Info( "Mod Initiated" );
         } catch ( Exception ex ) {
            Log.Error( ex.ToString() );
         }
      }

      private void AsmLoaded ( object sender, AssemblyLoadEventArgs args ) {
         Assembly asm = args.LoadedAssembly;
         if ( ignoreAssembly( asm ) ) return;
         Log.Fine( "DLL {0}, {1}", asm.FullName, asm.CodeBase );
         if ( ! isTargetAssembly( asm ) ) return;
         if ( Log.LogLevel <= TraceLevel.Info ) AppDomain.CurrentDomain.AssemblyLoad -= AsmLoaded;
         GameLoaded( asm );
      }

      private void GameLoaded ( Assembly asm ) { try {
         Log.Info( "Target assembly loaded." );
         OnGameAssemblyLoaded( asm );
         Log.Info( "Bootstrap complete." );
      } catch ( Exception ex ) { Log.Error( ex ); } }

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
      protected ZyLogger Log => ZySimpleMod.Log;
      public void Load () => Load ( Path.Combine( ZySimpleMod.AppDataDir, ZySimpleMod.ModName + ".ini" ) );
      public virtual void Load ( string path ) { try {
         if ( ! File.Exists( path ) ) {
            Create( path );
         } else {
            Log.Info( "Loading {0}", path );
            foreach ( var line in File.ReadAllLines( path ) ) {
               var split = line.Split( new char[]{ '=' }, 2 );
               if ( split.Length != 2 || line.StartsWith( ";" ) ) continue;
               var prop = GetType().GetField( split[ 0 ].Trim() );
               if ( prop == null ) { Log.Warn( "Unknown field: {0}", split[ 0 ] ); continue; }
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
                     if ( val == "yes" || val == "1" || val == "true" ) prop.SetValue( this, true );
                     else if ( val == "no" || val == "0" || val == "false" ) prop.SetValue( this, false );
                     break;
                  default :
                     Log.Warn( "Unexpected field type {0} of {1}", prop.FieldType, split[ 0 ] );
                     break;
               }
            }
         }
         foreach ( var prop in GetType().GetFields() ) Log.Info( "Config {0} = {1}", prop.Name, prop.GetValue( this ) );
      } catch ( Exception ex ) { Log.Warn( ex ); } }

      private string lastSection = "";

      public virtual void Create ( string ini ) { try {
         Log.Info( "Creating {0}", ini );
         using ( TextWriter tw = File.CreateText( ini ) ) {
            var attr = GetType().GetCustomAttribute<ConfigAttribute>();
            if ( ! string.IsNullOrWhiteSpace( attr?.Comment ) ) tw.Write( $"{attr.Comment}\r\n" );
            var fields = GetType().GetFields();
            if ( fields.Any( e => e.GetCustomAttribute<ConfigAttribute>() != null ) )
               fields = fields.Where( e => e.GetCustomAttribute<ConfigAttribute>() != null ).ToArray();
            foreach ( var f in fields ) {
               if ( ! f.IsPublic || f.IsStatic ) continue;
               if ( ( attr = f.GetCustomAttribute<ConfigAttribute>() ) != null ) {
                  if ( ! string.IsNullOrWhiteSpace( attr.Section ) && attr.Section != lastSection ) tw.Write( $"\r\n[{lastSection = attr.Section}]\r\n" );
                  if ( ! string.IsNullOrWhiteSpace( attr.Comment ) ) tw.Write( $"; {attr.Comment}\r\n" );
               }
               tw.Write( f.Name + " = " + f.GetValue( this ) + "\r\n" );
            }
            tw.Flush();
         }
         if ( File.Exists( ini ) ) Log.Fine( "{0} bytes written", new FileInfo( ini ).Length );
         else Log.Warn( "Config file not written." );
      } catch ( Exception ex ) { Log.Warn( "Cannot create config file: {0}", ex ); } }
   }

   public static class Modder { // Not thread safe.  Patches are assumed to happen on the same thread.

      private static Harmony harmony;

      public static IEnumerable< MethodInfo > AllMethods ( this Type type ) => type.GetMethods( Public | NonPublic | Instance | Static ).Where( e => ! e.IsAbstract );
      public static IEnumerable< MethodInfo > AllMethods ( this Type type, string name ) => type.AllMethods().Where( e => e.Name == name );
      public static MethodInfo Method ( this Type type, string method ) { try {
         return type?.GetMethod( method, Public | NonPublic | Instance | Static ) ?? throw new ApplicationException( $"Not found: {type}.{method}" );
      } catch ( AmbiguousMatchException ex ) { throw new ApplicationException( $"Multiple: {type}.{method}", ex ); } }
      public static MethodInfo TryMethod ( this Type type, string method ) { try { return Method( type, method ); } catch ( ApplicationException ) { return null; } }

      internal static MethodInfo Patch ( Type type, string method, string prefix = null, string postfix = null, string transpiler = null ) =>
         Patch( Method( type, method ), prefix, postfix, transpiler );

      internal static MethodInfo Patch ( MethodBase method, string prefix = null, string postfix = null, string transpiler = null ) {
         if ( harmony == null ) harmony = new Harmony( ZySimpleMod.ModName );
         ZySimpleMod.Log.Fine( "Patching {0} {1} | Pre: {2} | Post: {3} | Trans: {4}", method.DeclaringType, method, prefix, postfix, transpiler );
         return harmony.Patch( method, ToHarmony( prefix ), ToHarmony( postfix ), ToHarmony( transpiler ) );
      }

      internal static MethodInfo TryPatch ( Type type, string method, string prefix = null, string postfix = null, string transpiler = null ) =>
         TryPatch( Method( type, method ), prefix, postfix, transpiler );

      internal static MethodInfo TryPatch ( MethodBase method, string prefix = null, string postfix = null, string transpiler = null ) { try {
         return Patch( method, prefix, postfix, transpiler );
      } catch ( Exception ex ) {
         ZySimpleMod.Log.Warn( "Could not patch {0} {1} | Pre: {2} | Post: {3} | Trans: {4}\n{5}", method?.DeclaringType, method?.Name, prefix, postfix, transpiler, ex );
         return null;
      } }

      internal static MethodInfo Unpatch ( MethodInfo orig ) { if ( orig != null ) harmony?.Unpatch( orig, HarmonyPatchType.All, harmony.Id ); return null; }

      private static HarmonyMethod ToHarmony ( string name ) {
         if ( string.IsNullOrWhiteSpace( name ) ) return null;
         return new HarmonyMethod( ZySimpleMod.PatchClass?.GetMethod( name, Public | NonPublic | Static ) ?? throw new NullReferenceException( name + " not found" ) );
      }
   }

   public class ZyLogger { // Thread safe logger.  Format in foreground and write in background thread by default.
      private TraceLevel _LogLevel = TraceLevel.Info;
      public TraceLevel LogLevel { get { lock ( buffer ) return _LogLevel; } set { lock ( buffer ) _LogLevel = value; } }
      private string _TimeFormat = "HH:mm:ss.fff ";
      public string TimeFormat { get { lock ( buffer ) return _TimeFormat; } set { lock ( buffer ) _TimeFormat = value; } }
      public uint FlushInterval { get; private set; } = 2; // Seconds.  0 to write and flush every line, a lot slower.
      public string LogPath { get; private set; }
      private readonly List< string > buffer = new List<string>();
      private System.Timers.Timer flushTimer;
      public ZyLogger ( string path, uint interval = 2 ) { new FileInfo( path ); try {
         Initialize( path );
         if ( _LogLevel == TraceLevel.Off ) return;
         if ( ( FlushInterval = Math.Min( interval, 60 ) ) > 0 ) {
            flushTimer = new System.Timers.Timer( FlushInterval * 1000 ){ AutoReset = true };
            flushTimer.Elapsed += ( _, __ ) => Flush();
            AppDomain.CurrentDomain.ProcessExit += Terminate;
         }
         buffer.Insert( 0, $"{DateTime.Now:u} {ZySimpleMod.ModName} initiated, log level {_LogLevel}, " + ( FlushInterval > 0 ? $"flush every {FlushInterval}s." : "flush on write." ) );
         Flush();
         flushTimer?.Start();
      } catch ( Exception ) { } }
      private void Initialize ( string path ) { try {
         try { File.Delete( LogPath = path ); } catch ( IOException ) { }
         var conf = Path.Combine( Path.GetDirectoryName( path ), Path.GetFileNameWithoutExtension( path ) + "-log.conf" );
         buffer.Clear();
         buffer.Add( $"Logging controlled by {conf}.  First line is log level (Off/Error/Warn/Verbose).  Second line is write interval in seconds, 0 to 60, default 2." );
         if ( ! File.Exists( conf ) ) return;
         var lines = File.ReadLines( conf ).GetEnumerator();
         if ( lines.MoveNext() ) switch ( ( ( lines.Current?.ToUpperInvariant() ?? "" ) + "?" )[0] ) {
            case 'O' : LogLevel = TraceLevel.Off; break;
            case 'E' : LogLevel = TraceLevel.Error; break;
            case 'W' : LogLevel = TraceLevel.Warning; break;
            case 'I' : LogLevel = TraceLevel.Info; break;
            case 'V' : case 'F' : LogLevel = TraceLevel.Verbose; break;
         }
         if ( lines.MoveNext() && uint.TryParse( lines.Current, out uint i ) ) FlushInterval = i;
      } catch ( Exception ) { } }
      public void Error ( object msg, params object[] arg ) => Write( TraceLevel.Error, msg, arg );
      public void Warn  ( object msg, params object[] arg ) => Write( TraceLevel.Warning, msg, arg );
      public void Info  ( object msg, params object[] arg ) => Write( TraceLevel.Info, msg, arg );
      public void Fine  ( object msg, params object[] arg ) => Write( TraceLevel.Verbose, msg, arg );
      public void Flush () { try {
         string[] buf;
         lock ( buffer ) { if ( buffer.Count == 0 ) return; buf = buffer.ToArray(); buffer.Clear(); if ( _LogLevel == TraceLevel.Off ) return; }
         using ( TextWriter f = File.AppendText( LogPath ) ) foreach ( var line in buf ) f.WriteLine( line );
      } catch ( Exception ) { } }
      private void Terminate ( object _, EventArgs __ ) { flushTimer?.Stop(); flushTimer = null; Flush(); AppDomain.CurrentDomain.ProcessExit -= Terminate; }
      private HashSet< string > knownErrors = new HashSet<string>(); // Duplicate exception are ignored.  Modding is a risky business.
      public void Write ( TraceLevel level, object msg, params object[] arg ) {
         string line = "INFO ", time;
         lock ( buffer ) { if ( level > _LogLevel ) return; time = TimeFormat; }
         switch ( level ) {
            case TraceLevel.Off : return;
            case TraceLevel.Error : line = "ERROR "; break;
            case TraceLevel.Warning : line = "WARN "; break;
            case TraceLevel.Verbose : line = "FINE "; break;
         }
         try {
            if ( msg is string txt && txt.Contains( '{' ) && arg?.Length > 0 ) msg = string.Format( msg.ToString(), arg );
            else if ( msg is Exception ) { txt = msg.ToString(); if ( knownErrors.Contains( txt ) ) return; knownErrors.Add( txt ); msg = txt; }
            else if ( arg?.Length > 0 ) msg = string.Join( ", ", new object[] { msg }.Union( arg ).Select( e => e?.ToString() ?? "null" ) );
            else msg = msg?.ToString();
            line = DateTime.Now.ToString( time ?? "mm:ss " ) + line + ( msg ?? "null" );
         } catch ( Exception e ) { // toString error, format error, stacktrace error...
            if ( msg is Exception ex ) line = msg.GetType() + ": " + ex.Message;
            else { Warn( e ); if ( msg is string txt ) line = txt; else return; }
         }
         lock ( buffer ) buffer.Add( line );
         if ( level == TraceLevel.Error || FlushInterval == 0 ) Flush();
      }
   }
}
