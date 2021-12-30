using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using static System.Reflection.BindingFlags;
using static HarmonyLib.HarmonyPatchType;

// Sheepy's "Universal" skeleton mod and tools!  No depency other than Harmony / HarmonyX.
// Everything I need.  Bootstrap, Background Logging, Roundtrip Config, Reflection, Manual Patcher with Unpatch.  Enjoy!
namespace ZyMod {
   public abstract class RootMod {
      protected static object sync = new object();
      internal static RootMod instance;
      public static ZyLogger Log { get; private set; }
      internal static string ModName { get { lock ( sync ) return instance?.GetModName() ?? "ZyMod"; } }

      private static bool ignoreAssembly ( Assembly asm ) => asm.IsDynamic || asm.FullName.StartsWith( "DMDASM." ) || asm.FullName.StartsWith( "HarmonyDTFAssembly" );
      private static bool isTargetAssembly ( Assembly asm ) => asm.FullName.StartsWith( "Assembly-CSharp," );

      public void Initialize () {
         lock ( sync ) { if ( instance != null ) { Log.Warn( "Mod already initialized" ); return; } instance = this; }
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
         var asm = args.LoadedAssembly;
         if ( ignoreAssembly( asm ) ) return;
         Log.Fine( "DLL {0}, {1}", asm.FullName, asm.CodeBase );
         if ( ! isTargetAssembly( asm ) ) return;
         if ( Log.LogLevel <= TraceLevel.Info ) AppDomain.CurrentDomain.AssemblyLoad -= AsmLoaded;
         GameLoaded( asm );
      }

      private void GameLoaded ( Assembly asm ) { try {
         Log.Info( "Target assembly loaded." );
         OnGameAssemblyLoaded( asm );
         var patches = new Harmony( ModName ).GetPatchedMethods().Select( e => Harmony.GetPatchInfo( e ) );
         Log.Info( "Bootstrap complete." + ( patches.Any() ? "  Patched {0} methods with {1} patches." : "" ),
            patches.Count(), patches.Sum( e => e.Prefixes.Count + e.Postfixes.Count + e.Transpilers.Count ) );
      } catch ( Exception ex ) { Log.Error( ex ); } }

      private static string _AppDataDir;
      public static string AppDataDir { get {
         if ( _AppDataDir != null ) return _AppDataDir;
         lock ( sync ) { if ( instance == null ) return ""; _AppDataDir = instance.GetAppDataDir(); }
         try {
            if ( ! Directory.Exists( _AppDataDir ) ) {
               Directory.CreateDirectory( _AppDataDir );
               if ( ! Directory.Exists( _AppDataDir ) ) _AppDataDir = "";
            }
         } catch ( Exception ) { _AppDataDir = ""; }
         return _AppDataDir;
      } }

      // Override / Implement these to change mod name, log dir, what to do on Assembly-CSharp, and where patches are located by Modder.
      protected virtual string GetModName () => GetType().Name;
      protected abstract string GetAppDataDir (); // Called once on start.  At most once per thread.  Result will be cached.
      protected abstract void OnGameAssemblyLoaded ( Assembly game ); // Put all the actions here.
   }

   public static class ModHelpers { // Log and reflection helpers.  Reinventing the wheels.  But then Log4J JNDI Injection exploded during development of this code.  No whistles and bells thanks.
      public static void Err ( object msg ) => Error( msg );
      public static T Err < T > ( object msg, T val ) { Error( msg ); return val; }
      public static void Error ( object msg, params object[] arg ) => RootMod.Log?.Error( msg, arg );
      public static void Warn  ( object msg, params object[] arg ) => RootMod.Log?.Warn ( msg, arg );
      public static void Info  ( object msg, params object[] arg ) => RootMod.Log?.Info ( msg, arg );
      public static void Fine  ( object msg, params object[] arg ) => RootMod.Log?.Fine ( msg, arg );
      public static bool Non0 ( float val ) => val != 0 && ! float.IsNaN( val ) && ! float.IsInfinity( val );
      public static bool IsFound ( string path ) { if ( File.Exists( path ) ) return true; Warn( "Not Found: {0}", path ); return false; }
      public static bool IsFound ( string path, out string found ) { found = path; return IsFound( path ); }

