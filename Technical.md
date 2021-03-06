# Technical Overview #

a.k.a How this mod works.  How mod works.

Programming skill is required to understand this doc and the code.
For a small mod it has many common tasks - config, conditional patching, replacing game text and texture.
It is hoped that this explaination can help you understand the mod and may be get into modding if you haven't already.


## Requirements ##

The mod is written in C# as a .Net Standard Class Library.
It depends on Nuget HarmonyX, and a few game assembiles.  Nothing else.
HarmonyX is used over Harmony because it supports .Net Standard 2, requried to match the game.

It only works on a .Net build of the game, i.e. a Windows or Mono build.  Potentially through Wine or Proton.
Other builds or obfucated builds are not supported.

Every line of code is written by me, virtually all features are pure .Net.
The one exception is automatic conversion of unknown Chinese, using an old Win32 API.


## Reading Game Code ##

dnSpy is the tool of choice.  Point it to Assembly-CSharp.dll.
You don't get any design doc or comments - we all start blind.  Good luck!


## Design ##

This mod works in three stages: bootstrap, patching, and execution.

**Bootstrap**

On launch, the game loads ''our'' `version.dll` or `winhttp.dll`, which is actually [Unity Doorstop](https://github.com/NeighTools/UnityDoorstop/wiki).
It reads the doorstop config then loads and runs our mod (`Automodchef.dll`).

The mod, in turn, starts the logging system and monitors the loading progress of the game.
This stage happens in `ZyMod.cs`.  When game code (`Assembly-CSharp.dll`) is loaded,
we move to the next stage.

**Patching**

This part happsn in Automodchef.cs.  OnGameAssembly is called, which parse the config file,
then creates and applies a few mod components.

Each component surgically insert our code, or "patches", to the game's code using [HarmonyX](https://github.com/BepInEx/HarmonyX/wiki).
The patches are not immediately executed - the game code only finish loading after we are done.
That said, some basic data structurs required by the patches are created at this point for simplicity.

Unlike most dll mods, the patches are dynamically applied, making it possible to filtered them by mod config.
As a result, disabled features are skipped and does not affect the game at all.

**Execution**

After Unity booted up, like assigning a unique tracking id to you, the game runs.
As it reaches the patched code, its logic is changed by our patches, and thus behaves differently.

Most features are limited to one or a few methods.
They are generally short and as self-explanatory as patches can be.


## Architecture ##

Before we go ahead, let's talk about the elephant first.  My code is atypical.

First, Visual Studio (Code) can Format Code to "expand" my code into more common formats.

Second, there is blanket try catch coverage because this is a mod, to protect the game.

Third, many games have a dedicated mod manager, or can use generic Unity mod manager.
Each manager have their own requirements / dependencies.  Some may provide modding tools.

Finally, most simple mods simply call Harmony.PatchAll, like Harmony code examples.
Putting many features in one mod is generally less flexible and carries a higher risk.

In short, most mods probably do not look like this one.  Only the basic principles apply.


## Patches ##

Each patch is a method that we add before, add after, and/or rewrite an existing method.
See the HarmonyX documentation for a longer introduction and examples.

The patches vary according to what we want to accomplish.
It can be simple or complicated, we can replace a whole subsystem, hack other mods, mess with Unity or .Net.

Here are some common patch behaviours:

### Modify Input ###

A prefix patch runs right before the patched method, modifying parameters, fields, properties, data etc., changing behaviour(s).

Examples: SkipVideoSplashes, SetPackagingMachinePower

### Modify Output ###

A postfix patch runs right after the patched method, modifying its result, fields, properties, and/or other data.

Examples: OverrideCameraSettings, FixDishIngredientQuota

### Added Logic ###

Code can be added before or after the target method to do extra things.

Examples: PackagingMachinePassThrough, FixIngredientHintOnPause

### Total Disable ###

A prefix patch can prevent the original from running, totally bypassing it.
The bypass can be conditional.

Examples: DisableAnalytics, DisablePlatformLangSwitch

### Total Rewrite ###

An extension of total disable - you bypass the original *and* do your own thing.

Examples: ToggleDropdown (conditional), OverridePackagingMachineLogic (non-conditional)

### Partial Rewrite ###

A "transpiler" can surgically modify part of a method, replacing values, changing conditions, inserting or removing method calls etc.
It is perfect for making small changes without recreating the whole method, but you essentially need to learn a new mini language.

This mod does not use transpilers.

### Scoped Modification ###

Use prefix to set or change something, and restore it in postfix.

Examples: SuppressFreshnessTooltip & RestoreFreshnessTooltip

### Tracker ###

Sometimes you need to "listen" a method to keep track of game state or object associations, to be used later.
Often, you also need to listen to some events to clear the records.

Example 1: TrackOrdersEfficiency & TrackDeliveryEfficiency & ClearEfficiencyLog
Example 2: LogPackagingMachineLastDish & ClearPackagingMachineLastDish

### Dynamic Patching ###

For performance, risk control, damage control, flexibility, or other reasons, we can manually apply patches as required, instead of Harmony.PatchAll

Example 1: Every patch in this mod!
Example 2: ZhImg and ToZht, applied by DetectZh
Example 3: DisableAnalytics, applied in a loop.

### Combinations ###

Using a combination of techniques, you can extensively modify game logic.
The patches may or may not be closely related, and can be as elegance or as messy as real world programs.

Example: Efficiency log, Power log, Ask loadgame on level start, Traditional Chinese.

### Type Avoidance ###

Sometimes, you may need to avoid direct reference to a game class, to solve circular reference or play nice with (and without) DLC.
Playing with Type and Reflection is an unavoidable part of modding.

Example: orderedDish, cookedDish, target and body of ZhtEpic.

## Common Unity Tasks ##

Like .Net, Unity has its own unique ecosystem.
Once you learn it, the skill can be applied to other Unity games.

### Modifying UI ###

Everything displayed in Unity is a GameObject with a Transform, and potentially more components.
If you activate or inactivate the object, you can show or hide it.  Position can be adjusted through its transform.

There are a number of tools that can help you "see" the GUI tree.
BepIn comes with a visual plugin, and I got some commented out code in ZyMod to dump it to mod log.

This mod only do a minor tweaks.  From a computer command object it find the label object called "to", then find it Text component and change its text.
See FixCommandText for the code.

### Replacing Texture ###

Texture is widlely used in Unity both in 2D and 3D.

Replacing asset bundles requires no coding, but you need to redistribute the whole bundle (copyright, file size etc.), and it must be updated with every game patch.
So, if you can code, you can replace it on the fly.  Pretty easy, as long as you can find where to intercept it before it is displayed.

See LanguageSelectionButton for code that dynamically load a 2d texture.
The code then replaces the language button's image with it, provided the game is set to show Chinese.

## Last Words ##

Patching is not the only way to modify a game.
Cheat Engine can "mod" a game without patching any code, and some tools can rewrite the dll file.
So many ways to skin a robot CEO.
Conversingly, a dll mod does not necessary patch the game either.  Mod managers, modding libraries, mods that depends on other mods to do the dirty work...

It is what I am exceedingly good at, so it is what I focus on, but even I mod games in other ways.
Save file hacking, ini tweaking, asset replacement, x86 code hack, python script hack, men in the middle.  Been there done that.
So, yeah, keep your eyes open.
Even within .Net patching, Harmony / HarmonyX is not the only option.  RuntimeDetour, for example.  If you are mad like me.

Whether you think my code is crazy, elegance, or both, they took many sheep-days of decipering, testing, and polishing, built on decades of coding experience.
You don't need to mimic my style, and I do not expect to see it at work, where I were kid gloves.  Poor me.

Modding is a small circle.  Please be nice, respect other modders and the deveolpers.
