import 'package:drift/drift.dart';
import 'package:drift_flutter/drift_flutter.dart';

part 'app_database.g.dart';

class Notes extends Table {
  IntColumn get id => integer().autoIncrement()();
  TextColumn get content => text()();
  DateTimeColumn get createdAt => dateTime()();
}

@DriftDatabase(tables: [Notes])
class AppDatabase extends _$AppDatabase {
  AppDatabase(super.e);

  @override
  int get schemaVersion => 1;

  Stream<List<Note>> watchAllNotes() => (select(notes)
        ..orderBy([(n) => OrderingTerm.desc(n.createdAt)]))
      .watch();

  Future<void> addNote(String content) => into(notes).insert(
        NotesCompanion.insert(
          content: content,
          createdAt: DateTime.now(),
        ),
      );

  Future<void> deleteNote(int id) =>
      (delete(notes)..where((n) => n.id.equals(id))).go();
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
