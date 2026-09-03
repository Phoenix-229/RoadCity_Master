# Changelog
All notable changes to this RoadArchitect project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).


## [Unreleased] — Road & City Master, city layer

### Changed — read this if your terrain footprint looks different

**The road now moves far less dirt.** Terraforming used to derive one width from the road and use it for three
different jobs: how much ground to flatten, how far to blend back to natural terrain, and how wide to paint the
splat map. Because one number did all three, a road flattened a terrace far wider than the road itself, and the
flattened band ended in a hard step instead of easing back into the hillside.

These are now three independent quantities. The flattened band follows the emitted cross-section at each station
plus a shoulder, so a flare flattens wider and a narrow lane narrower; the blend runs a smoothstep from the edge of
that band out to natural ground, meeting both ends tangentially so there is no crease; and the painted width comes
from the surface type (narrow when sealed, wide when unsealed) rather than from the deform width.

This is the **only** intended behavioural change to the standalone terraform path in this release. If your terrain
footprint changed after upgrading, this is why, and nothing else in the upgrade is responsible. If you need the old
footprint, raise the shoulder and blend distance in the footprint class until it matches; nothing was removed.

Before and after: `ManualImages/terrain-footprint-before.png`, `ManualImages/terrain-footprint-after.png`.

### Added

- **Dual-mode terrain.** With a terrain provider connected, every terrain modification is *requested* through it and
  the provider owns the terrain, so deleting a road restores real ground rather than a stored copy. With no provider
  the tool shapes the terrain itself, exactly as it always did. The mode is decided once per scene from what is
  actually in the scene; neither mode is a degraded version of the other.
- **A stated availability table.** Every feature that needs a provider now says so in one sentence naming what to
  connect, in the inspector and in the manual. Nothing needing a provider silently does nothing.


## [Version: 3.0.0] - 2023.09.14
### Improvements
- Removed FormerlySerializedAs update code


## [Version: 2.5.0] - 2023.09.14
### Improvements
- Refactored DestroyImmediate usage
- Changed temporary material variables to private
- Added an option to disable road updates
- Refactored NullifyList method
- Refactored clamping of construction values
- Refactored setup of edge object maker
- Refactored deletion in road constructor
- Refactored deletion in intersection cleanup
- Removed redundant EdgeObjectEditorMaker
- Removed redundant SplinatedMeshEditorMaker
- Removed gizmo toggle on nodes
- Refactored road materials reset
- Refactored ToggleWireframes
- Removed editorDisplayString in node
- Removed isEditorSelected in node
- Removed unused intersection gizmos
- Refactored wizard window type
- Refactored node dragging in editor
- Added unit test 10
- Removed unused ProgressBar
- Changed Remove all button
- Refactored RemoveAllEdgeObjects
- Refactored RemoveAllSplinatedObjects
- Added OnDrawGizmosSelected for intersections
- Refactored ConstructRoadStoreTerrainHistory
- Simplified ConstructRoadStoreTerrainHistory
- Removed sortID code
- Moved editor specific code into EngineIntegration