      public static string ModPath => new Uri( Assembly.GetExecutingAssembly().CodeBase ).LocalPath;
      public static string ModDir => Path.GetDirectoryName( ModPath );

      public static IEnumerable< MethodInfo > Methods ( this Type type ) => type.GetMethods( Public | NonPublic | Instance | Static ).Where( e => ! e.IsAbstract );
      public static IEnumerable< MethodInfo > Methods ( this Type type, string name ) => type.Methods().Where( e => e.Name == name );

      public static MethodInfo Method ( this Type type, string name ) => type?.GetMethod( name, Public | NonPublic | Instance | Static );
      public static MethodInfo TryMethod ( this Type type, string name ) { try { return Method( type, name ); } catch ( Exception ) { return null; } }
      public static FieldInfo  Field ( this Type type, string name ) => type?.GetField( name, Public | NonPublic | Instance | Static );
      public static PropertyInfo Property ( this Type type, string name ) => type?.GetProperty( name, Public | NonPublic | Instance | Static );

      public static bool TryParse ( Type valueType, string val, out object parsed, bool logWarnings = true ) { parsed = null; try {
         if ( valueType == typeof( string ) ) { parsed = val; return false; }
         if ( string.IsNullOrWhiteSpace( val ) || val == "null" ) return ! ( valueType.IsValueType || valueType.IsEnum );
         switch ( valueType.FullName ) {
            case "System.SByte"   : if ( SByte .TryParse( val, out sbyte  bval ) ) parsed = bval; break;
            case "System.Int16"   : if ( Int16 .TryParse( val, out short  sval ) ) parsed = sval; break;
            case "System.Int32"   : if ( Int32 .TryParse( val, out int    ival ) ) parsed = ival; break;
            case "System.Int64"   : if ( Int64 .TryParse( val, out long   lval ) ) parsed = lval; break;
            case "System.Byte"    : if ( Byte  .TryParse( val, out byte   Bval ) ) parsed = Bval; break;
            case "System.UInt16"  : if ( UInt16.TryParse( val, out ushort Sval ) ) parsed = Sval; break;
            case "System.UInt32"  : if ( UInt32.TryParse( val, out uint   Ival ) ) parsed = Ival; break;
            case "System.UInt64"  : if ( UInt64.TryParse( val, out ulong  Lval ) ) parsed = Lval; break;
            case "System.Single"  : if ( Single.TryParse( val, out float  fval ) ) parsed = fval; break;
            case "System.Double"  : if ( Double.TryParse( val, out double dval ) ) parsed = dval; break;
            case "System.Boolean" : switch ( val.ToLowerInvariant() ) {
                                    case "true" : case "yes" : case "1" : parsed = true ; break;
                                    case "false" : case "no" : case "0" : parsed = false; break;
                                    } break;
            case "System.DateTime" :
               if ( DateTime.TryParse( val, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime dt ) ) parsed = dt;
               break;
            default :
               if ( valueType.IsEnum ) { parsed = Enum.Parse( valueType, val ); break; }
               if ( logWarnings ) Warn( new NotImplementedException( "Unsupported field type " + valueType.FullName ) );
               break;
         }
         return parsed != null;
      } catch ( ArgumentException ) { if ( logWarnings ) Warn( "Invalid value for {0}: {1}", valueType.FullName, val ); return false; } }

      public static void WriteCsvLine ( this TextWriter tw, params object[] values ) => tw.Write( new StringBuilder().AppendCsvLine( values ).Append( "\r\n" ) );

      public static StringBuilder AppendCsvLine ( this StringBuilder buf, params object[] values ) {
         if ( buf.Length > 0 ) buf.Append( "\r\n" );
         foreach ( var val in values ) {
            string v = val?.ToString() ?? "null";
            if ( v.IndexOfAny( new char[] { ',', '"', '\n', '\r' } ) >= 0 ) buf.Append( '"' ).Append( v.Replace( "\"", "\"\"" ) ).Append( "\"," );
            else buf.Append( v ).Append( ',' );
         }
         --buf.Length;
         return buf;
      }

      /* Try read a csv row from StreamReader or StringReader. Linebreaks will be converted to \n */
      public static bool TryReadCsvRow ( this TextReader source, out IEnumerable<string> row, StringBuilder quoteBuffer = null )
         => ( row = ReadCsvRow( source, quoteBuffer ) ) != null;

