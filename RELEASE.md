# GHShield — release checklist

Work top to bottom. Nothing below the line marked **STOP** should be done until
everything above it passes.

---

## 1. Build

- [ ] Close Rhino completely
- [ ] Build → Rebuild Solution. First build restores Lib.Harmony, so it is slower
- [ ] `bin\Debug` contains **both** `GHShield.gha` and `0Harmony.dll`

## 2. Smoke test on the dev machine

- [ ] Rhino starts, Grasshopper opens, `GHShield: ready...` appears once
- [ ] The GHShield menu is on the menu bar
- [ ] The GHShield component has the shield icon, not a blank square
- [ ] `Ctrl+Shift+F` freezes, and unfreezes when everything selected is frozen
      (click the canvas first — the shortcut only reaches GHShield when the
      canvas has focus)
- [ ] Right-click a frozen object → GHShield's menu, and Unfreeze works
- [ ] Right-click a free object → *Freeze with GHShield* and *Pin in Place*
- [ ] Right-click a group → *Freeze / Unfreeze This Group (n)*; the group's own
      padlock clears on unfreeze, and the count returns to 0
- [ ] Right-click empty canvas → Freeze / Pin / Unfreeze Selection
- [ ] With verbose logging OFF, freezing 10 objects prints nothing

## 3. The delete guard (untested as of writing — Harmony is the riskiest part)

- [ ] Freeze a component, press Delete → **nothing happens**, no flicker,
      no "was reverted" message
- [ ] Select one frozen and one normal component, press Delete → the normal one
      goes, the frozen one stays
- [ ] Right-click a frozen component → Delete → refused
- [ ] Delete the GHShield component while something is frozen → refused
- [ ] Unfreeze everything, delete the GHShield component → succeeds

If any of these revert instead of refusing, that route is not patched. Turn on
verbose logging and check whether `Delete guard installed` appeared at startup.

## 3b. Groups

- [ ] Freeze a group → every member gets the frost, the group gets a padlock
- [ ] Try to unfreeze one member → refused, and the menu offers the group
- [ ] Ungroup / Add to group / Remove from group → greyed out while frozen
- [ ] Unfreeze the group → everything comes free, the group's padlock clears
- [ ] Nothing anywhere changes position at any point

## 4. Persistence

- [ ] Freeze objects, save, close Rhino **entirely**, reopen, open the file
- [ ] `loaded protection for N object(s) from this file` appears
- [ ] The objects are still frozen and still refuse to be edited

## 5. A real definition

- [ ] Open an actual project file, few hundred objects
- [ ] Freeze a large group. Pan and zoom — no stutter
- [ ] Nothing you did NOT freeze is blocked
- [ ] The definition still computes the same results

---

## STOP — everything above must pass before packaging

---

## 6. Package

- [ ] `dotnet build -c Release`
- [ ] Assemble: `GHShield.gha`, `0Harmony.dll`, `ShieldIcon48.png`,
      `manifest.yml`, `README.md`, `LICENSE`
- [ ] Add the repo/product URL to `manifest.yml` (currently commented out —
      a dead link on a public listing is worse than no link)
- [ ] `Yak.exe build`

## 7. Install test on a CLEAN profile

This is where "works on my machine" fails. Do not skip it.

- [ ] Remove `bin\Debug` from `GrasshopperDeveloperSettings`
- [ ] Delete any GHShield copy from `%APPDATA%\Grasshopper\Libraries`
- [ ] `Yak.exe install --source . ghshield`
- [ ] Restart Rhino. Everything in sections 2–4 still works

## 8. The recipient test

- [ ] Open a protected `.gh` on a machine **without** GHShield installed
- [ ] Confirm what the missing-component placeholder looks like
- [ ] Confirm the rest of the definition still computes

Whatever this looks like is what your client sees if they have not installed
the plugin. If it looks like a broken file, say so clearly in the listing.

## 9. Before publishing

- [ ] Use it on 2–3 real jobs first. Two changes have wide blast radius:
      Harmony patches Grasshopper's document methods globally, and the runtime
      proxy swaps attributes on third-party components. Both fail open, but
      neither has met a large real definition yet
- [ ] GitHub repo created, README and LICENSE pushed
- [ ] Listing states plainly: **Rhino 7, Windows only**
- [ ] Listing does not claim encryption or security. It protects a file from
      being damaged; it does not hide the logic. Say that up front and nobody
      can call it out later
- [ ] Screenshots or a short screen recording of freezing and a failed edit

## Known limitations to state in the listing

- Rhino 7, Windows only
- The recipient must have GHShield installed for protection to apply
- Not encryption — the logic remains readable as ordinary components.
  For hiding logic, use Grasshopper's password-protected clusters
- Gene Pool is protected via the runtime proxy, not a dedicated adapter

## Dead files that can be deleted from the project

Not blockers, but they are unreferenced and will confuse anyone reading the
repo (including you, in six months):

- `MoveGuard.cs` — superseded by ProtectionService
- `GraphMapperHook.cs` — a forwarder to AdapterHook, no longer called
- `UI/GHShieldGenePoolAttributes.cs` — comment-only placeholder
- `Hooks/ContextMenuHook.cs` — empty stub, still called from DocumentHook;
  remove the call at the same time
