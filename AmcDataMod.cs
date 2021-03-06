using I2.Loc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Analytics;
using ZyMod;
using static ZyMod.ModHelpers;

namespace Automodchef {
   using Ex = Exception;

   internal class AmcDataMod : Automodchef.ModComponent {

      internal override void Apply () { try {
         if ( conf.disable_analytics ) {
            foreach ( var m in typeof( Analytics ).Methods().Where( e => e.Name == "CustomEvent" || e.Name == "Transaction" || e.Name.StartsWith( "Send" ) ) )
               TryPatch( m, nameof( DisableAnalytics ) );
            TryPatch( typeof( AutomachefAnalytics ), "Track", nameof( DisableAnalytics ) );
         }
         if ( conf.tooltip_power_usage || conf.power_log_rows > 0 ) {
            TryPatch( typeof( KitchenPart ).Methods( "ConsumePower" ).FirstOrDefault( e => e.GetParameters().Length > 0 ), nameof( LogPowerUsage ) );
            TryPatch( typeof( PowerMeter ), "Reset", nameof( ClearPowerUsage ) );
            if ( conf.tooltip_power_usage )
               TryPatch( typeof( KitchenPart ), "GetTooltipText", postfix: nameof( AppendPowerToTooltip ) );
         }
         if ( conf.tooltip_freshness ) {
            TryPatch( typeof( PackagingMachine ), "GetTooltipTextDetails", nameof( SuppressFreshnessTooltip ), nameof( RestoreFreshnessTooltip ) );
            TryPatch( typeof( Ingredient ), "GetTooltipText", postfix: nameof( AppendFreshnessTooltip ) );
         }
         if ( conf.efficiency_log || conf.power_log_rows > 0 ) {
            TryPatch( typeof( LevelStatus ), "RenderEvents", postfix: nameof( ForceShowEfficiencyLog ) );
            if ( conf.efficiency_log ) {
               extraLog = new List<string>();
               if ( conf.efficiency_log_breakdown )
                  if ( TryPatch( typeof( EfficiencyMeter ), "Reset", nameof( ClearEfficiencyLog ) ) != null ) {
                     orderedDish = new Dictionary<object, int>();
                     cookedDish = new Dictionary<object, int>();
                     TryPatch( typeof( EfficiencyMeter ), "AddOrder", nameof( TrackOrdersEfficiency ) );
                     TryPatch( typeof( EfficiencyMeter ), "AddDeliveredDish", nameof( TrackDeliveryEfficiency ) );
                  }
               TryPatch( typeof( EfficiencyMeter ), "GetEfficiency", postfix: nameof( CalculateEfficiency ) );
               TryPatch( typeof( KitchenEventsLog ), "ToString", postfix: nameof( AppendEfficiencyLog ) );
            }
            if ( conf.power_log_rows > 0 )
               TryPatch( typeof( KitchenEventsLog ), "ToString", postfix: nameof( AppendPowerLog ) );
         }
         if ( conf.dish_ingredient_quota_buffer >= 0 )
            TryPatch( typeof( SplashScreen ), "Awake", postfix: nameof( FixDishIngredientQuota ) );
         if ( conf.export_food_csv )
            TryPatch( typeof( SplashScreen ), "Awake", nameof( DumpFoodCsv ) );
         if ( conf.export_hardware_csv )
            TryPatch( typeof( SplashScreen ), "Awake", nameof( DumpHardwareCsv ) );
         if ( conf.export_text_csv )
            TryPatch( typeof( LocalizationManager ), "LocalizeAll", postfix: nameof( DumpLanguageCsv ) );
      } catch ( Ex x ) { Err( x ); } }

      private static bool DisableAnalytics () { Info( "Analytics Blocked" ); return false; }

      #region Power
      private class PowerLog { internal float power; }
      private static ConditionalWeakTable< KitchenPart, PowerLog > powerLog;

      private static void LogPowerUsage ( KitchenPart __instance, float multiplier ) { try {
         if ( ! Initializer.GetInstance().IsSimRunning() || powerLog == null ) return;
         powerLog.GetOrCreateValue( __instance ).power += __instance.powerInWatts * multiplier;
      } catch ( Ex x ) { Err( x ); } }

      private static void ClearPowerUsage () { Fine( "Reset power log" ); powerLog = new ConditionalWeakTable<KitchenPart, PowerLog>(); }

      private static void AppendPowerToTooltip ( KitchenPart __instance, ref string __result ) { try {
         if ( ! Initializer.GetInstance().IsSimRunning() || __instance.powerInWatts <= 0 || powerLog == null ) return;
         powerLog.TryGetValue( __instance, out PowerLog log );
         __result += $"\n{PowerMeter.GetInstance().GetLastPowerUsage( __instance )}W >> {kWh(log?.power??0)}";
      } catch ( Ex x ) { Err( x ); } }

