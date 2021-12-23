using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;
using static ZyMod.ModHelpers;

namespace Automodchef {
   using Ex = Exception;

   internal class AmcMechanicMod : Automodchef.ModComponent {

      internal override void Apply () { try {
         if ( conf.instant_speed_change )
            TryPatch( typeof( Initializer ), "Update", postfix: nameof( InstantGameSpeedUpdate ) );
         if ( conf.speed2 != 3 || conf.speed3 != 5 )
            TryPatch( typeof( Initializer ), "Start", nameof( AdjustGameSpeedPresets ) );
         if ( conf.food_processor_idle_power >= 0 )
            TryPatch( typeof( Processor ), "FixedUpdate", nameof( SetFoodProcessorPower ) );
         if ( conf.packaging_machine_idle_power >= 0 )
            TryPatch( typeof( PackagingMachine ), "FixedUpdate", nameof( SetPackagingMachinePower ) );
         if ( conf.packaging_machine_passthrough )
            TryPatch( typeof( PackagingMachine ), "FixedUpdate", nameof( PackagingMachinePassThrough ) );
         if ( conf.smart_packaging_machine ) {
            packMachineCanMake = typeof( PackagingMachine ).Method( "AllIngredientsReady" );
            packMachineConsume = typeof( PackagingMachine ).Method( "ConsumeIngredients" );
            packMachinePackage = typeof( PackagingMachine ).Method( "StartPackaging" );
            packMachineRandom  = new System.Random();
            packMachineLastDish = new ConditionalWeakTable<PackagingMachine, Dish>();
            TryPatch( typeof( KitchenPart ), "Reset", postfix: nameof( ClearPackagingMachineLastDish ) );
            TryPatch( typeof( PackagingMachine ), "StartPackaging", nameof( LogPackagingMachineLastDish ) );
            TryPatch( typeof( PackagingMachine ), "SeeIfSomethingCanBePackaged", nameof( OverridePackagingMachineLogic ) );
         }
         /*
         if ( conf.remove_spoiled_order ) {
            TryPatch( typeof( FoodDisplayCase ), "ManualFixedUpdate", nameof( TrackDisplayCase ) );
            TryPatch( typeof( OrderQueue ), "ContainsRequestFor", nameof( TrackOrderSource ) );
            TryPatch( typeof( Ingredient ), "HasGoneBad", nameof( TrackDisplayCaseFood ) );
            TryPatch( typeof( LevelManager ), "OnAcceptedSpoiledDish", nameof( RemoveSpoiledOrder ) );
            TryPatch( typeof( LevelManager ), "OnAcceptedInfectedDish", nameof( RemoveSpoiledOrder ) );
         }
         */
      } catch ( Ex x ) { Err( x ); } }

      private static void InstantGameSpeedUpdate ( float ___targetTimeScale ) { try {
         if ( Time.timeScale != ___targetTimeScale ) Time.timeScale = ___targetTimeScale;
      } catch ( Ex x ) { Err( x ); } }

      private static void AdjustGameSpeedPresets ( Initializer __instance ) { try {
         if ( __instance == null || __instance.speeds == null || __instance.speeds.Count < 4 ) return;
         Info( "Setting game speeds to [ {0}x, {1}x, {2}x, {3}x ]", __instance.speeds[0], __instance.speeds[1], __instance.speeds[2], __instance.speeds[3] );
         __instance.speeds[2] = conf.speed2;
         __instance.speeds[3] = conf.speed3;
      } catch ( Ex x ) { Err( x ); } }

      #region Packaging Machine and Food Processor
      private static float fullFPpower, fullPMpower;
      private static void SetFoodProcessorPower ( Processor __instance, float ___processingTime ) =>
         SetIdlePower( __instance, "Food Processor", ___processingTime > 0, ref fullFPpower, conf.food_processor_idle_power );
      private static void SetPackagingMachinePower ( PackagingMachine __instance, bool ___packaging ) =>
         SetIdlePower( __instance, "Packaging Machine", ___packaging, ref fullPMpower, conf.packaging_machine_idle_power );

      private static void SetIdlePower ( KitchenPart part, string name, bool isBusy, ref float fullPower, float idlePower ) { try {
         if ( fullPower == 0 ) {
            fullPower = part.powerInWatts;
            Info( "{0} power: {1}W when idle, {2}W when busy.", name, idlePower, fullPower );
         }
         part.powerInWatts = isBusy ? fullPower : idlePower;
      } catch ( Ex x ) { Err( x ); } }

      private static void PackagingMachinePassThrough ( PackagingMachine __instance, List<Ingredient> ___ingredientsInside, KitchenPart.NodeData[] ___ourIngredientNodes ) { try {
         KitchenPart.NodeData exitNode = ___ourIngredientNodes[ 3 ]; // 3 is hardcoded in the game.
         if ( exitNode.ingredientExists ) return;
         PartStatus status = __instance.GetPartStatus();
         if ( status == PartStatus.Off || status == PartStatus.Working ) return;
         foreach ( var i in ___ingredientsInside ) {
            var id = i.internalName;
            foreach ( var d in __instance.dishesToAssemble )
               if ( Dish.GetByInternalName( d ).recipe.Contains( id ) ) return;
            Fine( "Packaging Machine is passing through {0}", i.GetFriendlyNameTranslated() );
            ___ingredientsInside.Remove( i );
            i.SetPosition( exitNode.position );
            __instance.TransferToNode( i, exitNode );
            return;
         }
      } catch ( Ex x ) { Err( x ); } }

