Every feature of the mod can be individually turned on or off.
Some features also support finer controls.

Main config file is `Automodchef.ini`, in the game's app data folder at
%AppData%\..\LocalLow\HermesInteractive\Automachef\
The file is created on first run of the modded game,
and will be recreated whenever it is renamed or deleted.

= Logging Level =

To control logging level, create a text file `Automochef-log.conf` in the same folder as `Automodchef.ini`.

Its first line dictates the log level, which may be Off, Error, Warning, Info, or Verbose.
Default is Info.

Log level requires a different file because logging starts before config file is parsed.


= Main Config =

Main config is a standard ini file and should be pretty well commented.
Values are generally case insensitive.
Booleans can be 0 or 1, yes or no, true or false.
Invalid values will be ignored.

But just in case, this document explains every settings and the story behind them.

*config_version*

This field can be used by future version of the mod to detect old config and update them to new version.
If set to a number lower than the current default, the config file will be recreated while retaining config.

*skip_intro*

If true, it will skip all scenes straight to "Press spacebar to continue",
currently including the Unity logo, Hermes logo, Team 17 logo, and auto-save disclaimer.

If the press spacebar scene is not found, nothing will be skipped.
I think it took me a day to nail.  Why there isn't a standard way to skip intro?

*skip_spacebar*

If true, the "Press spacebar to continue" scene will be immediately bypassed.

This is the very first feature.
Imagine the disappointment when I see that hacking the `SplashScreen` class skips only the spacebar.

*disable_analytics*

Disable the game's level analytics and most Unity analytics.

Why, when a game have and actually calls `UnityEngine.Advertisements.dll`, what did you expect?
Given the changes from this mod, I don't think the polluted level stats would be useful anyway.

*fix_food_hint_when_paused*

The game couldn't show mouseover hint on foods when paused because of wrong layer mask.
Set to true to fix it.

*dish_ingredient_quota_buffer*

The game's efficiency calculation expects a certain amount of ingredients to be used per dish.
For most non-trivial dishes, there is a little headroom, but it is inconsistent,
and Double Bypass Meal actually expects less ingredients than is required leading to low efficiency.

When set to 1 (default), the mod makes sure all non-trivial dishes have a headroom of 1 ingredients.
When set to 0, the mod makes sure all dishes do not expect less than what is required (fix Double Bypass Meal).
When set to -1 or less, the feature is disabled and the mod does not change quota.

*side__view_angle*

Angle of left / right view, measured from center.
Game default 35.
Mod default is 0 which means do not change.

All camera settings accept floating point values.

The game's camera moves only in three horizontal angles (left, centre, right) and two vertical angles (far, close).
Modding it requires extensive rewrite of the camera subsystem, so I settle with angle controls, about two order of magnitude simpler.

*lose_view_angle*

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

No, you can't toggle individual warnings, because I am lazy and want to keep the config simple.
Just... be more careful.  Or disable this feature.

*ask_loadgame_on_level_start*

When you enter a level where an existing save is found,
the load dialog will automatically popup.

If a game is loaded, or if you press Esc key, the level goal reminder and Roboto speech will be bypassed.
If you click "Back" with your mouse, game will restore the reminder and speech (if any).

You get the normal fresh level experience if no save is found.

It took me a night to come up with this intuitive solution to the skip intro + Roboto speech problem,
killing two birds in one stone while preserving the new level experience.
Straightforward to mod too.  For me.

*dropdown_toogle_threshold*

If an in-game dropdown have less options than this value, it will behaves like a toggle button.
That means, each click on the button progress down the options.

Mod default is 3, which covers robot arm directions, power profiles, contract difficulties etc.
If set to 2, it covers only long arms range, computer/repeater output state etc.
On 1 or less, this feature is disabled.  I'd rather you see the single-value dropdown than to think the mod is bugged.

3 is the default because it wouldn't (shouldn't) take you more click to toggle than to pick from dropdown.
This feature, plus skip intro, is the main driving force of the creation of this mod.
In other words, I am fed up with stupid dropdowns, so I decided to mod the game.

*tooltip_power_usage*

When true, current power usage and total power usage is displayed in kitchen part's mouseover hint.

This allows you to better see whether a part is using power, and the lifetime power of each individual part.
(As long as you pause the game before it ends!)

*tooltip_freshness*

When true, the "freshness" of a food is displayed in its mouseover hint.

