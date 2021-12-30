using System;
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

   [ Config( "To change log level, create new file Automochef-log.conf with the first line saying Off, Error, Warning, Info, or Verbose." ) ]
   public class ModConfig : IniConfig {
      [ Config( "\r\n[System]\r\n; Skip Unity, Hermes, Team 17, and dont-quit-when-saving screens.  Default true." ) ]
      public bool skip_intro = true;
      [ Config( "Skip 'Press Spacebar to continue'.  Default true." ) ]
      public bool skip_spacebar = true;
      [ Config( "Disable mission stats analytics.  Default true." ) ]
      public bool disable_analytics = true;

      [ Config( "\r\n[Camera]\r\n; Angle of left / right view.  Game default 35.  Set to 0 to not change." ) ]
      public float side__view_angle = 0;
      [ Config( "Downward angle of close up view.  Game default 40.  Set to 0 to not change." ) ]
      public float close_view_angle = 0;
      [ Config( "Downward angle of far view.  Game default 60.  Mode default 70.  Set to 0 to not change.  Set to 90 for a top-down view." ) ]
      public float far_view_angle = 70;
      [ Config( "Height of far camera.  Game default 20.  Set to 0 to not change." ) ]
      public float far_view_height = 0;

      [ Config( "\r\n[User Interface]\r\n; Fix food mouseover hints not showing when game is paused.  Default true." ) ]
      public bool fix_food_hint_when_paused = true;
      [ Config( "Fix wrong text in computer's visual editor.  Default true." ) ]
      public bool fix_visual_editor_text = true;
      [ Config( "Suppress yes/no confirmations - save before quit, load game, delete or overwrite save / blueprint / scenario, quit level / game, reset layout.  Default true." ) ]
      public bool suppress_confirmation = true;
      [ Config( "Show load game prompt when entering a level (if any saves).  Loading a game will bypass level goal popup and roboto speech.  Default true." ) ]
      public bool ask_loadgame_on_level_start = true;
      [ Config( "Keep save dialog open after deleting a save.  Default true." ) ]
      public bool stay_open_after_delete_save = true;
      [ Config( "Max number if options to convert dropdown to toggle button.  Default 3.  0 to disable." ) ]
      public byte dropdown_toogle_threshold = 3;
      [ Config( "Hide efficiency of tutorial levels.  Default true." ) ]
      public bool hide_tutorial_efficiency = true;

      [ Config( "\r\n[Info]\r\n; Show real-time power usage in mouseover tooltips.  Default true." ) ]
      public bool tooltip_power_usage = true;
      [ Config( "Show food freshness in mouseover tooltips.  Default true." ) ]
      public bool tooltip_freshness = true;
      [ Config( "Add efficiency calculation to kitchen log.  Default true." ) ]
      public bool efficiency_log = true;
      [ Config( "Breakdown efficiency quotas by dishes.  Default true." ) ]
      public bool efficiency_log_breakdown = true;
      [ Config( "Show top X power consuming part types in kitchen log.  Default 5.  0 to disable." ) ]
      public byte power_log_rows = 5;

      [ Config( "\r\n[Simulation]\r\n; Change game speed instantaneously.  Default true." ) ]
      public bool instant_speed_change = true;
      [ Config( "Speed of double time (two arrows).  0-100 integer.  Game default 3.  Mod default 5." ) ]
      public byte speed2 = 5;
      [ Config( "Speed of triple time (three arrows).  0-100 integer.  Game default 5.  Mod default 20.  High speed may cause some orders to expire when they would not on slower speeds." ) ]
      public byte speed3 = 20;

      [ Config( "\r\n[Mechanic]\r\n; Make sure all dishes that have this much efficiency quota for their ingredients.  Mod default 0, which fixes Double Bypass Meal.  -1 to disable.  When set to 1, this mod will ignore single ingredient recipes for better balance." ) ]
      public sbyte dish_ingredient_quota_buffer = 0;
      [ Config( "Food processor use less power when not processing.  Game default 800 (no idle mode).  Mod default -1 which does not enable idle mode.  Its belt moves at half speed so 75W can be 'realistic'." ) ]
      public int food_processor_idle_power = -1;
      [ Config( "This section changes game mechanics. They do not break or brick saves, but may allow non-vanilla solutions.\r\n; Packaging machine use less power when not packaging.  Game default 800 (no idle mode).  Mod default 60 (2x slowest belts).  Set to -1 to not change." ) ]
      public int packaging_machine_idle_power = 60;
      [ Config( "Packaging machine will pass-through foods that it is not interested in, like assemblers.  You still need to assign at least one recipe to the machine.  Default true." ) ]
      public bool packaging_machine_passthrough = true;
      [ Config( "Packaging machine's sub-recipes have lowest priority (Fries < Bacon Fries < Loaded Cheese Fries), last processed recipe have lower priority, and random for remaining ties.  Default true." ) ]
      public bool smart_packaging_machine = true;
      //[ Config( "Mechanic", "Customers who received a spoiled / infected dish will leave, removing their order, preventing Advanced Order Reader from stucking on it.  Default true." ) ]
      //public bool remove_spoiled_order = true;

      [ Config( "\r\n[Misc]\r\n; Export foods to foods.csv on game launch.  Default false.  Neglectable speed impact, disabled because most players don't need the data." ) ]
      public bool export_food_csv = false;
      [ Config( "Export hardwares to hardwares.csv on game launch.  Default false.  Ditto." ) ]
      public bool export_hardware_csv = false;
      [ Config( "Export text from current language to text-0.csv on game launch.  Default false.  Ditto." ) ]
      public bool export_text_csv = false;
      [ Config( "Change Simplified Chinese to Traditional Chinese with improved translations.  No effect on other langauges.  Default true." ) ]
      public bool traditional_chinese = true;

      [ Config( "Version of this Automodchef config file.  Don't touch!" ) ]
      public int config_version = 20211222;

      public override void Load ( object subject, string path ) {
         base.Load( subject, path );
         if ( ! ( subject is ModConfig conf ) || conf.config_version >= 20211222 ) return;
         conf.config_version = 20211222;
         Save( conf, path );
      }
   }

   internal static class ModText {
      internal static string Format ( string key, params object[] augs ) => string.Format( Get( key ), augs );
      internal static Func< string, string > Get = GetTextEn;

      internal static string GetTextZh ( string key ) { switch ( key ) {
         case "Power/Header" : return "\n\n最耗電的{0}類機器：\n";
         case "Power/wh" : return "Wh";
         case "Power/kwh" : return "kWh";
         case "Power/mwh" : return "MWh";
         case "Group/Computer"  : return "電腦";
         case "Group/Fryer" : return "油炸機";
         case "Group/DumbRobotArm" : return "機械臂";
         case "Efficiency/Ingredient" : return "食材預算 {0} / 實耗 {1} = {2:0.00}";
         case "Efficiency/Power" : return "耗電預算 {0} / 實耗 {1} = {2:0.00}";
         case "Efficiency/Formula" : return "( 平均 {0:0.00}{1} )² = 總分 {2:0.00}";
         case "Efficiency/Penalty" : return " - 0.1 任務失敗";
         case "Efficiency/Header" : return "已配送 / 訂單數 ... 獲得預算\n";
         case "Efficiency/Breakdown" : return "{0}/{1} {2} ... {3} 食材 & {4}\n";
         case "Hint/FoodFreshness" : return "\n保鮮 {0:0.0}秒";
         case "Hint/Insects" : return "\n（附近有蟲！）";
         case "Hint/DishFreshness" : return "\n保質 {0:0.0}秒";
         default: return key;
      } }

      internal static string GetTextEn ( string key ) { switch ( key ) {
         case "Power/Header" : return "\n\nTop {0} power using equipment groups:\n";
         case "Power/wh" : return "Wh";
         case "Power/kwh" : return "kWh";
         case "Power/mwh" : return "MWh";
         case "Group/Computer"  : return "Computer";
         case "Group/Fryer" : return "Fryer";
         case "Group/DumbRobotArm" : return "Robot Arm";
         case "Efficiency/Ingredient" : return "Ingredients Quota {0} / {1} Spent = {2:0.00}";
         case "Efficiency/Power" : return "Power Quota {0} / {1} Spent = {2:0.00}";
         case "Efficiency/Formula" : return "( Average {0:0.00}{1} )² = Final {2:0.00}";
         case "Efficiency/Penalty" : return " - 0.1 goal failed";
         case "Efficiency/Header" : return "Delivered / Ordered Dish ... Quota Gained\n";
         case "Efficiency/Breakdown" : return "{0}/{1} {2} ... {3} mats & {4}\n";
         case "Hint/FoodFreshness" : return "\nGreen for {0:0.0}s";
         case "Hint/Insects" : return "\n(Insects nearby)";
         case "Hint/DishFreshness" : return "\nFresh for {0:0.0}s";
         default: return key;
      } }
   }
}