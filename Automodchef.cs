using System.IO;
using System.Reflection;
using ZyMod;

namespace Automodchef {

   public class Automodchef : RootMod {

      public static ModConfig Config { get; } = new ModConfig();

      public static void Main () => new Automodchef().Initialize();

      protected override string GetAppDataDir () {
         var path = System.Environment.GetFolderPath( System.Environment.SpecialFolder.LocalApplicationData );
         if ( string.IsNullOrEmpty( path ) ) return null;
         return Path.Combine( Directory.GetParent( path ).FullName, "LocalLow", "HermesInteractive", "Automachef" );
      }

      protected override void OnGameAssemblyLoaded ( Assembly game ) {
         Config.Load();
         new AmcDataMod().Apply();
         new AmcMechanicMod().Apply();
         new AmcUserInterfaceMod().Apply();
      }

      internal abstract class ModComponent : Patcher {
         protected static ModConfig conf => Config;
         internal abstract void Apply();
         protected static bool IsTutorial () => IsTutorial( Initializer.GetInstance().levelManager?.GetLevel() );
         protected static bool IsTutorial ( Level lv ) => lv == null || lv.IsTutorial() || lv.IsOptionalTutorial();
      }
   }

   public class ModConfig : IniConfig {
      [ ConfigAttribute( "To change log level, create new file Automochef-log.conf with the first line saying Off, Error, Warning, Info, or Verbose.\r\n; Version of this Automodchef config file.  Don't touch!" ) ]
      public int config_version = 20211206;

      [ ConfigAttribute( "System", "Skip Unity, Hermes, Team 17, and Autosave screens.  Default true." ) ]
      public bool skip_intro = true;
      [ ConfigAttribute( "System", "Skip 'Press Spacebar to continue'.  Default true." ) ]
      public bool skip_spacebar = true;
      [ ConfigAttribute( "System", "Disable mission stats analytics.  Default true." ) ]
      public bool disable_analytics = true;

      [ ConfigAttribute( "Camera", "Angle of left / right view.  Game default 35.  Set to 0 to not change." ) ]
      public float side__view_angle = 0;
      [ ConfigAttribute( "Camera", "Downward angle of close up view.  Game default 40.  Set to 0 to not change." ) ]
      public float close_view_angle = 0;
      [ ConfigAttribute( "Camera", "Downward angle of far view.  Game default 60.  Mode default 70.  Set to 0 to not change.  Set to 90 for a top-down view." ) ]
      public float far_view_angle = 70;
      [ ConfigAttribute( "Camera", "Height of far camera.  Game default 20.  Set to 0 to not change." ) ]
      public float far_view_height = 0;

      [ ConfigAttribute( "User Interface", "Fix food mouseover hints not showing when game is paused.  Default true." ) ]
      public bool fix_food_hint_when_paused = true;
      [ ConfigAttribute( "User Interface", "Suppress yes/no confirmations - save before quit, load game, delete or overwrite save / blueprint / scenario, quit level / game, reset layout.  Default true." ) ]
      public bool suppress_confirmation = true;
      [ ConfigAttribute( "User Interface", "Show load game prompt when entering a level (if any saves).  Loading a game will bypass level goal popup and roboto speech.  Default true." ) ]
      public bool ask_loadgame_on_level_start = true;
      [ ConfigAttribute( "User Interface", "Keep save dialog open after deleting a save.  Default true." ) ]
      public bool stay_open_after_delete_save = true;
      [ ConfigAttribute( "User Interface", "Max number if options to convert dropdown to toggle button.  Default 3.  0 to disable." ) ]
      public byte dropdown_toogle_threshold = 3;
      [ ConfigAttribute( "User Interface", "Hide efficiency of tutorial levels.  Default true." ) ]
      public bool hide_tutorial_efficiency = true;

      [ ConfigAttribute( "Info", "Show real-time power usage in mouseover tooltips.  Default true." ) ]
      public bool tooltip_power_usage = true;
      [ ConfigAttribute( "Info", "Show food freshness in mouseover tooltips.  Default true." ) ]
      public bool tooltip_freshness = true;
      [ ConfigAttribute( "Info", "Add efficiency calculation to kitchen log.  Default true." ) ]
      public bool efficiency_log = true;
      [ ConfigAttribute( "Info", "Breakdown efficiency quotas by dishes.  Default true." ) ]
      public bool efficiency_log_breakdown = true;
      [ ConfigAttribute( "Info", "Show top X power consuming part types in kitchen log.  Default 5.  0 to disable." ) ]
      public byte power_log_rows = 5;

      [ ConfigAttribute( "Simulation", "Change game speed instantaneously.  Default true." ) ]
      public bool instant_speed_change = true;
      [ ConfigAttribute( "Simulation", "Speed of double time (two arrows).  0-100 integer.  Game default 3.  Mod default 5." ) ]
      public byte speed2 = 5;
      [ ConfigAttribute( "Simulation", "Speed of triple time (three arrows).  0-100 integer.  Game default 5.  Mod default 20.  High speed may cause some orders to expire when they would not on slower speeds." ) ]
      public byte speed3 = 20;

      [ ConfigAttribute( "Mechanic", "Make sure all dishes that have this much efficiency quota for their ingredients.  Game is inconsistent.  Mod default 1.  0 to fix Double Bypass Meal.  -1 to disable.  When set to 1, this mod will not apply buffer to single ingredient recipes for better balance." ) ]
      public sbyte dish_ingredient_quota_buffer = 1;
      [ ConfigAttribute( "Mechanic", "Food processor use less power when not processing.  Game default 800 (no idle mode).  Mod default -1 which does not enable idle mode.  Its belt moves at half speed so 75W can be 'realistic'." ) ]
      public int food_processor_idle_power = -1;
      [ ConfigAttribute( "Mechanic", "This section changes game mechanics. They do not break or brick saves, but may allow non-vanilla solutions.\r\n; Packaging machine use less power when not packaging.  Game default 800 (no idle mode).  Mod default 60 (2x slowest belts).  Set to -1 to not change." ) ]
      public int packaging_machine_idle_power = 60;
      [ ConfigAttribute( "Mechanic", "Packaging machine will pass-through foods that it is not interested in, like assemblers.  You still need to assign at least one recipe to the machine.  Default true." ) ]
      public bool packaging_machine_passthrough = true;
      [ ConfigAttribute( "Mechanic", "Packaging machine's sub-recipes have lowest priority (Fries < Bacon Fries < Loaded Cheese Fries), last processed recipe have lower priority, and random for remaining ties.  Default true." ) ]
      public bool smart_packaging_machine = true;

      [ ConfigAttribute( "Misc", "Export foods to foods.csv on game launch.  Default false.  Neglectable speed impact, disabled because most players don't need the data." ) ]
      public bool export_food_csv = false;
      [ ConfigAttribute( "Misc", "Export hardwares to hardwares.csv on game launch.  Default false.  Ditto." ) ]
      public bool export_hardware_csv = false;
      [ ConfigAttribute( "Misc", "Export text from current language to text-0.csv on game launch.  Default false.  Ditto." ) ]
      public bool export_text_csv = false;
      [ ConfigAttribute( "Misc", "Change Simplified Chinese to Traditional Chinese with improved translations.  No effect on other langauges." ) ]
      public bool traditional_chinese = true;

      public override void Load ( string path = "" ) {
         base.Load( path );
         if ( config_version >= 20211206 ) return;
         config_version = 20211206;
         Save( path );
      }
   }

}