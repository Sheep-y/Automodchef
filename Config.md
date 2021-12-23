Every feature can be individually turned on or off, with some having fine controls.

Main config file is `Automodchef.ini`, in the game's app data folder at
%AppData%\..\LocalLow\HermesInteractive\Automachef\

If not exists, the file will be created when launching the modded game,


= Main Config =

The main config file is a standard ini.
Values are case insensitive.
Booleans can be 1 or 0, yes or no, true or false.

Invalid values will be ignored.
Sections are cosmetic and ignored by this mod.


*config_version*

This field can be used by the mod to detect old config and update it to new version.
If set to a number lower than the current default, the config file will be recreated while retaining config.


== System ==

*skip_intro*

If true, it will skip all scenes straight to "Press spacebar to continue",
namely Unity logo, Hermes logo, Team 17 logo, and auto-save disclaimer.

If the press spacebar scene is not found, nothing will be skipped.
Took me a while.  There is no standard way to skip intro. :(


*skip_spacebar*

If true, the "Press spacebar to continue" scene will be bypassed.

This is the very first feature.
Imagine the disappointment when I realise that hacking the `SplashScreen` class skips only the spacebar.


*disable_analytics*

Disable the game's level analytics and most Unity analytics.

Why, when a game comes with `UnityEngine.Advertisements.dll`, what did you expect?
Given the mechanic changes, the stats would be polluted anyway.


== Camera ==

*side_view_angle*

Angle of left / right view, measured from center.
Game default 35.
Mod default is 0 which means do not change.

All camera settings accept floating point values.

The game's camera moves only in three horizontal angles (left, centre, right) and two vertical angles (far, close).
Modding that would require extensive rewrite of the camera subsystem, so I settle with angle controls, about two order of magnitude simpler.


*close_view_angle*

Downward angle of close up view, measured from the ground.
Game default 40.
Mod default is 0 which means do not change.


*far_view_angle*

Downward angle of far view, measured from the ground.
Game default 60.
Mod default 70, for a "higher" view with less obstructions.
If set to 0, the game's value will not be changed.

Set this value to 90 for a bird's eye / top-down view.
It is not set as default because it differs too much from the game's vanilla experience.
Many players don't read readme and never know the mod is configurable.


*far_view_height*

Camera height of far view, measured from the ground.
Game default 20.
Mod default 0 which means do not change.


== User Interface ==

*fix_food_hint_when_paused*

The game couldn't show mouseover hint on foods when paused because of a bug.
Set to true to fix it.


*fix_visual_editor_text

When true, fix a minor visual editor bug that may be a source of confusion.

The "Subtract (variable-number)" command is using an incorrect text in visual editor.
It says "Subtract V0 to Number" when it should be "Subtract V0 by Number".

The fix is language independent, but the translation itself may not make sense.
I know because the original Chinese blocks are trash.  They use English grammer.
They are difficult to translate right.  Fortunately, I am a semi-professional translator.


*suppress_confirmation*

If true, suppress the following popups:

* Load game warning.
* Challenge level warning.
* Delete blueprint warning.
* Delete save slot warning.
* Overwrite blueprint warning.
* Overwrite save slot warning.
* Quit level warning.
* Quit game warning.
* Reset kitchen warning.

No, you can't toggle individual warnings, because I want to keep the config simple.
Just... be more careful.  Or disable this feature.


*ask_loadgame_on_level_start*

When you start a level and an existing save is found, the load dialog will immediately popup.

If a game is loaded, or if you press Esc key, level goals popup and Roboto speech will be bypassed.
If you click "Back", or if no save os found, game will show goals and speech (if any) as normal.

Took me a night to come up with this intuitive solution to the skip intro + Roboto speech problem,
killing two birds in one stone while preserving the new level experience.
Straightforward to mod too.  For me.


*stay_open_after_delete_save*

When true, the save dialog will stay open after the delete button is clicked.


*dropdown_toogle_threshold*

If an in-game dropdown have less options than this value, it will behave like a toggle button.

Default is 3, covering robot arm directions, power profiles, contract difficulties etc.
If set to 2, it covers only long arm range, computer/repeater output state etc.
On 1 or less, this mod feature is disabled.  I'd rather you see the dropdown than to think it is bugged.

3 is the default because it wouldn't (shouldn't) take you more clicks to toggle than to pick from dropdown.
This feature, plus skip intro, is the main driving force of the creation of this mod.
In other words, I am fed up with stupid dropdowns, so I decided to mod the game.


*hide_tutorial_efficiency*

When true, all completed tutorial levels will display "Success" on the level tree instead.
Power log, efficiency log, and efficiency score will also be disabled in tutorial levels.


= Info =

*tooltip_power_usage*

When true, current power usage and total power usage is displayed in kitchen part's mouseover hint.

This allows you to see how much power a part is drawing _right now_, and the lifetime power of each part.


*tooltip_freshness*

When true, the "freshness" of a food is displayed in its mouseover hint.

An ingredient's fresh timer is actually its "insect countdown", so it is refreshed whenever it is moved.
This also means they never expire on levels without insects.

All finished dish has a spoil countdown, on all levels.

Kitchen parts do not show the min freshness of the food inside because they are long enough.
Showing fresh timer when the foods are on transit seems to be sufficient.


*efficiency_log*

When true, add efficiency calculation to kitchen log.
It shows you the efficiency quotas, how much you have used, and calculation of the final score.

In effect, you can see what you need to optimise, and how many to optimise for.
Note that the mod's other tweaks may result in different values from vanilla game.