      private static void AppendPowerLog ( ref string __result ) { try {
         if ( powerLog == null || ( conf.hide_tutorial_efficiency && IsTutorial() ) ) return;
         Info( "Appending power log (up to {0} lines) to kitchen log.", conf.power_log_rows );
         float total = 0;
         Dictionary< string, PowerLog > byType = new Dictionary<string, PowerLog>();
         HashSet<KitchenPart> allParts = Initializer.GetInstance().kitchen.GetAllKitchenParts();
         foreach ( var part in allParts ) {
            if ( ! powerLog.TryGetValue( part, out PowerLog partPower ) || partPower.power <= 0 ) continue;
            var key = part.internalName;
            switch ( key ) {
               case "AdvancedAssembler"   : key = "Assembler"; break;
               case "AdvancedComputer"    : key = "Computer"; break;
               case "AdvancedOrderReader" : key = "OrderReader"; break;
               case "BeltBridge" : case "HighSpeedBelt" : case "Gate" : key = "Belt"; break;
               case "ConvectionFryer"     : key = "Fryer"; break;
               case "ConveyorGrill"       : key = "Grill"; break;
               case "HighSpeedDispenser"  : key = "Dispenser"; break;
               case "LargeStorageUnit"    : key = "StorageUnit"; break;
               case "LongRobotArm" : case "RobotArm" : case "StackingRobotArm" : key = "DumbRobotArm"; break;
            }
            if ( byType.TryGetValue( key, out PowerLog log ) ) log.power += partPower.power;
            else byType.Add( key, new PowerLog { power = partPower.power } );
            total += partPower.power;
         }
         Info( "Found {0} parts in {1} groups totaling {2}", allParts.Count, byType.Count, kWh( total ) );
         __result += ModText.Format( "Power/Header", Math.Min( allParts.Count, conf.power_log_rows ) ) + string.Join( "\n",
            byType.OrderByDescending( e => e.Value.power ).Take( conf.power_log_rows ).Select( e =>
               string.Format( "{0} ... {1} ({2:0.0}%)", GetPartTypeName( e.Key ), kWh( e.Value.power ), e.Value.power * 100 / total ) ) );
         __result = __result.Trim();
         Fine( __result );
      } catch ( Ex x ) { Err( x ); } }

      private static string GetPartTypeName ( string type ) {
         var txt = ModText.Get( "Group/" + type );
         return txt == $"Group/{type}" ? AutomachefResources.KitchenParts.CreateNewInstance( type ).partName.ToString() : txt;
      }
      #endregion

      #region Freshness
      private static bool simpleTooltip;
      private static void SuppressFreshnessTooltip () => simpleTooltip = true;
      private static void RestoreFreshnessTooltip  () => simpleTooltip = false;

      private static void AppendFreshnessTooltip ( Ingredient __instance, ref string __result ) { try {
            Initializer init = Initializer.GetInstance();
         if ( simpleTooltip ) { __result = __instance.GetFriendlyNameTranslated(); return; }
         if ( ! init.IsSimRunning() || __instance.HasGoneBad() || __instance.name.StartsWith( "Insects" ) ) return;
         if ( init.levelManager.GetLevel().hasInsectsDisaster && __instance.GetInsectTime() > 0 ) {
            var part = __instance.currentNode?.kitchenPart;
            if ( part != null ) {
               if ( ! init.levelManager.PartsWithInsects.Contains( part ) ) {
                  float timer = __instance.GetInsectTime(), spawn = part.GetComponent< InsectsSpawner >()?.stationaryTimeBeforeSpawning ?? 0;
                  if ( timer != 0 && spawn != 0 ) __result += ModText.Format( "Hint/FoodFreshness", spawn-timer );
               } else
                  __result += ModText.Get( "Hint/Insects" );
            }
         }
         if ( __instance is Dish dish ) {
            float spoil = dish.timeToBeSpoiled - __instance.GetAge();
            if ( spoil > 0 ) __result += ModText.Format( "Hint/DishFreshness", spoil );
         }
      } catch ( Ex x ) { Err( x ); } }
      #endregion

      #region Efficiency Log
      private static Dictionary< object, int > orderedDish, cookedDish;
      private static void ClearEfficiencyLog () { orderedDish.Clear(); cookedDish.Clear(); }
      private static void TrackOrdersEfficiency ( Dish dish ) => EfficiencyMeter_Dish_Log( orderedDish, dish );
      private static void TrackDeliveryEfficiency ( Dish dish ) => EfficiencyMeter_Dish_Log( cookedDish, dish );
      private static void EfficiencyMeter_Dish_Log ( Dictionary< object, int > log, Dish dish ) {
         log.TryGetValue( dish, out int i );
         log[ dish ] = i + 1;
      }

