# Food4Rhino listing — copy and paste

Food4Rhino is the *listing*; the actual install happens through Rhino's
Package Manager once the `.yak` is pushed. Do the Yak push first, then create
the listing, so the "available in the Package Manager" claim is true when
people read it.

---

## Title

    GHShield

## Short summary (one line)

    Freeze parts of a Grasshopper definition so they keep working but cannot be edited.

## Description

> Send a Grasshopper definition to a client, a consultant or a fabricator and
> it usually comes back changed. A slider nudged. A component dragged out of a
> cluster. Half the logic deleted by accident. You cannot hand someone a
> working file and also hand them a list of things not to touch.
>
> GHShield lets you freeze any part of a definition. A frozen object keeps
> solving and keeps feeding everything downstream — it simply cannot be moved,
> deleted, copied or edited. The person you send it to runs the file, changes
> the inputs you left open, and gets their result. Your logic stays exactly as
> you built it.
>
> **Frozen is not disabled.** The definition still works end to end.
>
> **How it works**
>
> Right-click any object to freeze it; right-click a group to freeze the whole
> group, which also stops it being ungrouped or reshaped. Ctrl+Shift+F freezes
> and unfreezes a selection. Frozen objects show a frost wash and a padlock.
>
> Protection is saved into the .gh file, so it survives closing Rhino and
> being emailed.
>
> **What it covers**
>
> Everything. Sliders, panels, toggles, value lists, graph mappers, gene
> pools, colour controls, MD sliders, gradients, image samplers — and
> components from other plugins, including ones released after this build.
> GHShield generates its protection at runtime from whatever an object
> actually is, rather than working from a fixed list of supported types.
>
> It protects the standard Grasshopper components you already use. There is
> nothing to swap in and nothing to rebuild.
>
> **What it is not**
>
> GHShield is not encryption. It stops a definition being damaged by someone
> using it — that is the problem it was built for. It does not hide your
> logic, and the recipient needs GHShield installed for protection to apply.
> If you need to conceal how something works rather than protect it, use
> Grasshopper's password-protected clusters; the two work well together.

## Requirements

    Rhino 7, Windows

## Tags

    protection, freeze, lock, read-only, collaboration, handover, workflow,
    definition, client, fabrication

## Licence

    MIT

---

## Screenshots to prepare

Four is plenty. Aim for clarity over polish.

1. **A frozen group** — several components with the frost and padlock, wired
   into an unfrozen part of the definition. Shows what protection looks like.
2. **The GHShield menu open** — shows how it is driven.
3. **The control panel** next to a canvas with a count showing.
4. **Before / after** — a slider at one value, then the same slider frozen
   with a cursor on it, unmoved.

A 10–15 second screen recording is worth more than all four: freeze a few
components, then try to drag one, type in a panel, and delete one. Nothing
happens each time. That single clip explains the product better than the
description does.

---

## Publishing sequence

1. `powershell -ExecutionPolicy Bypass -File package.ps1`
2. Test the built package on a clean profile — `RELEASE.md` sections 2–5
3. `Yak.exe login` (opens a browser; needs a Rhino account)
4. `Yak.exe push package\ghshield-1.0.0-rh7_0-win.yak`
5. Confirm it appears: run `_PackageManager` in Rhino and search GHShield
6. Create the Food4Rhino listing using the copy above

## Before you push

Yak versions are permanent — you cannot overwrite or delete a published
version, only push a higher one. So the first push should be something you
are happy to have people install.

Add the repo or product URL to `manifest.yml` first; it is commented out at
the moment and a listing with no link looks unfinished.
