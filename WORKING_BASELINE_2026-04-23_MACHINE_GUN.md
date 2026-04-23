# Working Baseline - Machine Gun Event

Date: 2026-04-23

Current saved state:
- Unity app A keeps the VSeeFace-centered realtime reaction flow.
- Existing event routing remains:
  - F6: single shot
  - F7: shotgun
  - F8: sticker box
- New event added:
  - Name: 기관총
  - Test key: F5
  - Behavior: fires head-collidable random projectile props continuously for 1.5 seconds.
  - Uses the existing single-shot projectile path, downloaded projectile pool, projectile scale slider, and head impact reaction.
- Command custom UI now includes 기관총 as an event option.
- Chat/donation router can resolve machinegun / Machine Gun / 기관총 to the new machine gun launch path.

Important files for this baseline:
- Assets/Scripts/SimpleLauncher.cs
- Assets/Scripts/ChatOrDonationRouter.cs
- Assets/Scripts/LauncherDebugUI.cs

Recovery note:
- If a future change breaks projectile routing, return to this behavior first:
  - F5 calls SimpleLauncher.LaunchMachineGun().
  - Machine gun internally reuses the single-shot config but bypasses normal cooldown during its 1.5 second burst.
  - F6/F7/F8 remain handled by ChatOrDonationRouter.
