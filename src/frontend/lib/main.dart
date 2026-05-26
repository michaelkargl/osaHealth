import 'dart:async';
import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'package:uuid/uuid.dart';

import 'database/app_database.dart';

const _backendUrl = 'http://localhost:5000';
const _hardcodedUserId = 'test-user-1';
final _uuid = Uuid();

void main() {
  runApp(OsaHealthApp(db: openDatabase()));
}

class OsaHealthApp extends StatelessWidget {
  final AppDatabase db;

  const OsaHealthApp({super.key, required this.db});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'osaHealth — Walking Skeleton',
      theme: ThemeData(
        colorScheme: ColorScheme.fromSeed(seedColor: Colors.teal),
        useMaterial3: true,
      ),
      home: RecordingsPage(db: db),
    );
  }
}

class RecordingsPage extends StatefulWidget {
  final AppDatabase db;

  const RecordingsPage({super.key, required this.db});

  @override
  State<RecordingsPage> createState() => _RecordingsPageState();
}

class _RecordingsPageState extends State<RecordingsPage> {
  final _systolicCtrl = TextEditingController();
  final _diastolicCtrl = TextEditingController();
  DateTime _selectedDate = DateTime.now().toUtc();
  Timer? _syncTimer;

  @override
  void initState() {
    super.initState();
    _syncTimer = Timer.periodic(const Duration(seconds: 30), (_) => _runSync());
    _runSync();
  }

  @override
  void dispose() {
    _syncTimer?.cancel();
    _systolicCtrl.dispose();
    _diastolicCtrl.dispose();
    super.dispose();
  }

  Future<void> _pickDate() async {
    final picked = await showDatePicker(
      context: context,
      initialDate: _selectedDate,
      firstDate: DateTime(2000),
      lastDate: DateTime.now(),
    );
    if (picked != null) {
      setState(() => _selectedDate = picked.toUtc());
    }
  }

  Future<void> _addRecording() async {
    final sys = int.tryParse(_systolicCtrl.text.trim());
    final dia = int.tryParse(_diastolicCtrl.text.trim());
    if (sys == null || dia == null) return;

    final id = _uuid.v4();
    final date = _selectedDate.toIso8601String().replaceFirst(
          RegExp(r'\.\d+$'),
          'Z',
        );

    await widget.db.addRecording(
      id: id,
      userId: _hardcodedUserId,
      date: date,
      systolic: sys,
      diastolic: dia,
    );

    _systolicCtrl.clear();
    _diastolicCtrl.clear();
    _runSync();
  }

  Future<void> _runSync() async {
    final unsynced = await widget.db.getUnsyncedRecordings();
    for (final r in unsynced) {
      try {
        final response = await http
            .post(
              Uri.parse('$_backendUrl/recordings'),
              headers: {'Content-Type': 'application/json'},
              body: jsonEncode({
                'id': r.id,
                'userId': r.userId,
                'date': r.date,
                'systolic': r.systolic,
                'diastolic': r.diastolic,
              }),
            )
            .timeout(const Duration(seconds: 10));

        if (response.statusCode == 200 || response.statusCode == 201) {
          await widget.db.markSynced(r.id);
        }
      } catch (_) {
        // backend not reachable — leave unsynced, retry next cycle
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        backgroundColor: Theme.of(context).colorScheme.inversePrimary,
        title: const Text('Blood Pressure Recordings'),
      ),
      body: Column(
        children: [
          Padding(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Row(
                  children: [
                    const Text('Date: '),
                    TextButton(
                      onPressed: _pickDate,
                      child: Text(
                        '${_selectedDate.year}-'
                        '${_selectedDate.month.toString().padLeft(2, '0')}-'
                        '${_selectedDate.day.toString().padLeft(2, '0')}',
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 8),
                Row(
                  children: [
                    Expanded(
                      child: TextField(
                        controller: _systolicCtrl,
                        keyboardType: TextInputType.number,
                        decoration: const InputDecoration(
                          labelText: 'Systolic (mmHg)',
                          border: OutlineInputBorder(),
                        ),
                      ),
                    ),
                    const SizedBox(width: 12),
                    Expanded(
                      child: TextField(
                        controller: _diastolicCtrl,
                        keyboardType: TextInputType.number,
                        decoration: const InputDecoration(
                          labelText: 'Diastolic (mmHg)',
                          border: OutlineInputBorder(),
                        ),
                        onSubmitted: (_) => _addRecording(),
                      ),
                    ),
                    const SizedBox(width: 12),
                    FilledButton(
                      onPressed: _addRecording,
                      child: const Text('Save'),
                    ),
                  ],
                ),
              ],
            ),
          ),
          const Divider(height: 1),
          Expanded(
            child: StreamBuilder<List<Recording>>(
              stream: widget.db.watchAllRecordings(),
              builder: (context, snapshot) {
                if (snapshot.connectionState == ConnectionState.waiting) {
                  return const Center(child: CircularProgressIndicator());
                }
                final records = snapshot.data ?? [];
                if (records.isEmpty) {
                  return const Center(
                    child: Text(
                      'No recordings yet — add one above.',
                      style: TextStyle(color: Colors.grey),
                    ),
                  );
                }
                return ListView.builder(
                  itemCount: records.length,
                  itemBuilder: (context, index) {
                    final r = records[index];
                    final synced = r.syncedAt != null;
                    return ListTile(
                      leading: Icon(
                        synced ? Icons.cloud_done : Icons.cloud_upload,
                        color: synced ? Colors.teal : Colors.grey,
                      ),
                      title: Text('${r.systolic} / ${r.diastolic} mmHg'),
                      subtitle: Text(r.date),
                      trailing: synced
                          ? null
                          : const Text(
                              'pending',
                              style: TextStyle(color: Colors.grey, fontSize: 12),
                            ),
                    );
                  },
                );
              },
            ),
          ),
        ],
      ),
    );
  }
}