      private static MethodInfo packMachineCanMake, packMachineConsume, packMachinePackage;
      private static System.Random packMachineRandom;
      private static ConditionalWeakTable< PackagingMachine, Dish > packMachineLastDish;

      private static void ClearPackagingMachineLastDish ( PackagingMachine __instance ) => packMachineLastDish?.Remove( __instance );
      private static void LogPackagingMachineLastDish ( PackagingMachine __instance, Dish dishToPrepare ) { try {
         packMachineLastDish.Remove( __instance );
         packMachineLastDish.Add( __instance, dishToPrepare );
      } catch ( Ex x ) { Err( x ); } }

      private static bool OverridePackagingMachineLogic ( PackagingMachine __instance, List<Ingredient> ___ingredientsInside ) { try {
         if ( ( ___ingredientsInside?.Count ?? 0 ) == 0 ) return false;
         HashSet<Dish> canMake = new HashSet<Dish>( __instance.dishesToAssemble.Select( Dish.GetByInternalName )
            .Where( e => (bool) packMachineCanMake.Invoke( __instance, new object[]{ e } ) ) );
         if ( canMake.Count == 0 ) return false;
         if ( canMake.Count > 1 ) {
            Info( "Packaging options: " + string.Join( ", ", canMake.Select( e => e.GetFriendlyNameTranslated() ) ) );
            foreach ( var dishA in canMake.ToArray() ) foreach ( var dishB in canMake ) {
               if ( dishA == dishB || dishA.recipe.Count >= dishB.recipe.Count ) continue;
               if ( dishA.recipe.Any( i => ! dishB.recipe.Contains( i ) ) ) continue;
               Fine( "Delisting {0} as a sub-recipe of {1}", dishA.GetFriendlyNameTranslated(), dishB.GetFriendlyNameTranslated() );
               canMake.Remove( dishA );
               break;
            }
            if ( canMake.Count > 1 && packMachineLastDish.TryGetValue( __instance, out Dish lastDish ) && canMake.Contains( lastDish ) ) {
               Fine( "Delisting last dish {0}", lastDish.GetFriendlyNameTranslated() );
               canMake.Remove( lastDish );
            }
            Info( canMake.Count > 1 ? "Randomly pick from reminders" : $"Winner: {canMake.ElementAt(0).GetFriendlyNameTranslated()}" );
         }
         var dish = canMake.ElementAt( canMake.Count == 1 ? 0 : packMachineRandom.Next( canMake.Count ) );
         if ( (bool) packMachineConsume.Invoke( __instance, new object[] { dish } ) ) packMachinePackage.Invoke( __instance, new object[] { dish } );
         return false;
      } catch ( Ex x ) { return Err( x, true ); } }
      #endregion

      #region Remove spoiled order
      /* No time to test more features.  Xmas is coming!
      private static FoodDisplayCase displayCase;
      private static List<Ingredient> dishesThatCanFulfillOrders;
	  private static Dictionary<object, List<Order>> ordersForPeople, ordersForCars;
      private static Ingredient deliveredFood;
      private static void TrackDisplayCase ( FoodDisplayCase __instance, List<Ingredient> ___dishesThatCanFulfillOrders ) { displayCase = __instance; dishesThatCanFulfillOrders = ___dishesThatCanFulfillOrders; }
      private static void TrackOrderSource ( Dictionary<object, List<Order>> ___ordersForPeople, Dictionary<object, List<Order>> ___ordersForCars  ) { ordersForPeople = ___ordersForPeople; ordersForCars = ___ordersForCars; }
      private static void TrackDisplayCaseFood ( Ingredient __instance ) => deliveredFood = __instance;

      private static void RemoveSpoiledOrder ( string dishName ) { try {
         Fine( "Spoiled / Infected {0} delivered", dishName );
         Fine( displayCase, dishesThatCanFulfillOrders, ordersForCars, ordersForCars, deliveredFood );
         if ( displayCase == null || ordersForPeople == null || deliveredFood == null ) return;
         dishesThatCanFulfillOrders.Remove( deliveredFood );
         // OrderQueue.FulfillOrder, without success logic
         List<Order> orders = Initializer.GetInstance().levelManager.orderQueue.GetOrdersFromSource( displayCase.sourceToFulfill );
         if ( orders == null || orders.Count < 1 ) return;
         var order = orders.Find( e => e.dish.internalName == deliveredFood.internalName );
         if ( order == null || ( displayCase.sourceToFulfill == OrderSource.DriveThru && order != orders[0] ) ) return;
         Info( "Removing {0} {1} order for {2}", deliveredFood.HasGoneBad() ? "spoiled" : "infected", deliveredFood.friendlyName, displayCase.sourceToFulfill );
         orders.Remove( order );
         foreach ( var key in ordersForPeople ) ordersForPeople[ key ].Remove( order );
         foreach ( var key in ordersForCars ) ordersForCars[ key ].Remove( order );
      } catch ( Ex x ) { Err( x ); } }
      */
      #endregion
   }
}