      private static List<string> extraLog;

      private static void CalculateEfficiency ( bool allGoalsFulfilled, int __result, int ___expectedIngredientsUsage, int ___expectedPowerUsage ) { try {
         float iUsed = IngredientsCounter.GetInstance().GetUsedIngredients(), pUsed = PowerMeter.GetInstance().GetWattsHour();
         float iMark = Mathf.Clamp01( ___expectedIngredientsUsage / iUsed ), pMark = Mathf.Clamp01( ___expectedPowerUsage / pUsed );
         float mark = ( iMark + pMark ) / 2f;
         extraLog.Clear();
         extraLog.Add( ModText.Format( "Efficiency/Ingredient", ___expectedIngredientsUsage, iUsed, iMark ) );
         extraLog.Add( ModText.Format( "Efficiency/Power", Wh( ___expectedPowerUsage ), Wh( pUsed ), pMark ) );
         extraLog.Add( ModText.Format( "Efficiency/Formula", mark, allGoalsFulfilled ? "" : ModText.Get( "Efficiency/Penalty" ), __result ) );
      } catch ( Ex x ) { Err( x ); } }

      // Show modded logs even when kitchen has no events
      private static void ForceShowEfficiencyLog ( LevelStatus __instance, KitchenEventsLog log ) { try {
         if ( log.GetEventsCount() <= 0 && ( ! conf.hide_tutorial_efficiency || ! IsTutorial() ) )
            __instance.eventsLogTextField.text = log.ToString();
      } catch ( Ex x ) { Err( x ); } }

      private static void AppendEfficiencyLog ( ref string __result ) { try {
         if ( extraLog.Count == 0 || ( conf.hide_tutorial_efficiency && IsTutorial() ) ) return;
         Info( "Appending efficiency log ({0} lines) to kitchen log.", extraLog.Count + orderedDish?.Count );
         __result += "\n\n";
         if ( orderedDish?.Count > 0 ) {
            __result += ModText.Get( "Efficiency/Header" );
            foreach ( var key in orderedDish.Keys ) {
               var dish = key as Dish;
               cookedDish.TryGetValue( dish, out int delivered );
               int eI = dish.expectedIngredients, eP = dish.expectedPower, ordered = orderedDish[ dish ], missed = ordered - delivered;
               int missedPowerPenalty = ( dish.expectedPower - dish.expectedPower / 3 ) * missed;
               //__result += $"Quota\\{dish.friendlyName} = {eI} mats & {eP}Wh each\n";
               //__result += $"  {ordered-missed}/{ordered} done = {eI*ordered-missed} mats & {eP*ordered-missedPowerPenalty}Wh\n";
               __result += ModText.Format( "Efficiency/Breakdown", ordered-missed, ordered, dish.friendlyName, eI*ordered-missed, Wh( eP*ordered-missedPowerPenalty ) );
            }
         }
         __result += "\n" + string.Join( "\n", extraLog.ToArray() );
         __result = __result.Trim();
         Fine( __result );
      } catch ( Ex x ) { Err( x ); } }

      private static string kWh ( float wh ) {
         var power = CachedData.fixedDeltaTime == 0 ? wh : CachedData.fixedDeltaTime * wh / 3600f;
         var unit = "Power/wh";
         if ( power >= 1000 ) { power /= 1000f; unit = "Power/kwh"; }
         if ( power >= 1000 ) { power /= 1000f; unit = "Power/mwh"; }
         return $"{power:0.00}" + ModText.Get( unit );
      }

      private static string Wh ( float wh ) => wh + ModText.Get( "Power/wh" );
      #endregion

      #region Bug fixes
      private static int FindDishMinIngredient ( Dish dish ) { try {
         return dish.recipe.Select( id => Ingredient.GetByInternalName( id ) ?? Dish.GetByInternalName( id ) ).Sum( e => e is Dish d ? FindDishMinIngredient( d ) : 1 );
      } catch ( Ex x ) { return Err( x, dish?.recipe?.Count ?? 0 ); } }

      private static void FixDishIngredientQuota () { try {
         var updated = false;
         foreach ( var dish in Dish.GetAll() ) {
            var i = FindDishMinIngredient( dish ) + conf.dish_ingredient_quota_buffer;
            if ( i == 2 && conf.dish_ingredient_quota_buffer == 1 ) i = 1;
            if ( i > dish.expectedIngredients ) {
               Info( "Bumping {0} ingredient quota from {1} to {2}.", dish.GetFriendlyNameTranslated(), dish.expectedIngredients, i );
               dish.expectedIngredients = i;
               updated = true;
            }
         }
         if ( updated && conf.dish_ingredient_quota_buffer == 1 ) Info( "Dishes made from single ingredient are not buffed for game balance." );
      } catch ( Ex x ) { Err( x ); } }
      #endregion

