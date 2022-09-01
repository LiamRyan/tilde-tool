# Tilde Tool
Welcome to the obscure little repo of Tilde Tool!

Tilde Tool provides a few convenient utilities that fill tiny niches other tools do not quite cover. See below for a list of the tools and how to use them.

**This tool is in a rough state!** A lot of work is still needed to make it easy and intuitive to set up and use. As for the code, it is cobbled together as quickly as possible, with minimal testing and little concern for cleanliness or stability of code. For now it probably won't be much use to you, unless you are trying to figure out how to do something specific, such as register for virtual desktop events in C#, listen for global keyboard events in C#, etc.

## Hotcommand
Type short commands to run programs, open directories, open websites, run batch files, etc.

Add the commands to Hotcommand.json. There are hotcommands themselves, each with a Tag and a list of Spawns. QuickTags are aliases for a hotcommand. Contexts contain their own list of hotcommands and quicktags, which can only be run while in that context. TODO: describe the format here.

Press WIN+` and, while holding WIN down, write the tag of a hotcommand and press enter, or write the tag of a quicktag and it will run without need of pressing enter. You can continue writing commands until you release WIN. Alternately, you can write the tag or quicktag of a context, which will switch to that context and allow you to write commands within it.

## Explorer Ring
Run programs on selected files or folders with a few brief hotkeys.

Add the programs and the associated extensions to Explorer.json. TODO: describe the format here.

Press WIN+NumpadInsert while a file or folder is selected in Windows Explorer to open the ring, then press a numpad direction or a shortcut key to run the command.

## Status Bar
Periodically check if local or online sources have changed, and display a notification when they do.

Add feeds to Sources.json. TODO: describe the format here.

Press WIN+Y to see the list of feeds and their latest update times and titles.

## Dictionary
Quickly look up words on a dictionary site.

Add a "DictionaryURL" field to Hotcommand.json pointing to a dictionary site of your preference, where "@URL@" in the value will be substituted with the written word to look up.

Press CTRL+ALT+W and type the word to look up, then press enter.

## Virtual Desktop Pane
When you switch between virtual desktops, a pane will appear at the top of the screen showing which one you switched to.  It does not work well with unnamed desktops right now.
