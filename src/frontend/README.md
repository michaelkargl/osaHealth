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

### 2. Download required web assets

Drift's web backend requires two files in `web/` that are downloaded from GitHub releases. The versions **must match** your `pubspec.lock`.

**Find your exact versions** (check `pubspec.lock` after `flutter pub get`):
- `sqlite3` version → needed for `sqlite3.wasm`
- `drift` version → needed for `drift_worker.dart.js`

**Download the files:**
- `sqlite3.wasm` from https://github.com/simolus3/sqlite3.dart/releases — pick the release matching your `sqlite3` version
- `drift_worker.dart.js` from https://github.com/simolus3/drift/releases — pick the release matching your `drift` version

**Place both files in `web/`:**

```
web/
├── drift_worker.dart.js
├── sqlite3.wasm
└── index.html
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

## Upgrading drift or sqlite3

The `sqlite3.wasm` and `drift_worker.dart.js` files in `web/` are version-matched to specific package releases. After upgrading either package, the files must be replaced with matching versions.

**Steps:**

1. Upgrade the packages in `pubspec.yaml` and run:
   ```
   flutter pub upgrade drift drift_flutter sqlite3
   ```

2. Check `pubspec.lock` for the new resolved versions of `sqlite3` and `drift`.

3. Download the updated files matching the new versions:
   - `sqlite3.wasm` from https://github.com/simolus3/sqlite3.dart/releases
   - `drift_worker.dart.js` from https://github.com/simolus3/drift/releases

4. Replace `web/sqlite3.wasm` and `web/drift_worker.dart.js` with the downloaded files.

5. If the Drift schema changed, regenerate the generated code:
   ```
   dart run build_runner build --delete-conflicting-outputs
   ```

6. Run the app and verify the database initializes without errors.
