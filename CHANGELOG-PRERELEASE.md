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

### Security

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
## [0.2.0-beta.2] - 2023-09-01
## [0.2.0-beta.1] - 2023-08-29
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
## [0.1.0-rc.1] - 2023-06-01
### Added
- Changelog [`#11`](https://github.com/anatawa12/ContinuousAvatarUploader/pull/11)

## [0.1.0-beta.1]
### Added
- Sleep between upload [`ccdec7a`](https://github.com/anatawa12/ContinuousAvatarUploader/commit/ccdec7a6893877890c572f19cc7a4e575c4464ec)
- Aborting Upload [`4ac239c`](https://github.com/anatawa12/ContinuousAvatarUploader/commit/4ac239c1022c4011b2a15baf5a152d6bcd0ff358)
- Workaround for Pipeline Manager is not marked as dirty [`9def9a7`](https://github.com/anatawa12/ContinuousAvatarUploader/commit/9def9a76312122eda4e1594822dbde521627460a)
- Uploading prefab avatars [`40f483a`](https://github.com/anatawa12/ContinuousAvatarUploader/commit/40f483a21ec5cedb83077035f6995b80974f0f91)
- Upload thumbnail every upload [`a6bb041`](https://github.com/anatawa12/ContinuousAvatarUploader/commit/a6bb041ded4aee915e40b1017d93baee30c2a0eb)

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

[Unreleased]: https://github.com/anatawa12/ContinuousAvatarUploader/compare/v0.2.5...HEAD
[0.2.5]: https://github.com/anatawa12/ContinuousAvatarUploader/compare/v0.2.4...v0.2.5
[0.2.4]: https://github.com/anatawa12/ContinuousAvatarUploader/compare/v0.2.3...v0.2.4
[0.2.3]: https://github.com/anatawa12/ContinuousAvatarUploader/compare/v0.2.2...v0.2.3
[0.2.2]: https://github.com/anatawa12/ContinuousAvatarUploader/compare/v0.2.1...v0.2.2
[0.2.1]: https://github.com/anatawa12/ContinuousAvatarUploader/compare/v0.2.0...v0.2.1
[0.2.0]: https://github.com/anatawa12/ContinuousAvatarUploader/compare/v0.2.0-beta.2...v0.2.0
[0.2.0-beta.2]: https://github.com/anatawa12/ContinuousAvatarUploader/compare/v0.2.0-beta.1...v0.2.0-beta.2
[0.2.0-beta.1]: https://github.com/anatawa12/ContinuousAvatarUploader/compare/v0.1.0...v0.2.0-beta.1
[0.1.0]: https://github.com/anatawa12/ContinuousAvatarUploader/compare/v0.1.0-rc.1...v0.1.0
[0.1.0-rc.1]: https://github.com/anatawa12/ContinuousAvatarUploader/compare/v0.1.0-beta.1...v0.1.0-rc.1
[0.1.0-beta.1]: https://github.com/anatawa12/ContinuousAvatarUploader/compare/v0.0.3...0.1.0-beta.1
[0.0.3]: https://github.com/anatawa12/ContinuousAvatarUploader/compare/v0.0.2...v0.0.3
[0.0.2]: https://github.com/anatawa12/ContinuousAvatarUploader/compare/v0.0.1...v0.0.2
[0.0.1]: https://github.com/anatawa12/ContinuousAvatarUploader/releases/tag/v0.0.1
