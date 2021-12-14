using I2.Loc;
using MaterialUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UI;
using ZyMod;
using static ZyMod.ModHelpers;
using static I2.Loc.ScriptLocalization.Warnings;

namespace Automodchef {
   using Ex = Exception;

   internal class AmcUserInterfaceMod : ModComponent {

      private void Apply () { try {
         if ( Non0( config.side__view_angle ) || Non0( config.close_view_angle ) || Non0( config.far_view_angle ) || Non0( config.far_view_height ) )
            TryPatch( typeof( CameraMovement ), "Awake", postfix: nameof( OverrideCameraSettings ) );
         if ( config.suppress_confirmation ) {
            var orig = typeof( DialogManager ).Methods( "ShowAlert" ).FirstOrDefault( e => e.GetParameters().Length == 7 );
            if ( orig != null ) TryPatch( orig, nameof( SuppressConfirmation ) );
         }
         if ( config.ask_loadgame_on_level_start ) {
            TryPatch( typeof( LevelManager ), "Start", postfix: nameof( SetNewLevelTrigger ) );
            TryPatch( typeof( SaveLoad ), "Close", nameof( RestorePreLevelScreen ) );
            TryPatch( typeof( SaveLoadManager ), "DeleteSave", nameof( ClearPreLevelFlag ) );
            TryPatch( typeof( SaveLoadManager ), "LoadAndBuildKitchen", nameof( ClearPreLevelFlag ) );
         }
         if ( config.stay_open_after_delete_save ) {
            TryPatch( typeof( SaveLoadManager ), "DeleteSave", nameof( DisableNextSaveLoadClose ) );
            TryPatch( typeof( SaveLoad ), "Close", nameof( CheckSaveLoadCloseDisabled ) );
         }
         if ( config.hide_tutorial_efficiency ) {
            TryPatch( typeof( LevelSelection ), "InitializeLevelList", postfix: nameof( HideTutorialMaxEfficiency ) );
            TryPatch( typeof( LevelStatus ), "RenderStats", postfix: nameof( HideTutorialEfficiencyStat ) );
         }
         if ( config.dropdown_toogle_threshold > 1 ) {
            dropdownIcon = new ConditionalWeakTable< MaterialDropdown, DropdownIcon >();
            TryPatch( typeof( PartProperties ), "PopulateDropdownForProperty", nameof( TrackDropdownIcon ) );
            TryPatch( typeof( MaterialDropdown ), "ShowDropdown", nameof( ToggleDropdown ) );
         }
      } catch ( Ex x ) { Err( x ); } }

      private static void OverrideCameraSettings ( ref float ___wideAngle, ref float ___wideHeight, ref float ___teleAngle, ref float ___isometricAngle ) {
         if ( Non0( config.side__view_angle ) ) ___isometricAngle = config.side__view_angle;
         if ( Non0( config.close_view_angle ) ) ___teleAngle  = config.close_view_angle;
         if ( Non0( config.far_view_angle   ) ) ___wideAngle  = config.far_view_angle;
         if ( Non0( config.far_view_height  ) ) ___wideHeight = config.far_view_height;
         Info( "Camera settigns applied." );
         // Default camera settings:
         // edgePanMarginMouse = 10
         // edgePanMarginController = 80
         // wideFoV = 32
         // teleFoV = 16
         // wideAngle = 60
         // teleAngle = 40
         // isometricAngle = 35
         // wideHeight = 20
         // zoomSpeed = 60
         // panSpeed = 5
         // modeChangeSpeed = 150
      }

      #region Pre-level load dialogue
      private static LevelManager currentLevel;
      private static bool lastPreLevelScreenState;

      private static void SetNewLevelTrigger ( LevelManager __instance, SaveLoadManager ___m_saveLoadManager ) { try {
         if ( IsTutorial( __instance.GetLevel() ) ) return;
         Fine( "Entering new non-tutorial level." );
         currentLevel = __instance;
         ___m_saveLoadManager.OnMetadataReady += OfferToLoadGameOnEnter;
      } catch ( Ex x ) { Err( x ); } }

      private static void OfferToLoadGameOnEnter () { try {
         Fine( "New level data loaded." );
         var data = SaveLoadManager.GetInstance().getSavedKitchenData();
         if ( data == null ) { Info( "Save data not found, aborting." ); return; }
		 for ( var i = 0; i < 5; i++ )
			if ( data.Get( i ) != null ) {
               Info( "Found saved level at slot {0}, offering load dialog.", i+1 );
               lastPreLevelScreenState = currentLevel.levelStatusUI.preLevelInfoRoot.gameObject.activeSelf;
               currentLevel.levelStatusUI.preLevelScreen.gameObject.SetActive( false );
               Initializer.GetInstance().saveLoadPanel.Open( false );
               return;
            }
         Info( "No saved level found.  Skipping load dialog." );
      } catch ( Ex x ) { Err( x ); } }

