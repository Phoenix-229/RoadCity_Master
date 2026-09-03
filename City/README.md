# City

The entire city layer of Road & City Master lives under this one folder. That is deliberate: it is a
single extension point, so deleting `City/` gets the plain road tool back and any review of the city
work has one place to look.

```
City/
├── Runtime/     Network, Blocks, Buildings, Zoning, Streetscape, Data, Integration, Math
├── Editor/      Windows, Inspectors, SceneTools, Icons
└── Content/     Prefabs, Materials, Meshes, Textures
```

`Runtime/` and `Editor/` are split at the top from the very first folder, before either holds
anything. Editor-only code compiled into a runtime assembly is a build error waiting for the first
player build, and it costs more the later it is found (RM-05, § 10.7).

Note that `City/Editor/` is **not** editor-only by virtue of its name. Unity's "Editor folder" special
case applies to the predefined assemblies; once an `.asmdef` governs a folder its platform list decides,
and a nested `Editor` folder is just a folder. Measured before RM-06 changed anything, everything here
compiled into the runtime assembly. RM-06 fixed that: `City/Editor/RoadCityMaster.Editor.asmref` folds
this folder into `RoadCityMaster.Editor`, whose `includePlatforms` is `["Editor"]`, so editor code placed
here now cannot reach a player build even without a `#if UNITY_EDITOR` guard. There is deliberately no
`Resources` folder anywhere in this tree: one would pull its entire contents into every build
unconditionally.

Empty on purpose as of RM-05. The per-folder `README.md` files say what belongs where and exist so
the tree survives a clean checkout; delete each one once its folder has real content.
