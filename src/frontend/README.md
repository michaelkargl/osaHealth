# osa_health_skeleton

Flutter web skeleton app with a Drift (SQLite/WASM) notes demo.

## Running the app

### 1. Install dependencies

```
flutter pub get
```

### 2. Download the required web assets

Drift on web requires two files in `web/`. Check your `pubspec.lock` for the exact resolved versions of `drift` and `sqlite3`, then download the matching files:

- **`sqlite3.wasm`** → [sqlite3.dart releases](https://github.com/simolus3/sqlite3.dart/releases)
- **`drift_worker.dart.js`** → [drift releases](https://github.com/simolus3/drift/releases)

Place both files in the `web/` directory:

```
web/
├── drift_worker.dart.js   ← downloaded from drift releases
├── sqlite3.wasm           ← downloaded from sqlite3.dart releases
└── index.html
```

See the [official Drift web setup docs](https://drift.simonbinder.eu/platforms/web/) for details.

### 3. Run

```
flutter run -d chrome
```

or

```
flutter run -d edge
```

## Updating dependencies

Repeat step 2 whenever you upgrade the `drift` or `sqlite3` package versions — the wasm and worker files are version-matched and must be updated together.

## Regenerating Drift code

After any schema changes, re-run the code generator:

```
dart run build_runner build --delete-conflicting-outputs
```