      /* Read a csv row from StreamReader or StringReader. Return null if no more rows. Linebreaks will be converted to \n */
      public static IEnumerable<string> ReadCsvRow ( this TextReader source, StringBuilder quoteBuffer = null ) {
         var line = source.ReadLine();
         return line == null ? null : ReadCsvCells( source, line, quoteBuffer );
      }

      private static IEnumerable<string> ReadCsvCells ( TextReader source, string line, StringBuilder buf ) {
         for ( var pos = 0 ; line?.Length >= pos ; ) yield return ReadCsvCell( source, ref line, ref pos, buf );
      }

      private static string ReadCsvCell ( TextReader source, ref string line, ref int pos, StringBuilder buf ) {
         var len = line.Length;
         if ( pos >= len ) { pos = len + 1; return ""; }
         if ( line[ pos ] != '"' ) { // Unquoted cell.
            int end = line.IndexOf( ',', pos ), head = pos;
            if ( end < 0 ) { pos = len + 1; return line.Substring( head ); }
            if ( end == pos ) { pos++; return ""; }
            pos = end + 1;
            return line.Substring( head, end - head );
         }
         if ( buf == null ) buf = new StringBuilder(); else buf.Clear();
         var start = ++pos; // Drop initial quote.
         while ( true ) {
            int end = pos < len ? line.IndexOf( '"', pos ) : -1, next = end + 1;
            if ( end < 0 ) { // End of line.  Append and read next line.
               buf.Append( line, start, len - start ).Append( "\n" );
               if ( ( line = source.ReadLine() ) == null ) return buf.ToString();
               start = pos = 0; len = line.Length;
            } else if ( next == len || line[ next ] == ',' ) { // End of cell.
               pos = end + 2;
               return buf.Append( line, start, end - start ).ToString();
            } else if ( line[ next ] == '"' ) { // Double quote.
               buf.Append( line, start, end - start + 1 );
               pos = start = end + 2;
            } else // Single quote not followed by EOL or comma.
               pos++;
         }
      }

      /* Dump unity components to log. *
      public static void DumpComponents ( UnityEngine.GameObject e ) => DumpComponents( Info, "", new HashSet<object>(), e );
      public static void DumpComponents ( Action< object, object[] > output, UnityEngine.GameObject e ) => DumpComponents( output, "", new HashSet<object>(), e );
      internal static void DumpComponents ( Action< object, object[] > output, string prefix, HashSet<object> logged, UnityEngine.GameObject e ) {
         if ( prefix.Length > 10 ) return;
         if ( e == null || logged.Contains( e ) ) return;
         logged.Add( e );
         Dump( output, "{0}- '{1}'{2} {3}{4}{5}{6} :{7}", prefix, e.name, ToTag( e.tag ), FindText( e ), TypeName( e ),
            e.activeSelf ? "" : " (Inactive)", e.layer == 0 ? "" : $" Layer {e.layer}", ToString( e.GetComponent<UnityEngine.Transform>() ) );
         if ( prefix.Length == 0 )
            foreach ( var c in e.GetComponents<UnityEngine.Component>() ) try {
               var typeName = TypeName( c );
               if ( c is UnityEngine.Transform cRect ) ;
               else if ( c is UnityEngine.UI.Text txt ) Dump( output, "{0}...{1} {2} {3}", prefix, typeName, txt.color, txt.text );
               else if ( c is I2.Loc.Localize loc ) Dump( output, "{0}...{1} {2}", prefix, typeName, loc.mTerm );
               else if ( c is UnityEngine.UI.LayoutGroup layout ) Dump( output, "{0}...{1} Padding {2}", prefix, typeName, layout.padding );
               else Dump( output, "{0}...{1}", prefix, typeName );
            } catch ( Exception ) { }
         for ( int i = 0 ; i < e.transform.childCount ; i++ )
            DumpComponents( output, prefix + "  ", logged, e.transform?.GetChild( i )?.gameObject );
      }
      private static void Dump ( Action< object, object[] > output, object msg, params object[] augs ) => output( msg, augs );
      private static string TypeName ( object c ) => c?.GetType().FullName.Replace( "UnityEngine.", "UE." ).Replace( "UE.GameObject", "" );
      private static string ToTag ( string tag ) => "Untagged".Equals( tag ) ? "" : $":{tag}";
      private static string FindText ( UnityEngine.GameObject obj ) { var text = obj.GetComponent< UnityEngine.UI.Text >()?.text; return text == null ? "" : $"\"{text}\" "; }
      private static string ToString ( UnityEngine.Transform t ) {
         if ( t == null ) return "";
         var result = string.Format( "Pos {0} Scale {1} Rotate {2}", t.localPosition, t.localScale, t.localRotation );
         return " " + result.Replace( ".0,", "," ).Replace( ".0)", ")" ).Replace( "Pos (0, 0, 0)", "" ).Replace( "Scale (1, 1, 1)", "" ).Replace( "Rotate (0, 0, 0, 1)", "" ).Trim();
      }
      /**/
   }

