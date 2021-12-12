Every feature of the mod can be individually turned on or off.
Some features also support finer controls.

Main config file is `Automodchef.ini`, in the game's app data folder at
%AppData%\..\LocalLow\HermesInteractive\Automachef\
The file is created on first run of the modded game.


= Logging Level =

To control logging level, create a file `Automochef-log.conf`
in the same folder as `Automodchef.ini`.

Its first line dictates the log level, which may be Off, Error, Warning, Info, or Verbose.
Default is Info, which logs configs and major tasks performed by the mod.

Log level requires a different file because logging starts before config file is parsed.


= Main Config =


; To change log level, create new file Automochef-log.conf with the first line saying Off, Error, Warning, Info, or Verbose.
; Version of this Automodchef config file.  Don't touch!
config_version = 20211206

[System]
; Skip Unity, Hermes, Team 17, and Autosave screens.  True or false.  Default true.
skip_intro = True
; Skip 'Press Spacebar to continue'.  True or false.  Default true.
skip_spacebar = True
; Disable mission stats analytics.  True or false.  Default true.
disable_analytics = True

[Bug Fix]
; Fix food mouseover hints not showing when game is paused.
fix_food_hint_when_paused = True
; Make sure all dishes that have this much efficiency quota for their ingredients.  Game is inconsistent.  Mod default 1.  0 to fix Double Bypass Meal.  -1 to disable.  When set to 1, this mod will not apply buffer to single ingredient recipes for better balance.
dish_ingredient_quota_buffer = 1

[Camera]
; Angle of left / right view.  Game default 35.  Set to 0 to not change.
side__view_angle = 0
; Downward angle of close up view.  Game default 40.  Set to 0 to not change.
close_view_angle = 0
; Downward angle of far view.  Game default 60.  Mode default 70.  Set to 0 to not change.  Set to 90 for a top-down view.
far_view_angle = 70
; Height of far camera.  Game default 20.  Set to 0 to not change.
far_view_height = 0

[User Interface]
; Suppress yes/no confirmations - save before quit, load game, delete or overwrite save / blueprint / scenario, quit level / game, reset layout
suppress_confirmation = True
; Show load game prompt when entering a level (if any saves).  Loading a game will bypass level goal popup and roboto speech.  True or false.  Default true.
ask_loadgame_on_level_start = True
; Max number if options to convert dropdown to toggle button.  Default 3.  0 to disable.
dropdown_toogle_threshold = 3
; Show real-time power usage in mouseover tooltips.
tooltip_power_usage = True
; Show food freshness in mouseover tooltips.
tooltip_freshness = True
; Add effiency calculation to kitchen log.  True or false.  Default true.
efficiency_log = True
; Breakdown efficiency quotas by dishes.  True or false.  Default true.
efficiency_log_breakdown = True
; Show top X power consuming part types in kitchen log.
power_log_rows = 5

[Simulation]
; Change game speed instantiously.
instant_speed_change = True
; Speed of double time (two arrows).  0-100 integer.  Game default 3.  Mod default 5.
speed2 = 5
; Speed of triple time (three arrows).  0-100 integer.  Game default 5.  Mod default 20.  High speed may cause some orders to expire when they would not on slower speeds.
speed3 = 20

[Mechanic]
; This section changes game mechanics. They do not break or brick saves, but may allow non-vanilla solutions.
; Packaging machine spend less power when not packaging.  Game default 800.  Mod default 60 (2x slowest belts).
packaging_machine_idle_power = 60
; Packaging machine's sub-recipes have lowest priority (Fries < Bacon Fries < Loaded Cheese Fries), last processed recipe have lower priority, and random for remaining ties.
smart_packaging_machine = True
; Packaging machine will pass-through foods that it is not interested in, like assemblers.  You still need to assign at least one recipe to the machine.
packaging_machine_passthrough = True

[Tools]
; Export foods to foods.csv on game launch.  True or false.  Default false.  Neglectable impact, disabled only because most won't need these.
export_food_csv = False
; Export hardwares to hardwares.csv on game launch.  True or false.  Default false.  Ditto.
export_hardware_csv = False