An ingredient's fresh timer is actually its "insect countdown", so it is refreshed whenever it is moved.
This also means they never become spoiled on levels without insects.

A finished dish's fresh timer is its spoil timer and apply to all levels.

Kitchen parts does not show the min freshness of the food inside because they are informative enough.
Having fresh time when the foods are on transit seems to be sufficiently informative.


*efficiency_log*

When true, add efficiency calculation to kitchen log.
It shows you the efficiency quotas, how much you have used, and calculation of the final score.

In effect, you can see what you need to optimise, and how many to optimise for.
Note that the mod's other tweaks may result in a different efficiency from vanilla game.
But from my experience - I started this mod with most of the game done - most scores stay the same or roughly the same.

*efficiency_log_breakdown*

When true, shows the count of each dishes, and how their contributions to the efficiency quotas.

Each ordered dishes contribute most of its ingredient quota and 1/3 of its power quota.
Each delivered dishes contribute the reminders.

This is enabled by default to explain why the efficiency quota is different from level goals,
but is also useful when you want to know which dishes are most or least ordered.

*power_log_rows*

When true, show top X power consuming part types in kitchen log.

Parts that do the same things are grouped together:

* Advanced Assembler / Computer / Dispenser / Order Reader / Storage are grouped into their basic versions.
* Conveyor Grill / Frier are grouped into their non-conveyor versions.
* Fast conveyors, bridges, and gates are grouped into simple Belt.
* All robotic arms are grouped into dump arms.

I could have Google Translated each of the groups in each languages, but too much effort to code and validate.
So each group will use the basic model as representative.

*instant_speed_change*

When true, make the game change speed instantaneously.

This means pausing or resuming the game will immediately take effect, allowing for precise control.

*speed2*

Set the speed of double time (displayed as two arrows), a 0-100 integer.
Game default is 3, which means 3x realtime.
Mod default to 5, same as vanilla triple time.

*speed3*

Set the speed of triple time (displayed as three arrows), a 0-100 integer.
Game default is 5.
Mod default to 20, so 1 minute in-game time can pass in 3 seconds... if your computer is fast enough.

The speeds are limited to integer because of game design.
It is not difficult to change to float but... I'd rather keep it simple.


*food_processor_idle_power*

When set to 0 or any positive number, the Food Processor will use this amount of power (in W) when not packaging.
Default is -1 which have them always use full power like in vanilla game.

Initially I didn't plan to do this, since I regards it as "always on" like grills and friers.
But some players may differ, all I need to do is copy a few lines from packaging machine, so here we are.

*packaging_machine_idle_power*

When set to 0 or any positive number, the Packaging Machine will use this amount of power (in W) when not packaging.
Default is 60 which is the expected power cost of its moving belts.
Set to -1 to always use full power.

This option aligns its power usage with machines like dispensers, robotic arms, and assemblers.
Because of the reduced power usage, some layouts may result in meaningful difference in power usage,
and may score higher efficiencies or make it pass power cap when it couldn't.

*packaging_machine_passthrough*

When true, Packaging Machine will pass-through all foods that are not part of its recipes.
This is another change that aligns it with assemblers.

You still need to assign at least one recipe to the machine to "turn it on".
Sure, another inconsistency, but not worth my modding time.

*smart_packaging_machine*

When true, Packaging Machine becomes smarter.  Military grade technology, right?

When a PM have enough ingredients to choice from multiple recipes:

Rule 1, disregard all recipes that is a sub-recipe of other options.
e.g. If a PM can make both Bacon Fries and Fries, always prefer Bacon Fries over Fries.

Rule 2, deprioritise previously made recipe.
e.g. If a multi-meals PM have just made a Triplane Meal, it would prefer something different when the next Fries come along.
Each machine remember their own last recipt.  Memory is not shared.

Rule 3, throw a dice if there is still multiple choices.
The dice is shared.  You humans also share dice in Monopoly, right?

*export_food_csv*

When true, export food data to foods.csv in the same folder as this config file.
The data includes its internal id, current translated name, outcome of various process etc.

The export process has neglectable performance impact, but is disabled by default because most players won't need the data.

*export_hardware_csv*

When true, export kitchen part data to hardwares.csv in the same folder as this config file.
The data is simpler than food because most differences are in code rather than in data.

Neglectable performance impact, but disabled by default for the same reason.
How neglectable?  1 to 2ms for me.  Just don't worry about it.