   public abstract class BaseConfig { // Abstract code to load and save simple config object to text-based file.  By default only process public instant fields, may be filtered by attributes.
      protected virtual string GetFileExtension () => ".conf";
      public virtual string GetDefaultPath () => Path.Combine( RootMod.AppDataDir, RootMod.ModName + GetFileExtension() );

      public void Load () => Load( this );
      public void Load ( string path ) => Load( this, path );
      public void Load ( object subject ) => Load( subject, GetDefaultPath() );
      public virtual void Load ( object subject, string path ) { try {
         if ( ! File.Exists( path ) ) {
            Save( subject, path );
         } else {
            _Log( TraceLevel.Info, "Loading {0} into {1}", path, new object[]{ subject.GetType().FullName } );
            _ReadFile( subject, path );
         }
         foreach ( var prop in GetType().GetFields() ) _Log( TraceLevel.Info, "Config {0} = {1}", prop.Name, prop.GetValue( this ) );
      } catch ( Exception ex ) {  _Log( TraceLevel.Warning, ex ); } }

      protected abstract void _ReadFile ( object subject, string path );
      protected virtual bool _ReadField ( object subject, string name, out FieldInfo field ) {
         field = subject.GetType().GetField( name );
         if ( field == null ) _Log( TraceLevel.Warning, "Unknown field: {0}", field ); // Legacy fields are expected to be kept in config class as [Obsolete].
         return field != null;
      }
      protected virtual void _LoadField ( object subject, FieldInfo f, string val ) {
         if ( ModHelpers.TryParse( f.FieldType, val, out object parsed ) ) f.SetValue( subject, parsed );
      }

      public void Save () => Save( this );
      public void Save ( string path ) => Save( this, path );
      public void Save ( object subject ) => Save( subject, GetDefaultPath() );
      public virtual void Save ( object subject, string path ) { try {
         var type = subject.GetType();
         _Log( TraceLevel.Info, "Creating {0} from {1}", path, type.FullName );
         using ( TextWriter tw = File.CreateText( path ) ) {
            var attr = type.GetCustomAttribute<ConfigAttribute>();
            var comment = ! string.IsNullOrWhiteSpace( attr?.Comment ) ? attr.Comment : null;
            _WriteData( tw, subject, type, subject, comment );
            foreach ( var f in _LoadFields( subject ) ) {
               comment = ( attr = f.GetCustomAttribute<ConfigAttribute>() ) != null && ! string.IsNullOrWhiteSpace( attr.Comment ) ? attr.Comment : null;
               _WriteData( tw, subject, f, f.GetValue( subject ), comment );
            }
            _WriteData( tw, subject, type, subject, "" );
         }
         if ( File.Exists( path ) ) _Log( TraceLevel.Verbose, "{0} bytes written", new FileInfo( path ).Length );
         else _Log( TraceLevel.Warning, "Config file not written." );
      } catch ( Exception ex ) { _Log( TraceLevel.Warning, "Cannot create config file: {0}", ex ); } }

      protected virtual IEnumerable<FieldInfo> _LoadFields ( object subject ) {
         var fields = subject.GetType().GetFields();
         if ( fields.Any( e => e.GetCustomAttribute<ConfigAttribute>() != null ) ) // If any field has ConfigAttribute, save only these fields.
            fields = fields.Where( e => e.GetCustomAttribute<ConfigAttribute>() != null ).ToArray();
         return fields.Where( e => ! e.IsStatic && e.GetCustomAttribute<ObsoleteAttribute>() == null );
      }
      protected abstract void _WriteData ( TextWriter f, object subject, MemberInfo target, object value, string comment );
      protected virtual void _Log ( TraceLevel level, object msg, params object[] arg ) => RootMod.Log.Write( level, msg, arg );
   }

