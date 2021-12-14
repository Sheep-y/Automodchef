using I2.Loc;
using MaterialUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UI;
using static ZyMod.ModHelpers;
using static I2.Loc.ScriptLocalization.Warnings;
using System.Runtime.InteropServices;

namespace Automodchef {
   using Ex = Exception;

   internal class AmcUserInterfaceMod : Automodchef.ModComponent {
      private static AmcUserInterfaceMod instance;

      internal void Apply () { try {
         instance = this;
         if ( conf.skip_intro )
            TryPatch( typeof( FaderUIController ), "Awake", nameof( SkipVideoSplashes ) );
         if ( conf.skip_spacebar )
            TryPatch( typeof( SplashScreen ), "Update", postfix: nameof( SkipSpacebarSplash ) );
         if ( Non0( conf.side__view_angle ) || Non0( conf.close_view_angle ) || Non0( conf.far_view_angle ) || Non0( conf.far_view_height ) )
            TryPatch( typeof( CameraMovement ), "Awake", postfix: nameof( OverrideCameraSettings ) );
         if ( conf.suppress_confirmation ) {
            var orig = typeof( DialogManager ).Methods( "ShowAlert" ).FirstOrDefault( e => e.GetParameters().Length == 7 );
            if ( orig != null ) TryPatch( orig, nameof( SuppressConfirmation ) );
         }
         if ( conf.ask_loadgame_on_level_start ) {
            TryPatch( typeof( LevelManager ), "Start", postfix: nameof( SetNewLevelTrigger ) );
            TryPatch( typeof( SaveLoad ), "Close", nameof( RestorePreLevelScreen ) );
            TryPatch( typeof( SaveLoadManager ), "DeleteSave", nameof( ClearPreLevelFlag ) );
            TryPatch( typeof( SaveLoadManager ), "LoadAndBuildKitchen", nameof( ClearPreLevelFlag ) );
         }
         if ( conf.stay_open_after_delete_save ) {
            TryPatch( typeof( SaveLoadManager ), "DeleteSave", nameof( DisableNextSaveLoadClose ) );
            TryPatch( typeof( SaveLoad ), "Close", nameof( CheckSaveLoadCloseDisabled ) );
         }
         if ( conf.hide_tutorial_efficiency ) {
            TryPatch( typeof( LevelSelection ), "InitializeLevelList", postfix: nameof( HideTutorialMaxEfficiency ) );
            TryPatch( typeof( LevelStatus ), "RenderStats", postfix: nameof( HideTutorialEfficiencyStat ) );
         }
         if ( conf.dropdown_toogle_threshold > 1 ) {
            dropdownIcon = new ConditionalWeakTable< MaterialDropdown, DropdownIcon >();
            TryPatch( typeof( PartProperties ), "PopulateDropdownForProperty", nameof( TrackDropdownIcon ) );
            TryPatch( typeof( MaterialDropdown ), "ShowDropdown", nameof( ToggleDropdown ) );
         }
         if ( conf.fix_food_hint_when_paused )
            TryPatch( typeof( IngredientTooltip ), "Update", postfix: nameof( FixIngredientHintOnPause ) );
         if ( conf.traditional_chinese ) {
            TryPatch( typeof( LanguageSelectionScreen ), "OnShown", nameof( ShowZht ) );
            TryPatch( typeof( LocalizationManager ), "CreateCultureForCode", nameof( DetectZh ) );
         }
      } catch ( Ex x ) { Err( x ); } }

      #region Skip Splash
      private static void SkipVideoSplashes ( ref FaderUIController.SplashStateInfo[] ___m_SplashStates ) { try {
         if ( ___m_SplashStates == null || ___m_SplashStates.Length <= 1 ) return;
         if ( ! ___m_SplashStates.Any( e => e.m_AnimToPlay == "LoadStartScreen" ) ) return;
         ___m_SplashStates = ___m_SplashStates.Where( e => e.m_AnimToPlay == "LoadStartScreen" ).ToArray();
         ___m_SplashStates[0].m_TimeInState = 0.01f;
         Info( "Skipping Logos and Warnings." );
         // [ { "Unity, 1, False }, { "HermesInteractive, 2, False }, { "Team 17, 4, True }, { "Legal, 3, False }, { "LoadStartScreen", 2, False } ]
      } catch ( Ex x ) { Err( x ); } }