      #region Csv dump
      private static bool foodDumped, hardwareDumped, textDumped;

      private static void DumpFoodCsv () { if ( foodDumped ) return; try {
         string file = Path.Combine( RootMod.AppDataDir, "foods.csv" );
         Info( "Exporting food list to {0}", file, foodDumped = true );
         var line = new StringBuilder( 24 * 1024 ).AppendCsvLine( "Id", "Name", "Process", "Seconds", "Recipe", "Liquids",
                "Processed", "Grilled", "Fried", "Steamed", "Baked", "Wet", //"Bacterias",
                "Spoil (sec)", "Ingredients Quota", "Power Quota" );
         foreach ( var mat in Ingredient.GetAll().Union( Dish.GetAll() ) ) {
            Fine( "FOOD {0} = {1}", mat.internalName, mat.friendlyName );
            float spoil = 0, iQ = 0, pQ = 0;
            if ( mat is Dish dish ) { spoil = dish.timeToBeSpoiled;  iQ = dish.expectedIngredients;  pQ = dish.expectedPower; }
            line.AppendCsvLine( mat.internalName, mat.friendlyName, mat.technique.ToString(), mat.timeToBeAssembled + "",
               mat.recipe == null ? "" : string.Join( " + ", mat.recipe ),
               mat.recipeLiquidIngredients == null ? "" : string.Join( " + ", mat.recipeLiquidIngredients ),
               mat.resultProcess, mat.resultGrill, mat.resultFry, mat.resultSteam, mat.resultBake, mat.resultWet,
               //mat.bacteria == null ? "" : string.Join( " & ", mat.bacteria.Select( e => e.friendlyName ) ),
               spoil == 0 ? "" : spoil.ToString(), iQ == 0 ? "" : iQ.ToString(), pQ == 0 ? "" : pQ.ToString()
               );
         }
         var data = line.ToString();
         line.Clear();
         Task.Run( () => { try { File.WriteAllText( file, data, Encoding.UTF8 ); Info( "Food list exported" ); } catch ( Exception ) { } } );
      } catch ( Ex x ) { Err( x ); } }

      private static void DumpHardwareCsv () { if ( hardwareDumped ) return; try {
         string file = Path.Combine( RootMod.AppDataDir, "hardwares.csv" );
         Info( "Exporting hardware list to {0}", file, hardwareDumped = true );
         var line = new StringBuilder( 12 * 1024 ).AppendCsvLine( "Id", "Name", "Description", "Category", "Price", "Power", "Speed", "Time", "Variant", "Code Class" );
         foreach ( var part in AutomachefResources.KitchenParts.GetList_ReadOnly() ) {
            Fine( "HW {0} = {1}", part.internalName, part.partName );
            var speed  = part.GetType().Field( "speed" )?.GetValue( part );
            var rspeed = part.GetType().Field( "rotationSpeed" )?.GetValue( part ) ??  part.GetType().Field( "armRotationSpeed" )?.GetValue( part );
            var pTime  = part.GetType().Field( "timeToProcess" )?.GetValue( part );
            line.AppendCsvLine( part.internalName, part.partName, part.description, part.category, part.cost, part.powerInWatts,
                  speed ?? rspeed ?? "", pTime ?? "", part.nextVariantInternalName, part.GetType().FullName );
         }
         var data = line.ToString();
         line.Clear();
         Task.Run( () => { try { File.WriteAllText( file, data, Encoding.UTF8 ); Info( "Hardware list exported" ); } catch ( Exception ) { } } );
      } catch ( Ex x ) { Err( x ); } }

      private static void DumpLanguageCsv ( List<LanguageSource> ___Sources ) { if ( textDumped || ___Sources == null ) return; try {
         for ( var i = 0 ; i < ___Sources.Count ; i++ ) {
            var file = Path.Combine( RootMod.AppDataDir, "text" + ( i == 0 ? "" : $"{i}" ) + ".csv" );
            Info( "Exporting game text to {0}", file, textDumped = true );
            var data = ___Sources[ i ].Export_CSV( null );
            Task.Run( () => { try { File.WriteAllText( file, data, Encoding.UTF8 ); Info( "{0} game text exported", ___Sources[0].GetTermsList().Count ); } catch ( Exception ) { } } );
         }
      } catch ( Ex x ) { Err( x ); } }
      #endregion
   }
}