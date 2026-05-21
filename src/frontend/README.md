# osa_health_skeleton

Flutter web skeleton app with a Drift (SQLite/WASM) notes demo.

## Prerequisites

- [Flutter SDK](https://docs.flutter.dev/get-started/install) (3.x or later)
- Run `flutter doctor` to verify your installation before proceeding

## Running the app

### 1. Install dependencies

```
flutter pub get
```

### 2. Copy required web assets

`drift_flutter` ships a setup script that copies `sqlite3.wasm` and `drift_worker.dart.js` into `web/`. Run it once per environment, and again whenever you upgrade `drift` or `sqlite3`:

```
dart run drift_flutter:setup
```

After this step you should see both files in `web/`:

```
web/
├── drift_worker.dart.js
├── sqlite3.wasm
└── index.html
```

If `dart` is not on your PATH, use Flutter's bundled dart:

```
flutter pub run drift_flutter:setup
```

### 3. Regenerate Drift code (first time or after schema changes)

```
dart run build_runner build --delete-conflicting-outputs
```

### 4. Run

```
flutter run -d chrome
```

or

```
flutter run -d edge
```

## Updating dependencies

Repeat steps 1–2 whenever you upgrade `drift` or `sqlite3` — the wasm and worker files are version-matched and must be updated alongside the packages.
