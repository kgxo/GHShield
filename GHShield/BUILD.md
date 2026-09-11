# Building and packaging GHShield

## Build

**Close Rhino first.** Rhino locks `GHShield.gha` while running; building with
it open silently leaves the old file in place and you end up testing stale code.

    Build > Rebuild Solution

Output: `bin\Debug\GHShield.gha` and `bin\Debug\0Harmony.dll`.

## Debugging

The `Rhino 7` launch profile starts Rhino with the debugger attached (F5).
Alternatively load from `GrasshopperDeveloperSettings` — add `bin\Debug`, and
untick "Memory load *.GHA assemblies using COFF byte arrays".

Keep only ONE copy of the .gha on the machine. Grasshopper scans the developer
folder recursively, so leftover `net48\` or `net7.0\` folders cause File
Conflict dialogs and misleading SDK-version errors.

## Packaging for Food4Rhino / Package Manager

1. Build in **Release**:

       dotnet build -c Release

2. Assemble a package folder containing:

       GHShield.gha
       0Harmony.dll          <- required, the .gha will not load without it
       ShieldIcon48.png
       manifest.yml
       README.md

3. Build the Yak package:

       "C:\Program Files\Rhino 7\System\Yak.exe" build

   This produces `ghshield-1.0.0-rh7_0-win.yak`.

4. Test the package locally before publishing:

       "C:\Program Files\Rhino 7\System\Yak.exe" install --source . ghshield

5. Publish (needs a Rhino account, one-time login):

       "C:\Program Files\Rhino 7\System\Yak.exe" login
       "C:\Program Files\Rhino 7\System\Yak.exe" push ghshield-1.0.0-rh7_0-win.yak

The csproj also has an opt-in Yak target:

    dotnet build -c Release -p:BuildYakPackage=true

## Food4Rhino listing

Food4Rhino is a listing, not a distribution channel — the actual install comes
from the Package Manager once you have pushed the .yak. For the listing you
need: name, one-line summary, description (the README works), the icon,
two or three screenshots showing frozen components, and a link to the repo.
