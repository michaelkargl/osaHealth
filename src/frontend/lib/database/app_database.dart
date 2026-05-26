import 'package:drift/drift.dart';
import 'package:drift_flutter/drift_flutter.dart';

part 'app_database.g.dart';

class Recordings extends Table {
  TextColumn get id => text()();
  TextColumn get userId => text()();
  TextColumn get date => text()(); // ISO 8601 UTC: "2026-05-26T09:00:00Z"
  IntColumn get systolic => integer()();
  IntColumn get diastolic => integer()();
  DateTimeColumn get syncedAt => dateTime().nullable()();

  @override
  Set<Column> get primaryKey => {id};
}

@DriftDatabase(tables: [Recordings])
class AppDatabase extends _$AppDatabase {
  AppDatabase(super.e);

  @override
  int get schemaVersion => 2;

  @override
  MigrationStrategy get migration => MigrationStrategy(
        onCreate: (m) => m.createAll(),
        onUpgrade: (m, from, to) async {
          await m.recreateAllViews();
          await customStatement('DROP TABLE IF EXISTS notes');
          await m.createAll();
        },
      );

  Stream<List<Recording>> watchAllRecordings() =>
      (select(recordings)
            ..orderBy([(r) => OrderingTerm.desc(r.date)]))
          .watch();

  Future<void> addRecording({
    required String id,
    required String userId,
    required String date,
    required int systolic,
    required int diastolic,
  }) =>
      into(recordings).insert(
        RecordingsCompanion.insert(
          id: id,
          userId: userId,
          date: date,
          systolic: systolic,
          diastolic: diastolic,
        ),
      );

  Future<List<Recording>> getUnsyncedRecordings() =>
      (select(recordings)..where((r) => r.syncedAt.isNull())).get();

  Future<void> markSynced(String id) =>
      (update(recordings)..where((r) => r.id.equals(id)))
          .write(RecordingsCompanion(syncedAt: Value(DateTime.now().toUtc())));
}

AppDatabase openDatabase() => AppDatabase(
      driftDatabase(
        name: 'osa_health',
        web: DriftWebOptions(
          sqlite3Wasm: Uri.parse('sqlite3.wasm'),
          driftWorker: Uri.parse('drift_worker.dart.js'),
        ),
      ),
    );