   public class CsvConfig : BaseConfig { // Load and save CSV to and from a config object.
      protected override string GetFileExtension () => ".csv";
      protected override void _ReadFile ( object subject, string path ) {
         var rowCount = 0;
         var buf = new StringBuilder();
         using ( var reader = new StreamReader( path, true ) ) while ( reader.TryReadCsvRow( out var row, buf ) ) {
            var cells = row.ToArray();
            if ( ++rowCount == 1 && cells.Length == 3 && cells[0] == "Config" && cells[1] == "Value" ) continue;
            if ( cells.Length < 2 || string.IsNullOrWhiteSpace( cells[0] ) || ! _ReadField( subject, cells[0].Trim(), out var field ) ) continue;
            _LoadField( subject, field, cells[1] ?? "" );
         }
      }
      protected override void _WriteData ( TextWriter f, object subject, MemberInfo target, object value, string comment ) {
         if ( target is Type ) { if ( comment != "" ) f.WriteCsvLine( "Config", "Value", comment ?? "Comment" ); }
         else f.WriteCsvLine( target.Name, value, comment ?? "" );
      }
   }

   public class IniConfig : BaseConfig { // Load and save INI to and from a config object.  Same field handling as CsvConfig.
      protected override string GetFileExtension () => ".ini";
      protected override void _ReadFile ( object subject, string path ) {
         foreach ( var line in File.ReadAllLines( path ) ) {
            var split = line.Split( new char[]{ '=' }, 2 );
            if ( split.Length != 2 || line.StartsWith( ";" ) || ! _ReadField( subject, split[ 0 ].Trim(), out var field ) ) continue;
            var val = split[1].Trim();
            if ( val.Length > 1 && val.StartsWith( "\"" ) && val.EndsWith( "\"" ) ) val = val.Substring( 1, val.Length - 2 );
            _LoadField( subject, field, val );
         }
      }
      protected override void _WriteData ( TextWriter f, object subject, MemberInfo target, object value, string comment ) {
         if ( ! string.IsNullOrEmpty( comment ) ) f.Write( comment.Substring( 0, 1 ).IndexOfAny( new char[]{ '[', ';', '\r', '\n' } ) != 0 ? $"; {comment}\r\n" : $"{comment}\r\n" );
         if ( ! ( target is Type ) ) f.Write( $"{target.Name} = {value}\r\n" );
      }
   }

   [ AttributeUsage( AttributeTargets.Class | AttributeTargets.Field | AttributeTargets.Property ) ]
   public class ConfigAttribute : Attribute { // Slap this on config attributes for auto-doc.
      public ConfigAttribute () {}
      public ConfigAttribute ( string comment ) { Comment = comment; }
      public string Comment;
   }

   public class Patcher { // Patch classes may inherit from this class for manual patching.  Or you can use Harmony.PatchAll, of course.
      public Harmony harmony { get; private set; }

      public class ModPatch {
         public readonly Harmony harmony;
         public ModPatch ( Harmony patcher ) { harmony = patcher; }
         public MethodBase original; public HarmonyMethod prefix, postfix, transpiler;
         public void Unpatch ( HarmonyPatchType type = All ) {
            if ( prefix     != null && ( type == All || type == Prefix     ) ) { harmony.Unpatch( original, prefix.method     ); prefix     = null; }
            if ( postfix    != null && ( type == All || type == Postfix    ) ) { harmony.Unpatch( original, postfix.method    ); postfix    = null; }
            if ( transpiler != null && ( type == All || type == Transpiler ) ) { harmony.Unpatch( original, transpiler.method ); transpiler = null; }
         }
      };

