# Changelog

## [0.1.0] - 2026-11-24
- Initial version: SaveSystem API (Save, Load, LoadWithScene, slots with metadata, Preferences,
  migrations, events), SaveData key-value container with Unity type converters.
- SaveableEntity, SaveableBehaviour<T>, SaveTransform, SaveActiveState, AutoSaver.
- FileStorage with atomic writes and backup recovery; MemoryStorage; ISaveStorage for custom storage.
- Optional GZip compression and AES-256 encryption with a self-describing file header.
- Editor: Save Browser, Open Save Folder, Delete All Saves, Create Demo Scene, SaveableEntity inspector.
- 15 PlayMode tests.