## [Version: 2.4.0] - 2021.09.22
### Improvements
- Added automatic rename of edge objects (Feature #29)
- Added automatic rename of splinated objects (Feature #29)


## [Version: 2.3.0] - 2021.09.19
### Improvements
- Removed runtime blockers
- Removed unused Notification script
- Changed EditorMenu namespace to RoadArchitect
- Renamed EditorMenu
- Changed Road to use its own settings instead of RoadSystem settings
- Implemented bridge and intersection view


## [Version: 2.2.0] - 2021.07.29
### Improvements
- Upgraded materials to use standard shader


## [Version: 2.1.0] - 2021.07.25
### Improvements
- Implemented GUID based GetBasePath
- Cache base path
- Fixed cross platform GetBasePathForIO
- Fixed oncoming rotation of edge objects
- Implemented rotation locking


## [Version: 2.0.2] - 2021.07.04
### Improvements
- Fixed meta file of RigidBody
- Fixed regression with versions prior to 2019.2


## [Version: 2.0.0] - 2021.06.09
### Improvements
- Refactor of Editor code
- Removed "Buffers" from Scripts
- Refactor of IsApproximately
- Updated RoadUtility to use Unity 2019.2 API
- Added an option to change the desired height of the ramp
- Refactor of Profiling
- Added the ability to change default and selected gizmo color
- Added the ability to change the preview gizmo color for a new Node
- Added Offline Manual buttons to the Inspectors
- Removed unused using directives
- Fixed some issues with the quick help inspector
- Fixed an issue, which caused long names of physic materials
- Instantiating EdgeObjects as Prefabs
- Improved performance of CheckCreateSpecialLibraryDirs
- Improved performance of SplineNEditor Init function
- Reduced target casting in Editor scripts
- Refactored Init function of SplineNEditor
- Improved performance of RoadEditor Init function
- Improved performance of TerrainEditor Init function
- Improved performance of RoadSystemEditor Init function
- Improved performance of RoadIntersectionEditor Init function
- Refactored CheckLoadTexture into EditorUtilities
- Removed unused editor timer
- Unified DrawLine in EditorUtilities
- Unified SetupUniqueIdentifier
- Improved performance by reducing the frequency at which GetBasePath is called
- Added a warning for Tests
- Changed TestCodeCount to support more folders
- Fixed additional issues with physic material names
- Changed terrains to use heightmapResolution
- Moved all scripts into RoadArchitect namespace
- Added code summaries for many methods
- Renamed scripts
- Separated some scripts into new files
- Refactored Intersections Nullify
- Reorganized folder hierarchy
- Changed extension of library files to .rao
- Removed redundant directory queries
- Reworked building behavior of RoadArchitect
- Updated runtime usage of RoadArchitect
- Added unit test 7 and 8
- Refactored material assignments
- Extracted material assignments from inner loops
- Removed Editor limitation for node setup
- Removed unused variables
- Added unit test 9
- Refactored stop sign rigidbody creation
- Refactored DrawGizmos of SplineN
- Fixed terrain deformations when heightmapResolution was increased (Bug #21)
- Fixed update error when heightmapResolution was decreased (Bug #21)
- Simplified remove of old terrain histories
- Updated HelpWindow
- Updated supported folder locations
- Fixed regression with versions prior to 2018.1
- Improved cross platform compatibility
- Fixed StreetLight positions and updates
- Disabled horizontal collider on traffic lights
- Fixed traffic light positioning (Bug #14)
- Fixed update of intersection street light values


## [Version: 1.9_FH] - 2019.04.22
### Improvements
- Improved the Help Window with new Layout and Links
- Redone #if UNITY_EDITOR in Scripts
- Removed an empty function

### Changed
- Changed links, which redirected to Github, with the Github Wiki Link of the Master. Closed embeddedt/RoadArchitect/issues/6
- Outcommented unused using directives
- Changed LICENSE to an md file
- Minor Layout changes
- Deleted GSDEditorSkin, since it has no purpose at all

### Added
- Added missing private attribute on some Vars and Functions
- Added "Imports" Regions in the Scripts for better overview
- Added a "Report a Bug" MenuItem, which links to https://github.com/embeddedt/RoadArchitect/issues
- Added/Updated some regions


## [Version: 1.8_FH] - 2019.02.10
### Improvements
- Added some FH_Tag Optimizable as comments, since there is a way to further optimize the Code tagged by this
- Added a few Lines of comments to the code, to get a better idea of what the code does

### Changed
- Changed some Layouts of the Scripts
- Changed some Vars in Scripts to better reflect their purpose

### Added
- Added a few Regions to some Scripts

### Fixed
- Fixed embeddedt/RoadArchitect/issues/4


## [Version: 1.7.5a_FH] - 2019.01.31
### Changed
- Changed most Layouts of the Scripts
- Changed some Vars in Scripts to better reflect their purpose


## [Version 1.7] - 2018.07.16
### Changed
- Adds support for Unity 2018.x
- Updates to unit tests
- No further Informations given at this point


## [Version 1.6] - 2017.02.28
### Changed
- Initial Release
- No further Informations given at this point