      protected ModPatch Patch ( Type type, string method, string prefix = null, string postfix = null, string transpiler = null ) =>
         Patch( type.Method( method ), prefix, postfix, transpiler );
      protected ModPatch Patch ( MethodBase method, string prefix = null, string postfix = null, string transpiler = null ) {
         if ( harmony == null ) harmony = new Harmony( RootMod.ModName );
         RootMod.Log.Fine( "Patching {0} {1} | Pre: {2} | Post: {3} | Trans: {4}", method.DeclaringType, method, prefix, postfix, transpiler );
         var patch = new ModPatch( harmony ) { original = method, prefix = ToHarmony( prefix ), postfix = ToHarmony( postfix ), transpiler = ToHarmony( transpiler ) };
         harmony.Patch( method, patch.prefix, patch.postfix, patch.transpiler );
         return patch;
      }

      protected ModPatch TryPatch ( Type type, string method, string prefix = null, string postfix = null, string transpiler = null ) =>
         TryPatch( type.Method( method ), prefix, postfix, transpiler );
      protected ModPatch TryPatch ( MethodBase method, string prefix = null, string postfix = null, string transpiler = null ) { try {
         return Patch( method, prefix, postfix, transpiler );
      } catch ( Exception ex ) {
         ModHelpers.Warn( "Could not patch {0} {1} | Pre: {2} | Post: {3} | Trans: {4}\n{5}", method?.DeclaringType, method?.Name, prefix, postfix, transpiler, ex );
         return null;
      } }

      protected void UnpatchAll () { harmony?.UnpatchSelf(); }
      protected MethodInfo UnpatchAll ( MethodInfo orig ) { if ( orig != null ) harmony?.Unpatch( orig, All, harmony.Id ); return null; }

      protected HarmonyMethod ToHarmony ( string name ) {
         if ( string.IsNullOrWhiteSpace( name ) ) return null;
         return new HarmonyMethod( GetType().GetMethod( name, Public | NonPublic | Static ) ?? throw new NullReferenceException( name + " not found" ) );
      }
   }

   // Thread safe logger.  Buffer and write in background thread by default.
   // Common usage: Log an Exception (will ignore duplicates), Log a formatted string with params, or log multiple objects (in one call and on one line).
   public class ZyLogger {
      private TraceLevel _LogLevel = TraceLevel.Info;
      public TraceLevel LogLevel {
         get { lock ( buffer ) return _LogLevel; }
         set { lock ( buffer ) { _LogLevel = value;
                  if ( value == TraceLevel.Off ) { flushTimer?.Stop(); buffer.Clear(); }
                  else flushTimer?.Start(); }  } }
      private string _TimeFormat = "HH:mm:ss.fff ";
      public string TimeFormat { get { lock ( buffer ) return _TimeFormat; } set { lock ( buffer ) _TimeFormat = value; } }
      public uint FlushInterval { get; private set; } = 2; // Seconds.  0 to write and flush every line, a lot slower.
      public string LogPath { get; private set; }

      private readonly List< string > buffer = new List<string>();
      private System.Timers.Timer flushTimer;

      public ZyLogger ( string path, uint interval = 2 ) { new FileInfo( path ); try {
         Initialize( path );
         if ( ( FlushInterval = Math.Min( interval, 60 ) ) > 0 ) {
            flushTimer = new System.Timers.Timer( FlushInterval * 1000 ){ AutoReset = true };
            flushTimer.Elapsed += ( _, __ ) => Flush();
            AppDomain.CurrentDomain.ProcessExit += Terminate;
         }
         if ( _LogLevel == TraceLevel.Off ) return;
         buffer.Insert( 0, $"{DateTime.Now:u} {RootMod.ModName} initiated, log level {_LogLevel}, " + ( FlushInterval > 0 ? $"refresh every {FlushInterval}s." : "no buffer." ) );
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
         lock ( buffer ) { if ( buffer.Count == 0 || _LogLevel == TraceLevel.Off ) return; buf = buffer.ToArray(); buffer.Clear(); }
         using ( TextWriter f = File.AppendText( LogPath ) ) foreach ( var line in buf ) f.WriteLine( line );
      } catch ( Exception ) { } }

      private void Terminate ( object _, EventArgs __ ) { Flush(); LogLevel = TraceLevel.Off; AppDomain.CurrentDomain.ProcessExit -= Terminate; }

      private HashSet< string > knownErrors = new HashSet<string>(); // Duplicate exception are ignored.  Modding is risky.

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
            for ( var i = arg.Length - 1 ; i >= 0 ; i-- ) if ( arg[i] is Func<string> f ) arg[i] = f();
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
