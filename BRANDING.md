# Branding

This mod does not own its preview image.

The design, the palette, the rules about what may and may not appear on a preview, and
the tool that renders it all live in **`..\_Branding`**. Read
[`_Branding\README.md`](../_Branding/README.md) before changing anything visual here.

`About\Preview.png` in this repo is **generated output**. Do not hand-edit it and do not
replace it with a one-off image — the next render will overwrite it and the mod will fall
out of step with the rest of the family.

To change this mod's preview, edit its entry in `_Branding\branding.json`, then:

```powershell
cd ..\_Branding
.\Tools\Build-Previews.ps1 -Only <key> -Deploy
```

## The short version of the rules

A preview image carries the accent rule, the `RESEARCH` eyebrow, and the mod word set as
large as the margins allow. Nothing else. In particular it never carries an icon or mark,
a subtitle, an author name, the name of the game, or a version number — and the build
fails if you add any of them to the spec.
