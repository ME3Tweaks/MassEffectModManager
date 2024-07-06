![Documentation Image](images/documentation_header.png)

## Update TLK files in ME1/LE1 with Mod Manager
With the `GAME1_EMBEDDED_TLK` header, you can merge TLK edits from .xml files, the same as you would if you dumped a TLK file to xml, and merge them into Mass Effect (1).


### The Problem

Mass Effect, both Original Trilogy and Legendary Edition versions, do not use global TLK Files like ME2 and ME3 do. It has a 'global' TLK file, but it's not actually global, mostly only containing non-conversational strings. The conversation strings are located in each conversation's file, which is very inconvenient to ship in a mod. For example, a new localization would have to ship gigabytes of package files for what amounts to maybe a couple of megabytes of actual new data, and it interferes significantly with using other mods that may modify these same packages.

The 'Game 1 TLK Merge' feature that is part of ME3Tweaks Mod Manager 7.0 can work around this issue by dynamically installing your TLK changes without having to perform full package file replacement.


### Setting up TLK merge

Your mod must target `cmmver 7.0` or higher for this feature to work. The feature level for different cmmver values are described below.

| cmmver | Features                                                                                                             |
|--------|----------------------------------------------------------------------------------------------------------------------|
| 7      | Initial feature release                                                                                              |
| 8      | Supports merging all .xml files into a single .m3za file on deployment, which is much faster to decompress for users |
| 9      | Supports optionkey folders that can be enabled through the alternate DLC system of moddesc                           |

In your moddesc.ini file, you must add the following section:

```
[GAME1_EMBEDDED_TLK]
usesfeature = true
```

For a very simple example of a full moddesc.ini file, you would have something like the following:

```
[ModManager]
cmmver = 7.0

[ModInfo]
game = LE1
modname =  TLK Merge Test
moddev = DevNameHere
moddesc = Tests the merging feature
modver = 1

[GAME1_EMBEDDED_TLK]
usesfeature = true
```

This will instruct Mod Manager to parse the directory named `GAME1_EMBEDDED_TLK` for TLK xml files. Files must have a specific naming pattern so that they can be found in game files. 

![Example folder setup](images/tlk_merge_foldersetup.png)

Files must have the following naming system or it will not work:

**[PackageFileNameBase].[InstancedFullPathInPackage].xml**

 - PackageFileNameBase: The name of the package, without the extension. For example `Startup_INT.pcc` would use `Startup_INT`.
 - InstancedFullPathInPackage: The instanced full path in the package. This can be found in the metadata tab of Legendary Explorer when viewing the TLK export
 
![Where to find instanced full path](images/tlk_merge_instancefullpath.png)

To get a dump of files that are already in the correct naming format, you can use my TLK dump experiment in Legendary Explorer. Go to Package Editor and make sure experiments are on in the `Debugging` menu. A new menu named `Experiments` will show up, and you can go to `Mgamerz's Programming Circus > Dump LE1 TLK to XML` to dump the entire game's TLK to properly named files. You can also filter it by localization, such as INT, DE, etc, the same ones that appear as suffixes to game files.

The xml files can contain just a few strings to update, all of the strings to update, or even add more strings.

### Using option keys
Option keys, in the context of `GAME1_EMBEDDED_TLK`, are not the same as DLCOptionKeys from `DLCRequirement`. An option key is something that can be switched on via an operating in an `alternatedlc` object. TLK xmls that are under a folder will be considered gated behind an `optionkey` of the folders name. Unless the key is switched on via alternates, it will not install. Option keys only can be used if your mod targets moddesc 9.0 or higher.

To set them up, move your .xml files you want conditionally installed into a subfolder.
![image](https://github.com/ME3Tweaks/ME3TweaksModManager/assets/2738836/a877fb98-9da3-41c2-8628-1fdd5ea78874)

And then in your moddesc, set up an alternate DLC object with ModOperation `OP_ENABLE_TLKMERGE_OPTIONKEY` that has the foldername for the `LE1TLKOptionKey` value.
![image](https://github.com/ME3Tweaks/ME3TweaksModManager/assets/2738836/745c02cc-5330-4c9f-80aa-ddddccd188a3)
When the option is selected, the xml files in that folder will install. In the event of multiple same-named xmls, they go in order of always installed -> order of alternates.