*efficiency_log_breakdown*

When true, show the count of each dishes, and their contributions to the efficiency quotas.

Each ordered dish contribute most of its ingredient quota and 1/3 of its power quota.
Each delivered dish contribute the reminders.

This is enabled by default to explain why the quotas is different from level goals,
but is also useful when you want to know which dishes are most or least ordered.


*power_log_rows*

When > 0, show top X power consuming part types in kitchen log.

Similiar parts are grouped together:

* Advanced Assembler / Computer / Dispenser / Order Reader / Storage are grouped into their basic versions.
* Conveyor Grill / Frier are grouped into their non-conveyor versions.
* Fast conveyors, bridges, and gates are grouped into simple Belt.
* All robot arms are grouped into dump arm.

I could have Google Translated each of the groups in each language, and verify them, but too much effort.
So each group will use the basic model as representative.


= Simulation =

*instant_speed_change*

When true, make the game change speed instantaneously.

This means pausing or resuming the game will immediately take effect, without delay.


*speed2*

Set the speed of double time (displayed as two arrows), a 0-100 integer.
Game default is 3, which means 3x realtime.
Mod default to 5, same as vanilla triple time.


*speed3*

Set the speed of triple time (displayed as three arrows), a 0-100 integer.
Game default is 5.
Mod default to 20, so 1 minute in-game time can pass in 3 seconds... if computer is fast enough.

The speeds are limited to integer because of game design. Cap of 100 is imposed by Unity.


= Mechanic =

*dish_ingredient_quota_buffer*

The game's efficiency calculation expects a predefined number of ingredients to be used per dish.
For most non-trivial dishes, there is a little headroom, but it is inconsistent,
and Double Bypass Meal actually expects less ingredients than is required to make, leading to low efficiency.

When set to 1 (default), the mod makes sure all non-trivial dishes have a headroom of 1 ingredients.
When set to 0, the mod makes sure all dishes do not expect less than what is required (fix Double Bypass Meal).
When set to -1 or less, the feature is disabled and the mod does not change quota.


*food_processor_idle_power*

When set to 0 or any positive number, the Food Processor will use this amount of power (in W) when not packaging.
Default is -1 which have them always use full power like in vanilla game.

Initially I didn't plan to do this, but some players may differ, and I only need to copy a few lines from below.


*packaging_machine_idle_power*

When set to 0 or any positive number, the Packaging Machine will use this amount of power (in W) when not packaging.
Default is 60 which is the expected power cost of its belts.
Set to -1 to always use full power.

This option aligns its power behaviour with machines like dispensers, robotic arms, and assemblers.
Because of the reduced power usage, some layouts may use slightly less power,
and may score higher efficiencies or make it pass power cap when it couldn't before.


*packaging_machine_passthrough*

When true, Packaging Machine will pass-through all foods that are not part of its recipes.
This is another change that aligns it with assemblers.

You still need to assign at least one recipe to the machine to make it runs.


*smart_packaging_machine*

When true, Packaging Machine becomes smarter.  Military grade technology, right?

When a PM have enough ingredients to choice from multiple recipes:

Rule 1, disregard all recipes that is a sub-recipe of other options.
e.g. If a PM can make both Bacon Fries and Fries RIGHT NOW, always prefer Bacon Fries over Fries.

Rule 2, deprioritise previously made dish.
e.g. If a multi-meals PM have just made a Triplane Meal, it would prefer everything else when the next Fries come along.
Each machine remember their own last recipt.  Memory is not shared.

Rule 3, throw a dice if there is still multiple choices.


= Misc =

*export_food_csv*

When true, export food data to foods.csv in the same folder as the config file.
The data includes its internal id, current translated name, recipe, outcome of various process etc.

The export process has neglectable performance impact (a few ms), but disabled by default because most players won't need the data.


*export_hardware_csv*

When true, export kitchen part data to hardwares.csv in the same folder as the config file.
The data is simpler than food because most differences are in code rather than in data.

Neglectable performance impact (a few ms), but disabled by default for the same reason.


*export_text_csv*

When true, export game text data to text.csv in the same folder as the config file.
Specifically, the localization key and the text from current game language will be dumped.
The dump is done by the game's internationaliztion engine, not by this mod.  Some values may be misaligned.


*fix_epic_locale_override*

When true, the game will no longer try to force Epic Game to change its language.

I can set the platform's language on my own, thanks.  Please don't change it for me, and Epic please don't automatically assume Chinese must be Simplified.


*traditional_chinese*

When true, and when game language is set to Chinese, a new Traditional Chinese translation will be used.
Regardless of language, the Chinese option in the language menu will also be renamed.

This game has the best Chinese translation I have seen for an Indie, make no mistake.  But there are mistakes.  Not to mention that some names mean different things in Traditional Chinese, used by Taiwan, Hong Kong, and Macau.  More people than Australia.  Rich people!

So I conveted the original text and rewrote most lines.  Need to make a stand, leave a mark before our cultures are wiped by a... determinated force.

Any new text added later will fallback to a win32 coversion api.  Literally a few lines of work.


= Logging Level =

To control logging level, create a text file `Automochef-log.conf` in the same folder as `Automodchef.ini`.
This file is not automatically created.

First line controls the log level, which may be Off, Error, Warning, Info (default), or Verbose.
Second line controls write interval in seconds.  A non zero value will buffer log entries and write to disk in the background.  Default 2.

Log level requires a different file from main config because logging starts at bootstrap, before config file is parsed.


