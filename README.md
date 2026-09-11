# GHShield

**Protection and access control for Grasshopper definitions.** Rhino 7.

Freeze any object in a definition. It keeps solving and feeding everything
downstream — but it can no longer be moved, deleted, copied or edited.

|          | Normal | Frozen |
|----------|--------|--------|
| Move     | yes    | no     |
| Delete   | yes    | no     |
| Copy     | yes    | no     |
| Edit     | yes    | no     |
| Solve    | yes    | **yes**|
| Output   | yes    | **yes**|

Frozen is not disabled. That distinction is the whole point: you can hand a
definition to someone who needs to *use* it without them being able to break
the logic inside it.

## The problem

You build a definition, then send it to a client, a consultant, a fabricator
or someone else in the office. They need to run it and change the inputs you
meant them to change. They do not need to drag your components around, delete
half a cluster, or nudge a slider that was carefully set.

## How it works

GHShield protects the standard Grasshopper components you already use. There
is no GHShield Slider or GHShield Panel to swap in — you build normally, then
select and freeze.

Coverage is not a fixed list. Protection adapters are generated at runtime
from whatever class an object's attributes actually are, so components from
other plugins are protected too, including ones released after this build.

## Using it

**Right-click is the main route.**

| Right-click on | You get |
|---|---|
| a frozen object | Unfreeze it, or unfreeze everything |
| any other object | *Freeze with GHShield* |
| a group | *Freeze / Unfreeze This Group (n)* |
| empty canvas | Freeze or Unfreeze the whole selection |

`Ctrl+Shift+F` freezes the selection, and unfreezes it when everything
selected is already frozen. It is the only keyboard shortcut, because Rhino's
command line and Grasshopper's menu bar claim most combinations before a
plugin can see them.

Everything is also on the **GHShield** menu on the Grasshopper menu bar, and
in the control panel — double-click the GHShield component to open it.

Frozen objects show a frost wash and a padlock.

Freezing a group seals it as a unit: nothing inside can be moved, and the
group cannot be ungrouped or have members added or removed while it is frozen -
so its outline stays exactly as you left it.

The first time you freeze something, GHShield adds a small component to your
definition. That component carries the protection list into the saved file —
without it, protection would not survive closing Rhino. Do not delete it; it
is protected too while anything else is.

**Unfreeze Everything** in the menu needs nothing selected, so you can always
recover a file.

## Installing

Rhino's Package Manager: run `_PackageManager`, search for **GHShield**,
install, restart Rhino.

Manual: copy `GHShield.gha` **and `0Harmony.dll`** into
`%APPDATA%\Grasshopper\Libraries`, then right-click both files, Properties,
and tick **Unblock**. Restart Rhino.

Both files are required. The `.gha` on its own will not load.

## What this is and is not

GHShield stops a definition being damaged by someone using it. That is the
problem it was built for, and it solves it well.

It is **not** encryption. The recipient needs GHShield installed for
protection to apply, and the logic sits in the file as ordinary Grasshopper
components. Someone determined, working without the plugin installed, can
edit the file. If you need to hide logic rather than protect it, use
Grasshopper's password-protected clusters — the two work well together.

## Requirements

- Rhino 7 (Windows)
- .NET Framework 4.8

## Licence

MIT