      private static void RestorePreLevelScreen () { try {
         if ( ! lastPreLevelScreenState ) return;
         Fine( "Restoring level brief screen." );
         lastPreLevelScreenState = false;
         currentLevel.levelStatusUI.preLevelScreen.gameObject.SetActive( true );
      } catch ( Ex ex ) { Error( ex ); } }

      private static void ClearPreLevelFlag () {
         Fine( "Save loaded or deleted.  Suppressing pre-level screen." );
         lastPreLevelScreenState = false;
         currentLevel?.levelStatusUI.preLevelScreen.gameObject.SetActive( false );
      }
      #endregion

      #region Save/Load Deletion
      private static bool bypassNextSaveLoad;
      private static void DisableNextSaveLoadClose ( ref Action OnDeleted ) {
         if ( OnDeleted != null ) return;
         bypassNextSaveLoad = true;
         OnDeleted = () => typeof( SaveLoad ).TryMethod( "RebuildList" )?.Invoke( Initializer.GetInstance().saveLoadPanel, null );
      }
      private static bool CheckSaveLoadCloseDisabled () => ( ! bypassNextSaveLoad ) || ( bypassNextSaveLoad = false );
      #endregion

      #region Dropdown Toggle
      private static ConditionalWeakTable< MaterialDropdown, DropdownIcon > dropdownIcon;

      private static void TrackDropdownIcon ( MaterialDropdown dropdown, KitchenPartProperty prop, DropdownIcon icon ) { try {
         Fine( "Tracking dropdown {0} icon for kitchen part prop {1}", dropdown.GetHashCode(), prop.name );
         dropdownIcon.Remove( dropdown ); if ( icon != null ) dropdownIcon.Add( dropdown, icon );
      } catch ( Ex x ) { Err( x ); } }

      private static bool ToggleDropdown ( MaterialDropdown __instance, ref int ___m_CurrentlySelected, OptionDataList ___m_OptionDataList ) { try {
         //if ( IsTutorial() ) return true;
         int max_options = ___m_OptionDataList?.options.Count ?? 0, new_selection = ___m_CurrentlySelected + 1;
         if ( max_options <= 1 || max_options > config.dropdown_toogle_threshold ) return true;
         Fine( "Toggle dropdown {0} of from {1} to {2} (or 0 if {3})", __instance.GetHashCode(), ___m_CurrentlySelected, new_selection, max_options );
         __instance.Select( new_selection >= max_options ? 0 : new_selection );
         if ( dropdownIcon.TryGetValue( __instance, out DropdownIcon icon ) ) icon?.UpdateIcon();
         return false;
      } catch ( Ex x ) { return Err( x, true ); } }
      #endregion

      private static void HideTutorialMaxEfficiency ( List<Level> ___levelsList, Dictionary<string, GameObject> ___levelObjectMapping ) { try {
         foreach ( var level in ___levelsList.Where( IsTutorial ) ) {
            var text = ___levelObjectMapping[ level.number ].GetComponent<LevelSelectionItem>()?.maxEfficiency;
            if ( text?.gameObject.activeSelf != true ) return;
            text.text = ScriptLocalization.Main_UI.Success;
            Fine( $"Hiding efficiency of tutorial level {0}", level.number );
         }
      } catch ( Ex x ) { Err( x ); } }

      private static void HideTutorialEfficiencyStat ( Text ___statsEfficiencyValueUI ) { try {
         if ( IsTutorial() ) ___statsEfficiencyValueUI.text = "n/a";
      } catch ( Ex x ) { Err( x ); } }

      private static bool SuppressConfirmation ( string bodyText, Action onAffirmativeButtonClicked, Action onDismissiveButtonClicked ) { try {
         foreach ( var msg in new string[] { About_To_Load_Game, Bonus_Level, Delete_Blueprint_Confirmation, Delete_Game,
                  Overwrite_Game, Overwrite_Blueprint, Quit_Confirmation, Quit_Confirmation_In_Game, Reset_Kitchen } )
            if ( bodyText == msg ) {
               Info( "Assuming yes for {0}", bodyText );
               onAffirmativeButtonClicked();
               return false;
            }
         if ( bodyText == Save_Before_Quitting ) {
            Info( "Assuming no for {0}", bodyText );
            onDismissiveButtonClicked();
            return false;
         }
         return true;
      } catch ( Ex x ) { return Err( x, true ); } }
   }
}