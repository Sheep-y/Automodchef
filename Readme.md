= ﻿Automodchef =
== A mod of Automachef ==

Automodchef is an unofficial code mod of the factory building game Automachef.
It tweaks various aspects of the game, including bug fixes, efficiency and power reports, QoL improvements, and mechanic weaks.

This mod does not add any new recipes or missions.
All vanilla / mod level saves are compatible; this mod does not affect save games.

Mod is compatible with all languages, but efficiency and power reports are in English only.

Main features:

* Skip intros.
* Fix game bugs.
* Efficiency and power report in kitchen log.
* Improved packaging machines, with passthrough.
* Startup load on all levels and bypass Roboto speech.
* Higher camera (bird view possible), faster speeds, expanded tooltips.
* Improved Chinese (Traditional) translation.
* And more.


= Installation =

1. Download latest release from GitHub or NexusMods.

2. Extract with 7-zip or similar tools.

3. Copy or move `version.dll`, `doorstop_config.ini`, and the `Mod` folder into game's root folder:

    %ProgramFiles(x86)%\Steam\steamapps\common\Automachef
    %ProgramFiles%\Epic Games\Automachef

4. That's all. When you launch the game, the intro should be skipped by default.

The mod has no special requirements.  It runs as part of the game.
The src folder contains source code and licenses.


= Configuration =

On first launch, the mod will create `automodchef.ini` and `automodchef.log` in the game's user data folder,
i.e. %AppData%\..\LocalLow\HermesInteractive\Automachef

You can edit the ini file to turn on/off mod features.
Each setting have a short description that explains its effects and options.
See config.md or config.txt for more information.

If you can't see file extensions, the file with a little gear in its icon is the right one.
You may want to google how to see file extensions.


= Compatibility =

The mod is developed and tested on Automachef version 1.1.0, Epic Game Store.
It works only on the Microsoft .Net build of the game, i.e. on Windows or Wine.

It should be compatible with Steam version, and is expected to be broadly compatible with future game versions.
It can survive normal game updates, but Steam's Verify File may remove the mod.

The mod does not change save games in any way.
But because of the mod's tweaks, some vanilla layouts may yield a higher efficiency.
Conversely, a layout that can clear a level with this mod may fails to do so in vanilla.

As the first known DLL mod for Automachef, it does not have other mods to be compatible with.
Newer mods can use their own method to "play nice" with this mod.  This one's on them.


= Troubleshoot =

If Unity and other logos were not skipped and you didn't explictly disable skip_intro,
the mod is not being loaded.  Check `version.dll` and `doorstop_config.ini` are in game root and non-empty,
and the Mod folder exists with at least 9 files.

Otherwise, if most features work, but the game was broken in some way, check automodchef.log for errors.
You can then disable the relevant feature(s) (see above) to try to fix the game.

I have cleared the game, so don't count on future development or support.
Mod is open source.  Feel free to fork and release in a new name (to avoid confusion), or make a mod that mod this mod.


= Removal =

To remove the mod, delete `version.dll`, `doorstop_config.ini`, and the `Mod` folder from game folder.

The mod does not modify any game files, so there is normally no need to Verify files.

You may also move / rename version.dll to temporary disable the mod.