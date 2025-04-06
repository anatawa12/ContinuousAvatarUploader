# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog].

[Keep a Changelog]: https://keepachangelog.com/en/1.0.0/

## [Unreleased]
### Added

### Changed

### Deprecated

### Removed

### Fixed
- Config changes are not saved `#98`

### Security

## [0.3.6] - 2025-04-06
### Fixed
- Incompatibility with older VRCSDK [`#96`](https://github.com/anatawa12/ContinuousAvatarUploader/pull/96)

## [0.3.5] - 2025-04-05
### Fixed
- Upload this avatar button condition is inverted [`#93`](https://github.com/anatawa12/ContinuousAvatarUploader/pull/93)

## [0.3.4] - 2025-04-05
### Added
- Multi Editing Support for AvatarUploadsSetting [`#91`](https://github.com/anatawa12/ContinuousAvatarUploader/pull/91)
  - You now can select multiple AvatarUploadSetting and edit them at once.

### Changed
- Declare compatibility with VRCSDK 3.8.x [`#90`](https://github.com/anatawa12/ContinuousAvatarUploader/pull/90)

### Fixed
- Unloaded scenes would be loaded upon upload completion [`#85`](https://github.com/anatawa12/ContinuousAvatarUploader/pull/85)
- Added a `Upload All` button for `AvatarUploadSettingGroupGroup` [`#78`](https://github.com/anatawa12/ContinuousAvatarUploader/pull/78)
- Fails to upload new avatar due to control panel problem [`#92`](https://github.com/anatawa12/ContinuousAvatarUploader/pull/92)
- This faile was already uploaded error if thumbnail doesn't changed [`#92`](https://github.com/anatawa12/ContinuousAvatarUploader/pull/92)

## [0.3.3] - 2024-10-29
### Added
- iOS platform support [`#76`](https://github.com/anatawa12/ContinuousAvatarUploader/pull/76)
- Group Group asset, which can group multiple AvatarGroup assets [`#76`](https://github.com/anatawa12/ContinuousAvatarUploader/pull/76)

### Changed
- Uploader window improvement [`#76`](https://github.com/anatawa12/ContinuousAvatarUploader/pull/76)

### Fixed
- Camera Position Editor is not working if two or more platforms are enabled [`#76`](https://github.com/anatawa12/ContinuousAvatarUploader/pull/76)
- Camera Position Editor is not working fom Group editor [`#76`](https://github.com/anatawa12/ContinuousAvatarUploader/pull/76)
- Upload in progress is shown after the upload is finished [`#76`](https://github.com/anatawa12/ContinuousAvatarUploader/pull/76)
- Automatic enabling avatar is not working for nested avatar [`#76`](https://github.com/anatawa12/ContinuousAvatarUploader/pull/76)

## [0.3.2] - 2024-08-17
### Added
- VRCSDK 3.7.0 compatibility [`#69`](https://github.com/anatawa12/ContinuousAvatarUploader/pull/69)
  - Actually, it was compatible so this is just a dependency declaration update.

## [0.3.1] - 2024-05-10
### Fixed
- VRCSDK 3.6.0 compatibility [`#63`](https://github.com/anatawa12/ContinuousAvatarUploader/pull/63)
  - It was working with VRCSDK 3.6.0-beta.1 but VRChat breaks API in VRCSDK 3.6.0.
  - We added version defines to determine if VRCSDK 3.6.0 or later, or not.

## [0.3.0] - 2024-05-07
### Added
- Change avatar order in Avatar Group [`#53`](https://github.com/anatawa12/ContinuousAvatarUploader/pull/53)
- Show index in Avatar Group [`#53`](https://github.com/anatawa12/ContinuousAvatarUploader/pull/53)
- Create Group of Variants from Prefab [`#54`](https://github.com/anatawa12/ContinuousAvatarUploader/pull/54)
- Create Group from Selected Avatars [`#54`](https://github.com/anatawa12/ContinuousAvatarUploader/pull/54)
- Upload button on inspector [`#55`](https://github.com/anatawa12/ContinuousAvatarUploader/pull/55)

### Changed
- Clear `Avatar to add` on `Add Avatar` button clicked [`#53`](https://github.com/anatawa12/ContinuousAvatarUploader/pull/53)
- Save sub-asset on adding/removing Avatar Group [`#53`](https://github.com/anatawa12/ContinuousAvatarUploader/pull/53)
- Unified the Setting list and Group list in the window [`#58`](https://github.com/anatawa12/ContinuousAvatarUploader/pull/58)

### Removed
- Unity 2019 support [`#57`](https://github.com/anatawa12/ContinuousAvatarUploader/pull/57)

### Fixed
- Opening the Group with many avatars is slow [`#55`](https://github.com/anatawa12/ContinuousAvatarUploader/pull/55)
- Error with Enter Play Mode disabled [`#56`](https://github.com/anatawa12/ContinuousAvatarUploader/pull/56)

## [0.2.6] - 2023-12-09
### Added
- Progress bar [`#39`](https://github.com/anatawa12/ContinuousAvatarUploader/pull/39)
- `Tools/Continuous Avatar Uploader` [`#40`](https://github.com/anatawa12/ContinuousAvatarUploader/pull/40)
- Support for VRCSDK 3.5.x [`#44`](https://github.com/anatawa12/ContinuousAvatarUploader/pull/44)

## [0.2.5] - 2023-11-14
### Fixed
- use name instead of GUID in asmdef [`#30`](https://github.com/anatawa12/ContinuousAvatarUploader/pull/30)

## [0.2.4] - 2023-11-03
### Fixed
- Disabling for platform is not working [`#29`](https://github.com/anatawa12/ContinuousAvatarUploader/pull/29)

## [0.2.3] - 2023-10-08
## [0.2.2] - 2023-10-05
### Fixed
- Null Reference Exception with None AvatarGroup(s) on the window [`#27`](https://github.com/anatawa12/ContinuousAvatarUploader/pull/27)

## [0.2.1] - 2023-09-14
### Fixed
 - Fixed an issue where the GUI was not rendering well when never built. [`#25`](https://github.com/anatawa12/ContinuousAvatarUploader/pull/25)

## [0.2.0] - 2023-09-12
### Added
- Support for VRCSDK 3.3.0 [`#18`](https://github.com/anatawa12/ContinuousAvatarUploader/pull/18)
- Option to show dialog when uploading finished [`#21`](https://github.com/anatawa12/ContinuousAvatarUploader/pull/21)

### Changed
- Thumbnails are taken in EditMode by default [`#18`](https://github.com/anatawa12/ContinuousAvatarUploader/pull/18)
  - You can change in uploader window OR for each avatars

### Removed
- Support for VRCSDK 3.2.x or older [`#18`](https://github.com/anatawa12/ContinuousAvatarUploader/pull/18)
- Harmony0.dll [`#20`](https://github.com/anatawa12/ContinuousAvatarUploader/pull/20)

### Fixed
- Incompatible with tools with Harmony by removing embedded Harmony [`#20`](https://github.com/anatawa12/ContinuousAvatarUploader/pull/20)

## [0.1.0] - 2023-06-01
### Added
- Sleep between upload [`ccdec7a`](https://github.com/anatawa12/ContinuousAvatarUploader/commit/ccdec7a6893877890c572f19cc7a4e575c4464ec)
- Aborting Upload [`4ac239c`](https://github.com/anatawa12/ContinuousAvatarUploader/commit/4ac239c1022c4011b2a15baf5a152d6bcd0ff358)
- Workaround for Pipeline Manager is not marked as dirty [`9def9a7`](https://github.com/anatawa12/ContinuousAvatarUploader/commit/9def9a76312122eda4e1594822dbde521627460a)
- Uploading prefab avatars [`40f483a`](https://github.com/anatawa12/ContinuousAvatarUploader/commit/40f483a21ec5cedb83077035f6995b80974f0f91)
- Upload thumbnail every upload [`a6bb041`](https://github.com/anatawa12/ContinuousAvatarUploader/commit/a6bb041ded4aee915e40b1017d93baee30c2a0eb)
- Changelog [`#11`](https://github.com/anatawa12/ContinuousAvatarUploader/pull/11)

### Changed
- Rename AvatarDescriptor -> AvatarUploadSetting [`fbd937a`](https://github.com/anatawa12/ContinuousAvatarUploader/commit/fbd937a01dbaaec9accd6e6d2ed9b2f72f27dce4)

## [0.0.3] - 2023-05-29
### Fixed
- Not working with non active avatar [`51a05b3`](https://github.com/anatawa12/ContinuousAvatarUploader/commit/51a05b353ac41350091ec5995be2f7e0d00edd77)

## [0.0.2] - 2023-05-29
### Added
- 日本語README [`48f2de7`](https://github.com/anatawa12/ContinuousAvatarUploader/commit/48f2de7288c2f276e528b134efaec8041b990438)
- Installation [`c22d430`](https://github.com/anatawa12/ContinuousAvatarUploader/commit/c22d4302df2b5c290300f1c0f4a800850dbe756e)
- 使い方 [`8ff789c`](https://github.com/anatawa12/ContinuousAvatarUploader/commit/8ff789ca0d7f90749abcd5359788afc6e96a9836)

### Fixed
- If the window is offscreen, CAU stops [`c2f0f32`](https://github.com/anatawa12/ContinuousAvatarUploader/commit/c2f0f3252adfb8677404d9ab67f8dee62ee53988)
- Editor not responding with non active avatars [`31c81f1`](https://github.com/anatawa12/ContinuousAvatarUploader/commit/31c81f1b7e796f43bb122596995f88711814a10c)

## [0.0.1] - 2023-05-27
### Added
- Basic features of ContinuousAvatarUploader

[Unreleased]: https://github.com/anatawa12/ContinuousAvatarUploader/compare/v0.3.6...HEAD
[0.3.6]: https://github.com/anatawa12/ContinuousAvatarUploader/compare/v0.3.5...v0.3.6
[0.3.5]: https://github.com/anatawa12/ContinuousAvatarUploader/compare/v0.3.4...v0.3.5
[0.3.4]: https://github.com/anatawa12/ContinuousAvatarUploader/compare/v0.3.3...v0.3.4
[0.3.3]: https://github.com/anatawa12/ContinuousAvatarUploader/compare/v0.3.2...v0.3.3
[0.3.2]: https://github.com/anatawa12/ContinuousAvatarUploader/compare/v0.3.1...v0.3.2
[0.3.1]: https://github.com/anatawa12/ContinuousAvatarUploader/compare/v0.3.0...v0.3.1
[0.3.0]: https://github.com/anatawa12/ContinuousAvatarUploader/compare/v0.2.6...v0.3.0
[0.2.6]: https://github.com/anatawa12/ContinuousAvatarUploader/compare/v0.2.5...v0.2.6
[0.2.5]: https://github.com/anatawa12/ContinuousAvatarUploader/compare/v0.2.4...v0.2.5
[0.2.4]: https://github.com/anatawa12/ContinuousAvatarUploader/compare/v0.2.3...v0.2.4
[0.2.3]: https://github.com/anatawa12/ContinuousAvatarUploader/compare/v0.2.2...v0.2.3
[0.2.2]: https://github.com/anatawa12/ContinuousAvatarUploader/compare/v0.2.1...v0.2.2
[0.2.1]: https://github.com/anatawa12/ContinuousAvatarUploader/compare/v0.2.0...v0.2.1
[0.2.0]: https://github.com/anatawa12/ContinuousAvatarUploader/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/anatawa12/ContinuousAvatarUploader/compare/v0.0.3...v0.1.0
[0.0.3]: https://github.com/anatawa12/ContinuousAvatarUploader/compare/v0.0.2...v0.0.3
[0.0.2]: https://github.com/anatawa12/ContinuousAvatarUploader/compare/v0.0.1...v0.0.2
[0.0.1]: https://github.com/anatawa12/ContinuousAvatarUploader/releases/tag/v0.0.1