      private static void SkipSpacebarSplash ( SplashScreen __instance, ref bool ___m_bProcessedCloseRequest ) { try {
         if ( ___m_bProcessedCloseRequest || InputWrapper.GetController() == null ) return;
         ___m_bProcessedCloseRequest = true;
         typeof( SplashScreen ).TryMethod( "ProceedToMainMenu" )?.Invoke( __instance, Array.Empty<object>() );
         Info( "Skipped Press Space Splash." );
      } catch ( Ex ex ) {
         Error( ex );
         ___m_bProcessedCloseRequest = false;
      } }
      #endregion


      private static void OverrideCameraSettings ( ref float ___wideAngle, ref float ___wideHeight, ref float ___teleAngle, ref float ___isometricAngle ) {
         if ( Non0( conf.side__view_angle ) ) ___isometricAngle = conf.side__view_angle;
         if ( Non0( conf.close_view_angle ) ) ___teleAngle  = conf.close_view_angle;
         if ( Non0( conf.far_view_angle   ) ) ___wideAngle  = conf.far_view_angle;
         if ( Non0( conf.far_view_height  ) ) ___wideHeight = conf.far_view_height;
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
         if ( max_options <= 1 || max_options > conf.dropdown_toogle_threshold ) return true;
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

      private static void FixIngredientHintOnPause ( IngredientTooltip __instance, RectTransform ___ourRectTransform ) { try {
         if ( __instance.canvasGroup.alpha != 0 || ! Initializer.GetInstance().IsSimRunning() ) return;
         if ( __instance.kitchenBuilder.IsSomethingBeingPlaced() || InputWrapper.IsPointerOverUI( -1 ) ) return;
         var ray = Camera.main.ScreenPointToRay( InputWrapper.mousePosition );
         if ( ! Physics.Raycast( ray, out RaycastHit raycastHit, 50 ) ) return;
         var food = raycastHit.transform.gameObject.GetComponent<Ingredient>();
         if ( food == null ) return;
         ___ourRectTransform.anchoredPosition = Camera.main.WorldToScreenPoint( raycastHit.transform.position ) / __instance.GetComponentInParent<Canvas>().scaleFactor;
         __instance.tooltipText.text = typeof( IngredientTooltip ).Method( "FormatText" ).Invoke( __instance, new object[]{ food.GetTooltipText() } ).ToString();
         __instance.canvasGroup.alpha = 1f;
      } catch ( Ex x ) { Err( x ); } }

      #region Traditional Chinese.  Hooray for Taiwan, Hong Kong, Macau!
      private static void ShowZht ( List<string> ___languageNames ) { try {
         for ( var i = ___languageNames.Count - 1 ; i >= 0 ; i-- )
            if ( ___languageNames[ i ] == "简体中文" ) {
               ___languageNames[ i ] = "中文";
               return;
            }
      } catch ( Ex x ) { Err( x ); } }

      private static void DetectZh ( string code ) { try {
         Info( "Game language set to {0}", code );
         if ( code != "zh" ) return;
         zhs2zht = new Dictionary< string, string >();
         instance.TryPatch( typeof( LanguageSource ), "TryGetTranslation", postfix: nameof( ToZht ) );
      } catch ( Ex x ) { Err( x ); } }

      private static Dictionary< string, string > zhs2zht;

      private static void ToZht ( string term, ref string Translation, bool __result ) { if ( ! __result ) return; try {
         if ( zhs2zht.TryGetValue( term, out string zht ) ) { Translation = zht; return; }
         zht = new String( ' ', Translation.Length );
         LCMapString( LOCALE_SYSTEM_DEFAULT, LCMAP_TRADITIONAL_CHINESE, Translation, Translation.Length, zht, zht.Length );
         Fine( "ZH {0} ({1} chars)", term, ( zht = FixZht( zht ) ).Length );
         zhs2zht.Add( term, Translation = zht );
      } catch ( Ex x ) { Err( x ); } }

      private static string FixZht ( string zht ) {
         zht = zht.Replace( "任務目標", "任務" ).Replace( "菜肴", "餐點" ).Replace( "美食評論家", "食評家" )
            .Replace( "已上餐點", "上菜" ).Replace( "電力消耗", "耗電" ).Replace( "使用的食材", "食材" );
         return zht;
      }

      [ DllImport( "kernel32", CharSet = CharSet.Unicode, SetLastError = true ) ]
      private static extern int LCMapString ( int Locale, int dwMapFlags, string lpSrcStr, int cchSrc, [Out] string lpDestStr, int cchDest );
      private const int LOCALE_SYSTEM_DEFAULT = 0x0800;
      private const int LCMAP_SIMPLIFIED_CHINESE = 0x02000000;
      private const int LCMAP_TRADITIONAL_CHINESE = 0x04000000;
      #endregion
   }
}